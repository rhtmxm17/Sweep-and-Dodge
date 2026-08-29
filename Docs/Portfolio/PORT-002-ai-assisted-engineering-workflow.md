# AI-assisted Engineering Workflow

> 목표와 제약은 개발자가 결정하고, AI coding agent가 탐색·구현·검증의 왕복을 가속한 실제 개발 사례

## Metadata
- doc_id: `PORT-002`
- type: `Portfolio`
- status: `draft`
- last_updated: `2026-08-29`
- related_docs:
  - [DOTS Large-Entity Pipeline Case Study](PORT-001-dots-large-entity-pipeline-case-study.md)
  - [BroomSweep default cleanup action](../ADR/ADR-20260402-01-broomsweep-default-cleanup-action.md)
  - [Player cleanup action runtime contract](../TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md)
  - [Stage Map Editor document SSOT](../ADR/ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)
  - [Stage Map Editor replacement](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)

## 1. 운영 모델

이 프로젝트는 AI coding agent를 설계 논의, 코드베이스 탐색, 대안 비교, 구현, 검증 실행과 문서화에 일상적인 개발 도구로 사용했다.

AI 활용을 별도의 자동 코드 생성 단계로 분리하지 않았다. 개발자가 해결할 문제와 범위를 정하면 agent가 기존 코드와 문서를 탐색하고 실행 가능한 대안을 제안했다. 개발자는 선택이 필요한 지점에서 방향을 결정했고, agent는 승인된 계약을 코드와 검증으로 옮겼다. 이후 플레이 감각이나 편집 workflow처럼 자동 검증으로 판단할 수 없는 결과는 개발자가 직접 사용하며 조정했다.

```text
문제 발견 또는 목표 정의
→ 기존 코드·문서 탐색
→ 대안과 제약 비교
→ 개발자가 방향 선택
→ agent가 계획·구현·자동 검증
→ 개발자가 플레이·사용성 재검증
→ 발견한 지식을 다음 작업 규칙에 반영
```

## 2. 역할과 책임

| Developer | AI coding agent |
|---|---|
| 프로젝트 목표와 우선순위 결정 | 관련 코드·문서·테스트 탐색 |
| 요구사항, 비범위와 변경 금지 경계 정의 | 구현 대안과 파급 범위 제안 |
| 여러 설계안 중 최종안 선택 | 승인된 설계를 실행 계획과 코드로 변환 |
| 플레이 감각, UI 가독성, 제작 workflow 판단 | compile, EditMode, PlayMode smoke 등 반복 검증 |
| 공개할 주장과 결과의 해석 범위 결정 | 변경 내용과 검증 결과 문서화 |

대량 엔티티 파이프라인처럼 여러 작업을 거치며 점진적으로 형성된 구조는 세부 요소의 최초 제안 주체를 임의로 귀속하지 않는다. 대신 개발자가 어떤 문제와 경계를 이해하고 설명할 수 있는지, agent와 어떤 방식으로 결정을 실행했는지를 구체 사례로 제시한다.

## 3. 플레이 기반 개선: BroomSweep

### 문제 발견

기존 청소 동작은 기능적으로 Dust를 제거했지만, 실제 플레이에서는 현실의 빗자루질과 연결되지 않는 기계적인 동작으로 느껴졌다. 자동 테스트만으로 발견하기 어려운 조작 감각 문제였다.

### 개발자가 정한 목표와 제약

- 좌우 스윕을 교대로 실행
- 스윕 중 이동 속도 제한
- 스윕 도중 방향을 크게 바꿔 비정상적으로 넓은 영역을 청소하지 못하도록 방향 잠금
- 구현 후 직접 플레이하며 시작 각도와 조작 감각 조정

개발자는 문제와 주요 행동 제약을 제시하고 agent에게 추가 대안을 요청했다. agent는 설계 문서와 구현 계획을 작성하고, 선택이 필요한 지점을 확인받은 뒤 코드와 자동 검증을 수행했다.

### 결과

스윕은 좌우 방향을 교대하고, 활성 구간에는 이동과 방향 전환 제약을 적용한다. 구현 후 개발자가 다시 플레이하면서 자동 테스트가 판단할 수 없는 시작 각도의 어색함을 조정했다.

이 사례에서 중요한 것은 누가 모든 코드를 직접 작성했는지가 아니다. 플레이 문제 발견과 최종 감각 판단은 개발자가, 설계 구체화와 구현·반복 검증은 agent가 담당하는 협업 루프를 만들었다는 점이다.

관련 런타임 계약은 [TD-012](../TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md)에서 확인할 수 있다.

## 4. 목표·경계 기반 개발: Stage Map Editor

### 기존 제작 방식의 문제

기존 Scene·Tilemap·Marker 기반 제작 방식은 프로젝트 전용 규칙과 작업 순서를 기억해야 했다. 원하는 데이터를 찾기 어렵고, 전체 맵 구조와 HazardActor 진행 상태를 미리 확인하기도 어려웠다.

