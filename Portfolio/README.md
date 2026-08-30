# Sweep and Dodge 기술 포트폴리오

이 폴더에는 `Sweep and Dodge`를 만들면서 해결한 기술 문제와 그 결과를 외부 독자가 살펴볼 수 있도록 정리했습니다.

프로젝트를 처음 접했다면 먼저 저장소의 [README](../README.md)를 읽어 주세요. 각 문서는 README에서 소개한 내용을 기술적 관심사에 따라 더 깊게 설명합니다.

## 기술 사례

### [DOTS로 대량 엔티티의 생명주기를 설계한 과정](CaseStudies/large-entity-pipeline.md)

대량의 Dust와 Hazard를 생성하고 이동시키며 다시 풀로 반환하는 과정에서, 판정과 실행을 분리하고 데이터의 소유권과 작업 순서를 명확하게 만든 사례입니다.

다음 내용을 다룹니다.

- `ExecutionBegin → Simulation → Request → ExecutionEnd` 파이프라인
- Pool·FreeList와 렌더 상태의 소유권
- CellMap과 Fence를 이용한 공간 탐색 및 작업 의존성
- Enableable Component를 이용한 상태 전환
- 프로파일링 결과를 바탕으로 Spawn 경로를 단순화한 과정

### [AI coding agent와 함께 기능을 설계하고 검증한 방식](CaseStudies/ai-assisted-development.md)

AI coding agent를 별도의 코드 생성 단계가 아니라 일상적인 개발 도구로 사용한 방식을 두 가지 실제 사례를 통해 설명합니다.

- 플레이 감각을 직접 확인하며 개선한 BroomSweep
- 제작 문제와 변경 금지 경계를 먼저 정의한 Stage Map Editor
- 사람의 판단과 agent의 탐색·구현·검증 역할 분담
- 반복해서 발견한 프로젝트 지식을 개발 규칙으로 축적한 과정

## 검증 자료

### [검증 자료 안내](Validation/README.md)

공개 측정 자료가 어떤 주장을 뒷받침하고 어디까지 해석할 수 있는지 설명합니다. 성능 수치뿐 아니라 자동 검증과 수동 플레이 확인이 담당한 범위도 함께 구분합니다.

### [대량 엔티티 누적 시나리오 프로파일링 결과](Validation/large-entity-scenario/README.md)

Windows standalone Development Build에서 Dust를 청소하지 않고 누적하는 시나리오를 동일 조건으로 3회 측정한 결과와 Profiler 이미지를 제공합니다. 정확한 측정 조건과 수치의 기준 문서입니다.

## 문서를 읽는 순서

- 프로젝트와 플레이를 빠르게 파악하려면 [저장소 README](../README.md)에서 시작해 주세요.
- DOTS 구조와 대표 설계 결정을 검토하려면 [대량 엔티티 생명주기 사례](CaseStudies/large-entity-pipeline.md)를 읽어 주세요.
- AI coding agent와 역할을 나눈 방식이 궁금하다면 [AI-assisted development 사례](CaseStudies/ai-assisted-development.md)를 읽어 주세요.
- 공개 성능 수치의 조건과 한계를 확인하려면 [대량 엔티티 누적 시나리오 프로파일링 결과](Validation/large-entity-scenario/README.md)를 확인해 주세요.

ADR과 Technical Design 등 개발 과정의 상세 기록은 `Docs/`에 보존되어 있습니다. 해당 기록은 공식 포트폴리오 독서 순서에는 포함하지 않으며, Case Study를 읽는 데 도움이 되는 일부 결정만 선택적으로 연결합니다.
