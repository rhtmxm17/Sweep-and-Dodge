# EmissionProfile Common Schema

## Metadata
- doc_id: `TD-033`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-07-02`
- related_docs:
  - [../TaskBoard/SESSION-20260701-01-emission-profile-redesign-board.md](../TaskBoard/SESSION-20260701-01-emission-profile-redesign-board.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-027-hazard-bullet-extension-contract.md](./TD-027-hazard-bullet-extension-contract.md)
  - [TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)

> 목적: `EmissionProfile`을 Source, HazardActor, Triggered emission이 공통으로 참조하는 탄막 데이터 단위로 재정의한다. 최종 목표는 마이그레이션을 포함한 전면 참조형 `EmissionProfileSO` 전환이다. 본 문서는 데이터 스키마와 authoring 책임 경계, 그리고 현재 구현된 profile-resolved runtime 적용 기준을 기록한다.

## 1. 문제 정의
- 현재 탄막 작성 경험은 작성 위치에 따라 데이터 구조가 갈라져 있다.
  - `WaveSpawnEntryAuthoring`은 `Payload / Emission / Sampling / PositionPattern / Aim / ShotPattern`을 직접 가진다.
  - `HazardEmitterEmissionProfileSO`는 유사한 `Bullet / EventRepeat / PositionPattern / Aim / ShotPattern` 구조를 별도로 가진다.
  - `BulletDefinitionSO`는 visual/pool 정체성뿐 아니라 `Speed`, `Lifetime`, `MovementFamily`, `DampedLinear`, `HomingLite`, lifecycle reaction까지 함께 가진다.
- 그 결과, 탄막 제작자는 "이 패턴에서 속도/수명/이동/후속 발사를 어떻게 줄 것인가"를 패턴 단위가 아니라 bullet definition 단위에서 우회 작성해야 한다.
- `MotionCompleted -> secondary spawn` 같은 패턴은 본질적으로 "이 이벤트에서 다른 탄막을 실행한다"에 가깝지만, 기존에는 `BulletDefinitionSO.OnMotionCompletedExplode` 안에 별도 축으로 중복 표현됐다.

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
- `MotionCompleted`, `CleanupRemoved` 발생 시 다른 `EmissionProfile`을 trigger하는 lifecycle trigger 스키마를 정의한다.
- `BulletDefinitionSO`의 movement/reaction 필드를 신규 데이터 작성 기준에서 deprecated로 둔다.
- 기존 WaveClip/Hazard/secondary spawn asset을 `EmissionProfileSO` 참조형으로 migration하는 최종 상태를 목표로 둔다.
- migration 완료 후 operational authoring에서 inline common emission grammar를 제거하거나 compatibility-only 상태로 격하한다.

### 2.2 비목표
- 이번 문서에서 runtime request/channel wire shape를 확정하지 않는다.
- 이번 문서에서 기존 asset migration을 실행하지는 않는다. 단, redesign 작업의 최종 완료 기준에는 migration 완료가 포함된다.
- 이번 문서에서 inspector graph/preview UX를 최종 확정하지 않는다.
- 이번 범위에서 `PlayerHit`, `LifetimeExpired`, `StageBlocked` trigger는 열지 않는다.

## 3. 채택 기준
- `EmissionProfile`은 canonical execution context를 소비한다.
- Source/Hazard/Triggered의 차이는 wrapper/context binding에서 해결한다.
- Triggered emission의 anchor/direction은 profile 내부가 아니라 trigger link가 제공한다.
- lifecycle trigger는 `MotionCompleted`, `CleanupRemoved`를 지원한다.
- 기존 secondary spawn path는 제거하고, triggered emission SSOT는 `EmissionProfileSO` lifecycle trigger와 `DiscreteEmitRequestBuffer` registry path로 고정한다.
- 최종 authoring SSOT는 참조형 `EmissionProfileSO`다.
- 전환형 하이브리드는 implementation staging으로만 사용했고, cleanup 완료 후 active authoring schema에서 제거했다.
  - `WaveSpawnEntryAuthoring`은 `EmissionProfileSO + Source Emission + Sampling`만 소유한다.
  - `HazardActorPatternSlotAuthoring`은 `TelegraphProfile + EmissionProfileSO + repeat/cooldown schedule`을 소유한다.
  - `HazardEmitterEmissionProfileSO`는 제거됐다.

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
  - 현재 지원 이벤트는 `MotionCompleted`, `CleanupRemoved`다.

### 4.2 `ResolvedEmissionCore`
Authoring source는 migration 단계에서 여러 형태가 공존할 수 있지만, resolver 출력의 공통 단위는 `ResolvedEmissionCore`로 고정한다.

```text
ResolvedEmissionCore
- ProfileRefId
- BulletTypeKey
- SpawnTuning
  - SpeedOverride
  - LifetimeOverride
