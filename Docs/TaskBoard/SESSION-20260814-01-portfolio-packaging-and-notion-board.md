# SESSION-20260814-01 Portfolio Packaging and Notion Board

## Metadata
- doc_id: `SESSION-20260814-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-08-24`
- related_docs:
  - [../../README.md](../../README.md)
  - [../Portfolio/INDEX.md](../Portfolio/INDEX.md)
  - [../Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md](../Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md)
  - [../Portfolio/PORT-002-ai-assisted-engineering-workflow.md](../Portfolio/PORT-002-ai-assisted-engineering-workflow.md)
  - [../Portfolio/PORT-003-validation-report.md](../Portfolio/PORT-003-validation-report.md)
  - [../Portfolio/PORT-NOTE-001-developer-perspective-and-claim-evidence.md](../Portfolio/PORT-NOTE-001-developer-perspective-and-claim-evidence.md)
  - [../Portfolio/PORT-NOTE-002-stage2-standalone-profiling-evidence.md](../Portfolio/PORT-NOTE-002-stage2-standalone-profiling-evidence.md)
  - [../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)
  - [SESSION-20260514-01-portfolio-demo-build-board.md](SESSION-20260514-01-portfolio-demo-build-board.md)
  - [../AGENTS/agent-ops.md](../AGENTS/agent-ops.md)

## Session Goal
- 한 줄 목표: `Sweep and Dodge`를 Unity 클라이언트 개발자용 테크 데모 포트폴리오로 정리하고, 저장소 문서와 증거 자료를 기반으로 Notion 포트폴리오까지 구성한다.
- 완료 기준: 포트폴리오 브리프, 주장-근거 인벤토리, 공개 데모 검증 자료, 저장소 문서, 시각 자료, Notion 문서가 일관된 메시지와 근거로 연결되고 최종 검수를 통과한다.
- 이번 작업에서 하지 않을 것: 포트폴리오 근거 확보에 필요하다고 별도 합의되지 않은 게임 기능 확장, 스토어 출시 준비, 전체 출시형 콘텐츠 제작.

## 운영 규칙
- 이 문서는 메인 대화와 분기 대화가 공유하는 상태 허브이자 결정 메모다.
- 새 분기는 작업 시작 전에 `Adopted Baseline`, `Now`, 자신의 작업 항목, 최신 `Branch Handoffs`를 읽는다.
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

## Adopted Baseline
- 프로젝트 포지셔닝은 완성 게임 공개본이 아니라 **플레이 가능한 Unity DOTS/Entities 기술 데모**다.
- 목표 직무는 일반적인 신입~주니어 Unity 클라이언트 개발자 포지션이다.
- 완성될 Notion 문서는 전체 포트폴리오 아래에 놓이는 여러 프로젝트 문서 중 하나다. 전체 포트폴리오가 넓은 Unity 클라이언트 역량을 설명하고, 이 프로젝트 문서는 DOTS와 대량 엔티티 처리라는 기술적 차별점을 첫인상으로 제시한다.
- 채용 담당자는 프로젝트의 범위와 결과를 빠르게 읽는 1차 독자로, Unity 실무자와 기술 면접관은 설계·구현·검증 근거를 확인하는 심화 독자로 본다.
- 프로젝트 한 문장 소개는 다음을 기준으로 한다.
  > `Sweep and Dodge`는 회피·청소·수집 플레이 루프에 Unity DOTS/Entities 기반 대량 엔티티 파이프라인을 적용하고, 명시적인 소유권과 업데이트 순서로 시스템을 구성한 플레이 가능한 기술 데모입니다.
- 포트폴리오의 핵심 역량은 다음 세 축을 기준으로 한다.
  1. DOTS 기반 대량 엔티티 처리와 데이터 지향 설계
  2. 명시적 ownership, update order, fence와 검증 가능한 아키텍처
  3. 게임플레이, UI, 콘텐츠 흐름을 연결한 플레이 가능한 데모 완성
