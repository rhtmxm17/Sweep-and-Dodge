# Source Pollution Recovery Wave Contract

## Metadata
- doc_id: `TD-026`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-08-25`
- related_docs:
  - [GD-003-stage-cleaning-route-mvp.md](../GameDesign/GD-003-stage-cleaning-route-mvp.md)
  - [GD-014-cleaning-trace-recovery-loop.md](../GameDesign/GD-014-cleaning-trace-recovery-loop.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [TD-036-runtime-stage-cell-overlay.md](./TD-036-runtime-stage-cell-overlay.md)
- related_adr:
  - [ADR-20260219-06-cleaning-trail-request-owner-and-fast-sampling.md](../ADR/ADR-20260219-06-cleaning-trail-request-owner-and-fast-sampling.md)
  - [ADR-20260330-01-source-pollution-recovery-wave-and-active-cell-contract.md](../ADR/ADR-20260330-01-source-pollution-recovery-wave-and-active-cell-contract.md)
  - [ADR-20260330-02-active-area-density-scaling-for-field-sampling.md](../ADR/ADR-20260330-02-active-area-density-scaling-for-field-sampling.md)

> `GD-014`의 공간 순환형 청소 흔적 복구를 DOTS runtime 계약으로 내리고, 1차 구현 범위를 `recent clean bias + active ratio recovery wave`로 고정한다.

## 1. 문제 정의
- 현재 pollution runtime은 `CellPollution.Value` 단일 값에 `drop -> 전역 regen`을 적용한다.
- 이 구조는 `GD-003` MVP의 정지 지점 밀도 저하에는 충분하지만, `GD-014`가 요구하는 `영역 상태 기반 복구`, `방금 청소한 자리 보호`, `작은 구역 단위 파형`을 직접 표현하지 못한다.
- 구현을 서두르면 아래 경계가 쉽게 흐려진다.
  - sampling reader와 pollution writer의 ownership
  - active/inactive 상태와 spawn weight의 의미 구분
  - topology prepare/reset과 runtime recovery의 책임 구분
  - 최근 청소 편향과 최근 체류 편향의 범위 구분

## 2. 목표/비목표
- 목표:
  - source pollution을 `active/inactive + weight` 2계층 모델로 고정한다.
  - recovery trigger를 `active ratio threshold` 기반으로 고정한다.
  - Request 그룹 단일 writer, ExecutionBegin read-only sampling 구조를 유지한다.
  - `GD-014`의 "작은 구역이 다시 어수선해짐" 체감을 만드는 최소 runtime 규칙을 정의한다.
- 비목표:
  - `TD-036`에서 정의한 Cell overlay 외 HUD/VFX/토스트의 최종 표현안 확정.
  - 최근 체류 셀 heatmap 도입.
  - 최종 밸런스 수치 확정.
  - source 외부 시스템이 pollution state를 직접 write하도록 허용하는 것.

## 3. 설계안
### 3.1 상태 모델
- `SourcePollutionCellBuffer`는 최소 아래 의미를 가진다.
  - `Value`
    - active 셀의 spawn weight.
    - inactive 셀에서도 보관 가능하지만, sampling weight로 직접 쓰지 않는다.
  - `IsValid`
    - region local grid 내 소유 셀 여부.
  - `IsActive`
    - sampling 가능한 셀인지 여부.
  - `LastDropFrame`
    - 최근 청소 시점.
  - `CooldownUntilFrame`
    - recovery 후보로 돌아올 수 있는 earliest logic frame.
- `SourcePollutionConfigComponent`는 최소 아래 필드를 추가 대상으로 본다.
  - `ActiveRatioThreshold`
  - `RecoveryCooldownFrames`
  - `RecoveryWaveSeedCount`
  - `RecoveryWaveClusterSize`
  - `RecoveryWaveRestoreValue`
  - `RecoveryRecentCleanBiasFrames`
- 네이밍/세부 타입은 구현 단계에서 조정 가능하지만, 의미 계약은 위 범위를 유지한다.

### 3.2 drop와 active/inactive 전환
- `BulletVacuumRequestSystem`은 기존처럼 source cell index를 계산해 drop request만 누적한다.
- `SourcePollutionUpdateSystem`은 drop request를 소비하면서 해당 셀에 대해 아래를 수행한다.
  - `Value` 감소
  - `LastDropFrame = currentFrame`
  - `CooldownUntilFrame = currentFrame + RecoveryCooldownFrames`
  - `Value`가 active 유지 기준 이하로 내려가면 `IsActive = 0`
- drop 이벤트가 발생한 프레임에 다른 시스템이 같은 셀을 즉시 active로 되돌리지 않는다.

### 3.3 active 셀 regen 계약
- regen은 active 셀에만 적용한다.
- inactive 셀은 cooldown 만료 전까지 전역 regen으로 active 복귀하지 않는다.
- cooldown 만료 후에도 자동 복귀시키지 않고, recovery wave에서 선택된 셀만 active 복귀시킨다.
- 이 규칙으로 `GD-003`의 단순 시간 회복을 `GD-014`의 영역 상태 기반 복구로 대체한다.

### 3.4 recovery trigger와 wave
- source 단위로 `active valid cell count / valid cell count`를 계산한다.
- active ratio가 `ActiveRatioThreshold` 아래면 recovery wave를 검토한다.
- wave 실행 규칙:
  - 후보는 `IsValid = 1`, `IsActive = 0`, `CooldownUntilFrame <= currentFrame` 셀이다.
  - 후보 중 최근 청소 프레임이 매우 가까운 셀은 seed 우선순위에서 약하게 불리하게 둔다.
  - seed를 `RecoveryWaveSeedCount`만큼 선택한다.
  - 각 seed는 local neighbor를 따라 최대 `RecoveryWaveClusterSize`까지 active 복귀시킨다.
  - 복귀 셀의 `Value`는 `RecoveryWaveRestoreValue` 이상으로 설정한다.
- 한 프레임에 모든 inactive 셀을 한꺼번에 복귀시키지 않는다.
- 목적은 full refill이 아니라 "다른 작은 구역이 다시 관리 대상으로 떠오른다"는 파형이다.

### 3.5 sampling 계약
- `PollutionTopK`는 현행 ExecutionBegin sampling path를 유지한다.
- sampling reader는 `IsActive = 0` 셀을 weight 0으로 취급한다.
- `UniformField`도 valid 셀 전체가 아니라 active valid 셀 집합을 우선 사용한다.
  - active valid 셀이 0개라면 fallback 정책은 recovery wave 이후에도 0개인 예외 상황에서만 제한적으로 둔다.
  - fallback이 필요하면 docs와 테스트에 명시된 방식으로만 허용한다.
- sampling은 계속 `region bounds local grid + valid cell mask`를 geometry authority로 사용한다.
- field sampling directive(`UniformField`, `PollutionTopK`)의 request 생성량과 `CapAndMaxDensity` 상한은 `active valid cell count / valid cell count` 비율만큼 축소된 effective area를 사용한다.
  - 목적은 active 셀이 줄었을 때 총량도 함께 줄여 cell당 기대 밀도가 과도하게 증가하지 않게 하는 것이다.
  - `LineEven`, `PointSet`은 full area 해석을 유지한다.
  - `Poisson` / `EventBurst`는 사건량 자체는 유지하고, cap 계산만 effective area를 사용한다.

### 3.6 최근 체류 편향 범위
- `GD-014`는 최근 체류 구역 회피를 권장하지만, 이번 단계는 source 내부 `recent clean bias`까지만 채택한다.
- 이유:
  - 현행 런 디렉터는 source 단위 점유/hold만 authoritative하게 보유한다.
  - source 내부 local cell 체류 heat를 바로 재사용할 수 없다.
- 후속 범위:
  - player local cell occupancy aggregator를 별도 owner로 도입
  - recovery seed scoring에 `recent stay penalty`를 추가

## 4. 업데이트 순서/소유권
- 프레임 파이프라인은 유지한다.
  - `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`
- ownership:
  - `BulletVacuumRequestSystem`
    - source pollution drop request append만 담당
    - pollution cell state 직접 write 금지
  - `SourcePollutionUpdateSystem`
    - drop 소비
    - active/inactive 전환
    - regen
    - active ratio 계산
    - recovery wave 실행
    - 단일 writer
  - `SpawnRequestRoundRobinExecutionSystem` 및 sampling helper
    - pollution state read-only 소비만 허용
  - `StageTopologyApplyPrepareSystem` / authoring utility
    - pollution grid 생성/초기화만 담당
    - runtime recovery state 초기값 reset owner
- group order:
  - `PlayerCleanupActionSelectSystem` before `BulletVacuumRequestSystem`
  - `BulletVacuumRequestSystem` before `SourcePollutionUpdateSystem`
  - `SourcePollutionUpdateSystem` before 나머지 Request 후속 시스템
  - `PollutionTopK` read path는 ExecutionBegin에 남긴다

## 5. 데이터 구조/제약
- `SourcePollutionCellBuffer`는 per-cell runtime state이므로 dynamic buffer 유지가 기본이다.
- region authoritative geometry 계약상 `Shape2DComponent`는 source pollution sampling/runtime authority로 읽지 않는다.
- topology prepare/reset 시 아래가 함께 초기화되어야 한다.
  - `IsActive`
  - `Value`
  - `LastDropFrame`
  - `CooldownUntilFrame`
  - drop request buffer
- logic time은 `BulletFrameCounterComponent` 기준 frame을 사용한다.
- recovery wave는 Request 그룹 내부 계산으로 제한하고, 별도 fence/shared static을 추가하지 않는다.

## 6. 작업 분해/진행 상태
- `P1` buffer/config schema 확장
  - 상태: pending
  - 범위: pollution config, cell buffer, authoring defaults, prepare reset
- `P2` Request writer 전환
  - 상태: pending
  - 범위: `SourcePollutionUpdateSystem`에 cooldown/active ratio/recovery wave 추가
- `P3` sampling reader 정합성
  - 상태: pending
  - 범위: `PollutionTopK`, `UniformField`가 inactive 셀 제외
- `P4` EditMode 회귀
  - 상태: pending
  - 범위: drop->inactive, cooldown gate, wave cluster, prepare reset, sampling exclude
- `P5` PlayMode smoke 관찰 포인트
  - 상태: pending
  - 범위: source 내부 이동 유도, "방금 닦은 자리 보호", 다른 구역 wave 체감

## 7. 검증 계획
- EditMode:
  - drop 누적 후 writer가 `IsActive`를 끄는지 검증
  - cooldown 이전 inactive 셀이 recovery 후보에서 제외되는지 검증
  - active ratio threshold 이하에서만 wave가 실행되는지 검증
  - wave가 dispersed noise가 아니라 localized cluster를 만드는지 검증
  - `PollutionTopK` / `UniformField`가 inactive 셀을 뽑지 않는지 검증
  - topology prepare/reset 이후 pollution state가 초기값으로 복구되는지 검증
- PlayMode:
  - source 내부에서 한쪽만 오래 청소하면 다른 작은 구역이 다시 떠오르는지 확인
  - 방금 청소한 위치가 즉시 재오염처럼 보이지 않는지 확인
  - source 이동 루프가 "기다렸다가 다시 오기"보다 "다른 쪽으로 이동"에 가깝게 읽히는지 확인
- 공통 게이트:
  - `compile -> console error 0 -> EditMode -> PlayMode smoke`

## 8. 오픈 이슈
- inactive가 100%가 된 극단 상황에서의 fallback sampling 정책을 어디까지 허용할지.
- cluster neighbor 탐색을 4-neighbor로 둘지, 8-neighbor로 둘지.
- `recent clean bias`를 frame 기반만으로 충분히 표현할지, 별도 decay score가 필요한지.
- 후속 단계에서 `recent stay bias`를 어느 owner가 공급할지.

## 9. 변경 이력
- 2026-08-25: Source active/inactive의 runtime Cell 표현 계약을 `TD-036`으로 연결했다.
- 2026-03-30: 초안 작성. `GD-014`의 공간 순환형 복구를 `active/inactive + recovery wave` runtime 계약으로 정리하고, 1차 구현 범위를 최근 청소 편향 중심으로 고정했다.
