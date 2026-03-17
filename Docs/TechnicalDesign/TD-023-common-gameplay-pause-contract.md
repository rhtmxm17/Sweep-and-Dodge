# 공통 Gameplay Pause 계약 (TD-023)

## Metadata
- doc_id: `TD-023`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-03-17`
- related_docs:
  - [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](./TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](./TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [TD-017-kb-mouse-input-options-accessibility-baseline.md](./TD-017-kb-mouse-input-options-accessibility-baseline.md)
  - [TD-022-in-world-dialogue-runtime-contract.md](./TD-022-in-world-dialogue-runtime-contract.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
- related_adr:
  - [ADR-20260317-01-fixed-tick-authoritative-stageplay-and-common-gameplay-pause.md](../ADR/ADR-20260317-01-fixed-tick-authoritative-stageplay-and-common-gameplay-pause.md)

> `StagePlay` gameplay 시간은 fixed tick을 authority로 사용하고, pause는 `Acquire/Release` 기반 공통 owner가 집계한 상태를 통해 simulation/input/presentation을 분리 제어한다.

## 1. 목표 / 비목표
### 1.1 목표
- pause menu, `StageClear` dialogue, 이후 cutscene이 같은 계약으로 gameplay pause를 요청할 수 있게 한다.
- UI modal owner와 simulation pause owner를 분리한다.
- `StagePlay` gameplay 시간원을 `Unity Update deltaTime`과 분리된 fixed tick authority로 고정한다.
- pause 중 world simulation, stage timer, timeout 판정, result elapsed가 함께 멈추도록 계약을 정리한다.
- `StageStart=overlay`처럼 gameplay를 멈추지 않는 연출도 같은 runtime contract 안에서 표현 가능하게 한다.

### 1.2 비목표
- `Time.timeScale` 기반 전역 정지
- UI가 ECS gameplay writer를 직접 소유하는 구조 변경
- cutscene 카메라, 보이스, timeline 전체 시스템을 이번 문서에서 확정
- pause 기능 구현과 무관한 전면 리플레이 재설계

## 2. 핵심 원칙
### 2.1 gameplay 시간 권한
- `StagePlay` gameplay 로직은 fixed tick time source를 authority로 사용한다.
- gameplay ECS는 `SystemAPI.Time.DeltaTime` 직접값이 아니라 해석된 logic tick delta만 소비한다.
- pause는 logic tick 생성 정지로 정의한다.

### 2.2 owner 분리
- pause menu는 modal/UI owner다.
- gameplay pause 집계와 simulation 정지는 공통 gameplay pause owner가 소유한다.
- ECS time source 반영은 단일 writer가 수행한다.

### 2.3 중첩 pause 허용
- pause requester는 복수 동시 활성화를 허용한다.
- `PauseMenu`, `DialogueGate`, `Cutscene`, `Debug`가 동시에 존재할 수 있다.
- release는 handle 기반으로 개별 해제한다.

### 2.4 presentation 시간 분리
- world simulation이 멈춰도 UI/presenter 시간은 계속 흐를 수 있다.
- `StageClear` dialogue auto-advance/skip 쿨다운은 `unscaledDeltaTime` 기준을 유지한다.

## 3. 소유권 (Owner / Reader / Writer)
### 3.1 pause requester
- `DemoShellPauseBridge`
  - pause menu open/close에 대응해 gameplay pause를 요청한다.
  - modal stack과 destructive action routing만 소유한다.
- `DemoShellDialogueBridge`
  - `StageClear` gate 또는 후속 `GateIntro/Cutscene`에서 gameplay pause를 요청한다.
  - `StageStart overlay` 기본값에서는 gameplay pause를 요청하지 않는다.
- future requester
  - cutscene controller
  - tutorial intervention controller
  - debug step controller

### 3.2 aggregate owner
- 새 GO owner: `DemoShellGameplayPauseController`
- 책임:
  - `Acquire/Release` handle 발급과 수명 관리
  - requester 상태 집계
  - 현재 frame의 pause snapshot 계산
  - read-only API 제공
- 원칙:
  - requester는 서로 직접 알지 않는다.
  - aggregate owner만이 최종 pause state를 계산한다.

### 3.3 ECS pause state publisher / simulation writer
- 새 ECS system: `GameplayPauseApplySystem`
- 책임:
  - `DemoShellGameplayPauseController.CurrentSnapshot`를 읽는다.
  - ECS singleton `GameplayPauseStateComponent`를 최신화한다.
  - `FixedTickTimeComponent.PauseRequested`를 write한다.
  - 필요 시 `StepRequested`와의 정합을 유지한다.
- 원칙:
  - GO -> ECS publish와 `FixedTickTimeComponent.PauseRequested` write owner는 이 시스템 하나만 둔다.
  - `FixedTickTimeComponent.PauseRequested`의 runtime writer는 이 시스템 하나만 둔다.
  - 개별 UI/dialogue requester는 fixed tick singleton을 직접 수정하지 않는다.

### 3.4 reader
- `PlayerWasdMovement`, `PlayerEcsBridge`
  - gameplay input block read-only 소비
- `RuntimeUiRoot`
  - modal open 가능 여부, exclusive presentation 입력 상태를 read-only 소비
- `DemoShellFlowController`
  - pause-aware stage timer/result timing을 read-only 소비

## 4. 데이터 구조 / API 계약
### 4.1 GO runtime handle
- `PauseHandle`
  - `int Id`
  - `GameplayPauseReasonId Reason`
  - `GameplayPauseFlags Flags`
  - `bool IsValid`
- handle은 GO runtime 전용이다.
- ECS에 handle 목록 전체를 mirror하지 않는다.

### 4.2 reason
- `GameplayPauseReasonId`
  - `PauseMenu`
  - `DialogueGate`
  - `Cutscene`
  - `Debug`

### 4.3 flags
- `GameplayPauseFlags`
  - `PauseSimulation`
  - `BlockGameplayInput`
  - `ExclusivePresentationInput`
  - `BlockPauseMenuOpen`

### 4.4 pause snapshot
- `GameplayPauseStateComponent`
  - `GameplayPauseFlags Flags`
  - `uint ReasonMask`
  - `uint Version`
- 필요 시 디버그용으로 `SimulationPauseOwnerCount`, `ExclusiveOwnerCount`를 추가할 수 있다.

### 4.5 aggregate owner API
- `PauseHandle Acquire(GameplayPauseReasonId reason, GameplayPauseFlags flags)`
- `bool Release(PauseHandle handle)`
- `GameplayPauseSnapshot CurrentSnapshot { get; }`

## 5. fixed tick / 시간원 계약
### 5.1 authority
- gameplay world는 fixed tick time source를 authority로 사용한다.
- pause 시 fixed tick은 `PauseRequested=1`로 step 생성만 멈춘다.
- pause를 위해 `EnableFixedTick`를 on/off 토글하는 구조는 채택하지 않는다.

### 5.2 운영 기본값
- v1 권장 운영값은 gameplay runtime에서 `EnableFixedTick=1` 유지다.
- 일반 플레이, pause, replay, step debug가 같은 time source contract를 공유해야 한다.
- `PauseRequested`는 fixed tick authority 위에서만 의미를 가진다.
- 운영 씬과 테스트 월드는 동일한 tick rule을 공유해야 한다.
- 현재 `P3` 구현은 `DemoShellGameplayPauseController`가 존재하는 runtime에서 `GameplayPauseApplySystem`이 `EnableFixedTick=1`을 보장하는 과도기 상태다.
- `P5`에서는 bootstrap/test helper를 정렬해 운영 씬과 테스트 월드가 같은 fixed tick 기본 정책을 사용하도록 통일한다.

### 5.3 timer authority
- 아래 값은 logic tick 기반 시간만 사용한다.
  - stage elapsed
  - timeout 판정 시간
  - result elapsed
- `Time.deltaTime` 기반 local timer는 authoritative source로 사용하지 않는다.
- `DemoShellFlowController._stagePlayElapsedSec`는 후속 구현에서 fixed-tick-aware source로 대체한다.

## 6. 업데이트 순서 / 상태 흐름
### 6.1 ECS 그룹 기준
- 기존 그룹 순서는 유지한다.
  - `StageTopologyPrepareGroup -> FixedTickRootGroup`
  - `PlayerFixedStepGroup -> BulletFramePipelineGroup`
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`

