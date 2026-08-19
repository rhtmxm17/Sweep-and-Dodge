# AGENTS.md
> Portfolio mini-game (Unity + C#, DOTS 중심). 설계 대화 기반으로 반복적으로 구조를 조정하며 구현한다.

## 0. TL;DR (프로젝트 요약)
- 장르/컨셉: 미니게임(탄막 회피 + 탄막 수집/청소 컨셉 포함 가능)
- 목표: 포트폴리오용 "완성도 높은 데모" 제작 후 공개
- 기술 스택: Unity (Entities/DOTS), C#
- 우선순위: 성능(대량 엔티티), 안정성, 유지보수 가능한 설계(소유권/업데이트 순서 명확화)

---

## 1. 목표(Goals)
- 대량 오브젝트(예: 회피/수집 대상 수만~수십만)에서도 **프레임 안정성** 확보
- 설계 변경이 빈번해도 코드베이스가 무너지지 않도록 **명확한 책임/소유권/업데이트 순서** 유지
- 테스트/디버깅 루틴을 통해 "변경 → 검증" 반복 비용을 낮춤

## 2. 비목표(Non-goals)
- 불필요한 전면 리라이트 금지(특히 DOTS 파이프라인 핵심부)
- 기능 추가를 위해 패키지/의존성 무분별 추가 금지(정당화 필요)
- 성능 문제를 "감으로 수정"하는 방식 지양: 근거(프로파일/측정) 기반

---

## 4. 아키텍처 원칙(Architecture Principles)
### 4.1 소유권(Ownership)
- "누가 데이터를 수정할 권한을 가지는가?"를 문서/코드로 강제한다.
- 예시:
  - Pool/FreeList 초기화·접근: **Pool Owner 영역 단일 소유**
    - 초기화: `BulletPoolOwnerBootstrapSystem`
    - Dequeue(스폰): `BulletSpawnFromPoolSystem` (ExecutionBegin)
    - Enqueue(디스폰 반납): `BulletDespawnExecutionSystem` (ExecutionEnd)
    - 주의: FreeList는 ECS 의존성 추적 밖 컨테이너이므로, **FreeListFence(JobHandle)** 로 접근 순서(시퀀싱)를 강제한다.
  - SpatialHash/CellMap: **Simulation 시스템 단일 소유(Writer)**
    - Writer: `BulletSimulationSystem` (Simulation)
    - Request 단계는 **ReadOnly 조회만** 허용하며, CellMap 접근도 **CellMapFence** 기반으로 순서를 강제한다.
  - 렌더 토글(ON/OFF): **Owner 시스템(Spawn/Despawn/Bootstrap) 단일 책임**
    - Simulation/Request 단계는 렌더 상태를 직접 변경하지 않고, 필요 시 **요청 태그(enable)** 만 남긴다.
    - 다중 렌더 파츠는 Bake 시 루트에 저장된 버퍼(아래 4.3) 기준으로 on/off 한다.

### 4.2 업데이트 순서(Update Order)
- Group 단위로 의미를 고정한다(프레임 내 파이프라인):
```
ExecutionBegin → Simulation → Request → ExecutionEnd
```
- 의미:
  - `ExecutionBegin`: 스폰 실행(풀 Dequeue)
  - `Simulation`: 이동/수명 갱신 + CellMap Build(Writer)
  - `Request`: 제거 판단(조회/요청 생성) ※ 외부 요청 시스템 위치
  - `ExecutionEnd`: 디스폰 실행(풀 Enqueue)
- `UpdateAfter/UpdateBefore`로 의존 관계를 문서화한다.
- (실무 규칙) SharedStatic/Native 컨테이너(CellMap, FreeList) 접근 시스템은 항상 해당 fence 의존을 결합한다:
  - `deps = Combine(state.Dependency, Fence)` 형태를 기본값으로 한다.

### 4.3 구조 변경(Structural Change) 최소화
- 대량 엔티티에서 구조 변경은 비용이 크므로 **플래그/Enableable 기반 상태 전환**을 기본으로 한다.
- `IEnableableComponent`는 **요청/소비(consume) 플래그**로 사용 가능:
- enable: 요청 생성
- disable: 실행 소비
- 병렬 Job에서의 플래그 토글:
- 기본: `EnabledRefRW<T>` 사용
- enableable 쿼리 주의:
  - `IJobEntity` 파라미터에 enableable 컴포넌트를 포함하면 기본적으로 "enabled만" 잡힌다.
  - "disabled 상태라도 write가 필요"하면 `EntityQueryOptions.IgnoreComponentEnabledState`(또는 WithOptions)를 **명시**한다.
  - 병렬 Job에서 "현재 엔티티 외 다른 엔티티"에 대해 enable/disable(write)가 필요하면,
    - **ECB.ParallelWriter를 사용**하거나(권장),
    - 또는 Owner 단일 스레드 단계로 이동한다.
- 다중 렌더 파츠 토글
  - 프리팹 계층 하위에 렌더 엔티티가 여러 개 존재할 수 있으므로, "루트에 Renderer/MaterialMeshInfo가 있다"를 가정하지 않는다.
  - Bake 시 루트에 `DynamicBuffer<EntityRenderElementBuffer>`(렌더 파츠 엔티티 목록)를 채워두고,
    - Spawn/Despawn/Bootstrap에서 이 버퍼를 순회하여 `MaterialMeshInfo` enable/disable을 수행한다.
  - 버퍼에는 "외형 렌더 파츠 엔티티"만 들어가는 것을 원칙으로 한다.
    - 원칙을 보장하기 어려운 시점에는 런타임에서 `HasComponent<MaterialMeshInfo>` 가드(또는 단일 단계 처리)를 허용한다.

---

## 5. 코딩 컨벤션(Conventions)
### 5.1 ECS 네이밍
- `IComponentData`: `*Component` / `*Tag`
- 굽기(MonoBehaviour): `*Authoring` + 내부 `Baker`

### 5.2 ECS System 작성
- Entities 권장 패턴 준수
- 병렬 Job에서 Lookup 기반 **쓰기**는 지양
  - 기본: `EnabledRefRW<T>`(자기 엔티티)로 처리
  - 교차 엔티티 write 필요 시: **ECB.ParallelWriter** 또는 Owner 단일 단계로 이동
- ReadOnly/Write 의도를 어트리뷰트로 명확히 표기.
- 항상 실행되어야 하는 Job은 enableable 쿼리 함정을 피하도록 옵션/시그니처를 설계.

### 5.3 주석(summary) 작성
- 공개 API, 복잡한 로직, 소유권/업데이트 순서처럼 규칙이 중요한 곳에는 `summary` 주석 작성을 권장한다.
- 자명한 코드에는 주석을 생략한다.

### 5.4 테스트/콘텐츠 검증 작성
- 테스트 오라클은 `코드 contract`, `명시적 SSOT`, `validation rule`, `runtime behavior`를 기준으로 잡는다.
- TD/ADR/GD의 예시값, 권장값, 설계 중 제안값을 serialized asset 검사 테스트의 기준값으로 승격하지 않는다.
- serialized asset 직접 검사 테스트는 아래 경우에만 허용한다.
  - 명시적 SSOT asset/table/manifest와의 동기 검증
  - validation rule, reference integrity, schema presence, 금지 참조 여부 검증
  - authoring -> bake/runtime 변환의 심볼릭 계약 검증
- 다음 패턴은 금지한다.
  - sample/default/demo asset의 metadata를 raw literal(`float/int/string`)로 비교
  - `DefinitionId`, 개수, 순서, stage id 등 콘텐츠 값을 외부 SSOT 없이 하드코딩
  - 설계 문서의 권장 범위/예시값을 exact assert로 고정
- exact tuning 값을 장기 계약으로 보호해야 하면 먼저 repo 내부 SSOT(constants, manifest asset, data table)를 만든다.
- 명시적 SSOT가 없으면 데이터 snapshot test 대신 validation test 또는 behavior smoke test를 작성한다.

---

## 6. 성능 기준(Performance Budget)
- 목표 프레임: 60fps
- GC/할당: per-frame allocation 금지(대량 엔티티 루프)
- 극단 케이스 고려:
  - 예) 10만 엔티티 동프레임 제거/회수
  - 병렬화 적용 여부는 **측정 기반**으로 결정

