# StageTopology Prepare Group Implementation Plan
> Agent 작업 메모 및 사용자 점검용 자유 문서. 프로젝트 관리 기준 문서 아님.

## 배경
- 현재 기준 구현 대상은 `StageTopologyApplyPrepareSystem`이며, `StageTopologyPrepareGroup` 소속으로 옮긴다.
- 실행 순서상 topology apply가 stage runtime보다 먼저 돌기는 하지만, 구조상으로는 fixed-tick bullet 파이프라인 일부처럼 보인다.
- 플레이 경험 측면에서는 각 스테이지가 페이드 아웃/페이드 인으로 완전히 구분될 예정이므로, topology 준비 단계와 stage runtime 단계도 시스템 그룹 수준에서 분리하는 편이 자연스럽다.
- `StageTopology`는 `FixedTickStepRuntimeComponent`나 logic `dt`를 직접 사용하지 않으므로 tick 비의존 prepare 단계로 다루는 것이 타당하다.

## 목표
- `StageTopology`를 fixed-tick stage runtime 파이프라인에서 분리한다.
- topology prepare는 `SimulationSystemGroup` 직속의 별도 그룹에서 수행한다.
- stage runtime은 topology가 `Ready`인 상태에서만 실질 동작하도록 gate를 강화한다.
- 기존 H1~H5 계약은 유지한다.
  - topology input: `StageTopologyBridge`
  - stage state input: `RunDirectorStageBridge`
  - topology owner: 단일 system
  - long-cycle stage 기준 `boundary-only apply`

## 목표 상태
```text
SimulationSystemGroup
  -> StageTopologyPrepareGroup
       -> StageTopologyBootstrapSystem
       -> StageTopologyApplySystem
  -> FixedTickRootGroup
       -> BulletFramePipelineGroup
            -> BulletExecutionBeginGroup
            -> BulletSimulationGroup
            -> BulletRequestGroup
            -> BulletExecutionEndGroup
```

의미:
- `StageTopologyPrepareGroup`
  - tick 비의존
  - stage 경계에서 topology request를 처리
  - Source / Deposit / 이후 Obstacle / Visual topology 준비 계층
- `BulletFramePipelineGroup`
  - topology 준비가 끝난 뒤에만 실질적으로 stage runtime을 수행하는 tick-driven 파이프라인

## 구현 범위
### 1. 그룹/시스템 구조
- 신규 `StageTopologyPrepareGroup` 추가
  - `SimulationSystemGroup` 직속
  - `FixedTickRootGroup`보다 먼저 업데이트
- 신규 `StageTopologyBootstrapSystem` 추가
  - `StageTopologyPrepareGroup`의 `OrderFirst`
  - topology singleton 보장 책임 분리
- `StageTopologyApplyPrepareSystem`
  - 소속은 `StageTopologyPrepareGroup`

### 2. bootstrap 책임 분리
- `BulletPoolOwnerBootstrapSystem`에 남길 것
  - bullet/runtime 파이프라인 singleton
  - pool/cellmap/runtime debug/hud 등 bullet 쪽 bootstrap
- `StageTopologyBootstrapSystem`으로 이동할 것
  - `StageTopologyRequestComponent`
  - `StageTopologyStateComponent`
  - `StageTopologyLifecycleStateComponent`
  - `StageCatalogRuntimeComponent`
  - `StageTopologyPrefabCatalogComponent`
- 의도
  - topology 계층은 bullet fixed-tick bootstrap에 종속되지 않게 한다.

### 3. topology apply system 변경
- `StageTopologyApplyPrepareSystem`
  - `StageTopologyPrepareGroup`에서 topology apply 수행
  - `BulletPoolOwnerBootstrapSystem` 이후 정렬 제거
  - topology bootstrap 이후만 전제
- 유지할 규칙
  - `StageTopologyRequestComponent` one-shot consume
  - `SelectedStageId / AppliedStageId / Ready`
  - `CurrentAppliedVersion`
  - infrastructure failure 시 기존 applied topology 유지
  - `Running / ClearReady` 중 reapply ignore

