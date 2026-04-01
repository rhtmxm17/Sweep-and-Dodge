# SESSION-20260330-01

## Metadata
- doc_id: `SESSION-20260330-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-31`
- related_docs:
  - [../TechnicalDesign/TD-027-hazard-bullet-extension-contract.md](../TechnicalDesign/TD-027-hazard-bullet-extension-contract.md)
  - [../TechnicalDesign/TD-003-spawn-directive-model.md](../TechnicalDesign/TD-003-spawn-directive-model.md)
  - [../TechnicalDesign/TD-007-common-combat-event-channel.md](../TechnicalDesign/TD-007-common-combat-event-channel.md)
  - [../TechnicalDesign/TD-018-hazardstack-runtime-contract.md](../TechnicalDesign/TD-018-hazardstack-runtime-contract.md)
  - [../GameDesign/GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [../GameDesign/GD-007-data-driven-bullet-pattern-definition.md](../GameDesign/GD-007-data-driven-bullet-pattern-definition.md)

## Session Goal
- 한 줄 목표: 다양한 Hazard 이동/반응 확장을 기존 bullet pipeline owner를 유지한 채 구현 가능한 작업 흐름으로 분해한다.
- 완료 기준: movement/reaction/lifecycle 확장 축과 slice별 구현이 문서와 작업 보드 기준으로 흔들리지 않게 유지된다.

## Now
- [ ] I9. `PeriodicTrailEmitter`를 별도 slice로 분리 설계
  - 완료 기준: non-terminal secondary producer의 append 시점, accumulator runtime state, budget/drop 정책을 문서 기준으로 확정한다.
  - 검증: TD 정합성 확인

## Next
- 없음

## Blocked
- 없음

## Parking Lot
- [ ] P1. `MotionOutput -> Apply` 2단 구조는 motion family가 더 늘어난 뒤 재평가한다.
  - 근거: 현재는 문서 메모 수준으로 충분하지만, writer 충돌이 반복되면 구조 승격이 필요하다.
- [ ] P2. 폭발/특수 반응을 공통 전투 이벤트 채널(`Hit/Collect/Cleanup`)에 합칠지는 구현 1차 이후 다시 판단한다.
  - 근거: 현재 공통 채널 범위는 `TD-007`에서 좁게 유지되고 있다.
- [ ] P3. `BounceLimited`, `HomingLite`, `WaveOffset` 같은 고급 movement family는 1차 대표 family 구현 이후로 미룬다.
  - 근거: 지금 필요한 것은 확장 축과 owner 정리이지 전체 motion catalog 확정이 아니다.

## Done
- [x] D1. Hazard 확장용 TD 초안을 작성했다.
  - 검증 결과: `TD-027`에 `TypeKey + CaptureRule`만으로는 부족하다는 문제와 `Movement + Reaction + LifecycleReason` 축이 정리되었다.
- [x] D2. motion component를 `family component`와 `modifier component`로 구분했다.
  - 검증 결과: 배타적 selector/data 역할과 modifier의 비배타 역할이 `TD-027`에 명시되었다.
- [x] D3. Simulation 구현 기본안과 확장형 메모를 문서에 남겼다.
  - 검증 결과: `family job + optional modifier` 기본안과 `MotionOutput -> Apply` 확장안이 `TD-027`에 기록되었다.
- [x] D4. T2 lifecycle request 데이터 구조를 구현 착수 가능한 수준으로 구체화했다.
  - 검증 결과: `TD-027`에 `BulletDespawnRequestTag + BulletLifecycleRequestComponent + BulletLifecycleContactComponent` 조합, priority 정책, producer helper, ExecutionEnd consumer owner가 명시되었다.
- [x] D5. T3 movement family 1차 구현 범위를 확정했다.
  - 검증 결과: `TD-027`에 `Linear`, `DampedLinear`, `HomingLite` 1차 세트, `HomingLite` steering 규칙, speed 유지 규칙, 거리 가드, 직진 fallback, 조합 제한이 명시되었다.
- [x] D6. T4 secondary spawn owner와 budget 분리 설계를 확정했다.
  - 검증 결과: `TD-027`에 `BulletSecondarySpawnChannel`, `SecondarySpawnExecutionSystem`, source/reaction budget 분리, pool 공유 원칙, source backlog와 reaction backlog 분리 관측 기준이 명시되었다.
- [x] D7. T5 구현 범위 분해를 완료했다.
  - 검증 결과: `TD-027`에 Slice 1~7 vertical slice 순서, 범위, 완료 기준, 검증 계획이 명시되었다.
- [x] I1. Slice 1 lifecycle request 인프라 구현을 완료했다.
  - 검증 결과: compile 성공 / console error 0 / EditMode 319 passed / PlayMode 38 passed
- [x] I2. Slice 2 `DampedLinear + MotionCompleted` 구현을 완료했다.
  - 검증 결과: `BulletDampedMotionComponent`, optional authoring, linear/damped family simulation 분리, `MotionCompleted` request 생성, damping 전용 EditMode 테스트가 추가되었다.
  - 검증 결과: compile 성공 / console error 0 / EditMode 323 passed / PlayMode 38 passed
- [x] I3. Slice 3 lifecycle reaction consume owner 구현을 완료했다.
  - 검증 결과: `BulletLifecycleReactionExecutionSystem`이 no-op intermediate owner로 추가되었고, `ExecutionEnd` 순서가 `PlayerHazardRiskResolve -> BulletLifecycleReactionExecution -> BulletDespawnExecution -> CombatEventChannelConsume`로 고정되었다.
  - 검증 결과: compile / console error 0 / ExecutionEnd 순서 및 consume 회귀 테스트 통과 / PlayMode smoke 통과
- [x] I4. Slice 4 secondary spawn channel 인프라 구현을 완료했다.
  - 검증 결과: `BulletSecondarySpawnChannelSingletonTag`, `BulletSecondarySpawnRequestBuffer`, `SecondarySpawnPolicyComponent`, `SecondarySpawnBacklogMetricsComponent`, `SecondarySpawnExecutionSystem`이 추가되었고 source spawn과 backlog/metrics가 분리되었다.
  - 검증 결과: `ExecutionBegin` 순서가 `BulletFieldAreaUpdate -> SecondarySpawnExecution -> SpawnRequestRoundRobinExecution`로 고정되었다.
  - 검증 결과: compile 성공 / console error 0 / EditMode 338 passed / PlayMode 38 passed
- [x] I5. Slice 5 `OnMotionCompletedExplode` 구현을 완료했다.
  - 검증 결과: `BulletOnMotionCompletedExplodeReactionComponent`와 optional authoring이 추가되었고, `BulletLifecycleReactionExecutionSystem`이 `MotionCompleted`를 읽어 secondary channel에 explode request를 append하도록 확장되었다.
  - 검증 결과: `DampedLinear -> MotionCompleted -> reaction append -> despawn -> next ExecutionBegin secondary spawn` 경로가 end-to-end로 닫혔다.
  - 검증 결과: compile 성공 / console error 0 / EditMode 341 passed / PlayMode 39 passed
- [x] I6. Slice 6 `HomingLite` 구현을 완료했다.
  - 검증 결과: `BulletHomingLiteMotionComponent`와 optional authoring이 추가되었고, `BulletSimulationSystem`이 `Linear / Damped / HomingLite` 3-family query로 분리되었다.
  - 검증 결과: `HomingLite`는 player 위치를 read-only로 읽어 제한 각속도로만 방향을 보정하고, acquire/min distance 가드 밖에서는 직진 fallback을 유지한다.
  - 검증 결과: compile 성공 / console error 0 / EditMode 348 passed / PlayMode 39 passed
- [x] I7. Slice 7 `OnCollectedSpawnSecondary` 구현을 완료했다.
  - 검증 결과: `BulletOnCollectedSpawnSecondaryReactionComponent`와 optional authoring이 추가되었고, `BulletLifecycleReactionExecutionSystem`이 `VacuumCollected` reason에서만 secondary channel append를 수행하도록 확장되었다.
  - 검증 결과: `CarryFullRemoved`는 collect reaction component가 있어도 no-op로 유지되고, source attribution은 `BulletSourceRefComponent`를 그대로 계승한다.
  - 검증 결과: compile 성공 / console error 0 / EditMode 354 passed / PlayMode 40 passed
- [x] I8. `BulletDefinitionSO` schema uplift를 구현했다.
- [x] I10. DefinitionSO 기반 샘플 bullet asset/prefab 세트를 추가했다.
  - 검증 결과: `LinearHazard`, `Bubble StopBurst`, `BubbleFragment`, `Candy CollectedReward`, `MagicDust`, `HomingHazard` 샘플 definition/prefab/material이 별도 sample 폴더에 추가되었고, 두 entities 씬의 `BulletVisualPrefabAuthoring.Definitions`에 등록되었다.
  - 검증 결과: compile / console error 0 / EditMode / PlayMode smoke 통과
  - 검증 결과: `BulletDefinitionSO`와 `BulletPoolDefinitionBuffer`가 `Linear/DampedLinear/HomingLite` movement와 `OnMotionCompletedExplode/OnCollectedSpawnSecondary` reaction metadata를 정식 필드로 가진다.
  - 검증 결과: bootstrap은 definition buffer 기준으로 sparse movement/reaction component를 pooled bullet에 적용하고, 금지된 optional behavior authoring은 content validation error로 처리된다.
  - 검증 결과: compile 성공 / console error 0 / EditMode/PlayMode smoke 통과
- [x] I8.1. `SecondaryBullet` editor reference 전환을 구현했다.
  - 검증 결과: `BulletDefinitionSO` reaction 입력은 `BulletDefinitionSO SecondaryBullet` 참조를 사용하고, bake 단계가 runtime `SecondaryBulletTypeKey`로 변환한다.
  - 검증 결과: validation은 null/invalid/unknown `SecondaryBullet` 참조를 error로 처리하고, bake/runtime regression은 유지된다.
  - 검증 결과: compile 성공 / console error 0 / EditMode/PlayMode smoke 통과

## End of Session
- 결과: Hazard 확장 논의를 기준으로 `Slice 1`부터 `Slice 7`까지 구현과 검증을 마쳤고, 다음 시작점은 movement profile schema 승격 재평가 또는 non-terminal producer 분리 설계다.
- 남은 리스크: `StageBlocked`/폭발 이벤트 채널 통합 범위와 secondary spawn merge/count attribution 세부 규칙이 아직 남아 있다.
- 다음 세션 시작점: `I10. DefinitionSO 기반 샘플 bullet 추가 범위 확정` 또는 `I9. PeriodicTrailEmitter를 별도 slice로 분리 설계`
