# SESSION-20260701-01 Emission Profile Redesign Board

## Metadata
- doc_id: `SESSION-20260701-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-07-03`
- related_docs:
  - [SESSION-20260406-01-waveclip-authoring-board.md](SESSION-20260406-01-waveclip-authoring-board.md)
  - [SESSION-20260407-01-hazard-emitter-design-board.md](SESSION-20260407-01-hazard-emitter-design-board.md)
  - [SESSION-20260417-02-hazard-actor-direct-emit-docs-board.md](SESSION-20260417-02-hazard-actor-direct-emit-docs-board.md)
  - [../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md](../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md)
  - [../TechnicalDesign/TD-003-spawn-directive-model.md](../TechnicalDesign/TD-003-spawn-directive-model.md)
  - [../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md](../TechnicalDesign/TD-029-discrete-emit-spawn-bridge-contract.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../TechnicalDesign/TD-033-emission-profile-common-schema.md](../TechnicalDesign/TD-033-emission-profile-common-schema.md)

## Reading Guide
- 현재 기준은 `Adopted Baseline`, `Done`의 최신 항목, `End of Session`, 그리고 `TD-033`이다.
- `Current Data Findings`, `Pipeline Preflight Findings`, `Transition Strategy Decision`은 redesign 초기에 작성된 조사/결정 기록이다. 해당 섹션 안의 legacy 명칭은 당시 상태를 설명하기 위한 역사적 표현이며, 현재 runtime/asset 경로를 의미하지 않는다.
- legacy lifecycle reaction 경로의 현재 상태:
  - `OnMotionCompletedExplode`: migration 완료 후 field/component/authoring/runtime fallback 제거.
  - `OnCleanupRemovedSpawnSecondary`: migration 완료 후 field/component/authoring/channel/system 제거.
  - `BulletSecondarySpawnRequestBuffer` / `SecondarySpawnExecutionSystem`: 제거.
  - `HazardEmitterEmissionProfileSO` / `heep_*`: 제거.

## Session Goal
- 한 줄 목표: `EmissionProfile`을 Source/Hazard/Triggered가 공통으로 사용하는 탄막 데이터 단위로 재설계해, 탄막 작성자가 속도/수명/이동/후속 발사 같은 플레이 감각 데이터를 패턴 단위에서 직관적으로 다룰 수 있게 한다.
- 완료 기준:
  - 공통 `EmissionProfile`의 책임과 Source/Hazard/Triggered wrapper의 책임이 문서 기준으로 분리된다.
  - `MotionCompleted -> TriggerEmissionProfile` 1차 스키마가 확정된다.
  - `BulletDefinitionSO`의 movement/reaction 필드를 deprecated로 다루는 마이그레이션 기준이 정리된다.
  - `WaveSpawnEntryAuthoring`, `HazardEmitterEmissionProfileSO`, 기존 secondary spawn reaction 데이터의 통합/호환 전략이 구현 착수 가능한 단위로 분해된다.
  - 최종 완료 시점에는 Source/Hazard/Triggered의 공통 탄막 문법이 모두 `EmissionProfileSO` 참조형으로 전환된다.
  - 기존 WaveClip/Hazard/secondary spawn asset migration이 완료되고, operational asset에 남은 inline common emission grammar는 제거되거나 compatibility-only 상태로 격하된다.
  - 데이터 스키마 확정 전에는 `SourceSpawnRequestBuffer`, `DiscreteEmitRequestBuffer` 같은 파이프라인 계층 변경을 착수하지 않는다.
- 이번 T2b 문서 반영 직후에 하지 않을 것:
  - runtime spawn request/channel 재설계 구현
  - 기존 asset migration 실행
  - 전체 WaveClip/HazardActor inspector UX polish

