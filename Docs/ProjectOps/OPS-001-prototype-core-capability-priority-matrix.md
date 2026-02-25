# 프로토타입 코어 기능 계획문서

## Metadata
- doc_id: `OPS-001`
- type: `ProjectOps`
- status: `active`
- last_updated: `2026-02-25`
- related_adr:
  - [ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md](../ADR/ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)

> 15~20분 규모 1인 플레이 미니게임 프로토타입을 빠르게 안정화하기 위한 코어 기능 우선순위 문서

## 1. 문서 목적
- 프로젝트 초기에 무엇을 먼저 구현해야 하는지 합의한다.
- 기능 우선순위와 예상 비용을 한 번에 비교한다.
- 구조적으로 나중에 붙이기 어려운 항목을 먼저 식별한다.

## 2. 평가 기준
- 중요도:
  - `P0`: 초반 필수(미도입 시 전체 개발 속도/안정성 급락)
  - `P1`: 중반 필수(콘텐츠 생산성과 품질에 큰 영향)
  - `P2`: 후반 강화(있으면 좋지만 초반 필수는 아님)
- 구조 고착 리스크(1~5): 프로젝트 구조와의 결합도 + 후행 도입 시 재작업 비용을 합친 지표
  - `5`: 초기에 안 넣으면 후반 비용이 매우 큼
  - `3`: 중간에 넣어도 가능하지만 리팩터링 비용이 발생
  - `1`: 후반 도입도 비교적 안전
- 상대 공수 비율: 전체 코어 기능 작업량을 100으로 봤을 때의 비중
- 진행 상태 코드:
  - `DONE`: 구현/검증 완료
  - `WIP`: 진행 중
  - `TODO`: 미착수
  - `BLOCK`: 외부 의존/이슈로 진행 차단

## 3. 진행 현황 스냅샷
- 전체(10): `DONE 7 | WIP 0 | TODO 3 | BLOCK 0` (완료율 70%)
- `P0`(5): 5/5 완료 (100%)
- `P1`(4): 2/4 완료 (50%)
- `P2`(1): 0/1 완료 (0%)

## 4. 우선순위 표
| 순서 | 코어 기능 | 중요도 | 구조 고착 리스크 | 상대 공수 비율 | 상태 | 진행률 | 다음 액션 | 최근 갱신 |
|---|---|---|---:|---:|---|---:|---|---|
| 1 | 프레임 파이프라인 고정 (`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`) | P0 | 5 | 4% | DONE | 100% | 계약 테스트 유지보수 | 2026-02-20 |
| 2 | Spawn/Despawn 요청-소비 구조 (Aggregated 요청 버퍼 + Owner 소비) | P0 | 5 | 8% | DONE | 100% | 스트레스 자동화 루틴에 운영 임계치 검증 추가 | 2026-02-20 |
| 3 | 풀링 + Fence 표준화 (FreeList/CellMap 접근 규약) | P0 | 5 | 11% | DONE | 100% | 계약 테스트 + 스트레스 지표 회귀 추적 유지 | 2026-02-20 |
| 4 | 디버그 HUD + 스트레스 스위치 (엔티티 수, 처리량, 프레임타임) | P0 | 2 | 7% | DONE | 100% | 운영 씬 적용 범위/표시 정책 확정 | 2026-02-23 |
| 5 | 스모크 테스트 루틴 (Play 진입, 핵심 루프, 대량 스폰/제거) | P0 | 3 | 8% | DONE | 100% | 전용 씬(작업 완료) + 운영 씬(정기) 2트랙 유지 | 2026-02-23 |
| 6 | 콘텐츠 검증기 (Authoring Validator) | P1 | 3 | 8% | DONE | 100% | 규칙/게이트 테스트 유지보수 | 2026-02-25 |
| 7 | 데이터 주도 패턴 정의 (패턴/웨이브/보상 분리) | P1 | 4 | 14% | DONE | 100% | 시나리오 지표 회귀 추적 유지 | 2026-02-25 |
| 8 | 런 타임라인 디렉터 (15~20분 페이싱) | P1 | 4 | 14% | TODO | 0% | 착수 전 | 2026-02-20 |
| 9 | 공통 전투 이벤트 채널 (피격/회피/수집/정리 집계) | P1 | 4 | 8% | TODO | 0% | 착수 전 | 2026-02-20 |
| 10 | 재현 가능한 시드/리플레이 최소 기반 | P2 | 4 | 18% | TODO | 0% | 착수 전 | 2026-02-20 |

