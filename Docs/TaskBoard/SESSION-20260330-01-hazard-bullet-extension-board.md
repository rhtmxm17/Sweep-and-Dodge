# SESSION-20260330-01

## Metadata
- doc_id: `SESSION-20260330-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-30`
- related_docs:
  - [../TechnicalDesign/TD-027-hazard-bullet-extension-contract.md](../TechnicalDesign/TD-027-hazard-bullet-extension-contract.md)
  - [../TechnicalDesign/TD-003-spawn-directive-model.md](../TechnicalDesign/TD-003-spawn-directive-model.md)
  - [../TechnicalDesign/TD-007-common-combat-event-channel.md](../TechnicalDesign/TD-007-common-combat-event-channel.md)
  - [../TechnicalDesign/TD-018-hazardstack-runtime-contract.md](../TechnicalDesign/TD-018-hazardstack-runtime-contract.md)
  - [../GameDesign/GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [../GameDesign/GD-007-data-driven-bullet-pattern-definition.md](../GameDesign/GD-007-data-driven-bullet-pattern-definition.md)

## Session Goal
- 한 줄 목표: 다양한 Hazard 이동/반응 확장을 기존 bullet pipeline owner를 유지한 채 구현 가능한 작업 흐름으로 분해한다.
- 완료 기준: movement/reaction/lifecycle 확장 축, 구현 기본안, 다음 구현 순서가 문서와 작업 보드 기준으로 흔들리지 않게 정리된다.
- 이번 세션에서 하지 않을 것: 실제 ECS 코드 구현, prefab/authoring 마이그레이션, compile/EditMode/PlayMode 검증

## Now
- [ ] T1. `TD-027`을 구현 착수 가능한 기준선 문서로 유지한다.
  - 목적: 구현 전에 movement family, modifier, lifecycle reason, secondary spawn owner 용어를 고정한다.
  - 완료 기준: 후속 구현이 `family component + modifier component` 구조와 current pipeline owner를 기준으로 진행될 수 있다.
  - 검증: `TD-027`에 family/modifier 구분, Simulation 기본안, 확장형 `MotionOutput -> Apply` 메모, T2/T3/T4/T5 계약이 반영되어 있다.
  - 근거: 현재 확장 논의는 이동 수식보다 owner 경계와 데이터 축 고정이 먼저다.

## Next
- [ ] I1. Slice 1 lifecycle request 인프라 구현
  - 완료 기준: `BulletLifecycleRequestComponent`, `BulletLifecycleContactComponent`, helper, 초기화 경로가 추가된다.
  - 검증: compile / console error 0 / helper priority EditMode 테스트
- [ ] I2. Slice 2 `DampedLinear + MotionCompleted` 구현
  - 완료 기준: damping family와 `MotionCompleted` request 생성이 붙는다.
  - 검증: compile / console error 0 / damping motion EditMode 테스트
- [ ] I3. Slice 3 lifecycle reaction consume owner 구현
  - 완료 기준: `BulletLifecycleReactionExecutionSystem`이 `BulletDespawnExecutionSystem` 앞에서 consume를 중계한다.
  - 검증: compile / console error 0 / ExecutionEnd 순서 EditMode 테스트

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

## End of Session
- 결과: Hazard 확장 논의가 구현 전에 흔들리지 않도록 TD 기준선과 구현 후보 흐름을 작업 보드로 고정했다.
- 남은 리스크: `StageBlocked`/폭발 이벤트 채널 통합 범위와 secondary spawn merge/count attribution 세부 규칙이 아직 남아 있다.
- 다음 세션 시작점: `I1. Slice 1 lifecycle request 인프라 구현`
