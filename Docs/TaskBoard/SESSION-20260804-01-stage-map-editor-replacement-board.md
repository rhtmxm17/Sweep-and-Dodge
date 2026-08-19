# SESSION-20260804-01

## Metadata
- doc_id: `SESSION-20260804-01`
- type: `SessionTaskBoard`
- status: `complete`
- last_updated: `2026-08-04`
- related_docs:
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)

## Session Goal
- 한 줄 목표: 기존 stage authoring 툴을 대체할 `StageMapDocument` 중심 실무형 맵 에디터 설계를 문서로 고정한다.
- 완료 기준:
  - 신규 ADR이 document SSOT와 legacy replacement 결정을 기록한다.
  - 신규 TD가 구현자가 바로 착수할 수 있는 editor architecture, UX, data flow, 검증 기준을 제공한다.
  - 기존 `TD-015`와 충돌하지 않고 신규 editor 책임이 분리된다.
  - 후속 구현 작업이 TaskBoard 단위로 분해된다.
- 이번 세션에서 하지 않을 것:
  - `StageMapDocument` 코드 구현
  - Editor Window / Scene View tool 구현
  - sample scene 또는 asset migration 실행

## Now
- 없음

## Next
- [ ] T0. Legacy freeze policy
  - 기존 `StageGridAuthoring`/Tilemap/Marker/Inspector 버튼 툴을 legacy import/debug/backend 경로로 격하한다.
  - 신규 기능 추가 금지와 허용 보수 범위를 문서/코드 UI에 반영한다.
- [ ] T1. `StageMapDocument` schema 설계/구현
  - editor-only document asset, schema version, dense cells, region/player/hazard/presentation data를 정의한다.
- [ ] T2. `StageMapEditorWindow` MVP
  - Stage list, Layer/Visibility, Palette, Inspector, Issue Navigator, Apply panel을 구현한다.
- [ ] T3. Custom Scene View tool/brush
  - select, movement paint, region paint, anchor, player start, hazard actor, presentation link 모드를 구현한다.
- [ ] T4. Import/export/apply pipeline
  - legacy scene import, document export, dry-run diff, stale-plan 거부, apply를 구현한다.
- [ ] T5. Validation navigator
  - issue list, scene/document target focus, quick-fix preview/apply를 구현한다.
- [ ] T6. Migration/compatibility plan
  - `StageLayoutEditingSampleV1` import/export 동등성 검증을 구현하고 legacy path의 유지 범위를 확정한다.

## Parking Lot
- [ ] HazardActor orchestration rule 전용 editor
- [ ] 외부 importer(`LDtk`, `Tiled` 등)
- [ ] 현재 document를 기준으로 한 advanced playtest tooling
- [ ] batch edit, prefab replace, region id regenerate 같은 대량 편집 도구

## Done
- [x] D1. 신규 Stage Map Editor 대체 설계를 문서로 고정
  - `StageMapDocument` editor-only SSOT 채택 결정을 ADR로 기록했다.
  - 신규 editor 실행 기준을 `TD-034`로 분리했다.
  - `TD-015`는 runtime layout/catalog와 legacy import/debug/backend 기준으로 격하했다.
  - 후속 구현 작업을 T0~T6으로 분해했다.
