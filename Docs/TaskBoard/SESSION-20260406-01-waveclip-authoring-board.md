# SESSION-20260406-01

## Metadata
- doc_id: `SESSION-20260406-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-04-06`
- related_docs:
  - [../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md](../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md)
  - [../TechnicalDesign/TD-003-spawn-directive-model.md](../TechnicalDesign/TD-003-spawn-directive-model.md)
  - [../TechnicalDesign/TD-005-spawn-directive-settings-reference.md](../TechnicalDesign/TD-005-spawn-directive-settings-reference.md)
  - [../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](../ADR/ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md)
  - [../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](../ADR/ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md)

## Session Goal
- 한 줄 목표: `WaveSpawnEntryAuthoring`의 authoring 축을 재정의해 `의미가 섞인 필드/타입`을 분리하고, 이후 구현 세션에서 사용할 decision-complete 기준을 고정한다.
- 완료 기준:
  - `Emission`, `Sampling`, `Direction` 현행 의미와 runtime 소비 규칙을 코드 기준으로 설명할 수 있다.
  - `PositionPattern` 분리와 `EventRepeatCount` 대체를 반영한 새 authoring 축을 고정한다.
  - 플레이어 방향 의존 `Aim`을 파이프라인에 수용하는 기준 입력과 runtime 참조 경로를 고정한다.
  - `EventRepeatCount`, `PlayerPositionAim`, `PositionPattern.LineEven`, runtime flatten naming에 대한 권장 기본값이 채택 상태로 고정된다.
  - event 내부 shot과 sampling 고정 규칙에 대해 채택 invariant를 명시한다.
  - `WaveSpawnEntryAuthoring` authoring 축 재구성안이 implementation-ready 상태로 고정된다.
  - 이후 세션에서 `WaveClipSO` schema 실제 수정, bake/validation 재구성, runtime hot path 구조 변경 여부 판단 및 필요 시 구현까지 이어질 수 있도록 단계별 작업 기준을 남긴다.
- 이번 세션에서 하지 않을 것:
  - inspector/polish 구현
  - asset migration 실행

## Now
- 없음

## Next
- [ ] T6. Plan E. 구현 착수 순서를 고정한다.
  - 완료 기준:
    - `Authoring -> Resolver -> Runtime -> Validation/Migration -> Verification` 순으로 착수 순서가 고정된다.
    - 각 단계의 소유 파일/타입과 중단 보고 조건이 정리된다.
  - 검증: 다음 구현 요청 시 바로 작업을 시작할 수 있다.
  - 근거:
    - 현재는 설계 채택안이 충분히 쌓였고, 남은 것은 실행 단위로 끊는 일이다.

## Blocked
- 없음

## Parking Lot
- [ ] P1. `SerializeReference` editor UX 세부 polish는 authoring 축 재구성 이후에 다시 본다.
  - 근거: 현재 핵심 문제는 표현 UX보다 authoring 의미 체계 자체의 혼선이다.
- [ ] P2. runtime flat shape rename 여부는 authoring 의미 재정의가 끝난 뒤 판단한다.
  - 근거: 지금 단계에서 runtime 계약 이름까지 함께 흔들면 회귀 범위가 커진다.

## Done
- [x] D1. `DirectionAuthoring`와 `EventShotSchedule`의 실제 runtime 결합 방식을 확인했다.
  - 검증 결과:
    - `Direction`은 각도 계산 규칙을, `EventShotSchedule`은 event 내부 shot 소비 타이밍을 결정한다.
    - `NWay`는 세트 atomic consume, `RadialBurst`는 `BurstShotsPerEvent` 기반 radial slot 순환, `Spiral`은 sequence 기반 각도 진행으로 확인됐다.
- [x] D2. `BurstShotsPerEvent`의 현행 의미와 혼선 지점을 확인했다.
  - 검증 결과:
    - `Poisson/EventBurst`에서는 event당 shot 수이고, `RadialBurst`에서는 radial slot count까지 겸한다.
    - `NWay`와 조합될 때는 `NWayCount`와 의미가 겹쳐 authoring UX가 나빠진다.
- [x] D3. sampling fixation이 현재 부분 적용 상태임을 확인했다.
  - 검증 결과:
    - `Timed + UniformField/PollutionTopK`만 event anchor position을 고정한다.
    - `LineEven/PointSet`은 동일 event 안에서도 shot sequence에 따라 위치가 다시 계산된다.
- [x] D4. `PositionPattern`을 `Sampling`과 별도 축으로 분리하는 권장안을 채택했다.
  - 검증 결과:
    - `LineEven`과 `PointSet`을 event anchor sampling이 아니라 shot-local position pattern으로 재분류하는 방향이 합의됐다.
    - `디자인상 의미를 갖는 직선상의 등간격 좌표에서 수직 발사` 같은 조합도 `PositionPattern + Aim` 조합으로 유지 가능하다는 점을 확인했다.
- [x] D5. `BurstShotsPerEvent`를 폐기하고 `EventRepeatCount`로 대체하는 방향을 채택했다.
  - 검증 결과:
    - `ShotPattern` 1회당 탄 수와 `event 내부 반복 횟수`를 분리하는 방향이 합의됐다.
    - 새 해석은 `총 탄 수 = ShotPattern 1회당 탄 수 × EventRepeatCount` 기준으로 설명 가능하다.
- [x] D6. 플레이어 방향 의존 `Aim`을 파이프라인에 미리 수용하는 방향을 채택했다.
  - 검증 결과:
    - 플레이어 입력 파이프라인에는 `AimWorldXZ/HasAimWorldPoint`와 player transform rotation, cleanup 전용 `LockedFacingXZ`가 이미 존재한다는 점을 확인했다.
    - WaveClip의 player-dependent aim은 별도 ad-hoc 입력이 아니라 fixed-step player snapshot 계약으로 수용하는 방향이 합의됐다.
