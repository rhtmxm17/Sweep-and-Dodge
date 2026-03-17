# 런타임 플레이 HUD 계약 (TD-011)

## Metadata
- doc_id: `TD-011`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-05`
- related_docs:
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [TD-007-common-combat-event-channel.md](./TD-007-common-combat-event-channel.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
- related_adr: 중요 결정 없음 (ADR 신규 작성 없음)

> S2 범위의 플레이 HUD를 `OnGUI + ECS snapshot` 계약으로 고정하고, 디버그 HUD와의 공존 정책을 명확히 한다.

## 1. 목표 / 비목표
### 1.1 목표
- 플레이 HUD를 디버그 HUD와 분리된 독립 경로로 운영한다.
- HUD 데이터 갱신 경로를 `ECS 단일 writer`로 고정한다.
- Source 진행 상태, 피격 피드백, 스테이지 상태를 플레이어 관점 최소 정보로 표시한다.
- Stage 메타(Stage ID/Screen)는 Demo Shell을 read-only로 조회한다.

### 1.2 비목표
- uGUI/UIToolkit 전환 및 아트 고도화
- Animator/VFX/SFX 소비자 고도화(S4/S5 범위)
- Stage 실패 UX/결과 화면 상세 확장(S3 범위)

## 2. 소유권 (Writer / Reader)
- ECS writer: `PlayerHudSnapshotCollectSystem`
  - `PlayerHudSnapshotComponent` 갱신 단일 책임
  - Source/Stage/Combat 메트릭을 읽어 HUD 스냅샷으로 정규화
- HUD reader: `PlayerRuntimeHudBridge` (GO)
  - `PlayerHudSnapshotComponent` read-only 소비
  - `DemoShellFlowController`를 read-only로 조회해 Stage 메타 표시
- Demo Shell owner: `DemoShellFlowController`
  - Stage 선택/전이 책임 유지
  - HUD는 전이 결정에 개입하지 않는다

## 3. 업데이트 순서
- 프레임 파이프라인은 기존 계약을 유지한다.
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- HUD 수집 시스템 배치:
  - Group: `BulletExecutionEndGroup`
  - Order: `UpdateAfter(CombatEventChannelConsumeSystem)`, `UpdateBefore(PlayerUiFeedbackConsumeSystem)`
- 의미:
  - Hit/Collect/Cleanup 집계가 완료된 값을 HUD 스냅샷에 반영한다.
  - UI 이벤트 소비(clear) 이전에 스냅샷이 확정된다.

## 4. 데이터 구조 / 제약
### 4.1 스냅샷 계약
- `PlayerHudSnapshotComponent`
  - Carry: `CarryLoad`, `CarryCapacity`
  - Source 집계: `DepletedSourceCount`, `TotalSourceCount`
  - Pressure Source: `PressureSourceStableId`, `PressureSourceCollected`, `PressureSourceThresholdDepleted`, `PressureSourceProgress01`
  - Stage:
    - `StageState`, `StageStateElapsedSec`
    - `GameplayElapsedSec`
  - Hit: `LastHitLossValue`, `HitFlashRemainingSec`
  - 갱신 프레임: `LastUpdatedFrame`

### 4.2 계산 규칙
- Pressure Source 선택:
  - `SourceRunDirectorStateComponent.State == Pressure`인 Source 후보만 사용
  - 복수 후보일 때 `SourceStableId` 최소값 우선
- Pressure 진행도:
  - `progress01 = saturate(CollectedCount / max(1, ThresholdDepleted))`
- Source 완료 수:
  - `SourceSpawn.State == Depleted` 개수
- Hit 피드백:
  - 트리거: `CombatEventMetrics.LastFrameHitCount > 0`
  - 값: `CombatEventMetrics.LastFrameHitValue`
  - 플래시 유지 시간: `0.6s`

### 4.3 Stage 메타 공급
- Stage ID/Screen:
  - `DemoShellFlowController.CurrentStageId`, `CurrentScreen` read-only 사용
  - ECS writer 경로에는 Stage ID 전송 컴포넌트를 추가하지 않는다
- Stage 시간:
  - `StageStateElapsedSec`는 상태 체류 시간이다.
  - `GameplayElapsedSec`는 `StagePlay` timer/timeout/HUD authority다.
- Stage 시간:
  - `RunDirectorStageStateComponent.StateElapsedSec` 사용

### 4.4 구조 변경 / Fence
- 본 작업은 Enableable 토글 기반 요청/소비 구조를 변경하지 않는다.
- SharedStatic/Native 컨테이너 Fence 규칙 변경 없음.
- HUD 스냅샷은 singleton IComponentData만 갱신하며 구조 변경을 유발하지 않는다.

## 5. 디버그 HUD 공존 정책
- `Play HUD`: 런타임 표시 대상
- `Debug HUD (BulletDebugHudBridge)`:
  - `UNITY_EDITOR` 또는 `DEVELOPMENT_BUILD`에서만 렌더
  - Non-development 빌드에서는 렌더 경로 비활성

## 6. 작업 분해 / 진행 상태
1. `PlayerHudSnapshotComponent` 추가 (`완료`)
2. `PlayerHudSnapshotCollectSystem` 추가 및 ExecutionEnd 순서 고정 (`완료`)
3. `PlayerRuntimeHudBridge` 추가 (OnGUI 소비 + DemoShell read-only) (`완료`)
4. DemoShell 런타임 HUD 브리지 연결 보장 (`완료`)
5. Debug HUD Non-development 렌더 가드 추가 (`완료`)
6. EditMode/PlayMode 테스트 반영 (`완료`)

## 7. 검증 계획 / 합격 기준
- 공통 절차:
  1. `refresh_unity(compile=request, wait_for_ready=true)`
  2. `read_console(action=get, types=["error"], include_stacktrace=true)` error 0
  3. `EditMode` 테스트 통과
  4. `PlayMode` 전용 씬 스모크 통과
- S2 추가 합격:
  - Snapshot 수집 테스트:
    - Carry/Source/Stage/Hit 필드 반영
    - Pressure tie-break(StableId 최소) 검증
    - `ThresholdDepleted=0` 진행도 계산 검증
  - 파이프라인 계약 테스트:
    - `PlayerHudSnapshotCollectSystem` 순서 assertion 통과
  - 운영 씬 PlayMode:
    - StagePlay 진입 후 HUD가 Stage 메타 + 스냅샷 데이터 반영
    - Hit 이벤트 입력 시 HUD 플래시/손실값 표시 후 시간 경과로 소멸
    - 기존 데모 셸/스모크 시나리오 회귀 없음

## 8. ADR 연계
- 본 작업은 Writer/Group/Fence 핵심 규칙을 변경하지 않는다.
- 따라서 ADR 신규 작성은 생략한다.

## 9. 변경 이력
- 2026-03-05: OPS-002 S2 문서 마감 반영. 진행 상태/검증 기준과 일치하도록 최신화했다.
- 2026-03-04: 문서 신규 작성. S2 플레이 HUD 계약(`OnGUI + ECS snapshot`), Stage 메타 read-only 공급, Debug HUD 빌드 노출 정책을 고정했다.
