# Portfolio Channel Content Architecture

> `Sweep and Dodge`의 README, `PORT-*`, 공개 Evidence, Notion이 같은 사실을 서로 다른 독자 깊이로 전달하기 위한 T4 실행 기준

## Metadata
- doc_id: `SESSION-20260814-01-NOTE-003`
- type: `SessionSupportNote`
- status: `adopted`
- audience: `internal`
- last_updated: `2026-08-28`
- related_docs:
  - [SESSION-20260814-01 Portfolio Packaging and Notion Board](../../SESSION-20260814-01-portfolio-packaging-and-notion-board.md)
  - [README](../../../../README.md)
  - [Portfolio Index](../../../Portfolio/INDEX.md)
  - [PORT-001](../../../Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md)
  - [PORT-002](../../../Portfolio/PORT-002-ai-assisted-engineering-workflow.md)
  - [PORT-003](../../../Portfolio/PORT-003-validation-report.md)
  - [Stage 2 Standalone Profiling Evidence](../../../Portfolio/Evidence/Stage2-Profiling/README.md)

## 1. 목적과 권위

- 이 문서는 T5 저장소 문서 개편과 T7 Notion 제작에서 사용할 채널별 정보 구조의 실행 기준이다.
- 포트폴리오의 채택 주장과 표현 한계는 TaskBoard의 `Adopted Baseline`을 우선한다.
- 개발자 관점과 역할 분담의 상세 배경은 `NOTE-001`, 측정 해석과 제외 기준은 `NOTE-002`를 참고한다.
- 현재 코드와 채택된 ADR·TD가 기술 사실의 우선 기준이다.
- 공개 문서는 이 내부 노트를 직접 링크하지 않는다.

## 2. 채택 전략: 점진적 공개

각 채널을 독립적인 장문 완결본으로 반복하지 않는다. 독자가 관심과 전문성에 따라 아래 단계로 내려가도록 구성한다.

```text
전체 포트폴리오
└─ Notion 프로젝트 페이지
   ├─ 실행·코드 확인 → README / 공개 빌드
   ├─ 핵심 기술 심화 → PORT-001
   ├─ AI-assisted workflow 심화 → PORT-002
   ├─ 검증·실행 안내 → PORT-003
   └─ 수치 상세 확인 → 공개 Evidence
```

Notion은 채용 독자를 위한 주 서사, README는 저장소 진입점, `PORT-*`는 선택적 심화 문서, 공개 Evidence는 주장과 수치의 확인 계층이다.

## 3. 채널별 역할

| 채널 | 주 독자 | 목표 읽기 깊이 | 책임 |
|---|---|---:|---|
| Notion 프로젝트 페이지 | 채용 담당자, Unity 실무자 | 3~5분 | 대표 영상과 함께 프로젝트 전체 서사를 전달한다. |
| `README.md` | GitHub 방문자 | 1~2분 | 결과·기술·증거를 빠르게 요약하고 코드·빌드·심화 문서로 연결한다. |
| `Docs/Portfolio/INDEX.md` | 문서 탐색자 | 30초 | 공개 문서의 역할과 탐색 경로만 안내한다. |
| `PORT-001` | Unity 실무자, 기술 면접관 | 심화 | DOTS 대량 엔티티 파이프라인과 대표 설계 결정을 설명한다. |
| `PORT-002` | AI 활용 방식에 관심 있는 독자 | 심화 | 실제 사례를 통해 개발자와 agent의 역할, 반복 workflow, guardrail을 설명한다. |
| `PORT-003` | 빌드·검증 확인 독자 | 중간 | 데모 관찰법, 최신 측정 요약, 공개 Evidence와 빌드 전달 경로를 안내한다. |
| 공개 Evidence | 수치와 조건을 검토하는 독자 | 상세 | 측정 조건, 집계표, 판독 가능한 캡처와 해석 한계를 보존한다. |
| 세션 Support 노트 | 후속 작업자, 면접 준비 | 내부 참고 | 판단 배경, 역할 분담, 원시 분석과 채널 설계를 복원한다. |

## 4. 공통 서사

모든 공개 채널은 다음 서사를 공유하되, 채널 역할에 따라 압축하거나 심화한다.

1. DOTS 학습 목표에 맞춰 각각 연산이 필요한 대량 개체 문제를 선택했다.
2. 회피·청소·수집을 결합한 플레이 가능한 3 Stage 기술 데모를 만들었다.
3. 대량 spawn/despawn, 충돌 후보 조회, 복수 상호작용, 상태 전환에서 책임과 실행 순서 문제가 발생했다.
4. `ExecutionBegin → Simulation → Request → ExecutionEnd` 파이프라인과 명시적 owner로 판정과 실행을 분리했다.
5. CellMap, fence, enableable component를 대표 설계 결정으로 사용했다.
6. Stage 2 통제 시나리오의 Development Build 측정을 설계가 실제 실행 환경에서 동작한 보조 근거로 제시한다.
7. `BroomSweep`과 `StageMapEditor`로 플레이 기반 개선과 목표·경계 기반 도구 개발을 보여준다.
8. AI coding agent를 일상적인 개발 도구로 사용하고 프로젝트 지식과 반복 오류를 guardrail로 구조화한 workflow를 설명한다.
9. 시청각 피드백, 게임 재미, 공개 Release Build 등 현재 범위 밖의 한계를 명시한다.
10. 빌드, 저장소, 기술 문서와 Evidence로 다음 행동을 제공한다.

