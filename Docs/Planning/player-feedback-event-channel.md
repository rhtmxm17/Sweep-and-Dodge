# ECS 피드백 이벤트 채널 설계 정리

본 문서는 다음 채팅에서 설계/구현 논의를 빠르게 이어가기 위한 설계 정리다.
대상 이벤트는 아래 4가지다.
- 흡입 시작 차단(Vacuum blocked)
- 스폰 소스 고갈(Source depleted 전이)
- 피격(Hazard hit)
- 피격 넉백(Impulse)

---

## 0. 현재 코드 상태 (2026-02-19 기준)

### 0.1 흡입 시작 차단
- 구현됨.
- 위치:
  - `Assets/_Project/02_Scripts/ECS/Systems/BulletVacuumRequestSystem.cs`
  - `Assets/_Project/02_Scripts/ECS/Components/PlayerComponents.cs`
- 요약:
  - `CarryBin`이 full이면 흡입 시작을 거부.
  - `PlayerVacuumStartBlockFeedbackComponent`에 프레임 이벤트로 기록.
  - 사유 enum: `VacuumStartBlockReasonId.CarryBinFull`.

### 0.2 스폰 소스 고갈
- 상태 전이는 이미 존재.
- 위치:
  - `Assets/_Project/02_Scripts/ECS/Systems/BulletVacuumRequestSystem.cs` (`TryAccumulateDepletion`)
- 요약:
  - 수거 누적 시 `SourceSpawnComponent.State`가 단방향 전이(`Normal -> Weakened -> Depleted`).
  - 현재는 "전이 발생" 자체를 별도 피드백 채널로 발행하지 않음.

### 0.3 피격
- 요청/소비 파이프라인 구현됨.
- 위치:
  - `Assets/_Project/02_Scripts/ECS/Systems/PlayerHazardCollisionSystem.cs`
- 요약:
  - Request 단계에서 충돌 감지 후 `PlayerHazardHitRequestTag` enable.
  - ExecutionEnd 단계에서 손실/타이머 적용 후 요청 consume(disable).
  - 현재는 로직 처리 위주이며, UI/연출용 공통 이벤트 채널은 없음.

---

## 1. 문제 인식

현재 피드백 데이터가 "이벤트별 산개" 상태다.
- 흡입 차단은 별도 컴포넌트에 기록됨.
- 피격은 request tag consume 패턴으로 처리되나, UI 소비 전용 payload가 없음.
- 소스 상태 전이는 내부 상태 변경만 있고, 전이 사실을 외부가 안정적으로 구독하기 어려움.

향후 HUD/사운드/VFX/GO Bridge까지 고려하면,
"한 프레임에 발생한 플레이어 피드백 이벤트를 단일 포맷으로 수집"하는 채널이 필요하다.

---

## 2. 제안: 타입별 버퍼 분리 채널 (A안)

### 2.1 데이터 모델(확정)
- 플레이어 엔티티 루트에 목적별 버퍼를 분리해 둔다.
  - `DynamicBuffer<PlayerUiFeedbackEventBufferElement>`
  - `DynamicBuffer<PlayerImpulseEventBufferElement>`
- 각 Owner 시스템은 자신이 책임지는 버퍼에만 append 한다.
- 소비자는 자신의 버퍼만 읽고 처리한다.
  - UI/HUD/VFX 소비자: `PlayerUiFeedbackEventBufferElement`만 소비
  - GO Bridge 소비자: `PlayerImpulseEventBufferElement`만 소비

`PlayerUiFeedbackEventBufferElement` 예시 필드:
- `Type` (`enum PlayerUiFeedbackEventType : byte`)
- `Reason` (`byte`)
- `Value` (`int`, loss/가산량 등)
- `RelatedEntity` (`Entity`)
- `Frame` (`uint`, 디버깅/중복억제용)
- `Sequence` (`uint`, 프레임 내 순서)

`PlayerImpulseEventBufferElement` 예시 필드:
- `Reason` (`byte`)
- `DirX`, `DirZ` (`float`, Impulse 방향)
- `Magnitude` (`float`, 고정값 사용)
- `Frame` (`uint`)
- `Sequence` (`uint`)

### 2.2 UI 이벤트 타입(1차 확정)
- `VacuumStartBlocked`
- `SourceStateChanged`
- `PlayerHazardHit`

### 2.3 Reason 코드(초안)
- `VacuumStartBlocked`:
  - `CarryBinFull`
  - (확장 예약) `VacuumLocked`, `CooldownActive` 등
- `SourceStateChanged`:
  - `ToWeakened`, `ToDepleted`
- `PlayerHazardHit`:
  - `Default`
- `PlayerImpulse`:
  - `Default`

---

## 3. 파이프라인/소유권 정렬

