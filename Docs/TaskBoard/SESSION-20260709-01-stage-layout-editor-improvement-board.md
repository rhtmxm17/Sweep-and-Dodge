# SESSION-20260709-01

## Metadata
- doc_id: `SESSION-20260709-01`
- type: `SessionTaskBoard`
- status: `complete`
- last_updated: `2026-07-09`
- related_docs:
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)
  - [./SESSION-20260324-01-stage-grid-layout-board.md](./SESSION-20260324-01-stage-grid-layout-board.md)
  - [./SESSION-20260417-01-hazard-actor-tooling-improvement-board.md](./SESSION-20260417-01-hazard-actor-tooling-improvement-board.md)

## Session Goal
- 한 줄 목표: `StageLayoutEditingSampleV1` 중심 스테이지 편집 툴의 좌표 정확성, Scene View 성능, Transform 기반 배치 UX를 개선할 실행 순서를 고정한다.
- 완료 기준:
  - workspace stage offset이 standalone presentation runtime 좌표에 섞이지 않는다.
  - grid/region/movement Scene View 표시가 repaint마다 전체 셀을 조회하고 개별 `Handles` primitive를 그리지 않는다.
  - region anchor와 player start를 Scene View Transform 조작으로 배치하고 serialized cell 데이터와 일관되게 동기화할 수 있다.
  - Source 하위 Hazard Actor placement를 생성·배치·검증하는 최소 툴 흐름이 제공된다.
- 이번 세션에서 하지 않을 것:
  - layout/definition/catalog 전체 생성 파이프라인 재설계
  - presentation preview scope UX 개편
  - 신규 stage 전체 scaffold wizard
  - Hazard Actor orchestration rule 전용 편집기 완성

## Design Direction

### D1. Anchor authoring authority
- 채택: Edit Mode에서는 marker `Transform`을 Scene View 편집 권한으로 두고 `AnchorCell/AnchorOffset`을 생성용 serialized cache로 동기화한다.
- 공통 적용 대상:
  - `StageRegionAnchorMarker`
  - `StagePlayerStartMarker`
- 공통 변환 책임은 별도 editor utility에 두고 두 marker가 같은 world/grid 변환 규칙을 사용한다.
- Scene View 이동은 Undo를 지원하고 grid plane 밖의 Y 이동은 canonical preview plane으로 보정한다.
- 확정 정책:
  - Scene View Transform 이동/회전: 가장 가까운 셀 중심에 snap하고 `AnchorOffset=0`으로 정규화한다.
  - Inspector `AnchorCell/AnchorOffset/YawDeg` 직접 편집: 명시적으로 입력한 값을 보존하고 Transform에 적용한다.
  - 이후 Transform을 다시 조작하면 explicit offset은 0으로 재설정한다.

### D2. Scene View grid visualization
- 1차 후보: authoring/tilemap 변경 시에만 재생성하는 cached mesh를 Scene View render callback에서 그린다.
- repaint hot path에서는 bounds 전체 `Tilemap.GetTile` 조회와 셀별 `Handles.DrawLine` 호출을 수행하지 않는다.
- mesh cache는 최소한 아래 층을 분리한다.
  - grid/bounds
  - movement overlay
  - source/deposit region overlay
- anchor label과 소수의 marker gizmo는 `Handles`를 유지할 수 있다.
- 숨은 `MeshRenderer` GameObject 방식은 scene hierarchy 오염, lifecycle, scene dirty 위험 때문에 fallback으로 둔다.
- 구현 전 baseline과 개선 후 Scene View repaint 비용을 동일 샘플 씬에서 측정한다.

### D3. Hazard Actor placement tool MVP
- 선택한 `SourceRuntimeTemplateAuthoringBase` 아래에 `StageHazardActorMarker` child를 생성한다.
- `PlacementInstanceId`는 owning stage 범위에서 충돌하지 않는 다음 값을 자동 할당한다.
- `ActorArchetypePrefab` 지정, Transform 기반 위치, `LocalYawDeg` 편집, 생성될 `LocalOffset` 미리보기를 제공한다.
- local offset 계산은 단순 world position 차가 아니라 source transform space 변환을 기준으로 한다.
- placement 누락 prefab, 중복 ID, 잘못된 부모를 Inspector/validation에서 즉시 표시한다.
- Scene marker는 `HazardActorPlacements`와 `HazardActorOrchestrationRules`만 소유하며, Definition의 나머지 binding 필드는 보존한다.
- Scene→Definition 반영은 자동 덮어쓰기가 아니라 Source Inspector의 명시적 Apply로 수행한다.
- Apply 전에 `+추가/~수정/-삭제`, prefab 교체, rule 변경을 표시하고 삭제/prefab 교체는 확인 대화상자를 거친다.
- stage 전체 marker와 prospective binding을 검증하며 실패 시 Definition을 수정하지 않는다.
- placement/rule 반영은 하나의 Undo 작업으로 처리하고, preview 이후 데이터가 변하면 stale plan 적용을 거부한다.
- orchestration rule의 TargetPlacement/TargetPhase 드롭다운은 MVP 이후 후속 작업으로 둔다.