- MovementTuning
- PositionPattern
- Aim
- ShotPattern
- LifecycleTriggers
```

- `ResolvedEmissionCore`는 Source/Hazard/Triggered wrapper가 공통으로 소비하는 resolved snapshot이다.
- Source wrapper는 여기에 `RateField`, `Sampling`, lane/phase/segment timing을 더한다.
- Hazard wrapper는 여기에 telegraph, cooldown, slot metadata, local offset을 더한다.
- Triggered wrapper는 여기에 lifecycle event와 context binding을 더한다.
- cleanup 완료 후 active authoring source는 `EmissionProfileSO` 참조형으로 단일화한다.

### 4.3 `EmissionExecutionContext`
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
- `WaveSpawnEntryAuthoring`은 inline `Payload / PositionPattern / Aim / ShotPattern`을 직접 소유하지 않고 `EmissionProfileSO`를 참조한다.

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
- pattern slot은 `EmissionProfileSO`를 직접 참조한다.
- repeat/cooldown schedule은 `EmissionProfile` 내부가 아니라 HazardActor slot wrapper 책임이다.

### 5.3 Triggered wrapper
Triggered wrapper는 lifecycle event와 context binding을 책임진다.

```text
LifecycleTrigger
- Event: MotionCompleted / CleanupRemoved
- TriggerEmissionProfile
- ContextBinding
  - OriginPosition: LifecycleContactPosition
  - ForwardDirection: LifecycleContactDirection
  - SourceEntity: CauserSourceEntity
  - ProducerEntity: TriggerOwner
  - CauserEntity: CompletedBullet
