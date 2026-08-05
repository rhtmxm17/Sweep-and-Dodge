# SESSION-20260805-03 HazardActor Editor Replacement

## Metadata
- doc_id: `SESSION-20260805-03`
- type: `SessionTaskBoard`
- status: `planned`
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
- 없음

## Next
- [ ] T0. Legacy Inspector freeze
  - 완료 기준: `HazardActorAuthoring` Inspector가 read-only 요약, validation, `Open Workbench`만 제공하고 공식 raw array 편집 경로가 제거된다.
- [ ] T1. Workbench UI Toolkit shell
  - 완료 기준: Archetype Library, Behavior/Preview surface, canonical selection, Contextual Inspector, Issue 영역과 session cleanup이 동작한다.
- [ ] T2. Phase/Pattern canvas와 Profile 편집
  - 완료 기준: Phase/transition/selector/pattern/timeline 편집, 구조 명령 Undo, 공유 profile 사용처와 `Duplicate & Assign`이 구현된다.
- [ ] T3. Preview snapshot/simulator
  - 완료 기준: 기존 resolver 기반 snapshot과 30Hz fixed-step Presence/Phase/Selector/Emit/movement simulation이 결정적으로 동작한다.
- [ ] T4. Embedded/Scene View preview renderer
  - 완료 기준: debug geometry와 가능한 inert ghost 외형, transport, cache, 1,024/4,096 cap, 집약 표시와 모든 Editor lifecycle cleanup이 구현된다.
- [ ] T5. StageMapDocument orchestration schema/migration
  - 완료 기준: schema version, source-local rule data, structured validation, TargetDefinition 명시적 migration, stale rejection과 document-authoritative export가 동작한다.
- [ ] T6. Encounter Track
  - 완료 기준: Stage Map Editor에서 rule marker와 다중 target fan-out을 편집하고 progress scrub으로 Spawn/PhaseSet/Retire를 재생한다.
- [ ] T7. Validation/Issue navigation
  - 완료 기준: actor/profile/phase/pattern/source/rule/placement issue가 Workbench, track, Scene View의 정확한 대상으로 이동한다.
- [ ] T8. Migration·UX·성능·runtime 회귀 검증
  - 완료 기준: 기존 content migration, GD-016 preview smoke, Undo/Redo, allocation/cap 측정, compile/Console/EditMode/PlayMode 결과가 기록된다.

## Blocked
- 없음

## Parking Lot
- actor motion/path authoring
- arbitrary condition/orchestration graph
- ECS Sandbox runtime-accurate preview
- advanced playtest tooling

## Done
- [x] D0. 신규 editor의 SSOT, UI, preview, stage orchestration, migration, 성능 정책을 ADR-20260805-01과 TD-035로 고정했다.
