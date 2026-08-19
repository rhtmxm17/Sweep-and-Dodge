# TD-034 Stage Map Editor Replacement
> 기존 Inspector/Tilemap/Marker 기반 stage authoring 툴을 `StageMapDocument` 중심의 실무형 맵 에디터로 대체하는 설계

## Metadata
- doc_id: `TD-034`
- type: `TechnicalDesign`
- status: `implemented`
- last_updated: `2026-08-12`
- related_docs:
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [TD-035-hazard-actor-authoring-workbench-and-preview.md](./TD-035-hazard-actor-authoring-workbench-and-preview.md)
  - [../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)
  - [../ADR/ADR-20260811-01-stage-map-legacy-authoring-retirement.md](../ADR/ADR-20260811-01-stage-map-legacy-authoring-retirement.md)
  - [../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)

## 1. 목표 / 비목표
### 1.1 목표
- 사용자-facing stage 편집 경로를 `StageMapEditorWindow + custom Scene View tool`로 통합한다.
- 편집 중 SSOT를 editor-only `StageMapDocument` asset으로 둔다.
- v1에서 아래 authoring 범위를 기존 Inspector/Tilemap/Marker 조작 흐름에서 대체한다.
  - Grid bounds / cell size
  - Movement flags
  - Source / Deposit region cells and anchors
  - PlayerStart
  - HazardActor placements
  - Presentation links
- 후속 HazardActor editor replacement 범위에서 source-local orchestration rule과 Encounter Track을 `StageMapDocument`/`StageMapEditorWindow`에 통합한다.
- runtime 입력은 계속 `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`로 유지한다.
- 모든 asset-writing 작업은 `Validate/Dry Run -> Diff Summary -> Apply` 흐름을 통과한다.

### 1.2 비목표
- v1에서 외부 importer(`LDtk`, `Tiled` 등)를 도입하지 않는다.
- runtime stage topology, DOTS update order, `StageLayoutSO` grid schema를 재설계하지 않는다.
- HazardActor behavior prefab/profile 편집 UI와 분석형 preview 내부 구현을 이 문서에서 중복 정의하지 않는다. 해당 기준은 `TD-035`를 따른다.
- 제거된 Scene/Tilemap/Marker authoring 또는 TargetDefinition rule import를 복구하지 않는다.

## 2. 채택 구조 요약
- `StageMapDocument`
  - editor-only authoring asset.
  - 신규 맵 에디터의 편집 중 SSOT.
  - runtime assembly와 runtime scene은 이 asset을 직접 참조하지 않는다.
- `StageMapEditingSession`
  - Editor Window가 로드한 document와 Scene View tool 상태를 묶는 transient state.
  - active stage, selected tool, selected layer, selection, dirty state, validation snapshot을 가진다.
- `StageMapApplyPlan`
  - document를 generated assets로 반영하기 전 만드는 dry-run 결과.
  - 대상 `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`, 변경 요약, validation 결과, stale 검사용 signature를 가진다.
- retired legacy authoring
  - Stage 1~3 one-shot migration과 runtime smoke를 완료했다.
  - scene, marker, Tilemap asset, importer, generator와 사용자 진입점은 제거되었다.

## 3. 데이터 모델
### 3.1 StageMapDocument
- `SchemaVersion`
- `StageId`, `DisplayName`, `IsFinalStage`, `StageTimeLimitSec`
- `Grid`
  - width, height, cellSize, origin policy, authoring bounds
- dense cell array
  - movement flags
  - source region id
  - deposit region id
  - optional visual tile key
- source region table
  - stable id, active, anchor cell, anchor offset
- deposit region table
  - stable id, active, anchor cell, anchor offset
- player start
  - active, anchor cell, anchor offset, yaw
- hazard actor placements
  - placement instance id, owning source stable id, actor archetype prefab, source-local offset, local yaw
- hazard actor orchestration rules
  - owning source stable id, source-local rule id, target placement instance ids
  - action type, trigger type, normalized threshold, target phase id
- presentation links
  - stable id, active, presentation key, placement mode, linked kind, linked stable id, local/standalone pose

