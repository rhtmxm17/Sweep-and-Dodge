# SESSION-20260324-01

## Metadata
- doc_id: `SESSION-20260324-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-26`
- related_docs:
  - [../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)
  - [../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
  - [../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](../ADR/ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md)

## Session Goal
- 한 줄 목표: 스테이지 layout SSOT를 `grid cell authoritative`로 전환하기 위한 실행 계획을 고정한다.
- 완료 기준: `explicit RegionId`, obstacle visual 분리, data schema/generator/runtime migration 순서가 문서로 확정된다.
- 이번 세션에서 하지 않을 것: runtime code 구현, tilemap brush 세부 UX 확정, visual auto-generation 세부 알고리즘 확정

## Now
- [x] P4. runtime movement / deposit 이관
  - 기준: runtime authoritative plane은 `xz`로 유지하고, movement/deposit 판정을 grid authority로 이관한다.
  - 산출물: `StageRuntimeGrid` cache, player movement block grid reader, deposit region request/consume 전환, sample asset/source bridge 보정
- [x] P4.1 bullet block owner 재정렬
  - 기준: `BulletObstacleHitRequestSystem`를 제거하고, bullet block owner를 `BulletSimulationSystem`으로 옮긴다.
  - 산출물: swept path broad phase seam, simulation owner `BlockBullet` full-cell consume, request 단계 bullet-wide full scan 제거
- [x] P4.2 player block swept semantics 정리
  - 기준: `PlayerObstacleBlockSystem`를 movement owner swept query로 전환하고, player tunneling을 blocked cell 기준으로 막는다.
  - 산출물: player `prevXZ -> nextXZ` swept query, shared `StageRuntimeBlockQuery` mask seam, deposit bounds-hit 유지 명시
- [x] 검증 및 회귀 정리
  - 검증 결과: P4 기준 compile error 0, console error 0, EditMode `269/269` pass, PlayMode `38/38` pass
  - 구현 메모: stage2 `9002` presentation은 deposit-link 대신 source-link로 정리했다.

## Next
- [ ] P7. terrain visual polish / follow-up
  - 기준: grid-only runtime/template cleanup 이후 남은 visual polish와 content-side 개선을 별도 단계에서 정리한다.
  - 산출물: terrain visual authoring polish, content debt 정리, 필요 시 importer 후속 논의

## Blocked
- 없음

## Inbox
- [x] I1. `SourceRegionId`, `DepositRegionId`는 auto-merge가 아니라 paint 시 명시 입력을 강제한다.
  - 검증 결과: stable id 의미와 diff/validation 명확성을 위해 explicit id가 더 적합하다는 결정이 고정됐다.
- [x] I2. obstacle visual은 기존 `Presentation` linked topology 규칙과 분리한다.
  - 검증 결과: obstacle은 movement authority로 흡수하고, visual은 read-only tilemap/presentation owner가 소비하는 구조로 정리됐다.
- [x] I3. P3 authoring 입력 모델은 `region id별 tile asset`이 아니라 `custom dense paint backing store + anchor marker`로 간다.
  - 검증 결과: explicit stable id paint를 유지하면서 region 수 증가에 따른 authoring 비용 폭증을 피하는 방향으로 고정됐다.
- [x] I4. P3 패키지 결론은 `com.unity.2d.tilemap` 필수, `com.unity.2d.tilemap.extras` 선택이다.
  - 검증 결과: 현재 manifest에는 tilemap module은 있지만 editor package는 없고, extras는 visual/auxiliary workflow에만 필요하다는 결론을 반영했다.

## Parking Lot
- [ ] P7. 외부 툴 importer(`LDtk`, `Tiled`)는 Unity Tilemap 경로가 안정화된 뒤 같은 grid schema로 추가 검토한다.
  - 근거: 지금 우선순위는 SSOT와 runtime owner 전환이지 authoring 툴 확장이 아니다.
- [ ] P8. obstacle visual auto-generation 세부 규칙은 gameplay migration 완료 후 별도 세션에서 정리한다.
  - 근거: 연결/코너/타일 선택 규칙은 visual polish 범위이며 현재 결정의 필수 선행 조건이 아니다.

## Done
- [x] D1. layout authority를 `grid cell`로 전환하기로 결정했다.
  - 검증 결과: tilemap 기반 stage design과 runtime query 모델을 같은 표현으로 맞추는 방향이 합의됐다.
- [x] D2. `Source / Deposit`는 explicit region id 기반 aggregate로 운영하기로 결정했다.
  - 검증 결과: `StageDefinitionSO.SourceBindings` key를 유지하면서 cell paint 기반 authoring을 수용할 수 있는 방향으로 고정됐다.
- [x] D3. obstacle gameplay authority와 obstacle visual을 분리하기로 결정했다.
  - 검증 결과: obstacle visual은 gameplay topology hard gate가 아니라 read-only consumer 계층으로 정리됐다.
- [x] D4. `TD-015`와 신규 ADR 초안을 작성했다.
  - 검증 결과: 문서 기준에서 shape-centric layout 규칙이 제거되고 grid-authoritative 방향이 명시됐다.
- [x] D5. 실행 플랜 TaskBoard를 작성했다.
  - 검증 결과: schema -> generator -> runtime movement/deposit -> source region -> visual 정리 순서가 세션 계획으로 고정됐다.
- [x] D6. `P2 StageLayoutSO grid schema + validation seam`을 구현했다.
  - 검증 결과: `SchemaVersion=2`, `Grid/Cells/SourceRegions/DepositRegions` 스키마, `StageGridLayoutValidationRules`, catalog source-region cross-validation, definition generator의 source-region 수집이 반영됐다.
  - 구현 메모: runtime/generator 전환 전까지 hidden legacy layout 필드는 compatibility bridge로 임시 유지한다.
- [x] D7. `P3 authoring generator`를 구현했다.
  - 검증 결과: `StageGridAuthoring`, `StageRegionAnchorMarker`, `StageMovementTile`, grid-only `StageLayoutCatalogGenerator`, sample authoring scene 구조, `com.unity.2d.tilemap` 의존, grid authoring/editor tests가 반영됐다.
  - 구현 메모: generator 출력은 legacy arrays를 비우지만, runtime migration 전까지 `sl_demo_*` 샘플 asset은 grid schema와 legacy compatibility bridge를 함께 유지한다.
- [x] D8. `P4 runtime movement / deposit migration` 구현을 시작했다.
  - 검증 결과: `StageRuntimeGridComponent`/buffer cache, source-only `StageTopologyApplyPrepareSystem`, grid 기반 `PlayerObstacleBlockSystem`, `PlayerCarryBinDeposit*`, canonical grid rotation validation, stage2 presentation fallback PlayMode 의존 제거가 코드 기준으로 반영됐다.
  - 구현 메모: bullet block owner는 후속 P4.1에서 request 단계 full scan 제거 기준으로 재정렬했다.
- [x] D9. `P4.1 bullet block owner realignment`를 반영했다.
  - 검증 기준: `BulletObstacleHitRequestSystem` 제거, `BulletSimulationSystem` owner 이동, `prevXZ -> nextXZ` swept path broad phase, `BlockBullet` full-cell narrow phase, 관련 EditMode/PlayMode 회귀 확인
- [x] D10. `P4.2 player block swept semantics`를 반영했다.
  - 검증 결과: EditMode `277/277` pass. PlayMode는 실행 중 Unity MCP 연결이 끊겨 최종 합격 여부를 회수하지 못했다.
  - 구현 메모: `PlayerObstacleBlockSystem`가 movement owner swept query로 `BlockPlayer`를 판정하고, deposit touch는 bounds-hit semantics를 유지한다.
- [x] D11. `P5 source region runtime migration`을 반영했다.
  - 검증 기준: `StageTopologyApplyPrepareSystem`가 `SourceRegions + Cells`로 source entity를 reconcile하고, source pressure/spawn/pollution runtime query가 region-derived local grid cache를 authoritative geometry로 사용한다.
  - 구현 메모: pressure source는 player center cell membership으로 결정한다.
- [x] D12. `P3 explicit authoring bounds + grid gizmo` 보정을 반영했다.
  - 검증 기준: `StageGridAuthoring`가 `BoundsMinCell/BoundsSize`를 authoring SSOT로 소유하고, tilemap `cellBounds`는 reference only로 내려갔다.
  - 구현 메모: `StageLayoutEditingSampleV1` 씬 상태를 authoring SSOT로 고정했고, Stage1은 `22x17 bounds / 50 source cells / 12 deposit cells`, Stage2/3는 `30+ source cells` sample로 layout 자산과 다시 동기화했다.
- [x] D13. runtime stage grid playmode gizmo를 추가했다.
  - 검증 기준: runtime `StageRuntimeGrid`와 source anchor ECS 데이터를 읽어 grid/movement/source/deposit/source-anchor gizmo를 PlayMode SceneView에서 표시한다.
  - 구현 메모: deposit anchor는 현재 runtime authority 방향이 열려 있으므로 gizmo 범위에서 제외했다.
- [x] D14. `P6 obstacle visual / metadata tilemap 재편`을 반영했다.
  - 검증 기준: obstacle visual을 presentation owner에서 분리하고, `MovementTilemap + RegionTilemap + Ground/Wall visual tilemap` 중심 authoring 경로를 도입했다.
  - 구현 메모: `StagePresentation`은 source/deposit/standalone만 owner로 유지하고, `StageRegionTile.RegionKind + RegionSlotIndex + slot mapping` 기반으로 metadata tilemap을 사용한다.
- [x] D15. `P6.next-A unified RegionTilemap authoring cleanup`을 반영했다.
  - 검증 기준: split tilemap과 `StageRegionPaintAsset` fallback을 제거하고, repo-tracked authoring을 unified `RegionTilemap` only 경로로 고정했다.
  - 구현 메모: sample scene/content의 paint 참조를 제거했고, generator/validation/editor/tests를 unified-only 계약으로 정리했다.
- [x] D16. `P6.next-B obstacle-linked presentation removal`을 반영했다.
  - 검증 기준: `StageObstacleMarker`를 sample scene과 editor authoring 경로에서 제거하고, presentation/editor validation은 source/deposit/standalone만 지원한다.
- [x] D17. `P6.next-C runtime/template legacy path cleanup`을 반영했다.
  - 검증 기준: `StageLayoutSO` hidden compatibility field, `StageSourceMarker / StageDepositMarker`, `StageLayoutValidationRules`, obstacle/deposit topology template를 제거하고 source-only runtime/template 계약으로 정리했다.
  - 구현 메모: `StagePresentationLinkKind`는 `None / Source / Deposit`만 남기고, legacy numeric 값은 grid layout validation에서 unsupported error로 막는다.

## End of Session
- 결과: movement/deposit/source runtime query가 모두 grid authority 기준으로 정리됐다.
- 남은 리스크: source region local-grid semantics는 bounds-based rectangle cache를 사용하므로, 추후 irregular-region density/presentation 세부 보정이 필요할 수 있다.
- 다음 세션 시작점: terrain visual polish 또는 importer/content follow-up 중 우선순위가 높은 쪽으로 이어간다.
