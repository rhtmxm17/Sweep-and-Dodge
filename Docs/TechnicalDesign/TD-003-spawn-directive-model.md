# SpawnDirective 모델 (Sampling / Emission / Payload)

## Metadata
- doc_id: `TD-003`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-26`
- related_adr:
  - [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md](../ADR/ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)

> GD-007의 스폰 개념 보강안을 런타임 데이터 모델 관점으로 분리 정리한 기술 문서.

## 1. 문제 정의
- 기존 보강안은 기획 원칙과 데이터 모델 상세가 한 문서에 혼재되어 GD/TD 경계가 흐려진다.
- 스폰 방식 확장(RateField, Poisson, EventBurst)과 방향 패턴 제어를 위해 공통 조합 단위를 명시할 필요가 있다.

## 2. 목표/비목표
- 목표:
  - 스폰을 `Sampling × Emission × Direction × Payload` 조합으로 고정한다.
  - 밀도형 스폰과 완전 무작위 스폰을 동일 모델로 포괄한다.
  - MVP에서 허용할 모드 범위를 제한해 데이터 과잉을 방지한다.
- 비목표:
  - 최종 밸런스 수치 확정.
  - ECS 시스템 구현 세부(소유권/업데이트 순서) 재정의.

## 3. 모델 개요
```text
SpawnDirective = SamplingProfile(어디) × EmissionProfile(언제/얼마나) × DirectionProfile(어느 방향으로) × PayloadProfile(무엇)
```

## 4. SamplingProfile (어디에서 뽑는가)
- 역할: 공간 분포와 공정성 제어.
- 주요 필드:
  - `FieldShape` (`Circle` / `Rect`)
  - `CenterMode` (`SourceCenter` / `FixedPoint` / `PlayerRelative`)
  - `SamplingMode` (`UniformField` / `PollutionTopK` / `LineEven` / `PointSet`)
  - `LineStart`, `LineEnd`, `SampleSpacing` (LineEven)
  - `PlayerNoSpawnRadius`
  - `SpawnSampleBudget`
- 원칙:
  - 공간 무작위성은 Sampling 단에서만 조정한다.
  - 밀도형/무작위형 여부와 관계없이 공정성 가드는 공통 적용한다.

## 5. EmissionProfile (언제/얼마나 뿌리는가)
- 역할: 시간 분포와 방출 리듬 제어.

### 5.1 RateField (밀도형)
- `RatePerSecPerArea`
- `IntensityMode` (`Flat` / `Pulse` / `Ramp`)
- `DurationSec`
- 용도: 지속 압박, 예측 가능한 밀도 형성.

### 5.2 Poisson (완전 무작위형)
- `MeanEventsPerSec (lambda)`
- 용도: 간헐적/불규칙 체감 형성.
- 원칙: 완전 무작위 소량 스폰은 초저밀도 `RateField` 대체가 아니라 `Poisson`으로 정의한다.

### 5.3 EventBurst (정식)
- 필드:
  - `BurstRepeatCount` (`-1`은 무한 반복, 그 외 `>=1`)
  - `BurstIntervalSec` (`>0`)
  - `BurstShotsPerEvent` (`>=1`)
- 소비 정책:
  - `carry`를 기본 정책으로 사용한다.
  - 프레임 예산 부족 시 미소비 샷은 요청 버퍼에 남겨 다음 프레임에서 이어서 소비한다.

## 6. DirectionProfile (어느 방향으로 쏘는가)
- 역할: 발사 벡터 분포/리듬 제어.
- 모드:
  - `Random`
  - `Fixed`
  - `NWay`
  - `Spiral`
  - `RadialBurst`
- 주요 필드:
  - `BaseAngleDeg`
  - `NWayCount` (`NWay`, 필수 `>=2`)
  - `SpiralStepDeg` (`Spiral`)
- 중복 최소화:
  - 런타임은 `NWay`/`RadialBurst`를 공통 슬롯 분배 로직으로 처리한다.
  - 슬롯 각도는 360도 균등 분할(`BaseAngleDeg + (360/N)*slot`)을 기본 규약으로 사용한다.
  - `RadialBurst`는 EventBurst와 결합했을 때의 의도 표현을 위한 별칭 모드로 유지한다.

## 7. PayloadProfile (무엇을 뿌리는가)
- 역할: 탄 타입과 상호작용 규칙 정의.
- 주요 필드:
  - `BulletTypeKey`
  - `CaptureRuleId`
  - `HazardMix` 또는 별도 Hazard 라인

## 8. 조합 규칙
- 밀도형 스폰: `Sampling + RateField + Payload`
- 완전 무작위 소량 스폰: `Sampling + Poisson + Payload`
- 이벤트성 패턴 스폰: `Sampling + EventBurst + Direction + Payload`
- 스테이지 체감 조정 시, 동일 Sampling에서 Emission만 교체하는 실험 경로를 우선한다.

## 9. 런타임 적용 규칙 (확정)
- 요청 집계 키는 `BulletTypeKey` 단독이 아니라 `DirectiveId`를 기본 키로 사용한다.
- Sampling 최종 평가는 Request가 아니라 `ExecutionBegin` 스폰 소비 시점에서 수행한다.
- 방향 계산(Direction)도 `ExecutionBegin` 스폰 소비 시점에서 수행한다.
- `WaveClipSO`는 `Segments[].Entries[]` 내부에 `Payload/Emission/Sampling/Direction` 인라인 프로필을 기본 구조로 사용한다.
- 프레임 예산(`BudgetPerFrame`)은 요청 전체(탄 종류 공용)에서 공유한다.
  - 우선순위 규칙: Lane 우선순위(`특수 > Hazard > Trash`)를 적용한다.

## 10. MVP 데이터 과잉 방지
- EmissionMode는 `RateField` / `Poisson` / `EventBurst` 세 가지로 제한한다.
- DirectionMode는 `Random` / `Fixed` / `NWay` / `Spiral` / `RadialBurst` 다섯 가지로 제한한다.
- Sampling 확장은 `LineEven`만 1차 활성화한다.
- `PointSet`은 1차에서 `Max=4` 로컬 오프셋 포인트셋으로 활성화한다.

## 11. 마이그레이션 체크리스트
- [x] 대상 `WaveClipSO.Segments[].Entries[]`가 인라인 프로필(`Payload/Emission/Sampling/Direction`)만 사용하도록 정리되었는가
- [x] `Payload.Bullet` 참조가 각 엔트리에 채워졌는가
- [x] `Sampling` 기본값을 명시했는가 (`SamplingMode=PollutionTopK`, `CenterMode=SourceCenter`, `SpawnSampleBudget=16`)
- [x] `Poisson` 사용 시 `EmissionMode=Poisson`, `MeanEventsPerSec>=0`를 확인했는가
- [x] 검증 루프를 통과했는가 (`refresh_unity` -> 콘솔 error 0 -> EditMode -> PlayMode 전용 스모크)

## 12. 문서 경계
- 기획 의도/체감 목표는 `GD-007`에서 관리한다.
- Pattern/Wave/Progress의 런타임 수식/검증 규칙은 `TD-002`에서 관리한다.
- 본 문서는 스폰 분해 모델과 모드 조합 규칙을 관리한다.

## 13. v3 확장 계약 (구현 반영)
- 상세 계약은 [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)를 기준으로 한다.
- 확장 방향:
  - `WaveClipSO`를 도입하고 `WaveTimelineSO` 타입/데이터/참조를 제거했다.
  - Source 런타임 슬롯 키를 `State + Phase + Lane`으로 고정한다(`Lane`은 확장 가능한 enum).
  - `Sustain`은 기본 `Hazard`/`Trash` Lane별 활성 클립 1개씩(최대 2개) 동시 실행한다.
  - 이벤트 클립(`OnStateEnterOnce`) 진입 시 하드 프리엠션(기존 sustain pending 폐기 + 생성 중지)을 적용한다.
  - 이벤트 중복 트리거는 큐잉한다.
  - `Sustain` 시간축은 클립 선택 시 로컬 시간 리셋 기준으로 동작한다.
  - 클립 종료 시 동일 슬롯 후보군에서 "직전 클립 제외 완전 랜덤"으로 다음 클립을 선택한다.
  - 우선순위는 Lane 규칙 `특수 > Hazard > Trash`를 우선하며, v3에서는 레거시 `SpawnPriority` 제거를 기본안으로 둔다.
  - 채널 명칭은 탄 타입과 혼동 방지를 위해 `SpawnLane` 계열 네이밍을 검토한다.

## 14. 변경 이력
- 2026-02-26: NWay 필수값(`NWayCount>=2`)과 360도 균등 슬롯 각도 규약을 명시
- 2026-02-25: `WaveClipSO` 기반 v3 경로를 반영하고 `WaveTimelineSO` 제거 상태를 문서에 동기화
- 2026-02-25: v3 합의 반영(하드 프리엠션, 큐잉, Lane 우선순위, RNG/선택 규칙)으로 초안을 갱신
- 2026-02-25: v3 확장 초안(클립/슬롯/채널, 이벤트 우선, sustain 체인) 및 ADR 링크를 추가
- 2026-02-25: `SpawnEntry` 레거시 fallback(`UseDirectiveProfiles`, legacy emission 필드)과 `WallEven` 전용 계약을 제거
- 2026-02-24: `DirectionMode.Fixed`를 추가해 `LineEven + 고정 방향` 시나리오를 명시적으로 지원
- 2026-02-24: `EventBurst`를 정식 계약으로 승격하고 `carry` 소비 정책, `DirectionProfile`, `LineEven/WallEven` 1차 범위를 추가
- 2026-02-24: 공유 예산 정책에서 Trash 최하 우선순위 규칙을 명시
- 2026-02-23: 실무 적용용 마이그레이션 체크리스트(UseDirectiveProfiles 전환/매핑/검증 루프)를 추가
- 2026-02-23: `DirectiveId` 기반 요청 집계, ExecutionBegin Sampling 평가, `WaveTimelineSO` 인라인 프로필(`UseDirectiveProfiles`) 적용 규칙을 확정
- 2026-02-23: `GD-007`의 보강 섹션을 분리해 `TD-003`으로 승격
