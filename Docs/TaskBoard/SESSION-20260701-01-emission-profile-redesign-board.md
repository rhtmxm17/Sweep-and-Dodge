# SESSION-20260701-01 Emission Profile Redesign Board

## Metadata
- doc_id: `SESSION-20260701-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-07-01`
- related_docs:
  - [SESSION-20260406-01-waveclip-authoring-board.md](SESSION-20260406-01-waveclip-authoring-board.md)
  - [SESSION-20260407-01-hazard-emitter-design-board.md](SESSION-20260407-01-hazard-emitter-design-board.md)
  - [SESSION-20260417-02-hazard-actor-direct-emit-docs-board.md](SESSION-20260417-02-hazard-actor-direct-emit-docs-board.md)
  - [../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md](../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md)
  - [../TechnicalDesign/TD-003-spawn-directive-model.md](../TechnicalDesign/TD-003-spawn-directive-model.md)
  - [../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md](../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../TechnicalDesign/TD-033-emission-profile-common-schema.md](../TechnicalDesign/TD-033-emission-profile-common-schema.md)

## Session Goal
- 한 줄 목표: `EmissionProfile`을 Source/Hazard/Triggered가 공통으로 사용하는 탄막 데이터 단위로 재설계해, 탄막 작성자가 속도/수명/이동/후속 발사 같은 플레이 감각 데이터를 패턴 단위에서 직관적으로 다룰 수 있게 한다.
- 완료 기준:
  - 공통 `EmissionProfile`의 책임과 Source/Hazard/Triggered wrapper의 책임이 문서 기준으로 분리된다.
  - `MotionCompleted -> TriggerEmissionProfile` 1차 스키마가 확정된다.
  - `BulletDefinitionSO`의 movement/reaction 필드를 deprecated로 다루는 마이그레이션 기준이 정리된다.
  - `WaveSpawnEntryAuthoring`, `HazardEmitterEmissionProfileSO`, 기존 secondary spawn reaction 데이터의 통합/호환 전략이 구현 착수 가능한 단위로 분해된다.
  - 데이터 스키마 확정 전에는 `SourceSpawnRequestBuffer`, `DiscreteEmitRequestBuffer`, `BulletSecondarySpawnRequestBuffer` 같은 파이프라인 계층 변경을 착수하지 않는다.
- 이번 세션에서 하지 않을 것:
  - runtime spawn request/channel 재설계 구현
  - 기존 asset migration 실행
  - 전체 WaveClip/HazardActor inspector UX polish

## Adopted Baseline
- `EmissionProfile`은 Source/Hazard/Triggered 모두가 쓰는 공통 탄막 데이터 단위로 둔다.
- `Sampling`, `RateField`, Source area sampling은 Source 전용 개념이므로 `EmissionProfile` 안에 넣지 않고 실행 context/wrapper가 제공한다.
- Triggered emission의 anchor/direction은 `EmissionProfile` 내부 데이터가 아니라 trigger link/context binding이 제공한다.
- 1차 lifecycle trigger 이벤트 범위는 `MotionCompleted`만 연다.
- `BulletDefinitionSO`의 `Speed`, `Lifetime`, `MovementFamily`, `DampedLinear`, `HomingLite`, `OnMotionCompletedExplode`, `OnCleanupRemovedSpawnSecondary`는 신규 데이터 작성 기준에서 deprecated로 취급한다.
- 기존 `BulletSecondarySpawnRequestBuffer` 기반 secondary spawn 경로는 호환 경로로 유지하되, 신규 설계의 SSOT로 확장하지 않는다.

## Working Schema Draft

### Common EmissionProfile
```text
EmissionProfile
- Bullet
- SpawnTuning
  - SpeedOverride
  - LifetimeOverride
  - MovementTuning
- PositionPattern
- Aim
- ShotPattern
- LifecycleTriggers
  - MotionCompleted only in first implementation slice
```

### Source Wrapper
```text
SourceWaveDirective
- EmissionProfile
- SourceEmission
  - RateField / Poisson / EventBurst
- Sampling
  - Anchor
  - AreaSampler
  - SpawnSampleBudget
  - PlayerNoSpawnRadius
- Lane / Phase / Segment timing
```

### Hazard Wrapper
```text
HazardActorPatternSlot
- TelegraphProfile
- EmissionProfile
- CooldownSec
- BaseWeight
- AvailabilityFlags
- LocalOffset
```

### Triggered Wrapper
```text
LifecycleTrigger
- Event: MotionCompleted
- TriggerEmissionProfile
- ContextBinding
  - OriginPosition: LifecycleContactPosition
  - ForwardDirection: LifecycleContactDirection
  - SourceEntity: CauserSourceEntity
  - CauserEntity: CompletedBullet
- DelaySec
```

## Design Constraints
- `EmissionProfile`은 "주어진 execution context에서 어떤 탄막을 펼칠지"만 책임진다.
- Source/Hazard/Triggered의 차이는 wrapper/context binding에 둔다.
- `EmissionProfile`에서 Source 전용 `Sampling`/`RateField`를 직접 소유하지 않는다.
- Trigger graph는 순환 참조를 허용하지 않는다.
- Trigger depth와 frame당 triggered emission budget은 별도 설계가 필요하지만, 구체 runtime policy는 데이터 구조 확정 후 다룬다.
- 기존 exact tuning 값은 테스트 oracle로 승격하지 않는다. 검증은 schema, reference integrity, authoring-to-runtime symbolic contract 중심으로 잡는다.