## 5. 채널별 섹션 청사진

### 5.1 Notion 프로젝트 페이지

1. Hero 영상, 프로젝트명, 한 문장 소개
2. 역할·기술·프로젝트 범위 요약
3. 핵심 플레이 루프와 3 Stage 데모 흐름
4. 해결한 기술 문제와 4단계 파이프라인
5. ownership, CellMap/fence, enableable의 대표 결정
6. Stage 2 검증 결과 카드와 공개 Evidence 연결
7. `BroomSweep` 플레이 기반 개선 사례
8. `StageMapEditor` 목표 기반 도구 개발 사례
9. AI-assisted engineering workflow
10. 트레이드오프, 현재 한계와 회고
11. 빌드·GitHub·심화 기술 문서 링크

Notion은 하나의 스크롤형 프로젝트 페이지를 기본으로 한다. 긴 기술 설명을 별도 Notion 하위 페이지로 복제하지 않고 `PORT-*`와 공개 Evidence로 연결한다.

### 5.2 README

1. 한 문장 소개와 기술 데모 포지셔닝
2. 대표 영상 또는 이미지 슬롯
3. 플레이 루프와 결과를 포함한 빠른 프로젝트 요약
4. 핵심 역량 3개
5. 4단계 파이프라인 도식과 대표 설계 결정
6. 최신 Stage 2 측정 한 줄 요약과 해석 범위
7. 공개 빌드, Notion, `PORT-*`, Evidence 링크
8. 현재 범위와 알려진 한계

README는 역사적 테스트 수치와 장문의 AI 설명을 전면에 두지 않는다. AI-assisted workflow는 짧은 소개와 `PORT-002` 링크로 발견 가능하게 만든다.

### 5.3 Portfolio Index

- 공개 문서와 Evidence의 역할을 한 줄씩 설명하는 탐색 지도만 제공한다.
- 프로젝트 서사, 상세 수치, 내부 원천 노트를 반복하지 않는다.

### 5.4 PORT-001: DOTS Large-Entity Pipeline Case Study

1. 기술 선택 배경과 문제 제약
2. 4단계 fixed-tick pipeline 개요
3. 판정과 실행 분리, lifecycle reason 병합
4. Pool/FreeList와 render toggle ownership
5. CellMap writer와 fence dependency
6. Enableable 기반 상태 전환과 query 의미
7. iterator dequeue 교정과 직렬 초기화 채택을 포함한 측정 기반 단순화 사례
8. 최신 Stage 2 보조 근거 연결
9. 트레이드오프와 현재 한계

최적화 비교는 결정과 대표 수치만 설명하고 raw run 분석은 반복하지 않는다. 상세 설계는 ADR, 공개 성능 조건은 `PORT-003`과 Evidence로 연결한다.

### 5.5 PORT-002: AI-assisted Engineering Workflow

1. AI coding agent를 일상적인 개발 도구로 사용한 운영 모델
2. 개발자가 목표·제약·경계·선택·플레이 판단을 담당하는 기준
3. `BroomSweep` 플레이 기반 개선 사례
4. `StageMapEditor` 목표·경계 기반 도구 개발 사례
5. agent의 탐색·대안·계획·구현·검증 역할
6. 프로젝트 지식과 반복 오류 패턴의 guardrail화
7. 자동 검증과 사람의 감각 판단이 담당한 서로 다른 범위
8. 컨텍스트 비용과 현재 workflow의 한계

일반적인 기능 목록이나 AI 사용을 방어하는 서술보다 두 대표 사례와 실제 역할 분담을 중심으로 쓴다.

### 5.6 PORT-003: Demo Build and Validation Guide

1. 데모 빌드에서 확인할 플레이와 기술 요소
2. 공개 자료를 함께 읽는 방법
3. 최신 Stage 2 통제 시나리오와 3-run 결과 요약
4. Development Build·uncapped·fixed Tick 해석 경계
5. 공개 Evidence, 영상과 빌드 링크
6. Windows x64 실행 안내와 알려진 제한

역사적 snapshot은 최신 결과와 혼동되지 않는 짧은 배경으로만 남긴다. Editor A/B/C 비교와 run별 상세 분석은 PORT-003의 주 서사에서 제외하고, 기술 결정은 `PORT-001`·ADR, 상세 측정은 공개 Evidence가 담당한다.

### 5.7 공개 Evidence