### 4. runtime gate 정리
- `BulletFramePipelineGroup` 자체를 끄지는 않는다.
- 대신 “stage runtime 의미를 가지는 시스템”은 `StageTopologyState.Ready == 1` 전에는 no-op 또는 `RequireForUpdate` gate를 둔다.
- 1차 점검 대상
  - `BulletSpawnFromPoolSystem`
  - `SourceClipRequestBuildSystem`
  - `RunProgressDirectorSystems`
  - `SourcePollutionUpdateSystem`
  - `SpawnRequestSystems`
  - stage runtime 의미의 source/deposit 소비 시스템
- 원칙
  - bootstrap/debug/metrics 계열은 필요 시 계속 돌아도 된다.
  - gameplay state를 진전시키는 시스템만 topology ready를 강하게 요구한다.

### 5. RunDirector 경계 유지
- `RunDirectorStageTransitionSystem`
  - 계속 `BulletRequestGroup`
  - topology `Ready == 1`
  - `AppliedStageId == SelectedStageId`
  - intro/min-idle gate 충족 시 `Idle -> Running`
- `StageTopology`는 stage start를 직접 수행하지 않는다.
- topology는 “prepare + ready 제공”까지만 책임진다.

## 단계별 작업 순서
### Phase 1. 그룹/부트스트랩 분리
- `StageTopologyPrepareGroup` 추가
- `StageTopologyBootstrapSystem` 추가
- topology singleton bootstrap 이동
- compile + EditMode 기본 회귀

### Phase 2. topology owner 이동
- `StageTopologyApplyPrepareSystem`을 새 그룹으로 이동
- 정렬 속성/주석/TD 반영
- topology apply 회귀 테스트 수정

### Phase 3. runtime gate 강화
- fixed-tick runtime 시스템 중 topology ready 전제 시스템 식별
- no-op / `RequireForUpdate` gate 적용
- `SampleScene` PlayMode 회귀 확인

### Phase 4. 문서/테스트 마감
- `TD-010`
  - topology prepare group과 run-director 경계 반영
- `TD-015`
  - topology prepare 계층과 fixed-tick runtime 계층 분리 반영
- 테스트 축 재정리
  - topology prepare
  - runtime gate
  - retry/next/lobby re-enter

## 예상 효과
- 구조적으로 `StageTopology`가 fixed-tick bullet 파이프라인 일부처럼 보이지 않게 된다.
- 플레이 경험상의 `전환 -> topology 준비 -> 플레이 시작`이 시스템 구조에도 드러난다.
- `Obstacle / Visual topology`를 같은 prepare 계층에 붙이기 쉬워진다.
- topology 문제와 stage runtime 문제를 테스트/디버깅에서 분리하기 쉬워진다.

## 예상 리스크
- bootstrap 분리 과정에서 singleton 누락 회귀 가능
- 일부 runtime 시스템이 암묵적으로 topology entity 존재를 전제하고 있으면 초기 프레임 회귀 가능
- `Retry / Next / DemoComplete / staged boot` 경로에서 stage state와 topology state 초기화 순서를 다시 확인해야 함

## 검증 계획
- compile
- console error 0
- EditMode
  - `StageTopologyApplyPrepareSystemTests`
  - `StageTopologyBridgeTests`
  - `RunDirectorStageBridgeTests`
  - `BulletPipelineContractTests`
- PlayMode
  - `BulletPlayModeSmokeTests`
  - `Title -> Lobby -> Stage -> Result -> Retry/Next -> DemoComplete`
  - same-frame `apply -> start` 유지 확인

## 현재 권장 결정
- `StageTopology`는 `FixedTick` 바깥의 prepare 계층으로 분리한다.
- 완전한 그룹 on/off 제어 대신, topology prepare group 분리 + runtime hard gate 방식으로 간다.
- 이후 `Obstacle / Visual` 확장은 이 구조 위에서 진행한다.
