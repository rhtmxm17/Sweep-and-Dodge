# SESSION-20260316-01

## Metadata
- doc_id: `SESSION-20260316-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-03-16`
- related_docs:
  - [../TechnicalDesign/TD-022-in-world-dialogue-runtime-contract.md](../TechnicalDesign/TD-022-in-world-dialogue-runtime-contract.md)
  - [../ADR/ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md](../ADR/ADR-20260316-02-in-world-dialogue-start-overlay-and-pre-result-clear-gate.md)
  - [../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md](../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md)
  - [../TechnicalDesign/TD-016-runtime-ui-shell-and-navigation-contract.md](../TechnicalDesign/TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [../TechnicalDesign/TD-020-hint-notification-runtime-contract.md](../TechnicalDesign/TD-020-hint-notification-runtime-contract.md)

## Session Goal
- 한 줄 목표: 인월드 연출 대화의 문서 정합성을 맞추고 구현 착수 가능한 작업 패키지로 분해한다.
- 완료 기준: 기존 TD 정합성 반영, `TD-022` 구현 단계 세분화, 다음 구현 턴의 시작점 확정
- 이번 세션에서 하지 않을 것: 런타임 코드 구현, Unity compile/test 실행

## Now
- [ ] T1. 인월드 연출 대화 구현 착수 순서를 `P1~P6` 기준으로 운영한다.
  - 목적: 다음 구현 턴에서 데이터 모델부터 테스트까지 순서를 흔들지 않도록 기준선을 고정한다.
  - 완료 기준: `TD-022`의 작업 패키지와 의존 순서가 합의된 구현 시작점으로 사용된다.
  - 검증: `TD-022` 작업 분해 확인
  - 근거: `StageClear` defer는 shell/gate/UI/test가 함께 엮여 있어 순서를 잘못 잡으면 되돌리기 비용이 크다.

## Next
- [ ] T2. `P1 데이터 모델 / authoring`을 확정한다.
  - 완료 기준: catalog/entry/line/blocking/retry schema가 코드 작업 가능한 수준으로 고정된다.
  - 검증: `TD-022` 4장과 8.1이 일치한다.
  - 근거: 다른 구현 작업이 모두 이 스키마를 참조한다.
- [ ] T3. `P2 Shell / gate 통합`의 상세 구현 포인트를 코드 기준으로 분해한다.
  - 완료 기준: `DemoShellFlowController`, `RunDirectorStageBridge`, result metrics snapshot 변경 지점이 식별된다.
  - 검증: 관련 코드 파일 목록과 영향 범위가 정리된다.
  - 근거: `pre-result clear gate`는 되돌리기 비용이 큰 구조 변경이다.

## Blocked
- 없음

## Parking Lot
- [ ] P1. `ThemeTransition` 전용 screen 또는 shell 문맥을 별도 UX로 확장할지 후속 세션에서 재평가한다.
  - 근거: current demo flow에는 별도 chapter screen이 아직 없다.
- [ ] P2. 튜토리얼 예외 개입은 start/clear v1 구현 이후 별도 세션으로 분리한다.
  - 근거: 현재 합의 범위에서 제외했다.

## Done
- [x] D1. `TD-022`와 `ADR-20260316-02` 초안을 작성했다.
  - 검증 결과: `StageStart=overlay`, `StageClear=pre-result clear gate`, GO 전용 계층 결정이 문서화됐다.
- [x] D2. `TD-010`, `TD-016`, `TD-020`을 `TD-022` 기준으로 정합화했다.
  - 검증 결과: clear defer, `PresentationLayer`, lower-center suppress 규칙이 기존 TD에 반영됐다.

## End of Session
- 결과: 인월드 연출 대화는 기존 shell/UI/notification 문서와 충돌하지 않는 상태로 정리됐고, 구현은 `P1 -> P6` 순서로 착수하면 된다.
- 남은 리스크: clear defer에 따라 result metrics snapshot 시점과 PlayMode smoke 케이스를 함께 바꿔야 한다.
- 다음 세션 시작점: `P1 데이터 모델 / authoring` 확정 후 `P2 Shell / gate 통합` 영향 파일을 구체화한다.
