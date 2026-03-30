# Hazard Bullet Extension Contract

> 다양한 Hazard 이동/반응 확장을 위해 `TypeKey + CaptureRule` 단일 축을 `Movement + Reaction + LifecycleReason` 조합으로 확장하는 초안

## Metadata
- doc_id: `TD-027`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-03-30`
- related_docs:
  - [GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [GD-007-data-driven-bullet-pattern-definition.md](../GameDesign/GD-007-data-driven-bullet-pattern-definition.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-007-common-combat-event-channel.md](./TD-007-common-combat-event-channel.md)
  - [TD-012-player-cleanup-action-runtime-contract.md](./TD-012-player-cleanup-action-runtime-contract.md)
  - [TD-018-hazardstack-runtime-contract.md](./TD-018-hazardstack-runtime-contract.md)

> 본 문서는 구현 전 설계 초안이다. 되돌리기 비용이 큰 소유권/순서 결정이 확정되면 ADR로 승격한다.

## 1. 문제 정의
- 현재 Hazard/Bullet 런타임은 사실상 아래 축만 강하게 모델링한다.
  - `TypeKey`: 풀/시각 구분
  - `CaptureRule`: 수거/위험 판정 구분
  - `Speed`, `Lifetime`, `Velocity`: 직선 이동 파라미터
- 이 구조는 기존 `Trash/Hazard` 2종 처리에는 충분하지만, 아래 사례를 자연스럽게 담기 어렵다.
  - 이동 중 경로에 소형 Hazard를 남기는 탄
  - 점감속 후 정지 시 폭발하는 탄
  - 수거/벽충돌/정지 같은 이벤트에 반응해 별도 효과를 남기는 탄
- 그대로 확장하면 `BulletSimulationSystem`, `BulletVacuumRequestSystem`, `PlayerHazardCollisionRequestSystem`에 개별 분기와 예외 경로가 누적되어,
  - 이동 로직과 이벤트 반응이 결합되고
  - 디스폰 이유가 사라진 상태로 최종 실행만 남으며
  - 후속 확장마다 Request/Execution 경계가 흐려질 위험이 크다.

## 2. 목표/비목표
### 2.1 목표
- Hazard 확장 축을 `시각/풀`, `상호작용`, `이동`, `반응`으로 분리한다.
- 기존 파이프라인 `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`와 owner 규칙을 유지한다.
- 이동 완료, 수거, 벽충돌, 피격 같은 종료/상호작용 원인을 `LifecycleReason`으로 명시한다.
- 2차 효과(꼬리 스폰, 폭발, 보상 드롭)를 디스폰 owner와 충돌하지 않는 별도 실행 경로로 분리한다.
- 기본 직선 탄 비용을 유지하고, 특수 Hazard만 추가 비용을 내는 sparse 확장 구조를 우선한다.

### 2.2 비목표
- 이번 단계에서 모든 이동 모드를 구현하는 것
- 스크립터블오브젝트 인스펙터 최종 UX 확정
- VFX/SFX/HUD 연출의 최종 표현안 확정
- 공통 전투 이벤트 채널 범위를 즉시 확장하는 것

## 3. 설계안
### 3.1 축 분리 원칙
- Bullet 정의는 아래 4축 조합으로 해석한다.

```text
BulletDefinition = TypeKey(시각/풀) × CaptureRule(플레이어 상호작용) × MovementProfile(어떻게 움직이는가) × ReactionProfile(어떤 이벤트에 반응하는가)
```

- 각 축의 역할:
  - `TypeKey`
    - 풀 키, 프리팹, 기본 수치, 식별성
  - `CaptureRule`
    - `StandardCollectible`, `RiskTimedResolve` 같은 플레이어 상호작용 해석
  - `MovementProfile`
    - 직선, 감속, 제한적 유도, 주기적 오프셋 같은 이동 규칙
  - `ReactionProfile`
    - 수거, 벽충돌, 이동 완료, 피격, 수명 만료에 대한 후속 효과

- 핵심 원칙:
  - `CaptureRule`은 계속 "플레이어가 어떻게 만나는가"만 설명한다.
  - `MovementProfile`은 "매 프레임 위치/속도를 어떻게 갱신하는가"를 설명한다.
  - `ReactionProfile`은 "특정 lifecycle event가 발생했을 때 무엇을 enqueue하는가"를 설명한다.

### 3.2 데이터 모델 초안
- `BulletDefinitionSO` / `BulletPoolDefinitionBuffer`는 향후 아래 축을 수용할 수 있어야 한다.
  - `MovementProfileId` 또는 이에 대응하는 bake 결과
  - `ReactionProfileId` 또는 이에 대응하는 bake 결과
- 런타임은 managed polymorphism 대신 ECS 조합을 우선한다.
  - 모든 탄 공통:
    - `BulletVelocityComponent`
    - `BulletLifetimeComponent`
    - `BulletTypeKeyComponent`
    - `BulletCaptureRuleComponent`
  - 배타적 motion family component:
    - `BulletLinearMotionComponent`
    - `BulletDampedMotionComponent`
    - `BulletSpiralMotionComponent`
  - 선택 modifier / reaction component:
    - `BulletDampingComponent`
    - `BulletStopTriggerComponent`
    - `BulletTrailEmitterComponent`
    - `BulletBounceReactionComponent`
    - `BulletCollectReactionComponent`
    - `BulletLifecycleReactionComponent`

- 원칙:
  - 기본 직선 탄은 지금 구조와 거의 동일한 비용을 유지한다.
  - 특수 Hazard만 sparse component를 가진다.
  - "특수 기능을 가진 탄"을 위해 범용 giant enum + giant switch 하나로 몰아넣지 않는다.

### 3.2.1 motion family component와 modifier component 구분
- motion component는 두 종류로 나눈다.
  - `family component`
    - 배타적이다.
    - "이 탄의 주 이동 해석기는 무엇인가"를 결정한다.
    - job query 관점에서는 일종의 selector/tag 역할을 한다.
    - 다만 단순 tag가 아니라, 해당 family가 요구하는 파라미터를 함께 보관하는 data component다.
  - `modifier component`
    - 비배타적이다.
    - 주 이동 위에 얹히는 추가 규칙이나 후속 판정을 표현한다.
    - 예: trail emitter, stop trigger, bounce counter

- 예시:
  - `BulletSpiralMotionComponent`
    - family component
    - spiral 전용 중심점, 각속도, 반경 증가율 같은 파라미터를 보관
  - `BulletTrailEmitterComponent`
    - modifier component
    - 어떤 family motion 위에도 함께 붙을 수 있음

- 운영 규칙:
  - 한 bullet에는 배타적 motion family component를 1종만 둔다.
  - modifier component는 0개 이상 허용한다.
  - bake/prepare 단계에서 family 중복 부착을 금지한다.
  - Simulation query는 family component를 기준으로 분리하고, modifier는 필요한 job에서 추가 조건으로 읽는다.

### 3.3 LifecycleReason 도입
- 현재 `BulletDespawnRequestTag`만으로는 "왜 종료되었는가"가 남지 않는다.
- Hazard 확장에는 최소한 아래 수준의 종료/반응 원인이 필요하다.
  - `LifetimeExpired`
  - `StageBlocked`
  - `VacuumCollected`
  - `CarryFullRemoved`
  - `PlayerHit`
  - `MotionCompleted`
  - `ReactionConsumed`

- 초안 계약:
  - Request/Simulation 단계는 `despawn requested`만 남기지 않고, `LifecycleReason + Context`를 포함한 요청을 남긴다.
  - ExecutionEnd owner는 해당 reason/context를 읽어 반응을 실행한 뒤 최종 디스폰을 수행한다.
  - 한 탄은 한 프레임에 primary reason 1개만 확정한다.

- 초기 우선순위 제안:
  1. `PlayerHit`
  2. `VacuumCollected` / `CarryFullRemoved`
  3. `MotionCompleted`
  4. `StageBlocked`
  5. `LifetimeExpired`

- 목적:
  - 중복 반응 방지
  - 테스트 가능한 종료 규칙 확보
  - 후속 연출/보상/2차 스폰의 입력 근거 확보

### 3.3.1 T2 채택안: per-bullet single pending request
- terminal lifecycle request는 전역 buffer/event channel이 아니라 bullet entity local pending state로 표현한다.
- 채택 구조:
  - `BulletDespawnRequestTag`
    - enableable pending flag
    - 의미: "이 bullet은 이번 프레임 ExecutionEnd에서 terminal lifecycle consume 대상이다"
  - `BulletLifecycleRequestComponent`
    - primary reason, priority, 최소 공통 payload를 저장
  - `BulletLifecycleContactComponent`
    - 위치/방향 같은 소형 공통 context를 저장

- 채택 이유:
  - 현재 코드베이스는 player deposit/hit request에서 이미 `Enableable RequestTag + ContextComponent` 패턴을 사용한다.
  - terminal lifecycle은 bullet 소유 상태이며, 한 bullet당 프레임 내 primary reason 1개만 유지하면 된다.
  - 전역 buffer 방식은 dedupe, priority resolve, clear owner가 추가로 필요해 현재 범위에는 과하다.

- 비채택 방향:
  - reason별 전용 request tag를 다수 추가하는 방식
    - 이유: `LifetimeExpired`, `StageBlocked`, `MotionCompleted`, `VacuumCollected` 등이 늘수록 tag/query/reset이 과도하게 늘어난다.
  - terminal lifecycle을 dedicated global event buffer로 두는 방식
    - 이유: 같은 bullet의 중복 종료 request와 우선순위 해석이 복잡해진다.

### 3.3.2 데이터 계약
- 1차 초안 구조:

```csharp
public enum BulletLifecycleReasonId : byte
{
    None = 0,
    LifetimeExpired = 1,
    StageBlocked = 2,
    VacuumCollected = 3,
    CarryFullRemoved = 4,
    PlayerHit = 5,
    MotionCompleted = 6,
}

