# SESSION-20260417-02

## Metadata
- doc_id: `SESSION-20260417-02`
- type: `SessionTaskBoard`
- status: `completed`
- last_updated: `2026-04-17`
- related_docs:
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [../TechnicalDesign/TD-028-hazard-emitter-common-contract.md](../TechnicalDesign/TD-028-hazard-emitter-common-contract.md)
  - [../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md](../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)

## Session Goal
- 한 줄 목표: `HazardEmitter` 런타임 엔티티 제거와 `HazardActor` 직접 발사 소유 전환을 기술문서 기준으로 단일 해석 가능하게 정리한다.
- 완료 기준:
  - 새 ADR 1건 작성
  - `TD-028/029/030/031/032`를 현재 구현 기준으로 정리
  - `TechnicalDesign / ADR` 인덱스 갱신
  - 링크와 금지 용어 잔존 여부 점검

## Inherited Context
- 코드와 테스트는 이미 actor direct emit 구조로 전환돼 있었다.
- 기존 기술문서는 `HazardEmitter` 독립 runtime entity, actor-emitter hierarchy, emitter-owned emit runtime을 전제로 작성된 부분이 남아 있었다.

## Now
- 없음

## Next
- 없음

## Parking Lot
- [ ] P1. `GD-015`, `GD-016`의 gameplay-facing 용어와 기술문서 연결 문구를 별도 세션에서 다듬기
  - 근거: 이번 세션은 기술문서 중심으로 제한했다.

## Done
- [x] D1. `ADR-20260417-01`을 추가해 actor direct emit ownership 결정을 기록했다.
  - 검증: 기존 `ADR-20260408-01`의 intermediate hierarchy를 부분 supersede 하는 후속 결정으로 연결했다.
- [x] D2. `TD-031`을 actor behavior/runtime의 현재 SSOT로 승격했다.
  - 검증: selector contract에서 `EmitterId`, `TargetEmitterId`를 제거하고 actor-owned emit runtime/update order를 명시했다.
- [x] D3. `TD-030`, `TD-032`를 actor-only hierarchy/apply/delivery 기준으로 갱신했다.
  - 검증: actor attach/reset/cleanup만 남기고 emitter entity lifecycle 설명을 제거했다.
- [x] D4. `TD-029`를 `HazardActorEmitSystem` producer 기준으로 갱신했다.
  - 검증: helper 이름과 actor transform + slot `LocalOffset` anchor 규칙을 반영했다.
- [x] D5. `TD-028`를 superseded 문서로 정리했다.
  - 검증: 현재 SSOT 링크와 historical note만 남기고 old emitter-runtime contract를 현재 계약처럼 두지 않았다.
- [x] D6. `Docs/TechnicalDesign/INDEX.md`, `Docs/ADR/INDEX.md`를 새 기준으로 갱신했다.
  - 검증: 새 ADR, active TD, superseded TD 요약을 인덱스에서 확인 가능하게 했다.

## End of Session
- 결과: 완료
- 남은 리스크: gameplay-facing GD 문서와 기술문서 간 용어 연결 문구는 후속 정리 여지가 있다.
- 다음 세션 시작점: `GD-015`, `GD-016`의 용어 보정 여부를 별도 범위로 판단한다.
