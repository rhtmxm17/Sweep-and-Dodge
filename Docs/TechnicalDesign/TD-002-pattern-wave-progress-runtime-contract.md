# Pattern / Wave / Progress 런타임 계약

## Metadata
- doc_id: `TD-002`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-07-03`
- related_docs:
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-033-emission-profile-common-schema.md](./TD-033-emission-profile-common-schema.md)
- related_adr:
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

> Current runtime contract for `WaveClipSO -> SourceClipPatternBuffer -> request -> spawn apply`.

## 1. Goals
- Fix the current WaveClip runtime data path.
- Clarify request item identity and event-local snapshot ownership.
- Keep Source wrapper data and `EmissionProfileSO` grammar ownership separate.

## 2. Runtime SSOT
```text
WaveClipSO.Directives[]
  -> WaveClipAuthoringResolver
  -> ResolvedWaveSpawnDirectiveSnapshot
  -> SourceClipPatternBuffer
  -> SourceSpawnRequestBuffer / DiscreteEmitRequestBuffer
  -> common spawned bullet apply
```

- `WaveClipAuthoringResolver` resolves directive source wrapper data and `EmissionProfileSO` data into one snapshot.
- `SourceClipPatternBuffer` stores the flattened runtime pattern for active source clips.
- Source sustain spawning uses `SourceSpawnRequestBuffer`.
- Source discrete events use `DiscreteEmitRequestBuffer`.
- Both request paths pass profile-resolved tuning to the common spawned bullet apply helper.

## 3. `SourceClipPatternBuffer`
The buffer stores these categories:
- directive/clip identity
- clip duration and local active interval
- source phase, lane, trigger state
- `ProfileRefId`
- bullet type key
- speed/lifetime override flags and values
- movement override flag and movement parameters
- source emission mode, rate, cap, burst/repeat schedule
- sampling anchor and area sampler data
- profile-resolved position pattern data
- profile-resolved aim data
- profile-resolved shot pattern data
- event-local mutable state for runtime consume

## 4. Request Item Identity

### 4.1 RateField
- `RateField` is the source sustain path.
- It does not create discrete event identity.
- Runtime may merge directive-level sustain flow according to source lane/state rules.

### 4.2 Poisson / EventBurst
- Each event creates a separate request item.
- Event anchor, aim snapshot, and repeat sequence are event-local state.
- `Instant` and `Timed` share the same event identity rules.

### 4.3 Count
```text
Count = remaining bullets in the request item
```

For discrete events:
```text
Count = EventRepeatCount x shot-pattern unit count
```

- `SpawnSequence` advances by repeat unit, not by individual bullet count.
- Atomic shot patterns are consumed as a unit.

## 5. Event-Local Snapshot Ownership
- Owner: request item buffer.
- Build owner:
  - Source sustain: `SourceClipRequestBuildSystem`
  - Source discrete event: `SourceClipDiscreteEmitBuildSystem`
- Consume owner:
  - Source sustain: `SpawnRequestRoundRobinExecutionSystem`
  - Source discrete event: `DiscreteEmitExecutionSystem`

Event-local mutable state:
- event anchor initialized flag
- event anchor position
- event aim initialized flag
- event aim target position
- event shot elapsed time
- spawn sequence

## 6. Consume Semantics

### 6.1 Sampling / Position
- Sampling resolves the event anchor once per event.
- Position pattern resolves repeat origin from that event anchor.
- `PlayerNoSpawnRadius` and sampling retry budget apply only during event anchor resolve.

### 6.2 Aim / Shot
- Aim resolves the base firing direction.
- Shot pattern expands one repeat into shot slots.
- `NWay` and `Radial` are atomic consume units.
- `PlayerPositionAim(EventStart)` fixes the target once per event.
- `PlayerPositionAim(PerShot)` retargets on each repeat consume.

### 6.3 Timed / Instant
- `Instant` consumes as many repeat units as budget and pool availability allow.
- `Timed` consumes repeat units according to `EventShotIntervalSec`.

## 7. Update Order / Ownership
```text
ExecutionBegin -> Simulation -> Request -> ExecutionEnd
```

- request build happens before request consume in the relevant execution group.
- request consume owns event-local mutable state mutation.
- spawn execution owns pool dequeue and spawned bullet state apply.
- despawn execution owns pool return.

## 8. Validation
- `WaveClip` segment duration must produce a positive active interval.
- directive `Profile`, `Emission`, `Sampling`, `Sampling.Anchor`, and `Sampling.AreaSampler` are required.
- source emission numeric values must satisfy mode-specific ranges.
- source sampling numeric values must satisfy sampler-specific ranges.
- managed reference nodes inside one `WaveClip` must not be shared across directives.
- profile reference, payload, pattern, movement, and lifecycle graph validation follows `TD-033`.

## 9. Tests / Acceptance Criteria
- request build regression
- source sustain request consume regression
- source discrete event request regression
- timed event anchor fixation
- player-position aim fixation/retarget behavior
- atomic `NWay` / `Radial` consume behavior
- profile speed/lifetime/movement override apply behavior
- compile success
- Unity Console error 0
- relevant EditMode tests
- dedicated PlayMode smoke

## 10. Progress / Stage Notes
- HazardStack and progress multiplier contracts remain in their own TDs.
- This document is the Wave/Spawn runtime contract only.
