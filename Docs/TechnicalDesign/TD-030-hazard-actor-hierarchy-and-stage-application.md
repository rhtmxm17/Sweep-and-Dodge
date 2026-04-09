# HazardActor Hierarchy and Stage Application

## Metadata
- doc_id: `TD-030`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-09`
- related_docs:
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [./TD-028-hazard-emitter-common-contract.md](./TD-028-hazard-emitter-common-contract.md)
  - [./TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TaskBoard/SESSION-20260408-01-hazard-actor-design-board.md](../TaskBoard/SESSION-20260408-01-hazard-actor-design-board.md)

> `HazardEmitter`를 `HazardActor`의 발사 ability slice로 재해석하기 위해, `Source -> HazardActor -> HazardEmitter` 계층, stage-applied binding, ref buffer, authoring/baker, apply/reset owner를 actor 기준으로 고정한다.

## 1. 문제 정의
- 현재 구현은 source가 emitter를 직접 소유하는 구조다.
- 그러나 설계 논의 결과, 플레이어가 인식하는 위험 주체는 `발사 장치`보다 `개체`에 가깝고 아래 축이 emitter 하나로 설명되기 어렵다.
  - presence/lifetime
  - activation orchestration
  - pattern selection
  - future motion/retire
- 이 상태에서 actor 계층 없이 기능을 계속 추가하면, stage binding, coordinator, selector, authoring hierarchy가 emitter 기준으로 다시 뭉친다.

## 2. 목표/비목표
- 목표:
  - `HazardActor`를 runtime/authoring/stage apply 상위 개념으로 고정한다.
  - `Source -> HazardActor -> HazardEmitter` 계층과 ref buffer lookup seam을 고정한다.
  - actor/emitter binding 분리와 explicit roster 규칙을 고정한다.
  - actor baseline/applied/runtime/selector 최소 계층과 stage apply/reset 순서를 고정한다.
  - 구현 범위를 플랜 모드 실행 단위로 분해해 바로 착수 가능한 상태로 만든다.
- 비목표:
  - motion/path policy 구현
  - pattern selector 실제 선택 로직 구현
  - multi-selection actor action contract
  - actor-level gate authoring UI
  - `DiscreteEmitRequest`/execution contract 상세

## 3. 구조 계약
### 3.1 계층
- runtime/authoring/stage apply 계층은 아래로 고정한다.

```text
Source
 -> HazardActor
   -> HazardEmitter
