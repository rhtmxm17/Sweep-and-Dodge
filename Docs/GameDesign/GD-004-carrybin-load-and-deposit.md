# CarryBin 기획서 (v2)

## Metadata
- doc_id: `GD-004`
- type: `GameDesign`
- status: `active`
- last_updated: `2026-02-26`
- related_adr: [ADR-20260212-04-carrybin-replaces-score-placeholder.md](../ADR/ADR-20260212-04-carrybin-replaces-score-placeholder.md), [ADR-20260219-05-carrybin-deposit-touch-request-execution.md](../ADR/ADR-20260219-05-carrybin-deposit-touch-request-execution.md)

## 0. 목적
CarryBin은 "수거량"을 런 내 규칙으로 승격시켜,
- Source 기반 고갈/이동 동기를 강화하고
- Hazard의 조건부 수거(기회/욕심) 보상을 "흐름 변화"로 만들며
- `RiskMultiplier`를 통해 고위험 유지 플레이의 효율 상승을 설계 가능한 형태로 고정하고
- DOTS 대량 탄막(대량 수거/처리량)이 플레이 규칙상 의미를 갖도록 한다.

---

## 1. 용어 / 데이터 모델

### 1.1 런 내 자원: CarryBinLoad
- 의미: 플레이어가 현재 들고 있는 "적치량(Load)"
- 범위: `0 ~ Capacity`
- 증가: 쓰레기(일반 탄) 수거, Hazard 수거 성공 보상(스파이크)

### 1.2 런 내 위험 누적치: HazardStack
- 의미: 고위험 플레이 구간에서 누적되는 위험 스택
- 역할: `RiskMultiplier`의 추가 계수
- 상한: `HazardStackMax` (상한 도달 후 증가 정지)

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
- 런타임 기본식(계약 기준):
  - `RiskMultiplier = 1 + (Load / Capacity) * RiskFactor + (HazardStack * HazardBonusRate)`
- 진행도 반영:
  - `TrashProgressDelta = BaseTrashValue * RiskMultiplier`
  - `HazardProgressDelta = BaseHazardValue * RiskMultiplier`
- 의도:
  - 더 오래 욕심을 유지할수록 진행 효율은 올라간다.
  - 동시에 피격/과밀 리스크도 함께 증가한다.

### 3.3 Deposit 리셋
- Deposit은 점수/메타 정산 장치가 아니다.
- Deposit에서만 리셋 가능:
  - `CarryBinLoad = 0`
  - `HazardStack = 0`

---

## 4. Hazard와 CarryBin 결합

### 4.1 보상 철학
Hazard 보상은 점수보다 "흐름을 바꾸는 보상"이어야 한다.
따라서 CarryBin과 직접 결합한다.

### 4.2 보상 방식(권장)
- Hazard 조건부 수거 성공 시:
  - `CarryBinLoad += SpikeAmount`
  - SpikeAmount는 "일반 쓰레기 다수 분량"으로 체감되게 설정

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
- `BaseTrashValue`
- `BaseHazardValue`
- `RiskFactor`
- `HazardBonusRate`
- `HazardStackMax`

### 7.3 리스크(Load 연동)
- `ActiveCapBonusByLoad`
- `PacketChanceBonusByLoad`
- (선택) `CaptureActiveTimePenaltyByLoad`

---

## 8. 오픈 이슈(결정 필요)
1) Deposit 트리거 방식:
   - 접촉 즉시 vs 접촉+인터랙션
2) SpikeAmount의 체감 기준:
   - "일반 쓰레기 몇 개 분량"을 기준으로 초기 목표치를 고정할지
3) Load 연동 리스크 레버의 우선순위:
   - `active cap` 우선 vs `packet 빈도` 우선

---
