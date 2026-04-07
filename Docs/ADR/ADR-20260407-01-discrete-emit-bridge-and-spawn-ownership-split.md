# ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split
> `HazardEmitter`와 `WaveClip` discrete branch를 공통 `DiscreteEmit` 브리지로 내리고 spawn ownership을 분리한 결정

## 상태
- 합의됨 (문서 반영, 구현 예정)

## 배경
- `TD-028`에서 `HazardEmitter`는 direct spawn이 아니라 `Emit 1회 request append` producer로 고정되었다.
- 기존 source spawn 구조는 `WaveClip` discrete event와 sustain/ratefield를 같은 request 경로에서 다루고 있어, `HazardEmitter`를 그대로 흡수하면 source-wave 전용 문맥과 결합이 과도해진다.
- 기존 ADR은 아래를 이미 고정하고 있다.
  - aggregated source request와 budgeted carry-over 정책
  - `EventBurst`/`Poisson`의 event anchor fixation과 timed event 의미
- 이번 단계에서는 위 기존 결정을 유지한 채, `HazardEmitter`와 source discrete branch가 어떤 공통 execution seam으로 만날지 새로 결정해야 했다.

## 결정
1. 공통 discrete bridge 채택
- `WaveClip EventBurst/Poisson event 1회`와 `HazardEmitter Emit 1회`는 공통 `DiscreteEmitRequest` 경계로 내린다.
- 공통화 대상은 discrete emit occurrence이고, `RateField` 지속 스폰은 여기에 포함하지 않는다.

2. producer ownership 분리
- `SourceClipDiscreteEmitBuildSystem`는 아래를 단일 소유한다.
  - source state change -> event queue append
  - queued event start/selection
  - active event clip progression
  - `EventBurst + Poisson event 1회 -> DiscreteEmitRequest` append
- source sustain/ratefield branch는 기존 `SourceSpawnRequestBuffer` 경로에 남기고 별도 owner가 유지한다.
- `HazardEmitterEmitBuildSystem`는 아래를 단일 소유한다.
  - policy 평가
  - `Dormant -> Telegraph -> Emit -> Cooldown`
  - emitter anchor resolve
  - `Emit 1회 -> DiscreteEmitRequest` append

3. execution ownership 분리
- `DiscreteEmitExecutionSystem`는 `DiscreteEmitRequestBuffer` consume, item mutable state, repeat/shot expansion, budget/pool gate, spawn apply 호출을 단일 소유한다.
- producer는 append-only로 남고, append 이후 item mutable state를 다시 수정하지 않는다.

4. request contract 경계
- discrete item 1개는 emit occurrence 1개를 의미한다.
- append 후 merge하지 않는다.
- consume atomic unit은 `repeat 1회`다.
- budget accounting unit은 `bullet 수`다.
- anchor payload는 `AnchorMode + AnchorEntity + AnchorPosition + AnchorLocalOffset`를 가진다.
- 현재 consume semantics는 `FixedWorld`만 지원하고 `SourceRelative`는 future slot로 남긴다.

5. helper seam 채택
- 공통 request 생성 helper는 `DiscreteEmitRequestSeed`를 입력으로 받는다.
- producer별 wrapper가 seed를 만들고, 공통 helper가 append-ready request를 조립한다.
- helper는 payload 조립, clamp/default 적용, mutable runtime state 초기화만 담당한다.
- helper는 policy 평가, clip/event 선택, sampling, anchor resolve, profile 해석을 담당하지 않는다.

6. update order와 budget 분리
- `ExecutionBegin` 관련 순서는 아래로 둔다.
  - `SecondarySpawnExecutionSystem`
  - `DiscreteEmitExecutionSystem`
  - `SpawnRequestRoundRobinExecutionSystem`
- budget은 아래 3경로로 분리한다.
  - `SecondarySpawn`
  - `DiscreteEmit`
  - `SourceRateField`
- pool ownership은 기존 bullet pipeline에 유지한다.

## 대안
- 기존 `SourceSpawnRequestBuffer` 확장으로 흡수
  - 장점: 단기적으로 schema 수가 늘지 않는다.
  - 단점: `Phase/Lane/Clip/Trigger` 등 source-wave 전용 문맥이 `HazardEmitter`까지 침투해 ownership과 request 의미가 흐려진다.
- `HazardEmitter` 전용 spawn consumer 추가
  - 장점: 초기 구현이 단순해 보일 수 있다.
  - 단점: pool/spawn ownership이 갈라지고 discrete emit 발사 문법이 중복된다.
- source/event/emitter를 모두 하나의 상위 build system로 유지
  - 장점: 시스템 수가 적다.
  - 단점: event lifecycle ownership과 sustain/ratefield ownership이 다시 뒤엉킨다.

## 결과
- `HazardEmitter`를 기존 bullet pipeline에 무리하게 끼워 넣지 않고도 공통 spawn execution seam을 확보한다.
- source discrete branch와 sustain/ratefield branch의 ownership이 명확히 분리된다.
- `EventBurst/Poisson`와 emitter emit이 같은 request wire shape를 공유할 수 있어, 발사 문법과 budget arbitration을 공통 계층에서 다룰 수 있다.
- 대신 `DiscreteEmit`이라는 별도 request/consumer 계층을 유지해야 하며, `RotatingSet coordinator`, `AnchorRef` wire shape, `SourceRelative` consume semantics는 후속 결정이 필요하다.

## 후속
- `TD-028`은 emitter 공통 계약 SSOT로 유지한다.
- `TD-029`는 discrete emit bridge SSOT로 유지한다.
- 구현 단계에서 `SourceClipDiscreteEmitBuildSystem`, `HazardEmitterEmitBuildSystem`, `DiscreteEmitExecutionSystem` 진입점과 request schema를 구체화한다.
- 후속 설계에서 `RotatingSet coordinator` owner와 `AnchorRef`/`SourceRelative` seam을 확정한다.
