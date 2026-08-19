# EmissionProfile Common Schema

## Metadata
- doc_id: `TD-033`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-07-03`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)

> `EmissionProfileSO`는 Source, HazardActor, Triggered emission이 공통으로 참조하는 탄막 실행 단위다. 이 문서는 현재 authoring schema, wrapper 책임, runtime resolve/apply 기준을 설명한다.

## 1. 목적
- 탄막 gameplay tuning을 `EmissionProfileSO` 중심으로 작성한다.
- Source, HazardActor, Triggered emission이 같은 payload/spawn/movement/pattern 문법을 사용한다.
- Source 전용 sampling/timeline과 HazardActor 전용 slot/telegraph/cooldown은 wrapper 책임으로 분리한다.
- spawned bullet에는 profile-resolved tuning을 적용하고, lifecycle trigger는 active stage registry를 통해 실행한다.

## 2. 비목표
- Source sustain branch와 discrete branch의 request channel 전면 통합은 이 문서 범위가 아니다.
- inspector graph/preview UX 최종안은 이 문서 범위가 아니다.
- `PlayerHit`, `LifetimeExpired`, `StageBlocked` trigger event 확장은 이 문서 범위가 아니다.

## 3. Current Authoring Model

### 3.1 `EmissionProfileSO`
```text
EmissionProfileSO
  Payload
  SpawnTuning
  MovementTuning
  PositionPattern
  Aim
  ShotPattern
  LifecycleTriggers
```

- `Payload`
  - 사용할 `BulletDefinitionSO`를 참조한다.
- `SpawnTuning`
  - profile 단위 speed/lifetime override를 가진다.
  - 값이 없으면 bullet definition 기반 fallback 값을 사용한다.
- `MovementTuning`
  - profile 단위 movement family와 파라미터를 가진다.
  - 지원 family는 현재 runtime component가 처리하는 family 집합을 따른다.
- `PositionPattern`
  - execution context origin 기준 spawn 위치를 만든다.
- `Aim`
  - execution context forward 또는 runtime target 기준 방향을 만든다.
- `ShotPattern`
  - repeat 1회에서 생성할 shot slot 구조를 만든다.
- `LifecycleTriggers`
  - 현재 지원 event는 `MotionCompleted`, `CleanupRemoved`다.
  - trigger target은 다른 `EmissionProfileSO`를 참조한다.

### 3.2 `ResolvedEmissionCore`
Runtime resolver의 공통 출력 단위다.

```text
ResolvedEmissionCore
  ProfileRefId
  BulletTypeKey
  SpawnTuning
  MovementTuning
  PositionPattern
  Aim
  ShotPattern
  LifecycleTriggers
```

- Source wrapper는 source timeline, rate, sampling 값을 더한다.
- HazardActor wrapper는 selected slot, telegraph, cooldown, local offset 값을 더한다.
- Triggered wrapper는 lifecycle event, target profile, context binding, delay 값을 더한다.

### 3.3 `EmissionExecutionContext`
`EmissionProfileSO`는 source grid, actor transform, lifecycle contact를 직접 알지 않는다. 실행자는 아래 context를 구성해 profile 문법을 적용한다.

```text
EmissionExecutionContext
  OriginPosition
  ForwardDirection
  SourceEntity
  ProducerEntity
  CauserEntity
  Frame
  SequenceSeed
```

- `OriginPosition`: position pattern 기준점.
- `ForwardDirection`: context-relative aim 기준 방향.
- `SourceEntity`: source attribution과 active count 계승에 사용.
- `ProducerEntity`: Source, HazardActor, Triggered producer 추적에 사용.
- `CauserEntity`: triggered emission에서 원인 bullet 추적에 사용.
- `Frame`: delay, age, debug trace에 사용.
- `SequenceSeed`: deterministic pattern 안정화에 사용.

## 4. Wrapper Responsibilities

### 4.1 Source WaveClip
```text
WaveSpawnEntryAuthoring
  Profile
  SourceEmission
  Sampling
  Lane / Phase / Segment timing
```

- `Profile`은 필수 `EmissionProfileSO` 참조다.
- `SourceEmission`은 source event 생성 방식과 rate를 소유한다.
- `Sampling`은 source field에서 event anchor를 고르는 방식을 소유한다.
- payload, position, aim, shot grammar는 profile에서만 온다.

### 4.2 HazardActor
```text
HazardActorPatternSlotAuthoring
  TelegraphProfile
  Emission
    Profile
    EventRepeatCount
    EventShotSchedule
    EventShotIntervalSec
    CooldownSec
  BaseWeight
  AvailabilityFlags
  LocalOffset
```

- actor는 selected slot과 transform을 기준으로 execution context를 만든다.
- telegraph와 cooldown은 actor slot wrapper 책임이다.
- repeat schedule은 actor slot에서 event tempo를 정하고, shot grammar는 profile이 정한다.

### 4.3 Triggered Emission
```text
LifecycleTrigger
  Event
  TargetProfile
  ContextBinding
  DelaySec
