# CarryBin 기획서 (v2)

## Metadata
- doc_id: `GD-004`
- type: `GameDesign`
- status: `active`
- last_updated: `2026-03-16`
- related_adr: [ADR-20260212-04-carrybin-replaces-score-placeholder.md](../ADR/ADR-20260212-04-carrybin-replaces-score-placeholder.md), [ADR-20260219-05-carrybin-deposit-touch-request-execution.md](../ADR/ADR-20260219-05-carrybin-deposit-touch-request-execution.md), [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

## 0. 목적
CarryBin은 "수거량"을 런 내 규칙으로 승격시켜,
- Source 기반 고갈/이동 동기를 강화하고
- Hazard의 조건부 수거(기회/욕심) 보상을 "흐름 변화"로 만들며
- 고위험 유지 플레이가 "빨리 밀 수 있지만 더 아픈" 구간으로 체감되게 만들고
- DOTS 대량 탄막(대량 수거/처리량)이 플레이 규칙상 의미를 갖도록 한다.

---

## 1. 용어 / 경험 변수

### 1.1 런 내 자원: CarryBinLoad
- 의미: 플레이어가 현재 들고 있는 "적치량(Load)"
- 범위: `0 ~ Capacity`
- 증가: 쓰레기(일반 탄) 수거, Hazard 수거 성공 보상(스파이크)

### 1.2 런 내 위험 누적치: HazardStack (경험 변수)
- 의미: 고위험 플레이 구간에서 누적되는 위험 스택
- 역할: "욕심 구간" 강도를 높이는 상태 변수
- 상한: `HazardStackMax` (상한 도달 후 증가 정지)
- 증가: `HazardCaptured` 성공 시 `+1`
- 리셋: `Deposit`, `Hit`

### 1.3 Deposit(배출 지점)
- 성격: **안정적으로 존재**(항상 접근 가능)
- 역할: 보상 정산이 아니라 **리스크 리셋**
  - `CarryBinLoad = 0`
  - `HazardStack = 0`

---

## 2. 최소 플레이 루프 정의 (런 내 가장 작은 단위)
플레이어가 Source에 진입해 수거 효율을 올리고, 위험이 커지면 Deposit으로 리셋하는 흐름을 최소 루프로 고정한다.

1) **Ingress (진입)**
   - Source 외곽 -> 내부 진입

2) **Harvest (수거/고갈 진행)**
   - 쓰레기 수거로 CarryBinLoad를 채우고
   - Hazard 상호작용으로 HazardStack을 누적하며
   - `RiskMultiplier`를 높여 진행 효율을 끌어올린다.
   - Source의 약화/고갈을 진행(체감상 "여긴 치웠다" 형성)

3) **Egress (탈출/리셋)**
   - Deposit으로 이동해 Load/Stack을 리셋
   - 리스크를 초기화하고 다음 진입 구간을 준비
   - 다음 Source로 이동 동기 확보

---

## 3. 핵심 규칙

### 3.1 수거 -> 적치 (Load)
- 일반 쓰레기 수거:
  - `CarryBinLoad += w` (기본 `w=1`, 탄종/Source에 따라 가중치 확장 가능)
- 용량:
  - `CarryBinLoad`는 `Capacity`를 초과할 수 없다.

### 3.2 위험-효율 연동 (RiskMultiplier)
- 의도:
  - 더 오래 욕심을 유지할수록 진행 효율은 올라간다.
  - 동시에 피격/과밀 리스크도 함께 증가한다.
- 플레이어 체감:
  - "지금 더 밀면 빨라진다"와 "지금 맞으면 크게 잃는다"가 동시에 성립해야 한다.
- 계산식/집계 계약은 [TD-002](../TechnicalDesign/TD-002-pattern-wave-progress-runtime-contract.md)에서 관리한다.
- 현행 구현 범위:
  - `RiskMultiplier = 1 + (HazardStack × HazardBonusRate)`만 사용한다.
  - 적용 대상은 `Trash + HazardCaptured`의 Source 진행도다.
  - `HazardCaptured`로 증가한 `HazardStack`은 다음 프레임 수거 배율부터 반영한다.

