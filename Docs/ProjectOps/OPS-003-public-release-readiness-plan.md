# 공개 릴리즈 준비 / 운영 계획문서

> 데모 플레이어블 완성 이후 공개 빌드 패키징, 브랜딩, QA, 배포 운영 범위를 분리해 관리하기 위한 초안 목차 문서

## Metadata
- doc_id: `OPS-003`
- type: `ProjectOps`
- status: `draft`
- last_updated: `2026-03-12`
- related_docs:
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](./OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [GD-008-demo-flow-design.md](../GameDesign/GD-008-demo-flow-design.md)
  - [TD-010-demo-shell-flow-and-bridge-contract.md](../TechnicalDesign/TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)

## 1. 문서 목적
- `OPS-002`가 다루는 "게임 플레이어블 완성"과 별도로, 공개 빌드 전달/운영 준비 범위를 관리한다.
- 빌드 메타, 브랜딩, 입력/옵션 정책, QA matrix, 공개 체크리스트를 한 문서에 모은다.
- 공개 채널 결정 전에도 필요한 준비 항목을 먼저 분리해 추적한다.

## 2. 운영 가정
- 대상 플랫폼: `Windows PC` 1차 공개 빌드
- 조작 기준: `키보드 + 마우스` 우선
- 패드 지원: 선택 과제
- 공개 채널: 미확정 (`직접 배포`, `itch.io`, `Steam Demo` 중 후속 결정)

## 3. 공개 릴리즈 완료 정의
- 공개 빌드에서 `Title -> Lobby -> Stage1~3 -> Result -> Demo Complete` 루프가 안정적으로 동작한다.
- 제품명/아이콘/버전/실행 파일 이름/기본 옵션이 외부 배포 기준으로 정리된다.
- 비개발 빌드에서 디버그 HUD/테스트 버튼/개발 전용 경고가 노출되지 않는다.
- 수동 QA matrix와 전달 체크리스트를 통과한 빌드만 공개 대상으로 간주한다.

## 4. 결정 체크포인트
- 공개 채널 결정 (`직접 배포` / `itch.io` / `Steam Demo`)
- 패드 지원 범위 (`미지원` / `UI만 준비` / `전면 지원`)
- 저장/옵션 데이터 호환 정책
- 데모와 향후 정식판의 앱 식별자/세이브 분리 정책
- 로그/크래시/핫픽스 운영 기준

## 5. 작업 스트림 초안
| ID | 스트림 | 목표 | 상태 | 우선순위 |
|---|---|---|---|---|
| R1 | Build Branding | 제품명, 아이콘, 버전, 앱 메타, 실행 파일 이름 확정 | TODO | P0 |
| R2 | Build Packaging | 배포 파일 구조, 압축본, 전달 단위, 릴리즈 노트 초안 확정 | TODO | P0 |
| R3 | Release QA Matrix | 해상도/윈도우 모드/입력 장치/반복 플레이 검증 루틴 확정 | TODO | P0 |
| R4 | Input / Options Policy | 공개 빌드 기준 입력/옵션/저장 정책 확정 | TODO | P0 |
| R5 | Debug Separation | 개발 전용 HUD/로그/테스트 기능 비노출 정책 확정 | TODO | P0 |
| R6 | Store / Channel Prep | 채널별 메타데이터, 설명, 스크린샷, 배포 절차 정리 | TODO | P1 |
| R7 | Hotfix Ops | 공개 후 치명 버그 대응, 버전 증가, 재배포 절차 정리 | TODO | P1 |

## 6. QA Matrix 초안
- 해상도:
  - `1920x1080`
  - `2560x1440`
- 화면 모드:
  - `Fullscreen`
  - `Windowed`
- 입력:
  - `Keyboard + Mouse`
  - `Gamepad` (지원 결정 시)
- 반복 시나리오:
  - `Retry` 반복
  - `Stage1 -> Stage2 -> Stage3` 완주
  - `Alt+Tab`, 포커스 손실
  - 옵션 변경 후 재실행/씬 재진입

## 7. 배포 체크리스트 초안
- 빌드 설정 확인
- 버전/아이콘/앱 메타 확인
- 비개발 빌드 smoke pass
- console error 0
- 수동 QA matrix 통과
- 압축본/실행 파일/읽을거리(controls/readme) 포함 여부 확인
- 공개용 스크린샷/설명/변경 메모 준비

## 8. 리스크와 대응
- 공개 채널 결정이 늦어져 패키징/저장 정책이 재작업될 위험
  - 대응: 채널 비의존 항목부터 먼저 고정하고, 채널 종속 항목은 결정 체크포인트로 분리
- 디버그 기능이 공개 빌드에 남을 위험
  - 대응: `Development Build`와 공개 빌드 노출 정책을 분리하고 체크리스트에 포함
- 입력/옵션 정책이 늦게 고정되어 UI/문서/QA가 같이 흔들릴 위험
  - 대응: `OPS-002`의 UI 전환과 연동해 초기에 기준을 고정

## 9. 변경 이력
- 2026-03-12: 초안 목차 작성. `OPS-002`에서 분리할 공개 빌드 운영 항목의 범위와 체크포인트를 정리했다.
