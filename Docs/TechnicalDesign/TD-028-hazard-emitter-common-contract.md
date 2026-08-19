# Hazard Emitter Common Contract

## Metadata
- doc_id: `TD-028`
- type: `TechnicalDesign`
- status: `superseded`
- last_updated: `2026-07-03`
- related_docs:
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [TD-033-emission-profile-common-schema.md](./TD-033-emission-profile-common-schema.md)

> 이 문서는 Hazard emission을 독립 runtime entity 중심으로 보던 초기 공통 계약 문서다. 현재 runtime ownership은 `HazardActor`에 있으며, 본 문서는 현재 구현 SSOT가 아니다.

## 1. 현재 기준 문서
- HazardActor behavior runtime: [TD-031](./TD-031-hazard-actor-behavior-runtime.md)
- HazardActor placement/orchestration: [TD-032](./TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
- direct emit bridge/request contract: [TD-029](./TD-029-discrete-emit-spawn-bridge-contract.md)
- common emission profile schema: [TD-033](./TD-033-emission-profile-common-schema.md)

## 2. 현재 해석
- Hazard emission은 actor-owned pattern slot runtime이 생성한다.
- Telegraph와 cooldown은 HazardActor slot 책임이다.
- Bullet payload, spawn tuning, movement tuning, shot grammar는 `EmissionProfileSO`가 제공한다.

## 3. 운영 메모
- 새 구현 설명은 이 문서가 아니라 현재 기준 문서에 추가한다.
- 이 문서는 TD 번호와 과거 참조의 연결을 유지하기 위한 대체 안내 문서로만 둔다.