## 5. 권장 실행 순서
1. `P0` 완료로 구조 안정성 확보: 1~5번
2. `P1`로 콘텐츠 제작/밸런싱 속도 확보: 6~9번
3. `P2`로 회귀 분석 효율 강화: 10번

## 6. 일정 감각(비율 기준)
- 단계별 상대 비중:
  - `P0`: 38%
  - `P1`: 44%
  - `P2`: 18%
- 운영 권장:
  - 전체 주기에서 `설계/검증` 비중을 최소 55% 이상으로 둔다.
  - `코드 작성/정리`는 45% 내에서 Codex 자동화로 압축한다.
- 해석:
  - 본 문서는 "시간(일수)"이 아니라 "전체 작업량 대비 비율"을 기준으로 우선순위를 맞춘다.
  - 실제 일정은 팀/툴 숙련도에 따라 스케일만 변경하고, 비율은 최대한 유지한다.

## 7. 코어 기능 상세(항목별 1줄)
1. 프레임 파이프라인 고정: `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`를 코드로 강제해 시스템 소유권/순서 꼬임을 초기에 차단한다.
2. Spawn/Despawn 요청-소비 구조: Spawn은 Aggregated 요청 버퍼를 Request에서 생성하고 ExecutionBegin Owner가 예산 기반으로 소비하며, Despawn은 요청-소비 경계를 유지해 대량 엔티티 안정성을 확보한다.
3. 풀링 + Fence 표준화: Bullet Pool/FreeList, CellMap 접근을 Fence(JobHandle) 규약으로 묶어 race condition을 예방한다.
4. 디버그 HUD + 스트레스 스위치: 엔티티 수/스폰/디스폰량/프레임타임을 실시간 표시하고 극단 부하 테스트를 즉시 실행 가능하게 만든다.
5. 스모크 테스트 루틴: Play Mode 진입, 핵심 루프 1회, 대량 스폰/제거 1회를 자동 검사해 변경-검증 반복 비용을 낮춘다.
6. 콘텐츠 검증기: 전용 규칙 파일(`ContentValidationRules.cs`)로 검사하며, `Duplicate DefinitionId`는 Error, 자동 보정 항목은 Warning으로 분리해 실행 전 오류를 차단한다.
7. 데이터 주도 패턴 정의: 방향성은 `GD-007`, 런타임 계약은 `TD-002`, 스폰 분해 모델은 `TD-003`으로 분리해 패턴/웨이브/진행도의 기획-구현 경계를 고정하고 반복 실험 속도를 높인다.
8. 런 타임라인 디렉터: 15~20분 플레이 구간의 난이도 곡선(구간, 피크, 휴식)을 시간축으로 일관되게 관리한다.
9. 공통 전투 이벤트 채널: 피격/회피/수집/정리 이벤트를 단일 집계 경로로 통합해 연출, 점수, 통계가 같은 소스를 보게 한다.
10. 재현 가능한 시드/리플레이 최소 기반: RNG 시드 고정과 입력 기록 최소 구조를 마련해 회귀 버그 추적 비용을 크게 줄인다.

## 8. 다른 컨텍스트에 이식할 때
- 장르가 달라도 `1~3번(파이프라인/요청-소비/동시성 규약)`은 거의 그대로 유지한다.
- 게임 고유성은 `7~9번(데이터/타임라인/이벤트 채널)`에서 조정한다.
- `10번(시드/리플레이)`은 우선순위는 낮춰도, 최소한 RNG 시드 고정은 초반에 도입한다.

## 9. 문서 사용 규칙
- 이 문서는 계획 문서이며, 되돌리기 비용이 크거나 파급 영향이 큰 **중요 결정**은 ADR로 기록한다.
- 실제 구현에서 순서/소요가 바뀌면 스냅샷/표를 함께 갱신하고 변경 이유를 1~2줄 남긴다.

## 10. 변경 메모
### 2026-02-23
- `#7 데이터 주도 패턴 정의`를 `WIP`로 전환했다.
  - `GD-007`을 기획 방향성 문서로 정리했다(경험 설계 원칙/스케일 앵커/지표 중심).
  - 구현 계약(데이터 스키마/수식/검증 규칙)을 `TD-002`로 분리했다.
  - 다음 단계는 `TD-002`의 `Error/Warning` 규칙을 `ContentValidationRules.cs`에 1차 반영하고 EditMode 게이트에 연결하는 것이다.