- 포트폴리오 문서의 포함 범위는 다음과 같다.
  - 회피·청소·수집 게임플레이와 데모 흐름
  - DOTS 대량 엔티티 파이프라인
  - ownership, update order, fence, enableable 상태 전환 설계
  - UI, 피드백, 스테이지 콘텐츠를 포함한 플레이 가능한 데모 완성 과정
  - 콘텐츠 제작과 반복 작업을 지원하는 authoring/editor tooling
  - 자동 테스트, PlayMode smoke, 성능 측정, 공개 빌드 자료 중 설명 가능한 검증 근거
  - AI-assisted engineering workflow
  - 선택한 설계의 트레이드오프와 현재 한계
- 포트폴리오 문서의 비범위는 다음과 같다.
  - 상용 출시 수준의 전체 게임으로 포장하는 것
  - 최종 아트, 사운드, 콘텐츠 분량을 핵심 성과로 주장하는 것
  - 모든 코드와 시스템을 빠짐없이 설명하는 것
  - 측정하지 않은 GameObject 방식 또는 다른 프레임워크와의 성능 우위를 주장하는 것
  - 개발 중 스냅샷을 최종 공개 빌드 성능으로 표현하는 것
  - 스토어 배포와 플랫폼별 장기 품질 보증
- AI-assisted engineering workflow는 위 핵심 역량과 경쟁하는 동일 계층의 카드로 두지 않고, AI 활용 방식을 확인하려는 독자가 쉽게 발견할 수 있는 전용 심화 섹션으로 구성한다.
- AI 활용 포지셔닝의 브리프 문구는 다음을 기준으로 한다.
  > 이 프로젝트는 AI coding agent를 설계 논의, 코드베이스 탐색, 구현, 검증 실행, 문서화에 일상적인 개발 도구로 활용했습니다. 반복 과정에서 확인된 프로젝트 지식과 오류 패턴을 ownership, update order, validation rule 등의 명시적인 guardrail로 축적하여 이후 작업의 일관성을 높이는 개발 workflow를 구성했습니다. 개발자는 프로젝트 목표와 요구사항을 정의하고, 설계 채택, 플레이 감각, 공개할 결과의 범위를 판단했습니다.
