# HazardActor Hierarchy and Stage Application

## Metadata
- doc_id: `TD-030`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-17`
- related_docs:
  - [../ADR/ADR-20260408-01-hazard-actor-intermediate-hierarchy.md](../ADR/ADR-20260408-01-hazard-actor-intermediate-hierarchy.md)
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [./TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)

> 현재 runtime/stage apply 기준에서 `HazardActor`는 source가 직접 attach/reset/cleanup 하는 단일 위험 개체다. pattern slot, selector, emit runtime까지 actor가 직접 소유하며 child emitter hierarchy는 더 이상 현재 계약이 아니다.

## 1. 현재 계층
```text
Source
 -> HazardActor
```

- `Source`는 actor hierarchy의 lifecycle owner다.
- `HazardActor`는 아래를 직접 소유한다.
  - applied config
  - presence runtime
  - phase runtime
  - pattern selector state
  - pattern slot metadata / execution snapshot
  - emit runtime
- `HazardEmitter`는 현재 runtime hierarchy 요소가 아니다.

## 2. Actor 최소 런타임 계층
- identity / source seam
  - `HazardActorComponent`
  - `HazardActorPlacementComponent`
- config / baseline
  - `HazardActorAppliedConfigBaselineComponent`
  - `HazardActorAppliedConfigComponent`
  - `HazardActorRuntimeBaselineComponent`
  - `HazardActorBehaviorPhaseBaselineComponent`
- runtime mutable state
  - `HazardActorRuntimeStateComponent`
  - `HazardActorBehaviorPhaseStateComponent`
  - `HazardActorPhaseTransitionRuntimeComponent`
  - `HazardActorPatternSelectorStateComponent`
  - `HazardActorEmitStateComponent`
  - `HazardActorEmitActiveTelegraphComponent`
  - `HazardActorEmitActiveEmissionComponent`
  - `HazardActorEmitCycleSignalComponent`
- actor-owned buffers
  - `HazardActorPhaseSelectorPolicyBuffer`
  - `HazardActorPhaseSelectorCandidateBuffer`
  - `HazardActorPhaseProgressTransitionBuffer`
  - `HazardActorPatternSlotBuffer`
  - `HazardActorPatternExecutionSlotBuffer`

## 3. Authoring / Bake Contract
- authoring root는 `HazardActorAuthoring` 단일 root다.
- actor authoring은 최소한 아래 축을 직접 가진다.
  - `ActorId`
  - enabled/suppressed baseline
  - presence policy
  - phase selector policy/candidate
  - phase progress transition
  - `PatternSlots`
- `PatternSlots`는 actor-owned SSOT다.
- selector candidate는 `PatternSlotId`만 참조한다.
- child 발사 서브 authoring, actor 하위 roster bake, actor-to-emitter lookup seam은 현재 계약에서 제거됐다.

## 4. Stage Apply / Reset Owner
- owner: `StageTopologyApplyPrepareSystem`
- source apply 시 actor 처리 순서:
  1. placement attach / resolve
  2. actor baseline/applied config 복원
  3. actor presence runtime reset
  4. actor phase runtime reset
  5. actor selector runtime reset
  6. actor emit runtime reset
  7. source-owned orchestration/runtime reset
- actor emit reset은 최소한 아래를 baseline으로 돌린다.
  - lifecycle `Dormant`
  - elapsed `0`
  - active telegraph/emission의 `AppliedPatternSlotId = invalid`
  - cycle signal `CompletedVersion = 0`

## 5. Cleanup / Teardown Contract
- source cleanup은 actor entity만 수집/삭제한다.
- placement ref seam:
  - `SourceHazardActorPlacementRefBuffer`
  - `SourceHazardActorRefBuffer`
- source `LinkedEntityGroup`도 actor attach만 관리한다.
- emitter entity cleanup 루프는 현재 계약에 없다.

## 6. 문서 경계
- actor behavior/runtime owner와 update order: [TD-031](./TD-031-hazard-actor-behavior-runtime.md)
- actor placement/orchestration content frame: [TD-032](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
- direct emit request bridge: [TD-029](./TD-029-discrete-emit-spawn-bridge-contract.md)
