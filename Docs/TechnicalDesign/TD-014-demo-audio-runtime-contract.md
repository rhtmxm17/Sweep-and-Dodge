# 데모 오디오 런타임 계약 (TD-014)

## Metadata
- doc_id: `TD-014`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-05`
- related_docs:
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
  - [TD-013-player-feedback-presentation-bridge-contract.md](./TD-013-player-feedback-presentation-bridge-contract.md)
- related_adr: 중요 결정 없음 (ADR 신규 작성 없음)

> OPS-002 S5 최소 범위에서 `DemoAudioBridge`를 기준으로 데모 사운드 라우팅/옵션/중복 억제 규칙을 고정한다.

## 1. 목표 / 비목표
### 1.1 목표
- 오디오 소비 경로를 GO reader-only 브리지(`DemoAudioBridge`)로 고정한다.
- 필수 버스(`Master/BGM/SFX/UI`)와 필수 cue(`UI/Stage/Combat`)를 계약으로 고정한다.
- 이벤트 중복 재생 방지 규칙(snapshot version/delta/cooldown)을 고정한다.
- 런타임 볼륨 옵션(`0..1`)의 즉시 반영 + 재시작 복원을 보장한다.

### 1.2 비목표
- 오디오 미들웨어(FMOD/Wwise) 도입
- 공간 오디오, 리버브 존, 고급 믹싱 자동화
- ECS writer 경로 확장

## 2. 소유권 (Writer / Reader)
- Reader-only 브리지: `DemoAudioBridge`
  - 읽기 입력:
    - `DemoShellFlowController.CurrentScreen/CurrentStageOutcome`
    - `PlayerUiFeedbackPresentationSnapshotComponent.Version/Type`
    - `PlayerHudSnapshotComponent.TotalCollectValue/TotalCleanupValue/TotalHitValue`
  - 쓰기 대상:
    - Unity AudioSource 재생 상태/볼륨
    - 로컬 옵션 저장(PlayerPrefs)
- 원칙:
  - ECS 컴포넌트 write 금지
  - Stage/HUD/Feedback 기존 writer를 변경하지 않는다

## 3. 버스 / 큐 계약
### 3.1 버스
- `Master`
- `BGM`
- `SFX`
- `UI`

### 3.2 큐
- UI:
  - `UiStart`, `UiSelect`, `UiBack`, `UiConfirm`
- Stage:
  - `StageEnter`, `StageClear`, `StageFail`, `DemoComplete`
- Combat:
  - `Hit`, `Collect`, `Cleanup`

## 4. 전이/이벤트 라우팅 규칙
- 화면 전이 기반:
  - `Title -> Lobby` => `UiStart`
  - `Lobby -> StagePlay` => `UiSelect`, `StageEnter`
  - `StagePlay -> StageResult` => `UiConfirm` + (`StageClear` or `StageFail`)
  - `* -> DemoComplete` => `DemoComplete`
  - `* -> Lobby`(Title 제외) => `UiBack`
- 전투 이벤트 기반:
  - `PlayerUiFeedbackPresentationSnapshot.Type=PlayerHazardHit` version 증가 => `Hit`
  - `TotalCollectValue` delta > 0 => `Collect`
  - `TotalCleanupValue` delta > 0 => `Cleanup`

## 5. 중복 억제 / 믹스 규칙
- Hit:
  - snapshot `Version` 증가 시 1회 재생
  - 재생 시 BGM ducking 적용
- Collect/Cleanup:
  - 누적 total delta 기준 트리거
  - 각 cue cooldown 기본값 `0.05s`
- BGM ducking:
  - gain 기본값 `0.65`
  - 유지 기본값 `0.15s`

## 6. 옵션 정책
- 버스 볼륨:
  - `Master/BGM/SFX/UI` 각각 `0..1` clamp
- 반영:
  - 런타임 즉시 반영
- 저장:
  - PlayerPrefs 키
  - 앱 재시작/씬 재진입 후 복원

## 7. Source/Clip 기본 세팅
- Source 토폴로지:
  - 하이브리드(씬 프리와이어 우선 + 누락 시 런타임 자동 생성)
  - 대상: `BgmSource`, `SfxSource`, `UiSource`
- Source 기본 정책:
  - 공통: `playOnAwake=false`, `spatialBlend=0`, `dopplerLevel=0`, `reverbZoneMix=0`
  - BGM: `loop=true`, priority `128`
  - SFX/UI: `loop=false`, priority `96/64`
- Fallback clip 정책:
  - `AutoAssignFallbackClips=true`일 때 null 슬롯만 런타임 임시 톤 clip 할당
  - 범위: BGM 4종, UI 4종, Stage 4종, Combat 3종
  - 기존 실클립이 할당된 슬롯은 절대 덮어쓰지 않는다
  - 런타임 생성 clip은 브리지 destroy 시 정리한다
- 누락 경고 정책:
  - `LogMissingAudioBinding=true`일 때 미바인딩 cue를 warn once로 기록한다

## 8. 데이터 구조 / 공개 API
- `DemoAudioTypes.cs`
  - `DemoAudioBusId`
  - `DemoAudioCueId`
- `DemoAudioBridge.cs`
  - `AutoCreateMissingSources`
  - `AutoAssignFallbackClips`
  - `LogMissingAudioBinding`
  - `SetBusVolume(DemoAudioBusId bus, float normalized)`
  - `GetBusVolume(DemoAudioBusId bus)`

## 9. 검증 계획 / 합격 기준
- 공통:
  1. `refresh_unity(compile=request, wait_for_ready=true)`
  2. `read_console(action=get, types=["error"], include_stacktrace=true)` error 0
  3. `EditMode` 테스트 통과
  4. `PlayMode` 전용 씬 스모크 통과
- S5 추가:
  - Source 누락 시 자동 보정(BGM/SFX/UI) 테스트 통과
  - Fallback clip 자동 할당/기존 clip 보존 테스트 통과
  - Screen transition cue 매핑 테스트 통과
  - 버스 볼륨 clamp/조회 및 즉시 반영 테스트 통과
  - 볼륨 옵션 씬 재진입 복원 테스트 통과
  - 기존 데모 루프/플레이 HUD/피드백 테스트 회귀 없음

## 10. ADR 연계
- 본 변경은 ECS Writer/Group/Fence 규칙을 변경하지 않는다.
- 따라서 ADR 신규 작성은 생략한다.
