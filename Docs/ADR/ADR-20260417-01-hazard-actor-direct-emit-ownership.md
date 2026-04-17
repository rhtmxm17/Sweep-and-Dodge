# ADR-20260417-01-hazard-actor-direct-emit-ownership
> `HazardEmitter` 런타임 엔티티를 제거하고 `HazardActor`가 pattern slot과 emit runtime을 직접 소유하도록 전환한 결정

## Metadata
- status: 합의됨 (문서 반영)
- related_docs:
  - [ADR-20260408-01-hazard-actor-intermediate-hierarchy.md](ADR-20260408-01-hazard-actor-intermediate-hierarchy.md)
  - [ADR-20260409-01-hazard-actor-behavior-runtime-phase-and-presence.md](ADR-20260409-01-hazard-actor-behavior-runtime-phase-and-presence.md)
  - [../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md](../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)

## 배경
- `ADR-20260408-01`은 `Source -> HazardActor -> HazardEmitter` intermediate hierarchy를 채택해 actor를 상위 개념으로 도입했다.
- 이후 `Presence`, `PhaseTransition`, `PatternSelector`는 actor owner로 정리됐지만, 실제 발사 경로는 여전히 emitter entity와 coordinator/build system에 남아 있었다.
- 이 상태는 아래 문제를 만들었다.
  - selector가 actor-owned인데 emit runtime은 emitter-owned라 소유권이 이중화된다.
  - stage topology reset/cleanup이 actor와 emitter를 모두 관리해야 한다.
  - `PatternSlot -> emit runtime -> discrete emit request` 경로가 actor가 아니라 emitter entity에 매달려 이해 비용이 높다.

## 결정
1. `HazardEmitter` 독립 런타임 엔티티를 제거한다.
2. 아래 런타임 데이터는 모두 `HazardActor`가 직접 소유한다.
   - pattern slot metadata
   - pattern execution snapshot
   - emit lifecycle state
   - active telegraph snapshot
   - active emission snapshot
   - cycle completion signal
3. selector contract는 `Phase -> PatternSlotId` 기준으로 단순화한다.
   - `EmitterId`
   - `TargetEmitterId`
   를 제거한다.
4. direct emit producer owner는 `HazardActorEmitSystem`으로 고정한다.
5. stage topology apply/reset/cleanup은 actor entity만 관리한다.
6. `HazardEmitter`는 gameplay-facing 용어와 profile asset 이름으로만 유지한다.

## 대안
- 대안 1: actor selector를 유지하되 emit runtime만 emitter entity로 남긴다.
  - 단점: selector/emit/state reset ownership이 계속 분리되고 topology cleanup이 복잡하다.
- 대안 2: actor를 제거하고 emitter 직접 구조로 되돌린다.
  - 단점: presence/phase/pattern selection/placement orchestration을 actor 단위로 읽게 만든 최근 설계와 충돌한다.

## 결과
- 현재 runtime hierarchy는 `Source -> HazardActor`다.
- `HazardEmitterCoordinatorSystem`, `HazardEmitterEmitBuildSystem`, `HazardActorEmitterRefBuffer`는 현재 계약에서 제거됐다.
- `PatternSlot / execution slot / emit runtime / cycle signal`은 actor-owned runtime으로 고정됐다.
- direct emit request provenance는 actor entity와 selected `PatternSlotId`를 사용한다.
- source placement/orchestration은 actor-only attach/reset/cleanup seam으로 단순화됐다.

## 후속
- `ADR-20260408-01`의 intermediate hierarchy 결정은 이 ADR에 의해 부분 supersede 된다.
- gameplay-facing `HazardEmitter` 용어와 기술문서의 actor-owned runtime 설명이 어긋나지 않도록 `TD-028/029/030/031/032`를 현재 SSOT로 유지한다.
