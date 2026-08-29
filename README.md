# Sweep and Dodge

> Unity DOTS/Entities technical portfolio demo for large-scale entity handling.

`Sweep and Dodge`는 회피·청소·수집 플레이 루프에 Unity DOTS/Entities 기반 대량 엔티티 파이프라인을 적용하고, 명시적인 소유권과 업데이트 순서로 시스템을 구성한 플레이 가능한 기술 데모입니다.

## Project Overview

| Item | Description |
|---|---|
| Type | Unity 클라이언트 개발자 포트폴리오용 playable technical demo |
| Core loop | 위험 탄환 회피, Dust 청소·수집, Deposit 복귀, Source 고갈 |
| Demo flow | `Title → Lobby → Stage → Result → Demo Complete` |
| Technology | Unity 6000.3.6f1, Entities/DOTS, C# |
| Platform | Windows PC |
| Current state | 3 Stage 데모와 공개 Evidence 구성 완료, 대표 영상과 공개 압축 빌드 준비 중 |

## What the Demo Shows

플레이어는 위험 탄환을 피하면서 Source 영역의 Dust를 쓸어 수거합니다. Carry가 차면 Deposit으로 돌아가 비우고, 아직 활성인 Source로 이동해 청소를 이어갑니다. 위험 탄환은 더 정밀한 스윕 타이밍으로 직접 제거할 수도 있습니다.

대량 Entity를 별도의 stress scene에만 배치하지 않고, Stage 선택·성공·실패·재시도와 최종 완료가 있는 플레이 흐름 안에서 동작하도록 구성했습니다.

## Technical Focus

### 1. DOTS 기반 대량 엔티티 처리

- Pool/FreeList 기반 spawn/despawn
- CellMap을 통한 공간 후보 조회
- Enableable component 기반 상태와 요청 전환
- 대량 데이터 순회를 Job으로 스케줄링

### 2. 검증 가능한 아키텍처

- 시스템별 writer와 owner 구분
- fixed Tick의 명시적 실행 단계
- ECS 밖 Native container 접근을 fence dependency로 연결
- 판정과 실행을 분리한 lifecycle request와 priority 병합

### 3. 플레이 가능한 기술 데모

- 회피·청소·수집을 연결한 핵심 플레이 루프
- Title, Lobby, 3 Stage, Result, Demo Complete 흐름
- UI·피드백·스테이지 콘텐츠와 DOTS runtime 연결
- Stage Map Editor를 포함한 authoring/editor tooling

## Fixed-tick Pipeline

```text
ExecutionBegin → Simulation → Request → ExecutionEnd
```

| Stage | Responsibility |
|---|---|
| `ExecutionBegin` | spawn request 소비, Pool dequeue, 활성 상태 초기화 |
| `Simulation` | 이동·수명 갱신, block 판정, CellMap build |
| `Request` | 플레이어 피격·청소 등 상호작용 판단과 lifecycle request 생성 |
| `ExecutionEnd` | lifecycle reaction, 비활성화, render off, Pool enqueue |

Request와 Simulation은 Entity를 즉시 풀에 반환하지 않습니다. 같은 Tick에 복수 사건이 발생하면 lifecycle priority를 병합하고, 실제 비활성화와 반환은 `ExecutionEnd` owner가 수행합니다.

상세 설계는 [DOTS Large-Entity Pipeline Case Study](Docs/Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md)에서 확인할 수 있습니다.

## Stage 2 Measurement Snapshot

최신 게임플레이 비주얼을 포함한 Windows standalone Development Build에서 Stage 2 무입력·무청소 plateau를 동일 조건으로 600 frame씩 3회 기록했습니다.

- Active Total 평균: `24,148.3`
- Active Total 범위: `24,077–24,236`
- Frame interval median/p95/max: `7.291/9.249/12.872ms`
- `16.67ms` 초과 interval: `0/1,797`

이 결과는 명시한 테스트 장비와 통제 시나리오의 Development Build 측정입니다. 일반 플레이의 상시 밀도, 모든 환경의 60fps, 최종 Release Build 성능 또는 GameObject 방식 대비 우위를 의미하지 않습니다.

[측정 조건, 전체 표와 Profiler 이미지 보기](Docs/Portfolio/Evidence/Stage2-Profiling/README.md)

## AI-assisted Development

AI coding agent를 설계 논의, 코드베이스 탐색, 대안 비교, 구현, 반복 검증과 문서화에 일상적인 개발 도구로 사용했습니다.

개발자는 프로젝트 목표와 제약, 설계 채택, 플레이 감각과 공개 범위를 판단했습니다. Agent는 관련 맥락 탐색, 실행 계획, 코드 구현과 자동 검증의 왕복을 가속했습니다. 반복해서 발견한 프로젝트 지식과 오류 패턴은 ownership, update order, validation rule 같은 명시적 guardrail로 구조화했습니다.

BroomSweep 플레이 개선과 Stage Map Editor 개발의 구체적인 역할 분담은 [AI-assisted Engineering Workflow](Docs/Portfolio/PORT-002-ai-assisted-engineering-workflow.md)에 정리했습니다.

## Portfolio Documents

- [Portfolio Index](Docs/Portfolio/INDEX.md): 공개 문서 탐색 지도
- [DOTS Large-Entity Pipeline Case Study](Docs/Portfolio/PORT-001-dots-large-entity-pipeline-case-study.md): pipeline, ownership, fence, enableable과 측정 기반 단순화
- [AI-assisted Engineering Workflow](Docs/Portfolio/PORT-002-ai-assisted-engineering-workflow.md): BroomSweep과 Stage Map Editor 중심의 실제 협업 사례
- [Demo and Validation Guide](Docs/Portfolio/PORT-003-validation-report.md): 데모 관찰법, 최신 측정 요약과 공개 자료 상태
- [Stage 2 Profiling Evidence](Docs/Portfolio/Evidence/Stage2-Profiling/README.md): 측정 조건, 집계 결과와 판독 가능한 캡처

## Current Scope and Limitations

- 플레이 가능한 3 Stage 기술 데모와 최신 Stage 2 공개 Evidence는 준비되어 있습니다.
- 대표 플레이·BroomSweep·Stage Map Editor 영상의 촬영과 최종 호스팅은 후속 작업입니다.
- Windows x64 공개 압축 패키지와 실행 안내, 최종 전달 smoke는 아직 준비 중입니다.
- 청소·피격·위험 탄환 제거의 시청각 피드백과 장기적인 게임 재미는 출시 후보 수준을 목표로 하지 않았습니다.
- 스토어 배포, 모든 입력 장치 지원, 플랫폼별 장기 벤치마크는 현재 범위가 아닙니다.
