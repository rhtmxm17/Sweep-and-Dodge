# SESSION-20260318-01

## Metadata
- doc_id: `SESSION-20260318-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-18`
- related_docs:
  - [../TechnicalDesign/TD-024-stageplay-intervention-dialogue-contract.md](../TechnicalDesign/TD-024-stageplay-intervention-dialogue-contract.md)
  - [../TechnicalDesign/TD-022-in-world-dialogue-runtime-contract.md](../TechnicalDesign/TD-022-in-world-dialogue-runtime-contract.md)
  - [../TechnicalDesign/TD-020-hint-notification-runtime-contract.md](../TechnicalDesign/TD-020-hint-notification-runtime-contract.md)
  - [../TechnicalDesign/TD-023-common-gameplay-pause-contract.md](../TechnicalDesign/TD-023-common-gameplay-pause-contract.md)

## Session Goal
- 한 줄 목표: `StagePlay` 도중 특수 조건에서 끼어드는 개입형 인월드 대화 범위를 설계한다.

## Now
- `P4 자동 검증` 범위 점검

## Next
- intervention sample authoring을 포함한 targeted PlayMode 검증 확장 여부 점검

## Blocked
- 없음

## Parking Lot
- [ ] P1. `LowTime`, `SourceDepleted`, `HazardHigh`는 후속 세션에서 우선순위와 개입 가치 재평가 후 추가한다.
- [ ] P2. intervention을 pause caller로 승격할지는 별도 플레이테스트와 `TD-023` caller 정책 정리 이후 결정한다.

## Done
- [x] D1. `StagePlayInterventionBridge` 분리 방향과 `DemoShellDialogueBridge` playback 재사용 방향을 고정했다.
- [x] D2. 첫 범위를 `InterventionCarryFull`, `InterventionFirstHit` 2개로 축소했다.
- [x] D3. intervention은 `OverlayOnly`, no-queue, active dialogue 시 drop 정책으로 정리했다.
- [x] D4. `TD-024` 초안을 추가하고 인덱스에 반영했다.
- [x] D5. `P1 trigger/state 확장`을 구현 범위로 고정했다.
  - 검증 기준: intervention trigger enum 추가, current-run seen-state 도입, `EnterStagePlay` run-state 초기화, intervention target/blocking validation 확장.
- [x] D6. `P2 StagePlayInterventionBridge`를 구현했다.
  - `StagePlayInterventionBridge`가 `StagePlay/Running` 중 `FirstHit`, `CarryFull` intervention을 판정한다.
  - `FirstHit`은 feedback snapshot version edge + `PlayerHazardHit` type으로만 시작한다.
  - `CarryFull`은 current-run seen-state를 읽고, 완료/skip 시 `DemoShellDialogueBridge`가 run seen을 기록한다.
- [x] D7. `P3 UI / suppress 회귀`를 구현했다.
  - `Stage 1` sample intervention entry(`CarryFull`, `FirstHit`)를 demo catalog에 추가했다.
  - 운영 씬 PlayMode와 EditMode에서 intervention active 중 `DialoguePanel` 표시, `NotificationPanel`/`HintPanel` suppress, `StageHudPanel` 유지, `OverlayOnly` dim 비표시, 종료 후 visibility 복구를 검증했다.
  - `StageStart` gate active 중 `FirstHit` edge가 들어와도 intervention이 현재 presentation을 교체하지 않는 회귀를 추가했다.

## End of Session
- 결과: `TD-024 P3` 기준으로 `Stage 1` intervention sample authoring과 UI/suppress 회귀 검증이 들어갔다. 운영 씬에서 `CarryFull`, `FirstHit` intervention이 실제로 노출되고, 기존 dialogue suppress 계약이 유지되는 상태다.
- 남은 리스크: cooldown authoring 여부, 후속 trigger 확장 범위, pause caller 승격 여부는 여전히 열려 있다.
- 다음 세션 시작점: `P4 자동 검증` 범위로 확장할지, 또는 추가 intervention trigger(`LowTime`, `SourceDepleted`, `HazardHigh`) 설계로 넘어갈지 결정한다.