## Current Data Findings
- `bd_sample_bubble.asset`은 기존 `MotionCompleted -> secondary spawn` 대표 데이터다.
  - `MovementFamily = DampedLinear`
  - `OnMotionCompletedExplode.Enabled = true`
  - secondary bullet은 `bd_sample_bubble_fragment.asset`
  - `PointBurst`, `SpawnCount = 8`, `SpreadAngleDeg = 360`, `SpawnRadius = 0.08`
- `bd_sample_candy.asset`는 `OnCleanupRemovedSpawnSecondary` 대표 데이터다. 1차 범위에는 포함하지 않고 후속 trigger event 후보로 둔다.
- `WaveSpawnEntryAuthoring`과 `HazardEmitterEmissionProfileSO`는 이미 유사한 emission grammar를 중복 소유한다.
- `HazardEmitterEmissionProfileSO`는 Source 전용 sampling/rate 개념 없이 discrete emission에 가까운 형태다.

## Pipeline Preflight Findings
- 현재 Source/Hazard 양쪽에 이미 공통 emission grammar가 존재하지만, wrapper 필드와 한 구조체에 섞여 있다.
  - Source: `SourceClipPatternBuffer`는 segment/lane/phase, rate/sampling 필드와 bullet/position/aim/shot 필드를 함께 가진다.
  - HazardActor: `HazardActorPatternExecutionSlotBuffer`와 `HazardActorEmitActiveEmissionComponent`는 telegraph/cooldown/slot 필드와 bullet/position/aim/shot 필드를 함께 가진다.
  - 결론: authoring 단계에서 공통 `ResolvedEmissionCore`를 먼저 만들고 Source/Hazard/Triggered wrapper가 이를 감싸는 방향이 가장 덜 흔들린다.
- `DiscreteEmitRequestBuffer`는 현재 런타임에서 공통 emission 실행 문법에 가장 가깝다.
  - Source discrete path와 HazardActor direct emit path가 모두 `DiscreteEmitRequestUtility`를 거쳐 같은 실행 시스템으로 들어간다.
  - 단, Source rate-field/sampling path 전체를 대체하지는 못하므로 Source wrapper의 실행 context는 유지해야 한다.
- profile-level `SpeedOverride`, `LifetimeOverride`, `MovementTuning`, `LifecycleTriggers`는 현재 request/apply 경계에 실려 있지 않다.
  - `SpawnRequestCommonUtility.ApplySpawnedBulletState`는 풀에서 꺼낸 bullet의 `BulletSpeedComponent`와 `BulletLifetimeMaxComponent`를 읽어 속도와 수명을 적용한다.
  - `BulletPoolOwnerBootstrapSystem`은 `BulletDefinitionSO` 기반 pool definition에서 speed/lifetime/movement/reaction 컴포넌트를 초기화한다.
  - 결론: 신규 profile tuning을 구현하려면 request payload 또는 spawned bullet apply 단계가 resolved tuning을 받을 수 있어야 한다.
- 기존 `BulletSecondarySpawnRequestBuffer`는 `MotionCompleted -> TriggerEmissionProfile`의 SSOT로 쓰기에는 좁다.
  - count/spread/radius/shape 중심의 secondary spawn만 표현하며 position/aim/shot/movement/lifecycle trigger 문법을 담지 못한다.
  - 결론: legacy compatibility path로 유지하고, 신규 triggered emission은 `DiscreteEmitRequestBuffer` 확장 또는 별도 `TriggeredEmissionRequest`로 설계한다.
- T2b 통합 전략은 runtime concrete schema를 확정하지 않고도 진행 가능하다.
  - 전제: 공통 authoring/resolver 출력은 `ResolvedEmissionCore`로 고정한다.
  - 전제: runtime T6에서 `ResolvedEmissionCore -> SourceSpawnRequestBuffer/DiscreteEmitRequestBuffer/TriggeredEmissionRequest` 매핑을 결정한다.

## Now
- [ ] T2b. `WaveSpawnEntryAuthoring`와 `HazardEmitterEmissionProfileSO` 통합 전략 결정
  - 완료 기준: WaveClip directive가 profile 참조형으로 전환될지, transition 기간 동안 inline directive를 유지할지 결정한다.
  - 점검: 기존 `WaveClipAuthoringResolver`, `HazardEmitterProfileResolver`의 중복 축과 공통 resolver 추출 가능성.
  - 전제: 공통 resolver 출력 단위는 `ResolvedEmissionCore`로 둔다.
  - 제외: request buffer/channel 수정, spawned bullet apply 단계 변경, asset migration.

## Next
- [ ] T3. `MotionCompleted -> TriggerEmissionProfile` validation rule 설계
  - 완료 기준: null reference, self/direct cycle, max depth, usage flag, operational/test asset 교차 참조, deprecated data coexistence 규칙이 정리된다.
