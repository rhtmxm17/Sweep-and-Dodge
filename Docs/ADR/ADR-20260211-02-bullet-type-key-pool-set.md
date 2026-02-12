# ADR-20260211-02-bullet-type-key-pool-set
> 탄환 종류를 2종 고정 enum에서 TypeKey 기반 Key-Pool Set으로 전환해 다종 확장성과 소유권 일관성을 확보한다.

## 상태
- 반영됨

## 배경
- 기존 구조는 `BulletKindId(Trash/Hazard)` 고정 2종 전제로 동작해, 스테이지 입력 데이터(SO) 기반 다종 탄환 확장에 취약했다.
- Hazard와 Trash를 시각적으로 즉시 구분하기 위해 프리팹 분리가 필요했지만, 타입이 늘어날 때마다 시스템 분기를 추가하는 방식은 유지보수 비용이 빠르게 증가한다.
- 기존 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)의 소유권/업데이트 순서 규칙은 유지해야 한다.

## 결정
- 탄환 종류 모델을 `Kind(enum)` 중심에서 `TypeKey(int)` 중심으로 전환한다.
- 수거 판정 규칙은 타입과 분리하여 `BulletCaptureRuleId`로 관리한다.
  - `StandardCollectible`
  - `RiskTimedResolve`
- 풀 구성 입력은 단일 프리팹 참조 대신 `BulletPoolDefinitionBuffer`(TypeKey/Prefab/PoolSize/CaptureRule)로 통합한다.
- 런타임 풀은 키 기반 컨테이너 `FreeByKey`(TypeKey -> Entity)로 운영한다.
- Spawn 단계는 Source 상태 비율로 룰 그룹을 우선 선택하고, 해당 그룹 내 TypeKey 목록에서 dequeue 가능한 타입을 선택한다.

## 구현 메모
- 컴포넌트
  - `BulletTypeKeyComponent`, `BulletCaptureRuleComponent` 추가
  - `BulletPoolRegistryTag`, `BulletPoolDefinitionBuffer` 추가
- Authoring
  - `BulletVisualPrefabAuthoring`은 `Entries[]`를 받아 타입별 풀 정의 버퍼를 베이크
  - `BulletAuthoring`은 기본 데이터로 `TypeKey`, `CaptureRule`를 보유
- 시스템
  - `BulletPoolOwnerBootstrapSystem`: 타입별 `PoolSize`만큼 Instantiate 후 `FreeByKey`에 반납
  - `BulletSpawnFromPoolSystem`: `StandardTypeKeys`/`RiskTypeKeys` 목록과 `FreeByKey`를 사용해 스폰
  - `BulletDespawnExecutionSystem`: `BulletTypeKeyComponent`를 기준으로 동일 key 풀에 반납
  - `BulletVacuumRequestSystem`: `BulletCaptureRuleComponent` 기준으로 수거 판정 수행

## 대안
- 대안 A: Trash/Hazard 완전 분리 시스템(풀/스폰/반납 2세트 고정)
  - 장점: 현재 요구(2종)만 보면 직관적
  - 단점: 종류가 늘 때 시스템 중복과 분기 폭증
- 대안 B: 단일 프리팹 + 머티리얼/색상 차별
  - 장점: 구현 단순
  - 단점: 즉시 식별성 한계, 프리팹 분리 요구 미충족

## 결과
- 다종 탄환 확장 시 시스템 구조 변경 없이 `BulletPoolDefinitionBuffer` 엔트리 추가로 대응 가능하다.
- 타입(시각/풀)과 수거 룰(게임플레이 판정)을 분리해 설계 결합도가 낮아졌다.
- 기존 Owner/Fence 규칙(풀 접근 Begin/End 단일 소유, CellMap Writer 단일 소유)을 유지한다.

## 리스크
- `BulletFieldConfigComponent.MaxActiveTarget`은 현재 스폰 억제 로직에 미반영 상태라 명시적 활성 상한 제어가 없다.
- 룰 그룹 내 타입 선택은 현재 균등 랜덤이라, 타입별 가중치 제어가 필요할 수 있다.

## 후속
- `BulletPoolDefinitionBuffer`에 `SpawnWeight`를 추가해 그룹 내 타입 가중치 스폰을 지원한다.
- 타입별 `DequeueFailCount`, `ActivePeak` 계측을 추가해 풀 사이즈 튜닝 근거를 확보한다.
- 필요 시 `MaxActiveTarget`을 실제 스폰 가드로 연결해 과부하 프레임 보호를 강화한다.


