# 스테이지 데이터(Definition) / 레이아웃(Layout) Dual Catalog 파이프라인 (TD-015)

## Metadata
- doc_id: `TD-015`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-09`
- related_docs:
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)
  - [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
  - [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)
  - [ADR-20260309-01-planar-shape2d-yaw-only-runtime-contract.md](../ADR/ADR-20260309-01-planar-shape2d-yaw-only-runtime-contract.md)
> 현재 런타임은 `StageCatalogSO`를 단일 운영 계약으로 사용한다. `StageTopologyApplyPrepareSystem`이 `StageCatalogRuntimeComponent`에서 `StageId` 기준으로 `StageLayoutSO + StageDefinitionSO`를 직접 resolve하고, `Source / Deposit / Obstacle` topology를 runtime template reconcile로 생성/재사용한다. raw planar shape는 `Shape2DComponent`로 통일하고, gameplay 판정은 `XZ 평면 + yaw-only` semantics를 사용한다. `Source`는 layout+definition 결합 적용과 `SourceShapeDerivedComponent`/pollution grid 재생성을 함께 수행하며, `Deposit`과 `Obstacle`는 layout-only shape apply를 수행한다.

## 1. 목표 / 비목표
### 1.1 목표
- 스테이지 메타/패턴 정의와 물리 레이아웃 데이터를 분리한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 순서로 고정한다.
- `StageTopologyBridge -> RequestTopologyApply(stageId)` topology 입력 경로를 사용하고, `RunDirectorStageBridge`는 stage state 입력만 담당한다.
- `StageDefinitionSO.SourceBindings`를 런타임 Source에 적용한다.
- 샘플 운영 씬과 에디터 파이프라인을 `StageCatalogSO` 중심으로 닫는다.
- `Source / Deposit / Obstacle`의 planar shape raw data를 `Shape2DComponent`로 통일한다.
- `Source Rectangle`의 sampling/occupancy를 `yaw-aware planar OBB`로 고정하고, editor에서는 marker GO의 `pitch/roll`을 0으로 강제한다.

### 1.2 비목표 (다음 페이즈 이월)
- `RunDirectorStageConfig/RunProgressDirectorConfig/SpawnRequestPolicy` stage-level override 런타임 적용
- Obstacle broadphase/query 최적화, Visual 런타임 소비
- 운영 빌드 fail-fast 정책 전환

## 2. 현재 상태(코드 기준)
- 런타임 적용은 `StageTopologyApplyPrepareSystem`이 `StageCatalogSO`를 `StageId`로 조회해 `Source / Deposit / Obstacle` runtime topology를 reconcile하고, `Source`는 layout+definition 결합 apply + 파생 재생성, `Deposit`/`Obstacle`는 layout apply를 수행한다.
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
- 런타임 Stage topology/apply Owner: `StageTopologyApplyPrepareSystem` (`StageTopologyPrepareGroup`)
- GO -> ECS Topology Writer: `StageTopologyBridge`
- GO -> ECS StageState Writer: `RunDirectorStageBridge`

## 4. 업데이트 순서
- 상위 파이프라인 계약:
  - `StageTopologyPrepareGroup -> FixedTickRootGroup`
  - fixed-tick runtime 내부: `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- Stage apply 순서:
  - `StageTopologyBootstrapSystem`
  - `StageTopologyApplyPrepareSystem`
  - `FixedTickRootGroup`
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

### 5.2A 공통 planar shape 계약
- 공통 runtime raw shape
  - `Shape2DKind`: `Circle`, `Rectangle`
  - `Shape2DComponent`: `Kind`, `Radius`, `Size`
- 공통 판정/샘플링 유틸
  - `Normalize`
  - `ComputeArea`
  - `ComputeHalfExtents`
  - `ContainsPointXZ`
  - `OverlapsCircleXZ`
  - `ComputeBoundsXZ`
  - `SampleUniformXZ`
- gameplay 판정 좌표계
  - 모든 판정은 `XZ` 평면에서만 수행한다.
  - 회전 semantics는 `yaw`만 사용한다.
  - `pitch/roll`은 gameplay 의미가 없으므로 authoring에서 0으로 강제한다.
- 사각형 계약
  - `Rectangle`은 `axis-aligned`가 아니라 `yaw-aware planar OBB`다.
  - `Circle`은 `yaw` 영향을 받지 않는다.
