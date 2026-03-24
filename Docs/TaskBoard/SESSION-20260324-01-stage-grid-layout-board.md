# SESSION-20260324-01

## Metadata
- doc_id: `SESSION-20260324-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-24`
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
- [ ] P3. authoring generator 도입
  - 기준: Unity Tilemap metadata authoring -> `StageLayoutSO`
  - 산출물: `StageGridLayoutGenerator`, sample authoring scene 경로, explicit region paint guardrail

## Next
- [ ] P3. authoring generator 도입
  - 기준: Unity Tilemap metadata authoring -> `StageLayoutSO`
  - 산출물: `StageGridLayoutGenerator`, sample authoring scene 경로, explicit region paint guardrail
- [ ] P4. runtime movement / deposit 이관
  - 기준: obstacle/player/bullet/deposit query가 grid authority를 읽는다.
  - 산출물: prepare cache build, movement/deposit reader 전환, 회귀 테스트
- [ ] P5. source region runtime 이관
  - 기준: source sampling, pollution, progress가 region cell 집합을 읽는다.
  - 산출물: source runtime geometry 전환, definition binding 연결, 회귀 테스트
- [ ] P6. obstacle visual / legacy path 정리
  - 기준: obstacle visual은 gameplay authority와 분리된 tilemap/presentation owner가 처리한다.
  - 산출물: visual rebuild 경로, legacy obstacle marker 제거 계획, 최종 migration 점검

## Blocked
- 없음

## Inbox
- [x] I1. `SourceRegionId`, `DepositRegionId`는 auto-merge가 아니라 paint 시 명시 입력을 강제한다.
  - 검증 결과: stable id 의미와 diff/validation 명확성을 위해 explicit id가 더 적합하다는 결정이 고정됐다.
- [x] I2. obstacle visual은 기존 `Presentation` linked topology 규칙과 분리한다.
  - 검증 결과: obstacle은 movement authority로 흡수하고, visual은 read-only tilemap/presentation owner가 소비하는 구조로 정리됐다.

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

## End of Session
- 결과: `TD-015`, 신규 ADR, 이 TaskBoard를 기준으로 stage layout 전환의 실행 계약이 고정됐다.
- 남은 리스크: PlayMode 운영 씬에서 stage topology apply timing 경고가 반복되며, 현재 P2 변경과 직접 무관한 `StagePlay`/topology 경계 회귀 가능성을 추적해야 한다. source runtime geometry 이관 범위도 여전히 넓다.
- 다음 세션 시작점: `StageGridLayoutGenerator`와 sample authoring scene 경로를 구현하고, hidden legacy bridge 제거 전까지 generator/runtime 전환 순서를 정리한다.