### 3.2 Validation issue model
- issue는 severity, code, message, location, optional fix id, optional scene/document target을 가진다.
- Issue Navigator는 issue 선택 시 document selection과 Scene View focus를 갱신한다.
- fix 가능한 항목은 dry-run fix preview를 먼저 보여주고 apply 시 document에 Undo 가능한 변경으로 반영한다.

### 3.3 Stale / dirty policy
- document 변경, generated asset 변경, import source 변경은 각각 signature를 가진다.
- `StageMapApplyPlan`은 생성 당시 signature를 보관한다.
- apply 직전 signature가 달라지면 stale plan으로 보고 거부한다.
- editor document 변경은 Unity Undo와 `EditorUtility.SetDirty`를 사용한다.

### 3.4 Schema migration
- current schema는 명시적 `StagePresentationCatalogSO` validation target과 마지막 성공 apply의 catalog entry identity를 가진다.
- migration owner는 `StageMapDocumentMigrationUtility` 하나로 고정한다.
- migration은 `Validate/Preview -> Diff -> Apply`에서만 실행하며 asset load 시 자동·무음 변경하지 않는다.
- v1 migration은 project presentation catalog가 정확히 하나일 때만 후보를 만들고, target catalog의 Definition/Layout pair가 정확히 하나의 entry와 일치할 때만 last-applied identity를 유도한다.
- current version은 no-op이며 지원하지 않는 과거/미래 version은 error로 거부한다.

### 3.5 Dense grid resize / repair
- movement/region paint는 `Cells.Length != Grid.Width * Grid.Height` 상태를 수정하지 않고 명시적 issue와 함께 거부한다.
- Grid 변경은 전용 resize preview/apply command가 소유한다.
- resize는 이전/신규 grid가 공유하는 `(x, y)` 좌표의 cell과 visual key를 보존한다.
- shrink로 잘리는 non-default cell 또는 visual key는 destructive diff와 confirmation 대상이다.
- 손상된 배열 길이 repair는 현재 grid의 보존 가능한 flattened index/coordinate 데이터를 유지하며 document만 수정한다.

### 3.6 Structured issue target
- Navigator용 issue는 원본 `ContentValidationIssue`와 별도로 target kind, stable id/array index, optional cell, optional fix id를 가진다.
- document validation owner가 runtime/content issue를 structured target으로 변환한다.
- UI는 location 문자열 재해석을 fallback으로만 사용한다.

## 4. Editor UX
### 4.1 StageMapEditorWindow
- Stage list
  - 열린 project/document의 stage 목록, active stage, generated asset 연결 상태를 표시한다.
- Layer / Visibility
  - grid, movement, source, deposit, anchors, player start, hazard actors, presentations 표시/잠금 토글을 제공한다.
- Palette
  - movement brush, source/deposit region id, player start, hazard actor archetype, presentation key를 선택한다.
- Inspector
  - 현재 선택된 cell/region/anchor/player/hazard/presentation 속성을 편집한다.
  - 기존 Unity Inspector에 의존하지 않는다.
- Selection Navigator
  - cell 좌표와 region/anchor/player/hazard/presentation inventory를 category별로 표시한다.
  - hidden 또는 locked element도 선택과 조회는 허용하며 Scene View hit-test/handle/mutation만 제한한다.
- Contextual Inspector
  - canonical selection kind에 대응하는 section 하나만 표시한다.
  - inventory 선택과 속성 편집 책임을 분리하고 array index 기반 병렬 선택 상태를 두지 않는다.
- Issue Navigator
  - validation 결과를 severity/code/location 기준으로 표시하고 선택 시 해당 위치로 이동한다.
- Apply panel
  - Validate, Dry Run, Diff Summary, Apply buttons를 제공한다.
  - destructive 변경은 confirmation을 요구한다.
- Hazard Encounter panel
  - 선택 source의 placement 행과 normalized progress 축에 Spawn/PhaseSet/Retire rule을 표시한다.
  - rule/target 편집, 다중 target fan-out, progress scrub과 Encounter Preview를 제공하며 세부 기준은 `TD-035`를 따른다.
