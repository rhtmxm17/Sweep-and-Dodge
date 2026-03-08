# 스테이지 데이터(Definition) / 레이아웃(Layout) Dual Catalog 파이프라인 (TD-015)

## Metadata
- doc_id: `TD-015`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-08`
- related_docs:
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)
  - [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
  - [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)
> 현재 런타임은 `StageCatalogSO`를 단일 운영 계약으로 사용한다. `StageTopologyApplyExecutionBeginSystem`이 `StageCatalogRuntimeComponent`에서 `StageId` 기준으로 `StageLayoutSO + StageDefinitionSO`를 직접 resolve하고, `Source/Deposit` topology를 runtime template reconcile로 생성/재사용한 뒤 `Source`는 layout+definition 결합 적용, `Deposit`은 layout 적용을 수행한다.

## 1. 목표 / 비목표
### 1.1 목표
- 스테이지 메타/패턴 정의와 물리 레이아웃 데이터를 분리한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 순서로 고정한다.
- `StageTopologyBridge -> RequestTopologyApply(stageId)` topology 입력 경로를 사용하고, `RunDirectorStageBridge`는 stage state 입력만 담당한다.
- `StageDefinitionSO.SourceBindings`를 런타임 Source에 적용한다.
- 샘플 운영 씬과 에디터 파이프라인을 `StageCatalogSO` 중심으로 닫는다.

### 1.2 비목표 (다음 페이즈 이월)
- `RunDirectorStageConfig/RunProgressDirectorConfig/SpawnRequestPolicy` stage-level override 런타임 적용
- Deposit/Obstacle/Visual 확장 스키마의 런타임 소비
- 운영 빌드 fail-fast 정책 전환

## 2. 현재 상태(코드 기준)
- 런타임 적용은 `StageTopologyApplyExecutionBeginSystem`이 `StageCatalogSO`를 `StageId`로 조회해 `Source/Deposit` runtime topology를 reconcile하고, `Source`는 layout+definition 결합 적용, `Deposit`은 layout 적용을 수행한다.
- `StageTopologyBridge`가 `StageCatalogRuntimeComponent`를 publish하고 topology prefab singleton을 bind한다.
- `DemoShellFlowController`는 `StageCatalogSO`가 있으면 카탈로그에서 `StageProfiles`를 구성하고, 없으면 기존 직렬화 `StageProfiles`를 fallback으로 사용한다.
- `SourceClipRequestBuildSystem`은 stage state gate를 가져 `Running` 전에는 clip request를 만들지 않는다.
- 샘플 자산은 `sc_demo -> Stage1~3 enabled entries`, `sd_demo_1~3`, `sl_demo_1~3`로 구성되며, `StageMapCatalogSO` 경로는 제거됐다.
- 샘플 자산의 SSOT는 `StageLayoutEditingSampleV1.unity`의 marker 구성이다. `sc_demo`, `sd_demo_*`, `sl_demo_*`는 생성물로 취급한다.

## 3. 소유권 (Owner / Writer)
- Definition 생성/보강 Owner: `StageDefinitionGenerator`
- Layout 생성 Owner: `StageLayoutCatalogGenerator`
- Catalog 조립 Owner: `StageCatalogComposer`
- StageCatalog 검증 Owner: `StageCatalogValidationRules`
- StageLayout 검증 Owner: `StageLayoutValidationRules`
- 런타임 Stage topology/apply Owner: `StageTopologyApplyExecutionBeginSystem` (ExecutionBegin)
- GO -> ECS Topology Writer: `StageTopologyBridge`
- GO -> ECS StageState Writer: `RunDirectorStageBridge`

## 4. 업데이트 순서
- 파이프라인 계약 유지:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- Stage apply 순서:
  - `BulletPoolOwnerBootstrapSystem`
  - `StageTopologyApplyExecutionBeginSystem`
  - `BulletFieldAreaUpdateSystem`
- H3 boundary-only apply 계약
  - topology apply는 `Idle`, `Completed`, 초기 비플레이 경계에서만 허용한다.
  - `Running`, `ClearReady` 중 요청은 warning 후 consume만 하고 현재 topology/state는 유지한다.
  - 장주기 스테이지(2분+) 안정성을 위해 mid-run topology reapply는 지원하지 않는다.

## 5. 데이터 구조 / 제약
### 5.1 SO 계약
- `StageCatalogSO`
  - `int SchemaVersion`
  - `StageCatalogEntry[] Entries`
- `StageCatalogEntry`
  - `bool Enabled`
  - `string EntryKey`
  - `StageDefinitionSO Definition`
  - `StageLayoutSO Layout`
- `StageDefinitionSO`
  - `int StageId`
  - `string DisplayName`
  - `bool IsFinalStage`
  - `float StageTimeLimitSec`
  - `StageSourceBinding[] SourceBindings`
- `StageLayoutSO`
  - `int StageId`
  - `StageSourceLayoutData[] Sources`
  - `StageDepositLayoutData[] Deposits`
  - `StageObstacleLayoutData[] Obstacles`
  - `StageVisualLayoutData[] Visuals`

### 5.2 Source 정의 계약
- `StageSourceBinding`
  - `SourceStableId`
  - `InitialSourceState`
  - `ThresholdWeakened`, `ThresholdDepleted`
  - `SustainSlotBinding[]`, `EventSlotBinding[]`
- 런타임 Source 조인 키는 `SourceStableId`다.
- `ThresholdDepleted >= ThresholdWeakened >= 0`
- sustain/event slot clip null 금지
- clip phase 검증:
  - sustain slot: `SourceWavePhaseId.Sustain`
  - event slot: `SourceWavePhaseId.OnStateEnterOnce`
- 불일치 정책: `Warn + partial apply`
  - layout 미매핑 또는 active=false: safe-disable
  - definition binding 미매핑: safe-disable
  - definition stage 미존재: layout-only apply

### 5.3 runtime template authoring 책임 (`SourceRuntimeTemplateAuthoring` / `DepositRuntimeTemplateAuthoring`)
- 주 경로 authoring
  - `StableIdOverride`
  - field shape/radius/size
  - pollution grid/config
  - gizmo/debug
- legacy alias (`BulletSourceAuthoring`, `DepositPointAuthoring`)에 남는 migration data
  - `SustainClipSlots[]`, `EventClipSlots[]`
  - `ThresholdWeakened`, `ThresholdDepleted`, `InitialCollectedCount`, `InitialState`
- 새 runtime template authoring baker는 deprecated seed 필드로 runtime clip/threshold/state를 bake하지 않는다. neutral 기본값만 bake하고, 실제 정의는 `StageDefinitionSO` apply가 책임진다.

## 6. 에디터 파이프라인
- `StageLayoutCatalogGenerator`
  - 단일 스테이지 `StageLayoutSO` 생성만 담당한다.
- `StageDefinitionGenerator`
  - `sync/overwrite`가 아니라 `additive/reconcile` 도구다.
  - source runtime template authoring을 기준으로 stable id 중 `StageDefinitionSO`에 없는 binding만 생성한다.
  - H2 동안은 `SourceRuntimeTemplateAuthoring`을 주 경로로 읽고 legacy `BulletSourceAuthoring`도 함께 읽는다.
  - 기존 binding의 clip/threshold/state 값은 덮어쓰지 않는다.
  - orphan binding은 제거하지 않고 warning만 남긴다.
- `StageCatalogComposer`
  - `Definition/Layout`를 명시적 페어 엔트리로 조립한다.
- `ContentValidationRunner`
  - `StageCatalogSO`, `StageLayoutSO`, `StageDefinitionSO` 기준으로 검증한다.
- 샘플 갱신 루틴(정식)
  1. `StageLayoutEditingSampleV1.unity`에서 marker를 수정한다.
  2. `StageDefinitionGenerator`로 누락 binding을 보강한다.
  3. `StageLayoutCatalogGenerator`로 `sl_demo_*`를 갱신한다.
  4. `StageCatalogComposer`로 `sc_demo`를 갱신한다.
  5. 생성된 asset을 검증/커밋한다.

## 7. 런타임 반영
- `DemoShellFlowController`
  - 시작 시 `StageCatalogSO`를 읽어 런타임 `StageProfiles` 구성
  - 미할당/유효 엔트리 없음 시 기존 `StageProfiles` fallback
- `SampleScene`
  - `DemoShellFlowController.StageCatalog = sc_demo`
  - `StageTopologyBridge`는 `StageCatalog`를 참조한다.
  - `RunDirectorStageBridge`는 stage state/gate/signal만 다룬다.
- `EnterStagePlay`
  - 선택 엔트리의 `Definition.StageId`를 사용해 `StageTopologyBridge.RequestTopologyApply(stageId)` 호출
- `StageTopologyApplyExecutionBeginSystem`
  - `RequestedStageId`로 layout/definition을 각각 resolve
  - `Source`는 layout+definition 결합 적용
  - `Deposit`은 layout 적용
  - 성공 apply 후 현재 stage에 매핑되지 않은 owned entity는 `disable-to-pool`로 전환한다.
  - infrastructure failure(`StageCatalog`/entry/layout/template/instantiate 실패)에서는 기존 applied topology를 유지하고 `SelectedStageId`에 대해서만 `Ready=0`을 남긴다.
  - definition/source mismatch, duplicate stable id, active=false는 `warn + partial apply`로 처리하고 stage 전체 `Ready`는 유지한다.
  - `OnStateEnterOnce`는 initial apply 직후 자동 발화하지 않음

## 8. 검증 계획 / 합격 기준
- 공통
  - compile error 0
  - console error 0
  - EditMode pass
  - PlayMode smoke pass
- EditMode
  - `StageCatalogValidationRulesTests`
  - `StageLayoutValidationRulesTests`
  - `StageDefinitionGeneratorTests`
  - `DemoShellFlowControllerStageCatalogTests`
  - `StageCatalogSampleAssetsTests`
  - `StageTopologyBridgeTests`
  - `RunDirectorStageBridgeTests`
  - `StageTopologyApplyExecutionBeginSystemTests`
- PlayMode
  - DemoShell 회귀 스모크(`Title -> Lobby -> Stage -> Result -> Retry/Next`)
  - `Stage2` layout/pattern 차이 반영 확인

## 9. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
- [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)






