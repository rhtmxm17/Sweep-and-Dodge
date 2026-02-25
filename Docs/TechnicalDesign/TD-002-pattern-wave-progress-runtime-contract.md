# Pattern/Wave/Progress 런타임 계약 (MVP)

## Metadata
- doc_id: `TD-002`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-25`
- related_adr:
  - [ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md](../ADR/ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md)
  - [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)

> GD-007의 기획 의도를 ECS 런타임 데이터 계약으로 변환한 기술 설계 문서.
> SpawnDirective 분해 모델(Sampling/Emission/Payload) 상세는 `TD-003`을 참조한다.

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
#### SpawnDirectiveDefinitionSlim (현재 기준)

| Field | Type | 설명 | 기본/범위 |
| --- | --- | --- | --- |
| DirectiveId | int | 요청/소비 추적 키 | > 0, Source 내 고유 |
| TriggerState | enum | Source 상태 조건 | Normal / Weakened / Depleted |
| Phase | enum | 구간 타입 | Sustain / OnStateEnterOnce |
| StartSec | float | 활성 시작 시간 | >= 0 |
| EndSec | float | 활성 종료 시간 | > StartSec |
| BulletTypeKey | int | Payload 탄 타입 | 풀 레지스트리에 존재 |
| EmissionMode | enum | 방출 모드 | RateField / Poisson / EventBurst |
| RatePerSecPerArea | float | RateField 밀도 | >= 0 |
| MeanEventsPerSec | float | Poisson 평균 이벤트율 | >= 0 |
| BurstRepeatCount | int | EventBurst 반복 수 | -1(무한) 또는 >= 1 |
| BurstIntervalSec | float | EventBurst 반복 간격 | > 0 |
| BurstShotsPerEvent | int | EventBurst 1회당 샷 수 | >= 1 |
| SpawnMode | enum | 활성 캡 정책 | FixedDensity / CapAndMaxDensity |
| MaxActiveDensityPerArea | float | Cap 모드 상한 | Cap 모드에서 >= 0 |
| SamplingMode | enum | 샘플링 모드 | UniformField / PollutionTopK / LineEven / PointSet |
| CenterMode | enum | 중심 모드 | SourceCenter / FixedPoint / PlayerRelative |
| FixedPoint | float2 | 고정 중심점 | CenterMode=FixedPoint |
| SpawnOffset | float2 | 플레이어 상대 오프셋 | CenterMode=PlayerRelative |
| LineStart / LineEnd | float2 | LineEven 기준 선분 | SamplingMode=LineEven |
| SampleSpacing | float | LineEven 등간격 간격 | > 0 |
| DirectionMode | enum | 방향 모드 | Random / Fixed / NWay / Spiral / RadialBurst |
| BaseAngleDeg | float | 기준 각도 | 자유 범위 |
| NWayCount | int | NWay 슬롯 수 | NWay에서 >= 2 |
| SpiralStepDeg | float | Spiral 각도 증분 | Spiral에서 권장 != 0 |
| SpawnSampleBudget | int | 샘플링 재시도 예산 | >= 1 (기본 16) |
| PlayerNoSpawnRadius | float | 플레이어 주변 제외 반경 | >= 0 |
| SpawnPriority | int | 요청 소비 우선순위 | 높을수록 우선 (Trash 최하) |

`PatternDefinitionSlim`은 밀도 기반 구버전 용어이며, 스폰 모델은 `TD-003`의 SpawnDirective 용어를 기준으로 유지한다.

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
- Directive 데이터를 사용해 `SourceSpawnRequestBuffer`를 누적 생성한다.
- 요청 집계 키는 `BulletTypeKey` 단독이 아니라 `DirectiveId`를 기본 키로 사용한다.
- EventBurst 소비 정책은 `carry`를 사용한다(미소비 샷은 다음 프레임 이월).
- ExecutionBegin 단계:
- Owner(`SpawnRequestRoundRobinExecutionSystem`)가 요청을 소비해 실제 스폰을 수행한다.
- Sampling(중심 계산/샘플링/NoSpawn 반경 검증)과 Direction 계산은 ExecutionBegin에서 최종 평가한다.
- `BudgetPerFrame`은 요청 전체(탄 종류 공용)에서 공유한다.
  - 우선순위: Hazard 우선, Trash는 최하 우선순위로 소비한다.
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
- Wave segment의 `EndSec <= StartSec` (`CV010`).
- Wave segment 간 시간 겹침 (`CV011`).
- Wave entry의 `RatePerSecPerArea < 0` (`CV015`, RateField 모드).
- Wave entry의 `MeanEventsPerSec < 0` (`CV017`, Poisson 모드).
- `CapAndMaxDensity`인데 `MaxActiveDensityPerArea < 0` (`CV016`).
- Wave entry의 `SpawnSampleBudget < 0` (`CV018`).
- Wave entry의 `PlayerNoSpawnRadius < 0` (`CV019`).
- EventBurst에서 `BurstIntervalSec <= 0` (`CV020`).
- EventBurst에서 `BurstRepeatCount`가 `-1` 또는 `>=1`이 아님 (`CV021`).
- EventBurst에서 `BurstShotsPerEvent < 1` (`CV022`).
- NWay에서 `NWayCount < 2` (`CV023`).
- RadialBurst에서 `BurstShotsPerEvent < 2` (`CV024`).
- LineEven에서 선분 길이 0 또는 `SampleSpacing <= 0` (`CV026`).
- Warning:
- `SpawnSampleBudget`가 권장 범위 초과.
- `MaxActiveDensityPerArea`가 Stage 목표 대비 과도함.
- `RiskMultiplier` 예상 상한이 운영 목표(3.0) 초과.
- Spiral에서 `SpiralStepDeg`가 0에 근접 (`CVW032`).
- PointSet 사용(1차에서는 Uniform fallback) (`CVW033`).

검증 코드 매핑(현재 구현):
- `CV012`: Wave segment의 `Entries` 비어 있음
- `CV013`: Wave entry의 `Bullet == null`
- `CV014`: Wave entry가 미등록 `DefinitionId` 참조
- `CV015`: Wave entry의 `RatePerSecPerArea < 0` (RateField)
- `CV016`: `CapAndMaxDensity`인데 `MaxActiveDensityPerArea < 0`
- `CV017`: Wave entry의 `MeanEventsPerSec < 0` (Poisson)
- `CV018`: Wave entry의 `SpawnSampleBudget < 0`
- `CV019`: Wave entry의 `PlayerNoSpawnRadius < 0`
- `CV020`: EventBurst `BurstIntervalSec <= 0`
- `CV021`: EventBurst `BurstRepeatCount` 범위 오류
- `CV022`: EventBurst `BurstShotsPerEvent < 1`
- `CV023`: NWay `NWayCount < 2`
- `CV024`: RadialBurst `BurstShotsPerEvent < 2`
- `CV026`: LineEven 파라미터 오류
- `CVW032`: Spiral `SpiralStepDeg` 0 근접 (Warning)
- `CVW033`: PointSet 사용 시 1차 fallback 경고 (Warning)
- `CV010`: Wave segment 범위 오류(`EndSec <= StartSec`)
- `CV011`: Wave segment 중첩

### 6.2 테스트 루프
- EditMode: 데이터 무결성/매핑 규칙 검증.
- PlayMode: 전용 씬 스모크로 기동/루프 정상성 확인.
- 스트레스: backlog/expired/drop 지표 회귀 추적.

## 7. 차기 확장(v3) 런타임 계약 초안 (미구현)
- 기준 ADR: [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
- 핵심 규약:
  - 슬롯 키: `State + Phase + Lane`
  - Lane enum은 확장 가능 구조로 설계한다(기본 운영: `Hazard`, `Trash`).
  - `Sustain`: 상태당 Lane별 활성 클립 1개씩(기본 최대 2개 동시)
  - `OnStateEnterOnce` 진입 시 하드 프리엠션(기존 sustain pending 폐기 + 생성 중지)
  - 이벤트 중복 트리거는 큐잉한다.
  - `Sustain`도 `StartSec/EndSec` 시간축 적용
  - 클립 선택 시 `Sustain` 로컬 시간은 0으로 리셋
  - `Sustain` 클립 종료 시 동일 슬롯 후보군에서 "직전 제외 랜덤"으로 다음 클립 선택
  - 상태 전환 시 기존 sustain 클립 즉시 중단
  - Lane 우선순위는 `특수 > Hazard > Trash`이며 Lane 규칙이 최우선
- 권장 ECS 스키마:
  - `SourceClipPatternBuffer` (`ClipId`, `Phase`, `Lane`, `LocalStartSec`, `LocalEndSec`, 기존 directive 필드)
  - `SourceSustainSlotCandidateBuffer` (`State`, `Lane`, `ClipId`, `Weight`)
  - `SourceSustainRuntimeComponent` (`ActiveClipByLane`, `ElapsedByLane`, `LastClipByLane`, `SelectionSequenceByLane`)
  - `SourceEventRuntimeComponent` (`IsPlaying`, `ActiveEventClipId`, `TriggerState`, `ElapsedSec`)
  - `SourceEventQueueBuffer` (`TriggerState`, `QueuedFrame`)
- 결정론 요구:
  - 무작위 선택 RNG 키는 `GlobalRunSeed + SourceStableId + SlotKey(State/Phase/Lane) + SelectionSequence`를 사용한다.
  - `Entity.Index` 단독 사용은 재현성 리스크로 지양한다.

## 8. 오픈 이슈
- Stage별 PlayerRelative 허용 비중 상한.
- Progress 지표를 Source 상태 전환과 연결하는 운영 규칙.

## 9. 변경 이력
- 2026-02-25: v3 합의 반영(하드 프리엠션, 큐잉, 상태전환 즉시중단, Lane 우선순위, RNG 키)으로 초안을 갱신
- 2026-02-25: v3 클립/슬롯/채널 확장 초안 및 런타임 스키마 초안을 추가
- 2026-02-25: `SpawnEntry` 레거시 fallback과 `WallEven` 전용 경로/검증 규칙(`CV025`, `CVW034`) 제거
- 2026-02-24: `WallEven`을 정책 비활성으로 전환하고 `CV025`/`CVW034` 검증 규칙을 추가
- 2026-02-24: `DirectionMode.Fixed`를 추가해 `LineEven + 고정 방향` 구성을 정식 지원
- 2026-02-24: `EventBurst(carry)`, `DirectionProfile`, `LineEven/WallEven` 계약 및 CV020~CV024/CV026/CVW032/CVW033 규칙을 추가
- 2026-02-24: 프레임 예산 공유 및 Trash 최하 우선순위 소비 규칙을 명시
- 2026-02-23: Spawn 계약을 `PatternDefinitionSlim` 중심에서 `SpawnDirectiveDefinitionSlim` 중심으로 전환하고, 요청 집계 키를 `DirectiveId` 기준으로 명시
- 2026-02-23: GD-007에서 구현 계약 항목(필드/수식/검증)을 분리해 `TD-002` 초안 작성
- 2026-02-23: Wave 정책을 "동시 지속 금지"로 확정하고 중첩 우선순위 이슈를 해소