## Adopted Baseline
- `EmissionProfile`은 Source/Hazard/Triggered 모두가 쓰는 공통 탄막 데이터 단위로 둔다.
- `Sampling`, `RateField`, Source area sampling은 Source 전용 개념이므로 `EmissionProfile` 안에 넣지 않고 실행 context/wrapper가 제공한다.
- Triggered emission의 anchor/direction은 `EmissionProfile` 내부 데이터가 아니라 trigger link/context binding이 제공한다.
- lifecycle trigger 이벤트 범위는 현재 `MotionCompleted`, `CleanupRemoved`까지 연다.
- `BulletDefinitionSO`의 `Speed`, `Lifetime`, `MovementFamily`, `DampedLinear`, `HomingLite`, `OnCleanupRemovedSpawnSecondary`는 신규 데이터 작성 기준에서 deprecated로 취급한다.
- `OnMotionCompletedExplode`는 `EmissionProfileSO.LifecycleTriggers.MotionCompleted`로 migration 완료 후 제거됐다.
- `OnCleanupRemovedSpawnSecondary`는 `EmissionProfileSO.LifecycleTriggers.CleanupRemoved`로 migration 완료 후 제거됐다.
- 기존 `BulletSecondarySpawnRequestBuffer` 기반 secondary spawn 경로는 제거됐다. 신규/현재 후속 발사의 SSOT는 `EmissionProfileSO` lifecycle trigger와 `DiscreteEmitRequestBuffer` registry path다.
- 최종 목표는 전면 참조형 전환이다. Source/Hazard/Triggered의 active authoring SSOT는 `EmissionProfileSO` 참조다.
- 전환형 하이브리드는 최종 아키텍처가 아니라 staged migration 전략으로만 사용했다. cleanup 완료 후 `WaveSpawnEntryAuthoring` inline common grammar와 `HazardEmitterEmissionProfileSO` compatibility source는 제거됐다.

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
  - MotionCompleted
  - CleanupRemoved
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
- Event: MotionCompleted / CleanupRemoved
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

## Initial Data Findings (Historical)
- redesign 시작 시점의 `bd_sample_bubble.asset`은 기존 `MotionCompleted -> secondary spawn` 대표 데이터였다.
  - `MovementFamily = DampedLinear`
  - `OnMotionCompletedExplode.Enabled = true`
  - secondary bullet은 `bd_sample_bubble_fragment.asset`
  - `PointBurst`, `SpawnCount = 8`, `SpreadAngleDeg = 360`, `SpawnRadius = 0.08`
- 기존 `OnCleanupRemovedSpawnSecondary` serialized block은 bullet definition asset에서 제거됐다. 확인 시점에 enabled operational cleanup secondary 데이터는 남아 있지 않아 별도 target profile 생성 migration은 필요하지 않았다.
- redesign 시작 시점에는 `WaveSpawnEntryAuthoring`과 `HazardEmitterEmissionProfileSO`가 유사한 emission grammar를 중복 소유했다.
- `HazardEmitterEmissionProfileSO`는 이후 `EmissionProfileSO` 참조형으로 migration된 뒤 제거됐다.

## Pipeline Preflight Findings (Historical)
- preflight 시점에는 Source/Hazard 양쪽에 이미 공통 emission grammar가 존재했지만, wrapper 필드와 한 구조체에 섞여 있었다.
  - Source: `SourceClipPatternBuffer`는 segment/lane/phase, rate/sampling 필드와 bullet/position/aim/shot 필드를 함께 가진다.
  - HazardActor: `HazardActorPatternExecutionSlotBuffer`와 `HazardActorEmitActiveEmissionComponent`는 telegraph/cooldown/slot 필드와 bullet/position/aim/shot 필드를 함께 가진다.
  - 결론: authoring 단계에서 공통 `ResolvedEmissionCore`를 먼저 만들고 Source/Hazard/Triggered wrapper가 이를 감싸는 방향이 가장 덜 흔들린다.
- `DiscreteEmitRequestBuffer`는 현재 런타임에서 공통 emission 실행 문법에 가장 가깝다.
  - Source discrete path와 HazardActor direct emit path가 모두 `DiscreteEmitRequestUtility`를 거쳐 같은 실행 시스템으로 들어간다.
  - 단, Source rate-field/sampling path 전체를 대체하지는 못하므로 Source wrapper의 실행 context는 유지해야 한다.
