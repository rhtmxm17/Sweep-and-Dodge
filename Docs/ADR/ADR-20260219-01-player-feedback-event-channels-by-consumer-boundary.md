# ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary
> 플레이어 피드백 이벤트를 소비자 경계 기준(UI/Impulse)으로 분리해 소유권, 소비 순서, clear 책임을 고정한 결정이다.

## 배경
- 기존 피드백 데이터는 이벤트별로 산개되어 있었다.
  - 흡입 시작 차단은 `PlayerVacuumStartBlockFeedbackComponent`에 기록
  - 피격은 `PlayerHazardHitRequestTag` consume 중심으로 처리
  - 소스 상태 전이는 내부 상태 변경만 존재
- HUD/VFX/GO Bridge까지 확장하려면 프레임 이벤트를 안정적으로 전달하는 채널이 필요했다.
- 프로젝트 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`) 및 Owner 단일 책임 원칙과 정렬되어야 했다.

## 결정
- 단일 공통 버퍼 대신 소비자 경계 기반 분리 버퍼(A안)를 채택한다.
  - UI/HUD/VFX용: `DynamicBuffer<PlayerUiFeedbackEventBufferElement>`
  - GO Bridge Impulse용: `DynamicBuffer<PlayerImpulseEventBufferElement>`
- Producer는 Owner 시스템에서만 append 한다.
  - `BulletVacuumRequestSystem`: `VacuumStartBlocked`, `SourceStateChanged` (UI 버퍼)
  - `PlayerHazardCollisionExecutionSystem`: `PlayerHazardHit` (UI 버퍼), `PlayerImpulse` (Impulse 버퍼)
- 소비/clear 책임은 버퍼별 소비 시스템으로 고정한다.
  - `PlayerUiFeedbackConsumeSystem`: UI 버퍼 소비 후 clear
  - `PlayerImpulseConsumeSystem`: Impulse 버퍼 소비 후 clear
- 순서 규칙을 고정한다.
  - 버퍼별 `Producer -> Consumer -> Clear`
  - Consumer는 Producer 이후에 `UpdateAfter`로 배치
- 운영 규칙을 함께 확정한다.
  - 기본적으로 프레임 내 중복 발행 허용
  - UI 버퍼는 동일 `Frame + Type + RelatedEntity` 병합 가능
  - Impulse 강도는 고정값, `loss`는 VFX 강도/길이에 반영
  - `Frame`, `Sequence`를 이벤트 기록 필수 필드로 사용
  - 초기 capacity: UI 16, Impulse 8

## 대안 비교
### 대안 1: 단일 공통 버퍼 + Type 분기
- 장점:
  - 구현 시작 비용이 낮고 구조가 단순하다.
- 단점:
  - payload 확장 시 필드 비대화 가능성이 높다.
  - 다중 소비자에서 clear/순서 충돌 리스크가 커진다.
  - gameplay 이벤트(Impulse)와 UI 이벤트 수명주기 분리가 어렵다.

### 대안 2: 이벤트 엔티티(타입별 컴포넌트)
- 장점:
  - 타입별 스키마 자유도가 가장 높다.
- 단점:
  - 구조 변경 비용과 수명 관리 비용이 증가한다.
  - 본 프로젝트의 고빈도 프레임 이벤트 처리에는 과한 복잡도다.

### 채택안(A안) 선택 이유
- DOTS 관점에서 데이터 레이아웃과 소비 경계를 명확히 유지하면서 확장성/운영 안정성 균형이 가장 좋다.
- 소유권, 업데이트 순서, clear 책임을 코드와 문서에 일치시킬 수 있다.

## 결과
- 이벤트 추가 시 "소비자 경계 + 타이밍 + payload 형태 + clear 정책 독립성"으로 버퍼 증설 여부를 판단한다.
- 기존 `PlayerVacuumStartBlockFeedbackComponent`는 과도기 병행 후 제거 대상이다.
- 소비 시스템은 현재 clear 책임 우선으로 도입되었고, 이후 UI/GO 브리지 연결 지점으로 확장한다.

## 리스크 및 후속
- 리스크:
  - GO Bridge 소비 방식(풀링/변경감지) 최종 선택 전까지 브리지 구현 방식 변동 가능
  - 고급 dedupe 정책 미확정 시 연출 요구사항에 따른 추가 조정 필요
  - 이벤트 폭주 프레임에서 clamp/드롭 정책 튜닝 필요
- 후속 작업:
  1. UI/GO 브리지 실제 소비 로직 연결
  2. profile 기반 capacity/clamp 재조정
  3. 과도기 전용 컴포넌트 제거 시점 확정

## 관련 문서
- [Docs/TechnicalDesign/TD-001-player-feedback-event-channel.md](../TechnicalDesign/TD-001-player-feedback-event-channel.md)
- [Docs/ADR/ADR-20260212-03-player-hazard-collision-request-consume.md](ADR-20260212-03-player-hazard-collision-request-consume.md)
