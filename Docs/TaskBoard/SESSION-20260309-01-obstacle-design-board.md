# SESSION-20260309-01

## Metadata
- doc_id: `SESSION-20260309-01`
- type: `SessionTaskBoard`
- status: `completed`
- last_updated: `2026-03-09`
- related_docs:
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)

## Session Goal
- 한 줄 목표: `Obstacle`의 데이터/런타임 계약을 확정한다.
- 완료 기준: `StageObstacleLayoutData`, obstacle runtime entity, topology apply / runtime read 경계를 합의한다.
- 이번 세션에서 하지 않을 것: `PlayerIntentMovementSystem`의 fixed-tick 이관 구현, `Visual GO-only` 설계

## Now
- 없음

## Next
- 없음

## Blocked
- 없음

## Inbox
- 없음

## Parking Lot
- [ ] P1. `PlayerIntentMovementSystem`을 fixed-tick 쪽으로 이관한다.
  - 근거: 플레이어 이동 차단을 gameplay 판정으로 다루려면 fixed-tick 파이프라인 정렬과 replay 일관성 검토가 함께 필요하다.
- [ ] P2. `Visual GO-only` 설계를 별도 세션에서 다룬다.
  - 근거: `Visual`은 시뮬레이션 영향이 없는 presentational layer로 분리 논의하는 것이 적절하고, 이번 세션은 `Obstacle`만 다룬다.
- [ ] P3. `Shape 범위 공통화`를 별도 세션에서 다룬다.
  - 근거: `Obstacle`는 이번 범위에서 `Circle/Box`로 고정하되, `Source` 등과의 공통 enum / helper 정리는 별도 구조 작업으로 다루는 편이 안전하다.

## Done
- [x] D1. 장애물 의미 계약을 확정했다.
  - 검증 결과: `단일 Obstacle + CollisionMask`, `BlockBullet` 접촉 탄환 기본 반응 `즉시 despawn`, `BlockPlayer|BlockBullet` / `BlockPlayer` 의미가 합의되었다.
- [x] D2. obstacle 스키마와 bullet read 기본 계약을 확정했다.
  - 검증 결과: `StageObstacleLayoutData`는 `StableId / Active / Position / EulerRotation / Shape / Radius / Size / CollisionMask`를 사용하고, runtime은 `StageTopologyObstacleTag`, `ObstacleStableIdComponent`, `ObstacleCollisionMaskComponent`, `ObstacleGeometryComponent`, `StageTopologyOwnedComponent(Kind=Obstacle)`, `LocalTransform` 기준으로 정리했다. bullet hit는 `point` 기준, active obstacle entity 직접 read, 기존 despawn request 재사용, 같은 tick 다중 remove 원인은 멱등 처리로 합의되었다.
- [x] D3. obstacle topology lifecycle / reader boundary를 확정했다.
  - 검증 결과: `Obstacle`는 `layout-only topology kind`로 정리했고, topology apply owner/lifecycle/failure policy는 기존 `StageTopology` 규칙을 따른다. bullet은 `Request` 단계 전용 `BulletObstacleHitRequestSystem`에서 읽고, player는 데이터 계약만 유지한 채 실행 owner를 보류하기로 합의되었다.

## End of Session
- 결과: `Obstacle`의 의미 계약, layout/runtime 스키마, topology apply/lifecycle 연결, bullet read 기본 계약을 확정했다. `Visual GO-only`와 `PlayerIntentMovementSystem` fixed-tick 이관은 이번 세션 범위에서 제외했다.
- 남은 리스크: obstacle query 최적화, bullet obstacle reader의 정확한 `Request` 내 세부 순서, player blocking 실행 owner/fixed-tick 정렬은 후속 설계가 필요하다.
- 다음 세션 시작점: `Obstacle`를 `StageTopologyPrepareGroup`과 `StageTopologyPrefabCatalogSO`에 실제 편입하는 구현 계획 또는 `PlayerIntentMovementSystem` fixed-tick 이관 설계로 이어간다.