- preflight 시점에는 profile-level `SpeedOverride`, `LifetimeOverride`, `MovementTuning`, `LifecycleTriggers`가 request/apply 경계에 실려 있지 않았다.
  - `SpawnRequestCommonUtility.ApplySpawnedBulletState`는 풀에서 꺼낸 bullet의 `BulletSpeedComponent`와 `BulletLifetimeMaxComponent`를 읽어 속도와 수명을 적용한다.
  - `BulletPoolOwnerBootstrapSystem`은 `BulletDefinitionSO` 기반 pool definition에서 speed/lifetime/movement/reaction 컴포넌트를 초기화한다.
  - 결론: 신규 profile tuning을 구현하려면 request payload 또는 spawned bullet apply 단계가 resolved tuning을 받을 수 있어야 한다.
  - 현재 상태: SpawnTuning/MovementTuning/LifecycleTriggers는 profile-resolved runtime path에 반영됐다.
- 기존 `BulletSecondarySpawnRequestBuffer`는 `MotionCompleted -> TriggerEmissionProfile`의 SSOT로 쓰기에는 좁다.
  - count/spread/radius/shape 중심의 secondary spawn만 표현하며 position/aim/shot/movement/lifecycle trigger 문법을 담지 못한다.
  - 당시 결론: legacy compatibility path로 유지하고, 신규 triggered emission은 `DiscreteEmitRequestBuffer` 확장 또는 별도 `TriggeredEmissionRequest`로 설계한다.
  - 현재 상태: `BulletSecondarySpawnRequestBuffer`는 제거됐고, 신규/현재 triggered emission은 `DiscreteEmitRequestBuffer` registry path를 사용한다.
- T2b 통합 전략은 runtime concrete schema를 확정하지 않고도 진행 가능하다.
  - 전제: 공통 authoring/resolver 출력은 `ResolvedEmissionCore`로 고정한다.
  - 전제: runtime T6에서 `ResolvedEmissionCore -> SourceSpawnRequestBuffer/DiscreteEmitRequestBuffer/TriggeredEmissionRequest` 매핑을 결정한다.

## Transition Strategy Decision
- 판단: 최종 목표가 마이그레이션 포함 전면 참조형 전환이어도, 전환형 하이브리드를 중간 단계로 거치는 것을 권장한다.
- 이유:
  - `HazardEmitterEmissionProfileSO`는 이미 별도 profile asset 구조라 `EmissionProfileSO`로 옮기기 쉽지만, `WaveSpawnEntryAuthoring`은 Source 전용 `Emission/Sampling`과 공통 `Payload/Position/Aim/Shot`이 같은 inline directive 안에 섞여 있다.
  - Source는 sustain/event timeline, rate-field, sampling, lane/phase 문맥이 강하므로 즉시 전면 참조형으로 바꾸면 migration과 runtime 변경 범위가 동시에 커진다.
  - `ResolvedEmissionCore`를 먼저 만들면 Source/Hazard/Triggered가 같은 공통 grammar를 resolve한다는 계약을 선행 검증할 수 있다.
  - 이후 `WaveSpawnEntryAuthoring`의 inline common grammar를 `EmissionProfileSO` 참조로 치환하고, `HazardEmitterEmissionProfileSO`를 compatibility alias 또는 migration source로 격하하면 전면 참조형 목표와 충돌하지 않는다.
- 결론:
  - 최종안: 전면 참조형 `EmissionProfileSO`.
  - 이행안: 전환형 하이브리드.
  - 금지: transition inline support를 장기 authoring SSOT로 고정하는 것.

## Now
- 없음

## Next
- 없음

## Blocked
- 없음

## Parking Lot
- [ ] P1. `PlayerHit`, `LifetimeExpired`, `StageBlocked` trigger event 확장
  - 근거: 현재 범위는 `MotionCompleted`, `CleanupRemoved`까지 열었고, 나머지 이벤트는 데이터 구조 안정화 후 판단한다.
- [ ] P2. `EmissionProfile` graph/preview editor UX
  - 근거: trigger profile 참조 구조가 확정된 뒤 graph view나 preview context를 설계하는 편이 안전하다.
