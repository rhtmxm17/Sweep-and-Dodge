# HazardStack HUD 계약

## Metadata
- doc_id: `TD-021`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-03-16`
- related_docs:
  - [GD-009-in-game-ui-screen-blueprint.md](../GameDesign/GD-009-in-game-ui-screen-blueprint.md)
  - [GD-010-in-game-ui-layout-and-zones.md](../GameDesign/GD-010-in-game-ui-layout-and-zones.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](./TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [TD-018-hazardstack-runtime-contract.md](./TD-018-hazardstack-runtime-contract.md)
- related_adr:
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

> `HazardStack`은 Carry 본체가 아니라 Carry 인접의 반상시 보조층으로 HUD에 표시하고, `RiskMultiplier`는 보조 텍스트로만 보여준다. `HazardStackMax`는 HUD에 직접 표시하지 않는다.

## 1. 목표 / 비목표
### 1.1 목표
- `HazardStack`을 `Carry` 인접 보조층으로 HUD에 추가한다.
- 메인 표현은 세그먼트, 보조 표현은 `RiskMultiplier` 텍스트로 고정한다.
- HUD는 기존 `PlayerHazardRisk*` owner를 유지한 채 snapshot reader-only로 동작한다.
- `Hit` / `Deposit` reset 결과가 HUD에 같은 프레임에 반영되도록 snapshot 수집 순서를 고정한다.

### 1.2 비목표
- `HazardStack` 규칙 자체 재정의
- `RiskMultiplier` 수식 변경
- `HazardStack` gain/reset 애니메이션
- `HazardStackMax` 수치의 HUD 직접 표시
- `HazardStack` 전용 Notification/Hint 추가

## 2. UX 기준
- 위치:
  - `LeftCarryRoot` 내부 하단
  - Carry와 같은 시선 축에서 읽히되, Carry 본체보다 존재감은 낮게 둔다.
- 표현:
  - 메인: 세그먼트
  - 보조: `RiskMultiplier` 텍스트 (`x1.15`)
- 표시 원칙:
  - 평상시에는 낮은 존재감
  - stack 증가/리셋 시 값 변화는 읽히되, Carry 본체를 가리지 않는다
  - `HazardStackMax`는 숫자나 분모 형태로 노출하지 않는다

## 3. 소유권
### 3.1 Owner 유지
- `PlayerHazardRiskResolveSystem`
  - 최종 `HazardStack` 단일 writer
- `PlayerHazardRiskStateComponent`
  - 현재 stack owner
- `PlayerHazardRiskConfigComponent`
  - `HazardStackMax`, `HazardBonusRate` owner

### 3.2 HUD reader
- `PlayerHudSnapshotCollectSystem`
  - HUD 표시용 read-only snapshot writer
- `PlayerRuntimeHudBridge`
  - latest snapshot cache
- `StageHudPresenter`
  - snapshot read-only 소비

원칙:
- UI는 `HazardStack`을 직접 계산/수정하지 않는다.
- `RiskMultiplier`도 presenter가 재계산하지 않고 snapshot의 최종값만 사용한다.

## 4. HUD 데이터 계약
`PlayerHudSnapshotComponent`에 아래 필드를 추가한다.

```csharp
public int HazardStack;
public float HazardRiskMultiplier;
```

설명:
- `HazardStack`
  - 현재 frame-end 기준 최종 stack
- `HazardRiskMultiplier`
  - HUD 표시용 최종 multiplier
  - 계산식:

```text
HazardRiskMultiplier = 1 + (HazardStack × HazardBonusRate)
```

제외:
- `HazardStackMax`는 snapshot에 넣지 않는다.
  - HUD 직접 표시 대상이 아니고, 세그먼트 개수는 현재 V1.5 범위에서 `config`를 직접 읽지 않고 고정 visual slot 수로 처리한다.

## 5. 표현 계약
### 5.1 레이아웃
`LeftCarryRoot`
- `CarryLabel`
- `CarryBar`
- `CarryValueText`
- `HazardStackRoot`
  - `HazardStackLabel`
  - `HazardStackSegmentsRoot`
  - `RiskMultiplierText`

### 5.2 세그먼트 규칙
- 세그먼트 수는 V1.5에서는 고정 visual slot으로 둔다.
- 활성 세그먼트 수는 `HazardStack` 값에 맞춘다.
- `HazardStack`이 세그먼트 수를 초과하면 마지막 세그먼트까지 모두 활성으로 clamp한다.
- `HazardStack == 0`이면 모든 세그먼트 비활성

### 5.3 텍스트 규칙
- `HazardStackLabel`
  - `Hazard`
- `RiskMultiplierText`
  - `x{HazardRiskMultiplier:0.00}`
- `HazardStackMax`나 `current / max` 형식 수치는 노출하지 않는다.

### 5.4 색 정책
- 비활성 세그먼트:
  - 저채도 중립색
- 활성 세그먼트:
  - Carry bar보다 약간 따뜻한 강조색
- `HazardStack == 0`
  - 전체 존재감 낮음
- `HazardStack > 0`
  - 활성 세그먼트와 multiplier만 강조

## 6. 업데이트 순서
핵심 규칙:
- `PlayerHazardRiskResolveSystem` 이후에 `PlayerHudSnapshotCollectSystem`이 실행되어야 한다.

의도:
- 같은 프레임 `HazardCaptured` 증가, `Hit` reset, `Deposit` reset의 최종 결과를 HUD가 바로 본다.
- HUD가 한 프레임 늦게 남아 있는 stack을 보여주지 않게 한다.

권장 순서:
1. `PlayerHazardRiskResolveSystem`
2. `PlayerHudSnapshotCollectSystem`
3. `PlayerRuntimeHudBridge`
4. `StageHudPresenter`

## 7. Presenter 책임
`StageHudPresenter`가 계속 소유한다.

추가 필드:
- `GameObject HazardStackRoot`
- `TextMeshProUGUI HazardStackLabel`
- `TextMeshProUGUI RiskMultiplierText`
- `Image[] HazardStackSegmentImages`

추가 메서드:
- `ApplyHazardStack(in PlayerHudSnapshotComponent snapshot)`

역할:
- `Carry`와 같은 좌측 레인 안에서 `HazardStack` 보조층을 갱신
- 세그먼트 fill / 색 / multiplier 텍스트만 갱신

비역할:
- `RiskMultiplier` 재계산
- `HazardStack` gain/reset 로직 처리
- 알림/힌트 발행

## 8. 작업 분해 / 진행 상태
1. TD 초안 작성 (`done`)
2. `PlayerHudSnapshotComponent` 확장 (`pending`)
3. `PlayerHudSnapshotCollectSystem`에 risk snapshot 수집 추가 (`pending`)
4. HUD snapshot 수집 순서 보강 (`pending`)
5. `RuntimeUiRoot.Hud`에 `HazardStackRoot` 빌드 추가 (`pending`)
6. `StageHudPresenter.ApplyHazardStack()` 구현 (`pending`)
7. EditMode / PlayMode 검증 추가 (`pending`)

## 9. 검증 계획 / 합격 기준
- compile
- console error 0
- EditMode
  - `HazardStack == 0`이면 모든 세그먼트 비활성
  - `HazardStack > 0`이면 해당 수만큼 세그먼트 활성
  - `RiskMultiplierText == x1.15` 형식 검증
  - `HazardStackMax` 수치가 HUD 어디에도 노출되지 않음
  - `PlayerHudSnapshotCollectSystem`가 risk state 최종값을 snapshot에 기록
  - `Hit` / `Deposit` reset 후 snapshot이 같은 프레임 최종값을 반영
- PlayMode
  - hazard capture 후 다음 프레임 HUD stack 증가 반영
  - hit 후 HUD stack 즉시 0 반영
  - deposit 후 HUD stack 즉시 0 반영
  - retry / stage start 후 HUD stack 0 시작

## 10. 오픈 이슈
- 세그먼트 고정 개수를 몇 칸으로 둘지 (`5` vs `10`)
- stack 증가/리셋 순간의 반응 FX를 V1.5에 넣을지, HUD V2로 미룰지
- `RiskMultiplier` 소수점 자릿수를 `0.00`로 고정할지, stage별로 줄일지

## 11. 변경 이력
- 2026-03-16: 초안 작성. `Carry` 인접 보조층, `HazardStackMax` 비표시, snapshot reader-only, risk resolve 이후 HUD snapshot 수집 순서를 기준안으로 고정했다.
