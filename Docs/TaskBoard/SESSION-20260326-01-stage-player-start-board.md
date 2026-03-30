# SESSION-20260326-01

## Metadata
- doc_id: `SESSION-20260326-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-26`
- related_docs:
  - [../TechnicalDesign/TD-025-stage-player-start-position-contract.md](../TechnicalDesign/TD-025-stage-player-start-position-contract.md)
  - [../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md](../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md)
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260326-01-stage-player-start-owned-by-layout-and-prepare-owner.md](../ADR/ADR-20260326-01-stage-player-start-owned-by-layout-and-prepare-owner.md)

## Session Goal
- 한 줄 목표: 스테이지별 플레이어 시작 위치를 layout SSOT와 prepare owner 기준으로 붙일 실행 계획을 고정한다.
- 완료 기준: data schema, authoring seam, runtime owner, 검증 범위가 구현 가능한 수준으로 문서에 고정된다.
- 이번 세션에서 하지 않을 것: 코드 구현, sample scene 실제 보정, PlayMode 검증

## Now
- [x] D1. 시작 위치 데이터를 `StageLayoutSO` 소유로 두는 방향을 채택했다.
  - 검증 결과: spatial data를 definition이 아니라 layout이 소유해야 기존 dual catalog 경계와 맞는다는 판단을 문서화했다.
- [x] D2. stage entry spatial apply owner를 prepare 계층으로 분리하기로 결정했다.
  - 검증 결과: `StageTopologyApplyPrepareSystem` publish + `PlayerStageEntryApplyPrepareSystem` write의 2단계 ownership을 채택했다.
- [x] D3. TD / ADR / 작업 보드 초안을 작성했다.
  - 검증 결과: 구현 전 SSOT 문서와 작업 분해가 생겼다.

## Next
- [ ] P1. data/authoring seam 구현
  - 기준: `StageLayoutSO.PlayerStart`, `StagePlayerStartMarker`, generator/validation 추가
  - 산출물: layout schema 반영, authoring validation, editor tests
- [ ] P2. runtime prepare seam 구현
  - 기준: stage entry 시 player spatial state를 prepare owner가 단일 writer로 적용
  - 산출물: runtime singleton, apply bookkeeping, player spatial sync tests
- [ ] P3. sample/content 반영
  - 기준: sample scene과 `sl_demo_*`, `sc_demo`가 새 schema를 사용한다.
  - 산출물: authoring scene marker 배치, asset 재생성, sample validation
- [ ] P4. 검증
  - 기준: compile / console / EditMode / PlayMode smoke
  - 산출물: stage별 시작 위치 회귀 확인

## Blocked
- 없음

## Parking Lot
- [ ] vertical spawn(`HeightY`) 필요 여부는 첫 구현 이후 다시 판단한다.
- [ ] 시작 셀 위 `Source/Deposit`를 warning에서 error로 올릴지는 content 시범 운영 후 결정한다.

## End of Session
- 결과: 구현 전에 ownership, update order, data schema, validation seam이 문서 기준으로 고정됐다.
- 남은 리스크: player start를 grid-relative `XZ + yaw`로 제한했기 때문에 vertical spawn 요구가 생기면 schema 확장이 필요하다.
- 다음 세션 시작점: `P1 data/authoring seam 구현`
