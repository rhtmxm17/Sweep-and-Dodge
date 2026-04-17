# HazardActor Behavior Runtime

## Metadata
- doc_id: `TD-031`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-17`
- related_docs:
  - [../ADR/ADR-20260409-01-hazard-actor-behavior-runtime-phase-and-presence.md](../ADR/ADR-20260409-01-hazard-actor-behavior-runtime-phase-and-presence.md)
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)
  - [./TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [./TD-030-hazard-actor-hierarchy-and-stage-application.md](./TD-030-hazard-actor-hierarchy-and-stage-application.md)

> `HazardActor`는 현재 `Presence + PhaseTransition + PatternSelector + actor-owned emit runtime`을 직접 소유하는 위험 개체다. 이 문서는 actor behavior/runtime의 현재 SSOT다.

## 1. 목적
- actor behavior owner와 update order를 현재 구현 기준으로 고정한다.
- selector contract를 `Phase -> PatternSlotId` 기준으로 정리한다.
- direct emit runtime이 actor-owned slot execution snapshot을 사용한다는 점을 명시한다.

## 2. Runtime Ownership
### 2.1 Presence
- owner: `HazardActorPresenceSystem`
- 상태:
  - `Hidden`
  - `Activating`
  - `Active`
  - `Retiring`
- actor `disabled/suppressed`는 presence owner가 `Hidden`으로 clamp한다.
- `PresenceState != Active`는 selector/emit progression을 차단한다.

### 2.2 Phase transition
- owner: `HazardActorPhaseTransitionSystem`
- progress truth는 source progress를 사용한다.
- transition runtime:
  - `Idle`
  - `Preparing`
- `Preparing` 동안 selector는 freeze되고 emit은 차단된다.

### 2.3 Pattern selector
- owner: `HazardActorPatternSelectorSystem`
- selector candidate는 actor-owned `PatternSlotId`만 참조한다.
- current runtime state:
  - `CurrentPatternSlotId`
  - `LastPatternSlotId`
  - `SelectionSequence`
  - `CurrentCandidateOrder`
  - `LastResolvedPhaseVersion`
  - `LastConsumedCycleVersion`
- 제거된 개념:
  - actor가 특정 하위 emitter를 고르는 별도 target 개념
  - candidate의 emitter-local lookup key
- selection mode:
  - `OrderedPriority`
  - `OrderedCycle`

### 2.4 Actor-owned emit runtime
- owner: `HazardActorEmitSystem`
- actor가 직접 소유하는 emit runtime:
  - `HazardActorEmitStateComponent`
  - `HazardActorEmitActiveTelegraphComponent`
  - `HazardActorEmitActiveEmissionComponent`
  - `HazardActorEmitCycleSignalComponent`
- actor-owned slot runtime data:
  - `HazardActorPatternSlotBuffer`
  - `HazardActorPatternExecutionSlotBuffer`
- emit system은 selected `PatternSlotId`의 execution snapshot을 읽어 아래 상태기계를 진행한다.
  - `Dormant -> Telegraph -> Emit -> Cooldown`

## 3. Slot / Execution Contract
- pattern data owner는 actor다.
- slot metadata는 actor buffer에 정규화되어 저장된다.
- execution snapshot은 slot별 별도 buffer에 저장된다.
- 최소 execution seam:
  - `PatternSlotId`
  - telegraph/emission profile ref
  - `LocalOffset`
  - resolved discrete emit grammar
- actor direct emit anchor는 `actor transform + slot LocalOffset`로 계산한다.

## 4. Update Order
- current behavior order:
  - `HazardActorPresenceSystem`
  - `HazardActorPhaseTransitionSystem`
  - `HazardActorPatternSelectorSystem`
  - `HazardActorEmitSystem`
  - `DiscreteEmitExecutionSystem`
- 해석:
  - presence가 actor gate를 먼저 확정한다.
  - phase transition이 source progress 기반 상태 변화를 확정한다.
  - selector가 current phase에서 slot을 고른다.
  - emit이 telegraph/emit/cooldown을 진행하고 discrete request를 append한다.

## 5. Current Runtime Rules
- actor가 `Hidden/Activating/Retiring`이면 emit runtime은 즉시 dormant/reset 처리된다.
- actor가 `Preparing`이면 emit runtime은 공격을 진행하지 않는다.
- selected slot이 바뀌면 emit lifecycle은 `Dormant + timer 0`으로 hard reset된다.
- `OrderedCycle`의 advance edge는 actor-local `CompletedVersion`이다.
- cycle completion signal은 natural cycle completion에서만 증가한다.

## 6. 문서 경계
- hierarchy/apply/reset/cleanup owner: [TD-030](./TD-030-hazard-actor-hierarchy-and-stage-application.md)
- placement/orchestration content frame: [TD-032](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
- discrete emit request contract: [TD-029](./TD-029-discrete-emit-spawn-bridge-contract.md)

## 7. 검증 기준
- active 기술문서는 actor-only hierarchy, actor-owned slot/runtime, `HazardActorEmitSystem` producer 경계를 현재 계약으로 설명해야 한다.
- 과거 emitter entity/runtime 설명은 superseded 문서나 ADR 맥락에서만 남긴다.
