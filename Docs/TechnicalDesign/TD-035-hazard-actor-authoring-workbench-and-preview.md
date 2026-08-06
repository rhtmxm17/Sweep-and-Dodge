# TD-035 HazardActor Authoring Workbench and Preview
> `HazardActorAuthoring` prefab, `StageMapDocument` encounter data, 공통 분석형 Preview Core를 연결하는 실무형 HazardActor 편집기 설계

## Metadata
- doc_id: `TD-035`
- type: `TechnicalDesign`
- status: `implemented`
- last_updated: `2026-08-06`
- related_docs:
  - [../ADR/ADR-20260805-01-hazard-actor-workbench-and-preview-ownership.md](../ADR/ADR-20260805-01-hazard-actor-workbench-and-preview-ownership.md)
  - [TD-031-hazard-actor-behavior-runtime.md](TD-031-hazard-actor-behavior-runtime.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [TD-033-emission-profile-common-schema.md](TD-033-emission-profile-common-schema.md)
  - [TD-034-stage-map-editor-replacement.md](TD-034-stage-map-editor-replacement.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)

## 1. 목표 / 비목표

### 1.1 목표
- 기본 Inspector의 중첩 배열을 대신하는 `HazardActor Workbench`에서 Actor, Phase, Transition, Pattern, 공유 emission profile을 제작한다.
- Phase/Pattern 관계와 `Telegraph -> Emit -> Cooldown` timing을 현재 runtime 계약과 같은 구조로 시각화한다.
- Workbench의 독립 preview와 Stage Map Editor의 실제 배치 preview가 동일한 분석형 Preview Core를 사용한다.
- `StageMapDocument`에서 placement와 source-local Spawn/PhaseSet/Retire orchestration을 함께 편집한다.
- preview와 validation이 runtime authoring resolver의 resolved data를 사용하며 미지원·잘림 상태를 숨기지 않는다.
- 편집기 steady preview의 반응성과 수명주기를 명시적으로 제한한다.

### 1.2 비목표
- runtime HazardActor ECS component, system ownership, baker, update order를 변경하지 않는다.
- 임의 조건/분기용 범용 node graph를 제공하지 않는다.
- actor 자체 movement/path authoring을 추가하지 않는다.
- v1에서 격리된 ECS Preview World 또는 완전한 runtime collision/cleanup simulation을 제공하지 않는다.
- Play Mode를 일상적인 authoring preview surface로 사용하지 않는다.
- 기존 prefab/profile을 신규 Blueprint asset으로 변환하지 않는다.

## 2. 소유권과 편집 경계

### 2.1 영속 데이터
- actor behavior owner: `HazardActorAuthoring` prefab
  - identity, presence baseline, initial phase, selector policies, pattern slots, progress transitions를 소유한다.
- emission content owner: 공유 `EmissionProfileSO`와 `HazardEmitterTelegraphProfileSO`
  - Workbench가 편집 표면을 제공해도 참조 asset의 공유 소유권은 바뀌지 않는다.
- stage encounter owner: `StageMapDocument`
  - placement와 source-local orchestration rule을 함께 소유한다.
- generated runtime input: `StageLayoutSO`, `StageDefinitionSO`, `StageCatalogSO`
  - document export 결과이며 Workbench/Preview의 편집 SSOT가 아니다.

### 2.2 Transient data
- `HazardActorEditingSession`은 선택 archetype, 선택 Phase/Transition/Pattern/Profile, foldout, filter, preview scope를 소유한다.
- `HazardActorPreviewSession`은 play state, elapsed time, source progress, target pose, forced phase/pattern, manual cleanup event와 표시 옵션을 소유한다.
- session 변경은 Undo/dirty/save 대상이 아니다.
- `Auto Play on Selection`은 user-local editor preference이며 기본값은 false다.

### 2.3 공식 편집 진입점
- `HazardActorWorkbenchWindow`가 actor behavior의 공식 편집 진입점이다.
- `HazardActorAuthoring` Custom Inspector는 다음만 제공한다.
  - read-only identity/Phase/Pattern/validation 요약
  - `Open HazardActor Workbench`
  - legacy/debug 경로 안내
- raw array 전체 편집을 공식 병행 경로로 유지하지 않는다.
- Stage orchestration 편집은 `StageMapEditorWindow`의 Encounter 패널만 소유한다.

## 3. Workbench UX

### 3.1 화면 구조
- UI Toolkit 기반 편집 layout을 사용한다.
  - toolbar `Current Archetype`: 현재 선택 prefab, Phase/Pattern/Profile 수, validation 요약을 표시한다.
  - `Change Archetype`: popup picker를 열어 prefab 검색, 선택, validation badge를 표 형태로 제공한다.
  - 중앙 `Behavior Canvas`: 선택된 Archetype 하나의 Phase/Pattern 상태 차트와 선택 항목 timing timeline을 표시한다.
  - 오른쪽 `Contextual Inspector`: 현재 canonical selection 하나의 상세만 표시한다.
- 하단에는 preview transport와 Issue Navigator를 둔다.
- 중앙 preview tab은 `Behavior`와 `Preview`를 전환하거나 분할할 수 있으며, preview는 top-down 기준이다.
- canvas는 `GraphView`나 임의 연결을 사용하지 않고 custom `VisualElement`로 현재 runtime 구조만 표현한다.
- Archetype Library는 상시 side panel이 아니라 짧은 선택/전환 작업을 위한 popup picker가 기본이다.
- Archetype popup은 `Name / Phases / Patterns / Profiles / Issues` 열을 제공하고, 선택 후 닫힌다.

### 3.2 Selection model
- canonical selection kind는 `Actor`, `Phase`, `Transition`, `PatternSlot`, `EmissionProfile`, `TelegraphProfile`이다.
- identity는 object reference와 안정 id 조합을 사용한다.
  - Phase: actor prefab + `PhaseId`
  - Transition: actor prefab + source `PhaseId`
  - Pattern: actor prefab + `PatternSlotId`
  - Profile: asset reference
- array index는 selection identity가 아니며 reorder/Undo/Redo 후 id로 재해석한다.
- missing, duplicate, ambiguous identity는 selection을 해제하고 issue를 생성한다.

### 3.3 Behavior Canvas
- Phase는 좌우 진행 순서로 표시하고 progress transition edge에 threshold와 lead-in을 표시한다.
- 각 Phase 내부에 selector mode와 candidate Pattern card를 순서대로 표시한다.
- Phase 목록은 `Phase / Selector / Candidates / Transition / Issues / Commands` 열을 가진 행 요약으로 표시한다.
- Pattern 목록은 `Pattern / Telegraph / Emission / Schedule / Movement / Issues / Commands` 열을 가진 행 요약으로 표시한다.
- Pattern card는 다음 resolved 요약을 표시한다.
  - position pattern
  - aim mode와 snapshot timing
  - shot pattern/count/N-way spacing
  - repeat/schedule/interval
  - telegraph/cooldown
  - movement family와 local offset
- `OrderedPriority`와 `OrderedCycle`은 서로 다른 badge와 실행 순서 표시를 사용한다.
- 선택 Pattern의 timeline은 실제 시간 비율로 `Telegraph`, event shot/repeat, `Cooldown` 구간을 표시한다.
- 긴 상세 문장은 기본 목록에 압축하지 않고 선택 후 Contextual Inspector 또는 선택 상세 영역에서 표시한다.

### 3.4 편집 명령
- Actor/Phase/Transition/Pattern의 단일 필드 편집은 `SerializedObject`로 즉시 반영한다.
- 구조 명령은 `Add`, `Duplicate`, `Move`, `Remove`를 제공하며 하나의 Undo group으로 기록한다.
- id 자동 할당은 같은 actor의 사용 중인 양의 id를 제외한 최소 양수를 사용한다.
- 삭제가 selector candidate, transition, stage PhaseSet rule에서 참조 중이면 영향 요약과 확인 없이 실행하지 않는다.
- 참조를 자동 재지정하지 않는다. 사용자가 참조 제거 또는 삭제 취소를 선택한다.

### 3.5 공유 Profile 편집
- 공유 `EmissionProfileSO`를 Contextual Inspector에서 명시적으로 편집할 수 있다.
- profile header에 asset path, actor/pattern 사용처 수, 공유 상태를 표시한다.
- `Open`은 Project/Inspector에서 해당 asset을 선택한다.
- `Duplicate & Assign`은 새 asset 경로를 확인한 뒤 profile을 복제하고 현재 Pattern만 새 asset을 참조하도록 하나의 Undo group으로 처리한다.
- 공유 profile 직접 변경은 영향받는 사용처를 표시하지만 자동 복제하지 않는다.
- 지원되는 SerializeReference grammar 교체는 명시적 type picker와 destructive confirmation을 사용한다.

## 4. Preview Core

### 4.1 타입과 데이터 흐름
- `HazardActorPreviewSnapshotBuilder`
  - actor authoring과 profile 참조를 읽고 immutable `HazardActorPreviewSnapshot`을 만든다.
  - `HazardActorPatternSlotAuthoringUtility`와 기존 profile resolver의 resolved 결과를 사용한다.
  - runtime buffer의 의미를 다시 해석하는 별도 preview 전용 authoring 규칙을 만들지 않는다.
- `HazardActorPreviewSimulator`
  - snapshot과 preview input을 받아 fixed-step으로 presence, phase, selector, emit lifecycle과 ghost motion을 진행한다.
- `HazardActorPreviewSession`
  - Pattern/Actor/Encounter scope, transport, source progress, target, forced selection과 warning state를 소유한다.
- `HazardActorPreviewRenderer`
  - simulator output을 embedded preview와 Scene View에 표시하며 simulation state를 수정하지 않는다.

### 4.2 Preview scope
- `Pattern`
  - 선택한 Pattern 하나를 반복하며 Phase/selector/orchestration을 건너뛴다.
- `Actor`
  - 선택 actor의 presence, phase transition, selector, telegraph, emit, cooldown을 재생한다.
- `Encounter`
  - 선택 source의 placements와 orchestration rule을 source progress에 따라 함께 재생한다.
- scope 전환과 snapshot 변경은 time/state를 0으로 reset하고 자동 재생하지 않는다.

### 4.3 Transport와 입력
- 공통 transport는 `Play`, `Pause`, `Restart`, `Step`을 제공한다.
- Step은 1 fixed step인 `1/30초`를 진행한다.
- source progress는 기본적으로 time과 독립된 수동 slider다.
- optional `Sweep Progress`는 session-only duration 동안 0에서 1까지 선형 진행하며 기본 duration은 10초다.
- progress 또는 time을 뒤로 scrub하면 초기 상태에서 해당 지점까지 고정 step으로 재평가한다.
- player target은 draggable preview handle이며 Stage Map에서는 PlayerStart를 최초 기본값으로 사용한다.
- forced Phase/Pattern은 preview 격리용이며 asset이나 runtime rule을 변경하지 않는다.

### 4.4 지원 행동과 제한
- v1은 현재 resolved schema의 position, aim, shot, schedule, repeat, speed/lifetime override를 재생한다.
- ghost movement는 `Linear`, `DampedLinear`, `HomingLite`를 지원한다.
- `MotionCompleted` lifecycle emission은 최대 재귀 깊이 3까지 재생한다.
- `CleanupRemoved`는 toolbar 명령으로 가장 오래된 활성 ghost 하나에 수동 발생시킨다.
- 실제 gameplay collision, player hit, collect/cleanup 판정은 계산하지 않는다.
- 미지원 mode, 잘못된 profile, cap/depth 초과는 정상처럼 생략하지 않고 preview warning과 Issue Navigator에 표시한다.

### 4.5 시각화
- 항상 표시하는 debug layer:
  - actor proxy와 forward
  - slot origin/local offset
  - telegraph cone/arc/line과 진행도
  - aim ray와 spawn point
  - ghost bullet과 예측 trajectory
  - 현재 presence/phase/pattern/lifecycle HUD
- actor/bullet prefab에서 안전하게 renderer resource를 해석할 수 있으면 inert ghost 외형을 함께 표시한다.
- collider, rigidbody, script, particle system, animation은 preview simulation owner가 아니며 실행하지 않는다.
- renderer를 해석할 수 없거나 prefab에 외형이 없으면 debug proxy를 사용한다.
- active scene에 preview GameObject를 영속 생성하지 않는다.

### 4.6 성능 예산
- simulation fixed step과 repaint 요청은 최대 30Hz다.
- Actor Preview는 active ghost 최대 1,024개, Encounter Preview는 최대 4,096개다.
- trajectory는 독립 branch당 최대 16 sample이며 예측 시간은 resolved lifetime과 4초 중 작은 값이다.
- cap을 넘으면 preview를 중단하지 않고 방향 bundle, 대표 trajectory, density count로 집약한다.
- unchanged snapshot의 steady preview는 managed allocation 0을 목표로 한다.
- renderer resource, geometry, resolved snapshot은 signature 기반으로 cache하고 authoring/selection/visibility 변경 시에만 재생성한다.
- preview update는 `EditorApplication.update` 단일 owner가 수행하고 Scene repaint callback에서 전체 재계산하지 않는다.

### 4.7 수명주기
- Workbench와 Stage Map Editor는 동시에 별도 simulator를 진행하지 않고 전역 preview coordinator의 active session 하나를 공유한다.
- selection/document/scope 변경, Undo/Redo, asset import는 snapshot을 invalidate하고 session을 안전하게 reset한다.
- Scene/Window close, assembly reload, prefab stage 변경, Play Mode 진입 시 preview resource와 hidden instance를 즉시 정리한다.
- preview renderer가 만든 object/resource에는 save되지 않는 hide flag를 적용한다.

## 5. Stage Map Encounter 편집

### 5.1 Document schema
- `StageMapDocument.SchemaVersion`을 올리고 `StageMapHazardActorOrchestrationRuleData[]`를 추가한다.
- rule data는 다음을 가진다.
  - `OwningSourceStableId`
  - source-local `RuleId`
  - `TargetPlacementInstanceIds[]`
  - `ActionType`
  - `TriggerType`
  - `TriggerThresholdNormalized`
  - `TargetPhaseId`
- placement와 target identity는 항상 owning source id를 포함한다.
- runtime `StageDefinitionSO`의 `HazardActorOrchestrationRuleBinding` shape는 변경하지 않는다.

### 5.2 Encounter Track
- `StageMapEditorWindow`에 선택 source 기준 `Hazard Encounter` 패널을 추가한다.
- 각 placement를 한 행으로 표시하고 normalized source progress를 가로축으로 사용한다.
- `OnStageStart` rule은 0 지점의 고정 lane에, `OnSourceProgressAtOrAbove` rule은 threshold 위치에 표시한다.
- Spawn/PhaseSet/Retire marker를 생성, 선택, 이동, 복제, 삭제할 수 있다.
- 다중 target rule은 하나의 rule marker와 각 target row로 이어지는 fan-out으로 표시한다.
- rule 선택과 placement 선택은 기존 `StageMapSelection`의 canonical identity 정책을 확장해 관리하며 Contextual Inspector는 선택 하나만 표시한다.
- `PhaseSet` picker는 모든 target archetype이 공통으로 정의한 PhaseId 교집합만 선택 가능하게 한다.
- 기존 값이 일부 target에 유효하지 않으면 값을 숨기지 않고 error 상태로 표시하며 export를 차단한다.

### 5.3 Scene View Preview
- HazardActor placement 선택 시 preview snapshot을 준비하고 정적 proxy를 표시한다.
- 명시적 Play 이후에만 Actor 또는 Encounter simulation을 시작한다.
- world pose는 현재 document의 source anchor, placement local offset, local yaw, slot local offset으로 계산한다.
- legacy scene marker transform과 generated `StageDefinitionSO`는 preview pose의 SSOT로 사용하지 않는다.
- Scene View Overlay는 Workbench와 같은 transport/scope/progress/target 상태를 사용한다.

### 5.4 Migration / Export
- schema migration은 load 시 자동으로 rule을 쓰지 않는다.
- migration preview는 document의 `TargetDefinition`에서 source binding별 orchestration rule을 읽어 candidate를 만든다.
- 다음 조건에서는 import를 거부한다.
  - target definition 또는 source binding identity가 없거나 ambiguous함
  - source-local RuleId 또는 target placement identity가 중복됨
  - target placement가 document에 없거나 다른 source에 속함
- 사용자는 `Validate/Preview -> Diff -> Apply`를 거쳐 candidate를 document에 반영한다.
- migration plan은 document와 target definition signature를 보관하고 apply 직전 변경되면 stale로 거부한다.
- migration 완료 후 exporter는 document rule을 `StageDefinitionSO`에 기록한다.
- 기존 target definition rule을 암묵적으로 보존하거나 merge하지 않는다.
- legacy `HazardActorSourceAuthoringMarker`는 명시적 legacy import/debug 입력으로만 남긴다.

## 6. Validation / Undo / Failure Policy
- actor validation은 기존 `HazardActorAuthoringValidationUtility`와 profile resolver를 기준으로 한다.
- Workbench issue는 severity, code, message, target kind, actor/profile reference, optional PhaseId/PatternSlotId를 가진다.
- Stage encounter issue는 source id, rule id, optional placement id를 structured target으로 가진다.
- Issue 선택 시 Workbench selection 또는 StageMap selection을 갱신하고 해당 canvas/track/Scene 위치로 이동한다.
- 단일 필드와 구조 편집은 대상 prefab/profile/document를 정확히 기록한 Unity Undo group으로 처리한다.
- preview transport, scrub, target, visibility, user preference는 Undo와 dirty 대상이 아니다.
- validation error가 있어도 안전하게 해석 가능한 부분은 정적으로 표시할 수 있지만 Play/export는 차단한다.
- destructive 구조 변경과 profile 복제는 영향 요약과 사용자 확인을 요구한다.

## 7. Runtime 경계와 업데이트 순서
- 신규 코드는 editor-only assembly/폴더에 둔다.
- preview는 runtime ECS World, Native container, Fence에 접근하지 않는다.
- runtime owner와 순서는 변경하지 않는다.
  - `HazardActorPresenceSystem`
  - `HazardActorPhaseTransitionSystem`
  - `HazardActorPatternSelectorSystem`
  - `HazardActorEmitSystem`
  - `DiscreteEmitExecutionSystem`
- preview simulator는 이 순서의 의미를 모사하지만 runtime writer가 아니며 gameplay state를 publish하지 않는다.
- runtime behavior 변경이 필요하다고 판단되면 TD-031/032와 관련 ADR을 먼저 갱신하고 별도 승인을 받는다.

## 8. 작업 분해
- T0. Legacy Inspector freeze
  - read-only 요약과 Workbench 진입점을 제공하고 공식 raw array 편집 경로를 닫는다.
- T1. Workbench shell
  - UI Toolkit Window, toolbar current archetype selector, popup Archetype picker, canonical selection, Contextual Inspector, session 수명주기를 구현한다.
- T2. Behavior/Profile editing
  - Phase/Pattern canvas, timeline, 구조 명령, 공유 profile 사용처와 Duplicate & Assign을 구현한다.
- T3. Preview snapshot/simulator
  - resolver 기반 snapshot, fixed-step lifecycle, movement와 lifecycle trigger 제한을 구현한다.
- T4. Preview surfaces
  - embedded preview, Scene View Overlay, inert ghost/proxy renderer, cache와 cleanup을 구현한다.
- T5. Stage document orchestration
  - schema, validation, explicit migration, dry-run/export/stale 정책을 구현한다.
- T6. Encounter Track
  - source track, rule/target 편집, progress scrub, Actor/Encounter preview 연결을 구현한다.
- T7. Issue navigation
  - actor/profile/stage structured issue와 canvas/track/Scene navigation을 구현한다.
- T8. 검증과 migration 완료
  - 기존 콘텐츠 migration, UX smoke, 성능 측정, runtime 회귀 검증을 완료한다.
- Parking Lot
  - actor motion/path authoring
  - arbitrary condition/orchestration graph
  - ECS Sandbox runtime-accurate preview
  - advanced playtest tooling

## 9. 검증 계획 / 합격 기준

### 9.1 EditMode
- snapshot builder가 기존 authoring resolver와 동일한 execution field를 생성한다.
- phase transition, OrderedPriority/OrderedCycle, telegraph/emit/cooldown fixed-step 결과를 검증한다.
- Linear/DampedLinear/HomingLite와 target snapshot timing을 대표 profile로 검증한다.
- MotionCompleted depth 3, cleanup manual trigger, ghost cap과 집약 warning을 검증한다.
- selection reconcile, shared profile edit, Duplicate & Assign, 구조 명령 Undo/Redo와 dirty 대상을 검증한다.
- StageMapDocument schema migration preview/diff/apply, stale rejection, rule validation과 export round-trip을 검증한다.
- 기존 `StageDefinitionSO` orchestration rule이 명시적 migration에서 의미 손실 없이 document로 이동하는지 검증한다.

### 9.2 Editor UX smoke
- 기본 Inspector 없이 Actor, Phase, Pattern, Transition, Profile을 생성·수정할 수 있다.
- GD-016 대표 actor를 Pattern/Actor Preview로 판독할 수 있다.
- 선택 시 preview는 준비되지만 자동 재생되지 않고, transport와 Undo/Redo 후 selection/preview가 일관된다.
- Stage Map에서 실제 placement 위치/yaw로 telegraph와 탄막이 표시된다.
- Source Progress scrub과 역방향 재평가에서 Spawn/PhaseSet/Retire 결과가 결정적이다.
- issue 클릭 시 해당 Workbench 항목, Encounter marker 또는 Scene 위치로 이동한다.

### 9.3 성능 / 수명주기
- steady preview에서 current-thread managed allocation 0을 확인한다.
- 1,024/4,096 ghost cap, branch당 16 trajectory sample, 30Hz 상한과 집약 전환을 측정한다.
- Window/Scene close, domain reload, Play Mode 전환 후 hidden object, callback, native/editor resource가 남지 않는다.

### 9.4 Runtime 회귀
- Unity compile 성공과 Console error 0을 확인한다.
- 관련 EditMode 테스트와 기존 HazardActor PlayMode smoke를 통과한다.
- runtime component, baker output, update order, discrete emit producer 계약에 의도하지 않은 변경이 없어야 한다.

## 10. 2026-08-05 구현 / 검증 결과
- T0: `HazardActorAuthoringEditor`를 추가해 기본 Inspector를 read-only summary, validation, `Open HazardActor Workbench` 진입점으로 제한했다.
- T1~T2: `HazardActorWorkbenchWindow`와 command utility를 추가해 Actor/Phase/Transition/Pattern/Profile canonical selection, Contextual Inspector, 구조 명령, shared profile 사용처, `Open`, `Duplicate & Assign`, Undo/dirty 경로를 구현했다.
- T3: `HazardActorPreviewSnapshotBuilder`와 `HazardActorPreviewSession`이 기존 authoring resolver/profile resolver 결과를 사용하며 30Hz fixed-step Presence/Phase/Selector/Telegraph/Emit/Cooldown, Linear/DampedLinear/HomingLite, MotionCompleted depth 3, manual CleanupRemoved를 재생한다.
- T4: Workbench embedded preview와 Scene View preview는 `HazardActorPreviewCoordinator`의 active session을 공유한다. Actor cap 1,024, Encounter cap 4,096, branch sample 16, 30Hz update owner, callback cleanup을 editor-only로 구현했다.
- T5: `StageMapDocument` schema v3에 source-local `StageMapHazardActorOrchestrationRuleData[]`를 추가했다. TargetDefinition rule import는 preview/diff/apply 전용이며 stale/missing/ambiguous identity를 거부하고, exporter는 document rule을 authoritative하게 기록한다.
- T6: Stage Map Editor `Hazard Encounter` track에서 source별 placement row, Spawn/PhaseSet/Retire marker, multi-target target id edit, common PhaseId picker, duplicate/move/delete, progress scrub preview를 document pose 기준으로 연결했다.
- T7: actor/workbench issue와 StageMap source/rule/placement issue target mapping을 canonical selection과 Scene View navigation으로 확장했다.
- T8: `smd_demo_1` actual document를 schema v3로 명시 migration하고 기존 `sd_demo_1` source rule을 document-owned rule로 반영했다. Runtime ECS component, baker, owner, update order, Fence 규칙은 변경하지 않았다.

검증 결과:
- EditMode targeted Workbench/StageMap suite: 58/58 pass.
- EditMode full: 565/565 pass.
- PlayMode full: 46/46 pass. 첫 전체 실행에서 pause intervention smoke 1건이 실패했으나 단독 재실행 1/1 pass, 이후 전체 재실행 46/46 pass.
- Preview steady step managed allocation: `HazardActorWorkbenchPreviewTests.PreviewSimulator_SteadyStepDoesNotAllocateManagedMemory`에서 0 byte assertion pass.
- MCP Editor smoke: `Tools/Project/Hazard Actor Workbench/Open`와 `Tools/Project/Stage Map Editor/Open` menu item 실행 성공, Scene View screenshot capture 성공.

## 11. 2026-08-06 후속 구현 / 검증 결과
- Preview coordinator는 editor callback count가 아니라 wall-clock anchor에서 target preview time을 계산하고 `EvaluateAt` fixed-step catch-up으로 재생한다. Workbench는 `PreviewRepaintRequested`에서 embedded preview repaint를 요청한다.
- Preview simulator는 `EventRepeatCount`, `EventShotSchedule`, `EventShotIntervalSec`를 반영하고, `LineEven` spawn count를 branch trajectory sample cap과 분리한다.
- Scene View ghost renderer는 `Graphics.DrawMeshInstanced` 기반 batch renderer를 사용한다. Workbench embedded preview는 UI Toolkit `MeshGenerationContext`의 단일 mesh에 각 visible ghost의 정확한 투영 위치를 기록한다. `Exact`가 기본이며 `Density`는 사용자가 명시적으로 선택하는 진단용 집약 모드다. 화면 밖 ghost는 가장자리로 clamp하지 않고 clip한다.
- Encounter preview session은 source placement plan과 document-owned Spawn/PhaseSet/Retire rule preview를 보유하고, progress scrub forward/backward에서 active actor set과 forced phase를 재평가한다.
- Workbench는 actor/profile/telegraph dirty signature를 주기적으로 감지해 snapshot/preview를 자동 재생성하고, canvas에 selector/state summary와 pattern timeline summary를 표시한다.

검증 결과:
- `HazardActorWorkbenchPreviewTests`: 17/17 pass.
- StageMap targeted suite (`StageMapSampleMigrationAndWindowSmokeTests`, `StageMapDocumentTests`, `StageMapEditorInteractionTests`): 50/50 pass.
- EditMode full: 574/574 pass.
- PlayMode full: 46/46 pass.
- Preview performance 단독 측정: Actor p95 `0.815 ms`, Encounter p95 `4.002 ms`, managed GC `0 B`, submissions Actor `2` / Encounter `5`.
- MCP Editor smoke: operational actor Workbench live tree에서 `Timeline:`/`Selector:`/`Preview` 확인, explicit Step 45회 후 ghost 8개 생성, `CleanupRemoved` 후 7개, Restart 후 time `0.033s`.
- Stage Map smoke: `smd_demo_1` source `1001` Encounter Preview에서 active actor 1, ghost 1, preview time `0.200s`, warning 없음.
- Scene View smoke: active preview 상태에서 Scene View screenshot capture 성공. 완료 전 임시 screenshot/generated validation asset은 삭제했다.
- Runtime ECS component, baker output, update order, owner/Fence 규칙은 변경하지 않았다.

정확 위치 표시 후속 검증:
- Workbench fixed density 기본 표시를 제거하고, `Exact` 기본/`Density` 명시 선택, view center/zoom, `Fit Active Ghosts` 조작을 추가했다.
- world XZ를 preview pixel로 직접 투영하며 서로 다른 위치를 개별 quad로 유지하고, view 밖 좌표는 edge cell로 왜곡하지 않는다.
- Workbench 가시성 개선으로 상시 `Archetype Library` side panel을 제거하고 toolbar `Change Archetype` popup picker로 대체했다. popup은 `Name / Phases / Patterns / Profiles / Issues` 열을 제공하며 선택 후 닫힌다.
- Phase/Pattern 기본 목록은 긴 버튼 텍스트 대신 열 기반 행 요약으로 표시하고, 상세 편집은 기존 Contextual Inspector의 canonical selection 하나에만 표시한다.
- `pf_stage2_fan_sentry` live Workbench smoke에서 active ghost 12개, visible exact ghost 12개, UI mesh submission 1회를 확인했다.
- `HazardActorWorkbenchPreviewTests`: 가시성 회귀 테스트 2개를 포함해 21/21 pass.
- `HazardEmitterPlayModeTests`: 기존 HazardActor PlayMode smoke 2/2 pass.
- 전체 EditMode/PlayMode 최신 재실행은 별도 검증 대상이다.
