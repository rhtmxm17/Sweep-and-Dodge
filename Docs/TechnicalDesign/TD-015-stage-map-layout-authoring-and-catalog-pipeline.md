# 스테이지 데이터(Definition) / 그리드 레이아웃(Layout) Dual Catalog 파이프라인 (TD-015)

## Metadata
- doc_id: `TD-015`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-08-11`
- related_docs:
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-025-stage-player-start-position-contract.md](./TD-025-stage-player-start-position-contract.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)
  - [../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
  - [../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)
  - [../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)
  - [TD-034-stage-map-editor-replacement.md](./TD-034-stage-map-editor-replacement.md)
  - [../TaskBoard/SESSION-20260324-01-stage-grid-layout-board.md](../TaskBoard/SESSION-20260324-01-stage-grid-layout-board.md)
> `StageCatalogSO`의 dual catalog와 grid-authoritative runtime 계약은 유지한다. 편집 SSOT는 `StageMapDocument`이며 `StageMapEditorWindow`가 유일한 공식 사용자-facing authoring 경로다. runtime은 Apply된 `StageLayoutSO / StageDefinitionSO / StageCatalogSO`만 읽는다.

## 1. 목표 / 비목표
### 1.1 목표
- 스테이지 layout SSOT를 `grid cell` 기반으로 재정의한다.
- 셀 단위에서 최소한 아래 속성을 authoring / validation / runtime에 일관되게 반영한다.
  - `MovementFlags`: 플레이어/탄환 이동 가능 여부
  - `SourceRegionId`
  - `DepositRegionId`
- `SourceRegionId`, `DepositRegionId`는 paint 시 명시 입력을 강제한다.
- `StageDefinitionSO.SourceBindings` 계약은 유지하되, key 의미를 `Source region stable id`로 고정한다.
- obstacle gameplay authority를 `grid movement`로 유지하고, obstacle visual은 presentation 계층에서 별도로 운영한다.
- 모든 공식 authoring 변경은 `StageMapDocument`에서 시작해 runtime asset의 동일한 grid schema로 수렴시킨다.

### 1.2 비목표
- mid-run topology reapply 허용
- connected cell 자동 병합으로 region id를 추론하는 규칙
- obstacle visual을 gameplay authoritative topology entity로 되돌리는 설계
- 첫 단계에서 모든 legacy shape 기반 runtime system을 한 번에 제거하는 리라이트

## 2. 현재 상태(코드 기준)
- 현재 `StageLayoutSO`는 `Grid / Cells / SourceRegions / DepositRegions / PlayerStart / Presentations`를 authoritative layout schema로 가진다.
- Scene/Tilemap/Marker 기반 legacy authoring과 generator/import 경로는 제거되었다.
- `smd_demo_1 / smd_demo_2 / smd_demo_3`이 데모 stage의 현재 authoring Documents다.
- runtime apply는 layout의 grid/region 데이터를 stable id map으로 바꿔 `Source` aggregate runtime entity와 `StageRuntimeGrid` cache를 reconcile한다.
- `Deposit` gameplay와 `Movement/Obstacle` gameplay는 standalone topology entity가 아니라 grid cache를 직접 읽는다.
- 남아 있는 후속 범위는 terrain visual polish와 외부 importer 같은 authoring/content 확장이지, shape-centric runtime 계약 복구가 아니다.

## 3. 채택 구조 요약
- `StageCatalogSO`의 dual catalog 구조는 유지한다.
- `StageDefinitionSO`는 stage meta와 source binding 정의를 계속 소유한다.
- `StageLayoutSO`는 v2부터 `grid cell authoritative layout`를 소유한다.
- runtime gameplay query의 authoritative source는 `StageGridRuntimeComponent`가 가리키는 grid cache/blob이다.
- `Source / Deposit`는 grid cell에 새겨진 region id를 기반으로 생성되는 aggregate runtime entity다.
- `Obstacle`는 standalone topology kind가 아니라 `MovementFlags`가 표현하는 셀 상태다.
- obstacle visual은 grid 또는 obstacle layer를 읽는 presentation 계층에서 재생성할 수 있지만, gameplay authority와는 분리한다.

## 4. 소유권 (Owner / Writer)
- 편집 SSOT Owner: `StageMapDocument`
- Definition/Layout/Catalog snapshot Owner: `StageMapDocumentExporter`
- Diff/Apply Owner: `StageMapApplyPlanner`
- Document validation Owner: `StageMapDocumentValidationRules`
- Layout 검증 Owner: `StageGridLayoutValidationRules`
- 런타임 stage topology/apply Owner: `StageTopologyApplyPrepareSystem`
  - stage entry 시 grid cache build + `Source / Deposit` region aggregate reconcile을 단일 writer로 수행한다.
- 런타임 stage session reset Owner: `StageSessionResetPrepareSystem`
- GO -> ECS topology input Writer: `StageTopologyBridge`
- GO -> ECS stage state Writer: `RunDirectorStageBridge`
- obstacle visual rebuild Owner:
  - gameplay owner와 분리된 presentation owner가 read-only로 grid를 소비한다.
  - 필요 시 자동 obstacle visual 생성은 이 계층에서 수행한다.

## 5. 업데이트 순서
- 상위 파이프라인 계약:
  - `StageTopologyPrepareGroup -> FixedTickRootGroup`
  - fixed-tick runtime 내부: `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- Stage apply 순서:
  - `StageTopologyBootstrapSystem`
  - `StageSessionResetPrepareSystem`
  - `StageTopologyApplyPrepareSystem`
