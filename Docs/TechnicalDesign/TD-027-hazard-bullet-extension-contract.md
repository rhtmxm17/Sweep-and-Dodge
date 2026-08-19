# Hazard Bullet Extension Contract

## Metadata
- doc_id: `TD-027`
- type: `TechnicalDesign`
- status: `superseded`
- last_updated: `2026-07-03`
- related_docs:
  - [TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)
  - [TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [TD-033-emission-profile-common-schema.md](./TD-033-emission-profile-common-schema.md)

> 이 문서는 Hazard bullet 확장과 lifecycle 반응을 별도 축으로 보던 초기 계약 문서다. 현재 구현 계약은 `EmissionProfileSO`와 공통 discrete emit 경로로 대체됐으며, 본 문서는 현재 runtime/authoring SSOT가 아니다.

## 1. 현재 기준 문서
- bullet gameplay tuning과 lifecycle-triggered emission: [TD-033](./TD-033-emission-profile-common-schema.md)
- discrete emit request ownership/execution: [TD-029](./TD-029-discrete-emit-spawn-bridge-contract.md)
- HazardActor behavior runtime: [TD-031](./TD-031-hazard-actor-behavior-runtime.md)

## 2. 현재 해석
- Hazard bullet behavior는 `EmissionProfileSO`와 HazardActor pattern slot을 통해 작성한다.
- 후속 emission은 profile 사이의 lifecycle trigger link로 표현한다.
- Source event, HazardActor, Triggered emission producer는 공통 discrete emit request 경로를 사용한다.
- `BulletDefinitionSO`는 bullet identity, prefab/visual identity, pool baseline, collision baseline, capture rule, score value, compatibility fallback movement data를 소유한다.

## 3. 운영 메모
- 새 구현 설명은 이 문서가 아니라 현재 기준 문서에 추가한다.
- 이 문서는 TD 번호와 과거 참조의 연결을 유지하기 위한 대체 안내 문서로만 둔다.
