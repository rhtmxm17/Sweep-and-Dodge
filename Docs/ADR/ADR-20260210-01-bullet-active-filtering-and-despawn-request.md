# ADR-20260210-01-bullet-active-filtering-and-despawn-request
> 비활성 탄환의 불필요한 시뮬레이션을 제거하고 디스폰 요청/실행 책임을 분리

## 상태
- 대체됨
- same-entity lifecycle 요청 접근 결정은 [ADR-20260819-01-bullet-self-lifecycle-request-with-present.md](ADR-20260819-01-bullet-self-lifecycle-request-with-present.md)로 대체됐다.
- 활성 `BulletActiveTag`만 시뮬레이션하고 디스폰 실행 책임을 분리한다는 결정은 유지된다.

## 배경
- `BulletSimulationSystem`의 `BulletMoveAndLifetimeJob`에서 `EntityQueryOptions.IgnoreComponentEnabledState` 사용으로 인해 `BulletActiveTag`가 비활성이어도 이동/수명 갱신이 수행될 수 있다.
- 비활성 탄환까지 업데이트되면 성능 낭비와 디스폰/풀링 상태 불일치가 발생할 수 있다.
- 아키텍처 원칙상 Simulation 단계는 활성 탄환만 처리하고, 디스폰은 ExecutionEnd 단일 책임으로 처리해야 한다.

## 결정
- `BulletMoveAndLifetimeJob`은 활성 `BulletActiveTag` 엔티티만 처리한다.
- 수명 종료 시 `BulletDespawnRequestTag`를 enable하여 요청만 남기고, 실제 디스폰은 `BulletDespawnExecutionSystem`에서 단일 책임으로 수행한다.
- enableable write를 위해 `ComponentLookup<BulletDespawnRequestTag>` 사용 시, 병렬 Job에서 동일 엔티티 접근만 허용한다는 주의 주석을 추가한다.

## 구현 메모
- `ComponentLookup<BulletDespawnRequestTag>`를 사용해 수명 종료 시 요청 태그를 enable한다.
- `EnabledRefRW<BulletDespawnRequestTag>`는 enableable 쿼리 특성상 disabled 상태를 명시적으로 enable하기 어렵고, 활성 필터를 깨뜨릴 수 있어 사용하지 않는다.
- `[NativeDisableParallelForRestriction]`은 동일 엔티티에 대한 enable 토글에 한정해 안전하다.
- 교차 엔티티 write가 섞이면 레이스 위험이 있으므로 금지한다. 필요 시 `ECB.ParallelWriter` 또는 Owner 단일 스레드 단계로 이동한다.

## 대안
- `IgnoreComponentEnabledState` 유지
  - 장점: disabled 상태에서도 요청 토글 가능
  - 단점: 비활성 탄환까지 시뮬레이션 수행, 성능/상태 일관성 리스크
- `EnabledRefRW<BulletDespawnRequestTag>` 유지
  - 장점: 간결한 코드
  - 단점: enableable 쿼리 특성상 disabled 요청 태그에 대한 명시적 enable이 어렵고, 활성 필터를 깨뜨릴 수 있음

## 결과
- 활성 탄환만 갱신되어 성능 및 상태 일관성 개선.
- 디스폰 요청/실행 책임 분리가 명확해짐.
- 병렬 Job에서 Lookup 쓰기 사용에 대한 주의가 필요하며, 교차 엔티티 write는 금지된다.

## 후속
- Play Mode 스모크 테스트로 활성/비활성 탄환 갱신 여부 확인.
- Entities Profiler로 Simulation 단계의 대상 수가 기대대로 감소하는지 확인.