### 6.2 pause 반영 순서
1. requester가 GO `Update()`에서 `Acquire/Release`를 호출한다.
2. `DemoShellGameplayPauseController`가 aggregate snapshot을 계산한다.
3. `GameplayPauseApplySystem`이 snapshot을 읽어 `GameplayPauseStateComponent` singleton을 최신화한다.
4. `GameplayPauseApplySystem`이 `FixedTickTimeComponent.PauseRequested`를 갱신한다.
5. `FixedTickTimeResolveSystem`이 현재 frame의 logic step 실행 여부를 결정한다.
6. 이후 `PlayerFixedStepGroup`과 `BulletFramePipelineGroup`이 `HasStep/LogicDeltaTime` 기준으로 실행된다.

### 6.3 ordering guardrail
- `GameplayPauseApplySystem`은 반드시 `FixedTickTimeResolveSystem`보다 먼저 실행되어야 한다.
- 구현 시 `UpdateBefore(typeof(FixedTickTimeResolveSystem))`를 기본값으로 둔다.
- pause state가 같은 frame에 반영되지 않으면 input block과 simulation stop이 한 프레임 어긋날 수 있다.

## 7. 시나리오별 정책
### 7.1 Pause menu
- acquire flags:
  - `PauseSimulation`
  - `BlockGameplayInput`
