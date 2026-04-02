# 플레이어 청소 액션 런타임 계약 (TD-012)

## Metadata
- doc_id: `TD-012`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-02`
- related_docs:
  - [GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [TD-001-player-feedback-event-channel.md](./TD-001-player-feedback-event-channel.md)
  - [TD-007-common-combat-event-channel.md](./TD-007-common-combat-event-channel.md)
  - [TD-018-hazardstack-runtime-contract.md](./TD-018-hazardstack-runtime-contract.md)
- related_adr:
  - [ADR-20260219-03-player-cleanup-action-profile-so-externalization.md](../ADR/ADR-20260219-03-player-cleanup-action-profile-so-externalization.md)
  - [ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](../ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)
  - [ADR-20260402-01-broomsweep-default-cleanup-action.md](../ADR/ADR-20260402-01-broomsweep-default-cleanup-action.md)

> 플레이어 청소 액션의 현재 기준안을 `BroomSweep` 단일 기본 동작으로 고정하고, `Trash` 스윕 판정, `Hazard` 정면 타이밍 판정, 활성 중 방향 잠금/이동 제한 계약을 정리한다.

## 1. 문제 정의
- 기존 액션 모델(`RadialRing`, `ForwardFanLine`)은 "빗자루로 휩쓴다"는 현재 게임 컨셉과 직접 대응되지 않는다.
- `Trash`와 `Hazard`를 같은 판정으로 처리하면 체감은 단순해 보이지만, 실제로는 요구하는 위치/타이밍/튜닝 축이 달라 조정 비용이 커진다.
- `Vacuum` 활성 중 회전과 이동이 자유로우면 스윕 기준축이 흔들려 판정 해석과 표현 연동이 불안정해진다.

## 2. 목표/비목표
- 목표:
  - 기본 청소 동작을 `BroomSweep` 1종으로 고정한다.
  - `Trash`와 `Hazard`를 같은 액션 안에서 묶되, 판정 기하와 튜닝 메타데이터는 분리한다.
  - `Vacuum` 활성 중 기준 방향 고정과 이동속도 제한을 통해 스윕 읽기성을 확보한다.
  - 기존 Request owner/공통 후처리 구조를 유지한다.
- 비목표:
  - 액션 다변화 복귀.
  - HUD/VFX/사운드의 상세 연출 규칙 확정.
  - 피격 페널티 수치 튜닝.

## 3. 설계안
### 3.1 Action 모델
- 현재 지원 액션:
  - `BroomSweep`
- 레거시 상태:
  - `RadialRing`, `ForwardFanLine`은 이번 합의 기준에서 미사용으로 내린다.
  - 런타임에 남아 있는 선택/슬롯 경로는 호환 레이어로만 취급한다.
- 상태 컴포넌트:
  - `PlayerCleanupActionStateComponent`
    - 현재 유효한 `SelectedActionId`는 `BroomSweep`만 허용한다.
    - `PendingActionId` 경로는 유지하되, 기본 슬롯도 `BroomSweep`로 수렴시킨다.
  - `PlayerCleanupSweepRuntimeStateComponent`(구현 예정)
    - `NextSweepDirection`
    - `ActiveSweepDirection`
    - `ActiveFacing`
    - `ActiveFacingLocked`
    - `ActivationFrame` 또는 동등한 진행률 계산 기준

### 3.2 입력 해석 계약 (`Input -> Slot -> ActionId`)
- 브리지(`PlayerEcsBridge`)는 입력 시 슬롯(`Primary`/`Secondary`)만 요청한다.
- fixed-tick consume 시스템(`PlayerIntentConsumeSystem`)이 슬롯을 ActionId로 매핑해 `PendingActionId`에 기록한다.
- 현재 기준에서는 두 슬롯 모두 `BroomSweep`를 가리키는 구성을 기본값으로 사용한다.
- 입력 계층은 좌/우 스윕 방향을 직접 소유하지 않는다.
  - 좌/우 교대는 `BroomSweep` 내부 runtime state가 단일 책임으로 관리한다.

### 3.3 상태 적용 계약 (`Pending` 확정 타이밍)
- 선택 확정 owner: `PlayerCleanupActionSelectSystem`.
- 적용 규칙:
  - `PendingActionId == None`이면 변경 없음.
  - `PendingActionId == SelectedActionId`이면 요청만 소비.
  - Vacuum 활성 중(`IsActive != 0`) 전환 요청은 즉시 소비하고 전환하지 않는다.
  - Vacuum 비활성 상태에서만 `SelectedActionId = PendingActionId`를 확정하고 `Version`을 증가시킨다.
- 현재 기준에서는 결과적으로 `SelectedActionId = BroomSweep`를 유지하는 경로가 기본이다.

### 3.4 프로파일 데이터 경로
- Authoring 원본:
  - `PlayerCleanupActionSetSO`
- Bake 대상:
  - `PlayerCleanupActionStateComponent`
  - `PlayerCleanupActionSlotMapComponent`
  - `DynamicBuffer<PlayerCleanupActionProfileBufferElement>`
- 프로파일 구조:
  - 액션은 1개지만, 내부 메타데이터는 아래 두 서브 프로파일로 분리한다.
  - `TrashSweepProfile`
    - 스윕 반경(`InnerRadius`, `OuterRadius`)
    - 스윕 폭(`HalfAngleDeg`)
    - 시작/종료 각(`StartAngleDeg`, `EndAngleDeg`)
    - 진행 곡선 또는 보간 규칙
  - `HazardFocusProfile`
    - 정면 직사각형 길이/폭
    - 유효 시점(`ForwardWindowAngleDeg` 또는 동등한 타이밍 창)
    - 필요 시 별도 보정값
- 공통 타이밍 메타데이터:
  - `CaptureActiveTime`
  - `CaptureCooldown`
  - 좌/우 교대 규칙
  - 활성 시 기준 전방 고정 여부
- fallback:
  - SO 미지정/비어 있음 시에도 기본 `BroomSweep` 프로파일 1종만 베이크한다.

### 3.5 Request 단계 판정 분기와 공통 후처리
- 판정 분기 owner: `BulletVacuumRequestSystem`.
- 분기 범위:
  - `Trash` 판정 기하: "스윕을 따라 이동하는 얇은 부채꼴 띠"
  - `Hazard` 판정 기하: "스윕이 정면을 향하는 짧은 타이밍에 발생하는 정면 직사각형"
- 계약:
  - 두 판정은 같은 `BroomSweep` 액션에 속한다.
  - 두 판정은 같은 스윕 진행률/방향 상태를 공유한다.
  - 두 판정은 메타데이터와 튜닝 축을 분리한다.
  - 두 판정을 별도 액션이나 별도 owner 시스템으로 분리하지 않는다.
- 후처리 계약(결과 분기 고정):
  - `HazardCaptured` (`Load < Capacity`):
    - `BulletDespawnRequestTag` enable
    - CarryBin 누적 적용
    - Source 진행도 누적/상태 전이 이벤트 발행
    - `HazardStack` 증가 요청 생성(실제 state write는 `TD-018`의 player risk owner가 담당)
    - Combat event append(`Collect`)
    - UI/VFX 이벤트 `HazardCaptured` 발행
  - `HazardRemovedWhenCarryFull` (`Load == Capacity`):
    - `BulletDespawnRequestTag` enable (제거 전용)
    - CarryBin/Source 진행/HazardStack 갱신 생략
    - Combat event `Collect` append 생략
    - UI/VFX 이벤트 `HazardRemoved` 발행
  - FullBin 상태 `Trash` 판정:
    - 디스폰/수거량 반영 없음
    - `VacuumStartBlocked(CarryBinFull)` 피드백만 발행

### 3.6 활성 중 방향 잠금/이동 제한
- 이동/회전 writer는 `PlayerFixedStepGroup`의 `PlayerIntentMovementSystem`이 단일 책임으로 가진다.
- `Vacuum` 활성 중 제약 설정은 별도 config component로 분리한다.
  - `LockFacingWhileActive`
  - `ActiveMoveSpeedScale`
- 적용 규칙:
  - 발동이 승인된 프레임의 전방을 `ActiveFacing`에 저장한다.
  - `LockFacingWhileActive = true`면 활성 종료 전까지 `AimWorldXZ` 입력으로 회전하지 않는다.
  - 이동은 계속 허용하되, `ActiveMoveSpeedScale`만큼 감속한다.
  - 기준 전방 스냅샷은 좌/우 교대와 함께 `BroomSweep` runtime state가 소유한다.
- 기본 권장값:
  - `LockFacingWhileActive = true`
  - `ActiveMoveSpeedScale = 0.4 ~ 0.6`

## 4. 업데이트 순서/소유권
- 파이프라인:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- `PlayerFixedStepGroup` 내 순서:
  1. `PlayerPreviousPositionCaptureSystem`
  2. `PlayerIntentMovementSystem`
  3. `PlayerObstacleBlockSystem`
  4. `PlayerIntentConsumeSystem`
- Request 그룹 내 순서:
  1. `PlayerCleanupActionSelectSystem` (선택 상태 확정)
  2. `BulletVacuumRequestSystem` (`BroomSweep` 진행률 해석/요청 생성)
  3. 후속 충돌/Deposit 요청 시스템
- 소유권 규칙:
  - 액션 선택 확정은 `PlayerCleanupActionSelectSystem` 단일 책임.
  - 활성 중 이동/회전 제약 적용은 `PlayerIntentMovementSystem` 단일 책임.
  - 스윕 방향 교대와 기준 전방 스냅샷 확정은 `BulletVacuumRequestSystem` 단일 책임.
  - 판정 분기는 `BulletVacuumRequestSystem` 단일 책임.
  - `HazardStack` 상태 확정은 `TD-018`의 player risk owner 단일 책임.
  - 실제 디스폰 실행/풀 반납은 ExecutionEnd owner 시스템 책임.

## 5. 데이터 구조/제약
- `BroomSweep`는 구조 변경 없이 enableable/lifecycle request 경로를 재사용한다.
- CellMap 조회는 기존과 동일하게 Request 단계 ReadOnly + fence 결합으로 유지한다.
- `Trash`와 `Hazard` 판정은 같은 셀 탐색 경로를 공유하되, 기하 계산 함수와 프로파일 데이터는 분리한다.
- 활성 중 기준 전방은 프레임마다 다시 계산하지 않고 발동 시 스냅샷을 우선 사용한다.
  - 이유: 스윕 판정과 표현의 기준축 흔들림 방지

## 6. 작업 분해/진행 상태
- 완료:
  - `BroomSweep`를 기본 청소 동작으로 채택하는 설계 방향 합의
  - `Trash`/`Hazard` 판정 메타데이터 분리 원칙 합의
  - 활성 중 방향 잠금/이동 제한 필요성 확인
- 예정:
  - `PlayerCleanupActionId`/프로파일 구조를 `BroomSweep` 기준으로 정리
  - `PlayerCleanupSweepRuntimeStateComponent`와 활성 제약 config component 추가
  - `BulletVacuumRequestSystem`의 정적 기하 분기를 스윕 진행률 기반 판정으로 교체
  - `PlayerIntentMovementSystem`에 활성 중 회전 잠금/이동 감속 적용
  - 기존 `RadialRing`/`ForwardFanLine` 기본값/테스트/샘플 자산 정리

## 7. 성능/리스크
- 리스크 1: 정면 직사각형 `Hazard` 판정이 지나치게 빡빡하면 체감 실패율이 급증한다.
  - 대응: `ForwardWindowAngleDeg`와 직사각형 폭을 분리 튜닝한다.
- 리스크 2: 활성 중 회전 허용 시 스윕 판정과 표현이 어긋난다.
  - 대응: 기본값을 방향 잠금으로 둔다.
- 리스크 3: 이동까지 완전 고정하면 조작 답답함이 커진다.
  - 대응: 이동은 허용하고 감속만 적용한다.
- 리스크 4: 레거시 액션 ID/프로파일이 남아 있으면 문서와 코드가 장기간 불일치할 수 있다.
  - 대응: 구현 단계에서 기본 asset, fallback, 테스트를 함께 정리한다.

## 8. 검증 계획
- EditMode:
  - 기본 슬롯이 `BroomSweep`를 가리키는지 검증.
  - Vacuum 활성 중 전환 요청 소비(미적용) 검증.
  - 발동 승인 시 `NextSweepDirection -> ActiveSweepDirection` 교대가 올바른지 검증.
  - `Trash` 스윕 부채꼴 판정이 진행률에 따라 회전하는지 검증.
  - `Hazard` 정면 직사각형 판정이 정면 타이밍 창에서만 활성화되는지 검증.
  - 활성 중 조준 변경에도 회전이 유지되는지 검증.
  - 활성 중 이동 속도가 설정 배율만큼 줄어드는지 검증.
  - `Load < Capacity` Hazard 성공 시 `HazardCaptured` 경로 반영 검증.
  - `Load == Capacity` Hazard 성공 시 `HazardRemovedWhenCarryFull` 경로 반영 검증.
  - `Load == Capacity` `Trash` 판정 시 `VacuumStartBlocked(CarryBinFull)`만 발행되는지 검증.
- PlayMode:
  - 좌->우, 우->좌 스윕 교대 체감이 유지되는지 확인.
  - `Trash`는 쓸리는 감각, `Hazard`는 정면 타이밍 감각으로 읽히는지 확인.
  - 활성 중 방향 잠금과 이동 감속이 과도한 답답함 없이 읽기성을 높이는지 확인.
- 공통 게이트:
  - `compile -> console error 0 -> EditMode -> PlayMode 스모크`

## 9. 오픈 이슈
- 슬롯 UI를 계속 노출할지, 단일 기본 액션 기준으로 축소할지 여부.
- 스윕 진행 곡선을 선형으로 둘지, 정면 통과 구간을 느리게 하는 easing을 둘지 여부.
- `Hazard` 직사각형 판정을 충돌 즉시형으로 둘지, 아주 짧은 지속 창으로 둘지 여부.

## 10. 변경 이력
- 2026-04-02: 기본 청소 동작을 `BroomSweep` 단일 액션으로 재정의하고, `Trash` 스윕 판정 / `Hazard` 정면 판정 / 활성 중 방향 잠금·이동 제한 계약을 추가했다.
- 2026-03-16: `HazardCaptured` 결과를 `HazardStack` 직접 write가 아닌 증가 요청 생성으로 정리하고, 실제 상태 확정 owner를 `TD-018` 참조로 분리했다.
- 2026-03-09: 액션 슬롯 해석 책임을 `PlayerGoSyncSystem`에서 `PlayerIntentConsumeSystem`으로 옮기고, fixed-tick player path 기준으로 문구를 갱신했다.
- 2026-03-05: 문서 신규 작성. 액션 모델/슬롯 매핑/활성 중 입력 소비/판정 분기 책임 계약을 정식화했다.
