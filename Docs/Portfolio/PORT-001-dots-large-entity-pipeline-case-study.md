# DOTS Large-Entity Pipeline Case Study

> 대량 엔티티의 판정과 실행을 분리하고, 소유권과 Job dependency를 명시적으로 연결한 기술 사례

## Metadata
- doc_id: `PORT-001`
- type: `Portfolio`
- status: `draft`
- last_updated: `2026-08-29`
- related_docs:
  - [Bullet pipeline ownership](../ADR/ADR-20260206-01-bullet-pipeline-ownership.md)
  - [Fixed-tick pipeline root](../ADR/ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md)
  - [Same-entity lifecycle request query](../ADR/ADR-20260819-01-bullet-self-lifecycle-request-with-present.md)
  - [FreeByKey iterator dequeue](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)
  - [Stage 2 Standalone Profiling Evidence](Evidence/Stage2-Profiling/README.md)
  - [Demo Build and Validation Guide](PORT-003-validation-report.md)

## 1. 기술 선택 배경

이 프로젝트는 완성된 GameObject 게임을 사후 최적화하기 위해 DOTS를 도입한 사례가 아니다. 계획 단계부터 Unity ECS/DOTS를 학습하고 실제 게임플레이 문제에 적용하는 것이 목표였다.

학습 과제로는 각자 이동·상태 갱신·상호작용 연산이 필요한 대량 개체를 선택했다. 회피해야 하는 위험 탄환과 쓸어 모아 수거하는 Dust가 같은 공간에 존재하도록 구성해, 대량 데이터 처리를 고립된 stress scene이 아니라 회피·청소·수집 플레이 안에서 다루고자 했다.

GameObject 방식으로 같은 문제를 해결할 수 없다고 주장하지 않는다. 직접 비교 측정은 수행하지 않았다. 이 프로젝트에서 DOTS를 선택한 이유는 동일한 컴포넌트 데이터를 일괄 처리하고 Job으로 스케줄링하는 방식이 설정한 문제와 학습 목표에 적합하다고 판단했기 때문이다.

## 2. 해결하려 한 문제

개체 수와 상호작용 종류가 늘어나면서 단순한 spawn/update/despawn 루프만으로는 다음 책임을 안정적으로 나누기 어려웠다.

- Pool에서 실제 Entity를 대여하고 반환하는 주체
- 이동·수명·공간 인덱스를 갱신하는 주체
- 플레이어 피격, 청소, 장애물, lifetime 같은 복수 판정의 우선순위
- 렌더 상태와 active 상태를 전환하는 주체
- ECS dependency 추적 밖의 FreeList와 CellMap 접근 순서
- 빈번한 상태 전환에서 발생할 수 있는 structural change

핵심 과제는 단순히 많은 Entity를 화면에 표시하는 것이 아니라, 동일 Tick에 여러 사건이 발생해도 최종 결과와 데이터 writer가 예측 가능하도록 만드는 것이었다.

## 3. 4단계 fixed-tick pipeline

대량 엔티티 처리는 다음 순서로 구성한다.

```text
ExecutionBegin → Simulation → Request → ExecutionEnd
```

| Stage | Responsibility |
|---|---|
| `ExecutionBegin` | spawn request 소비, Pool/FreeList dequeue, 활성 상태 초기화 |
| `Simulation` | 이동·수명 갱신, motion 완료와 block 판정, CellMap build |
| `Request` | 플레이어 피격·청소 등 외부 상호작용 판단, lifecycle request 생성 |
| `ExecutionEnd` | lifecycle reaction, 비활성화, render off, Pool/FreeList enqueue |

시스템 그룹 순서는 각 시스템이 Job을 스케줄하는 호출 순서를 정한다. 비동기로 실행되는 Job의 완료 순서까지 자동으로 보장하는 것은 아니다. ECS 밖 Native container에 접근하는 시스템은 별도의 fence를 dependency에 결합한다.