- Editing Session과 Palette
  - IMGUI 기반 Window를 유지하고 Tool 및 소수의 배타적 brush 선택은 segmented button으로 상시 노출한다.
  - Movement brush는 Passable, Block Player, Block Bullet, Block Both의 네 최종 flag preset을 사용한다.
  - Source/Deposit 종류는 segmented button, 가변 Stable ID는 Erase와 기존 ID 및 Custom/New ID를 포함하는 최대 6행 scroll radio 목록으로 선택한다.
  - Active/Visible/Locked layer 상태는 행 단위 matrix로 통합하며 transient session state만 변경한다.
- 우측 탐색·검증 패널
  - 좌우 패널 사이에는 400~720px 범위의 transient resize splitter를 두고 Document panel 최소 360px을 보장한다.
  - Selection, Issues, Diff는 상위 tab으로 전환하며 Contextual Inspector는 tab 밖의 독립 section으로 계속 표시한다.
  - Selection inventory는 Regions/Hazards/Rules/Links category table로 표시한다. 행 단일 클릭은 canonical selection만 갱신하고 더블클릭은 Scene View 또는 asset을 frame/ping한다.
  - Issue table은 단일 클릭 시 selection과 상세만 갱신하고 더블클릭 시 structured target navigation을 수행한다. quick-fix의 lock, preview, confirmation, Undo 계약은 유지한다.
  - Diff table은 현재 apply plan의 Kind/Target/Field/Summary를 표시하되 apply change에 navigation identity를 추가하지 않는다.
  - table row summary는 document load/mutation/Undo·Redo/external mutation/Validate/Dry Run에서만 invalidate하고 repaint에서는 cache를 재사용한다.

### 4.2 Scene View tool modes
- Select
  - cell, region, anchor, player start, hazard actor, presentation link를 선택한다.
- Paint Movement
  - movement flags를 brush로 칠한다.
- Paint Region
  - source/deposit region id를 명시 선택한 뒤 cell에 칠한다.
  - source/deposit overlap은 즉시 warning 또는 error overlay로 표시한다.
- Place Anchor
  - 선택 region의 anchor를 cell 중심 snap 기준으로 배치한다.
- Place PlayerStart
  - player start 위치와 yaw를 배치한다.
- Place HazardActor
  - 선택 source에 대해 placement instance id를 자동 할당하고 actor archetype을 배치한다.
- Place PresentationLink
  - source/deposit/standalone target에 presentation key를 연결한다.

### 4.3 Selection / lock / invalidation
- selection kind는 Cell, Source/Deposit Region, Source/Deposit Anchor, PlayerStart, HazardActor, Presentation을 명시적으로 구분한다.
- selection identity는 Cell=`(x,y)`, Region/Anchor=`kind+stableId`, HazardActor=`sourceStableId+placementInstanceId`, Presentation=`stableId`를 사용한다.
- resize/reorder/delete/Undo/Redo/external mutation은 단일 reconcile 경로에서 identity를 다시 해석하고 missing/ambiguous/out-of-bounds selection을 해제한다.
- lock은 Scene View tool, structured inspector, delete, resize를 포함한 해당 layer의 모든 mutation을 거부하되 선택과 조회는 허용한다.
- Source/Deposit Anchor mutation은 owner region layer와 Anchors layer가 모두 unlocked일 때만 허용한다.
- hidden layer element는 Scene View hit-test에서 제외한다.
- `CenterRegionAnchors`와 `CenterPlayerStart`는 서로 독립적인 Undo 가능 editor-session preference이며 기본값은 false다. enable 시 compatible selection offset을 0으로 만들고 이후 place/move에도 0을 강제한다.
- document switch, structured/raw edit, schema migration, quick-fix, resize, Undo/Redo, 외부 serialized 변경은 selection clamp와 apply plan 폐기 및 overlay invalidation을 수행한다.

