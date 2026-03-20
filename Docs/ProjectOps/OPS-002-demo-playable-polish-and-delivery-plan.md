# 데모 플레이어블 완성 계획문서

> OPS-001(단일 런 프로토타입 코어) 이후, 실제 플레이 가능한 데모 버전 완성을 위한 기획/설계/운영 계획

## Metadata
- doc_id: `OPS-002`
- type: `ProjectOps`
- status: `draft`
- last_updated: `2026-03-20`
- related_docs:
  - [OPS-001-prototype-core-capability-priority-matrix.md](./OPS-001-prototype-core-capability-priority-matrix.md)
  - [OPS-003-public-release-readiness-plan.md](./OPS-003-public-release-readiness-plan.md)
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [TD-006-run-progress-director-design.md](../TechnicalDesign/TD-006-run-progress-director-design.md)
  - [TD-007-common-combat-event-channel.md](../TechnicalDesign/TD-007-common-combat-event-channel.md)
  - [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](../TechnicalDesign/TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-011-runtime-player-hud-contract.md](../TechnicalDesign/TD-011-runtime-player-hud-contract.md)
  - [TD-013-player-feedback-presentation-bridge-contract.md](../TechnicalDesign/TD-013-player-feedback-presentation-bridge-contract.md)
  - [TD-014-demo-audio-runtime-contract.md](../TechnicalDesign/TD-014-demo-audio-runtime-contract.md)
  - [TD-021-hazardstack-hud-contract.md](../TechnicalDesign/TD-021-hazardstack-hud-contract.md)
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
- 현재 런타임 UI/HUD/옵션 표시는 `OnGUI`와 브리지 중심의 내부 테스트 수준이며, 공개 빌드 전 `출시형 Runtime UI`로 교체가 필요하다.
- 현재 조작 기준은 `키보드 + 마우스`를 우선한다.
- 패드 지원은 공개 채널/시연 방식이 요구할 때 확장하되, 현재 단계에서는 `포커스 기반 UI 구조를 유지해 후속 확장 비용을 낮추는 것`을 기본 목표로 둔다.
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
| S2 | 플레이 HUD | 디버그 HUD와 별개로 플레이어 HUD 최소 세트 확정 | DONE | 1.5 | P0 |
| S3 | 결과/실패 UX | Clear/Fail 판정, `Next/Retry/Return`, `Demo Complete` 요약 지표/동선 확정 | DONE | 2.0 | P0 |
| S4 | 이벤트 피드백 브리지 | `PlayerUiFeedback`/`Impulse`를 실제 UI/Animator/Impulse 표현으로 연결 | DONE | 2.0 | P0 |
| S5 | 사운드 시스템 | BGM/SFX/UI 버스, 이벤트 라우팅, 볼륨 옵션 설계 | DONE | 2.0 | P0 |
| S6 | 온보딩/가이드 | `Stage1` 첫 30~60초에 조작/목표/Deposit 루프를 학습시키는 인런 힌트 설계 | TODO | 1.5 | P0 |
| S7 | 데모 운영/릴리즈 게이트 | 빌드 체크리스트, 시연 모드, QA 체크리스트, 비개발 빌드 합격 기준 확정 | TODO | 1.5 | P0 |
| S8 | Runtime UI 전환 | `OnGUI` 기반 `Title/Lobby/HUD/Result/Options`를 `uGUI` 기반 출시형 UI로 교체 | DONE | 3.0 | P0 |
| S9 | KB+Mouse UX / Input Baseline | 마우스+키보드 기준 메뉴/옵션/조작 UX 정리, 포커스 기반 UI로 후속 패드 확장 비용 절감 | TODO | 2.0 | P0 |
| S10 | 출시형 HUD / 튜토리얼 메시지 | 플레이 정보 구조 재설계 + 문맥형 힌트/실패 학습 루프 연결 | IN_PROGRESS | 2.0 | P0 |
| S11 | VFX 제품화 | 브리지 이벤트를 실제 화면/월드 VFX로 연결하고 전달 우선순위/쿨다운을 고정 | TODO | 2.0 | P0 |
| S12 | 브랜딩 / 패키징 마감 | 아이콘, 버전, 앱 메타, 기본 해상도/윈도우 정책, 비개발 빌드 점검 | TODO | 1.0 | P0 |

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
- 기술 고정: `OnGUI` 기반 플레이 HUD(`PlayerRuntimeHudBridge`)
- 데이터 계약: `PlayerHudSnapshotCollectSystem` 단일 writer + `PlayerHudSnapshotComponent` singleton snapshot
- Source 표시 고정: `Pressure Source progress(Collected/ThresholdDepleted)` + `Depleted/Total`
- Stage 메타 공급: `DemoShellFlowController.CurrentStageId/CurrentScreen` read-only
- Hit 피드백 고정: 짧은 플래시 + 손실값(`0.6s`)
- Debug HUD 노출 고정: `UNITY_EDITOR/DEVELOPMENT_BUILD` 전용

