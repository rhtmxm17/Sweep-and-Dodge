# ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate
> 인월드 연출 대화를 GO 전용 계층으로 두고, 시작은 overlay 기본값, 클리어는 `Result` 전 clear gate로 처리하는 결정

## 상태
- 제안됨 (문서 초안)

## 배경
- `GD-011`은 인월드 연출 대화를 `시작/클리어/전환` 중심의 약차단형 계층으로 정의한다.
- 현재 구현은 `StageStart`에서 `IntroPresentationDone=true`를 즉시 기록하고, `ClearReady`에서 곧바로 `StageResult`로 넘어간다.
- 이 구조에서는 월드를 유지한 클리어 대화를 `Result` 전에 재생할 자리가 없다.
- 반면 `RunDirectorStageBridge`와 ECS stage state에는 `IntroPresentationDone`, `ClearPresentationDone` gate가 이미 존재한다.
- stage presentation anchor는 이미 `StagePresentationRuntimeController`가 stableId 기반으로 공급할 수 있다.

## 결정
1. Runtime 계층 선택
- 인월드 연출 대화는 새 ECS gameplay writer를 만들지 않는다.
- `DemoShellFlowController` + `DemoShellDialogueBridge` + `InWorldDialoguePresenter`의 GO 전용 계층으로 운영한다.

2. StageStart 정책
- v1 기본값은 `OverlayOnly`다.
- 시작 대화는 `Running`과 병행 가능하며, 플레이 진입 템포를 우선한다.
- 단, 데이터 모델은 후속 확장을 위해 `GateIntro`를 표현 가능하게 유지한다.

3. StageClear 정책
- `ClearReady`에서 즉시 `StageResult`로 넘어가지 않는다.
- 월드 위에서 clear dialogue를 재생한 뒤 `ClearPresentationDone=true`와 `RequestConfirm()`를 통해 `Completed`로 진행한다.
- `StageRunCompleted` 수신 후에만 `StageResult`로 전환한다.

4. Anchor / presentation 경계
- 월드 bubble anchor는 기존 `StagePresentationRuntimeController`의 stableId 기반 spawned presentation을 재사용한다.
- dialogue active 동안 lower-center `Hint/Notification`은 suppress한다.

5. v1 범위
- `StageStart`, `StageClear`, `ThemeTransition`
- 튜토리얼 예외 개입은 이번 결정 범위에서 제외한다.

## 대안
- 대안 1: 시작/클리어 모두 overlay
  - 장점: 구현이 단순하고 기존 `DemoShellFlowController` 변경량이 적다.
  - 단점: `Result` 전 클리어 대화의 존재감이 약해지고 `GD-011`의 전환 감정 정리 목적이 흐려진다.

- 대안 2: 시작/클리어 모두 gate
  - 장점: 전환 연출 구조가 균일하고 상태 모델이 단순하다.
  - 단점: 시작 템포가 무거워지고 데모 진입 속도가 느려진다.

- 대안 3: ECS 이벤트 큐 기반 대화 요청 시스템
  - 장점: 튜토리얼 예외 개입까지 하나의 이벤트 파이프라인으로 확장하기 쉽다.
  - 단점: 현재 범위에 비해 구조가 무겁고, presentation 계층이 gameplay writer 경계를 침범할 가능성이 커진다.

## 결과
- 클리어 대화를 `Result` 전 월드 위에서 재생하는 기준이 고정된다.
- 시작은 overlay 기본값을 채택해 데모 진입 템포를 유지한다.
- 기존 `RunDirector` gate를 clear 단계에서 실제로 활용하게 된다.
- `ClearReady -> Result` 직행 구조를 더 이상 전제할 수 없으므로 shell/result timing과 metrics snapshot 시점을 함께 재정렬해야 한다.

## 후속
1. `TD-022`를 SSOT로 삼아 dialogue catalog, runtime state, presentation layer 계약을 세부화한다.
2. `DemoShellFlowController`의 clear defer와 result timing snapshot 정책을 구현 설계로 구체화한다.
3. EditMode/PlayMode 테스트에 `pre-result clear dialogue` 회귀 케이스를 추가한다.
