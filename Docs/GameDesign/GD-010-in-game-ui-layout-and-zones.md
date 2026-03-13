# 인게임 UI 화면 구성 및 영역 계획 (GD-010)

## Metadata
- doc_id: `GD-010`
- type: `GameDesign`
- status: `draft`
- last_updated: `2026-03-13`
- related_docs:
  - [GD-001-campaign-loop-design.md](./GD-001-campaign-loop-design.md)
  - [GD-004-carrybin-load-and-deposit.md](./GD-004-carrybin-load-and-deposit.md)
  - [GD-006-hazard-conditional-capture-system.md](./GD-006-hazard-conditional-capture-system.md)
  - [GD-008-demo-flow-design.md](./GD-008-demo-flow-design.md)
  - [GD-009-in-game-ui-screen-blueprint.md](./GD-009-in-game-ui-screen-blueprint.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [TD-011-runtime-player-hud-contract.md](../TechnicalDesign/TD-011-runtime-player-hud-contract.md)
  - [TD-013-player-feedback-presentation-bridge-contract.md](../TechnicalDesign/TD-013-player-feedback-presentation-bridge-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](../TechnicalDesign/TD-016-runtime-ui-shell-and-navigation-contract.md)

> [GD-009](./GD-009-in-game-ui-screen-blueprint.md)에서 정리한 인게임 UI 청사진을 영역별 기준안으로 풀어쓰는 후속 상세 기획 문서. Carry 블록, Objective 블록, 하단 중앙 레인, 월드 인디케이터의 역할과 우선순위를 구체화한다.

## 1. 목적
- `GD-009` 청사진을 바탕으로 주요 영역별 역할과 정보 우선순위를 구체화한다.
- 인게임 UI를 개별 위젯이 아니라 플레이 화면 전체의 점유 구조와 역할 분담 관점에서 정리한다.
- 플레이어가 "내 상태", "현재 목표", "방금 일어난 일", "지금 어디로 가야 하는가"를 서로 다른 위치에서 안정적으로 읽도록 화면 구성을 정리한다.
- Carry / Objective / Hint / Notification / FX / 월드 인디케이터의 책임 범위를 명확히 분리해 후속 상세 논의의 기준선을 만든다.

## 2. 적용 범위
- 스테이지 플레이 중 표시되는 인게임 UI의 상위 구조
- 화면 레이어 및 레인별 역할 정의
- Carry 블록과 Objective 블록의 내부 정보 우선순위
- 하단 중앙 레인의 Hint / Notification 운영 원칙
- 월드 인디케이터의 대상과 제한 규칙

## 3. 비범위
- 개별 위젯의 최종 시각 스타일, 색상, 아이콘 디자인, 애니메이션 수치
- UI 프리팹 구조, Presenter 책임, 이벤트 채널 매핑 등 기술 설계
- Title / Lobby / Result / Demo Complete 등 스테이지 외부 화면 상세
- 카피 최종 문안, 사운드 큐, 접근성 세부 옵션
- `GD-009`의 상위 청사진 자체를 대체하거나 다시 정의하는 일

## 4. 가정/제약
- 인게임 화면의 최우선은 탄막과 공간 판독이며, 고정 UI는 이를 과도하게 가리지 않아야 한다.
- 현재 코어 플레이 루프는 `수거 -> Carry 증가 -> Capacity 도달 시 Deposit 이동 -> 재진입` 흐름을 반복하며 모든 Source를 `Depleted`까지 밀어붙이는 구조를 가진다.
- Stage 1에서는 첫 60초 내 핵심 루프를 이해시켜야 하므로, 문맥형 힌트는 상시 HUD가 아니라 별도 레인으로 운용한다.
- Carry는 단순 보유량 표시가 아니라 행동 전환 신호로 취급한다.
- 월드 인디케이터는 위치 정보 전용 레이어로 제한하며, 일반적인 위험 구역 상시 강조는 지양한다.

## 5. 경험 목표
| 목표 | 의도 |
| --- | --- |
| 빠른 상태 파악 | Carry와 Objective를 짧은 시선 이동만으로 읽게 한다. |
| 행동 전환 명료화 | "더 담기 / 비우러 가기 / 현재 Source를 더 밀기"의 판단 전환을 빠르게 지원한다. |
| 사건 가독성 확보 | 피격, Deposit, 차단, Source 단계 전환을 상시 HUD와 분리해 읽히게 한다. |
| 화면 중앙 보존 | 플레이 월드와 탄막 판독을 가리는 고정 UI 면적을 최소화한다. |
| 학습과 실전의 분리 | 온보딩/힌트 문구가 HUD 본체를 오염시키지 않도록 별도 레인으로 운영한다. |
| 공간 판단 보조 | HUD는 무엇이 중요한지 요약하고, 월드 인디케이터는 그것이 어디 있는지를 알려준다. |

## 6. 핵심 원칙
### 6.1 상시 HUD 본체는 Carry + Objective 2축으로 유지한다
상시 HUD 본체는 다음 두 축을 기준안으로 둔다.
- Carry: 지금 더 담을 수 있는가 / 지금 비워야 하는가
- Objective: 스테이지 목표가 얼마나 남았는가 / 지금 어떤 Source를 밀고 있는가

### 6.2 HazardStack은 상시 본체가 아니라 Carry 인접의 반상시 보조층으로 둔다
HazardStack은 시스템적으로 배율 계산에 영향을 주지만, 플레이 체감상 보너스 텐션에 가깝다.
따라서 Carry와 붙여 읽히게 하되, 상시 본체보다 갱신 반응이 강조되는 보조층으로 취급한다.

### 6.3 Hint와 Notification은 같은 구역을 쓰더라도 다른 언어를 사용한다
- Hint: 지금 무엇을 해야 하는가
- Notification: 방금 무슨 일이 일어났는가
두 정보는 동일한 하단 중앙 영역을 쓰더라도 문장 성격과 체류 시간이 달라야 한다.

### 6.4 위치성은 월드에서 처리한다
HUD는 중요 대상의 요약을 담당하고, 실제 위치와 방향성은 월드 인디케이터가 담당한다.

### 6.5 위험 관련 월드 인디케이터는 패턴 전조 중심으로 제한한다
일반적인 위험 구역 상시 표시는 지양한다.
위험 관련 표시가 필요하다면, 특정 탄막 패턴이나 공간 위험이 곧 형성된다는 사실을 미리 인지시키는 전조 표시 위주로 제한한다.

## 7. 화면 레인 구조
본 문서의 레인 구조는 `GD-009`의 레이어 분류를 실제 배치 슬롯 관점에서 풀어쓴 것이다.
### 7.1 레인 1 — 상단 중앙 Objective 레인
**역할**
- 현재 스테이지 목표를 요약한다.
- 전체 진행도와 현재 핵심 Source를 상기시킨다.
- 필요 시 Timer / StageState를 보조 정보로 붙인다.

### 7.2 레인 2 — 좌측 Carry 레인
**역할**
- 현재 런에서 가장 자주 확인하는 자기 상태 축을 담당한다.
- Carry 증감과 Capacity 접근 여부를 빠르게 읽히게 한다.

### 7.3 레인 2 보조 — Carry 인접 Hazard 구역
**역할**
- HazardStack을 Carry와 연결된 반상시 보너스 층으로 보여준다.
- 성공 시 누적 텐션, Deposit / Hit 시 리셋 감각을 짧게 전달한다.

### 7.4 레인 3 — 하단 중앙 Hint 레인
**역할**
- 온보딩, 문맥형 행동 유도, 실패 후 재시도 방향 제시를 담당한다.
- 상시 HUD를 설명문으로 오염시키지 않는다.

### 7.5 레인 4 — 하단 중앙 Notification 레인
**역할**
- 피격 손실, Deposit, 차단, Source 상태 전환 등 사건 결과를 짧게 전달한다.

### 7.6 레인 5 — 전체 화면 FX 레인
**역할**
- 피격, Timeout, Clear / Fail 등 감정 우선순위가 높은 사건을 화면 전체 차원에서 전달한다.

### 7.7 레인 6 — 월드 인디케이터 레인
**역할**
- 목표와 동선의 공간성을 담당한다.
- HUD가 요약한 중요 대상을 실제 월드 위치와 연결한다.

## 8. 권장 화면 배치 요약
```text
┌──────────────────────────────────────────────┐
│                [ Objective ]                 │
│        전체 진행 / 핵심 Source / Timer       │
│                                              │
│ [ Carry ]                                    │
│ Load/Capacity                                │
│ (인접) HazardStack                           │
│                                              │
│                 플레이 월드                  │
│      Source / Deposit / 패턴 전조 표시       │
│                                              │
│      [ Notification ]                        │
│      [ Hint / Toast ]                        │
└──────────────────────────────────────────────┘
```

## 9. Carry 블록 내부 구성
### 9.1 역할
Carry 블록은 "지금 얼마나 담았는가"를 넘어서, "계속 수거 가능한가"와 "이제 Deposit으로 빠져야 하는가"를 가장 빠르게 읽히게 만드는 행동 전환 블록이다.

### 9.2 권장 구성
1. **메인 게이지**
   - `Load / Capacity` 비율을 가장 먼저 읽히게 한다.
   - Carry 블록의 시각적 중심은 숫자가 아니라 게이지여야 한다.
2. **수치 텍스트**
   - `Load / Capacity` 형식의 정확한 숫자는 보조 텍스트로 제공한다.
3. **상태 단계 표시**
   - 최소 3단계로 운영한다.
   - 여유 구간
   - 욕심 구간
   - Full 구간
4. **Full 전용 강조 신호**
   - `Load == Capacity`에서는 단순히 가득 찬 수치가 아니라, 행동 전환이 필요하다는 사실을 명확히 전달한다.
5. **HazardStack 인접 보조층**
   - Carry 본체 내부 핵심 정보는 아니며, Carry 옆에서 반응형 보너스 감각으로 붙는다.

### 9.3 포함하지 않는 정보
- Deposit 거리/방향 설명
- 피격 손실 수치의 상세 서술
- RiskMultiplier 본체 설명
- 긴 규칙 문장

### 9.4 현재 기준안
- 메인 표현은 `Load / Capacity` 게이지 중심으로 한다.
- 숫자는 보조 텍스트로 둔다.
- 상태 단계는 최소 3단계(여유 / 욕심 구간 / Full)로 둔다.
- Full 상태는 별도 강조가 필요하다.
- HazardStack은 Carry 본체가 아니라 인접 보조층으로 유지한다.

## 10. Objective 블록 내부 구성
### 10.1 역할
Objective 블록은 현재 런의 방향성을 요약한다.
플레이어가 상단을 볼 때 "얼마나 끝냈는가"와 "지금 무엇을 밀고 있는가"를 한 번에 읽게 한다.

### 10.2 권장 구성
1. **전체 스테이지 진행도**
   - 모든 Source 정리 목표의 전체 진척을 보여준다.
2. **현재 핵심 Source 상태**
   - 현재 압박 중인 대표 Source 1개의 상태를 보조로 보여준다.
3. **보조 상태 정보**
   - Timer / StageState는 Objective의 보조 슬롯으로 붙인다.

### 10.3 포함하지 않는 정보
- 모든 Source의 상시 나열
- 긴 목표 설명 문장
- Danger / Hazard 정보의 혼합

### 10.4 현재 기준안
- Objective 블록은 상단 중앙 고정으로 둔다.
- 내부는 `전체 진행 + 현재 핵심 Source`의 2단 구조로 본다.
- Timer / StageState는 보조 슬롯으로 붙인다.
- 모든 Source를 상시 나열하지 않는다.

## 11. 하단 중앙 레인 운영 규칙
### 11.1 역할 분리
하단 중앙 구역은 내부적으로 아래 두 층으로 분리한다.
- **Notification 레인**: 짧은 사건 결과 전달
- **Hint 레인**: 행동 유도와 상황 해석 전달

### 11.2 Notification 레인 규칙
**적합한 정보**
- `Load -X`
- `오염 +X`
- `Deposit`
- `Blocked`
- `Source Depleted`
- `Hazard Captured`

**원칙**
- 짧고 즉시 읽혀야 한다.
- 사건성 강한 정보만 보낸다.
- 설명문이나 규칙 해설은 넣지 않는다.

### 11.3 Hint 레인 규칙
**적합한 정보**
- 첫 이동
- 첫 수집
- 첫 Carry 증가
- 첫 Carry Full
- 첫 Deposit 필요
- 첫 피격
- 첫 실패 후 재시도 방향 제시

**원칙**
- 한 번에 하나의 힌트만 유지한다.
- 상황 설명과 다음 행동 제시에 집중한다.
- 이미 학습한 내용을 과도하게 반복하지 않는다.

### 11.4 우선순위 규칙
하단 중앙 레인은 단순 큐가 아니라 행동 우선순위 큐로 본다.
기본 우선순위는 아래와 같다.
1. 생존/실패 직결 Notification
2. 강제 행동 전환 Notification
3. 온보딩 Hint
4. 반복 설명형 Hint

### 11.5 현재 기준안
- 하단 중앙은 Hint / Notification 전용 레인으로 사용한다.
- 내부적으로 `Notification 위, Hint 아래`의 2층 구조로 본다.
- Notification은 사건 결과형, Hint는 행동 유도형으로 분리한다.
- Hint는 한 번에 1개만 유지한다.
- Notification은 짧고 빠르게 지나가게 한다.

## 12. 월드 인디케이터 규칙
### 12.1 역할
월드 인디케이터는 HUD가 요약한 중요 정보를 실제 공간 위치와 연결한다.
핵심 질문은 "무엇이 중요한가"가 아니라 "그것이 어디 있는가"이다.

### 12.2 대상 분류
#### A. 목적지 인디케이터
대상:
- Deposit
- 현재 핵심 Source

원칙:
- Deposit은 항상 후보지만, Carry가 여유 구간일 때는 존재감을 낮게 둔다.
- Carry가 Near Full / Full에 가까워질수록 Deposit 우선순위를 높인다.
- 현재 핵심 Source는 Objective와 연결되는 대표 대상 1개 중심으로 유지한다.

#### B. 사건 결과 인디케이터
대상:
- 피격 직후 오염 반환 위치
- 고갈 직전 Source
- 고갈 완료 직전/직후 전환 대상

원칙:
- 상시 유지하지 않는다.
- 사건 직후 짧게 강조하고 일반 상태로 복귀한다.

#### C. 패턴 전조 인디케이터
대상:
- 특정 탄막 패턴의 전조
- 곧 위험해질 공간 예고
- 짧은 시간 뒤 회피/이탈 판단이 필요한 구역 예고

원칙:
- 일반적인 위험 구역 상시 강조를 대체하지 않는다.
- 위험 관련 표시는 패턴 전조와 공간 예고 중심으로 제한한다.
- 플레이어가 지금 회피 판단을 바꿔야 하는 순간에만 존재감을 높인다.

### 12.3 포함하지 않는 것
- 일반적인 위험 구역의 지속 강조
- Hazard 밀집 지역 전체의 상시 경고
- 모든 Source / Deposit / 위험 요소의 동시 강조
- 월드 위에 긴 설명문이나 수치 본문을 계속 띄우는 방식

### 12.4 현재 기준안
- 월드 인디케이터는 위치 정보 전용 레이어로 둔다.
- 상시 대상은 Deposit + 대표 Source 중심으로 제한한다.
- 피격 반환 / 고갈 직전은 사건형 공간 피드백으로 처리한다.
- 위험 관련 월드 인디케이터는 일반 위험 구역 표시 대신 패턴 전조 중심으로 제한한다.
- 인디케이터 강도는 플레이어 상태와 사건 맥락에 따라 달라져야 한다.

## 13. 정보 분류 요약
| 분류 | 예시 | 기본 원칙 |
| --- | --- | --- |
| 상시 HUD | Carry, Objective | 플레이 내내 반복 확인하는 핵심 판단 정보만 포함 |
| 반상시 정보 | HazardStack, Timer 보조 | 자리는 있으나 존재감은 낮게, 갱신 순간만 강하게 |
| 문맥형 힌트 | 첫 Deposit 필요, 첫 피격 설명 | 학습/유도 목적, 상시 HUD와 분리 |
| 사건형 알림 | `Load -X`, `Blocked`, `Depleted` | 짧고 즉시적인 사건 전달 전용 |
| 화면 FX | hit vignette, timeout overlay | 감정/긴장 전달 우선 |
| 월드형 인디케이터 | Deposit, 대표 Source, 패턴 전조 | 위치성과 방향성을 공간 안에서 전달 |

## 14. 오픈 이슈
- Carry 블록의 게이지 형태(수평 / 원형 / 기타)는 후속 상세 논의에서 확정한다.
- Objective의 현재 핵심 Source 선정 기준은 별도 정리가 필요하다.
- Timer를 Objective 내부에 둘지, 외곽 보조 슬롯으로 뺄지는 후속 시안 검토 후 결정한다.
- Notification 동시 표출 허용 개수와 Hint 재노출 조건은 후속 UX 상세 규칙으로 분리한다.
- 패턴 전조 인디케이터의 시각 강도와 표현 범위는 실제 플레이 테스트 기준으로 보정이 필요하다.

## 15. 변경 이력
- 2026-03-13: 초안 작성