3. `P0` 결과/실패 루프
- Fail 트리거: `Timeout + StagePlay GiveUp`으로 고정
- Timeout 설정: `StageProfile` per-stage(`Stage1=150s`, `Stage2=180s`, `Stage3=210s`)
- Result UX: 단일 `StageResult` 화면 유지, `Fail`에서는 `Retry/Return`만 허용(`Next` 비노출/거부)
- Result 지표: `시간/수집/정리/피격` 코어 세트로 고정
- Demo Complete 요약: 코어 총합(`시간/수집/정리/피격`)만 노출
- Demo Complete 집계: **성공(clear) 시도만 누적**, fail/retry 실패 시도 제외

4. `P0` 피드백 소비자 연결
- `PlayerUiFeedbackConsumeSystem`, `PlayerImpulseConsumeSystem`을 로그 소비에서 표현 소비로 전환
- Animator 파라미터 계약(예: `VacuumActive`, `HitReact`) 확정
- HUD feed/Impulse 오프셋 표현 규칙(중복 억제/쿨다운) 확정

5. `P0` 사운드 계약
- 버스 구조: `Master/BGM/SFX/UI`
- 이벤트 매핑: `Hit/Collect/Cleanup/StageState` -> SFX/BGM cue
- 중복 재생 방지와 믹스 정책(쿨다운/보이스 제한/ducking) 확정
- 운영 기본 세팅: Source 하이브리드(프리와이어 + 누락 자동 생성), fallback clip(누락 슬롯만), DemoShell OnGUI 볼륨 슬라이더 확정

6. `P1` 온보딩/가이드
- 첫 시도 실패를 줄이는 최소 가이드 문구/타이밍
- 튜토리얼 모드 분리 여부 대신 인런 힌트 우선 적용 검토

7. `P0` 데모 릴리즈 게이트
- 데모 빌드 옵션(해상도/입력/볼륨 기본값) 고정
- 시연 체크리스트(기동, 완주, 재시작, 옵션 반영, 에러 0) 확정

8. `P0` Runtime UI 전환
- 현재 `DemoShellFlowController`, `PlayerRuntimeHudBridge`, `DemoAudioBridge`의 `OnGUI` 표시를 출시형 런타임 UI로 교체
- 대상 화면: `Title`, `Lobby`, `Stage HUD`, `Pause`, `Settings`, `Stage Result`, `Demo Complete`, 확인 다이얼로그
- 소유권 원칙 유지: DemoShell/Bridge는 상태/명령 owner, UI는 reader/presenter
- 권장 기본값: `uGUI + EventSystem + InputSystemUIInputModule`
- 세부 구현 순서: `Shell -> Modal -> HUD/Fx` 우선 순으로 전환
- 2026-03-13 구현 반영:
  - `RuntimeUiRoot` 프리팹 + `SampleScene`/`PlayModeSmoke_Dedicated` 씬 고정 인스턴스 배치 완료
  - `Title`, `Lobby`, `Result`, `DemoComplete`, shared `Settings(audio)` 완료
  - `Pause`, `Confirm` modal stack 완료
  - shell/settings/HUD `OnGUI`는 runtime UI 활성 시 비노출 경로로 게이트 완료

9. `P0` KB+Mouse UX / Input Baseline
- 공개 빌드 기준 조작축은 `키보드 + 마우스`
- 메뉴/옵션은 마우스 우선 사용성을 확보하고, 키보드 포커스 이동은 접근성/후속 패드 확장 대비용으로 유지
- 현재 `Input` 직접 참조 경로와 `Input System` UI 경로의 역할을 분리/정리
- 패드 지원은 별도 범위로 분리하되, UI 구조는 포커스/Submit/Cancel 기반으로 설계해 후속 확장을 가능하게 유지

