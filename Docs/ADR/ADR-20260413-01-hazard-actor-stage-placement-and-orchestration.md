# ADR-20260413-01-hazard-actor-stage-placement-and-orchestration
> stage를 actor placement/orchestration owner로 고정하고, actor를 재사용 가능한 archetype content unit으로 재해석한 결정

## Metadata
- status: 합의됨 (문서 반영)
- related_docs:
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../TaskBoard/SESSION-20260413-01-hazard-actor-stage-placement-board.md](../TaskBoard/SESSION-20260413-01-hazard-actor-stage-placement-board.md)
  - [ADR-20260408-01-hazard-actor-intermediate-hierarchy.md](ADR-20260408-01-hazard-actor-intermediate-hierarchy.md)

## 배경
- `HA-1` cutover 이후 stage actor 입력은 `StageSourceBinding.HazardActors[]`의 `HazardActorBinding` 중첩 구조였다.
- 이 구조는 actor roster on/off와 emitter override 중심으로 설계되어 아래 요구를 자연스럽게 수용하지 못했다.
  - 같은 actor archetype을 여러 stage/source에서 재사용
  - 같은 stage 안에서 같은 archetype의 여러 instance를 서로 다른 규칙으로 운용
  - source prefab duplication 없이 stage variation 구성
- 이 상태에서 계속 기능을 추가하면 actor archetype이 stage마다 복제돼 재사용 가능성이 사라진다.

## 결정
1. **actor는 재사용 가능한 archetype content unit이다.** stage는 actor behavior SSOT가 아니라 placement/orchestration SSOT다.
2. **placement와 orchestration은 분리된 schema로 본다.**
   - placement: 어떤 archetype을 어느 source에 attach하는지 + source local offset 같은 배치성 값. 정적 데이터.
   - orchestration: 배치된 instance에 대해 언제 `Spawn / PhaseSet / Retire` request를 발행하는지. 동적 content layer.
3. **PlacementInstanceId로 인스턴스를 식별한다.** stage 전체에서 유일한 식별자로 본다. orchestration target은 actor entity가 아니라 `PlacementInstanceId`다.
4. **orchestration first-pass action set**: `Spawn / PhaseSet / Retire`. 각 action은 request semantic이다.
   - `Spawn`: actor presence flow의 등장 시작 request
   - `PhaseSet`: actor phase flow의 target phase 전환 request
   - `Retire`: actor presence flow의 퇴장 시작 request
5. **trigger first-pass**: `OnStageStart / OnSourceProgressAtOrAbove`. cross-source orchestration은 first-pass 범위 밖이다.
6. **one-shot rule**: 같은 stage run에서 같은 rule은 한 번만 발화한다. request 발행 시점에만 consumed로 본다.
7. **fired-state owner는 source-owned runtime이다.** `RuleId + HasFired` 수준의 rule별 buffer로 시작한다.
8. **runtime ownership은 계속 source-owned hierarchy를 유지한다.** source apply/reset/teardown이 attach된 actor hierarchy lifecycle을 소유한다.
9. **`HazardActorBinding` stage path를 제거한다(SP-4 direct cutover).** 현재 stage actor 입력은 `HazardActorPlacements + HazardActorOrchestrationRules`만 사용한다.

## 대안
- **대안 1 — actor self-orchestration**: actor가 자신의 등장/퇴장/phase 전환 조건을 내부적으로 소유한다.
  - 단점: 같은 archetype을 서로 다른 stage에서 다른 orchestration 규칙으로 재사용할 수 없다. stage별 variation이 필요할 때마다 archetype을 복제해야 한다.
- **대안 2 — stage를 actor 직접 configurator로 유지 (`HazardActorBinding` 확장)**: 기존 구조를 확장해 orchestration rule도 actor binding 안에 내포한다.
  - 단점: 같은 archetype instance 2개를 같은 stage에서 서로 다른 규칙으로 운용하는 경우를 binding 구조로 표현할 수 없다. archetype 재사용이 source duplication 없이 불가능하다.

## 결과
- stage actor delivery/orchestration SSOT가 `HazardActorPlacements + HazardActorOrchestrationRules`로 전환됐다.
- legacy `HazardActorBinding` path와 source child actor scan path가 제거됐다.
- source apply 시 placement instance마다 actor archetype hierarchy를 source-owned runtime에 pre-attach한다.
- source는 placement instance resolve를 위해 `SourceHazardActorPlacementRefBuffer`(`PlacementInstanceId + ActorEntity`)를 가진다.
- `HazardActorOrchestrationSystem`이 source 단위로 rule을 평가하고 request를 발행하는 owner다.
- stage orchestration system은 trigger를 평가하고 request만 발행하며, 실제 상태 전이 수행은 actor runtime owner가 담당한다.
- 같은 frame에 같은 instance에 여러 rule이 eligible이면 declaration order로 직렬화하고 instance당 frame당 최대 1개 request만 발행한다.

## 후속
- `SourceHazardActorRefBuffer` 축소/제거 조건은 placement resolve seam 안정화 이후 별도 점검한다.
- group targeting 도입 시점은 별도 논의로 미룬다.
- lookup indirection(catalog key vs direct prefab reference) 전환 시점은 별도 논의로 미룬다.
