# HazardStack 런타임 계약

## Metadata
- doc_id: `TD-018`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-16`
- related_docs:
  - [GD-004-carrybin-load-and-deposit.md](../GameDesign/GD-004-carrybin-load-and-deposit.md)
  - [GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-012-player-cleanup-action-runtime-contract.md](./TD-012-player-cleanup-action-runtime-contract.md)
- related_adr:
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

> HazardStack 규칙이 `CarryBin`, `HazardCaptured`, `Hit`, `Deposit`, `Source 진행도` 문서에 분산되어 있어, 상태 writer와 동프레임 처리 순서를 별도 기술 설계로 고정한다.

## 1. 목표 / 비목표
### 1.1 목표
- `HazardStack` 상태 writer를 플레이어 리스크 owner 단일 책임으로 고정한다.
- 같은 프레임 수거, `Hit`, `Deposit`이 겹쳐도 결과가 순서 의존 없이 결정되도록 한다.
- `RiskMultiplier`의 `HazardStack` 항이 다음 프레임부터 반영되도록 고정한다.
- `Source 진행도`를 정수 계약으로 유지한다.

### 1.2 비목표
- HUD 표시 추가
- `Load / Capacity` 기반 `RiskFactor` 복구
- 플레이어 튜닝 데이터 저장 위치 최종 통합

## 2. 데이터 구조 / 제약
### 2.1 플레이어 리스크 상태
- 플레이어 단일 상태 컴포넌트가 아래를 소유한다.
  - 현재 `HazardStack`
  - `HazardStackMax`
  - `HazardBonusRate`
- 상태 읽기는 Request 단계에서 read-only로만 허용한다.

### 2.2 프레임 내 요청 데이터
- `HazardCaptured`는 `HazardStack +1` 직접 write 대신 증가 요청만 남긴다.
- `Hit`, `Deposit`은 `HazardStack = 0` 직접 write 대신 reset 요청만 남긴다.
- 실제 상태 반영은 ExecutionEnd 말단의 player risk owner가 단일 책임으로 수행한다.

### 2.3 진행도 정수 유지
- `Source 진행도`는 기존처럼 정수 누적치로 유지한다.
- multiplier 적용 해상도는 탄의 정수 진행 값 authoring으로 확보한다.
- 이번 범위는 별도 float 누적 버퍼나 fixed-point 상태를 도입하지 않는다.

## 3. 수식 / 적용 대상
### 3.1 RiskMultiplier
```text
RiskMultiplier = 1 + (FrameStartHazardStack × HazardBonusRate)
```

- `FrameStartHazardStack`은 해당 Request 프레임 시작 시점 snapshot이다.
- 같은 프레임에 `HazardCaptured`로 오른 stack은 현재 프레임 수거에는 반영하지 않는다.
- 다음 프레임부터 `Trash + HazardCaptured`의 `Source 진행도` 계산에 사용한다.

### 3.2 이벤트별 반영 규칙
- `Trash`
  - `Source 진행도`에 `RiskMultiplier` 적용
  - `HazardStack` 변화 없음
- `HazardCaptured`
  - `Source 진행도`에 `RiskMultiplier` 적용
  - `HazardStack +1` 증가 요청 생성
- `HazardRemovedWhenCarryFull`
  - `Source 진행도`, `Carry`, `HazardStack`, `Collect` 미반영
- `Hit`
  - `HazardStack reset` 요청 생성
- `Deposit`
  - 기존 Deposit 요청이 실제로 생성된 경우에 한해 `HazardStack reset` 요청 생성

## 4. 업데이트 순서 / 소유권
- 프레임 파이프라인:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`

### 4.1 Request 단계
1. `BulletVacuumRequestSystem`
   - `FrameStartHazardStack`을 read-only로 읽는다.
   - `Trash/HazardCaptured`의 `Source 진행도`를 계산한다.
   - `HazardCaptured`에 대해서만 증가 요청을 남긴다.
2. `PlayerHazardCollisionRequestSystem`
   - 피격 요청만 생성한다.
3. `PlayerCarryBinDepositRequestSystem`
   - Deposit 요청만 생성한다.

### 4.2 ExecutionEnd 단계
1. 피격/Deposit 소비 시스템은 자신의 gameplay 효과를 적용한다.
   - `Hit`: Carry 손실, iFrame, VacuumLock
   - `Deposit`: Carry 비우기
2. 위 시스템들은 `HazardStack` 직접 write를 하지 않고 reset 요청만 남긴다.
3. ExecutionEnd 말단의 player risk owner가 증가 요청과 reset 요청을 함께 소비해 최종 `HazardStack`을 확정한다.

### 4.3 동프레임 처리 규칙
- 계약명: `수거 확정 후 리셋`
- 적용 순서:
  1. 수거 결과(`Source 진행도`, `Carry`, `HazardCaptured` 증가 요청) 확정
  2. 같은 프레임 `Hit/Deposit` reset 요청 확인
  3. reset 요청이 하나라도 있으면 프레임 종료 시점 `HazardStack = 0`
- 의미:
  - 수거 자체는 롤백하지 않는다.
  - 최종 플레이어 리스크 상태는 reset이 덮는다.

## 5. 작업 분해 / 진행 상태
1. 문서 계약 반영 (`완료`)
2. 플레이어 리스크 상태/요청 데이터 구조 추가 (`예정`)
3. `BulletVacuumRequestSystem`의 `HazardCaptured` 증가 요청 경로 추가 (`예정`)
4. `Hit/Deposit` reset 요청 경로 추가 (`예정`)
5. player risk owner 구현 및 ExecutionEnd 순서 고정 (`예정`)
6. EditMode/PlayMode 검증 추가 (`예정`)

## 6. 검증 계획 / 합격 기준
- EditMode
  - `HazardCaptured`만 `HazardStack +1` 요청을 남기는지 검증
  - `HazardRemovedWhenCarryFull`이 `HazardStack`에 영향을 주지 않는지 검증
  - 같은 프레임 다중 수거가 동일 `FrameStartHazardStack` snapshot을 공유하는지 검증
  - 같은 프레임 `HazardCaptured + Hit` 시 수거는 반영되고 최종 `HazardStack`이 0인지 검증
  - 같은 프레임 `HazardCaptured + Deposit` 시 수거는 반영되고 최종 `HazardStack`이 0인지 검증
  - 다음 프레임 수거부터만 증가한 stack이 multiplier에 반영되는지 검증
  - 스테이지 시작/재시작 시 `HazardStack = 0` 초기화 검증
- PlayMode
  - Hazard 연속 수거 시 다음 프레임부터 Source 진행 속도가 높아지는지 확인
  - Deposit/피격 직후 HazardStack이 즉시 리셋되는지 확인
- 공통 게이트
  - `compile -> console error 0 -> EditMode -> PlayMode 스모크`

## 7. 관련 ADR
- 되돌리기 비용이 큰 결정(단일 owner, 동프레임 `수거 확정 후 리셋`, 다음 프레임 반영)은 [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)에 기록한다.

## 8. 변경 이력
- 2026-03-16: 문서 신규 작성. HazardStack 단일 owner, 프레임 snapshot, 동프레임 처리 순서, 검증 계획을 고정했다.
