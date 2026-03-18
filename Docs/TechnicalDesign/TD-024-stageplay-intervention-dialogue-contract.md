# StagePlay 특수 조건 개입형 인월드 대화 계약 (TD-024)

## Metadata
- doc_id: `TD-024`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-03-18`
- related_docs:
  - [GD-011-in-world-dialogue-direction.md](../GameDesign/GD-011-in-world-dialogue-direction.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
  - [TD-020-hint-notification-runtime-contract.md](./TD-020-hint-notification-runtime-contract.md)
  - [TD-022-in-world-dialogue-runtime-contract.md](./TD-022-in-world-dialogue-runtime-contract.md)
  - [TD-023-common-gameplay-pause-contract.md](./TD-023-common-gameplay-pause-contract.md)

> `StagePlay` 도중 특수 조건에서 끼어드는 인월드 연출 대화는 `StagePlayInterventionBridge`가 조건 판정을 소유하고, `DemoShellDialogueBridge`가 playback을 재사용하는 구조로 운영한다. 첫 범위는 `CarryFull`, `FirstHit` 2종의 overlay intervention만 대상으로 한다.

## 1. 목표 / 비목표
### 1.1 목표
- 플레이 도중 특수 조건에서 짧게 끼어드는 연출 대화의 owner, 우선순위, 재노출 정책을 고정한다.
- 기존 `DemoShellDialogueBridge`, `RuntimeUiRoot`, `InWorldDialoguePresenter`를 재사용해 구현 비용을 낮춘다.
- `Hint/Notification`과의 역할 충돌 없이, 상황 개입형 대화를 별도 계층으로 확장한다.
- 현재 `StageStart`, `StageClear` 전환형 dialogue와 충돌하지 않는 계약을 정의한다.

### 1.2 비목표
- shell-context dialogue(`ThemeTransition`)까지 같은 pause contract로 일반화하는 구조 변경
- queue/stack 기반 cut-in 스케줄러
- 튜토리얼 전체를 대화 시스템으로 치환하는 구조 변경
- 카메라 컷, 타임스케일 제어, 별도 cinematic screen
- 이번 범위에서 `LowTime`, `SourceDepleted`, `HazardHigh`까지 구현하는 확장

## 2. 범위 / 사용자 경험 기준
### 2.1 첫 구현 범위
- `InterventionCarryFull`
- `InterventionFirstHit`

### 2.2 후속 확장 후보
- `InterventionLowTime`
- `InterventionSourceDepleted`
- `InterventionHazardHigh`

### 2.3 개입 강도 기본값
- 모든 stage-play intervention은 `OverlayOnly`로 시작한다.
- active dialogue가 이미 있으면 intervention은 대기하지 않고 drop한다.
- 동일 프레임에 여러 조건이 동시에 충족되면 1개만 재생한다.
- intervention active 동안 기존 dialogue suppress 규칙을 그대로 재사용한다.

## 3. 소유권 (Owner / Reader)
### 3.1 조건 판정 owner
- `StagePlayInterventionBridge`
  - `DemoShellFlowController`, `PlayerRuntimeHudBridge`, 필요 시 `DemoShellHintBridge`를 read-only로 조회한다.
  - intervention trigger 판정, 우선순위 충돌 해소, run/session seen-state 소비를 소유한다.
  - playable intervention을 찾으면 `DemoShellDialogueBridge.TryStartStagePlayIntervention(trigger, stageId)`에 시작을 요청한다.

### 3.2 playback owner
- `DemoShellDialogueBridge`
  - 대화 line 진행, skip, auto-advance, anchor/presentation state를 계속 소유한다.
  - stage-play intervention도 기존 sequence 재생 경로를 재사용한다.
  - stage-play intervention도 다른 `StagePlay` dialogue와 동일하게 `GameplayPauseReasonId.DialogueGate`를 획득한다.

### 3.3 durable state owner
- `DemoShellSessionStaging`
  - session 범위 seen-state를 계속 소유한다.
  - stage-play intervention의 run 범위 seen-state도 같은 저장소에 확장한다.

### 3.4 reader
- `RuntimeUiRoot`
- `InWorldDialoguePresenter`
- `StagePresentationRuntimeController`
  - 기존 reader-only 계약을 유지한다.

## 4. Trigger / 판정 계약
### 4.1 Trigger 추가
- `InWorldDialogueTriggerId`
  - `InterventionCarryFull`
  - `InterventionFirstHit`

### 4.2 공통 시작 조건
- `DemoShell.CurrentScreen == StagePlay`
- `DemoShell.CurrentStagePlayPhase == Running`
- pause menu, result modal, settings modal이 열려 있지 않다.
- `DemoShellDialogueBridge.IsDialogueActive == false`
- shell 전환형 gate dialogue(`StageStart`, `StageClear`)가 active가 아니다.

### 4.3 개별 조건
- `InterventionCarryFull`
  - `CarryCapacity > 0`
  - `CarryLoad >= CarryCapacity`
  - 현재 run에서 아직 미노출
  - 기본 정책: `OncePerRun`
- `InterventionFirstHit`
- `PlayerUiFeedbackPresentationSnapshotComponent.Version`이 증가한 새 edge여야 한다.
- feedback type은 `PlayerHazardHit`여야 한다.
  - 기본 정책: `OncePerSession`

### 4.4 우선순위
1. `InterventionFirstHit`
2. `InterventionCarryFull`

- 같은 프레임에 둘 다 만족하면 우선순위가 높은 것만 재생한다.
- 이번 범위에서는 queue를 두지 않는다.

## 5. 데이터 구조 / authoring 계약
### 5.1 카탈로그 재사용
- `InWorldDialogueCatalogSO`를 그대로 사용한다.
- intervention entry도 기존 `EntryKey`, `Trigger`, `TargetKind`, `BlockingMode`, `RetryPolicy`, `FullVariant`, `RetryVariant` 구조를 재사용한다.

### 5.2 target 제한
- 첫 범위에서는 `Stage`, `Global`만 허용한다.
- `Theme` target은 intervention 범위에서 사용하지 않는다.

### 5.3 조건 필드 정책
- 첫 구현에서는 별도 authoring 조건 필드를 추가하지 않는다.
- `CarryFull`, `FirstHit` 판정식은 `StagePlayInterventionBridge` 코드에 고정한다.
- 조건식이 안정화되면 후속 세션에서 `MinStageElapsedSec`, `CooldownSec` 같은 authoring 필드를 재평가한다.

## 6. 상태 / 재노출 정책
### 6.1 기본 정책
- `InterventionCarryFull`
  - 동일 stage run에서 1회만
- `InterventionFirstHit`
  - 앱 세션 전체에서 1회만

### 6.2 skip 처리
- skip도 노출로 간주한다.
- 완료와 skip 모두 seen-state를 기록한다.

### 6.3 재시도 / 재진입
- intervention은 retry variant를 우선 목표로 삼지 않는다.
- 현재 범위에서는 `AlwaysFull` 또는 `OncePerSession`만 권장한다.

## 7. Hint / Notification / Pause 정합성
### 7.1 Hint / Notification
- intervention active 동안 `NotificationPanel`, `HintPanel`은 기존 dialogue suppress 규칙에 따라 숨긴다.
- intervention이 끝나면 hint/notification은 기존 resolver 상태를 그대로 복원한다.
- hint는 intervention을 발생시키지 않는다. hint bridge는 read-only 문맥 공급자에 머문다.

### 7.2 Pause
- intervention은 `StagePlay` dialogue로서 `GameplayPauseReasonId.DialogueGate`를 획득한다.
- 공통 gameplay pause 계약은 `TD-023`을 따르며, `CarryFull`, `FirstHit`도 같은 pause flags를 사용한다.
- pause 중 새 intervention을 시작하지 않는다.

## 8. 업데이트 순서 / 상태 전이
1. `StagePlayInterventionBridge.Update()`
2. shell / HUD snapshot / pause / dialogue active 상태를 읽는다.
3. intervention 조건을 우선순위 순서로 판정한다.
4. playable entry가 있으면 `DemoShellDialogueBridge.TryStartStagePlayIntervention(trigger, stageId)`를 호출한다.
5. `DemoShellDialogueBridge`는 기존 playback 루프로 sequence를 재생한다.
6. 완료/skip 시 `DemoShellSessionStaging`에 seen-state를 기록한다.
7. queue 없이 종료한다.

## 9. 구현 분해 / 진행 상태
### 9.1 P1 trigger / state 확장
- `InWorldDialogueTriggerId`에 intervention trigger 추가
- `DemoShellSessionStaging`에 run/session seen-state 확장
- 상태: `completed`

### 9.2 P2 StagePlayInterventionBridge
- `StagePlayInterventionBridge` 추가
- `CarryFull`, `FirstHit` 조건 판정 구현
- `DemoShellDialogueBridge` 시작 seam 연결
- 상태: `completed`

### 9.3 P3 UI / suppress 회귀
- intervention active 동안 기존 dialogue suppress가 그대로 유지되는지 검증
- hint/notification과 충돌하지 않는지 확인
- `Stage 1` sample intervention authoring을 추가해 운영 씬에서 실제 노출 기준으로 회귀를 검증한다.
- 상태: `completed`

### 9.4 P4 자동 검증
- EditMode
  - run/session seen-state
  - 우선순위 충돌
  - active dialogue 시 drop
- PlayMode
  - `CarryFull`, `FirstHit` overlay intervention
  - start/clear gate보다 낮은 우선순위
  - intervention 중 simulation/gameplay clock 정지 및 종료 후 resume
  - intervention active 동안 pause menu 비허용
- 상태: `planned`

## 10. 검증 계획 / 합격 기준
- compile
- console error 0
- EditMode pass
- PlayMode smoke pass
- 추가 합격 기준
  - `CarryFull`은 동일 run에서 1회만 나온다.
  - `FirstHit`은 동일 session에서 1회만 나온다.
  - active start/clear dialogue가 있으면 intervention은 시작되지 않는다.
  - intervention active 동안 `Hint/Notification`은 숨겨지고, 종료 후 복원된다.
  - intervention active 동안 world simulation은 멈추고, 종료 후 resume된다.
  - intervention active 동안 pause menu는 열리지 않는다.

## 11. 오픈 이슈
- `LowTime`, `SourceDepleted`, `HazardHigh`를 같은 bridge에 계속 올릴지 후속 세션에서 검토한다.
- intervention에 cooldown authoring 필드를 둘지, bridge 고정 상수로 둘지 아직 미정이다.
- 특정 intervention을 `OverlayOnly`에서 `GateIntro` 수준으로 승격할 필요가 있는지는 플레이테스트 후 결정한다.

## 12. 변경 이력
- 2026-03-18: `P1 trigger/state 확장`을 반영했다. intervention trigger enum을 추가했고, `DemoShellSessionStaging`에 active-stage-run seen-state를 도입했다. `EnterStagePlay`는 stage 진입 시 run seen-state를 초기화하고, validation은 intervention trigger의 `Theme` target 및 non-`OverlayOnly` blocking mode를 금지하도록 확장했다.
- 2026-03-18: `P2 StagePlayInterventionBridge`를 반영했다. `StagePlay`의 `Running` 상태에서 `FirstHit`, `CarryFull` intervention을 overlay-only로 판정하는 owner bridge를 추가했고, `DemoShellDialogueBridge.TryStartStagePlayIntervention(trigger, stageId)` seam을 통해 playback을 재사용하도록 연결했다. `FirstHit`은 feedback snapshot version edge + `PlayerHazardHit` type으로만 판정하고, `CarryFull`은 완료/skip 시 current-run seen-state를 기록한다.
- 2026-03-18: `P3 UI / suppress 회귀`를 반영했다. `Stage 1` sample intervention entry(`CarryFull`, `FirstHit`)를 demo catalog에 추가했고, 운영 씬과 EditMode/PlayMode 회귀에서 intervention active 동안 `DialoguePanel` 표시, `NotificationPanel`/`HintPanel` suppress, `StageHudPanel` 유지, `OverlayOnly` dim 비표시, 종료 후 visibility 복구를 실제 노출 기준으로 검증했다.
- 2026-03-18: `StagePlay dialogue pause` 일반화를 반영했다. `CarryFull`, `FirstHit` intervention도 `StageStart`, `StageClear`와 같은 `DialogueGate` pause flags를 획득하도록 규약을 정정했고, `OverlayOnly`는 pause 비대상이 아니라 presentation policy만 표현하는 mode로 재정의했다.
