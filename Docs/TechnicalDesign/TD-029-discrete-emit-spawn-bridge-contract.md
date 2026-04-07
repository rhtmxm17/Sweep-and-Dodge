# Discrete Emit Spawn Bridge Contract

## Metadata
- doc_id: `TD-029`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-04-07`
- related_docs:
  - [../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md](../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md)
  - [./TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [./TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [./TD-028-hazard-emitter-common-contract.md](./TD-028-hazard-emitter-common-contract.md)
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)

> `WaveClip EventBurst/Poisson`와 `HazardEmitter Emit`을 공통 `DiscreteEmitRequest` 경계로 내리기 위해, producer ownership, request contract, execution order, budget 경계를 `DiscreteEmit` 브리지 기준으로 고정한다.

## 1. 문제 정의
- 기존 source spawn 구조는 `WaveClip` 기반 discrete event와 sustain/ratefield를 같은 request 경로에서 다루고 있다.
- `HazardEmitter`는 `TD-028`에서 direct spawn이 아닌 `Emit 1회 request append` producer로 고정되었으므로, 기존 source-wave discrete branch와 공통 실행 경계를 맞출 필요가 있다.
- 하지만 현재 `SourceSpawnRequestBuffer`는 `Phase/Lane/Clip/Trigger` 등 source-wave 전용 문맥을 강하게 포함하고 있어, `HazardEmitter`를 동일 schema에 직접 흡수하기 어렵다.

## 2. 목표/비목표
- 목표:
  - `WaveClip EventBurst + Poisson`와 `HazardEmitter Emit`을 공통 `DiscreteEmit` 경계로 내리는 구조를 SSOT로 고정한다.
  - `SourceClipDiscreteEmitBuildSystem`, `HazardEmitterEmitBuildSystem`, `DiscreteEmitExecutionSystem`의 책임 경계를 결정 완료 상태로 고정한다.
  - `DiscreteEmitRequest`와 `DiscreteEmitRequestSeed`의 역할 차이를 문서 기준으로 단일 해석 가능하게 만든다.
  - discrete emit 경로의 update order와 budget 분리 기준을 고정한다.
- 비목표:
  - runtime code 구현
  - `AnchorRef` wire shape 확정
  - `RotatingSet coordinator`의 구체 owner 확정
  - `SourceRelative` anchor consume semantics 구현
  - authoring asset/schema 상세 확정

## 3. 구조 개요
- 공통화 대상은 아래 두 discrete 발사 단위다.
  - `WaveClip`의 `EventBurst event 1회`
  - `WaveClip`의 `Poisson event 1회`
  - `HazardEmitter`의 `Emit 1회`
- 공통화 대상이 아닌 것은 아래다.
  - `RateField` 지속 스폰
  - source `Phase/Lane/Clip` 선택 로직
  - emitter `ActivationPolicy/Telegraph/Cooldown` 평가 로직

```text
WaveClip discrete branch ----\
                              -> DiscreteEmitRequest -> DiscreteEmitExecutionSystem -> bullet spawn apply
HazardEmitter branch --------/

