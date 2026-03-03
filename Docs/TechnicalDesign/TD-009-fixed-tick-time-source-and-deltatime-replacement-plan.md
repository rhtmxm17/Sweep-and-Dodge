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

2. Stage-1: 1차 시스템 치환
- 1차 대상 시스템에서 `SystemAPI.Time.DeltaTime` 직접 참조 제거
- 동일 seed/input 반복 실행 결과 비교 테스트 추가

3. Stage-2: 2차 시스템 치환
- Director/Vacuum/오염도 누적 시간축 통일
- 장시간(3~5분) 누적 drift 검증

4. Stage-3: 도구/운영 정리
- Pause/Step(1 tick) 제어 노출
- 로그/디버그 HUD에 tick 정보 표시

## 테스트 계획
1. `Determinism_FixedTick_SameSeedAndInput_SamePlayerTrack`
2. `Determinism_FixedTick_SameSeedAndInput_SameBulletSnapshot`
3. `Replay_PauseAndSingleStep_AdvancesExactlyOneTick`
4. 기존 EditMode/PlayMode 스모크 회귀

## 리스크와 대응
1. 시간축 혼재(일부 시스템만 치환)
- 대응: 단계별 완료 기준에 "직접 DeltaTime 참조 0건" 체크 추가

2. 누적기/서브스텝 과실행
- 대응: 프레임당 최대 step cap 및 경고 로그 추가

3. 표현 계층과 로직 체감 불일치
- 대응: 표현 계층은 보간으로 유지하고 로직 검증은 tick 기준으로 수행
