# SESSION-20260409-01

## Metadata
- doc_id: `SESSION-20260409-01`
- type: `SessionTaskBoard`
- status: `in_progress`
- last_updated: `2026-04-11`
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
- current runtime은 actor-aware presence seed까지 완료됐다.
  - actor applied config와 `PresenceState`는 activation truth에 포함
  - room-entry activation seed는 `SourceDirectorPressureInputBuffer.InfluenceOccupancy` 기반으로 시작됐다.
  - selector state는 아직 invalid sentinel cleanup/invariant만 가진다.
- `HazardEmitter`는 여전히 single-pattern compatibility path를 유지한다.

## Now
- `HB-2B. PatternSelector runtime owner` 구현 및 Unity MCP 검증 재시도 완료
- 다음 구현 단위는 `HB-2C. Emit-build selector seam cutover`

## Next
- `HB-2C. Emit-build selector seam cutover` 실행 플랜을 수립한다.

## Blocked
- Unity MCP `EditMode` 검증 시 MCP client exit 로그가 테스트 오라클에 걸려 일부 테스트가 실패함

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
- [x] D6. `HB-1B. Presence gate integration` 구현을 완료했다.
  - `PresenceState != Active`가 actor activation truth에 결합됐다.
  - actor `disabled/suppressed`는 presence system이 `Hidden`으로 clamp하고, non-`Active` actor는 selector invalid sentinel을 유지한다.
  - coordinator는 `ActorPresenceHidden`, `ActorPresenceActivating`, `ActorPresenceRetiring` reason을 사용해 차단한다.
  - 검증 결과: console blocking error 없음, EditMode `491/491`, PlayMode `45/45`.
- [x] D7. `HB-1C. Blueprint trigger seed` 구현을 완료했다.
  - `HazardActorPresenceTriggerMode.SourceOccupied`가 추가됐고, room-entry activation seed는 `SourceDirectorPressureInputBuffer.InfluenceOccupancy`를 읽는다.
  - `HazardActorAuthoring`는 이제 presence policy 전체를 노출하고, authoring/factory는 그 값을 runtime policy로 seed한다.
  - `HazardActorPresencePresentationSignalComponent`가 추가돼 actor-level activation/retire 시작을 ECS signal로 관측할 수 있다.
  - 검증 결과: console blocking error 없음, EditMode `497/497`, PlayMode `45/45`.
- [x] D8. `HB-2` 범위는 단일 구현 플랜이 아니라 3개 실행 단위로 분리하기로 고정했다.
  - `HB-2A. PatternSet compatibility data layer`
  - `HB-2B. PatternSelector runtime owner`
  - `HB-2C. Emit-build selector seam cutover`
  - 이유: 현재 repo에는 selector state만 있고 실제 pattern data layer와 selector writer, emit-build seam이 모두 비어 있어, 한 단계에 묶으면 구현 중 결정이 다시 생길 가능성이 크다.
  - 운영 원칙:
    - `HB-2`에서는 multi-slot authoring을 열지 않는다.
    - `HB-2`에서는 weighted/random selection을 넣지 않는다.
    - `HB-2`의 첫 목적은 selector-emitter seam을 current single-pattern compatibility path 위에 성립시키는 것이다.
- [x] D9. `HB-2A. PatternSet compatibility data layer` 구현을 완료했다.
  - emitter-owned `HazardEmitterPatternSlotBuffer`가 추가됐다.
  - 현재는 emitter당 slot 1개만 유지하며, `PatternSlotId = 1`, `BaseWeight = 1`, `AvailabilityFlags = 0`을 사용한다.
  - slot ref는 emitter final applied `TelegraphProfileRefId` / `EmissionProfileRefId`를 mirror한다.
  - bake, fallback template factory, stage apply에서 compatibility slot reseed를 수행하도록 맞췄다.
  - 수동 runtime fixture와 stage-apply tests에도 slot buffer mirror assertion을 추가했다.
  - 검증 결과:
    - Unity MCP `refresh_unity(compile=request)` 요청 후 PlayMode `45/45 passed`
    - Unity MCP EditMode는 `MCP-FOR-UNITY` client exit 로그가 테스트 오라클에 걸려 실패
    - `read_console(error)`는 MCP client exit 로그와 기존 `SpawnBacklog` 테스트 로그 노이즈를 포함해 반환했다.
  - 해석:
    - gameplay regression과 PlayMode path는 회귀 없음
    - EditMode 실패는 현재 코드 contract가 아니라 MCP 로그 노이즈 영향으로 분리 기록한다.
- [x] D10. `HB-2B. PatternSelector runtime owner` 구현을 완료했다.
  - `HazardActorPatternSelectorSystem`이 추가됐고, selector state의 첫 runtime writer가 됐다.
  - 현재 deterministic policy는 `PresenceState == Active`, `ActivationAllowed == 1`, slot buffer non-empty를 만족하는 emitter 중 `EmitterId`가 가장 낮은 emitter를 고르는 방식이다.
  - selected pair가 바뀔 때만 `SelectionSequence`를 증가시키고, no-eligible 상태에서는 current만 invalid로 비우고 `LastPatternSlotId`는 최근 valid 선택 이력으로 유지한다.
  - non-`Active` actor의 selector reset owner는 계속 `HazardActorPresenceSystem`으로 둔다.
  - 검증 결과:
    - Unity MCP `refresh_unity(compile=request)` 요청 후 PlayMode `45/45 passed`
    - Unity MCP EditMode는 다시 `MCP-FOR-UNITY` client exit 로그가 테스트 오라클에 걸려 실패
    - `read_console(error)`에도 동일한 MCP client exit 로그가 포함됐다.
  - 해석:
    - selector writer 도입 이후 gameplay regression은 관측되지 않았다.
    - EditMode 실패는 여전히 MCP 로그 노이즈로 분리 기록한다.

## End of Session
- 결과: 진행 중
- 다음 시작점:
  - `HB-2C. Emit-build selector seam cutover` 실행 플랜을 수립한다.
