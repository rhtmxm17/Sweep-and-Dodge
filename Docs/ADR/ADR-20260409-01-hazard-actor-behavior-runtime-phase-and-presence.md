# ADR-20260409-01-hazard-actor-behavior-runtime-phase-and-presence
> Presence를 실제 runtime progression state로 승격하고, escalation을 slot mutation이 아닌 actor-owned phase 전환으로 모델링한 결정

## Metadata
- status: 합의됨 (문서 반영)
- related_docs:
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)
  - [../TaskBoard/SESSION-20260409-01-hazard-actor-behavior-board.md](../TaskBoard/SESSION-20260409-01-hazard-actor-behavior-board.md)
  - [ADR-20260408-01-hazard-actor-intermediate-hierarchy.md](ADR-20260408-01-hazard-actor-intermediate-hierarchy.md)

## 배경
- `TD-030` 이후 `PresenceState`는 reset 기준값으로만 존재했고, actual runtime progression은 없었다.
- `PatternSelector`는 invalid sentinel 상태만 가졌고, emitter는 단일 pattern always-cycle path를 유지했다.
- 이 상태에서 기능을 추가하면 actor 도입의 목적인 "개체처럼 읽히는 위험 주체"가 다시 emitter 수준의 단순 발사 장치로 축소될 위험이 있었다.
- 목표 청사진(GD-016)에서 핵심 시나리오는 "진행도 threshold에서 패턴이 강화되는 개체 행동"이었으며, 이를 구현하는 방식으로 두 가지 모델이 경합했다.

## 결정
### 결정 A — Presence를 실제 runtime progression으로 승격
- `PresenceState`는 reset-only가 아니라 `Hidden -> Activating -> Active -> Retiring` 실제 진행 상태로 동작한다.
- `HazardActorPresenceSystem`이 presence progression owner다.
- `PresenceState != Active`는 actor activation truth를 차단한다.
  - coordinator는 `ActorPresenceHidden / ActorPresenceActivating / ActorPresenceRetiring` reason으로 emitter를 차단한다.
  - actor `disabled/suppressed`는 presence system이 `Hidden`으로 clamp한다.
- actor-level activation/retire 시작은 `HazardActorPresencePresentationSignalComponent`로 ECS signal seam에 노출한다.

### 결정 B — escalation은 slot mutation이 아닌 actor-owned phase 전환으로 모델링
- escalation은 slot 내용의 조건부 변형이 아니라 actor-level phase state 전환으로 해석한다.
- `slot B`와 `slot B'`는 phase-conditioned variant가 아니라 별도 slot으로 둔다.
- actor-owned phase는 selector policy를 교체한다(`Phase 1: [A, B]`, `Phase 2: [A, B']`).
- escalation transition 모델:
  - actor-owned `HazardActorPhaseProgressTransitionBuffer`(`FromPhaseId / ToPhaseId / ProgressThresholdNormalized / TransitionLeadInSec`)가 transition rule을 소유한다.
  - threshold 평가는 `PresenceState == Active` actor에만 수행한다.
  - threshold 도달 시 즉시 phase를 바꾸지 않고 `Preparing` staging에 진입한다.
  - `Preparing` 동안 selector는 freeze되고, coordinator는 `ActorPhaseTransitionPreparing` reason으로 emitter 공격을 차단한다.
  - `TransitionLeadInSec` 경과 후 phase commit이 일어나고, selector는 새 phase policy로 fresh select한다.
  - first implementation은 commit당 단계 transition 하나만 허용한다.
- selector mode:
  - `OrderedPriority`: current phase 후보군 중 첫 eligible candidate를 지속 선택한다. 기존 compatibility path와 의미가 같다.
  - `OrderedCycle`: phase entry에서 첫 eligible candidate를 선택하고, selected emitter의 natural cycle completion edge에서만 다음 candidate로 advance한다. phase change는 hard boundary다.

## 대안
- **대안 — slot mutation 모델 (phase-conditioned variant)**: source 진행도에 따라 slot 내용 자체를 조건부로 바꿔 escalation을 표현한다.
  - 단점: slot 조합 수가 phase 조합에 지수적으로 늘어나고, "같은 slot이 상황에 따라 다른 내용"을 갖는 모델은 authoring 파악 비용이 크다.
  - 단점: presence와 escalation이 동일 slot schema에 섞여 연출 분리가 어렵다.

## 결과
- `HazardActorPresenceSystem`이 `HazardEmitterCoordinatorSystem` 이전에 update되어 activation truth를 먼저 확정한다.
- `HazardActorPhaseTransitionSystem`이 source progress truth(`CollectedCount / max(1, ThresholdDepleted)`)를 읽어 `Idle -> Preparing -> commit` 흐름의 runtime owner가 됐다.
- `HazardActorPatternSelectorSystem`은 `HazardEmitterCoordinatorSystem` 이후, `HazardEmitterEmitBuildSystem` 이전에 update된다.
- `StageTopologyApplyPrepareSystem`은 actor phase state / selector state / emitter cycle signal을 baseline idle로 reset한다.
- authoring validation은 invalid phase selector policy를 `CV091`, invalid phase transition을 `CV092`로 보고한다.

## 후속
- presence presentation asset schema와 bridge는 이 ADR 범위 밖이며 후속 세션에서 다룬다.
- multi-emitter coordinated action contract는 별도 범위다.
- actor motion/path 축은 `TD-031` 또는 별도 TD로 분리 여부를 별도 논의한다.
