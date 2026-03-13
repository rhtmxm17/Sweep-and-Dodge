# 인게임 UI 화면 청사진 (GD-009)

## Metadata
- doc_id: `GD-009`
- type: `GameDesign`
- status: `draft`
- last_updated: `2026-03-13`
- related_docs:
  - [GD-001-campaign-loop-design.md](./GD-001-campaign-loop-design.md)
  - [GD-004-carrybin-load-and-deposit.md](./GD-004-carrybin-load-and-deposit.md)
  - [GD-006-hazard-conditional-capture-system.md](./GD-006-hazard-conditional-capture-system.md)
  - [GD-008-demo-flow-design.md](./GD-008-demo-flow-design.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-011-runtime-player-hud-contract.md](../TechnicalDesign/TD-011-runtime-player-hud-contract.md)
  - [TD-013-player-feedback-presentation-bridge-contract.md](../TechnicalDesign/TD-013-player-feedback-presentation-bridge-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](../TechnicalDesign/TD-016-runtime-ui-shell-and-navigation-contract.md)

> 인게임 플레이 화면에서 어떤 정보가 어느 위치와 레이어에 속하는지 정의하는 상위 청사진 문서. 개별 위젯 스펙이나 구현 계약은 후속 문서에서 다룬다.

## 1. 목적
- 인게임 UI를 개별 위젯 단위가 아니라 화면 전체 구조 관점에서 정리한다.
- 플레이어가 "내 상태", "현재 목표", "방금 일어난 일", "지금 어디로 가야 하는가"를 서로 다른 위치에서 안정적으로 읽도록 레이어와 레인을 구분한다.
- Carry, Objective, Hazard, Hint, Notification, Screen FX, 월드 인디케이터의 역할 충돌을 줄이고 후속 상세 설계의 기준선을 만든다.

## 2. 적용 범위
- 스테이지 플레이 중 표시되는 인게임 UI의 상위 구성
- 화면 레이어와 레인별 역할 정의
- 상시 HUD / 반상시 정보 / 사건형 피드백 / 월드형 인디케이터의 분리 기준
- 인게임 UI 범위 안에서의 배치 원칙과 우선순위

## 3. 비범위
- Carry 블록, Objective 블록 내부 위젯 상세 구성
- 개별 카피 문안, 아이콘 디자인, 애니메이션 수치, 사운드 상세
- UI 프리팹 구조, Presenter 책임, 이벤트 채널 상세 매핑 등 구현 계약
- 외부 흐름 화면(`Title`, `Lobby`, `Result`, `Demo Complete`)의 상세 구성

## 4. 가정/제약
- 데모 플레이어블 기준 인게임 HUD는 짧은 시선 이동으로 읽혀야 하며, 플레이 월드를 과도하게 가리지 않아야 한다.
- 현재 코어 플레이는 `수거 -> Carry 증가 -> Capacity 도달 시 Deposit 이동 -> 재진입` 루프를 반복하며 모든 Source를 `Depleted`까지 밀어붙이는 구조를 가진다.
- Stage 1에서는 첫 60초 안에 핵심 루프를 이해할 수 있도록 최소 한 번 이상의 문맥형 힌트 노출이 필요하다.
- HUD는 설명서가 아니라 판단 보조 수단이어야 하며, 사건성 정보와 학습성 정보는 상시 HUD에 과적재하지 않는다.
- 본 문서는 상위 청사진 문서이므로, 세부 표현 방식은 열어둔다.

## 5. 경험 목표
| 목표 | 의도 |
| --- | --- |
| 빠른 상태 파악 | Carry와 Objective를 짧은 시선 이동만으로 읽게 한다. |
| 판단 전환 지원 | "더 담기 / 비우러 가기 / 현재 Source를 더 밀기"의 전환 타이밍을 흐리지 않는다. |
| 사건 가독성 확보 | 피격, Deposit, Hazard 성공, 차단 등의 사건을 상시 HUD와 분리해 읽히게 한다. |
| 화면 중앙 보존 | 플레이 월드와 탄막 판독을 가리는 고정 UI 면적을 최소화한다. |
| 학습과 실전의 분리 | 튜토리얼/힌트 문구가 상시 HUD를 오염시키지 않도록 별도 레인으로 분리한다. |

## 6. 설계 원칙
### 6.1 화면은 정보 종류가 아니라 점유권으로 나눈다
인게임 UI는 "무엇을 보여줄 것인가"보다 먼저 "얼마나 자주, 얼마나 오래, 어디를 점유하는가"로 나눈다.

- 상시 점유: 항상 존재해야 하는 정보
- 반상시 점유: 늘 자리는 있으나 갱신 순간에만 강하게 읽히는 정보
- 순간 점유: 짧게 떠서 사건을 전달하고 사라지는 정보
- 월드 점유: 스크린 고정이 아니라 공간 안에서 위치를 알려주는 정보

