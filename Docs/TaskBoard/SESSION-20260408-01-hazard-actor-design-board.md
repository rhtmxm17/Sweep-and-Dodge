# SESSION-20260408-01

## Metadata
- doc_id: `SESSION-20260408-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-04-08`
- related_docs:
  - [./SESSION-20260407-01-hazard-emitter-design-board.md](./SESSION-20260407-01-hazard-emitter-design-board.md)
  - [../TechnicalDesign/TD-028-hazard-emitter-common-contract.md](../TechnicalDesign/TD-028-hazard-emitter-common-contract.md)
  - [../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md](../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)

## Session Goal
- 한 줄 목표: `HazardEmitter`를 상위 개념인 `HazardActor` 아래의 발사 ability slice로 재해석할 수 있는지 점검하고, actor/emitter ownership 축을 문서 기준으로 흔들리지 않게 정리한다.
- 완료 기준:
  - `HazardActor`를 별도 상위 개념으로 둘지 여부와 이유가 정리돼 있다.
  - `HazardActor`와 `HazardEmitter`의 책임 경계가 최소 수준으로 분리돼 있다.
  - 이후 문서/코드 이관 여부를 판단할 수 있을 만큼 actor 측 작업 항목이 보드 기준으로 분해돼 있다.

## Inherited Context
- 현재 구현된 `HazardEmitter`는 source 소속의 runtime 위험 주체처럼 사용되고 있으며, 발사 자체는 `DiscreteEmit` producer로 동작한다.
- `HazardEmitterCoordinatorSystem`은 source pressure / source progress / player distance를 읽어 activation gate를 계산한다.
- `HazardEmitterBinding`은 stage-applied baseline override seam까지만 담당한다.
- 플레이어 경험 기준으로는 emitter가 단순 발사 장치보다 `비공격 대상 몬스터형 hazard actor`에 가깝다는 합의가 형성됐다.

## Now
- [ ] Plan HA-2. authoring / baker hierarchy migration
  - 완료 기준:
    - `HazardActorAuthoring`가 도입되고 authoring 계층이 `Source -> Actor -> Emitter`로 바뀐다.
    - `HazardActorAuthoring.Baker`가 source -> actor ref를, `HazardEmitterAuthoring.Baker`가 actor -> emitter ref를 기록한다.
    - `HazardEmitterComponent`는 `ActorEntity`를 structural owner로 사용한다.
  - 검증:
    - bake/EditMode authoring tests
    - sample prefab hierarchy compile 회귀

## Next
- [ ] Plan HA-3. stage apply / explicit roster cutover
  - 완료 기준:
    - `StageTopologyApplyPrepareSystem`이 actor 기준 apply/reset 순서로 전환된다.
    - omitted actor/emitter는 explicit roster 규칙으로 비활성/미적용 처리된다.
    - 기존 source direct emitter apply seam은 제거된다.
  - 검증:
    - apply/reset EditMode tests
    - stage reapply/disable regression
- [ ] Plan HA-4. runtime compatibility migration
  - 완료 기준:
    - current runtime systems가 actor hierarchy를 읽도록 최소 migration 된다.
    - coordinator/emit build는 source를 actor를 통해 resolve한다.
    - actor runtime state/selector state는 도입되지만 selector 선택 로직 자체는 아직 구현하지 않는다.
  - 검증:
    - existing emitter discrete path regression
    - coordinator / emit build EditMode regression
    - PlayMode smoke
- [ ] Plan HA-5. validation / sample / document closeout
  - 완료 기준:
    - sample prefab/test fixture/generator/document index가 actor hierarchy 기준으로 정리된다.
    - `TD-028`, `TD-029`, `TD-030`, TaskBoard 사이 경계가 충돌하지 않는다.
  - 검증:
    - console error 0
    - EditMode full suite
    - PlayMode smoke full suite

## Blocked
- 없음

## Parking Lot
- [ ] P1. `HazardActor`가 발사 외 ability를 실제로 언제 받아야 하는지
  - 예: 근접 위험, field aura, contact hazard