### 3.3 Deposit 리셋
- Deposit은 점수/메타 정산 장치가 아니다.
- Deposit에서만 리셋 가능:
  - `CarryBinLoad = 0`
  - `HazardStack = 0`
- 현행 기준:
  - 기존 Deposit 요청이 실제로 생성된 경우에만 리셋이 일어난다.
  - 현재 규칙에서는 `Load == 0`이면 Deposit 요청이 만들어지지 않는다.

---

## 4. Hazard와 CarryBin 결합

### 4.1 보상 철학
Hazard 보상은 점수보다 "흐름을 바꾸는 보상"이어야 한다.
따라서 CarryBin과 직접 결합한다.

### 4.2 보상 방식(권장)
- `HazardCaptured` (`Load < Capacity`) 성공 시:
  - `CarryBinLoad += SpikeAmount`
  - `HazardStack = min(HazardStack + 1, HazardStackMax)`
  - SpikeAmount는 "일반 쓰레기 다수 분량"으로 체감되게 설정
  - 증가한 `HazardStack`은 같은 프레임이 아니라 다음 프레임 `RiskMultiplier`부터 반영된다.
- `HazardRemovedWhenCarryFull` (`Load == Capacity`) 성공 시:
  - Hazard는 `제거`로만 처리한다.
  - `SpikeAmount`는 적용하지 않는다.
  - CarryBin/Source 진행/HazardStack/`Collect` 집계는 갱신하지 않는다.

---

## 5. CarryBinLoad 기반 리스크 증폭(환경 반응형)

### 5.1 설계 의도
속도 감소 같은 단순 디버프 대신, **환경이 플레이어를 더 노리는** 형태가
- "욕심의 긴장감"을 만들고
- cap/분포 기반 튜닝 철학과 일치한다.

### 5.2 권장 리스크 레버(피격 패널티와 무관하게 독립 운용)
CarryBinLoad가 높을수록:
- Hazard **동시 존재량(active cap)** 증가
- Hazard **패킷(뭉침) 생성 빈도** 증가
- (선택) `captureActiveTime`의 소폭 감소(타이밍 정확도 요구 강화)

> 목표: "많이 들고 있을수록 지금이 욕심 구간"이라는 체감을 명확히 만든다.

---

## 6. UX / 피드백 요구사항

### 6.1 HUD 필수 요소
- CarryBinLoad / Capacity 게이지
- 현재 `RiskMultiplier` 실시간 표시
- 현재 HazardStack 표시

### 6.2 Deposit 접근 피드백
- "지금 Deposit하면 리스크가 리셋된다"를 크게 노출
- 리셋 후 예상 상태(Load/HazardStack 초기화)를 즉시 인지 가능해야 한다.

### 6.3 욕심 구간 경고(권장)
- CarryBinLoad 임계치 이상에서 화면/사운드로 위험도를 명확히 표기
- 목표: 결과가 불쾌가 아닌 "내가 욕심냈다"로 해석되게 만들기

---

## 7. 튜닝 파라미터 목록 (밸런싱 레버)

### 7.1 CarryBin
- `Capacity`
- `w` (기본 수거 가중치)
- `SpikeAmount`

### 7.2 Progress / RiskMultiplier
- Progress 기본 가치(Trash/Hazard 상대 가치)
- 욕심 구간 효율 기울기(Load 연동)
- 위험 누적 보너스 강도(HazardStack 연동)
- 위험 누적 상한(HazardStack cap)

### 7.3 리스크(Load 연동)
- `ActiveCapBonusByLoad`
- `PacketChanceBonusByLoad`
- (선택) `CaptureActiveTimePenaltyByLoad`

---

## 8. 오픈 이슈(결정 필요)
1) SpikeAmount의 체감 기준:
   - "일반 쓰레기 몇 개 분량"을 기준으로 초기 목표치를 고정할지
2) Load 연동 리스크 레버의 우선순위:
   - `active cap` 우선 vs `packet 빈도` 우선

현행 기준(구현 반영):
- Deposit은 **접촉 즉시 비우기**를 사용한다.
- `RiskMultiplier`는 현재 `HazardStack` 항만 사용한다.
- 같은 프레임 수거와 `Hit/Deposit`이 겹치면, 수거를 먼저 확정한 뒤 리셋이 최종 상태를 덮는다.

---
