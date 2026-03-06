# 데모 셸 플로우 및 브리지 계약 (TD-010)

## Metadata
- doc_id: `TD-010`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-06`
- related_docs:
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)

> DemoShell은 화면 전이 Owner를 유지하고, Stage 입력은 `RunDirectorStageBridge` 단일 경로로 ECS에 전달한다. Bridge는 `RequestStageMapApply(stageId)` 시 `StageMapCatalogRuntimeComponent`와 `StageCatalogRuntimeComponent`를 함께 publish하며, legacy `StageMapCatalogSO`가 비어 있으면 `StageCatalog.Entry.Layout`로 runtime 호환 카탈로그를 합성할 수 있다.

## 1. 목표 / 비목표
### 1.1 목표
- DemoShell 화면 전이를 단일 소유한다.
- GO->ECS 쓰기 경로를 `RunDirectorStageBridge` 단일 접점으로 유지한다.
- Stage 시작 전에 `RequestStageMapApply(stageId)`를 선행해 맵 적용을 보장한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 기반으로 데이터 주도화한다.

### 1.2 비목표
- StageMapApply Owner 변경
- StageDefinition의 Source 패턴 ECS 재구성 적용 (v1 비범위)
- Obstacle/Visual 런타임 적용

## 2. 소유권 (Owner / Writer)
- DemoShell Owner: `DemoShellFlowController`
  - 화면 상태 전이
  - 로비 선택/결과 선택 후속 처리
  - StageCatalog 로딩과 fallback 결정
- GO->ECS Writer: `RunDirectorStageBridge`
  - `RequestStageMapApply(int stageId)`
  - `RequestStageStart()`, `RequestConfirm()`
  - `SetIntroPresentationDone(bool)`, `SetClearPresentationDone(bool)`
- ECS StageMap/Definition 적용 Owner: `StageMapApplyExecutionBeginSystem`
- ECS Stage 상태/전이 Owner: 기존 시스템 유지 (`RunDirectorStageTransitionSystem` 등)

## 3. 업데이트 순서 / 전이 계약
- 파이프라인 순서:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- StagePlay 시작 루프:
  1. `RequestStageMapApply(stageId)` 성공
  2. `SetIntroPresentationDone(true)` + `SetClearPresentationDone(false)`
  3. `RequestStageStart()`
- `DemoShellFlowController`는 ECS 직접 write 금지

## 4. 데이터 구조 및 입력 계약
- `DemoShellStageProfile`
  - `StageId`, `DisplayName`, `IsFinalStage`, `StageTimeLimitSec`
- `DemoShellFlowController`
  - `StageCatalogSO StageCatalog` (신규)
  - `DemoShellStageProfile[] StageProfiles` (fallback)
- StageCatalog 로딩 계약
  - `StageCatalog` 할당 시: `Entries` 순서대로 `Enabled=true` 엔트리만 런타임 프로필 구성
  - `StageCatalog` 미할당/유효 엔트리 없음 시: 직렬화된 `StageProfiles` 사용
  - 로딩 중 불일치(예: null Definition/Layout, StageId 중복/불일치)는 경고 후 skip
- Bridge runtime publish 계약
  - `StageMapCatalog`가 있으면 이를 runtime singleton에 publish
  - `StageMapCatalog`가 없고 `StageCatalog.Entry.Layout`가 있으면 runtime 호환 `StageMapCatalogSO`를 합성해 publish
  - `StageCatalog`가 있으면 `StageCatalogRuntimeComponent`도 함께 publish

## 5. StageId 경로
- 로비 선택 -> `EnterStagePlay(stageIndex)`
- `stageIndex`에 대응하는 런타임 프로필의 `StageId` 결정
- `RequestStageMapApply(StageId)` 호출
- 이후 기존 Stage start/confirm 경로 유지

## 6. 씬/운영 기준
- `SampleScene`
  - `DemoShellFlowController.StageCatalog` 연결 시 카탈로그 기반 로비 목록 사용
  - 미연결 시 기존 `StageProfiles` fallback 동작
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
- PlayMode 회귀
  - `Title -> Lobby -> Stage -> Result -> Retry/Next -> DemoComplete`

## 8. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
