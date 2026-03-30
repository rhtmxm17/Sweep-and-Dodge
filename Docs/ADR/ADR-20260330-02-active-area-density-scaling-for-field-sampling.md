# ADR-20260330-02-active-area-density-scaling-for-field-sampling
> `GD-014` recovery wave 도입 이후 field sampling directive의 총 스폰량과 density cap을 active-area 비율에 연동한 결정

## 배경
- `active/inactive + recovery wave` 전환 이후 `UniformField` / `PollutionTopK`는 active 셀만 샘플링한다.
- 그러나 request 생성량과 `CapAndMaxDensity` 상한은 여전히 source 전체 `ComputedArea` 기준이라, active 셀이 줄수록 셀당 기대 밀도가 과도하게 높아졌다.
- 이 상태는 `GD-014`가 의도한 "다른 작은 구역이 다시 떠오름"보다 "남은 active 셀에 스폰이 과집중됨"으로 읽히기 쉽다.

## 결정
- field sampling directive의 effective area는 `active valid cell count / valid cell count` 비율로 축소한다.
  - authoritative ratio는 pollution runtime cell state에서 계산한다.
  - `Value` 가중 effective area는 사용하지 않는다.
- 적용 범위는 `UniformField` / `PollutionTopK`만이다.
  - `LineEven` / `PointSet`은 full area 해석을 유지한다.
- 스케일 범위:
  - `RateField` 생성량은 `effectiveArea`를 사용한다.
  - `CapAndMaxDensity` 상한도 `effectiveArea`를 사용한다.
  - `Poisson` / `EventBurst`는 사건량 계산 자체는 유지하고, cap 계산만 축소한다.
- 별도 authoring knob는 추가하지 않는다.
- 같은 Request 프레임에서 `SourcePollutionUpdateSystem`이 갱신한 active cell 상태를 `SourceClipRequestBuildSystem`이 읽는 순서를 명시한다.

## 대안
- 대안 A: 총 스폰량은 유지하고 sampling만 active 셀에 집중
  - 장점: 구현 변경이 가장 작다.
  - 단점: active 셀 감소 시 셀당 기대 밀도가 급격히 증가한다.
  - 기각 사유: `GD-014`의 공간 순환 체감보다 과집중 체감이 커진다.
- 대안 B: source 전체 directive 출력량을 모두 active-area 비율에 연동
  - 장점: source 단위 규칙은 단순하다.
  - 단점: `LineEven` / `PointSet` 같은 비면적 패턴도 의도 없이 약해진다.
  - 기각 사유: field sampling 전용 밀도 규칙과 패턴성 directive의 의미가 섞인다.
- 대안 C: `Value` 가중 effective area 사용
  - 장점: active/inactive 경계보다 부드러운 변화가 가능하다.
  - 단점: weight와 geometry 의미가 다시 섞이고 튜닝/설명이 어려워진다.
  - 기각 사유: 이번 단계는 active-area count 기반 계약을 유지하는 편이 안전하다.

## 결과
- 긍정 효과
  - field sampling에서 cell당 기대 밀도가 active 셀 수 변화에도 대체로 안정된다.
  - recovery wave로 active 영역이 늘고 줄 때 source 총 출력이 함께 반응한다.
  - `LineEven` / `PointSet` 같은 비면적 패턴은 기존 해석을 유지한다.
- 트레이드오프
  - density 기반 request 생성과 cap 계산이 pollution runtime state에 read-only로 의존한다.
  - 같은 source 내부에서도 sampling mode에 따라 density 스케일 적용 여부가 달라진다.

## 후속
- `TD-026`에 active-area density 규칙을 추가한다.
- `TD-003`, `TD-005`에 field sampling directive의 effective area 해석을 동기화한다.
- EditMode에서 rate 축소, cap 축소, non-field 제외, same-frame order를 회귀 검증한다.
