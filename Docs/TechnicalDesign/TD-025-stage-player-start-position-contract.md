# 스테이지별 플레이어 시작 위치 계약 (TD-025)

## Metadata
- doc_id: `TD-025`
- type: `TechnicalDesign`
- status: `implemented`
- last_updated: `2026-08-11`
- related_docs:
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [ADR-20260309-02-stage-session-reset-and-prepare-owner.md](../ADR/ADR-20260309-02-stage-session-reset-and-prepare-owner.md)
  - [ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md](../ADR/ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id.md)
  - [ADR-20260326-01-stage-player-start-owned-by-layout-and-prepare-owner.md](../ADR/ADR-20260326-01-stage-player-start-owned-by-layout-and-prepare-owner.md)

> 플레이어 시작 위치는 stage-level spatial data로 보고 `StageLayoutSO`가 소유한다. stage 진입 시 `StageTopologyApplyPrepareSystem`이 player-start runtime singleton을 publish하고, `PlayerStageEntryApplyPrepareSystem`이 `LocalTransform`, `PlayerGoSyncComponent`, `PlayerPreviousPositionComponent`를 단일 writer로 갱신한다.

## 1. 목표 / 비목표
### 1.1 목표
- 스테이지마다 서로 다른 플레이어 시작 위치와 시작 바라보기를 authoring 가능하게 한다.
- 시작 위치 데이터를 기존 grid-authoritative layout SSOT에 포함시킨다.
- stage entry 시 플레이어 위치 적용 owner, 적용 순서, reset 범위를 명확히 고정한다.
- Retry / Next Stage / Lobby -> Stage 재진입에서 동일한 stage start 결과를 보장한다.

### 1.2 비목표
- mid-run respawn/checkpoint
- 멀티 플레이어별 복수 start slot
- 수직 지형/층 분리까지 포함한 3D spawn 시스템
- 이번 단계에서 stage intro 카메라 연출까지 연결하는 확장

## 2. 채택안 요약
- player start는 `StageDefinitionSO`가 아니라 `StageLayoutSO`에 둔다.
- authoring은 `StageMapDocument.PlayerStart`를 기준으로 한다.
- 저장 형식은 grid-relative `AnchorCell + AnchorOffset + YawDeg`를 사용한다.
- stage apply owner는 player start를 world position으로 resolve해 runtime singleton에 publish한다.
- 실제 player entity write는 `PlayerStageEntryApplyPrepareSystem` 단일 writer가 수행한다.
- 적용 대상은 최소 아래 3개다.
  - `LocalTransform`
  - `PlayerGoSyncComponent`
  - `PlayerPreviousPositionComponent`

## 3. 소유권 (Owner / Writer)
- Layout SSOT Owner: `StageLayoutSO`
  - `PlayerStart` 필드를 소유한다.
- Authoring Owner: `StageMapDocument`
  - `PlayerStart` record를 단일 편집 SSOT로 소유한다.
- Export/Apply Owner: `StageMapDocumentExporter` / `StageMapApplyPlanner`
  - document record를 `StageLayoutSO.PlayerStart`에 반영한다.
- Validation Owner:
  - authoring seam: `StageMapDocumentValidationRules`
  - asset seam: `StageGridLayoutValidationRules`
- Runtime publish Owner: `StageTopologyApplyPrepareSystem`
  - layout의 player start를 world-space runtime singleton으로 publish한다.
- Runtime apply Owner: `PlayerStageEntryApplyPrepareSystem`
  - player spatial state write를 단일 소유한다.
- DemoShell / GO bridge
  - 기존처럼 `StageTopologyBridge.RequestTopologyApply(stageId)`와 `RunDirectorStageBridge.RequestStageStart()`만 호출한다.
  - GO가 player position을 직접 쓰지 않는다.

## 4. 업데이트 순서
- prepare 계층 순서:
  1. `StageTopologyBootstrapSystem`
  2. `StageSessionResetPrepareSystem`
  3. `StageTopologyApplyPrepareSystem`
  4. `PlayerStageEntryApplyPrepareSystem`
- 의미:
  - `StageSessionResetPrepareSystem`
    - stage-entry transient state reset
    - 이전 run의 carry/hit/request/UI snapshot 정리
  - `StageTopologyApplyPrepareSystem`
    - stage layout/grid/source 적용
    - player start runtime singleton publish
  - `PlayerStageEntryApplyPrepareSystem`
    - 새로 publish된 player start를 player entity에 반영
- fixed-tick gameplay는 player start apply가 끝난 뒤에만 시작한다.

## 5. 데이터 구조 계약
### 5.1 Layout asset
- `StageLayoutSO`
  - `StagePlayerStartLayoutData PlayerStart`

### 5.2 PlayerStart layout record
- `StagePlayerStartLayoutData`
  - `bool Active`
  - `Vector2Int AnchorCell`
  - `Vector2 AnchorOffset`
  - `float YawDeg`
- 첫 버전은 planar gameplay 기준으로 `XZ + yaw`만 저장한다.
- `Y`는 layout의 `Grid.Origin.y`를 기본값으로 사용한다.
  - 현재 sample/editor contract에서는 실질적으로 `0`과 같다.
  - 별도 vertical spawn 요구가 생기기 전까지 독립 `HeightY`는 두지 않는다.

### 5.3 Runtime singleton
- `StagePlayerStartRuntimeComponent`
  - `int StageId`
  - `float PositionX`
  - `float PositionY`
  - `float PositionZ`
  - `float YawDeg`
  - `byte Ready`
  - `uint AppliedVersion`