- 정책:
  - world simulation 정지
  - gameplay input 정지
  - menu 입력은 허용
  - destructive action confirm은 modal owner가 계속 소유

### 7.2 StageStart overlay
- 기본값:
  - acquire 없음
- 정책:
  - gameplay 계속 진행
  - player input 허용
  - dialogue presentation만 표시
- 후속 확장:
  - `GateIntro` variant는 `DialogueGate`와 같은 계약으로 승격 가능

### 7.3 StageClear dialogue gate
- acquire flags:
  - `PauseSimulation`
  - `BlockGameplayInput`
  - `ExclusivePresentationInput`
  - `BlockPauseMenuOpen`
- 정책:
  - world simulation 정지
  - gameplay input 정지
  - dialogue advance/skip만 입력 우선권 획득
  - pause menu open 금지

### 7.4 future cutscene
- acquire flags:
  - `PauseSimulation`
  - `BlockGameplayInput`
  - `ExclusivePresentationInput`
- 정책:
  - gameplay time은 정지
  - cutscene/local presentation time은 별도 시간축 사용 가능

## 8. 입력 / UI 계약
### 8.1 input block
- `PlayerWasdMovement`, `PlayerEcsBridge`는 common pause snapshot을 기준으로 gameplay input을 막는다.
- 기존 `DemoShellPauseBridge.GameplayInputBlocked`는 공통 pause snapshot에 대한 facade 또는 제거 대상으로 본다.

### 8.2 pause menu open guard
- `RuntimeUiRoot`는 `PauseMenuBlocked`가 켜져 있으면 pause open을 시도하지 않는다.
- `StageClear` dialogue gate 중에는 pause menu보다 dialogue 입력이 우선한다.

### 8.3 presenter 시간
- dialogue line hold/auto-advance는 `unscaledDeltaTime` 기준을 유지한다.
- 즉 simulation pause와 presenter advance는 서로 독립이다.

## 9. 작업 분해 / 진행 상태
### 9.1 P1 문서화
- 목표:
  - 공통 gameplay pause 권장안과 fixed tick authority를 문서로 고정한다.
- 상태: `completed`

### 9.2 P2 aggregate owner
- 목표:
  - `DemoShellGameplayPauseController`와 handle 기반 `Acquire/Release` runtime을 추가한다.
