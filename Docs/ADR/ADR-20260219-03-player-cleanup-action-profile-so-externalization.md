# ADR-20260219-03-player-cleanup-action-profile-so-externalization
> 플레이어 청소 행동 프로파일을 베이커 하드코딩에서 ScriptableObject 기반 외부 데이터로 전환한 결정이다.

## 배경
- 행동 분기 확장 시 프로파일 수치(거리/각도/폭)를 코드에서 직접 수정하면 반복 비용이 커진다.
- 캐릭터/프리셋 단위로 서로 다른 행동 세트를 빠르게 시험해야 한다.
- 현 단계에서는 튜닝/검증 속도가 런타임 미세 최적화보다 우선이다.

## 결정
- `PlayerCleanupActionSetSO`를 도입해 행동 프로파일과 초기 선택/슬롯 매핑을 외부 데이터로 관리한다.
  - 파일: `Assets/_Project/02_Scripts/ECS/Authoring/PlayerCleanupActionSetSO.cs`
- `PlayerProxyAuthoring`는 `CleanupActionSet` 참조를 받아 ECS로 bake한다.
  - `PlayerCleanupActionStateComponent`
  - `PlayerCleanupActionSlotMapComponent`
  - `DynamicBuffer<PlayerCleanupActionProfileBufferElement>`
- 에셋이 없거나 프로파일 배열이 비어 있으면 기존 샘플(원형/전방)로 fallback한다.

## 대안 비교
### 대안 1: 코드 상수 유지
- 장점: 구현 단순
- 단점: 튜닝 반복 비용 증가, 캐릭터/프리셋 분리 어려움

### 대안 2: BlobAsset 즉시 도입
- 장점: 런타임 메모리/접근 효율 우수
- 단점: 제작 파이프라인 복잡도 증가, 초기 기획 반복 속도 저하

### 채택안 선택 이유
- 현재 프로젝트 단계에서 에디터 반복 작업 효율이 가장 중요해 SO가 적합하다.
- 필요 시 동일 스키마를 BlobAsset으로 이관할 수 있다.

## 결과
- 행동 수치 변경은 에셋 수정으로 처리 가능해졌다.
- 캐릭터 단위 행동 세트 운영이 가능한 구조를 확보했다.

## 리스크 및 후속
- 리스크:
  - 에셋 누락/잘못된 값으로 인한 런타임 fallback 의존
- 후속:
  1. 전용 프리셋 에셋(`pas_*`) 생성 및 밸런싱 기준값 고정
  2. 필요 시 authoring validation(프로파일 중복 ActionId 검사) 추가
  3. 성능 이슈 발생 시 BlobAsset 이관 ADR 작성

## 관련 문서
- `Docs/ADR/ADR-20260219-02-cleanup-action-branching-by-profile.md`
