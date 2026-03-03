# ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume
> 입력을 행동 ID에 직접 매핑하지 않고 슬롯(Primary/Secondary)을 거쳐 해석하며, 동작 중 들어온 입력은 전환 없이 즉시 소비하도록 고정한 결정이다.

## 상태
- 반영됨
- 다음에서 일부 대체됨: [ADR: 플레이어 런타임 권한 ECS 이전 + 표현 브리지 경계 고정](ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md)

## 배경
- 입력 계층이 행동 ID를 직접 소유하면 키바인딩/캐릭터 전환 시 결합도가 높아진다.
- 행동 분기 확장 시 입력 시스템 수정 없이 슬롯 매핑만 바꾸는 구조가 필요하다.
- 동작 중 입력 처리 정책이 모호하면 전환/큐잉 동작이 프레임마다 불안정해질 수 있다.

## 결정
- 입력 경로를 아래 2단계로 고정한다.
  - `Input -> Slot(Primary/Secondary) -> ActionId`
- 구성 요소:
  - `PlayerCleanupActionSlotMapComponent`가 슬롯별 ActionId를 소유
  - `PlayerEcsBridge`는 슬롯 요청만 전송
  - `PlayerGoSyncSystem`이 슬롯을 ActionId로 해석해 `PendingActionId`에 기록
- 동작 중 입력 정책:
  - `Vacuum`이 이미 활성(`IsActive != 0`)이면 행동 전환 요청은 무시하고 소비한다.
  - `Vacuum`이 이미 활성(`IsActive != 0`)이면 추가 발동 요청도 무시하고 소비한다.

## 대안 비교
### 대안 1: Input -> ActionId 직접 매핑
- 장점: 초기 구현 간단
- 단점: 입력 계층과 행동 데이터 결합, 확장성 저하

### 대안 2: 동작 중 입력 큐잉(다음 발동 예약)
- 장점: 입력 손실 최소화
- 단점: 예약/취소 규칙 복잡화, 체감 일관성 저하 가능

### 채택안 선택 이유
- 슬롯 계층으로 입력 결합도를 낮추고, 동작 중 입력 소비 정책으로 런타임 상태를 단순하게 유지할 수 있다.

## 결과
- 입력 바인딩 변경과 행동 매핑 변경을 분리할 수 있게 됐다.
- 동작 중 전환/발동 큐가 남지 않아 프레임 동작이 예측 가능해졌다.

## 리스크 및 후속
- 리스크:
  - 동작 중 입력 무시는 플레이어가 "입력 누락"으로 느낄 수 있다.
- 후속:
  1. 필요 시 UI 피드백(`입력 무시됨`) 이벤트 발행 여부 검토
  2. 캐릭터 선택 화면에서 슬롯 맵 재구성 경로 연결

## 관련 문서
- [Docs/ADR/ADR-20260219-02-cleanup-action-branching-by-profile.md](ADR-20260219-02-cleanup-action-branching-by-profile.md)
- [Docs/ADR/ADR-20260219-03-player-cleanup-action-profile-so-externalization.md](ADR-20260219-03-player-cleanup-action-profile-so-externalization.md)
