# EmissionProfile Common Schema

## Metadata
- doc_id: `TD-033`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-07-01`
- related_docs:
  - [../TaskBoard/SESSION-20260701-01-emission-profile-redesign-board.md](../TaskBoard/SESSION-20260701-01-emission-profile-redesign-board.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-027-hazard-bullet-extension-contract.md](./TD-027-hazard-bullet-extension-contract.md)
  - [TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)

> 목적: `EmissionProfile`을 Source, HazardActor, Triggered emission이 공통으로 참조하는 탄막 데이터 단위로 재정의한다. 본 문서는 데이터 스키마와 authoring 책임 경계를 먼저 고정하며, `SourceSpawnRequestBuffer` / `DiscreteEmitRequestBuffer` / `BulletSecondarySpawnRequestBuffer` 같은 런타임 파이프라인 계층의 구체 변경은 후속 설계로 분리한다.

## 1. 문제 정의
- 현재 탄막 작성 경험은 작성 위치에 따라 데이터 구조가 갈라져 있다.
  - `WaveSpawnEntryAuthoring`은 `Payload / Emission / Sampling / PositionPattern / Aim / ShotPattern`을 직접 가진다.
  - `HazardEmitterEmissionProfileSO`는 유사한 `Bullet / EventRepeat / PositionPattern / Aim / ShotPattern` 구조를 별도로 가진다.
  - `BulletDefinitionSO`는 visual/pool 정체성뿐 아니라 `Speed`, `Lifetime`, `MovementFamily`, `DampedLinear`, `HomingLite`, lifecycle reaction까지 함께 가진다.
- 그 결과, 탄막 제작자는 "이 패턴에서 속도/수명/이동/후속 발사를 어떻게 줄 것인가"를 패턴 단위가 아니라 bullet definition 단위에서 우회 작성해야 한다.
- `MotionCompleted -> secondary spawn` 같은 패턴은 본질적으로 "이 이벤트에서 다른 탄막을 실행한다"에 가깝지만, 현재는 `BulletDefinitionSO.OnMotionCompletedExplode` 안에 별도 축으로 중복 표현된다.

## 2. 목표 / 비목표

### 2.1 목표
- `EmissionProfile`을 Source/Hazard/Triggered가 공통으로 사용하는 authoring SSOT로 정의한다.
- 탄막의 플레이 감각 데이터는 가능한 한 `EmissionProfile`에서 직접 조정한다.
  - speed
  - lifetime
  - movement tuning
  - position / aim / shot pattern
  - first-slice lifecycle trigger
- Source 전용 개념과 공통 탄막 문법을 분리한다.
  - `Sampling`, `RateField`, source density/cap은 Source wrapper 책임이다.
  - `EmissionProfile`은 주어진 execution context에서 탄막을 어떻게 펼칠지만 책임진다.
- `MotionCompleted` 발생 시 다른 `EmissionProfile`을 trigger하는 1차 스키마를 정의한다.
- `BulletDefinitionSO`의 movement/reaction 필드를 신규 데이터 작성 기준에서 deprecated로 둔다.

### 2.2 비목표
- 이번 문서에서 runtime request/channel wire shape를 확정하지 않는다.
- 이번 문서에서 기존 asset migration을 실행하지 않는다.
- 이번 문서에서 inspector graph/preview UX를 최종 확정하지 않는다.
- 이번 1차 범위에서 `CleanupRemoved`, `PlayerHit`, `LifetimeExpired`, `StageBlocked` trigger를 열지 않는다.

## 3. 채택 기준
- `EmissionProfile`은 canonical execution context를 소비한다.
- Source/Hazard/Triggered의 차이는 wrapper/context binding에서 해결한다.
- Triggered emission의 anchor/direction은 profile 내부가 아니라 trigger link가 제공한다.
- 첫 lifecycle trigger는 `MotionCompleted`만 지원한다.
- 기존 secondary spawn path는 호환 경로로 유지하되 신규 SSOT로 확장하지 않는다.

## 4. 공통 데이터 모델

### 4.1 `EmissionProfile`
```text
EmissionProfile
  = Payload
  x SpawnTuning
  x MovementTuning
  x PositionPattern
  x Aim
  x ShotPattern
  x LifecycleTriggers