- DelaySec
```

- `MotionCompleted`와 `CleanupRemoved`는 현재 지원하는 trigger event다.
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
- authoring validation의 1차 trigger depth 한도는 `MaxTriggerDepth = 4`로 둔다.
  - 의미: root profile을 depth 0으로 보고, trigger link를 따라 내려간 target depth가 4를 초과하면 invalid다.
  - frame당 triggered emission budget과 backlog policy는 runtime pipeline 설계에서 별도로 다룬다.
- `MotionCompleted` trigger가 있어도 movement family가 motion completion을 만들 수 없으면 warning으로 본다.

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
| `OnMotionCompletedExplode` | `EmissionProfile.LifecycleTriggers.MotionCompleted` | 제거 완료 |
| `OnCleanupRemovedSpawnSecondary` | `EmissionProfile.LifecycleTriggers.CleanupRemoved` | 제거 완료 |

### 7.2 남길 책임
`BulletDefinitionSO`는 아래 역할을 계속 가진다.

- stable definition id / type key
- prefab / visual identity
- pool size
- baseline collision radius
- capture rule / interaction family
- compatibility fallback 값
  - movement fallback

### 7.3 현재 runtime path
- `MotionCompleted` 후속 발사는 `EmissionProfileSO.LifecycleTriggers.MotionCompleted`와 runtime registry 경로로 전환됐다.
- `BulletDefinitionSO.OnMotionCompletedExplode`, `BulletOnMotionCompletedExplodeReactionComponent`, optional authoring, legacy runtime fallback은 제거됐다.
- `CleanupRemoved` 후속 발사는 `EmissionProfileSO.LifecycleTriggers.CleanupRemoved`와 runtime registry 경로로 전환됐다.
- `BulletDefinitionSO.OnCleanupRemovedSpawnSecondary`, `BulletOnCleanupRemovedSpawnSecondaryReactionComponent`, optional authoring, `BulletSecondarySpawnRequestBuffer`, `SecondarySpawnExecutionSystem`은 제거됐다.
- movement 값은 아직 `BulletDefinitionSO` pool/bootstrap fallback 값을 유지한다. profile movement override가 있으면 spawned bullet runtime 값이 우선한다.

### 7.4 마이그레이션 단계
현재 runtime은 `EmissionProfileSO` 값을 spawned bullet apply 단계와 lifecycle trigger registry에 반영한다. movement fallback은 `BulletDefinitionSO -> BulletPoolDefinitionBuffer -> BulletPoolOwnerBootstrapSystem` 경로를 보존한다.

| 단계 | 기준 | validation 정책 | runtime 정책 |
| --- | --- | --- | --- |
| Phase 1. 완료 | `MotionCompleted` 후속 발사를 profile trigger로 전환 | `MotionCompleted` trigger graph를 검증하고 legacy coexistence warning은 제거 | registry 기반 `DiscreteEmitRequestBuffer` append만 사용 |
| Phase 2. 완료 | movement fallback 유지, `CleanupRemoved` legacy 제거 | operational 신규 작성은 profile 값을 우선하고, legacy reaction coexistence는 허용하지 않음 | profile 값이 있으면 profile 우선, 없으면 movement만 `BulletDefinitionSO` fallback |
| Phase 3. 완료 | `CleanupRemoved` lifecycle trigger event migration | `OnCleanupRemovedSpawnSecondary`와 secondary spawn path를 제거 | `DiscreteEmitRequestBuffer` registry path만 사용 |

### 7.5 필드별 migration 기준
| `BulletDefinitionSO` 필드 | `EmissionProfileSO` 위치 | migration 기준 |
| --- | --- | --- |
| `Speed` | `SpawnTuning.SpeedOverride` | profile 값이 있으면 우선한다. profile 값이 없으면 fallback으로만 읽는다. |
| `Lifetime` | `SpawnTuning.LifetimeOverride` | profile 값이 있으면 우선한다. profile 값이 없으면 fallback으로만 읽는다. |
| `MovementFamily` | `MovementTuning.Family` | profile movement가 있으면 bullet definition movement family는 무시한다. |
| `DampedLinear` | `MovementTuning.DampedLinear` | `MovementTuning.Family = DampedLinear`일 때 profile 파라미터를 우선한다. |
| `HomingLite` | `MovementTuning.HomingLite` | `MovementTuning.Family = HomingLite`일 때 profile 파라미터를 우선한다. |
| `OnMotionCompletedExplode` | `LifecycleTriggers.MotionCompleted` | 변환 완료. legacy field/component/runtime fallback은 제거됐다. |
| `OnCleanupRemovedSpawnSecondary` | `LifecycleTriggers.CleanupRemoved` | 변환 완료. legacy field/component/channel/system은 제거됐다. |

### 7.6 신규 작성 금지 기준
- 신규 operational 탄막 데이터는 gameplay tuning을 `EmissionProfileSO`에 작성한다.
- `BulletDefinitionSO`에 새 movement/reaction 값을 추가해 신규 패턴을 만드는 것은 금지 방향이다.
- 단, `BulletDefinitionSO`는 pool/visual identity와 movement fallback 값을 보존하므로 해당 필드는 후속 migration 전까지 유지한다.
- test-only asset과 compatibility fixture는 남아 있는 movement deprecated field를 명시적으로 사용할 수 있다.

## 8. Validation Rules

### 8.1 Profile shape
- `EmissionProfile.Bullet`은 null이면 error다.
- `SpawnTuning.SpeedOverride`를 사용하는 경우 값은 0보다 커야 한다.
- `SpawnTuning.LifetimeOverride`를 사용하는 경우 값은 0보다 커야 한다.
- movement family는 profile당 1개만 유효하다.
- `DampedLinear.StopSpeedThreshold > 0`이고 `DampingPerSec >= 0`이어야 한다.
- `HomingLite`는 non-negative parameter와 `MinRetargetDistance <= MaxAcquireDistance`를 만족해야 한다.
- Source wrapper의 `Sampling`, `RateField`, source density/cap 필드는 `EmissionProfile` 내부에 있으면 error다.

### 8.2 Lifecycle trigger graph
- 현재 lifecycle trigger event는 `MotionCompleted`, `CleanupRemoved`를 허용한다.
- enabled lifecycle trigger의 target profile은 null이면 error다.
- self trigger는 error다.
- direct cycle과 transitive cycle은 error다.
- authoring validation의 1차 graph depth 한도는 `MaxTriggerDepth = 4`다.
  - root profile depth는 0이다.
  - trigger target으로 한 단계 이동할 때마다 depth를 1 증가시킨다.
  - traversal 중 depth 4를 초과하면 error다.
- 하나의 event에 여러 trigger link를 허용할지는 이번 slice에서 열지 않는다. 현재 schema는 event별 단일 target link로 검증한다.
- `MotionCompleted` trigger가 있는데 profile의 movement family가 motion completion request를 만들 수 없으면 warning이다.
  - 예: `Linear` movement가 lifetime/stage/player hit로만 종료되는 경우.
  - 이유: authoring reuse나 future movement 확장 가능성 때문에 1차부터 error로 잠그지 않는다.

### 8.3 Asset boundary
- operational asset이 test-only trigger profile을 참조하면 error다.
- operational asset이 test-only bullet definition을 참조하면 error다.
- test-only asset이 operational profile이나 bullet definition을 참조하는 것은 허용한다.
- operational/test 판정은 현재 validation의 `Assets/_Project/99_Tests/` 경로 기준을 재사용한다.

### 8.4 Deprecated data coexistence
- 신규 `EmissionProfileSO` 값이 존재하면 `BulletDefinitionSO`의 deprecated movement 값보다 우선한다.
- `MotionCompleted -> TriggerEmissionProfile`과 `BulletDefinitionSO.OnMotionCompletedExplode` 공존 상태는 더 이상 허용하지 않는다. legacy field와 runtime fallback은 제거됐다.
- `CleanupRemoved -> TriggerEmissionProfile`과 `BulletDefinitionSO.OnCleanupRemovedSpawnSecondary` 공존 상태는 더 이상 허용하지 않는다. legacy field와 runtime fallback은 제거됐다.

### 8.5 Validation implementation target
- T3의 문서 기준은 이후 `ContentValidationRules` 또는 별도 `EmissionProfileValidationRules`에서 구현한다.
- 테스트 oracle은 exact tuning snapshot이 아니라 아래 계약을 검증한다.
  - null/reference integrity
  - self/direct/transitive cycle
  - `MaxTriggerDepth = 4`
  - operational/test asset boundary
  - deprecated movement compatibility policy

## 9. 구현 분해 초안

### 9.1 Plan A. schema 문서 확정
- 본 문서를 바탕으로 공통 `EmissionProfile` 책임과 wrapper 책임을 확정한다.
- 필요 시 되돌리기 비용이 큰 선택은 ADR로 승격한다.

### 9.2 Plan B. authoring schema 도입
- 공통 `EmissionProfileSO`를 추가한다.
- 기존 `HazardEmitterEmissionProfileSO`는 새 profile로 migration한 뒤 제거한다.
- `WaveSpawnEntryAuthoring`은 profile 참조형으로 전환한다.
- cleanup 완료 후 inline common grammar fallback은 유지하지 않는다.

### 9.3 Plan C. resolver 통합
- `WaveClipAuthoringResolver`와 `HazardEmitterProfileResolver`의 공통 grammar resolve를 `EmissionProfileResolver`로 추출한다.
- `EmissionProfileResolver`의 공통 출력은 `ResolvedEmissionCore`다.
- Source/Hazard wrapper resolve는 context/wrapper 전용 값만 추가한다.

### 9.4 Plan D. validation/migration rule
- `EmissionProfile` graph validation을 추가한다.
- deprecated `BulletDefinitionSO` 필드 warning 정책을 추가한다.
- `bd_sample_bubble` 계열을 migration fixture 후보로 문서화한다.
- migration 완료 기준을 정의한다.
  - operational WaveClip/Hazard asset이 inline common grammar 대신 `EmissionProfileSO`를 참조한다.
  - 기존 `OnMotionCompletedExplode`는 `MotionCompleted -> TriggerEmissionProfile`로 변환된다.
- `OnCleanupRemovedSpawnSecondary`는 `CleanupRemoved -> TriggerEmissionProfile`로 변환된다.

### 9.5 Plan E. runtime pipeline 설계 점검
- 시작 조건: Plan A~D에서 데이터 구조와 validation 기준이 확정된 뒤 착수한다.
- 검토 대상:
  - `SourceSpawnRequestBuffer`
  - `DiscreteEmitRequestBuffer`
  - triggered emission 전용 request 필요 여부
  - common resolved emission snapshot 필요 여부

### 9.6 Plan F. full reference migration
- 기존 `WaveSpawnEntryAuthoring` inline common grammar를 `EmissionProfileSO` asset으로 추출했다.
- 기존 `HazardEmitterEmissionProfileSO` asset을 `EmissionProfileSO` asset으로 변환하고 pattern slot 참조를 교체했다.
- `bd_sample_bubble`의 `OnMotionCompletedExplode`는 parent/fragment `EmissionProfileSO` 쌍으로 변환한다.
- cleanup 완료 후 compatibility source가 operational/test asset에서 사용되지 않는지 validation한다.

### 9.7 `bd_sample_bubble` conversion candidate
`bd_sample_bubble` 계열은 기존 `MotionCompleted -> secondary spawn` 데이터를 `MotionCompleted -> TriggerEmissionProfile`로 옮기는 migration fixture 후보로 둔다. 아래 값은 migration 예시이며, 테스트 oracle로 exact tuning snapshot을 고정하지 않는다.

부모 profile:

```text
EmissionProfileSO: ep_sample_bubble_parent
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
    ContextBinding
      OriginPosition: LifecycleContactPosition
      ForwardDirection: LifecycleContactDirection
      SourceEntity: CauserSourceEntity
      CauserEntity: CompletedBullet
    DelaySec: 0
