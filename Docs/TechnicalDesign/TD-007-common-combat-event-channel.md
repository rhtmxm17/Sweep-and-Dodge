# 공통 전투 이벤트 채널 설계

## Metadata
- doc_id: `TD-007`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-28`
- related_docs:
  - [OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)
  - [TD-001-player-feedback-event-channel.md](./TD-001-player-feedback-event-channel.md)
  - [ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup.md](../ADR/ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup.md)

> 전투 핵심 이벤트를 단일 집계 경로로 수렴시켜 연출/점수/통계가 동일한 소스를 소비하도록 고정한다.

## 1. 목표
- 공통 전투 이벤트 채널의 범위를 고정한다.
- Producer/Consumer 소유권과 업데이트 순서를 고정한다.
- 기존 `PlayerHazardHit` 소비 경로를 공통 채널 경유로 이관한다.

## 2. 범위
- 포함 이벤트:
  - `Hit`
  - `Collect`
  - `Cleanup`
- 제외 이벤트:
  - `Dodge(회피)`는 기획/설계 범위에서 제거한다.
  - `Hazard 조건부 캡처 성공`은 별도 상세 경로로 분리하며, 본 채널 범위에 포함하지 않는다.

## 3. 데이터 계약
- 공통 채널은 singleton + dynamic buffer를 사용한다.
- 이벤트 공통 필드:
  - `Type`
  - `Count`
  - `Value`
  - `SourceEntity`
  - `RelatedEntity`
  - `Frame`
  - `Sequence`

### 3.1 타입별 의미
- `Hit`
  - 의미: 플레이어 피격 1건
  - 권장 값: `Count=1`, `Value=loss`
- `Collect`
  - 의미: 프레임 수집량 집계
  - 규칙: 프레임 총합만 기록한다.
  - 권장 값: `Count=1`, `Value=frameCollectedTotal`
- `Cleanup`
  - 의미: Deposit 정리 처리 1건
  - 권장 값: `Count=1`, `Value=depositedLoad`

## 4. 소유권/순서
- 파이프라인:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- Producer:
  - `BulletVacuumRequestSystem`: `Collect`
  - `PlayerHazardCollisionExecutionSystem`: `Hit`
  - `PlayerCarryBinDepositExecutionSystem`: `Cleanup`
- Consumer:
  - `ExecutionEnd` 말단의 단일 소비 시스템이 집계/누적/clear를 수행한다.

## 5. 운영 규칙
- Producer는 append-only를 유지한다.
- clear 책임은 Consumer 단일 소유로 고정한다.
- 연출/점수/통계는 공통 채널 소비 결과(집계 메트릭)만 읽는다.
- `PlayerHazardHit`는 기존 직접 소비 경로를 사용하지 않고 공통 채널 경유로 완전 이관한다.

## 6. 비목표
- `Dodge(회피)` 이벤트 추가/재도입
- `Collect`의 per-bullet 상세 기록
- `Hazard 조건부 캡처 성공`의 세부 분해 규칙 설계

## 7. 변경 이력
- 2026-02-28: 문서 신규 작성. 공통 전투 이벤트 채널 범위를 `Hit/Collect/Cleanup`으로 고정하고, `Dodge` 제거 및 `PlayerHazardHit` 공통 채널 경유 이관 규칙을 반영했다.
