# ADR-20260219-05-carrybin-deposit-touch-request-execution
> CarryBin 내려놓기 동작을 접촉 기반 Request-Execution 파이프라인으로 추가한 결정

## 상태
- 반영됨

## 배경
- 기존 코드베이스는 `CarryBin.Load` 증가(흡입)와 감소(피격)만 구현되어 있고, Deposit 비우기 동작은 비어 있었다.
- 기획 문서 MVP 규칙은 Deposit 수행 시 `CarryBin.Load = 0`이며, 트리거는 접촉 즉시 또는 접촉+인터랙션 중 하나로 미확정 상태였다.
- 프로젝트 파이프라인 원칙은 `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`이며, 요청 생성과 실제 상태 변경의 소유권 분리를 유지한다.

## 결정
- Deposit 트리거는 MVP로 `접촉 즉시 비우기`를 채택한다.
- 구현은 Request-Execution 분리 패턴을 따른다.
  - Request: `PlayerCarryBinDepositRequestSystem`가 플레이어-Deposit 접촉을 감지하고 `PlayerCarryBinDepositRequestTag`를 enable 한다.
  - ExecutionEnd: `PlayerCarryBinDepositExecutionSystem`가 요청을 소비하며 `CarryBin.Load = 0`을 적용한다.
- Deposit 컨텍스트(`PlayerCarryBinDepositContextComponent`)에 접촉한 Deposit 엔티티를 기록한다.
- Deposit 지점은 `DepositPointComponent` + `DepositPointAuthoring`으로 정의한다.
- 이번 단계에서는 MetaScrap 정산을 연결하지 않는다(비우기만 구현).

## 대안
- 접촉 + 인터랙션 입력 방식
  - 장점: 의도성 강화
  - 단점: 입력 경로/상태 확장 필요, MVP 템포 저하 가능성
- Request 단계에서 즉시 `Load=0` 적용
  - 장점: 시스템 수 감소
  - 단점: Request/Execution 소유권 분리 원칙 약화

## 결과
- 문서의 핵심 MVP 규칙(`Load=0`)이 코드에 반영된다.
- 기존 Hazard 충돌 처리와 동일한 요청-소비 패턴으로 일관성이 유지된다.
- Deposit 정산(MetaScrap, 피드백 확장)을 후속으로 연결 가능한 최소 구조가 마련된다.

## 후속
- `MetaScrap += depositedLoad` 정산 로직 연결.
- Deposit 수행 이벤트를 UI 피드백 채널(`PlayerUiFeedbackEventBufferElement`)에 발행.
- 접촉 즉시 방식의 플레이 체감 검증 후, 필요 시 인터랙션 방식으로 전환 검토.
