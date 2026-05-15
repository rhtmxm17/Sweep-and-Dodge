# Sweep and Dodge

> Unity DOTS/Entities technical portfolio demo for large-scale bullet simulation.

`Sweep and Dodge`는 Unity DOTS/Entities 기반으로 대량 탄환 처리, 청소/수집 루프, 명시적 업데이트 파이프라인을 실험하는 포트폴리오용 기술 데모입니다.

## Highlights

- ECS/DOTS 워크플로우 학습과 검증을 목표로 시작한 기술 데모
- GameObject 중심 구조에서 비용이 커지는 대량 탄환/대량 상태 전환 문제를 의도적으로 선택
- Unity DOTS/Entities 기반 대량 탄환 처리
- `ExecutionBegin -> Simulation -> Request -> ExecutionEnd` 프레임 파이프라인
- Pool/FreeList 기반 spawn/despawn 실행 경계
- SpatialHash/CellMap writer 단일화와 request 단계 read-only 조회
- Enableable component 기반 상태 전환으로 구조 변경 최소화
- FreeList/CellMap 같은 ECS 외부 Native container 접근을 fence 규칙으로 시퀀싱
- Codex/Claude 계열 AI coding agent를 활용한 설계 검토, 코드 생성, 반복 구현, 테스트 보강, 문서화

## Portfolio Positioning

이 저장소는 완성 게임 공개본이 아니라, **Unity 클라이언트 개발자 포트폴리오용 playable technical demo**를 목표로 정리 중인 프로젝트입니다.

프로젝트의 출발점은 ECS/DOTS 워크플로우를 실제 게임플레이 문제에 적용해 보는 것이었습니다. 이를 위해 일반적인 GameObject 구조에서 생성/삭제, Transform 업데이트, 충돌/조회, 상태 전환 비용이 커지는 대량 탄환 시나리오를 기술 과제로 선택했습니다.

채용자에게 보여주려는 핵심은 다음입니다.

- 대량 Entity 처리 구조를 설계하고 유지하는 능력
- 시스템별 writer/owner와 update order를 문서와 코드 기준으로 관리하는 능력
- 성능/안정성 리스크를 테스트와 검증 루틴으로 다루는 능력
- AI coding agent를 설계-구현-검증 흐름에 적극적으로 편입하는 능력

## Current Status

| Area | Status | Notes |
|---|---|---|
| Core DOTS bullet pipeline | Implemented | 기존 ADR/OPS 문서 기준으로 파이프라인과 ownership 규칙이 정리되어 있습니다. |
| Playable technical demo | In progress | 외부 공개용 빌드 스냅샷은 별도 범위로 정리 중입니다. |
| Validation evidence | Partial snapshot | 기존 자동 테스트와 PlayMode smoke 기록이 있으며, 최신 공개 빌드 수치는 별도 캡처 대상입니다. |
| Performance evidence | Partial snapshot | 개발 중 스모크/스트레스 관측값은 문서화되어 있으나, 공개용 벤치마크 표는 아직 별도 정리 전입니다. |
| AI-assisted workflow | Documented | 설계 guardrail을 기준으로 AI coding agent를 코드 생성과 반복 구현에 활용한 방식을 정리했습니다. |

## Existing Validation Evidence

기존 운영 문서에는 자동 테스트 기반의 스모크/스트레스 관측값이 기록되어 있습니다. 예를 들어 `OPS-001`에는 Editor 자동 테스트에서 `maxBudgetUsed=5000`, `dropCount=0`, `expiredByAge=0`가 기록되어 있고, PlayMode 자동 테스트에서 약 2.5만 active bullet 수준의 관측값이 기록되어 있습니다.

이 값들은 개발 중 확보한 검증 스냅샷이며, 공개용 빌드 벤치마크와는 구분됩니다. 데모 빌드와 참고자료를 함께 읽는 방법은 `Docs/Portfolio/PORT-003-validation-report.md`에 정리되어 있습니다.

## Documents

- [Portfolio Index](Docs/Portfolio/INDEX.md)
- [DOTS Bullet Pipeline Case Study](Docs/Portfolio/PORT-001-dots-bullet-pipeline-case-study.md)
- [AI-assisted Engineering Workflow](Docs/Portfolio/PORT-002-ai-assisted-engineering-workflow.md)
- [Portfolio Demo Build and Reference Materials](Docs/Portfolio/PORT-003-validation-report.md)

## AI-assisted Development

이 프로젝트는 Codex/Claude 계열 AI coding agent를 설계 검토, 구현 패치 생성, 반복 리팩터링, 테스트 보강, 문서 초안 작성에 적극 활용했습니다.

핵심 방식은 먼저 DOTS ownership, update order, fence, enableable component 규칙을 문서화하고, agent가 그 경계 안에서 코드를 생성하도록 운영하는 것입니다. AI를 통해 구현 시간 비용을 줄이되, 아키텍처 판단과 최종 검증 책임은 개발자가 유지했습니다.

프로젝트 내부 작업 규칙은 [AGENTS.md](AGENTS.md)에 정리되어 있으며, 외부용 AI 활용 사례는 [AI-assisted Engineering Workflow](Docs/Portfolio/PORT-002-ai-assisted-engineering-workflow.md)에 요약되어 있습니다.