```

- `HazardActor`는 개체 전체를 설명한다.
  - presence
  - activation orchestration
  - pattern selection
  - future motion/retire
- `HazardEmitter`는 actor의 발사 ability slice다.
  - telegraph
  - emit
  - emitter recovery
  - `DiscreteEmitRequest` producer

### 3.2 Actor 최소 런타임 계층
- `HazardActorComponent`
  - `ActorId`
  - `SourceEntity`
- `HazardActorAppliedConfigBaselineComponent`
  - `IsEnabled`
  - `IsSuppressed`
- `HazardActorAppliedConfigComponent`
  - `IsEnabled`
  - `IsSuppressed`
- `HazardActorRuntimeBaselineComponent`
  - `InitialPresenceState`
- `HazardActorRuntimeStateComponent`
  - `PresenceState`
  - `StateElapsedSec`
- `HazardActorPatternSelectorStateComponent`
  - `TargetEmitterId`
  - `CurrentPatternSlotId`
  - `LastPatternSlotId`
  - `SelectionSequence`

### 3.3 Presence / Selector 최소 규칙
- `PresenceState`는 아래 4상태를 사용한다.
  - `Hidden`
  - `Activating`
  - `Active`
  - `Retiring`
- `PatternSelector`는 actor owner다.
- selector는 `PresenceState == Active`일 때만 유효하다.
- selector 초기값은 명시적 invalid sentinel 규칙을 사용한다.
  - `TargetEmitterId = -1`
  - `CurrentPatternSlotId = -1`
  - `LastPatternSlotId = -1`
  - `SelectionSequence = 0`
- selector는 첫 계약에서 emitter-slot `1쌍`만 선택한다.

### 3.4 Emitter structural 변경
- `HazardEmitterComponent`는 actor 하위 ability 구조를 반영하도록 본다.
  - `EmitterId`
  - `ActorEntity`
  - `ActivationPolicy`
  - `InitialLifecycleState`
  - `AnchorKind`
  - `Mobility`
- `SourceEntity`는 emitter가 직접 소유하지 않고 actor를 통해 resolve한다.
- emitter applied/profile/runtime layering은 [TD-028](./TD-028-hazard-emitter-common-contract.md)를 SSOT로 유지한다.

### 3.5 PatternSet runtime 표현
- pattern data owner는 emitter다.
- selector state owner는 actor다.
- emitter는 `buffer of slot metadata + profile ref` 형태의 pattern set을 가진다.
- slot metadata 최소 필드는 아래다.
  - `PatternSlotId`
  - `TelegraphProfileRefId`
  - `EmissionProfileRefId`
  - `BaseWeight`
  - `AvailabilityFlags`
- `PatternSlotId`는 stable id 기반으로 사용한다.

## 4. Stage Binding / Ref Buffer / Apply
### 4.1 Stage binding 최종 스키마
- `StageSourceBinding`은 아래 actor 기준 중첩 구조를 사용한다.
  - `HazardActorBinding[] HazardActors`
- `HazardActorBinding`
  - `ActorId`
  - `EnabledMode`
  - `StartSuppressedMode`
  - `HazardEmitterBinding[] Emitters`
- `HazardEmitterBinding`
  - `EmitterId`
  - `OverrideLocalOffset`
  - `LocalOffset`
  - `TelegraphProfileOverride`
  - `EmissionProfileOverride`

### 4.2 Explicit roster 규칙
- actor와 emitter 모두 stage binding에서 명시적으로 관리한다.
- `StageSourceBinding.HazardActors`에 없는 actor는 비활성/미적용으로 정리한다.
- `HazardActorBinding.Emitters`에 없는 emitter도 비활성/미적용으로 정리한다.
- `HazardActorBinding.Emitters == []`는 유효하다.
  - 의미: actor는 존재하지만 이 stage에서 활성 emitter ability는 없다.

### 4.3 Ref buffer 계약
- source root는 아래 버퍼를 가진다.

```csharp
SourceHazardActorRefBuffer
- Entity ActorEntity
- int ActorId
```

- actor root는 아래 버퍼를 가진다.

```csharp
HazardActorEmitterRefBuffer
- Entity EmitterEntity
- int EmitterId
```

- 기존 source 직하 emitter lookup seam은 direct cutover로 제거한다.
  - `StageSourceBinding.HazardEmitterBindings`
  - `SourceHazardEmitterRefBuffer`

### 4.4 Stage apply/reset owner와 순서
- `StageTopologyApplyPrepareSystem`가 stage-applied layer reset/override owner다.
- apply 순서는 아래로 고정한다.
  1. actor baseline 복원
  2. actor binding 적용
  3. actor runtime reset
  4. emitter baseline 복원
  5. emitter binding 적용
  6. emitter runtime reset
  7. coordinator/selector state reset
- actor runtime reset은 아래를 포함한다.
  - `PresenceState = InitialPresenceState`
  - `StateElapsedSec = 0`
- selector reset은 invalid sentinel 규칙을 사용한다.
- stage apply는 설정과 초기 상태만 적용한다.
  - coordinator 계산
  - selector 결정
  - emit progression
  - discrete backlog 정리
  는 stage apply owner 범위 밖이다.

## 5. Authoring / Baker
### 5.1 Authoring hierarchy
- authoring 계층은 아래로 고정한다.

```text
SourceRuntimeTemplateAuthoring
 -> HazardActorAuthoring
   -> HazardEmitterAuthoring[]
