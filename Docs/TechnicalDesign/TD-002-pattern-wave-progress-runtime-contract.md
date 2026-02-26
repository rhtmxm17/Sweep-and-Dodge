# Pattern/Wave/Progress 런타임 계약 (MVP)

## Metadata
- doc_id: `TD-002`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-26`
- related_adr:
  - [ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md](../ADR/ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md)
  - [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md](../ADR/ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md)
  - [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)

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
| BurstShotsPerEvent | int | 사건형 이벤트 1회당 샷 수 | Poisson / EventBurst에서 >= 1 |
| EventShotSchedule | enum | 사건형 이벤트 내부 샷 스케줄 | Poisson / EventBurst: Instant / Timed (합의, 구현 예정) |
| EventShotIntervalSec | float | 사건형 이벤트 내부 샷 간격 | Poisson / EventBurst에서 Timed일 때 > 0 (합의, 구현 예정) |
| SpawnMode | enum | 활성 캡 정책 | FixedDensity / CapAndMaxDensity |
| MaxActiveDensityPerArea | float | Cap 모드 상한 | Cap 모드에서 >= 0 |
| SamplingMode | enum | 샘플링 모드 | UniformField / PollutionTopK / LineEven / PointSet |
| CenterMode | enum | 중심 모드 | SourceCenter / FixedPoint / PlayerRelative |
| FixedPoint | float2 | 고정 중심점 | CenterMode=FixedPoint |
| SpawnOffset | float2 | 플레이어 상대 오프셋 | CenterMode=PlayerRelative |
| LineStart / LineEnd | float2 | LineEven 기준 선분 | SamplingMode=LineEven |
| SampleSpacing | float | LineEven 등간격 간격 | > 0 |
| PointSetCount | int | PointSet 포인트 개수 | PointSet에서 `1..4` |
| Point0..Point3 | float2 | PointSet 로컬 오프셋 포인트 | PointSet에서 사용 |
| DirectionMode | enum | 방향 모드 | Random / Fixed / NWay / Spiral / RadialBurst |
| BaseAngleDeg | float | 기준 각도 | 자유 범위 |
| NWayCount | int | NWay 슬롯 수 | NWay에서 `>= 2` (필수) |
| SpiralStepDeg | float | Spiral 각도 증분 | Spiral에서 권장 != 0 |
| SpawnSampleBudget | int | 샘플링 재시도 예산 | >= 1 (기본 16) |
| PlayerNoSpawnRadius | float | 플레이어 주변 제외 반경 | >= 0 |
| SpawnPriority | int | 요청 소비 우선순위(legacy) | v3 클립 경로에서는 `LanePriority` 사용 |

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

### 3.4 Clip Segment 중첩 정책 (확정)
- 동일 `WaveClipSO` 내부에서 segment 시간축 중첩을 허용한다.
- 운영 규칙:
  - 각 segment는 `StartSec < EndSec`만 만족하면 된다.
  - 같은 시점에 활성인 segment가 여러 개면 모두 요청 생성 대상으로 평가한다.
  - 경계 프레임은 `[StartSec, EndSec)` 반열림 구간으로 해석한다.

### 3.5 발행 단위 계약 (합의)
- 밀도형 발행(`RateField` 중심):
  - 요청은 엔티티 수 예약으로 해석한다.
  - 대량 스폰에서 요청 버퍼 폭주를 방지하기 위해 집계된 수량(`Count`)을 우선 사용한다.
- 사건형 발행(`Poisson/EventBurst + Direction` 중심):
  - 요청은 사건 단위 예약으로 해석한다.
  - ExecutionBegin에서 사건을 샘플/방향 슬롯으로 확장해 실제 엔티티를 소비한다.
  - `NWay`는 샘플 지점별 `NWay 1세트`를 원자 단위로 소비한다.
  - (확장 합의) `EventShotSchedule=Timed`는 이벤트 1회 내부에서 샷을 시간 간격으로 분할 소비한다.
  - (확장 합의) 샘플링 기준점은 이벤트 시작 시 1회 확정하고 이벤트 종료까지 고정한다(월드 고정).

## 4. 업데이트 순서/소유권
- Request 단계:
- Directive 데이터를 사용해 `SourceSpawnRequestBuffer`를 누적 생성한다.
- 요청 집계 키는 `BulletTypeKey` 단독이 아니라 `DirectiveId`를 기본 키로 사용한다.
- EventBurst 소비 정책은 `carry`를 사용한다(미소비 샷은 다음 프레임 이월).
- ExecutionBegin 단계:
- Owner(`SpawnRequestRoundRobinExecutionSystem`)가 요청을 소비해 실제 스폰을 수행한다.
- Sampling(중심 계산/샘플링/NoSpawn 반경 검증)과 Direction 계산은 ExecutionBegin에서 최종 평가한다.
- (확장 합의) `Poisson/EventBurst`의 `Timed` 이벤트는 "이벤트 시작 시 샘플링 고정 -> 이벤트 내부 재샘플링 없이 소비" 순서로 처리한다.
- `BudgetPerFrame`은 요청 전체(탄 종류 공용)에서 공유한다.
  - 우선순위: Lane 규칙(`특수 > Hazard > Trash`)을 최우선으로 적용한다.
- `NWay`/`RadialBurst` 방향 슬롯은 360도 균등 분할을 기본 규약으로 사용한다.
- `NWay`는 샘플 지점별 1세트를 원자적으로 소비한다.
  - 세트를 프레임 내에 완결할 수 없으면 세트 전체를 이월한다.
  - 세트 이월 시 `SpawnSequence`는 증가시키지 않고 동일 좌표/위상으로 다음 프레임에 재시도한다.
- ExecutionEnd 단계:
- 디스폰 owner가 반납과 렌더 토글을 처리한다.

현행 ECS 매핑 대상:
- Request 빌더 시스템: `SourceClipRequestBuildSystem`
- Source 클립 런타임 버퍼: `SourceClipPatternBuffer`
- Source 서스테인/이벤트 런타임: `SourceSustainRuntimeLaneBuffer`, `SourceEventRuntimeComponent`, `SourceEventQueueBuffer`
- 요청 버퍼: `SourceSpawnRequestBuffer`
- 정책/백로그/시드: `SpawnRequestPolicyComponent`, `SpawnBacklogMetricsComponent`, `SpawnRunSeedComponent`, `SourceStableIdComponent`

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
- Source에 WaveClip 바인딩이 전혀 없음 (`CV006`).
- Wave clip `Segments` 비어 있음 (`CV008`).
- `ClipId` 중복 (`CV009`).
- Wave entry의 `RatePerSecPerArea < 0` (`CV015`, RateField 모드).
- Wave entry의 `MeanEventsPerSec < 0` (`CV017`, Poisson 모드).
- `CapAndMaxDensity`인데 `MaxActiveDensityPerArea < 0` (`CV016`).
- Wave entry의 `SpawnSampleBudget < 0` (`CV018`).
- Wave entry의 `PlayerNoSpawnRadius < 0` (`CV019`).
- EventBurst에서 `BurstIntervalSec <= 0` (`CV020`).
- EventBurst에서 `BurstRepeatCount`가 `-1` 또는 `>=1`이 아님 (`CV021`).
- Poisson/EventBurst에서 `BurstShotsPerEvent < 1` (`CV022`, 합의 기준. 구현은 EventBurst 우선).
- Poisson/EventBurst에서 `EventShotSchedule=Timed`인데 `EventShotIntervalSec <= 0` (합의, 구현 예정).
- NWay에서 `NWayCount < 2` (`CV023`, 필수 제약 위반).
- RadialBurst에서 `BurstShotsPerEvent < 2` (`CV024`).
- LineEven에서 선분 길이 0 또는 `SampleSpacing <= 0` (`CV026`).
- PointSet에서 `PointCount <= 0` (`CV028`).
- Warning:
- `SpawnSampleBudget`가 권장 범위 초과.
- `MaxActiveDensityPerArea`가 Stage 목표 대비 과도함.
- `RiskMultiplier` 예상 상한이 운영 목표(3.0) 초과.
- Spiral에서 `SpiralStepDeg`가 0에 근접 (`CVW032`).
- PointSet `PointCount > 4` 입력(clamp 경고) (`CVW033`).

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
- `CV022`: EventBurst `BurstShotsPerEvent < 1` (현재 구현)
- `CV023`: NWay `NWayCount < 2`
- `CV024`: RadialBurst `BurstShotsPerEvent < 2`
- `CV026`: LineEven 파라미터 오류
- `CV028`: PointSet `PointCount <= 0`
- `CVW032`: Spiral `SpiralStepDeg` 0 근접 (Warning)
- `CVW033`: PointSet `PointCount` max 초과 clamp 경고 (Warning)
- `CV010`: Wave segment 범위 오류(`EndSec <= StartSec`)

### 6.2 테스트 루프
- EditMode: 데이터 무결성/매핑 규칙 검증.
- PlayMode: 전용 씬 스모크로 기동/루프 정상성 확인.
- 스트레스: backlog/expired/drop 지표 회귀 추적.

## 7. v3 런타임 계약 (구현 반영)
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
  - `SourceSustainRuntimeLaneBuffer` (`Lane`, `ActiveClipId`, `ElapsedSec`, `LastClipId`, `SelectionSequence`)
  - `SourceEventRuntimeComponent` (`IsPlaying`, `ActiveEventClipId`, `TriggerState`, `ElapsedSec`)
  - `SourceEventQueueBuffer` (`TriggerState`, `QueuedFrame`)
- 결정론 요구:
  - 무작위 선택 RNG 키는 `GlobalRunSeed + SourceStableId + SlotKey(State/Phase/Lane) + SelectionSequence`를 사용한다.
  - `Entity.Index` 단독 사용은 재현성 리스크로 지양한다.

## 8. 오픈 이슈
- Stage별 PlayerRelative 허용 비중 상한.
- Progress 지표를 Source 상태 전환과 연결하는 운영 규칙.

## 9. 런 진행도 디렉터 연동 네이밍 가드 (초안)
- `SourceState` 용어는 Source 고갈 상태(`Normal/Weakened/Depleted`) 전용으로 유지한다.
- 런/스테이지 진행 상태 용어는 분리해 `RunProgressState` 또는 `StageFlowState`를 사용한다.
- `Channel` 용어는 탄 타입과 혼동 가능성이 있어 신규 설계에서도 `Lane` 용어를 유지한다.
- 디렉터가 발행하는 선택 요청과 스폰 실행 요청을 분리한다.
  - 디렉터 -> Source 선택 단계: `SourcePatternSelectRequest*` 계열(신규)
  - Source -> 스폰 실행 단계: `SourceSpawnRequestBuffer`(기존 유지)
- 역할 분리 기준:
  - 디렉터: 진행도/상태/이벤트 해석 + 패턴 선택 요청 발행
  - SourceClipRequestBuildSystem: 선택 결과를 소비해 `SourceSpawnRequestBuffer` 생성
  - ExecutionBegin Owner: `SourceSpawnRequestBuffer` 소비 후 실제 스폰 실행

## 10. 변경 이력
- 2026-02-26: 사건형 이벤트 모드(`Poisson`/`EventBurst`) 지속 사건형 확장 합의(`EventShotSchedule`, `EventShotIntervalSec`)와 이벤트 기준점 고정(월드 고정/이벤트 범위) 계약을 추가(구현 예정)
- 2026-02-26: NWay 실행 규약(360도 균등/세트 원자성/이월 시 SpawnSequence 보존)과 발행 단위 계약(밀도형 vs 사건형)을 합의안으로 추가
- 2026-02-26: PointSet 런타임 샘플러를 활성화하고(`Max=4`, 로컬 오프셋), 검증 규칙을 `CV028`/갱신된 `CVW033` 기준으로 동기화
- 2026-02-26: `WaveClipSO` 내부 segment 중첩을 전면 허용하도록 정책/검증 문구를 갱신(`CV011` 제거)
- 2026-02-25: `WaveClipSO` 기반 v3 단일 경로 반영 상태(규약/검증/CV 코드)로 문서를 동기화
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
