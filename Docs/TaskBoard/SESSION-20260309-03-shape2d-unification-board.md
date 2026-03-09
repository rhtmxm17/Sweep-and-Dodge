# SESSION-20260309-03

## Metadata
- doc_id: `SESSION-20260309-03`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-09`
- related_docs:
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)

## Session Goal
- 한 줄 목표: `Source / Deposit / Obstacle`의 shape 계약을 `Shape2DComponent + Source 전용 파생 번들` 방향으로 재정렬한다.
- 완료 기준: shape 공통 contract, `Source` 파생 책임 경계, yaw-only 가드레일, `Deposit` shape 확장, 구현/검증 반영을 마감한다.
- 이번 세션에서 하지 않을 것: obstacle broadphase 최적화, 세부 성능 튜닝

## Now
- [ ] T5. 문서와 검증을 현재 구현 상태로 마감한다.
  - 목적: TD/ADR/TaskBoard와 테스트 상태를 현재 `Shape2D` 계약과 맞춘다.
  - 완료 기준: TD/ADR 반영, console error 0, EditMode/PlayMode 검증 결과 정리
  - 검증: Unity compile, EditMode, PlayMode smoke
  - 근거: 이번 변경은 런타임/에디터/데이터 스키마를 함께 바꿔서 코드와 문서가 같이 맞아야 한다.

## Next
- [ ] 없음

## Blocked
- 없음

## Inbox
- [ ] I1. `Source Rectangle`은 현재 gizmo는 회전을 보지만 런타임 판정/샘플링은 회전을 보지 않는 불일치가 있다.
  - 근거: `StageSourceMarker`, `StageTopologyApplyPrepareSystem`, `RunProgressDirectorSystems`, `SpawnRequestSystems` 확인
- [x] I2. `Deposit` shape 확장을 이번 범위에 포함했다.
  - 근거: `StageDepositLayoutData`, `DepositPointComponent`, `PlayerCarryBinDepositSystem`을 `Shape2DComponent` 기반으로 이관했다.

## Parking Lot
- [ ] P2. shape 공통화 이후 obstacle broadphase / persistent cache 최적화를 별도 세션으로 분리한다.
  - 근거: 현재 논의 범위는 shape contract와 owner 정리이며, 성능 최적화는 별도 측정과 검증이 필요하다.

## Done
- [x] D1. `Shape2DComponent + Source 전용 파생 번들` 방향을 채택했다.
  - 검증 결과: `Source`의 파생 책임은 공통 shape raw data와 분리해 owner가 함께 관리해야 한다는 합의를 정리했다.
- [x] D2. 회전 semantics를 `모든 3D 회전 반영`이 아니라 `XZ 판정 + yaw만 반영`으로 정리했다.
  - 검증 결과: `yaw`만 gameplay 의미로 사용하고 `pitch/roll`은 무시 또는 경고하는 방향으로 정리했다.
- [x] D3. Stage Layout editor guardrail에 marker GO의 `yaw-only` 강제를 추가하기로 합의했다.
  - 검증 결과: Stage Layout 생성 툴과 marker authoring 경로에서 `pitch/roll`을 허용하지 않는 가드레일이 필요하다는 점을 확정했다.
- [x] D4. `Source / Deposit / Obstacle` runtime data를 `Shape2DComponent` 중심으로 이관했다.
  - 검증 결과: `DepositPointComponent`, `ObstacleGeometryComponent`, `BulletFieldAreaComponent`는 semantic marker로 유지하고 raw shape는 `Shape2DComponent`로 통일했다.
- [x] D5. `Source` 전용 파생 번들을 도입했다.
  - 검증 결과: `SourceShapeDerivedComponent`가 `ComputedArea`, `HalfExtents`를 담당하고 source owner가 pollution grid 재구축과 함께 관리하도록 정리했다.
- [x] D6. Stage Layout / marker / generator를 `YawDeg + Shape/Radius/Size` 스키마로 정리했다.
  - 검증 결과: source/deposit/obstacle marker가 `pitch/roll`을 0으로 강제하고, layout 생성도 `yaw-only`로 저장한다.

## End of Session
- 결과: `Shape2DComponent + SourceShapeDerivedComponent + yaw-only` 계약을 코드와 테스트 기준으로 반영했다.
- 남은 리스크: edit/play smoke 결과와 source pollution grid 동작 회귀를 마지막으로 확인해야 한다.
- 다음 세션 시작점: 검증 결과를 보고 obstacle broadphase 최적화 또는 추가 shape 회귀 테스트를 분리한다.