## Now
- 없음

## Next
- 없음

## Parking Lot
- [ ] P1. Layout → Definition → Catalog 생성 파이프라인을 `Validate/Dry Run → 변경 요약 → 전체 적용`의 원자적 흐름으로 재설계
  - 근거: 현재 독립 버튼과 즉시 저장 방식은 부분 성공 및 결과 확인 불편이 있다.
- [ ] P2. validation 중 hidden `StageRegionAnchorMarker.StableId`를 수정하는 동작을 순수 검증과 명시적 Fix/Migration으로 분리
  - 근거: 현재 Validate 명칭과 실제 쓰기 동작이 일치하지 않고 Undo/Dirty 계약이 없다.
- [ ] P3. Presentation Preview에 `Off / Selected / All`, 현재 대상 stage, instance count, clear 동작 제공
  - 근거: selected scope가 이전 stage를 유지할 수 있고 현재 preview 상태가 보이지 않는다.
- [ ] P4. 신규 Stage/Grid/Tilemap/PlayerStart/Anchor/target asset을 생성하는 stage scaffold wizard
  - 근거: 현재 신규 stage 구성은 기존 hierarchy 복제나 수동 연결에 의존한다.
- [ ] P5. Hazard Actor orchestration rule의 TargetPlacement/TargetPhase 드롭다운과 참조 무결성 편집 UX
  - 근거: placement MVP와 분리해 기존 `SESSION-20260417-01`의 후속 툴링 요구와 함께 다룬다.

## Done
- [x] D1. `StageLayoutEditingSampleV1` 중심 편집 툴 점검 및 우선순위 재평가
  - 검증 결과:
    - standalone presentation workspace offset 오염을 실제 scene/asset/runtime 경로에서 확인했다.
    - Transform/cell 이중 편집 불편을 실제 사용 이슈로 확인했다.
    - 전체 셀 Gizmo 순회에 따른 Scene View 성능 문제를 HIGH 우선순위로 승격했다.
    - Hazard Actor placement tool 필요성을 현재 세션 범위에 추가했다.
- [x] T1. **[CRITICAL]** Standalone presentation에서 workspace stage offset 제거
  - 변경:
    - standalone presentation 위치/회전을 owning `StageLayoutStageMarker` 기준으로 생성한다.
    - linked presentation의 topology parent-local 계약은 유지한다.
    - `sl_demo_3.asset`의 Presentation_9003 위치를 workspace X=115에서 stage-local X=15로 재생성했다.
  - 검증 결과:
    - 관련 EditMode `StageLayoutCatalogGeneratorTests` 14/14 통과
    - sample asset 동기 검증 1/1 통과
    - 전체 EditMode 465/465 통과
    - 전체 PlayMode 46/46 통과
    - Unity project code error 0건; Console 조회 시 MCP transport client 종료 로그만 잔존
  - 구현 메모:
    - stage workspace Y rotation은 기존 canonical Grid rotation validation과 충돌하므로 지원 계약에서 제외하고 translation 제거를 회귀 검증했다.
- [x] T2. **[HIGH]** Cached mesh 기반 StageGrid Scene View renderer 구현 및 측정
  - 변경:
    - repaint마다 수행하던 bounds 전체 셀 조회와 셀별 `Handles.DrawLine`을 제거했다.
    - grid/movement/source/deposit 선을 transient cached mesh로 통합하고 authoring별 1 draw call로 렌더한다.
    - Tilemap 변경, project 변경, Undo/Redo, authoring signature 변경 시에만 cache를 재생성한다.
    - workspace Transform 이동은 draw matrix만 변경하고 cell cache를 재생성하지 않는다.
    - assembly reload/editor 종료 시 transient Mesh/Material을 명시적으로 해제한다.
  - 성능 측정:
    - 대상: `StageLayoutEditingSampleV1`, 3 stages, 2,860 cells
    - 기존 hot path: repaint당 `Tilemap.GetTile` 8,580회, 조회 비용 평균 1.328ms
    - 신규 hot path: authoring 3개 cache signature 확인 평균 0.004ms
    - cache rebuild: tile lookup 5,720회, vertices 3,176개, 1.736ms; tile/authoring 변경 시에만 수행
  - 검증 결과:
    - cached renderer EditMode 3/3 통과
    - 전체 EditMode 468/468 통과
    - 전체 PlayMode 46/46 통과
    - Scene View에서 grid/movement/source/deposit 색상과 anchor label 출력 확인
    - hierarchy/scene asset에 preview GameObject를 생성하지 않음
    - Unity project code error 0건; Console 조회 시 MCP transport client 종료 로그만 잔존
