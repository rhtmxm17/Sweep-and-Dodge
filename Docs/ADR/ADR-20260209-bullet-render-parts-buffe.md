# ADR-20260209-bullet-render-parts-buffer
> 프리팹 하위에 다중 렌더 엔티티가 존재하는 탄환을 위해, 베이킹 시 렌더 파츠 엔티티 목록을 루트 버퍼에 저장하고 런타임 토글은 버퍼 기반으로 수행한다.

## 상태
- 반영됨

## 배경
- 이는 기존 파이프라인 ADR([20260206](ADR-20260206-bullet-pipeline-ownership.md))에서 TODO로 명시된, 다중 렌더 파츠를 고려하여 단일 `MaterialMeshInfo` 토글 대신 렌더 파츠 버퍼를 적용해 Bake 시 루트에 버퍼를 채우고 Owner가 순회 토글하는 설계 및 구현이다.
- Bullet 프리팹은 "루트(로직/풀링/이동) + 자식(외형)" 형태를 전제로 하며, 외형은 1개가 아니라 **여러 개의 렌더 파츠(엔티티)** 로 구성될 수 있다.
- 프리팹에서 렌더는 루트가 아닌 자식에 붙는 경우가 흔하며, 이때 엔티티라면 루트 엔티티만 렌더 on/off 해서는 외형이 함께 꺼지지 않는다.
- 풀링 정책상 Bullet의 스폰/디스폰은 구조 변경(Add/Remove) 대신 enable/disable 기반 상태 전환을 기본으로 하며, 렌더 on/off 역시 Owner 단계(Spawn/Despawn/Bootstrap)에서 일관되게 제어되어야 한다.

## 결정
- Bullet 루트 엔티티에 **렌더 파츠 엔티티 목록 버퍼**를 둔다.
  - `DynamicBuffer<EntityRenderElementBuffer>` (요소는 Render Entity `Entity`만 저장)
  - `InternalBufferCapacity`는 기본 4로 둔다(평균 파츠 2 가정, 대부분 힙 할당 회피).
- 베이킹(BulletAuthoring/Baker)에서 프리팹 계층을 스캔하여 렌더 파츠 엔티티를 버퍼에 채운다.
  - 대상 렌더러: `MeshRenderer`, `SkinnedMeshRenderer` (자식 포함, 비활성 포함)
  - 버퍼에는 **외형 렌더 목적 엔티티만** 담는다(로직/콜라이더 등 비-렌더 엔티티는 제외).
- 런타임 렌더 on/off는 다음 원칙을 따른다.
  - 렌더 토글은 **Owner 시스템(Spawn/Despawn/Bootstrap) 단일 책임**으로 수행한다.
  - 루트 엔티티에 `MaterialMeshInfo`가 있다고 가정하지 않는다.
  - 버퍼를 순회하여 각 렌더 파츠 엔티티의 `MaterialMeshInfo` enable/disable을 수행한다.
  - 병렬 디스폰(ExecutionEnd)에서 “현재 엔티티 외 엔티티(렌더 파츠)”를 토글해야 하므로,
    - 교차 엔티티 write는 직접 Lookup 쓰기 대신 **ECB.ParallelWriter**로 기록한다.

## 구현 메모
- Baker(베이킹) 구현 가이드
  - 루트: `TransformUsageFlags.Dynamic` (시뮬레이션/풀링 기준)
  - 렌더 파츠 엔티티 획득:
    - `GetComponentsInChildren<MeshRenderer>(includeInactive: true)`
    - `GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)`
    - 각 renderer에 대해 `GetEntity(renderer, TransformUsageFlags.Renderable)` 호출
  - `EntityRenderElementBuffer`에 `Entity`를 추가(중복 방지 권장).
- 런타임 토글 구현 가이드
  - Spawn(단일 Job/단일 스레드 단계)에서는 `ComponentLookup<MaterialMeshInfo>.SetComponentEnabled(renderEntity, true/false)` 방식이 가능하다.
  - Despawn(병렬)에서는 렌더 파츠 토글을 **ECB.ParallelWriter**로 기록한다.
    - ECB playback 의존성은 `EndSimulationEntityCommandBufferSystem.Singleton.AddJobHandleForProducer(handle)`로 연결한다.
  - 안전장치:
    - 버퍼에 포함된 엔티티가 예상과 다르게 렌더 컴포넌트를 갖지 않는 경우를 대비해 `HasComponent<MaterialMeshInfo>` 가드를 둘 수 있다.
    - 다만 “버퍼에는 MMI 보유 렌더 엔티티만 들어간다”는 계약을 Baker 단계에서 보장할 수 있다면 가드는 제거 가능(성능/단순화).
- 성능/메모리 고려
  - `InternalBufferCapacity(4)`는 엔티티당 고정 오버헤드를 증가시키지만,
    - 평균 파츠 수가 1~3 범위라면 힙 할당을 대부분 회피하여 전체 프레임 안정성에 유리하다.
  - 극단 케이스(예: 10만 탄 동프레임 디스폰)에서 ECB 커맨드 수가 급증할 수 있으므로, 실제 측정(Entities Profiler/Timeline)로 판단한다.

## 대안
- (A) "루트가 항상 렌더를 가진다" 제약(단일 `MaterialMeshInfo` 토글)
  - 장점: 구현 최단/단순
  - 단점: 프리팹 구조 제약이 커지고(외형 자식 분리 불가), 멀티 파츠 표현이 어려움
- (B) 렌더 파츠 최대 개수를 고정(예: 4개)하고 고정 필드로 보관
  - 장점: DynamicBuffer 비용/복잡도 감소, 스폰/디스폰 루프가 고정 길이
  - 단점: 프리팹 확장성 저하(파츠 수가 변하면 다시 규칙/코드 변경)
- (C) `LinkedEntityGroup` 순회로 렌더 파츠를 찾는다
  - 장점: 별도 버퍼 설계가 필요 없음(이미 존재하는 그룹 활용 가능)
  - 단점: 렌더 외 엔티티도 포함될 수 있어 필터링이 필요하고,
    병렬 디스폰에서 교차 엔티티 토글 문제가 동일하게 남음(ECB/가드 필요)
- (D) 디스폰/스폰 시 렌더 파츠를 런타임에서 “발견”한다
  - 장점: 베이킹 부담 감소
  - 단점: DOTS 런타임에서 프리팹 계층(GameObject)을 신뢰할 수 없고, 반복 탐색 비용이 커서 부적합

## 결과
- 루트가 렌더를 갖지 않는 구조에서도, Bullet의 렌더 on/off를 **일관되게** 제어할 수 있다.
- "프리팹 하위 렌더 파츠 수가 변할 수 있다"는 요구를 베이킹 단계에서 흡수하여 런타임 복잡도를 낮춘다.
- 병렬 디스폰에서 교차 엔티티 토글이 필요해 ECB 경로가 추가되며, 극단 케이스에서는 커맨드 수 스파이크가 발생할 수 있다(측정 필요).

## 후속
- 스모크 테스트
  - 멀티 파츠 Bullet 프리팹으로 스폰/디스폰 반복 시 외형이 누락 없이 on/off 되는지 확인
- 성능 측정
  - 일반 케이스(초당 디스폰 2.5만, 평균 파츠 2)와 극단 케이스(10만 동프레임 디스폰)에서
    ECB 커맨드 비용이 허용 가능한지 Entities Profiler/Timeline로 확인
- 계약 강화(선택)
  - Baker 단계에서 “버퍼에는 `MaterialMeshInfo`를 가진 엔티티만” 넣도록 보장해,
    런타임 `HasComponent` 가드를 제거할지 결정
