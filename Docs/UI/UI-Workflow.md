# UI 레이아웃 워크플로우

## Metadata
- doc_id: `UI-WORKFLOW`
- type: `ProjectOps`
- status: `active`
- last_updated: `2026-03-18`
- related_docs:
  - [../../AGENTS.md](../../AGENTS.md)
  - [../GameDesign/GD-009-in-game-ui-screen-blueprint.md](../GameDesign/GD-009-in-game-ui-screen-blueprint.md)
  - [../GameDesign/GD-010-in-game-ui-layout-and-zones.md](../GameDesign/GD-010-in-game-ui-layout-and-zones.md)
  - [../TechnicalDesign/TD-016-runtime-ui-shell-and-navigation-contract.md](../TechnicalDesign/TD-016-runtime-ui-shell-and-navigation-contract.md)

> UI 레이아웃 작업을 `대화 -> Penpot 시각 협업 -> Agent 재해석 -> Unity 반영` 루프로 운영하기 위한 상세 절차 문서.

## 1. 목적
- 텍스트 설명만으로 배치 의도를 주고받는 비용을 줄인다.
- UI 작업에서 "무엇을 위한 화면인가"와 "어디에 무엇이 놓이는가"를 분리해 관리한다.
- Penpot을 협업 캔버스로 사용하되, 최종 구현 SSOT는 repo 내부 문서와 prefab으로 유지한다.

## 2. 역할 분리
### 2.1 Penpot
- 레이아웃 시안 작성
- 사용자 직접 조작
- 대안 비교
- 코멘트 기반 피드백

### 2.2 Repo 문서
- 승인된 의도 기록
- 구현 범위/비범위 명시
- Unity 반영 전 기준선 유지

### 2.3 Unity
- 실제 구현 대상
- prefab/presenter/binding 반영
- 검증과 회귀 확인

## 3. 기본 원칙
- Penpot은 협업 캔버스이며 구현 SSOT가 아니다.
- 구현 전 기준안은 `Approved` 보드와 repo 문서에 함께 남긴다.
- 큰 UI 구조 변경은 Penpot 초안만으로 곧바로 씬 인스턴스를 수정하지 않는다.
- 공용 UI 구조는 `RuntimeUiRoot` prefab SSOT 원칙을 유지한다.
- Exploration 보드와 Approved 보드를 분리한다.
- 코멘트는 "무엇을 왜 바꾸는가"에 집중하고, 위치/크기 의도는 가능한 한 직접 조작으로 표현한다.

## 4. 운영 단위
### 4.1 화면 단위
- 하나의 화면 또는 목적 단위를 한 번의 레이아웃 작업 단위로 본다.
- 예:
  - `Pause`
  - `Result`
  - `HUD / Objective`
  - `Settings`

### 4.2 보드 분리
- `Exploration/<ScreenName>`
  - 빠른 시안, 대안 비교, 배치 실험
- `Approved/<ScreenName>`
  - 구현 반영 기준안
- `Components/<Domain>`
  - 공용 파츠 또는 반복 블록

## 5. 표준 워크플로우
### 5.1 기능 중심 대화
작업 시작 시 먼저 기능 목적을 정리한다.

최소 확인 항목:
- 이 UI는 무엇을 위한 화면인가
- 사용자가 여기서 무엇을 해야 하는가
- 필수 정보와 필수 액션은 무엇인가
- 화면 상태가 몇 가지로 갈라지는가
- 이번 단계의 비범위는 무엇인가

권장 산출물:
- `UI Brief`

### 5.2 Agent 초안 작성
Agent는 Penpot에서 저충실도 레이아웃 초안을 만든다.

초기 초안 원칙:
- 정보 우선순위가 먼저
- 그룹 구조가 둘째
- 시각 스타일은 나중
- 아직 확정되지 않은 항목은 빈 슬롯 또는 주석으로 남긴다

### 5.3 사용자 직접 조작
사용자는 Penpot에서 직접 다음을 수행한다.
- 위치 이동
- 크기 조정
- 그룹 재배치
- 필요 시 코멘트 추가