public struct BulletLifecycleRequestComponent : IComponentData
{
    public BulletLifecycleReasonId Reason;
    public byte Priority;
    public Entity RelatedEntity;
    public uint Frame;
}

public struct BulletLifecycleContactComponent : IComponentData
{
    public float2 PositionXZ;
    public float2 DirectionXZ;
}
```

- 필드 의미:
  - `Reason`
    - primary terminal lifecycle reason
  - `Priority`
    - 다중 producer 경합 시 승격 판단에 사용
  - `RelatedEntity`
    - source bullet, player, wall owner 등 최소한의 관련 entity
  - `Frame`
    - request가 마지막으로 확정된 logic frame
  - `PositionXZ`
    - 폭발/보상 드롭/반응 spawn 기준 위치
  - `DirectionXZ`
    - 반응 방향 또는 피격/충돌 방향

- 제약:
  - `BulletLifecycleRequestComponent`는 bullet당 1개만 가진다.
  - terminal lifecycle reason은 같은 frame에 1개만 최종 확정된다.
  - reason별 특수 payload를 모두 공통 component에 넣지 않는다.
    - 공통 필드로 부족한 데이터는 개별 reaction component의 정적 파라미터 또는 별도 secondary request buffer로 보완한다.

### 3.3.3 priority 정책
- 1차 채택 priority는 아래와 같이 고정한다.

| Reason | Priority | 의도 |
| --- | ---: | --- |
| `PlayerHit` | 100 | 플레이어와의 직접 상호작용을 최우선으로 본다 |
| `VacuumCollected` | 80 | cleanup 수거 성공은 환경/시간 기반 종료보다 우선 |
| `CarryFullRemoved` | 80 | 수거 시도 결과 제거도 cleanup 계열로 동일 우선순위 |
| `MotionCompleted` | 60 | 이동 종료 반응은 환경 충돌/수명 만료보다 우선 |
| `StageBlocked` | 40 | 벽/차단 판정은 기본 환경 종료 |
| `LifetimeExpired` | 20 | 단순 시간 만료는 최하 우선 |

- 동률 규칙:
  - 같은 priority면 기존 pending request를 유지한다.
  - 즉 동일 priority의 후행 producer는 기존 reason을 덮어쓰지 않는다.

### 3.3.4 producer helper 규칙
- producer는 `BulletDespawnRequestTag`를 직접 enable하지 않는다.
- 모든 terminal lifecycle producer는 공통 helper를 통해 request를 승격시킨다.

```csharp
TryPromoteLifecycleRequest(
    bullet,
    reason,
    priority,
    relatedEntity,
    frame,
    positionXZ,
    directionXZ,
    ...lookups)
