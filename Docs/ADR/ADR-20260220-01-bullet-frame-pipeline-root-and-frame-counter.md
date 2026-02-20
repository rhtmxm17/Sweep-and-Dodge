# ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter
> Bullet 파이프라인 루트 그룹을 도입하고 Request fence publish 단일화 + 프레임 카운터 기반 Frame ID를 채택한다

## 상태
- 반영됨

## 배경
- 기존 4단 파이프라인은 `SimulationSystemGroup` 직속 그룹 간 순서로 유지되었고, Request 단계의 `CellMapFence` publish가 복수 시스템에 분산되어 있었다.
- 구조가 커질수록 "어느 시스템이 fence 최종 책임자인가"가 흐려질 수 있고, Update 순서 계약이 깨져도 리뷰에서 놓치기 쉽다.
- Frame ID를 `elapsed/delta` 추정으로 만들면, 타임스케일/델타 변화 상황에서 이벤트 프레임 표기 일관성이 약해질 수 있다.

## 결정
1. 루트 그룹 도입:
- `BulletFramePipelineGroup`을 `SimulationSystemGroup` 아래에 두고,
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 4단 그룹을 루트 그룹 하위로 고정한다.

2. Request fence publish 단일화:
- CellMap reader 시스템은 `Combine(state.Dependency, CellMapFence)`만 수행한다.
- `BulletRequestFencePublishSystem`(Request 그룹 `OrderLast`)이 `CellMapFence`를 최종 publish한다.

3. Frame ID 생성 방식 전환:
- `BulletFrameCounterComponent`를 도입하고 `BulletFrameCounterAdvanceSystem`(ExecutionBegin `OrderFirst`)에서 프레임당 1회 증가시킨다.
- 이벤트 기록 Frame ID는 `FrameSequenceUtility.GetCurrentFrame(ref state)`를 사용한다.

4. 계약 테스트 추가:
- Editor 테스트에서 그룹 계층/순서, Request fence publish 단일 책임, 프레임 카운터 시스템 위치를 검증한다.

## 대안
- 기존 구조 유지:
  - 장점: 코드 변경 최소화
  - 단점: 순서/책임 계약이 암묵적으로 남아 회귀 탐지가 어려움
- elapsed/delta 추정 Frame 유지:
  - 장점: 추가 상태 없음
  - 단점: 프레임 표기 재현성이 약함

## 결과
- 파이프라인 고정 강도가 높아지고, Request-CellMap fence 책임 경계가 명확해졌다.
- 프레임 기반 이벤트 디버깅/분석 시 프레임 번호 일관성이 좋아졌다.
- 구조 계약 위반은 Editor 테스트에서 조기에 탐지 가능해졌다.

## 후속
- 신규 Request 시스템 추가 시, CellMap reader면 fence 결합만 수행하고 publish는 하지 않는 규칙을 유지한다.
- 파이프라인 계약 테스트에 신규 시스템 순서 제약을 지속 반영한다.
