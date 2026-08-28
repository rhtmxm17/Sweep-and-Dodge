# Portfolio Evidence Index

이 폴더는 `Sweep and Dodge`의 공개 포트폴리오 문장을 뒷받침하는 공개 가능한 상세 근거와 해석 경계를 보존한다. 채용 독자를 위한 핵심 서사는 상위 `PORT-*` 문서에 두고, 이 폴더의 자료는 수치·조건·제한을 더 깊게 확인할 때 사용한다.

## Metadata
- doc_id: `PORT-EVIDENCE-INDEX`
- type: `Portfolio Evidence`
- status: `draft`
- last_updated: `2026-08-28`
- related_docs:
  - [../INDEX.md](../INDEX.md)
  - [../PORT-003-validation-report.md](../PORT-003-validation-report.md)

## Public evidence

- [Stage2-Profiling/README.md](Stage2-Profiling/README.md): 공개 가능한 Stage 2 측정 조건, 결과표, Profiler 정지 캡처

## 공개와 로컬 보관의 경계

저장소에는 공개 주장을 확인하는 데 필요한 측정 조건, 집계 결과와 판독 가능한 이미지까지만 둔다. raw Profiler `.data`, run별 frame CSV, Player/build 로그, 편집 전 영상과 내부 manifest는 `ProfilerCaptures/`에 로컬 개발 근거로 보존하며 기본 공개 패키지에는 포함하지 않는다.

이미지·영상·CSV의 SHA-256은 공개 포트폴리오에 기록하지 않는다. 내부 provenance나 실제 배포 채널에서 checksum이 필요할 때만 별도로 관리한다.

개발자 관점, 역할 분담, 원시 분석과 채널 설계는 포트폴리오 범위 밖의 세션 보조 기록으로 관리하며 이 공개 Evidence Index에서 연결하지 않는다.
