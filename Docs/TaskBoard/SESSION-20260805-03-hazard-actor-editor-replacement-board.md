# SESSION-20260805-03 HazardActor Editor Replacement

## Metadata
- doc_id: `SESSION-20260805-03`
- type: `SessionTaskBoard`
- status: `implemented`
- last_updated: `2026-08-05`
- related_docs:
  - [../TechnicalDesign/TD-035-hazard-actor-authoring-workbench-and-preview.md](../TechnicalDesign/TD-035-hazard-actor-authoring-workbench-and-preview.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [../ADR/ADR-20260805-01-hazard-actor-workbench-and-preview-ownership.md](../ADR/ADR-20260805-01-hazard-actor-workbench-and-preview-ownership.md)

## Session Goal
- `HazardActorAuthoring` prefab SSOT와 runtime 계약을 유지하면서 공식 편집 경로를 UI Toolkit Workbench로 교체한다.
- actor behavior와 실제 stage placement/encounter를 하나의 분석형 Preview Core로 확인한다.
- `StageMapDocument`가 source-local orchestration rule을 소유하고 명시적 migration/export를 수행한다.

## Completion Criteria
- Workbench만으로 Actor/Phase/Transition/Pattern/Profile 제작과 validation navigation을 수행할 수 있다.
- Pattern/Actor/Encounter preview가 동일 snapshot/simulator를 사용하고 실제 document pose를 반영한다.
- 기존 StageDefinition rule을 명시적 preview/diff/apply로 document에 손실 없이 이전할 수 있다.
- Scene View preview가 정의된 30Hz/ghost/trajectory 예산과 cleanup 수명주기를 지킨다.
- Unity compile, Console error 0, 관련 EditMode와 기존 HazardActor PlayMode smoke를 통과한다.

## Now
- [x] T0~T8 구현 감사 완료 후 editor-only 구현 착수
  - 감사 결과: Workbench/Preview Core 타입 없음, `HazardActorAuthoring` 기본 Inspector raw array 경로 유지, `StageMapDocument`는 v2이며 source-local orchestration rule 배열 없음.
  - 감사 결과: `StageMapEditorWindow`는 placement 편집과 Scene View overlay를 제공하지만 Encounter Track, rule selection/navigation, document-authoritative rule export는 미구현.
  - 감사 결과: runtime HazardActor authoring/resolver/system 계약은 TD-031/TD-032 기준으로 유지 가능하며 이번 작업에서 변경하지 않는다.
- [x] 구현/검증 완료
  - EditMode full: 565/565 pass.
  - PlayMode full: 46/46 pass.
  - Preview steady step allocation: 0 byte assertion pass.
  - MCP smoke: Workbench/Stage Map Editor menu open 성공, Scene View screenshot capture 성공.

## Next
- 없음

## Blocked
- 없음

## Parking Lot
- actor motion/path authoring
- arbitrary condition/orchestration graph
- ECS Sandbox runtime-accurate preview
- advanced playtest tooling

## Done
- [x] D0. 신규 editor의 SSOT, UI, preview, stage orchestration, migration, 성능 정책을 ADR-20260805-01과 TD-035로 고정했다.
- [x] T0. Legacy Inspector freeze
  - `HazardActorAuthoringEditor`가 read-only 요약/validation/Open Workbench만 제공한다.
- [x] T1. Workbench UI Toolkit shell
  - `HazardActorWorkbenchWindow`가 library, canvas, Contextual Inspector, Issue/Preview 영역과 canonical selection/session cleanup을 제공한다.
- [x] T2. Phase/Pattern canvas와 Profile 편집
  - 구조 명령, selector/transition/pattern edit, shared profile 사용처, `Open`, `Duplicate & Assign`, Undo/dirty 경로를 구현했다.
- [x] T3. Preview snapshot/simulator
  - resolver 기반 snapshot과 30Hz fixed-step Presence/Phase/Selector/Telegraph/Emit/Cooldown, Linear/DampedLinear/HomingLite, lifecycle trigger replay를 구현했다.
- [x] T4. Embedded/Scene View preview renderer
  - Workbench embedded preview와 Scene View preview가 `HazardActorPreviewCoordinator` active session을 공유하며 cap/30Hz/callback cleanup을 검증했다.
- [x] T5. StageMapDocument orchestration schema/migration
  - schema v3, source-local rule validation, explicit TargetDefinition import preview/apply/stale rejection, document-authoritative export를 구현했다.
- [x] T6. Encounter Track
  - source별 placement row, Spawn/PhaseSet/Retire marker, multi-target edit, common PhaseId picker, progress scrub composite preview를 구현했다.
- [x] T7. Validation/Issue navigation
  - Workbench issue selection과 StageMap source/rule/placement issue target navigation을 구현했다.
- [x] T8. Migration·UX·성능·runtime 회귀 검증
  - `smd_demo_1` schema v3 migration, targeted 58/58, EditMode 565/565, PlayMode 46/46, allocation 0 byte, MCP window/Scene View smoke를 기록했다.