- `#6 콘텐츠 검증기`를 `WIP`로 전환했다.
  - 고정 규칙 파일(`ContentValidationRules.cs`)을 추가하고, 규칙 변경은 코드 리뷰 경로로만 반영하도록 시작점을 고정했다.
  - 정책 확정:
    - `Duplicate DefinitionId`는 즉시 `Error`로 승격한다.
    - 베이크/런타임 자동 보정 대상 값은 동작을 유지하되 `Warning`으로 보고한다.
    - 무렌더 Bullet(`BulletAuthoring`에 `MeshRenderer/SkinnedMeshRenderer` 없음)는 `Error`로 차단한다.
    - `BulletSourceAuthoring.WaveTimeline == null`은 `Error`로 차단한다.
    - `Warning` 출력은 상한을 두고(기본 100건), 초과분은 집계로만 보고한다.
  - 실행 루트(`ContentValidationRunner`)를 추가해 `Tools/Project/Validate Content`에서 수동 검증을 수행할 수 있게 했다.
  - EditMode 테스트(`ContentValidationRulesTests`)를 추가해 위 정책(Error/Warning 분리)을 계약으로 고정했다.
  - 검증 범위를 `Assets/_Project`로 제한해 운영 대상 콘텐츠만 검사하도록 고정했다.
  - EditMode 게이트(`ContentValidationGateTests`)를 추가해 콘텐츠 검증 `Error` 0건을 CI/로컬 루틴에서 강제한다.
- `#4 디버그 HUD + 스트레스 스위치`를 `WIP`로 전환했다.
  - ECS 단일 singleton 지표(`DebugHudMetricsComponent`)를 추가해 active/spawn/despawn(back-calc)/pending/frameTime을 프레임 단위로 수집한다.
  - 스트레스 명령 singleton(`StressSwitchStateComponent`)과 Request 단계 소비 시스템을 추가해 `BurstOnce`/`Sustain`/`StopSustain`을 기존 요청-소비 파이프라인 안에서 실행한다.
  - Mono 브리지(`BulletDebugHudBridge`)를 추가해 OnGUI에서 실시간 지표 표시와 스트레스 스위치 입력을 제공한다.
  - EditMode 테스트(`StressSwitch_BurstOnce_InjectsRequests_AndUpdatesHudMetrics`)를 추가해 burst 요청 주입/소비와 HUD 수집값 반영을 검증했다.
- `#4 디버그 HUD + 스트레스 스위치`를 `DONE`으로 전환했다.
  - 전용 PlayMode 테스트 씬(`PlayModeSmoke_Dedicated`)에 `BulletDebugHudBridge`를 배치했다.
  - PlayMode 스모크 테스트(`PlayMode_DedicatedScene_StressSwitch_BurstRequest_ImpactsBacklogAndHud`)를 추가해 스트레스 burst가 backlog/HUD에 반영되는 경로를 자동 검증했다.
- `#5 스모크 테스트 루틴` 운영 합의를 갱신했다.
  - `작업 완료마다 전용 PlayMode 테스트 씬 스모크 강제` + `정기 운영 씬 스모크` 2트랙으로 운영한다.
  - PlayMode 1차 판정은 기동/루프 정상성으로 고정하고, 성능 임계치는 추적 항목으로 분리한다.
- `#5 스모크 테스트 루틴`을 `DONE`으로 전환했다.
  - 전용 씬 `Assets/_Project/01_Scenes/PlayModeTests/PlayModeSmoke_Dedicated.unity`를 추가했다.
  - `BulletPlayModeSmokeTests`를 `전용 씬(기본)` + `운영 씬(정기)` 2개 테스트로 분리했다.
  - 결과: 2개 PlayMode 스모크 모두 pass.

### 2026-02-25
- `#7 데이터 주도 패턴 정의` 진행률을 상향했다.
  - Spawn 규약 업데이트를 반영했다:
    - `WaveTimelineSO.SpawnEntry`는 인라인 프로필 전용(legacy fallback 제거)
    - 샘플링은 `LineEven` 중심으로 운영하고, 벽 발사는 `LineEven + Direction` 조합으로 표현
    - EventBurst 소비는 carry 정책 유지, 예산은 탄종 공용, Trash 최하 우선순위
  - 후속 작업:
    - 샘플 시나리오용 실제 WaveTimeline 데이터 세팅(초기/전환/전환후 3구간)
    - 시나리오 성립 검증용 PlayMode 스모크/지표 기준선 추가
