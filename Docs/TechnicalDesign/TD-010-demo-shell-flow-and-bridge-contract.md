# 데모 셸 플로우 및 브리지 계약 (TD-010)

## Metadata
- doc_id: `TD-010`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-26`
- related_docs:
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [TD-025-stage-player-start-position-contract.md](./TD-025-stage-player-start-position-contract.md)
  - [TD-022-in-world-dialogue-runtime-contract.md](./TD-022-in-world-dialogue-runtime-contract.md)
  - [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
  - [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)
  - [ADR-20260309-02-stage-session-reset-and-prepare-owner.md](../ADR/ADR-20260309-02-stage-session-reset-and-prepare-owner.md)
  - [ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md](../ADR/ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md)
> DemoShell은 화면 전이 Owner를 유지하고, topology 입력은 `StageTopologyBridge`, stage state 입력은 `RunDirectorStageBridge`를 통해 ECS에 전달한다. 현재 런타임은 `StageCatalogSO + StageTopologyPrefabCatalog`를 publish하며, topology apply one-shot의 정식 API는 `StageTopologyBridge.RequestTopologyApply(stageId)`다. 이 요청은 stage entry reset과 topology apply를 함께 의미한다. `StageClear` 경로는 `TD-022` 기준으로 `Result` 전 인월드 연출 대화 완료까지 defer될 수 있다.

## 1. 목표 / 비목표
### 1.1 목표
- DemoShell 화면 전이를 단일 소유한다.
- GO->ECS 쓰기 경로를 `StageTopologyBridge`와 `RunDirectorStageBridge`의 이원 경계로 분리한다.
- Stage 시작 전에 `StageTopologyBridge.RequestTopologyApply(stageId)`를 선행해 topology + layout + definition 적용을 보장한다.
- 현재 topology 적용 범위인 `Source / Deposit / Obstacle`가 stage 시작 전에 모두 준비되도록 한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 기반으로 데이터 주도화한다.

### 1.2 비목표
- `Presentation` continuous follow / pooling / addressable 전환
- StageDefinition의 stage-level override 적용

## 2. 소유권 (Owner / Writer)
- DemoShell Owner: `DemoShellFlowController`
  - 화면 상태 전이
  - 로비 선택/결과 선택 후속 처리
  - StageCatalog 로딩과 fallback 결정
  - `StageStart` / `StageClear` 인월드 연출 대화 트리거 및 완료 후속 처리
- GO->ECS Topology Writer: `StageTopologyBridge`
  - `RequestTopologyApply(int stageId)`
  - `StageCatalogRuntimeComponent` publish/bind
- GO->ECS StageState Writer: `RunDirectorStageBridge`
  - `RequestStageStart()`, `RequestConfirm()`
  - `SetIntroPresentationDone(bool)`, `SetClearPresentationDone(bool)`
- ECS stage session reset Owner: `StageSessionResetPrepareSystem`
- ECS stage topology/apply Owner: `StageTopologyApplyPrepareSystem`
- ECS Stage 상태/전이 Owner: 기존 시스템 유지 (`RunDirectorStageTransitionSystem` 등)

## 3. 업데이트 순서 / 전이 계약
- 파이프라인 순서:
  - `StageTopologyPrepareGroup -> FixedTickRootGroup`
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- prepare 계층 순서:
  - `StageTopologyBootstrapSystem`
  - `StageSessionResetPrepareSystem`
  - `StageTopologyApplyPrepareSystem`
  - `PlayerStageEntryApplyPrepareSystem`
- StagePlay 시작 루프:
  1. `StageTopologyBridge.RequestTopologyApply(stageId)` 성공
  2. `SetIntroPresentationDone(true)` + `SetClearPresentationDone(false)`
  3. `RequestStageStart()`
- start dialogue 기본값
  - `TD-022` v1 기본값은 `StageStart=OverlayOnly`다.
  - 따라서 시작 대화가 존재하더라도 `IntroPresentationDone=true`를 유지하고 `Idle -> Running`을 막지 않는다.
- clear dialogue 계약
  - `RunDirectorStageStateId.ClearReady` 관측 직후 `DemoShellFlowController`는 즉시 `StageResult`로 가지 않을 수 있다.
  - `StageClear` 인월드 연출 대화가 활성인 경우, 완료 전까지 `SetClearPresentationDone(true)`와 `RequestConfirm()`를 보내지 않는다.
  - 대화 완료 또는 skip 이후에만 `SetClearPresentationDone(true)` + `RequestConfirm()`를 보내고, `StageRunCompleted` 수신 후 `StageResult`로 전환한다.
- session reset 계약
  - `RequestTopologyApply(stageId)`는 stage entry reset을 포함한다.
  - reset owner는 `StageTopologyPrepareGroup`의 ECS 시스템이다.
  - world recreation이나 scene reload에 의존하지 않고 session singleton + player stage-entry transient state를 명시적으로 초기화한다.
  - same-frame `apply -> start`를 유지하기 위해 `StageStartRequested`와 intro/clear gate는 reset 중 보존한다.
  - stage entry transient reset 범위에는 `PlayerCarryBin.Load`, `PlayerHazardRisk/HazardPenalty` 임시 상태, request tag/context, UI feedback snapshot/buffer, HUD snapshot seed가 포함된다.
  - stage-specific player spatial state는 reset이 아니라 `PlayerStageEntryApplyPrepareSystem`에서 반영한다.
    - 적용 대상: `LocalTransform`, `PlayerGoSyncComponent`, `PlayerPreviousPositionComponent`
- H3 long-cycle 규칙
  - topology apply는 stage 경계(`Idle`, `Completed`, 초기 부트스트랩)에서만 허용한다.
  - `Running`, `ClearReady` 중 topology apply 요청은 warning 후 무시되고 현재 stage topology는 유지된다.
  - 2분 이상 장주기 스테이지를 기준으로 mid-run topology reapply는 지원하지 않는다.
- `DemoShellFlowController`는 ECS 직접 write 금지

## 4. 데이터 구조 및 입력 계약
- `DemoShellStageProfile`
  - `StageId`, `DisplayName`, `IsFinalStage`, `StageTimeLimitSec`
- `DemoShellFlowController`
  - `StageCatalogSO StageCatalog`
  - `DemoShellStageProfile[] StageProfiles` (fallback)
- StageCatalog 로딩 계약
  - `StageCatalog` 할당 시: `Entries` 순서대로 `Enabled=true` 엔트리만 런타임 프로필 구성
  - `StageCatalog` 미할당/유효 엔트리 없음 시: 직렬화된 `StageProfiles` 사용
  - 로딩 중 불일치(예: null Definition/Layout, StageId 중복/불일치)는 경고 후 skip
  - `sc_demo`는 수작업 운영 자산이 아니라 편집 씬 + generator/composer가 만든 생성물로 취급한다
- Bridge runtime publish 계약
  - `StageTopologyBridge`가 `StageCatalogRuntimeComponent`를 최신화한다
  - topology prefab singleton은 `StageTopologyBridge`가 bind/보강한다
  - `RunDirectorStageBridge`는 topology singleton이 없어도 run-director singleton만으로 bind된다
  - stage session reset은 `StageTopologyPrepareGroup` owner가 수행한다

## 5. Topology Boundary
- `StageTopologyBridge`
  - topology 입력 전용 GO->ECS writer
  - `StageCatalogRuntimeComponent` publish
  - `StageTopologyRequestComponent` one-shot write
  - `RequestTopologyApply(stageId)`는 stage entry reset을 포함한다
  - `StageTopologyStateComponent`는 read-only 조회만 허용
- `RunDirectorStageBridge`
  - stage state/gate/signal 입력 전용 GO->ECS writer
  - topology singleton이 없어도 독립 bind
- same-frame 계약
  - `RequestTopologyApply(stageId)`와 `RequestStageStart()`는 같은 프레임에 연속 호출 가능
  - `RunDirectorStageTransitionSystem`은 `StageTopologyState.Ready == 1` 및 `AppliedStageId == SelectedStageId`일 때만 `Idle -> Running`을 허용한다

## 6. StageId 경로
- 로비 선택 -> `EnterStagePlay(stageIndex)`
- `stageIndex`에 대응하는 런타임 프로필의 `StageId` 결정
- `StageTopologyBridge.RequestTopologyApply(StageId)` 호출
- 이후 기존 Stage start 경로 유지
- clear 경로는 `TD-022` 기준으로 `ClearReady -> pre-result clear dialogue -> confirm -> completed -> result` 순서를 사용한다

## 7. 씬/운영 기준
- `SampleScene`
  - `DemoShellFlowController.StageCatalog = sc_demo`
  - `StageTopologyBridge`는 `StageCatalog`를 참조한다
  - `StagePresentationRuntimeController`는 `StageCatalog`, `StagePresentationCatalogSO`, `StageTopologyBridge`를 참조한다
  - `RunDirectorStageBridge`는 stage state/gate/signal만 다룬다
  - `DemoShellDialogueBridge`와 `RuntimeUiRoot.PresentationLayer`는 GO-only reader 계층으로 붙는다
  - 현재 stage entry prepare 범위는 `Source / Deposit / Obstacle`다
  - `Presentation`은 GO-only layer로, `AppliedStageId + Ready` 기준 stage entry rebuild만 수행한다
  - 미연결 시 기존 `StageProfiles` fallback 동작
  - `sc_demo` 갱신은 `StageLayoutEditingSampleV1.unity` 수정 후 generator/composer 실행으로 수행한다
- `PlayModeSmoke_Dedicated`
  - 기존 스모크 목적 유지

## 8. 검증 계획 / 합격 기준
- 공통
  1. compile
  2. console error 0
  3. EditMode pass
  4. PlayMode smoke pass
- EditMode 추가
  - `DemoShellFlowControllerStageCatalogTests`
    - Entries 순서 반영
    - Enabled 필터
    - fallback 동작
  - `StageTopologyBridgeTests`
    - `RequestTopologyApply`
    - topology state read helper
    - topology singleton publish/bind
  - `RunDirectorStageBridgeTests`
    - topology singleton 없이 독립 bind
  - `StageTopologyApplyPrepareSystemTests`
    - boundary-only apply
    - explicit stage-entry reset
    - lifecycle stamp/versioning
    - failure keep-current-stage policy
  - `StagePresentationCatalogValidationRulesTests`
    - duplicate key
    - null prefab
    - usage mismatch
  - `StagePresentationRuntimeControllerTests`
    - `Ready` edge rebuild
    - `AppliedStageId` change rebuild
    - `Ready -> 0` clear
    - linked target resolve
  - `StageCatalogSampleAssetsTests`
    - `sc_demo` / `sd_demo_1~3` / `sl_demo_1~3` 자산 유효성
- PlayMode 회귀
  - `Title -> Lobby -> Stage -> Result -> Retry/Next -> DemoComplete`
  - `Stage2` layout/pattern/obstacle 차이 반영
  - presentation rebuild (`Stage1 -> Next -> Stage2`, `Retry`) 반영
  - `StageClear` 인월드 연출 대화 완료 전에는 `StageResult`가 열리지 않는지 검증
  - `StageClear` 인월드 연출 대화 skip 후 `Completed -> StageResult`가 정상 진행되는지 검증

## 9. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
- [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)
- [ADR-20260309-02-stage-session-reset-and-prepare-owner.md](../ADR/ADR-20260309-02-stage-session-reset-and-prepare-owner.md)
- [ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md](../ADR/ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md)

## 10. 변경 이력
- 2026-03-20: stage entry reset scope를 player transient state까지 확장 반영했다. `CarryBin/HazardStack/UI feedback/HUD snapshot`이 새 stage 진입 직후 stale 값을 노출하지 않도록 prepare owner 책임 범위를 문서에 명시했다.
- 2026-03-26: `TD-025` 연계 반영. stage entry prepare 순서에 `PlayerStageEntryApplyPrepareSystem`을 추가했고, stage-specific player spatial state는 reset이 아니라 post-apply prepare owner가 반영하도록 계약을 명시했다.
- 2026-03-16: `TD-022` 연계 반영. `StageStart=overlay` 기본값과 `StageClear`의 `pre-result clear dialogue -> confirm -> result` defer 계약을 추가했다.





