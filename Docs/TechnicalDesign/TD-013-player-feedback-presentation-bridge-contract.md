# 플레이어 피드백 프레젠테이션 브리지 계약 (TD-013)

## Metadata
- doc_id: `TD-013`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-05`
- related_docs:
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-001-player-feedback-event-channel.md](./TD-001-player-feedback-event-channel.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
- related_adr:
  - [ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md](../ADR/ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md)

> OPS-002 S4 범위에서 `PlayerUiFeedback`/`Impulse` 이벤트를 `ECS snapshot writer + Mono reader` 구조로 실제 표현(Animator/HUD/Impulse offset)에 연결한다.

## 1. 목표 / 비목표
### 1.1 목표
- 이벤트 생산자(`BulletVacuumRequestSystem`, `CombatEventChannelConsumeSystem`, `PlayerHazardCollisionExecutionSystem`)를 유지한다.
- `PlayerUiFeedbackConsumeSystem`, `PlayerImpulseConsumeSystem`을 로그 소비에서 표현 스냅샷 writer로 전환한다.
- `PlayerEcsBridge`가 Animator trigger와 Impulse 시각 오프셋을 read-only로 소비한다.
- `PlayerRuntimeHudBridge`가 피드백 1줄 feed를 read-only로 표시한다.

### 1.2 비목표
- uGUI/UIToolkit 전환
- 사운드 라우팅/믹서 정책(S5)
- 카메라 흔들림 연동

## 2. 소유권 (Writer / Reader)
- Writer:
  - `PlayerUiFeedbackConsumeSystem` -> `PlayerUiFeedbackPresentationSnapshotComponent`
  - `PlayerImpulseConsumeSystem` -> `PlayerImpulsePresentationSnapshotComponent`
- Reader:
  - `PlayerEcsBridge` -> Animator bool/trigger, impulse visual offset
  - `PlayerRuntimeHudBridge` -> feedback feed 텍스트 1줄
- 원칙:
  - GO는 ECS 이벤트 버퍼를 직접 clear/write하지 않는다.
  - 버퍼 clear는 consume 시스템 단일 책임으로 유지한다.

## 3. 업데이트 순서
- 기존 파이프라인 유지:
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- ExecutionEnd 서브시퀀스:
  - Producer(기존) -> `PlayerUiFeedbackConsumeSystem`/`PlayerImpulseConsumeSystem` -> `DebugHudMetricsCollectSystem`
- `BulletPipelineContractTests`의 순서 계약을 변경하지 않는다.

## 4. 데이터 구조 / 제약
### 4.1 UI 표현 스냅샷
- `PlayerUiFeedbackPresentationSnapshotComponent`
  - 최신 승인 이벤트 payload(`Type/Reason/Value/RelatedEntity/Frame`)
  - `Version`(변경 감지)
  - `RemainingSec`(HUD feed 표시 시간)
  - `ClockSec`/타입별 cooldown next-time

### 4.2 Impulse 표현 스냅샷
- `PlayerImpulsePresentationSnapshotComponent`
  - 합산 결과(`DirX/DirZ/Magnitude`, `MergedEventCount`, `Frame`)
  - `Version`(변경 감지)

### 4.3 구조 변경 제약
- 이벤트 엔티티 도입 없이 기존 버퍼 구조를 유지한다.
- 스냅샷은 플레이어 엔티티의 `IComponentData` 갱신만 수행한다.

## 5. 소비 규칙
### 5.1 UI 이벤트 승인 규칙
- dedupe: 동일 `Frame + Type + RelatedEntity`는 1건으로 간주한다.
- 우선순위:
  - `PlayerHazardHit > HazardCaptured > HazardRemoved > SourceStateChanged > VacuumStartBlocked`
- cooldown:
  - 기본 UI 이벤트: `0.15s`
  - `PlayerHazardHit`: `0.10s`
- 표시 유지:
  - HUD feed 유지 시간: `1.25s`

### 5.2 Impulse 합산 규칙
- 원칙: `PlayerHazardCollisionRequestSystem`의 프레임당 1회 요청 고정 + iFrame 게이트로 동일 프레임 다건 hit는 발생하지 않아야 한다.
- 방어 규칙: 예외 입력/회귀에 대비해 동일 프레임 다건 impulse가 들어오면 단일 벡터로 합산 후 프레임 상한(`1.5`)으로 clamp한다.
- 합산 방향/크기를 snapshot 1건으로 발행하고 버퍼를 clear한다.

### 5.3 iFrame 중 추가 hit 정책
- iFrame 동안 추가 hit는 누적 제외한다.
- Request 단계에서 iFrame이 활성(`CarryBinRules.IsHazardHitBlocked`)이면 `PlayerHazardHitRequestTag`를 생성하지 않는다.

## 6. Animator/HUD/Impulse 브리지 계약
### 6.1 Animator 파라미터
- Bool:
  - `VacuumActive`
- Trigger:
  - `PlayerHazardHit -> HitReact`
  - `VacuumStartBlocked -> VacuumBlocked`
  - `HazardCaptured -> HazardCaptured`
  - `HazardRemoved -> HazardRemoved`
  - `SourceStateChanged -> SourceStateChanged`

### 6.2 HUD Feed
- `PlayerRuntimeHudBridge` OnGUI 영역에 `Feedback: ...` 1줄 표시
- 표시 문구는 `Type/Reason` 매핑으로 생성한다.

### 6.3 Impulse 표시
- `PlayerEcsBridge`에서 ECS 위치 위에 로컬 오프셋을 누적하고 스프링-댐퍼(기본 critically damped)로 복귀시킨다.
- 표현 강도는 게임플레이 loss와 분리한다.
  - 입력: ECS impulse magnitude
  - 표현 gain: `base + lossScale * log(1 + hitLoss)`
  - 프레임 반영 상한: `ImpulseVisualPerFrameMax`
- 기본값:
  - `ImpulseSpringFrequency = 18`
  - `ImpulseDampingRatio = 1`
  - `ImpulseVisualBase = 0.08`
  - `ImpulseVisualLossScale = 0.03`
  - `ImpulseVisualPerFrameMax = 0.20`
  - `ImpulseMaxOffset = 0.35`
- ECS 위치/회전 write 경계는 변경하지 않는다.

## 7. Null-safe 정책
- `Animator` 참조가 비어 있으면 Animator 표현만 skip한다.
- 런타임은 계속 진행한다.
- `UNITY_EDITOR`/`DEVELOPMENT_BUILD`에서 1회 경고를 허용한다.

## 8. 검증 계획 / 합격 기준
- 공통:
  1. `refresh_unity(compile=request, wait_for_ready=true)`
  2. `read_console(action=get, types=["error"], include_stacktrace=true)` error 0
  3. `EditMode` 테스트 통과
  4. `PlayMode` 전용 씬 스모크 통과
- S4 추가:
  - UI 소비 시 version 증가 + buffer clear
  - dedupe/cooldown 규칙 검증
  - Impulse 합산 snapshot 검증 + 프레임 상한 clamp 검증(방어 규칙)
  - `iFrame` 동안 추가 hit/impulse 누적 제외 검증
  - Animator null-safe 동작 검증

## 9. ADR 연계
- 본 변경은 이벤트 생산자 owner/업데이트 그룹 계약을 변경하지 않는다.
- 소비 단계 표현 브리지 추가이므로 ADR 신규 생성은 생략한다.

## 10. 변경 이력
- 2026-03-05: 문서 신규 작성. S4 이벤트 피드백 브리지 계약(writer/read-only 경계, dedupe/cooldown, Animator/HUD/Impulse 매핑)을 고정했다.
