# Pattern/Wave/Progress 런타임 계약 (MVP)

## Metadata
- doc_id: `TD-002`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-23`
- related_adr:
  - [ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md](../ADR/ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md)
  - [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)

> GD-007의 기획 의도를 ECS 런타임 데이터 계약으로 변환한 기술 설계 문서.

## 1. 문제 정의
- GD-007은 기획 의도와 밸런싱 방향을 명확히 정의하지만, 구현에 필요한 필드/수식/검증 규칙 상세는 분리 관리가 필요하다.
- 동일 개념이 Authoring, ECS 버퍼, Request/Execution 파이프라인으로 내려갈 때 계약이 없으면 구현 편차가 발생한다.

## 2. 목표/비목표
- 목표:
  - Pattern/Wave/Progress의 최소 런타임 스키마를 고정한다.
  - Request 단계에서의 생성 규칙과 ExecutionBegin 소비 경계를 명확히 한다.
  - 콘텐츠 검증 규칙(Error/Warning) 초안을 고정한다.
- 비목표:
  - 세부 밸런싱 수치의 최종 확정.
  - UI/연출 소비 규칙 상세.

## 3. 설계안
### 3.1 데이터 모델
#### PatternDefinitionSlim

| Field | Type | 설명 | 기본/범위 |
| --- | --- | --- | --- |
| PatternId | int | 고유 ID | > 0, 중복 금지 |
| BulletTypeKey | int | 탄 타입 키 | 풀 레지스트리에 존재 |
| CaptureRuleId | byte | 수거 규칙 | `BulletCaptureRuleId` 매핑 |
| DurationSec | float | 패턴 지속 시간 | > 0 |
| IntensityMode | enum | Flat / RampUp / RampDown / Pulse | 필수 |
| PulsePeriodSec | float | Pulse 주기 | Pulse일 때 > 0 |
| PulseDuty01 | float | Pulse 듀티 | 0~1, 기본 0.5 |
| SpawnDensityPerSecPerArea | float | 면적당 초당 스폰 밀도 | >= 0 |

#### WaveSpawnContextSlim

| Field | Type | 설명 | 기본/범위 |
| --- | --- | --- | --- |
| FieldShape | enum | 필드 형태 | Circle / Rectangle |
| FieldParamA | float | 필드 파라미터 A | Shape별 해석 |
| FieldParamB | float | 필드 파라미터 B | Shape별 해석 |
| SpawnCenterMode | enum | 중심 계산 모드 | SourceCenter / FixedPoint / PlayerRelative |
| SpawnOffset | float2 | 중심 오프셋 | PlayerRelative에 사용 |
| FixedPoint | float2 | 고정 중심점 | FixedPoint에 사용 |
| PlayerNoSpawnRadius | float | 플레이어 주변 제외 반경 | >= 0 |
| SpawnSampleBudget | int | 샘플링 시도 예산 | >= 1, 기본 16 |
| MaxActiveSoftCap | int | 활성 탄 소프트 캡 | >= 0 |

`SpawnCenterMode` 규약:
- `SourceCenter`: `C = SourcePos`
- `FixedPoint`: `C = FixedPoint`
- `PlayerRelative`: `C = PlayerPos + Rotate(SpawnOffset, PlayerFacing)`

### 3.2 Progress 모델
#### StageProgressProfile

| Field | 설명 | 권장 범위(Stage 1) |
| --- | --- | --- |
| BaseTrashValue | Trash 1개 기본 진행도 | 1 |
| BaseHazardValue | Hazard 기본 진행도 | 2~5 |
| RiskFactor | Load 기반 계수 | 0.5~1.0 |
| HazardBonusRate | HazardStack 계수 | 0.03~0.08 |
| HazardStackMax | HazardStack 상한 | 5 (피크 스테이지 최대 10) |

#### RiskMultiplier
```text
RiskMultiplier =
  1
    + (Load / Capacity) × RiskFactor
    + (HazardStack × HazardBonusRate)
```

기획 상한 목표는 2.5~3.0배이며, 별도 Clamp는 두지 않는다.

### 3.3 이벤트 반영 계약
```text
Trash:  ProgressDelta = BaseTrashValue × RiskMultiplier
Hazard: ProgressDelta = BaseHazardValue × RiskMultiplier
        HazardStack = min(HazardStack + 1, HazardStackMax)
