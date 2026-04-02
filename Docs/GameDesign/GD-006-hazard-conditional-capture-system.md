# Hazard 조건부 수거 시스템 기획서

## Metadata
- doc_id: `GD-006`
- type: `GameDesign`
- status: `active`
- last_updated: `2026-04-02`
- related_adr:
  - [ADR-20260219-03-player-cleanup-action-profile-so-externalization.md](../ADR/ADR-20260219-03-player-cleanup-action-profile-so-externalization.md)
  - [ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](../ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)
  - [ADR-20260402-01-broomsweep-default-cleanup-action.md](../ADR/ADR-20260402-01-broomsweep-default-cleanup-action.md)

## 0. 전제
- 본 문서는 [`Source 기반 스폰 & 고갈 시스템`](GD-002-source-based-spawn-and-depletion.md)이 이미 적용된 상태를 전제로 한다.
- 목표는 `Trash` 수거 흐름을 유지하면서도, `Hazard` 처리 순간에 집중을 요구하는 액션 리듬을 만드는 것이다.
- Hazard 피격 패널티 상세는 [`GD-005`](GD-005-hazard-collision-penalty-spec.md)를 단일 기준으로 따른다.

---

## 1. 시스템 도입 목적

### 해결하려는 문제
- Source 기반 이동 동기가 충분해진 이후, `Trash` 수거가 지나치게 루틴화되어 긴장 곡선이 약해지는 문제
- 기존 청소 행동 후보 2종이 현재 컨셉인 "빗자루로 휩쓴다"와 직접 대응되지 않는 문제

### 설계 목표
- `Hazard`를 통해 "간헐적 집중 스위치"를 제공한다.
- `Trash`와 `Hazard` 처리를 하나의 행동으로 묶어, 입력은 단순하게 유지하고 체감은 풍부하게 만든다.
- 실패는 억울함이 아니라 "내가 욕심냈다"로 귀결되도록 한다.

---

## 2. 핵심 개념 정의

### 2.1 Hazard란 무엇인가
- `Trash`와 함께 Source 주변에서 혼입되어 등장하는 "위험 오브젝트(위험 탄환)"
- 기본적으로 상시 Vacuum으로는 수거되지 않는다.
- 플레이어 접촉 시 페널티가 발생한다.

▶ `Hazard` = "자원 수거 흐름 속에서 간헐적으로 집중을 요구하는 요소"

---

### 2.2 조건부 수거(Conditional Capture)란 무엇인가
- `Hazard`는 특정 타이밍과 위치 조건에서만 수거 가능해진다.
- 조건을 만족시키기 위해 플레이어는 순간적인 타이밍/거리 판단을 수행한다.

▶ 조건부 수거 = "욕심을 실력으로 바꾸는 한 박자 액션"

---

## 3. 기본 게임플레이 루프 (Hazard 관여 지점)

1. Source 진입 -> `Trash` 수거로 CarryBin 축적
2. `Hazard` 혼입 등장 -> 시선과 동선에 간헐적 부담 제공
3. 플레이어 선택:
   - (A) `Hazard` 무시: 안전하지만 효율이 낮아짐
   - (B) `Hazard` 조건부 수거 시도: 고효율이지만 실패 리스크 존재
4. Source 고갈 구간으로 갈수록 `Hazard` 비율 증가 -> 마무리 구간 긴장 상승
5. CarryBin 과적/손실 위험과 결합 -> 욕심 유도

---

## 4. Hazard 행동 규칙

### 4.1 스폰 규칙
- `Hazard`는 Source 반경 내에서 `Trash`와 함께 생성된다.
- 초기에는 소량(5~10% 미만)으로 시작하며, Source 상태에 따라 비율이 변화한다.

### 4.2 이동/상호작용
- `Hazard`는 기본적으로 상시 Vacuum에 끌려오지 않는다.
- 플레이어와 접촉하면 `GD-005` 규칙으로 피격 페널티가 발생한다.
- 조건부 수거 성공 결과는 CarryBin 상태에 따라 분기한다.
  - `HazardCaptured` (`Load < Capacity`):
    - 수거 + 보상 적용
    - `HazardStack`이 1 증가하며, 증가분은 다음 프레임 수거 배율부터 반영된다.
  - `HazardRemovedWhenCarryFull` (`Load == Capacity`):
    - 제거 전용 처리(디스폰)
    - Carry/Source 진행/HazardStack/`Collect` 미반영

---

## 5. 조건부 수거 설계 (현재 기준안)