## 4. 판정과 실행의 분리

Request와 Simulation의 판정 시스템은 Entity를 즉시 비활성화하거나 풀에 반환하지 않는다. 대신 enableable request tag와 lifecycle payload에 제거 원인, priority, 관련 Entity, 발생 Tick, 접촉 위치·방향을 기록한다.

같은 Tick에 하나의 Entity가 여러 사건과 접촉할 수 있으므로, lifecycle request는 기존 요청의 enabled 상태와 priority를 함께 확인한다. 현재 우선순위는 다음과 같다.

```text
PlayerHit
> VacuumCollected / CarryFullRemoved
> MotionCompleted
> StageBlocked
> LifetimeExpired
```

더 높은 priority의 원인은 기존 요청을 승격하고, 같거나 낮은 원인은 먼저 기록된 요청을 유지한다. 실제 비활성화와 FreeList 반환은 `ExecutionEnd`의 owner만 수행한다.

이 분리로 판정 시스템이 실행 책임까지 가져가는 것을 막고, 시스템 실행 순서에 따라 후속 결과가 달라질 가능성을 줄였다.

## 5. 소유권과 공유 컨테이너 dependency

### Pool/FreeList와 render ownership

Pool/FreeList의 실제 dequeue는 `ExecutionBegin`의 spawn owner, enqueue는 `ExecutionEnd`의 despawn owner가 담당한다. Simulation과 Request는 풀을 직접 변경하지 않는다.

렌더 상태도 같은 실행 경계에서 전환한다. 루트 Entity 하나에 renderer가 있다고 가정하지 않고, bake 단계에서 수집한 render element buffer를 owner가 순회해 여러 렌더 파츠를 함께 켜고 끈다.

### CellMap writer

CellMap은 활성 Entity를 공간 셀에 분류해 상호작용 후보를 좁힌다. `Simulation`이 clear/build writer이고, `Request` 시스템은 관련 셀을 read-only로 조회한다. 모든 활성 Entity를 각 상호작용 시스템이 다시 순회하지 않도록 공간 후보 조회를 공유한다.

### Fence

FreeList와 CellMap은 `SharedStatic`에 보관된 ECS 외부 Native container이므로 component dependency만으로 접근 순서가 연결되지 않는다. 각 owner는 이전 관련 `JobHandle`을 fence에서 받아 현재 dependency와 결합하고, 예약한 작업을 다음 단계가 사용할 fence로 다시 게시한다.

```text
CellMap: 이전 Request reader → Simulation clear/build → 현재 Request reader
Pool:    ExecutionEnd enqueue → 다음 ExecutionBegin dequeue
```

Fence는 lock이나 접근 권한 토큰이 아니라, 공유 영역에 대한 앞 작업의 완료를 다음 작업 dependency로 전달하는 handle chain이다.

## 6. Enableable component와 query 의미

활성 상태와 lifecycle request는 `IEnableableComponent`로 표현한다. 컴포넌트를 반복해서 추가·제거하지 않고 enabled 상태를 전환해 대량 Entity의 빈번한 archetype 변경을 피한다.

요청 producer는 disabled 요청을 처음 활성화하는 경우뿐 아니라, 이미 enabled인 낮은 priority 요청을 승격하는 경우도 처리해야 한다. 현재 Entity의 request tag 양쪽 상태를 다루는 Simulation Job은 다음 계약을 사용한다.

- `[WithPresent(typeof(BulletDespawnRequestTag))]`
- `EnabledRefRW<BulletDespawnRequestTag>`
- 같은 `Execute`의 lifecycle payload `ref` 접근

`IgnoreComponentEnabledState`를 사용하면 활성 Entity 필터까지 함께 무시할 수 있으므로 request tag 하나의 양쪽 상태를 처리하는 용도로 사용하지 않는다. 다른 Entity를 수정해야 하는 producer는 `ComponentLookup`, ECB 또는 후속 owner 단계로 분리한다.