프로젝트 고정 파이프라인:
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`

권장 규칙:
- 이벤트 생성은 "상태가 실제로 확정되는 Owner 시스템"에서만.
- Request 단계 이벤트는 Request 단계에서 append.
- Execution 결과 이벤트(예: 최종 loss)는 ExecutionEnd에서 append.
- Impulse 이벤트는 피격 확정 시점에 발행하고, 실제 위치/이동 반영은 GO Bridge 소유로 처리한다.

이벤트별 권장 발행 지점:
- `VacuumStartBlocked`:
  - `BulletVacuumRequestSystem` 내부 시작 게이트에서 append.
- `SourceStateChanged`:
  - `TryAccumulateDepletion`에서 state 전이 확정 시 append.
  - 전이 직전/직후 상태를 비교하여 전이 발생 프레임에만 발행.
- `PlayerHazardHit`:
  - `PlayerHazardCollisionExecutionSystem`에서 loss 확정 후 UI 버퍼 append.
- `PlayerImpulse`:
  - `PlayerHazardCollisionExecutionSystem`에서 피격 확정 직후 Impulse 버퍼 append.
  - 강도는 고정값을 사용한다.
  - `loss`는 Impulse 강도에 직접 반영하지 않고, VFX 강도/길이에만 반영한다.

---

## 4. 기존 컴포넌트와의 관계

현재 추가된 `PlayerVacuumStartBlockFeedbackComponent`는
- 단기적으로 유지 가능(빠른 HUD 적용).
- 중기적으로는 UI 피드백 버퍼(`PlayerUiFeedbackEventBufferElement`)로 흡수 권장.

이관 방법(권장):
1. 분리 버퍼 채널(UI/Impulse) 도입
2. Vacuum blocked 이벤트를 UI 버퍼에도 중복 발행
3. 소비처(UI/Bridge) 전환 완료 후 기존 전용 컴포넌트 제거 여부 결정

---

## 4.1 버퍼 증설 기준(규칙)

버퍼 증설 1차 기준은 "소비자 경계"로 둔다. 단, 아래 3가지를 함께 만족할 때 신규 버퍼를 만든다.
- 소비자 실행 그룹/타이밍이 다르다.
- payload 형태가 다르고, 공용 스키마 유지 이점이 낮다.
- clear/중복 억제 정책을 독립 운영해야 한다.

위 조건을 만족하지 않으면 기존 버퍼 내 타입 추가를 우선한다.

---

## 5. 구현 단계안 (리스크 낮은 순서)

1. `PlayerUiFeedbackEventBufferElement`, `PlayerImpulseEventBufferElement` 및 enum 정의
2. Player Baker에서 두 버퍼 부착
3. `BulletVacuumRequestSystem`에서 UI 버퍼로 `VacuumStartBlocked` 발행
4. `PlayerHazardCollisionExecutionSystem`에서
   - UI 버퍼로 `PlayerHazardHit` 발행(loss 포함)
   - Impulse 버퍼로 `PlayerImpulse` 발행(고정 강도)
5. 소스 상태 전이 시점에서 UI 버퍼로 `SourceStateChanged` 발행
6. 소비 시스템 추가
   - `PlayerUiFeedbackConsumeSystem` (UI/HUD/VFX)
   - `PlayerImpulseConsumeSystem` (GO Bridge)
7. clear 책임
   - 각 버퍼는 자신의 consume 시스템에서 clear
   - 시스템 순서를 `Producer -> Consumer -> Clear`로 고정

---

## 6. 오픈 이슈

- 이벤트 중복 억제 전략:
  - 고급 dedupe 규칙(타입별 세부 합산/병합 정책)
- GO Bridge 소비 방식:
  - 매 프레임 풀링 vs 변경 감지형
- 성능:
  - 이벤트량이 많은 프레임에서 buffer clamp/드롭 정책

---

## 6.1 운영 규칙 확정안 (2026-02-19)

### A. clear 시점/소유자
- 버퍼별 소비 시스템이 자기 버퍼를 소비 직후 clear 한다.
  - UI 버퍼: `PlayerUiFeedbackConsumeSystem`에서 소비 후 `Clear()`
  - Impulse 버퍼: `PlayerImpulseConsumeSystem`에서 소비 후 `Clear()`
- 별도 전역 clear 시스템은 두지 않는다.

### B. 중복 억제 최소 규칙
- 기본 정책: 프레임 내 중복 발행 허용.
- UI 버퍼 예외: 동일 프레임, 동일 `Type`, 동일 `RelatedEntity`는 1건으로 병합 가능.
- Impulse 버퍼: 중복 허용(필요 시 소비자에서 누적량 clamp).
- 공통 기록 필드: `Frame`, `Sequence`를 필수로 기록한다.

### C. 순서 고정
- 각 버퍼는 `Producer -> Consumer -> Clear` 순서를 강제한다.
- UI 버퍼:
  - Producer: Request/ExecutionEnd Owner 시스템
  - Consumer/Clear: `PlayerUiFeedbackConsumeSystem`
- Impulse 버퍼:
  - Producer: `PlayerHazardCollisionExecutionSystem`
  - Consumer/Clear: `PlayerImpulseConsumeSystem`
- 구현 규칙:
  - Consumer 시스템은 모든 Producer 뒤에 `UpdateAfter`로 배치
  - clear는 Consumer 내부에서만 수행

### D. 다건 처리 순서
- 동일 프레임 다건 이벤트는 `Sequence` 오름차순으로 소비한다.

### E. 초기 capacity
- UI 버퍼 초기 capacity: 16
- Impulse 버퍼 초기 capacity: 8
- 수치는 프로파일링 기반으로 조정한다.

---

## 7. 결정 사항 (2026-02-19)

- 단일 공통 버퍼 대신 타입별 버퍼 분리(A안) 채택
- Impulse는 UI 버퍼와 분리된 전용 버퍼로 발행/소비
- Impulse 강도 정책: 고정값
- `loss` 반영 정책: Impulse 강도에는 미반영, VFX 강도/길이에 반영
- clear 소유권/중복 억제 최소 규칙/처리 순서 규칙 확정