10. `P0` 출시형 HUD / 튜토리얼 메시지
- 테스트용 텍스트 HUD를 `Carry / Source / Timer / Objective / Danger` 중심 정보 구조로 재설계
- 첫 이동, 첫 수집, 첫 Carry 증가, 첫 Deposit 필요, 첫 피격, 첫 실패에 문맥형 힌트 연결
- `Stage1`을 튜토리얼 전용 스테이지가 아니라 인런 학습 구간으로 사용
- 실패 원인과 재시도 행동(`Deposit`, 회피, 정리 우선순위)을 결과/힌트 카피로 연결
- 2026-03-13 구현 반영:
  - HUD V1 완료: `StageLabel`, `Objective`, `SourceProgress`, `Pressure Source progress`, `Carry`, `Timer`, `Danger banner`, `single toast`
  - `Pressure Source`는 pressure 상태 source에 대해서만 노출되며, bar 위에 `Normal -> Weakened` 임계 marker 표시
  - `HazardStack` lane은 `Carry` 인접 보조 레인에 `slot + display` 구조로 고정하고, stage-start `HazardStackMax` 기준 slot 수 / frame height / `brush gold/gray` sprite를 사용한다
  - 미완료: `Stage1` 온보딩 힌트 시퀀스, 실패 학습 카피, 접근성 옵션 연동

11. `P0` VFX 제품화
- 현재 "브리지 이벤트 수신 예정" 수준의 VFX를 실제 전달 수단으로 구현
- 우선순위: `Hit`, `Deposit`, `Cleanup`, `Stage Clear/Fail`, `Timeout Warning`
- 이벤트 중복 억제, 쿨다운, 강도 옵션, 과도한 플래시 방지 규칙을 계약화
- 정보 전달 목적의 최소 제품화를 우선하고, 고급 연출은 후순위로 둔다

12. `P0` 브랜딩 / 패키징 마감
- `PlayerSettings`의 앱 이름/식별자/아이콘/버전/기본 해상도/윈도우 정책을 공개 빌드 기준으로 정리
- `Development Build` 전용 HUD/로그/테스트 버튼 노출 정책을 분리
- 배포 패키지, 압축물 구성, 실행 파일 이름, 릴리즈 노트 초안까지 포함한 전달 단위를 고정

## 6. 남은 기간 실행안 (2~3주)
1. Week A (출시형 UI 골격)
- S8 Runtime UI 전환 1차 (`Title/Lobby/Result/Demo Complete/Settings` 화면 골격)
- S9 KB+Mouse UX 기준 정리
- S7 릴리즈 게이트 초안 작성

2. Week B (플레이 표시 / 온보딩)
- S10 HUD 재설계 1차
- S6 온보딩/가이드 최소 반영
- S9 입력/옵션 반영 및 저장/복원 정리

3. Week C (피드백 / VFX / 안정화)
- S11 VFX 제품화 1차
- Pause/Confirm/Recovery 흐름 마감
- PlayMode 운영 씬 정기 스모크 + 회귀 수정

4. Week D (패키징 / 공개 준비)
- S12 브랜딩 / 패키징 마감
- S7 비개발 빌드 QA 체크리스트 실행
- 데모 빌드 고정 및 문서 정리

## 7. 설계 문서 분해 계획 (권장)
- TD 후속(권장):
  - `TD-010`: Demo Shell Flow/Presentation Contract
  - `TD-011`: Runtime Player HUD Contract
  - `TD-013`: Player Feedback Presentation Bridge Contract
  - `TD-016`: Runtime UI Shell And Navigation Contract
  - `TD-017`: KB+Mouse Input / Options / Accessibility Baseline
  - `TD-018`: Runtime VFX Presentation Bridge Contract
  - `TD-019`: Release Readiness And Build Gate
- GD 후속(권장):
  - `GD-008`: 데모 화면/전이 흐름 기준 문서로 유지
  - `GD-NNN`: Demo Onboarding And Result Copy/UX(필요 시 분리)
