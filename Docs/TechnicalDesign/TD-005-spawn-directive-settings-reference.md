# SpawnDirective 설정 레퍼런스 (Authoring 기준)

## Metadata
- doc_id: `TD-005`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-27`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness.md](../ADR/ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness.md)
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md](../ADR/ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)

> 목적: `WaveClipSO.Segments[].Entries[]`(SpawnEntry)의 각 설정이 실제 런타임에서 무엇을 의미하는지 빠르게 확인하는 운영 레퍼런스.

## 1. 적용 범위
- 대상: `WaveClipSO.Segments[].Entries[]` 인라인 프로필
  - `Payload`
  - `Emission`
  - `Sampling`
  - `Direction`
- 비대상:
  - 레거시 fallback 경로(`UseDirectiveProfiles` 등, 제거됨)
  - `WallEven` 및 전용 데이터(`WallMask`, `WallInset`, 제거됨)

## 2. Payload 설정
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `Payload.Bullet` | 스폰할 탄 정의(SO) 참조 | null이면 유효 엔트리로 처리되지 않음 |

## 3. Emission 설정
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `Emission.EmissionMode` | 시간 분포 모드 | `RateField` / `Poisson` / `EventBurst` |
| `Emission.SpawnMode` | 활성 상한 정책 | `FixedDensity` 또는 `CapAndMaxDensity` |
| `Emission.RatePerSecPerArea` | 면적당 초당 스폰 밀도 | `RateField`에서 사용 |
| `Emission.MeanEventsPerSec` | 평균 이벤트율(lambda) | `Poisson`에서 사용, `>= 0` |
| `Emission.BurstRepeatCount` | 이벤트 반복 횟수 | `EventBurst`: `-1`(무한) 또는 `>=1` |
| `Emission.BurstIntervalSec` | 이벤트 간격(초) | `EventBurst`: `> 0` |
| `Emission.BurstShotsPerEvent` | 사건형 이벤트 1회 샷 수 | `Poisson` / `EventBurst`: `>= 1` |
| `Emission.EventShotSchedule` | 이벤트 내부 샷 스케줄 | `Poisson` / `EventBurst`: `Instant` / `Timed` (합의, 구현 예정) |
| `Emission.EventShotIntervalSec` | 이벤트 내부 샷 간격(초) | `Poisson` / `EventBurst`에서 `EventShotSchedule=Timed`일 때 `> 0` (합의, 구현 예정) |
| `Emission.MaxActiveDensityPerArea` | 활성 상한 밀도 | `CapAndMaxDensity`에서 사용 |

### 3.1 EmissionMode 값 의미
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `RateField` | 0 | 면적당 초당 비율로 누적 스폰 | `RatePerSecPerArea` 사용 |
| `Poisson` | 1 | 평균 이벤트율 기반 확률 스폰 | `MeanEventsPerSec` 사용, 이벤트 확정 후 `EventShotSchedule` 적용 가능 |
| `EventBurst` | 2 | 고정 간격 이벤트성 버스트 스폰 | carry 소비 정책 적용, `EventShotSchedule` 적용 가능 |

### 3.2 SpawnMode 값 의미
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `FixedDensity` | 0 | 밀도 규칙대로 스폰, 활성 상한 캡 없음 | 순수 밀도 기반 |
| `CapAndMaxDensity` | 1 | 활성 수 + pending을 포함한 상한 캡 적용 | `MaxActiveDensityPerArea` 사용 |

### 3.3 EventBurst 소비 해석
- 요청 생성은 Request 단계에서 누적된다.
- 실행은 ExecutionBegin에서 예산 기반으로 소비된다.
- 프레임에 다 못 쓴 샷은 버리지 않고 다음 프레임으로 이월(carry)된다.

### 3.4 사건형 이벤트 모드 지속 확장 (합의, 구현 예정)
- 이벤트 내부 타임라인은 `Emission` 책임으로 정의한다.
- 적용 대상은 `Poisson` / `EventBurst`다.
- `EventShotSchedule=Instant`:
  - 기존과 동일하게 이벤트 1회에서 샷을 즉시 소비한다.
- `EventShotSchedule=Timed`:
  - 이벤트 1회 내부에서 `EventShotIntervalSec` 간격으로 `BurstShotsPerEvent` 샷을 분할 소비한다.
- `Poisson`은 이벤트 발생 시점만 확률적으로 결정되고, 발생이 확정된 이후 샷 소비 규약은 `EventBurst`와 동일하다.
- `BurstShotsPerEvent`는 이벤트 1회 내부 샷 횟수 의미를 유지한다.

## 4. Sampling 설정
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `Sampling.SamplingMode` | 위치 샘플링 모드 | `UniformField` / `PollutionTopK` / `LineEven` / `PointSet` |
| `Sampling.CenterMode` | 샘플링 중심 기준 | `SourceCenter` / `FixedPoint` / `PlayerRelative` |
| `Sampling.FixedPoint` | 고정 중심점 좌표 | `CenterMode=FixedPoint`에서 사용 |
| `Sampling.SpawnOffset` | 플레이어 기준 오프셋 | `CenterMode=PlayerRelative`에서 사용 |
| `Sampling.LineStart` | 선분 시작점(로컬 오프셋) | `LineEven`에서 사용 |
| `Sampling.LineEnd` | 선분 끝점(로컬 오프셋) | `LineEven`에서 사용 |
| `Sampling.SampleSpacing` | 등간격 샘플 간격 | `LineEven`에서 `> 0` |
| `Sampling.PointCount` | PointSet 포인트 개수 | `PointSet`에서 `1..4` |
| `Sampling.Point0..Point3` | PointSet 로컬 오프셋 포인트 | `Center + Point[i]` |
| `Sampling.SpawnSampleBudget` | 샘플 재시도 예산 | 플레이어 안전거리 필터 재시도 횟수 |
| `Sampling.PlayerNoSpawnRadius` | 플레이어 주변 금지 반경 | `>= 0` |

### 4.1 SamplingMode
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `UniformField` | 0 | 필드 내부 균등 무작위 샘플링 | 기본 무작위 분포 |
| `PollutionTopK` | 1 | Pollution 가중치 상위 후보 중심 샘플링 | 밀도 기반 분포 강화 |
| `LineEven` | 2 | `LineStart~LineEnd` 선분에서 등간격 샘플링 | 라인/벽 발사 표현에 사용 |
| `PointSet` | 4 | 사전 정의 포인트셋 기반 샘플링 | 최대 4포인트, round-robin |

### 4.2 CenterMode
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `SourceCenter` | 0 | Source 앵커를 중심으로 샘플링 | 기본값 |
| `FixedPoint` | 1 | 월드 고정 좌표를 중심으로 샘플링 | `FixedPoint` 사용 |
| `PlayerRelative` | 2 | 플레이어 위치 + 오프셋을 중심으로 샘플링 | `SpawnOffset` 사용 |

### 4.3 벽 발사 표현 규약
- 별도 `WallEven`은 사용하지 않는다.
- 벽 근처에 `LineStart/LineEnd`를 배치하고 `Direction`으로 진행 방향을 지정한다.

### 4.4 PointSet 규약
- 좌표계는 월드 절대값이 아니라 `CenterMode`로 계산된 중심 기준의 로컬 오프셋이다.
- 샘플 선택은 `SpawnSequence % PointCount` round-robin을 사용한다.
- `PointSet + Spiral/NWay/RadialBurst` 조합에서는 방향 시퀀스를 포인트별 로컬 시퀀스로 계산한다.
  - `localSequence = SpawnSequence / PointCount`
- `PlayerNoSpawnRadius`로 거부될 경우 다음 포인트로 순환 재시도하며, `SpawnSampleBudget` 한도 내에서만 수행한다.

### 4.5 이벤트 기준점 고정 규약 (합의, 구현 예정)
- 샘플링 기준점은 이벤트 시작 시 1회 확정하고, 이벤트 종료까지 고정한다.
- 이벤트 진행 중에는 Source/Player가 이동해도 기준점을 재샘플링하지 않는다(월드 고정).
- 고정 좌표는 이벤트 범위에만 유효하며, 다음 이벤트는 다시 샘플링한다.
- Sampling 실패 정책(`PlayerNoSpawnRadius`, `SpawnSampleBudget`)은 기존 규약을 그대로 사용한다.
  - 스폰 형태(`NWay`, `Spiral`, 지속 사건형)와 무관하게 동일 정책을 적용한다.
- 기준점 수(기본 규약):
  - `LineEven`: 라인 샘플링으로 확정된 유효 포인트 전체
  - `PointSet`: `PointCount` 유효 포인트
  - `UniformField` / `PollutionTopK`: 이벤트당 1개 포인트

## 5. Direction 설정
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `Direction.DirectionMode` | 발사 방향 모드 | `Random` / `Fixed` / `NWay` / `Spiral` / `RadialBurst` |
| `Direction.BaseAngleDeg` | 기준 각도(도) | 모든 모드의 기준값 |
| `Direction.NWayCount` | 슬롯 수 | `NWay`에서 `>=2` (필수) |
| `Direction.SpiralStepDeg` | 샷당 회전 증분(도) | `Spiral`에서 권장 `!= 0` |

### 5.1 DirectionMode 값 의미
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `Random` | 0 | 무작위 방향 | 균등 각도 분포 |
| `NWay` | 1 | 슬롯 수 기반 다방향 분배 | `NWayCount` 사용, `BaseAngleDeg + (360/NWayCount)*slot` |
| `Spiral` | 2 | 샷 시퀀스마다 각도 누적 회전 | `SpiralStepDeg` 사용, 이벤트 내부 시간축은 Emission에서 정의 |
| `RadialBurst` | 3 | 버스트 의도 중심 방사 발사 | 런타임 슬롯 로직은 NWay와 공통 |
| `Fixed` | 4 | 기준각 고정 발사 | `BaseAngleDeg` 사용 |

### 5.2 NWay vs RadialBurst
- 런타임은 두 모드를 공통 슬롯 분배 로직으로 처리한다.
- 의도 차이:
  - `NWay`: 고정 슬롯 기반 분산 발사
  - `RadialBurst`: 버스트 이벤트와 결합된 방사 의도 표기

### 5.3 NWay 실행 규약 (합의)
- 각도 규약은 360도 균등 분할을 사용한다.
- `NWayCount`는 필수로 `>=2`를 만족해야 한다(콘텐츠 검증 Error 대상).
- 원자성 단위는 "샘플 1지점의 NWay 1세트"다.
  - 예: `NWayCount=4`이면 4발을 하나의 세트로 소비한다.
- 예산/풀 부족으로 세트 전체를 소비하지 못하면, 해당 세트는 다음 프레임으로 이월한다.
- 세트 이월 시 `SpawnSequence`는 증가시키지 않는다.
  - 다음 프레임에서 동일 좌표/동일 슬롯 위상으로 재시도한다.

### 5.4 Direction 책임 경계 (합의)
- `Direction`은 "각 샷의 방향 계산"만 담당한다.
- 이벤트 내부 타임라인(샷 간격/샷 회수)과 기준점 고정 정책은 `Emission`에서 담당한다.
- `Spiral`은 각도 진행 규약이며, 지속 사건형 스폰 자체를 정의하지 않는다.

## 6. 우선순위/예산 해석
- 예산(`BudgetPerFrame`)은 요청 전체에서 공유된다.
- 같은 프레임에 경합 시 Lane 우선순위를 먼저 적용한다.
- Lane 우선순위는 `특수 > Hazard > Trash`다.
- v3 클립 경로에서는 레거시 `SpawnPriority` 대신 Lane 기반 우선순위를 사용한다.

## 7. 샘플 시나리오 매핑 가이드
1. 초기 구간
- Hazard: `Sampling=PollutionTopK`, `Emission=RateField`, `Direction=Random`(또는 목적형 기본값), `PlayerNoSpawnRadius>0`
- Trash: `Sampling=PollutionTopK`, `Emission=RateField`

2. 전환 구간
- Hazard 나선: `Emission=RateField(or EventBurst)`, `Direction=Spiral`
- Hazard 방사 3회: `Emission=EventBurst(BurstIntervalSec=0.2, BurstRepeatCount=3)`, `Direction=RadialBurst`

3. 전환 후 구간
- Hazard 4방향: `Direction=NWay(NWayCount=4)` 또는 `Fixed` 다중 엔트리
- 벽 발사: `Sampling=LineEven` + 벽 라인 배치 + `Direction=Fixed/NWay`
- Trash 감소: 동일 Sampling 유지, `Emission` 레이트 하향

## 8. 검증 체크
- 컴파일/콘솔 error 0
- EditMode pass
- PlayMode 전용 씬 스모크 pass
- 시나리오 관측값(권장):
  - `LastFrameBudgetUsed`
  - `PendingCount`
  - `DeferredByBudget`
  - `SpawnSkipRate01`

## 9. v3 Authoring 스키마 (현행)
- 기준 ADR: [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
- 운영 목표:
  - `WaveClipSO` 기반 단일 경로로 운영한다(`WaveTimelineSO` 제거 완료).
  - Source 바인딩은 1차에서 `BulletSourceAuthoring` 직참조 배열로 운영한다.
  - `런 진행도 디렉터` 도입 시에도 `WaveClipSO` 스키마(`ClipId/Phase/Lane/DurationSec/Segments`)는 유지한다.
  - `BulletSource`는 외부 선택 요청을 소비해 `SourceSpawnRequestBuffer`를 출력하는 구조로 확장하되, 슬롯 authoring 필드는 유지한다.
  - `Sustain`은 기본 `Hazard`/`Trash` 2 Lane을 독립 운영하고, Lane enum 확장을 허용한다(`SourceSpawnLaneId`).
  - 특수 Lane 확장 시 Lane 우선순위는 `특수 > Hazard > Trash`를 유지한다.

### 9.1 WaveClipSO(클립 자산) 권장 필드
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `ClipId` | 클립 식별자 | 전역 고유, `> 0` |
| `Phase` | 클립 용도 | `Sustain` / `OnStateEnterOnce` |
| `Lane` | 클립 Lane | 기본 운영 `Hazard` / `Trash`, `Special` 예약 + enum 확장 허용 |
| `DurationSec` | 클립 총 길이 | `> 0` |
| `Segments[]` | 클립 로컬 구간 | 구간별 `StartSec < EndSec`, overlap 허용 |
| `Segments[].Entries[]` | SpawnDirective 인라인 프로필 | 현재 `Payload/Emission/Sampling/Direction` 규약 재사용 |

### 9.2 BulletSourceAuthoring 직참조 필드(현행)
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `SustainClipSlots[].State` | Source 상태 슬롯 | `Normal` / `Weakened` / `Depleted` |
| `SustainClipSlots[].Lane` | 슬롯 Lane | Lane enum 값 |
| `SustainClipSlots[].Clips[]` | 해당 Lane 후보군 | 비어 있으면 런타임 skip + Error 로그 |
| `SustainClipSlots[].Weights[]` | 선택 가중치(옵션) | 길이 불일치/0 이하는 1.0 fallback |
| `EventClipSlots[].TriggerState` | 이벤트 트리거 상태 | 상태 전환 감지 시 발동 |
| `EventClipSlots[].EventClips[]` | 이벤트 클립 참조 | 중복 트리거는 큐잉 |

### 9.3 실행 규약
1. 슬롯 키는 `State + Phase + Lane`으로 고정한다.
2. Clip 선택 주체는 Source가 아니라 `런 진행도 디렉터`다.
3. Source는 디렉터가 할당한 `단일 활성 클립`을 재생하고, 전환/재생의 상세 시점 규칙은 기존 Source Clip 선택/전환 규칙 형태를 재사용한다.
4. `Baseline <-> Pressure` 전환에서는 Clip을 교체하지 않는다.
5. `Baseline`은 밀도 기반 스폰만 곱셈 배율로 축소하고, `hazard/event`는 디렉터 배율로 조정하지 않는다.
6. `Pressure` 기본 배율은 `1.0`을 사용한다(추가 요소 미적용 기준).
7. `Finish`는 `SourceState -> Depleted` 전환과 함께 강제 진입하며, 진입 시 Clip을 `중단` 또는 `고갈 연출용 미량 스폰`으로 교체한다.
8. `Finish` 지속 Clip은 `Trash Lane`만 허용한다. 지속 Clip이 없으면 `중단` 경로를 사용한다.
9. `Finish` 전환 시점의 1회성 연출은 추후 결정(TBD)한다.
10. Lane 우선순위는 `특수 > Hazard > Trash`를 적용하고, Lane 규칙을 요청 우선순위의 최상위 규칙으로 둔다.
11. 결정론 RNG 키는 `GlobalRunSeed + SourceStableId + SlotKey(State/Phase/Lane) + SelectionSequence`를 사용한다.
12. `SpawnRunSeedComponent` 기본값은 `1`이며, 필요 시 런 시작 시점에 외부에서 주입해 재현성을 제어한다.

## 10. 변경 이력
- 2026-02-27: 런 진행도 디렉터 책임 이관 기준에 맞춰 실행 규약을 갱신했다(Clip 선택 주체 디렉터, `Baseline/Pressure` Clip 유지+배율, `Finish` 강제 교체/Trash Lane 제약).
- 2026-02-26: 사건형 이벤트 모드(`Poisson`/`EventBurst`)의 지속 사건형 확장 합의(`EventShotSchedule`, `EventShotIntervalSec`)와 이벤트 기준점 고정(월드 고정/이벤트 범위) 규약을 추가