### 6.2 상시 HUD 본체는 최소 2축으로 유지한다
상시 HUD의 본체는 다음 두 축으로 고정한다.
- Carry 축: "지금 더 담을 수 있는가 / 지금 비워야 하는가"
- Objective 축: "스테이지 목표가 얼마나 남았는가 / 지금 어떤 Source를 밀고 있는가"

### 6.3 Hazard는 상시 정보가 아니라 반상시 보조층으로 본다
HazardStack은 시스템적으로는 배율 계산에 영향을 주지만, 플레이 체감상으로는 "성공 시 누적되는 보너스 텐션"에 가깝다.
따라서 별도 상시 블록보다 Carry에 붙는 반상시 보조층으로 취급한다.

### 6.4 힌트와 사건 알림은 상시 HUD에서 분리한다
문맥형 힌트, 짧은 알림, 피드백 텍스트는 상시 HUD에 상주시키지 않는다.
이들은 하단 중앙의 별도 레인에서 사건성/학습성 정보로 운영한다.

### 6.5 위치 정보는 월드에서 처리한다
Source, Deposit, 위험 구역의 "어디"는 가능한 한 월드 인디케이터가 맡는다.
상단 HUD는 요약을 담당하고, 공간 내 방향성은 월드 내부 표현으로 보완한다.

## 7. 인게임 UI 레이어 구조
인게임 화면은 아래 5개 층으로 구성한다.
여기서 레이어는 시각적/기능적 점유권 기준이고, 8장의 레인은 실제 배치 슬롯 기준이다. 하나의 레이어가 여러 레인으로 나뉠 수 있다.

1. **상시 HUD 본체**
   - Carry
   - Objective
2. **반상시 상태/보너스 표시**
   - HazardStack
   - Timer / StageState 보조 정보
3. **문맥형 힌트**
   - 온보딩 힌트
   - 행동 유도 문구
4. **사건 알림 / 즉시 반응 피드백 / 화면 FX**
   - `Load -X`, `Blocked`, `Source Depleted` 같은 사건 알림
   - 피격, Deposit, Timeout, Clear/Fail 계열 피드백
5. **월드 결합형 인디케이터**
   - Source / Deposit / 위험 구역 방향성 및 위치성

## 8. 화면 레인 구성
### 8.1 레인 1 — 상단 중앙 레인
**역할**
- 현재 스테이지 목표를 요약해 보여준다.
- 전체 진행도와 현재 핵심 Source를 상기시킨다.
- 필요 시 Timer / StageState를 보조 정보로 붙인다.

**포함 후보**
- 전체 진행도
- 현재 핵심 Source 진행 상태
- Timer 또는 StageState(보조)

**배치 의도**
- 목표 정보는 화면 전체 문맥을 대표하므로 상단 중앙이 가장 자연스럽다.
- 모든 Source를 나열하지 않고도 현재 전장 방향을 요약할 수 있다.

### 8.2 레인 2 — 좌측 코어 레인
**역할**
- 현재 런에서 가장 자주 확인하는 자기 상태 축을 담당한다.
- Carry 증감과 Capacity 접근 여부를 빠르게 읽히게 한다.

**포함 후보**
- Load / Capacity
- Capacity 경고
- Carry 관련 핵심 상태 표시

**배치 의도**
- Carry는 현재 행동 전환을 결정하는 정보이므로 가장 빠르게 눈에 들어오는 코너에 고정한다.
- 상단 Objective와 분리해, 자기 상태와 목표 상태가 한눈에 섞이지 않게 한다.

### 8.3 레인 2 보조 — Carry 인접 Hazard 구역
**역할**
- HazardStack을 상시 HUD 본체가 아니라 Carry와 연결된 반상시 보너스 층으로 표현한다.
- 성공 시 누적 텐션과 리셋 타이밍을 사건 감각으로 전달한다.

**포함 후보**
- HazardStack 수치 또는 세그먼트
- 갱신 순간 강조 반응
- Deposit / Hit 리셋 반응

**배치 의도**
- Hazard는 Objective보다 Carry와 함께 읽힐 때 의미가 크다.
- 상시 존재감은 낮추고, 갱신 순간의 반응성을 높여 "늘 보이지만 늘 읽지 않는 정보"로 운영한다.

### 8.4 레인 3 — 하단 중앙 힌트 레인
**역할**
- Stage 1 온보딩과 문맥형 행동 유도를 담당한다.
- 상시 HUD를 설명문으로 오염시키지 않고, 필요한 순간에만 플레이어에게 짧은 문장을 건넨다.

**포함 후보**
- 첫 이동
- 첫 수집
- 첫 Carry 증가
- 첫 Deposit 필요
- 첫 피격
- 실패 후 재시도 행동 힌트