- AI 활용은 방어적으로 정당화할 대상이 아니라 현대적인 개발 도구 체인의 기본 구성요소로 설명한다.
- AI 관련 guardrail은 위험한 도구를 통제한 사례가 아니라, 사람과 자동화 도구가 일관된 결과를 내도록 만든 프로젝트 수준의 엔지니어링 체계로 설명한다.
- 사람은 프로젝트 방향, 아키텍처 판단, 플레이 감각, 최종 검증 책임을 맡고, agent는 탐색, 구현, 반복 검증, 문서화의 왕복을 가속한 것으로 정리한다.
- 자동 테스트, PlayMode smoke, 성능 측정은 독립적인 핵심 역량 주장이 아니라 프로젝트 결과를 뒷받침하는 보조 증거로 취급한다.
- 테스트 자동화는 agent 제안으로 도입했으며, 문서화된 ownership/update order/runtime behavior 중 자동 관찰 가능한 계약을 후속 구현에서 다시 확인하는 2차 guardrail로 설명한다. 포괄적인 테스트 전략 수립 능력이나 테스트 자동화 전문성은 주장하지 않는다.
- 대량 엔티티 파이프라인은 기술 설계 사례로 사용하되, 장기간 여러 작업에 걸쳐 형성되어 최초 제안 주체가 불명확한 세부 요소의 역할 귀속은 주장하지 않는다.
- `BroomSweep` 개선은 개발자가 플레이 감각 문제와 개선 목표를 정의하고 최종안을 선택하며, agent가 설계 구체화·구현·자동 검증을 수행한 플레이 기반 개선 대표 사례로 사용한다.
- `StageMapEditor` 교체는 개발자가 제작 문제·편집 범위·runtime 비변경 경계·UX 후속 요구를 정의하고, agent가 document SSOT와 validation/dry-run/stale/Undo 설계를 제안·구현한 목표 기반 개발 대표 사례로 사용한다.
- `StageMapEditor`는 메인 프로젝트 페이지의 짧은 대표 보조 사례와 심화 자료로 배치한다. `HazardActorWorkbench`와 encounter preview의 세부 구현은 선택적 심화 자료로 둔다.
- 장애물 충돌 병목은 개발자가 Profiler로 지배적인 병목 시스템을 특정하고 CellMap 후보 축소를 요구한 정성적 개선 사례로 사용한다. 원본 캡처와 동일 조건 사후 측정이 없으므로 `175.12ms`, `0.024ms`, 개선 배율, 사후 FPS는 공개 성과 수치로 사용하지 않는다.
- 포괄적인 성능 엔지니어링 전문성은 주장하지 않는다. standalone Development Build 측정은 보조 근거로 사용하고, 최종 공개 후보 빌드 성능은 T3에서 별도로 확인한다.
- 개발 중 성능/테스트 스냅샷과 최종 공개 빌드 벤치마크를 구분한다.
- 과거 약 2.5만 active entity 기록은 현재 동일 테스트 이름과 재현 시나리오가 일치하지 않는 historical development snapshot이므로 최신 성능이나 회귀 보장 수치로 사용하지 않는다.
- 기존 `Bullet` 명칭의 카운터를 인용할 때는 실제로 포함하는 위험 요소와 청소/수집 대상의 범위를 오해 없이 설명한다.
- 최신 2.5만 active entity 근거는 별도 stress preset이 아니라 공개 빌드와 동일한 Stage 2 콘텐츠에서 청소를 수행하지 않고 Dust를 자연 누적시키는 통제 시나리오로 재현한다.
- Stage 2 무청소 시나리오는 최초 Dust가 lifetime에 도달한 뒤 spawn과 lifetime despawn이 균형을 이루는 plateau를 약 2.5만 active entity로 맞추는 것을 목표로 한다.
- 2.5만 근거는 일반 플레이의 상시 밀도라고 표현하지 않고, 공개 빌드에서 재현 가능한 무청소 누적 시나리오라고 명시한다. 전체 active 수와 Dust/Hazard 구성 비율을 함께 기록한다.
- 무청소 plateau의 합격 기준 초안은 평균 `22,500~27,500`, 최소 15초 유지, 동일 조건 3회 재현이다. frame time·GC·backlog·drop/expire 공개 강도는 실제 측정 후 결정한다.
- Dust lifetime은 실제 이동 거리와 도달 가능한 위치를 바꾸므로 plateau 튜닝 수단으로 우선 사용하지 않고 현재 `4초`를 유지한다.
- Stage 2 plateau는 Source cell 구성을 우선 조절해 맞춘다. Source geometry로 목표를 달성하기 어렵거나 플레이 가독성을 해치는 경우에만 spawn rate 등 다른 stage-local tuning을 후순위로 검토한다.
- lifetime을 유지하므로 무청소 시나리오의 warm-up은 최초 Dust lifetime에 맞춘 약 4초를 기준으로 시작하고, 실제 plateau 진입 시점은 런타임 측정으로 확정한다.
- 2.5만 plateau는 Stage 2의 초기 무입력 위치에서만 만족하도록 설계하며, 어느 Source를 점유해도 같은 수치를 유지하는 것을 목표로 하지 않는다.
- Stage 2 Source geometry 초안은 `1002` 면적을 `1004`의 약 3배로 두고, `1002`가 `1004`의 좌측부터 상부까지 둘러싸는 위치 관계로 구성한다. 구체 cell 수와 경계는 플레이 테스트와 plateau 측정으로 다듬는다.
- Stage 2 무입력·무청소 시나리오는 Windows Editor에서 warm-up 6초 후 15초 동안 3회 측정해 active entity 평균 `24,158.6 / 24,161.4 / 24,159.1`, 전체 범위 `24,084~24,208`을 재현했다. Drop/expire와 Spawn system 하위 `GC.Alloc` 표본은 모두 0이었다.
- 위 수치는 Windows Editor와 Profiler 오버헤드가 포함된 development profiling evidence다. standalone/public build 성능, 60fps 보장, GameObject 대비 우위로 표현하지 않는다.
- Spawn 병목은 `FreeByKey` 대여에서 `Remove(key, entity)`가 동일 TypeKey chain을 끝까지 순회한 경로로 확인했다. `TryGetFirstValue`가 반환한 iterator를 직접 제거하는 방식으로 교정한다.
- iterator 제거 후 병렬 상태 초기화 B와 직렬 상태 초기화 C를 비교한 결과 C의 Frame median/p95/Spawn median 증가가 단순화 임계치 안이었다. 최종 구현은 Pool Owner의 직렬 dequeue와 직렬 상태 초기화를 유지한다.
- Stage 2 standalone Development Build의 최초 Deep Profile Support 활성 캡처는 큰 계측 오버헤드가 확인되어 성능 근거에서 제외한다. 이후 측정은 Deep Profile Support를 비활성화한다.
- uncapped standalone 측정은 fixed Tick이 있는 frame과 없는 frame이 섞이므로 전체 평균 FPS를 ECS 성능 대표값으로 사용하지 않는다. Tick frame과 비-Tick frame의 분포를 분리해 해석한다.
- 임시 `Application.targetFrameRate = 60` 조건의 standalone 측정에서 600개 모든 frame에 pipeline이 실행됐고, frame interval median `16.670ms`, p95 `17.022ms`, max `17.402ms`, 20ms 초과 0개를 기록했다. 이는 명시한 테스트 장비와 통제 시나리오의 60fps frame budget 보조 근거다.
- 60fps cap은 ECS Tick과 GameObject Update를 같은 frame에서 관찰하기 위한 일시적 측정 조건이며 실제 데모 운영 정책으로 채택하지 않는다. 측정 후 코드는 원복한다.
- Editor의 평균 약 2.4만 active entity 직접 집계와 standalone frame time은 같은 Stage 2 콘텐츠에 대응하지만 동시 측정이 아니다. standalone active 수와 Dust/Hazard 구성 비율이 확보되기 전에는 `약 2.5만에서 60fps`를 하나의 무조건적 성능 주장으로 사용하지 않는다.
- 작업 순서는 `포트폴리오 브리프 -> 주장-근거 인벤토리 -> 공개 데모/검증 기준 -> 정보 구조 -> 저장소 문서 -> 시각 자료/데모 패키지 -> Notion -> 최종 검수`로 진행한다.