### 4.4 Scene View overlay geometry
- movement/source/deposit은 layer별 cached `Mesh` geometry로 생성한다.
- geometry는 document 또는 visibility 관련 cache input이 바뀔 때만 재생성한다.
- steady repaint는 전체 cell scan, cell별 corner 배열 생성, cell별 `Handles.Draw*`를 수행하지 않고 layer별 고정 draw submission만 수행한다.
- transient mesh/material은 Window lifecycle에서 명시적으로 정리한다.
- dense synthetic grid에서 vertex/index/layer 계약, build count, managed allocation, draw submission 수를 검증한다.

## 5. Import / Export / Apply 흐름
### 5.1 Legacy retirement
- Stage 1~3 semantic migration과 runtime/Preview smoke를 완료했다.
- Scene/Tilemap/Marker import, TargetDefinition orchestration import, generator/composer 및 전용 tests는 제거했다.
- `StageMapEditorWindow`에는 legacy 입력이나 import 조작이 존재하지 않는다.
- 외부 importer는 별도 schema와 검증 계약을 갖는 신규 기능으로만 도입할 수 있다.


### 5.2 Export / Apply
- document에서 `StageLayoutSO` grid schema를 생성한다.
- document의 stage meta와 source binding 관련 값은 `StageDefinitionSO`에 반영한다.
- document의 source-local HazardActor orchestration rule을 `StageDefinitionSO` source binding에 반영한다.
- orchestration schema migration 이후에는 target definition의 기존 rule을 암묵적으로 보존하거나 merge하지 않는다.
- catalog entry pair는 기존 `StageCatalogSO` dual catalog 구조를 유지한다.
- generator/validation 기존 구현은 backend로 재사용할 수 있으나 사용자 진입점은 `StageMapEditorWindow`로 제한한다.
- apply 성공 전까지 generated asset은 수정하지 않는다.

### 5.3 동등성 기준
- migration 확정 시 layout/definition/catalog의 semantic equivalence를 검증했다.
- 허용된 차이는 runtime에서 읽지 않는 non-PhaseSet `TargetPhaseId`의 `0 -> 1` canonicalization뿐이었다.
- 현재 document는 자체 validation, target reference integrity와 document-to-runtime dry-run을 기준으로 검증한다.
- 실제 document와 runtime asset의 차이는 정상적인 미적용 편집 상태일 수 있으므로 serialized snapshot equality를 장기 테스트 oracle로 사용하지 않는다.

### 5.4 Catalog identity / apply group
- catalog entry identity는 last-applied key, Definition/Layout pair의 단일 일치, 아직 apply되지 않은 document의 현재 key 순서로 해석한다.
- ambiguous identity는 validation error이며 임의 수정하지 않는다.
- key rename은 기존 entry update로 처리하고 destructive diff/confirmation에 표시한다.
- `IncludeInCatalog=false`는 식별된 entry를 제거하며 `TargetCatalog`는 include 여부와 무관하게 필수다.
- candidate catalog에 기존 `StageCatalogValidationRules`를 적용해 duplicate/invalid pair를 asset mutation 전에 거부한다.
- apply는 모든 target과 candidate catalog를 mutation 전에 검증한다.
- successful apply는 Layout/Definition/Catalog mutation과 document last-applied identity 갱신을 하나의 Undo group으로 기록한다. 이는 rollback 가능한 atomic transaction을 의미하지 않는다.