- Stage apply 내부 순서:
  1. `StageLayoutSO`에서 grid schema를 resolve한다.
  2. runtime `StageGridRuntimeComponent` cache/blob를 rebuild한다.
  3. `SourceRegionId` 기준으로 source aggregate entity를 reconcile한다.
  4. `DepositRegionId` 기준 grid cache를 publish한다.
  5. presentation/tilemap owner가 준비 상태를 읽어 visual rebuild를 수행한다.
- boundary-only apply 계약은 유지한다.
  - 허용: `Idle`, `Completed`, 초기 비플레이 경계
  - 비허용: `Running`, `ClearReady`

## 6. 데이터 구조 / 제약
### 6.1 SO 계약
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
  - `StageGridSpec Grid`
  - `StageCellLayoutData[] Cells`
  - `StageSourceRegionLayoutData[] SourceRegions`
  - `StageDepositRegionLayoutData[] DepositRegions`
  - `StagePlayerStartLayoutData PlayerStart`
  - `StagePresentationLayoutData[] Presentations`
  - 구현 전환기 메모:
    - grid schema만 유지한다.
    - validation/catalog cross-mapping의 authoritative schema는 grid 쪽만 사용한다.

### 6.2 Grid 계약
- `StageGridSpec`
  - `int Width`
  - `int Height`
  - `float CellSize`
  - `Vector3 Origin`
- `StageCellLayoutData`
  - `StageCellMovementFlags MovementFlags`
  - `uint SourceRegionId`
  - `uint DepositRegionId`
- `StageCellMovementFlags`
  - `None`
  - `BlockPlayer`
  - `BlockBullet`
  - `BlockAll = BlockPlayer | BlockBullet`
- grid 좌표계 계약
  - 모든 gameplay query는 world position을 `GridSpec` 기준 cell coord로 변환한 뒤 판단한다.
  - cell 크기와 origin은 runtime/cache build 시 immutable 입력으로 본다.
  - `Cells`는 dense row-major 배열로 저장한다.
  - 길이는 항상 `Width * Height`다.
  - 인덱스는 `index = y * Width + x`다.
  - runtime cache는 이 dense grid를 O(1) lookup 구조로 유지한다.

### 6.3 Region 계약
- `SourceRegionId`, `DepositRegionId`는 셀 paint 시 명시 입력을 강제한다.
- region id는 connected cell 자동 병합으로 추론하지 않는다.
- `StageSourceRegionLayoutData`
  - `uint StableId`
  - `bool Active`
  - `Vector2Int AnchorCell`
  - `Vector2 AnchorOffset`
- `StageDepositRegionLayoutData`
  - `uint StableId`
  - `bool Active`
  - `Vector2Int AnchorCell`
  - `Vector2 AnchorOffset`
- 모든 source/deposit region은 anchor를 필수로 가진다.
- runtime layout의 anchor는 `AnchorCell + AnchorOffset`의 grid-relative(normalized local) 값으로 저장한다.
- runtime entity 위치는 `GridSpec + AnchorCell + AnchorOffset`으로 world 좌표를 계산해 사용한다.
- region table과 cell 데이터의 관계
  - source region table에 있는 `StableId`는 최소 한 개 이상의 셀에서 참조돼야 한다.
  - deposit region table에 있는 `StableId`는 최소 한 개 이상의 `BlockPlayer`가 아닌 셀에서 참조돼야 한다.
  - 셀이 `SourceRegionId` 또는 `DepositRegionId`를 가질 때, 대응 region table entry가 반드시 존재해야 한다.
  - `AnchorCell`은 grid 범위 안에 있어야 한다.
  - `AnchorCell`은 해당 region을 참조하는 셀이어야 한다.
  - deposit anchor는 `BlockPlayer` 여부와 무관하게 허용한다.
  - 한 셀은 `SourceRegionId`와 `DepositRegionId`를 동시에 가질 수 없다.
  - `StableId == 0`은 `None`을 의미한다.