---

## 7. Definition of Done (DoD)
- 컴파일 성공, 경고/에러 증가 없음
- MCP 검증 기준: Unity Console `error` 0건
- 최소 스모크 테스트 통과:
  - `EditMode` 테스트 통과
  - `PlayMode` 전용 씬 스모크 통과(작업 완료 기준)
- 성능 리스크 변화시 근거(수치/캡처) 첨부
- 신규/수정 테스트는 설계 제안값 snapshot이 아니라 명시적 SSOT 또는 behavior contract를 검증해야 한다.
- 코드 리뷰 관점:
  - 소유권/업데이트 순서/플래그 전환 규칙이 문서·코드에서 일치
- 예외 처리:
  - Unity/MCP 미연결 등으로 검증 불가하면 사유와 미검증 범위를 완료 보고에 명시

---

## 9. 보안/권한(코딩 에이전트 운영)
- 모든 .md 파일과 Assets/_Project 아래 .cs 파일을 읽을 때는 UTF-8로 강제(Get-Content -Encoding UTF8).
- 보고에서 프로젝트 내부 파일을 언급할 때는 웹 링크를 사용하지 않는다.
- 기본 모드: read-only
- 파일 수정/명령 실행이 필요한 경우에만 권한 상승(최소 권한 원칙)
- 네트워크/외부 업로드는 기본 금지(필요 시 명시적 승인)