- OPS 후속(권장):
  - `OPS-003`: 공개 릴리즈 준비/운영 체크리스트와 QA matrix를 별도 운영 문서로 분리
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

5. `OnGUI` 기반 내부 테스트 UI를 늦게 교체해 출시 UX가 한 번에 무너질 위험
- 대응: S8을 `P0`로 승격하고, 상태/명령 owner는 유지한 채 presenter만 교체한다

6. 마우스+키보드 중심 게임에서 UI 입력/포커스 정책이 늦게 정리되어 옵션/일시정지/확인 흐름이 분산될 위험
- 대응: S9에서 `KB+Mouse 우선 UX`를 먼저 고정하고, 포커스 기반 UI는 접근성/후속 패드 확장 대비선으로 유지한다

7. VFX를 후반에 한꺼번에 붙여 전달 우선순위와 광과민성/가독성 문제가 동시에 발생할 위험
- 대응: S11에서 `이벤트 우선순위/쿨다운/강도 옵션`을 먼저 고정하고 최소 제품화부터 적용한다

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
  - S3 Result: `Timeout`/`GiveUp`으로 `StageResult(Fail)` 진입
  - S3 Result: Fail에서 `Retry/Return` 동작, `Next` 거부
  - S3 Result: `Stage1->2->3` 클리어 후 Demo Complete 총합이 clear 시도만 누적됨
  - S2 HUD: StagePlay에서 Carry/Source/Hit/Stage 메타가 갱신된다
  - S2 HUD: Hit 이벤트 입력 시 플래시/손실값 표시 후 시간 경과로 소멸한다
  - S2 HUD: Debug HUD는 non-development 빌드에서 비노출이다
  - S4 Feedback: UI 버퍼 소비 후 스냅샷 version 증가 + buffer clear
  - S4 Feedback: 동일 `Frame+Type+RelatedEntity` dedupe, cooldown(`0.15s`, Hit `0.10s`)이 반영된다
  - S4 Feedback: Impulse 다건 입력 시 단일 합산 snapshot 반영 + buffer clear
  - S4 Feedback: Animator null-safe skip(개발 빌드 경고 1회)로 런타임이 유지된다
  - S5 Audio: `Master/BGM/SFX/UI` 버스 볼륨 `0..1` clamp 및 즉시 반영이 동작한다
  - S5 Audio: 운영 씬에서 소스/클립 누락 시 자동 보정으로 재생 경로가 유지된다(실클립 할당 슬롯은 비파괴 유지)
  - S5 Audio: `Title->Lobby`, `Lobby->StagePlay`, `StagePlay->StageResult`, `Stage3->DemoComplete` 전이 cue가 1회씩 발행된다
  - S5 Audio: `Hit`는 snapshot version 기준 1회 소비되고, `Collect/Cleanup`은 total delta + cooldown(`0.05s`) 기준으로 소비된다
  - S5 Audio: 볼륨 옵션이 씬 재진입/재시작 후 복원된다
  - S5 Audio: DemoShell Overlay에서 `Master/BGM/SFX/UI` 볼륨을 조작하면 즉시 반영된다
  - S7 Gate: 완료 보고 전 `EditMode 전체`, `PlayMode BulletPlayModeSmokeTests 전체(13개 이상)`, `console error 0`를 모두 통과한다
  - S8 UI: `Title/Lobby/Stage HUD/Result/Demo Complete/Settings/Pause`가 `OnGUI` 없이 동작한다
  - S8 UI: `Development Build` 전용 디버그 요소와 공개 빌드 UI가 분리된다
  - S9 Input: 마우스+키보드 기준으로 메뉴/옵션/플레이/일시정지 전 구간 조작이 가능하다
  - S9 Input: 포커스 기반 기본 내비게이션과 `Submit/Cancel` 경로가 유지되어 후속 패드 확장 가능성이 남는다
  - S10 HUD: `Carry/Source/Timer/Objective/Danger` 정보가 짧은 시선 이동으로 읽힌다
  - S10 Onboarding: `Stage1` 첫 60초 내 핵심 루프 힌트가 1회 이상 노출되고, 재시도 시 과잉 반복되지 않는다
  - S11 VFX: `Hit/Deposit/Cleanup/Clear/Fail/Timeout Warning`가 실제 연출로 구분되며 중복 억제 규칙을 따른다
  - S11 VFX: 플래시/흔들림 강도 옵션이 있다면 즉시 반영되고, 옵션이 없으면 강도 기본값이 과도하지 않다
  - S12 Packaging: 제품명/아이콘/버전/기본 해상도/윈도우 정책이 공개 빌드 기준으로 정리된다
  - S12 Packaging: 비개발 빌드에서 테스트 버튼/디버그 HUD/개발용 경고 노출이 제거되거나 제한된다
  - HUD/사운드 옵션 반영 확인
  - 운영 씬 정기 스모크 결과 기록

