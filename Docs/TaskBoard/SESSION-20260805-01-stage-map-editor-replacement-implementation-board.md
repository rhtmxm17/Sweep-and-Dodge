# SESSION-20260805-01

## Metadata
- doc_id: `SESSION-20260805-01`
- type: `SessionTaskBoard`
- status: `complete`
- last_updated: `2026-08-05`
- related_docs:
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)

## Session Goal
- `StageMapDocument + StageMapEditorWindow + custom Scene View tool`을 v1 stage 제작의 실제 사용자-facing 경로로 완성한다.
- runtime의 `StageLayoutSO / StageDefinitionSO / StageCatalogSO` 입력 계약과 legacy import/debug/backend 경로는 유지한다.
- `StageLayoutEditingSampleV1`을 실제 document asset으로 migration하고 compile, EditMode, Editor smoke, PlayMode, console 기준을 검증한다.

## Now
- 없음

## Next
- 없음

## Blocked
- 없음

## Parking Lot
- HazardActor orchestration rule 전용 편집기
- LDtk/Tiled 등 외부 importer
- advanced playtest, batch edit, prefab replace, region id regenerate
- runtime topology schema 또는 DOTS update order 변경
- legacy authoring scene/marker/backend 삭제

## Done
- [x] N1. TD-034와 후속 구현 요청의 T1~T6 완료 기준을 코드, 테스트, 실제 asset, 측정 증거로 대조했다.
- [x] T1. schema v2 explicit migration owner와 presentation catalog/applied catalog identity를 구현하고 dense paint/resize/repair 데이터 보존 경계를 고정했다.
- [x] T2. project document list, metadata/target/grid workflow, structured inspector, lock/visibility, Undo/dirty/stale-plan/cache 갱신을 완료했다.
- [x] T3. 명시적 selection, hit-test, place/move/delete, world/local round-trip, overlap error overlay를 완료했다.
- [x] P1. movement/source/deposit/overlap을 cached mesh로 교체했다.
  - 조건: `32 x 32` synthetic dense grid, 1,024 cells, 네 layer 모두 populated.
  - 결과: layer당 `4,096 vertices / 6,144 indices`, visible layer 전체 최대 `4 submissions`.
  - unchanged `EnsureBuilt` 256회: build count 증가 `0`, current-thread managed allocation `0 B`.
  - 실제 `smd_demo_1`: 1,225 cells, 첫 build 1회, movement/source/deposit/overlap vertices=`312/800/48/0`.
- [x] T4. complete legacy diff/validation, SHA-256 stale signature, target asset GUID identity, prevalidated single-Undo-group apply, catalog rename/remove/identity, SourceBinding SSOT를 완료했다.
- [x] T5. structured issue target/navigation과 preview-first Undo 가능한 quick-fix를 완료했다.
- [x] T6. `StageLayoutEditingSampleV1` Stage 1을 `Assets/_Project/03_Datas/StageMapDocuments/smd_demo_1.asset`으로 migration했다.
  - import document changes `8`, generated runtime diff `0`.
  - legacy Layout, existing Definition, Catalog entry, HazardActor, Presentation equivalence 통과.
  - legacy ground/wall visual tilemap은 TD-015 runtime generator 비대상이므로 optional `VisualTileKeys`는 empty로 유지했다.
- [x] V1. Unity `6000.3.6f1` compile/project console error 0, Stage Map EditMode `53/53`, PlayMode `3/3`, `git diff --check`, `.meta` 검증을 통과했다.
  - operational core loop: 180 frames, max active bullets 5,935.
  - dedicated core loop: 120 frames, max active bullets 4,017.
  - operational HazardActor emitter smoke 통과.
  - 실제 Window open/load/list/targets/overlay 확인 및 migrated document clone을 통한 v1 Scene tool 제작 workflow smoke 통과.
- [x] D1. TD-034 진행 상태와 검증 결과를 실제 증거에 맞게 갱신했다.
