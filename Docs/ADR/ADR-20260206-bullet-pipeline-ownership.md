# ADR-20260206-bullet-pipeline-ownership-and-update-order
> 대량 탄막 처리를 위해 풀/CellMap 소유권과 프레임 내 업데이트 순서를 4단 파이프라인으로 고정한다

## 상태
- 반영됨
- 다음에서 일부 대체됨: [ADR: 비활성 탄환의 불필요한 시뮬레이션을 제거](ADR-20260210-bullet-active-filtering-and-despawn-request.md)

## 배경
- 대량 Bullet(수만~10만+)을 DOTS로 처리하는 과정에서 다음 문제가 관찰되었다.
  - **소유권 불명확**: FreeList(풀) 및 SpatialHash(CellMap) 같은 Native 컨테이너가 ECS 의존성 추적 밖에 존재하여, 여러 시스템이 접근하면 JobHandle 누락/경쟁 조건이 발생하기 쉬움
  - **업데이트 순서 불명확**: 이동(LocalTransform RW), 제거 판정(LocalTransform RO), 디스폰 처리(활성/렌더 토글)가 섞이면 타입 충돌 및 "Complete 강제"로 성능/구조가 악화됨
  - **구조 변경 비용**: 대량 엔티티에서 Add/RemoveComponent 기반 구조 변경은 비용이 크므로 enable/disable 기반 상태 전환이 필요
  - **확장성 요구**: Vacuum 외에도 Bomb/Sweep 등 제거 행동 확장 가능성이 있어 "요청 생성"과 "실행(디스폰/풀 반납)" 책임 분리가 필요

## 결정
### 1) 업데이트 순서(파이프라인) 4단 고정
프레임 내 Bullet 처리를 다음 Group 순서로 고정한다.

```Plain Text
ExecutionBegin → Simulation → Request → ExecutionEnd
```

- ExecutionBegin: 스폰 실행(풀 Dequeue)
- Simulation: 이동/수명 갱신 + CellMap Build(Writer)
- Request: 제거 판단(조회/요청 생성)
- ExecutionEnd: 디스폰 실행(활성/렌더 off) + 풀 Enqueue

> 근거: 동프레임 즉시 반영(스폰/디스폰)과 타입 충돌 방지를 동시에 만족시키기 위해.

### 2) 소유권(Ownership) 명문화
- Pool/FreeList
  - 초기화/소유: Pool Owner 영역 단일 소유
  - Dequeue(스폰): ExecutionBegin만
  - Enqueue(디스폰 반납): ExecutionEnd만
- SpatialHash/CellMap
  - Writer: Simulation 단일 소유
  - Reader: Request 단계에서 ReadOnly 조회만 허용
- Render 토글(ON/OFF)
  - Spawn/Despawn/Bootstrap(Owner) 단일 책임
  - Simulation/Request 단계는 렌더 상태를 직접 변경하지 않고, 필요 시 “요청 플래그(enable)”만 남긴다

> 근거: 컨테이너 경쟁과 책임 분산을 구조적으로 차단하여 대량 병렬 처리에서 안정성을 확보하기 위함.

### 3) 구조 변경 최소화: enableable 기반 상태 전환
- 대량 엔티티에서 구조 변경을 피하기 위해 상태 전환은 기본적으로 enable/disable로 한다.
- Bullet 운용 기본 플래그:
  - `BulletActiveTag : IEnableableComponent` (시뮬레이션 대상 여부)
  - `BulletDespawnRequestTag : IEnableableComponent` (제거 행동 시스템들이 남기는 “디스폰 요청/소비 플래그”)

요청/소비 규칙(consume pattern):
- enable: 요청 생성 (Simulation의 수명 만료, Request의 제거 행동)
- disable: 실행 소비 (ExecutionEnd에서 디스폰 확정 후)

enableable 쿼리 규칙:
- `IJobEntity` 파라미터에 enableable을 포함하면 기본적으로 “enabled만” 잡힐 수 있으므로,
  “항상 실행되어야 하는 Job”에서 enableable을 포함할 때는 `EntityQueryOptions.IgnoreComponentEnabledState`(또는 WithOptions)를 명시한다.
- 병렬 Job에서의 플래그 토글은 기본적으로 `EnabledRefRW<T>`(자기 엔티티)로 처리한다.
- 병렬 Job에서 “현재 엔티티 외 다른 엔티티” enable/disable(write)가 필요하면 ECB.ParallelWriter(권장) 또는 Owner 단일 단계로 이동한다.

