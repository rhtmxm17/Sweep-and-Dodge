# Handoff Packet v1 설계 보류 메모 (HOLD)

## Metadata
- doc_id: `SESSION-20260317-02-HOLD`
- type: `DesignHoldMemo`
- status: `paused`
- last_updated: `2026-03-17`
- task_id: `HPACKET-SCHEMA-V1`
- related_docs:
  - [./SESSION-20260317-02-handoff-packet-schema-board.md](./SESSION-20260317-02-handoff-packet-schema-board.md)
  - [../../AGENTS.md](../../AGENTS.md)

## 1. 현재까지 확정된 선택
- `handoff packet`은 설계 세션이 승인된 단일 구현 단계를 새 구현 thread로 위임할 때 사용하는 문서다.
- 1차 소비자는 사람보다 오케스트레이터와 구현 세션이며, `에이전트 우선` 원칙을 따른다.
- 문서 형식은 사람이 검토 가능하고 자동화가 파싱 가능한 `Markdown frontmatter`를 기본값으로 둔다.
- `handoff packet`은 저장소 문서로 보존하고, 외부 큐나 DB만을 SSOT로 사용하지 않는다.
- 문서 하나는 항상 `단일 구현 단계`만 담당한다.
- `handoff packet`은 `얇은 envelope`로 유지하고, 설계 authority는 TD/ADR/승인 플랜 같은 SSOT 문서에 둔다.
- 구현 결과는 같은 문서에 덧붙이지 않고 `별도 completion report` 문서로 분리한다.
- 전용 문서 계층은 `Docs/Handoff`를 도입하는 방향으로 잡고, `TaskBoard`는 상태와 링크만 추적한다.

## 2. 아직 미결정인 항목
- `Docs/Handoff/Packets`와 `Docs/Handoff/Reports`의 최종 파일명 규칙과 metadata 필드 집합
- `handoff packet`과 `completion report`의 lifecycle 상태 집합과 전이 규칙
- 오케스트레이터가 소비할 최소 frontmatter 필드와 설계 세션에 되돌릴 요약 필드
- `TaskBoard`에 남길 상태 표기 수준과 `task_id` 운영 규칙

## 3. 재개 시 첫 시작점
- `Subagents` 시범 운영 결과를 먼저 정리하고, 구현 세션 역할을 충분히 대체하는지 판단한다.
- 별도 구현 세션이 여전히 필요하면 `Docs/Handoff` 계층 도입 가정을 유지할지 다시 확인한다.
- 그 다음 `handoff packet`과 `completion report`의 frontmatter 최소 필드를 확정한다.
- 마지막으로 `packet/report lifecycle`과 `TaskBoard` 연결 규칙을 닫는다.

## 4. 관련 문서
- [../../AGENTS.md](../../AGENTS.md)
- [./SESSION-20260317-02-handoff-packet-schema-board.md](./SESSION-20260317-02-handoff-packet-schema-board.md)

## 5. 보류 상태
- 보류 상태: `Subagents` 공식 추가에 따라 일정 기간 `설계 세션 + Subagent 구현 worker` 흐름을 먼저 사용하며, 그동안 `handoff packet` 설계 확정을 중지한다.
- 재개 조건: `Subagents` 시범 운영 후에도 별도 구현 세션 오케스트레이션이 필요하다고 판단되면 `Docs/Handoff` 구조와 `packet/report lifecycle`을 재검토한다.
