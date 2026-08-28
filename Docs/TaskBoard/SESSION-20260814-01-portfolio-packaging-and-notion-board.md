# SESSION-20260814-01 Portfolio Packaging and Notion Board

## Metadata
- doc_id: `SESSION-20260814-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-08-28`
- related_docs:
  - [../../README.md](../../README.md)
  - [../Portfolio/INDEX.md](../Portfolio/INDEX.md)
  - [../Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md](../Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md)
  - [../Portfolio/PORT-002-ai-assisted-engineering-workflow.md](../Portfolio/PORT-002-ai-assisted-engineering-workflow.md)
  - [../Portfolio/PORT-003-validation-report.md](../Portfolio/PORT-003-validation-report.md)
  - [Support/SESSION-20260814-01/INDEX.md](Support/SESSION-20260814-01/INDEX.md)
  - [Support/SESSION-20260814-01/NOTE-001-developer-perspective-and-claim-evidence.md](Support/SESSION-20260814-01/NOTE-001-developer-perspective-and-claim-evidence.md)
  - [Support/SESSION-20260814-01/NOTE-002-stage2-standalone-profiling-evidence.md](Support/SESSION-20260814-01/NOTE-002-stage2-standalone-profiling-evidence.md)
  - [Support/SESSION-20260814-01/NOTE-003-channel-content-architecture.md](Support/SESSION-20260814-01/NOTE-003-channel-content-architecture.md)
  - [../Portfolio/Evidence/Stage2-Profiling/README.md](../Portfolio/Evidence/Stage2-Profiling/README.md)
  - [../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)
  - [SESSION-20260514-01-portfolio-demo-build-board.md](SESSION-20260514-01-portfolio-demo-build-board.md)
  - [../AGENTS/agent-ops.md](../AGENTS/agent-ops.md)

## Session Goal
- 한 줄 목표: `Sweep and Dodge`를 Unity 클라이언트 개발자용 테크 데모 포트폴리오로 정리하고, 저장소 문서와 증거 자료를 기반으로 Notion 포트폴리오까지 구성한다.
- 완료 기준: 포트폴리오 브리프, 주장-근거 인벤토리, 공개 데모 검증 자료, 저장소 문서, 시각 자료, Notion 문서가 일관된 메시지와 근거로 연결되고 최종 검수를 통과한다.
- 이번 작업에서 하지 않을 것: 포트폴리오 근거 확보에 필요하다고 별도 합의되지 않은 게임 기능 확장, 스토어 출시 준비, 전체 출시형 콘텐츠 제작.

## 운영 규칙
- 이 문서는 메인 대화와 분기 대화가 공유하는 상태 허브이자 결정 메모다.
- 새 분기는 작업 시작 전에 `Adopted Baseline`, `Now`, 자신의 작업 항목, `Pending Branch Handoffs`를 읽는다.
- 분기 작업은 담당 항목의 상태, 산출물 링크, 검증 결과, 다음 시작점만 갱신한다.
- 합의되지 않은 새 범위나 설계 결정을 TaskBoard 갱신만으로 확정하지 않는다.
- 중요한 새 결정은 메인 대화에서 합의한 뒤 `Adopted Baseline`에 추가한다.
- 상세 설계와 긴 근거는 해당 Portfolio/ADR/OPS 문서에 두고, 이 문서에는 결론과 링크만 남긴다.
- 저장소 문서를 내용의 기준으로 삼고, Notion은 채용 독자에게 맞춘 표현 계층으로 구성한다.
- `맥락 병합`은 분기 작업에서 문서로 고정할 수준은 아니지만 배경 이해나 후속 판단에 도움이 되는 보조 맥락을 메인 대화에 전달하는 작업을 뜻한다.
- 맥락 병합으로 전달된 보조 맥락은 TaskBoard와 확정 문서를 대체하지 않으며, 기본적으로 직접 문서화하지 않는다.
- 보조 맥락은 이미 확정된 결정을 재논의하는 근거로 사용하지 않는다. 새로운 충돌 근거나 범위 변경 필요가 확인된 경우에만 메인 대화에서 별도로 논의한다.
- 보조 맥락이 이후 작업의 공식 판단 기준이나 새 결정으로 승격되어야 한다면, 사용자와 합의한 뒤 해당 SSOT 또는 `Adopted Baseline`에 반영한다.
- 분기에서 검토 후 폐기한 문장 후보나 표현 대안은 별도 보존 요청이 없는 한 맥락 병합 대상에 포함하지 않는다.
- `Pending Branch Handoffs`에는 아직 메인 세션에 맥락 병합되지 않은 분기 결과와 미해결 충돌·질문만 둔다.
- 맥락 병합이 끝나면 채택 결정은 `Adopted Baseline`, 완료 결과는 `Done`, 상세 근거는 세션 Support 노트에 흡수하고 해당 handoff를 제거한다.