- semantic marker
  - `BulletFieldAreaComponent`, `DepositPointComponent`, `ObstacleGeometryComponent`는 raw shape holder가 아니라 semantic marker로 유지한다.
- source 전용 파생 번들
  - `SourceShapeDerivedComponent`: `ComputedArea`, `HalfExtents`
  - `SourcePollutionGridComponent` 및 pollution buffers는 source owner가 `Shape2DComponent`와 함께 재생성한다.

### 5.3 runtime template authoring 책임 (`SourceRuntimeTemplateAuthoring` / `DepositRuntimeTemplateAuthoring`)
- 주 경로 authoring
  - `StableIdOverride`
  - shape/radius/size
  - pollution grid/config
  - gizmo/debug
- legacy alias (`BulletSourceAuthoring`, `DepositPointAuthoring`)에 남는 migration data
  - `SustainClipSlots[]`, `EventClipSlots[]`
  - `ThresholdWeakened`, `ThresholdDepleted`, `InitialCollectedCount`, `InitialState`
- 새 runtime template authoring baker는 deprecated seed 필드로 runtime clip/threshold/state를 bake하지 않는다. neutral 기본값만 bake하고, 실제 정의는 `StageDefinitionSO` apply가 책임진다.
- `StageSourceMarker`, `StageDepositMarker`, `StageObstacleMarker`는 `OnValidate`와 layout generator 경로에서 `pitch/roll`을 0으로 보정한다.

### 5.4 Obstacle 설계 계약
- `Obstacle`는 `StageTopology`에 편입된 runtime kind이며, 현재 단계에서는 `bullet/player` 소비의 기본 경계까지 구현한다.
- 의미 계약
  - `단일 Obstacle + CollisionMask`
  - `BlockPlayer | BlockBullet`: 플레이어와 총알 모두 차단
  - `BlockPlayer`: 플레이어 이동만 차단
  - `BlockBullet` 접촉 탄환 기본 반응: `즉시 despawn`
- `StageObstacleLayoutData`
  - `StableId`
  - `Active`
  - `Position`
  - `YawDeg`
  - `Shape`
  - `Radius`
  - `Size`
  - `CollisionMask`
- shape 범위
  - 이번 범위에서는 `Circle`, `Rectangle`만 지원한다.
  - obstacle raw shape는 `Shape2DComponent`를 사용한다.
- runtime obstacle component 세트(계약)
  - `StageTopologyObstacleTag`
  - `ObstacleStableIdComponent`
  - `ObstacleCollisionMaskComponent`
  - `ObstacleGeometryComponent`
  - `Shape2DComponent`
  - `StageTopologyOwnedComponent(Kind=Obstacle)`
  - `LocalTransform`
- lifecycle / owner
  - `Obstacle`는 `layout-only topology kind`다.
  - topology apply owner/lifecycle/failure policy는 기존 `Source/Deposit`의 `StageTopology` 규칙을 그대로 따른다.
  - 즉 `instantiate -> reuse -> mapped-active -> pooled-disabled`, `LastAppliedVersion` stamp, infrastructure failure 시 `Ready=0 + 기존 applied topology 유지` 규칙을 동일하게 사용한다.
- bullet read 계약
  - reader owner: `BulletObstacleHitRequestSystem`
  - 그룹: `BulletRequestGroup`
  - 순서: `BulletVacuumRequestSystem` 이후, `PlayerHazardCollisionRequestSystem` 이전
  - 판정 모델: bullet은 `point`, obstacle는 `Circle/Rectangle` inside test
  - 읽기 source: active obstacle entity 직접 query
  - 결과: 기존 `BulletDespawnRequestTag` enable 경로 재사용
  - 다중 remove 원인은 멱등 처리한다.
- player read 계약
  - reader owner: `PlayerObstacleBlockSystem`
  - 그룹: `PlayerFixedStepGroup`
  - 순서: `PlayerPreviousPositionCaptureSystem -> PlayerIntentMovementSystem -> PlayerObstacleBlockSystem -> PlayerIntentConsumeSystem`
  - 판정 모델: player는 `circle(PlayerRadius)`, obstacle는 `Circle/Rectangle`
  - 현재 채택안은 `post-move correction`이며, `rollback + axis slide`를 적용한다.
  - `PlayerPreviousPositionComponent`로 movement 직전 위치를 저장하고 correction에 사용한다.
  - 향후 dash/knockback/external force 등 이동 영향 요소가 증가하면 `movement-resolve 통합` 재설계가 필요할 수 있다.

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
### 7.1 Topology Layer
- topology owner
  - `StageTopologyApplyPrepareSystem`
  - `StageTopologyPrepareGroup` 단일 writer
