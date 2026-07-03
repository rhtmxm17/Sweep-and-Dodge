# Discrete Emit Spawn Bridge Contract

## Metadata
- doc_id: `TD-029`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-07-03`
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

## 6. 검증 기준
- `WaveClip` discrete event와 `HazardActor` emit 1회가 같은 `DiscreteEmitRequest` wire shape를 공유한다.
- actor direct emit helper가 `ProducerEntity=actor`, `EmissionId=PatternSlotId`로 request를 생성한다.
- 문서상 현재 producer 이름이 `HazardActorEmitSystem`으로만 남아 있어야 한다.