```

후속 profile:

```text
EmissionProfileSO: ep_sample_bubble_fragments
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

`BulletDefinitionSO`에 남는 책임:
- `bd_sample_bubble` / `bd_sample_bubble_fragment`의 `DefinitionId`
- prefab / visual identity
- pool size
- capture rule
- baseline collision radius
- score value

미해결 변환 항목:
- 기존 `OnMotionCompletedExplode.SpawnRadius = 0.08`은 `PositionPattern: SinglePoint`와 `ShotPattern: Radial`만으로는 직접 보존되지 않는다.
- 1차 변환 후보에서는 lifecycle contact position을 origin으로 사용하는 단순 변환을 기본으로 둔다.
- spawn radius 보존이 필요하면 T6/T7에서 triggered emission request 또는 `PositionPattern`에 radial spawn offset/radius 표현을 추가할지 결정한다.
- 기존 `OnMotionCompletedExplode.Shape = PointBurst`, `SpreadAngleDeg = 360`, `SpawnCount = 8`은 `ShotPattern: Radial, ShotCount: 8`로 대응한다.

### 9.8 T6 runtime pipeline decision
채택안은 기존 `DiscreteEmitRequestBuffer`를 확장해 신규 `EmissionProfile` discrete execution의 주 채널로 승격하는 것이다.

