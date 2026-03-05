# 스테이지 맵 레이아웃 Authoring + Catalog 파이프라인 (TD-015)

## Metadata
- doc_id: `TD-015`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-05`
- related_docs:
  - [TD-010-demo-shell-flow-and-bridge-contract.md](./TD-010-demo-shell-flow-and-bridge-contract.md)
  - [TD-006-run-progress-director-design.md](./TD-006-run-progress-director-design.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)

> 디자이너가 씬에서 스테이지별 맵 요소를 직접 배치하고, 생성된 `StageMapCatalogSO`만 런타임 입력으로 사용하는 편집/검증 파이프라인을 정의한다.

## 1. 목표 / 비목표
### 1.1 목표
- Source/Deposit/Obstacle/Visual 배치를 씬에서 직접 편집 가능한 워크플로우를 제공한다.
- 배치 결과를 `StageMapCatalogSO`로 생성하고 자동 검증한다.
- 런타임 입력 경로를 `SO -> (후속) StageMapApplySystem` 단일 경로로 고정한다.
- 이번 페이즈에서 기반(데이터/에디터/검증/테스트)만 완료하고 런타임 적용은 분리한다.

### 1.2 비목표
- 런타임 StageMap 적용 시스템 구현
- 장애물 충돌/차단 규칙 구현
- DemoShell -> ECS StageMap 적용 경로 연결

## 2. 소유권 (Owner / Writer)
- 에디터 배치 Owner
  - `StageLayoutRootMarker`, `StageLayoutStageMarker`, `Stage*Marker`
- 카탈로그 생성 Owner
  - `StageLayoutCatalogGenerator`
- 스테이지 맵 검증 Owner
  - `StageLayoutValidationRules`
- 런타임 소비 Owner
  - 이번 페이즈 미구현 (다음 페이즈에서 단일 시스템으로 고정)

## 3. 업데이트 순서
- 런타임 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)은 이번 페이즈에서 변경 없음.
- StageMap 적용 순서는 다음 페이즈에서 `ExecutionBegin` 내 적용 원칙으로 설계한다.

## 4. 데이터 구조 / 제약
- `StageMapCatalogSO`
  - `StageMapDefinition[] Stages`
- `StageMapDefinition`
  - `StageId`
  - `Sources[]`, `Deposits[]`, `Obstacles[]`, `Visuals[]`
- 식별 키
  - 전 요소 수동 `StableId` 사용
- 제약
  - `StageId >= 1`
  - `StableId >= 1`
  - 반경/크기 음수 금지
  - Visual `VisualKey` 빈 문자열은 warning

## 5. 에디터 워크플로우
1. 씬 내 `StageLayoutRootMarker` 배치
2. 하위에 `StageLayoutStageMarker(StageId)` 그룹 배치
3. 각 그룹에 Source/Deposit/Obstacle/Visual marker 배치
4. `Generate Target Catalog` 또는 `Tools/Project/Stage Layout/Generate Catalogs From Open Scenes` 실행
5. 생성 결과를 `Tools/Project/Validate Content`로 검증

## 6. 검증 규칙 (STG 코드)
- Error
  - `STG001`: StageId invalid
  - `STG002`: duplicate StageId in same catalog
  - `STG003`: duplicate StableId in same stage+category
  - `STG004`: StableId invalid
  - `STG005`: negative radius/size
- Warning
  - `STG006`: stage has no source or no deposit
  - `STG007`: visual key empty
  - `STG008`: all layout elements inactive

## 7. 작업 분해 / 진행 상태
1. StageMap SO 타입 추가 (`완료`)
2. Stage layout marker authoring 추가 (`완료`)
3. Catalog 생성기 + 루트 인스펙터 버튼 (`완료`)
4. Stage validation rules 추가 (`완료`)
5. Content validation runner 연동 (`완료`)
6. Editor 테스트 추가 (`완료`)
7. 런타임 적용 시스템 설계/구현 (`다음 페이즈`)

## 8. 테스트 / 합격 기준
- 컴파일 에러 0
- EditMode 테스트 통과
- PlayMode 스모크 통과
- 신규 Editor 테스트
  - Stage validation rules
  - Stage catalog generator

## 9. 관련 ADR
- 이번 페이즈는 런타임 소유권/업데이트 순서/Fence 규칙 변경이 없으므로 ADR 신규 작성 없음.

