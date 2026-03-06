# 스테이지 데이터(Definition) / 레이아웃(Layout) Dual Catalog 파이프라인 (TD-015)

## Metadata
- doc_id: `TD-015`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-06`
- related_docs:
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)
  - [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
  - [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)

> v1.5는 `정의/레이아웃 분리(Dual Catalog)`를 유지하면서, `StageDefinitionSO`의 Source 정의/패턴을 `StageMapApplyExecutionBeginSystem`이 런타임에서 함께 소비한다. `Obstacle/Visual`, stage-level override, 운영 fail-fast는 다음 페이즈 이월이다.

## 1. 목표 / 비목표
### 1.1 목표
- 스테이지 메타/패턴 정의와 물리 레이아웃 데이터를 분리한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 순서로 고정한다.
- `RunDirectorStageBridge -> RequestStageMapApply(stageId)` 단일 입력 경로를 유지한다.
- `StageDefinitionSO.SourceBindings`를 런타임 Source에 적용한다.
- `SourceClipRequestBuildSystem`은 `RunDirectorStageState == Running`에서만 요청을 생성한다.

### 1.2 비목표 (다음 페이즈 이월)
- `RunDirectorStageConfig/RunProgressDirectorConfig/SpawnRequestPolicy` stage-level override 런타임 적용
- Deposit/Obstacle/Visual 확장 스키마의 런타임 소비
- 운영 빌드 fail-fast 정책 전환

## 2. 현재 상태(코드 기준)
- 런타임 적용은 `StageMapApplyExecutionBeginSystem`이 `StageMapCatalogSO` + `StageCatalogSO`를 `StageId`로 조회해 `Source`는 layout+definition 결합 적용, `Deposit`은 layout 적용을 수행한다.
- `DemoShellFlowController`는 `StageCatalogSO`가 있으면 카탈로그에서 `StageProfiles`를 구성하고, 없으면 기존 직렬화 `StageProfiles`를 fallback으로 사용한다.
- `RunDirectorStageBridge`는 `RequestStageMapApply(stageId)` 호출 시 `StageMapCatalogRuntimeComponent`와 `StageCatalogRuntimeComponent`를 함께 최신화한다.
- `SourceClipRequestBuildSystem`은 stage state gate를 가져 `Running` 전에는 clip request를 만들지 않는다.

## 3. 소유권 (Owner / Writer)
- Definition 생성/동기화 Owner: `StageDefinitionGenerator`
- Layout 생성 Owner: `StageLayoutCatalogGenerator` (`StageLayoutSO` 대상)
- Catalog 조립 Owner: `StageCatalogComposer`
- StageCatalog 검증 Owner: `StageCatalogValidationRules`
- 런타임 StageMap/Definition 적용 Owner: `StageMapApplyExecutionBeginSystem` (ExecutionBegin)
- GO -> ECS 요청 Writer: `RunDirectorStageBridge` 단일 Writer

## 4. 업데이트 순서
- 파이프라인 계약 유지:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- StageMap 적용 순서:
  - `BulletPoolOwnerBootstrapSystem`
  - `StageMapApplyExecutionBeginSystem`
  - `BulletFieldAreaUpdateSystem`

## 5. 데이터 구조 / 제약
### 5.1 신규 SO
- `StageCatalogSO`
  - `int SchemaVersion`
  - `StageCatalogEntry[] Entries`
- `StageCatalogEntry`
  - `bool Enabled`
  - `string EntryKey`
  - `StageDefinitionSO Definition`
  - `StageLayoutSO Layout`
- `StageDefinitionSO`
  - `int StageId`
  - `string DisplayName`
  - `bool IsFinalStage`
  - `float StageTimeLimitSec`
  - `StageSourceBinding[] SourceBindings`
- `StageLayoutSO`
  - `int StageId`
  - `StageSourceLayoutData[] Sources`
  - `StageDepositLayoutData[] Deposits`
  - `StageObstacleLayoutData[] Obstacles`
  - `StageVisualLayoutData[] Visuals`

### 5.2 StageDefinition Source 패턴
- `StageSourceBinding`
  - `SourceStableId`
  - `InitialSourceState`
  - `ThresholdWeakened`, `ThresholdDepleted`
  - `SustainSlotBinding[]`, `EventSlotBinding[]`
- `SustainSlotBinding`
  - `State`, `Lane`, `WaveClipSO[] Clips`, `float[] Weights`
- `EventSlotBinding`
  - `TriggerState`, `WaveClipSO[] EventClips`

### 5.3 규칙
- 로비/진행 순서: `StageCatalogSO.Entries` 배열 순서
- 로비/진행 대상: `Enabled == true` 엔트리만
- `EntryKey`는 카탈로그 내 유니크
- `Definition.StageId == Layout.StageId` 필수
- Source 조인 키: `StageId + SourceStableId`
- `StageTimeLimitSec > 0`
- `ThresholdDepleted >= ThresholdWeakened >= 0`
- sustain/event slot clip null 금지
- clip phase 검증:
  - sustain slot: `SourceWavePhaseId.Sustain`
  - event slot: `SourceWavePhaseId.OnStateEnterOnce`
- 불일치 정책: `Warn + partial apply`
  - 정의-레이아웃 stage/source 누락은 경고 기록
  - 일치 항목만 적용
- 런타임 Source 조인 규칙
  - 조인 키: `StageId + SourceStableId`
  - layout active=false 또는 layout 미매핑: safe-disable
  - definition binding 미매핑: safe-disable
  - definition stage 미존재: layout-only apply + baked clip pattern 유지

## 6. 에디터 파이프라인
- `StageLayoutCatalogGenerator`
  - 단일 스테이지 `StageLayoutSO` 생성
  - 기존 `StageMapCatalogSO` 생성 경로는 v1 호환 유지(deprecated)
- `StageDefinitionGenerator`
  - Stage별 Source binding 템플릿 동기화
- `StageCatalogComposer`
  - `Definition/Layout`를 명시적 페어 엔트리로 조립
- `ContentValidationRunner`
  - `StageCatalogSO` 수집/검증 체인 추가

## 7. 런타임 반영(v1.5)
- `DemoShellFlowController`
  - 시작 시 `StageCatalogSO`를 읽어 런타임 `StageProfiles` 구성
  - 미할당/유효 엔트리 없음 시 기존 `StageProfiles` fallback
- `EnterStagePlay`
  - 선택 엔트리의 `Definition.StageId`를 사용해 `RequestStageMapApply(stageId)` 호출
- `StageMapApplyExecutionBeginSystem`
  - `RequestedStageId`로 layout stage와 definition stage를 각각 resolve
  - `Source`는 layout+definition 결합 적용
  - definition stage 누락 시 layout-only apply
  - `Deposit`은 기존 layout 경로 유지
- `StageDefinitionSO.SourceBindings`
  - threshold / initial state / clip pattern / runtime buffer / pollution init 재구성에 사용
  - `OnStateEnterOnce`는 initial apply 직후 자동 발화하지 않음
  - stage-level override, Deposit/Obstacle/Visual 소비는 이월
- `RunDirectorStageBridge`
  - `StageCatalogRuntimeComponent`를 함께 publish
  - legacy `StageMapCatalogSO` 미할당 시 `StageCatalog.Entry.Layout`로 runtime 호환 `StageMapCatalogSO`를 합성 가능

## 8. 진행 상태
1. Dual Catalog 타입(`StageCatalogSO`, `StageDefinitionSO`, `StageLayoutSO`) 추가 (`완료`)
2. Generator/Composer/ValidationRules 추가 (`완료`)
3. ContentValidationRunner 체인 추가 (`완료`)
4. DemoShell StageCatalog 로딩 + fallback 경로 반영 (`완료`)
5. StageDefinition Source runtime apply (`완료`)
6. stage-level override / Deposit/Obstacle/Visual 런타임 소비 (`다음 페이즈`)

## 9. 검증 계획 / 합격 기준
- 공통
  - compile error 0
  - console error 0
  - EditMode pass
  - PlayMode smoke pass
- EditMode
  - `StageCatalogValidationRulesTests`:
    - null ref
    - StageId mismatch
    - EntryKey duplicate
    - enabled StageId duplicate
    - threshold 순서 오류
    - clip phase 오류
    - source stableId 불일치 경고
  - `DemoShellFlowControllerStageCatalogTests`:
    - Entries 순서 반영
    - Enabled 필터
    - fallback 동작
- PlayMode
  - DemoShell 회귀 스모크(`Title -> Lobby -> Stage -> Result -> Retry/Next`)

## 10. 관련 ADR
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](../ADR/ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md)
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](../ADR/ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md)