- [ ] P2. actor-level motion owner를 `HazardActorMotionSystem`으로 분리할지 여부
- [ ] P3. `PatternSet / PatternSelector`를 emitter 전용 개념으로 둘지 actor 공용 개념으로 끌어올릴지 여부

## Done
- [x] D1. actor 관점 논의를 기존 `HazardEmitter` 구현 보드와 분리했다.
  - 이유: 현행 보드는 emitter/discrete emit 구현 이력이 누적돼 있어, 상위 actor 개념 논의를 같은 보드에 섞으면 범위와 owner가 흐려질 가능성이 높다.
- [x] D2. `HazardActor`를 상위 개념, `HazardEmitter`를 actor의 발사 ability slice로 두는 명명/계층 방향을 채택했다.
  - 이유: 현재 요구 범위는 발사 장치를 넘어 presence, motion, activation, pattern selection까지 포함하므로, `Emitter`보다 `Actor`가 개체 전체를 설명하는 이름으로 더 적합하다.
- [x] D3. `A2` ownership 분리 방향에 actor/emitter binding 분리 완료 시점을 추가로 고정했다.
  - 이유: `HazardActor` 구현이 끝날 때까지 binding이 분리되지 않으면 stage-applied config owner가 다시 섞일 가능성이 높다.
- [x] D4. `PresenceState`는 `Hidden / Activating / Active / Retiring` 4상태를 첫 계약으로 채택했다.
  - 이유: 개체의 존재/활성/퇴장 경험을 emitter의 발사 상태기계와 분리해 설명할 수 있어야 한다.
- [x] D5. `PatternSelector`는 actor owner로 두고, `PatternSet`과 분리하는 방향을 채택했다.
  - 이유: 패턴 선택은 발사 능력 내부 서브로직보다 개체의 행동 결정에 가깝고, future source state / player distance / presence 입력까지 자연스럽게 수용할 수 있다.
- [x] D6. `PatternSelector`의 최소 runtime state 초안을 `CurrentPatternSlotId / LastPatternSlotId / SelectionSequence`로 고정했다.
  - 이유: 단일 패턴 반복부터 조건부 선택까지 확장 가능하면서도 현재 단계에서 가장 작은 mutable selector state다.
- [x] D7. selector-emitter 전달 seam은 `slot reference seam`으로 두는 방향을 채택했다.
  - 이유: selector가 패턴 내용을 직접 복사하지 않고, actor는 선택 상태만 소유하고 emitter는 pattern data만 소유하는 구조가 owner 분리를 가장 잘 보존한다.
- [x] D8. emitter-owned `Cooldown`은 행동 결정용 cooldown이 아니라 ability 진행 상태로 보고, `Emitter Recovery` 명칭으로 재해석하기로 했다.
  - 이유: emit payload에는 불필요하지만 emitter가 다음 cycle을 시작할 수 있는지 설명하는 readiness/recovery state는 emitter owner에 남는 편이 적절하다.
- [x] D9. `PatternSet` runtime 표현은 `buffer of slot metadata + profile ref` 구조를 채택했다.
  - 이유: selector는 slot reference만 다루고, emitter는 pattern data를 소유하며, stage-applied baseline/applied layering과도 가장 자연스럽게 연결된다.
- [x] D10. `PatternSlot metadata` 최소 필드와 `PatternSlotId` 전략을 고정했다.
  - 검증 결과:
    - 최소 필드는 `PatternSlotId`, `TelegraphProfileRefId`, `EmissionProfileRefId`, `BaseWeight`, `AvailabilityFlags`로 본다.
    - `PatternSlotId`는 reorder와 binding 변경에 강한 stable id 기반으로 본다.
    - `AvailabilityFlags`는 첫 단계에서 bitmask 확장 슬롯으로 본다.
- [x] D11. `PatternSelector` 결과 state에 `TargetEmitterId`를 포함하고, selector 결과를 emitter-slot `1쌍`으로 제한하기로 했다.
  - 이유: 다중 ability actor 가능성은 낮더라도 계약을 열어두는 편이 안전하고, 첫 단계에서 multi-selection까지 허용하면 actor 행동 결정과 emit lowering이 동시에 복잡해질 수 있다.