**배치 의도**
- 상단 Objective 및 좌측 Carry와 시선 충돌을 피한다.
- 플레이 월드 중앙을 직접 가리지 않으면서도 짧게 읽히는 문장 영역으로 쓰기 쉽다.

### 8.5 레인 4 — 하단 중앙 사건 알림 레인
**역할**
- 짧은 사건성 알림을 전달한다.
- 수치 손실/획득, 차단, Source 상태 전환 등 즉시 결과를 짧게 읽히게 한다.

**포함 후보**
- `Load -X`
- `오염 +X`
- `Deposit`
- `Blocked`
- `Source Depleted`
- `Hazard Captured`

**배치 의도**
- 힌트와 가까운 영역에 두되, 설명 문구와 사건 로그가 같은 레벨로 섞이지 않도록 별도 레인으로 본다.
- 상시 HUD에 사건 텍스트를 끼워 넣지 않는다.

### 8.6 레인 5 — 전체 화면 FX 레인
**역할**
- 감정 우선순위가 높은 사건을 화면 전체 차원에서 전달한다.

**포함 후보**
- Hit vignette
- Timeout warning overlay
- Clear / Fail 전환 오버레이
- 짧은 플래시 계열 반응

**배치 의도**
- 정보 전달보다 감각 전달이 우선인 층이다.
- HUD 위에 얹히되 HUD 가독성을 무너뜨릴 정도로 강하지 않게 유지한다.

### 8.7 레인 6 — 월드 인디케이터 레인
**역할**
- 목표와 동선의 공간성을 담당한다.
- 스크린 HUD가 요약한 정보를 실제 공간 위치와 연결한다.

**포함 후보**
- 핵심 Source 강조
- Deposit 위치 유도
- 위험 구역 / 주의 대상 표시
- 고갈 직전 Source의 강조

**배치 의도**
- Source / Deposit / 위험 구역은 "어디에 있는가"가 중요한 정보이므로 월드 안에서 읽히는 편이 자연스럽다.
- 화면 고정 HUD만으로 동선을 지시하려는 시도를 줄인다.

## 9. 권장 화면 배치 요약
```text
┌──────────────────────────────────────────────┐
│                [ Objective ]                 │
│          전체 진행 / 핵심 Source /           │
│          (보조) Timer·StageState             │
│                                              │
│                                              │
│ [ Carry ]                                    │
│ Load/Capacity                                │
│ (인접) HazardStack                           │
│                                              │
│                                              │
│                 플레이 월드                  │
│      Source / Deposit / 위험영역 인디케이터   │
│                                              │
│                                              │
│     [ Hint / Toast ]    [ Notification ]     │
└──────────────────────────────────────────────┘
```

## 10. 정보 분류 기준
| 분류 | 예시 | 기본 원칙 |
| --- | --- | --- |
| 상시 HUD | Carry, Objective | 플레이 내내 반복 확인하는 핵심 판단 정보만 포함 |
| 반상시 정보 | HazardStack, Timer 보조 | 자리는 있으나 존재감은 낮게, 갱신 순간만 강하게 |
| 문맥형 힌트 | 첫 Deposit 필요, 첫 피격 설명 | 학습/유도 목적, 상시 HUD와 분리 |
| 사건형 알림 | `Load -X`, `Blocked`, `Depleted` | 짧고 즉시적인 사건 전달 전용 |
| 화면 FX | hit vignette, timeout overlay | 감정/긴장 전달 우선 |
| 월드형 인디케이터 | Source, Deposit, 위험영역 | 위치성과 방향성을 공간 안에서 전달 |

## 11. 현재 기준안
- 좌측은 Carry 축으로 사용한다.
- 상단 중앙은 Objective 축으로 사용한다.
- HazardStack은 Carry에 붙는 반상시 보조 요소로 둔다.
- 하단 중앙은 Hint / Notification 전용 레인으로 사용한다.
- 전체 화면 가장자리는 Screen FX 레인으로 사용한다.
- Source / Deposit의 방향성과 위치성은 월드 인디케이터가 담당한다.

## 12. 열어둘 결정
- Timer를 Objective 바 내부에 포함할지, 우측 보조 슬롯으로 분리할지
- Hint와 Notification을 시각적으로 두 줄 분리할지, 한 레인 안에서 상태 전환형으로 운영할지
- HazardStack의 표현을 숫자 중심으로 둘지, 세그먼트/링 기반으로 둘지
- 월드 인디케이터의 적극도를 최소 유도선 수준으로 둘지, 강한 목표 유도로 둘지

## 13. 후속 상세 설계 항목
- Carry 블록 내부 구성
- Objective 블록 내부 구성
- 하단 중앙 레인의 우선순위 규칙(Hint vs Notification)
- HazardStack 갱신/리셋의 피드백 방식
- Screen FX 강도와 광과민성/가독성 기준
- 월드 인디케이터의 노출 조건과 강도 기준

## 14. 변경 이력
- 2026-03-13: 초안 작성