```

- `Payload`
  - 어떤 bullet definition을 사용할지 결정한다.
  - `BulletDefinitionSO` 참조를 가진다.
- `SpawnTuning`
  - 이 profile에서 사용할 gameplay scalar를 결정한다.
  - 1차 후보:
    - `SpeedOverride`
    - `LifetimeOverride`
  - 후속 후보:
    - `RadiusMultiplier`
    - `ScoreValueOverride`
- `MovementTuning`
  - 이 profile에서 사용할 movement family와 파라미터를 결정한다.
  - 1차 후보:
    - `Linear`
    - `DampedLinear`
    - `HomingLite`
  - `BulletDefinitionSO`의 movement 값은 fallback/compat read path로만 유지한다.
- `PositionPattern`
  - execution context origin 기준 spawn origin을 배치한다.
  - 기존 `SinglePoint`, `LineEven`, `PointSet` 의미를 재사용한다.
- `Aim`
  - execution context forward 또는 runtime target을 기준으로 base direction을 계산한다.
  - 기존 `Fixed`, `Spiral`, `PlayerPosition`, `LineNormal` 의미를 재사용한다.
  - Triggered emission에서는 context forward를 기준으로 해석할 수 있어야 한다.
- `ShotPattern`
  - repeat 1회가 만드는 bullet slot 구조를 결정한다.
  - 기존 `Single`, `NWay`, `Radial` 의미를 재사용한다.
- `LifecycleTriggers`
  - 이 profile로 spawn된 bullet가 특정 lifecycle event에 도달했을 때 실행할 후속 emission link를 가진다.
  - 1차는 `MotionCompleted`만 허용한다.

### 4.2 `EmissionExecutionContext`
`EmissionProfile`은 직접 source grid, actor transform, lifecycle contact를 알지 않는다. 대신 wrapper가 아래 canonical context를 제공한다.

```text
EmissionExecutionContext
- OriginPosition
- ForwardDirection
- SourceEntity
- ProducerEntity
- CauserEntity
- Frame
- SequenceSeed
```

- `OriginPosition`
  - profile의 position pattern 기준점이다.
- `ForwardDirection`
  - fixed/context-relative aim의 기준 방향이다.
- `SourceEntity`
  - active count/source attribution을 계승해야 하는 경우 사용한다.
- `ProducerEntity`
  - Source/HazardActor/Triggered producer를 추적하기 위한 provenance다.
- `CauserEntity`
  - triggered emission에서 원인 bullet를 추적한다.
- `Frame`
  - deterministic delay, age, debug trace에 사용한다.
- `SequenceSeed`
  - random/sequence 기반 pattern 안정화를 위한 seed다.

## 5. Wrapper 책임

### 5.1 Source wrapper
Source wrapper는 Source만이 아는 sampling/density/timeline을 책임진다.

```text
SourceWaveDirective
- EmissionProfile
- SourceEmission
  - RateField
  - Poisson
  - EventBurst
- Sampling
  - Anchor
  - AreaSampler
  - SpawnSampleBudget
  - PlayerNoSpawnRadius
- Lane
- Phase
- Segment timing
```

- `RateField`, `Poisson`, `EventBurst`는 Source event 생성 방식이다.
- `Sampling`은 source field에서 event anchor를 고르는 방식이다.
- `EmissionProfile`은 sampling 결과로 만들어진 context를 받아 탄막 문법만 실행한다.

### 5.2 HazardActor wrapper
HazardActor wrapper는 actor state, telegraph, slot selection, cooldown을 책임진다.

```text
HazardActorPatternSlot
- TelegraphProfile
- EmissionProfile
- CooldownSec
- BaseWeight
- AvailabilityFlags
- LocalOffset
```

- actor는 selected slot과 actor transform을 기준으로 `EmissionExecutionContext`를 만든다.
- `CooldownSec`은 actor slot 실행 tempo이며 `EmissionProfile` 내부에 넣지 않는다.
- `TelegraphProfile`은 발사 전 예고 연출/타이밍이며 `EmissionProfile`의 탄막 문법과 분리한다.

### 5.3 Triggered wrapper
Triggered wrapper는 lifecycle event와 context binding을 책임진다.

```text
LifecycleTrigger
- Event: MotionCompleted
- TriggerEmissionProfile
- ContextBinding
  - OriginPosition: LifecycleContactPosition
  - ForwardDirection: LifecycleContactDirection
  - SourceEntity: CauserSourceEntity
  - ProducerEntity: TriggerOwner
  - CauserEntity: CompletedBullet