```text
ResolvedEmissionCore
  + EmissionExecutionContext
  -> DiscreteEmitRequestBuffer
  -> DiscreteEmitExecutionSystem
  -> ApplySpawnedBulletState(profile-resolved tuning)
```

채택 기준:
- `SourceClip` event branch와 `HazardActor` direct emit branch는 이미 `DiscreteEmitRequestBuffer`를 공유한다.
- `DiscreteEmitRequestBuffer`는 현재 runtime에서 `PositionPattern`, `Aim`, `ShotPattern`, repeat, backlog, budget 실행 문법을 가장 많이 포함한다.
- `MotionCompleted -> TriggerEmissionProfile`은 별도 secondary spawn 문법이 아니라 profile-resolved discrete emission request로 변환한다.
- `BulletSecondarySpawnRequestBuffer`는 legacy cleanup migration 완료 후 제거됐으며, 신규 SSOT로 사용하지 않는다.
- Source sustain branch는 즉시 `DiscreteEmitRequestBuffer`로 완전 흡수하지 않는다. 다만 profile-resolved gameplay tuning은 `SourceSpawnRequestBuffer`와 `DiscreteEmitRequestBuffer` 양쪽이 같은 apply 계약을 통해 사용할 수 있어야 한다.

`DiscreteEmitRequestBuffer` 확장 후보:

```text
DiscreteEmitRequestBuffer
- existing producer/provenance
- existing anchor/position/aim/shot/repeat fields
- ProfileRefId
- ProfileExecutionDepth
- SpawnTuning
  - HasSpeedOverride
  - SpeedOverride
  - HasLifetimeOverride
  - LifetimeOverride
- MovementTuning
  - Family
  - DampedLinear
  - HomingLite
- LifecycleTriggerRuntimeRef
  - MotionCompleted target profile/runtime key
  - context binding id
  - delay frames/sec
```

`SpawnRequestCommonUtility.ApplySpawnedBulletState`는 profile-resolved tuning을 받을 수 있는 overload 또는 parameter object를 가진다.

```text
SpawnedBulletRuntimeTuning
- HasSpeedOverride
- SpeedOverride
- HasLifetimeOverride
- LifetimeOverride
- MovementTuning
- LifecycleTriggerRuntimeRef
```

해석 규칙:
- profile speed/lifetime 값이 있으면 spawn 시점에 `BulletSpeedComponent`, `BulletLifetimeMaxComponent`, `BulletVelocityComponent`, `BulletLifetimeComponent`에 반영한다.
- profile movement 값이 있으면 spawn 시점에 movement component를 덮어쓴다.
  - `Linear`: `BulletDampedMotionComponent`, `BulletHomingLiteMotionComponent` 제거 또는 비활성화 기준이 필요하다.
  - `DampedLinear`: `BulletDampedMotionComponent` 값 적용.
  - `HomingLite`: `BulletHomingLiteMotionComponent` 값 적용.
- profile lifecycle trigger runtime ref가 있으면 spawned bullet에 신규 trigger component를 적용한다.
- profile 값이 없으면 `BulletDefinitionSO -> BulletPoolDefinitionBuffer`로 bootstrap된 fallback 값을 사용한다.

