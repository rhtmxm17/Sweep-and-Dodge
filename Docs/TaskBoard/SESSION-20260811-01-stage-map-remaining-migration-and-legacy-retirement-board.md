# SESSION-20260811-01

## Metadata
- doc_id: `SESSION-20260811-01`
- type: `SessionTaskBoard`
- status: `completed`
- last_updated: `2026-08-11`
- related_docs:
  - [../ADR/ADR-20260811-01-stage-map-legacy-authoring-retirement.md](../ADR/ADR-20260811-01-stage-map-legacy-authoring-retirement.md)
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)

## Session Goal
- Stage 2·3을 `StageMapDocument`로 semantic-equivalent migration한다.
- migration smoke 이후 Scene/Tilemap/Marker 기반 legacy authoring을 완전히 제거한다.
- runtime stage 계약과 `StageMapDocument` schema는 변경하지 않는다.

## Now
- 없음

## Next
- 없음

## Blocked
- 없음

## Done
- [x] T0. ADR-20260811-01을 채택하고 TD-010/015/025/034/035, GD-017의 현재 authoring 경계를 `StageMapDocument -> Dry Run/Diff/Apply`로 갱신했다.
- [x] T1. scoped backup, stale signature, rollback, finalize를 갖는 one-shot runner를 구현하고 사용 후 제거했다.
- [x] T2. Preflight를 확인했다.
  - Stage 2: legacy changes 8, orchestration rules 4, 승인된 `STG018` warning 1.
  - Stage 3: legacy changes 9, orchestration rules 2, warning 0.
  - 두 stage의 generated diff는 runtime에서 읽지 않는 non-PhaseSet `TargetPhaseId` canonicalization만 존재했다.
- [x] T3. `smd_demo_2`, `smd_demo_3`을 생성하고 post-validation/dry-run을 통과했다.
  - 모든 Hazard placement/source/rule을 Actor/Encounter Preview로 준비했다.
  - Stage 2: placements 2, sources 2, rules 4.
  - Stage 3: placements 2, sources 1, rules 2.
  - Source Progress 0/1 deterministic rebuild를 확인했다.
  - 지정 Stage 2·3 PlayMode smoke 2/2 pass.
- [x] T4. migration을 Finalize하고 sample scene, Tilemap/Marker types/assets, importer/generator/composer, legacy Inspector/preview/UI 진입점과 temporary runner를 제거했다.
- [x] T5. legacy fixture를 제거하고 synthetic `StageMapEditorWindowSmokeTests`와 permanent Document/Scene tool/Hazard/runtime behavior tests만 유지했다.
- [x] T6. 최종 검증을 완료했다.
  - targeted StageMap/Hazard/Catalog EditMode: 92/92 pass.
  - full EditMode: 514/514 pass.
  - full PlayMode: 46/46 pass.
  - Unity Console error 0.
  - 삭제 GUID 참조, legacy code/asset symbol, direct `com.unity.2d.tilemap` dependency, generated test residue 0.
