# ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup
> 공통 전투 이벤트 채널 범위를 `Hit/Collect/Cleanup`으로 고정하고, `PlayerHazardHit`를 공통 채널 경유로 이관한 결정

## 상태
- 합의됨 (문서 반영)

## 배경
- `OPS-001 #9`는 전투 이벤트를 단일 집계 경로로 수렴하는 목표를 가진다.
- 현재 구현은 피격/수집/정리 정보가 시스템별로 분산되어 있어, 연출/점수/통계가 동일한 소스를 공유하기 어렵다.
- 대량 엔티티 루프 특성상 per-bullet 이벤트 스트림은 운영 리스크가 커서, 집계 단위 계약이 필요하다.

## 결정
1. 공통 전투 이벤트 채널 범위
- 채널 범위를 `Hit`, `Collect`, `Cleanup`으로 고정한다.
- `Dodge(회피)`는 기획/설계 범위에서 제거한다.
- `Hazard 조건부 캡처 성공`은 별도 상세 경로로 분리하고 본 채널에는 포함하지 않는다.

2. 수집 집계 단위
- `Collect`는 프레임 총합만 기록한다.
- 기본 해석은 `Count=1`, `Value=frameCollectedTotal`로 둔다.

3. PlayerHazardHit 이관
- `PlayerHazardHit`는 기존 직접 소비 경로를 유지하지 않는다.
- `Hit`를 공통 전투 이벤트 채널로 발행하고, 소비자는 공통 채널 결과를 기준으로 처리한다.

4. 소유권/업데이트 순서
- Producer:
  - `BulletVacuumRequestSystem` (`Collect`)
  - `PlayerHazardCollisionExecutionSystem` (`Hit`)
  - `PlayerCarryBinDepositExecutionSystem` (`Cleanup`)
- Consumer:
  - `ExecutionEnd` 말단 단일 시스템이 집계/누적/clear 단일 책임을 갖는다.
- 규칙:
  - Producer append-only
  - clear는 Consumer 단일 소유

## 대안
- 대안 1: `Dodge`를 유지하고 채널 범위를 넓게 유지
  - 장점: 확장성이 있어 보인다.
  - 단점: 현재 기획 범위와 불일치하고, 정의 불명확 이벤트가 채널 계약을 오염시킨다.

- 대안 2: `Collect`를 per-bullet로 기록
  - 장점: 상세 분석에 유리하다.
  - 단점: 대량 엔티티 프레임에서 이벤트량 폭증 리스크가 크다.

- 대안 3: `PlayerHazardHit` 직접 소비 경로 유지
  - 장점: 기존 코드 변경량이 적다.
  - 단점: 단일 소스 원칙이 깨져 연출/점수/통계 일관성이 낮아진다.

## 결과
- 전투 이벤트 소비 경로가 단일 채널 기준으로 정렬된다.
- 채널 범위가 기획 범위와 일치한다.
- 이벤트 폭주 리스크를 낮춘 집계 단위 계약(`Collect` 프레임 총합)을 확보한다.

## 후속
1. 공통 전투 이벤트 채널 컴포넌트/집계 시스템 구현
2. Producer 시스템에서 `Hit/Collect/Cleanup` 발행 연결
3. 기존 `PlayerHazardHit` 직접 소비 로직 제거 및 공통 채널 경유로 치환