## 6. 소유권 / 업데이트 순서 / 제약
- Editor document writer: `StageMapEditorWindow`와 Scene View tool command layer.
- Validation owner: `StageMapDocumentValidationRules`.
- Apply plan owner: `StageMapApplyPlanner`.
- Export owner: `StageMapDocumentExporter`.
- HazardActor behavior authoring/preview owner는 `TD-035`의 Workbench와 Preview Core다.
- HazardActor orchestration document writer는 Stage Map Editor의 Encounter command layer다.
- Runtime apply owner는 `TD-015`의 `StageTopologyApplyPrepareSystem` 계약을 유지한다.
- runtime `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 순서는 변경하지 않는다.
- 신규 editor data는 runtime ECS Native container, Fence, Enableable ownership을 변경하지 않는다.

## 7. 작업 분해 / 진행 상태
- T0. Legacy freeze policy
  - 대체 구현 중 신규 기능 추가를 동결했고 Stage 1~3 migration 완료 후 해당 경로를 폐기했다.
- T1. `StageMapDocument` schema
  - document asset, serialized fields, schema version, migration hook, tests를 설계/구현한다.
- T2. `StageMapEditorWindow` MVP
  - Stage list, layer panel, palette, inspector, issue navigator, apply panel을 구현한다.
- T3. Custom Scene View tool / brush
  - select, movement paint, region paint, anchor, player start, hazard actor, presentation link 모드를 구현한다.
- T4. Import / export / apply pipeline
  - generated asset dry-run, diff summary, stale-plan 거부, apply를 구현한다. one-shot legacy import는 migration 완료 후 제거한다.
- T5. Validation navigator
  - issue list, scene focus, quick-fix preview/apply를 구현한다.
- T6. Migration / compatibility
  - Stage 1~3을 document로 migration하고 export 동등성을 검증한 뒤 legacy source를 폐기한다.
- 2026-08-05 implementation 상태
  - T0: 완료. legacy Inspector/import/debug/backend 경로를 폐기했다.
  - T1: 완료. schema v2 explicit migration과 dense grid resize/repair 안전성을 구현했다.
  - T2: 완료. Window 제작 표면, lock/visibility, Undo/dirty/cache/stale-plan 정책을 구현했다.
  - T3: 완료. 명시적 selection과 v1 Scene View 배치/이동/삭제, overlap 표시를 구현했다.
  - T4: 완료. document export diff/validation/stale 검사와 prevalidated single-Undo-group apply/catalog identity를 구현했다.
  - T5: 완료. structured issue target/navigation과 preview-first quick-fix를 구현했다.
  - T6: 완료. Stage 1~3 document migration과 Layout/Definition/Catalog 동등성, operational runtime을 검증했다.
  - Scene View overlay 성능 교체: 완료. layer별 cached mesh와 fixed submission 경계를 검증했다.
- T7. HazardActor Encounter extension: 완료
  - orchestration schema/export, Encounter Track과 Preview 연결은 `TD-035`를 따른다.
- Parking Lot
  - external importer
  - advanced playtest tooling

- T8. Stage 2·3 migration / legacy retirement: 완료
  - semantic migration, runtime/Preview smoke, legacy authoring subsystem과 전용 테스트 제거를 `SESSION-20260811-01`에서 완료했다.

## 8. 검증 계획 / 합격 기준
- 문서/설계 검증
  - `TD-034`는 editor 구현 기준이고, `TD-015`는 runtime layout/catalog 계약 기준으로 역할이 분리되어야 한다.
- EditMode
  - `StageMapDocument` serialization/schema test
  - validation rules test
  - document export equivalence test
  - apply plan stale rejection test
  - destructive diff confirmation seam test
- Editor UX smoke
  - Scene View에서 Inspector 없이 movement, region, anchor, player start, hazard actor, presentation link를 배치할 수 있어야 한다.
  - issue 클릭 시 대상 위치 또는 document element로 이동해야 한다.
- Runtime smoke
  - document에서 생성된 `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`로 기존 PlayMode stage entry smoke가 통과해야 한다.
- HazardActor Encounter extension
  - document rule export와 stale-plan 거부, source/rule/placement structured navigation을 검증해야 한다.
  - 실제 placement pose를 사용하는 Encounter Preview 기준은 `TD-035`를 따른다.

### 8.1 2026-08-05 실제 검증 결과
- 실제 migration
  - source: `Assets/_Project/01_Scenes/StageLayoutEditingSampleV1.unity` Stage 1.
  - document: `Assets/_Project/03_Datas/StageMapDocuments/smd_demo_1.asset`.
  - saved legacy scene은 temporary document/Layout/Definition/Catalog round-trip에서 검증했고 실제 document/generated asset signature는 유지했다.
  - 실제 document는 validation, target integrity, document dry-run generated runtime diff 0건을 통과했다.
  - dirty active saved legacy scene의 automated migration/import 거부와 stale signature 반영을 검증했다.
  - TD-015에서 ground/wall visual tilemap은 runtime layout generator 입력이 아니므로 legacy import의 optional `VisualTileKeys`는 empty로 유지한다.
- overlay 측정
  - `32 x 32` synthetic dense grid, 1,024 cells, movement/source/deposit/overlap 모두 populated.
  - layer당 4 vertices와 6 indices: `4,096 vertices / 6,144 indices`.
  - 모든 layer visible 시 draw submission은 cell 수와 무관하게 최대 4회.
  - unchanged `EnsureBuilt` 256회에서 rebuild 0회, current-thread managed allocation `0 B`.
  - 실제 migrated document 1,225 cells의 최초 build 결과는 movement/source/deposit/overlap vertices `312/800/48/0`.
- EditMode / Editor smoke
  - Stage Map 관련 6개 test fixture `71/71` 통과.
  - 실제 메뉴에서 `StageMapEditorWindow` open, `smd_demo_1` load/list/target 연결, category selection, contextual Inspector 단일 section, Scene hit/Issue navigation, hidden handle, dry-run 0을 확인했다.
  - migrated document clone으로 movement/region paint+erase, anchor/PlayerStart/HazardActor/Presentation place+move+delete, center lock, composite lock, resize/reorder/delete selection reconcile, stale plan/Undo를 Window command/Scene tool route에서 확인했다.
- PlayMode
  - operational/dedicated core loop, operational HazardActor actor-owned emitter, Presentation Stage 1->2->retry rebuild smoke `4/4` 통과.
  - operational Stage 1에서 PlayerStart runtime ready, 위치 적용, apply version 소비를 확인했다.
- 정적 / console
  - Unity `6000.3.6f1` compile 성공, 최종 console error 0.
  - `git diff --check` 통과, `Assets/_Project` 파일 `.meta` 누락 0, `InitTestScene*`/`__Generated*` 잔여물 0.

### 8.2 2026-08-11 Stage 2·3 migration / retirement 결과
- `smd_demo_2`, `smd_demo_3`을 current schema로 생성했다.
- Stage 2는 placement 2/source 2/rule 4, 승인된 PlayerStart overlap warning 1건을 유지한다.
- Stage 3은 placement 2/source 1/rule 2, warning 0이다.
- 허용된 runtime asset 변경은 non-PhaseSet rule의 의미 없는 `TargetPhaseId 0 -> 1` canonicalization뿐이다.
- 두 Document의 모든 placement/source/rule Preview 준비와 progress 0/1 rebuild를 확인했다.
- sample scene, Scene/Tilemap/Marker authoring, legacy importer/generator/composer, TargetDefinition rule import와 직접 Tilemap package 의존을 제거했다.
- targeted StageMap/Hazard/Catalog EditMode 92/92, full EditMode 514/514, full PlayMode 46/46을 통과했다.
- Unity Console error 0, 삭제 asset GUID 참조 0, legacy code/asset symbol 0, generated test residue와 `.meta` 누락 0을 확인했다.

### 8.3 2026-08-12 Palette·우측 패널 가시성 개선 결과
- IMGUI Window를 유지하면서 segmented Tool/Palette, layer matrix, 400~720px resize splitter, Selection/Issues/Diff table tab과 독립 Contextual Inspector를 구현했다.
- `smd_demo_1/2/3`은 session-only Palette 변경 후 document signature를 유지했고 Dry Run runtime diff는 모두 0이었다. Stage 2의 기존 승인 warning 1건만 유지했다.
- table summary cache는 steady `EnsureBuilt` 256회에서 rebuild 0회, current-thread managed allocation `0 B`를 확인했다.
- 실제 Window 1100x900에서 Paint Region Stable ID radio, Regions table, Stage 2 Issue table과 Contextual Inspector 분리를 시각 점검했다.
- 최종 full EditMode `516/516`, full PlayMode `46/46`을 통과했다. MCP transport의 allowlisted client-disconnect 로그를 제외한 compile/Console error는 0이며 generated test residue와 C# `.meta` 누락도 0이다.