- 완료 기준:
  - requester가 직접 고정 tick singleton을 수정하지 않는다.
  - 중첩 acquire/release가 안정적으로 동작한다.
- 상태: `completed`

### 9.3 P3 ECS apply
- 목표:
  - `GameplayPauseStateComponent`와 `GameplayPauseApplySystem`을 추가한다.
- 완료 기준:
  - `FixedTickTimeResolveSystem` 이전에 pause state가 반영된다.
- 상태: `completed`

### 9.4 P4 requester integration
- 목표:
  - `DemoShellPauseBridge`, `DemoShellDialogueBridge`를 공통 pause owner에 연결한다.
- 완료 기준:
  - pause menu는 simulation pause를 발생시킨다.
  - `StageClear` dialogue는 gate 동안 simulation pause를 유지한다.
- 상태: `completed`

### 9.5 P5 timer authority 정리
- 목표:
  - stage elapsed / timeout / result elapsed를 logic tick 기반으로 통일한다.
  - 운영 씬과 테스트 월드의 tick rule을 동일한 fixed tick 정책으로 통일한다.
- 완료 기준:
  - pause 중 elapsed/result time이 증가하지 않는다.
  - 동일한 gameplay scenario가 운영 씬과 테스트 월드에서 같은 tick contract를 따른다.
  - fixed tick 기본 정책이 bootstrap/test helper 경로에서 일관되게 적용된다.
- 상태: `pending`

### 9.6 P6 검증
- 목표:
  - pause contract 회귀 테스트를 추가한다.
- 완료 기준:
  - 아래 검증 계획을 통과한다.
- 상태: `pending`

## 10. 검증 계획 / 합격 기준
- 공통
  1. compile
  2. console error 0
  3. EditMode pass
  4. PlayMode dedicated smoke pass
  5. PlayMode operational scene pause 회귀 pass

- EditMode
  - `Acquire/Release` 중첩과 stale handle 해제 검증
  - `GameplayPauseApplySystem` ordering 검증
  - `PauseRequested` / `StepRequested` 정합 검증
  - 테스트 월드가 운영 씬과 같은 tick rule을 사용하는지 검증
  - timer authority가 `Time.deltaTime`에 의존하지 않는지 검증

- PlayMode
  - pause menu open 시 bullet/player/source/run director가 정지한다
  - pause menu close 시 gameplay가 정상 resume된다
  - `StageClear` dialogue active 동안 simulation이 정지하고 dialogue 입력만 허용된다
  - `StageStart overlay`는 gameplay를 멈추지 않는다
  - pause 중 restart/return to lobby 이후 stale handle이 남지 않는다
  - pause 중 result elapsed와 timeout이 증가하지 않는다

## 11. 리스크 / 오픈 이슈
- 리스크 1: fixed tick authority를 runtime 기본값으로 켜지 않으면 pause 의미가 약해진다.
  - 대응: `P5`에서 운영 씬과 테스트 월드의 fixed tick 기본 정책을 동일하게 고정한다.

- 리스크 2: stage elapsed가 여전히 GO local time에 남아 있으면 pause 중 결과 시간이 증가한다.
  - 대응: timer authority를 logic tick source로 이관한다.

- 리스크 3: requester가 `FixedTickTimeComponent`를 직접 수정하면 owner 경계가 무너진다.
  - 대응: ECS apply writer를 단일화한다.

## 12. 변경 이력
- 2026-03-17: 초안 작성. `StagePlay` fixed tick authority, `Acquire/Release` 기반 공통 gameplay pause owner, simulation/input/presentation 분리 계약을 정리했다.
- 2026-03-17: `P2 aggregate owner` 구현 반영. `GameplayPauseApplySystem` 단일 writer, `GameplayPauseStateComponent`의 `Flags/ReasonMask/Version` shape, `P2/P3` 진행 상태를 현재 코드 기준으로 정정했다.
- 2026-03-17: `P3 ECS apply` 구현 완료를 반영했다. `P4 requester integration`은 acceptance를 충족한 것으로 정리했고, `P5`에 운영 씬/테스트 월드 tick rule 통일 작업을 추가했다.