2026-07-02 1차 구현 상태:
- `SpawnTuning.SpeedOverride` / `LifetimeOverride`는 `SourceClipPatternBuffer`, `SourceSpawnRequestBuffer`, `DiscreteEmitRequestBuffer`, `HazardActor` execution/active emission runtime을 통해 spawned bullet apply 단계까지 전달된다.
- `SpawnRequestCommonUtility.ApplySpawnedBulletState`는 profile speed/lifetime override가 있으면 `BulletSpeedComponent`, `BulletVelocityComponent`, `BulletLifetimeComponent`, `BulletLifetimeMaxComponent`를 spawn 시점에 갱신한다.

2026-07-02 MovementTuning C안 구현 상태:
- `MovementTuning`은 `SourceClipPatternBuffer`, `SourceSpawnRequestBuffer`, `DiscreteEmitRequestBuffer`, `HazardActor` execution/active emission runtime을 통해 spawned bullet apply 단계까지 전달된다.
- spawned bullet에는 `BulletMovementRuntimeComponent`를 둔다. profile movement override가 있으면 spawn 시점에 이 component의 `Family`, `DampedLinear`, `HomingLite` 값을 갱신한다.
- `BulletSimulationSystem`은 `BulletMovementRuntimeComponent`가 있는 bullet을 runtime movement job에서 처리한다. 기존 `BulletDampedMotionComponent` / `BulletHomingLiteMotionComponent` 기반 job은 runtime component가 없는 compatibility/fallback entity만 처리한다.
- `BulletDefinitionSO` movement 값은 pool/bootstrap fallback으로 유지한다. profile override가 있으면 runtime movement 값이 우선한다.

2026-07-02 LifecycleTrigger registry 구현 상태:
- `BulletEmissionProfileRefComponent`를 spawned bullet에 둔다. pool/prefab 기본값은 `0`이고, spawn apply 단계에서 request의 `ProfileRefId`를 기록한다.
- `EmissionProfileRuntimeRegistryTag` singleton과 `EmissionProfileRuntimeRegistryBuffer`를 둔다.
- Stage apply 시점에 active `StageDefinitionSO`에서 registry를 재구성한다.
  - Source WaveClip directive profile
  - HazardActor pattern slot emission profile
  - 각 profile의 `MotionCompleted.TargetProfile` recursive target
- registry key는 `EmissionProfileSO.GetInstanceID()` 기반 `ProfileRefId`이며 중복 profile은 de-duplicate한다.
- registry entry는 target emission 실행에 필요한 resolved payload, spawn/movement tuning, position/aim/shot pattern, `MotionCompleted`/`CleanupRemoved` target/context binding/delay를 저장한다.
- registry miss 또는 target miss는 runtime exception이 아니라 no-op/fallback으로 처리한다. authoring 오류는 validation이 담당한다.

`MotionCompleted -> TriggerEmissionProfile` runtime path:
1. spawned bullet은 `BulletEmissionProfileRefComponent.ProfileRefId`로 자신이 어떤 `EmissionProfileSO`에서 spawn됐는지만 보유한다.
2. `BulletLifecycleReactionExecutionSystem`은 `MotionCompleted` request를 읽고 registry에서 source profile entry를 조회한다.
3. source profile에 `MotionCompleted` trigger가 있으면 registry에서 target profile entry를 조회한다.
4. trigger context는 아래 binding으로 구성한다.
   - origin: `BulletLifecycleContactComponent.PositionXZ` + bullet 현재 Y
   - direction: `BulletLifecycleContactComponent.DirectionXZ`, fallback `(1, 0)`
   - source: `BulletSourceRefComponent.Value`
   - producer/causer: completed bullet entity
5. target profile의 resolved core를 `DiscreteEmitRequestBuffer`로 변환해 append한다.
6. `DelaySec`는 fixed tick 기준 frame delay로 변환하며, 최소 다음 frame부터 실행되도록 `ReadyFrame >= currentFrame + 1`로 둔다.
7. `DiscreteEmitExecutionSystem`은 `ReadyFrame > currentFrame`인 request를 실행 후보에서 제외하고 backlog에는 남긴다.
8. source profile trigger append에 성공하면 triggered emission request는 `DiscreteEmitExecutionSystem`의 일반 discrete emission 실행 계약을 따른다.
9. source profile trigger가 없거나 registry lookup이 실패하면 `MotionCompleted` 후속 emission은 no-op이다. legacy `BulletSecondarySpawnRequestBuffer` fallback은 제거됐다.

