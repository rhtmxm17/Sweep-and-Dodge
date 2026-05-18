# SESSION-20260514-01 Portfolio Demo Build Board

## Session Goal
- 한 줄 목표: 외부 포트폴리오 문서에 쓸 데모 빌드, 성과 수치, 영상 자료가 사실로 성립하도록 준비 작업을 추적한다.
- 완료 기준: 공개 데모 빌드 산출물, 검증 수치, 영상/GIF, README/PORT-003 갱신 항목이 완료되거나 명확한 다음 시작점으로 남는다.
- 이번 세션에서 하지 않을 것: 프로젝트 네이밍 결정, 스토어 배포 준비, 최종 아트/사운드 품질 확정.

## Now
- [ ] T1. 공개 데모 빌드 기준 씬과 진입 경로 확정
  - 목적: 외부 독자가 실행할 빌드의 시작점과 확인할 핵심 루프를 고정한다.
  - 완료 기준: 데모 빌드에서 보여줄 씬, 진입 흐름, 제외할 개발 전용 기능이 문서화된다.
  - 검증: 공개 빌드 후보에서 해당 경로로 진입 가능한지 확인한다.
  - 근거: `Docs/Portfolio/PORT-003-validation-report.md`
  - 결정:
    - 공개 빌드 대상 플랫폼은 Windows standalone으로 둔다.
    - 공개 데모 기준 씬은 현재 `Assets/_Project/01_Scenes/SampleScene.unity`를 기준으로 하되, 이후 `DemoEntry` 같은 명확한 이름으로 변경한다.
    - 공개 진입 흐름은 `Launch -> Title -> Lobby -> Stage Play -> Stage Result -> Demo Complete`를 기준으로 한다.
    - `Lobby`에서 Stage 1~3 직접 선택은 공개 기능으로 유지한다.
    - `Give Up`은 테스트 버튼이 아니라 공개 UX로 유지한다.
    - 공개 빌드 설정에서는 PlayMode 테스트 씬을 제거한다. 단, DOTS/Entities 빌드 의존 씬은 제거 전 의존성 점검을 수행한다.
    - 개발 전용 overlay, 강제 진행 버튼, stress/replay/fixed tick/debug HUD 조작 UI는 공개 빌드에서 노출하지 않는다.
  - 스테이지별 공개 데모 역할:
    - Stage 1: 기본 조작, 청소/수집, Deposit, 단순 위험 회피를 학습시키는 첫 루프. 현재 행동 패턴 샘플용 actor는 온보딩용으로 다소 크므로, 고정 방향으로 주기 발사하는 단순 actor 중심으로 축소하는 방향을 검토한다.
    - Stage 2: 대량 개체 처리와 actor 기반 동선 선택을 보여주는 대표 스테이지. 현재 다양한 탄환 발사 샘플은 공개 데모용 직접 샘플 노출보다 actor 구성으로 흡수하거나 제거하는 방향을 검토한다. Stage 1의 단순 actor를 포함해 간단한 패턴을 가진 actor 2종을 Source 영역별 2~3개체 배치하는 안을 우선 후보로 둔다.
    - Stage 3: 다양한 탄환 반응(예: 가벼운 homing, 만료 시 산탄 등)을 활용한 최종 시각 쇼케이스 후보. 현재는 구체 레벨 디자인이 모호하므로 별도 설계가 필요하다.
  - 성능/시각 증거 후보:
    - Stage 2는 공개 데모의 대표 성능 캡처 후보로 둔다.
    - Stage 3는 영상/GIF용 시각 하이라이트 후보로 둔다.
    - 정확한 측정 수치, 캡처 시점, 영상 저장/링크 정책은 데모 빌드 작업 이후 T4/T5 단계에서 확정한다.
  - 남은 점검:
    - `SampleScene` rename 방식과 참조/GUID 유지 절차 확인.
    - Build Settings에서 제거할 테스트 씬과 유지해야 하는 DOTS/Entities 의존 씬 구분.
    - Stage 2를 기존 단일 Source 구조로 유지할지, 다중 Source 구조로 확장할지 결정.
    - Stage 3의 최종 쇼케이스 구성을 actor 기반, 탄환 reaction 기반, 또는 혼합형 중 무엇으로 둘지 결정.
  - 2026-05-18 읽기 전용 점검 결과:
    - `ProjectSettings/EditorBuildSettings.asset`와 Unity MCP `manage_build(action=scenes)` 모두 현재 활성 빌드 씬 4개를 보고한다.
    - 현재 활성 빌드 씬은 `SampleScene`, `PlayModeSmoke_Dedicated`, `PlayModeSmoke_SampleVerification`, `Assets/Scenes/Entity Prefab Build Registry.unity`다.
    - `PlayModeSmoke_Dedicated`와 `PlayModeSmoke_SampleVerification`는 공개 빌드에서 제거할 테스트 씬 후보로 확인했다.
    - `Assets/Scenes/Entity Prefab Build Registry.unity`는 현재 워크트리에 파일과 폴더가 존재하지 않는다. 제거 후보로 보되, Entities 빌드 과정에서 자동 생성/요구되는 경로인지 Unity 빌드 전 확인이 필요하다.
    - `SampleScene`은 `Assets/_Project/01_Scenes/SampleScene/Entities.unity` SubScene을 `AutoLoadScene=1`로 참조한다. 공개 빌드 씬 정리 시 이 SubScene 의존은 유지해야 한다.
    - `SampleScene`에는 `RuntimeUiRoot` 프리팹 인스턴스가 있고, runtime UI 활성 시 `DemoShellFlowController`의 OnGUI overlay와 `PlayerRuntimeHudBridge` OnGUI HUD가 비활성화되는 구조다.
    - `DemoShellFlowController.ShowOverlay`는 현재 씬에서 `1`이며, runtime UI가 활성화되지 못하면 `Force ClearReady (Test)`가 포함된 fallback overlay가 노출될 수 있다. 공개 빌드 기준에서는 `ShowOverlay=0` 또는 `UNITY_EDITOR/DEVELOPMENT_BUILD` 게이트가 필요하다.
    - `BulletDebugHudBridge`는 non-development 빌드에서 OnGUI가 return되며, `SampleScene`의 `ShowHud=0` 상태를 확인했다.
    - Unity MCP `manage_scene`은 Editor instance를 찾지 못해 실제 씬 로드/Play 진입은 확인하지 못했다. `read_console`은 현재 error 항목 2개를 반환했으나, 하나는 Input Manager deprecation 메시지이고 하나는 MCP client handler 종료 로그다. T2 검증 전 noise/실제 에러 구분이 필요하다.
  - 다음 액션 후보:
    - 공개 빌드용 Build Settings 목표 목록을 `SampleScene` + 필요한 Entities/SubScene 의존으로 축소하는 패치 초안 작성.
    - `DemoShellFlowController` fallback overlay 공개 빌드 비노출 방식 결정.
    - Unity Editor 연결 후 `SampleScene` 로드, Title 진입, Runtime UI 활성, overlay 비노출을 실제로 확인.

