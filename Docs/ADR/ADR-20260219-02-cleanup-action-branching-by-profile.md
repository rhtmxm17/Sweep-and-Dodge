# ADR-20260219-02-cleanup-action-branching-by-profile
> 플레이어 청소 행동 분기를 선택 상태와 판정 프로파일로 분리해, Request 단계에서 기하 판정만 교체 가능하도록 고정한 결정이다.

## 배경
- 현재 청소 행동은 사실상 1종(`원형 흡입 + 외곽 링 위험탄`)이며, 이후 조작 선택/캐릭터 선택 경로로 행동 분기가 추가될 예정이다.
- 분기 추가를 늦게 시작하면 시스템 단위 분기(중복 코드/소유권 충돌)로 확산될 가능성이 높다.
- 프로젝트의 고정 파이프라인과 Owner 원칙을 유지해야 한다.
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
  - Request는 제거 요청만 생성, 실제 반납/비활성은 ExecutionEnd Owner 단일 책임

## 결정
- 행동 분기를 아래 두 레이어로 분리한다.
  - 선택 레이어: `PlayerCleanupActionStateComponent`
    - `SelectedActionId`, `PendingActionId`, `Version`
    - 확정 책임: `PlayerCleanupActionSelectSystem`(Request 그룹 선두)
  - 판정 레이어: `DynamicBuffer<PlayerCleanupActionProfileBufferElement>`
    - 행동별 Trash/Hazard 판정 파라미터를 보관
- `BulletVacuumRequestSystem`은 선택된 ActionId와 프로파일을 읽어 판정 함수만 분기한다.
  - `RadialRing`: 원형 Trash + 링 Hazard
  - `ForwardFanLine`: 전방 부채꼴 Trash + 전방 직선 Hazard
- 공통 후처리(디스폰 요청 enable, CarryBin 증가, Source depletion 누적, UI 이벤트)는 행동과 무관하게 단일 경로로 유지한다.
- 외부 입력/캐릭터 선택 경로는 직접 시스템 교체를 하지 않고, `PendingActionId`만 기록한다.

## 대안 비교
### 대안 1: 행동별 요청 시스템 분리
- 장점:
  - 초기 구현이 빠르다.
- 단점:
  - Request 단계 소유권이 분산되고 중복 코드가 늘어난다.
  - 공통 후처리 변경 시 행동별 동기화 비용이 커진다.

### 대안 2: 탄환 측 CaptureRule만 확장
- 장점:
  - 탄환 데이터 중심으로 분기 가능하다.
- 단점:
  - 플레이어 조작/캐릭터 선택 기반 분기와 결합이 약하다.
  - 플레이어 행동 전환 상태를 표현하기 어렵다.

### 채택안 선택 이유
- 행동 추가 시 "프로파일 데이터 + 판정 함수"만 추가하면 되므로 확장 비용이 가장 낮다.
- 기존 파이프라인 책임 분리를 그대로 유지할 수 있다.

## 결과
- 현재 구현은 행동 분기 샘플 2종을 지원한다.
  - 기본: `RadialRing`
  - 샘플: `ForwardFanLine`
- Authoring에서 행동 프로파일을 베이크할 수 있으며, 향후 캐릭터/조작 선택 UI는 `PendingActionId`만 갱신하면 된다.

## 리스크 및 후속
- 리스크:
  - 전방 판정(부채꼴/직선)의 체감 튜닝값이 미고정 상태
  - 선택 이벤트(UI 피드백) 채널 표준화는 후속 과제
- 후속 작업:
  1. 캐릭터/조작 선택 경로에서 `PendingActionId` 반영 브리지 추가
  2. 행동 전환 시 UI 피드백 이벤트 타입 확정 여부 결정
  3. 행동별 프로파일 ScriptableObject 데이터화 여부 평가

## 관련 문서
- [Docs/GameDesign/GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
- [Docs/GameDesign/GD-001-campaign-loop-design.md](../GameDesign/GD-001-campaign-loop-design.md)
- [Docs/ADR/ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md](ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md)
