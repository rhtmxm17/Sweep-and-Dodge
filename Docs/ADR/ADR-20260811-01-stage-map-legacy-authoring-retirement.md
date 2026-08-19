# ADR-20260811-01-stage-map-legacy-authoring-retirement
> Demo Stage 2·3의 단발성 migration 완료 후 Scene/Tilemap/Marker 기반 Stage authoring을 제거하는 결정

## Metadata
- doc_id: `ADR-20260811-01`
- type: `ArchitectureDecisionRecord`
- status: `accepted`
- date: `2026-08-11`
- supersedes:
  - `ADR-20260804-01`의 legacy import/debug/backend 유지 결정
- related_docs:
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)

## 배경
- `StageMapDocument` 기반 편집기가 공식 authoring surface로 자리 잡았고 Stage 1은 이미 document SSOT로 전환됐다.
- 남은 Stage 2·3 migration 이후에는 Scene/Tilemap/Marker importer를 다시 사용할 계획이 없다.
- 사용하지 않을 migration 경로를 영구 validation/schema/test로 유지하면 공식 편집 경계가 다시 두 갈래로 보이고 유지보수 비용만 남는다.

## 결정
- Stage 2·3은 임시 one-shot runner로 legacy scene과 기존 `StageDefinitionSO` orchestration을 document에 편입한다.
- migration equivalence는 action-aware semantic equivalence로 판정한다.
  - `PhaseSet.TargetPhaseId`는 정확히 보존한다.
  - runtime에서 소비하지 않는 `Spawn/Retire.TargetPhaseId`는 canonical value로 정규화할 수 있다.
  - 그 밖의 grid, identity, pose, rule action/trigger/threshold/target/order 차이는 허용하지 않는다.
- migration과 runtime smoke가 통과한 뒤 Scene/Tilemap/Marker authoring scene, 사용자 진입점, 전용 구현·asset·test를 제거한다.
- 이후 지원되는 stage authoring 경로는 `StageMapDocument -> Validate/Dry Run -> Diff -> Apply` 하나뿐이다.
- `StageMapDocument` 자체의 schema migration은 legacy scene import와 다른 책임이므로 유지한다.
- 제거된 legacy 자료는 live debug path로 보관하지 않으며 필요 시 version control history에서 조회한다.

## 대안
- legacy import를 영구 hardening하고 유지한다.
  - 재사용 가능성은 생기지만 사용 계획이 없는 이중 authoring 경로와 test matrix가 남아 기각한다.
- legacy 사용자 진입점만 숨기고 구현과 sample scene을 보관한다.
  - 복구는 빠르지만 숨은 의존과 오래된 콘텐츠 oracle이 계속 남아 기각한다.
- 기존 UI를 수동으로 순서대로 실행한다.
  - 임시 코드가 필요 없지만 orchestration import 전 Apply 실수를 구조적으로 막지 못하므로 기각한다.

## 결과
- 신규 stage 제작과 수정의 SSOT 및 사용자 진입점이 `StageMapDocument`로 단일화된다.
- migration 전용 guard/test는 최종 코드에 남지 않는다.
- Stage 2의 PlayerStart/SourceRegion overlap은 기존 runtime smoke가 통과한 의도된 warning으로 승인한다.
- 외부 importer가 필요해지면 제거된 Tilemap/Marker 경로를 복구하지 않고 별도 기능으로 설계한다.
