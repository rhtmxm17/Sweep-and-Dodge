# HazardEmitter Common Contract

## Metadata
- doc_id: `TD-028`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-04-08`
- related_docs:
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md](../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md)
  - [./TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [./TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [./TD-027-hazard-bullet-extension-contract.md](./TD-027-hazard-bullet-extension-contract.md)
  - [./TD-029-discrete-emit-spawn-bridge-contract.md](./TD-029-discrete-emit-spawn-bridge-contract.md)

> `GD-015`의 기획 의도를 구조 설계 기준으로 내리기 위해, `HazardEmitter`의 최소 공통 계약과 공통 상태기계를 `유형 기준 + profile ref + emit append` 경계로 고정한다.

## 1. 문제 정의
- `GD-015`는 `HazardEmitter`의 경험 목표와 배치 의도를 충분히 설명하지만, 구현을 시작하기 전에 필요한 최소 공통 계약은 아직 분리되지 않았다.
- 현재 기획 분류에는 아래 두 축이 섞여 있다.
  - emitter가 무엇에 붙어 존재하는가
  - emitter가 언제 켜지고 어떻게 순환하는가
- 이 상태로 구현을 서두르면 아래 경계가 쉽게 흐려진다.
  - `PlacementIntent`와 runtime contract의 경계
  - emitter 공통 상태기계와 policy-specific 조건의 경계
  - telegraph/emission의 의미와 authoring/runtime payload의 경계
  - emitter가 bullet을 직접 생성하는지, emit request producer인지의 경계

## 2. 목표/비목표
- 목표:
  - `HazardEmitter`의 최소 공통 계약을 `유형 기준`으로 고정한다.
  - `ActivationPolicy` 최소 집합을 현재 범위에서 결정 완료 상태로 고정한다.
  - 모든 emitter가 공유하는 공통 상태기계를 정의한다.
  - telegraph와 emission을 `profile ref` 경계로 분리한다.
  - emitter의 출력 경계를 `Emit 1회 request append`로 고정한다.
- 비목표:
  - authoring schema의 serialization wire shape 확정
  - `AnchorRef`의 구체 런타임 식별자 표현 확정
  - `DiscreteEmitRequest` payload 상세 확정
  - `DiscreteEmitExecutionSystem`의 budget/backlog/owner 상세 확정
  - stage별 실배치와 콘텐츠 메타데이터 규칙 확정

- `T2`의 discrete emit bridge 상세는 [TD-029](./TD-029-discrete-emit-spawn-bridge-contract.md)를 SSOT로 참조한다.

## 3. 설계안
### 3.1 구조 축
- `HazardEmitter`의 공통 구조 축은 아래로 고정한다.
  - `Identity`
    - `EmitterId`
    - `SourceRef`
  - `Spatial`
    - `AnchorKind`
    - `Mobility`
    - `AnchorRef`
    - `LocalOffset`
  - `Activation`
    - `ActivationPolicy`
    - `LifecycleState`
    - `IsEnabled`
    - `IsSuppressed`
    - `NextReadyFrame` 또는 동등한 cooldown readiness 값
  - `Presentation boundary`
    - `TelegraphProfileRef`
  - `Emission boundary`
    - `EmissionProfileRef`

- 위 구조 축은 구현 전 SSOT로 본다.
- `PlacementIntent`는 공통 구조 축에 포함하지 않는다.
  - 이유: `PlacementIntent`는 콘텐츠 배치 의도와 튜닝 방향을 설명하는 메타데이터이며, emitter의 런타임 타입/상태기계/owner를 결정하는 기준이 아니다.

### 3.1.1 Runtime Layering
- `HazardEmitter` 관련 런타임 데이터는 아래 4층으로 분리한다.
  - structural identity
    - `HazardEmitterComponent`
  - baked baseline snapshot
    - `HazardEmitterAppliedConfigBaselineComponent`
    - `HazardEmitterTelegraphProfileBaselineComponent`
    - `HazardEmitterEmissionProfileBaselineComponent`
  - applied snapshot
    - `HazardEmitterAppliedConfigComponent`
    - `HazardEmitterTelegraphProfileComponent`
    - `HazardEmitterEmissionProfileComponent`
  - runtime mutable state
    - `HazardEmitterRuntimeStateComponent`
- 목적:
  - prefab baseline
  - stage-applied override 결과
  - runtime lifecycle state
  를 섞지 않고 owner별로 분리한다.

### 3.1.2 Stage Binding Seam
- `HazardEmitter`의 stage별 차이는 `StageSourceBinding` 하위의 `HazardEmitterBinding[]`으로 적용한다.
- `HazardEmitterBinding`은 아래 역할만 가진다.
  - enable/disable override
  - start suppression override
  - local offset override
  - telegraph/emission profile override
- `HazardEmitterBinding`은 runtime reactive rule 저장소가 아니다.
  - source state
  - player distance
  - progress threshold
  - future trigger/group 조건
  은 binding이 아니라 coordinator/runtime gate에서 해석한다.

#### 3.1.2.1 HazardEmitterBinding 최소 필드
- `EmitterId`
- `EnabledMode`
  - `Inherit`
  - `ForceEnabled`
  - `ForceDisabled`
- `StartSuppressedMode`
  - `Inherit`
  - `ForceUnsuppressed`
  - `ForceSuppressed`
- `OverrideLocalOffset`
- `LocalOffset`
- `TelegraphProfileOverride`
- `EmissionProfileOverride`

#### 3.1.2.2 Source Ownership Seam
- source root는 `SourceHazardEmitterRefBuffer(Entity, EmitterId)`를 가진다.
- 목적:
  - stage apply 시 source 소속 emitter 집합을 빠르게 찾는다.
  - future coordinator가 source 소속 emitter 집합을 순회할 수 있게 한다.
  - hierarchy/LinkedEntityGroup 탐색에 의존하지 않는다.

#### 3.1.2.3 Stage Apply 규칙
- stage apply 시 emitter는 아래 순서로 처리한다.
  1. baked baseline snapshot을 applied snapshot으로 복사
  2. `HazardEmitterBinding` override 적용
  3. `HazardEmitterRuntimeStateComponent` reset
  4. future coordinator state도 reset
- stage apply에서 항상 reset하는 값:
  - `LifecycleState`
  - `StateElapsedSec`
- stage apply는 emitter의 설정과 초기 상태만 적용하고, emit state machine 진행이나 discrete backlog 정리는 소유하지 않는다.

### 3.2 Spatial 축
#### 3.2.1 AnchorKind
- 현재 지원값:
  - `ObjectBound`
  - `PointBound`
- 의미:
  - emitter가 어떤 기준에 붙어 위치/방향을 resolve하는가를 설명한다.
- `AnchorKind`는 future `DynamicObject` 추가를 막지 않도록 닫지 않는다.

#### 3.2.2 Mobility
- 현재 지원값:
  - `Static`
- future 확장 슬롯:
  - `Dynamic`
- 의미:
  - anchor가 프레임 간 정적인가, 별도 이동 owner를 따라 갱신되는가를 설명한다.

#### 3.2.3 AnchorRef / LocalOffset
- `AnchorRef`
  - 의미 계약: anchor transform/position을 resolve할 수 있는 stable reference
  - 현재 단계에서는 serialization/wire shape를 확정하지 않는다.
- `LocalOffset`
  - emitter의 기준 anchor에서 실제 telegraph/emission이 발생하는 local offset
  - offset 자체는 공통 계약에 포함하되, world-space resolve owner는 후속 구현 단계에서 정한다.

### 3.3 ActivationPolicy
- `HazardEmitter` 최소 `ActivationPolicy` 집합은 아래 4종으로 고정한다.
  - `AlwaysCycle`
  - `ProgressReactive`
  - `TriggerReactive`
  - `RotatingSet`

#### 3.3.1 AlwaysCycle
- 별도 외부 조건 없이 emitter 자체 cadence와 cooldown을 기준으로 순환한다.
- 별도 policy payload는 최소화한다.
- cadence 상세는 `EmissionProfile`과 cooldown 값에서 해결한다.

#### 3.3.2 ProgressReactive
- `Source` 또는 상위 진행도 상태에 반응해 활성 조건을 얻는다.
- threshold/window 같은 조건 값은 policy extension config에서 관리한다.
- 공통 계약에는 policy 종류만 포함하고, 세부 조건 값은 넣지 않는다.

#### 3.3.3 TriggerReactive
- 접근, 상호작용, 외부 신호, 특정 상태 전이 등에 반응해 활성 조건을 얻는다.
- trigger 종류와 payload는 policy extension config에서 관리한다.
- 공통 계약에는 policy 종류만 포함하고, 세부 trigger 조건은 넣지 않는다.

#### 3.3.4 RotatingSet
- policy 자체는 emitter 공통 계약에 포함한다.
- 단, 실제 순번, 동시 활성 제한, set membership, coordinator state는 상위 owner가 소유한다.
- 개별 emitter는 "나는 rotating set 제어를 받는다"는 사실만 공통 계약으로 가진다.

### 3.4 Activation Orchestration
- `HazardEmitter`의 외부 입력 집계 owner는 `HazardEmitterCoordinatorSystem`으로 고정한다.
- coordinator는 emitter를 source의 부속 데이터로 직접 실행하지 않고, source 소속 emitter 집합에 대한 activation orchestration만 소유한다.
- coordinator의 책임:
  - stage-applied baseline(`IsEnabled`, `IsSuppressed`) 읽기
  - source state / source pressure / source progress 입력 읽기
  - player distance 입력 읽기
  - 최종 activation gate와 suppression reason 계산
- coordinator의 비책임:
  - `Telegraph` timer 진행
  - `Cooldown` timer 진행
  - `Emit 1회 request` append
  - spawn execution
  - 발사 grammar 해석

#### 3.4.1 Coordinator State
- coordinator의 최소 runtime 출력은 아래 성격을 가진다.
  - `ActivationAllowed`
  - `SuppressionReasonMask`
  - `LastPlayerDistanceSq`
- 목적:
  - emit build가 외부 입력 해석을 직접 소유하지 않게 유지
  - source 상태, 거리, future trigger/group gate를 같은 seam으로 확장 가능하게 유지

#### 3.4.2 Gate Extension
- coordinator 입력은 optional gate component 조합으로 확장한다.
- 현재 열어두는 최소 gate 축:
  - `HazardEmitterSourcePressureGateComponent`
    - `Pressure` 상태 요구 여부
    - 최소 pressure hold 시간
  - `HazardEmitterPlayerDistanceGateComponent`
    - 최소/최대 거리
  - `HazardEmitterSourceProgressGateComponent`
    - 정규화된 source progress 구간
- 위 gate는 모두 enabled일 때만 activation 계산에 참여한다.
- gate 조합 규칙은 AND 누적으로 본다.

#### 3.4.2.1 HazardEmitterSourcePressureGateComponent
- 최소 필드:
  - `Enabled`
  - `RequirePressureState`
  - `MinPressureOccupancySec`
- 의미:
  - source가 `RunDirectorSourceStateId.Pressure`인지
  - `PressureOccupancySec`가 최소 시간 이상 누적됐는지
  를 gate로 요구한다.

#### 3.4.2.2 HazardEmitterPlayerDistanceGateComponent
- 최소 필드:
  - `Enabled`
  - `MinDistanceSq`
  - `MaxDistanceSq`
- 의미:
  - player와 emitter 사이의 거리 제약을 gate로 요구한다.
  - player가 없고 gate가 enabled면 `MissingPlayer` 계열 reason으로 차단한다.

#### 3.4.2.3 HazardEmitterSourceProgressGateComponent
- 최소 필드:
  - `Enabled`
  - `MinProgress01`
  - `MaxProgress01`
- 진행도는 아래 규칙으로 정규화한다.
  - `progress01 = saturate(CollectedCount / max(1, ThresholdDepleted))`
- 의미:
  - source 정복 진행이 특정 구간에 들어왔을 때만 emitter 활성 조건을 만족시킨다.

#### 3.4.3 Example Rules
- 예시 1: `플레이어가 Source와 상호작용중(Pressure)이며 일정 거리 안에 있을 경우 활성화`
  - source의 `RunDirectorSourceStateId == Pressure`
  - `PressureOccupancySec >= MinPressureOccupancySec`
  - emitter 기준 player distance가 `MinDistanceSq ~ MaxDistanceSq` 범위 안
- 예시 2: `Source 정복 진행도가 일정 수준에 도달하면 활성화`
  - `progress01 = saturate(CollectedCount / max(1, ThresholdDepleted))`
  - `MinProgress01 <= progress01 <= MaxProgress01`
- 위 규칙은 coordinator에서만 해석하고, `HazardEmitterEmitBuildSystem`은 최종 gate 결과만 읽는다.

#### 3.4.4 Suppression Reason Mask
- 첫 단계 suppression reason mask는 아래 비트 축으로 고정한다.
  - `DisabledByAppliedConfig`
  - `SuppressedByAppliedConfig`
  - `MissingSource`
  - `SourcePressureBlocked`
  - `SourceProgressBlocked`
  - `PlayerDistanceBlocked`
  - `MissingPlayer`
  - `GroupSuppressed`
- 계산 규칙:
  - 각 gate 실패 이유를 bit mask로 누적한다.
  - `ActivationAllowed = (reasonMask == None)`으로 계산한다.
  - `LastPlayerDistanceSq`는 player가 있으면 실제 거리, 없으면 큰 값으로 기록한다.

### 3.5 공통 상태기계
- 모든 emitter는 아래 4상태를 공유한다.

```text
Dormant -> Telegraph -> Emit -> Cooldown
```

#### 3.5.1 Dormant
- emit 예약, 전조, cooldown 중이 아닌 기본 대기 상태다.
- policy 평가 결과가 충족되면 `Telegraph`로 진입할 수 있다.

#### 3.5.2 Telegraph
- 전조를 표시하는 상태다.
- 전조가 없는 emitter도 이 상태를 생략하지 않고 `duration=0`으로 동일 경계를 통과한다.
- 목적:
  - 모든 emitter가 같은 상태기계와 update order로 설명되도록 유지
  - "전조 없음"을 별도 구조 분기로 만들지 않기

#### 3.5.3 Emit
- 실제 bullet spawn 직접 실행 상태가 아니다.
- 의미 계약:
  - `EmissionProfileRef`를 기준으로 `Emit 1회` request append를 수행하는 논리 경계
- emitter는 이 상태에서 direct spawn하지 않는다.

#### 3.5.4 Cooldown
- emit 후 재활성화가 금지되는 상태다.
- `NextReadyFrame` 또는 동등한 readiness 값이 충족될 때 `Dormant`로 복귀한다.

### 3.6 ProfileRef 경계
#### 3.6.1 TelegraphProfileRef
- telegraph의 길이, 시각 문법, 사운드, 전조 판독 계열은 `TelegraphProfile`이 소유한다.
- 공통 계약에는 아래를 직접 넣지 않는다.
  - 전조 길이 수치
  - VFX/SFX asset
  - 시각 문법별 분기값

#### 3.6.2 EmissionProfileRef
- 발사 패턴, 반복, 조준, burst 규칙은 `EmissionProfile`이 소유한다.
- 공통 계약에는 아래를 직접 넣지 않는다.
  - 발사 각도
  - 반복 수
  - 샷 패턴
  - burst/schedule 세부값
- 위 값들은 `Emit 1회 request build` 단계에서 resolve한다.

### 3.7 출력 경계
- `HazardEmitter`의 출력은 항상 `Emit 1회 request append`로 고정한다.
- 금지:
  - emitter direct spawn
  - emitter별 독자적인 dequeue/pool consumer
  - emitter가 bullet pool owner를 우회하는 경로
- 의미:
  - emitter는 `Request` 쪽 producer다.
  - 실제 bullet spawn 실행은 후속 공통 execution owner가 담당한다.

## 4. 업데이트 순서/소유권
- 현재 단계에서 고정하는 ownership은 아래까지다.
  - `HazardEmitterCoordinatorSystem`은 외부 입력을 집계해 activation gate를 계산한다.
  - `HazardEmitterEmitBuildSystem`은 coordinator 결과를 읽고 자신의 state를 평가한다.
  - `Telegraph -> Emit` 전이를 관리한다.
  - `Emit` 상태에서 `Emit 1회 request`를 append한다.
- 현재 단계에서 고정하지 않는 ownership은 아래다.
  - `Emit 1회 request`의 payload schema 상세
  - request channel singleton 구조
  - `DiscreteEmitExecutionSystem` budget/backlog/metrics 상세
  - `RateField`/`EventBurst`와의 공통 execution 순서
- 공통 원칙:
  - emitter는 producer
  - spawn execution은 별도 owner
  - pool ownership은 기존 bullet pipeline에 남긴다
- 관련 update order 원칙:
  - source/director state update
  - `HazardEmitterCoordinatorSystem`
  - `HazardEmitterEmitBuildSystem`
  - `BulletRequestFencePublishSystem`
- 권장 `BulletRequestGroup` 순서:
  - `UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))`
  - `UpdateAfter(typeof(RunProgressDirectorSystem))`
  - `UpdateAfter(typeof(SourcePollutionUpdateSystem))`
  - `UpdateBefore(typeof(HazardEmitterEmitBuildSystem))`
  - `UpdateBefore(typeof(BulletRequestFencePublishSystem))`
- 목적:
  - source `Pressure`/progress 입력이 확정된 뒤 coordinator가 gate를 계산한다.
  - emit build는 coordinator 결과만 읽고 자신의 상태기계를 진행한다.

## 5. 성능/리스크
- 성능 원칙:
  - emitter 공통 계약은 profile ref 중심으로 두어, state 없는 세부 발사 필드가 emitter마다 중복 저장되지 않게 한다.
  - 전조 없는 emitter도 동일 4상태를 통과시키되 `Telegraph=0`으로 처리해 runtime 분기를 줄인다.
- 주요 리스크:
  - `PlacementIntent`를 구조 계약에 다시 끌어오면 타입/상태 분기가 불필요하게 증가한다.
  - `RotatingSet`의 실제 순번/동시 활성 제어를 개별 emitter contract에 넣으면 coordinator owner가 흐려진다.
  - `EmissionProfile` 세부 발사 필드를 emitter 공통 계약에 평탄화하면, `T2`의 discrete emit 경계와 중복이 생긴다.
  - emitter가 direct spawn 경로를 가지면 기존 pool/spawn owner와 충돌한다.
  - coordinator가 telegraph/cooldown/emit까지 소유하면 owner 경계가 다시 뭉친다.

## 6. 검증 계획
- 문서/설계 기준 검증:
  - `PlacementIntent`가 runtime contract 또는 policy 분기 기준으로 사용되지 않는다.
  - `RotatingSet`은 개별 emitter policy로 남되, 동시 활성/순번 제어는 상위 coordinator 책임으로 분리된다.
  - 전조 없는 emitter도 `Telegraph=0`으로 동일 4상태를 통과한다.
  - `Emit`이 direct spawn이 아니라 request append 경계로 정의된다.
- 후속 구현 최소 시나리오:
  - `AlwaysCycle` emitter가 `Dormant -> Telegraph(0 가능) -> Emit -> Cooldown` 순서를 유지한다.
  - `ProgressReactive` emitter가 진행도 조건 전까지 `Dormant`를 유지한다.
  - `TriggerReactive` emitter가 trigger 충족 전까지 `Telegraph/Emit`으로 진입하지 않는다.
  - `RotatingSet` emitter가 단독 policy가 아니라 group/coordinator 경유로만 활성 순번을 받는다.
  - `Pressure + player distance` gate emitter가 coordinator에서만 활성 조건을 계산한다.
  - `Source progress threshold` gate emitter가 `CollectedCount / ThresholdDepleted` 기준의 정규화 progress를 사용한다.
  - `DynamicObject` form 확장 없이도 현재 `ObjectBound/PointBound + Static` contract가 유지된다.

## 7. 오픈 이슈
- `RotatingSet coordinator`를 `HazardEmitterCoordinatorSystem` 내부 확장으로 둘지, 별도 emitter group owner로 분리할지.
- `AnchorRef`의 stable reference 표현을 어떤 authoring/runtime seam으로 둘지.
- future `DynamicObject` 추가 시 position/facing resolve owner를 어디에 둘지.
- `HazardEmitterBinding`에서 future gate override까지 다룰지, binding은 stage-applied baseline override에만 고정할지.

## 8. 변경 이력
- 2026-04-07: 초안 작성. `HazardEmitter` 최소 공통 계약을 `유형 기준 + 4정책 + 4상태 + profile ref + emit append` 경계로 고정했다.
- 2026-04-08: `HazardEmitterCoordinatorSystem`을 activation orchestration owner로 추가하고, `Pressure + player distance`, `Source progress threshold` gate 예시를 공통 계약에 반영했다.
- 2026-04-08: `HazardEmitterBinding`, baseline/applied/runtime layering, `SourceHazardEmitterRefBuffer`, stage apply reset 규칙, coordinator gate component 최소 필드를 공통 계약에 추가했다.
