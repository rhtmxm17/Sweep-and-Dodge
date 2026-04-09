# SESSION-20260409-01

## Metadata
- doc_id: `SESSION-20260409-01`
- type: `SessionTaskBoard`
- status: `in_progress`
- last_updated: `2026-04-09`
- related_docs:
  - [./SESSION-20260408-01-hazard-actor-design-board.md](./SESSION-20260408-01-hazard-actor-design-board.md)
  - [../TechnicalDesign/TD-028-hazard-emitter-common-contract.md](../TechnicalDesign/TD-028-hazard-emitter-common-contract.md)
  - [../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md](../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)

## Session Goal
- 한 줄 목표: `HazardActor`를 실제로 행동하는 위험 개체로 확장하기 위해, `Presence + PatternSelector + Emitter execution seam`의 구현 범위를 문서와 작업 단위 기준으로 닫는다.
- 완료 기준:
  - current actor-aware compatibility path에서 actor behavior runtime으로 넘어가는 경계가 문서 기준으로 단일 해석 가능하다.
  - 청사진 vertical slice의 최소 구현 범위가 분해돼 있다.
  - 구현 착수를 위한 다음 plan 단위가 정리돼 있다.

## Inherited Context
- `TD-030` 기준 `Source -> HazardActor -> HazardEmitter` hierarchy, binding, authoring/baker, stage apply/reset은 구현 완료 상태다.
- current runtime은 actor-aware compatibility까지만 완료됐다.
  - actor applied config는 activation truth에 포함
  - `PresenceState`와 selector state는 아직 reset-only/informational
- `HazardEmitter`는 여전히 single-pattern compatibility path를 유지한다.

## Now
- `HB-1A. Presence runtime owner` 완료
- 다음 구현 단위는 `HB-1B. Presence gate integration`

## Next
- `HB-1B. Presence gate integration` 실행 플랜을 수립한다.
- 이후 `HB-1C. Blueprint trigger seed`로 room-entry activation seed를 붙인다.

## Blocked
- 없음

## Parking Lot
- [ ] P1. multi-emitter coordinated action contract를 언제 여는지
- [ ] P2. actor-level motion/path를 같은 TD에 넣을지 분리할지
- [ ] P3. state escalation을 presence 축으로 볼지 selector/pattern-set swap 축으로 볼지

## Done
- [x] D1. 행동 확장 범위를 hierarchy/apply 완료 문서(`TD-030`)와 분리해 별도 TD(`TD-031`)로 시작했다.
  - 이유: `TD-030`은 계층/적용 ownership SSOT로 닫혀 있어야 하고, behavior 확장은 별도 설계 축으로 관리하는 편이 안전하다.
- [x] D2. 이번 세션의 운영 보드를 별도로 생성했다.
  - 이유: 이전 actor 세션은 migration closeout까지 완료된 상태라, behavior 확장 논의를 같은 보드에 누적하면 완료 범위와 신규 범위가 섞이게 된다.
- [x] D3. 목표 청사진 시나리오를 별도 `GD-016`으로 분리하고, `GD-015`의 `HazardActor` 용어 보정을 반영했다.
  - 이유: 행동 확장 출발점은 player-facing blueprint이므로 기획 문서 기준의 별도 기록이 필요하고, `GD-015`도 구현 상위 개념과의 관계가 명시돼야 이후 TD와 용어가 어긋나지 않는다.
- [x] D4. `HB-1` presence 확장은 단일 구현 플랜이 아니라 3개 실행 단위로 분리하기로 고정했다.
  - `HB-1A. Presence runtime owner`
  - `HB-1B. Presence gate integration`
  - `HB-1C. Blueprint trigger seed`
  - 이유: presence owner 도입, runtime activation truth 변경, room-entry activation seed는 서로 다른 위험도를 가지며, 특히 room-entry activation은 청사진 vertical slice 성격이 강해 별도 단계로 다루는 편이 안전하다.
- [x] D5. `HB-1A. Presence runtime owner` 구현을 완료했다.
  - `HazardActorPresencePolicyComponent`와 `HazardActorPresenceSystem`이 추가됐다.
  - actor presence는 이제 reset-only가 아니라 실제 runtime progression state로 동작한다.
  - 기본 policy는 `Immediate activation / no retire`로 seed되고, current emitter compatibility gate는 유지된다.
  - 검증 결과: console blocking error 없음, EditMode `485/485`, PlayMode `45/45`.

## End of Session
- 결과: 진행 중
- 다음 시작점:
  - `PresenceState`를 activation truth에 결합하는 `HB-1B` 범위를 먼저 논의한다.
