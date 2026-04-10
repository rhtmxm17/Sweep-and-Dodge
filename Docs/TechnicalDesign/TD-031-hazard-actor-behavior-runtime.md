# HazardActor Behavior Runtime

## Metadata
- doc_id: `TD-031`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-04-10`
- related_docs:
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)
  - [./TD-028-hazard-emitter-common-contract.md](./TD-028-hazard-emitter-common-contract.md)
  - [./TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [./TD-030-hazard-actor-hierarchy-and-stage-application.md](./TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TaskBoard/SESSION-20260409-01-hazard-actor-behavior-board.md](../TaskBoard/SESSION-20260409-01-hazard-actor-behavior-board.md)

> `TD-030`이 actor hierarchy와 stage application을 닫은 이후, `HazardActor`의 실제 행동 계층을 `Presence + PatternSelector + Emitter execution seam` 기준으로 확장하기 위한 작업 기준 문서.

## 1. 문제 정의
- 현재 runtime은 `HazardActor`를 hierarchy와 activation gate의 상위 owner로만 인식한다.
- 그러나 actor의 플레이어 경험은 아직 기존 emitter compatibility path에 머물러 있다.
  - `PresenceState`는 actor activation truth에 결합됐고 room-entry activation seed도 시작됐지만, 상위 presentation bridge와 selector-driven behavior는 아직 없다.
  - `PatternSelector`는 invalid sentinel 상태만 가짐
  - emitter는 사실상 단일 pattern always-cycle path를 유지
- 이 상태로 기능을 계속 추가하면, actor 도입의 목적이었던 "개체처럼 읽히는 위험 주체"가 다시 emitter 수준의 단순 발사 장치로 축소된다.

## 2. 목표/비목표
- 목표:
  - `HazardActor`의 행동 계층을 `Presence`, `PatternSelector`, `Emitter execution seam`으로 분리한다.
  - 청사진 시나리오를 포함할 수 있도록 actor의 존재 연출, 패턴 선택, 상태 강화 확장 축을 연다.
  - 현재 single-pattern emitter gameplay를 보존하면서 단계적으로 actor-driven behavior로 이행할 수 있는 실행 단위를 정의한다.
- 비목표:
  - 다중 emitter 동시 action contract 구현
  - motion/path follow 구현
  - selector weighted selection의 최종 수식 고정
  - pattern-slot authoring UI 완성
  - presence-driven art/VFX/SFX asset 명세 확정

## 3. 설계 배경과 청사진
### 3.1 발화점이 된 청사진
- 목표 시나리오와 일반화 범위는 [GD-016](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)를 기획 SSOT로 참조한다.
- 이 문서는 그 청사진을 runtime owner와 execution seam으로 번역하는 기술 문서다.

### 3.2 일반화된 목표
- 위 시나리오는 특정 상태 분기 예시일 뿐이다.
- actor behavior는 더 넓은 범위를 받아야 한다.
  - 특정 진행도 구간에만 출현해 단일 패턴만 반복
  - `Source` 정복 시 소멸
  - 지정 경로 순회
  - 플레이어 거리와 source 상태에 따른 가중치 기반 패턴 선택
- 즉 `HazardActor`는 플레이어 입장에서 "비공격 대상 몬스터처럼 읽히는 위험 개체"를 일반화한 구현 상위 개념이다.

## 4. 현재까지 닫힌 계약
### 4.1 Hierarchy / ownership
- `Source -> HazardActor -> HazardEmitter`
- hierarchy, binding, ref buffer, stage apply/reset owner는 [TD-030](./TD-030-hazard-actor-hierarchy-and-stage-application.md)를 SSOT로 유지한다.

### 4.2 Presence 최소 계약
- `PresenceState`
  - `Hidden`
  - `Activating`
  - `Active`
  - `Retiring`
- `HazardActorRuntimeBaselineComponent.InitialPresenceState`가 actor runtime reset 기준이다.

### 4.3 PatternSelector 최소 계약
- `PatternSelector`는 actor owner다.
- selector runtime state:
  - `TargetEmitterId`
  - `CurrentPatternSlotId`
  - `LastPatternSlotId`
  - `SelectionSequence`
- invalid sentinel:
  - `TargetEmitterId = -1`
  - `CurrentPatternSlotId = -1`
  - `LastPatternSlotId = -1`
  - `SelectionSequence = 0`
- selector는 first contract에서 emitter-slot `1쌍`만 선택한다.

### 4.4 PatternSet / emitter seam
- pattern data owner는 emitter다.
- runtime 표현은 `buffer of slot metadata + profile ref`를 사용한다.
- 최소 slot metadata:
  - `PatternSlotId`
  - `TelegraphProfileRefId`
  - `EmissionProfileRefId`
  - `BaseWeight`
  - `AvailabilityFlags`
- 현재 구현 상태:
  - `HB-2A`에서 `HazardEmitterPatternSlotBuffer`가 compatibility runtime layer로 추가됐다.
  - 현재 emitter마다 slot은 정확히 1개만 존재한다.
    - `PatternSlotId = 1`
    - `BaseWeight = 1`
    - `AvailabilityFlags = 0`
  - slot ref는 emitter의 final applied `TelegraphProfileRefId` / `EmissionProfileRefId`를 mirror한다.
  - 이 slot buffer는 별도 baseline/applied owner를 갖지 않고, bake/stage apply에서 emitter applied truth로부터 derived reseed된다.
  - selector writer와 emit-build seam cutover는 아직 구현되지 않았다.

### 4.5 Current compatibility boundary
- 현재 runtime은 actor-aware seed를 넘어, actor behavior의 첫 gate까지 결합된 상태다.
- 현재 activation truth에 포함되는 것은:
  - `HazardActorAppliedConfigComponent.IsEnabled`
  - `HazardActorAppliedConfigComponent.IsSuppressed`
  - `PresenceState == Active`
  - emitter applied config
  - existing source/player gate
- 아직 activation truth에 포함되지 않는 것은:
  - selector invalid sentinel state
  - pattern-slot execution seam
  - room-entry activation의 one-shot latch 정책
  - presence presentation bridge/asset schema

## 5. 이번 확장 범위의 핵심 축
### 5.1 Presence runtime
- actor의 존재 상태를 reset-only가 아니라 runtime progression state로 승격한다.
- 첫 설계 질문:
  - 어떤 owner가 `Hidden -> Activating -> Active -> Retiring`를 전이시키는가
  - activation gate와 presence gate를 어디서 결합하는가
- 현재 구현 상태:
  - `HazardActorPresenceSystem`이 presence progression owner다.
  - `HazardActorPresencePolicyComponent`
    - `ActivationTrigger`
    - `ActivationDurationSec`
    - `RetireTrigger`
    - `RetireDurationSec`
    를 사용해 `Hidden / Activating / Active / Retiring` 전이를 수행한다.
  - 현재 기본 seed는 `Immediate activation / no retire`다.
  - `HB-1C` 이후 `SourceOccupied` activation trigger가 추가됐고, room-entry seed는 `SourceDirectorPressureInputBuffer.InfluenceOccupancy`를 읽는다.
  - `HB-1B` 이후 `PresenceState != Active`는 actor activation truth를 차단한다.
  - actor `disabled/suppressed`는 presence system이 `Hidden`으로 clamp하고 selector invalid sentinel을 강제한다.
  - `HazardActorPresencePresentationSignalComponent`
    - `ActivationStarted`
    - `RetireStarted`
    를 통해 actor-level presentation hook이 ECS signal로 노출된다.

### 5.2 PatternSelector runtime
- actor가 실제로 emitter slot을 선택하도록 만든다.
- 첫 설계 질문:
  - selector가 언제 selection을 갱신하는가
  - `PresenceState == Active`와 어떤 순서로 결합하는가
  - selector 결과를 emitter execution이 어떤 seam으로 읽는가
- 현재 구현 상태:
  - `HB-2B`에서 `HazardActorPatternSelectorSystem`이 selector state의 첫 runtime writer로 추가됐다.
  - update order는 `HazardEmitterCoordinatorSystem` 이후, `HazardEmitterEmitBuildSystem` 이전이다.
  - 현재 deterministic selection policy:
    - actor `PresenceState == Active`
    - emitter coordinator `ActivationAllowed == 1`
    - emitter slot buffer non-empty
    를 만족하는 emitter 중 `EmitterId`가 가장 낮은 emitter를 선택한다.
  - 선택 slot은 그 emitter의 `PatternSlotId`가 가장 낮은 slot이다.
  - current compatibility layer에서는 사실상 `EmitterId lowest + PatternSlotId = 1`을 고른다.
  - actor가 `Active`지만 eligible emitter/slot이 없으면 `TargetEmitterId`와 `CurrentPatternSlotId`만 invalid로 비우고, `LastPatternSlotId`는 최근 valid 선택 이력으로 유지한다.
  - non-`Active` actor의 selector reset owner는 계속 `HazardActorPresenceSystem`이다.

### 5.3 Emitter execution seam
- `HazardEmitterEmitBuildSystem`은 여전히 emitter-owned execution owner로 유지한다.
- 다만 이후 actor selector가 고른 slot을 실제 telegraph/emit/recovery path로 lowering하는 seam이 필요하다.
- 첫 설계 질문:
  - 현재 applied telegraph/emission snapshot을 slot 실행과 어떻게 연결하는가
  - slot resolve는 emitter build owner가 수행하는가
- 현재 구현 상태:
  - `HB-2C`에서 `HazardEmitterEmitBuildSystem`이 selector-aware로 전환됐다.
  - emit-build는 이제 아래 조건을 모두 만족할 때만 emitter를 진행시킨다.
    - coordinator `ActivationAllowed == 1`
    - actor selector state 존재
    - selector `TargetEmitterId == emitter.EmitterId`
    - selector `CurrentPatternSlotId`가 emitter의 slot buffer 안에 실제로 존재
  - selector가 이 emitter를 가리키지 않거나 selected slot이 없으면, emitter lifecycle은 즉시 `Dormant + timer 0`으로 강제된다.
  - 다만 실제 execution payload는 아직 slot별 profile resolve를 하지 않는다.
    - selected slot은 execution eligibility contract로만 사용
    - 실제 emit은 계속 emitter applied `HazardEmitterTelegraphProfileComponent` / `HazardEmitterEmissionProfileComponent`를 직접 읽는다.

### 5.4 State escalation / encounter presentation
- 청사진을 달성하려면 actor에는 발사 전조와 별개의 상위 존재 연출이 필요하다.
- 예:
  - room-entry activation presentation
  - progress-threshold escalation presentation
- 현재 상태:
  - `HB-1C`에서 actor-level presentation hook은 ECS signal seam까지 구현됐다.
  - presentation asset schema와 bridge는 아직 닫지 않는다.

## 6. 작업 시작 전에 문서로 먼저 닫아야 할 항목
- `PresenceState` 실제 전이 owner와 update order
- `PatternSelector` 갱신 시점과 emitter-slot 전달 seam
- current compatibility path에서 actor behavior로 넘어가는 migration boundary
- 청사진 달성을 위한 최소 vertical slice 범위
  - 예: room-entry presence activation
  - 예: 두 pattern slot 중 선택
  - 예: progress threshold에서 pattern 강화

## 7. 작업 단위 초안
### 7.1 HB-1. Presence runtime owner
- `PresenceState`를 실제 runtime progression으로 승격
- current actor gate와의 결합 위치 설계
- 구현 상태:
  - `HB-1A. Presence runtime owner` 완료
  - `HB-1B. Presence gate integration` 완료
  - `HB-1C. Blueprint trigger seed` 완료

### 7.2 HB-2. PatternSet / selector runtime seam
- selector가 emitter-slot `1쌍`을 실제로 선택
- emitter execution이 selector 결과를 읽는 seam 도입
- 구현 상태:
  - `HB-2A. PatternSet compatibility data layer` 구현 완료
  - `HB-2B. PatternSelector runtime owner` 구현 완료
  - `HB-2C. Emit-build selector seam cutover` 구현 완료

### 7.3 HB-3. Blueprint vertical slice
- room-entry activation presentation
- two-pattern selection
- progress-threshold escalation

### 7.4 HB-4. Validation / sample update
- operational sample과 test-only verification path를 actor behavior 기준으로 확장

## 8. 검증 기준
- 문서 기준:
  - `TD-030`과 ownership 충돌 없음
  - `TD-028` emitter local contract와 역할 충돌 없음
- 구현 기준:
  - current actor-aware single-pattern path가 presence gate 통합 이후에도 유지된다
  - presence/selector 도입 후에도 compile, console error 0, EditMode, PlayMode smoke를 통과한다
- gameplay 기준:
  - actor가 플레이어 입장에서 "발사 장치"보다 "행동하는 위험 개체"로 읽히는 최소 vertical slice를 제공한다

## 9. 오픈 이슈
- selector가 slot을 매 frame 재평가할지, state transition 시점에만 갱신할지
- state escalation을 selector policy 변경으로 볼지, actor-level presentation + slot-set swap으로 볼지
- 이후 motion/path 축을 actor behavior TD에 포함할지, 별도 TD로 분리할지