- `AppliedVersion`은 `StageTopologyLifecycleStateComponent.CurrentAppliedVersion`과 같은 stage-entry wave를 가리킨다.

### 5.4 Apply bookkeeping
- `PlayerStageEntryApplyStateComponent`
  - `uint LastAppliedVersion`
- 목적:
  - 같은 stage entry에서 player start를 한 번만 적용한다.
  - stage reapply/retry/next 시 새 apply version만 다시 반영한다.

## 6. Authoring 계약
- 편집 SSOT는 `StageMapDocument.PlayerStart`다.
- `StageMapEditorWindow`의 PlayerStart tool과 contextual inspector가 공식 편집 표면이다.
- 저장 형식은 grid-relative `Active / AnchorCell / AnchorOffset / YawDeg`를 유지한다.
- `CenterPlayerStart` session 옵션이 켜지면 offset을 0으로 만들고 이후 배치·이동에서도 cell center를 강제한다.
- validation error:
  - `AnchorCell`이 bounds 밖
  - start cell이 `BlockPlayer`
- validation warning:
  - start cell이 source 또는 deposit region과 겹침
- `StageMapDocumentExporter`가 `StageLayoutSO.PlayerStart` snapshot을 만들고, `StageMapApplyPlanner`가 dry-run/diff/apply 및 stale rejection을 소유한다.


## 7. Runtime apply 계약
### 7.1 StageTopologyApplyPrepareSystem
- layout resolve 후 `PlayerStart.Active == true`이면 world-space start를 계산한다.
- 계산식:
  - `position = GridSpec + AnchorCell + AnchorOffset`
  - `rotation = yaw-only`
- publish 실패 시:
  - `Ready = 0`
  - topology apply 전체를 실패로 처리하는 대신 warning 후 기존 `Ready=0` 정책을 따른다.
  - player start 없는 stage는 시작 불가로 본다.

### 7.2 PlayerStageEntryApplyPrepareSystem
- 입력:
  - `StagePlayerStartRuntimeComponent`
  - `PlayerTag`
  - `StageTopologyLifecycleStateComponent`
- 출력:
  - `LocalTransform`
  - `PlayerGoSyncComponent`
  - `PlayerPreviousPositionComponent`
- 규칙:
  - `Ready == 1`이고 `AppliedVersion > LastAppliedVersion`일 때만 적용한다.
  - 모든 player entity에 동일 start를 적용한다.
    - 현재는 single-player 전제지만, 복수 player 도입 전까지 deterministic broadcast로 둔다.
  - `PlayerGoSyncComponent.SyncRotation != 0`이면 rotation도 같이 갱신한다.
  - `PlayerPreviousPositionComponent.Position`은 새 위치로 즉시 맞춘다.
    - movement owner의 첫 swept query가 stale prev position을 보지 않게 하기 위함이다.

### 7.3 Reset scope 연동
- `StageSessionResetPrepareSystem`는 기존 transient reset owner를 유지한다.
- player spatial state는 reset 단계가 아니라 apply 단계에서만 쓴다.
  - 이유: start position은 selected stage layout resolve 이후에만 결정 가능하다.
- 단, reset 단계에서 아래 입력 잔상은 계속 지운다.
  - `PlayerInputIntentComponent`
  - `PlayerResolvedInputSnapshotComponent`
  - `PlayerGoSyncComponent`의 request성 필드

## 8. 테스트 계획 / 합격 기준
- EditMode
  - `StageMapDocumentTests`
    - PlayerStart record가 layout snapshot에 기록되는지
  - `StageMapEditorInteractionTests`
    - bounds / blocked-cell validation, center option, Scene tool interaction
  - `StageLayoutValidationRulesTests`
    - layout asset의 player start validation
  - `StageTopologyApplyPrepareSystemTests`
    - runtime player start singleton publish
  - `PlayerStageEntryApplyPrepareSystemTests`
    - player entity spatial sync
    - `AppliedVersion` 멱등성
- PlayMode
  - `Stage1 -> Stage2 -> Stage3`에서 서로 다른 시작 위치 적용
  - Retry 시 동일 stage start 재적용
  - 시작 직후 player가 blocked cell에 끼지 않음
  - camera / GO presenter가 ECS 위치와 어긋나지 않음

## 9. 작업 분해 / 진행 상태
- P1. 문서/결정 고정
  - TD/ADR/TaskBoard 작성
- P2. 데이터/에디터 seam (완료)
  - `StageLayoutSO.PlayerStart`
  - `StageMapDocument.PlayerStart`
  - exporter/validation
- P3. runtime seam
  - bootstrap singleton
  - topology apply publish
  - player stage-entry apply owner
- P4. sample/content (완료)
  - `smd_demo_1/2/3`, `sl_demo_*`, `sc_demo` 갱신
- P5. 검증
  - compile / console / EditMode / PlayMode smoke

## 10. 미결 / 보고 조건
- vertical spawn(`HeightY`)이 필요해지면 layout schema를 확장해야 한다.
- start cell 위 `Source / Deposit`를 error로 올릴지 warning으로 둘지는 content 시범 운영 후 다시 판단한다.
- intro 연출이 player yaw를 일시적으로 override해야 하면 `PlayerStageEntryApplyPrepareSystem`이 아니라 presentation owner에서 read-only 후속 회전을 처리한다.
