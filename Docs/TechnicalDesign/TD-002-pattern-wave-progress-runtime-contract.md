# Pattern / Wave / Progress 런타임 계약

## Metadata
- doc_id: `TD-002`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-06`
- related_adr:
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

> 목적: 현재 `WaveClip` -> request -> execution 경로의 runtime 계약을 canonical authoring 축 기준으로 고정한다.

## 1. 목표
- `WaveClipSO` authoring이 runtime buffer와 어떻게 연결되는지 고정한다.
- request item identity와 event-local snapshot ownership을 명확히 한다.
- validation / test / 운영 문서가 같은 runtime 의미를 공유하도록 한다.

## 2. Runtime SSOT
```text
WaveClipSO.Directives[]
  -> WaveClipAuthoringResolver
  -> ResolvedWaveSpawnDirectiveSnapshot
  -> SourceClipPatternBuffer
  -> SourceSpawnRequestBuffer
  -> SpawnRequestRoundRobinExecutionSystem
```

### 2.1 Canonical runtime field
- `SourceClipPatternBuffer`
  - `SamplingAnchorMode`
  - `AreaSamplerMode`
  - `PositionPatternMode`
  - `AimMode`
  - `AimSnapshotTiming`
  - `AimAngleOffsetDeg`
  - `ShotPatternMode`
  - `ShotCount`
  - `EventRepeatCount`
- `SourceSpawnRequestBuffer`
  - 위 canonical field를 복사한다.
  - event-local mutable state도 함께 가진다.

### 2.2 Compat field 정책
- compat field mirror는 Plan E에서 제거됐다.
- runtime/product code는 canonical field만 유지한다.
- 테스트가 필요하면 test-local canonical helper로만 request / pattern을 구성한다.

## 3. Request item identity

### 3.1 RateField
- directive 단위 merge를 유지한다.
- discrete event identity를 만들지 않는다.

### 3.2 Poisson / EventBurst
- event마다 별도 `SourceSpawnRequestBuffer` item을 만든다.
- `Instant`여도 event끼리 merge하지 않는다.
- 이유:
  - event anchor 고정
  - player aim snapshot 고정
  - repeat sequence 고정

### 3.3 Count 의미
```text
Count = request item에 남아 있는 bullet 수
```

- discrete event item:
```text
Count = EventRepeatCount × ShotPattern 1회당 탄 수
```
- `SpawnSequence`는 bullet 수가 아니라 repeat 단위로 증가한다.

## 4. Event-local snapshot ownership
- owner: `SourceSpawnRequestBuffer`
- 생성:
  - `SourceClipRequestBuildSystem`
- mutation / consume:
  - `SpawnRequestRoundRobinExecutionSystem`
- 그 외 시스템:
  - read-only 또는 무관

### 4.1 Event-local mutable state
- `EventAnchorInitialized`
- `EventAnchorPosition`
- `EventAimInitialized`
- `EventAimTargetPosition`
- `EventShotElapsedSec`
- `SpawnSequence`

### 4.2 고정 규약
- `Poisson` / `EventBurst`:
  - event anchor는 첫 consume 시 1회 resolve
  - 같은 event의 repeat는 `EventAnchorPosition` 재사용
  - `PlayerPositionAim(EventStart)`는 첫 consume 시 `EventAimTargetPosition`을 잡고 재사용
  - `PlayerPositionAim(PerShot)`는 repeat consume 시점마다 현재 player world position을 다시 읽는다
- `Instant`와 `Timed`는 같은 고정 규칙을 공유한다.
  - 차이는 repeat 간 시간 간격뿐이다.

## 5. Consume semantics

### 5.1 Sampling / PositionPattern
- `Sampling`은 event anchor 1회 결정 책임만 가진다.
- `PositionPattern`은 event anchor 기준 repeat origin 분포 책임만 가진다.
- `PlayerNoSpawnRadius` / `SpawnSampleBudget`는 sampling 단계에만 적용한다.

### 5.2 Aim / ShotPattern
- `Aim`은 base angle 계산 책임만 가진다.
- `ShotPattern`은 repeat 1회가 만드는 슬롯 구조 책임만 가진다.
- `NWay` / `Radial`은 모두 atomic consume이다.
- `Spiral + NWay`, `Spiral + Radial`, `PlayerPosition + NWay`, `PlayerPosition + Radial` 조합을 지원한다.

### 5.3 Timed vs Instant
- `Instant`
  - 한 프레임 안에서 budget / pool이 허용하는 만큼 repeat를 연속 consume
- `Timed`
  - `EventShotIntervalSec` 간격으로 repeat를 1회씩 consume
- 공통:
  - event anchor는 event 범위에서 고정
  - `PlayerPositionAim(EventStart)`는 event aim snapshot을 고정한다
  - `PlayerPositionAim(PerShot)`는 repeat마다 현재 player world position으로 retarget한다

## 6. 변경 이력
- 2026-04-06: Plan E 반영. compat runtime mirror 필드와 canonical fallback 경로를 제거하고 runtime/product code를 canonical-only로 고정했다.
- 2026-04-06: Plan D / Plan C 반영. event-local snapshot ownership과 canonical runtime contract를 문서화했다.

## 6. 업데이트 순서 / 소유권
- Group 의미:
```text
ExecutionBegin -> Simulation -> Request -> ExecutionEnd
```
- 관련 owner:
  - request build: `SourceClipRequestBuildSystem`
  - request consume: `SpawnRequestRoundRobinExecutionSystem`
  - despawn / pool 반납: existing ExecutionEnd owner
- Plan C 기준으로 request/event-local snapshot mutation owner는 `ExecutionBegin` 하나로 고정됐다.

## 7. Validation 기준

### 7.1 Structural validation
- `CV040`
- 대상:
  - `Emission`
  - `Sampling`
  - `Sampling.Anchor`
  - `Sampling.AreaSampler`
  - `PositionPattern`
  - `Aim`
  - `ShotPattern`

### 7.2 Semantic validation
- `CV015`: `RatePerSecPerArea < 0`
- `CV016`: `CapAndMaxDensity`인데 `MaxActiveDensityPerArea < 0`
- `CV017`: `MeanEventsPerSec < 0`
- `CV018`: `SpawnSampleBudget <= 0`
- `CV019`: `PlayerNoSpawnRadius < 0`
- `CV020`: `BurstIntervalSec <= 0`
- `CV021`: invalid `BurstRepeatCount`
- `CV022`: `Poisson` / `EventBurst`의 `EventRepeatCount <= 0`
- `CV023`: `NWay ShotPattern`의 `ShotCount < 2`
- `CV024`: `Radial ShotPattern`의 `ShotCount < 2`
- `CV025`: `Timed`인데 `EventShotIntervalSec <= 0`
- `CV026`: invalid `LineEven PositionPattern`
- `CV028`: invalid `PointSet PositionPattern`
- `CVW032`: near-zero `SpiralStepDeg`
- `CVW033`: `PointSet` authored count > runtime clamp max

## 8. 테스트 / 합격 기준

### 8.1 최소 회귀 세트
- validation code regression
- request build regression
- event item split regression
- timed event anchor fixation
- `PlayerPositionAim(EventStart)` fixation
- `PlayerPositionAim(PerShot)` retarget
- `NWay` / `Radial` atomic consume
- `LineEven` / `PointSet` repeat-sequence origin selection

### 8.2 최종 검증
- compile success
- console error 0
- EditMode 전체 pass
- dedicated PlayMode smoke pass

## 9. Progress / Stage 쪽 주의점
- `HazardStack` 및 progress multiplier 계약은 기존 문서를 유지한다.
- 이번 문서는 Wave / Spawn runtime contract만 SSOT로 다룬다.

## 10. 변경 이력
- 2026-04-06: Plan F 반영. `PlayerPositionAim`의 `PerShot` retarget을 허용하고 관련 validation 제약(`CV041`)을 제거했다.
- 2026-04-06: Plan D 반영. canonical runtime field, event item split, event-local snapshot ownership, validation 기준을 현재 구현 상태로 전면 정리했다.
- 2026-04-06: Plan C 반영. `SourceSpawnRequestBuffer`를 event-local snapshot owner로 고정하고 `Poisson` / `EventBurst` event item split을 도입했다.
- 2026-04-03: typed-only authoring / resolver snapshot 경로를 runtime 계약에 반영했다.