`CleanupRemoved -> TriggerEmissionProfile` runtime path:
1. `VacuumCollected` 또는 `CarryFullRemoved` lifecycle request가 발생하면 cleanup removal trigger 후보로 본다.
2. spawned bullet의 `BulletEmissionProfileRefComponent.ProfileRefId`로 source profile registry entry를 조회한다.
3. source profile에 `CleanupRemoved` trigger가 있으면 target profile entry를 조회한다.
4. trigger context는 `MotionCompleted`와 같은 binding 기본값을 사용한다.
   - origin: `BulletLifecycleContactComponent.PositionXZ` + bullet 현재 Y
   - direction: `BulletLifecycleContactComponent.DirectionXZ`, fallback `(1, 0)`
   - source: `BulletSourceRefComponent.Value`
   - producer/causer: cleanup removed bullet entity
5. target profile resolved core를 `DiscreteEmitRequestBuffer`로 append한다.
6. source profile trigger가 없거나 registry lookup이 실패하면 cleanup removed 후속 emission은 no-op이다.

동시 존재 정책:
- `MotionCompleted` 후속 발사의 SSOT는 profile lifecycle trigger다.
- legacy `BulletDefinitionSO.OnMotionCompletedExplode`, `BulletOnMotionCompletedExplodeReactionComponent`, optional authoring, runtime fallback은 제거됐다.
- `CleanupRemoved` 후속 발사의 SSOT도 profile lifecycle trigger다.
- legacy `BulletDefinitionSO.OnCleanupRemovedSpawnSecondary`, `BulletOnCleanupRemovedSpawnSecondaryReactionComponent`, optional authoring, `BulletSecondarySpawnRequestBuffer`, `SecondarySpawnExecutionSystem`은 제거됐다.

`SpawnRadius` 보존 기준:
- legacy `OnMotionCompletedExplode.SpawnRadius`는 현재 변환에서 보존하지 않았다. exact radius 보존이 필요하면 `PositionPattern` 확장으로 다룬다.
- 1차 구현에서는 `SinglePoint` + `Radial ShotPattern` 단순 변환을 허용한다.
- exact legacy radius 보존이 필요한 fixture는 `PositionPattern`에 radial spawn offset/radius 표현을 추가한 뒤 migration한다.

보류/제외:
- `TriggeredEmissionRequestBuffer`를 별도 채널로 만들지 않는다.
- `SourceSpawnRequestBuffer`, `DiscreteEmitRequestBuffer`의 전면 통합은 이번 단계에서 하지 않는다.

2026-07-02 T15 ProducerKind별 budget/backlog 정책:
- 채택안은 C안이다. 단일 `DiscreteEmitRequestBuffer`와 `DiscreteEmitExecutionSystem`은 유지하고, `DiscreteEmitProducerKind`별 budget/cap/metrics를 추가한다.
- 기존 `DiscreteEmitPolicyComponent.BudgetPerFrame`, `MaxPendingCount`, `MaxPendingAgeFrames`는 total/global guard로 유지한다.
- ProducerKind별 `BudgetPerFrame` / `MaxPendingCount` 값은 0이면 legacy-compatible fallback으로 해석한다.
  - per-kind budget 0: 해당 producer는 global remaining budget 안에서 소비된다.
  - per-kind max pending 0: 해당 producer는 global `MaxPendingCount` 안에서만 제한된다.
- `TriggeredEmission`에 별도 budget/cap을 줄 수 있어 Source/Hazard discrete emit이 lifecycle trigger storm에 완전히 밀리지 않게 한다.
- 별도 `TriggeredEmissionRequestBuffer` 또는 별도 execution system은 만들지 않는다.
- metrics는 total 값과 ProducerKind별 pending/deferred/budget used/dropped/expired count를 함께 기록한다.
- request 선택은 기존 priority/oldest 기준을 유지하되, global budget과 producer-specific budget을 모두 만족하는 request만 실행 후보로 본다.

2026-07-02 T15 구현 상태:
- `DiscreteEmitPolicyComponent`에 ProducerKind별 budget/cap field를 추가했다.
  - `WaveClipEventBudgetPerFrame`, `HazardActorBudgetPerFrame`, `TriggeredEmissionBudgetPerFrame`
  - `WaveClipEventMaxPendingCount`, `HazardActorMaxPendingCount`, `TriggeredEmissionMaxPendingCount`
- `DiscreteEmitBacklogMetricsComponent`에 ProducerKind별 pending/deferred/budget used/dropped/expired metrics를 추가했다.
- `DiscreteEmitExecutionSystem`은 global budget/cap과 ProducerKind별 budget/cap을 동시에 만족하는 request만 실행 후보로 선택한다.
- ProducerKind별 값이 0인 기존 fixture/asset은 기존 global policy만 적용받는다.

## 10. 검증 계획
- 문서 단계:
  - TechnicalDesign index에 등록한다.
  - TaskBoard의 T1 완료 상태를 갱신한다.