## Adopted Baseline

### A. 프로젝트 포지셔닝과 독자
- 프로젝트는 완성 게임 공개본이 아니라 **플레이 가능한 Unity DOTS/Entities 기술 데모**이며, 일반적인 신입~주니어 Unity 클라이언트 개발자 포지션을 목표로 한다.
- Notion 프로젝트 문서는 전체 포트폴리오 아래의 프로젝트 페이지로 두고 DOTS와 대량 엔티티 처리라는 기술적 차별점을 첫인상으로 제시한다. 채용 담당자는 빠른 범위·결과 확인, Unity 실무자와 기술 면접관은 설계·구현·검증 근거 확인을 주 독서 경로로 본다.
- 프로젝트 한 문장 소개는 다음을 기준으로 한다.
  > `Sweep and Dodge`는 회피·청소·수집 플레이 루프에 Unity DOTS/Entities 기반 대량 엔티티 파이프라인을 적용하고, 명시적인 소유권과 업데이트 순서로 시스템을 구성한 플레이 가능한 기술 데모입니다.

### B. 핵심 주장과 비주장
- 핵심 역량은 ① DOTS 기반 대량 엔티티 처리와 데이터 지향 설계, ② ownership·update order·fence를 통한 검증 가능한 아키텍처, ③ 게임플레이·UI·콘텐츠 흐름을 연결한 플레이 가능한 데모 완성이다.
- 공개 범위에는 회피·청소·수집 루프, DOTS pipeline, enableable 상태 전환, authoring/editor tooling, 설명 가능한 검증 근거, AI-assisted workflow, 트레이드오프와 현재 한계를 포함한다.
- 상용 출시 수준의 게임, 최종 아트·사운드·콘텐츠 분량, 모든 코드의 설명, GameObject 대비 정량적 우위, 테스트·성능 엔지니어링 전문성, 스토어 배포와 플랫폼별 장기 품질은 주장하지 않는다.
- 자동 테스트와 PlayMode smoke는 ownership·update order·runtime behavior의 자동 관찰 가능한 계약을 후속 구현에서 다시 확인하는 2차 guardrail이자 보조 증거로 설명한다.

### C. 대표 사례와 AI 역할
- AI coding agent는 방어적으로 정당화할 대상이 아니라 일상적인 개발 도구다. 개발자는 목표·제약·설계 채택·플레이 감각·공개 범위를 판단하고, agent는 탐색·대안·계획·구현·반복 검증·문서화를 가속했다.
- guardrail은 AI 통제가 아니라 프로젝트 지식과 반복 오류 패턴을 ownership, update order, validation rule 등으로 구조화한 엔지니어링 체계다. AI 활용은 핵심 역량과 경쟁시키지 않고 발견 가능한 전용 심화 섹션으로 둔다.
- 대량 엔티티 파이프라인은 여러 작업에 걸쳐 형성됐으므로 최초 제안 주체가 불명확한 세부 요소의 역할 귀속을 주장하지 않는다.
- `BroomSweep`은 개발자가 플레이 감각 문제와 제약을 정의하고 최종안을 선택하며 agent가 설계 구체화·구현·자동 검증을 수행한 플레이 기반 개선 사례다.
- `StageMapEditor`는 개발자가 제작 문제·편집 범위·runtime 비변경 경계·UX 요구를 정의하고 agent가 document SSOT, validation, dry-run, stale-plan 거부, Undo 설계를 제안·구현한 목표 기반 개발 사례다. 메인 페이지에서는 대표 보조 사례, `HazardActorWorkbench`는 선택적 심화 자료로 둔다.
- 장애물 충돌 병목은 Profiler로 지배적인 시스템을 특정해 CellMap 후보 축소를 요구한 정성적 진단 사례로만 사용한다. 사후 동일 조건 측정이 없는 수치·배율·FPS는 공개하지 않는다.
- 상세 역할 분담과 주장 경계는 [NOTE-001](Support/SESSION-20260814-01/NOTE-001-developer-perspective-and-claim-evidence.md)을 따른다.

