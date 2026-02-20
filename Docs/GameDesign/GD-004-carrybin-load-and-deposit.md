# CarryBin 기획서 (v1)

## Metadata
- doc_id: `GD-004`
- type: `GameDesign`
- status: `active`
- last_updated: `2026-02-12`
- related_adr: [ADR-20260212-04-carrybin-replaces-score-placeholder.md](../ADR/ADR-20260212-04-carrybin-replaces-score-placeholder.md), [ADR-20260219-05-carrybin-deposit-touch-request-execution.md](../ADR/ADR-20260219-05-carrybin-deposit-touch-request-execution.md)

## 0. 목적
CarryBin은 "수거량"을 런 내 규칙으로 승격시켜,
- Source 기반 고갈/이동 동기를 강화하고
- Hazard의 조건부 수거(기회/욕심) 보상을 "흐름 변화"로 만들며
- DOTS 대량 탄막(대량 수거/처리량)이 플레이 규칙상 의미를 갖도록 한다.

---

## 1. 용어 / 데이터 모델

### 1.1 런 내 자원: CarryBinLoad
- 의미: 플레이어가 현재 들고 있는 "적치량(Load)"
- 범위: `0 ~ Capacity`
- 증가: 쓰레기(일반 탄) 수거, Hazard 수거 성공 보상(스파이크)

### 1.2 런 외 자원: MetaScrap (정산)
- 의미: 배출 지점(Deposit)에서 CarryBinLoad를 "확정"한 런 외 자원
- 사용처: 메타 성장(해금/강화/영구 해제 등) 또는 메타에 영향을 주는 누적치

### 1.3 Deposit(배출 지점)
- 성격: **안정적으로 존재**(항상 접근 가능)
- 역할: CarryBinLoad를 MetaScrap으로 전환하는 유일한 정산 수단

---

## 2. 최소 플레이 루프 정의 (런 내 가장 작은 단위)
플레이어가 Source에 "깊게 진입"하고, 무사히 빠져나와 정산하는 흐름을 최소 루프로 고정한다.

1) **Ingress (진입)**
   - Source 외곽 → 내부로 파고듦

2) **Harvest (수거/고갈 진행)**
   - 쓰레기 수거로 CarryBinLoad를 채우고
   - Source의 약화/고갈을 진행(체감상 "여긴 치웠다" 형성)

3) **Egress (탈출/정산)**
   - Deposit으로 이동하여 CarryBinLoad를 정산(MetaScrap 획득)
   - 다음 Source로 이동 동기 확보

---

## 3. 핵심 규칙

### 3.1 수거 → 적치 (Load)
- 일반 쓰레기 수거:
  - `CarryBinLoad += w` (기본 `w=1`, 탄종/Source에 따라 가중치 확장 가능)
- 용량:
  - `CarryBinLoad`는 `Capacity`를 초과할 수 없다.

### 3.2 적치 → 정산 (Bank)
- Deposit에서만 정산 가능:
  - `MetaScrapGain = Settlement(CarryBinLoad, Depth, Streak, etc.)`
  - 정산 후 `CarryBinLoad = 0` (권장: 완전 비움; 변형 가능)

---

## 4. "깊게 진입"을 보상으로 만드는 장치: 정산 배율(Depth Multiplier)

### 4.1 설계 의도
배출 지점이 안정적으로 존재하면, 재미는 "언제 돌아갈지"의 선택에서 나온다.
이를 위해 "깊이"를 정산 이득과 직접 연결한다.

### 4.2 정산 공식 (기본형)
- `MetaScrapGain = CarryBinLoad * BaseRate * DepthMultiplier * (Optional)StreakMultiplier`

권장 기본값:
- `BaseRate = 1.0` (초기에는 단순하게 유지)
- `DepthMultiplier`는 체감 튜닝이 쉬운 범위로 제한(예: `1.0 ~ 2.0`)

