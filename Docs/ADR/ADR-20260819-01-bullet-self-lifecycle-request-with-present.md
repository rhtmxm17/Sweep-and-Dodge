# ADR-20260819-01-bullet-self-lifecycle-request-with-present
> 현재 탄환의 enableable lifecycle 요청을 `WithPresent + EnabledRefRW`로 갱신하는 결정

## Metadata
- doc_id: `ADR-20260819-01`
- type: `ArchitectureDecisionRecord`
- status: `accepted`
- date: `2026-08-19`
- supersedes:
  - [ADR-20260210-01-bullet-active-filtering-and-despawn-request.md](ADR-20260210-01-bullet-active-filtering-and-despawn-request.md)의 same-entity `ComponentLookup + NativeDisableParallelForRestriction` 결정
- related_docs: 없음

## 배경
- `BulletSimulationSystem`의 네 `IJobEntity`는 탄환을 직접 순회하면서 현재 탄환의 디스폰 요청 태그와 lifecycle payload만 수정한다.
- `BulletDespawnRequestTag`는 처음 요청할 때 disabled에서 enabled로 전환되지만, 이미 enabled인 낮은 priority 요청도 더 높은 원인으로 덮어쓸 수 있어야 한다.
- 기존 writable `ComponentLookup` 방식은 same-entity 접근이라는 사실을 ECS safety system에 표현하지 못해 `NativeDisableParallelForRestriction`에 의존했다.
- 모든 enableable 상태를 무시하는 `IgnoreComponentEnabledState`는 request tag뿐 아니라 `BulletActiveTag` 필터도 무시하여 비활성 탄환을 다시 시뮬레이션할 위험이 있다.

## 결정
- 현재 쿼리 엔티티만 수정하고 request tag의 enabled/disabled 양쪽을 처리해야 하는 job은 `[WithPresent(typeof(BulletDespawnRequestTag))]`와 `EnabledRefRW<BulletDespawnRequestTag>`를 사용한다.
- 일반 lifecycle payload는 같은 `Execute`의 `ref BulletLifecycleRequestComponent`와 `ref BulletLifecycleContactComponent`로 갱신한다.
- `BulletActiveTag`는 별도 `in` 파라미터의 기본 enabled 필터를 유지한다. `WithPresent`는 request tag에만 적용한다.
- 요청이 disabled면 stale payload와 무관하게 새 요청을 허용하고, enabled면 새 원인의 priority가 기존 값보다 높을 때만 payload를 교체한다. equal/lower priority는 최초 요청을 보존한다.
- block 충돌 계산 전 같은 priority 판정을 수행하여 승격할 수 없는 요청에는 grid 조회를 생략한다.
- 임의의 다른 엔티티를 수정하는 producer에는 이 패턴을 적용하지 않는다. 해당 경로는 `ComponentLookup`, ECB 또는 후속 resolver/Owner 단계에서 별도로 다룬다.

## 대안
- `WithDisabled<BulletDespawnRequestTag>`
  - disabled 요청을 처음 활성화하는 경우에는 가장 좁은 쿼리를 만들 수 있다.
  - 이미 enabled인 낮은 priority 요청을 승격할 수 없으므로 lifecycle 계약을 만족하지 못한다.
- `EntityQueryOptions.IgnoreComponentEnabledState`
  - 한 쿼리에서 enabled/disabled 상태를 모두 다룰 수 있다.
  - 쿼리의 다른 enableable 필터까지 함께 무시하므로 `BulletActiveTag`가 disabled인 풀 탄환도 이동·수명 갱신 대상이 될 수 있다.
- writable `ComponentLookup + NativeDisableParallelForRestriction`
  - 현재 엔티티와 교차 엔티티 접근을 같은 API로 처리할 수 있다.
  - same-entity write에서도 안전 제한을 수동으로 해제해야 하고, 접근 범위가 코드 시그니처에 드러나지 않아 향후 교차 엔티티 write가 섞일 위험이 있다.

## 결과
- 네 Simulation job의 same-entity lifecycle write가 ECS가 추적할 수 있는 직접 component access로 표현된다.
- 비활성 탄환 필터를 유지하면서 disabled 요청 생성과 enabled 요청 승격을 모두 지원한다.
- Entities 1.4.4에서는 이 implicit `IJobEntity` scheduling을 포함한 system `OnUpdate`를 Burst 컴파일하면 생성 코드에서 `NullReferenceException`이 발생한다. 따라서 orchestration `OnUpdate`는 managed로 두고 실제 대량 순회 job의 `[BurstCompile]`은 유지한다.
- 모든 런타임 탄환에 request tag/request/contact가 존재한다는 Baker 스키마 계약이 job query 요구조건이 된다. 해당 컴포넌트가 누락된 비정상 탄환은 Simulation query에서 제외된다.
- Vacuum, 플레이어 충돌 등 cross-entity producer의 최종 priority 및 부수효과 중재는 이 결정의 범위 밖이며 별도 설계 대상으로 남는다.
