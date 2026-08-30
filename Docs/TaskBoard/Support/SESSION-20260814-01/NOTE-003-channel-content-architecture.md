# Public Portfolio Channel Content Architecture

> `Sweep and Dodge`의 Notion, 저장소 README, 공개 Portfolio, Validation과 Demo가 같은 사실을 서로 다른 독자 깊이로 전달하기 위한 실행 기준

## Metadata
- doc_id: `SESSION-20260814-01-NOTE-003`
- type: `SessionSupportNote`
- status: `adopted`
- audience: `internal`
- last_updated: `2026-08-30`
- related_docs:
  - [SESSION-20260814-01 Portfolio Packaging and Notion Board](../../SESSION-20260814-01-portfolio-packaging-and-notion-board.md)
  - [Project README](../../../../README.md)
  - [Public Portfolio](../../../../Portfolio/README.md)
  - [Large-Entity Pipeline Case Study](../../../../Portfolio/CaseStudies/large-entity-pipeline.md)
  - [AI-assisted Development Case Study](../../../../Portfolio/CaseStudies/ai-assisted-development.md)
  - [Validation Overview](../../../../Portfolio/Validation/README.md)
  - [Large-Entity Scenario Profiling](../../../../Portfolio/Validation/large-entity-scenario/README.md)

## 1. 목적과 권위

- 이 문서는 저장소 공개 문서와 T7 Notion 제작에서 사용할 정보 구조와 편집 기준이다.
- 포트폴리오의 채택 주장과 표현 한계는 TaskBoard의 `Adopted Baseline`을 우선한다.
- 개발자 관점과 역할 분담의 상세 배경은 `NOTE-001`, 측정 해석과 제외 기준은 `NOTE-002`를 참고한다.
- 현재 코드와 채택된 ADR·TD가 기술 사실의 우선 기준이다.
- 공개 문서는 이 내부 노트와 TaskBoard를 직접 링크하지 않는다.

2026-08-30 외부 독자 관점의 검토에서 기존 번호 중심 공개 문서 구조와 Metadata 중심 문체가 내부 운용 문서처럼 보인다는 문제가 확인됐다. 이에 따라 점진적 공개 전략은 유지하되 공개 경로, 문서명과 편집 계약을 교정했다.

## 2. 채택 전략: 공개·데모·운영 경계 분리

각 채널을 독립적인 장문 완결본으로 반복하지 않는다. 독자가 관심과 전문성에 따라 아래 단계로 이동하도록 구성한다.

```text
전체 포트폴리오
└─ Notion 프로젝트 페이지
   ├─ 프로젝트·코드 확인 → README
   ├─ 기술 심화 → Portfolio/CaseStudies
   ├─ 수치와 조건 확인 → Portfolio/Validation
   ├─ 실행·미디어 확인 → Demo (T6에서 생성)
   └─ 선택적 원천 설계 확인 → Docs/ADR, Docs/TechnicalDesign
```

- `README.md`: 저장소와 프로젝트의 외부 진입점
- `Portfolio/`: 외부 독자를 위한 기술 사례와 공개 검증 자료
- `Demo/`: T6에서 생성할 실행 안내와 공개 미디어 진입점
- `Docs/`: ADR, TD, Game Design, TaskBoard 등 개발·운영 기록

`Docs/`는 기밀 영역을 뜻하지 않는다. 공개 저장소에서 열람할 수 있지만 공식 포트폴리오 독서 경로에서는 제외되는 개발 기록이다.

## 3. 채널별 역할

| 채널 | 주 독자 | 목표 읽기 깊이 | 책임 |
|---|---|---:|---|
| Notion 프로젝트 페이지 | 채용 담당자, Unity 실무자 | 3~5분 | 대표 영상과 함께 프로젝트 전체 서사를 전달한다. |
| `README.md` | GitHub 방문자 | 1~2분 | 프로젝트, 플레이, 핵심 기술과 대표 결과를 소개하고 다음 문서로 연결한다. |
| `Portfolio/README.md` | 기술 문서 탐색자 | 30초 | Case Study와 Validation의 역할과 권장 독서 순서를 안내한다. |
| Large-Entity Pipeline Case Study | Unity 실무자, 기술 면접관 | 심화 | DOTS 대량 엔티티 파이프라인과 대표 설계 결정을 문제 해결 과정으로 설명한다. |
| AI-assisted Development Case Study | AI 활용 방식에 관심 있는 독자 | 심화 | BroomSweep과 Stage Map Editor 사례로 개발자와 Agent의 역할을 설명한다. |
| `Portfolio/Validation/README.md` | 검증 범위를 확인하는 독자 | 중간 | 성능 측정, 자동 검증과 수동 Smoke의 역할을 구분하고 상세 자료로 연결한다. |
| Large-Entity Scenario Profiling | 수치와 조건을 검토하는 독자 | 상세 | 대량 엔티티 누적 시나리오의 측정 조건, 집계표, Profiler 이미지와 해석 범위의 공개 SSOT를 제공한다. |
| `Demo/README.md` | 직접 실행하려는 독자 | 짧음 | T6부터 다운로드, 실행 방법, 조작법과 알려진 제한을 안내한다. |
| `Docs/` | 후속 개발자, 심화 검토자 | 내부 참고 | 설계 결정, 구현 계약, 운영 상태와 판단 배경을 보존한다. |