- [x] D12. `PatternSelector`는 `PresenceState == Active`일 때만 유효하다는 결합 규칙을 채택했다.
  - 이유: 존재하지 않거나 퇴장 중인 actor가 패턴을 고르는 것을 구조적으로 막고, `Activating`에서 존재 강조 연출과 공격 선택을 분리할 수 있어야 한다.
- [x] D13. actor/emitter binding 분리 방향을 채택했다.
  - 검증 결과:
    - actor binding에는 actor-level 존재/활성/억제 계열 override를 올린다.
    - emitter binding에는 `LocalOffset`, `TelegraphProfileOverride`, `EmissionProfileOverride`를 남긴다.
    - 현재 `HazardEmitterBinding`의 `EnabledMode`, `StartSuppressedMode`는 future `HazardActorBinding`으로 이동 예정으로 본다.
- [x] D14. 최소 `HazardActorBinding` 필드를 `ActorId`, `EnabledMode`, `StartSuppressedMode`로 고정했다.
  - 이유: actor/emitter owner 분리를 위한 최소 seam을 만드는 것이 목적이며, 초기 존재/활성 override만 먼저 분리해도 stage-applied ownership을 안정적으로 자를 수 있기 때문이다.
- [x] D15. authoring 기준 `HazardActorBinding`이 `HazardEmitterBinding[]`를 감싸는 중첩 구조를 채택했다.
  - 이유: editor에서 다루는 authoring 데이터는 운영 단순화보다 구조와 관계 파악이 쉬운 형태를 우선해야 하며, actor-emitter 귀속 관계를 구조적으로 드러내는 편이 더 적합하기 때문이다.
- [x] D16. actor와 emitter 모두 stage binding에서 명시적으로 관리하는 방향을 채택했다.
  - 이유: 명시되지 않은 emitter가 baseline으로 남으면 authoring에서 파악되지 않은 동작이 발생할 수 있으므로, stage roster가 actor/emitter 모두에 대해 결정적이어야 한다.
- [x] D17. `HazardActorBinding.HazardEmitterBindings[]`는 빈 배열을 허용하기로 했다.
  - 이유: actor는 존재하되, 특정 stage에서는 발사 ability를 모두 비활성화한 채 presence나 다른 actor-level 동작만 유지하는 경우를 표현할 수 있어야 한다.
- [x] D18. `HazardActor` 최소 runtime 계층을 채택했다.
  - 검증 결과:
    - actor entity는 필수, emitter entity는 유지한다.
    - 최소 actor runtime 계층은 `HazardActorComponent`, baseline/applied config, `HazardActorRuntimeStateComponent`, `HazardActorPatternSelectorStateComponent`로 본다.
    - selector state는 stable `TargetEmitterId`와 stable `PatternSlotId`를 기준으로 행동 결정을 표현한다.
- [x] D19. `HazardActorAuthoring` 최소 필드와 계층 방향을 채택했다.
  - 검증 결과:
    - authoring 계층은 `SourceRuntimeTemplateAuthoring -> HazardActorAuthoring -> HazardEmitterAuthoring[]`로 본다.
    - `HazardActorAuthoring` 최소 필드는 `ActorId`, `Enabled`, `StartSuppressed`, `InitialPresenceState`다.
    - `InitialPresenceState`는 첫 계약에서 모든 presence 상태를 허용한다.
- [x] D20. actor/emitter baker 책임 분리 방향을 채택했다.
  - 검증 결과:
    - `HazardActorAuthoring.Baker`는 actor entity 생성, actor structural/baseline/applied/runtime/selector 초기 컴포넌트 기록, `HazardActorEmitterRefBuffer` 준비, source의 `SourceHazardActorRefBuffer` 등록을 담당한다.
    - `HazardEmitterAuthoring.Baker`는 emitter entity 생성, emitter structural/baseline/applied/runtime/profile 초기 컴포넌트 기록, actor의 `HazardActorEmitterRefBuffer` 등록을 담당한다.
    - actor baker는 source parent를, emitter baker는 actor parent를 validation으로 강제한다.
    - stage binding 적용, coordinator 계산, selector 결정, runtime motion/presence 전이는 baker 책임 밖으로 둔다.