이 세부 규칙은 [same-entity lifecycle request ADR](../ADR/ADR-20260819-01-bullet-self-lifecycle-request-with-present.md)에 정리되어 있다.

## 7. 측정으로 단순화한 spawn 경로

Stage 2에서 Dust를 무청소 상태로 누적해 약 2.4만 active entity plateau를 만들었을 때, Editor Profiler에서 spawn 실행 시스템이 지배적인 비용으로 나타났다.

기존 `FreeByKey` 대여 경로는 첫 Entity와 iterator를 얻은 뒤에도 `Remove(key, entity)`를 사용했다. 같은 TypeKey에 많은 Entity가 들어 있는 경우 동일 key chain을 다시 순회하는 비용이 커졌다. 이를 이미 얻은 iterator를 직접 제거하는 `Remove(iterator)` 방식으로 교정했다.

| Version | Frame median | Frame p95 | Spawn median |
|---|---:|---:|---:|
| 기존 제거 API + 병렬 초기화 | 38.358ms | 42.267ms | 13.391ms |
| iterator 제거 + 직렬 초기화 | 27.441ms | 31.448ms | 2.848ms |

최종 경로는 기존 비교안보다 Frame median 28.46%, Spawn median 78.73% 낮았다. 별도로 시도한 상태 초기화 Job은 iterator 교정 이후 직렬 초기화보다 작은 추가 이득만 보였고, command buffer와 writable lookup 복잡도를 유지할 근거가 부족했다. 따라서 dequeue와 상태 초기화는 Pool owner의 직렬 경로로 유지했다.

이 값은 Windows Editor와 Profiler가 포함된 동일 조건 비교 결과다. standalone FPS나 모든 상황의 성능을 의미하지 않는다. 상세 조건과 대안은 [iterator dequeue ADR](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)을 따른다.

## 8. 실행 환경의 보조 근거

최신 게임플레이 비주얼을 포함한 Windows standalone Development Build에서 Stage 2 무입력·무청소 plateau를 같은 조건으로 600 frame씩 3회 기록했다.

- Active Total 평균: `24,148.3`
- Active Total 범위: `24,077–24,236`
- Frame interval median/p95/max: `7.291/9.249/12.872ms`
- `16.67ms` 초과 interval: `0/1,797`

이 수치는 명시한 테스트 장비와 통제 시나리오의 Development Build 결과다. 일반 플레이의 상시 밀도, ECS Tick 하나의 비용, 최종 Release Build, 모든 하드웨어의 60fps 또는 GameObject 대비 우위를 의미하지 않는다.

측정 조건·전체 표·Profiler 이미지는 [Stage 2 Standalone Profiling Evidence](Evidence/Stage2-Profiling/README.md)에서 확인할 수 있다.

## 9. Trade-offs와 현재 한계

- 명시적인 단계와 owner는 단순 샘플보다 구조가 복잡하고, DOTS와 Job dependency에 익숙하지 않은 독자에게 추가 설명이 필요하다.
- CellMap과 FreeList fence는 ECS dependency 추적 밖의 공유 상태를 프로젝트가 직접 관리해야 한다.
- fixed-tick pipeline은 현재 render frame과 완전히 독립된 simulation loop로 운영되지 않는다.
- 자동 테스트와 PlayMode smoke는 일부 ownership·update order·runtime behavior 회귀를 보조하지만, 플레이 감각·시청각 품질·모든 성능 상황을 보장하지 않는다.
- 최신 측정은 Development Build의 통제 시나리오이며 최종 공개 후보 Release Build 측정은 아니다.

이 사례의 핵심은 DOTS가 구조를 자동으로 해결했다는 주장이 아니다. 대량 데이터 흐름에 맞춰 writer, 실행 단계, dependency와 상태 전환 규칙을 명시하고, 측정 결과에 따라 구현 복잡도를 조정한 경험에 있다.