## Now
- [ ] T3. 공개 데모와 검증 기준을 확정한다.
  - 완료 기준: 대표 플레이 구간, 성능 시나리오, 측정 환경·지표, 촬영 목록, 빌드 전달 기준이 정해진다.
  - 검증: 결과를 재현하거나 최소한 측정 조건과 해석 범위를 확인할 수 있다.
  - 근거: `PORT-003`, `SESSION-20260514-01`, T2에서 확인한 증거 공백.
  - 확보: Stage 2 Editor 통제 시나리오의 active entity plateau, A/B/C spawn profiling, standalone Development Build의 uncapped 및 임시 60fps cap 측정.
  - 남음: standalone 측정과 결합한 active entity/Dust/Hazard 카운터, 완전한 환경 manifest와 반복 측정, 최종 공개 후보 빌드 smoke, 대표 캡처·영상과 구체 증거 자료 형식, 임시 60fps cap 코드 원복.

## Next
- [ ] T4. 포트폴리오 서사와 채널별 정보 구조를 확정한다.
  - 완료 기준: 문제 선택부터 설계·검증·결과·AI-assisted workflow까지의 이야기 흐름과 README/PORT/Notion의 역할이 정해진다.
  - 검증: 같은 사실을 채널별로 다르게 표현하더라도 메시지와 수치가 충돌하지 않는다.
  - 근거: T1, T2, T3 산출물.
