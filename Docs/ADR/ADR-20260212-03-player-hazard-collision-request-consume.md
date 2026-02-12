# ADR-20260212-03-player-hazard-collision-request-consume
> 위험탄 태그/전용 CellMap 분리와 충돌 Request-Execution 분리를 하나의 파이프라인 설계로 통합한 결정

## 상태
- 반영됨

## 배경
- 플레이어와 위험탄 충돌 확인(로그 출력)까지만 우선 구현하되, 이후 넉백/패널티/무적 프레임 확장에 지장이 없어야 한다.
- 대량 탄환 환경에서 충돌 로직이 디스폰/풀 반납 책임을 침범하면 파이프라인 소유권이 흔들릴 수 있다.
- 기존 단일 `CellMap`은 모든 활성 탄을 포함하므로, 플레이어 주변 먼지/자원이 많은 상황에서 위험탄 충돌 후보 수가 과도하게 커질 수 있다.
- 동일 프레임에서 Vacuum/수명 만료와 충돌이 동시에 발생할 때 우선순위를 명확히 해야 한다.

## 결정
- 위험탄 카테고리를 `BulletHazardTag`(enableable)로 명시한다.
  - `BulletCaptureRuleId.RiskTimedResolve`인 탄은 생성/풀 초기화 시 `BulletHazardTag`를 enabled로 유지한다.
- SpatialHash를 2개로 운용한다.
  - `CellMap`: 전체 활성 탄
  - `HazardCellMap`: `BulletHazardTag` enabled + 활성 탄
- 플레이어 위험탄 충돌 시스템은 `HazardCellMap`만 조회한다.
- 위험탄 충돌은 `BulletRequestGroup`에서 요청만 생성하고, `BulletExecutionEndGroup`에서 소비한다.
- 플레이어 충돌 요청은 `PlayerHazardHitRequestTag`(enableable)로 표현한다.
- 충돌은 프레임당 1회만 기록한다.
- 동일 프레임에서 제거 요청과 충돌이 동시에 가능할 경우, 제거를 우선한다.
  - 구현 규칙: `BulletDespawnRequestTag`가 이미 enabled인 탄은 충돌 후보에서 제외한다.
- 충돌 확인 단계에서도 해당 탄에 `BulletDespawnRequestTag`를 enable하여 중복 충돌을 완화한다.

## 구현 메모
- `BulletAuthoring`
  - `BulletHazardTag`를 항상 추가하고, 초기 enabled를 `CaptureRule` 기준으로 설정한다.
- `BulletPoolOwnerBootstrapSystem`
  - 풀 엔티티에 대해 `BulletPoolDefinitionBuffer.CaptureRule` 기준으로 `BulletHazardTag` enabled 상태를 재설정해 분류를 고정한다.
- `BulletSimulationSystem`
  - `CellMap`/`HazardCellMap` 각각 Clear/Build 후 동일 `CellMapFence`로 Request read 순서를 시퀀싱한다.
- `PlayerHazardCollisionRequestSystem`
  - 그룹: `BulletRequestGroup`
  - 순서: `UpdateAfter(BulletVacuumRequestSystem)`로 Vacuum 기반 제거 요청이 먼저 반영되도록 고정
  - 데이터: `BulletFieldShared.HazardCellMap` read, `BulletDespawnRequestTag` write, `PlayerHazardHitRequestTag` write
- `PlayerHazardCollisionExecutionSystem`
  - 그룹: `BulletExecutionEndGroup`
  - 역할: 요청 소비(현재는 로그 출력) + 태그 disable

## 대안
- 단일 CellMap 유지 + `CaptureRule` 필터만 사용
  - 장점: 구현 단순
  - 단점: 공간 조회 후보 축소가 되지 않아 고밀도 장면에서 충돌 판정 비용이 불리함
- 충돌 시 즉시 데미지/넉백/무적 처리까지 포함
  - 장점: 단일 시스템으로 완결
  - 단점: 초기 디버깅 난도 증가, 규칙 변경 시 파급 범위 확대
- 충돌 요청을 일반 컴포넌트 카운터로 유지
  - 장점: 수치 누적이 직관적
  - 단점: "프레임당 1회" 제약과 소비 시점 관리가 번거로움

## 결과
- 충돌 사실 확인 단계와 게임플레이 효과 적용 단계를 분리해 확장성이 확보됨.
- 위험탄 충돌 판정의 공간 조회 대상이 위험탄으로 제한되어 후보 수가 감소함.
- 제거 우선/프레임당 1회 규칙이 코드 순서와 조건으로 명시됨.
- 위험탄 분류 기준이 데이터(`CaptureRule`)와 런타임 태그(`BulletHazardTag`)로 연결됨.
- 향후 넉백/패널티/무적은 Execution 소비 시스템에서 안전하게 확장 가능.

## 후속
- Play Mode에서 위험탄 접촉 시 로그가 프레임당 1회만 출력되는지 확인.
- Vacuum 캡처 타이밍과 동시 발생 시 충돌 로그가 발생하지 않는지 확인.
- Entities Profiler에서 충돌 요청 시스템 후보 수/실행 시간이 감소했는지 측정.
