# SESSION-20260413-01

## Metadata
- doc_id: `SESSION-20260413-01`
- type: `SessionTaskBoard`
- status: `completed`
- last_updated: `2026-04-14`
- related_docs:
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)
  - [./SESSION-20260409-01-hazard-actor-behavior-board.md](./SESSION-20260409-01-hazard-actor-behavior-board.md)

## Session Goal
- 한 줄 목표: `HazardActor`를 stage-driven content unit으로 재해석하기 위해, actor archetype / placement / orchestration 프레임의 문서 초안과 후속 설계 분해 기준을 닫는다.
- 완료 기준:
  - `TD-032`가 용어/책임/대표 시나리오/비범위를 단일 해석 가능하게 정리한다.
  - 기존 `TD-030`, `TD-031`과의 역할 경계가 충돌하지 않는다.
  - 후속 구현 계획을 `SP-1..SP-4`로 분리할 수 있을 정도의 중간 수준 설계가 정리돼 있다.

## Inherited Context
- `TD-030` 기준 `Source -> HazardActor -> HazardEmitter` hierarchy, source-owned apply/reset, ref buffer contract는 구현 완료 상태다.
- `TD-031` 기준 actor behavior runtime은 `presence + phase-aware selector + escalation staging`까지 구현/검증을 닫았다.
- current `StageDefinitionSO.HazardActorBinding`은 actor roster on/off와 emitter override 중심이라, stage별 actor archetype placement/orchestration을 표현하는 장기 프레임으로는 부족하다.

## Now
- 없음

## Next
- 후속 설계가 필요하면 아래를 별도 세션에서 다룬다.
  - `SourceHazardActorRefBuffer` 축소/제거 조건
  - group targeting 도입 시점
  - lookup indirection(catalog key) 필요성

## Blocked
- 없음

## Parking Lot
- [ ] P1. direct prefab reference와 catalog key/id 중 어떤 lookup 방식을 first-pass에 채택할지
- [ ] P2. orchestration targeting에 group 개념을 언제 도입할지
- [ ] P3. `Spawn / PhaseSet / Retire` rule schema를 통합할지 타입별로 나눌지
- [ ] P4. motion/path, presentation bridge 같은 후속 축을 어느 TD로 분리할지

## Done
- [x] D1. `HB-3` closeout 이후 후속 범위는 기존 behavior board를 연장하지 않고 새 세션 보드로 분리하기로 고정했다.
- [x] D2. 이번 주제는 `TD-030`이나 `TD-031` 확장이 아니라 새 TD(`TD-032`)로 관리하기로 고정했다.
- [x] D3. 현재까지의 설계 합의를 문서 기준으로 정리했다.
  - actor는 재사용 가능한 archetype content unit이다.
  - stage는 actor instance placement/orchestration owner다.
  - runtime ownership은 source-owned hierarchy를 유지한다.
  - 등장/phase 전환/소멸은 장기적으로 placement script/orchestration 소관이다.
- [x] D4. 중간 수준 설계 고정점을 추가했다.
  - placement와 orchestration은 분리된 schema로 본다.
  - orchestration의 first-pass target은 instance-only다.
  - `PhaseSet`은 2-phase 전용이 아니며, archetype이 정의한 임의의 valid phase를 대상으로 한다.
  - first-pass action set은 `Spawn / PhaseSet / Retire`다.
  - first-pass trigger 모델은 `OnStageStart / OnSourceProgressAtOrAbove`다.
  - first-pass orchestration rule은 one-shot request semantic이다.
- [x] D5. `SP-1`부터 `SP-3`까지의 중간 수준 설계 결정을 문서에 고정했다.
  - first-pass delivery는 direct prefab reference + source-owned pre-attach다.
  - placement 최소 정보는 `PlacementInstanceId`, `ActorArchetypeRef`, `SourceStableId`, `LocalOffset`이다.
  - `PlacementInstanceId`는 stage-global unique이며 placement transform authority는 `LocalOffset` 하나다.
  - source는 별도 placement ref buffer로 `PlacementInstanceId -> ActorEntity` resolve seam을 가진다.
  - actor root는 unified versioned orchestration request signal과 owner별 last-consumed version을 가진다.
  - source-owned orchestration runtime은 `RuleId + HasFired` fired-state buffer로 시작한다.
- [x] D6. `SP-1`부터 `SP-4`까지 direct cutover를 구현했다.
  - source template prefab의 actor child baseline을 제거하고 standalone actor archetype prefab으로 분리했다.
  - stage asset은 `HazardActorPlacements + HazardActorOrchestrationRules`만 사용하도록 전환됐다.
  - legacy `HazardActorBinding` stage path와 source child actor scan path는 제거됐다.
  - placement/orchestration validation, sample asset, apply/reset owner가 새 frame 기준으로 정리됐다.
  - sample verification과 operational smoke는 새 placement/orchestration frame 기준으로 통과한다.
  - full EditMode/PlayMode suite의 잔여 실패는 `MCP-FOR-UNITY NetworkStream disposed` noise뿐이며, project code failure는 없다.

## End of Session
- 결과: 완료
- 다음 시작점:
  - placement/orchestration frame 위에서 후속 확장 요구를 별도 세션으로 다룬다.
