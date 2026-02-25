# SpawnDirective 설정 레퍼런스 (Authoring 기준)

## Metadata
- doc_id: `TD-005`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-25`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness.md](../ADR/ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness.md)
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)

> 목적: `WaveTimelineSO.SpawnEntry`의 각 설정이 실제 런타임에서 무엇을 의미하는지 빠르게 확인하는 운영 레퍼런스.

## 1. 적용 범위
- 대상: `WaveTimelineSO.SpawnEntry` 인라인 프로필
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
| `Emission.BurstShotsPerEvent` | 이벤트 1회 샷 수 | `EventBurst`: `>= 1` |
| `Emission.MaxActiveDensityPerArea` | 활성 상한 밀도 | `CapAndMaxDensity`에서 사용 |

### 3.1 EmissionMode 값 의미
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `RateField` | 0 | 면적당 초당 비율로 누적 스폰 | `RatePerSecPerArea` 사용 |
| `Poisson` | 1 | 평균 이벤트율 기반 확률 스폰 | `MeanEventsPerSec` 사용 |
| `EventBurst` | 2 | 고정 간격 이벤트성 버스트 스폰 | carry 소비 정책 적용 |

### 3.2 SpawnMode 값 의미
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `FixedDensity` | 0 | 밀도 규칙대로 스폰, 활성 상한 캡 없음 | 순수 밀도 기반 |
| `CapAndMaxDensity` | 1 | 활성 수 + pending을 포함한 상한 캡 적용 | `MaxActiveDensityPerArea` 사용 |

### 3.3 EventBurst 소비 해석
- 요청 생성은 Request 단계에서 누적된다.
- 실행은 ExecutionBegin에서 예산 기반으로 소비된다.
- 프레임에 다 못 쓴 샷은 버리지 않고 다음 프레임으로 이월(carry)된다.

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
| `Sampling.SpawnSampleBudget` | 샘플 재시도 예산 | 플레이어 안전거리 필터 재시도 횟수 |
| `Sampling.PlayerNoSpawnRadius` | 플레이어 주변 금지 반경 | `>= 0` |

### 4.1 SamplingMode
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `UniformField` | 0 | 필드 내부 균등 무작위 샘플링 | 기본 무작위 분포 |
| `PollutionTopK` | 1 | Pollution 가중치 상위 후보 중심 샘플링 | 밀도 기반 분포 강화 |
| `LineEven` | 2 | `LineStart~LineEnd` 선분에서 등간격 샘플링 | 라인/벽 발사 표현에 사용 |
| `PointSet` | 4 | 사전 정의 포인트셋 기반 샘플링 의도 | 1차에서는 Uniform fallback |

### 4.2 CenterMode
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `SourceCenter` | 0 | Source 앵커를 중심으로 샘플링 | 기본값 |
| `FixedPoint` | 1 | 월드 고정 좌표를 중심으로 샘플링 | `FixedPoint` 사용 |
| `PlayerRelative` | 2 | 플레이어 위치 + 오프셋을 중심으로 샘플링 | `SpawnOffset` 사용 |

### 4.3 벽 발사 표현 규약
- 별도 `WallEven`은 사용하지 않는다.
- 벽 근처에 `LineStart/LineEnd`를 배치하고 `Direction`으로 진행 방향을 지정한다.

## 5. Direction 설정
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `Direction.DirectionMode` | 발사 방향 모드 | `Random` / `Fixed` / `NWay` / `Spiral` / `RadialBurst` |
| `Direction.BaseAngleDeg` | 기준 각도(도) | 모든 모드의 기준값 |
| `Direction.NWayCount` | 슬롯 수 | `NWay`에서 권장 `>=2` |
| `Direction.SpiralStepDeg` | 샷당 회전 증분(도) | `Spiral`에서 권장 `!= 0` |

### 5.1 DirectionMode 값 의미
| 값 | enum 값(byte) | 의미 | 비고 |
| --- | ---: | --- | --- |
| `Random` | 0 | 무작위 방향 | 균등 각도 분포 |
| `NWay` | 1 | 슬롯 수 기반 다방향 분배 | `NWayCount` 사용 |
| `Spiral` | 2 | 샷 시퀀스마다 각도 누적 회전 | `SpiralStepDeg` 사용 |
| `RadialBurst` | 3 | 버스트 의도 중심 방사 발사 | 런타임 슬롯 로직은 NWay와 공통 |
| `Fixed` | 4 | 기준각 고정 발사 | `BaseAngleDeg` 사용 |

### 5.2 NWay vs RadialBurst
- 런타임은 두 모드를 공통 슬롯 분배 로직으로 처리한다.
- 의도 차이:
  - `NWay`: 고정 슬롯 기반 분산 발사
  - `RadialBurst`: 버스트 이벤트와 결합된 방사 의도 표기

## 6. 우선순위/예산 해석
- 예산(`BudgetPerFrame`)은 요청 전체에서 공유된다.
- 같은 프레임에 경합 시 우선순위가 높은 요청을 먼저 소비한다.
- Trash(`StandardCollectible`)는 최하 우선순위다.

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

## 9. 차기 v3 Authoring 스키마 초안 (미구현)
- 기준 ADR: [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
- 운영 목표:
  - 신규 `WaveClipSO`를 도입하고 `WaveTimelineSO`는 임시 레거시로 유지한다.
  - Source 바인딩은 1차에서 `BulletSourceAuthoring` 직참조 배열로 운영한다.
  - `Sustain`은 기본 `Hazard`/`Trash` 2 Lane을 독립 운영하고, Lane enum 확장을 허용한다.
  - 채널 명칭은 `BulletType`과의 혼동을 줄이기 위해 `SpawnLane` 네이밍을 우선 검토한다.

### 9.1 WaveClipSO(클립 자산) 권장 필드
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `ClipId` | 클립 식별자 | 전역 고유, `> 0` |
| `Phase` | 클립 용도 | `Sustain` / `OnStateEnterOnce` |
| `Lane` | 클립 Lane | 기본 `Hazard` / `Trash`, enum 확장 허용 |
| `DurationSec` | 클립 총 길이 | `> 0` |
| `Segments[]` | 클립 로컬 구간 | 구간별 `StartSec < EndSec`, non-overlap |
| `Segments[].Entries[]` | SpawnDirective 인라인 프로필 | 현재 `Payload/Emission/Sampling/Direction` 규약 재사용 |

### 9.2 BulletSourceAuthoring 직참조 권장 필드(1차)
| 필드 | 의미 | 운영 규칙 |
| --- | --- | --- |
| `SustainSlots[].State` | Source 상태 슬롯 | `Normal` / `Weakened` / `Depleted` |
| `SustainSlots[].Lane` | 슬롯 Lane | Lane enum 값 |
| `SustainSlots[].Clips[]` | 해당 Lane 후보군 | 비어 있어도 런타임 skip + Error 로그 |
| `SustainSlots[].Weights[]` | 선택 가중치(옵션) | 길이 불일치 시 균등 선택 fallback |
| `EventSlots[].TriggerState` | 이벤트 트리거 상태 | 상태 전환 감지 시 발동 |
| `EventSlots[].EventClips[]` | 이벤트 클립 참조 | 중복 트리거는 큐잉 |

### 9.3 실행 규약
1. 슬롯 키는 `State + Phase + Lane`으로 고정한다.
2. 같은 `State + Sustain`에서 활성 클립은 Lane별 1개씩 허용한다(기본 최대 2개).
3. 이벤트 진입 시 하드 프리엠션(기존 sustain pending 폐기 + 생성 중지)을 적용한다.
4. 이벤트 중복 트리거는 큐잉한다.
5. 상태 전환 시 기존 sustain 클립은 즉시 중단한다.
6. sustain 클립 선택 시 로컬 시간은 0으로 리셋한다.
7. sustain 클립 종료 시 동일 슬롯 후보군에서 "직전 제외 랜덤"으로 다음 클립을 선택한다.
8. Lane 우선순위는 `특수 > Hazard > Trash`를 적용하고, Lane 규칙을 요청 우선순위의 최상위 규칙으로 둔다.
9. 결정론 RNG 키는 `GlobalRunSeed + SourceStableId + SlotKey(State/Phase/Lane) + SelectionSequence`를 사용한다.