- `#6 콘텐츠 검증기`를 `DONE`으로 전환했다.
  - 게이트 실패 메시지 식별성을 강화했다(정렬된 에러 요약 + 코드 히스토그램).
  - Warning 상한 정책(기본 100, 초과 suppress 집계)을 단위 테스트로 고정했다.
  - 에셋/이슈 정렬을 결정론적으로 맞춰 게이트 플래키를 줄였다.
- `#7 데이터 주도 패턴 정의`를 `DONE`으로 전환했다.
  - `bwt_from_bsp_default`에 초기/전환/전환후 3구간 시나리오를 authoring했다.
  - LineEven 중심 샘플링 + EventBurst(carry) + 공용 예산 + Trash 최하 우선순위 규약을 유지했다.
  - PlayMode 전용 씬에 데이터 시나리오 스모크를 추가하고 baseline 지표(active/pending/oldest/drop/expired) 로그를 남기도록 반영했다.

### 2026-02-20
- `#1 프레임 파이프라인 고정`을 `DONE`으로 갱신했다.
  - 루트 그룹(`BulletFramePipelineGroup`) 도입, Request fence publish 단일화, 프레임 카운터 기반 Frame ID 전환을 반영했다.
- `#3 풀링 + Fence 표준화`를 `WIP`로 갱신했다.
  - CellMapFence 최종 publish 단일화는 반영했으며, 전체 사용처 점검과 스트레스 수치화는 후속이다.
- `#5 스모크 테스트 루틴`을 `WIP`로 갱신했다.
  - Editor 계약 테스트를 추가했고, PlayMode 자동 스모크는 후속이다.
- `#2 Spawn/Despawn 요청-소비 구조` 상세 합의를 ADR로 분리했다.
  - OPS에는 우선순위/실행 순서만 유지하고, 설계 상세는 `ADR-20260220-02`를 참조한다.
- `#2 Spawn/Despawn 요청-소비 구조`를 `DONE`으로 갱신했다.
  - `SourceSpawnRequestBuildSystem` + `SpawnRequestRoundRobinExecutionSystem` + `SpawnBacklogWarningSystem` 분리/정리 반영.
  - backlog 정책 기본값을 `BudgetPerFrame=1024`, `MaxPendingCount=32768`, `MaxPendingAgeFrames=120`으로 조정.
- `#3 풀링 + Fence 표준화` 진행률을 상향했다.
  - 비활성화된 `BulletSpawnFromPoolSystem`를 기본 경로에서 제거하고 레거시 파일로 분리해 Owner 경계를 단순화했다.
- `#3 풀링 + Fence 표준화` 계약 테스트를 확장했다.
  - `FenceOwnershipContractTests`를 추가해 `PoolFence/CellMapFence` 런타임 publish owner와 Request reader 결합 규칙을 고정했다.
  - Editor 스모크/스트레스 테스트(`BulletSmokeAndStressTests`)를 추가해 수치 첨부를 완료했다.
  - 측정값(자동 테스트): `maxBudgetUsed=5000`, `maxPending=5000`, `maxOldestAge=0`, `dropCount=0`, `expiredByAge=0`.
  - `#3`를 `DONE`으로 전환했다.
- `#5 스모크 테스트 루틴` 진행률을 상향했다.
  - `Smoke_CoreLoopAndBurstSpawnDespawn_RunWithoutHardLimit` 테스트를 추가해 핵심 루프 + 대량 스폰/디스폰 자동 검증을 도입했다.
  - 현재는 Editor World 기반 자동화까지 반영됐고, 운영 씬 기반 PlayMode 자동화는 후속이다.