- [ ] T5. 저장소 포트폴리오 문서를 개편한다.
  - 완료 기준: `PORT-001 -> PORT-003 -> PORT-002 -> README -> INDEX` 순으로 합의된 서사와 근거를 반영한다.
  - 검증: 링크, 명칭, 수치, 문서 간 역할, AI 활용 뉘앙스를 교차 점검한다.
  - 근거: T4에서 확정한 정보 구조.
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

## Branch Handoffs
- 2026-08-24 / T3 Stage 2 Standalone Profiling / Deep Profile Support 오버헤드 캡처를 제외하고 uncapped와 임시 60fps cap 캡처를 분리 해석, cap 측정 600 frame은 median 16.670ms·p95 17.022ms·max 17.402ms·20ms 초과 0개 / `PORT-NOTE-002`, `PORT-003`, `PORT-NOTE-001`, 본 문서 갱신 / raw Profiler capture와 marker 분포 대조 / 다음 시작점: cap 원복, standalone active 구성 직접 기록, 측정 manifest·반복 측정과 공개 증거 형식 확정.
- 2026-08-22 / T3 Stage 2 Spawn 최적화 / `FreeByKey.Remove(iterator)`를 채택하고 초기화 병렬 Job은 B/C 단순화 판정에 따라 제거, 최종 C는 A 대비 Frame median 28.46%·p95 25.60%·Spawn median 78.73% 감소 / `ADR-20260822-01`, `PORT-003`, 본 문서 갱신 / Unity Editor profiling 3회와 전체 회귀 검증 / 다음 시작점: Dust/Hazard 구성 비율과 standalone/public build 측정, 구체 증거 자료 형식 확정.
- 2026-08-21 / T3 Stage 2 Source 배치 초안 / 2.5만은 초기 무입력 위치에서만 만족하고 `1002:1004 ≈ 3:1`, `1002`가 `1004`의 좌측~상부를 둘러싸는 배치를 초기안으로 채택 / 본 문서 `Adopted Baseline` 갱신 / 사용자 결정 확인 / 다음 시작점: Deposit·Player Start·안전 동선과 Source 경계 조건 확정.
- 2026-08-21 / T3 plateau 튜닝 우선순위 / Dust lifetime `4초`를 이동 범위 계약으로 유지하고 Stage 2 Source cell 편집을 우선 사용 / 본 문서 `Adopted Baseline` 갱신 / 사용자 결정 확인 / 다음 시작점: 2.5만 목표를 초기 Source 중심으로 맞출지 양쪽 Source 점유 상태까지 맞출지 결정.
- 2026-08-21 / T3 2.5만 재현 기준 / Stage 2 무청소 자연 누적과 lifetime equilibrium으로 약 2.5만 active entity plateau를 재현하는 방향 채택 / 본 문서 `Adopted Baseline` 갱신 / 사용자 제안 채택 확인 / 다음 시작점: 현재 Dust 실효 lifetime과 Stage 2 spawn rate·plateau 확인.
- 2026-08-14 / T1 포트폴리오 브리프 / 목표 직무·문서 역할·한 문장 소개 확정 / 본 문서 `Adopted Baseline`, `Now` 갱신 / 사용자 문답으로 문구 채택 확인 / 다음 시작점: 핵심 역량 3개의 최종 표현 확정.
- 2026-08-14 / T1 핵심 역량 / 대량 엔티티·검증 가능한 아키텍처·플레이 가능한 데모 완성을 핵심 3축으로 확정하고 AI 활용은 전용 심화 섹션으로 분리 / 본 문서 `Adopted Baseline`, `Now` 갱신 / 사용자 결정 확인 / 다음 시작점: 프로젝트 범위와 비범위 확정.
- 2026-08-14 / T1 검증 자료의 주장 강도 / 자동 테스트·PlayMode smoke·성능 측정을 핵심 역량이 아닌 보조 증거로 잠정 분류하고 T2에서 이해도·설명 가능성 감사 예정 / 본 문서 `Adopted Baseline`, T2 갱신 / 사용자 우려 반영 / 다음 시작점: 범위·비범위에서 검증 자료의 위치를 확정.
- 2026-08-14 / T1 범위·비범위 / 제안된 포함 범위와 비범위를 확정하고 검증 자료는 설명 가능한 보조 증거로 제한 / 본 문서 `Adopted Baseline`, `Now` 갱신 / 사용자 승인 확인 / 다음 시작점: AI 활용 포지셔닝의 브리프 문구 확정.
- 2026-08-14 / T1 완료 / AI 활용 브리프 문구를 채택하고 목표 직무·독자·한 문장 소개·핵심 역량·범위/비범위를 모두 확정 / 본 문서 `Adopted Baseline`, `Now`, `Done` 갱신 / 사용자 완료 승인 / 다음 시작점: 별도 대화 또는 분기에서 T2 주장-근거 인벤토리 작성.
- 2026-08-20 / T2 완료 / 핵심 주장·대표 사례·테스트 및 성능 증거의 공개 강도를 확정 / 본 문서 `Adopted Baseline`, `Now`, `Done` 갱신 / 코드·ADR·TD·TaskBoard·Git 이력과 사용자 문답 대조 / 다음 시작점: T3 공개 데모와 최신 검증 기준 확정.
- 2026-08-20 / T2 원천 노트 / 개발자 관점·역할 분담·설명 가능 수준·주장 제한을 `PORT-NOTE-001`에 보존 / `Docs/Portfolio/PORT-NOTE-001-developer-perspective-and-claim-evidence.md` 생성 및 본 문서 연결 / 사용자 승인 초안과 UTF-8 내용 대조 / 다음 시작점: T3와 이후 공개 문서 작성에서 원천 자료로 참조.

