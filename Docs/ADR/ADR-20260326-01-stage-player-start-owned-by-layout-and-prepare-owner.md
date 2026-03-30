# ADR-20260326-01-stage-player-start-owned-by-layout-and-prepare-owner
> 스테이지별 플레이어 시작 위치를 `StageLayoutSO`가 소유하고, stage entry apply는 prepare 계층 단일 writer가 수행하도록 고정한 결정

## 배경
- 현재 플레이어 초기 위치는 `PlayerProxyAuthoring`의 bake 시점 transform에 사실상 고정돼 있다.
- 하지만 stage 진입은 이미 `StageTopologyBridge.RequestTopologyApply(stageId)`를 통해 stage별 layout을 적용하는 구조다.
- 따라서 스테이지마다 다른 시작 위치를 두려면 player spatial state를 scene 고정값이 아니라 stage data 기준으로 다시 적용해야 한다.
- 이 기능은 layout schema, stage-entry reset, movement owner의 이전 위치 처리까지 함께 걸린다.

## 결정
- 플레이어 시작 위치는 `StageDefinitionSO`가 아니라 `StageLayoutSO`가 소유한다.
- authoring은 `StageLayoutStageMarker` 하위의 `StagePlayerStartMarker` 1개를 기준으로 한다.
- 저장 형식은 grid-relative `AnchorCell + AnchorOffset + YawDeg`를 사용한다.
- stage apply owner는 world-space player start runtime singleton을 publish한다.
- player entity에 대한 실제 위치/회전 write는 `PlayerStageEntryApplyPrepareSystem` 단일 writer가 수행한다.
- 적용 대상은 최소 아래 3개를 함께 맞춘다.
  - `LocalTransform`
  - `PlayerGoSyncComponent`
  - `PlayerPreviousPositionComponent`
- reset 단계는 player spatial state를 직접 쓰지 않는다.
  - spatial state는 selected stage layout이 resolve된 뒤에만 결정되므로 apply 단계에서만 쓴다.

## 대안
- 대안 A: `StageDefinitionSO`에 시작 위치를 둔다
  - 장점: stage meta와 한곳에 모일 수 있다.
  - 단점: 시작 위치는 spatial data인데 definition은 비공간 메타/소스 바인딩 소유자라 경계가 흐려진다.
  - 기각 사유: 현재 프로젝트는 layout/definition dual catalog ownership을 유지하는 편이 일관된다.
- 대안 B: scene의 `PlayerProxyAuthoring` transform을 stage별로 직접 옮긴다
  - 장점: 구현이 단순해 보인다.
  - 단점: runtime stage 선택, retry, next stage, sample asset sync 모두 scene override에 의존하게 된다.
  - 기각 사유: stage-driven 진입 구조와 맞지 않고 prefab/scene drift를 만든다.
- 대안 C: `StageTopologyApplyPrepareSystem`이 player entity까지 직접 쓴다
  - 장점: 시스템 수가 적다.
  - 단점: topology/layout/source apply owner에 player spatial write 책임까지 섞여 응집도가 떨어진다.
  - 기각 사유: player spatial write는 별도 prepare owner로 분리하는 편이 ownership이 선명하다.

## 결과
- 긍정 효과
  - 스테이지별 시작 위치가 layout SSOT에 들어가 content pipeline과 함께 관리된다.
  - retry/next stage에서도 같은 stage-entry contract로 재적용된다.
  - movement owner가 보는 previous position과 present sync를 같이 맞출 수 있어 첫 프레임 오동작을 줄인다.
- 트레이드오프
  - `StageLayoutSO`, generator, validation, prepare group 순서, 테스트까지 연쇄 수정이 필요하다.
  - 시작 위치가 grid-relative이므로 vertical spawn이 필요해지면 schema를 다시 열어야 한다.

## 후속
- `TD-025`에 세부 계약을 기록한다.
- `TD-010`, `TD-015`를 새 ownership/prepare order 기준으로 갱신한다.
- runtime 구현 시 compile / console / EditMode / PlayMode smoke까지 stage start 회귀를 추가한다.
