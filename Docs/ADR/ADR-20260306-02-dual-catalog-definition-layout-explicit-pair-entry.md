# ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry
> 스테이지 정의와 레이아웃을 분리하고 StageCatalog에서 명시적 페어 엔트리로 조립하는 결정

## 배경
- 기존 Stage 데이터는 `StageMapCatalogSO` 단일 카탈로그 구조였다.
- 이 구조는 Source 패턴(`WaveClipSO`) 같은 게임플레이 정의와 씬 배치 레이아웃이 강결합되어, 로비/진행 데이터 주도 전환 및 점진적 확장(Deposit/Obstacle/Visual)을 어렵게 만들었다.
- 런타임 적용 Owner(`StageMapApplyExecutionBeginSystem`)와 입력 경로(`RunDirectorStageBridge`)는 이미 안정화되어 있어, v1에서 런타임 소비 경로를 크게 흔들지 않고 데이터 구조를 분리할 필요가 있었다.

## 결정
- `StageCatalogSO`를 도입해 카탈로그 최상위 엔트리를 `StageCatalogEntry` 명시적 페어 구조로 고정한다.
  - `StageCatalogEntry = { Enabled, EntryKey, Definition, Layout }`
- `StageDefinitionSO`와 `StageLayoutSO`를 분리한다.
  - `StageDefinitionSO`: Stage 메타 + Source 패턴/상태(clip slot 포함)
  - `StageLayoutSO`: 단일 스테이지 배치(Source/Deposit/Obstacle/Visual)
- v1 정책은 다음으로 고정한다.
  - Source 패턴(`WaveClipSO`)은 `StageDefinitionSO`에 저장/검증
  - ECS 패턴 재구성 적용은 다음 페이즈 이월
  - 불일치 정책은 `Warn + partial apply`
- DemoShell 스테이지 목록/순서는 `StageCatalogSO.Entries`를 우선 사용한다.
  - `Enabled=true`만 대상
  - 카탈로그 미할당/유효 엔트리 없음 시 기존 `StageProfiles` fallback

## 대안
- 대안 A: `StageMapCatalogSO` 단일 구조 유지
  - 장점: 런타임 경로 변경 최소화
  - 단점: 정의/레이아웃 분리 실패, 데이터 주도 확장성 낮음
  - 기각 사유: 중장기 유지보수성과 단계적 확장 요구를 충족하지 못함
- 대안 B: StageId 기반 느슨한 조인(카탈로그에서 Definition/Layout를 별도 배열 관리)
  - 장점: 참조 필드 수 감소
  - 단점: 중복 StageId/누락 참조 오류가 런타임까지 늦게 발견됨
  - 기각 사유: 조합 명시성이 약해 운영 리스크 증가
- 대안 C: v1부터 StageDefinition 패턴까지 ECS 즉시 소비
  - 장점: 데이터 주도 완성 속도 증가
  - 단점: 런타임 파급 범위가 커져 회귀 리스크 증가
  - 기각 사유: 안정화된 StageMap 적용 경로 회귀 위험이 높아 단계 분리가 필요함

## 결과
- 긍정 효과
  - 로비/진행/정의/레이아웃의 책임 경계가 명확해졌다.
  - Definition/Layout 조립을 명시적 엔트리로 관리해 검증 포인트가 선명해졌다.
  - v1에서 기존 StageMap 런타임 경로를 유지하면서 데이터 모델만 확장할 수 있게 되었다.
- 트레이드오프
  - v1 동안은 정의 패턴이 런타임에 직접 반영되지 않으므로, 완전한 데이터 주도 스테이지 구성은 v2 작업이 필요하다.
  - `StageMapCatalogSO`와 Dual Catalog가 병행되어 일시적 중복 관리 비용이 존재한다.

## 후속
- v2에서 `StageDefinitionSO.SourceBindings`를 ECS `SourceClipPatternBuffer`로 재구성 적용한다.
- stage-level override(`RunDirectorStageConfig/RunProgressDirectorConfig/SpawnRequestPolicy`) 적용 범위를 확정한다.
- `StageMapCatalogSO` legacy 경로 제거 시점과 migration 절차를 확정한다.
