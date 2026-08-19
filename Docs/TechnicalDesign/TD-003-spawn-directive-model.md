# Spawn Directive Model

## Metadata
- doc_id: `TD-003`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-07-03`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-033-emission-profile-common-schema.md](./TD-033-emission-profile-common-schema.md)
- related_adr:
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)

> `WaveClipSO.Segments[].Directives[]`의 현재 authoring contract. Wave directive는 source timeline/sampling wrapper이고, common bullet grammar는 `EmissionProfileSO`가 소유한다.

## 1. Goals
- `WaveSpawnEntryAuthoring`의 현재 책임 경계를 고정한다.
- `WaveClipAuthoringResolver`가 directive를 runtime snapshot으로 내리는 기준을 설명한다.
- Source 전용 emission/sampling과 공통 profile grammar의 소유권을 분리한다.

## 2. Current Model
```text
WaveSpawnEntryAuthoring
  Profile
  Emission
  Sampling
```

- `Profile`
  - 필수 `EmissionProfileSO` 참조다.
  - bullet payload, spawn tuning, movement tuning, position pattern, aim, shot pattern, lifecycle trigger를 제공한다.
- `Emission`
  - source event generation과 density/cap을 제공한다.
  - mode는 `RateField`, `Poisson`, `EventBurst`다.
- `Sampling`
  - source field에서 event anchor를 고르는 방식을 제공한다.
  - anchor와 area sampler를 포함한다.

## 3. Clip Segment
- `WaveClipSO.ClipSegment`는 `StartSec`, `DurationSec`, `Directives[]`를 가진다.
- authoring은 end time을 저장하지 않는다.
- effective segment end는 `StartSec + DurationSec`다.
- `clip.DurationSec > 0`이면 runtime flatten에서 clip end로 clamp한다.
- runtime buffer는 `LocalStartSec`, `LocalEndSec`를 사용한다.

## 4. Source Emission
- `RateField`
  - continuous source sustain spawn을 표현한다.
  - directive 단위로 merge 가능한 흐름이며 discrete event identity를 만들지 않는다.
- `Poisson`
  - 평균 event rate를 기준으로 discrete event request를 만든다.
  - event 내부 repeat는 `EventRepeatCount`, `EventShotSchedule`, `EventShotIntervalSec`가 제어한다.
- `EventBurst`
  - segment 안에서 burst repeat를 만들고, 각 event 내부 repeat는 Poisson과 같은 규칙을 따른다.
- `SpawnMode`
  - source active density/cap 정책을 정한다.

## 5. Sampling
- `Sampling.Anchor`
  - `SourceCenter`
  - `FixedPoint`
  - `PlayerRelative`
- `Sampling.AreaSampler`
  - `CenterPoint`
  - `UniformField`
  - `PollutionTopK`
- `SpawnSampleBudget`와 `PlayerNoSpawnRadius`는 event anchor resolve 단계에만 적용한다.
- 같은 discrete event 안에서는 resolved event anchor를 재사용한다.

## 6. Resolver Output
`WaveClipAuthoringResolver.TryResolveTypedEntry`는 directive를 `ResolvedWaveSpawnDirectiveSnapshot`으로 변환한다.

```text
ResolvedWaveSpawnDirectiveSnapshot
  EmissionCore
  Bullet
  Source emission fields
  Sampling fields
  Profile-resolved position / aim / shot fields
```

- `EmissionCore`는 `EmissionProfileResolver`가 만든 `ResolvedEmissionCore`다.
- `Bullet`은 profile payload에서 온다.
- source emission/sampling fields는 directive wrapper에서 온다.
- position/aim/shot fields는 profile에서 온 값을 snapshot에 복사한다.

## 7. Runtime Output
`SourceRuntimeApplyUtility`는 resolved snapshot을 `SourceClipPatternBuffer`로 flatten한다.

`SourceClipPatternBuffer`는 아래 범주의 값을 함께 가진다.
- clip identity and local timing
- phase/lane/trigger state
- `ProfileRefId`
- bullet type key
- profile spawn tuning override
- profile movement tuning override
- source emission mode/rate/cap
- source sampling mode and sampling parameters
- profile-resolved position/aim/shot fields
- event-local mutable state for runtime consume

## 8. Validation
- directive `Profile`은 필수다.
- directive `Emission`, `Sampling`, `Sampling.Anchor`, `Sampling.AreaSampler`는 필수다.
- `WaveClip` 내부에서 managed reference node를 segment/directive 사이에 공유하면 invalid다.
- source emission/sampling 값은 source wrapper validation이 담당한다.
- profile payload/pattern/lifecycle graph validation은 `TD-033` 기준을 따른다.

## 9. Acceptance Criteria
- Wave directive authoring 설명에서 payload, position pattern, aim, shot pattern을 directive inline 책임으로 설명하지 않는다.
- Source-only emission/sampling과 common profile grammar가 문서상 분리되어 있다.
- runtime snapshot 설명은 `ResolvedEmissionCore`와 `ProfileRefId`를 포함한다.