- [x] T3. **[HIGH]** Region Anchor와 Player Start를 Transform-authoritative 편집으로 전환
  - 변경:
    - `StageAnchorTransformEditorUtility`가 Transform↔Cell/Offset/Yaw 변환과 Undo 기록을 단일 소유한다.
    - Region Anchor와 PlayerStart 모두 Scene View Transform 조작 시 셀 중심 snap 및 `AnchorOffset=0` 정규화를 적용한다.
    - Inspector의 Cell/Offset/Yaw 직접 편집은 explicit offset을 보존하고 Transform에 역동기화한다.
    - PlayerStart 전용 CustomEditor와 양쪽 marker의 `Snap To Cell Center` 동작을 추가했다.
  - 콘텐츠 마이그레이션:
    - Stage_01 PlayerStart `AnchorCell (4,20) -> (5,3)`
    - Stage_02 PlayerStart `AnchorCell (2,-10) -> (2,1)`
    - `sl_demo_1/2.asset`을 새 Cell 기준으로 재생성했다.
    - Region Anchor와 Stage_03 PlayerStart는 기존 Transform/data가 이미 일치해 변경하지 않았다.
  - 검증 결과:
    - T3 round-trip/negative cell/workspace offset/+90° Grid/Undo 테스트 4/4 통과
    - sample scene↔layout Transform/data 동기 검증 1/1 통과
    - 전체 EditMode 472/472 통과
    - 전체 PlayMode 46/46 통과
    - 첫 전체 EditMode 실행의 MCP `NetworkStream disposed` 유입 실패는 재실행에서 해소됨
- [x] T4. **[HIGH]** Hazard Actor placement tool MVP 구현
  - 생성/편집:
    - Source Inspector와 GameObject 메뉴에 `Add Hazard Actor Placement` 진입점을 추가했다.
    - owning stage 전체에서 다음 `PlacementInstanceId`를 할당하고 선택 Source의 direct child로 생성한다.
    - marker Inspector에 source-local offset/yaw 미리보기와 prefab/부모/ID inline validation을 제공한다.
    - Transform 회전을 `LocalYawDeg` cache에 동기화하고 Generator는 cache가 아닌 Transform에서 source-local pose를 산출한다.
    - nested Source marker는 바깥 Source Generator 수집 대상에서 제외한다.
  - 명시적 Scene→Definition 동기화:
    - Source Inspector에 `Up to date`/diff 상태와 `Apply Hazard Actor Data To Definition`을 추가했다.
    - placement/rule 두 배열만 갱신하고 threshold 및 sustain/event 등 다른 binding 필드는 보존한다.
    - 삭제/prefab 교체 확인, stale plan 거부, validation 실패 시 무수정, 단일 Undo를 적용했다.
    - stage-global ID 중복을 scene marker와 서로 다른 Definition SourceBinding 사이에서 모두 검증한다.
    - 기존 `Ensure Missing Stage Definition Bindings`의 보존 동작은 변경하지 않았다.
  - 일회성 데이터 정리:
    - 이후 동기화 정책과 무관하게 `sd_demo_1`을 기준으로 Stage_01/Source 1001의 씬 데이터를 역동기화했다.
    - placement 2개(id=1, 2), orchestration rule 2개가 definition 및 source-space Generator 산출값과 일치한다.
  - 검증 결과:
    - T4 utility/diff/validation/Undo 및 cross-binding ID 테스트 8/8 통과
    - 실제 sample Stage_01에서 임시 placement ID 3 자동 생성, diff `+1`, prefab/local pose Definition 적용, Undo 원상복구 확인
    - sample scene↔definition Hazard placement/rule 동기 검증 통과
    - 전체 EditMode 480/480 통과
    - 전체 PlayMode 46/46 통과
    - Unity project code error 0건; 테스트 의도 로그 및 MCP transport 종료 로그만 확인

## End of Session
- 결과: complete
- 남은 리스크:
  - Hazard Actor orchestration rule UX는 placement MVP 이후 Parking Lot에서 다룬다.
- 다음 시작점: Parking Lot 우선순위를 재평가한다.
