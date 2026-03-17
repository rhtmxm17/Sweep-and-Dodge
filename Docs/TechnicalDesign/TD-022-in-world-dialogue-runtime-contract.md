# 인월드 연출 대화 런타임 계약 (TD-022)

## Metadata
- doc_id: `TD-022`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-03-17`
- related_docs:
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [GD-011-in-world-dialogue-direction.md](../GameDesign/GD-011-in-world-dialogue-direction.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](./TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [TD-020-hint-notification-runtime-contract.md](./TD-020-hint-notification-runtime-contract.md)
  - [ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md](../ADR/ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md)

> 인월드 연출 대화는 `DemoShellFlowController`가 전환 타이밍을 소유하고, `DemoShellDialogueBridge`가 active sequence 상태를 소유하는 GO 전용 계층으로 운영한다. v1 기본 정책은 `StageStart=overlay`, `StageClear=pre-result clear gate`다.

## 1. 목표 / 비목표
### 1.1 목표
- `GD-011` 기준의 인월드 연출 대화를 런타임 owner/reader 경계와 전이 규칙까지 포함해 고정한다.
- `StageStart`, `StageClear`, `ThemeTransition` 범위의 전환성 대화를 기존 Demo Shell 흐름 안에 자연스럽게 삽입한다.
- 월드 상태를 유지한 채 `Result` 전 클리어 대화를 재생할 수 있도록 clear gate 사용 규칙을 정의한다.
- 기존 `Hint/Notification`과 역할이 겹치지 않도록 presentation layer와 suppress 규칙을 명확히 한다.
- 월드 말풍선/스피커 앵커는 기존 `StagePresentationRuntimeController` 경로를 재사용해 authoring 비용을 낮춘다.

### 1.2 비목표
- 튜토리얼 예외 개입 구현
- 대화 선택지, 분기형 스토리, 장문 컷신
- 대화 시스템을 ECS gameplay writer로 확장하는 구조 변경
- 보이스, 립싱크, 카메라 컷, 타임스케일 제어까지 포함한 대형 연출 시스템
- 현 시점에서 `ThemeTransition` 전용 외부 화면 추가

## 2. 범위 / 사용자 경험 기준
### 2.1 v1 범위
- `StageStart`
- `StageClear`
- `ThemeTransition`

### 2.2 기본 개입 강도
- `StageStart`: `OverlayOnly`
  - 기본값은 플레이 진입 템포를 보존하는 비차단 오버레이다.
  - 데이터는 후속 확장을 위해 `GateIntro`도 표현 가능하게 설계한다.
- `StageClear`: `GateClear`
  - `ClearReady` 직후 `Result`로 즉시 넘어가지 않고 월드 위에서 짧은 연출 대화를 재생한다.
- `ThemeTransition`: `ShellOverlay`
  - current demo에서는 별도 chapter screen이 없으므로, stage 전환 전후 shell 문맥에서 재생 가능한 타입으로만 정의한다.

### 2.3 반복 노출 기본값
- 첫 노출은 full variant를 우선한다.
- 동일 stage 재도전 시에는 `ShortOnRetry` 또는 `SkipOnRetry`를 기본 선택지로 둔다.
- v1에서는 `GD-011` 기준에 맞춰 `1~3`개 발화 안에서 종료한다.

## 3. 소유권 (Owner / Reader)
### 3.1 전환 owner
- `DemoShellFlowController`
  - 어떤 전환에서 대화를 시작하는지 결정한다.
  - `StageClear`에서 `ClearReady -> StageResult` 즉시 전환을 지연한다.
  - `RunDirectorStageBridge`에 대한 최종 write 책임을 가진다.
  - `StageRunCompleted` 수신 후 `StageResult` 전환을 마무리한다.

### 3.2 대화 session owner
- `DemoShellDialogueBridge`
  - active sequence 선택 결과, 현재 line index, advance/skip 쿨다운, retry variant 상태를 소유한다.
  - 스크립트 데이터와 현재 shell 문맥을 조합해 `DialoguePresentationState`를 생성한다.
  - ECS write는 하지 않는다.
  - sequence 완료 시 `DemoShellFlowController`에 completion을 통지한다.

### 3.3 durable seen-state owner
- `DemoShellSessionStaging`
  - session 범위 seen-state
  - active stage 기준 seen-state
  - retry variant 판단에 필요한 최소 runtime flag

### 3.4 presentation reader
- `RuntimeUiRoot`
  - `DemoShellDialogueBridge`를 auto-bind하고, 후속 `InWorldDialoguePresenter`가 읽을 owner를 노출한다.
  - `PresentationLayer`와 `DialoguePanel` visibility를 소유한다.
  - dialogue active 시 `Notification` / `Hint` suppress 적용 지점을 제공한다.
- `InWorldDialoguePresenter`
  - portrait, nameplate, text plate, advance/skip prompt, world bubble 위치를 표시만 담당한다.
  - `DialoguePresentationState` snapshot만 읽고 입력은 직접 읽지 않는다.
- `StagePresentationRuntimeController`
  - speaker/world bubble anchor를 read-only로 공급한다.

## 4. 데이터 구조 / authoring 계약
### 4.1 Catalog
- `InWorldDialogueCatalogSO`
  - `SchemaVersion`
  - `Entries[]`
- `InWorldDialogueSpeakerCatalogSO`
  - `SchemaVersion`
  - `Profiles[]`
- `InWorldDialogueCatalogEntry`
  - `Enabled`
  - `EntryKey`
  - `Trigger`
  - `TargetKind`
  - `StageId`
  - `ThemeKey`
  - `Priority`
  - `BlockingMode`
  - `RetryPolicy`
  - `FullVariant`
  - `RetryVariant`

### 4.2 Trigger / blocking
- `InWorldDialogueTriggerId`
  - `StageStart`
  - `StageClear`
  - `ThemeTransition`
- `InWorldDialogueTargetKind`
  - `Stage`
  - `Theme`
  - `Global`
- `InWorldDialogueBlockingMode`
  - `OverlayOnly`
  - `GateIntro`
  - `GateClear`
  - `ShellOverlay`

### 4.3 Line data
- `InWorldDialogueLine`
  - `SpeakerKey`
  - `Text`
  - `AnchorRef`
  - `MinHoldSec`
  - `AutoAdvanceSec`
- `InWorldDialogueSpeakerProfile`
  - `SpeakerKey`
  - `DisplayName`
  - `Portrait`
  - `PortraitSide`

### 4.4 Anchor resolution
- `InWorldDialogueAnchorRef`
  - `StagePresentationStableId`
  - `ScreenAnchor`
  - `None`
- 기본 원칙
  - 월드 앵커는 새 registry를 만들지 않는다.
  - 가능한 경우 `StagePresentationRuntimeController`가 stage presentation stableId로 spawned GO를 찾아 bubble anchor를 공급한다.
  - 앵커를 찾지 못하면 screen-space fallback으로 내려간다.

## 5. 업데이트 순서 / 상태 전이 계약
### 5.1 StageStart
1. `DemoShellFlowController.EnterStagePlay(stageIndex)`
2. topology apply 요청
3. `RunDirectorStageBridge.RequestStageStart()`는 기존 경로 유지
4. `CurrentStagePlayPhase == Running` 최초 관측 edge에서 기본값이 `OverlayOnly`인 start sequence가 있으면 `DemoShellDialogueBridge`가 재생 시작
5. dialogue는 `Running`과 병행 가능하며, 완료/스킵 시 presentation만 정리한다

### 5.2 StageClear
1. ECS가 `RunDirectorStageStateId.ClearReady`에 진입
2. `DemoShellFlowController`는 즉시 `EnterStageResult()`를 호출하지 않는다
3. `DemoShellDialogueBridge`가 `StageClear` sequence를 재생한다
4. sequence 완료/스킵 시 `DemoShellFlowController`가 아래를 같은 단계에서 수행한다
   - `RunDirectorStageBridge.SetClearPresentationDone(true)`
   - `RunDirectorStageBridge.RequestConfirm()`
5. ECS `Completed` 전환 후 `StageRunCompleted` 신호 발생
6. `DemoShellFlowController`가 `StageResult`로 전환한다

### 5.3 ThemeTransition
- `ThemeTransition`은 shell screen 전환 문맥에서 재생한다.
- current demo에서는 stage clear 직후 `Result`를 대체하지 않고, stage 진입/다음 stage 진입 전후 shell overlay로만 허용한다.
- 별도 chapter screen이 도입되면 이 항목을 해당 shell screen owner에 연결한다.

### 5.4 suppress 계약
- dialogue active 동안 `NotificationPresenter`와 `HintPresenter`는 기본적으로 숨긴다.
- `StageHudPresenter`는 유지한다.
- current v1 UI에는 별도 lower-center toast lane이 없으므로, suppress 대상은 `NotificationPanel`, `HintPanel`로 한정한다.

## 6. Runtime UI / 입력 계약
### 6.1 레이어
- `RuntimeUiRoot`에 `PresentationLayer`를 추가한다.
- 권장 순서
  - `ShellLayer`
  - `HudLayer`
  - `PresentationLayer`
  - `ModalLayer`
  - `FxLayer`

### 6.2 presenter 구성
- `InWorldDialoguePresenter`
  - `DimRoot`
  - `PortraitRoot`
  - `DialoguePlateRoot`
  - `NameText`
  - `BodyText`
  - `AdvancePrompt`
  - `SkipPrompt`
  - `WorldBubbleRoot`
  - `StagePresentationStableId` anchor는 `StagePresentationRuntimeController` read API를 통해 screen projection으로 해석한다.
  - lookup 실패, camera 부재, off-screen, behind-camera인 경우는 즉시 plate fallback으로 내린다.

### 6.3 입력
- advance: `Submit` 또는 좌클릭
- skip: `Cancel` 또는 별도 skip binding
- presentation owner가 입력을 해석하고, presenter는 입력을 직접 소비하지 않는다.
- `StageClear` gate 재생 중에는 pause/confirm modal 입력보다 dialogue advance/skip이 우선한다.

## 7. 성능 / 리스크
### 7.1 성능 원칙
- per-entity ECS 조회 금지
- singleton bridge + 현재 stage presentation anchor만 사용
- 앵커 projection과 text 갱신은 active dialogue 동안에만 수행

### 7.2 주요 리스크
- 리스크 1: `ClearReady -> Result` 지연으로 결과 시간 집계가 흔들릴 수 있다
  - 대응: 결과 metrics snapshot 시점을 `StageResult` 진입 시점이 아니라 `ClearReady` 또는 fail 확정 시점으로 고정한다.
- 리스크 2: start overlay와 gameplay/pause 입력 충돌
  - 대응: v1 기본값은 overlay지만 pause 우선순위를 명시하고, `StageClear` gate 중에는 dialogue 입력 우선으로 고정한다.
- 리스크 3: 월드 앵커 유실 시 bubble이 튀는 문제
  - 대응: stableId lookup 실패 시 즉시 screen-space fallback으로 내린다.

## 8. 작업 분해 / 진행 상태
### 8.1 P1 데이터 모델 / authoring
- 목표
  - `InWorldDialogueCatalogSO`, `InWorldDialogueEntry`, `InWorldDialogueLine`, `RetryPolicy`, `BlockingMode`를 확정한다.
- 완료 기준
  - stage별 sequence를 데이터로 authoring 가능하다.
  - `StageStart`, `StageClear`, `ThemeTransition` lookup 규칙이 고정된다.
- 상태: `completed`

### 8.2 P2 Shell / gate 통합
- 목표
  - `DemoShellFlowController`에 `StageClear defer`와 dialogue completion callback을 연결한다.
- 완료 기준
  - `ClearReady` 직후 즉시 `StageResult`로 넘어가지 않는다.
  - clear sequence 완료/skip 시에만 `SetClearPresentationDone(true)` + `RequestConfirm()`가 호출된다.
  - 결과 metrics snapshot 시점이 `ClearReady/fail confirm` 기준으로 재정렬된다.
- 상태: `completed`

### 8.3 P3 Dialogue bridge / runtime state
- 목표
  - `DemoShellDialogueBridge`가 active sequence 선택, line 진행, skip/advance, retry-short 판정을 소유한다.
- 완료 기준
  - shell 문맥만으로 active dialogue state를 재현 가능하다.
  - `DemoShellSessionStaging`과 session/stage seen-state 연동이 된다.
- 상태: `completed`

### 8.4 P4 Runtime UI / presenter
- 목표
  - `RuntimeUiRoot.PresentationLayer`와 `InWorldDialoguePresenter`를 추가한다.
- 완료 기준
  - portrait, nameplate, text plate, prompt, world bubble이 reader-only로 갱신된다.
  - dialogue active 동안 `Notification/Hint`가 suppress된다.
- 상태: `completed`

### 8.5 P5 Anchor / stage presentation 연동
- 목표
  - `StagePresentationRuntimeController`에서 dialogue anchor lookup을 제공한다.
- 완료 기준
  - stableId 기반 월드 bubble anchor를 찾을 수 있다.
  - lookup 실패 시 screen-space fallback이 즉시 동작한다.
- 상태: `completed`

### 8.6 P6 테스트 / 스모크
- 목표
  - EditMode와 PlayMode에 clear defer, suppress, retry policy 회귀를 추가한다.
- 완료 기준
  - `StageStart overlay`, `StageClear pre-result gate`, `dialogue skip`, `anchor fallback` 케이스가 자동 검증된다.
- 상태: `pending`

### 8.7 권장 구현 순서
1. `P1 데이터 모델 / authoring`
2. `P2 Shell / gate 통합`
3. `P3 Dialogue bridge / runtime state`
4. `P4 Runtime UI / presenter`
5. `P5 Anchor / stage presentation 연동`
6. `P6 테스트 / 스모크`

## 9. 검증 계획 / 합격 기준
- 공통
  1. compile
  2. console error 0
  3. EditMode pass
  4. PlayMode smoke pass
- 추가 EditMode
  - `StageClear`에서 `Result` 즉시 진입이 지연되는지 검증
  - clear dialogue 완료 전에는 `SetClearPresentationDone(true)`가 호출되지 않는지 검증
  - retry policy(`FullFirstSeen`, `ShortOnRetry`, `SkipOnRetry`) 판정 검증
  - anchor resolve 실패 시 screen-space fallback 검증
- 추가 PlayMode
  - `StageStart` overlay가 `Running`과 병행되는지 검증
  - `StageClear` dialogue가 월드 위에서 노출된 뒤 `Result`로 넘어가는지 검증
  - dialogue active 동안 `Hint/Notification`이 suppress되는지 검증

## 10. 오픈 이슈
- `ThemeTransition` 전용 문맥을 현재 demo flow에서 별도 screen으로 둘지, stage start variant로 흡수할지 후속 정리 필요
- `StageStart`의 `GateIntro` 모드를 실제 공개 빌드 기본값으로 승격할지 플레이테스트가 필요
- speaker portrait와 world actor visual이 항상 1:1 대응해야 하는지 art pipeline 합의가 필요
- accessibility 옵션(`자동 진행 속도`, `skip hold`, `dim 강도`)을 v1에 넣을지 후속으로 미룰지 결정 필요

## 11. 변경 이력
- 2026-03-16: 초안 작성. `StageStart=overlay`, `StageClear=pre-result clear gate`, `DemoShellFlowController` 전환 owner, `DemoShellDialogueBridge` session owner, `PresentationLayer`/anchor 재사용 계약을 정리했다.
- 2026-03-16: `P1`, `P2` 구현 반영. `DemoShellSessionStaging`에 dialogue state를 추가했고, `DemoShellFlowController`가 `ClearReady -> pre-result defer -> Completed -> StageResult`를 직접 소유하도록 갱신했다. EditMode 210 pass, PlayMode dedicated smoke pass, clear defer subscriber PlayMode pass를 확인했다.
- 2026-03-16: `P3` 구현 반영. `DemoShellDialogueBridge`와 `DialoguePresentationState`를 추가했고, `StageStart` running edge 시작, `StageClear` shell seam 소비, retry/seen-state, skip/auto-advance, PlayMode/Editor 회귀 테스트를 문서 기준으로 맞췄다.
- 2026-03-17: `P4` 구현 반영. `RuntimeUiRoot`에 `PresentationLayer`와 `DialoguePanel`을 추가했고, `InWorldDialoguePresenter`가 screen-space plate/portrait/dim과 prompt를 reader-only로 갱신하도록 반영했다. dialogue active 동안 `NotificationPanel`과 `HintPanel` suppress를 적용했다.
- 2026-03-17: `P5` 구현 반영. `StagePresentationRuntimeController`에 stableId root/anchor read API를 추가했고, `StagePresentationAnchorMarker`와 stage presentation prefab seam을 통해 marker 우선 / root fallback anchor 규칙을 적용했다. `InWorldDialoguePresenter`는 world anchor를 screen projection으로 배치하고, 실패 시 bubble을 숨긴 채 plate fallback을 유지한다.
