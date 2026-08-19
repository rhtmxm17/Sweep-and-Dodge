# Spawn Directive Settings Reference

## Metadata
- doc_id: `TD-005`
- type: `TechnicalDesign`
- status: `superseded`
- last_updated: `2026-07-03`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-033-emission-profile-common-schema.md](./TD-033-emission-profile-common-schema.md)

> 이 문서는 Wave directive가 공통 탄막 문법을 직접 소유하던 시기의 설정 레퍼런스다. 현재 Wave directive 설정은 Source wrapper 필드와 `EmissionProfileSO`로 분리됐으며, 본 문서는 현재 authoring SSOT가 아니다.

## 1. 현재 기준 문서
- Wave directive authoring model: [TD-003](./TD-003-spawn-directive-model.md)
- Wave runtime request/consume contract: [TD-002](./TD-002-pattern-wave-progress-runtime-contract.md)
- common profile grammar: [TD-033](./TD-033-emission-profile-common-schema.md)

## 2. 현재 해석
- `WaveSpawnEntryAuthoring.Profile`은 필수 common grammar reference다.
- Source event generation과 sampling은 Wave directive에 남아 있다.
- Bullet payload, spawn tuning, movement tuning, position pattern, aim, shot pattern, lifecycle trigger는 `EmissionProfileSO`가 소유한다.

## 3. 운영 메모
- 새 구현 설명은 이 문서가 아니라 현재 기준 문서에 추가한다.
- 이 문서는 TD 번호와 과거 참조의 연결을 유지하기 위한 대체 안내 문서로만 둔다.
