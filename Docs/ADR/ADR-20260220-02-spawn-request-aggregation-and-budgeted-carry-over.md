# ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over
> Spawn 요청을 aggregated 단위로 전환하고, 풀 부족 시 Budget Cap + bounded carry-over 정책을 채택한다

## 상태
- 채택됨

## 배경
- 파이프라인은 `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`로 고정되어 있고, Despawn은 요청-소비 구조가 이미 정착되어 있다.
- Spawn도 동일한 요청-소비 경계로 정리해야 소유권과 업데이트 순서가 명확해진다.
- 대량 엔티티 상황에서 풀 부족이 발생할 수 있으며, 즉흥적인 처리 방식은 프레임 스파이크 또는 페이싱 왜곡을 유발한다.
- ProjectOps 문서는 운영 계획 중심으로 유지하고, 상세 설계 합의는 ADR에 기록한다.

## 결정
1. Spawn 지연 허용
- Spawn은 `1프레임 지연 허용`을 기본으로 한다.
- 요청 생성 프레임과 실제 스폰 소비 프레임을 분리해 Owner 소비 경계를 명확히 한다.

2. Spawn 요청 단위
- Spawn 요청은 `aggregated` 단위(예: `Source + TypeKey + Count`)로 관리한다.
- 탄환 1개 단위 요청을 직접 누적하지 않는다.

3. 풀 부족 기본 정책
- 기본 정책은 `Budget Cap + bounded carry-over`로 한다.
- 프레임당 스폰 실행량은 예산(`Budget Cap`)으로 상한을 둔다.
- 예산 초과분은 이월하되, `최대 이월량`과 `유효 프레임`으로 상한을 둔다.

4. 운영 목표와 가드레일
- 완료판 목표: 정상/스트레스 시나리오에서 `최대 이월량`/`유효 프레임` 제한에 실제로 도달하지 않도록 튜닝한다.
- 제한은 안정성 안전장치이며, 일상적인 경로가 되어서는 안 된다.

5. 관측/경고 규약
- 최소 관측 지표:
  - `frame_spawn_budget_used`
  - `pending_backlog_count`
  - `oldest_backlog_age`
  - `drop_count`
  - `expired_by_age`
- 경고 임계치(기본값):
  - backlog 사용률 `>= 70%`: `Warning`
  - backlog 사용률 `>= 85%`: `Warning(High)`
  - `oldest_backlog_age >= 유효프레임 - 1`: `Warning(Critical)`
- 제한 초과/만료 발생 시 `Error` 로그로 승격하고 카운터를 누적한다.
- 경고 로그는 레이트 리밋을 적용해 로그 폭주를 방지한다.

## 대안
- `Drop`(부족분 즉시 폐기):
  - 장점: 구현이 단순하고 프레임 안정성이 높다.
  - 단점: 패턴 의도 대비 스폰량 손실이 커져 페이싱 왜곡이 크다.
- `Carry-over only`(무제한 이월):
  - 장점: 총량 보존이 쉽다.
  - 단점: backlog 폭증 시 버스트 스폰으로 프레임 스파이크가 발생한다.
- `Unbounded budget`(예산 상한 없음):
  - 장점: 지연이 적다.
  - 단점: 최악 구간에서 프레임 안정성을 보장하기 어렵다.

## 결과
- Spawn도 요청-소비 경계가 명확해져 소유권 일관성이 높아진다.
- 프레임 예산 중심으로 최악 케이스를 제어할 수 있다.
- 대신 backlog/임계치/로그 운영이 필수이며, 튜닝 품질이 성능 안정성에 직접 영향을 준다.

## 후속
- Spawn 요청 버퍼/컴포넌트 스키마(`Source + TypeKey + Count + Age`)를 확정한다.
- ExecutionBegin 소비 시스템에 Budget Cap + carry-over 처리와 임계치 로그를 구현한다.
- 스모크/스트레스 루틴에 `drop_count == 0`, `expired_by_age == 0` 검증을 포함한다.
