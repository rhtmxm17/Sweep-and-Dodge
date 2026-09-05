# Discrete Emit Spawn Bridge Contract

## Metadata
- doc_id: `TD-029`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-09-05`
- related_docs:
  - [../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md](../ADR/ADR-20260407-01-discrete-emit-bridge-and-spawn-ownership-split.md)
  - [../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md](../ADR/ADR-20260417-01-hazard-actor-direct-emit-ownership.md)
  - [./TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [./TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [./TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [./TD-033-emission-profile-common-schema.md](./TD-033-emission-profile-common-schema.md)

> `WaveClip` discrete branch와 `HazardActor` 직접 발사를 공통 `DiscreteEmitRequest` 경계로 내리는 현재 SSOT. source-wave와 actor emit은 같은 request wire shape를 공유하지만, producer owner와 상위 상태기계는 분리한다.

## 1. 목적
- `SourceClipDiscreteEmitBuildSystem`와 `HazardActorEmitSystem`의 공통 출력 경계를 문서 기준으로 고정한다.
- `DiscreteEmitRequestSeed`와 `DiscreteEmitRequest`의 역할을 현재 코드 기준으로 정리한다.
- direct emit producer가 actor entity와 actor-owned slot execution snapshot을 기준으로 동작한다는 점을 고정한다.
- 예약된 발사 대기와 실제 backlog 정체를 구분하는 age 계약을 고정한다.

## 2. Producer Ownership
### 2.1 Source clip discrete branch
- owner: `SourceClipDiscreteEmitBuildSystem`
- 책임:
  - source event queue 해석
  - event discrete emit occurrence resolve
  - source anchor/sampling 계산
  - `DiscreteEmitRequest` append

### 2.2 Hazard actor direct emit branch
- owner: `HazardActorEmitSystem`
- 책임:
  - actor-owned emit lifecycle (`Dormant -> Telegraph -> Emit -> Cooldown`)
  - selected `PatternSlotId`에 대응하는 execution snapshot resolve
  - actor transform + slot `LocalOffset`로 world anchor 계산
  - `DiscreteEmitRequest` append
- 비책임:
  - discrete backlog consume
  - pool dequeue/spawn apply
  - source-wave phase/lane/clip 문맥

### 2.3 Shared execution branch
- owner: `DiscreteEmitExecutionSystem`
- 책임:
  - `DiscreteEmitRequestBuffer` consume
  - repeat/shot expansion
  - budget/pool gate
  - spawn apply
  - discrete emit backlog/metrics 집계

### 2.4 Pending age contract
- `MaxPendingAgeFrames`는 요청 생성 뒤의 총 체류 시간이 아니라, 실행 가능한 요청이 전진하지 못한 연속 frame 수의 상한이다.
- 다음 상태는 authoring/runtime이 의도한 예약 대기이므로 age 만료 대상이 아니다.
  - `ReadyFrame` 이전의 지연
  - 첫 repeat 이후 `EventShotIntervalSec`가 아직 경과하지 않은 `Timed` 대기
- 요청이 실행 가능 상태로 전환되거나 repeat 하나가 정상 소비되면 age 기준 frame을 갱신한다.
- 실행 가능한 요청이 budget, producer budget 또는 pool 부족으로 진행되지 않을 때만 age가 증가한다.
- `Pending*`은 예약 대기를 포함한 전체 잔여 bullet-equivalent를 나타낸다.
- `DeferredByBudget*`과 `DeferredByPool*`은 실행 가능하지만 해당 원인으로 진행하지 못한 요청만 나타낸다.
- capacity trim은 age와 별개이며 예약 대기를 포함한 전체 pending에 계속 적용한다.

## 3. Request Contract
### 3.1 `DiscreteEmitRequestSeed`
- seed는 producer가 이미 해석한 emit occurrence 1개를 표현한다.
- 최소 provenance:
  - `ProducerKind`
  - `SourceEntity`
  - `ProducerEntity`
  - `EmissionId`
  - `BulletTypeKey`
- actor direct emit path의 provenance 규칙:
  - `ProducerEntity`는 actor entity다.
  - `EmissionId`는 현재 selected `PatternSlotId`다.
  - source 참조는 `HazardActorComponent.SourceEntity`를 사용한다.

### 3.2 Anchor Contract
- 현재 direct emit anchor는 actor 기준 fixed world resolve다.
- world anchor는 아래 순서로 계산한다.
  - `LocalToWorld(actor)`가 있으면 `transform(actor, slot.LocalOffset)`
  - 없으면 `LocalTransform.Position + slot.LocalOffset`
- request payload는 기존 공통 anchor 필드를 유지한다.
  - `AnchorMode`
  - `AnchorEntity`
  - `AnchorPosition`
  - `AnchorLocalOffset`

### 3.3 Shared helper
- source helper:
  - `BuildDiscreteEmitSeedFromWaveEvent(...)`
- actor helper:
  - `BuildDiscreteEmitSeedFromHazardActor(...)`
- 공통 helper:
  - `CreateDiscreteEmitRequest(in DiscreteEmitRequestSeed seed, uint frame)`

## 4. Update Order
- current order:
  - `HazardActorPatternSelectorSystem`
  - `HazardActorEmitSystem`
  - `BulletRequestFencePublishSystem`
  - `DiscreteEmitExecutionSystem`
- 문서상 해석:
  - selector가 current slot을 확정한다.
  - actor emit이 telegraph/emit/cooldown을 진행하고 request를 append한다.
  - discrete execution은 request 채널만 소비한다.

## 5. 제약
- `HazardEmitter` 독립 runtime entity는 더 이상 producer가 아니다.
- actor direct emit path는 selected slot이 없거나 actor가 suppress된 경우 request를 append하지 않는다.
- Telegraph authoring과 `EmissionProfileSO` 참조는 actor slot authoring의 일부이며 request provenance owner는 actor다.
- 기본 `MaxPendingAgeFrames=120`은 유지하며 콘텐츠의 정상 schedule을 맞추기 위해 전역 제한값을 늘리지 않는다.
- age 계약 변경은 pool owner, `FreeListFence`, 시스템 그룹과 producer ownership을 변경하지 않는다.

## 6. 검증 기준
- `WaveClip` discrete event와 `HazardActor` emit 1회가 같은 `DiscreteEmitRequest` wire shape를 공유한다.
- actor direct emit helper가 `ProducerEntity=actor`, `EmissionId=PatternSlotId`로 request를 생성한다.
- 문서상 현재 producer 이름이 `HazardActorEmitSystem`으로만 남아 있어야 한다.
- 전체 소요 시간이 age 상한보다 긴 정상 `Timed` 요청과 age 상한보다 먼 `ReadyFrame` 요청이 예약 대기 중 만료되지 않는다.
- 실행 가능한 상태에서 실제로 정체된 요청은 기존 age 상한과 producer별 만료 metrics를 유지한다.