> 근거: enableable 의미론(“enabled-only”)과 병렬 NativeContainer 제약을 명시적으로 다루어, Job 미실행/타입 충돌/레이스를 방지하기 위함.

### 4) Shared Native 컨테이너 접근 순서 강제: Fence(JobHandle) 규칙
- FreeList와 CellMap은 ECS 의존성 추적 밖 컨테이너이므로, 접근 순서(시퀀싱)를 Fence로 강제한다.
- 기본 규칙:
  - `deps = Combine(state.Dependency, <Fence>)`
  - 스케줄 후 `<Fence> = deps` 형태로 갱신
- FreeListFence: FreeList 접근(Dequeue/Enqueue) 구간 시퀀싱
- CellMapFence: CellMap Clear/Build/Query 구간 시퀀싱

> 근거: “문서상 그룹 순서”만으로는 Native 컨테이너 경쟁을 완전히 방지할 수 없으므로, Fence를 운영 규칙으로 고정한다.

## 구현 메모
- Bullet 엔티티는 최소한 다음 상태를 가진다:
  - Active(enable)일 때만 Simulation에서 이동/수명 갱신 대상
  - DespawnRequest(enable)일 때 ExecutionEnd가 디스폰/반납 실행
- Simulation 단계:
  - 이동(LocalTransform RW) 및 수명 감소
  - 수명 만료 시 `BulletDespawnRequestTag` enable(요청 생성)
  - CellMap Clear/Build는 반드시 Simulation이 수행(Writer 단일)
- Request 단계:
  - CellMap을 ReadOnly로 조회하여 후보를 좁힌 뒤 제거 요청만 생성
  - 직접 비활성화/풀 반납 금지(ExecutionEnd 단일 책임)
- ExecutionEnd 단계:
  - `BulletDespawnRequestTag` enabled 엔티티만 처리
  - Active/Render off → 요청 플래그 disable(consume) → FreeList Enqueue

렌더 토글:
- 다중 렌더 파츠를 고려하여 "루트에 Renderer가 있다"를 가정하지 않는다.
- Bake 시 루트에 렌더 파츠 버퍼를 채워두고(예: `DynamicBuffer<...>`),
  Owner 시스템에서 버퍼를 순회하여 `MaterialMeshInfo` enable/disable 한다.
- TODO: 현재 코드가 단일 `MaterialMeshInfo` 토글로 가정하고 있어, 멀티 파츠 토글로 이관 필요.

## 대안
- (A) 단일 시스템(monolithic)에서 스폰/이동/해시/제거/반납을 모두 처리
  - 장점: 구현 단순
  - 단점: 타입 충돌/Complete 강제/책임 혼재로 확장성 저하
- (B) 구조 변경(Add/RemoveComponent) 기반으로 활성/비활성/요청을 표현
  - 장점: 쿼리 의미론이 직관적
  - 단점: 대량 엔티티에서 구조 변경 비용이 커서 부적합
- (C) ECB 중심(요청을 모두 ECB로 기록하고 Playback으로 커밋)
  - 장점: 복합 커밋에 강함, 커밋 지점 중앙화 가능
  - 단점: Playback 위치 설계가 필요하며 동프레임 즉시 반영 유지가 어려워질 수 있음
- (D) 일반 컴포넌트 플래그(`byte Requested`)로 전수 스캔
  - 장점: enableable 쿼리 함정 회피
  - 단점: 요청 비율이 낮은 구간에서도 전체 스캔 비용(메모리 스트리밍)이 커질 수 있음

## 결과
- 소유권(풀/CellMap/렌더 토글)과 업데이트 순서가 문서/코드 기준으로 고정되어,
  타입 충돌 및 경쟁 조건 발생 가능성이 감소한다.
- 제거 행동 확장 시 Request 시스템 추가만으로 확장 가능하며,
  실제 디스폰/반납은 ExecutionEnd 단일 책임으로 유지된다.
- 단, Fence(JobHandle) 결합 규칙 및 enableable 의미론을 위반하면 문제(Job 미실행/레이스)가 재발할 수 있으므로 규율 준수가 필수다.

## 후속
- 스모크 테스트:
  - 동프레임 스폰 → 이동/해시 → 제거 요청 → 디스폰/반납까지 정상 동작 확인
- Entities Profiler:
  - 활성 Bullet 수(ActiveTag enabled)와 Simulation 대상 수가 일치하는지 확인
  - 극단 케이스(예: 10만 탄 동프레임 제거)에서 프레임 타임 관찰