- [x] D7. 구현 착수 전 권장 기본값 6개를 채택했다.
  - 검증 결과:
    - `EventRepeatCount`는 `Poisson` / `EventBurst` 전용 필드로 둔다.
    - `PlayerPositionAim`은 1차에서 `EventStart` snapshot timing만 지원한다.
    - `PlayerPositionAim` snapshot은 request/event-local state에 저장하고 consume 시 재계산하지 않는다.
    - `PositionPattern.LineEven`은 1차에서 `SampleSpacing` 기준만 유지한다.
    - `Aim.LineNormal`은 이번 단계에 포함하지 않고 후속 확장으로 미룬다.
    - runtime flat shape rename은 이번 단계에서 수행하지 않고 compat 이름을 한 단계 더 유지한다.
- [x] D8. Plan A. `WaveClipSO` authoring contract를 새 축 기준으로 재구성했다.
  - 검증 결과:
    - `WaveSpawnEntryAuthoring`는 `Payload / Emission / Sampling / PositionPattern / Aim / ShotPattern` 6축으로 재구성됐다.
    - `WaveSamplingAuthoring`는 `Anchor + AreaSampler + SpawnSampleBudget + PlayerNoSpawnRadius` 컨테이너로 분리됐다.
    - `BurstShotsPerEvent`는 authoring schema에서 제거되고 `EventRepeatCount`로 대체됐다.
    - `PointSet`, `LineEven`, `Random/Fixed/Spiral/PlayerPosition`, `Single/NWay/Radial` subtype 트리와 SerializeReference drawer가 구현됐다.
- [x] D9. Plan B. resolver/validation contract를 새 축 기준으로 재정의하고 검증했다.
  - 검증 결과:
    - `ResolvedWaveSpawnDirectiveSnapshot`가 canonical snapshot으로 교체됐다.
    - validation은 `CV040` 구조 검증과 semantic 검증(`CV018`, `CV022`, `CV023`, `CV024`, `CV026`, `CV028`, `CV041`, `CVW032`, `CVW033`)으로 재배치됐다.
    - 운영 6개 + test 2개 WaveClip asset이 새 authoring schema로 마이그레이션됐다.
    - `compile -> console error 0 -> EditMode 404/404 -> PlayMode smoke 1/1`까지 통과했다.
- [x] D10. Plan C. runtime request/event-local snapshot ownership을 canonical runtime 기준으로 고정했다.
  - 검증 결과:
    - runtime SSOT가 `ResolvedWaveSpawnDirectiveSnapshot -> SourceClipPatternBuffer -> SourceSpawnRequestBuffer` 흐름으로 재정렬됐다.
    - `SourceClipPatternBuffer`와 `SourceSpawnRequestBuffer`에 canonical runtime 필드(`SamplingAnchorMode`, `AreaSamplerMode`, `PositionPatternMode`, `AimMode`, `AimSnapshotTiming`, `AimAngleOffsetDeg`, `ShotPatternMode`, `ShotCount`, `EventRepeatCount`)가 추가됐다.
    - `SourceSpawnRequestBuffer`가 `EventAnchorPosition`, `EventAimTargetPosition`, `EventShotElapsedSec`, `SpawnSequence`를 포함한 event-local mutable state의 owner로 정리됐다.
    - `Poisson` / `EventBurst`는 `Instant`와 `Timed` 모두 event 단위 request item을 유지하고 merge하지 않도록 변경됐다.
    - runtime consume는 `Aim + ShotPattern + PositionPattern` canonical field를 읽고, `PlayerPositionAim(EventStart)` snapshot을 request-local state에 고정한다.
    - `compile -> console error 0 -> EditMode 403/403 -> PlayMode smoke 1/1`까지 통과했다.
- [x] D11. Plan D. validation / fixture / document 정합성을 canonical 계약 기준으로 마감했다.
  - 검증 결과:
    - `ContentValidationRules`의 canonical 코드 의미(`CV022`, `CV023`, `CV024`, `CV026`, `CV028`, `CV041`, `CVW032`, `CVW033`)와 테스트 이름/메시지의 stale 용어를 정리했다.
    - sample asset test가 typed-only뿐 아니라 각 directive 축(`Emission`, `Sampling`, `Sampling.Anchor`, `Sampling.AreaSampler`, `PositionPattern`, `Aim`, `ShotPattern`)의 non-null도 확인하도록 강화됐다.
    - request build / stress fixture의 stale `BurstShotsPerEvent` 명칭을 `EventRepeatCount` 중심으로 정리했다.
    - `TD-002`, `TD-003`, `TD-005`를 current canonical contract 기준으로 전면 갱신했다.
    - 운영/test `WaveClip` asset YAML에서 `Directives[]` only와 current managed reference type 사용 상태를 재확인했다.
    - `compile -> console error 0 -> EditMode -> PlayMode smoke` 최종 루프를 다시 통과했다.

## End of Session
- 결과: `WaveSpawnEntryAuthoring` 개편 논의의 핵심 쟁점을 `Sampling`, `PositionPattern`, `Aim`, `ShotPattern`, `EventRepeatCount`, `player-dependent Aim` 축으로 재정리했다.
- 남은 리스크:
  - `PlayerPositionAim`에서 player world position만 볼지, player aim point subtype를 추가할지는 아직 후속 결정이 남아 있다.
  - runtime flat shape rename을 미룬 만큼, authoring 용어와 runtime 용어가 당분간 완전히 일치하지 않을 수 있다.
- 다음 세션 시작점:
  - `Plan E`에서 구현 착수 순서와 남은 compat 제거/후속 범위를 정리한다.
