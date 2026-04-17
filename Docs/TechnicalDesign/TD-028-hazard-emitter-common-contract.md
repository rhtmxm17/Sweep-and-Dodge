# HazardEmitter Common Contract

## Metadata
- doc_id: `TD-028`
- type: `TechnicalDesign`
- status: `superseded`
- last_updated: `2026-04-17`
- related_docs:
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md](../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md)
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [./TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [./TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)

> 이 문서는 `HazardEmitter`를 독립 런타임 엔티티로 보던 초기 공통 계약 기록이다. 현재 구현 SSOT는 `TD-029`, `TD-031`, `ADR-20260417-01`이며, `HazardEmitter`는 gameplay-facing 용어와 profile asset 이름으로만 유지된다.

## 1. 현재 해석
- 현재 runtime owner는 `HazardActor`다.
- 아래 런타임 데이터는 모두 actor가 직접 소유한다.
  - `PatternSlot`
  - slot execution snapshot
  - emit lifecycle state
  - active telegraph snapshot
  - active emission snapshot
  - cycle completion signal
- `HazardEmitter` 런타임 엔티티, coordinator, emit-build system 경로는 제거됐다.

## 2. 이 문서가 여전히 의미 있는 범위
- gameplay-facing 문서에서 `HazardEmitter`를 위험 발화점 용어로 사용하는 맥락
- `HazardEmitterTelegraphProfileSO`, `HazardEmitterEmissionProfileSO` 자산 이름이 유지된다는 점
- discrete emit 경계에서 "직접 spawn이 아니라 emit request producer였다"는 역사적 배경

## 3. 더 이상 현재 계약이 아닌 항목
- `HazardEmitterComponent` 중심 structural identity
- emitter baseline/applied/runtime layering
- `HazardEmitterCoordinatorSystem`
- `HazardEmitterEmitBuildSystem`
- `HazardEmitterBinding`, emitter-local stage override, emitter reset/apply 순서
- `Source -> HazardActor -> HazardEmitter`를 현재 runtime hierarchy로 보는 설명

## 4. 현재 SSOT
- actor-owned emit/runtime contract: [TD-031](./TD-031-hazard-actor-behavior-runtime.md)
- direct emit bridge/request contract: [TD-029](./TD-029-discrete-emit-spawn-bridge-contract.md)
- actor delivery/placement/orchestration: [TD-032](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
- direct emit ownership 결정 기록: [ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)

## 5. 운영 메모
- 새 설계나 구현 설명에서 `HazardEmitter`를 독립 runtime entity처럼 다루지 않는다.
- gameplay-facing 설명이 필요할 때는 `HazardEmitter`를 유지할 수 있지만, 기술문서에서는 actor-owned runtime이라는 점을 함께 명시한다.
