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
- cooldown 설계 검토 대기

## Next
- cooldown authoring 여부와 shape 정리

## Blocked
- 없음

## Parking Lot
- [x] P1. `LowTime`, `SourceDepleted`, `HazardHigh`는 경로 예약으로 정리했고, 실제 필요 전까지 작업 비대상으로 둔다.
- [x] P2. `ThemeTransition`, `Global`, `Stage 2` 이상 intervention authoring은 경로 예약으로만 유지한다.

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
- [x] D8. `P4 자동 검증`을 구현했다.
  - `CarryFull` same-run 비재생과 `FirstHit` same-session 비재생을 자동 검증으로 추가했다.
  - `StageClear` gate active 중 intervention 비개시, pause menu open 중 intervention 비개시를 EditMode/PlayMode에서 잠갔다.
  - intervention active 동안 simulation/gameplay clock 정지, pause menu 비허용, 종료 후 resume를 운영 씬 기준으로 검증했다.
- [x] D9. `ThemeTransition`, `Global`, `Stage 2` 이상 intervention authoring, 후속 trigger는 경로 예약으로 정리했다.
  - 실제 필요가 생기기 전까지는 작업 비대상으로 유지한다.

## End of Session
- 결과: `TD-024 P4` 기준으로 `CarryFull`, `FirstHit`의 주요 acceptance가 자동 검증으로 잠겼다. 운영 씬 기준으로 same-run/session 재노출 방지, gate 비개입, pause menu 비개시, intervention pause/resume가 회귀 테스트로 고정된 상태다.
- 남은 리스크: cooldown authoring 여부와 적용 방식은 여전히 열려 있다.
- 다음 세션 시작점: cooldown 정책과 authoring shape 세분화로 넘어간다.