- DelaySec
```

- `MotionCompleted`는 1차 trigger event다.
- trigger link는 대상 `EmissionProfile`과 context binding을 가진다.
- 대상 `EmissionProfile`은 자신이 lifecycle trigger로 실행되는지, source/actor에서 직접 실행되는지 몰라도 된다.

## 6. `MotionCompleted -> TriggerEmissionProfile`

### 6.1 데이터 표현
부모 profile:

```text
EmissionProfile: ep_sample_bubble_parent
- Bullet: bd_sample_bubble
- SpawnTuning
  - SpeedOverride: 2.2
  - LifetimeOverride: 10
- MovementTuning
  - Family: DampedLinear
  - DampingPerSec: 0.8
  - StopSpeedThreshold: 0.18
- PositionPattern: SinglePoint
- Aim: ContextForward
- ShotPattern: Single
- LifecycleTriggers
  - Event: MotionCompleted
    TriggerEmissionProfile: ep_sample_bubble_fragments
    ContextBinding:
      OriginPosition: LifecycleContactPosition
      ForwardDirection: LifecycleContactDirection
    DelaySec: 0
```

후속 profile:

```text
EmissionProfile: ep_sample_bubble_fragments
- Bullet: bd_sample_bubble_fragment
- SpawnTuning
  - SpeedOverride: 1.6
  - LifetimeOverride: 0.8
- MovementTuning
  - Family: Linear
- PositionPattern: SinglePoint
- Aim: ContextForward
- ShotPattern
  - Mode: Radial
  - ShotCount: 8
