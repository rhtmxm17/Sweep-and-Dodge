# ADR-20260309-02-stage-session-reset-and-prepare-owner
> 씬 리로드/Retry/Next 재진입에서 world 재생성에 의존하지 않고, StageTopologyPrepareGroup의 explicit reset owner가 세션 상태를 초기화하도록 고정한 결정

## 배경
- `StageTopologyPrepareGroup` 도입 이후 topology apply는 fixed-tick runtime 파이프라인 밖의 prepare 계층으로 분리되었다.
- 하지만 씬 리로드, `Retry`, `Next Stage` 재진입에서는 `RunDirectorStageStateComponent`와 `StageTopologyStateComponent` 같은 session singleton이 항상 깨끗하게 초기화된다는 보장이 없었다.
- 실제 PlayMode 경로에서 stale `Completed` 상태가 남아 topology는 `Ready=1`인데도 `Idle -> Running` 재진입이 막히는 문제가 발생했고, 테스트는 별도의 수동 reset helper로 이를 우회하고 있었다.
- 이 상태는 운영 경로와 테스트 경로가 다르다는 의미이므로, world 재생성 여부와 무관하게 session state를 명시적으로 reset하는 owner가 필요했다.

## 결정
- `ECS world reset`이 아니라 `Stage Session Reset`을 채택한다.
- reset owner는 GO가 아니라 `StageTopologyPrepareGroup`의 ECS 시스템으로 둔다.
  - 순서: `StageTopologyBootstrapSystem -> StageSessionResetPrepareSystem -> StageTopologyApplyPrepareSystem`
- `StageTopologyBridge.RequestTopologyApply(stageId)`는 단순 topology apply가 아니라 `stage entry reset + topology apply`를 의미하는 주 진입점으로 확장한다.
- reset 대상은 session singleton으로 한정한다.
  - 포함:
    - `RunDirectorStageStateComponent`
    - `RunDirectorStageGateComponent`
    - `RunDirectorStageRequestComponent`
    - `RunDirectorStageSignalComponent`
    - `StageTopologyRequestComponent`
    - `StageTopologyStateComponent`
    - `StageTopologyLifecycleStateComponent`
  - 제외:
    - `StageCatalogRuntimeComponent`
    - `StageTopologyPrefabCatalogComponent`
    - config singleton
    - pool/template/static registry
- boot 시에는 1회 safety reset을 수행한다.
  - 목적: world/session 잔재 제거
  - 완료 후 bootstrap flag를 소비한다.
- explicit stage-entry reset은 `StageStartRequested`와 intro/clear gate를 보존한다.
  - same-frame `apply -> start` 모델을 깨지 않기 위함이다.
- `Running`, `ClearReady` 경계에서의 apply 요청은 reset하지 않는다.
  - 기존처럼 topology owner가 warning 후 ignore하고 현재 세션을 유지한다.

## 대안
- 대안 A: GO(`DemoShellFlowController`, bridge)가 session singleton을 직접 reset
  - 장점: 구현이 빠르다.
  - 단점: GO가 ECS owner state를 직접 쓰게 되어 소유권 경계가 무너진다.
  - 기각 사유: 현재 구조의 ownership 원칙과 충돌한다.
- 대안 B: world 재생성/씬 리로드가 항상 reset을 보장한다고 가정
  - 장점: 추가 시스템이 적다.
  - 단점: 이미 운영 PlayMode 경로에서 보장이 깨졌고, 테스트 helper에 의존하게 된다.
  - 기각 사유: 안정성 근거가 없고 운영/테스트 경로가 분리된다.
- 대안 C: `RequestStageStart()` 전에 별도 GO reset API 추가
  - 장점: 호출 지점이 명시적이다.
  - 단점: topology 진입점과 reset 진입점이 분리되어 API surface와 호출 규칙이 불필요하게 늘어난다.
  - 기각 사유: `RequestTopologyApply(stageId)` 단일 진입점에 reset 의미를 포함하는 편이 더 단순하다.

## 결과
- 긍정 효과
  - 씬 리로드, `Retry`, `Next Stage` 재진입에서 stale `Completed`/`ClearReady` 상태가 남아 새 run을 막는 문제를 prepare 계층에서 일관되게 해결할 수 있다.
  - world 재생성 여부와 무관하게 운영 경로가 session reset을 책임지므로, 테스트 helper 의존을 줄일 수 있다.
  - same-frame `apply -> start` 모델을 유지하면서도 reset/apply/start 순서를 `prepare -> apply -> transition`으로 명확히 고정할 수 있다.
- 트레이드오프
  - topology prepare 계층이 run-director session singleton 초기화 책임 일부를 함께 갖게 된다.
  - boot safety reset과 explicit stage-entry reset의 차이를 문서/테스트로 계속 유지해야 한다.

## 후속
- `TD-010`, `TD-015`에 `RequestTopologyApply(stageId)`의 reset 포함 의미와 prepare 계층 reset owner를 반영한다.
- PlayMode 테스트의 수동 reset helper는 운영 reset 경로 검증 이후 축소/제거 방향으로 정리한다.
- 이후 `Obstacle/Visual` 확장 시에도 새 run 경계 reset이 world 재생성에 의존하지 않는다는 원칙을 유지한다.
