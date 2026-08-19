# Portfolio Index

이 폴더는 `Sweep and Dodge`를 Unity 클라이언트 개발자용 기술 포트폴리오로 설명하기 위한 공개용 문서 묶음이다.

## Metadata
- doc_id: `PORT-INDEX`
- type: `Portfolio`
- status: `draft`
- last_updated: `2026-05-15`
- related_docs:
  - [../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)
  - [../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [../ProjectOps/OPS-003-public-release-readiness-plan.md](../ProjectOps/OPS-003-public-release-readiness-plan.md)
  - [../ADR/ADR-20260206-01-bullet-pipeline-ownership.md](../ADR/ADR-20260206-01-bullet-pipeline-ownership.md)

## 문서 목적

- 루트 `README.md`에서 다 담기 어려운 기술 사례, AI-assisted workflow, 검증 상태, 공개 데모 빌드 기준을 분리해 정리한다.
- 채용담당자, 면접관, 기술 리뷰어가 프로젝트의 기술적 초점과 현재 상태를 빠르게 이해할 수 있게 한다.
- 공개 표기명과 GitHub 저장소명은 `Sweep and Dodge` 기준으로 정리했으며, Unity 제품명도 같은 기준으로 맞춘다.

## Documents

- [PORT-001-dots-large-entity-pipeline-case-study.md](PORT-001-dots-large-entity-pipeline-case-study.md): DOTS 대량 엔티티 파이프라인 기술 사례
- [PORT-002-ai-assisted-engineering-workflow.md](PORT-002-ai-assisted-engineering-workflow.md): Codex/Claude 계열 AI coding agent 활용 사례와 품질 관리 방식
- [PORT-003-validation-report.md](PORT-003-validation-report.md): 포트폴리오 데모 빌드와 참고자료를 함께 읽는 방법

## 사용 규칙

- README는 채용자 첫 화면, `PORT-*` 문서는 면접/심화 검토용 보조 문서로 사용한다.
- 검증 수치는 기존 문서에 근거가 있을 때만 사용하고, 개발 중 스냅샷과 공개용 벤치마크를 구분한다.
- AI 활용 설명은 설계 guardrail, 코드 생성, 반복 구현, 검증 책임을 함께 설명한다.