## Next
- [ ] T2. Unity Console error 0 및 EditMode/PlayMode smoke 결과 확보
  - 완료 기준: 최신 검증 결과가 날짜, Unity 버전, 테스트 범위와 함께 기록된다.
  - 검증: Console error 0, EditMode, PlayMode smoke 결과 기록.
  - 근거: `Docs/ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md`
  - 2026-05-18 재검증 메모:
    - Unity `6000.3.6f1`, `Sweep-and-Dodge@ee769676` MCP 연결 기준으로 EditMode 전체 테스트는 `479/479 passed`를 확인했다.
    - PlayMode 전체 테스트는 `50 total` 중 실패가 남아 T2 완료 조건을 충족하지 못했다.
    - `PlayMode_SampleVerificationScene_ExpandedBulletSamples_ExerciseRepresentativeStateTransitions`는 테스트 전용 씬을 공개 Build Settings에 포함해야 하는 전제가 잘못된 것으로 판단했다. `EditorSceneManager.LoadSceneInPlayMode` 기반 로딩으로 수정 후 단일 재실행 `1/1 passed`를 확인했다.
    - `PlayMode_DedicatedScene_RunDirectorStageBridge_ConfirmTransitionsToCompleted`는 `SweepNDodge.PlayModeTests.csproj` 재빌드 이후 통과했다. 과거 실패 로그의 `D:/Workspace/DOTS-minigame` 경로는 이전 프로젝트 경로가 남은 빌드 산출물/심볼 영향으로 본다.
  - 남은 추적 항목:
    - `PlayMode_OperationalScene_PresentationController_RebuildsAcrossNextAndRetry`는 실패 원인이 운영 씬 presentation runtime 문제가 아니라 테스트 전제 오류로 확인되어 수정했다. Stage 1 실패의 `childCount=2` 중 두 번째 child는 `StageGridVisualController`가 같은 transform 하위에 생성한 `GridVisual_Stage1`였고, 테스트를 `StagePresentationRuntimeController.SpawnedRootCount` 및 stableId 등록 root 기준으로 보정했다. Unity `6000.3.6f1`, `Sweep-and-Dodge@ee769676` MCP 기준 단일 재실행 `1/1 passed`를 확인했다.
    - PlayMode 종료/정리 단계 안정성 문제는 원인을 확인해 수정했다. 원인은 SubScene source entity의 `LinkedEntityGroup`에 runtime-created hazard actor entity를 추가해, PlayMode 종료 시 `SubScene.OnDisable -> SceneSystem.UnloadScene -> DestroyEntity(EntityQuery)` 경로의 section unload query에 포함되지 않은 actor를 참조하게 만든 ownership 경계 위반이었다. runtime hazard actor를 source `LinkedEntityGroup`에 넣지 않도록 변경했고, Unity `6000.3.6f1`, `Sweep-and-Dodge@ee769676` MCP 기준 재현 PlayMode 단일 테스트 `1/1 passed` 및 `LinkedEntityGroup` 콘솔 검색 `0건`을 확인했다.
