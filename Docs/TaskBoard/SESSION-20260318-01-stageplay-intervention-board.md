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
- `P3 UI / suppress 회귀` 범위 점검

## Next
- intervention active 동안 기존 dialogue suppress 규칙과 hint/notification 회귀 확인

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

## End of Session
- 결과: `TD-024 P2` 기준의 runtime intervention owner와 `DemoShellDialogueBridge` 시작 seam이 연결됐다. 현재 sample asset에는 intervention entry를 넣지 않았으므로 운영 플레이에서의 노출은 아직 발생하지 않는다.
- 남은 리스크: cooldown authoring 여부, 후속 trigger 확장 범위, pause caller 승격 여부, sample intervention authoring은 아직 열려 있다.
- 다음 세션 시작점: `P3 UI / suppress 회귀`를 점검하고, 필요 시 intervention sample authoring과 targeted PlayMode 검증을 이어간다.
