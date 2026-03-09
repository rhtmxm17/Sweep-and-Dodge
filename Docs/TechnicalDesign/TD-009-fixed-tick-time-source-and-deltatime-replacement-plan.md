# TD-009 Fixed Tick Time Source And DeltaTime Replacement Plan

## 목적
- 리플레이/결정론 품질을 위해 로직 시간원을 고정 Tick으로 통일한다.
- `SystemAPI.Time.DeltaTime` 직접 의존 경로를 단계적으로 치환한다.

## 범위
- 포함:
  - 고정 Tick 시간원 데이터 모델
  - 로직 파이프라인 Tick 실행 규칙
  - DeltaTime 의존 시스템 치환 우선순위
  - 검증 시나리오(동일성/회귀)
- 제외:
  - 표현 계층(카메라 damping, Animator 블렌딩)의 고정 Tick 강제
  - 네트워크 동기화 모델

## 설계 개요
1. 시간원 싱글톤 도입
- 예시: `FixedTickTimeComponent`
  - `uint Tick`
  - `float FixedDelta` (예: `1/60`)
  - `float Accumulator`
  - `byte PauseRequested`

2. Tick 구동 규칙
- 로직 파이프라인은 Tick 단위로 실행한다.
- 렌더 프레임에서 누적기가 충분할 때만 Tick을 진행한다.
- `BulletFrameCounter`는 Tick 실행 시점에만 증가한다.

3. 리플레이 규칙
- 입력은 Tick 인덱스 기준으로 기록/재생한다.
- 재생 모드에서 로직 시간은 고정 Tick만 사용한다.
- 디버그 모드에서 `Pause/Step(1 tick)` 실행을 허용한다.

## 선택 방안(확정)
1. Tick 실행 경계
- `FixedTickRootGroup`를 신설한다.
- `PlayerFixedStepGroup`와 `BulletFramePipelineGroup`를 이 그룹 하위에 둔다.
- `PlayerFixedStepGroup`에서 입력 적용, 플레이어 이동, 1회성 액션 consume, replay 기록을 처리한다.
- `BulletFramePipelineGroup`에서 탄환 시뮬레이션, 스폰/요청 핵심 경로를 처리한다.
- 기존 `Initialization/Simulation` 경계는 표현 계층과 로직 계층 분리에 맞춰 정리한다.

2. 입력 수집/소비
- 1차: GO/브리지가 `PlayerInputIntentComponent`에 쓴 입력을 tick 소비 큐에 적재하고 tick 루프에서 순서대로 consume한다.
- replay 적용은 `ReplayTickInputApplySystem`, 기록은 `ReplayTickRecordSystem`으로 분리한다.
- 2차: 필요 시 InputSystem `Manual Update` 전환으로 tick 경계 일치를 강화한다.

3. 과부하(누적기) 처리
- `MaxSubSteps` 제한 + `Accumulator Clamp` 조합을 사용한다.
- 리플레이/결정론 검증 모드 기본값은 `MaxSubSteps=1`로 고정한다.
- 일반 플레이 모드는 성능/체감 기준으로 cap 값을 완화해 운영한다.

## 치환 우선순위
1. 1차(결정론 핵심)
- `PlayerIntentMovementSystem`
- `BulletSimulationSystems`
- `SpawnRequestSystems`
- `SpawnRequestCommonUtility`
- `SourceClipRequestSystems`

2. 2차(상태 누적/연동)
- `BulletVacuumRequestSystem`
- `RunProgressDirectorSystems`
- `SourcePollutionUpdateSystem`
- 기타 DeltaTime 직접 참조 시스템

## 단계 계획
1. Stage-0: 시간원 골격 추가
- `FixedTickTimeComponent`, 시간 유틸 API 추가
- 기존 경로와 공존(기능 토글 가능) 상태로 시작
- `FixedTickStepRuntimeComponent.CurrentLogicFrame`을 추가해 현재 logic step의 authoritative tick을 publish한다.

2. Stage-1: 1차 시스템 치환
- 1차 대상 시스템에서 `SystemAPI.Time.DeltaTime` 직접 참조 제거
- `PlayerIntentMovementSystem`을 `PlayerFixedStepGroup`으로 이관한다.
- `ReplayInputSyncSystem`을 `ReplayTickInputApplySystem` / `ReplayTickRecordSystem`으로 분리한다.
- `PlayerIntentConsumeSystem`을 추가해 vacuum/action one-shot consume을 fixed-tick 경계로 이동한다.
- 동일 seed/input 반복 실행 결과 비교 테스트 추가

3. Stage-2: 2차 시스템 치환
- Director/Vacuum/오염도 누적 시간축 통일
- 장시간(3~5분) 누적 drift 검증

4. Stage-3: 도구/운영 정리
- Pause/Step(1 tick) 제어 노출
- 로그/디버그 HUD에 tick 정보 표시

5. Stage-4: 검증 자동화
- 고정 Tick 구현 검증 단계에 `DeltaTime` 직접 사용 금지 검사를 추가한다.
- 기본 규칙: 로직 시스템(`Assets/_Project/02_Scripts/ECS/Systems`)에서
  - `SystemAPI.Time.DeltaTime` 금지
  - `Time.deltaTime` 금지
- 예외(화이트리스트): 표현 계층(HUD/카메라 등)만 허용
- CI 또는 로컬 검증 스크립트에서 정규식 검사로 fail-fast 처리

## 테스트 계획
1. `Determinism_FixedTick_SameSeedAndInput_SamePlayerTrack`
2. `Determinism_FixedTick_SameSeedAndInput_SameBulletSnapshot`
3. `Replay_PauseAndSingleStep_AdvancesExactlyOneTick`
4. 기존 EditMode/PlayMode 스모크 회귀
5. `DeltaTimeBan_NoUsageInLogicSystems`

## 리스크와 대응
1. 시간축 혼재(일부 시스템만 치환)
- 대응: 단계별 완료 기준에 "직접 DeltaTime 참조 0건" 체크 추가

2. 누적기/서브스텝 과실행
- 대응: 프레임당 최대 step cap 및 경고 로그 추가

3. 표현 계층과 로직 체감 불일치
- 대응: 표현 계층은 보간으로 유지하고 로직 검증은 tick 기준으로 수행

## 관련 문서
- [ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md](../ADR/ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md)
- [TD-008-replay-io-persistence-and-version-policy.md](TD-008-replay-io-persistence-and-version-policy.md)
