# HazardActor Stage Placement and Orchestration Framework

## Metadata
- doc_id: `TD-032`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-17`
- related_docs:
  - [../ADR/ADR-20260413-01-hazard-actor-stage-placement-and-orchestration.md](../ADR/ADR-20260413-01-hazard-actor-stage-placement-and-orchestration.md)
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [./TD-030-hazard-actor-hierarchy-and-stage-application.md](./TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [./TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [./TD-034-stage-map-editor-replacement.md](./TD-034-stage-map-editor-replacement.md)
  - [./TD-035-hazard-actor-authoring-workbench-and-preview.md](./TD-035-hazard-actor-authoring-workbench-and-preview.md)

> stage는 actor archetype의 placement/orchestration owner이고, runtime lifecycle owner는 여전히 source다. 현재 delivery/reset/cleanup seam은 actor-only 기준으로 닫혀 있다.

## 1. 현재 역할 분리
- actor archetype이 소유하는 것:
  - phase 집합
  - selector policy
  - actor-owned pattern slot content
  - emit baseline
- stage placement가 소유하는 것:
  - `PlacementInstanceId`
  - `ActorArchetypePrefab`
  - `LocalOffset`
  - source attach 대상
- stage orchestration이 소유하는 것:
  - `Spawn`
  - `PhaseSet`
  - `Retire`
  - trigger evaluation / one-shot fired state
- authoring 시점에는 `StageMapDocument`가 placement와 source-local orchestration rule의 SSOT다.
- runtime에서는 document export 결과인 `StageDefinitionSO`와 source runtime buffer가 기존 실행 소유권을 유지한다.

## 2. Delivery / Resolve Seam
- source apply 시 placement instance마다 actor archetype runtime을 attach한다.
- current resolve seam:
  - `SourceHazardActorPlacementRefBuffer`
    - `PlacementInstanceId`
    - `ActorEntity`
  - `SourceHazardActorRefBuffer`
    - `ActorEntity`
    - `ActorId`
- orchestration target은 actor entity 직접 참조가 아니라 `PlacementInstanceId`다.

## 3. Lifecycle Ownership
- source는 attach된 actor hierarchy의 lifecycle owner다.
- source reset/reapply/teardown은 actor attach만 관리한다.
- stage topology cleanup은 actor entity만 수집/삭제한다.
- emitter entity lifecycle 설명은 현재 계약에서 제거됐다.

## 4. Current Runtime Contract
- placement attach helper는 actor archetype prefab을 standalone actor root로 해석한다.
- actor attach 시 아래 runtime이 함께 seed된다.
  - selector policy/candidate
  - pattern slot metadata / execution snapshot
  - emit runtime baseline
  - orchestration request signal / consumption state
- source-owned orchestration rule baseline과 fired state는 기존처럼 source runtime buffer에 남는다.

## 5. Reset / Teardown Notes
- `StageTopologyApplyPrepareSystem`은 source reset 시 actor selector/phase/presence뿐 아니라 actor emit runtime도 baseline으로 돌린다.
- `StageTopologyBridge`와 source cleanup 경로는 placement actor attachment만 정리한다.
- 문서상 더 이상 emitter attachment/cleanup 단계는 없다.

## 6. 검증 기준
- active 기술문서 기준으로 placement/orchestration 문맥에서 emitter entity attach/cleanup 설명이 남아 있지 않아야 한다.
- `PlacementInstanceId -> ActorEntity` resolve seam이 현재 stage actor targeting seam으로 유지돼야 한다.
- editor authoring/migration/preview는 [TD-035](./TD-035-hazard-actor-authoring-workbench-and-preview.md)를 따르며 runtime resolve/lifecycle 계약을 변경하지 않아야 한다.
