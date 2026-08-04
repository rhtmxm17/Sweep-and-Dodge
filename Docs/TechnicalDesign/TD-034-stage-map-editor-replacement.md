# TD-034 Stage Map Editor Replacement
> 기존 Inspector/Tilemap/Marker 기반 stage authoring 툴을 `StageMapDocument` 중심의 실무형 맵 에디터로 대체하는 설계

## Metadata
- doc_id: `TD-034`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-08-04`
- related_docs:
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)
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
- runtime 입력은 계속 `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`로 유지한다.
- 모든 asset-writing 작업은 `Validate/Dry Run -> Diff Summary -> Apply` 흐름을 통과한다.

### 1.2 비목표
- v1에서 HazardActor orchestration rule 상세 편집기를 완성하지 않는다.
- v1에서 외부 importer(`LDtk`, `Tiled` 등)를 도입하지 않는다.
- runtime stage topology, DOTS update order, `StageLayoutSO` grid schema를 재설계하지 않는다.
- 기존 Tilemap/Marker scene authoring을 즉시 삭제하지 않는다.

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
- legacy authoring scene
  - `StageGridAuthoring`, Tilemap, `StageRegionAnchorMarker`, `StagePlayerStartMarker`, `StageHazardActorMarker`, `StagePresentationMarker`는 import/debug/backend 자료로 유지한다.
  - 신규 editor와 같은 사용자-facing 공식 편집 경로로 병행 확장하지 않는다.

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
- Issue Navigator
  - validation 결과를 severity/code/location 기준으로 표시하고 선택 시 해당 위치로 이동한다.
- Apply panel
  - Validate, Dry Run, Diff Summary, Apply buttons를 제공한다.
  - destructive 변경은 confirmation을 요구한다.

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

## 5. Import / Export / Apply 흐름
### 5.1 Legacy import
- `StageLayoutEditingSampleV1` 같은 기존 authoring scene에서 document를 생성할 수 있어야 한다.
- import source:
  - `StageGridAuthoring` bounds, cell size, movement tilemap, region tilemap, mapping table
  - region anchors, player start marker
  - hazard actor markers
  - presentation markers
- import는 document를 수정하는 작업이므로 preview summary와 Undo/dirty 정책을 따른다.

### 5.2 Export / Apply
- document에서 `StageLayoutSO` grid schema를 생성한다.
- document의 stage meta와 source binding 관련 값은 `StageDefinitionSO`에 반영한다.
- catalog entry pair는 기존 `StageCatalogSO` dual catalog 구조를 유지한다.
- generator/validation 기존 구현은 backend로 재사용할 수 있으나 사용자 진입점은 `StageMapEditorWindow`로 제한한다.
- apply 성공 전까지 generated asset은 수정하지 않는다.

### 5.3 동등성 기준
- 기존 sample scene import 후 export한 layout/definition/catalog는 기존 asset과 동등해야 한다.
- 동등하지 않은 값은 diff summary에서 설명 가능해야 하며, 문서화된 migration rule 없이는 silent rewrite하지 않는다.

## 6. 소유권 / 업데이트 순서 / 제약
- Editor document writer: `StageMapEditorWindow`와 Scene View tool command layer.
- Validation owner: `StageMapDocumentValidationRules`.
- Import owner: `StageMapLegacyImportUtility`.
- Apply plan owner: `StageMapApplyPlanner`.
- Export owner: `StageMapDocumentExporter`.
- Runtime apply owner는 `TD-015`의 `StageTopologyApplyPrepareSystem` 계약을 유지한다.
- runtime `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 순서는 변경하지 않는다.
- 신규 editor data는 runtime ECS Native container, Fence, Enableable ownership을 변경하지 않는다.

## 7. 작업 분해 / 진행 상태
- T0. Legacy freeze policy
  - 기존 Inspector/Tilemap/Marker UI의 신규 기능 추가를 중단하고 import/debug/backend 지위를 명시한다.
- T1. `StageMapDocument` schema
  - document asset, serialized fields, schema version, migration hook, tests를 설계/구현한다.
- T2. `StageMapEditorWindow` MVP
  - Stage list, layer panel, palette, inspector, issue navigator, apply panel을 구현한다.
- T3. Custom Scene View tool / brush
  - select, movement paint, region paint, anchor, player start, hazard actor, presentation link 모드를 구현한다.
- T4. Import / export / apply pipeline
  - legacy scene import, generated asset dry-run, diff summary, stale-plan 거부, apply를 구현한다.
- T5. Validation navigator
  - issue list, scene focus, quick-fix preview/apply를 구현한다.
- T6. Migration / compatibility
  - `StageLayoutEditingSampleV1`을 document로 import하고 export 동등성을 검증한다.
- Parking Lot
  - HazardActor orchestration rule editor
  - external importer
  - advanced playtest tooling

## 8. 검증 계획 / 합격 기준
- 문서/설계 검증
  - `TD-034`는 신규 editor 구현 기준이고, `TD-015`는 runtime layout/catalog와 legacy import 기준으로 역할이 분리되어야 한다.
- EditMode
  - `StageMapDocument` serialization/schema test
  - validation rules test
  - legacy import fixture test
  - document export equivalence test
  - apply plan stale rejection test
  - destructive diff confirmation seam test
- Editor UX smoke
  - Scene View에서 Inspector 없이 movement, region, anchor, player start, hazard actor, presentation link를 배치할 수 있어야 한다.
  - issue 클릭 시 대상 위치 또는 document element로 이동해야 한다.
- Runtime smoke
  - document에서 생성된 `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`로 기존 PlayMode stage entry smoke가 통과해야 한다.
