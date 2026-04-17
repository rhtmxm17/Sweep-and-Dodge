# ADR-20260408-01-hazard-actor-intermediate-hierarchy
> `Source -> HazardEmitter` 직접 구조에서 `Source -> HazardActor -> HazardEmitter` 계층으로 전환하고, `HazardEmitter`를 actor의 발사 ability slice로 재해석한 결정

## Metadata
- status: 합의됨 (문서 반영)
- related_docs:
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TaskBoard/SESSION-20260408-01-hazard-actor-design-board.md](../TaskBoard/SESSION-20260408-01-hazard-actor-design-board.md)

## 배경
- 기존 구현은 `Source -> HazardEmitter` 직접 소유 구조였다.
- `HazardEmitterCoordinatorSystem`이 source pressure / progress / player distance 기반 activation gate를 담당했고, stage binding은 emitter 단위(`HazardEmitterBinding`)였다.
- 설계 논의 결과, 플레이어가 인식하는 위험 주체는 발사 장치보다 "비공격 대상 몬스터형 개체"에 가깝다는 합의가 형성됐다.
- presence/activation/pattern selection/motion 같은 개체 축을 emitter 하나로 계속 수용하면 stage binding, coordinator, selector, authoring hierarchy가 emitter 기준으로 다시 뭉쳐 소유권이 흐려진다.

## 결정
1. **계층 전환**: `Source -> HazardActor -> HazardEmitter`로 전환한다. `HazardActor`가 상위 개념, `HazardEmitter`는 actor의 발사 ability slice다.
2. **PatternSelector owner**: PatternSelector는 actor owner로 두고, PatternSet(data)과 분리한다. 선택 결과는 emitter-slot `1쌍`으로 제한한다.
3. **Presence 4상태**: `Hidden / Activating / Active / Retiring`을 첫 계약으로 채택한다. selector는 `PresenceState == Active`일 때만 유효하다.
4. **Selector-emitter seam**: actor는 선택 상태만 소유하고 emitter는 pattern data만 소유하며, slot reference seam으로 연결한다.
5. **Emitter Recovery**: 기존 emitter `Cooldown`은 행동 결정용 cooldown이 아니라 ability 진행 readiness/recovery state로 재해석한다.
6. **Binding 분리**: actor-level 존재/활성/억제 override는 `HazardActorBinding`으로, emitter-level LocalOffset/Profile override는 `HazardEmitterBinding`으로 분리한다. authoring 계층은 `HazardActorBinding`이 `HazardEmitterBinding[]`을 감싸는 중첩 구조로 둔다.
7. **Explicit roster**: 명시되지 않은 actor와 emitter는 baseline 유지가 아니라 비활성/미적용으로 정리하는 explicit roster 규칙을 채택한다.
8. **TD 분리**: `HazardActor` 상위 계층 계약은 새 `TD-030`으로 관리하고, `TD-028`은 emitter ability slice와 emit/discrete contract SSOT로 유지한다.

## 대안
- **대안 1**: HazardEmitter를 확장해 actor 동작(presence, selector, orchestration)을 수용한다.
  - 단점: emitter owner가 발사 ability 이상을 소유하게 되어 stage binding과 ownership이 다시 뭉친다.
- **대안 2**: HazardActor를 HazardEmitter와 sibling으로 둔다(Source 직하위에 Actor와 Emitter 병렬).
  - 단점: Source가 두 타입을 동시 관리해야 하며 ref buffer 이중화, stage apply 순서 결정이 복잡해진다.

## 결과
- runtime/authoring/stage apply 계층이 `Source -> HazardActor -> HazardEmitter`로 고정됐다.
- stage apply/reset 순서가 `actor baseline -> actor binding -> actor runtime reset -> emitter baseline -> emitter binding -> emitter runtime reset -> coordinator/selector reset`으로 확정됐다.
- `HazardEmitterComponent`는 `SourceEntity` 대신 `ActorEntity`를 structural owner로 갖는다.
- source는 `SourceHazardActorRefBuffer`, actor는 `HazardActorEmitterRefBuffer`로 hierarchy ref를 보유한다.
- actor/emitter authoring 계층: `SourceRuntimeTemplateAuthoring -> HazardActorAuthoring -> HazardEmitterAuthoring[]`.

## 후속
- actor behavior runtime(presence 실제 progression, PatternSelector 선택 로직, phase 확장)은 `TD-031`에서 별도 세션으로 다룬다.
- stage-driven placement/orchestration 프레임은 `TD-032`에서 별도 세션으로 다룬다.