```

- `HazardActorAuthoring` 최소 필드:
  - `ActorId`
  - `Enabled`
  - `StartSuppressed`
  - `InitialPresenceState`
- `InitialPresenceState`는 4상태 모두 허용한다.

### 5.2 Baker ownership
- `HazardActorAuthoring.Baker`
  - actor entity 생성
  - actor structural/baseline/applied/runtime/selector 초기 컴포넌트 기록
  - `HazardActorEmitterRefBuffer` 준비
  - source의 `SourceHazardActorRefBuffer` 등록
- `HazardEmitterAuthoring.Baker`
  - emitter entity 생성
  - emitter structural/baseline/applied/runtime/profile 초기 컴포넌트 기록
  - actor의 `HazardActorEmitterRefBuffer` 등록
- validation:
  - actor baker는 source parent를 요구
  - emitter baker는 actor parent를 요구
- stage binding 적용, coordinator 계산, selector 결정, runtime motion/presence 전이는 baker 책임 밖이다.

## 6. 구현 완료 단위
### 6.1 Plan HA-1. Actor schema / binding cutover
- actor runtime component, actor binding type, stage schema, ref buffer type를 추가한다.
- `StageSourceBinding.HazardActors`로 스키마를 direct cutover 한다.
- acceptance:
  - compile 성공
  - stage definition / generator / validation이 새 스키마를 인식한다.
- 구현 상태:
  - 완료

### 6.2 Plan HA-2. Authoring / baker hierarchy migration
- `HazardActorAuthoring`를 도입하고 `HazardEmitterAuthoring` parent를 actor로 바꾼다.
- source -> actor / actor -> emitter ref buffer를 bake에서 완성한다.
- `HazardEmitterComponent`는 `ActorEntity`를 structural owner로 갖도록 전환한다.
- acceptance:
  - bake 후 source가 actor roster를, actor가 emitter roster를 가진다.
- 구현 상태:
  - 완료

### 6.3 Plan HA-3. Stage apply / explicit roster cutover
- `StageTopologyApplyPrepareSystem`를 actor 기준 apply/reset 순서로 전환한다.
- omitted actor/emitter는 explicit roster 규칙에 따라 비활성/미적용으로 정리한다.
- 기존 source direct emitter apply seam은 제거한다.
- acceptance:
  - stage 재적용 시 actor/emitter baseline/applied/runtime이 결정적으로 reset된다.
- 구현 상태:
  - 완료

### 6.4 Plan HA-4. Runtime compatibility migration
- current runtime systems가 actor hierarchy를 읽도록 최소 마이그레이션한다.
- `HazardEmitterCoordinatorSystem`과 `HazardEmitterEmitBuildSystem`은 source를 actor를 통해 resolve한다.
- actor runtime baseline/state/selector state는 도입하되, selector 선택 로직 자체는 아직 구현하지 않는다.
- acceptance:
  - existing emitter gameplay behavior는 유지되고, actor entity 존재 하에서도 discrete emit path가 회귀하지 않는다.
- 구현 상태:
  - 완료
- runtime compatibility boundary:
  - `HazardActorAppliedConfigComponent`는 현재 activation truth에 포함된다.
  - `PresenceState`와 selector invalid sentinel state는 아직 current emitter runtime을 gate하지 않는다.

### 6.5 Plan HA-5. Validation / sample / document closeout
- validation, sample asset graph, taskboard/TD closeout을 마감한다.
- actor/emitter hierarchy 기준의 bake/apply/runtime 회귀 테스트를 보강한다.
- acceptance:
  - compile, console error 0, EditMode, PlayMode smoke 통과
  - TD-028/TD-029/TaskBoard 간 경계가 충돌하지 않는다.
- 구현 상태:
  - 완료
- closeout 결과:
  - operational `SampleScene` 경로는 `stpc_demo -> pf_stage_source_template -> HazardActor/HazardEmitter` 최소 샘플을 가진다.
  - `PlayModeSmoke_SampleVerification`은 test-only topology catalog와 test-only actor sample prefab을 사용한다.
  - validation은 nested hazard binding duplicate/id contract, source template actor/emitter hierarchy integrity, operational asset의 test-only 참조 금지를 포함한다.

## 7. 검증 계획
- 문서 기준 검증:
  - `TD-028`은 emitter ability/local contract만 남고 actor hierarchy는 이 문서가 소유한다.
  - actor/emitter binding 분리와 explicit roster 규칙이 단일 해석 가능하다.
- 구현 acceptance:
  - source는 actor를 직접 찾고, actor는 emitter를 직접 찾는다.
  - stage apply는 actor -> emitter 순서로 baseline/applied/runtime을 결정적으로 reset한다.
  - 기존 emitter discrete emit path는 actor 계층 도입 후에도 회귀하지 않는다.
- 기본 검증 루프:
  - `refresh_unity(compile=request, wait_for_ready=true)`
  - console error 0
  - EditMode
  - PlayMode smoke

## 8. Assumptions
- actor 상위 계층 도입은 direct cutover로 진행한다. emitter-only compat bridge는 두지 않는다.
- `HazardEmitterCoordinatorSystem`의 이름은 당장 유지할 수 있지만, ownership 해석은 actor-side orchestration으로 본다.
- motion/path/pattern selection 실제 로직과 actor-level gate authoring은 이 TD 범위 밖이다.
