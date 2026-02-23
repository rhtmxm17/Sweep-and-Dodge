# SpawnDirective 모델 (Sampling / Emission / Payload)

## Metadata
- doc_id: `TD-003`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-23`
- related_adr:
  - [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)

> GD-007의 스폰 개념 보강안을 런타임 데이터 모델 관점으로 분리 정리한 기술 문서.

## 1. 문제 정의
- 기존 보강안은 기획 원칙과 데이터 모델 상세가 한 문서에 혼재되어 GD/TD 경계가 흐려진다.
- 스폰 방식 확장(RateField, Poisson, EventBurst 예정)을 위해 공통 조합 단위를 명시할 필요가 있다.

## 2. 목표/비목표
- 목표:
  - 스폰을 `Sampling × Emission × Payload` 조합으로 고정한다.
  - 밀도형 스폰과 완전 무작위 스폰을 동일 모델로 포괄한다.
  - MVP에서 허용할 모드 범위를 제한해 데이터 과잉을 방지한다.
- 비목표:
  - 최종 밸런스 수치 확정.
  - ECS 시스템 구현 세부(소유권/업데이트 순서) 재정의.

## 3. 모델 개요
```text
SpawnDirective = SamplingProfile(어디) × EmissionProfile(언제/얼마나) × PayloadProfile(무엇)
```

## 4. SamplingProfile (어디에서 뽑는가)
- 역할: 공간 분포와 공정성 제어.
- 주요 필드:
  - `FieldShape` (`Circle` / `Rect`)
  - `CenterMode` (`SourceCenter` / `FixedPoint` / `PlayerRelative`)
  - `Distribution` (`Uniform` / `EdgeBiased` / `CenterBiased` / `Donut`)
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

### 5.3 EventBurst (확장 예약)
- 상태 전환/연출 이벤트 기반 기하학 패턴은 후속 모드로 추가한다.

## 6. PayloadProfile (무엇을 뿌리는가)
- 역할: 탄 타입과 상호작용 규칙 정의.
- 주요 필드:
  - `BulletTypeKey`
  - `CaptureRuleId`
  - `HazardMix` 또는 별도 Hazard 라인

## 7. 조합 규칙
- 밀도형 스폰: `Sampling + RateField + Payload`
- 완전 무작위 소량 스폰: `Sampling + Poisson + Payload`
- 스테이지 체감 조정 시, 동일 Sampling에서 Emission만 교체하는 실험 경로를 우선한다.

## 7.1 런타임 적용 규칙 (확정)
- 요청 집계 키는 `BulletTypeKey` 단독이 아니라 `DirectiveId`를 기본 키로 사용한다.
- Sampling 최종 평가는 Request가 아니라 `ExecutionBegin` 스폰 소비 시점에서 수행한다.
- `WaveTimelineSO`는 `SpawnEntry` 내부에 `Payload/Emission/Sampling` 인라인 프로필을 기본 구조로 사용한다.
  - 이행기 호환을 위해 legacy 필드를 유지하되, `UseDirectiveProfiles`가 켜진 엔트리부터 신규 프로필을 우선 적용한다.

## 8. MVP 데이터 과잉 방지
- EmissionMode는 `RateField` / `Poisson` 두 가지로 제한한다.
- Distribution 기본값은 `Uniform`을 유지한다.
- Sampling과 Emission의 동시 복잡도 증가를 회피한다.

## 9. 마이그레이션 체크리스트
- [x] 대상 `WaveTimelineSO.SpawnEntry`에서 `UseDirectiveProfiles=true`로 전환했는가
- [x] `Payload.Bullet`이 legacy `Bullet`과 동일 참조로 매핑되었는가
- [x] `Emission`에 legacy 동등값(`SpawnMode`, `RatePerSecPerArea`, `MaxActiveDensityPerArea`)을 채웠는가
- [x] `Sampling` 기본값을 명시했는가 (`SamplingMode=PollutionTopK`, `CenterMode=SourceCenter`, `SpawnSampleBudget=16`)
- [x] `Poisson` 사용 시 `EmissionMode=Poisson`, `MeanEventsPerSec>=0`를 확인했는가
- [x] 검증 루프를 통과했는가 (`refresh_unity` -> 콘솔 error 0 -> EditMode -> PlayMode 전용 스모크)

## 10. 문서 경계
- 기획 의도/체감 목표는 `GD-007`에서 관리한다.
- Pattern/Wave/Progress의 런타임 수식/검증 규칙은 `TD-002`에서 관리한다.
- 본 문서는 스폰 분해 모델과 모드 조합 규칙을 관리한다.

## 11. 변경 이력
- 2026-02-23: 실무 적용용 마이그레이션 체크리스트(UseDirectiveProfiles 전환/매핑/검증 루프)를 추가
- 2026-02-23: `DirectiveId` 기반 요청 집계, ExecutionBegin Sampling 평가, `WaveTimelineSO` 인라인 프로필(`UseDirectiveProfiles`) 적용 규칙을 확정
- 2026-02-23: `GD-007`의 보강 섹션을 분리해 `TD-003`으로 승격