- topology input
  - `StageTopologyBridge`
  - `StageCatalogRuntimeComponent` publish
  - `StageTopologyRequestComponent` one-shot write
- stage state input
  - `RunDirectorStageBridge`
  - `RunDirectorStageRequestComponent`, gate/signal write
- topology ready gate
  - `RunDirectorStageTransitionSystem`은 `StageTopologyState.Ready == 1` 및 `AppliedStageId == SelectedStageId`일 때만 `Idle -> Running`을 허용한다

### 7.2 DemoShell / Runtime Flow
- `DemoShellFlowController`
  - 시작 시 `StageCatalogSO`를 읽어 런타임 `StageProfiles` 구성
  - 미할당/유효 엔트리 없음 시 기존 `StageProfiles` fallback
- `SampleScene`
  - `DemoShellFlowController.StageCatalog = sc_demo`
  - `StageTopologyBridge`는 `StageCatalog`를 참조한다.
  - `RunDirectorStageBridge`는 stage state/gate/signal만 다룬다.
- `EnterStagePlay`
  - 선택 엔트리의 `Definition.StageId`를 사용해 `StageTopologyBridge.RequestTopologyApply(stageId)` 호출
  - `StageTopologyApplyPrepareSystem`
  - `RequestedStageId`로 layout/definition을 각각 resolve
  - `Source`는 `Shape2DComponent + SourceShapeDerivedComponent`와 pollution grid 재생성을 포함한 layout+definition 결합 apply를 수행한다.
  - `Deposit`은 `Shape2DComponent` 기반 layout apply를 수행하고, `PlayerCarryBinDepositRequestSystem`은 `player circle overlap deposit shape`로 접촉 판정한다.
  - `Obstacle`는 `Shape2DComponent` 기반 layout-only topology apply를 수행하고, `BulletObstacleHitRequestSystem`과 `PlayerObstacleBlockSystem`이 runtime consumer로 이를 읽는다.
  - owned entity 공통 메타
    - `StageTopologyOwnedComponent.Kind`
    - `StageTopologyOwnedComponent.LastAppliedVersion`
  - lifecycle singleton
    - `StageTopologyLifecycleStateComponent.CurrentAppliedVersion`
  - 성공 apply에서만 `CurrentAppliedVersion`을 증가시키고, 이번 version에 stamp되지 않은 owned entity는 `disable-to-pool`로 정리한다.
  - 성공 apply 후 현재 stage에 매핑되지 않은 owned entity는 `disable-to-pool`로 전환한다.
  - infrastructure failure(`StageCatalog`/entry/layout/template/instantiate 실패)에서는 기존 applied topology를 유지하고 `SelectedStageId`에 대해서만 `Ready=0`을 남긴다.
  - definition/source mismatch, duplicate stable id, active=false는 `warn + partial apply`로 처리하고 stage 전체 `Ready`는 유지한다.
  - `OnStateEnterOnce`는 initial apply 직후 자동 발화하지 않음
  - 현재 구현된 topology kind는 `Source`, `Deposit`, `Obstacle`다.
  - `Visual`은 별도 세션에서 GO-only presentational layer 여부를 먼저 확정한다.

### 7.3 Template Catalog / Validation Boundary
- `StageTopologyPrefabCatalogSO`
  - shape는 v1에서 고정
    - `SourceTemplatePrefab`
    - `DepositTemplatePrefab`
  - entry-list 구조로 일반화하지 않는다
- `ContentValidationRunner` / `ContentValidationRules`
  - `StageTopologyPrefabCatalogSO.SourceTemplatePrefab` null은 오류
  - `StageTopologyPrefabCatalogSO.DepositTemplatePrefab` null은 오류
  - unsupported kind / optional kind 정책은 아직 도입하지 않는다

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
  - `StageTopologyApplyPrepareSystemTests`
  - `ContentValidationRulesTests`
    - topology prefab catalog required template 검증
- PlayMode
  - DemoShell 회귀 스모크(`Title -> Lobby -> Stage -> Result -> Retry/Next`)
  - `Stage2` layout/pattern 차이 반영 확인

## 9. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
- [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)