### D. 공개 증거 계약
- 최신 공개 성능 근거는 공개 데모와 같은 Stage 2에서 무입력·무청소로 Dust를 자연 누적한 plateau를 동일 Windows x64 IL2CPP Development Build에서 600 frame × 3회 측정한 결과다.
- 3-run 합산 active Total 평균은 `24,148.3`, 범위는 `24,077–24,236`, frame interval median/p95/max는 `7.291/9.249/12.872ms`, 16.67ms 초과는 `0/1,797`이다. 일반 플레이의 상시 밀도나 보편적인 60fps 보장으로 해석하지 않는다.
- 정확한 공개 조건·표·이미지의 SSOT는 [Stage 2 Standalone Profiling Evidence](../Portfolio/Evidence/Stage2-Profiling/README.md)다. 과거 약 2.5만 snapshot과 Editor 측정은 최신 standalone 결과나 회귀 보장 수치로 사용하지 않는다.
- 기존 `Bullet` active 명칭은 위험 요소와 청소·수집 대상을 함께 포함하므로 공개 시 Total/Dust/Hazard 구성을 명시한다.
- Spawn 병목은 `FreeByKey` iterator 직접 제거로 교정했고, 측정 비교 후 Pool Owner의 직렬 dequeue·상태 초기화를 채택했다. 상세 결정은 [ADR-20260822-01](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)을 따른다.
- 공개 증거 패키지는 3-run 표, 약 22초 HUD 영상, 같은 fixed Tick frame의 CPU Timeline과 composition counter 이미지로 구성한다. raw Profiler 자료·CSV·로그·내부 manifest와 편집 전 영상은 로컬에 유지하고 공개 SHA는 기록하지 않는다.
- Development Build 결과를 최종 Release Build, 모든 하드웨어·플레이 상황의 60fps, GameObject 대비 우위로 확장하지 않는다. runtime 코드·Stage 2 콘텐츠·렌더링·품질 설정이 바뀔 때 재측정 필요성을 다시 판단한다.
- 측정 해석과 제외된 캡처의 상세 배경은 [NOTE-002](Support/SESSION-20260814-01/NOTE-002-stage2-standalone-profiling-evidence.md)를 따른다.

### E. 채널과 공개 경계
- Notion은 채용 독자를 위한 주 서사, README는 저장소 진입점, `PORT-*`는 선택적 심화, 공개 Evidence는 수치 확인 계층으로 구성한다. 공통 서사·섹션 청사진·정보 라우팅은 [NOTE-003](Support/SESSION-20260814-01/NOTE-003-channel-content-architecture.md)을 따른다.
- 공개 가능한 조건·표·이미지는 `Docs/Portfolio/Evidence/`에 두고 세션 Support 노트는 내부 운용·기록 문서로 관리한다. README, `PORT-*`, 공개 Evidence에서는 Support 노트를 직접 참조하지 않는다.
- 내부 운용 문서는 공개 저장소를 직접 탐색하면 보일 수 있지만 공식 포트폴리오 독자 경로에서는 제외하며 기밀을 의미하지 않는다.