## Done
- [x] T15. ProducerKind별 DiscreteEmit budget/backlog 정책 구현
  - 결과: C안 기준으로 단일 `DiscreteEmitRequestBuffer` / `DiscreteEmitExecutionSystem`을 유지했다.
  - 결과: `DiscreteEmitPolicyComponent`에 `WaveClipEvent`, `HazardActor`, `TriggeredEmission`별 budget/cap field를 추가했다.
  - 결과: `DiscreteEmitBacklogMetricsComponent`에 ProducerKind별 pending/deferred/budget used/dropped/expired metrics를 추가했다.
  - 결과: `DiscreteEmitExecutionSystem`이 global budget/cap과 ProducerKind별 budget/cap을 함께 만족하는 request만 실행 후보로 선택하도록 갱신했다.
  - 결과: ProducerKind별 값이 0이면 기존 global 정책을 fallback으로 사용해 기존 fixture 호환성을 유지한다.
  - 검증: 관련 EditMode 테스트 29개 통과.
  - 검증: DiscreteEmit 관련 EditMode 테스트 12개 통과.
  - 검증: 전체 EditMode 464개 통과.
  - 검증: `BulletPlayModeSmokeTests` 39개 통과.
  - 검증: 최종 compile 후 프로젝트 코드 error 0건. 단, MCP bridge 연결 로그는 Unity Console의 error 타입으로 남는 도구 로그로 확인했다.
- [x] T14. CleanupRemoved legacy runtime/data 제거 및 profile trigger 전환
  - 결과: `EmissionProfileSO.LifecycleTriggers.CleanupRemoved` authoring schema와 resolver/runtime registry field를 추가했다.
  - 결과: `VacuumCollected`, `CarryFullRemoved` lifecycle request는 profile registry에서 `CleanupRemoved.TargetProfile`을 조회해 `DiscreteEmitRequestBuffer`에 triggered emission request를 append한다.
  - 결과: `BulletDefinitionSO.OnCleanupRemovedSpawnSecondary`, cleanup secondary component/authoring, `BulletSecondarySpawnRequestBuffer`, `SecondarySpawnExecutionSystem`을 제거했다.
  - 결과: bullet definition asset의 `OnCleanupRemovedSpawnSecondary` serialized block을 제거했다.
  - 결과: legacy secondary spawn 전용 EditMode/PlayMode 테스트를 제거하고, CleanupRemoved profile trigger behavior/validation/registry 테스트로 갱신했다.
  - 검증: 관련 EditMode 테스트 75개 통과.
  - 검증: 전체 EditMode 462개 통과.
  - 검증: `BulletPlayModeSmokeTests` 39개 통과.
  - 검증: 최종 compile 후 프로젝트 코드 error 0건. 단, MCP bridge 연결 로그는 Unity Console의 error 타입으로 남는 도구 로그로 확인했다.
- [x] T13. MotionCompleted legacy runtime/authoring/test 제거
  - 결과: `BulletDefinitionSO.OnMotionCompletedExplode` 필드, `BulletOnMotionCompletedExplodeReactionComponent`, optional authoring 파일을 제거했다.
  - 결과: `BulletVisualPrefabAuthoring` / `BulletPoolOwnerBootstrapSystem`의 MotionCompleted legacy bake/bootstrap 경로를 제거했다.
  - 결과: `BulletLifecycleReactionExecutionSystem`에서 registry miss 시 legacy secondary spawn fallback을 제거했다. 이제 MotionCompleted 후속 발사는 profile registry 경로만 사용한다.
  - 결과: `ContentValidationRules`의 legacy coexistence warning `CVW041`과 forbidden optional authoring 검사 항목을 제거했다.
  - 결과: legacy MotionCompleted explode 전용 EditMode/PlayMode 테스트를 삭제하고, profile trigger 테스트로 갱신했다.
  - 검증: 관련 EditMode 테스트 64개 통과.
  - 검증: 전체 EditMode 478개 통과.
  - 검증: `BulletPlayModeSmokeTests` 39개 통과.
  - 검증: 최종 compile 후 프로젝트 코드 error 0건. 단, MCP bridge 연결 로그는 Unity Console의 error 타입으로 남는 도구 로그로 확인했다.
- [x] T12. MotionCompleted legacy data migration
  - 결과: operational/test bullet definition asset의 `OnMotionCompletedExplode` serialized block을 제거했다.
  - 결과: enabled legacy 데이터는 `bd_sample_bubble` 1건이었고, 이미 존재하는 `ep_sample_bubble_parent -> ep_sample_bubble_fragments` profile trigger 경로로 대체됐다.
  - 결과: `OnMotionCompletedExplode.SpawnRadius = 0.08`은 기존 TD 기준대로 1차 단순 변환에서는 보존하지 않았다. exact radius 보존은 후속 `PositionPattern` 확장 후보로 유지한다.