### 6.4 Source 정의 계약
- `StageSourceBinding`
  - `SourceStableId`
  - `InitialSourceState`
  - `ThresholdWeakened`, `ThresholdDepleted`
  - `SustainSlotBinding[]`, `EventSlotBinding[]`
- `SourceStableId`의 의미는 `source region stable id`다.
- source runtime entity는 region 단위 aggregate다.
- source sampling / pollution / progress는 region에 속한 셀 집합을 기준으로 동작한다.
- connected geometry를 shape 하나로 근사하지 않는다.

### 6.5 Deposit 계약
- 플레이어 deposit 접촉 판정은 `player circle -> current/neighbor cell -> DepositRegionId lookup` 기반으로 수행한다.
- P4 기준으로 deposit gameplay runtime entity는 제거한다.
- deposit 기준점은 authoring/presentation 기준점으로만 유지한다.
- deposit anchor는 연출상 진입 불가능한 위치에 놓일 수 있지만, 여전히 해당 deposit region 셀 위에 있어야 한다.
- 여러 deposit region이 동시에 닿는 경우에는 deterministic priority를 새로 정의하지 않는다.
  - current bounds-hit 순회에서 먼저 만난 region을 선택해도 무방하다.

### 6.6 Obstacle / Movement 계약
- obstacle gameplay는 `StageCellMovementFlags`가 단일 authoritative source다.
- `Obstacle` standalone runtime entity와 `StageObstacleLayoutData`는 신규 설계에서 채택하지 않는다.
- 플레이어 이동 차단과 bullet 차단은 동일 grid를 읽되, 소비 규칙은 각 owner가 가진다.
  - player reader owner: `PlayerObstacleBlockSystem`
    - `prevXZ -> nextXZ` swept path broad phase로 traversed cells를 모으고, `BlockPlayer` full-cell narrow phase를 movement owner에서 처리한다.
    - `xOnly / zOnly / rollback` slide resolution 규칙은 현재 movement owner가 계속 소유한다.
  - bullet reader owner: `BulletSimulationSystem`
    - `prevXZ -> nextXZ` swept path broad phase로 traversed cells를 모으고, `BlockBullet` full-cell narrow phase를 같은 simulation pass 안에서 처리한다.
- deposit 접촉은 의도적으로 simple bounds-hit를 유지한다.
  - movement와 달리 swept query나 partial-cell seam으로 올리지 않는다.
- obstacle visual은 gameplay authority가 아니다.
  - visual tilemap 또는 auto-generated mesh/tile은 grid obstacle layer를 read-only로 소비한다.
  - visual 생성 실패는 gameplay hard gate가 아니다.

### 6.7 Presentation 계약
- `Presentation`은 계속 GO-only presentational layer다.
- obstacle visual은 `StagePresentationLayoutData`와 별도 계층으로 본다.
  - obstacle tile/mesh 생성은 `PresentationKey` linked topology 규칙에 편입하지 않는다.
- `StagePresentationLayoutData`는 `Source / Deposit` region stable id 또는 standalone anchor를 참조할 수 있다.
- obstacle linked presentation은 신규 기본 경로에서 지원하지 않는다.
  - P6.next-B 기준으로 editor/sample scene의 obstacle-linked authoring 경로와 `StageObstacleMarker`는 제거됐다.
  - `StagePresentationLinkKind`는 `None / Source / Deposit`만 지원한다.
  - legacy serialized numeric link kind 값은 layout validation에서 unsupported error로 막는다.
  - 필요 시 explicit presentation anchor marker 또는 tilemap visual owner 경로를 사용한다.

### 6.8 PlayerStart 계약
- player start는 stage-level spatial data이므로 `StageLayoutSO`가 소유한다.
- 첫 범위는 단일 player start만 지원한다.
- `StagePlayerStartLayoutData`
  - `bool Active`
  - `Vector2Int AnchorCell`
  - `Vector2 AnchorOffset`
  - `float YawDeg`
- start는 grid-relative `XZ + yaw`만 저장한다.
  - `Y`는 현재 `Grid.Origin.y`를 따른다.
- start cell 제약
  - bounds 안이어야 한다.
  - `BlockPlayer` 셀은 허용하지 않는다.
  - `SourceRegionId` / `DepositRegionId`와 겹치는 경우는 첫 단계에서 warning으로만 취급한다.
