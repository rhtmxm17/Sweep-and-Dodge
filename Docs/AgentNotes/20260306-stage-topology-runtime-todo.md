# StageTopology Hardening Phase TODO
> Agent 작업 메모 및 사용자 점검용 임시 문서. 프로젝트 관리 대상 아님.

## 목적
- 현재 구현된 `StageTopology v1`를 `Obstacle / Visual topology` 확장 전에 견고한 기반으로 정리한다.
- `StageCatalogSO + StageLayoutSO + StageDefinitionSO` 기반 topology 계층을 `RunDirector` 상태머신과 계약상 분리한다.
- runtime template authoring의 명칭과 책임을 정리해 stage editing authoring과 runtime topology template authoring을 혼동하지 않게 만든다.

## 현재 상태 요약
- `StageTopologyApplyExecutionBeginSystem`이 `StageCatalogRuntimeComponent + StageTopologyPrefabCatalogComponent + StageTopologyRequestComponent + StageTopologyStateComponent`를 사용해 `Source/Deposit` topology를 reconcile한다.
- `Source/Deposit` 존재성은 더 이상 씬/SubScene의 prebaked 개수에 의존하지 않는다.
- `RunDirectorStageTransitionSystem`은 topology `Ready`를 읽고 `Idle -> Running`을 gate한다.
- 하지만 topology request/state의 public 진입점은 여전히 `RunDirectorStageBridge`에 모여 있어, topology layer가 `RunDirector` 바깥 독립 계약으로 완전히 분리되었다고 보긴 어렵다.
- `BulletSourceAuthoring`, `DepositPointAuthoring`는 실제로는 runtime template prefab bake용 역할인데, 이름과 일부 문서/테스트 맥락은 여전히 stage scene 배치 authoring처럼 읽힌다.
- 현재 template 전략은 `Source 1종 / Deposit 1종`으로 고정되어 있으며, `Obstacle / Visual` 확장 계약은 아직 없다.

## 핵심 문제 정의
1. topology request/state가 구조적으로는 분리됐지만, public API와 브리지 책임은 아직 `RunDirector`와 결합돼 있다.
2. runtime template authoring의 명칭/역할이 불명확해 이후 `Obstacle / Visual` 확장 시 책임 혼동이 다시 생길 가능성이 높다.
3. topology-owned entity lifecycle, failure policy, template 공급 계약이 `Source/Deposit v1` 수준에 머물러 있어 일반화 수준이 낮다.
4. 문서와 테스트는 `StageTopology v1` 동작은 반영했지만, “확장 가능한 topology layer 계약”으로는 아직 덜 정리됐다.

## Hardening 목표 상태
1. topology request/state의 계약과 public API가 `RunDirector` stage state request와 명확히 분리된다.
2. runtime template authoring은 이름과 용도가 분리되어, stage editing authoring과 혼동되지 않는다.
3. topology-owned entity lifecycle와 failure policy가 `Source/Deposit` 공통 규칙으로 정리되고, 이후 `Obstacle / Visual`을 같은 방식으로 수용할 수 있다.
4. template catalog가 단순 `Source/Deposit` 보관함이 아니라 topology kind별 공급 계약으로 정리된다.
5. TD/ADR/테스트가 `StageTopology hardening` 기준으로 최신화된다.

## 구현 계획
### Phase H1. request/state 계약 완전 분리
- 목표
  - topology apply를 `RunDirector` stage state request와 별도 계층으로 고정한다.
- 작업
  - `RunDirectorStageBridge`에서 topology API surface를 분리한다.
    - 예: topology 전용 bridge/adapter 분리 여부 검토
    - 최소안은 기존 bridge 내부를 topology writer와 run-director writer로 명시 분리
  - `RunDirectorStageRequestComponent`는 stage state request만 다루고, topology request/state는 topology 계층 문맥으로만 서술되도록 문서/테스트/명칭 정리
  - `DemoShellFlowController`는 topology 요청과 start 요청의 계약만 가진다고 명시
  - `RunDirectorStageTransitionSystem`의 topology ready 의존을 TD에 별도 항목으로 명문화
- 완료 기준
  - topology request/state를 `RunDirector` request의 일부로 읽히게 하는 명칭/문서/테스트 잔재 제거
  - topology ready gate 계약이 TD/테스트와 일치

### Phase H2. runtime template authoring 명칭/역할 분리
- 목표
  - `BulletSourceAuthoring`, `DepositPointAuthoring`가 더 이상 stage scene authoring처럼 읽히지 않게 한다.