사용자 피드백 규칙:
- 배치 변경은 가능하면 객체를 실제로 옮긴다
- 코멘트는 변경 이유를 설명한다
- 단순 취향 코멘트보다 판단 기준 코멘트를 우선한다

### 5.4 Agent 재해석
Agent는 Penpot 변경 후 아래 관점으로 결과를 재정리한다.
- 레이아웃 변화
- 의도 변화
- 구현 영향
- 남은 open question

권장 출력 형식:
1. 변경된 배치 요약
2. 유지할 의도
3. 구현 시 주의점
4. 확인이 필요한 항목

### 5.5 승인 후 구현 계획 고정
Unity 반영 전 아래 중 하나 이상을 갱신한다.
- 관련 TD
- 작업용 계획 문서
- 간단한 layout intent 메모

이 단계에서 확정해야 할 것:
- 구현 범위
- prefab 우선 반영 대상
- scene-specific 값 여부
- binding/presenter 영향 범위

### 5.6 Unity 반영
승인 후 Unity에 반영한다.

반영 원칙:
- 공용 구조 변경은 prefab 우선
- scene 변경은 scene-specific binding 또는 배치 값에 한정
- Penpot 배치를 기계적으로 복사하지 않고 runtime 제약을 함께 본다

### 5.7 검증
- compile
- console error 0
- EditMode
- PlayMode smoke
- 필요 시 Penpot 기준안과 구현 차이 비교

## 6. 문서화 기준
### 6.1 AGENTS.md에 남길 것
- 운영 원칙
- 가드레일
- 승인 게이트

### 6.2 상세 문서에 남길 것
- Penpot 사용 절차
- 보드 구조
- 협업 규칙
- 로컬 연결 절차

### 6.3 TD/ADR로 승격할 것
- 되돌리기 비용이 큰 UI 구조 결정
- 공용 UI 루트/Presenter 경계 변경
- 런타임 소유권 또는 업데이트 순서에 영향을 주는 결정

## 7. Penpot 사용 규칙
### 7.1 Exploration 보드
- 대안 비교 허용
- 임시 노트 허용
- 빠른 이동/재배치 우선

### 7.2 Approved 보드
- 구현 기준안만 유지
- 불확정 메모 최소화
- 구현자 관점의 읽기 쉬움 우선

### 7.3 코멘트 규칙
- "왼쪽이 더 예쁨"보다 "Carry 판단 시선 이동을 줄이기 위해 좌측 고정"처럼 남긴다
- 코멘트만 남기지 말고 가능하면 실제 위치도 함께 바꾼다
- 동일 주제 논의는 한 스레드로 유지한다

## 8. Agent가 Penpot으로 할 일
- 현재 페이지/보드 구조 읽기
- 저충실도 wireframe 생성/조정
- 선택 요소 요약
- 시안 export로 시각 확인
- 사용자 변경 이후 차이 해석

Agent가 Penpot만으로 확정하지 않는 것:
- 구현 승인
- prefab 구조 확정
- presenter 책임 확정
- 런타임 제약 무시한 최종 배치 확정

## 9. 구현 반영 시 체크리스트
- 이 변경이 공용 UI 구조 변경인가
- `RuntimeUiRoot` prefab SSOT와 충돌하지 않는가
- scene override가 필요한가
- 관련 TD/GD와 의도가 충돌하지 않는가
- Penpot Approved 보드와 repo 문서 기준이 일치하는가

## 10. 추천 운영 예시
1. 사용자: `Pause 화면은 설정 진입보다 즉시 재개/재시작 판단이 빨라야 한다`
2. Agent: Penpot `Exploration/Pause` 초안 생성
3. 사용자: 버튼 위치 이동, 설명 코멘트 추가
4. Agent: 변경 의도와 구현 영향 재정리
5. 사용자 승인
6. Agent: 관련 문서/plan 갱신 후 Unity prefab 반영
7. 검증 후 완료 보고

## 11. 변경 이력
- 2026-03-18: Penpot MCP 기반 UI 레이아웃 협업 루프를 프로젝트 운영 문서로 추가했다.