- [x] D21. actor baker 세부 초기화 규칙의 첫 계약을 채택했다.
  - 검증 결과:
    - `InitialPresenceState`는 actor structural identity가 아니라 runtime reset 기준값으로 보고 baseline layer에 둔다.
    - selector state는 명시적 invalid sentinel 규칙을 사용한다.
    - 첫 계약의 selector 초기값은 `TargetEmitterId = -1`, `CurrentPatternSlotId = -1`, `LastPatternSlotId = -1`, `SelectionSequence = 0`이다.
- [x] D22. actor runtime baseline 분리와 selector reset 규칙을 채택했다.
  - 검증 결과:
    - `HazardActorAppliedConfigBaselineComponent`와 `HazardActorRuntimeBaselineComponent`를 분리한다.
    - `InitialPresenceState`는 `HazardActorRuntimeBaselineComponent`가 소유한다.
    - selector는 별도 baseline snapshot 없이 stage apply/reset 시 invalid sentinel 규칙으로만 초기화한다.
- [x] D23. stage apply/reset의 최종 순서를 actor 계층 기준으로 고정했다.
  - 검증 결과:
    - apply 순서는 `actor baseline -> actor binding -> actor runtime reset -> emitter baseline -> emitter binding -> emitter runtime reset -> coordinator/selector reset`이다.
    - actor가 emitter보다 상위 layer이며, actor applied 결과가 먼저 확정된 뒤 emitter ability applied/runtime이 정리된다.
    - 명시되지 않은 actor와 emitter는 baseline 유지가 아니라 비활성/미적용으로 정리하는 explicit roster 규칙과 함께 사용한다.
- [x] D24. `HazardActor` 상위 개념은 별도 TD로 분리하고, `TD-028`은 emitter 공통 계약 SSOT로 유지하기로 했다.
  - 검증 결과:
    - `TD-028`은 `HazardEmitter` ability slice와 emit/discrete contract의 SSOT로 유지한다.
    - actor 상위 계층, `HazardActorBinding`, actor/emitter ref buffer, actor-stage apply owner, actor authoring 계층은 새 TD로 분리한다.
    - actor 구현은 기존 emitter TD를 확장하지 않고 별도 TD 기준으로 시작한다.
- [x] D25. `TD-030`을 작성하고 actor 구현 범위를 플랜 모드 실행 단위로 분해했다.
  - 검증 결과:
    - `TD-030`이 actor hierarchy, binding/ref buffer, authoring/baker, apply/reset owner의 SSOT로 추가됐다.
    - 구현 범위는 `Plan HA-1`부터 `Plan HA-5`까지의 실행 단위로 분해됐다.
    - 현행 actor 보드는 더 이상 개념 논의 목록이 아니라 구현 시작점 기준으로 읽을 수 있게 정리됐다.
- [x] D26. `Plan HA-1` actor schema / binding cutover를 완료했다.
  - 검증 결과:
    - `StageSourceBinding.HazardActors`, `HazardActorBinding`, emitter-local `HazardEmitterBinding` 스키마가 코드와 generator에 반영됐다.
    - actor 최소 runtime schema와 `SourceHazardActorRefBuffer` / `HazardActorEmitterRefBuffer` 타입이 추가됐다.
    - source-level emitter stage binding apply path는 제거됐고, actor/emitter actual apply/reset behavior 검증은 `HA-3`로 이관됐다.
    - `refresh_unity(compile=request, wait_for_ready=true)` 이후 EditMode `465/465`, PlayMode `44/44`를 통과했다.

## End of Session
- 결과: 진행 중
- 다음 시작점: `Plan HA-1` actor schema / binding cutover부터 시작한다.