- [x] T11. `MotionCompleted` LifecycleTrigger runtime registry 구현
  - 결과: `BulletEmissionProfileRefComponent`를 추가해 spawned bullet이 자신이 spawn된 `ProfileRefId`를 보유하도록 했다.
  - 결과: `EmissionProfileRuntimeRegistryTag` singleton과 `EmissionProfileRuntimeRegistryBuffer`를 추가했다.
  - 결과: Stage apply 시 active `StageDefinitionSO`에서 Source WaveClip directive profile, HazardActor pattern slot emission profile, recursive `MotionCompleted.TargetProfile`을 수집해 registry를 재구성한다.
  - 결과: `ProfileRefId = EmissionProfileSO.GetInstanceID()` 기준으로 registry entry를 de-duplicate한다.
  - 결과: `DiscreteEmitProducerKind.TriggeredEmission`, `DiscreteEmitRequestBuffer.CauserEntity`, `DiscreteEmitRequestBuffer.ReadyFrame`을 추가했다.
  - 결과: 기존 Source/Hazard discrete emit request는 `ReadyFrame = frame`으로 생성하고, MotionCompleted trigger request는 fixed tick 기준 delay를 적용해 최소 다음 frame부터 실행한다.
  - 결과: `DiscreteEmitExecutionSystem`은 `ReadyFrame > currentFrame` request를 실행 후보에서 제외하고 backlog에 유지한다.
  - 결과: `BulletLifecycleReactionExecutionSystem`은 MotionCompleted 처리 시 registry profile trigger를 먼저 조회하고, append 성공 시 legacy `BulletOnMotionCompletedExplodeReactionComponent`를 실행하지 않는다.
  - 결과: registry miss/target miss는 exception 없이 no-op/fallback 처리한다.
  - 결과: `ContentValidationRules`에 profile MotionCompleted trigger와 legacy OnMotionCompletedExplode 공존 warning을 추가했다.
  - 검증: compile 후 프로젝트 코드 error 0건. 단, MCP bridge 연결 로그가 Unity Console의 error 타입으로 남는 현상은 별도 도구 로그로 확인했다.
  - 검증: 관련 EditMode 테스트 75개 통과.
  - 검증: 전체 EditMode 483개 통과.
  - 검증: `BulletPlayModeSmokeTests` 39개 통과.
- [x] T10. profile runtime SpawnTuning/MovementTuning 적용
  - 결과: `SpawnTuning.SpeedOverride` / `LifetimeOverride`가 Source/Hazard request와 spawned bullet apply 단계까지 전달된다.
  - 결과: spawn 시점에 profile speed/lifetime override가 있으면 `BulletSpeedComponent`, `BulletVelocityComponent`, `BulletLifetimeComponent`, `BulletLifetimeMaxComponent`를 갱신한다.
  - 결과: `BulletMovementRuntimeComponent`를 추가해 spawned bullet에 profile movement override를 기록한다.
  - 결과: `BulletSimulationSystem`은 runtime movement component가 있는 bullet을 profile-resolved movement 값 기준으로 처리한다.
  - 결과: 기존 `BulletDefinitionSO` movement 값은 pool/bootstrap fallback으로 유지하고, profile override가 있으면 runtime component가 우선한다.
  - 검증: compile 후 프로젝트 코드 error 0건.
  - 검증: 관련 EditMode/PlayMode smoke 통과.
- [x] T9. unused emission compatibility cleanup
  - 결과: `HazardEmitterEmissionProfileSO` 타입, `heep_*` operational/test asset, T8 migration utility를 제거했다.
  - 결과: `HazardActorPatternSlotAuthoring`은 `Emission.Profile`과 repeat/cooldown schedule을 직접 소유하도록 전환했다.
  - 결과: `WaveSpawnEntryAuthoring`은 `Profile + Emission + Sampling`만 소유하고, inline `Payload / PositionPattern / Aim / ShotPattern` fallback을 제거했다.
  - 결과: 운영/테스트 WaveClip 및 Hazard prefab을 `EmissionProfileSO` 참조형으로 재직렬화했다.
  - 결과: 테스트 전용 `Assets/_Project/99_Tests/TestData/EmissionProfiles/` fixture를 생성했다.
  - 결과: `ContentValidationRules`에서 `CV050`과 Hazard wrapper 수집을 제거하고, WaveClip directive `Profile` 필수 계약을 `CV046`으로 고정했다.
  - 결과: 관련 resolver/editor/validation/sample asset 테스트를 참조형 기준으로 갱신했다.
  - 검증: compile 후 Unity Console code error 0건.
