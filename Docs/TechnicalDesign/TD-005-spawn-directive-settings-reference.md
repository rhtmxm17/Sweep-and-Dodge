# SpawnDirective 설정 레퍼런스 (Canonical Authoring 기준)

## Metadata
- doc_id: `TD-005`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-06`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
- related_adr:
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)

> 목적: 현재 `WaveClipSO.Segments[].Directives[]` typed authoring과 canonical snapshot이 실제로 무엇을 뜻하는지 빠르게 확인하는 운영 레퍼런스.

## 1. 적용 범위
- 대상:
  - `Payload`
  - `Emission`
  - `Sampling`
  - `PositionPattern`
  - `Aim`
  - `ShotPattern`
- 비대상:
  - historical ADR의 과거 schema 상세

## 2. Payload
| 필드 | 의미 | 규약 |
| --- | --- | --- |
| `Payload.Bullet` | 사용할 `BulletDefinitionSO` | null이면 유효 directive가 아님 |

## 3. Emission
| 필드 | 의미 | 규약 |
| --- | --- | --- |
| `Emission.EmissionMode` | 시간 분포 모드 | `RateField` / `Poisson` / `EventBurst` |
| `Emission.SpawnMode` | 활성 상한 정책 | `FixedDensity` / `CapAndMaxDensity` |
| `Emission.MaxActiveDensityPerArea` | 캡 상한 밀도 | `CapAndMaxDensity`에서 사용 |
| `Emission.RatePerSecPerArea` | 면적당 초당 밀도 | `RateField`에서 사용 |
| `Emission.MeanEventsPerSec` | 평균 이벤트율 | `Poisson`에서 `>= 0` |
| `Emission.BurstRepeatCount` | burst 반복 횟수 | `EventBurst`: `-1` 또는 `>= 1` |
| `Emission.BurstIntervalSec` | burst 간격 | `EventBurst`: `> 0` |
| `Emission.EventRepeatCount` | event 내부 repeat 횟수 | `Poisson` / `EventBurst`: `> 0` |
| `Emission.EventShotSchedule` | repeat 시간축 | `Instant` / `Timed` |
| `Emission.EventShotIntervalSec` | repeat 간격 | `Timed`에서 `> 0` |

### 3.1 해석 규약
- `Poisson` / `EventBurst`는 event 단위 request item을 만든다.
- `Instant`와 `Timed` 모두 event anchor / event aim snapshot을 event 범위에서 고정한다.
- 총 탄 수:
```text
EventRepeatCount × ShotPattern 1회당 탄 수
```

## 4. Sampling
| 필드 | 의미 | 규약 |
| --- | --- | --- |
| `Sampling.Anchor` | event anchor 중심 계산 방식 | `SourceCenter` / `FixedPoint` / `PlayerRelative` |
| `Sampling.AreaSampler` | 중심 주변의 area sampling 방식 | `CenterPoint` / `UniformField` / `PollutionTopK` |
| `Sampling.SpawnSampleBudget` | sampling 재시도 예산 | `> 0`, 기본 16 |
| `Sampling.PlayerNoSpawnRadius` | 플레이어 주변 금지 반경 | `>= 0` |

### 4.1 Anchor subtype
| 타입 | 의미 | 추가 필드 |
| --- | --- | --- |
| `SourceCenterSamplingAnchorAuthoring` | source center 사용 | 없음 |
| `FixedPointSamplingAnchorAuthoring` | 월드 고정 중심 사용 | `FixedPoint` |
| `PlayerRelativeSamplingAnchorAuthoring` | 플레이어 기준 중심 사용 | `SpawnOffset` |

### 4.2 AreaSampler subtype
| 타입 | 의미 |
| --- | --- |
| `CenterPointAreaSamplerAuthoring` | anchor 자체를 event anchor로 사용 |
| `UniformFieldAreaSamplerAuthoring` | 필드 내부 균등 샘플링 |
| `PollutionTopKAreaSamplerAuthoring` | pollution top-k 기반 샘플링 |

### 4.3 해석 규약
- `SpawnSampleBudget`와 `PlayerNoSpawnRadius`는 event anchor resolve에만 적용한다.
- 이후 `PositionPattern`이 만든 repeat origin에는 재적용하지 않는다.
- `UniformField` / `PollutionTopK`는 pollution runtime이 있으면 effective area를 사용한다.

## 5. PositionPattern
| 타입 | 의미 | 필드 |
| --- | --- | --- |
| `SinglePointPositionPatternAuthoring` | anchor 그대로 사용 | 없음 |
| `LineEvenPositionPatternAuthoring` | line 슬롯 분포 | `LineStart`, `LineEnd`, `SampleSpacing` |
| `PointSetPositionPatternAuthoring` | authored point set 분포 | `Points[]` |

### 5.1 해석 규약
- `LineEven` / `PointSet`은 shot-local origin pattern이다.
- `LineEven` / `PointSet`은 더 이상 sampling 축이 아니다.
- `PointSet` authored `Points[]`는 runtime snapshot에서 최대 4개로 clamp한다.
- repeat sequence가 `LineEven` / `PointSet` 슬롯 선택 기준이다.

## 6. Aim
| 타입 | 의미 | 필드 |
| --- | --- | --- |
| `RandomAimAuthoring` | 랜덤 각도 | 없음 |
| `FixedAimAuthoring` | 고정 각도 | `BaseAngleDeg` |
| `SpiralAimAuthoring` | repeat마다 누적 회전 | `BaseAngleDeg`, `SpiralStepDeg` |
| `PlayerPositionAimAuthoring` | 플레이어 위치를 향함 | `AngleOffsetDeg`, `SnapshotTiming` |

### 6.1 해석 규약
- `Spiral`은 aim 축이다.
- `PlayerPositionAim`은 `SnapshotTiming=EventStart | PerShot`를 지원한다.
- `EventStart`는 discrete event 범위에서 aim target을 고정한다.
- `PerShot`는 repeat consume 시점마다 현재 player world position으로 retarget한다.
- target source는 player world position만 사용한다.

## 7. ShotPattern
| 타입 | 의미 | 필드 |
| --- | --- | --- |
| `SingleShotPatternAuthoring` | 1발 | 없음 |
| `NWayShotPatternAuthoring` | 다방향 세트 | `ShotCount >= 2` |
| `RadialShotPatternAuthoring` | 원형 세트 | `ShotCount >= 2` |

### 7.1 해석 규약
- `NWay`와 `Radial`은 모두 atomic consume이다.
- 하나의 repeat에서 세트를 다 소비하지 못하면 repeat 전체를 이월한다.
- `SpawnSequence`는 bullet 수가 아니라 repeat 단위로 증가한다.

## 8. Canonical validation 기준
| 코드 | 의미 |
| --- | --- |
| `CV022` | `Poisson` / `EventBurst`의 `EventRepeatCount <= 0` |
| `CV023` | `NWay ShotPattern`의 `ShotCount < 2` |
| `CV024` | `Radial ShotPattern`의 `ShotCount < 2` |
| `CV026` | `LineEven PositionPattern` 파라미터 오류 |
| `CV028` | `PointSet PositionPattern`의 포인트 개수 오류 |
| `CVW032` | `SpiralAim`의 near-zero `SpiralStepDeg` |
| `CVW033` | `PointSet` authored 포인트 수가 runtime clamp 상한 초과 |

## 9. 현재 runtime 적용 메모
- canonical snapshot은 `SourceClipPatternBuffer`와 `SourceSpawnRequestBuffer`에 내려간다.
- runtime/product code는 canonical field만 유지한다.
- `SourceSpawnRequestBuffer`가 event-local snapshot owner다.

## 10. 변경 이력
- 2026-04-06: Plan E 반영. runtime/product code가 canonical field만 유지하는 상태로 문서 메모를 보정했다.
- 2026-04-06: Plan F 반영. `PlayerPositionAim`의 `PerShot` retarget 지원과 validation 제약 제거를 문서에 반영했다.
- 2026-04-06: Plan D 반영. canonical authoring 축과 validation 용어 기준으로 문서를 전면 정리했다.
- 2026-04-03: typed-only authoring과 resolver snapshot 경로를 문서에 반영했다.
