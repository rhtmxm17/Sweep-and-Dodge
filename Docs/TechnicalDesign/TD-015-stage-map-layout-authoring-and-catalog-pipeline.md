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

> v1은 `정의/레이아웃 분리(Dual Catalog)`를 도입하고, 런타임 맵 적용 Owner(`StageMapApplyExecutionBeginSystem`)는 유지한다. `StageDefinitionSO`의 Source 패턴(`WaveClipSO`)은 v1에서 저장/검증까지 수행하고 ECS 재구성 적용은 이월한다.

## 1. 목표 / 비목표
### 1.1 목표
- 스테이지 메타/패턴 정의와 물리 레이아웃 데이터를 분리한다.
- 로비/진행 순서를 `StageCatalogSO.Entries` 순서로 고정한다.
- `RunDirectorStageBridge -> RequestStageMapApply(stageId)` 단일 입력 경로를 유지한다.
- v1에서 Source 패턴 데이터(`WaveClipSO`)를 `StageDefinitionSO`에 저장하고 검증한다.

### 1.2 비목표 (다음 페이즈 이월)
- `StageDefinitionSO`의 `SustainSlots/EventSlots`를 ECS `SourceClipPatternBuffer`로 런타임 재구성
- `RunDirectorStageConfig/RunProgressDirectorConfig/SpawnRequestPolicy` stage-level override 런타임 적용
- Deposit/Obstacle/Visual 확장 스키마의 런타임 소비
- 운영 빌드 fail-fast 정책 전환

## 2. 현재 상태(코드 기준)
- 런타임 레이아웃 적용은 `StageMapApplyExecutionBeginSystem`이 `StageMapCatalogSO`를 `StageId`로 조회해 `Source/Deposit`만 반영한다.
- `DemoShellFlowController`는 `StageCatalogSO`가 있으면 카탈로그에서 `StageProfiles`를 구성하고, 없으면 기존 직렬화 `StageProfiles`를 fallback으로 사용한다.
- `RequestStageMapApply(stageId)` 경로는 유지되며, Stage 시작 전에 맵 적용 요청이 선행된다.

## 3. 소유권 (Owner / Writer)
- Definition 생성/동기화 Owner: `StageDefinitionGenerator`
- Layout 생성 Owner: `StageLayoutCatalogGenerator` (`StageLayoutSO` 대상)
- Catalog 조립 Owner: `StageCatalogComposer`
- StageCatalog 검증 Owner: `StageCatalogValidationRules`
- 런타임 StageMap 적용 Owner: `StageMapApplyExecutionBeginSystem` (ExecutionBegin)
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

## 7. 런타임 반영(v1)
- `DemoShellFlowController`
  - 시작 시 `StageCatalogSO`를 읽어 런타임 `StageProfiles` 구성
  - 미할당/유효 엔트리 없음 시 기존 `StageProfiles` fallback
- `EnterStagePlay`
  - 선택 엔트리의 `Definition.StageId`를 사용해 `RequestStageMapApply(stageId)` 호출
- `StageMapApplyExecutionBeginSystem`
  - v1에서 변경 없음 (StageMapCatalogSO 기반 `Source/Deposit` 반영)
- `StageDefinitionSO.SourceBindings`
  - v1은 저장/검증만 수행, 런타임 소비는 이월

## 8. 진행 상태
1. Dual Catalog 타입(`StageCatalogSO`, `StageDefinitionSO`, `StageLayoutSO`) 추가 (`완료`)
2. Generator/Composer/ValidationRules 추가 (`완료`)
3. ContentValidationRunner 체인 추가 (`완료`)
4. DemoShell StageCatalog 로딩 + fallback 경로 반영 (`완료`)
5. StageDefinition 패턴의 ECS 재구성 적용 (`다음 페이즈`)

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