- runtime write owner는 layout apply owner와 분리된 `PlayerStageEntryApplyPrepareSystem`이다.
  - stage entry 시 `LocalTransform`, `PlayerGoSyncComponent`, `PlayerPreviousPositionComponent`를 함께 맞춘다.

## 7. 에디터 파이프라인

> 사용자-facing 맵 편집 기준은 [TD-034 Stage Map Editor Replacement](./TD-034-stage-map-editor-replacement.md)를 따른다. Scene/Tilemap/Marker 기반 authoring과 그 import/generator 경로는 2026-08-11에 폐기되었다.

### 7.1 채택 authoring 경로
- 편집 SSOT: editor-only `StageMapDocument`
- 공식 편집 표면: `StageMapEditorWindow`와 custom Scene View tool
- Document validation owner: `StageMapDocumentValidationRules`
- runtime snapshot owner: `StageMapDocumentExporter`
- dry-run/diff/apply owner: `StageMapApplyPlanner`
- schema migration owner: `StageMapDocumentMigrationUtility`
- runtime은 `StageMapDocument`를 직접 읽지 않고, Apply된 `StageLayoutSO / StageDefinitionSO / StageCatalogSO`만 읽는다.

### 7.2 편집·적용 계약
1. Window에서 Document의 grid, movement, regions, anchors, PlayerStart, HazardActor placement/rule, presentation link를 편집한다.
2. Document validation과 issue navigation을 수행한다.
3. `Validate/Dry Run -> Diff Summary -> Apply`를 통과한다.
4. Apply는 Document와 target asset signature가 preview 이후 바뀌면 stale로 거부한다.
5. Apply 성공 후 runtime asset과 catalog pair를 저장한다.

### 7.3 좌표와 시각화
- Document grid의 `Origin / CellSize / AnchorCell / AnchorOffset`가 편집·export 좌표의 authority다.
- Scene View overlay와 selection handle은 Document에서 직접 world pose를 계산한다.
- region/player anchor offset을 cell center로 고정하는 옵션은 session preference이며 Undo 가능한 Document 변경만 기록한다.
- obstacle visual은 gameplay authority가 아니며 presentation 계층이 grid를 read-only로 소비한다.

### 7.4 지원 범위와 폐기 경계
- `smd_demo_1 / smd_demo_2 / smd_demo_3`이 현재 데모 stage의 authoring Documents다.
- Scene/Tilemap/Marker authoring, legacy import, scene 기반 generator/composer, 전용 palette/tile assets는 지원하지 않는다.
- 직접 `com.unity.2d.tilemap` 패키지 의존은 제거했다. Unity core tilemap module은 다른 Unity 기능과의 호환을 위해 별도 계약으로 취급한다.
- 외부 importer는 향후 신규 기능으로만 도입할 수 있으며 제거된 legacy scene schema를 복구하지 않는다.


## 8. 런타임 반영
### 8.1 Topology Layer
- topology owner는 `StageTopologyApplyPrepareSystem` 단일 writer를 유지한다.
- 입력 경로는 기존과 동일하게 `StageTopologyBridge.RequestTopologyApply(stageId)`다.
- apply 성공 시 prepare owner는 아래를 publish한다.
  - `StageRuntimeGridComponent`
  - `StagePlayerStartRuntimeComponent`
  - source region aggregate runtime set
- apply 실패 정책은 기존 `Ready=0 + 이전 topology 유지`를 따른다.

### 8.2 Runtime Query Boundary
- movement / obstacle 관련 query는 runtime grid cache를 직접 읽는다.
- source runtime은 기존 aggregate entity를 유지한다.
- deposit gameplay query는 grid -> region id lookup만 수행한다.
- `RunProgressDirector`는 player center의 `StageRuntimeGrid.SourceRegionId` membership으로 pressure source를 결정한다.
- source spawn/pollution은 `region bounds local grid + valid cell mask`를 authoritative geometry로 사용한다.
- `UniformField`는 valid local cell 균등 샘플 + cell 내부 jitter를 사용한다.
- `PollutionTopK`는 valid local cell 집합 내부에서만 weight sampling을 수행한다.
- `Shape2DComponent`는 source runtime geometry authority로 읽지 않는다.

### 8.3 Lifecycle
- lifecycle 기본 정책은 `disable-to-pool`을 유지한다.
- source aggregate는 `instantiate -> reuse -> mapped-active -> pooled-disabled`를 따른다.
- grid cache는 stage entry마다 rebuild하며, mid-run mutation은 지원하지 않는다.

