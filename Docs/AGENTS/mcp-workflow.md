# mcp-workflow.md
> 코드 생성/수정 및 MCP 검증 시 참조

## 실행/빌드/테스트(Commands)

- Unity 버전: `Unity 6000.3.6f1`
- 빌드/실행:
  - Unity Editor에서 Play Mode / Standalone Build
- 테스트:
  - `PlayMode` 테스트는 작업 완료마다 `전용 PlayMode 테스트 씬` 스모크를 강제 실행한다.
  - PlayMode 1차 판정은 `기동/루프 정상성`으로 하고, 성능 임계치 초과는 추적 항목으로 기록한다.
- 프로파일링:
  - Profiler(Entities Profiler 포함), Frame Debugger(필요 시)
- 코드 생성/수정 후 기본 검증 절차(MCP 연결 시):
  1. `refresh_unity(compile=request, wait_for_ready=true)`로 컴파일 요청
  2. `read_console(action=get, types=["error"], include_stacktrace=true)`로 에러 확인
  3. `EditMode` 테스트 실행
  4. `PlayMode` 전용 씬 스모크 실행
  5. 에러/실패가 있으면 수정 후 1~4 반복
  6. 에러 0건 + 테스트 통과 시 작업 완료 보고
- 테스트 예외:
  - 테스트 과정에서 생성되는 `Assets/InitTestScene*.unity` 및 대응 `.meta` 파일은 임시 산출물로 간주한다.
  - 위 파일은 작업 중 unexpected change로 취급하지 않고 무시할 수 있다.
  - 단, 작업 완료 직전에는 해당 파일을 삭제한 뒤 최종 상태를 보고한다.

---

## Unity MCP 사용 원칙

- Unity MCP 기본 사용 범위:
  - 관측(Observability): 콘솔, 씬 상태, 에셋 참조 관계 조회
  - 반영(Apply): 프리팹, 씬, ScriptableObject 변경 적용
  - 검증(Verify): refresh, 콘솔 확인, 테스트 실행
- 스크립트 편집은 MCP 대상에서 제외하고 일반 파일 편집 워크플로우를 사용한다.
- 예외: 사용자가 명시적으로 요청하면 범위를 확장할 수 있다.

---

## 검증 및 완료 보고

- 완료 전 검증은 위 절차(compile → console error 0 → EditMode → PlayMode 스모크)를 기본으로 한다.
- 완료 보고에는 아래를 포함한다.
  - 변경 내용 요약
  - 검증 결과
  - 남은 리스크 또는 미검증 사유/범위