### 5.1 발동 방식: Vacuum ON 트리거
- Vacuum을 ON 하는 순간, 짧은 시간 동안 `BroomSweep` 청소 동작이 실행된다.
- `BroomSweep`는 좌->우, 우->좌 방향으로 번갈아 가며 실행된다.
- 플레이어는 "켜는 순간"에 행동을 선택하는 것이 아니라, 하나의 기본 동작을 반복적으로 숙련한다.

▶ 핵심: "켜는 순간"에 집중을 몰아준다.

---

### 5.2 기본 청소 동작: BroomSweep
- 현재 기본 청소 동작은 `BroomSweep` 1종으로 고정한다.
- 레거시 후보였던 `RadialRing`, `ForwardFanLine`은 이번 기준에서 미사용으로 내린다.
- `BroomSweep`는 하나의 액션이지만, 내부적으로는 두 판정을 가진다.
  - `Trash` 수거: 스윕 궤적을 따라가는 판정
  - `Hazard` 수거: 스윕이 정면을 향하는 순간의 강한 판정
- 플레이어 체감상으로는 하나의 빗자루질이지만, 튜닝 메타데이터는 분리한다.

---

### 5.3 Trash 판정: 스윕 부채꼴
- `Trash` 수거는 "얇은 부채꼴 띠" 형태의 판정을 사용한다.
- 의도:
  - 빗자루 머리가 바깥쪽 궤적을 따라 지나가는 느낌을 준다.
  - 밀집된 `Trash`를 넓게 쓸어 담는 체감을 만든다.
- 권장 규칙:
  - 플레이어 바로 발밑은 비워 둔다.
  - 스윕 시작각에서 종료각까지 활성 시간 동안 회전한다.
  - 좌->우와 우->좌는 각도만 미러링한다.

---

### 5.4 Hazard 판정: 정면 직사각형
- `Hazard` 수거는 정면 직사각형 판정을 사용한다.
- 판정은 `BroomSweep`가 정확히 정면을 향하는 짧은 타이밍 창에서만 활성화된다.
- 의도:
  - "빗자루질의 힘이 가장 실리는 순간"을 명확하게 느끼게 한다.
  - `Trash`와는 다른 정확도 요구를 준다.
- 권장 규칙:
  - 정면 기준은 발동 순간에 고정한 플레이어 전방을 사용한다.
  - 활성 중 실시간 조준 변화를 따라가지 않는다.
  - 직사각형 길이/폭과 타이밍 창은 별도로 튜닝한다.

---

### 5.5 이동/방향 제약
- `BroomSweep` 활성 중에는 플레이어 방향을 잠그는 것을 기본값으로 둔다.
- 이동은 완전 봉인하지 않고 감속만 적용한다.
- 이유:
  - 스윕 궤적과 `Hazard` 정면 판정의 기준축을 안정적으로 읽히게 한다.
  - 제자리 고정으로 인한 답답함은 피한다.

권장 기본값:
- `LockFacingWhileActive = true`
- `ActiveMoveSpeedScale = 0.4 ~ 0.6`

---

### 5.6 메타데이터 분리 원칙
- `Trash`와 `Hazard` 판정은 같은 액션에 속하지만, 메타데이터는 분리한다.
- 분리 이유:
  - 판정 형태가 다르다.
  - 조정하고 싶은 체감 축이 다르다.
  - `Trash`는 넓은 쓸기 감각, `Hazard`는 정확한 타이밍 감각이 핵심이다.
- 단, 아래는 공유한다.
  - 활성 시간
  - 좌/우 교대
  - 발동 순간 기준 전방

▶ 요약: "하나의 빗자루질, 두 개의 서브 판정"

---

## 6. 보상/페널티 설계

### 6.1 보상(Reward)
`Hazard` 성공 결과는 `수거`와 `제거`를 분리한다.

- `HazardCaptured` 보상형:
  - 보상은 "점수 증가"만으로는 부족하며, 아래 중 1~2개를 MVP에서 사용한다.
  - CarryBin 즉시 증가: `hazardCarryGain` (`Trash` 다수 분량)
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
- 피격한 `Hazard` 즉시 소멸
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

