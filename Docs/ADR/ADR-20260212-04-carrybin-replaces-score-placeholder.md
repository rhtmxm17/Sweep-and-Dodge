# ADR-20260212-04-carrybin-replaces-score-placeholder
> Score 플레이스홀더를 제거하고 CarryBin 기반 수거/피격 파이프라인으로 전환한 결정

## 상태
- 반영됨

## 배경
- 기존 `ScoreComponent`는 수거량 임시 확인용 플레이스홀더였다.
- 기획 문서 기준 핵심 자원은 Score가 아니라 `CarryBinLoad`이며, Hazard 피격 패널티도 CarryBin 손실을 기준으로 정의되어 있다.
- 충돌 Request-Execution 파이프라인은 이미 분리되어 있어, Execution 소비 단계에 패널티 규칙을 주입하기 적합했다.

## 결정
- `ScoreComponent`를 제거하고 CarryBin을 플레이어 단일 자원으로 사용한다.
- 플레이어에 아래 컴포넌트를 추가한다.
  - `PlayerCarryBinComponent` (`Load`, `Capacity`)
  - `PlayerHazardPenaltyConfigComponent` (`CarryLossFrac/Min/Max`, `IFrameTime`, `VacuumLockTime`)
  - `PlayerHazardPenaltyStateComponent` (`IFrameTimer`, `VacuumLockTimer`)
- Vacuum 수거 누적은 `BulletVacuumRequestSystem`에서 `PlayerCarryBinComponent.Load`에 반영한다.
- Hazard 피격 소비는 `PlayerHazardCollisionExecutionSystem`에서 처리한다.
  - 손실식: `loss = clamp(floor(load * frac), min, max)`, `load = max(0, load - loss)`
  - 기본값: `frac=0.15`, `min=5`, `max=30`
  - 피격 시 `IFrameTimer`, `VacuumLockTimer`를 설정한다.
- Request 단계에서는 `IFrameTimer > 0`이면 충돌 요청 생성을 중단한다.
- Depth 개념/배율은 이번 단계에서 제외한다.

## 대안
- Score와 CarryBin을 병행 유지
  - 장점: 기존 UI/디버그 경로 유지
  - 단점: 자원 의미 중복, 규칙 소유권 불명확
- 피격 패널티를 Request 단계에서 즉시 적용
  - 장점: 시스템 수 감소
  - 단점: Request-Execution 책임 분리 원칙 약화

## 결과
- 수거/피격 핵심 규칙이 기획 문서와 코드에서 동일한 자원(CarryBin) 기준으로 정렬된다.
- 무적/봉인 타이머가 플레이어 상태로 분리되어 Vacuum/충돌 시스템 간 결합이 명확해진다.
- Deposit/MetaScrap은 별도 시스템으로 확장 가능한 상태를 유지한다.

## 후속
- HUD에 `Load/Capacity`, `IFrameTimer`, `VacuumLockTimer` 노출.
- Deposit 접촉 시 `CarryBin -> MetaScrap` 정산 시스템 추가.
- Play Mode에서 피격 1회당 손실량 및 무적 재피격 차단 동작 스모크 테스트 수행.