## 4. 공개 문서 편집 계약

- 공개 문서는 번호와 내부 식별자가 아니라 독자가 내용을 예상할 수 있는 제목과 파일명을 사용한다.
- Stage 번호처럼 외부 독자가 목적을 알기 어려운 콘텐츠 식별자는 대표 명칭으로 사용하지 않고, 필요한 경우 상세 조건에서 출처 정보로만 표시한다.
- `Metadata`, `doc_id`, `status`, `last_updated`, 작업 진행률과 후속 TODO를 공개 문서에 표시하지 않는다.
- 한국어 본문은 `~합니다/~했습니다` 경어체를 기본으로 한다.
- 첫 두 단락에서 프로젝트 또는 사례의 대상, 문제와 결과를 설명한 뒤 세부 기술로 진입한다.
- 기술 용어는 첫 등장 시 프로젝트에서 맡는 역할을 설명한다.
- 직접 확보하지 않은 링크와 자료는 `준비 중` Placeholder로 노출하지 않고, 실제 공개 시점에 추가한다.
- 주장 제한과 트레이드오프는 문장마다 반복하지 않고 측정 범위 또는 한계 섹션에 모은다.
- 측정하지 않은 GameObject 대비 우위, 모든 환경의 60fps와 출시 품질은 주장하지 않는다.
- Case Study는 필요한 ADR·TD만 `관련 설계 기록`으로 선택적으로 연결한다.
- README, Portfolio와 Validation에서는 TaskBoard와 세션 Support 노트를 직접 링크하지 않는다.

## 5. 공통 서사

모든 공개 채널은 다음 사실을 공유하되 채널 역할에 맞게 압축하거나 심화한다.

1. 각각 연산이 필요한 대량 개체 문제를 DOTS 학습 목표와 함께 선택했다.
2. 회피·청소·수집을 결합한 플레이 가능한 3 Stage 기술 데모를 만들었다.
3. 대량 Spawn·Despawn, 공간 후보 조회와 복수 상호작용에서 책임과 실행 순서 문제가 발생했다.
4. `ExecutionBegin → Simulation → Request → ExecutionEnd` 파이프라인과 명시적 Owner로 판정과 실행을 분리했다.
5. CellMap, Fence와 Enableable Component를 대표 설계 결정으로 사용했다.
6. 대량 엔티티 누적 시나리오의 Development Build 측정을 실행 환경의 보조 근거로 제시한다.
7. BroomSweep과 Stage Map Editor로 플레이 기반 개선과 목표·경계 기반 도구 개발을 보여준다.
8. AI coding agent를 일상적인 개발 도구로 사용하고 반복 지식을 프로젝트 계약으로 구조화했다.
9. 시청각 피드백, 반복 재미와 배포 범위를 현재 한계로 구분한다.
10. README에서 Portfolio, 이후 Demo와 Notion으로 다음 행동을 제공한다.

## 6. 채널별 구성

### README

1. 한 문장 소개와 플레이 설명
2. 프로젝트 한눈에 보기
3. 대량 개체, 아키텍처와 완결된 데모 흐름
4. 플레이 방식
5. 4단계 파이프라인과 대표 설계 요소
6. Stage 2 측정 요약과 상세 Validation 연결
7. AI-assisted Development 소개와 Case Study 연결
8. 공개 Portfolio 탐색 링크
9. 프로젝트 범위와 한계

README는 내부 상태표, 긴 변경 이력이나 공개되지 않은 빌드·영상 Placeholder를 포함하지 않는다.

### Portfolio README

- 공개 Case Study와 Validation의 목적을 한 문단씩 설명한다.
- 처음 방문한 독자에게 README부터 시작하는 권장 경로를 제공한다.
- 프로젝트 서사와 정확한 수치를 반복하지 않는다.

### Large-Entity Pipeline Case Study

1. 사례가 다루는 게임플레이 문제와 결과
2. DOTS 선택 배경
3. 4단계 Fixed Tick Pipeline
4. 판정과 실행 분리, Lifecycle Priority
5. Pool·FreeList·Render·CellMap의 소유권과 Fence
6. Enableable Component와 Query 의미
7. Iterator Dequeue 교정과 직렬 초기화 채택
8. 최신 Stage 2 보조 근거
9. 트레이드오프와 관련 설계 기록

