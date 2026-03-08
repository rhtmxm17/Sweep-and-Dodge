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
- 상태: 완료
- 반영 내용
  - topology apply를 `boundary-only`로 고정
    - 허용: `Idle`, `Completed`, 초기 부트스트랩 경계
    - 비허용: `Running`, `ClearReady`
  - 장주기 스테이지(2분+) 안정성 우선 규칙 반영
    - mid-run topology reapply는 거부
    - retry / next / lobby return만 정식 재진입 경로로 간주
  - lifecycle/failure policy 고정
    - `instantiate -> reuse -> mapped-active -> pooled-disabled`
    - 성공 apply 후 extras는 `disable-to-pool`
    - 실패 apply 시 기존 applied topology 유지
    - infra failure는 `Ready=0 hard gate`
    - definition/source mismatch는 `warn + partial apply`
  - 관련 ADR/TD 반영
    - `ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md`
    - `TD-010`, `TD-015`
  - H3 검증 보강 및 회귀 수정
    - EditMode/PlayMode 테스트 추가
    - operational scene PlayMode state reset helper 정리
- 완료 기준 판정
  - long-cycle stage 전제를 둔 topology lifecycle/failure policy가 코드/TD/ADR/테스트에 일치함
  - topology apply 실패 시 현재 stage 유지 원칙이 검증으로 닫힘

### H4. topology template catalog 일반화 준비
- 상태: 완료
- 목표
  - `StageTopologyPrefabCatalogSO`를 이후 `Obstacle / Visual` 확장 가능한 형태로 정리한다.
- 반영 내용
  - `StageTopologyPrefabCatalogSO`는 리스트형으로 바꾸지 않고 `SourceTemplatePrefab`, `DepositTemplatePrefab` 고정 필드를 유지
  - 공통 topology 메타 도입
    - `StageTopologyKind`
    - `StageTopologyOwnedComponent.Kind`
    - `StageTopologyOwnedComponent.LastAppliedVersion`
  - lifecycle singleton 도입
    - `StageTopologyLifecycleStateComponent.CurrentAppliedVersion`
  - 성공 apply에서만 version 증가, stamp되지 않은 owned entity는 `disable-to-pool`
  - `TemplateRevision`, `OwnerToken`, 공통 `StableId`는 도입하지 않음
  - `StageTopologyPrefabCatalogSO` content validation 추가
    - `SourceTemplatePrefab`, `DepositTemplatePrefab` null만 오류로 취급
- 완료 기준 판정
  - 이후 `Obstacle / Visual` 확장 시에도 catalog shape 자체는 explicit field add 방식으로 확장 가능
  - 공통 lifecycle stamp가 코드/테스트/TD에 반영됨

### H5. 문서 / ADR / 테스트 재정렬
- 상태: H4와 병행 또는 직후
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

## 검증 상태
- compile: H4 반영 후 재검증 예정
- console error: H4 반영 후 재검증 예정
- EditMode: H4 반영 후 재검증 예정
- PlayMode: H4 반영 후 재검증 예정

## 다음 작업 계획(H5 요약)
1. 문서 / ADR / 테스트 재정렬
- `TD-015`의 topology layer 섹션 구조 정리
- `TD-010`의 bridge / ready gate / boundary-only apply 계약 정리
- 필요 시 `template catalog generalization` ADR 추가 여부 판단
- 테스트 축을 `boundary / lifecycle / failure / template resolution` 관점으로 재정리

2. `Obstacle / Visual` 설계 진입 조건 정리
- H4/H5 결과를 SSOT로 고정한 뒤에만 `Obstacle / Visual topology` 설계로 진입
- 즉, 다음 확장은 구현보다 계약 정리가 먼저다

## 범위 밖
- `Obstacle / Visual topology` 실제 instantiate/reconcile 구현
- multi-template key 지원
- stage-level override 재도입
- 운영 빌드 fail-fast 정책 변경

## 설계 논의에서 남은 확정 항목
1. H5에서 별도 ADR이 필요한지
2. `Obstacle / Visual` 확장 시 `StageTopologyKind`에 값을 즉시 사용할지, 구현 시점에 문서상 활성화할지
3. explicit field add 방식의 catalog 확장을 어느 시점에 문서로 고정할지

## 체크 포인트
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 계약 유지
- topology owner는 단일 writer 유지
- topology ready gate는 `Idle -> Running`뿐 아니라 향후 retry/reenter 경로에서도 일관돼야 함
- runtime template authoring rename 이후에도 prefab/scene serialization 경고가 없어야 함
- `Obstacle / Visual` 확장 논의 전에 hardening 산출물이 TD 기준 SSOT가 되어야 함

## 현재 결론
- `H1`은 완료됐다.
- `H2`는 완료됐다.
- `H3`는 완료됐다.
- `H4`는 완료됐다.
- 다음 단계는 `H5: 문서/ADR/테스트 재정렬`이다.
- `Obstacle / Visual topology` 설계는 H4/H5 산출물이 고정된 뒤에 시작한다.
