# SESSION-20260805-02 Stage Map Editor UX Completion

## Metadata
- doc_id: `SESSION-20260805-02`
- type: `SessionTaskBoard`
- status: `complete`
- last_updated: `2026-08-05`
- related_docs:
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)

## Session Goal
- 기존 `StageMapDocument + StageMapEditorWindow + custom Scene View tool` 구현과 실제 사용자 편집 데이터를 보존한다.
- Selection Navigator와 contextual Inspector를 분리하고 selection을 논리 identity 하나로 유지한다.
- center offset lock, 복합 layer lock, import/apply 검증 계약을 구현 보장 수준과 일치시킨다.
- runtime 입력 계약과 legacy import/debug/backend 지위는 변경하지 않는다.

## Now
- 없음

## Next
- 없음

## Blocked
- 없음

## Parking Lot
- runtime topology 또는 DOTS update order 변경
- legacy authoring scene/marker/backend 삭제
- HazardActor orchestration rule 전용 편집기
- 외부 importer 및 새로운 duplicate 기능

## Done
- [x] T0. 당시 T1~T6 작업 기준과 기존 구현·테스트·실제 asset을 항목별 감사했다.
- [x] T1. Editing Session -> Selection Navigator -> Contextual Inspector 순서로 UI를 분리했다.
  - Navigator만 inventory/category 선택을 소유하고 Inspector는 `Selection.Kind`에 대응하는 한 section만 표시한다.
  - Scene hit-test, Navigator, Issue Navigator가 Window의 동일 `TrySelect` command를 사용한다.
- [x] T2. `StageMapSelection`에서 array index와 session 병렬 selected-index state를 제거했다.
  - Cell=`(x,y)`, Region/Anchor=`kind+stableId`, Hazard=`sourceStableId+placementId`, Presentation=`stableId`를 canonical identity로 사용한다.
  - resize/reorder/delete/Undo/Redo/external mutation은 단일 `ReconcileSelection` 경로를 사용한다.
- [x] T3. `CenterRegionAnchors`와 `CenterPlayerStart`를 Undo 가능한 transient session preference로 구현했다.
  - 기본값 false, load 시 data mutation 없음, enable 시 compatible selection offset zero, place/move에서 zero 강제, 두 preference 독립을 검증했다.
- [x] T4. Source/Deposit Anchor의 owner layer + Anchors 복합 lock과 hidden selection handle 정책을 구현했다.
  - Scene place/move, contextual edit, delete, quick-fix, shortcut command가 중앙 mutation gate를 사용한다.
  - lock/hidden 상태에서도 Navigator selection과 조회는 유지한다.
- [x] T5. legacy temporary round-trip과 actual document consistency 계약을 분리했다.
  - saved sample scene -> temporary document/Layout/Definition/Catalog round-trip 및 actual asset signature 불변을 검증했다.
  - dirty active sample scene의 automated migration/import를 `SMI923`으로 거부하고 scene dirty state를 stale signature에 포함했다.
  - actual `smd_demo_1`은 validation + target integrity + document-to-runtime dry-run 0만 검사하며 legacy equality를 사용하지 않는다.
- [x] T6. Window UX, overlay 성능, EditMode/PlayMode/Console/정적 검증을 완료했다.
  - actual Window menu open과 `smd_demo_1` load/list/target/category selection/contextual section/Scene hit/Issue navigation/hidden handle/dry-run 0을 확인했다.
  - synthetic `32 x 32` grid에서 layer당 `4,096 vertices / 6,144 indices`, 최대 4 submissions, unchanged `EnsureBuilt` 256회 rebuild 0/current-thread managed allocation `0 B`를 검증했다.
  - Stage Map 관련 EditMode 6 fixture `71/71`, operational/dedicated/HazardActor/Presentation PlayMode smoke `4/4` 통과했다.
  - operational Stage 1 PlayerStart runtime ready/position/apply-version 소비를 검증했다.
  - Unity `6000.3.6f1` compile 및 최종 console error 0, `git diff --check`, `.meta` 누락 0, test scene/generated 잔여물 0을 확인했다.
  - 실제 `smd_demo_1`, sample scene, catalog identity는 테스트에서 수정하지 않았고 사용자 편집된 target asset 차이는 보존했다.
- [x] D1. TD-034와 이전 implementation board의 apply 보장 표현을 prevalidated single-Undo-group 수준으로 교정했다.
