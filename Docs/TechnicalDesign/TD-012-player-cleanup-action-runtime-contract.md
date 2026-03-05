# 플레이어 청소 액션 런타임 계약 (TD-012)

## Metadata
- doc_id: `TD-012`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-05`
- related_docs:
  - [GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [TD-001-player-feedback-event-channel.md](./TD-001-player-feedback-event-channel.md)
  - [TD-007-common-combat-event-channel.md](./TD-007-common-combat-event-channel.md)
- related_adr:
  - [ADR-20260219-02-cleanup-action-branching-by-profile.md](../ADR/ADR-20260219-02-cleanup-action-branching-by-profile.md)
  - [ADR-20260219-03-player-cleanup-action-profile-so-externalization.md](../ADR/ADR-20260219-03-player-cleanup-action-profile-so-externalization.md)
  - [ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](../ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)

> 플레이어 청소 액션 분기(`RadialRing`, `ForwardFanLine`)의 입력 해석, 상태 적용, Request 단계 판정 책임을 고정한다.

## 1. 문제 정의
- 조건부 수거 액션이 확장되면서 입력 계층, 선택 상태, 판정 로직이 분산되면 소유권 충돌과 회귀가 발생한다.
- 행동 전환 타이밍(특히 Vacuum 활성 중 입력 처리)이 모호하면 플레이 체감이 프레임마다 달라질 수 있다.

## 2. 목표/비목표
- 목표:
  - 액션 모델과 슬롯 기반 입력 해석을 단일 계약으로 고정한다.
  - `PendingActionId` 적용 시점과 활성 중 입력 소비 정책을 명확히 한다.
  - Request 단계 판정 분기와 공통 후처리 책임 경계를 고정한다.
- 비목표:
  - 신규 액션 타입 추가.
  - HUD/VFX/사운드 소비 규칙 상세.
  - 피격 페널티 수치 튜닝.

## 3. 설계안
### 3.1 Action 모델
- 지원 액션:
  - `RadialRing`
  - `ForwardFanLine`
- 상태 컴포넌트:
  - `PlayerCleanupActionStateComponent`
    - `SelectedActionId`
    - `PendingActionId`
    - `Version`
- 슬롯 맵:
  - `PlayerCleanupActionSlotMapComponent`
    - `PrimaryActionId`
    - `SecondaryActionId`

### 3.2 입력 해석 계약 (`Input -> Slot -> ActionId`)
- 브리지(`PlayerEcsBridge`)는 입력 시 슬롯(`Primary`/`Secondary`)만 요청한다.
- 입력 해석 시스템(`PlayerGoSyncSystem`)이 슬롯을 ActionId로 매핑해 `PendingActionId`에 기록한다.
- 입력 계층은 ActionId를 직접 소유하지 않는다.

### 3.3 상태 적용 계약 (`Pending` 확정 타이밍)
- 선택 확정 owner: `PlayerCleanupActionSelectSystem`.
- 적용 규칙:
  - `PendingActionId == None`이면 변경 없음.
  - `PendingActionId == SelectedActionId`이면 요청만 소비.
  - Vacuum 활성 중(`IsActive != 0`) 전환 요청은 즉시 소비하고 전환하지 않는다.
  - Vacuum 비활성 상태에서만 `SelectedActionId = PendingActionId`를 확정하고 `Version`을 증가시킨다.

### 3.4 프로파일 데이터 경로
- Authoring 원본:
  - `PlayerCleanupActionSetSO`
- Bake 대상:
  - `PlayerCleanupActionStateComponent`
  - `PlayerCleanupActionSlotMapComponent`
  - `DynamicBuffer<PlayerCleanupActionProfileBufferElement>`
- fallback:
  - SO 미지정/비어 있음 시 기본 2종 프로파일(`RadialRing`, `ForwardFanLine`)을 베이크한다.

### 3.5 Request 단계 판정 분기와 공통 후처리
- 판정 분기 owner: `BulletVacuumRequestSystem`.
- 분기 범위:
  - Trash 판정 기하
  - Hazard 판정 기하
- 후처리 계약(결과 분기 고정):
  - `HazardCaptured` (`Load < Capacity`):
    - `BulletDespawnRequestTag` enable
    - CarryBin 누적 적용
    - Source 진행도 누적/상태 전이 이벤트 발행
    - Combat event append(`Collect`)
    - UI/VFX 이벤트 `HazardCaptured` 발행
  - `HazardRemovedWhenCarryFull` (`Load == Capacity`):
    - `BulletDespawnRequestTag` enable (제거 전용)
    - CarryBin/Source 진행/HazardStack 갱신 생략
    - Combat event `Collect` append 생략
    - UI/VFX 이벤트 `HazardRemoved` 발행
  - FullBin 상태 Trash 판정:
    - 디스폰/수거량 반영 없음
    - `VacuumStartBlocked(CarryBinFull)` 피드백만 발행

## 4. 업데이트 순서/소유권
- 파이프라인:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- Request 그룹 내 순서:
  1. `PlayerCleanupActionSelectSystem` (선택 상태 확정)
  2. `BulletVacuumRequestSystem` (선택 액션 기반 판정/요청 생성)
  3. 후속 충돌/Deposit 요청 시스템
- 소유권 규칙:
  - 액션 선택 확정은 `PlayerCleanupActionSelectSystem` 단일 책임.
  - 판정 분기는 `BulletVacuumRequestSystem` 단일 책임.
  - 실제 디스폰 실행/풀 반납은 ExecutionEnd owner 시스템 책임.

## 5. 성능/리스크
- 리스크 1: 액션 전환 연타로 인한 상태 흔들림.
  - 대응: 활성 중 입력 즉시 소비 정책으로 큐 누적 차단.
- 리스크 2: 프로파일 미설정으로 런타임 불안정.
  - 대응: Bake 시 fallback 2종 강제.
- 리스크 3: 결과 분기 확장 시 후처리 누락/중복.
  - 대응: 결과 타입을 `HazardCaptured`/`HazardRemovedWhenCarryFull`로 고정하고 경로별 후처리 목록을 계약으로 유지.

## 6. 검증 계획
- EditMode:
  - 슬롯 매핑(`Primary/Secondary`) 해석 검증.
  - Vacuum 활성 중 전환 요청 소비(미적용) 검증.
  - Vacuum 비활성 상태에서 `Pending -> Selected` 확정 및 `Version` 증가 검증.
  - 액션별 판정 분기(`RadialRing`, `ForwardFanLine`) 최소 케이스 검증.
  - `Load < Capacity` Hazard 성공 시 `HazardCaptured` 경로(Carry/Source/`Collect`) 반영 검증.
  - `Load == Capacity` Hazard 성공 시 `HazardRemovedWhenCarryFull` 경로(디스폰 전용, Carry/Source/`Collect` 미반영) 검증.
  - `Load == Capacity` Trash 수거 시도 시 `VacuumStartBlocked(CarryBinFull)`만 발행되는지 검증.
- PlayMode:
  - 액션 전환 입력 후 다음 발동에서만 전환 체감되는지 확인.
  - 조건부 수거 흐름에서 두 액션의 역할 차이가 유지되는지 확인.
  - FullBin 구간에서 Hazard 성공이 제거 전용 VFX로 구분되는지 확인.
- 공통 게이트:
  - `compile -> console error 0 -> EditMode -> PlayMode 스모크`.

## 7. 오픈 이슈
- 액션 전환 피드백(UI/사운드) 노출 강도 기준.
- 액션 타입 확장 시 슬롯 수를 유지할지 여부.

## 8. 변경 이력
- 2026-03-05: 문서 신규 작성. 액션 모델/슬롯 매핑/활성 중 입력 소비/판정 분기 책임 계약을 정식화했다.
- 2026-03-05: FullBin Hazard 예외 규칙을 반영해 결과 타입(`HazardCaptured`, `HazardRemovedWhenCarryFull`)과 경로별 후처리/피드백 계약을 추가했다.
