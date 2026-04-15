# ui-workflow.md
> UI 레이아웃 및 Penpot 작업 시 참조

## Penpot MCP 사용 원칙

- Penpot MCP 사용 범위:
  - 관측(Observability): 페이지/보드/선택 요소 구조 조회, 시각 자료 export, 레이아웃 상태 확인
  - 반영(Apply): UI 레이아웃 초안 생성, 시각 자료 배치 조정, 협업용 보드 갱신
  - 협업(Collaboration): 사용자 조작 이후 변경 해석, Exploration/Approved 보드 운영 지원
  - 제한(Limit): Penpot 변경은 설계/협업 산출물로 취급하며, Unity 코드/프리팹 반영의 SSOT로 직접 사용하지 않는다
- 공통 규칙:
  - Penpot 변경은 설계/협업 산출물로 취급하며, 코드/프리팹 반영 전 승인 게이트와 문서 정합성 확인을 유지한다.
  - 위치/정렬 판단이 중요한 UI 작업에서는 구조 조회만으로 충분하다고 가정하지 않고, 필요 시 보드 export 이미지를 함께 확인한다.
- 예외: 사용자가 명시적으로 요청하면 범위를 확장할 수 있다.

---

## prefab/scene 규칙

- `RuntimeUiRoot`와 유사한 공용 UI 루트는 prefab SSOT로 관리한다.
  - Agent는 공통 UI 구조/레이아웃/presenter 변경 시 씬 인스턴스를 직접 수정하지 않고 prefab 자산을 우선 수정한다.
  - scene 수정은 scene-specific binding 또는 배치처럼 씬 소유 값에 한정한다.
  - 예외적으로 scene override가 필요하면 완료 보고에 이유와 범위를 명시한다.

---

## UI 레이아웃 워크플로우

- UI 레이아웃 작업은 기본적으로 `기능 중심 대화 -> Penpot 초안 -> 사용자 직접 조작 -> Agent 재해석 -> 승인 후 Unity 반영` 순서를 따른다.
- Penpot은 협업용 시각 캔버스이며, 구현 SSOT는 repo 내부 문서/JSON/Prefab으로 유지한다.
- Exploration 보드와 Approved 보드를 분리한다.
  - Exploration: 빠른 배치 실험, 대안 비교, 코멘트 중심
  - Approved: 구현 반영 기준안
- Penpot에서 `Board`는 화면/레이어/패널처럼 구현 대응이 있는 단위를 표현하는 기본 컨테이너로 사용한다.
- `Group`은 비교, 임시 이동, 묶음 조작 같은 작업 보조 단위로 제한한다.
- 필요 시 `WorkArea Board -> Viewport Board -> Layer Board -> Panel Board` 순서로 계층화한다.
  - `Viewport Board` 내부의 구현 대응형 `Board`만 Unity 반영 후보로 본다.
  - 메모, 질문, 비교안은 `WorkArea`의 `NotesBoard`/`OpenQuestionsBoard` 등으로 분리하고 반영 대상에서 제외한다.
  - 사용자는 위치/크기/그룹 의도를 가능한 한 Penpot 객체 조작으로 표현하고, 코멘트는 "왜 바꾸는가"에 집중한다.
  - `Approved` 보드의 실제 반영 후보는 각 위젯의 `screen attach intent`가 Unity의 `Anchor + Pivot`으로 번역 가능하도록 표현한다.
  - 레이아웃 반영 판단 시에는 `어느 부모 프레임에 붙는가(parent frame)`와 내부 배치가 `manual / stretch / layout-driven` 중 무엇인지도 함께 본다.
  - Penpot에서 attach intent를 남길 때는 `constraints + REF__ 기준선 + SPEC__Attach/SPEC__Layout` 조합을 기본값으로 사용한다.
  - Agent는 Penpot 변경사항을 재해석할 때 레이아웃 변화, 의도 변화, 구현 영향, 남은 open question을 분리해서 정리한다.
  - 위치/정렬/점유 비율 의도가 구조 데이터만으로 충분히 전달되지 않으면, Agent는 선택 상태와 무관하게 `Viewport Board` 또는 `Approved` 보드를 직접 export한 단일 이미지(PNG/SVG)를 함께 사용해 판단을 보조한다.
  - Unity UI 반영 전에는 가능하면 `보드 구조 데이터 + Viewport/Approved 보드 export 이미지`를 함께 확인해 위치/정렬 오해를 줄인다.
  - Penpot, 관련 문서, 현재 Unity 상태만으로 `Anchor / Pivot / parent frame / 배치 소유 방식`을 단일하게 결정할 수 없으면, Agent는 임의 적용하지 않고 필요한 확인 사항을 사용자에게 보고한 뒤 작업을 중단한다.
  - 공용 UI 구조 변경은 Penpot 승인안만으로 바로 씬 인스턴스를 수정하지 않는다.
    - 구현 전 `RuntimeUiRoot` prefab SSOT, TD, 관련 운영 문서와의 정합성을 먼저 확인한다.
  - UI 워크플로우 상세 절차는 아래 문서를 따른다.
    - `Docs/UI/UI-Workflow.md`