## 11. Fence 점검 체크리스트 (실행용)
- 대상: `FreeByKey`, `CellMap`, `HazardCellMap`, `PoolFence`, `CellMapFence`
- 규칙 1: SharedStatic 컨테이너를 직접 읽거나 쓰는 시스템은 반드시 대응 fence를 `Combine(state.Dependency, fence)`로 결합한다.
- 규칙 2: `CellMapFence` 최종 publish는 `BulletRequestFencePublishSystem` 단일 책임으로 유지한다.
- 규칙 3: `PoolFence` 런타임 publish는 Spawn Owner/Despawn Owner만 수행한다.
- 규칙 4: Request 단계는 CellMap `ReadOnly` 조회 + 요청 태그(enable)만 수행하고, 풀 반납/렌더 off는 ExecutionEnd에서만 수행한다.
- 검증 소스:
  - `Assets/_Project/99_Tests/Editor/BulletPipelineContractTests.cs`
  - `Assets/_Project/99_Tests/Editor/FenceOwnershipContractTests.cs`

## 12. 스트레스 수치 기록 템플릿
- 실행 날짜: `YYYY-MM-DD`
- Unity: `6000.3.6f1`
- 시나리오:
  - `A`: 대량 스폰 지속(`N`프레임)
  - `B`: 동프레임 대량 제거(목표 `10만`)
- 정책값:
  - `BudgetPerFrame=`
  - `MaxPendingCount=`
  - `MaxPendingAgeFrames=`
- 관측값(최소):
  - `frame_spawn_budget_used(최대/평균)=`
  - `pending_backlog_count(최대)=`
  - `oldest_backlog_age(최대)=`
  - `drop_count(합계)=`
  - `expired_by_age(합계)=`
- 판정:
  - 완료판 목표: `drop_count == 0`, `expired_by_age == 0`
  - 임계치 도달/초과 시 원인과 조정값을 1~2줄로 기록

### 최근 측정값 (2026-02-20, Editor 자동 테스트)
- 실행 테스트: `BulletSmokeAndStressTests.Smoke_CoreLoopAndBurstSpawnDespawn_RunWithoutHardLimit`
- Unity: `6000.3.6f1`
- 시나리오: `A+B` (핵심 루프 180프레임, 버스트 스폰/즉시 디스폰 반복)
- 정책값:
  - `BudgetPerFrame=6000`
  - `MaxPendingCount=32768`
  - `MaxPendingAgeFrames=120`
- 관측값:
  - `maxBudgetUsed=5000`
  - `maxPending=5000`
  - `maxOldestAge=0`
  - `dropCount=0`
  - `expiredByAge=0`

### 최근 측정값 (2026-02-23, PlayMode 자동 테스트)
- 실행 테스트(기본): `BulletPlayModeSmokeTests.PlayMode_DedicatedScene_PipelineBootAndCoreLoop_RunWithoutHardErrors`
- Unity: `6000.3.6f1`
- 씬: `Assets/_Project/01_Scenes/PlayModeTests/PlayModeSmoke_Dedicated.unity`
- 관측값:
  - `maxActiveBullets=25467`
  - `framesWithActiveBullets=119`
- 실행 테스트(정기): `BulletPlayModeSmokeTests.PlayMode_OperationalScene_PipelineBootAndCoreLoop_RunWithoutHardErrors`
- Unity: `6000.3.6f1`
- 씬: `Assets/_Project/01_Scenes/SampleScene.unity`
- 관측값:
  - `maxActiveBullets=25514`
  - `framesWithActiveBullets=179`

## 13. 스모크 테스트 절차/규약(운영 합의)
- 테스트 구조:
  - `EditMode 계약 테스트`: 파이프라인/소유권/규약 위반 탐지
  - `EditMode 스모크/스트레스`: ECS 월드 기준 기능/부하 회귀 탐지
  - `PlayMode 스모크`: `전용 씬(기본)` + `운영 씬(정기)` 기동/루프 정상성 탐지
- 실행 절차:
  1. `refresh_unity(compile=request, wait_for_ready=true)`
  2. `read_console(action=get, types=["error"], include_stacktrace=true)`로 에러 0건 확인
  3. `EditMode` 테스트 실행
  4. `PlayMode` 스모크 실행
- 운영 주기:
  - 작업 완료마다: `PlayMode_DedicatedScene_PipelineBootAndCoreLoop_RunWithoutHardErrors` 강제
  - 정기 실행: `PlayMode_OperationalScene_PipelineBootAndCoreLoop_RunWithoutHardErrors` 별도 주기 실행(예: 일 1회, 머지 전 1회)
- 판정 규약:
  - PlayMode 1차 판정: 기동/루프 정상성
  - 성능 임계치 초과: fail이 아닌 추적 항목으로 기록
