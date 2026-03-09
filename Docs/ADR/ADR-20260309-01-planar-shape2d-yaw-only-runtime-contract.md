# ADR-20260309-01-planar-shape2d-yaw-only-runtime-contract
> `Source / Deposit / Obstacle`의 planar shape raw data를 `Shape2DComponent`로 통일하고 gameplay 판정을 `XZ + yaw-only`로 고정한 결정

## 배경
- 기존 런타임은 `Source`, `Deposit`, `Obstacle`가 서로 다른 shape 표현을 사용했다.
  - `Source`: `BulletFieldAreaComponent`가 raw shape와 `ComputedArea`를 함께 가졌다.
  - `Deposit`: `DepositPointComponent`가 반경만 가졌다.
  - `Obstacle`: `ObstacleGeometryComponent`가 별도 shape enum을 가졌다.
- 이 상태에서는 공통 geometry utility를 공유하기 어렵고, 사각형의 회전 semantics도 일관되지 않았다.
  - `Obstacle`는 회전된 사각형으로 판정됐다.
  - `Source`는 일부 gizmo/transform은 회전을 보지만 sampling/occupancy는 축 정렬처럼 동작했다.
- `Deposit`도 후속 확장을 고려하면 반경 전용 계약을 유지하는 비용이 커지기 시작했다.

## 결정
- `Source / Deposit / Obstacle`의 raw planar shape는 `Shape2DComponent`로 통일한다.
  - `Shape2DKind`: `Circle`, `Rectangle`
  - `Shape2DComponent`: `Kind`, `Radius`, `Size`
- gameplay 판정은 항상 `XZ` 평면에서만 수행한다.
- 회전 semantics는 `yaw`만 사용한다.
  - `pitch/roll`은 gameplay 의미가 없으므로 authoring에서 0으로 강제한다.
- `Rectangle`은 `yaw-aware planar OBB`로 해석한다.
- semantic marker는 유지한다.
  - `BulletFieldAreaComponent`, `DepositPointComponent`, `ObstacleGeometryComponent`는 raw shape holder가 아니라 semantic marker다.
- `Source`는 공통 shape 위에 전용 파생 번들을 둔다.
  - `SourceShapeDerivedComponent`: `ComputedArea`, `HalfExtents`
  - `SourcePollutionGridComponent`와 관련 buffers는 source owner가 shape 변경과 함께 재생성한다.
- `Deposit` 접촉 판정은 `player circle overlap deposit shape`로 고정한다.
- Stage Layout과 editor marker는 `YawDeg + Shape/Radius/Size` 스키마로 통일한다.

## 대안
- 대안 A: 기존 kind별 raw shape 컴포넌트를 유지하고 helper만 공유
  - 장점: 변경량이 적다.
  - 단점: 중복 contract가 남고 editor/layout/runtime semantics가 계속 분기된다.
  - 기각 사유: 이번 변경의 핵심은 공통 raw shape와 회전 계약을 고정하는 것이다.
- 대안 B: 모든 런타임 데이터를 단일 generic shape component 하나로만 운영
  - 장점: 외형상 가장 단순하다.
  - 단점: `Source`의 `ComputedArea`, pollution grid 같은 파생 책임 owner가 흐려진다.
  - 기각 사유: source 전용 파생 책임은 generic derivation보다 source owner 묶음이 더 안전하다.
- 대안 C: `XZ` 평면 원칙을 이유로 사각형 회전을 전부 무시
  - 장점: 일부 구현이 단순해진다.
  - 단점: obstacle 기존 계약과 editor gizmo 의미를 후퇴시키고, 사각형 배치 자유도를 잃는다.
  - 기각 사유: `XZ 평면`과 `yaw-only`는 양립 가능하며, 그쪽이 더 일관적이다.

## 결과
- 긍정 효과
  - `Source / Deposit / Obstacle`가 같은 planar shape 언어를 사용한다.
  - rectangle의 판정/샘플링/overlap contract가 `yaw-aware`로 일관된다.
  - editor marker와 layout 생성 경로에서 `pitch/roll`을 차단해 데이터 부정합을 줄인다.
- 트레이드오프
  - `Source`의 sampling/occupancy/pollution grid 관련 테스트와 회귀 확인 범위가 넓어진다.
  - 기존 테스트용 layout/샘플 데이터는 새 스키마 기준으로 재작성 비용이 발생할 수 있다.

## 후속
- `TD-015`에 `Shape2DComponent`, `SourceShapeDerivedComponent`, `YawDeg` 스키마를 반영한다.
- EditMode/PlayMode 테스트를 `Shape2DComponent` 기준으로 정리한다.
- obstacle broadphase 최적화는 별도 세션으로 분리한다.