### 4.3 Depth(깊이) 산정 방식 (추천)
A안(추천): **최근 N초 동안 상호작용한 Source 위험 레벨 기반**
- 플레이어가 실제로 "그 Source에 잠수했다"를 가장 자연스럽게 반영
- 거리가 아닌 "활동 기반"이라 악용(살짝 들어갔다 나오는) 억제가 쉬움

대안:
- B안: Source 중심으로부터 거리(링) 기반
- C안: Source 약화/고갈 단계 기반(깊이 대신 "정리 진행도"로 보상)

---

## 5. Hazard와 CarryBin 결합

### 5.1 보상 철학
Hazard 보상은 점수보다 "흐름을 바꾸는 보상"이어야 한다.
따라서 CarryBin과 직접 결합한다.

### 5.2 보상 방식(권장)
- Hazard 조건부 수거 성공 시:
  - `CarryBinLoad += SpikeAmount`
  - SpikeAmount는 "일반 쓰레기 다수 분량"으로 체감되게 설정

(선택) 연계 보상:
- `StreakMultiplier`: Hazard 성공 연속 달성 시 정산 배율에 보너스
- 단, Streak는 UI 피드백이 충분해야 "억지 시스템"으로 느껴지지 않는다.

---

## 6. CarryBinLoad 기반 리스크 증폭(환경 반응형)

### 6.1 설계 의도
속도 감소 같은 단순 디버프 대신, **환경이 플레이어를 더 노리는** 형태가
- "욕심의 긴장감"을 만들고
- cap/분포 기반 튜닝 철학과 일치한다.

### 6.2 권장 리스크 레버(피격 패널티와 무관하게 독립 운용)
CarryBinLoad가 높을수록:
- Hazard **동시 존재량(active cap)** 증가
- Hazard **패킷(뭉침) 생성 빈도** 증가
- (선택) captureActiveTime의 소폭 감소(타이밍 정확도 요구 강화)

> 목표: "많이 들고 있을수록 지금이 욕심 구간"이라는 체감을 명확히 만든다.

---

## 7. UX / 피드백 요구사항

### 7.1 HUD 필수 요소
- CarryBinLoad / Capacity 게이지
- 현재 DepthMultiplier(정산 배율) 실시간 표시
- (선택) Streak 상태(있다면)

### 7.2 Deposit 접근 피드백
- "지금 정산하면 MetaScrapGain이 얼마"를 크게 노출
- 플레이어가 "더 들어갈지 / 지금 나갈지"를 즉시 판단할 수 있어야 한다.

### 7.3 욕심 구간 경고(권장)
- CarryBinLoad 임계치 이상에서 화면/사운드로 위험도를 명확히 표기
- 목표: 결과가 불쾌가 아닌 "내가 욕심냈다"로 해석되게 만들기

---

## 8. 튜닝 파라미터 목록 (밸런싱 레버)

### 8.1 CarryBin
- `Capacity`
- `w` (기본 수거 가중치)
- `SpikeAmount`

### 8.2 정산(Depth/Multiplier)
- `BaseRate`
- `DepthMultiplierMin/Max`
- `DepthSampleWindowSeconds` (A안 기준)

### 8.3 리스크(Load 연동)
- `ActiveCapBonusByLoad`
- `PacketChanceBonusByLoad`
- (선택) `CaptureActiveTimePenaltyByLoad`

### 8.4 (선택) Streak
- `StreakMultiplierStep`
- `StreakDecayTime`

---

## 9. 오픈 이슈(결정 필요)
1) 정산 후 CarryBinLoad 처리:
   - 완전 비움(권장) vs 일부 유지(이동 루프 약화 가능)
2) Depth 산정 방식 최종 선택:
   - 활동 기반(A) vs 거리(B) vs 진행도(C)
3) SpikeAmount의 체감 기준:
   - "일반 쓰레기 몇 개 분량"을 기준으로 초기 목표치를 고정할지

---