- [x] T8. asset migration 및 legacy 격하
  - 결과: `Assets/_Project/02_Scripts/ECS/Editor/EmissionProfileAssetMigrationUtility.cs`를 추가해 operational WaveClip/Hazard/secondary spawn sample을 `EmissionProfileSO` 참조형으로 migration할 수 있게 했다.
  - 결과: `Assets/_Project/03_Datas/EmissionProfiles/` 아래에 WaveClip 12개, Hazard 5개, BulletReaction 2개 profile asset을 생성했다.
  - 결과: 기존 operational WaveClip directive의 `Profile` 참조를 모두 채웠고, Source 전용 `Emission/Sampling`은 wrapper 책임으로 유지했다.
  - 결과: 기존 operational `HazardEmitterEmissionProfileSO` 5개는 wrapper asset으로 유지하되 내부 common grammar source를 `EmissionProfileSO` 참조로 이동했다.
  - 결과: `bd_sample_bubble` 계열 secondary spawn sample을 `ep_sample_bubble_parent` / `ep_sample_bubble_fragments` profile 쌍으로 변환했고, parent의 `MotionCompleted` trigger가 fragments profile을 참조한다.
  - 결과: profile asset에는 `BulletDefinitionSO`의 speed/lifetime/movement 값을 profile override로 복사해 operational authoring SSOT가 profile 쪽으로 이동했다.
  - 결과: `BulletDefinitionSO`의 deprecated movement/reaction 필드는 runtime compatibility fallback으로 유지했다. 실제 `DiscreteEmitRequestBuffer` 확장과 spawned bullet apply override는 T6 후속 runtime 구현 범위로 남겼다.
  - 결과: `ContentValidationRunner`가 `EmissionProfileSO`와 `HazardEmitterEmissionProfileSO`를 수집하도록 확장했다.
  - 결과: `ContentValidationRules`에 operational WaveClip/Hazard wrapper의 profile 참조 필수 규칙, profile bullet/trigger reference integrity, trigger graph cycle/depth 검증을 추가했다.
  - 결과: `WaveClipManagedReferenceGraphUtility.CloneDirective`가 `Profile` 참조를 보존하도록 수정했다.
  - 검증: compile 후 `Assets/_Project` 경로 기준 Unity Console error 0건.
  - 검증: 관련 EditMode 테스트 64개 통과.
  - 검증: 전체 EditMode 486개 통과.
  - 검증: `ContentValidationRunner.ValidateProjectAssets()` 결과 error 0건.
  - 검증: operational WaveClip/Hazard wrapper의 missing `Profile` 참조 0건.
  - 검증: `BulletPlayModeSmokeTests.PlayMode_DedicatedScene_PipelineBootAndCoreLoop_RunWithoutHardErrors` 통과.
  - 참고: `ContentValidationRunner.ValidateProjectAssets()` warning 2건은 기존 콘텐츠 범위의 `CVW040` unreachable tail burst, `STG018` Stage 3 PlayerStart/DepositRegion overlap이며 T8 migration error는 아니다.