### 8.2 BroomSweep
| 파라미터 | 권장 초기값 | 설명 |
|---|---:|---|
| `captureActiveTime` | 0.20 sec | Vacuum ON 직후 스윕 활성 시간 |
| `captureCooldown` | 0.4 ~ 0.8 sec (선택) | 연타 방지 |
| `trashSweepInnerRadius` | 1.0 | 발밑 비우기 시작 반경 |
| `trashSweepOuterRadius` | 2.8 ~ 3.2 | 빗자루 궤적 바깥 반경 |
| `trashSweepHalfAngle` | 10 ~ 14 deg | 스윕 띠 두께 |
| `trashSweepStartAngle` | -20 deg | 좌->우 기준 시작각 |
| `trashSweepEndAngle` | +80 deg | 좌->우 기준 종료각 |
| `hazardRectLength` | 2.6 ~ 3.2 | 정면 직사각형 길이 |
| `hazardRectHalfWidth` | 0.45 ~ 0.70 | 정면 직사각형 반폭 |
| `hazardForwardWindowAngle` | 6 ~ 8 deg | 정면 타이밍 허용 창 |
| `lockFacingWhileActive` | true | 활성 중 방향 잠금 |
| `activeMoveSpeedScale` | 0.4 ~ 0.6 | 활성 중 이동 감속 비율 |

주:
- 우->좌 스윕은 각도 부호만 반전한 미러 규칙을 사용한다.
- 정면 통과 시점을 스윙 중간이 아니라 초반/후반에 두고 싶으면 시작/종료각 또는 진행 곡선으로 조정한다.

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
- `Hazard`는 `Trash`와 즉시 구분 가능해야 한다.
  - 실루엣/형태 차이
  - 깜빡임/파동
  - 사운드(위협음)로 인지 보조

### 9.2 Capture 타이밍 피드백
- Vacuum ON 순간, 빗자루질 시작이 읽히는 짧은 예고 FX를 준다.
- `Trash` 스윕 경로는 얇게 읽히고, `Hazard` 정면 판정 타이밍은 더 강하게 읽혀야 한다.
- 성공 시 결과별로 분리된 피드백을 사용한다.
  - `HazardCaptured`: 묵직한 흡수/정리 사운드 + 보상 UI 반응
  - `HazardRemovedWhenCarryFull`: 제거 전용 VFX/사운드 + 비보상 UI 반응

▶ 의도: "지금 힘이 실린다"를 즉시 알린다.

---

## 10. 성공 판단 기준(프로토타입 평가)

### 반드시 확인할 질문
- 플레이어가 `Hazard`를 보자마자 무시만 하는가? (보상/조건 문제)
- 플레이어가 `BroomSweep` 타이밍을 의식적으로 맞추는가? (집중 스위치 성공)
- `Trash`는 넓게 쓸리고, `Hazard`는 정면 타이밍으로 읽히는가? (판정 분리 성공)
- 활성 중 방향 잠금/이동 감속이 읽기성을 올리되 답답함을 과하게 만들지 않는가?
- 실패했을 때 억울함보다 "욕심냈다"가 먼저 드는가? (페널티 적정)
- 고갈 직전 구간이 체감상 더 긴장되는가? (Source 연동 성공)

---

## 11. 튜닝 가이드 (증상 -> 조정 순서)

### 11.1 Hazard를 거의 시도하지 않음
1. `hazardCarryGain` 증가
2. `hazardRectHalfWidth` 증가
3. `hazardForwardWindowAngle` 증가
4. `activeMoveSpeedScale` 상향
5. `carryLossFrac` 감소

### 11.2 Hazard가 너무 쉬워서 의미 없음
1. `hazardForwardWindowAngle` 감소
2. `hazardRectHalfWidth` 감소
3. `captureActiveTime` 0.20 -> 0.15 sec
4. `captureCooldown` 추가/증가

### 11.3 Trash 청소가 너무 답답함
1. `trashSweepHalfAngle` 증가
2. `trashSweepOuterRadius` 증가
3. `activeMoveSpeedScale` 상향

### 11.4 스윕이 흔들려 읽기 어렵다
1. `lockFacingWhileActive`를 유지한다.
2. 정면 통과 구간을 시각적으로 더 강조한다.
3. 필요 시 스윕 진행 곡선을 조정해 정면 부근 체류 시간을 늘린다.

---

## 12. 의도적 범위 제한 (MVP)
- `Hazard` 종류는 1종만 사용
- 청소 액션은 `BroomSweep` 1종만 사용
- `Trash` 부채꼴 판정 + `Hazard` 정면 직사각형 외의 복합 조건은 MVP에서 제외
- Source 이동/폭풍 이동은 적용하지 않는다 (현재 단계에서는 불필요)

---

## 13. 요약 한 줄
**`BroomSweep`는 하나의 빗자루질이지만, `Trash`는 넓게 쓸고 `Hazard`는 정면 순간에 강하게 처리하게 만들어, 단순 입력으로도 숙련과 욕심이 드러나는 액션을 만든다.**