```

- helper 기본 규칙:
  1. 현재 pending request가 없으면 새 request를 기록하고 `BulletDespawnRequestTag`를 enable한다.
  2. pending request가 있으면 `Priority`를 비교한다.
  3. 새 request의 priority가 더 높을 때만 기존 reason/context를 덮어쓴다.
  4. priority가 같거나 낮으면 기존 reason/context를 유지한다.
  5. 채택된 request만 `Frame`, `RelatedEntity`, `PositionXZ`, `DirectionXZ`를 갱신한다.

- 목적:
  - Simulation/Request producer가 같은 bullet에 동시에 접근해도 결과를 결정적으로 유지한다.
  - "이유 없이 tag만 켜는" 경로를 제거한다.

### 3.3.5 producer/consumer ownership
- producer:
  - `BulletSimulationSystem`
    - `LifetimeExpired`
    - `MotionCompleted`
  - `BulletVacuumRequestSystem`
    - `VacuumCollected`
    - `CarryFullRemoved`
  - `PlayerHazardCollisionRequestSystem`
    - `PlayerHit`
  - stage block query owner
    - `StageBlocked`

- consumer:
  - `BulletLifecycleReactionExecutionSystem`
    - `BulletExecutionEndGroup`
    - `UpdateBefore(BulletDespawnExecutionSystem)`
    - 역할: reason/context를 읽어 reaction 실행, secondary request append
  - `BulletDespawnExecutionSystem`
    - 기존 책임 유지
    - active/render off, pool enqueue, request consume

- 핵심 분리:
  - reaction execute owner와 final pool owner를 분리한다.
  - `BulletDespawnExecutionSystem`은 low-level terminal consume만 유지한다.

### 3.3.6 secondary request와의 경계
- terminal lifecycle request와 non-terminal secondary effect request는 분리한다.
- 아래는 `BulletLifecycleRequestComponent`에 넣지 않는다.
  - trail spawn append
  - explosion fragment append
  - reward dust spawn append
- 위 항목은 별도 secondary spawn request lane으로 보낸다.

- 이유:
  - terminal lifecycle은 "왜 이 bullet이 끝나는가"를 표현해야 한다.
  - secondary effect는 "끝나기 전에/끝나면서 어떤 후속 효과를 낳는가"를 표현해야 한다.
  - 둘을 같은 component에 섞으면 payload가 비대해지고 clear/consume 경계가 흐려진다.

### 3.3.7 초기화/리셋 규칙
- pool bootstrap 시:
  - `BulletDespawnRequestTag = false`
  - `BulletLifecycleRequestComponent.Reason = None`
  - `Priority = 0`
  - `RelatedEntity = Entity.Null`
  - `Frame = 0`
  - `BulletLifecycleContactComponent = default`
- spawn 시:
  - 동일 초기 상태를 다시 보장한다.
- stage prepare/reset 시:
  - bullet prefab schema에 lifecycle component가 포함되면 동일 초기화 규칙을 적용한다.

### 3.4 이동 확장 구조
- 이동 owner는 계속 `BulletSimulationGroup`에 둔다.
- 단, `LocalTransform` writer를 하나의 시스템에 모두 몰아넣는 대신, 의미가 분명한 ordered simulation sub-step으로 나누는 방안을 허용한다.
  - 예:
    - `BulletMotionStateUpdateSystem`
    - `BulletTransformApplySystem`
    - `BulletSpatialHashBuildSystem`
- 유지할 원칙:
  - CellMap writer는 Simulation 단일 owner
  - Request 단계는 read-only 조회만
  - motion 완료 판정도 Simulation 또는 Request에서 request 생성까지만 수행

- 1차 지원 대상 이동 모드 초안:
  - `Linear`
    - 기존 직선 이동 유지
  - `DampedLinear`
    - 속도를 매 프레임 감쇠
    - 임계 속도 이하가 되면 `MotionCompleted` 요청 가능
  - `HomingLite`
    - 플레이어 위치를 read-only로 읽고, 현재 진행 방향을 플레이어 쪽으로 제한된 각속도로만 굽힌다
    - 즉시 타겟팅이 아니라 "플레이어를 향해 서서히 휘는 이동"을 표현한다
    - 플레이어 입력/위치에 동적으로 영향을 받는 예시 family로 사용한다
  - `LinearWithPeriodicTrail`
    - 이동은 선형
    - 일정 시간/거리마다 trail spawn request 생성

- 후속 후보:
  - `BounceLimited`
  - `Spiral`
  - `WaveOffset`

### 3.4.0 T3 1차 구현 범위 채택안
- 1차 movement family 세트는 아래 3종으로 고정한다.
  - `Linear`
    - 기존 방식 기준선
  - `DampedLinear`
    - 이동 방식 확장 예시
  - `HomingLite`
    - 플레이어에 동적으로 영향을 받는 예시

- 채택 이유:
  - `Linear`는 현재 파이프라인과 성능 기준선을 유지하는 기준점이다.
  - `DampedLinear`는 외부 target 의존 없이 motion state만으로 이동 확장이 가능함을 보여준다.
  - `HomingLite`는 Simulation이 외부 read-only 데이터(플레이어 위치)를 받아 movement를 수정하는 대표 예시가 된다.
  - 위 3종을 확보하면 "기존 방식", "자기 상태 기반 확장", "외부 상태 기반 확장"을 모두 검증할 수 있다.

- 이번 단계에서 `Spiral`을 1차 대표 family에서 내리는 이유:
  - `Spiral`은 수식 확장 예시로는 유효하지만, 외부 상태 읽기나 타겟 영향이라는 중요한 확장 축을 보여주지 못한다.
  - 현재 Hazard 기획상 더 우선인 사례는 "플레이어를 압박하거나 유도하는 동적 휘어짐" 쪽이다.

### 3.4.0.1 T3 확정 구현 계약
- `T3`의 1차 family 구현 계약은 아래 기준으로 고정한다.
  - `Linear`
    - 기존 직선 이동 기준선
  - `DampedLinear`
    - 자기 상태 기반 이동 확장 기준선
  - `HomingLite`
    - 외부 상태(플레이어 위치) 기반 이동 확장 기준선

- 1차 구현에서는 아래를 보류한다.
  - `Spiral`, `BounceLimited`, `WaveOffset` 등 추가 family
  - family끼리의 조합 확장
  - `MotionOutput -> Apply` 구조 전환

- 목적:
  - 현재 파이프라인을 크게 흔들지 않고도
    - 기존 방식
    - 자기 상태 기반 확장
    - 외부 상태 기반 확장
    의 3축을 모두 검증한다.

### 3.4.1 Simulation 구현 기본안
- 현재 단계의 기본안은 `family job + optional modifier` 구조다.
  - `LinearMotionJob`
    - `in BulletLinearMotionComponent`
  - `DampedMotionJob`
    - `in BulletDampedMotionComponent`
  - `HomingLiteMotionJob`
    - `in BulletHomingLiteMotionComponent`
- 각 job은 자기 family query에만 매칭된다.
- modifier는 해당 family job 내부에서 함께 읽거나, 별도 sub-step job에서 처리한다.
  - 예:
    - `TrailEmitterAccumulateJob`
    - `MotionStopTriggerBuildJob`

- 이 방식의 장점:
  - 현재 코드베이스 구조에 가장 자연스럽게 얹힌다.
  - family별 수식과 state를 분리해 giant switch를 피할 수 있다.
  - query만으로 처리 대상을 구분할 수 있어 테스트가 단순하다.

- 이 방식의 전제:
  - 동일 bullet가 두 개 이상의 motion family query에 동시에 잡히지 않아야 한다.
  - `LocalTransform` writer 충돌이 없도록 family exclusivity를 bake 단계에서 보장해야 한다.

### 3.4.1.1 HomingLite family 초안
- `HomingLite`는 "플레이어를 향해 진행 방향이 휘어지는 이동"을 의미한다.
- 1차 계약:
  - 플레이어 위치는 read-only로만 읽는다.
  - bullet는 현재 진행 방향을 유지하되, 프레임당 최대 회전량 제한 안에서만 목표 방향으로 보정한다.
  - 순간 회전(snap)이나 완전 추적(full homing)은 이번 범위에 포함하지 않는다.

- component 초안:

```csharp
public struct BulletHomingLiteMotionComponent : IComponentData
{
    public float TurnRateDegPerSec;
    public float MaxAcquireDistance;
    public float MinRetargetDistance;
}
```

- 해석 규칙:
  - `TurnRateDegPerSec`
    - 1초 동안 허용되는 최대 회전량
  - `MaxAcquireDistance`
    - 플레이어가 너무 멀면 steering을 하지 않는 가드
  - `MinRetargetDistance`
    - 플레이어와 너무 가까울 때 불안정한 각도 튐을 막기 위한 하한

- runtime 처리 확정안:
  1. 현재 bullet 진행 방향을 읽는다.
  2. player까지의 `desired direction`을 계산한다.
  3. player와의 거리가 `MinRetargetDistance <= distance <= MaxAcquireDistance` 범위에 있을 때만 steering을 수행한다.
  4. steering은 `TurnRateDegPerSec * dt` 만큼의 최대 회전량 안에서만 허용한다.
  5. speed 크기는 유지하고 방향만 바꾼다.
  6. speed authoritative source는 1차에서 `BulletSpeedComponent`로 고정한다.
  7. player가 acquire 범위 밖이거나 gameplay gate 조건상 유효 target이 없으면 마지막 방향으로 직진한다.

- 목적상 비허용:
  - velocity를 플레이어 방향으로 즉시 재설정하는 snap homing
  - 거리와 무관한 상시 steering
  - target 상실 시 정지하거나 반전하는 동작

- ownership 규칙:
  - player 위치 읽기는 `Simulation`의 read-only 입력이다.
  - player entity에 write하지 않는다.
  - CellMap build 이전에 final transform이 확정되어야 한다.

- 목적:
  - "플레이어에 의해 결과가 달라지는 이동"을 최소 비용으로 검증한다.
  - 향후 더 강한 `Homing` 또는 target selection 계열로 확장할 수 있는 seam을 만든다.

### 3.4.1.2 T3 조합 규칙 확정안
- 1차 구현에서는 motion family 간 조합을 허용하지 않는다.
  - 한 bullet은 `Linear`, `DampedLinear`, `HomingLite` 중 정확히 1개의 family만 가진다.
- modifier는 제한적으로만 허용한다.
  - 허용 후보:
    - `BulletTrailEmitterComponent`
    - `BulletStopTriggerComponent`
  - 보류:
    - `HomingLite + Damping` 같은 family 성격 중첩 조합
    - family 해석을 바꾸는 추가 steering modifier

- 이유:
  - family 단위 query와 writer exclusivity를 단순하게 유지해야 1차 테스트 범위를 관리할 수 있다.
  - 조합 폭을 일찍 열면 movement contract 검증보다 상호작용 조합 회귀가 더 커진다.

### 3.4.1.3 T3 검증 관점
- `Linear`
  - 기존 직선 이동과 동일한 결과/비용 기준선인지 확인한다.
- `DampedLinear`
  - 감쇠만으로 motion state 기반 확장이 가능한지 확인한다.
  - 임계 속도 이하에서 `MotionCompleted` request가 고정적으로 발생하는지 확인한다.
- `HomingLite`
  - 방향만 부드럽게 휘고 speed 크기는 유지되는지 확인한다.
  - acquire/min distance 가드 밖에서는 직진 fallback이 유지되는지 확인한다.
  - player 위치 변화에 따라 결과가 바뀌지만 player write 없이 Simulation read-only 경계가 유지되는지 확인한다.

### 3.4.2 확장성을 더 보는 방식 메모
- 이동 family가 더 많아지고 modifier 조합도 늘어나면, direct transform write 방식만으로는 writer 충돌과 sub-step 관리가 복잡해질 수 있다.
- 그 경우 아래 2단 구조를 검토한다.

```text
Motion Family Jobs -> BulletMotionOutputComponent 계산 -> MotionApplyJob -> LocalTransform write
```

- 초안 역할:
  - family job
    - `LocalTransform`를 직접 쓰지 않고, 이번 프레임의 목표 위치/회전/파생 상태를 `BulletMotionOutputComponent`에 기록
  - `MotionApplyJob`
    - 단일 writer로 `LocalTransform`를 갱신
  - 이후 spatial hash build는 apply 이후에 수행

- 장점:
  - `LocalTransform` writer를 최종 1곳으로 모을 수 있다.
  - family별 계산과 최종 적용을 분리해 모드 수 증가에 더 잘 버틴다.
  - 향후 motion blending, debug capture, rollback 검증 지점을 만들기 쉽다.

- 단점:
  - 공통 output/state 구조가 추가된다.
  - 현재 단계에선 구조가 다소 무거울 수 있다.

- 이번 문서의 권장:
  - 1차 구현은 `family job + optional modifier`를 기본안으로 둔다.
  - motion family가 4종 이상으로 늘거나 writer 충돌/순서 문제가 반복되면 `MotionOutput -> Apply` 구조를 재평가한다.

### 3.5 반응 확장 구조
- 반응은 Request/Execution 책임을 분리한다.
  - 감지/판정:
    - Simulation 또는 Request 단계
    - reason/context request 생성만 수행
  - 실행:
    - ExecutionEnd 단계
    - secondary spawn, area effect, carry 변화, feedback append, combat event append 수행

- 반응 프로파일 초안:
  - `OnCollectedSpawnSecondary`
    - 수거 시 보상용 먼지/소형 Hazard 생성
  - `OnMotionCompletedExplode`
    - 감속 종료 시 폭발
  - `OnStageBlockedSpawnSecondary`
    - 벽충돌 시 파편 생성
  - `PeriodicTrailEmitter`
    - 이동 중 trail spawn request 생성

- 반응 실행 원칙:
  - source bullet의 active/render toggle과 pool enqueue는 계속 `BulletDespawnExecutionSystem` 단일 책임
  - secondary effect는 별도 request buffer로 전달
  - reaction이 carry/hazard stack/combat event에 영향을 주면 기존 owner 시스템과 충돌하지 않도록 기존 request 채널 또는 전용 request를 통해 합류한다

### 3.6 2차 스폰(owner 분리) 초안
- Source 기반 스폰과 Bullet 반응 기반 스폰은 owner를 분리한다.
- 이유:
  - source wave pressure와 reaction burst는 기원과 튜닝 축이 다르다.
  - source request buffer에 reaction spawn을 직접 섞으면 lane/budget 의미가 흐려진다.

- 초안 구조:
  - `SecondaryHazardSpawnRequestBuffer` 또는 동등 singleton/buffer를 별도 운용
  - producer:
    - trail emitter
    - explode reaction
    - collect reward reaction
  - consumer:
    - `BulletExecutionBeginGroup`의 별도 owner system
  - shared rule:
    - 같은 pool/fence 규칙 사용
    - frame budget 및 cap 정책은 별도 필드로 관리

- 상세 budget 공유 방식은 이번 초안에서 확정하지 않는다.
  - 최소한 source spawn backlog와 reaction spawn backlog를 구분 관측할 수 있어야 한다.

### 3.7 기존 예시 매핑
- 작은 유성 같은 조각을 뿌림
  - `MovementProfile = Linear`
  - `ReactionProfile = PeriodicTrailEmitter`
  - 결과: 이동 중 경로에 소형 Hazard spawn request 생성

- 정지시 터지는 거품
  - `MovementProfile = DampedLinear`
  - `ReactionProfile = OnMotionCompletedExplode`
  - 결과: 감속 후 임계 속도 도달 시 `MotionCompleted` -> 폭발 실행

- 수거하면 고가치 마법 먼지를 남기는 사탕
  - `CaptureRule = StandardCollectible` 또는 별도 reward collectible 해석
  - `ReactionProfile = OnCollectedSpawnSecondary`
  - 결과: 수거 시 carry/collect와 별개로 보상 드롭 spawn request 생성
  - 필요 시 `OnStageBlockedSpawnSecondary`도 같은 탄에 추가 가능

## 4. 업데이트 순서/소유권
### 4.1 파이프라인 유지
- 루트 순서는 그대로 유지한다.

```text
ExecutionBegin -> Simulation -> Request -> ExecutionEnd
```

### 4.2 소유권 초안
- `ExecutionBegin`
  - source spawn owner
  - secondary hazard spawn owner
  - 풀 dequeue / spawn state initialize

- `Simulation`
  - movement state update
  - transform apply
  - motion-complete 후보 판정
  - `CellMap`, `HazardCellMap` build

- `Request`
  - vacuum collect/carry-full remove
  - player hazard collision
  - stage block query
  - lifecycle reaction request 확정

- `ExecutionEnd`
  - player hit/deposit/risk owner
  - lifecycle reaction execute owner
  - 최종 despawn + pool enqueue owner
  - combat/ui feedback consume

### 4.2.1 T2 기준 terminal lifecycle 흐름
1. `Simulation` 또는 `Request` producer가 `TryPromoteLifecycleRequest`를 호출한다.
2. helper가 primary reason을 bullet local pending state에 확정한다.
3. `BulletDespawnRequestTag`가 enabled인 bullet은 `ExecutionEnd`에서 lifecycle consume 대상이 된다.
4. `BulletLifecycleReactionExecutionSystem`이 reason/context를 읽어 secondary effect를 실행한다.
5. `BulletDespawnExecutionSystem`이 최종 active/render/pool consume를 수행한다.

### 4.2.2 기존 시스템 치환 기준
- 기존 `BulletDespawnRequestTag` 직접 write 경로는 아래 원칙으로 치환한다.
  - Simulation 수명 만료/정지 완료:
    - `TryPromoteLifecycleRequest(... LifetimeExpired/MotionCompleted ...)`
  - vacuum collect/remove:
    - `TryPromoteLifecycleRequest(... VacuumCollected/CarryFullRemoved ...)`
  - hazard collision:
    - `TryPromoteLifecycleRequest(... PlayerHit ...)`
  - stage block:
    - `TryPromoteLifecycleRequest(... StageBlocked ...)`

- 직접 tag enable만 남기는 경로는 legacy로 간주하고 단계적으로 제거한다.

### 4.3 writer 경계
- `LocalTransform`
  - Simulation만 write
- `CellMap/HazardCellMap`
  - Simulation만 write
- `LifecycleReason/ReactionRequest`
  - Simulation/Request는 생성만
  - ExecutionEnd는 소비만
- `BulletActiveTag`, render toggle, pool enqueue
  - ExecutionEnd만 write
- `SecondaryHazardSpawnRequestBuffer`
  - ExecutionEnd append / ExecutionBegin consume

### 4.4 fence 규칙
- 기존 `PoolFence`, `CellMapFence` 규칙을 유지한다.
- secondary spawn queue가 SharedStatic/native container 기반으로 구현될 경우 별도 fence가 필요할 수 있다.
- 가능하면 secondary spawn request는 ECS buffer 기반으로 두고 SharedStatic 추가를 피하는 방향을 우선 검토한다.

## 5. 성능/리스크
### 5.1 성능 원칙
- 기본 직선 탄은 현재 비용을 유지해야 한다.
- 특수 이동/반응은 sparse query로만 추가 비용을 낸다.
- reaction 때문에 구조 변경을 남발하지 않고 enableable/request-consume 패턴을 유지한다.
- periodic trail/emitter는 반드시 frequency guard를 가진다.
  - 매 프레임 무조건 spawn 금지
  - 시간/거리 누적 기준으로 샘플링

### 5.2 주요 리스크
- `MovementProfile`을 giant switch로 구현하면 Simulation이 다시 monolithic system이 된다.
- `LifecycleReason` 없이 reaction만 늘리면 같은 프레임 다중 이벤트 우선순위가 불명확해진다.
- secondary spawn을 source spawn backlog와 섞으면 pressure/budget 진단이 어려워진다.
- 폭발/꼬리 spawn이 frame burst를 만들 수 있으므로 reaction 전용 budget/cap이 필요할 가능성이 높다.

## 6. 검증 계획
- EditMode
  - 기본 직선 탄이 기존과 동일하게 이동/디스폰하는지
  - `DampedLinear`가 임계 속도 도달 시 `MotionCompleted` reason을 남기는지
  - 같은 프레임 `MotionCompleted + PlayerHit` 충돌 시 primary reason 우선순위가 고정되는지
  - `OnCollectedSpawnSecondary`가 source despawn owner를 침범하지 않는지
  - `PeriodicTrailEmitter`가 frequency guard 없이 과도한 request를 만들지 않는지
  - secondary spawn queue가 source spawn backlog 계측과 분리되는지
- PlayMode
  - 전용 씬에서 정지 폭발/경로 trail/수거 보상 드롭 3종 스모크
  - peak active bullets와 frame 안정성 관찰
- 공통 게이트
  - 문서 단계 이후 구현 시 `compile -> console error 0 -> EditMode -> PlayMode smoke`

## 7. 작업 분해 초안
1. `TD-027` 기준으로 movement/reaction/lifecycle 용어와 축을 고정한다.
2. reason-aware lifecycle request 데이터 구조를 추가한다.
3. `DampedLinear + MotionCompleted` 1종을 먼저 구현한다.
4. `OnMotionCompletedExplode` 반응 1종을 구현한다.
5. `OnCollectedSpawnSecondary` 또는 `PeriodicTrailEmitter` 중 1종을 추가한다.
6. reaction 전용 budget/metrics가 필요하면 별도 TD 또는 ADR로 승격한다.

## 8. 오픈 이슈
- secondary spawn budget을 source spawn과 완전 분리할지, 동일 전역 예산에서 slice를 나눌지
- `StageBlocked`를 단순 despawn reason으로만 볼지, bounce/fragment의 전처리 reason으로 볼지
- 폭발이 공통 전투 이벤트 채널(`Hit/Collect/Cleanup`)에 포함되어야 하는지, 별도 채널을 둘지
- movement sub-step을 시스템 분할로 표현할지, 단일 simulation owner 내부 ordered job으로 유지할지

## 9. 변경 이력
- 2026-03-30: 초안 작성. Hazard 확장을 `Movement + Reaction + LifecycleReason` 조합으로 분리하는 방향과 owner 경계를 정리했다.
- 2026-03-30: `T2` 구체화. terminal lifecycle을 `BulletDespawnRequestTag + BulletLifecycleRequestComponent + BulletLifecycleContactComponent` 조합으로 두고, producer helper / priority / consumer owner 규칙을 추가했다.