- [ ] T3. 비개발 빌드 기동 smoke 수행
  - 완료 기준: 공개 후보 빌드가 Windows PC에서 기동하고 핵심 루프에 진입한다.
  - 검증: 실행 파일 기동, 기본 조작, 종료/재시작 경로 확인.
  - 근거: `Docs/ProjectOps/OPS-003-public-release-readiness-plan.md`
- [ ] T4. 성능/검증 수치 캡처
  - 완료 기준: 공개 데모 빌드 기준으로 active entity 규모, frame time 또는 동등한 관측값을 기록한다.
  - 검증: 측정 날짜, Unity 버전, 시나리오, 관측값, 해석 메모 기록.
  - 근거: `Docs/Portfolio/PORT-003-validation-report.md`
- [ ] T5. 영상/GIF 촬영
  - 완료 기준: 10~30초 범위에서 플레이어 조작, 대량 개체, 청소/수집 또는 디스폰 흐름이 보이는 자료를 확보한다.
  - 검증: README에서 참조 가능한 파일 또는 링크가 준비된다.
  - 근거: `Docs/Portfolio/PORT-003-validation-report.md`
- [ ] T6. README와 PORT-003 갱신
  - 완료 기준: 공개 데모 빌드 수치와 영상/GIF가 development snapshot과 구분되어 반영된다.
  - 검증: Markdown 링크 확인, `Sweep and Dodge` 외 임의 제품명 미도입 확인.
  - 근거: `README.md`, `Docs/Portfolio/PORT-003-validation-report.md`

## Blocked
- 없음

## Parking Lot
- [ ] P1. 프로젝트 공개명/저장소명/빌드명 결정
  - 근거: 현재 Portfolio 문서는 `Sweep and Dodge`를 공개 표기명으로 사용한다.
- [ ] P2. 스토어 배포용 메타데이터와 장기 벤치마크 정리
  - 근거: 현재 범위는 포트폴리오 기술 데모이며 출시 후보 빌드가 아니다.

## Done
- [x] B1. `PlayMode_OperationalScene_PresentationController_RebuildsAcrossNextAndRetry` 실패 분석 및 수정
  - 결과: 실패 원인은 presentation runtime stale child가 아니라 테스트가 같은 GameObject transform에 생성된 grid visual child를 presentation root로 오인한 전제 오류였다.
  - 수정: presentation settle/identity 검증을 `StagePresentationRuntimeController.SpawnedRootCount`와 stableId 등록 root 기준으로 변경했다.
  - 검증: 2026-05-18 Unity `6000.3.6f1`, `Sweep-and-Dodge@ee769676` MCP 기준 B1 단일 PlayMode 테스트 `1/1 passed`.
- [x] B2. PlayMode 종료/정리 단계 Entities unload 안정성 확인
  - 결과: source `LinkedEntityGroup`이 SubScene unload query 외부의 runtime hazard actor를 참조해 `DestroyEntity(EntityQuery)` 검증 예외를 유발했다.
  - 수정: runtime hazard actor를 source `LinkedEntityGroup`에 추가하지 않도록 변경하고, source는 ref/placement buffers로 actor를 추적하게 유지했다.
  - 검증: 2026-05-18 Unity `6000.3.6f1`, `Sweep-and-Dodge@ee769676` MCP 기준 관련 EditMode 계약 테스트 `1/1 passed`, B2 재현 PlayMode 테스트 `1/1 passed`, 콘솔 `LinkedEntityGroup` 검색 `0건`.

## End of Session
- 결과:
- 남은 리스크:
- 다음 세션 시작점:

## 사용자 메모

테크 데모 공개 빌드 범위 확정

- 어떤 씬을 공개 데모로 쓸지
- 어떤 기능은 포함하고, 어떤 기능은 숨길지
- 어떤 성능 시나리오를 보여줄지
- UI/디버그 오버레이를 어느 정도 둘지

데모 빌드 작업

- 플레이 가능한 핵심 루프 정리
- 깨진 기능 제거 또는 비노출
- 데모용 진입 씬/설정 정리
- 빌드 가능한 상태 확보

빌드 작업 후

- 성능 캡처
- README/Validation Report 수치 반영
- GitHub README 정리
- 영상/GIF 첨부
- 릴리즈 태그