- [x] T7. 전면 참조형 authoring schema/resolver 구현
  - 결과: `Assets/_Project/02_Scripts/ECS/Authoring/EmissionProfileSO.cs`를 추가했다.
  - 결과: `Assets/_Project/02_Scripts/ECS/Authoring/EmissionProfileResolver.cs`에 `ResolvedEmissionCore`와 공통 resolver를 추가했다.
  - 결과: `WaveSpawnEntryAuthoring.Profile` optional 참조를 추가하고, profile 참조가 있으면 common grammar를 profile에서 우선 resolve하도록 연결했다.
  - 결과: `HazardEmitterEmissionProfileSO.Profile` optional 참조를 추가하고, profile 참조가 있으면 기존 Hazard profile inline grammar보다 우선 resolve하도록 연결했다.
  - 결과: 기존 inline common grammar는 compatibility source로 유지했다.
  - 결과: Source/Hazard resolved snapshot이 `ResolvedEmissionCore`를 포함한다.
  - 결과: `EmissionProfileResolverTests`를 추가해 profile 우선 resolve와 inline compatibility resolve를 검증했다.
  - 검증: compile 후 `Assets/_Project` 경로 기준 Unity Console error 0건.
  - 검증: 신규 `EmissionProfileResolverTests` 3개 통과.
  - 검증: 전체 EditMode 482개 통과.
  - 검증: `BulletPlayModeSmokeTests.PlayMode_DedicatedScene_PipelineBootAndCoreLoop_RunWithoutHardErrors` 통과.
  - 참고: 전체 EditMode 1차 실행에서 `WaveClip_WithNonPositiveDefinitionId_IsError` 회귀를 발견했으나 Source inline path의 DefinitionId 검사를 validation layer에 남기도록 수정했고, 이후 전체 EditMode가 통과했다.
  - 제외: 실제 asset migration, runtime `DiscreteEmitRequestBuffer` 확장, spawned bullet apply override 구현.
- [x] T6. runtime pipeline 계층 설계 점검
  - 결과: `Docs/TechnicalDesign/TD-033-emission-profile-common-schema.md`의 `### 9.8 T6 runtime pipeline decision`에 A안을 채택해 반영했다.
  - 결과: `DiscreteEmitRequestBuffer`를 신규 `EmissionProfile` discrete execution의 주 채널로 확장하는 방향을 채택했다.
  - 결과: `MotionCompleted -> TriggerEmissionProfile`은 별도 `TriggeredEmissionRequestBuffer`가 아니라 profile-resolved `DiscreteEmitRequestBuffer` append로 실행한다.
  - 결과: `BulletSecondarySpawnRequestBuffer`는 legacy compatibility path로 유지하고 신규 SSOT로 확장하지 않는다고 정리했다.
  - 결과: `SpawnRequestCommonUtility.ApplySpawnedBulletState`는 profile-resolved speed/lifetime/movement/lifecycle tuning을 받을 수 있는 apply contract로 확장해야 한다고 정리했다.
  - 결과: Source sustain branch는 즉시 discrete channel로 완전 흡수하지 않고, 같은 profile-resolved apply contract를 공유하는 후속 구현 기준으로 둔다.
  - 결과: legacy `SpawnRadius` 보존은 triggered request context가 아니라 `PositionPattern` 확장 후보로 정리했다.
  - 제외: 실제 request buffer/component/schema 코드 수정, `EmissionProfileSO` 구현, spawned bullet apply 구현, asset migration.
- [x] T5. 기존 샘플 데이터 변환 후보 작성
  - 결과: `Docs/TechnicalDesign/TD-033-emission-profile-common-schema.md`의 `### 9.7 bd_sample_bubble conversion candidate`에 변환 후보를 추가했다.
  - 결과: `bd_sample_bubble`은 `ep_sample_bubble_parent`로, `bd_sample_bubble_fragment`는 `ep_sample_bubble_fragments`로 분리하는 예시를 문서화했다.
  - 결과: `Speed`, `Lifetime`, `DampedLinear`, `MotionCompleted`, `SpawnCount`, `SpreadAngleDeg`의 대응 위치를 정리했다.
  - 결과: `DefinitionId`, prefab/visual identity, pool size, capture rule, baseline radius, score value는 `BulletDefinitionSO` 책임으로 유지한다고 정리했다.
  - 결과: 기존 `OnMotionCompletedExplode.SpawnRadius = 0.08`은 1차 변환에서 완전 보존하지 않고 T6/T7 판단 대상으로 남겼다.
  - 제외: 실제 `EmissionProfileSO` asset 생성, 기존 asset migration, exact tuning snapshot test 작성.
