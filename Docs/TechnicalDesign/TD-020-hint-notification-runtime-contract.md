# Hint / Notification Runtime Contract (TD-020)

## Metadata
- doc_id: `TD-020`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-16`
- related_docs:
  - [GD-009-in-game-ui-screen-blueprint.md](../GameDesign/GD-009-in-game-ui-screen-blueprint.md)
  - [GD-010-in-game-ui-layout-and-zones.md](../GameDesign/GD-010-in-game-ui-layout-and-zones.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
  - [TD-013-player-feedback-presentation-bridge-contract.md](./TD-013-player-feedback-presentation-bridge-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](./TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [TD-022-in-world-dialogue-runtime-contract.md](./TD-022-in-world-dialogue-runtime-contract.md)
- related_adr: 중요 결정 없음 (ADR 신규 작성 없음)

> `Notification`은 사건 통지, `Hint`는 행동 유도로 역할을 분리하고, Runtime UI는 기존 owner가 만든 스냅샷/상태를 읽어 최종 표시 1개를 결정한다. 인월드 연출 대화가 active인 동안 lower-center lane은 suppress된다.

## 1. 목표 / 비목표
### 1.1 목표
- `GD-009`, `GD-010` 기준으로 하단 중앙 `Notification` / `Hint` 2레인 계약을 고정한다.
- `Notification`은 사건 결과 전달, `Hint`는 행동 유도/학습 전달로 역할을 분리한다.
- Runtime UI는 기존 owner 경계를 유지한 채 `NotificationResolver`와 `Hint` 상태 판정 규칙을 정의한다.
- `Carry Full`, `Hit`, `Time Low/Critical`, `Source state change`, `첫 피격`, `실패 후 재시도 힌트`를 다룰 수 있는 기본 상태 모델을 정의한다.
- `HUD V2`, `HazardStack`, `월드 인디케이터`와 충돌하지 않는 책임 경계를 만든다.

### 1.2 비목표
- `HazardStack` 구현
- 월드 인디케이터 구현
- Screen FX 구현
- 최종 카피, 아이콘, 애니메이션 수치 확정
- Gameplay owner를 UI owner로 승격하는 구조 변경
- 모든 알림을 ECS 이벤트 큐로 재작성하는 전면 리라이트

## 2. 역할 정의
### 2.1 Notification
- 성격: 사건 통지
- 질문: `방금 무슨 일이 일어났는가?`
- 체류: 짧음
- 규칙: 우선순위, 교체, suppress, cooldown이 강함
- 한 번에 1개만 표시한다.

### 2.2 Hint
- 성격: 행동 유도 / 학습
- 질문: `지금 무엇을 해야 하는가?`
- 체류: Notification보다 김
- 규칙: seen-state, 재노출 정책, 실패 후 재허용이 핵심
- 한 번에 1개만 표시한다.

## 3. 소유권 (Owner / Reader)
### 3.1 Owner 유지
- `PlayerHudSnapshotCollectSystem`
  - Carry, Source progress, Stage elapsed, hit flash 등 HUD snapshot writer
- `PlayerUiFeedbackConsumeSystem`
  - discrete feedback snapshot writer
- `DemoShellFlowController`
  - screen/stage/session 결과 owner
- `RunDirectorStage*` owner
  - stage 상태/clear/fail/time-up 의미 owner

### 3.2 Runtime UI Reader / Resolver
- `NotificationResolver`
  - discrete event candidate + derived candidate를 합쳐 최종 Notification 1개를 선택한다.
  - 게임플레이 사건 자체를 생성하지 않는다.
- `NotificationPresenter`
  - resolver 결과를 표시만 한다.
- `HintStateSource` 또는 동등한 owner
  - `session seen`, `stage seen`, `failure hint bucket`을 소유한다.
- `HintPresenter`
  - hint 판정 결과를 표시만 한다.

### 3.3 책임 경계
- 도메인 사건 감지:
  - 기존 gameplay owner/system이 담당한다.
  - 예: `Hit`, `SourceWeakened`, `SourceCleared`, `Deposited`, `StageClear`, `TimeUp`
- 프레젠테이션 조건 판정:
  - resolver가 담당한다.
  - 예: `TimeLow`, `TimeCritical`, `CarryFull`
- UI는 ECS write 금지
- UI는 `DemoShellFlowController`, `PlayerRuntimeHudBridge`, feedback snapshot을 read-only로만 소비한다.

## 4. 업데이트 순서 / 데이터 흐름
### 4.1 ECS 파이프라인
프로젝트 기본 순서는 유지한다.

`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`

- HUD/feedback snapshot writer는 기존 ECS 순서에서 값만 갱신한다.
- `Notification` / `Hint`용 신규 gameplay writer는 기본적으로 추가하지 않는다.

### 4.2 Runtime UI 소비 순서
1. ECS owner가 snapshot을 갱신한다.
2. `PlayerRuntimeHudBridge`가 최신 snapshot을 캐시한다.
3. `RuntimeUiRoot.Update()`가 화면 활성 상태를 적용한다.
4. 인월드 연출 대화가 active면 lower-center lane suppress를 먼저 적용한다.
5. `NotificationResolver`가 discrete event candidate + derived candidate를 평가한다.
6. `Hint` 판정이 현재 context와 seen-state를 기준으로 hint 후보를 평가한다.
7. presenter가 최종 텍스트/표시 상태만 갱신한다.

원칙:
- resolver는 frame 내 최종 출력 owner다.
- presenter는 표시만 담당한다.

## 5. Notification 설계 계약
### 5.1 입력 소스
- `PlayerHudSnapshotComponent`
- `PlayerUiFeedbackPresentationSnapshotComponent`
- `PlayerRuntimeHudBridge.LastFeedbackLine`
- `DemoShellFlowController`의 screen/stage 문맥
- 필요 시 stage owner 결과(`StageClear`, `TimeUp`)의 read-only 상태

### 5.2 Candidate 분류
- `Event candidate`
  - 기존 owner가 감지한 discrete 사건
  - 예: `Hit`, `SourceWeakened`, `SourceCleared`, `HazardCaptured`, `HazardRemoved`
- `Derived candidate`
  - resolver가 snapshot에서 직접 계산하는 UI 조건
  - 예: `TimeLow`, `TimeCritical`, `CarryFull`

### 5.3 Notification ID
- `None`
- `HitCarryLost`
- `TimeLow`
- `TimeCritical`
- `CarryFull`
- `SourceWeakened`
- `SourceCleared`
- `HazardCaptured`
- `HazardRemoved`
- `Deposited`
- `VacuumLocked`
- `VacuumCooldown`
- `StageClear`
- `TimeUp`

### 5.4 우선순위
1. `HitCarryLost`
2. `TimeCritical`
3. `CarryFull`
4. `TimeLow`
5. `SourceWeakened`
6. `SourceCleared`
7. `VacuumLocked` / `VacuumCooldown`
8. `HazardCaptured` / `HazardRemoved` / `Deposited`
9. `StageClear` / `TimeUp`

초기 구현은 위 순서를 기본값으로 사용한다.
후속 UX 보정에서 `StageClear` / `TimeUp`은 shell/result 전환 타이밍과 함께 재평가할 수 있다.

### 5.5 문구 표준
| ID | 기본 문구 |
| --- | --- |
| `HitCarryLost` | `Hit! Carry lost` |
| `TimeLow` | `Time is running out` |
| `TimeCritical` | `Time critical` |
| `CarryFull` | `Carry full - deposit now` |
| `SourceWeakened` | `Source weakened` |
| `SourceCleared` | `Source cleared` |
| `HazardCaptured` | `Hazard captured` |
| `HazardRemoved` | `Hazard removed` |
| `Deposited` | `Deposited` |
| `VacuumLocked` | `Vacuum locked` |
| `VacuumCooldown` | `Vacuum cooling down` |
| `StageClear` | `Stage clear` |
| `TimeUp` | `Time up` |

### 5.6 suppress / merge 규칙
- `PlayerHazardHit` feedback line은 suppress하고 `HitCarryLost`로 통일한다.
- `VacuumStartBlocked + CarryBinFull` feedback line은 suppress하고 `CarryFull`로 통일한다.
- danger candidate가 존재하면 feedback candidate보다 항상 우선한다.
- 한 시점에 1개만 표시한다.
- 인월드 연출 대화 active 동안 `Notification` lane 표시는 suppress한다.

### 5.7 Notification runtime state
```csharp
struct NotificationRuntimeState
{
    NotificationId CurrentId;
    NotificationId LastShownId;
    float RemainingSec;
    float CooldownUntilSec;
    bool TimeLowLatched;
    bool TimeCriticalLatched;
    bool CarryFullLatched;
}
```

### 5.8 재노출 정책
- `HitCarryLost`
  - 허용
  - 짧은 cooldown만 사용
- `TimeLow`
  - stage 내 임계 진입 시 1회
  - latch로 중복 표시 방지
- `TimeCritical`
  - stage 내 임계 진입 시 1회
  - latch로 중복 표시 방지
- `CarryFull`
  - `not full -> full` 전이 시 1회
  - full 상태 유지 중 반복 금지
- `SourceWeakened`, `SourceCleared`
  - source state transition마다 1회
- 일반 feedback (`HazardCaptured`, `HazardRemoved`, `Deposited`)
  - 허용
  - 짧은 cooldown만 사용

## 6. Hint 설계 계약
### 6.1 입력 소스
- `PlayerHudSnapshotComponent`
- `DemoShellFlowController`의 screen/stage 문맥
- 실패 후 결과 문맥 (`StageResult`, `TimeUp`, hit count 등)
- 별도 owner가 가진 seen-state

### 6.2 Hint ID
- `None`
- `StageStartMoveAndCollect`
- `CarryFullGoToDeposit`
- `CollectFromSources`
- `DepositRemainingTrash`
- `FirstHitAvoidHazards`
- `FailTimeoutMoveFaster`
- `FailHighHitKeepDistance`

### 6.3 scope 구분
- `session`
  - 앱 실행 동안 유지
- `stage`
  - 현재 스테이지 문맥 동안 유지
- `failure`
  - 직전 실패 상황에만 귀속

### 6.4 기본 문구
| ID | scope | 기본 문구 |
| --- | --- | --- |
| `StageStartMoveAndCollect` | `stage` | `Collect trash from active sources.` |
| `CarryFullGoToDeposit` | `stage` | `Carry is full. Head to Deposit.` |
| `CollectFromSources` | `stage` | `Collect trash from active sources.` |
| `DepositRemainingTrash` | `stage` | `Return remaining trash to Deposit.` |
| `FirstHitAvoidHazards` | `session` | `Avoid hazards to keep your carry.` |
| `FailTimeoutMoveFaster` | `failure` | `Move faster between Source and Deposit.` |
| `FailHighHitKeepDistance` | `failure` | `Keep distance from hazards while carrying.` |

### 6.5 Hint seen-state
```csharp
struct HintSeenState
{
    HashSet<HintId> SessionSeen;
    HashSet<HintId> StageSeen;
    HintId LastFailureHint;
}
```

### 6.6 재노출 정책
- `StageStartMoveAndCollect`
  - stage 1회
- `CarryFullGoToDeposit`
  - stage 1회
- `CollectFromSources`
  - stage 1회
- `DepositRemainingTrash`
  - stage 1회
- `FirstHitAvoidHazards`
  - session 1회
- `FailTimeoutMoveFaster`
  - failure 기반 재허용
- `FailHighHitKeepDistance`
  - failure 기반 재허용

### 6.7 리셋 규칙
- `SessionSeen`
  - 앱 실행 동안 유지
  - `Lobby` 복귀, `Restart Demo`로 초기화하지 않는다.
- `StageSeen`
  - 새 stage 진입 시 초기화
  - 기본값은 `Retry 시 유지`
- `LastFailureHint`
  - 새 실패 분류 시 갱신
  - 다음 stage 진입 시 초기화 가능

## 7. Hint / Notification 상호작용
- 두 레인은 동시에 표시될 수 있다.
- 같은 의미의 문구는 두 레인에 동시에 표시하지 않는다.
- `Notification`은 사건 결과, `Hint`는 다음 행동 제시로 언어를 분리한다.
- 인월드 연출 대화 active 동안 두 레인은 모두 숨긴다.
- 예:
  - `CarryFull` notification이 표시 중이면 동일 순간 `CarryFullGoToDeposit` hint는 지연 또는 suppress 대상이다.
  - `HitCarryLost` 직후 `FirstHitAvoidHazards` hint는 짧은 지연 뒤 표시하는 편을 기본값으로 둔다.

## 8. Message Catalog
### 8.1 Notification Catalog
| ID | 타입 | 우선순위 | 기본 문구 | 목적 | 발행자 / 감지 주체 | 현재 상태 |
| --- | --- | --- | --- | --- | --- | --- |
| `HitCarryLost` | `Notification` | `1` | `Hit! Carry lost` | 피격 결과 즉시 통지 | `PlayerUiFeedbackConsumeSystem` event 또는 `PlayerHudSnapshotComponent` hit flash, 최종 선택은 `NotificationResolver` | `implemented` |
| `TimeCritical` | `Notification` | `2` | `Time critical` | 남은 시간 위험 통지 | `NotificationResolver` derived (`remainingSec <= 10s`) | `implemented` |
| `CarryFull` | `Notification` | `3` | `Carry full - deposit now` | 적재량 가득 참 통지 | `NotificationResolver` derived (`carry full`) 또는 `VacuumStartBlocked + CarryBinFull` suppress merge | `implemented` |
| `TimeLow` | `Notification` | `4` | `Time is running out` | 시간 압박 사전 경고 | `NotificationResolver` derived (`remainingSec <= 30s`) | `implemented` |
| `SourceWeakened` | `Notification` | `5` | `Source weakened` | Source 상태 전이 통지 | `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`, 최종 선택은 `NotificationResolver` | `implemented` |
| `SourceCleared` | `Notification` | `6` | `Source cleared` | Source 정리 완료 통지 | `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`, 최종 선택은 `NotificationResolver` | `implemented` |
| `VacuumLocked` | `Notification` | `7` | `Vacuum locked` | Vacuum blocked 사유 통지 | `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`, 최종 선택은 `NotificationResolver` | `implemented` |
| `VacuumCooldown` | `Notification` | `7` | `Vacuum cooling down` | Vacuum blocked 사유 통지 | `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`, 최종 선택은 `NotificationResolver` | `implemented` |
| `HazardCaptured` | `Notification` | `8` | `Hazard captured` | 수집 성공 통지 | `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`, 최종 선택은 `NotificationResolver` | `implemented` |
| `HazardRemoved` | `Notification` | `8` | `Hazard removed` | 제거/정리 결과 통지 | `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`, 최종 선택은 `NotificationResolver` | `implemented` |
| `Deposited` | `Notification` | `8` | `Deposited` | Deposit 성공 통지 | 별도 discrete producer 필요 | `planned_v2_b` |
| `StageClear` | `Notification` | `9` | `Stage clear` | 스테이지 클리어 통지 fallback | `DemoShellFlowController` stage/result 문맥을 `DemoShellNotificationBridge`가 감지, 인월드 연출 대화 active 시에는 suppress 또는 omit | `implemented_fallback` |
| `TimeUp` | `Notification` | `9` | `Time up` | 타임아웃 실패 통지 | `DemoShellFlowController` screen/result 전이를 `DemoShellNotificationBridge`가 감지, 최종 선택은 `NotificationResolver` | `implemented` |

### 8.2 Hint Catalog
| ID | 타입 | 우선순위 | scope | 기본 문구 | 목적 | 발행자 / 감지 주체 | 현재 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `StageStartMoveAndCollect` | `Hint` | `-` | `stage` | `Collect trash from active sources.` | 스테이지 시작 온보딩 | `DemoShellHintBridge` + `HintResolver` stage-start 조건 | `planned_v2_b` |
| `CarryFullGoToDeposit` | `Hint` | `1` | `stage` | `Carry is full. Head to Deposit.` | 적재가 가득 찼을 때 다음 행동 유도 | `DemoShellHintBridge` + `HintResolver` | `implemented` |
| `DepositRemainingTrash` | `Hint` | `2` | `stage` | `Return remaining trash to Deposit.` | Source 소진 후 남은 Carry 처리 유도 | `DemoShellHintBridge` + `HintResolver` | `implemented` |
| `CollectFromSources` | `Hint` | `3` | `stage` | `Collect trash from active sources.` | 현재 수집 우선 행동 유도 | `DemoShellHintBridge` + `HintResolver` | `implemented` |
| `FirstHitAvoidHazards` | `Hint` | `4` | `session` | `Avoid hazards to keep your carry.` | 첫 피격 후 생존/회피 학습 | `DemoShellHintBridge` + `HintResolver` | `implemented` |
| `FailTimeoutMoveFaster` | `Hint` | `5` | `failure` | `Move faster between Source and Deposit.` | 타임아웃 실패 원인 피드백 | `DemoShellHintBridge` + `HintResolver`, fail classification from `DemoShellFlowController.CurrentStageResult` | `implemented` |
| `FailHighHitKeepDistance` | `Hint` | `5` | `failure` | `Keep distance from hazards while carrying.` | 피격 과다 실패 원인 피드백 | `DemoShellHintBridge` + `HintResolver`, fail classification from `DemoShellFlowController.CurrentStageResult` | `implemented` |

## 9. 구현 전략
### 9.1 V2-A
- 기존 입력만 사용한다.
  - `PlayerHudSnapshotComponent`
  - `PlayerUiFeedbackPresentationSnapshotComponent`
  - `DemoShellFlowController`
- `NotificationResolver`
  - priority / cooldown / latch / suppress 구현
- `HintStateSource`
  - `SessionSeen`, `StageSeen`, `LastFailureHint` owner
- `HintPresenter`
  - stage/snapshot 기반 조건 + seen-state를 읽어 표시

### 9.2 V2-B
- 현재 입력만으로 부족한 discrete 사건 source를 보강한다.
- 후보:
  - `Deposited`
  - `StageClear`
  - `TimeUp`
  - 실패 이유 분류값

## 10. 작업 분해 / 진행 상태
- `P1` Notification 후보/우선순위/문구 표 확정: `done`
- `P1` Hint ID / seen-state / 재노출 정책 표 확정: `done`
- `P2` `NotificationResolver` / `NotificationPresenter V2` 구현: `done`
- `P2` `HintStateSource` / `HintPresenter V2` 구현: `done`
- `P2` `Retry` / `Stage enter` / `Fail` 시 seen-state reset 연결: `done`
- `P3` `Deposited`, `StageClear`, `TimeUp` discrete source 보강 여부 결정: `pending`
- `P3` stage별 카피 override 필요성 검토: `pending`
- `P3` 인월드 연출 대화 active 시 lower-center lane suppress 연동: `pending`

## 11. 검증 계획 / 합격 기준
- compile
- console error 0
- EditMode
  - priority table 검증
  - latch / cooldown 검증
  - `CarryFull` 전이 기반 재표시 억제 검증
  - `Hint` session/stage/failure scope 검증
  - `Retry` / `Stage enter` 시 seen-state reset 검증
- PlayMode smoke
- `CarryFull` notification + hint 지연/중복 억제
- `Hit` notification 우선순위
- `TimeLow -> TimeCritical` 전이
- 첫 피격 힌트 1회성
- 실패 후 timeout/hit 기반 힌트 노출
- 인월드 연출 대화 active 동안 `Notification/Hint` 숨김

## 12. 오픈 이슈
- `HintSeenState`를 presenter 내부 상태로 둘지, 별도 bridge/owner로 둘지 구현 시점에 확정 필요
- `Retry` 시 `StageSeen` 유지/초기화는 UX 플레이테스트 후 조정 가능
- `Deposited`, `StageClear`, `TimeUp` discrete source는 현재 snapshot만으로 충분한지 확인 필요
- stage별 hint copy override가 필요하면 `GameDesign` 후속 문서와 연결 필요
- `StageClear` fallback notification을 유지할지, clear dialogue가 없는 시나리오 전용으로 축소할지 후속 정리 필요

## 13. 변경 이력
- 2026-03-16: 초안 작성. `Notification`과 `Hint`의 책임 분리, owner/read-only 경계, message ID, 재노출 정책, seen-state 모델, V2 구현 전략을 정리했다.
- 2026-03-16: 현재 구현 기준 `Message Catalog`를 추가했다. 각 문구의 타입, 우선순위, 목적, 발행자/감지 주체, 구현 상태를 표로 정리했다.
- 2026-03-16: `TD-022` 연계 반영. 인월드 연출 대화 active 동안 lower-center lane suppress와 `StageClear` fallback 정책을 추가했다.
