# 데모 플레이어블 완성 계획문서

> OPS-001(단일 런 프로토타입 코어) 이후, 실제 플레이 가능한 데모 버전 완성을 위한 기획/설계/운영 계획

## Metadata
- doc_id: `OPS-002`
- type: `ProjectOps`
- status: `draft`
- last_updated: `2026-03-04`
- related_docs:
  - [OPS-001-prototype-core-capability-priority-matrix.md](./OPS-001-prototype-core-capability-priority-matrix.md)
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [TD-006-run-progress-director-design.md](../TechnicalDesign/TD-006-run-progress-director-design.md)
  - [TD-007-common-combat-event-channel.md](../TechnicalDesign/TD-007-common-combat-event-channel.md)
  - [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](../TechnicalDesign/TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md)
  - [GD-001-campaign-loop-design.md](../GameDesign/GD-001-campaign-loop-design.md)
  - [GD-004-carrybin-load-and-deposit.md](../GameDesign/GD-004-carrybin-load-and-deposit.md)
  - [GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
- related_adr:
  - [ADR-20260227-01-run-progress-director-runtime-ownership-and-pressure-policy.md](../ADR/ADR-20260227-01-run-progress-director-runtime-ownership-and-pressure-policy.md)
  - [ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup.md](../ADR/ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup.md)
  - [ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md](../ADR/ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md)
  - [ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md](../ADR/ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md)

## 1. 문서 목적
- OPS-001에서 확정한 코어 루프를 기반으로, 데모 공개가 가능한 플레이 경험 범위를 계획한다.
- 단일 런 프로토타입 중심 항목 외에 UI/HUD, StageFlow, 결과 화면, 사운드, 피드백 연출을 포함한 후속 작업 우선순위를 정한다.
- GD-008에서 정의한 `Title -> Lobby -> Stage -> Result -> Demo Complete` 외부 흐름을 구현 가능한 작업 단위로 분해한다.
- 1인 개발, 2개월 총 개발 주기에서 남은 기간(후반 2~3주)의 실행 순서와 합격 기준을 고정한다.

## 2. 운영 가정
- 개발 인원: 1인
- 총 개발 기간: 2개월
- 현재 기준일: 2026-03-04
- 목표 결과물: 외부 시연 가능한 단일 데모 빌드(3개 스테이지 + 최소 UI/사운드/결과 흐름)
- 아트 자산 제작은 범위에서 제외한다.
- 단, Animator/파티클/사운드가 연결될 수 있는 시스템 구조와 이벤트 계약은 범위에 포함한다.
- 데모 플레이 방식은 2가지를 모두 지원한다.
  - 순차 플레이: `Stage1 -> Stage2 -> Stage3 -> Demo Complete`
  - 단일 반복 플레이: 로비에서 선택한 스테이지 `Retry` 반복 후 로비 복귀

## 3. 데모 완료 정의 (Demo DoD)
- 플레이어가 `Title -> Lobby -> Stage Play -> Stage Result` 기본 루프를 끊김 없이 수행할 수 있다.
- `Stage3 Result`의 `Next Stage`가 `Demo Complete`로 전이되고, `Restart Demo/Return to Lobby/Quit` 동작이 일관된다.
- 런타임 HUD가 최소 핵심 정보를 제공한다.
- 전투 이벤트(`Hit/Collect/Cleanup`)가 UI/피드백/통계에 일관되게 반영된다.
- 사운드(BGM/SFX/UI) 라우팅과 볼륨 옵션이 동작한다.
- 작업 완료 루틴(`compile -> console error 0 -> EditMode -> PlayMode 전용 스모크`)을 통과한다.
- 운영 씬 PlayMode 스모크를 정기 실행해 기동/루프 회귀가 없다.

## 4. 작업 스트림 (후반 집중 범위)
| ID | 스트림 | 목표 | 현재 상태 | 예상 공수(일) | 우선순위 |
|---|---|---|---|---:|---|
| S1 | Demo Shell Flow 연동 | 임시 호출자 제거 + `Title/Lobby/Result/Demo Complete` 전이 플로우 확정 | DONE | 2.5 | P0 |
| S2 | 플레이 HUD | 디버그 HUD와 별개로 플레이어 HUD 최소 세트 확정 | TODO | 1.5 | P0 |
| S3 | 결과/실패 UX | Clear/Fail 판정, `Next/Retry/Return`, `Demo Complete` 요약 지표/동선 확정 | TODO | 2.0 | P0 |
| S4 | 이벤트 피드백 브리지 | `PlayerUiFeedback`/`Impulse`를 실제 UI/Animator/VFX 트리거로 연결 | WIP | 2.0 | P0 |
| S5 | 사운드 시스템 | BGM/SFX/UI 버스, 이벤트 라우팅, 볼륨 옵션 설계 | TODO | 2.0 | P0 |
| S6 | 온보딩/가이드 | 첫 진입 30~60초 조작/목표 안내 설계 | TODO | 1.0 | P1 |
| S7 | 데모 운영/릴리즈 게이트 | 빌드 체크리스트, 시연 모드, QA 체크리스트 확정 | TODO | 1.0 | P0 |

## 5. 우선순위 백로그 (세부)
1. `P0` Demo Shell Flow 실연동 계약
- `RunDirectorStageTempFlowDriver` 제거 + `DemoShellFlowController`/`DemoShellSessionStaging` 도입
- `Title -> Lobby -> Stage Play -> Stage Result -> Demo Complete` 전이 책임을 Demo Shell Owner로 고정
- `Stage3`에서 `Next Stage` 선택 시 `Demo Complete` 전이 규칙 구현 완료
- Stage 시작 기본 모드 운영 기준 확정
  - 운영 씬(`SampleScene`): `InitialStageState = Idle`
  - 전용 스모크 씬(`PlayModeSmoke_Dedicated`): 전용 SubScene 분리 + `InitialStageState = Running`

2. `P0` 플레이 HUD 최소 사양
- 필수 표시: CarryBin Load/Capacity, Source 진행 상태, 위험 피격 피드백, 스테이지 상태
- 표시 갱신 책임: ECS writer 고정 + GO/HUD는 reader-only
- 디버그 HUD와 플레이 HUD 공존 정책(빌드 노출 범위 포함)

3. `P0` 결과/실패 루프
- Fail 조건(피격 누적, 타임아웃 등) 확정
- Result 지표 세트(시간, 수집, 정리, 피격, 등급) 확정
- `Next Stage`, `Retry`, `Return to Lobby` 선택지 동작 고정
- `Demo Complete` 요약 지표 범위와 버튼 노출 조건 확정

4. `P0` 피드백 소비자 연결
- `PlayerUiFeedbackConsumeSystem`, `PlayerImpulseConsumeSystem`을 로그 소비에서 표현 소비로 전환
- Animator 파라미터 계약(예: `VacuumActive`, `HitReact`) 확정
- 파티클 트리거 규칙(중복 억제/쿨다운) 확정

5. `P0` 사운드 계약
- 버스 구조: `Master/BGM/SFX/UI`
- 이벤트 매핑: `Hit/Collect/Cleanup/StageState` -> SFX/BGM cue
- 중복 재생 방지와 믹스 정책(쿨다운/보이스 제한/ducking) 확정

6. `P1` 온보딩/가이드
- 첫 시도 실패를 줄이는 최소 가이드 문구/타이밍
- 튜토리얼 모드 분리 여부 대신 인런 힌트 우선 적용 검토

7. `P0` 데모 릴리즈 게이트
- 데모 빌드 옵션(해상도/입력/볼륨 기본값) 고정
- 시연 체크리스트(기동, 완주, 재시작, 옵션 반영, 에러 0) 확정

## 6. 남은 기간 실행안 (2~3주)
1. Week A (P0 집중)
- S1 Demo Shell Flow 연동
- S2 HUD 최소 사양
- S3 결과/실패 루프 1차

2. Week B (피드백/사운드)
- S4 이벤트 피드백 브리지
- S5 사운드 시스템 1차
- S7 릴리즈 게이트 초안

3. Week C (안정화, 선택)
- S6 온보딩 최소 반영
- PlayMode 운영 씬 정기 스모크 + 회귀 수정
- 데모 빌드 고정 및 문서 정리

## 7. 설계 문서 분해 계획 (권장)
- TD 후속(권장):
  - `TD-010`: Demo Shell Flow/Presentation Contract
  - `TD-011`: Runtime Player HUD Contract
  - `TD-012`: Audio Event Routing And Mixer Policy
- GD 후속(권장):
  - `GD-008`: 데모 화면/전이 흐름 기준 문서로 유지
  - `GD-009`: Demo Onboarding And Result Copy/UX(필요 시 분리)
- ADR 기록 기준:
  - Writer/Owner 변경, 업데이트 순서 변경, 공통 이벤트 계약 변경 시 ADR 생성
  - 단순 화면 구성/문구/레이아웃 조정은 OPS/TD/GD 내에서 관리

## 8. 리스크와 대응
1. UI/사운드 연동이 로직 writer 경계를 침범할 위험
- 대응: GO/UI/Audio는 reader-only, 로직 write는 ECS owner 시스템으로 제한

2. 임시 StageFlow 경로를 늦게 제거해 실제 데모 루프 검증이 지연될 위험
- 대응: Week A에서 임시 호출자 제거를 P0로 고정

3. 피드백/사운드 이벤트 과다 발생으로 프레임/가독성 저하 위험
- 대응: 이벤트 병합/쿨다운/보이스 제한 정책을 계약화하고 테스트에 반영

4. 데모 막판에 품질 검증 시간이 부족해질 위험
- 대응: Week B부터 릴리즈 게이트를 병행 운영하고, Week C는 회귀/패키징 전용으로 확보

## 9. 검증/합격 기준
- 공통 절차:
  1. `refresh_unity(compile=request, wait_for_ready=true)`
  2. `read_console(action=get, types=["error"], include_stacktrace=true)` 에러 0건
  3. `EditMode` 테스트 통과
  4. `PlayMode` 전용 씬 스모크 통과
- 데모 추가 체크:
  - 순차 플레이: `Title -> Lobby -> Stage1 -> Stage2 -> Stage3 -> Demo Complete -> Lobby`
  - 단일 반복 플레이: `Lobby -> StageN -> Result -> Retry(반복) -> Lobby`
  - `Stage3` 결과의 `Next Stage`가 `Demo Complete`로 정확히 연결
  - `Retry` 즉시 재진입
  - HUD/사운드 옵션 반영 확인
  - 운영 씬 정기 스모크 결과 기록

## 10. 변경 이력
- 2026-03-04: S1 구현 반영. `RunDirectorStageTempFlowDriver` 제거, Demo Shell 전이 계약(`TD-010`) 추가, 운영/테스트 씬 SubScene 분리 기준을 확정했다.
- 2026-03-04: GD-008 반영. 범위를 단일 런 중심에서 `Title/Lobby/3스테이지/Demo Complete` 데모 셸 플로우 기준으로 확장했다.
- 2026-03-04: OPS-002 초안 작성 (데모 플레이 완성 범위: StageFlow/UI, HUD, 결과 UX, 피드백, 사운드, 릴리즈 게이트)