---

## 11. Agent 작업 절차
### 11.1 적용 원칙
- "빠른 실행"을 기본값으로 하되, 리스크가 커질 때만 절차를 강화한다.
- 절차 준수보다 결과(안정성/검증 가능성/회귀 방지)를 우선한다.

### 11.3 고영향 변경(승인 권장)
- 아래 중 하나에 해당하면 구현 전 짧은 계획 공유 후 승인받는다.
  - 소유권(Writer) 변경
  - 업데이트 순서(Group/UpdateAfter/Before) 변경
  - Fence/Native 컨테이너 접근 규칙 변경
  - 시스템 간 파급이 큰 구조 변경
- 승인 형식은 간단히 1~3줄로 충분하다.

### 11.4 저영향 변경(승인 후 즉시 진행 가능)
- 저영향 변경이라도 11.6의 명시 승인 게이트를 먼저 통과해야 한다.
- 승인 이후에는 별도 추가 승인 없이 즉시 진행 가능하다.

### 11.6 수정 게이트(Explicit Approval Gate)
- 기본 원칙: Agent는 기본적으로 read-only로 동작하며, 사용자 명시 승인 전에는 문서/코드/에셋을 수정하지 않는다.
- 수정 허용 조건: 사용자가 현재 컨텍스트에서 명시적으로 수정 승인을 요청한 경우에만 수행한다.
  - 예: "플랜 승인", "이 플랜으로 수정 진행", "코드 수정해", "문서 반영해"
- 예외: 현재 대화 세션의 TaskBoard 갱신은 작업 추적을 위한 운영 행위로 간주하며, 해당 세션의 승인된 작업 범위 안에서는 추가 승인 없이 수시 갱신할 수 있다.
- 비허용 신호: 모호한 동의(예: "ㅇㅋ", "좋아"), 단순 질의, 리뷰 요청, 아이디어 논의는 수정 승인으로 간주하지 않는다.
- 승인 범위: 승인 시점에 합의된 단일 작업 범위에만 유효하며, 범위가 바뀌면 재승인을 받아야 한다.
- TaskBoard 예외는 범위 변경까지 자동 승인하는 의미가 아니다. 새로운 작업 범위 추가, 설계 결정 변경, 코드/문서 본문 수정은 기존 승인 게이트를 그대로 따른다.
- 승인 전 허용 작업: 분석, 설계/플랜 작성, 영향도 검토, 패치 초안(diff) 제시, 읽기 전용 명령 실행.
- 승인 후 실행 규칙: 수정 시작 전에 "승인 문구"와 "적용 범위"를 1~3줄로 재확인한 뒤 구현한다.

---

## 작업별 세부 지침

아래 참조 파일은 단순 링크가 아니라 상황별 선행 로딩 의무다. 해당 상황에 해당하는 작업을 시작하기 전에 반드시 참조 파일을 먼저 읽고 적용한다.

| 상황 | 참조 파일 |
| ------ | ----------- |
| 코드 생성/수정 및 MCP 검증 시 | [mcp-workflow.md](Docs/AGENTS/mcp-workflow.md) |
| TD/ADR 문서 작성 시 | [docs-workflow.md](Docs/AGENTS/docs-workflow.md) |
| UI 레이아웃 및 Penpot 작업 시 | [ui-workflow.md](Docs/AGENTS/ui-workflow.md) |
| 플랜 수립 및 Subagent 운영 시 | [agent-ops.md](Docs/AGENTS/agent-ops.md) |
