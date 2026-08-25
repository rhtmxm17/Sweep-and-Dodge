# HazardStack HUD 계약

## Metadata
- doc_id: `TD-021`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-08-25`
- related_docs:
  - [GD-009-in-game-ui-screen-blueprint.md](../GameDesign/GD-009-in-game-ui-screen-blueprint.md)
  - [GD-010-in-game-ui-layout-and-zones.md](../GameDesign/GD-010-in-game-ui-layout-and-zones.md)
  - [TD-011-runtime-player-hud-contract.md](./TD-011-runtime-player-hud-contract.md)
  - [TD-016-runtime-ui-shell-and-navigation-contract.md](./TD-016-runtime-ui-shell-and-navigation-contract.md)
  - [TD-018-hazardstack-runtime-contract.md](./TD-018-hazardstack-runtime-contract.md)
- related_adr:
  - [ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md](../ADR/ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order.md)

> `HazardStack`은 좌측 Carry 세로 토템에 결합된 보조층으로 HUD에 표시하고, `RiskMultiplier`는 보조 텍스트로만 보여준다. `HazardStackMax`는 HUD에 직접 표시하지 않는다.

## 1. 목표 / 비목표
### 1.1 목표
- `HazardStack`을 좌측 `Carry` 세로 토템의 보조층으로 HUD에 추가한다.
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
  - `LeftCarryRoot` 내부의 세로 토템 오른쪽 보조 레인
  - Carry fill과 같은 카드 안에서 읽히되, Carry 본체보다 존재감은 낮게 둔다.
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
public int HazardStackMax;
public float HazardRiskMultiplier;
```

설명:
- `HazardStack`
  - 현재 frame-end 기준 최종 stack
- `HazardStackMax`
  - HUD 레이아웃 입력용 최대 stack
  - slot 수와 frame 높이 계산에만 사용하고, 숫자 텍스트로는 노출하지 않는다.
- `HazardRiskMultiplier`
  - HUD 표시용 최종 multiplier
  - 계산식:

```text
HazardRiskMultiplier = 1 + (HazardStack × HazardBonusRate)
```

## 5. 표현 계약
### 5.1 레이아웃
`LeftCarryRoot`
- `CarryTotemRoot`
  - `CarryBar`
  - `HazardStackRoot`
    - `Segment Frame`
    - `HazardStackSegmentsRoot`
      - `SegmentSlotTemplate`
        - `Display`
    - `RiskMultiplierText`

원칙:
- `RuntimeUiRoot.prefab`을 layout SSOT로 둔다.
- `RuntimeUiRoot.Hud` builder는 테스트용 mirror로만 유지한다.

### 5.2 세그먼트 규칙
- 세그먼트 수는 `HazardStackMax`와 동일하다.
- 활성 세그먼트 수는 `HazardStack` 값에 맞춘다.
- `HazardStack`이 `HazardStackMax`를 초과하면 마지막 세그먼트까지 모두 활성으로 clamp한다.
- `HazardStack == 0`이면 모든 세그먼트 비활성
- 세그먼트는 `slot + display` 이중 구조를 사용한다.
  - `slot`: anchored position / sibling order만 담당
  - `display`: sprite pivot 적용과 실제 렌더만 담당
- `slot`은 `anchoredPosition.y = index * SegmentStepY`로 배치한다.
- `display`는 sprite pivot 적용 후 `localPosition = (0, 0)`을 유지한다.
- `display` 크기는 `SetNativeSize()` 이후 `SegmentScale`을 곱해 맞춘다.
- draw order:
  - 활성 세그먼트는 비활성 세그먼트 위에 그린다.
  - 활성 세그먼트끼리는 위쪽 세그먼트가 아래쪽 세그먼트 위에 온다.
  - 비활성 세그먼트끼리는 아래쪽 세그먼트가 위쪽 세그먼트 위에 온다.
- slot sibling order는 아래 순서를 강제한다.
  1. inactive: 높은 index -> 낮은 index
  2. active: 낮은 index -> 높은 index
- frame 높이는 아래 식으로 계산한다.

```text
FrameHeight = FrameBaseHeight + (HazardStackMax × FrameHeightPerSegment)
```

### 5.3 텍스트 규칙
- `RiskMultiplierText`
  - `x{HazardRiskMultiplier:0.00}`
- `HazardStackMax`나 `current / max` 형식 수치는 노출하지 않는다.

### 5.4 색 정책
- `LegacyIllustrated`:
  - 비활성 세그먼트는 `brush gray` sprite를 사용한다.
  - 활성 세그먼트는 `brush gold` sprite를 사용한다.
  - 기존 sprite pivot과 `native size * SegmentScale` 표현을 보존한다.
- `TechDemoFlat`:
  - sprite가 없는 평면 `Image` 세그먼트를 사용한다.
  - 비활성 세그먼트는 메뉴 계열 청회색, 활성 세그먼트는 기존 warning gold를 사용한다.
  - slot 배치, frame 높이, sibling order는 Legacy와 동일하다.
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
- `TextMeshProUGUI RiskMultiplierText`
- `RectTransform HazardStackSegmentsRoot`
- `Image HazardStackFrameImage`
- `RectTransform HazardStackSegmentSlotTemplate`
- `Sprite HazardStackActiveSprite`
- `Sprite HazardStackInactiveSprite`
- `Image[] HazardStackSegmentImages`
- `float SegmentScale`
- `float SegmentStepY`
- `float FrameBaseHeight`
- `float FrameHeightPerSegment`