WaveClip sustain/ratefield -------------------------> SourceSpawnRequestBuffer -> SpawnRequestRoundRobinExecutionSystem
```

## 4. Ownership
### 4.1 SourceClipDiscreteEmitBuildSystem
- 공식 범위는 `EventBurst + Poisson`이다.
- 아래를 단일 소유한다.
  - source state change -> `SourceEventQueueBuffer` append
  - queued event start/selection
  - active event clip progression
  - source geometry/sampling을 통한 discrete emit anchor resolve
  - `EventBurst + Poisson event 1회 -> DiscreteEmitRequest` append
- 아래는 소유하지 않는다.
  - sustain lane/runtime
  - sustain clip selection
  - `RateField`/sustain request build
  - `DiscreteEmitRequest` consume
  - pool dequeue/spawn apply

### 4.2 Existing Source Sustain/RateField Path
- 기존 source sustain/ratefield branch는 `SourceSpawnRequestBuffer` 경로에 남긴다.
- 아래를 단일 소유한다.
  - `SourceSustainRuntimeComponent`
  - `SourceSustainRuntimeLaneBuffer`
  - sustain clip selection
  - `RateField`/sustain request build
- 아래는 소유하지 않는다.
  - event queue
  - active event clip discrete progression
  - `DiscreteEmitRequest` append

### 4.3 HazardEmitterEmitBuildSystem
- 아래를 단일 소유한다.
  - `ActivationPolicy` 평가
  - `Dormant -> Telegraph -> Emit -> Cooldown` 전이
  - emitter anchor resolve
  - `Emit 1회 -> DiscreteEmitRequest` append
- 아래는 소유하지 않는다.
  - `DiscreteEmitRequest` consume
  - pool dequeue/spawn apply
  - source-wave `Phase/Lane/Clip` 문맥

### 4.4 DiscreteEmitExecutionSystem
- 아래를 단일 소유한다.
  - `DiscreteEmitRequestBuffer` consume
  - item mutable state 갱신
  - repeat/shot expansion
  - budget/pool gate
  - spawn apply 호출
  - discrete emit backlog/metrics 집계
- 아래는 소유하지 않는다.
  - source event queue lifecycle
  - emitter policy/state 전이
  - sampling/anchor resolve
  - pool ownership

## 5. Request Contract
### 5.1 DiscreteEmitRequest 의미
- discrete item 1개는 emit occurrence 1개를 의미한다.
- append 후 merge하지 않는다.
- consume atomic unit은 `repeat 1회`다.
- budget accounting unit은 `bullet 수`다.
- completion은 `RemainingRepeats == 0`이다.

### 5.2 Anchor Contract
- request anchor payload는 아래 필드를 가진다.
  - `AnchorMode`
  - `AnchorEntity`
  - `AnchorPosition`
  - `AnchorLocalOffset`
- 현재 consume semantics는 `FixedWorld`만 지원한다.
- `SourceRelative`는 future slot로 남긴다.
  - schema에는 포함하지만 현재 execution owner는 consume하지 않는다.

### 5.3 DiscreteEmitRequestSeed
- 공통 request 생성 helper 입력 타입은 `DiscreteEmitRequestSeed`다.
- seed는 이미 resolve된 emit occurrence만 표현한다.
- seed는 최소한 아래 정보를 포함한다.
  - provenance
    - `ProducerKind`
    - `SourceEntity`
    - `ProducerEntity`
    - `EmissionId`
    - `BulletTypeKey`
  - resolved anchor
    - `AnchorMode`
    - `AnchorEntity`
    - `AnchorPosition`
    - `AnchorLocalOffset`
  - resolved emission grammar
    - `PositionPatternMode`
    - pattern geometry fields
    - `AimMode`
    - `AimSnapshotTiming`
    - angle/spiral fields
    - `ShotPatternMode`
    - `ShotCount`
    - `NWayAngleSpacingDeg`
  - repeat/schedule
    - `EventShotSchedule`
    - `EventShotIntervalSec`
    - `RepeatCount`
  - queue policy
    - `Priority`

### 5.4 Shared Helper Contract
- producer별 wrapper:
  - `BuildDiscreteEmitSeedFromWaveEvent(...)`
  - `BuildDiscreteEmitSeedFromEmitter(...)`
- 공통 helper:
  - `CreateDiscreteEmitRequest(in DiscreteEmitRequestSeed seed, uint frame)`
- helper는 아래만 담당한다.
  - payload 조립
  - clamp/default 적용
  - mutable runtime state 초기화
  - append-ready request 반환
- helper는 아래를 담당하지 않는다.
  - policy 평가
  - clip/event 선택
  - sampling
  - anchor resolve
  - profile 해석

### 5.5 Helper 기본값
- helper는 최소한 아래 mutable state를 초기화한다.
  - `RemainingRepeats = max(1, RepeatCount)`
  - `RepeatSequence = 0`
  - `EventAimInitialized = 0`
  - `EventAimTargetPosition = float3.zero`
  - `EventShotElapsedSec = 0`
  - `OldestFrame = frame`

## 6. Update Order / Budget
### 6.1 ExecutionBegin 순서
- `ExecutionBegin`의 관련 시스템 순서는 아래로 고정한다.

```text
SecondarySpawnExecutionSystem
-> DiscreteEmitExecutionSystem
-> SpawnRequestRoundRobinExecutionSystem
```

- 의도:
  - reaction secondary는 먼저 처리
  - authored/discrete emit은 그 다음 처리
  - ambient 성격의 `RateField`는 마지막 처리

### 6.2 Budget 경계
- budget은 아래 3경로로 분리한다.
  - `SecondarySpawn`
  - `DiscreteEmit`
  - `SourceRateField`
- 공유는 pool만 한다.
  - `FreeByKey`
  - pool ownership
  - low-level spawn apply helper
- 분리는 아래를 기준으로 한다.
  - request channel
  - backlog
  - budget
  - metrics

### 6.3 Arbitration
- `DiscreteEmitRequest`는 `Priority`를 payload에 가진다.
- 채널 내부 arbitration 기본 규칙은 아래다.
  - `Priority DESC`
  - 동률이면 `OldestFrame ASC`
- producer round-robin은 초기 범위에 포함하지 않는다.
  - starvation이 실제로 관측될 때 후속 범위로 추가 검토한다.

## 7. 제약 / 제외 문맥
- 아래 source-wave 전용 문맥은 `DiscreteEmitRequest`에 넣지 않는다.
  - `SourceWavePhaseId`
  - `SourceSpawnLaneId`
  - `TriggerState`
  - `ClipId`
  - `SamplingAnchorMode`
  - `AreaSamplerMode`
  - `SpawnSampleBudget`
  - `PlayerNoSpawnRadius`
- 이유:
  - 위 정보는 source discrete producer 단계에서 이미 해석되어야 하며, `HazardEmitter`와 공유되는 execution wire shape의 일부가 아니다.

## 8. 검증 계획
- 문서 기준 검증:
  - `TD-028`은 emitter 공통 계약만 다루고, `TD-029`는 discrete emit bridge만 다룬다.
  - `EventBurst + Poisson`이 `SourceClipDiscreteEmitBuildSystem` 범위로 명시된다.
  - `RateField`가 기존 source path에 남는다고 명시된다.
  - `HazardEmitter`가 direct spawn하지 않는다고 명시된다.
  - `DiscreteEmitRequestSeed`와 `DiscreteEmitRequest`의 역할 차이가 분명하다.
  - `FixedWorld` 현재 지원 / `SourceRelative` future slot 구분이 모호하지 않다.
- 후속 구현 acceptance 기준:
  - `WaveClip EventBurst` 1회와 `HazardEmitter Emit` 1회가 같은 request wire shape로 내려간다.
  - `Poisson`도 같은 discrete channel로 내릴 수 있다.
  - `RateField`는 기존 `SourceSpawnRequestBuffer` 경로에 남는다.
  - discrete item은 merge 없이 repeat 단위로만 소비된다.
  - `Secondary`, `DiscreteEmit`, `RateField` budget이 독립적으로 계측된다.

## 9. 오픈 이슈
- `RotatingSet coordinator`의 구체 owner를 source owner에 둘지, emitter group owner에 둘지.
- `AnchorRef`의 stable reference 표현을 어떤 authoring/runtime seam으로 둘지.
- `SourceRelative` anchor consume semantics를 언제 구현 범위로 승격할지.
- `DiscreteEmitExecutionSystem` metrics 구체 필드와 경고 임계치를 어떻게 둘지.

## 10. 변경 이력
- 2026-04-07: 초안 작성. `HazardEmitter`와 `WaveClip EventBurst/Poisson`를 공통 `DiscreteEmit` 브리지로 내리는 ownership, request contract, update order, budget 경계를 고정했다.
