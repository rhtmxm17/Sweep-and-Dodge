# Hazard 조건부 수거 시스템 기획서

## Metadata
- doc_id: `GD-006`
- type: `GameDesign`
- status: `active`
- last_updated: `2026-03-16`
- related_adr:
  - [ADR-20260219-02-cleanup-action-branching-by-profile.md](../ADR/ADR-20260219-02-cleanup-action-branching-by-profile.md)
  - [ADR-20260219-03-player-cleanup-action-profile-so-externalization.md](../ADR/ADR-20260219-03-player-cleanup-action-profile-so-externalization.md)
  - [ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](../ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

## 0. 전제
- 본 문서는 [`Source 기반 스폰 & 고갈 시스템`](GD-002-source-based-spawn-and-depletion.md)이 이미 적용된 상태를 전제로 한다.
- 목표는 Trash(자원) 수거 흐름을 유지하면서도, "집중해야 하는 순간"을 추가해 플레이 밀도를 높이는 것이다.
- Hazard 피격 패널티 상세는 [`GD-005`](GD-005-hazard-collision-penalty-spec.md)를 단일 기준으로 따른다.

---

## 1. 시스템 도입 목적

### 해결하려는 문제
- Source 기반 이동 동기가 충분해진 이후, Trash 수거가 "무난하고 비어있는 느낌"을 주는 문제
- 제자리 수거는 해결됐지만, 수거 자체가 루틴화되어 긴장 곡선이 약해지는 문제

### 설계 목표
- Hazard를 통해 "간헐적 집중 스위치"를 제공한다.
- Hazard는 회피만을 강요하는 대상이 아니라, 플레이어가 위험을 감수하고 보상을 노리는 선택지로 설계한다.
- 실패는 억울함이 아니라 "내가 욕심냈다"로 귀결되도록 한다.

---

## 2. 핵심 개념 정의

### 2.1 Hazard란 무엇인가
- Trash와 함께 Source 주변에서 혼입되어 등장하는 "위험 오브젝트(위험 탄환)"
- 기본적으로 Vacuum(흡입)으로는 수거 불가
- 플레이어 접촉 시 페널티가 발생한다.

▶ Hazard = "자원 수거 흐름 속에서 간헐적으로 집중을 요구하는 요소"

---

### 2.2 조건부 수거(Conditional Capture)란 무엇인가
- Hazard는 특정 조건에서만 수거 가능해진다.
- 조건을 만족시키기 위해 플레이어는 순간적인 타이밍/거리 판단을 수행한다.

▶ 조건부 수거 = "욕심을 실력으로 바꾸는 한 박자 액션"

---

## 3. 기본 게임플레이 루프 (Hazard 관여 지점)

1. Source 진입 → Trash 수거로 CarryBin 축적
2. Hazard 혼입 등장 → 시선과 동선에 간헐적 부담 제공
3. 플레이어 선택:
   - (A) Hazard 무시: 안전하지만 효율이 낮아짐
   - (B) Hazard 조건부 수거 시도: 고효율이지만 실패 리스크 존재
4. Source 고갈 구간으로 갈수록 Hazard 비율 증가 → 마무리 구간 긴장 상승
5. CarryBin 과적/손실 위험과 결합 → 욕심 유도

---

## 4. Hazard 행동 규칙

### 4.1 스폰 규칙
- Hazard는 Source 반경 내에서 Trash와 함께 생성된다.
- 초기에는 소량(5~10% 미만)으로 시작하며, Source 상태에 따라 비율이 변화한다.

### 4.2 이동/상호작용
- Hazard는 기본적으로 Vacuum에 끌려오지 않는다.
- 플레이어와 접촉하면 `GD-005` 규칙으로 피격 페널티가 발생한다.
- 조건부 수거 성공 결과는 CarryBin 상태에 따라 분기한다.
  - `HazardCaptured` (`Load < Capacity`):
    - 수거 + 보상 적용
    - `HazardStack`이 1 증가하며, 증가분은 다음 프레임 수거 배율부터 반영된다.
  - `HazardRemovedWhenCarryFull` (`Load == Capacity`):
    - 제거 전용 처리(디스폰)
    - Carry/Source 진행/HazardStack/`Collect` 미반영

---

## 5. 조건부 수거 설계 (MVP 권장안)

### 5.1 발동 방식: Vacuum ON 트리거
- Vacuum을 ON 하는 순간, 짧은 시간 동안 "강력한 수거 순간"
- 강력한 수거 순간 동안 특정 거리 조건을 만족하는 Hazard를 수거할 수 있다.

▶ 핵심: "켜는 순간"에 집중을 몰아준다 (유지형이 아님)

---

### 5.2 거리 조건: Capture Ring(링 밴드)
- 플레이어 중심 반경 `captureRingRadius`를 기준으로, 폭 `captureRingWidth`를 가진 링 밴드 내 Hazard만 수거 가능
- 너무 가까운 Hazard를 즉시 처치하는 흐름을 막고, "정확한 위치"를 요구한다.

권장 초기 체감:
- 링 폭은 넉넉하게 시작(시도 유도)
- 타이밍은 짧게 시작(집중 유도)

---

### 5.3 추가 제약: 발동 쿨다운(선택)
- Vacuum ON/OFF를 연타해서 Hazard를 난이도 없이 처리하는 것을 방지
- `captureCooldown` 동안 재발동 불가

※ MVP에서는 쿨다운을 약하게 넣거나, 필요 시에만 적용한다.

---

### 5.4 청소 액션 분기(경험 규칙)
Hazard 조건부 수거는 단일 조작이 아니라, 상황에 맞는 "행동 선택" 경험을 포함한다.

- 기본 액션 2종:
  - `RadialRing`: 주변 안정 정리에 유리한 기본형
  - `ForwardFanLine`: 전방 돌파/정밀 정리에 유리한 지향형
- 슬롯 전환:
  - 플레이어는 `Primary/Secondary` 슬롯을 통해 액션을 빠르게 전환한다.
  - 의도는 "같은 위험 상황에서도 내 선택으로 대응 방식이 달라진다"는 체감을 주는 것이다.
- 입력 소비 체감 규칙:
  - 이미 Vacuum 동작이 진행 중일 때 들어온 전환 입력은 즉시 다음 행동으로 바뀌지 않는다.
  - 즉, "한 번 켠 동작은 끝까지 수행하고 다음 선택은 다음 타이밍에 반영"되는 예측 가능한 리듬을 유지한다.
- 기술 계약(슬롯 해석, Pending 적용 타이밍, 활성 중 입력 소비)은 [TD-012](../TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md)에서 관리한다.

---

## 6. 보상/페널티 설계

### 6.1 보상(Reward)
Hazard 성공 결과는 `수거`와 `제거`를 분리한다.

- `HazardCaptured` 보상형:
  - 보상은 "점수 증가"만으로는 부족하며, 아래 중 1~2개를 MVP에서 사용한다.
  - CarryBin 즉시 증가: `hazardCarryGain` (Trash 다수 분량)
  - HazardStack 증가: 성공 시 `+1`, 다음 프레임 수거부터 `RiskMultiplier`에 반영
  - Source 고갈 진행 가속은 기본적으로 `HazardStack` 경유 배율로 처리한다.
  - 별도 `hazardDepletionBonus` 직접 가산은 선택 확장으로 남긴다.
  - 짧은 버프: 일정 시간 Vacuum 효율 증가(선택)

- `HazardRemovedWhenCarryFull` 제거형:
  - CarryBin/Source 진행/HazardStack/`Collect` 집계 보상은 없다.
  - 목적은 Full 상태의 불쾌한 무효 행동을 줄이고, 위험탄 정리 체감만 제공하는 것이다.

▶ 의도: "지금 먹으면 흐름이 바뀐다"를 체감시키기

---

### 6.2 실패 페널티(Penalty)
즉사는 금지한다. 페널티는 "욕심의 대가"로 명확해야 한다.

피격 확정 시 적용(고정):
- CarryBin 손실:
  - `loss = clamp(floor(carry * carryLossFrac), carryLossMin, carryLossMax)`
- Vacuum 봉인: `vacuumLockTime`
- 무적 프레임: `iFrameTime`
- 피격한 Hazard 즉시 소멸
- 동일 프레임 다중 충돌은 `first-hit wins`(최대 1회 처리)

선택 확장:
- 오염 누적: 일정량 누적 시 이동/시야 페널티(선택)

▶ 의도: 억울함이 아니라 "내가 무리했다"로 귀결

---

## 7. Source 상태 연동 규칙

### 7.1 혼입 비율(추천)
| Source 상태 | Hazard 비율(총 생성 중) | 의도 |
|---|---:|---|
| 정상 | 3~5% | 적응 구간 |
| 약화 | 8~12% | 집중 요구 증가 |
| 고갈 직전 | 15~20% | 마지막 욕심 유도 |

### 7.2 의도
- 초반: 수거 흐름을 학습
- 중반: 판단/집중 빈도 증가
- 마무리: "한 번만 더"를 유도하는 압력 형성

---

## 8. 파라미터 테이블 (Prototype Baseline)

### 8.1 Hazard 생성/분포
| 파라미터 | 권장 초기값 | 설명 |
|---|---:|---|
| `hazardRatioNormal` | 0.04 | 정상 상태 Hazard 비율 |
| `hazardRatioWeakened` | 0.10 | 약화 상태 Hazard 비율 |
| `hazardRatioNearDepleted` | 0.18 | 고갈 직전 Hazard 비율 |
| `hazardMaxActivePerSource` | 2,000 ~ 5,000 | Source당 Hazard 활성 상한(가드) |

### 8.2 조건부 수거(Capture)
| 파라미터 | 권장 초기값 | 설명 |
|---|---:|---|
| `captureActiveTime` | 0.20 sec | Vacuum ON 직후 수거 가능 시간 |
| `captureRingRadius` | VacuumRange × 0.9 | 링 중심 반경 |
| `captureRingWidth` | max(0.8, VacuumRange × 0.25) | 링 폭(초기 넉넉) |
| `captureCooldown` | 0.4 ~ 0.8 sec (선택) | 연타 방지(필요 시) |

### 8.3 보상/페널티
| 파라미터 | 권장 초기값 | 설명 |
|---|---:|---|
| `hazardCarryGain` | Trash 25~50개 분량 | CarryBin 즉시 증가 |
| `hazardDepletionBonus` | Source 수거량 + 1~3%p | 고갈 가속(선택) |
| `carryLossFrac` | 0.10 ~ 0.20 | Carry 비율 손실 |
| `carryLossMin` | 1 ~ 10 | 최소 손실 |
| `carryLossMax` | 10 ~ 50 | 최대 손실(캡) |
| `iFrameTime` | 0.5 ~ 0.9 sec | 추가 피격 방지 시간 |
| `vacuumLockTime` | 0.4 ~ 0.9 sec | 피격 시 Vacuum 봉인 |

---

## 9. 연출/피드백(필수)

### 9.1 식별성
- Hazard는 Trash와 즉시 구분 가능해야 한다.
  - 실루엣/형태 차이
  - 깜빡임/파동
  - 사운드(위협음)로 인지 보조

### 9.2 Capture 타이밍 피드백
- Vacuum ON 순간, 링 밴드가 짧게 표시되거나(약한 FX)
- 짧은 "띵" 또는 "찰칵" 같은 입력 피드백 제공
- 성공 시 결과별로 분리된 피드백을 사용한다.
  - `HazardCaptured`: 묵직한 흡수/정리 사운드 + 보상 UI 반응
  - `HazardRemovedWhenCarryFull`: 제거 전용 VFX/사운드 + 비보상 UI 반응

▶ 의도: "지금이 타이밍"을 즉시 알린다

---

## 10. 성공 판단 기준(프로토타입 평가)

### 반드시 확인할 질문
- 플레이어가 Hazard를 보자마자 무시만 하는가? (보상/조건 문제)
- 플레이어가 "Vacuum ON 타이밍"을 의식적으로 조절하는가? (집중 스위치 성공)
- 실패했을 때 억울함보다 "욕심냈다"가 먼저 드는가? (페널티 적정)
- 고갈 직전 구간이 체감상 더 긴장되는가? (Source 연동 성공)

---

## 11. 튜닝 가이드 (증상 → 조정 순서)

### 11.1 Hazard를 거의 시도하지 않음
1) `hazardCarryGain` 증가
2) `captureRingWidth` 증가
3) `captureActiveTime` 0.20 → 0.25 sec
4) `carryLossFrac` 감소 (필요 시 `carryLossMin` 하향)

### 11.2 Hazard가 너무 쉬워서 의미 없음
1) `captureRingWidth` 감소
2) `captureActiveTime` 0.20 → 0.15 sec
3) `captureCooldown` 추가/증가
4) `hazardRatioNearDepleted` 소폭 증가(마무리 압력만 강화)

### 11.3 다시 탄막 회피 게임처럼 됨
1) `hazardRatio*` 전반 감소
2) 피격 페널티를 즉사/큰 이동 제한에서 "Carry 손실" 중심으로 변경
3) Hazard 이동/판정을 지나치게 공격적으로 만들지 않기

---

## 12. 의도적 범위 제한 (MVP)
- Hazard 종류는 1종만 사용
- "링 + 시간" 외의 복합 조건(게이지 임계, 정밀 거리, 방향 조건)은 MVP에서 제외
- Source 이동/폭풍 이동은 적용하지 않는다 (현재 단계에서는 불필요)

---

## 13. 요약 한 줄
**Hazard는 ‘상시 회피’가 아니라 ‘켜는 순간의 집중’이며,  
큰 보상으로 욕심을 유도하고, 실패는 Carry 손실로 납득되게 만든다.**