## Done
- [x] T2. 주장-근거 인벤토리를 확정했다.
  - 결과: DOTS 대량 엔티티 처리, 명시적 pipeline/ownership, 플레이 가능한 데모 완성을 핵심 주장으로 유지했다.
  - 결과: fence는 심화 기술 근거, 자동 테스트는 설계 계약의 2차 guardrail, 과거 성능 수치는 development snapshot으로 분류했다.
  - 결과: 플레이 기반 `BroomSweep` 개선과 목표 기반 `StageMapEditor` 교체를 개발자 판단과 AI-assisted 실행의 대표 사례로 채택했다.
  - 결과: `StageMapEditor`는 메인 페이지의 대표 보조 사례, `HazardActorWorkbench`는 선택적 심화 자료로 배치한다.
  - 결과: 장애물 충돌 Profiler 사례는 정성적 병목 진단 사례로만 사용하고, 사후 측정 없는 정확 수치·개선 배율·성능 전문성은 주장하지 않는다.
  - 결과: 개발자 관점과 역할 분담의 상세 판단은 `PORT-NOTE-001` 원천 노트로 분리해 후속 문서 작성과 면접 준비의 참고 자료로 보존했다.
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
- 결과: T1·T2를 완료했고, T3에서 Stage 2 Editor active entity 직접 집계와 standalone Development Build frame budget 근거를 서로 구분해 확보했다.
- 남은 리스크: standalone active entity 구성의 동시 기록, 완전한 측정 manifest와 반복 측정, 최종 공개 후보 빌드 smoke, 대표 플레이 구간과 영상/GIF·빌드 전달 기준은 아직 확정되지 않았다.
- 다음 시작점: 임시 60fps cap을 원복한 뒤 T3의 남은 증거 공백과 공개 자료 형식을 확정한다.
