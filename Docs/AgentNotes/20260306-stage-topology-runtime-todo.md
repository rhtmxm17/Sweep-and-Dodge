# Stage Topology Runtime TODO
> Agent 작업 메모 및 사용자 점검용 임시 문서. 프로젝트 관리 대상 아님.

## 목적
- `StageLayoutSO + StageDefinitionSO + StageCatalogSO`만으로 `Source/Deposit` 배치가 성립하는 구조로 전환한다.
- 씬/SubScene에 미리 배치된 `BulletSourceAuthoring`, `DepositPointAuthoring` 개수에 스테이지 성립이 의존하지 않도록 한다.
- 데이터 기반 스테이지 배치를 `RunDirector` 상태머신과 계약상 분리하고, stage start보다 선행하도록 고정한다.

## 현재 상태 요약
- 현재 `StageCatalogApplyExecutionBeginSystem`은 기존 runtime `Source/Deposit` 엔티티를 `StableId` 기준으로 찾아 재배치/재정의만 수행한다.
- 따라서 씬에 baked 된 `BulletSourceAuthoring` / `DepositPointAuthoring`가 충분히 존재해야 stage apply가 성립한다.
- `StageDefinitionSO`는 source 정의 SSOT이고, `BulletSourceAuthoring`는 더 이상 pattern/threshold/state의 운영 SSOT가 아니다.
- 다만 `BulletSourceAuthoring`는 여전히 runtime source 엔티티의 존재성, pollution/grid/runtime buffer 초기 bake의 기반이다.
- 현재 stage apply는 `BulletExecutionBeginGroup`, stage transition은 `BulletRequestGroup`에서 돌아가므로 실행 순서상 stage start보다 먼저 적용된다.
- 하지만 입력 계약은 아직 `RunDirectorStageRequestComponent`에 묶여 있어 topology apply가 상태머신 바깥 책임으로 완전히 분리되지는 않았다.

## 문제 정의
1. 현재 구조는 `data-driven instantiate`가 아니라 `prebaked entity remap`이다.
2. Stage 수요와 runtime source entity 공급량이 씬 authoring 수에 결합되어 있다.
3. topology apply와 stage state request가 같은 request singleton에 들어 있어 책임 경계가 흐리다.
4. 최종 목표인 `StageCatalog` 단일 운영 계약이 topology 존재성까지 포함하지 못하고 있다.

## 목표 상태
1. `StageCatalogSO`의 selected entry만으로 해당 stage의 `Source/Deposit` topology가 생성/갱신된다.
2. stage 씬은 `BulletSourceAuthoring`를 스테이지 개수만큼 미리 배치하지 않는다.
3. topology apply는 `RunDirector` 상태 전이보다 선행할 뿐 아니라, request/state 계약도 분리된다.
4. `RunDirectorStageTransitionSystem`은 topology ready 이후에만 `Idle -> Running`을 허용한다.

## 권장 설계 방향
### 1. topology request/state 분리
- 신규 `StageTopologyRequestComponent`
  - `RequestedStageId`
  - `ApplyRequested`
  - 필요 시 `RequestVersion`
- 신규 `StageTopologyStateComponent`
  - `AppliedStageId`
  - `Ready`
  - 필요 시 `AppliedVersion`
- `RunDirectorStageRequestComponent`는 `StageStartRequested`, `ConfirmPressed` 등 stage state request만 유지

### 2. owner 시스템 분리/정리
- `StageCatalogApplyExecutionBeginSystem`의 역할을 topology owner로 재정의하거나, 이름을 `StageTopologyApplyExecutionBeginSystem`으로 변경 검토
- 책임:
  - selected stage resolve
  - `StageLayoutSO` 기반 source/deposit entity set reconcile
  - `StageDefinitionSO` 기반 source definition apply
  - topology ready 갱신

### 3. runtime template prefab 기반 instantiate/reconcile
- 권장: source/deposit runtime template prefab 도입
- apply owner가 layout stable id 집합 기준으로
  - 없으면 instantiate
  - 있으면 update
  - 불필요하면 disable/recycle
- 순수 archetype 코드 생성은 초기화 복제가 커서 비권장

### 4. authoring 역할 재정의
- `BulletSourceAuthoring`
  - stage scene 배치 수단에서 제거
  - runtime template prefab authoring 또는 별도 명칭으로 분리 검토
- `DepositPointAuthoring`
  - 동일하게 runtime template 역할로 이동 검토
- stage 편집 씬은 `StageSourceMarker`, `StageDepositMarker` 중심 유지

## 다음 설계 논의에서 확정할 항목
1. topology owner 시스템 명칭과 책임
2. `StageTopologyRequestComponent` / `StageTopologyStateComponent` 스키마
3. runtime template prefab 참조 위치
   - 별도 `StageRuntimePrefabCatalogSO`
   - bridge reference
   - bootstrap singleton authoring
4. instantiate 이후 stable id / transform / field / pollution / runtime buffer 초기화 규칙
5. stage 변경 시 reconcile 정책
   - destroy vs disable/recycle
   - source/deposit별 정책 분리 여부
6. topology ready gate를 `RunDirectorStageTransitionSystem`에 어떻게 연결할지
7. `DemoShellFlowController` / `RunDirectorStageBridge` 입력 API를 topology apply와 stage start로 어떻게 분리할지

## 권장 작업 순서
### Phase B2-1
- topology request/state 분리
- topology ready gate 도입
- stage start contract 정리

### Phase B2-2
- source/deposit runtime template prefab 도입
- instantiate/reconcile owner 구현
- 기존 prebaked source/deposit 의존 제거

### Phase B2-3
- `BulletSourceAuthoring` / `DepositPointAuthoring` 명칭 및 책임 정리
- generator/tooling 입력 source 재검토

## 체크 포인트
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 계약 유지
- topology apply owner는 단일 writer 유지
- structural change 비용이 커지므로 create/destroy 빈도와 재사용 전략을 함께 설계할 것
- pollution grid/runtime buffer 초기화는 template instantiate 후 deterministic rebuild 가능해야 함
- stage change 중간 프레임에서 `SourceClipRequestBuildSystem` 등 request 계열이 빈 topology/부분 topology를 읽지 않도록 ready gate 또는 적용 순서 보장 필요

## 현재 결론
- 지금 구조는 stage apply 시점은 맞지만, topology 존재성은 아직 data-driven이 아니다.
- 다음 설계 페이즈의 핵심은 `remap model -> instantiate/reconcile model` 전환이다.