### F. 전달 자료와 작업 순서
- 대표 플레이·`BroomSweep`·`StageMapEditor` 보조 영상은 T7 Notion 레이아웃에 맞춰 촬영하고, T6에서는 파이프라인 도식과 Windows x64 압축 패키지·실행 안내·최종 전달 smoke를 준비한다.
- 공개 빌드는 debug HUD 기본 비활성, uncapped 운영 정책을 유지하고 raw Profiler 자료를 포함하지 않는다.
- 현재 순서는 `T5 저장소 문서 → T6 시각 자료·데모 패키지 → T7 Notion → T8 최종 검수`다.
## Now
- [ ] T5. 저장소 포트폴리오 문서를 개편한다.
  - 완료 기준: `PORT-001 -> PORT-003 -> PORT-002 -> README -> INDEX` 순으로 합의된 서사와 근거를 반영한다.
  - 검증: 링크, 명칭, 수치, 문서 간 역할, AI 활용 뉘앙스를 교차 점검한다.
  - 근거: `Support/SESSION-20260814-01/NOTE-003-channel-content-architecture.md`.

## Next
- [ ] T6. 시각 자료와 공개 데모 패키지를 준비한다.
  - 완료 기준: 대표 플레이 영상/GIF, 파이프라인 다이어그램, 검증 결과 표, 공개 빌드와 실행 안내가 준비된다.
  - 검증: 각 자료가 T2의 특정 주장을 뒷받침하고, 공개 후보 빌드 기동 smoke를 통과한다.
  - 근거: T2, T3 및 `SESSION-20260514-01`.
- [ ] T7. Notion 포트폴리오를 제작한다.
  - 완료 기준: 프로젝트 요약, 대표 영상, 담당 범위, 기술 문제, 아키텍처, 검증 결과, AI-assisted workflow, 회고, 외부 링크가 채용 독자 중심으로 구성된다.
  - 검증: 저장소 문서를 그대로 복제하지 않고, 저장소의 최신 사실과 링크를 기준으로 내용을 대조한다.
  - 근거: T4~T6 산출물.
- [ ] T8. 최종 포트폴리오 검수를 수행한다.
  - 완료 기준: 채용 담당자, Unity 개발자, 기술 면접관, 일반 방문자 관점의 검수와 명칭·수치·링크 일관성 검사를 마친다.
  - 검증: 발견된 수정 사항이 반영되거나 명시적인 남은 리스크로 기록된다.
  - 근거: T5~T7 산출물.

## Blocked
- 없음.

## Pending Branch Handoffs
- 없음.
## Done
- [x] T4. 포트폴리오 서사와 채널별 정보 구조를 확정했다.
  - 결과: Notion 주 서사, README 저장소 진입점, `PORT-*` 심화 문서, 공개 Evidence 검증 계층의 점진적 공개 구조를 채택했다.
  - 결과: 공통 서사, 채널별 섹션 청사진, 주장·수치 라우팅, 중복·인용 규칙과 T5 완료 조건을 `NOTE-003`에 고정했다.
  - 결과: 내부 `NOTE-001/002`를 Portfolio 범위 밖의 `SESSION-20260814-01` Support 영역으로 이동하고 공개 문서의 직접 참조를 제거했다.
  - 검증 결과: 공개 문서와 내부 운용 문서의 탐색 경계를 분리하고 상대 링크, UTF-8 내용, `git diff --check`를 확인했다.
- [x] T3. 공개 데모와 검증 기준을 확정했다.
  - 결과: 최신 비주얼 standalone Development Build에서 Total/Dust/Hazard와 frame interval을 함께 기록하고 동일 조건 600 frame × 3회를 재현했다.
  - 결과: 공개 결과를 3-run 합산 표, 약 22초 누적·plateau 영상, CPU Timeline과 counter 이미지 두 장으로 구성했다.
  - 결과: `Evidence/` 문서 구조와 공개/로컬 원시 자료 경계, 공개 SHA 생략 원칙을 확정했다.
  - 결과: 대표 플레이·BroomSweep·Stage Map Editor 촬영 내용은 고정하되 실제 촬영은 T7 Notion 제작 시점으로 넘겼다.
  - 결과: Windows x64 압축 빌드의 구성, 실행 안내, debug HUD 기본 비활성, uncapped 운영 정책과 raw 자료 제외 기준을 확정했다.
  - 검증 결과: 한글 폰트와 미사용 Feel을 정리한 현재 상태 빌드 및 가벼운 수동 플레이 smoke에서 직접 드러나는 오류가 없었다. 측정 조건에 영향을 주는 변경이 없어 추가 profiling은 수행하지 않는다.