### 개발자가 정의한 범위

- 전용 Editor Window와 Scene View Tool을 함께 사용
- 이동 가능 영역, Source, Deposit과 Player Start·HazardActor·Anchor 편집
- 기존 Stage 1~3 데이터 migration
- HazardActor encounter 진행 상태 preview
- 기존 runtime asset과 ECS 계약은 변경하지 않음
- HazardActor archetype과 발사 pattern 편집은 별도 도구로 유지

### Agent 제안과 선택

agent는 제작 입력을 하나의 `StageMapDocument`로 모으고, validation issue 이동, dry-run diff 후 Apply, stale plan 거부, Undo 가능한 적용 절차를 제안했다. 개발자는 이 구조를 선택하고 runtime 비변경 경계를 유지하도록 했다.

```text
StageMapDocument
→ validation
→ dry-run diff
→ Apply
→ StageLayoutSO / StageDefinitionSO / StageCatalogSO
→ 기존 runtime ECS
```

새 document는 runtime 구조를 교체하지 않는다. 제작자가 편집하는 입력을 통합하고 기존 runtime asset을 출력으로 유지한다.

구현 후 개발자가 실제 도구를 사용하면서 palette를 라디오 버튼으로 바꾸고, 목록을 표 형태로 정리하며, Source 진행도에 따른 encounter preview를 추가하도록 후속 UX를 조정했다.

이 사례는 agent가 기능을 대신 결정한 것이 아니라, 개발자가 문제·범위·비변경 경계와 실제 사용 결과를 판단하고 agent가 구조적 대안·구현·migration·검증을 실행한 목표 기반 workflow를 보여준다.

## 5. 프로젝트 지식의 guardrail화

작업이 반복되면서 문서와 코드에 존재하는 프로젝트 지식이 agent의 다음 작업에서도 일관되게 적용되어야 했다. 반복해서 발견된 오류 패턴과 설계 결정을 다음과 같은 명시적 규칙으로 축적했다.

- Request 단계는 직접 despawn이나 render toggle을 수행하지 않는다.
- Simulation만 CellMap writer가 된다.
- Pool/FreeList의 dequeue와 enqueue owner를 분리하지 않는다.
- ECS 밖 Native container 접근에는 fence dependency를 결합한다.
- enableable request의 query 의미를 enabled/disabled 상태에 맞게 명시한다.
- sample asset의 예시값을 근거 없는 snapshot test로 승격하지 않는다.

이 guardrail은 사람과 자동화 도구가 같은 프로젝트 계약을 반복해서 적용할 수 있도록 지식을 구조화한 엔지니어링 체계다.

## 6. 자동 검증과 사람의 판단

Agent는 compile, Console 확인, EditMode 계약 테스트와 PlayMode smoke처럼 반복 실행 가능한 검증을 담당하기 좋았다. 이 프로젝트에서 테스트는 문서화된 설계 계약 중 자동으로 관찰할 수 있는 일부를 후속 변경에서 다시 확인하는 2차 guardrail로 사용했다.

반면 다음 항목은 사람이 직접 판단했다.

- 조작감과 행동의 자연스러움
- UI 가독성과 화면 구성
- 렌더링과 연출 강도
- 콘텐츠 제작 도구의 발견성과 workflow
- 포트폴리오에서 공개할 결과와 주장 강도

테스트 자동화 전문성이나 테스트 주도 개발을 이 프로젝트의 성과로 주장하지 않는다. 자동 검증과 사람의 판단이 서로 다른 범위를 담당하도록 작업 흐름에 배치한 경험을 설명한다.

## 7. 비용과 현재 한계

AI coding agent를 넓은 범위에 사용할수록 참조해야 할 코드·설계·테스트·문서가 늘어나며 컨텍스트 비용도 커졌다. 긴 작업에서는 오래된 전제, 중복 구현, 과도한 범위 제안이 생길 수 있었고 이를 바로잡는 왕복에도 시간이 들었다.

이를 줄이기 위해 다음 방식을 사용했다.

- 작업 주제와 성공 조건을 먼저 작게 정의
- 설계와 구현을 분리하고 변경 전 승인 범위를 명시
- TaskBoard와 Support note로 장기 결정과 분기 결과를 공유
- 코드·문서·검증에서 반복되는 계약을 프로젝트 지침으로 승격
- 자동 검증으로 판단할 수 없는 항목은 직접 플레이하거나 도구를 사용해 확인

현재 workflow가 모든 1차 구현을 오류 없이 만드는 것은 아니다. 핵심 가치는 개발자가 판단해야 할 영역과 agent가 가속할 수 있는 영역을 구분하고, 반복 과정의 지식을 다음 작업에 재사용한 데 있다.
