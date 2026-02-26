# 런 진행도 디렉터 설계 초안

## Metadata
- doc_id: `TD-006`
- type: `TechnicalDesign`
- status: `draft`
- last_updated: `2026-02-26`
- related_docs:
  - [OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)
  - [GD-007-data-driven-bullet-pattern-definition.md](../GameDesign/GD-007-data-driven-bullet-pattern-definition.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-005-spawn-directive-settings-reference.md](./TD-005-spawn-directive-settings-reference.md)

> 복수 Source 스테이지에서 `Source 진행도` 중심으로 패턴 선택을 지휘하는 `런 진행도 디렉터`의 책임 경계와 연동 계약을 정의한다.

## 1. 배경
- 기존 v3 경로는 `SourceClipRequestBuildSystem` 내부에서
  - Source 상태 변화 감지
  - sustain/event 클립 선택
  - spawn 요청 누적
  를 함께 처리한다.
- 복수 Source 스테이지에서 페이싱/전환 정책을 확장하려면, "선택(진행도 해석)"과 "실행 요청 생성" 경계를 분리할 필요가 있다.

## 2. 목표
- `런 진행도 디렉터`를 시간축 스케줄러가 아니라 `Source 진행도/상태/이벤트` 기반 오케스트레이터로 정의한다.
- `WaveClipSO`/`BulletSourceAuthoring` 자산 스키마를 유지한 상태로 확장 가능 경로를 만든다.
- 기존 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)과 Owner 경계를 유지한다.

## 3. 비목표
- 탄환 스폰/디스폰 실행 로직 변경.
- 풀/CellMap/렌더 토글 책임 이동.
- `WaveClipSO` 데이터 모델 개편.

## 4. 책임/비책임
### 4.1 책임
- 스테이지 내 Source별 진행 상태를 해석해 현재 `RunProgressState`(또는 `StageFlowState`)를 결정한다.
- 상태 전환(구간 전환, Source 전환, StageClearReady)을 판정한다.
- 현재 상태에 맞는 패턴 선택 요청을 발행한다.
- 스테이지 시간 목표(2~3분)와 캠페인 목표(무실패 15~20분)는 가드레일로만 사용한다.

### 4.2 비책임
- `SourceSpawnRequestBuffer` 소비/스폰 실행.
- 탄환 시뮬레이션/충돌/수명 갱신.
- 풀/CellMap 접근.
- 렌더 on/off 및 구조 변경 중심 처리.

## 5. 네이밍 가드
- `SourceState`: Source 고갈 상태 전용 (`Normal/Weakened/Depleted`).
- 런 진행 상태는 분리 명칭 사용:
  - `RunProgressState` 또는 `StageFlowState`.
- 탄 라인 의미는 기존대로 `Lane` 용어를 유지한다(`Channel` 미사용).
- 요청 계층 분리:
  - 디렉터 선택 요청: `SourcePatternSelectRequest*` (신규)
  - 실행 요청: `SourceSpawnRequestBuffer` (기존)

## 6. 파이프라인 배치 (초안)
- 그룹: `BulletRequestGroup`
- 순서:
  1. 진행도/이벤트 집계 시스템
  2. `RunProgressDirectorSystem` (신규)
  3. `SourceClipRequestBuildSystem` (기존, 소비 전용으로 점진 전환)
  4. `BulletRequestFencePublishSystem`
- 원칙: 디렉터는 요청 발행만 수행하고, 실제 spawn 요청 누적/소비는 기존 Owner 경로를 유지한다.

## 7. 데이터 계약 (초안)
### 7.1 입력(ReadOnly)
- `SourceSpawnComponent` (상태/진행 관련 값)
- `SourceSustainRuntimeComponent`, `SourceEventRuntimeComponent` (현재 재생 상태)
- 전투 이벤트 집계(피격/수거/정산) 컴포넌트 또는 버퍼
- 런/스테이지 정책 데이터(목표 시간, 전환 규칙, 동시 활성 Source 제한)

### 7.2 출력(Write)
- `SourcePatternSelectRequestBuffer` (신규, Source별 선택 결과)
- `StageTransitionRequest` (신규, 스테이지 전환 요청)
- `RunDirectorDebugMetricsComponent` (신규, 전환/선택 관측)

### 7.3 소비 경계
- `SourceClipRequestBuildSystem`는 `SourcePatternSelectRequestBuffer`를 소비해 `SourceSpawnRequestBuffer`를 생성한다.
- `SpawnRequestRoundRobinExecutionSystem`는 기존처럼 `SourceSpawnRequestBuffer`를 소비한다.

## 8. 마이그레이션 단계
1. 문서/명칭 정렬
- 레거시 Build 시스템 명칭을 `SourceClipRequestBuildSystem`으로 통일.
- `런 타임라인 디렉터` -> `런 진행도 디렉터`로 용어 고정.

2. 선택 요청 계층 추가
- `SourcePatternSelectRequestBuffer`를 추가하되, 초기에는 기존 선택 로직 결과를 그대로 미러링한다.

3. 선택 책임 이관
- 클립 선택/전환 정책을 `RunProgressDirectorSystem`으로 이동한다.
- `SourceClipRequestBuildSystem`은 선택 결과 소비 + 요청 누적에 집중한다.

4. 회귀 고정
- EditMode: 선택/전환 규칙 계약 테스트 추가.
- PlayMode: 전용 씬 스모크에서 Source 전환/이벤트 트리거/요청 생성 경로를 고정한다.

## 9. 리스크와 완화
- 리스크: 선택 책임 이관 중 중복 요청 또는 누락 발생.
- 완화: 이관 단계에서 "기존 경로 vs 디렉터 경로" 동시 관측 지표를 비교한다.

- 리스크: 용어 혼용(`State`, `ProgressState`, `Phase`)으로 설계 혼선.
- 완화: 본 문서의 네이밍 가드 테이블을 TD-002/TD-005와 동기화한다.

## 10. 오픈 이슈
- 디렉터 상태 모델 최소 집합(`Intro/Pressure/Peak/Recover` 등) 확정.
- 복수 Source 활성 시 공정성 정책(기아 방지, 동시 활성 상한) 수치화.
- 시간 가드레일 강제 전환 규칙(최소/최대 체류시간) 확정.
