# SpawnDirective 모델 (Emission / Sampling / PositionPattern / Aim / ShotPattern / Payload)

## Metadata
- doc_id: `TD-003`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-06`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-005-spawn-directive-settings-reference.md](./TD-005-spawn-directive-settings-reference.md)
- related_adr:
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)

> 목적: `WaveClipSO.Segments[].Directives[]`의 현재 authoring SSOT를 canonical 축 기준으로 설명한다.

## 1. 목표
- `WaveSpawnEntryAuthoring`의 책임 경계를 고정한다.
- `WaveClipAuthoringResolver`가 어떤 의미를 canonical snapshot으로 내리는지 명확히 한다.
- 이후 runtime/validation/test가 같은 authoring 의미를 공유하도록 한다.

## 2. 현재 모델
```text
WaveSpawnEntryAuthoring
  = Payload
  × Emission
  × Sampling
  × PositionPattern
  × Aim
  × ShotPattern
```

- authoring SSOT:
  - `WaveClipSO.Segments[].Directives[]`
- 공통 해석 SSOT:
  - `WaveClipAuthoringResolver`
  - `ResolvedWaveSpawnDirectiveSnapshot`
- runtime flatten:
  - `SourceClipPatternBuffer`
  - `SourceSpawnRequestBuffer`
  - canonical field만 직접 사용한다.
- authoring invariant:
  - 하나의 `WaveClipSO` 안에서 서로 다른 `Segment` / `Directive`가 `SerializeReference` managed node를 공유하면 invalid다.

## 2.1 ClipSegment 시간 authoring
- `WaveClipSO.ClipSegment`는 `StartSec + DurationSec + Directives[]`를 사용한다.
- authoring은 end 시각을 저장하지 않는다.
- effective segment end:
  - `StartSec + DurationSec`
  - `clip.DurationSec > 0`이면 runtime flatten에서는 clip end로 clamp된다.
- runtime flatten은 계속 `LocalStartSec`, `LocalEndSec`를 사용한다.

## 3. 축별 책임

### 3.1 Payload
- 역할: 어떤 탄 정의를 사용할지 결정한다.
- 현재 필드:
  - `Payload.Bullet`

### 3.2 Emission
- 역할: event가 언제 발생하는지, event 내부 repeat가 몇 번인지 결정한다.
- subtype:
  - `RateFieldEmissionAuthoring`
  - `PoissonEmissionAuthoring`
  - `EventBurstEmissionAuthoring`
- 핵심 필드:
  - 공통: `SpawnMode`, `MaxActiveDensityPerArea`
  - `RateField`: `RatePerSecPerArea`
  - `Poisson`: `MeanEventsPerSec`, `EventRepeatCount`, `EventShotSchedule`, `EventShotIntervalSec`
  - `EventBurst`: `BurstRepeatCount`, `BurstIntervalSec`, `EventRepeatCount`, `EventShotSchedule`, `EventShotIntervalSec`
- 규약:
  - `EventRepeatCount`는 `Poisson` / `EventBurst` 전용이다.
  - `BurstShotsPerEvent`는 authoring schema에서 제거됐다.

### 3.3 Sampling
- 역할: event anchor를 어디에 둘지 1회 결정한다.
- 현재 형태:
  - `WaveSamplingAuthoring`
  - `Anchor + AreaSampler + SpawnSampleBudget + PlayerNoSpawnRadius`
- subtype:
  - Anchor:
    - `SourceCenterSamplingAnchorAuthoring`
    - `FixedPointSamplingAnchorAuthoring`
    - `PlayerRelativeSamplingAnchorAuthoring`
  - AreaSampler:
    - `CenterPointAreaSamplerAuthoring`
    - `UniformFieldAreaSamplerAuthoring`
    - `PollutionTopKAreaSamplerAuthoring`
- 규약:
  - `SpawnSampleBudget`와 `PlayerNoSpawnRadius`는 event anchor resolve에만 적용한다.
  - 같은 event 안에서는 sampling 결과를 재사용한다.

### 3.4 PositionPattern
- 역할: event anchor 기준으로 repeat별 spawn origin을 어떻게 배치할지 결정한다.
- subtype:
  - `SinglePointPositionPatternAuthoring`
  - `LineEvenPositionPatternAuthoring`
  - `PointSetPositionPatternAuthoring`
- 규약:
  - `LineEven` / `PointSet`은 더 이상 sampling 축이 아니다.
  - `PointSet` authoring은 `Points[]`를 사용하고 runtime snapshot은 최대 4포인트로 clamp한다.

### 3.5 Aim
- 역할: 각 repeat의 base firing angle을 어떻게 계산할지 결정한다.
- subtype:
  - `RandomAimAuthoring`
  - `FixedAimAuthoring`
  - `LineNormalAimAuthoring`
  - `SpiralAimAuthoring`
  - `PlayerPositionAimAuthoring`
- 규약:
  - `Spiral`은 aim 축이다.
  - `LineNormalAim`은 `PositionPattern.LineEven`의 tangent normal을 base angle로 사용한다.
  - `LineNormalAim.NormalSide = Left | Right`는 `LineStart -> LineEnd` 기준 좌/우 법선을 뜻한다.
  - `LineNormalAim.AngleOffsetDeg = 0`이면 선분 법선 방향 그대로 발사되고, 양수/음수는 선택한 법선을 기준으로 추가 회전한다.
  - `LineNormalAim`은 `LineEven PositionPattern`에서만 valid하다.
  - `PlayerPositionAim`은 `player world position`만 target source로 사용한다.
  - `SnapshotTiming=EventStart`는 event 범위에서 aim target을 고정한다.
  - `SnapshotTiming=PerShot`는 repeat consume 시점마다 현재 player world position으로 retarget한다.

### 3.6 ShotPattern
- 역할: repeat 1회가 몇 발을 어떤 슬롯 구조로 만드는지 결정한다.
- subtype:
  - `SingleShotPatternAuthoring`
  - `NWayShotPatternAuthoring`
  - `RadialShotPatternAuthoring`
- 규약:
  - `ShotCount`는 `NWay` / `Radial`에서 사용한다.
  - `AngleSpacingDeg`는 `NWay`에서만 사용하며, 인접 fan 슬롯 간 각도 간격을 뜻한다.
  - `NWay`와 `Radial`은 runtime에서 모두 atomic consume이다.
  - `NWay`는 기준 방향 중심 fan spread다.
  - `Radial`은 기준 방향 기준 full-circle 분배다.

## 4. 핵심 의미 규약

### 4.1 이벤트 고정 규약
- `Poisson` / `EventBurst`는 event 단위 request item을 생성한다.
- `Instant`와 `Timed` 모두 event anchor / event aim snapshot을 event 범위에서 고정한다.
- 차이는 시간 간격뿐이다.

### 4.2 총 탄 수 해석
```text
총 탄 수 = EventRepeatCount × ShotPattern 1회당 탄 수
```

- `RateField`는 discrete event가 아니므로 `EventRepeatCount`를 사용하지 않는다.
- `Poisson` / `EventBurst`는 event가 먼저 결정되고, 그 안에서 `EventRepeatCount`와 `ShotPattern`이 탄 수를 결정한다.

### 4.3 PositionPattern / Aim / ShotPattern 조합
- `PositionPattern`은 origin 배치 책임만 가진다.
- `Aim`은 base angle 계산 책임만 가진다.
- `ShotPattern`은 슬롯 확장 책임만 가진다.
- 예:
  - `LineEven + FixedAim + NWay`
  - `PointSet + SpiralAim + Single`
  - `SinglePoint + PlayerPositionAim + Radial`

## 5. 구현 SSOT
- authoring:
  - `WaveClipSO`
- canonical resolve:
  - `WaveClipAuthoringResolver`
  - `ResolvedWaveSpawnDirectiveSnapshot`
- runtime:
  - `SourceClipPatternBuffer`
  - `SourceSpawnRequestBuffer`
- validation:
  - `ContentValidationRules`

## 6. 합격 기준
- authoring 설명에서 `Direction` 단일 축을 더 이상 SSOT로 쓰지 않는다.
- `Sampling`과 `PositionPattern`이 분리된 상태로 설명된다.
- `BurstShotsPerEvent` 대신 `EventRepeatCount`를 기준 용어로 사용한다.
- `PlayerPositionAim(EventStart / PerShot)` 계약이 문서와 구현에서 일치한다.
- `WaveClip` authoring graph는 shared `SerializeReference` 없이 unique ownership으로 유지된다.

## 7. 변경 이력
- 2026-04-06: Plan E 반영. runtime/product code의 compat field mirror 제거 상태를 문서 기준에도 반영했다.
- 2026-04-06: Plan I 반영. `LineNormalAim`을 추가하고 `LineEven PositionPattern` 전용 line-normal base angle 계약을 문서화했다.
- 2026-04-06: Plan F 반영. `PlayerPositionAim`이 `EventStart`와 `PerShot` snapshot timing을 모두 지원하도록 모델 설명을 갱신했다.
- 2026-04-06: Plan D 반영. canonical 축(`Emission / Sampling / PositionPattern / Aim / ShotPattern / Payload`) 기준으로 문서를 전면 정리했다.
- 2026-04-03: 2차 정리 반영. typed-only authoring SSOT로 고정했다.
- 2026-04-03: 1차 정리 반영. `Directives[]` typed authoring과 resolver snapshot 경로를 도입했다.
