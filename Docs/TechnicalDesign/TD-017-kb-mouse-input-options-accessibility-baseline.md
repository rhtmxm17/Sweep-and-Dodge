# KB+Mouse 입력 / 옵션 / 접근성 기본선 (TD-017)

## Metadata
- doc_id: `TD-017`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-03-12`
- related_docs:
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [OPS-003-public-release-readiness-plan.md](../ProjectOps/OPS-003-public-release-readiness-plan.md)
  - [TD-012-player-cleanup-action-runtime-contract.md](./TD-012-player-cleanup-action-runtime-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](./TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](../ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)
- related_adr: 중요 결정 없음 (ADR 신규 작성 없음)

> 공개 빌드 1차 기준 입력 축은 `키보드 + 마우스`로 고정하고, UI 입력/옵션/접근성 최소선을 `출시형 Runtime UI`와 함께 운영 가능한 범위로 정리한다.

## 1. 목표 / 비목표
### 1.1 목표
- 공개 빌드의 1차 조작 기준을 `키보드 + 마우스`로 고정한다.
- `UI 입력`과 `gameplay 입력`의 책임 경계를 분리한다.
- 공개 빌드에 반드시 포함할 옵션 항목과 접근성 기본선을 고정한다.
- `TD-016`의 `uGUI + InputSystemUIInputModule` 구조와 호환되는 입력/내비게이션 기준을 제공한다.
- 후속 패드 확장을 막지 않는 수준의 포커스/Submit/Cancel 경로를 유지한다.

### 1.2 비목표
- 패드-only gameplay 지원
- 입력 장치별 완전 리맵 시스템 1차 도입
- Steam Input 공식 설정 발행
- 스크린 리더/내레이션 지원
- 플랫폼별 접근성 기능 전체 구현

## 2. 현재 상태(코드 기준)
- gameplay 입력은 아래 Mono 경로가 직접 `Input` API를 읽는다.
  - `PlayerWasdMovement`
    - `W/A/S/D` 이동
    - `Input.mousePosition` 기반 월드 조준점 계산
  - `PlayerEcsBridge`
    - `Input.GetMouseButtonDown()` 기반 청소 액션 입력
- shell 입력은 현재 `DemoShellFlowController.ProcessKeyboardFallback()`이 `Input.anyKeyDown`, 숫자키, `N/R/L/Q/Escape` 등을 직접 읽는다.
- UI 입력 자산은 이미 존재한다.
  - `Assets/InputSystem_Actions.inputactions`
  - `UI` 액션맵(`Navigate`, `Submit`, `Cancel`, `Point`, `Click` 등)
- 프로젝트는 `activeInputHandler = 2`(`Both`) 상태다.

## 3. 입력 책임 경계
### 3.1 1차 채택안
- UI 입력:
  - `Input System`
  - `EventSystem + InputSystemUIInputModule`
- gameplay 입력:
  - 기존 `Input` API 기반 경로를 1차 공개 빌드까지 유지
  - 대상:
    - `PlayerWasdMovement`
    - `PlayerEcsBridge`
- 이유:
  - 런타임 UI 전환과 gameplay 입력 경로 전면 이관을 같은 스트림에서 동시에 수행하면 회귀 범위가 커진다.
  - 현재 gameplay는 `mouse world aim`과 replay 억제 경로가 이미 얽혀 있어, 공개 직전에는 입력 구조 전체 이관보다 UX/옵션 정리가 우선이다.

### 3.2 Shell 입력 처리
- `DemoShellFlowController.ProcessKeyboardFallback()`는 출시형 UI 전환 후 제거 대상이다.
- shell 명령은 버튼 클릭 또는 UI 표준 입력(`Submit`, `Cancel`)을 통해 owner public API를 호출한다.
- `Title`, `Lobby`, `Result`, `DemoComplete`, `Settings`, `Pause`는 모두 `uGUI` 경로만 사용한다.

### 3.3 gameplay 입력 처리
- 이동:
  - 기본: `W/A/S/D`
- 조준:
  - 기본: `Mouse Position -> world aim point`
- 청소 액션:
  - 기본: `Left Mouse Button`, `Right Mouse Button`
- system 입력:
  - `Escape`: Pause / Back
- 슬롯 기반 액션 해석(`Input -> Slot -> ActionId`)은 `TD-012` 계약을 유지한다.

## 4. KB+Mouse UX 기본선
### 4.1 공통
- 마우스 커서는 메뉴/UI에서 항상 표시한다.
- gameplay 중 마우스 커서는 조준 정보의 일부로 취급하며 숨기지 않는다.
- 버튼/슬라이더/토글은 hover, pressed, disabled 상태를 시각적으로 구분한다.
- 클릭 가능한 UI는 텍스트만이 아니라 박스/배경/아이콘으로도 구분한다.

### 4.2 메뉴 / 모달
- 마우스 클릭이 1차 경로다.
- 키보드 보조 경로:
  - `W/S` 또는 `Up/Down`: 선택 이동
  - `A/D` 또는 `Left/Right`: 값 변경
  - `Enter` 또는 `Space`: Submit
  - `Escape`: Cancel / Back / Close
- 포커스 이동 순서는 위->아래, 왼쪽->오른쪽 기준으로 예측 가능해야 한다.
- 첫 진입 시 기본 선택 대상이 존재해야 한다.

### 4.3 플레이 중 UI
- `Pause`는 `Escape`로 열고 닫는다.
- `Settings`는 Pause 내부에서 진입한다.
- `Result`와 `DemoComplete`는 마우스 클릭을 우선하되, `Submit/Cancel` 보조 경로를 유지한다.
- gameplay 중 조작 가이드는 현재 입력 장치 기준 텍스트로 표기한다.

## 5. 옵션 기본선
### 5.1 필수 옵션 (P0)
- Audio
  - `Master`
  - `BGM`
  - `SFX`
  - `UI`
- Display
  - `Fullscreen / Windowed`
  - `Resolution`
- UI / Accessibility
  - `UI Scale`
  - `Text Size`
  - `Hit Flash Intensity`
  - `Screen Shake Intensity`
  - `Background Motion` 또는 `Ambient Motion`
  - `Tutorial / Hint Visibility`

### 5.2 선택 옵션 (P1 이후)
- 색상 프리셋 / 고대비 프리셋
- 커서 크기 / 커서 강조
- 마우스 감도(조준 방식 변경 시)
- gameplay key remap

### 5.3 저장 정책
- 옵션은 로컬 저장을 기본으로 한다.
- 기존 `DemoAudioBridge`의 볼륨 저장 경로와 충돌하지 않도록 통합 키 정책이 필요하다.
- 1차 공개 빌드에서는 `씬 재진입`, `재실행` 후 복원을 합격 기준으로 둔다.

## 6. 접근성 기본선
### 6.1 텍스트
- 기본 UI 텍스트는 출시 시점 기본값으로 충분히 읽을 수 있어야 한다.
- 텍스트는 이미지 위에 직접 두지 않고, 필요 시 불투명 배경판을 둔다.
- 장식용 폰트가 있더라도 UI 본문은 읽기 쉬운 sans-serif 계열을 사용한다.
- 중요한 텍스트(`HUD`, `튜토리얼`, `옵션`, `결과`)는 `Text Size` 옵션의 영향을 받는다.

### 6.2 대비 / 가독성
- 텍스트와 배경은 높은 대비를 유지한다.
- 색만으로 상태를 구분하지 않는다.
  - 예: 위험 상태는 색 + 아이콘 + 텍스트 병행
- 경고/피격/정리/Deposit 가능 상태는 최소 2개 채널로 전달한다.

### 6.3 움직임 / 플래시
- `Screen Shake Intensity`, `Hit Flash Intensity`, `Background Motion` 제어 옵션을 제공한다.
- 자동으로 움직이거나 깜박이는 UI는 끄거나 약화할 수 있어야 한다.
- 텍스트를 읽어야 하는 화면에서는 과도한 배경 움직임을 피한다.

### 6.4 시간 제한 / 힌트
- 튜토리얼/힌트/피드백 텍스트는 플레이어가 읽을 수 있는 최소 시간을 확보한다.
- 자동 소멸하는 문구는 필요 시 다시 확인 가능한 경로(반복 힌트 또는 옵션)를 둔다.
- 타임아웃 경고는 마지막 순간 1회가 아니라 단계적으로 제공한다.

## 7. 구현 구조
### 7.1 UI 입력
- `RuntimeUiRoot`
  - `EventSystem`
  - `InputSystemUIInputModule`
- `UI` 액션맵 사용:
  - `Navigate`
  - `Submit`
  - `Cancel`
  - `Point`
  - `Click`

### 7.2 gameplay 입력
- `PlayerWasdMovement`
  - 이동축, 마우스 aim publish
- `PlayerEcsBridge`
  - 마우스 버튼 -> 슬롯 요청 publish
- replay 억제 로직은 기존대로 유지한다.

### 7.3 옵션 적용 지점
- Audio:
  - `DemoAudioBridge`
- UI / Accessibility:
  - `RuntimeUiRoot` 및 presenter 계층
  - `VFX/UI warning presenter` 계층
- Display:
  - 전용 설정 owner(후속 도입)

## 8. 마이그레이션 단계
1. shell 입력을 `InputSystemUIInputModule` 경로로 이관
2. `DemoShellFlowController.ProcessKeyboardFallback()` 제거
3. 옵션 UI를 `SettingsPresenter`로 이관
4. `UI Scale`, `Text Size`, `Flash/Shake/Motion` 기본 옵션 연결
5. gameplay 입력 구조는 공개 빌드 1차에서 유지
6. 후속 세션에서 필요 시 gameplay `Input System` 전면 이관 검토

## 9. 성능 / 리스크
### 9.1 리스크
- 리스크 1: UI는 `Input System`, gameplay는 `Input` API를 써서 혼용 복잡도가 생길 위험
  - 대응: 공개 빌드 1차는 역할을 명확히 분리하고, UI 전환 안정화 후 gameplay 이관 여부를 재평가
- 리스크 2: 접근성 옵션이 VFX/UI 각 계층에 흩어질 위험
  - 대응: 설정 owner와 적용 지점을 문서로 먼저 고정
- 리스크 3: 옵션 항목을 과도하게 늘려 구현만 복잡해질 위험
  - 대응: 본 문서의 필수 옵션만 `P0`로 제한

### 9.2 운영 원칙
- 입력 구조 변경보다 조작 이해도/읽기 쉬움/옵션 복원을 우선한다.
- 패드 확장성은 "막지 않는 수준"까지만 확보한다.

## 10. 검증 계획 / 합격 기준
- 공통
  1. compile
  2. console error 0
  3. EditMode pass
  4. PlayMode smoke pass
- TD-017 추가 검증
  - 마우스 클릭으로 모든 shell/menu/settings 경로를 탐색 가능
  - 키보드 `Submit/Cancel`과 기본 포커스 이동이 동작
  - gameplay 중 `W/A/S/D`, 마우스 조준, 마우스 버튼 입력이 회귀 없이 유지
  - `Escape`로 Pause 진입/복귀 가능
  - `Master/BGM/SFX/UI` 볼륨 저장/복원
  - `UI Scale`, `Text Size`, `Hit Flash`, `Screen Shake`, `Background Motion`, `Hint Visibility` 옵션 저장/즉시 반영
  - 텍스트 가독성과 대비가 기본 해상도(`1920x1080`)에서 유지

## 11. 오픈 이슈
- gameplay 입력을 `Input System`으로 전면 이관할지 여부는 공개 빌드 1차 이후 재평가
- `Resolution`/`Fullscreen` 설정 owner와 적용 타이밍은 후속 구현에서 확정 필요
- 키 리맵을 `P1`로 둘지 `OPS-003` 범위로 넘길지는 공개 채널 결정 후 재논의

## 12. 변경 이력
- 2026-03-12: 초안 작성. `KB+Mouse`를 1차 공개 빌드 입력 기준으로 고정하고, UI 입력과 gameplay 입력의 역할 분리, 필수 옵션, 접근성 최소선을 정리했다.
