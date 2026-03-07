# 데모 셸 플로우 및 브리지 계약 (TD-010)

## Metadata
- doc_id: `TD-010`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-07`
- related_docs:
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)

> DemoShell은 화면 전이 Owner를 유지하고, topology 입력은 `StageTopologyBridge`, stage state 입력은 `RunDirectorStageBridge`를 통해 ECS에 전달한다. 현재 런타임은 `StageCatalogSO + StageTopologyPrefabCatalog`를 publish하며, topology apply one-shot의 정식 API는 `StageTopologyBridge.RequestTopologyApply(stageId)`다.

## 1. 목표 / 비목표
### 1.1 목표
- DemoShell 화면 전이를 단일 소유한다.
- GO->ECS 쓰기 경로를 `StageTopologyBridge`와 `RunDirectorStageBridge`의 이원 경계로 분리한다.
- Stage 시작 전에 `RequestStageTopologyApply(stageId)`를 선행해 topology + layout + definition 적용을 보장한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 기반으로 데이터 주도화한다.

### 1.2 비목표
- Source 외 Deposit/Obstacle/Visual 확장 소비
- StageDefinition의 stage-level override 적용

## 2. 소유권 (Owner / Writer)
- DemoShell Owner: `DemoShellFlowController`
  - 화면 상태 전이
  - 로비 선택/결과 선택 후속 처리
  - StageCatalog 로딩과 fallback 결정
- GO->ECS Topology Writer: `StageTopologyBridge`
  - `RequestTopologyApply(int stageId)`
  - `StageCatalogRuntimeComponent` publish/bind
- GO->ECS StageState Writer: `RunDirectorStageBridge`
  - `RequestStageStart()`, `RequestConfirm()`
  - `SetIntroPresentationDone(bool)`, `SetClearPresentationDone(bool)`
- ECS stage topology/apply Owner: `StageTopologyApplyExecutionBeginSystem`
- ECS Stage 상태/전이 Owner: 기존 시스템 유지 (`RunDirectorStageTransitionSystem` 등)

## 3. 업데이트 순서 / 전이 계약
- 파이프라인 순서:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- StagePlay 시작 루프:
  1. `StageTopologyBridge.RequestTopologyApply(stageId)` 성공
  2. `SetIntroPresentationDone(true)` + `SetClearPresentationDone(false)`
  3. `RequestStageStart()`
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

## 5. StageId 경로
- 로비 선택 -> `EnterStagePlay(stageIndex)`
- `stageIndex`에 대응하는 런타임 프로필의 `StageId` 결정
- `StageTopologyBridge.RequestTopologyApply(StageId)` 호출
- 이후 기존 Stage start/confirm 경로 유지

## 6. 씬/운영 기준
- `SampleScene`
  - `DemoShellFlowController.StageCatalog = sc_demo`
  - `StageTopologyBridge`는 `StageCatalog`를 참조한다
  - `RunDirectorStageBridge`는 stage state/gate/signal만 다룬다
  - 미연결 시 기존 `StageProfiles` fallback 동작
  - `sc_demo` 갱신은 `StageLayoutEditingSampleV1.unity` 수정 후 generator/composer 실행으로 수행한다
- `PlayModeSmoke_Dedicated`
  - 기존 스모크 목적 유지

## 7. 검증 계획 / 합격 기준
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
  - `RunDirectorStageBridgeTests`
    - topology singleton 없이 독립 bind
  - `StageCatalogSampleAssetsTests`
    - `sc_demo` / `sd_demo_1~3` / `sl_demo_1~3` 자산 유효성
- PlayMode 회귀
  - `Title -> Lobby -> Stage -> Result -> Retry/Next -> DemoComplete`
  - `Stage2` layout/pattern 차이 반영

## 8. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)