- [x] T4. `BulletDefinitionSO` deprecated 필드 마이그레이션 기준 작성
  - 결과: `Docs/TechnicalDesign/TD-033-emission-profile-common-schema.md`의 `## 7. BulletDefinitionSO deprecated 정책`에 Phase 0/1/2 마이그레이션 기준을 추가했다.
  - 결과: Phase 0에서는 기존 `BulletDefinitionSO` schema validation과 pool/bootstrap fallback source를 유지한다.
  - 결과: Phase 1에서는 `EmissionProfileSO` 값이 있으면 profile 값을 우선하고, 없으면 `BulletDefinitionSO` 값을 fallback으로 읽는다.
  - 결과: Phase 2 migration 완료 후에는 operational asset에서 deprecated field가 gameplay SSOT로 사용되는 상태를 error 후보로 전환한다.
  - 결과: `Speed`, `Lifetime`, `MovementFamily`, `DampedLinear`, `HomingLite`, `OnMotionCompletedExplode`, `OnCleanupRemovedSpawnSecondary`의 신규 위치와 migration 기준을 필드별로 정리했다.
  - 결과: test-only asset과 compatibility fixture는 deprecated field 사용을 허용한다.
  - 제외: code validation 변경, `BulletPoolDefinitionBuffer` 변경, actual asset migration.
- [x] T3. `MotionCompleted -> TriggerEmissionProfile` validation rule 설계
  - 결과: `Docs/TechnicalDesign/TD-033-emission-profile-common-schema.md`의 `## 8. Validation Rules`에 1차 validation 기준을 구체화했다.
  - 결과: null target, self/direct/transitive cycle, `MaxTriggerDepth = 4` 초과는 error로 둔다.
  - 결과: `MotionCompleted` trigger가 있으나 movement family가 motion completion request를 만들 수 없는 경우는 warning으로 둔다.
  - 결과: operational asset이 test-only trigger profile 또는 test-only bullet definition을 참조하면 error로 둔다.
  - 결과: test-only asset이 operational profile/bullet definition을 참조하는 것은 허용한다.
  - 결과: 신규 `MotionCompleted -> TriggerEmissionProfile`과 기존 `BulletDefinitionSO.OnMotionCompletedExplode`가 migration 기간에 같은 logical emission path에서 공존하면 warning으로 두고, migration 완료 후 error 후보로 전환한다.
  - 결과: `OnCleanupRemovedSpawnSecondary`는 T3 범위 밖 후속 event migration 후보로 분류했다.
  - 제외: 실제 `ContentValidationRules` 구현, runtime frame budget/backlog policy, asset migration.
- [x] T2b. `WaveSpawnEntryAuthoring`와 `HazardEmitterEmissionProfileSO` 통합 전략 결정
  - 결과: 최종 목표는 마이그레이션 포함 전면 참조형 `EmissionProfileSO` 전환으로 확정했다.
  - 결과: 전환형 하이브리드는 최종안이 아니라 staged migration 전략으로 채택했다.
  - 결과: `ResolvedEmissionCore`와 공통 resolver를 먼저 도입하고, Source/Hazard/Triggered wrapper는 이를 감싸는 방향으로 정리한다.
  - 결과: transition 기간 동안 `WaveSpawnEntryAuthoring` inline common grammar와 `HazardEmitterEmissionProfileSO`는 compatibility source로 유지할 수 있으나, 신규 작성 기준은 `EmissionProfileSO` 참조형으로 이동한다.
  - 제외: request buffer/channel 수정, spawned bullet apply 단계 변경, 실제 asset migration.
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
- 결과: T1~T15 완료. `EmissionProfileSO` authoring schema, resolver, operational asset migration, reference integrity validation, profile SpawnTuning/MovementTuning runtime 적용, `MotionCompleted`/`CleanupRemoved -> TriggerEmissionProfile` registry runtime path, legacy secondary spawn 제거, ProducerKind별 DiscreteEmit budget/backlog 정책이 반영됐다.
- 남은 리스크: `PlayerHit`, `LifetimeExpired`, `StageBlocked` trigger event는 아직 열지 않았다. ProducerKind별 budget/cap field는 추가됐지만 실제 operational tuning 값은 아직 기본값 0(global fallback)이다.
- 다음 세션 시작점: 남은 lifecycle event 확장 여부, operational ProducerKind별 budget/cap tuning 적용 여부, legacy `SpawnRadius` 보존을 위한 `PositionPattern` 확장 여부 검토.
