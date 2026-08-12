# ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement
> 신규 실무형 Stage Map Editor의 편집 SSOT를 `StageMapDocument`로 고정하고 기존 Tilemap/Marker/Inspector 툴을 legacy import/debug 경로로 격하하는 결정

## Metadata
- doc_id: `ADR-20260804-01`
- type: `ArchitectureDecisionRecord`
- status: `accepted`
- date: `2026-08-04`
- partially_superseded_by:
  - [ADR-20260811-01-stage-map-legacy-authoring-retirement.md](ADR-20260811-01-stage-map-legacy-authoring-retirement.md)
- related_docs:
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)

## 배경
- 기존 stage authoring 흐름은 `StageGridAuthoring`, Unity Tilemap, Scene marker, Custom Inspector 버튼, generator를 조합해 운영한다.
- 이 구조는 grid-authoritative runtime 계약과는 맞지만, 사용자가 맵을 제작하는 작업 모델로는 분산되어 있다.
  - 현재 편집 대상 stage, visible layer, validation 상태, asset apply 결과를 한 화면에서 파악하기 어렵다.
  - destructive 또는 asset-writing 작업이 `Validate/Dry Run -> Diff Summary -> Apply` 흐름으로 일관되게 강제되지 않는다.
  - Source/Deposit/PlayerStart/HazardActor/Presentation authoring이 서로 다른 Inspector와 Scene hierarchy 지식에 의존한다.
- 실무형 맵 에디터로 전환하려면 사용자-facing 편집 권한을 기존 Scene/Inspector 조합에서 전용 editor workflow로 옮겨야 한다.

## 결정
- 신규 Stage Map Editor의 편집 중 SSOT는 editor-only `StageMapDocument` asset으로 둔다.
- 신규 사용자-facing 편집 표면은 `StageMapEditorWindow`와 custom Scene View tool로 구성한다.
- Unity Tilemap은 신규 편집 SSOT로 사용하지 않는다.
  - 기존 Tilemap 기반 scene은 import source, migration sample, debug reference로 유지한다.
  - 신규 editor v1은 document를 직접 수정하는 brush/tool을 기본 경로로 삼는다.
- 기존 `StageGridAuthoring` / Tilemap / Marker / Inspector 버튼 기반 툴은 legacy import/debug/backend 경로로 격하한다.
  - 즉시 삭제하지 않는다.
  - 신규 기능을 병행 확장하지 않는다.
  - 치명적 데이터 손상 버그와 migration/import 검증에 필요한 보수만 허용한다.
- runtime 입력 계약은 유지한다.
  - runtime은 계속 `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`를 읽는다.
  - `StageMapDocument`는 editor authoring asset이며 runtime dependency가 아니다.
- destructive 또는 repo/asset writing 작업은 `Validate/Dry Run -> Diff Summary -> Apply` 순서를 통과해야 한다.

## 대안
- 대안 A: 기존 Tilemap/Marker/Inspector 툴을 유지하고 Stage Editor Window만 얹는다.
  - 장점: migration 부담이 작고 기존 generator path를 거의 그대로 쓸 수 있다.
  - 단점: 편집 SSOT가 계속 Scene/Tilemap/Marker/asset에 분산되고, 사용자는 기존 구조 지식을 계속 알아야 한다.
  - 기각 사유: 이번 목표는 기능 노출 개선이 아니라 사용자-facing 맵 제작 워크플로우 대체다.
- 대안 B: Unity Tilemap을 계속 신규 editor의 SSOT로 삼는다.
  - 장점: Unity 기본 Tile Palette 경험을 활용할 수 있다.
  - 단점: Source/Deposit stable id, anchor, HazardActor placement, Presentation link, apply diff를 통합된 document transaction으로 다루기 어렵다.
  - 기각 사유: 실무형 editor의 validation/diff/apply/undo 정책을 일관되게 강제하려면 독립 document model이 필요하다.
- 대안 C: 기존 툴과 신규 에디터를 모두 공식 지원한다.
  - 장점: 기존 사용 습관을 보존한다.
  - 단점: 동일 데이터를 두 사용자-facing 경로가 수정하면서 divergence와 테스트 매트릭스가 커진다.
  - 기각 사유: 프로젝트 목표는 유지보수 가능한 ownership과 업데이트 순서 명확화이며, 공식 병행 지원은 이 원칙에 맞지 않는다.

## 결과
- 2026-08-11 이후 legacy import/debug/backend 유지 결정은 `ADR-20260811-01`에 의해 폐기됐다. `StageMapDocument` SSOT와 runtime asset 경계 결정은 그대로 유효하다.
- 긍정 효과
  - stage 편집 상태, validation, layer visibility, apply diff를 한 workflow 안에서 다룰 수 있다.
  - Scene hierarchy/Inspector 구조 지식 없이 주요 맵 제작 작업을 수행할 수 있다.
  - 기존 runtime catalog 계약을 유지하면서 editor UX를 교체할 수 있다.
- 트레이드오프
  - 기존 Tilemap scene과 document 사이의 import/migration 검증이 필요하다.
  - document와 generated runtime asset 사이의 stale-plan, dirty, undo/save 정책을 별도로 설계해야 한다.
  - custom Scene View tool이 Unity Tilemap 기본 brush 기능을 대체해야 한다.
- 후속
  - `TD-034`를 신규 editor 실행 기준 문서로 유지한다.
  - `TD-015`는 runtime layout/catalog 계약 기준으로 유지한다.
  - legacy retirement의 최종 경계와 결과는 `ADR-20260811-01`을 따른다.