- [ ] T4. `BulletDefinitionSO` deprecated 필드 마이그레이션 기준 작성
  - 완료 기준: 신규 작성 금지 필드, 기존 asset compatibility read path, warning/error 전환 시점이 정리된다.
  - 대상: `Speed`, `Lifetime`, `MovementFamily`, `DampedLinear`, `HomingLite`, `OnMotionCompletedExplode`, `OnCleanupRemovedSpawnSecondary`.
- [ ] T5. 기존 샘플 데이터 변환 후보 작성
  - 완료 기준: `bd_sample_bubble -> ep_sample_bubble_parent + ep_sample_bubble_fragments` 변환 예시가 문서화된다.
  - 제외: 실제 asset migration.
- [ ] T6. runtime pipeline 계층 설계 점검
  - 시작 조건: T1~T5에서 데이터 구조와 validation 기준이 확정된 뒤, T2a preflight 결론을 구현 수준으로 구체화할 필요가 있을 때 착수한다.
  - 검토 대상: `SourceSpawnRequestBuffer`, `DiscreteEmitRequestBuffer`, `BulletSecondarySpawnRequestBuffer`, 공통 `ResolvedEmission`/`TriggeredEmissionRequest` 필요 여부.

## Blocked
- 없음

## Parking Lot
- [ ] P1. `CleanupRemoved`, `PlayerHit`, `LifetimeExpired`, `StageBlocked` trigger event 확장
  - 근거: 1차 범위는 `MotionCompleted`만 열고, 이벤트 확장은 데이터 구조 안정화 후 판단한다.
- [ ] P2. `EmissionProfile` graph/preview editor UX
  - 근거: trigger profile 참조 구조가 확정된 뒤 graph view나 preview context를 설계하는 편이 안전하다.
- [ ] P3. frame당 triggered emission budget과 backlog 정책
  - 근거: 데이터 스키마 확정 전 runtime policy를 먼저 고정하면 구조가 불필요하게 좁아질 수 있다.
- [ ] P4. 기존 `BulletSecondarySpawnRequestBuffer` 제거 여부
  - 근거: 신규 구조가 안정화될 때까지 legacy compatibility path로 유지한다.

## Done
- [x] T2a. Pipeline Preflight
  - 결과: T2b는 진행 가능하되, 공통 authoring/resolver 출력 단위로 `ResolvedEmissionCore`가 필요하다고 판단했다.
  - 결과: `DiscreteEmitRequestBuffer`는 공통 emission 실행 문법에 가장 가까운 기존 경로지만 `SpeedOverride`, `LifetimeOverride`, `MovementTuning`, `LifecycleTriggers`를 담지 못하므로 T6에서 확장/분리 설계가 필요하다.
  - 결과: Source rate-field/sampling, Hazard cooldown/telegraph, Trigger context binding은 계속 wrapper/context 책임으로 유지한다.
  - 결과: `BulletSecondarySpawnRequestBuffer`는 신규 `MotionCompleted -> TriggerEmissionProfile`의 SSOT가 아니라 legacy compatibility path로 유지한다.
  - 제외: concrete request buffer/channel 수정, spawned bullet apply 단계 구현, asset migration.
- [x] T1. 공통 `EmissionProfile` 데이터 스키마 TD 초안 작성
  - 결과: `Docs/TechnicalDesign/TD-033-emission-profile-common-schema.md` 초안을 작성했다.
  - 포함: 공통 `EmissionProfile`, `EmissionExecutionContext`, Source/Hazard/Triggered wrapper 책임, `MotionCompleted -> TriggerEmissionProfile`, `BulletDefinitionSO` deprecated 정책, validation 초안, 구현 분해 초안.
  - 제외: concrete spawn request buffer/channel 구조 변경은 T6 후속 점검으로 유지했다.
- [x] D1. 기존 `MotionCompleted -> secondary spawn` 데이터와 테스트 경로를 확인했다.
  - 결과: `bd_sample_bubble.asset`, `BulletMotionCompletedExplodePlayModeTests`, `BulletLifecycleReactionExecutionSystem`, `SecondarySpawnExecutionSystem` 경로가 확인됐다.
- [x] D2. `EmissionProfile` 공통화 방향을 채택했다.
  - 결과: Source/Hazard/Triggered 모두가 공통 `EmissionProfile`을 사용하고, Source 전용 sampling/rate 개념은 wrapper/context가 제공하기로 했다.
- [x] D3. Triggered emission의 anchor/direction은 trigger context binding이 제공하는 B안을 채택했다.
  - 결과: `EmissionProfile`은 canonical context를 소비하는 순수 탄막 문법으로 두고, trigger link가 `LifecycleContactPosition` / `LifecycleContactDirection` 같은 context binding을 제공한다.
- [x] D4. 1차 lifecycle trigger 범위를 `MotionCompleted`로 제한했다.
  - 결과: `CleanupRemoved` 등 다른 lifecycle event는 후속 확장 후보로 분리했다.

## End of Session
- 결과:
- 남은 리스크:
- 다음 세션 시작점:
