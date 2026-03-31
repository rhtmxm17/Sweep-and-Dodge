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
- 이번 세션에서 하지 않을 것: Slice 범위를 벗어나는 reaction/secondary spawn/HomingLite 구현

## Now
- [ ] I5. Slice 5 `OnMotionCompletedExplode` 구현
  - 완료 기준: `MotionCompleted` reaction이 secondary spawn으로 연결된다.
  - 검증: compile / console error 0 / motion-completed explosion EditMode 테스트

## Next
- [ ] I6. Slice 6 `HomingLite` 구현
  - 완료 기준: 플레이어 위치 read-only 기반 steering family가 추가되고 직진 fallback 규칙이 유지된다.
  - 검증: compile / console error 0 / homing steering EditMode 테스트

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

## End of Session
- 결과: Hazard 확장 논의를 기준으로 `Slice 1`, `Slice 2`, `Slice 3`, `Slice 4` 구현과 검증을 마쳤고, 다음 시작점은 `OnMotionCompletedExplode`를 secondary channel에 연결하는 작업이다.
- 남은 리스크: `StageBlocked`/폭발 이벤트 채널 통합 범위와 secondary spawn merge/count attribution 세부 규칙이 아직 남아 있다.
- 다음 세션 시작점: `I5. Slice 5 OnMotionCompletedExplode 구현`