```

### 6.2 해석 규칙
1. 부모 profile로 spawn된 bullet가 movement 조건에 의해 `MotionCompleted` lifecycle request를 만든다.
2. lifecycle reaction owner는 bullet에 resolved trigger link가 있으면 triggered emission request를 만든다.
3. triggered emission request는 trigger link의 context binding으로 `EmissionExecutionContext`를 구성한다.
4. target `EmissionProfile`은 해당 context 기준으로 일반 emission과 같은 문법으로 해석된다.
5. final despawn/pool enqueue owner는 기존 terminal lifecycle owner 경계를 유지한다.

### 6.3 제약
- trigger target은 null이면 invalid다.
- self trigger는 invalid다.
- direct cycle과 transitive cycle은 invalid다.
- trigger depth 제한은 필요하다. 구체 값은 runtime pipeline 설계에서 확정한다.
- `MotionCompleted` trigger가 있어도 movement family가 motion completion을 만들 수 없으면 validation warning 또는 error 후보로 본다.

## 7. `BulletDefinitionSO` deprecated 정책

### 7.1 신규 작성 기준
신규 탄막 데이터에서는 아래 필드를 `EmissionProfile` 입력으로 옮긴다.

| 기존 필드 | 신규 위치 | 정책 |
| --- | --- | --- |
| `Speed` | `EmissionProfile.SpawnTuning.SpeedOverride` | deprecated |
| `Lifetime` | `EmissionProfile.SpawnTuning.LifetimeOverride` | deprecated |
| `MovementFamily` | `EmissionProfile.MovementTuning.Family` | deprecated |
| `DampedLinear` | `EmissionProfile.MovementTuning.DampedLinear` | deprecated |
| `HomingLite` | `EmissionProfile.MovementTuning.HomingLite` | deprecated |
| `OnMotionCompletedExplode` | `EmissionProfile.LifecycleTriggers.MotionCompleted` | deprecated |
| `OnCleanupRemovedSpawnSecondary` | 후속 lifecycle trigger 확장 후보 | deprecated, 1차 migration 제외 |

### 7.2 남길 책임
`BulletDefinitionSO`는 아래 역할을 계속 가진다.

- stable definition id / type key
- prefab / visual identity
- pool size
- baseline collision radius
- capture rule / interaction family
- compatibility fallback 값

### 7.3 호환 경로
- 기존 asset은 즉시 깨지지 않아야 한다.
- compatibility read path는 기존 `BulletDefinitionSO` movement/reaction 값을 읽을 수 있다.
- 신규 authoring UI/validation은 `EmissionProfile` 값을 우선 사용한다.
- 경고/오류 전환 시점은 별도 migration 계획에서 정한다.

## 8. Validation 초안
- `EmissionProfile.Bullet`은 null이면 error다.
- `SpawnTuning.SpeedOverride`를 사용하는 경우 값은 0보다 커야 한다.
- `SpawnTuning.LifetimeOverride`를 사용하는 경우 값은 0보다 커야 한다.
- movement family는 profile당 1개만 유효하다.
- `DampedLinear.StopSpeedThreshold > 0`이고 `DampingPerSec >= 0`이어야 한다.
- `LifecycleTrigger.Event = MotionCompleted`의 target profile은 null이면 error다.
- self/direct/transitive trigger cycle은 error다.
- trigger depth가 정책 한도를 넘으면 error다.
- Source wrapper의 `Sampling`은 `EmissionProfile` 내부에 있으면 invalid다.
- operational asset이 test-only trigger profile을 참조하면 error다.
- test-only asset이 operational sample bullet definition을 참조하는 것은 별도 정책으로 허용할 수 있다.
- `BulletDefinitionSO` deprecated 필드와 `EmissionProfile` 신규 필드가 동시에 설정된 경우 우선순위와 warning 정책을 migration 계획에서 확정한다.

## 9. 구현 분해 초안

### 9.1 Plan A. schema 문서 확정
- 본 문서를 바탕으로 공통 `EmissionProfile` 책임과 wrapper 책임을 확정한다.
- 필요 시 되돌리기 비용이 큰 선택은 ADR로 승격한다.

### 9.2 Plan B. authoring schema 도입
- 공통 `EmissionProfileSO`를 추가한다.
- 기존 `HazardEmitterEmissionProfileSO`는 새 profile로 대체하거나 compatibility alias로 둔다.
- `WaveSpawnEntryAuthoring`은 profile 참조형 전환 또는 transition inline 유지 중 하나를 택한다.

### 9.3 Plan C. resolver 통합
- `WaveClipAuthoringResolver`와 `HazardEmitterProfileResolver`의 공통 grammar resolve를 `EmissionProfileResolver`로 추출한다.
- Source/Hazard wrapper resolve는 context/wrapper 전용 값만 추가한다.

### 9.4 Plan D. validation/migration rule
- `EmissionProfile` graph validation을 추가한다.
- deprecated `BulletDefinitionSO` 필드 warning 정책을 추가한다.
- `bd_sample_bubble` 계열을 migration fixture 후보로 문서화한다.

### 9.5 Plan E. runtime pipeline 설계 점검
- 시작 조건: Plan A~D에서 데이터 구조와 validation 기준이 확정된 뒤 착수한다.
- 검토 대상:
  - `SourceSpawnRequestBuffer`
  - `DiscreteEmitRequestBuffer`
  - `BulletSecondarySpawnRequestBuffer`
  - triggered emission 전용 request 필요 여부
  - common resolved emission snapshot 필요 여부

## 10. 검증 계획
- 문서 단계:
  - TechnicalDesign index에 등록한다.
  - TaskBoard의 T1 완료 상태를 갱신한다.
- 구현 단계:
  - compile
  - Unity Console error 0
  - EditMode validation tests
  - `MotionCompleted -> TriggerEmissionProfile` behavior smoke
  - 기존 `BulletMotionCompletedExplodePlayModeTests` compatibility path 유지 여부 확인
- 테스트 oracle:
  - exact tuning snapshot을 기준으로 삼지 않는다.
  - schema integrity, reference integrity, cycle/depth validation, authoring-to-runtime symbolic contract를 기준으로 삼는다.

## 11. 오픈 이슈
- `EmissionProfile`이 `RateField` 없이 direct discrete emission만 표현할지, source sustain emission의 payload profile까지 완전히 대체할지
- `ContextForward`를 기존 `FixedAimAuthoring`의 확장으로 둘지, 별도 `ContextForwardAimAuthoring` subtype으로 둘지
- `Radius`를 `BulletDefinitionSO` baseline으로 둘지, `RadiusMultiplier`를 1차 범위에 포함할지
- `BulletDefinitionSO.CaptureRule`은 profile override를 허용할지, bullet identity에 고정할지
- trigger depth 기본값과 frame budget 정책을 어디에 둘지
- `OnCleanupRemovedSpawnSecondary` migration을 언제 열지

## 12. 변경 이력
- 2026-07-01: 초안 작성. Source/Hazard/Triggered 공통 `EmissionProfile` 스키마, B안 context binding, `MotionCompleted -> TriggerEmissionProfile`, `BulletDefinitionSO` deprecated 정책을 정리했다.
