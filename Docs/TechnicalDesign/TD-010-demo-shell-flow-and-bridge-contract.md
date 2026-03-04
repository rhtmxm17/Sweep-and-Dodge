# 데모 셸 플로우 및 브리지 계약 (TD-010)

## Metadata
- doc_id: `TD-010`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-04`
- related_docs:
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)

> `Title -> Lobby -> Stage Play -> Stage Result -> Demo Complete` 데모 셸 플로우의 런타임 소유권, 브리지 계약, 씬 재진입 계약을 정의한다.

## 1. 목표 / 비목표
### 1.1 목표
- 임시 호출 주체 없이 Demo Shell이 화면 전이와 사용자 선택을 단일 소유한다.
- ECS Stage 상태 읽기/요청 쓰기는 `RunDirectorStageBridge` 단일 접점으로 통일한다.
- `Retry/Next/Return` 재진입은 씬 재로드 + one-shot staging으로 일관 처리한다.
- `Stage3 Next -> Demo Complete` 규칙을 런타임 계약으로 고정한다.

### 1.2 비목표
- Fail 판정/Fail Result UX 확장 (S3 범위)
- Stage별 난이도 수치/스폰 밸런스 차별화
- uGUI/UIToolkit 기반 시각 고도화

## 2. 소유권 (Owner / Writer)
- Demo Shell Owner: `DemoShellFlowController` (GO)
  - 화면 상태(`Title/Lobby/StagePlay/StageResult/DemoComplete`) 전이 소유
  - 로비 선택/결과 선택/데모 완료 선택 후속 동작 소유
- ECS Stage Writer: 기존 ECS 시스템 유지
  - `RunDirectorStageTransitionSystem`이 `StageStartRequested`, `ConfirmPressed`, `StageRunCompleted` 소비/리셋
- GO->ECS 접점: `RunDirectorStageBridge`
  - 요청 쓰기: `RequestStageStart`, `RequestConfirm`, `SetIntroPresentationDone`, `SetClearPresentationDone`
  - 상태 읽기: `TryGetStageState(out RunDirectorStageStateComponent)`
- 씬 재로드 경계 전달: `DemoShellSessionStaging`
  - `StageLobby`/`StageStagePlay` 요청을 1회 전달하고 로드 후 즉시 소비한다.

## 3. 업데이트 순서 / 전이 계약
- ECS 파이프라인 순서는 기존 계약을 유지한다.
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- Demo Shell 전이:
  1. `Title -> Lobby`
  2. `Lobby -> StagePlay` (Stage1~3 선택)
  3. `StagePlay -> StageResult` (`ClearReady` 감지 시)
  4. `StageResult` 선택 시 `SetClearPresentationDone(true)` + `RequestConfirm()`
  5. `Completed` 신호 수신 후
     - `Next` (Stage1/2): 다음 Stage staging + 씬 재로드
     - `Next` (Stage3): `DemoComplete`
     - `Retry`: 동일 Stage staging + 씬 재로드
     - `Return to Lobby`: Lobby staging + 씬 재로드
  6. `DemoComplete`에서 `Restart Demo`, `Return to Lobby`는 모두 Lobby staging + 씬 재로드

## 4. 데이터 구조 및 제약
- `DemoShellScreenId`: `Title`, `Lobby`, `StagePlay`, `StageResult`, `DemoComplete`
- `DemoShellResultActionId`: `NextStage`, `Retry`, `ReturnToLobby`
- `DemoShellStageProfile`: `StageId`, `DisplayName`, `IsFinalStage`
- S1 Stage Profile 깊이:
  - 플로우 메타만 포함한다.
  - 난이도 파라미터 연결은 후속 스트림으로 이월한다.
- `Clear` 루프 고정:
  - S1에서 Result는 `ClearReady` 기반 진입만 다룬다.
  - Fail 루프는 S3에서 분리 설계한다.

## 5. 씬/서브씬 운영 기준
- `SampleScene`은 Demo Shell 운영 씬이다.
  - `InitialStageState = Idle`
- `PlayModeSmoke_Dedicated`는 파이프라인/성능 스모크 전용 씬이다.
  - 전용 SubScene 분리
  - `InitialStageState = Running`
- 두 씬은 같은 SubScene GUID를 공유하지 않는다.

## 6. 작업 분해 / 진행 상태
1. `RunDirectorStageBridge` 상태 조회 API 추가 (`완료`)
2. `DemoShellFlowController`, `DemoShellSessionStaging`, shell 타입 추가 (`완료`)
3. `RunDirectorStageTempFlowDriver` 코드/씬 참조 제거 (`완료`)
4. SampleScene Demo Shell 연결 (`완료`)
5. PlayMode 전용 SubScene 분리 (`완료`)
6. PlayMode 핵심 3 시나리오 테스트 반영 (`완료`)

## 7. 검증 계획 / 합격 기준
- 공통:
  1. `refresh_unity(compile=request, wait_for_ready=true)`
  2. `read_console(action=get, types=["error"], include_stacktrace=true)` error 0
  3. `EditMode` 테스트 통과
  4. `PlayMode` 테스트 통과
- S1 추가 합격:
  - `Title -> Lobby -> Stage -> Result` 진입 확인
  - `Result -> Retry` 동일 Stage 재진입 확인
  - `Stage3 Result Next -> DemoComplete -> Lobby` 확인
  - 기존 전용 스모크(파이프라인/리플레이/스트레스) 회귀 없음

## 8. 관련 ADR
- 본 작업은 ECS Writer/Group/Fence 규칙 변경이 없으므로 ADR 신규 작성은 생략한다.