- 작업
  - 신규 명칭으로 분리
    - 예: `SourceRuntimeTemplateAuthoring`
    - 예: `DepositRuntimeTemplateAuthoring`
  - 기존 authoring은 prefab migration을 고려해 thin wrapper 또는 이동 경로를 검토
  - `StageTopologyPrefabCatalogSO`와 template prefab 자산은 새 명칭 기준으로 연결
  - stage editing 씬/문서에서는 `StageSourceMarker`, `StageDepositMarker`만 배치 대상임을 명시
- 완료 기준
  - runtime template prefab authoring과 stage layout authoring의 용도 구분이 코드/문서/씬에서 명확
  - `BulletSourceAuthoring`, `DepositPointAuthoring`가 남더라도 legacy alias 또는 migration wrapper 수준으로 축소

### Phase H3. topology-owned lifecycle 규칙 고정
- 목표
  - 이후 `Obstacle / Visual`이 같은 규칙으로 붙을 수 있게 공통 lifecycle 계약을 만든다.
- 작업
  - 공통 태그/식별/재사용 규칙 문서화
    - create
    - reuse
    - disable-to-pool
    - stable id overwrite
    - stage change cleanup
  - extras disable 정책과 재사용 우선순위 명시
  - topology apply 실패 시 이전 stage owned entity를 어떻게 유지/비활성화할지 정책 고정
  - `warn + partial apply`와 `Ready=0 hard gate` 경계 사례를 표로 정리
- 완료 기준
  - `Source/Deposit`에 이미 적용된 reconcile 규칙이 명시적 공통 계약으로 문서화
  - 향후 새 topology kind 추가 시 예외 규칙 없이 확장 가능

### Phase H4. topology template catalog 일반화 준비
- 목표
  - `StageTopologyPrefabCatalogSO`를 이후 `Obstacle / Visual` 확장 가능한 형태로 정리한다.
- 작업
  - 현재는 kind별 단일 template라는 사실을 명시
  - 이후 추가될 topology kind를 위한 필드/검증 규칙 방향만 정리
  - null template / unsupported kind / kind-specific optionality 정책 정의
- 완료 기준
  - 현재 구현은 그대로 두되, 다음 확장이 “구조 변경”이 아니라 “필드 추가”가 되도록 계약 정리

### Phase H5. 문서 / ADR / 테스트 재정렬
- 목표
  - `StageTopology`를 확장 가능한 레이어로 보는 기준을 문서와 테스트에 고정한다.
- 작업
  - `TD-015`에 topology layer 책임을 별도 섹션으로 승격
  - `TD-010`에 `RunDirector`와 topology의 경계, ready gate, request 분리 규칙을 명시
  - 필요 시 ADR 추가
    - 주제: `StageTopology hardening: topology request/state boundary + runtime template authoring split`
  - 테스트 축 재정리
    - topology request/state boundary
    - topology ready gate
    - template resolve failure
    - owned entity lifecycle/reuse
- 완료 기준
  - hardening 결과가 코드/테스트/TD/ADR에서 같은 용어와 계약으로 표현됨

## 구현 순서 제안
1. `H1 request/state 계약 분리`
2. `H2 runtime template authoring 명칭/역할 분리`
3. `H3 lifecycle / failure policy 문서화 및 코드 정리`
4. `H4 template catalog 일반화 준비`
5. `H5 TD/ADR/테스트 재정렬`

## 범위 밖
- `Obstacle / Visual topology` 실제 instantiate/reconcile 구현
- multi-template key 지원
- stage-level override 재도입
- 운영 빌드 fail-fast 정책 변경

## 설계 논의에서 확정할 항목
1. topology public API를 기존 `RunDirectorStageBridge` 내부 분리로 충분히 볼지, 별도 bridge로 독립시킬지
2. runtime template authoring rename 시 migration 전략
   - immediate rename
   - wrapper + obsolete
3. topology-owned entity 공통 메타가 필요한지
   - kind enum
   - template revision
   - owner token
4. failure policy에서 “이전 stage topology 유지 vs 전부 비활성화” 기준
5. ADR 신규 작성 여부

## 체크 포인트
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 계약 유지
- topology owner는 단일 writer 유지
- topology ready gate는 `Idle -> Running`뿐 아니라 향후 retry/reenter 경로에서도 일관돼야 함
- runtime template authoring rename이 prefab/scene serialization을 깨지 않도록 migration 순서를 설계할 것
- `Obstacle / Visual` 확장 논의 전에 hardening 산출물이 TD 기준 SSOT가 되어야 함

## 현재 결론
- `StageTopology v1`은 `Source/Deposit` 범위에서 구현/검증 완료 상태다.
- 다음 단계는 기능 확장이 아니라 `StageTopology hardening`이다.
- 특히 선행 우선순위는 `request/state 경계 정리`와 `runtime template authoring 명칭/역할 분리`다.
