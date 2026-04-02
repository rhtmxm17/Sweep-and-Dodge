# ADR-20260402-01-broomsweep-default-cleanup-action
> 플레이어 기본 청소 동작을 `BroomSweep` 1종으로 고정하고, `Trash` 스윕 판정과 `Hazard` 정면 타이밍 판정을 같은 액션 안의 분리된 서브 프로파일로 운영하는 결정

## 배경
- 기존 기본안은 `RadialRing`, `ForwardFanLine` 2종을 전제로 했지만, 현재 안정된 게임 컨셉은 "빗자루로 휩쓴다"는 단일 행동에 더 가깝다.
- `Trash`와 `Hazard`는 같은 행동 안에서 처리되더라도 요구하는 판정 체감이 다르다.
  - `Trash`: 넓게 쓸리는 감각
  - `Hazard`: 정면에 힘이 실리는 타이밍 감각
- 활성 중 플레이어 회전이 자유로우면 스윕 기준축이 흔들려 판정과 표현 읽기성이 떨어진다.

## 결정
- 기본 청소 동작을 `BroomSweep` 1종으로 고정한다.
- 기존 `RadialRing`, `ForwardFanLine`은 이번 기준에서 미사용으로 내린다.
- `BroomSweep`는 하나의 액션이지만, 내부적으로 두 개의 판정 서브 프로파일을 가진다.
  - `TrashSweepProfile`
    - 스윕을 따라 이동하는 얇은 부채꼴 띠 판정
  - `HazardFocusProfile`
    - 스윕이 정면을 향하는 짧은 타이밍에 발생하는 정면 직사각형 판정
- 두 서브 판정은 메타데이터를 분리하되, 아래 상태는 공유한다.
  - 활성 시간
  - 좌/우 교대
  - 발동 순간 기준 전방
- `Vacuum` 활성 중에는 기준 전방을 잠그고, 이동은 감속만 적용한다.
  - 기본 방향: `LockFacingWhileActive = true`
  - 이동: `ActiveMoveSpeedScale` 기반 감속

## 대안 비교
### 대안 1: 기존 2종 액션 유지
- 장점:
  - 현재 구현/데이터 구조를 덜 건드린다.
- 단점:
  - 안정된 컨셉과 맞지 않는다.
  - 액션 선택 체감보다 "빗자루질" 읽기성이 더 중요한 현재 방향과 충돌한다.

### 대안 2: `Trash`와 `Hazard`를 같은 기하로 유지
- 장점:
  - 구현이 단순하다.
- 단점:
  - `Trash`와 `Hazard`의 목표 체감이 달라 튜닝 축이 서로 간섭한다.
  - `Hazard`를 타이밍 액션으로 읽히게 만들기 어렵다.

### 대안 3: `Trash` 판정과 `Hazard` 판정을 별도 액션/별도 시스템으로 분리
- 장점:
  - 각 판정을 완전히 독립적으로 조정할 수 있다.
- 단점:
  - 플레이 체감상 하나의 빗자루질이어야 하는 동작을 로직/소유권까지 분리하게 된다.
  - 현재 Request owner 단일 책임 원칙과 맞지 않는다.

### 채택안 선택 이유
- 입력 체감은 단순하게 유지하면서도, `Trash`와 `Hazard`를 다른 축으로 정밀 조정할 수 있다.
- 기존 Request owner/공통 후처리 구조를 유지하면서 현재 컨셉과 가장 잘 맞는 행동을 만들 수 있다.

## 결과
- 문서 SSOT는 `BroomSweep` 기준으로 갱신한다.
- `TD-012`는 단일 기본 액션 + 서브 프로파일 분리 + 활성 중 방향 잠금/이동 제한 계약으로 재정의한다.
- `GD-006`은 `Trash` 부채꼴 스윕 + `Hazard` 정면 직사각형 타이밍 판정 기준으로 갱신한다.
- 구현 단계에서는 다음이 후속 범위가 된다.
  1. 레거시 액션 ID/기본 asset/fallback 정리
  2. 스윕 runtime state 추가
  3. 이동/회전 제약 config와 movement system 연동

## 리스크 및 후속
- 리스크:
  - `Hazard` 정면 판정이 너무 짧으면 체감 실패율이 과도하게 높아질 수 있다.
  - 방향 잠금/이동 감속이 과하면 액션이 답답하게 느껴질 수 있다.
- 후속 작업:
  1. `BroomSweep` 진행률/좌우 교대 상태를 runtime component로 도입
  2. `BulletVacuumRequestSystem` 판정을 정적 기하에서 진행률 기반 스윕으로 변경
  3. `PlayerIntentMovementSystem`에 활성 중 방향 잠금/이동 감속 적용

## 관련 문서
- [Docs/GameDesign/GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
- [Docs/TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md](../TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md)
- [Docs/ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)
