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
- `StageTopologyBridge`가 topology apply 요청과 `StageCatalogRuntimeComponent` publish를 담당하고, `RunDirectorStageBridge`는 stage state/start-confirm-complete 계약만 담당한다.
- `StageTopologyStateComponent`의 write owner는 `StageTopologyApplyExecutionBeginSystem` 단일 owner다. bridge는 state를 직접 쓰지 않는다.
- `DemoShellFlowController`는 `StageTopologyBridge.RequestTopologyApply(stageId)` 후 `RunDirectorStageBridge.RequestStageStart()`를 같은 프레임에 연속 요청하고, ECS gate가 topology ready 이전 start를 보류한다.
- `SourceRuntimeTemplateAuthoring`, `DepositRuntimeTemplateAuthoring`가 runtime template authoring의 주 경로가 되었고, `BulletSourceAuthoring`, `DepositPointAuthoring`는 legacy wrapper로 축소되었다.
- 현재 template 전략은 `Source 1종 / Deposit 1종`으로 고정되어 있으며, `Obstacle / Visual` 확장 계약은 아직 없다.

## Hardening 진행 상태
### H1. request/state 계약 완전 분리
- 상태: 완료
- 반영 내용
  - `StageTopologyBridge` 신규 추가
  - `RunDirectorStageBridge`에서 topology 관련 필드/API 제거
  - `DemoShellFlowController`가 topology bridge와 run-director bridge를 분리 참조
  - `TD-010`, `TD-015`를 새 bridge 경계 기준으로 갱신
  - EditMode/PlayMode 검증 통과
- 완료 기준 판정
  - topology request/state를 `RunDirector` request의 일부로 읽히게 하는 코드/문서/테스트 잔재는 정리됨
  - topology ready gate 계약이 코드/TD/테스트와 일치함

### H2. runtime template authoring 명칭/역할 분리
- 상태: 완료
- 반영 내용
  - `SourceRuntimeTemplateAuthoring`, `DepositRuntimeTemplateAuthoring` 신규 추가
  - 공용 base/helper로 neutral template bake 책임 이동
  - `BulletSourceAuthoring`, `DepositPointAuthoring`를 legacy runtime template alias wrapper로 축소
  - `StageDefinitionGenerator`, `ContentValidationRunner`, `ContentValidationRules`가 구/신 타입을 모두 읽도록 정리
  - `StageTopology` template prefab을 새 authoring 기준으로 마이그레이션
  - `TD-015`를 새 용어 기준으로 갱신
- 완료 기준 판정
  - runtime template prefab authoring과 stage layout authoring의 용도 구분이 코드/문서/프리팹에서 명확
  - legacy 타입은 한 페이즈 동안 migration wrapper로만 남아 있음

### H3. topology-owned lifecycle 규칙 고정
- 상태: 다음 우선순위
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

### H4. topology template catalog 일반화 준비
- 상태: H3 이후
- 목표
  - `StageTopologyPrefabCatalogSO`를 이후 `Obstacle / Visual` 확장 가능한 형태로 정리한다.
- 작업
  - 현재는 kind별 단일 template라는 사실을 명시
  - 이후 추가될 topology kind를 위한 필드/검증 규칙 방향만 정리
  - null template / unsupported kind / kind-specific optionality 정책 정의

### H5. 문서 / ADR / 테스트 재정렬
- 상태: 일부 선반영, 최종 정리는 H3~H4 이후
- 작업
  - `TD-015`에 topology layer 책임을 별도 섹션으로 승격
  - `TD-010`에 `RunDirector`와 topology의 경계, ready gate, request 분리 규칙을 유지 갱신
  - 필요 시 ADR 추가
    - 주제: `StageTopology hardening: lifecycle/failure policy + template catalog generalization`
  - 테스트 축 재정리
    - topology request/state boundary
    - topology ready gate
    - template resolve failure
    - owned entity lifecycle/reuse

## 다음 작업 계획(H3 요약)
1. topology-owned lifecycle 공통 규칙 확정
- `Source / Deposit / Obstacle / Visual` 공통으로 적용 가능한 create / reuse / disable-to-pool / cleanup 규칙을 고정
- stable id overwrite, disabled pool 재사용 우선순위, stage change cleanup 기준을 문서/테스트로 명시

2. failure policy 경계 정리
- `Ready=0 hard gate`와 `warn + partial apply`의 구분 기준을 표로 정리
- topology apply 실패 시 이전 stage owned entity를 유지할지, 전부 disable할지 정책을 확정

3. 테스트 축 보강
- owned entity lifecycle/reuse
- topology apply failure / recovery
- retry / next / re-enter 시 topology state 일관성

4. 문서 반영 준비
- `TD-015`, `TD-010`에 lifecycle/failure policy를 반영할 초안 정리
- 필요 시 hardening 결정용 ADR 추가 여부 판단

## 범위 밖
- `Obstacle / Visual topology` 실제 instantiate/reconcile 구현
- multi-template key 지원
- stage-level override 재도입
- 운영 빌드 fail-fast 정책 변경

## 설계 논의에서 남은 확정 항목
1. topology-owned entity 공통 메타가 필요한지
   - kind enum
   - template revision
   - owner token
2. failure policy에서 “이전 stage topology 유지 vs 전부 비활성화” 기준
3. H4에서 multi-kind template catalog의 최소 필드 수준
4. ADR 신규 작성 여부

## 체크 포인트
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 계약 유지
- topology owner는 단일 writer 유지
- topology ready gate는 `Idle -> Running`뿐 아니라 향후 retry/reenter 경로에서도 일관돼야 함
- runtime template authoring rename 이후에도 prefab/scene serialization 경고가 없어야 함
- `Obstacle / Visual` 확장 논의 전에 hardening 산출물이 TD 기준 SSOT가 되어야 함

## 현재 결론
- `H1`은 완료됐다.
- `H2`는 완료됐다.
- 다음 단계는 `H3: topology-owned lifecycle / failure policy 고정`이다.
- 그 다음에 `H4 template catalog 일반화`, `H5 문서/ADR/테스트 재정렬` 순서로 진행한다.