Deposit:
        Load = 0
        HazardStack = 0
Hit:
        Load 감소 (기존 규칙)
        HazardStack = 0
        Source Remaining 증가 (기존 반환 규칙 유지)
```

### 3.4 Wave 중첩 정책 (확정)
- 대원칙: 두 개 이상의 Wave가 같은 시점에 동시에 지속되도록 구성하지 않는다.
- 운영 규칙:
  - Wave 시간축은 서로 겹치지 않게 authoring한다.
  - 경계 프레임은 `종료 후 시작` 순서를 기본으로 본다.
  - 겹침이 발견되면 데이터 오류로 간주하고 수정 대상에 포함한다.

## 4. 업데이트 순서/소유권
- Request 단계:
- Pattern/Wave 데이터를 사용해 `SourceSpawnRequestBuffer`를 누적 생성한다.
- ExecutionBegin 단계:
- Owner(`SpawnRequestRoundRobinExecutionSystem`)가 요청을 소비해 실제 스폰을 수행한다.
- ExecutionEnd 단계:
- 디스폰 owner가 반납과 렌더 토글을 처리한다.

현행 ECS 매핑 대상:
- Source 패턴 런타임 버퍼: `SourceSpawnPatternBuffer`
- 요청 버퍼: `SourceSpawnRequestBuffer`
- 정책/백로그: `SpawnRequestPolicyComponent`, `SpawnBacklogMetricsComponent`

## 5. 성능/리스크
- Risk 1: 과밀 데이터로 인한 pending backlog 급증.
- 대응: `MaxPendingCount`, `BudgetPerFrame`, `MaxPendingAgeFrames` 운영.
- Risk 2: PlayerRelative 중심 과사용으로 공정성 악화.
- 대응: `SpawnSkipRate01` 상시 추적 + `PlayerNoSpawnRadius` 가드.
- Risk 3: Stage별 Progress 배율 과증폭으로 체감 난이도 급상승.
- 대응: `HitRatePerMin`, `StageClearTime/TargetTime` 동시 모니터링.

## 6. 검증 계획
### 6.1 콘텐츠 검증 규칙 초안
- Error:
- 중복 `PatternId`.
- `DurationSec <= 0`.
- `SpawnDensityPerSecPerArea < 0`.
- `Pulse` 모드인데 `PulsePeriodSec <= 0`.
- `PulseDuty01`이 0~1 범위를 벗어남.
- `BulletTypeKey`가 풀 레지스트리/정의에 없음.
- Wave segment의 `EndSec <= StartSec` (`CV010`).
- Wave segment 간 시간 겹침 (`CV011`).
- Warning:
- `SpawnSampleBudget`가 권장 범위 초과.
- `MaxActiveSoftCap`이 Stage 목표 대비 과도함.
- `RiskMultiplier` 예상 상한이 운영 목표(3.0) 초과.

검증 코드 매핑(현재 구현):
- `CV012`: Wave segment의 `Entries` 비어 있음
- `CV013`: Wave entry의 `Bullet == null`
- `CV014`: Wave entry가 미등록 `DefinitionId` 참조
- `CV015`: Wave entry의 `SpawnDensityPerSecPerArea < 0`
- `CV016`: `CapAndMaxDensity`인데 `MaxActiveDensityPerArea < 0`
- `CV010`: Wave segment 범위 오류(`EndSec <= StartSec`)
- `CV011`: Wave segment 중첩

### 6.2 테스트 루프
- EditMode: 데이터 무결성/매핑 규칙 검증.
- PlayMode: 전용 씬 스모크로 기동/루프 정상성 확인.
- 스트레스: backlog/expired/drop 지표 회귀 추적.

## 7. 오픈 이슈
- Stage별 PlayerRelative 허용 비중 상한.
- Progress 지표를 Source 상태 전환과 연결하는 운영 규칙.

## 8. 변경 이력
- 2026-02-23: GD-007에서 구현 계약 항목(필드/수식/검증)을 분리해 `TD-002` 초안 작성
- 2026-02-23: Wave 정책을 "동시 지속 금지"로 확정하고 중첩 우선순위 이슈를 해소