- 구현 단계:
  - compile
  - Unity Console error 0
  - EditMode validation tests
  - `MotionCompleted -> TriggerEmissionProfile` behavior smoke
  - legacy MotionCompleted/CleanupRemoved compatibility path 제거 확인
  - migration 후 operational asset reference integrity 검사
  - migrated WaveClip/Hazard asset이 `EmissionProfileSO` 참조형으로 resolve되는지 검사
  - Source event, HazardActor direct emit, Triggered emission이 같은 `DiscreteEmitRequestBuffer` apply contract를 사용하는지 검사
  - profile speed/lifetime/movement override가 bullet definition fallback보다 우선 적용되는지 behavior test로 확인
- 테스트 oracle:
  - exact tuning snapshot을 기준으로 삼지 않는다.
  - schema integrity, reference integrity, cycle/depth validation, authoring-to-runtime symbolic contract를 기준으로 삼는다.

## 11. 오픈 이슈
- `EmissionProfile`이 `RateField` 없이 direct discrete emission만 표현할지, source sustain emission의 payload profile까지 완전히 대체할지
- `ContextForward`를 기존 `FixedAimAuthoring`의 확장으로 둘지, 별도 `ContextForwardAimAuthoring` subtype으로 둘지
- `Radius`를 `BulletDefinitionSO` baseline으로 둘지, `RadiusMultiplier`를 1차 범위에 포함할지
- `BulletDefinitionSO.CaptureRule`은 profile override를 허용할지, bullet identity에 고정할지
- legacy secondary spawn `SpawnRadius`를 보존하기 위한 `PositionPattern` 확장 shape를 어떻게 정의할지
- `PlayerHit`, `LifetimeExpired`, `StageBlocked` lifecycle trigger를 언제 열지

## 12. 변경 이력
- 2026-07-02: T15 ProducerKind별 budget/backlog 정책으로 C안을 채택했다. 단일 `DiscreteEmitRequestBuffer`는 유지하고, `DiscreteEmitProducerKind`별 budget/cap/metrics를 `DiscreteEmitPolicyComponent` / `DiscreteEmitBacklogMetricsComponent`에 추가하는 기준을 반영했다.
- 2026-07-02: MotionCompleted legacy data/runtime 제거 상태를 반영했다. `BulletDefinitionSO.OnMotionCompletedExplode`, legacy component/authoring/fallback, 관련 tests를 제거하고 profile registry 기반 triggered emission을 유일한 MotionCompleted 후속 발사 경로로 고정했다.
- 2026-07-02: CleanupRemoved legacy data/runtime 제거 상태를 반영했다. `BulletDefinitionSO.OnCleanupRemovedSpawnSecondary`, cleanup secondary component/authoring/channel/system, 관련 tests를 제거하고 profile registry 기반 triggered emission을 유일한 CleanupRemoved 후속 발사 경로로 고정했다.
- 2026-07-02: MotionCompleted LifecycleTrigger runtime registry 구현 상태를 반영했다. spawned bullet은 `ProfileRefId`만 보유하고, lifecycle reaction owner가 stage registry에서 source/target profile을 조회해 `DiscreteEmitRequestBuffer`에 triggered emission request를 append한다.
- 2026-07-01: cleanup 완료 기준을 반영했다. `WaveSpawnEntryAuthoring` inline common grammar와 `HazardEmitterEmissionProfileSO` compatibility source를 제거하고, active authoring SSOT를 `EmissionProfileSO` 참조형으로 단일화했다.
- 2026-07-01: T6 runtime pipeline 결정으로 `DiscreteEmitRequestBuffer` 확장안을 채택하고, triggered emission을 profile-resolved discrete emission으로 실행하는 기준을 추가했다.
- 2026-07-01: 최종 목표를 마이그레이션 포함 전면 참조형 `EmissionProfileSO` 전환으로 명시하고, 전환형 하이브리드는 implementation staging으로만 채택하도록 정리했다.
- 2026-07-01: 초안 작성. Source/Hazard/Triggered 공통 `EmissionProfile` 스키마, B안 context binding, `MotionCompleted -> TriggerEmissionProfile`, `BulletDefinitionSO` deprecated 정책을 정리했다.
- 2026-07-02: runtime pipeline 1차 구현 상태를 반영했다. `SpawnTuning.SpeedOverride` / `LifetimeOverride`는 Source/Hazard request와 공통 spawn apply 경로에서 적용되며, movement/lifecycle trigger 적용은 후속 단계로 유지한다.
- 2026-07-02: MovementTuning C안을 반영했다. `BulletMovementRuntimeComponent`를 통해 profile movement override를 spawn 시점에 적용하고, simulation은 runtime movement component를 우선 처리한다.
