# ADR-20260324-01-grid-authoritative-stage-layout-and-explicit-region-id
> 스테이지 layout authority를 grid cell로 전환하고, `Source / Deposit`는 paint 시 명시하는 region id 기반 aggregate로 운영하는 결정

## 배경
- 현재 stage layout은 `Source / Deposit / Obstacle` shape entry 배열을 중심으로 동작한다.
- 이 구조는 타일맵 기반 stage design과 정합성이 낮다.
  - authoring은 타일/셀 중심으로 생각하게 되는데, runtime과 asset은 원/사각형 shape로 다시 번역해야 한다.
  - 자유형 지형, 복합 경계, 셀 단위 속성 편집이 불편하다.
- 특히 `Source`는 기존 shape 기반 sampling/occupancy 계약 때문에 복잡한 필드 형태를 표현하기 어렵다.
- `Obstacle`는 gameplay적으로는 이동 차단 셀 집합에 가깝지만, 현재는 standalone shape topology와 presentation 연동까지 함께 들고 있다.

## 결정
- layout SSOT는 `shape entry array`가 아니라 `grid cell authoritative`로 전환한다.
- `StageCatalogSO`의 dual catalog 구조는 유지하고, layout 쪽만 grid schema로 재정의한다.
- 셀은 최소한 아래 속성을 가진다.
  - `MovementFlags`
  - `SourceRegionId`
  - `DepositRegionId`
  - `TerrainTileId` 또는 이에 준하는 visual tile key
- `SourceRegionId`, `DepositRegionId`는 paint 시 명시 입력을 강제한다.
  - connected cell 자동 병합으로 region id를 추론하지 않는다.
  - region id 없이 source/deposit 의미를 가진 셀은 허용하지 않는다.
- `Source`와 `Deposit`는 region id를 기준으로 생성되는 aggregate runtime entity다.
  - `StageDefinitionSO.SourceBindings`는 유지하되, key 의미를 `source region stable id`로 고정한다.
- obstacle gameplay authority는 standalone topology entity가 아니라 `MovementFlags`가 소유한다.
- obstacle visual은 gameplay authority와 분리한다.
  - 필요 시 obstacle layer를 읽어 visual tile 자동 생성 또는 별도 tilemap rebuild를 수행할 수 있다.
  - 이 visual 경로는 gameplay hard gate가 아니다.
- Unity Tilemap, 외부 툴 import, 수동 편집 어느 경로든 최종 입력은 동일한 grid schema의 `StageLayoutSO`다.

## 대안
- 대안 A: 기존 `StageSourceMarker / StageDepositMarker / StageObstacleMarker`를 유지하고 brush 편의만 추가
  - 장점: 기존 generator/runtime과의 연결이 단순하다.
  - 단점: 셀 단위 사고와 asset SSOT가 계속 어긋나고, 자유형 stage 편집성이 근본적으로 개선되지 않는다.
  - 기각 사유: 이번 변경의 핵심은 editor UX 보강이 아니라 layout authority 자체 전환이다.
- 대안 B: connected cell을 자동 병합해 region id를 추론
  - 장점: paint 절차가 단순해 보인다.
  - 단점: region 경계, source binding key, 수정 후 diff 의미가 불안정해진다.
  - 기각 사유: 현재 프로젝트는 ownership/stable id/validation 명확성이 더 중요하다.
- 대안 C: obstacle visual을 gameplay obstacle topology와 계속 결합
  - 장점: obstacle 한 종류의 데이터로 gameplay/visual을 동시에 관리할 수 있다.
  - 단점: 타일맵 visual 변경이 gameplay topology와 강하게 결합되고, visual 실패가 gameplay 리스크로 번진다.
  - 기각 사유: obstacle은 본질적으로 movement authority이고, visual은 read-only consumer로 분리하는 편이 안전하다.

## 결과
- 긍정 효과
  - stage design과 runtime query 모델이 모두 grid cell 언어를 사용하게 된다.
  - 자유형 지형, 복합 장애물, 셀 단위 gameplay 속성을 타일맵처럼 편집할 수 있다.
  - source/deposit는 region stable id를 유지해 기존 definition ownership과 연결할 수 있다.
  - obstacle visual을 gameplay authority에서 분리해 tilemap/auto-generation 실험이 쉬워진다.
- 트레이드오프
  - `Source` sampling, pollution, progress를 region cell 집합 기준으로 옮겨야 하므로 runtime 영향 범위가 크다.
  - 기존 shape 기반 validation/test/authoring 자산은 migration 비용이 발생한다.
  - obstacle linked presentation 같은 기존 암묵 연결은 더 이상 기본 경로가 아니다.

## 후속
- `TD-015`를 grid-authoritative layout 기준으로 갱신한다.
- `StageLayoutSO` grid schema, validation, generator seam을 도입한다.
- movement/deposit/source runtime consumer를 단계적으로 grid authority로 이관한다.
- obstacle visual auto-generation 또는 tilemap rebuild는 별도 presentation owner로 구현한다.