### 8.4 Runtime Debug Gizmo
- PlayMode in Editor에서는 `StageRuntimeGridDebugDrawer`가 runtime `StageRuntimeGridComponent`와 source anchor ECS 데이터를 읽어 scene gizmo를 그릴 수 있다.
- 표시 범위:
  - grid bounds / cell line
  - movement overlay
  - source region overlay
  - deposit region overlay
  - source anchor
- deposit anchor는 현재 gameplay runtime authority가 아니므로 runtime gizmo 범위에서 제외한다.
- 이 경로는 editor/debug 전용이며, 빌드용 debug overlay를 의미하지 않는다.

## 9. 작업 분해 / 진행 상태
> P1~P6는 grid-authoritative runtime으로 전환하던 당시의 이력이다. 현재 authoring 계약은 P7과 7절을 따른다.

- P1. 문서/결정 고정
  - `TD-015`, 신규 ADR, TaskBoard로 grid authority / explicit region id / obstacle visual 분리 기준을 확정한다.
- P2. 데이터 스키마 전환
  - `StageLayoutSO` grid schema와 validation/generator seam을 도입한다.
- P3. authoring 경로 도입
  - `StageGridAuthoring`, region paint backing store, anchor marker, `StageGridLayoutGenerator`를 구현한다.
  - generator는 legacy arrays를 비우되, runtime smoke 유지가 필요한 운영 샘플 asset은 compatibility bridge를 병행 유지한다.
- P4. runtime movement/deposit 이관
  - obstacle/player/bullet/deposit query를 grid authoritative path로 옮긴다.
- P4.1. bullet block owner 재정렬
  - `BulletObstacleHitRequestSystem`를 제거하고, bullet block를 `BulletSimulationSystem` owner로 옮긴다.
  - request 단계의 활성 bullet 전량 재순회를 금지하고, swept path broad phase seam만 남긴다.
- P4.2. player block swept semantics 정리
  - `PlayerObstacleBlockSystem`를 movement owner swept query로 전환하고, player tunneling을 blocked cell 기준으로 막는다.
  - deposit touch는 bounds-hit semantics를 유지한다.
- [x] P5. source region runtime 이관
  - source sampling, pollution, progress를 region cell 집합 기준으로 옮긴다.
- P6. obstacle visual / metadata tilemap 재편
  - obstacle visual을 presentation owner에서 분리하고, ground / wall visual tilemap과 gameplay metadata tilemap(`Movement / Region`) 중심 경로를 정리한다.
  - `Source / Deposit` authoring은 단일 `RegionTilemap`에서 `StageRegionTile.RegionKind + RegionSlotIndex`와 `StageGridAuthoring`의 `slot -> stable id` mapping을 기준으로 한다.
- P6.next-A. unified RegionTilemap authoring cleanup
  - split tilemap과 `StageRegionPaintAsset` fallback을 제거하고, repo-tracked authoring은 `MovementTilemap + RegionTilemap + mapping table + anchor marker`만 사용한다.
- [x] P6.next-B. obstacle-linked presentation 제거
  - presentation/editor/sample scene에서 obstacle-linked authoring 경로와 `StageObstacleMarker`를 제거했다.
- [x] P6.next-C. runtime/template legacy path 정리
  - hidden compatibility field, legacy marker fallback, obstacle/deposit topology template를 제거하고 source-only runtime/template 계약으로 정리했다.
- [x] P7. Stage Map Editor 대체와 legacy authoring 폐기
  - `StageMapDocument` 3개를 데모 authoring SSOT로 확정했다.
  - Scene/Tilemap/Marker authoring, generator/import/composer 및 전용 테스트와 직접 Tilemap package 의존을 제거했다.

## 10. 검증 계획 / 합격 기준
- 공통
  - compile error 0
  - console error 0
  - EditMode pass
  - PlayMode smoke pass
- EditMode
  - `StageMapDocumentTests`
  - `StageMapEditorInteractionTests`
  - `StageMapEditorWindowSmokeTests`
  - `StageGridLayoutValidationRulesTests`
  - `StageCatalogValidationRulesTests`
  - grid coord / region id / explicit anchor validation 회귀
- PlayMode
  - stage entry 시 grid cache build 성공
  - player movement block은 movement owner swept query로, bullet block은 simulation owner swept query로 동작
  - deposit 접촉이 deposit region 기준으로 동작
  - source sampling / progress가 region cell 집합 기준으로 동작
  - obstacle visual rebuild 실패가 gameplay hard gate로 승격되지 않음

## 11. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
- [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)
- [ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)