- [x] T2. 주장-근거 인벤토리를 확정했다.
  - 결과: DOTS 대량 엔티티 처리, 명시적 pipeline/ownership, 플레이 가능한 데모 완성을 핵심 주장으로 유지했다.
  - 결과: fence는 심화 기술 근거, 자동 테스트는 설계 계약의 2차 guardrail, 과거 성능 수치는 development snapshot으로 분류했다.
  - 결과: 플레이 기반 `BroomSweep` 개선과 목표 기반 `StageMapEditor` 교체를 개발자 판단과 AI-assisted 실행의 대표 사례로 채택했다.
  - 결과: `StageMapEditor`는 메인 페이지의 대표 보조 사례, `HazardActorWorkbench`는 선택적 심화 자료로 배치한다.
  - 결과: 장애물 충돌 Profiler 사례는 정성적 병목 진단 사례로만 사용하고, 사후 측정 없는 정확 수치·개선 배율·성능 전문성은 주장하지 않는다.
  - 결과: 개발자 관점과 역할 분담의 상세 판단은 `NOTE-001` 세션 보조 노트로 분리해 후속 문서 작성과 면접 준비의 참고 자료로 보존했다.
  - 검증 결과: 코드, ADR/TD/OPS/TaskBoard, 로컬 Git 이력과 사용자 설명 가능 수준을 대조해 `핵심 주장`, `보조 증거`, `심화 자료`, `비공개` 경계를 확정했다.
- [x] T1. 포트폴리오 브리프를 확정했다.
  - 결과: 일반적인 신입~주니어 Unity 클라이언트 개발자 포지션과 채용 담당자/Unity 실무자/기술 면접관의 독자 계층을 확정했다.
  - 결과: 전체 포트폴리오 아래의 프로젝트 문서로서 DOTS 대량 엔티티 기술 데모를 첫인상으로 두고, 프로젝트 한 문장 소개를 채택했다.
  - 결과: 대량 엔티티·데이터 지향 설계, 검증 가능한 아키텍처, 플레이 가능한 데모 완성을 핵심 역량 3축으로 확정했다.
  - 결과: 프로젝트 범위와 비범위, 검증 자료의 보조 증거 원칙, AI-assisted engineering 전용 심화 섹션과 브리프 문구를 확정했다.
  - 검증 결과: T1 완료 조건의 목표 직무와 독자, 한 문장 소개, 핵심 역량 3개, 범위/비범위, AI 활용 포지셔닝이 모두 `Adopted Baseline`에 기록됐다.
- [x] D1. 현재 포트폴리오 문서의 기준 내용을 읽고 핵심 메시지와 증거 공백을 정리했다.
  - 검증 결과: `README.md`, `Docs/Portfolio/INDEX.md`, `PORT-001~003`을 UTF-8로 확인했다.
- [x] D2. 포트폴리오 작업의 전체 순서를 합의했다.
  - 검증 결과: 포지셔닝부터 최종 검수까지 T1~T8의 선후 관계를 현재 보드에 반영했다.
- [x] D3. AI 활용 서술의 기준을 보정했다.
  - 검증 결과: AI를 방어적으로 정당화하지 않고, AI-native 개발 생산성과 프로젝트 수준의 품질 체계를 보여주는 방향을 `Adopted Baseline`에 반영했다.
- [x] D4. 메인 대화와 분기 대화가 공유할 TaskBoard를 생성했다.
  - 검증 결과: 운영 규칙, 채택 기준, 현재 작업, 후속 순서, 분기 인수인계 위치를 한 문서에서 확인할 수 있다.

## End of Session
- 결과: T1~T4를 완료했다. 채택 주장과 공개 증거를 바탕으로 Notion, README, `PORT-*`, 공개 Evidence의 서사·깊이·탐색 경계를 확정하고 내부 원천 노트를 세션 Support 기록으로 분리했다.
- 남은 리스크: 공개 문서 본문 개편, 대표 영상·파이프라인 도식, 공개 Windows x64 압축 패키지 생성과 최종 전달 smoke는 T5~T7 실행 작업으로 남아 있다.
- 다음 시작점: T5에서 `NOTE-003`을 실행 기준으로 `PORT-001`부터 저장소 포트폴리오 문서를 개편한다.