### AI-assisted Development Case Study

1. 일상적인 개발 도구로서의 운영 방식
2. 개발자 판단과 Agent 실행의 역할 구분
3. BroomSweep 플레이 기반 개선
4. Stage Map Editor 목표·경계 기반 개발
5. 반복 지식의 프로젝트 규칙화
6. 자동 검증과 사람의 판단 범위
7. 배운 점, 현재 방식의 한계와 관련 설계 기록

### Validation

- `Portfolio/Validation/README.md`는 성능 측정, 자동 검증과 수동 Smoke의 서로 다른 역할을 설명한다.
- `Portfolio/Validation/large-entity-scenario/README.md`는 최신 3회 측정의 조건, 집계표, CPU Timeline, Entity Counter와 해석 범위를 보존한다.
- 정확한 대량 엔티티 누적 시나리오 수치의 공개 SSOT는 Large-Entity Scenario Profiling 문서다. 다른 공개 채널은 필요한 수치만 축약해 인용하고 링크한다.
- Raw Profiler Data, Frame CSV, Log와 내부 Manifest는 로컬 분석 자료로 유지한다.

### Demo

- T6에서 공개 Windows x64 압축 패키지가 준비될 때 `Demo/README.md`를 생성한다.
- 실행 방법, 조작법, 확인할 플레이, 알려진 제한과 외부 다운로드 링크를 제공한다.
- 대표 영상과 GIF는 `Demo/Media/` 또는 확정된 외부 호스팅 위치에서 연결한다.
- 빌드 바이너리는 저장소에 직접 포함하지 않는다.

## 7. 주장과 정보 라우팅

| 정보 | Notion | README | Case Study | Validation | Demo |
|---|---|---|---|---|---|
| 프로젝트 소개와 플레이 | Hero·핵심 서사 | 첫 화면·플레이 방식 | 필요한 배경만 | 반복하지 않음 | 조작·관찰 안내 |
| 4단계 Pipeline | 핵심 도식 | 축약 도식 | 상세 설명 | 통계 근거와 분리 | 관찰 포인트만 |
| Ownership·CellMap·Fence·Enableable | 대표 결정 | 핵심 Bullet | 상세 설명 | 반복하지 않음 | 반복하지 않음 |
| 대량 엔티티 누적 결과 | 결과 카드 | 핵심 수치 | 보조 근거 | 조건·표·이미지 SSOT | 필요 시 링크 |
| BroomSweep·Stage Map Editor | 대표 사례 | 짧은 소개 | AI 사례에서 상세 | 반복하지 않음 | 영상 관찰 항목 |
| AI-assisted Workflow | 전용 섹션 | 짧은 소개 | 전용 Case Study | 반복하지 않음 | 반복하지 않음 |
| 빌드와 실행 | CTA | 공개 후 링크 | 반복하지 않음 | 반복하지 않음 | 실행 안내 SSOT |
| 한계 | 회고 | 프로젝트 범위 | 주제별 Trade-off | 측정 해석 범위 | 실행 제한 |

## 8. 공개와 내부 운용 경계

### 공식 공개 독자 경로

- `README.md`
- `Portfolio/README.md`
- `Portfolio/CaseStudies/large-entity-pipeline.md`
- `Portfolio/CaseStudies/ai-assisted-development.md`
- `Portfolio/Validation/README.md`
- `Portfolio/Validation/large-entity-scenario/README.md`와 공개 이미지
- 이후 연결할 Notion과 `Demo/`

### 개발·운영 기록 경로

- `Docs/ADR/`, `Docs/TechnicalDesign/`, `Docs/GameDesign/`
- `Docs/ProjectOps/`, `Docs/TaskBoard/`와 세션 Support 노트
- 로컬 `ProfilerCaptures/`와 `Builds/`의 Raw 자료

공개 Case Study는 설명에 필요한 일부 ADR·TD만 선택적으로 연결한다. TaskBoard, Support 노트와 로컬 Raw 자료는 공식 독자 경로에 포함하지 않는다.

## 9. 저장소 공개 문서 개편 완료 조건

- 기존 번호 중심 공개 구조를 제거하고 `Portfolio/`의 주제 중심 경로를 사용한다.
- 모든 공개 문서에서 내부 Metadata와 진행 상태 표현을 제거한다.
- README와 Case Study가 외부 독자에게 프로젝트와 문제를 먼저 소개하고 경어체를 사용한다.
- Stage 2 수치와 해석 범위가 상세 Profiling SSOT와 일치한다.
- 공개 문서에서 TaskBoard와 Support 노트를 직접 참조하지 않는다.
- 이전 공개 경로와 문서 식별자 참조, 깨진 상대 링크와 UTF-8 Replacement Character가 남지 않는다.
- `Demo/`는 T6 공개 자료가 실제 준비될 때 생성한다.
