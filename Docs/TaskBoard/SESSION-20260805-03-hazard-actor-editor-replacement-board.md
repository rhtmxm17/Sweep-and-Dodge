# SESSION-20260805-03 HazardActor Editor Replacement

## Metadata
- doc_id: `SESSION-20260805-03`
- type: `SessionTaskBoard`
- status: `completed`
- last_updated: `2026-08-06`
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
- [x] R0. `PROMPT.md` 기준 후속 감사로 완료 상태 복구
  - 기준선 재확인: `HazardActorPreviewCoordinator.OnEditorUpdate`는 callback당 1 step만 진행하고 `_lastUpdateTime = now`로 누적 wall time을 유실한다.
  - 기준선 재확인: Workbench embedded preview는 ghost마다 `GUI.DrawTexture`, Scene View preview는 ghost마다 `Handles.DrawSolidDisc/DrawLine`을 호출한다.
  - 기준선 재확인: `EventRepeatCount`, `EventShotSchedule`, `EventShotIntervalSec`가 preview emission에 반영되지 않는다.
  - 기준선 재확인: `TrajectorySamplesPerBranch = 16`이 `LineEven` spawn count clamp에 사용되어 simulation 의미를 바꾼다.
  - 기준선 재확인: Encounter Preview는 start/scrub progress snapshot으로 actor set/phase를 만들며 재생 중 Spawn/PhaseSet/Retire event plan을 동적으로 평가하지 않는다.
  - 변경 경계: runtime ECS component/system/baker/update order/Fence 계약은 TD-031/TD-032 기준으로 변경하지 않는다.
- [x] R1/R2/R4/R3/R5 1차 구현 및 targeted 검증
  - R1: coordinator가 wall-clock anchor에서 target preview time을 계산하고 `EvaluateAt`으로 fixed-step catch-up한다. Workbench는 `PreviewRepaintRequested`에서 `IMGUIContainer.MarkDirtyRepaint()`를 호출한다.
  - R2: preview emit이 `EventRepeatCount`, `EventShotSchedule`, `EventShotIntervalSec`를 반영하며 `LineEven` spawn count와 `TrajectorySamplesPerBranch`를 분리했다.
  - R3: Scene View ghost 표시를 `Graphics.DrawMeshInstanced` batch renderer로 옮겼다. 후속 정확성 감사에서 embedded preview는 UI Toolkit 단일 mesh 기반 `Exact` 기본 표시로 교체하고, `Density`는 명시적 진단 모드로 분리했다.
  - R4: Encounter session이 source placement plan과 rule preview를 보유하고 progress scrub 시 Spawn/PhaseSet/Retire를 재평가한다.
  - R5: Workbench가 actor/profile/telegraph dirty signature를 감지해 snapshot/preview를 자동 재생성한다.
  - 검증: `HazardActorWorkbenchPreviewTests` 17/17 pass, StageMap 관련 targeted suite 50/50 pass.
- [x] R1~R7 후속 마감 검증
  - 논리 시간: `PreviewCoordinator_AdvancesByWallClockInsteadOfCallbackCount`와 cadence parity 테스트로 5초 wall-clock catch-up 및 callback cadence 독립성을 검증했다.
  - 행동 정확성: repeat/schedule/interval, `LineEven` spawn count와 trajectory sample 분리, dynamic Encounter Spawn/PhaseSet/Retire backward scrub을 `HazardActorWorkbenchPreviewTests`에 고정했다.
  - renderer/performance: per-ghost immediate draw 금지 테스트, instanced renderer submission budget, renderer resource shutdown 검증을 추가했다.
  - 측정: `PreviewRenderer_MeetsMeasuredActorAndEncounterFrameBudgets` 단독 재실행 기준 Actor p95 `0.815 ms`, Encounter p95 `4.002 ms`, managed GC `0 B`, submissions Actor `2` / Encounter `5`.
  - 실제 UI smoke: operational actor Workbench live tree에서 `Timeline:`/`Selector:`/`Preview` 표시 확인, explicit Step 45회 후 ghost 8개 생성, `CleanupRemoved`로 7개 감소, Restart 후 time `0.033s`.
  - Stage Map smoke: `smd_demo_1`을 실제 `StageMapEditorWindow`에 로드하고 source `1001` Encounter Preview를 시작해 active actor 1, ghost 1, preview time `0.200s`, warning 없음, callback 2개를 확인했다.
  - Scene View smoke: active preview 상태에서 `Assets/Screenshots/hazard_actor_final_sceneview_smoke.png` 캡처를 성공했고, 완료 전 임시 screenshot/generated validation asset은 삭제 대상에 포함한다.
  - 전체 검증: compile 후 project console error 0, targeted Workbench 17/17, targeted StageMap 50/50, full EditMode 574/574, full PlayMode 46/46 pass. Console에는 MCP transport noise와 PlayMode expected-error 로그가 발생했으나 preview shutdown 후 callback 0을 확인했다.
- [x] R8. Workbench exact-position preview 회귀 수정
  - fixed 16x16 density grid를 기본 표시에서 제거하고, 각 visible ghost의 world XZ를 개별 pixel position으로 투영하는 UI Toolkit 단일 mesh renderer를 적용했다.
  - 기본 display는 `Exact`이며 `Density`는 사용자 명시 선택만 허용한다. view 밖 ghost는 clip하고 edge cell로 clamp하지 않는다.
  - session-only view center/zoom과 `Fit Active Ghosts`를 추가했으며 transport/view 조작은 asset dirty 대상이 아니다.
  - 위치 투영, out-of-view clipping, Exact 기본값과 Density 명시 선택을 EditMode 회귀 테스트로 고정했다.
  - live Workbench smoke: `pf_stage2_fan_sentry`, active 12 / visible exact 12 / UI submission 1.
  - 검증: `HazardActorWorkbenchPreviewTests` 19/19, full EditMode 576/576, full PlayMode 46/46 pass.

## Next
- 없음. 후속 R1~R7 감사 기준 구현과 검증을 완료했다.

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
  - 2026-08-06 후속 감사 기준으로는 `PROMPT.md` R1~R7 하드 게이트 미충족이 확인되어 이 완료 기록은 과거 T0~T8 기준 결과로만 보존한다.