```

- trigger source profile은 target profile 참조와 delay를 가진다.
- target profile은 일반 emission과 같은 문법으로 실행된다.
- trigger context는 lifecycle contact position/direction, source attribution, causer bullet로 구성한다.

## 5. Runtime Registry

### 5.1 Registry Build
- Stage apply 시점에 active `StageDefinitionSO` 기준으로 registry를 재구성한다.
- 수집 대상:
  - Source WaveClip directive profile
  - HazardActor pattern slot emission profile
  - lifecycle trigger target profile
- `ProfileRefId`는 `EmissionProfileSO.GetInstanceID()` 기준이다.
- 중복 profile은 de-duplicate한다.
- registry miss와 target miss는 runtime exception이 아니라 no-op으로 처리한다. authoring 오류는 validation이 담당한다.

### 5.2 Spawned Bullet Profile Ref
- spawned bullet은 `BulletEmissionProfileRefComponent.ProfileRefId`만 보유한다.
- profile의 전체 데이터는 bullet entity에 복제하지 않는다.
- spawn apply 단계에서 request의 profile id를 bullet component에 기록한다.

### 5.3 Lifecycle Trigger Execution
1. lifecycle event owner가 completed/removed bullet의 `ProfileRefId`를 읽는다.
2. registry에서 source profile entry를 찾는다.
3. 해당 event trigger가 있으면 target profile entry를 찾는다.
4. lifecycle contact position/direction과 source attribution으로 triggered execution context를 만든다.
5. target profile의 resolved core를 `DiscreteEmitRequestBuffer`에 append한다.
6. `DelaySec`는 fixed tick frame delay로 변환하며 최소 다음 frame부터 실행되도록 한다.
7. target miss, source miss, disabled trigger는 no-op이다.

## 6. Runtime Apply Contract

### 6.1 Source Sustain
- Source sustain branch는 `SourceSpawnRequestBuffer`를 사용한다.
- profile-resolved spawn/movement tuning은 spawned bullet apply 단계까지 전달된다.
- speed/lifetime/movement override가 있으면 profile 값이 bullet fallback보다 우선한다.

### 6.2 Discrete Emit
- Source event branch, HazardActor direct emit, Triggered emission은 `DiscreteEmitRequestBuffer`를 공유한다.
- `DiscreteEmitExecutionSystem`은 repeat/shot expansion, budget/pool gate, spawn apply를 수행한다.
- `DiscreteEmitProducerKind`는 현재 `WaveClipEvent`, `HazardActor`, `TriggeredEmission` producer budget과 metrics를 구분한다.

### 6.3 Movement
- spawned bullet은 `BulletMovementRuntimeComponent` 기준으로 movement를 처리한다.
- profile movement override가 있으면 spawn 시점에 runtime movement 값을 적용한다.
- profile movement override가 없으면 pool/bootstrap fallback 값을 사용한다.

## 7. `BulletDefinitionSO` Boundary
`BulletDefinitionSO`는 bullet identity와 baseline data를 소유한다.

- stable definition id / type key
- prefab / visual identity
- pool size
- baseline collision radius
- capture rule / interaction family
- score value
- compatibility fallback movement data

신규 gameplay tuning은 `EmissionProfileSO`에 작성한다. `BulletDefinitionSO`에 새 movement gameplay 값을 추가해 신규 패턴을 만드는 것은 금지 방향이다.

## 8. Validation Rules

### 8.1 Profile Shape
- `EmissionProfileSO.Payload.Bullet`은 null이면 error다.
- enabled speed/lifetime override는 0보다 커야 한다.
- movement family는 profile당 1개만 유효하다.
- movement parameter는 non-negative 범위와 family별 invariant를 만족해야 한다.
- Source-only sampling/rate field는 profile 내부에 둘 수 없다.

### 8.2 Lifecycle Trigger Graph
- enabled lifecycle trigger의 target profile은 null이면 error다.
- self trigger는 error다.
- direct cycle과 transitive cycle은 error다.
- graph depth 한도는 `MaxTriggerDepth = 4`다.
- `MotionCompleted` trigger가 있는데 profile movement가 motion completion request를 만들 수 없으면 warning이다.

### 8.3 Content Boundary
- 운영 WaveClip directive는 `Profile`을 필수로 가진다.
- 운영 asset에서 test-only profile을 참조하면 error다.
- HazardActor pattern slot emission은 `Profile`을 필수로 가진다.
- validation은 exact tuning snapshot이 아니라 schema integrity, reference integrity, graph integrity, runtime contract를 기준으로 한다.

## 9. Verification
- compile
- Unity Console error 0
- 관련 EditMode validation/runtime tests
- `BulletPlayModeSmokeTests`
- profile speed/lifetime/movement override가 fallback보다 우선 적용되는지 behavior test
- lifecycle trigger가 registry를 통해 `DiscreteEmitRequestBuffer`로 이어지는지 behavior test
- ProducerKind별 discrete emit budget/cap/metrics가 global guard와 함께 적용되는지 behavior test

## 10. Open Issues
- Source sustain branch를 언제 discrete emit channel로 통합할지.
- `RadiusMultiplier`를 profile spawn tuning 1차 범위에 포함할지.
- `BulletDefinitionSO.CaptureRule`을 profile override 대상으로 열지.
- radial spawn offset/radius 표현을 `PositionPattern`에 추가할지.
- `PlayerHit`, `LifetimeExpired`, `StageBlocked` lifecycle trigger를 언제 열지.
