# Runtime UI 셸 / 내비게이션 계약 (TD-016)

## Metadata
- doc_id: `TD-016`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-17`
- related_docs:
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [OPS-003-public-release-readiness-plan.md](../ProjectOps/OPS-003-public-release-readiness-plan.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
  - [TD-013-player-feedback-presentation-bridge-contract.md](./TD-013-player-feedback-presentation-bridge-contract.md)
  - [TD-014-demo-audio-runtime-contract.md](./TD-014-demo-audio-runtime-contract.md)
  - [TD-020-hint-notification-runtime-contract.md](./TD-020-hint-notification-runtime-contract.md)
  - [TD-022-in-world-dialogue-runtime-contract.md](./TD-022-in-world-dialogue-runtime-contract.md)
- related_adr: 중요 결정 없음 (ADR 신규 작성 없음)

> Runtime UI는 `uGUI` 단일 스택으로 고정하고, 기존 owner/bridge 경계를 유지한 채 `OnGUI` 표시를 `reader-only presenter`로 치환한다.

## 0. 구현 상태 요약
- 구현 완료 범위
  - `RuntimeUiRoot` 프리팹 + `SampleScene` / `PlayModeSmoke_Dedicated` 씬 고정 인스턴스 배치
  - Shell V1: `Title`, `Lobby`, `Result`, `DemoComplete`, shared `Settings(audio)`
  - Modal V2: `Pause`, `Confirm`
  - HUD V1: `Stage HUD`, `Notification`, `Hint`
- 현재 공개 빌드 경로에서 `Title/Lobby/Stage HUD/Result/DemoComplete/Settings/Pause/Confirm`는 `uGUI` 기준으로 동작한다.
- 미완료 범위
  - 온보딩 전용 힌트 시퀀스
  - `PresentationLayer` 기반 인월드 연출 대화
  - `FxLayer` 기반 화면 플래시/비네트/타임아웃 overlay
  - 접근성 확장 옵션(`UI Scale`, `Text Size`, `Hit Flash`, `Screen Shake`, `Background Motion`)

## 1. 목표 / 비목표
### 1.1 목표
- 공개 빌드 기준의 런타임 UI를 `uGUI` 단일 스택으로 고정한다.
- 현재 `OnGUI` 기반 `Title/Lobby/HUD/Result/Options`를 `Canvas + Panel + Presenter` 구조로 전환한다.
- 기존 owner를 유지한다.
  - 화면 상태/명령 owner: `DemoShellFlowController`
  - ECS 스냅샷 writer: 기존 시스템 유지
  - 오디오/피드백 writer: 기존 시스템 유지
- UI는 끝까지 `reader/presenter-only`로 유지한다.
- 입력 우선순위는 `키보드 + 마우스`로 고정하되, 포커스 기반 구조를 유지해 후속 패드 확장 비용을 낮춘다.

### 1.2 비목표
- Runtime UI Toolkit 도입
- UI를 owner로 승격하는 구조 변경
- Stage 흐름을 UI 상태 머신으로 재구성
- 고급 애니메이션 프레임워크, 데이터 바인딩 프레임워크 도입
- 패드-only gameplay 완주 지원

## 2. 기술 선택 고정
### 2.1 채택안
- Runtime UI: `uGUI`
- 입력 모듈: `EventSystem + InputSystemUIInputModule`
- 기본 토폴로지: `SampleScene` 고정 `RuntimeUiRoot`
- 전달 형태: `RuntimeUiRoot` 프리팹 자산을 만들고, 운영 씬/스모크 씬에는 `씬 고정 인스턴스`로 배치한다

### 2.2 기각안
- Runtime `UI Toolkit`
  - 기각 사유: 현재 목표가 "출시형 UI로의 빠른 전환"이며, 본 프로젝트는 이미 `OnGUI`와 브리지 구조가 강해 `uGUI`가 더 낮은 리스크를 가진다.
- 복수 UI 스택 혼용(`uGUI + UI Toolkit`)
  - 기각 사유: 운영/유지보수 복잡도 증가
- 화면별 씬 분리
  - 기각 사유: 현재 `DemoShellFlowController` 중심 전이 구조와 충돌하고 반복 재진입 비용이 커진다.

## 3. 소유권 (Owner / Reader)
- Owner 유지:
  - `DemoShellFlowController`
    - 화면 상태 전이
    - 버튼 입력에 대한 명령 처리
  - `PlayerHudSnapshotCollectSystem`
    - HUD 스냅샷 writer
  - `PlayerUiFeedbackConsumeSystem`, `PlayerImpulseConsumeSystem`
    - 피드백/임펄스 스냅샷 writer
  - `DemoAudioBridge`
    - 오디오 재생/볼륨 저장 owner 유지
- UI Reader / Presenter:
  - `RuntimeUiRoot`
    - 화면 패널 활성/비활성 조정
    - 공통 모달/레이어 조정
  - `TitleScreenPresenter`
  - `LobbyScreenPresenter`
  - `StageHudPresenter`
  - `PausePresenter`
  - `ResultPresenter`
  - `DemoCompletePresenter`
  - `SettingsPresenter`
  - `NotificationPresenter`
  - `HintPresenter`
  - `InWorldDialoguePresenter`
- 원칙:
  - UI는 ECS 컴포넌트 write 금지
  - UI는 `DemoShellFlowController`, `PlayerRuntimeHudBridge`, `DemoAudioBridge`를 read-only로 참조한다
  - 버튼 클릭은 owner public API 호출만 허용한다

## 4. Runtime UI 토폴로지
### 4.1 씬 루트
- `RuntimeUiRoot`를 `SampleScene`의 고정 GO로 둔다.
- `RuntimeUiRoot`는 프리팹으로 authoring하고, `SampleScene`, `PlayModeSmoke_Dedicated`에는 미리 배치된 인스턴스를 사용한다.
- 초기 단계에서는 런타임 instantiate를 사용하지 않는다.
- 운영 가드레일:
  - `Assets/_Project/04_Prefabs/UI/RuntimeUiRoot.prefab`을 공통 구조의 SSOT로 둔다.
  - `RuntimeUiRoot`의 공통 hierarchy, presenter 구성, 기본 레이아웃, 공통 스타일 변경은 prefab 자산에서 수정한다.
  - `SampleScene`, `PlayModeSmoke_Dedicated`의 씬 인스턴스는 prefab instance로 유지하고, scene override는 scene-specific binding 또는 배치 값으로 제한한다.
  - scene 인스턴스에서 공통 구조를 먼저 수정한 경우에도 최종 반영은 prefab override 적용으로 정리한다.
- 구성:
  - `Canvas`
  - `CanvasScaler`
  - `GraphicRaycaster`
  - `RuntimeUiRoot`
  - `EventSystem`
  - `InputSystemUIInputModule`
- 권장 기본값:
  - `Canvas.renderMode = ScreenSpaceOverlay`
  - `CanvasScaler = Scale With Screen Size`
  - 기준 해상도: `1920x1080`
  - 텍스트 계층은 `TMP_Text` 사용 권장

### 4.2 레이어 구조
- `ShellLayer`
  - `TitlePanel`
  - `LobbyPanel`
  - `ResultPanel`
  - `DemoCompletePanel`
- `HudLayer`
  - `StageHudPanel`
  - `NotificationPanel`
  - `HintPanel`
- `PresentationLayer`
  - `InWorldDialoguePanel`
- `ModalLayer`
  - `PausePanel`
  - `SettingsPanel`
  - `ConfirmDialogPanel`
- `FxLayer`
  - screen flash
  - hit vignette
  - timeout warning overlay

### 4.2.1 현재 HUD V1 구성
- `StageHudPanel`
  - `TopCenterObjectiveRoot`
    - `ObjectiveSummaryText`
    - `ObjectiveDetailText`
    - `TimerValueText`
    - `PressureSourceProgressRoot`
      - pressure progress bar
      - `WeakThresholdMarker`
      - `PressureSourceValueText`
  - `LeftCarryRoot`
    - `CarryLabel`
    - carry fill bar
    - `CarryValueText`
- `NotificationPanel`
  - `NotificationRoot`
  - `NotificationText`
- `HintPanel`
  - `HintRoot`
  - `HintText`

### 4.3 Presenter 구조
- `RuntimeUiRoot`
  - 참조 보유
  - 패널 활성/비활성
  - 기본 선택 대상/포커스 이동
  - 모달 stack 제어
- presenter는 패널 단위로 분리한다.
  - `TitleScreenPresenter`
  - `LobbyScreenPresenter`
  - `ResultPresenter`
  - `DemoCompletePresenter`
  - `StageHudPresenter`
  - `PausePresenter`
  - `SettingsPresenter`
  - `ConfirmDialogPresenter`
  - `NotificationPresenter`
  - `HintPresenter`
  - `InWorldDialoguePresenter`
  - `ScreenFxPresenter`
- 원칙:
  - `RuntimeUiRoot`는 얇은 coordinator로 유지한다.
  - 화면별 세부 로직은 패널 presenter가 가진다.

### 4.4 기본 화면 정책
- 한 시점에 `ShellLayer`의 주 패널은 1개만 활성
- `HudLayer`는 `StagePlay`에서만 활성
- `PresentationLayer`는 `StagePlay`와 `pre-result clear dialogue` 구간에서 활성 가능
- `ModalLayer`는 shell/hud 위에 중첩 가능
- `FxLayer`는 shell/hud/modal과 독립적으로 재생 가능
- Shell 패널은 `DemoShellFlowController.CurrentScreen`에 따라 전환한다.
- Modal은 shell 상태와 독립적으로 열고 닫되, 최종 명령은 owner에 위임한다.
- 인월드 연출 대화가 active면 lower-center `Notification` / `Hint`는 suppress 가능하다.

## 5. 데이터 연결 계약
### 5.1 Shell 화면
- 입력 source: `DemoShellFlowController`
- 표시 데이터:
  - `CurrentScreen`
  - `CurrentStageId`
  - `CurrentStageOutcome`
  - 현재 스테이지/세션 결과값
- 명령:
  - `RequestStartFromTitle()`
  - `RequestSelectStageById(stageId)`
  - `RequestResultAction(action)`
  - `RequestRestartDemo()`
  - `RequestReturnToLobbyFromComplete()`
  - `RequestQuit()`

### 5.2 HUD
- 입력 source:
  - `PlayerRuntimeHudBridge.TryGetLastSnapshot()`
  - `DemoShellFlowController` stage meta read
- 표시 데이터:
  - Carry
  - Source progress
  - Pressure source progress + weakened threshold marker
  - Stage timer/state
  - Objective / danger
  - feedback feed

### 5.3 Settings
- 입력 source:
  - `DemoAudioBridge`
- 표시/명령:
  - `GetBusVolume(bus)`
  - `SetBusVolume(bus, normalized)`
- 후속 확장:
  - UI scale
  - flash/shake intensity
  - input tooltip/guide visibility

### 5.4 Hint / Notification
- 입력 source:
  - `PlayerRuntimeHudBridge` feedback state
  - 온보딩/seen-state 공급자(후속 도입)
- 원칙:
  - gameplay writer를 추가하지 않고 기존 스냅샷/세션 상태를 읽는다
  - `Notification` / `Hint` 책임 분리와 재노출 정책은 [TD-020-hint-notification-runtime-contract.md](./TD-020-hint-notification-runtime-contract.md)를 SSOT로 둔다
  - 인월드 연출 대화 active 동안 lower-center lane suppress 규칙은 [TD-022-in-world-dialogue-runtime-contract.md](./TD-022-in-world-dialogue-runtime-contract.md)를 따른다

### 5.5 In-World Dialogue
- 입력 source:
  - `DemoShellFlowController`
  - `DemoShellDialogueBridge`
  - `StagePresentationRuntimeController`
- 원칙:
  - `PresentationLayer`는 `InWorldDialoguePresenter`를 통해 reader-only로 갱신한다
  - `StageClear` dialogue는 `ResultPanel`보다 먼저 노출될 수 있다
  - `DemoShellFlowController`가 전환 owner이며, UI는 상태 표시와 입력 전달만 담당한다

### 5.6 갱신 방식
- presenter는 매 프레임 전체 UI rebuild를 하지 않는다.
- `CurrentScreen`, `StageOutcome`, snapshot version, 마지막 표시값을 캐시해 변경 시점에만 표시를 갱신한다.
- HUD 수치와 feedback/toast는 폴링을 허용하되, 레이아웃 rebuild를 최소화한다.

### 5.7 현재 read-only bridge 계약
- `PlayerRuntimeHudBridge`
  - `TryGetLastSnapshot(out PlayerHudSnapshotComponent snapshot)`
  - `TryGetLastFeedbackSnapshot(out PlayerUiFeedbackPresentationSnapshotComponent snapshot)`
  - `LastFeedbackLine`
  - `RuntimeUiHudActive`
  - `SetRuntimeUiHudActive(bool active)`
- `PlayerRuntimeHudBridge.OnGUI()`는 `RuntimeUiHudActive == true`일 때 runtime HUD를 그리지 않는다.
- `PlayerHudSnapshotComponent`의 runtime UI 소비 필드:
  - `CarryLoad`, `CarryCapacity`
  - `DepletedSourceCount`, `TotalSourceCount`
  - `PressureSourceStableId`, `PressureSourceCollected`
  - `PressureSourceThresholdWeakened`, `PressureSourceThresholdDepleted`, `PressureSourceProgress01`
  - `StageState`, `StageStateElapsedSec`, `GameplayElapsedSec`
  - `LastHitLossValue`, `HitFlashRemainingSec`
  - `TotalCollectValue`, `TotalCleanupValue`, `TotalHitValue`

## 6. 입력 / 내비게이션 정책
### 6.1 우선순위
- 1차 입력 기준: `키보드 + 마우스`
- 메뉴는 마우스 사용성을 우선한다.
- 키보드 포커스 이동과 `Submit/Cancel` 경로는 유지한다.
- 패드-only 합격 기준은 현재 범위에서 제외한다.

### 6.2 EventSystem 계약
- `InputSystemUIInputModule`은 프로젝트의 `InputSystem_Actions.inputactions` 내 `UI` 액션맵을 사용한다.
- `Submit`, `Cancel`, `Navigate`, `Point`, `Click`을 표준 UI 입력으로 사용한다.
- `UI` 액션맵은 메뉴/모달/설정에 공통 적용한다.

### 6.3 KB+Mouse UX 기준
- Title: 클릭 또는 `Submit`
- Lobby: 버튼 클릭, 필요 시 화살표/WASD + `Submit`
- Dialogue: `Submit` 또는 좌클릭 advance, `Cancel` 또는 별도 skip binding
- Settings: 슬라이더 drag, 좌우 입력, `Cancel`로 닫기
- Pause/Confirm: `Cancel` 또는 명시 버튼으로 닫기

## 7. OnGUI 마이그레이션 정책
### 7.1 단계
1. `Shell` 우선
   - `Title`, `Lobby`, `Result`, `DemoComplete`, `Settings`
2. `Modal`
   - `Pause`, `Confirm`
3. `HUD / Hint / Fx`
   - `Stage HUD`, `HintToast`, `ScreenFx`
4. development-only fallback으로 `OnGUI`를 잠시 유지
5. 기능 parity와 smoke pass 확인 후 `OnGUI` 제거

### 7.2 기존 컴포넌트 처리
- `DemoShellFlowController.OnGUI()`
  - shell 화면/오디오 옵션 표시 제거 대상
- `PlayerRuntimeHudBridge.OnGUI()`
  - HUD 표시 제거 대상
- `DemoAudioBridge`
  - 옵션 UI 표시는 제거, 오디오 owner 역할만 유지

### 7.3 fallback 원칙
- fallback은 `UNITY_EDITOR`/`DEVELOPMENT_BUILD`에 한정한다.
- 공개 빌드에서는 `OnGUI`를 비활성하거나 제거한다.

### 7.4 현재 마이그레이션 결과
- `DemoShellFlowController`
  - Shell/Settings `OnGUI`는 `RuntimeUiShellActive` 기준으로 공개 빌드 경로에서 비활성
- `PlayerRuntimeHudBridge`
  - runtime HUD `OnGUI`는 `RuntimeUiHudActive` 기준으로 비활성
- `DemoAudioBridge`
  - 볼륨 owner만 유지, settings UI는 `SettingsPresenter`가 소비

## 8. 성능 / 리스크
### 8.1 성능 원칙
- UI는 엔티티 수에 비례하는 per-entity 접근 금지
- Presenter는 기존 singleton snapshot/owner만 읽는다
- HUD 갱신은 필요 시점에만 수행하고, 레이아웃 rebuild를 과도하게 유발하지 않는다

### 8.2 리스크
- 리스크 1: UI presenter가 owner 역할까지 침범할 위험
  - 대응: 모든 명령은 `DemoShellFlowController` public API 호출만 허용
- 리스크 2: `OnGUI`와 `uGUI` 병행 기간에 중복 표시/입력 충돌이 생길 위험
  - 대응: stage별 migration 동안 표시 가드와 build define를 분리
- 리스크 3: 마우스 중심 UX와 키보드 포커스 경로가 따로 놀 위험
  - 대응: 기본 선택 대상과 `Submit/Cancel` 규칙을 문서/테스트로 고정

## 9. 검증 계획 / 합격 기준
- 공통
  1. compile
  2. console error 0
  3. EditMode pass
  4. PlayMode smoke pass
- TD-016 추가 검증
  - `RuntimeUiRoot`가 `DemoShellFlowController`와 정상 연결된다
  - `Title -> Lobby -> Stage -> Result -> DemoComplete` 화면 전환이 `uGUI`에서 정상 노출된다
  - `RuntimeUiRoot` 프리팹 인스턴스가 운영 씬과 스모크 씬에서 동일 구조를 유지한다
  - HUD가 `PlayerHudSnapshotComponent` 값을 표시한다
  - Settings가 `DemoAudioBridge` 볼륨 값을 읽고 즉시 반영한다
  - `Pause/Confirm` 모달이 shell/hud 위에서 정상 동작한다
  - `PresentationLayer`가 `HudLayer`와 독립적으로 활성/비활성된다
  - 인월드 연출 대화 active 동안 `Notification/Hint`가 숨겨지고, clear dialogue 완료 후에만 `ResultPanel`이 열린다
  - 공개 빌드에서 `OnGUI` 경로가 비노출이다
  - 마우스 클릭과 키보드 `Submit/Cancel` 모두 기본 경로에서 동작한다

### 9.1 최신 검증 결과
- 2026-03-13 기준 확인 완료
  - compile 성공
  - Unity Console `error` 0건
  - EditMode `190/190` 통과
  - PlayMode 개별 스모크 통과
    - `PlayMode_OperationalScene_RuntimeUiRoot_ShellPanelsFollowShellFlow`
    - `PlayMode_OperationalScene_RuntimeUiRoot_SettingsAudio_ApplyAndPersist`
    - `PlayMode_OperationalScene_RuntimeUiRoot_PauseResumeAndSettings_Work`
    - `PlayMode_OperationalScene_RuntimeUiRoot_PauseRestartAndReturnToLobby_Work`
    - `PlayMode_OperationalScene_RuntimeUiRoot_PauseIsBlockedOutsideStagePlay`
    - `PlayMode_OperationalScene_RuntimeUiRoot_HudVisibilityAndPauseLayering_Work`
    - `PlayMode_OperationalScene_RuntimeUiRoot_HudPresenter_ReflectsDangerAndToast`
    - `PlayMode_DedicatedScene_RuntimeUiRoot_Exists`
- 참고
  - 전체 PlayMode 스위트는 별도 `BlobAsset/SubScene unload` 계열 불안정성이 남아 있어 TD-016 합격 판정은 개별 Runtime UI 스모크 기준으로 관리한다.

## 10. 오픈 이슈
- `Pause`가 `Time.timeScale`을 직접 사용할지, fixed tick gate 기반으로 처리할지는 후속 TD에서 확정 필요
- 화면 해상도/안전 영역/UI scale 옵션의 세부 기본값은 `TD-017`에서 확정
- VFX overlay와 UI warning 계층 충돌 규칙은 `TD-018`에서 확정

## 11. 변경 이력
- 2026-03-12: 초안 작성. Runtime UI를 `uGUI` 단일 스택으로 고정하고, `OnGUI -> reader-only presenter` 마이그레이션 구조와 입력/내비게이션 기준을 정리했다.
- 2026-03-12: 구현 권장안을 반영해 `RuntimeUiRoot` 프리팹 + 씬 고정 인스턴스, `Shell -> Modal -> HUD/Fx` 마이그레이션 순서, `root coordinator + panel presenter` 구조를 추가로 고정했다.
- 2026-03-13: 구현 반영. `Shell V1`, `Modal V2`, `HUD V1`의 실제 프리팹/패널/bridge 계약과 최신 검증 결과를 문서에 반영하고, `Pressure Source` 진행 바와 weakened threshold marker를 HUD V1 범위에 포함시켰다.
- 2026-03-16: `HUD V1` 재배치와 후속 설계 반영. `HintToast`를 `Notification` / `Hint` 2레인 구조로 교체하고, `TD-020`을 `Hint/Notification V2` SSOT로 연결했다.
- 2026-03-16: `TD-022` 연계 반영. `PresentationLayer`, `InWorldDialoguePresenter`, dialogue 입력/표시 우선순위, lower-center lane suppress 규칙을 추가했다.
- 2026-03-17: `RuntimeUiRoot` 운영 가드레일을 추가했다. 공용 UI 구조의 SSOT를 `RuntimeUiRoot.prefab`으로 고정하고, agent와 씬 작업은 prefab 우선 / scene-specific override 제한 규칙을 따른다.
