# ADR-20260415-01-orchestration-rule-under-source-binding
> `StageDefinitionSO` 최상위 flat 배열이었던 `HazardActorOrchestrationRules`를 `StageSourceBinding` 하위로 이동하고, `PlacementInstanceId` / `RuleId` 유일성 범위를 source-local로 좁힌 결정

## Metadata
- status: 합의됨 (구현 예정)
- related_docs:
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [ADR-20260413-01-hazard-actor-stage-placement-and-orchestration.md](ADR-20260413-01-hazard-actor-stage-placement-and-orchestration.md)

## 배경
- `ADR-20260413-01` 이후 stage actor 입력은 `SourceBindings[].HazardActorPlacements + StageDefinitionSO.HazardActorOrchestrationRules`였다.
- placement는 source-scoped(`SourceBinding` 하위)인 반면, orchestration rule은 stage 최상위 flat 배열로 분리돼 있었다.
- 이 구조는 아래 불일치를 만들었다.
  - rule의 `OnSourceProgressAtOrAbove` trigger는 rule이 속한 source의 progress truth를 읽는다. 그러나 authoring 레이아웃에서 rule은 source를 명시적으로 참조하지 않고 `PlacementInstanceId`로 간접 귀속됐다.
  - apply 시점(`SeedSourceHazardActorOrchestration`)에서 stage 전체 rule 배열을 순회하며 `ownedPlacementIds.Contains()` 필터링을 수행했다. runtime은 이미 source-scoped였으나 authoring 레이아웃이 source를 몰랐다.
  - validation(`ValidateHazardActorOrchestrationRules`)이 모든 source에 걸친 `placementDataById` 딕셔너리로 rule-placement 연결을 검증했다.
- 이 게임의 구조는 Source별로 분리된 형태(source-separated)이며, rule과 그 trigger semantics는 모두 source scope에 속한다.

## 결정
1. `HazardActorOrchestrationRuleBinding[] HazardActorOrchestrationRules`를 `StageDefinitionSO` 최상위에서 제거하고 `StageSourceBinding` 하위로 이동한다.
2. **`PlacementInstanceId` 유일성 범위를 source-local로 좁힌다.** stage-global uniqueness를 요구하는 first-pass use case가 없고, runtime resolve도 source 소유 `SourceHazardActorPlacementRefBuffer` 안에서만 수행된다.
3. **`RuleId` 유일성 범위를 source-local로 좁힌다.** fired-state buffer도 source-owned이므로 rule 식별도 source scope 안에서 충분하다.
4. apply 경로(`SeedSourceHazardActorOrchestration`)는 source binding의 rules를 직접 읽으며, 더 이상 `ownedPlacementIds` 필터링이 필요하지 않다.
5. validation은 각 source binding 루프 안에서 source-local `placementDataById`를 기준으로 rule을 검증한다.
6. cross-source orchestration은 first-pass 범위에서 계속 배제한다. SourceBinding 하위 구조는 이 제약을 schema 수준에서 구조적으로 강제한다.

## 대안
- **대안 1 — stage 최상위 flat 배열 유지 (현 상태)**: rule에 `SourceStableId` 필드를 명시 추가해 소속 source를 나타낸다.
  - 단점: authoring 시 rule마다 source를 중복 지정해야 하며, placement와 rule의 source가 불일치하는 authoring 오류가 발생할 수 있다. apply 시점 필터링이 계속 필요하다.
- **대안 2 — stage 최상위 flat 배열 유지, SourceStableId 없이 PlacementInstanceId cross-source 조회**: 현 구조 유지.
  - 단점: trigger semantics(`OnSourceProgressAtOrAbove`)와 authoring 레이아웃이 불일치한 채로 유지된다. 플레이어가 어느 source의 progress를 읽는지 authoring에서 명확하지 않다.

## 결과
- authoring inspector에서 source 단위로 "배치 + 오케스트레이션 규칙"이 함께 표시된다.
- apply 경로에서 `ownedPlacementIds` 필터 루프가 제거된다.
- validation이 source-local 범위로 단순화되며, cross-source rule-placement 불일치가 schema 수준에서 불가능해진다.
- `PlacementInstanceId`와 `RuleId`는 source-local unique contract로 좁혀진다.

## 후속
- `TD-032` §8.1 및 §8.5의 `PlacementInstanceId stage-global unique` 서술을 source-local unique로 갱신한다.
- `SourceHazardActorRefBuffer` 축소/제거 조건 검토는 이 변경과 독립적으로 유지한다.