- `Docs/Portfolio/Evidence/INDEX.md`는 공개 가능한 Evidence만 안내한다.
- `Stage2-Profiling/README.md`는 최신 3-run 조건·집계표·CPU Timeline·composition counter와 해석 한계를 보존한다.
- raw Profiler `.data`, frame CSV, 로그, 내부 manifest와 편집 전 영상은 로컬에 유지한다.
- 세션 Support 노트를 공개 Evidence Index에서 연결하지 않는다.

## 6. 주장과 정보 라우팅

| 정보 | Notion | README | PORT 문서 | 공개 Evidence |
|---|---|---|---|---|
| 프로젝트 한 문장 소개 | Hero | 첫 문단 | 필요 시 동일 표현 | 반복하지 않음 |
| 플레이 가능한 3 Stage 데모 | 핵심 서사·영상 | 짧은 요약 | `PORT-003` 관찰 안내 | 필요 시 실행 근거만 |
| 4단계 pipeline | 핵심 도식 | 축약 도식 | `PORT-001` 상세 | 통계 근거로 사용하지 않음 |
| ownership·CellMap·fence·enableable | 대표 결정 | 핵심 bullet | `PORT-001` 상세 | 측정 자료와 분리 |
| Stage 2 성능 결과 | 결과 카드 | 한 줄 snapshot | `PORT-003` 요약 | 조건·표·캡처 SSOT |
| BroomSweep | 대표 사례 | 선택적 한 줄 | `PORT-002` 상세 | 별도 수치 근거 없음 |
| StageMapEditor | 대표 보조 사례 | authoring/tooling 한 줄 | `PORT-002` 상세 | 영상은 이후 연결 |
| AI-assisted workflow | 전용 섹션 | 짧은 소개·링크 | `PORT-002` SSOT | 반복하지 않음 |
| 빌드와 실행 안내 | CTA | Quick start | `PORT-003` 상세 | raw 자료 제외 기준만 |
| 트레이드오프와 한계 | 회고 | 짧은 범위 표기 | 각 문서 주제별 상세 | 측정 해석 한계만 |

## 7. 중복과 인용 규칙

- 같은 사실은 채널별로 표현 깊이만 바꾸고 의미, 명칭, 단위와 수치를 바꾸지 않는다.
- 최신 Stage 2 정확 수치의 공개 SSOT는 `Evidence/Stage2-Profiling/README.md`다. 다른 공개 채널의 숫자는 이 값을 축약해 인용하고 링크한다.
- `PORT-003`은 측정 결과를 안내하지만 raw 분석 문서 역할을 맡지 않는다.
- 역사적 약 2.5만 snapshot은 최신 공개 성능처럼 사용하지 않는다.
- Development Build 결과를 최종 Release Build 또는 모든 플레이 상황의 60fps 보장으로 확장하지 않는다.
- GameObject 방식과 직접 비교하지 않는다.
- AI 활용은 방어적으로 정당화하지 않고 개발자와 agent의 실제 역할 분담을 구체 사례로 설명한다.
- README, Portfolio Index, `PORT-*`, 공개 Evidence는 세션 Support 노트를 직접 링크하지 않는다.

## 8. 공개와 내부 운용 경계

### 공식 공개 독자 경로

- `README.md`
- `Docs/Portfolio/INDEX.md`
- `PORT-001~003`
- `Docs/Portfolio/Evidence/INDEX.md`
- `Docs/Portfolio/Evidence/Stage2-Profiling/README.md`와 공개 이미지
- 이후 연결할 Notion, 영상과 Windows x64 압축 빌드

### 내부 운용·기록 경로

- `Docs/TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md`
- `Docs/TaskBoard/Support/SESSION-20260814-01/`
- 로컬 `ProfilerCaptures/`와 `Builds/`의 raw 자료

내부 운용 문서는 공개 저장소를 직접 탐색하면 보일 수 있지만 공식 포트폴리오 탐색 경로에서는 제외한다. 기밀을 의미하지 않는다.

## 9. T5 저장소 문서 개편 계약

개편 순서는 `PORT-001 → PORT-003 → PORT-002 → README → Portfolio INDEX`로 유지한다.

T5 완료 조건은 다음과 같다.

- 각 문서가 본 문서의 채널 역할과 섹션 청사진을 따른다.
- README와 `PORT-*`에서 내부 Support 노트 참조가 없다.
- Stage 2 수치가 공개 Evidence와 일치하고 측정 조건 및 주장 제한이 함께 보인다.
- `PORT-001`은 핵심 기술 사례, `PORT-002`는 사례 중심 AI workflow, `PORT-003`은 빌드·검증 안내 역할로 구분된다.
- 현재 결과가 `in progress` 또는 `partial snapshot`으로 남아 있는 오래된 상태 표현을 최신 기준으로 보정한다.
- Notion과 T6 산출물이 아직 없는 경우 링크나 영상 슬롯을 사실처럼 만들지 않고 명시적인 후속 자리로 남긴다.
- 상대 링크, 명칭, 날짜, 수치와 공개/내부 탐색 경계를 검사한다.

