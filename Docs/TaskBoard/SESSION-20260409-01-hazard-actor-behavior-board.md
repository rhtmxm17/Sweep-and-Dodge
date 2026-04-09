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
- actor behavior 확장을 위한 문서 범위 확인
- `PresenceState`, `PatternSelector`, `Emitter execution seam`의 다음 논의 순서 정리

## Next
- `PresenceState` runtime owner와 update order를 먼저 닫는다.
- 이어서 selector-emitter execution seam과 blueprint vertical slice 범위를 닫는다.

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

## End of Session
- 결과: 진행 중
- 다음 시작점:
  - `PresenceState` runtime 전이와 actor activation truth의 결합 범위를 먼저 논의한다.
