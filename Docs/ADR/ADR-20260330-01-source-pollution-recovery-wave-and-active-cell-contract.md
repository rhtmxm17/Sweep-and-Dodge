# ADR-20260330-01-source-pollution-recovery-wave-and-active-cell-contract
> `GD-014` 청소 흔적 복구를 시간 기반 전역 regen에서 `active/inactive + recovery wave` 계약으로 승격하고, 1차 구현 범위를 최근 청소 이력 중심으로 제한한 결정

## 배경
- 현재 청소 흔적은 `CellPollution.Value` 단일 스칼라를 기준으로 `drop -> 전역 regen`만 수행한다.
- 이 모델은 `GD-003` MVP의 "방금 청소한 자리가 잠시 비어 보인다"는 체감에는 맞지만, `GD-014`가 요구하는 "영역 활성 비율 하락 시 다른 구역이 다시 관리 대상으로 떠오르는 공간 순환 규칙"과는 다르다.
- `GD-014`는 다음 조건을 동시에 요구한다.
  - 복구 트리거를 개별 셀 타이머가 아니라 영역 상태로 둘 것
  - 방금 청소한 자리를 즉시 복구 후보에서 제외할 것
  - 완전 무작위 점노이즈가 아니라 작은 구역 단위의 파형으로 느껴지게 할 것
  - 최근 청소/최근 체류 구역을 약하게 피하되, 조작감이 과해지지 않게 할 것
- 현재 runtime은 source pollution을 `region bounds local grid + valid cell mask` 기준으로 운영하며, Request 그룹 단일 writer 계약을 이미 갖고 있다.

## 결정
- 청소 흔적 복구 모델은 `CellPollution.Value` 단일 스칼라 해석에서 `active/inactive`가 명시된 2계층 모델로 전환한다.
- `SourcePollutionUpdateSystem`은 계속 Request 그룹 단일 writer로 유지한다.
  - `BulletVacuumRequestSystem`은 drop request만 누적한다.
  - `SourcePollutionUpdateSystem`이 drop 소비, active 셀 regen, inactive cooldown 판정, recovery wave 실행을 단일 책임으로 수행한다.
- recovery trigger는 `영역 active cell ratio` 기반으로 둔다.
  - active ratio가 설정 임계치 아래로 내려간 경우에만 recovery wave를 검토한다.
  - 개별 셀의 단순 시간 만료만으로 즉시 active 복귀시키지 않는다.
- inactive 셀은 `cooldown`이 지난 뒤에만 recovery 후보가 된다.
  - cooldown 기준은 logic frame 기반으로 관리한다.
  - "방금 청소한 자리 즉시 재오염" 체감을 막기 위해 frame/cooldown 계약을 명시한다.
- recovery wave는 seed + localized cluster 방식으로 운영한다.
  - 완전 무작위로 흩뿌리지 않는다.
  - 한 번의 wave는 소수 seed를 고르고, 각 seed 주변 valid neighbor를 제한 수만큼 복구한다.
- `PollutionTopK` sampling은 유지하되, inactive 셀은 read path에서 weight 0으로 취급한다.
  - ExecutionBegin sampling은 read-only를 유지한다.
  - Request 단계 외 writer를 추가하지 않는다.
- 1차 구현의 거리 편향은 `최근 청소 이력`만 사용한다.
  - `최근 체류 구역` 편향은 보류한다.
  - 이유: 현재 runtime은 source 내부 셀 단위 체류 heat를 authoritative하게 보유하지 않으며, 런 디렉터의 source 점유/hold 정보는 source 내부 복구 편향에 재사용하기엔 해상도가 부족하다.

## 대안
- 대안 A: 기존 `Value` 스칼라만 유지하고 threshold로 active/inactive를 암묵 해석한다
  - 장점: 구조 변경이 가장 작다.
  - 단점: `Value`가 스폰 weight와 상태 전이를 동시에 의미하게 되어 튜닝/판독이 불안정해진다.
  - 기각 사유: `GD-014`의 "영역 상태 기반 파형 복구"를 안정적으로 표현하기 어렵다.
- 대안 B: 개별 셀 타이머 만료 시 자동 active 복귀를 유지하고, sampling 편향만 추가한다
  - 장점: 기존 regen 구조를 대부분 보존할 수 있다.
  - 단점: 플레이어 체감이 다시 "잠깐 기다렸다가 다시 오면 된다"로 수렴하기 쉽다.
  - 기각 사유: `GD-014`의 핵심 의도와 맞지 않는다.
- 대안 C: 최근 체류 셀 heatmap까지 이번 단계에서 같이 도입한다
  - 장점: `GD-014` 문구를 가장 직접적으로 충족한다.
  - 단점: player occupancy 수집 owner, source 내부 cell attribution, 추가 검증 범위가 커진다.
  - 기각 사유: 이번 단계는 복구 모델 전환이 우선이며, 체류 heat는 별도 설계로 분리하는 편이 안전하다.

## 결과
- 긍정 효과
  - `GD-014`의 복구 해석을 "시간이 지나면 원복"이 아니라 "영역 상태가 바뀌면 다른 구역이 다시 살아난다"로 고정할 수 있다.
  - active/inactive가 명시되므로 HUD/VFX/디버그 표시와의 연결 지점이 분명해진다.
  - Request 그룹 단일 writer와 ExecutionBegin read-only sampling 구조를 유지해 기존 DOTS ownership을 해치지 않는다.
- 트레이드오프
  - `SourcePollutionCellBuffer`와 config schema, prepare 초기화, sampling 조건, EditMode 테스트까지 연쇄 수정이 필요하다.
  - localized cluster wave는 neighbor 계산과 tuning parameter가 늘어나므로 문서/테스트 없이 구현하면 회귀 위험이 크다.
  - 최근 체류 편향은 후속 범위로 남아 `GD-014` 전체 의도가 한 번에 완결되지는 않는다.

## 후속
- `TD-026`에 data contract, update order, validation 범위를 기록한다.
- `TD-003`의 pollution 갱신 설명은 specialized contract를 `TD-026` 우선 참조로 정리한다.
- 구현 단계에서는 최소 아래 검증을 추가한다.
  - active ratio trigger 동작
  - cooldown 전 셀 제외
  - localized cluster recovery
  - inactive 셀 sampling 제외
  - topology reset/prepare 시 pollution state 초기화
