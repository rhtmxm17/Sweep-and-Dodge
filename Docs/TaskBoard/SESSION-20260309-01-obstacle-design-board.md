# SESSION-20260309-01

## Metadata
- doc_id: `SESSION-20260309-01`
- type: `SessionTaskBoard`
- status: `draft`
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
- [ ] T1. `StageObstacleLayoutData`와 obstacle runtime entity 계약을 확정한다.
  - 목적: `Obstacle`를 `StageTopology`에 정식 편입할 최소 데이터/컴포넌트 구성을 고정한다.
  - 완료 기준: layout 필드와 runtime component 세트가 합의된다.
  - 검증: 대화 기준으로 schema와 component 구성이 명시된다.
  - 근거: 의미 계약은 확정됐고, 다음 단계는 topology apply 가능한 데이터/런타임 계약 정리다.

## Next
- [ ] T2. obstacle topology apply owner와 lifecycle 규칙을 정리한다.
  - 완료 기준: `StageTopologyPrepareGroup`에서의 instantiate/reuse/disable-to-pool 규칙과 failure policy가 obstacle에 대해 설명된다.
  - 검증: apply owner와 lifecycle 계약이 기존 topology 규칙과 연결되어 설명된다.
  - 근거: obstacle는 `Source/Deposit` 다음 topology kind로 편입될 후보다.
- [ ] T3. obstacle runtime read 경계를 bullet / player로 나눠 정리한다.
  - 완료 기준: bullet은 이번 세션 범위에서 기본 반응과 read 경로가 정리되고, player는 데이터 계약만 남기고 실행 owner는 보류 상태로 정리된다.
  - 검증: reader와 deferred boundary가 명시된다.
  - 근거: `BlockBullet -> despawn`은 확정됐고, player blocking 최종 owner는 아직 보류다.

## Blocked
- 없음

## Inbox
- 없음

## Parking Lot
- [ ] P1. `PlayerIntentMovementSystem`을 fixed-tick 쪽으로 이관한다.
  - 근거: 플레이어 이동 차단을 gameplay 판정으로 다루려면 fixed-tick 파이프라인 정렬과 replay 일관성 검토가 함께 필요하다.
- [ ] P2. `Visual GO-only` 설계를 별도 세션에서 다룬다.
  - 근거: `Visual`은 시뮬레이션 영향이 없는 presentational layer로 분리 논의하는 것이 적절하고, 이번 세션은 `Obstacle`만 다룬다.

## Done
- [x] D1. 장애물 의미 계약을 확정했다.
  - 검증 결과: `단일 Obstacle + CollisionMask`, `BlockBullet` 접촉 탄환 기본 반응 `즉시 despawn`, `BlockPlayer|BlockBullet` / `BlockPlayer` 의미가 합의되었다.

## End of Session
- 결과:
- 남은 리스크:
- 다음 세션 시작점:
