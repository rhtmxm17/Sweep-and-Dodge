# ADR-20260220-03-ecs-file-splitting-boundaries-by-ownership
> ECS 스크립트 분리 기준을 "라인 수"가 아니라 "소유권/업데이트 단계/트랜잭션 응집도" 기준으로 고정한다

## 상태
- 반영됨

## 배경
- 분리 전 기준으로 `BulletFieldSystem.cs`, `SpawnRequestSystems.cs`, `BulletVacuumRequestSystem.cs`가 비대해지며 탐색/리뷰 비용이 증가했다.
- 단순히 "큰 파일은 분리" 규칙을 적용하면, 실제로 항상 같이 수정되는 강결합 로직까지 인위적으로 분리되어 변경 비용이 오히려 커진다.
- 본 프로젝트는 `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 파이프라인과 Owner 책임 경계가 핵심이므로, 파일 분리 기준도 이 경계와 일치해야 한다.

## 결정
1. 1차 분리 축은 `소유권 + 업데이트 단계`로 고정한다.
- 서로 다른 Owner 책임(예: Pool Owner, Simulation Owner, Request 소비/퍼블리시)은 기본적으로 분리 후보로 본다.
- 같은 단계라도 "다른 fence 계약"을 가진 로직은 분리 후보로 본다.

2. 아래 조건을 만족하면 "명백한 묶음"으로 간주해 하나의 파일로 유지한다.
- 같은 데이터 트랜잭션을 공유하고(같은 입력/출력 버퍼, 같은 누적치), 단독 분리 시 파라미터 전달만 증가하는 경우
- 한 시스템 내부 private helper/Job이 외부 재사용 없이 해당 시스템의 정책을 완결하는 경우
- 같은 정책 단위를 함께 읽어야 의미가 보존되는 경우(예: 행동 프로파일 해석 + 판정 기하 + 요청 생성)

3. 크기 기반 보조 트리거(권고)
- `350`라인 초과 + 책임 축이 2개 이상이면 분리 검토
- `500`라인 초과 + 책임 축이 3개 이상이면 분리 우선
- 단, 2번의 "명백한 묶음" 조건을 만족하면 유지 가능

4. 파일명 규칙(권고)
- 파이프라인/SharedStatic 정의: `*System.cs` 또는 `*Pipeline*.cs`
- Owner 실행계: `*OwnerSystems.cs`
- 단계별 처리계: `*SimulationSystems.cs`, `*RequestSystems.cs`
- 이름은 "도메인 + 책임 경계"가 드러나도록 유지한다.

5. 즉시 적용
- 분리:
  - `BulletFieldSystem.cs` -> 파이프라인 그룹/프레임 카운터/SharedStatic 코어만 유지
  - `BulletPoolOwnerSystems.cs` -> Bootstrap + FieldAreaUpdate + DespawnExecution(풀 Owner 경계)
  - `BulletSimulationSystems.cs` -> Simulation + Request fence publish(CellMap writer/fence 경계)
- 유지(의도적 미분리):
  - `SpawnRequestSystems.cs`: 요청 생성(Request)과 예산 소비(Begin)가 하나의 backlog 정책 트랜잭션을 공유
  - `BulletVacuumRequestSystem.cs`: vacuum 상태 갱신/행동 프로파일 해석/CellMap 기반 요청 생성이 단일 정책 단위로 결합

## 대안
- `1 시스템 = 1 파일` 강제:
  - 장점: 탐색 경로가 기계적으로 단순하다.
  - 단점: 강결합 정책이 잘려 파일 간 점프와 인자 전달이 증가한다.
- 대형 파일 유지:
  - 장점: 관련 코드를 한 눈에 본다.
  - 단점: 책임 경계가 흐려지고 회귀 영향 범위 파악이 어려워진다.

## 결과
- 분리 기준이 소유권/순서 계약과 일치해, 구조 변경 시 회귀 분석 범위가 명확해진다.
- "큰 파일=무조건 분리"를 피하면서도, 책임 축이 다른 혼재 파일은 선별적으로 줄일 수 있다.

## 후속
- `SpawnRequestSystems.cs`와 `BulletVacuumRequestSystem.cs`는 현재 정책 단위 유지, 정책 축이 추가되어 책임 경계가 갈라질 때 재평가한다.
- 기준 점검 시점: 신규 시스템 추가로 파일이 `+150`라인 이상 증가하거나, 동일 파일 내에서 다른 Owner/fence 계약이 새로 생기는 시점.