## 10. 변경 이력
- 2026-03-20: S10 진행 상태 반영. `HazardStack` HUD를 `HazardStackMax` 기반 slot/frame 구조와 `brush gold/gray` sprite 기준으로 고정하고, 관련 EditMode/PlayMode 검증이 통과한 상태를 문서에 반영했다.
- 2026-03-13: Runtime UI 전환 구현 반영. S8을 `DONE`으로 전환하고 `RuntimeUiRoot` 기반 `Shell/Modal/HUD V1` 구현 상태를 반영했다. S10은 HUD V1이 완료되어 `IN_PROGRESS`로 갱신했고, `Pressure Source` 진행 바/약화 임계 marker와 남은 온보딩 범위를 명시했다.
- 2026-03-12: 공개 빌드 기준 부족분을 반영해 S8~S12를 추가했다. `OnGUI -> Runtime UI 전환`, `KB+Mouse UX`, `출시형 HUD/온보딩`, `VFX 제품화`, `브랜딩/패키징`을 후반 P0 작업 스트림으로 승격하고, `OPS-003` 분리 계획을 추가했다.
- 2026-03-05: S5 잔여 작업 완료. `DemoAudioBridge`에 Source 하이브리드 자동 보정, fallback tone clip 자동 할당/정리, missing cue warn-once 정책을 추가하고 `DemoShellFlowController` Overlay에 4버스 볼륨 슬라이더를 연결했다. EditMode/PlayMode 테스트를 확장해 전이 cue, 자동 세팅, 볼륨 복원을 검증하고 S5 상태를 `DONE`으로 갱신했다.
- 2026-03-05: S5 1차 구현 반영. `DemoAudioBridge`와 `TD-014`를 추가해 reader-only 오디오 소비 계약(버스/큐/dedupe/cooldown/볼륨 복원)을 고정하고, OPS 합격 기준을 테스트 가능 문장으로 갱신했다.
- 2026-03-05: S4 구현 반영. `TD-013`을 추가하고 피드백 소비를 snapshot writer로 전환했다. `PlayerEcsBridge` Animator trigger/impulse offset, `PlayerRuntimeHudBridge` feedback feed, dedupe/cooldown 규칙과 EditMode 테스트를 반영해 S4 상태를 `DONE`으로 갱신했다.
- 2026-03-05: S3 완료 반영. Fail 트리거(`Timeout/GiveUp`), 단일 Result 분기(클리어 전용 Next), Demo Complete 코어 총합/clear 시도 누적 규칙을 코드/테스트 기준으로 고정했다.
- 2026-03-05: S2 문서 마감. S2 상태를 `DONE`으로 전환하고 HUD 계약/검증 항목을 기준선으로 고정했다.
- 2026-03-04: S2 설계 고정 반영. `TD-011`을 추가하고 플레이 HUD를 `OnGUI + ECS snapshot(writer 단일)` 계약으로 확정했다. Source/Hit/Stage 표시 규칙과 Debug HUD 노출 정책(Editor/Development 전용)을 문서에 반영했다.
- 2026-03-04: S1 구현 반영. `RunDirectorStageTempFlowDriver` 제거, Demo Shell 전이 계약(`TD-010`) 추가, 운영/테스트 씬 SubScene 분리 기준을 확정했다.
- 2026-03-04: GD-008 반영. 범위를 단일 런 중심에서 `Title/Lobby/3스테이지/Demo Complete` 데모 셸 플로우 기준으로 확장했다.
- 2026-03-04: OPS-002 초안 작성 (데모 플레이 완성 범위: StageFlow/UI, HUD, 결과 UX, 피드백, 사운드, 릴리즈 게이트)