추가 메서드:
- `ApplyHazardStack(in PlayerHudSnapshotComponent snapshot)`

역할:
- `Carry` 세로 토템 안에서 `HazardStack` 보조층을 갱신
- `HazardStackMax` 기준 slot 수와 frame 높이를 보정
- 세그먼트 sprite(active/inactive), sibling order, multiplier 텍스트를 갱신

비역할:
- `RiskMultiplier` 재계산
- `HazardStack` gain/reset 로직 처리
- 알림/힌트 발행

## 8. 작업 분해 / 진행 상태
1. TD 초안 작성 (`done`)
2. `PlayerHudSnapshotComponent`에 `HazardStack/HazardStackMax/HazardRiskMultiplier` 확장 (`done`)
3. `PlayerHudSnapshotCollectSystem`에 risk snapshot 수집 추가 (`done`)
4. HUD snapshot 수집 순서 보강 (`done`)
5. `RuntimeUiRoot.prefab` hazard lane을 `slot + display` 구조로 정리 (`done`)
6. `StageHudPresenter.ApplyHazardStack()`를 `HazardStackMax` 기반 구성으로 구현 (`done`)
7. EditMode / PlayMode 검증 추가 (`done`)
8. `TechDemoFlat` / `LegacyIllustrated` 세그먼트 전환과 복구 검증 추가 (`done`)

## 9. 검증 계획 / 합격 기준
- compile
- console error 0
- EditMode
  - `TechDemoFlat`에서 `HazardStackMax=5`, `HazardStack=0`이면 slot 5개와 inactive 색상 세그먼트 5개 생성
  - `TechDemoFlat`에서 `HazardStack=3`이면 active 3개 / inactive 2개 색상 적용
  - `LegacyIllustrated`에서 기존 active/inactive sprite, pivot, 크기가 복구됨
  - 반복 스타일 전환 후에도 세그먼트 geometry와 sibling order가 누적 변경되지 않음
  - `HazardStack=9`, `HazardStackMax=5`이면 active 5개로 clamp
  - frame 높이가 `FrameBaseHeight + max * FrameHeightPerSegment`로 계산
  - `Display.localPosition == (0,0)` 유지
  - sprite pivot과 `native size * scale`이 반영
  - sibling order가 active/inactive 규칙대로 적용
  - `RiskMultiplierText == x1.15` 형식 검증
  - `HazardStackMax` 수치가 HUD 어디에도 노출되지 않음
  - `PlayerHudSnapshotCollectSystem`가 risk state 최종값을 snapshot에 기록
  - `Hit` / `Deposit` reset 후 snapshot이 같은 프레임 최종값을 반영
- PlayMode
  - hazard capture 후 다음 프레임 HUD stack 증가 반영
  - hit 후 HUD stack 즉시 0 반영
  - deposit 후 HUD stack 즉시 0 반영
  - retry / stage start 후 HUD stack 0 시작

### 9.1 최신 검증 결과
- 2026-08-25
  - EditMode `527/527` 통과
  - 전용/운영 PlayMode 스모크 `3/3` 통과
  - Flat에서 color segment와 null sprite, Legacy에서 brush sprite/pivot 복구 확인
  - 반복 왕복 전환 후 fill amount, segment 수·순서·크기 유지 확인

## 10. 오픈 이슈
- stack 증가/리셋 순간의 반응 FX를 V1.5에 넣을지, HUD V2로 미룰지
- stage 시작 시 brush 외의 sprite 세트를 선택 가능하게 확장할지
- `RiskMultiplier` 소수점 자릿수를 `0.00`로 고정 유지할지, stage별로 줄일지

## 11. 변경 이력
- 2026-08-25: Flat/Legacy 왕복 구현과 최종 EditMode/PlayMode 검증 결과를 반영했다.
- 2026-08-25: `TechDemoFlat` 색상 세그먼트와 `LegacyIllustrated` brush sprite 복구 계약을 추가하고, 공통 slot/frame/order 규칙은 유지했다.
- 2026-03-20: 구현 반영. `HazardStackMax`를 HUD snapshot에 포함시키고, `RuntimeUiRoot.prefab` hazard lane을 `slot + display` 구조와 `brush gold/gray` sprite 기준으로 고정했다. `StageHudPresenter`의 frame height, sibling order, pivot/size 검증과 prefab SSOT 원칙을 문서에 반영했다.
- 2026-03-19: Penpot viewport export 재검토를 반영해 세그먼트 overlap 및 draw-order 규칙을 명시했다.
- 2026-03-18: 승인된 HUD 레이아웃에 맞춰 `HazardStack` 위치를 `Carry` 세로 토템 내부 보조 레인으로 정리하고, 라벨 없는 세그먼트 + multiplier 구성을 기준안으로 갱신했다.
- 2026-03-16: 초안 작성. `Carry` 인접 보조층, `HazardStackMax` 비표시, snapshot reader-only, risk resolve 이후 HUD snapshot 수집 순서를 기준안으로 고정했다.
