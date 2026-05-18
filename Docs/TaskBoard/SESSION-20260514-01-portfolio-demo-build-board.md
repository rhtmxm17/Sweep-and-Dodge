# SESSION-20260514-01 Portfolio Demo Build Board

## Session Goal
- 한 줄 목표: 외부 포트폴리오 문서에 쓸 데모 빌드, 성과 수치, 영상 자료가 사실로 성립하도록 준비 작업을 추적한다.
- 완료 기준: 공개 데모 빌드 산출물, 검증 수치, 영상/GIF, README/PORT-003 갱신 항목이 완료되거나 명확한 다음 시작점으로 남는다.
- 이번 세션에서 하지 않을 것: 프로젝트 네이밍 결정, 스토어 배포 준비, 최종 아트/사운드 품질 확정.

## 현재 상태
- 공개 빌드 대상 플랫폼은 Windows standalone으로 둔다.
- 공개 데모 기준 씬은 `Assets/_Project/01_Scenes/SampleScene.unity`다. 이후 이름 정리가 필요하면 `DemoEntry` 같은 명확한 이름으로 별도 작업에서 처리한다.
- 공개 진입 흐름은 `Launch -> Title -> Lobby -> Stage Play -> Stage Result -> Demo Complete`를 기준으로 한다.
- `Lobby`에서 Stage 1~3 직접 선택은 공개 기능으로 유지한다.
- `Give Up`은 테스트 버튼이 아니라 공개 UX로 유지한다.
- 공개 빌드 설정은 `SampleScene` 단일 활성 씬 기준이다. `SampleScene`의 `Assets/_Project/01_Scenes/SampleScene/Entities.unity` SubScene 의존은 유지한다.
- `Assets/Scenes/Entity Prefab Build Registry.unity`는 현재 워크트리에 없고 코드/설정 참조도 남아 있지 않아 별도 빌드 씬 의존으로 보지 않는다.
- 개발 전용 overlay, 강제 진행 버튼, stress/replay/fixed tick/debug HUD 조작 UI는 공개 빌드에서 노출하지 않는다.
- 스테이지별 공개 데모 역할:
  - Stage 1: 기본 조작, 청소/수집, Deposit, 단순 위험 회피를 학습시키는 첫 루프.
  - Stage 2: 대량 개체 처리와 actor 기반 동선 선택을 보여주는 대표 스테이지. 대표 성능 캡처 후보로 둔다.
  - Stage 3: 다양한 탄환 반응과 최종 시각 쇼케이스 후보. 영상/GIF 하이라이트 후보로 둔다.
- Stage 1~3의 구체 레벨 디자인과 실제 StageCatalog/Layout 편집은 T1b/T1c에서 다룬다.

## Now
- 없음. 다음 시작점은 T1b/T1c 또는 T2 검증 갱신이다.

## Next
- [ ] T1b. 레벨 디자인 구체화
  - 목적: T2 검증 전에 Stage 1~3이 공개 데모에서 맡을 역할, 난이도 곡선, Source/Deposit/HazardActor 배치 의도를 문서 기준으로 고정한다.
  - 완료 기준: Stage 1~3별 학습/성능/쇼케이스 역할, 주요 actor 패턴, Source 구조, 실패 유도/완화 포인트가 TaskBoard 또는 GD/OPS 문서에 정리된다.
  - 검증: 설계 제안값을 serialized asset exact assert로 승격하지 않고, 실제 편집 단계에서 확인할 체크리스트로 남긴다.
  - 근거: `Docs/TaskBoard/SESSION-20260514-01-portfolio-demo-build-board.md`, `Docs/GameDesign/GD-008-demo-flow-design.md`
- [ ] T1c. 실제 스테이지 편집
  - 목적: 구체화된 레벨 디자인을 `StageLayoutEditingSampleV1` / StageCatalog 자산에 반영해 공개 빌드 후보의 플레이 경험을 실제로 맞춘다.
  - 완료 기준: Stage 1~3 layout/catalog/presentation 후보가 공개 데모 역할에 맞게 편집되고, generator/composer 결과가 운영 씬에서 참조된다.
  - 검증: StageCatalog validation, 운영 씬 stage entry smoke, 필요한 경우 PlayMode 대표 루프 확인.
  - 근거: `Docs/TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md`, `Docs/TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md`
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
- [x] T1. 공개 데모 빌드 기준 씬과 진입 경로 확정
  - 결과: Windows standalone 공개 데모 기준 씬을 `Assets/_Project/01_Scenes/SampleScene.unity`로 두고, 공개 진입 흐름을 `Launch -> Title -> Lobby -> Stage Play -> Stage Result -> Demo Complete`로 확정했다.
  - 결과: Build Settings는 `SampleScene` 단일 활성 씬 상태로 정리되어 있으며, `SampleScene`의 SubScene 의존(`Assets/_Project/01_Scenes/SampleScene/Entities.unity`)은 유지한다.
  - 결과: PlayMode 테스트 씬과 존재하지 않는 `Assets/Scenes/Entity Prefab Build Registry.unity`는 공개 빌드 씬 의존으로 보지 않는다.
  - 수정: `DemoShellFlowController` fallback `OnGUI`, keyboard shortcut, `Force ClearReady` test API를 `UNITY_EDITOR || DEVELOPMENT_BUILD` 전용으로 제한했다.
  - 문서: 세션 공통 결정은 `현재 상태` 단락으로 분리했고, `TD-016`에 공개 빌드 fallback 비노출 정책을 반영했다.
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
