# ADR-20260317-01-fixed-tick-authoritative-stageplay-and-common-gameplay-pause
> `StagePlay` gameplay 시간을 fixed tick authority로 고정하고, pause는 `Acquire/Release` 기반 공통 owner가 집계한 상태를 통해 simulation/input/presentation을 분리 제어하는 결정

## 상태
- 제안됨 (문서 초안)

## 배경
- 현재 `DemoShellPauseBridge`는 UI modal과 입력 차단만 소유하고, world simulation 자체는 멈추지 않는다.
- `DemoShellDialogueBridge`의 `StageClear` gate도 입력 exclusive만 보장할 뿐 실제 gameplay pause를 만들지 않는다.
- ECS에는 `FixedTickTimeComponent.PauseRequested`와 `FixedTickTimeResolveSystem`이 이미 존재해 logic step 정지 seam이 있다.
- 하지만 운영 경로에서는 fixed tick authority가 runtime 기본값으로 고정되지 않았고, `DemoShellFlowController`는 여전히 local elapsed를 `Time.deltaTime`으로 누적한다.
- 이 상태에서는 pause menu, clear dialogue, future cutscene이 동일한 계약을 공유할 수 없고, simulation pause와 result/time 측정이 쉽게 어긋난다.

## 결정
1. gameplay 시간원
- `StagePlay` gameplay 시간은 fixed tick을 authority로 사용한다.
- pause는 `Time.timeScale`이 아니라 logic tick 생성 정지로 정의한다.

2. 공통 gameplay pause owner
- `PauseMenu`, `DialogueGate`, `Cutscene`, `Debug`는 모두 `Acquire/Release` 기반 공통 owner에 pause를 요청한다.
- 개별 requester가 서로의 상태를 직접 참조하지 않는다.

3. owner 분리
- `DemoShellPauseBridge`는 UI/modal owner로 축소한다.
- 공통 gameplay pause aggregate owner가 simulation/input/presentation block 상태를 계산한다.
- ECS fixed tick time source 반영은 단일 writer가 수행한다.

4. scenario 기본 정책
- `PauseMenu`는 simulation과 gameplay input을 멈춘다.
- `StageClear` dialogue gate는 simulation을 멈추고 dialogue 입력만 독점한다.
- `StageStart`는 기본값이 overlay이므로 gameplay pause를 만들지 않는다.

5. timer 정합
- stage elapsed, timeout, result elapsed는 gameplay logic time 기준으로만 계산한다.
- `Time.deltaTime` 기반 local result timer를 authoritative source로 두지 않는다.

## 대안
- 대안 1: 현재 구조 유지(input-only pause)
  - 장점: 구현량이 가장 적다.
  - 단점: world simulation, timeout, result elapsed가 계속 진행되어 pause 의미가 UI 수준에 머문다.

- 대안 2: requester가 각자 `FixedTickTimeComponent`를 직접 수정
  - 장점: 구현이 빠르다.
  - 단점: owner 경계가 분산되고, 중첩 pause/release와 future cutscene 확장에서 회귀 위험이 크다.

- 대안 3: fixed tick authority 없이 시스템마다 pause singleton을 개별 조회
  - 장점: 현재 runtime 기본값을 크게 건드리지 않고 도입 가능하다.
  - 단점: pause 체크가 시스템 전역에 흩어지고, replay/timer/time source 정합이 약하다.

## 결과
- pause menu, dialogue gate, future cutscene이 같은 runtime contract 위에서 확장된다.
- pause의 정의가 `입력 잠금`이 아니라 `logic time 정지`로 올라간다.
- timer/result/time limit이 simulation pause와 같은 기준으로 정렬된다.
- runtime 기본값으로 fixed tick authority를 더 강하게 요구하게 되므로, 기존 가변 delta 기반 local 경로는 축소 대상이 된다.

## 후속
1. `TD-023`를 SSOT로 삼아 aggregate owner, ECS apply writer, timer authority를 세부화한다.
2. `DemoShellPauseBridge`와 `DemoShellDialogueBridge`를 공통 pause owner에 연결한다.
3. pause 회귀 테스트에 menu, clear dialogue gate, overlay, restart/return 시나리오를 추가한다.
