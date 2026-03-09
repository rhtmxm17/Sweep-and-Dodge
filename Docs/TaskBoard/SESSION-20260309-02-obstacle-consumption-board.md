# SESSION-20260309-02

## Metadata
- doc_id: `SESSION-20260309-02`
- type: `SessionTaskBoard`
- status: `completed`
- last_updated: `2026-03-09`
- related_docs:
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)

## Session Goal
- 한 줄 목표: obstacle 소비 시스템의 bullet/player 경계를 구현한다.
- 완료 기준: bullet은 `Request`, player는 `PlayerFixedStepGroup`에서 obstacle를 소비하고, 기본 회귀 테스트가 통과한다.

## Done
- [x] bullet obstacle 소비를 `BulletObstacleHitRequestSystem`으로 구현했다.
  - `BulletRequestGroup`, `BulletVacuumRequestSystem` 이후, `PlayerHazardCollisionRequestSystem` 이전
  - bullet은 `point`, obstacle는 `Circle/Box`
  - `BlockBullet` hit 시 기존 `BulletDespawnRequestTag`를 enable
- [x] player obstacle 소비를 `PlayerPreviousPositionCaptureSystem` + `PlayerObstacleBlockSystem`으로 구현했다.
  - `PlayerFixedStepGroup`
  - `PlayerPreviousPositionCaptureSystem -> PlayerIntentMovementSystem -> PlayerObstacleBlockSystem -> PlayerIntentConsumeSystem`
  - player는 `circle(PlayerRadius)`, correction은 `rollback + axis slide`
- [x] 문서에 현재 방식이 `post-move correction`이며 향후 `movement-resolve 통합` 가능성이 있음을 반영했다.

## Parking Lot
- [ ] obstacle query broadphase / persistent cache
- [ ] obstacle bullet/player 판정의 고급 연속 충돌/모서리 처리
- [ ] 이동 영향 요소 확장 시 `movement-resolve 통합` 재설계 검토
