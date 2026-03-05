# AGENTS.md
> Portfolio mini-game (Unity + C#, DOTS 중심). 설계 대화 기반으로 반복적으로 구조를 조정하며 구현한다.

## 0. TL;DR (프로젝트 요약)
- 장르/컨셉: 미니게임(탄막 회피 + 탄막 수집/청소 컨셉 포함 가능)
- 목표: 포트폴리오용 "완성도 높은 데모" 제작 후 공개
- 기술 스택: Unity (Entities/DOTS), C#
- 우선순위: 성능(대량 엔티티), 안정성, 유지보수 가능한 설계(소유권/업데이트 순서 명확화)

---

## 1. 목표(Goals)
- 대량 오브젝트(예: 탄환 수만~수십만)에서도 **프레임 안정성** 확보
- 설계 변경이 빈번해도 코드베이스가 무너지지 않도록 **명확한 책임/소유권/업데이트 순서** 유지
- 테스트/디버깅 루틴을 통해 "변경 → 검증" 반복 비용을 낮춤

## 2. 비목표(Non-goals)
- 불필요한 전면 리라이트 금지(특히 DOTS 파이프라인 핵심부)
- 기능 추가를 위해 패키지/의존성 무분별 추가 금지(정당화 필요)
- 성능 문제를 "감으로 수정"하는 방식 지양: 근거(프로파일/측정) 기반

---

## 3. 실행/빌드/테스트(Commands)
- Unity 버전: `Unity 6000.3.6f1`
- 빌드/실행:
  - Unity Editor에서 Play Mode / Standalone Build
- 테스트:
  - `PlayMode` 테스트는 작업 완료마다 `전용 PlayMode 테스트 씬` 스모크를 강제 실행한다.
  - PlayMode 1차 판정은 `기동/루프 정상성`으로 하고, 성능 임계치 초과는 추적 항목으로 기록한다.
- 프로파일링:
  - Profiler(Entities Profiler 포함), Frame Debugger(필요 시)
- 코드 생성/수정 후 기본 검증 절차(MCP 연결 시):
  1. `refresh_unity(compile=request, wait_for_ready=true)`로 컴파일 요청
  2. `read_console(action=get, types=["error"], include_stacktrace=true)`로 에러 확인
  3. `EditMode` 테스트 실행
  4. `PlayMode` 전용 씬 스모크 실행
  5. 에러/실패가 있으면 수정 후 1~4 반복
  6. 에러 0건 + 테스트 통과 시 작업 완료 보고

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
  - `IJobEntity` 파라미터에 enableable 컴포넌트를 포함하면 기본적으로 “enabled만” 잡힌다.
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

---

## 6. 성능 기준(Performance Budget)
- 목표 프레임: 60fps
- GC/할당: per-frame allocation 금지(대량 엔티티 루프)
- 극단 케이스 고려:
  - 예) 10만 탄 동프레임 제거
  - 병렬화 적용 여부는 **측정 기반**으로 결정

---

## 7. Definition of Done (DoD)
- 컴파일 성공, 경고/에러 증가 없음
- MCP 검증 기준: Unity Console `error` 0건
- 최소 스모크 테스트 통과:
  - `EditMode` 테스트 통과
  - `PlayMode` 전용 씬 스모크 통과(작업 완료 기준)
- 성능 리스크 변화시 근거(수치/캡처) 첨부
- 코드 리뷰 관점:
  - 소유권/업데이트 순서/플래그 전환 규칙이 문서·코드에서 일치
- 예외 처리:
  - Unity/MCP 미연결 등으로 검증 불가하면 사유와 미검증 범위를 완료 보고에 명시

---

## 8. 문서 관련 규칙
### 8.1 문서/결정 기록(ADR)
- 아래 조건 중 하나 이상에 해당하는 **중요 결정**만 `Docs/ADR/ADR-YYYYMMDD-NN-*.md`에 기록한다(해당하지 않는 설계 결정/변경에 대해서는 의무가 아니다):
  - 되돌리기 비용이 큰 구조/소유권/업데이트 순서 결정
  - 여러 시스템/문서에 파급되는 규칙 변경
  - 대안 비교와 근거를 남겨야 재논의 비용이 줄어드는 선택
  - 성능/안정성/운영 리스크에 직접 영향을 주는 결정
- 문서 포맷 강제는 최소화한다.
  - **필수**: 파일명 규칙, Metadata
  - **권장(세션 제안)**: 본문 섹션 구성/순서, 예시 템플릿
- ADR 본문 기본 구성(권장):
  - 문제(왜), 결정(무엇), 대안(비교), 결과(리스크/후속)
  - 권장 구성을 맞추기 위해, 논의된 적이 없고 기록 가치가 낮은 내용을 임의로 추가하는 것을 금지한다.
- AGENTS.md는 "현재 합의된 기준"만 유지, 상세 논의는 ADR로 분리
- ADR 파일 규칙
  - 파일명(필수): `ADR-YYYYMMDD-NN-kebab-case.md`
  - NN 논리 의존순, 제목/요약 라인, 본문 구성은 탐색성과 일관성을 위한 권장 규칙이다.
  - 예시:

```markdown
# ADR-20260212-01-ecb-vs-flag-state
> 대량 엔티티 상태 전환에서 ECB 대신 Flag 기반 접근을 채택한 결정

## 배경
...
```

### 8.2 기술 설계 문서(TD)
- TD는 항상 최신 설계와 설계 계획을 반영하는 작업 기준 문서로 유지한다.
- ADR은 결정 기록, TD는 현재 실행 기준(SSOT)으로 역할을 분리한다.
- 1. TD 생성 기준
  - 2개 이상 시스템/그룹에 영향을 주는 기능
  - 소유권/업데이트 순서/Fence 규칙이 등장하거나 변경되는 기능
  - 성능 예산(프레임/할당)에 직접 영향을 주는 기능
  - 구현 범위가 크거나 작업 분해가 필요한 기능
- 2. TD 편집 기준
  - 요구사항 변경
  - 채택 설계안 변경(대안 전환 포함)
  - 작업 분해/구현 계획 변경
  - 코드와 TD 간 불일치 발생
  - 검증/성능 기준 변경
- 3. TD 갱신 시점
  - 구현 시작 전 최신화
  - 구현 중 의미 있는 설계 변경 발생 시 동시 갱신
  - 구현 완료 후 검증 결과 반영
- 4. TD 필수 항목
  - 목표/비목표
  - 소유권(Writer/Owner)
  - 업데이트 순서(ExecutionBegin → Simulation → Request → ExecutionEnd)
  - 데이터 구조/제약(Enableable, Structural Change 최소화, Fence)
  - 작업 분해/진행 상태
  - 검증 계획/합격 기준
  - 관련 ADR 링크
- 5. ADR 연동 기준
  - 되돌리기 비용이 큰 결정은 ADR로 기록한다.
  - TD에는 현재 채택안만 유지하고 근거/대안은 ADR로 링크한다.
  - ADR과 TD가 충돌하면 ADR 기준으로 TD를 즉시 갱신한다.
- 6. 운영 가드레일
  - TD가 최신이 아니면 구현보다 TD 갱신이 우선이다.

---

## 9. 보안/권한(코딩 에이전트 운영)
- 모든 .md 파일과 Assets/_Project 아래 .cs 파일을 읽을 때는 UTF-8로 강제(Get-Content -Encoding UTF8).
- 답변에서 프로젝트 내부 파일을 참조할 때는 웹 링크나 절대 경로를 사용하지 않고, 프로젝트 루트 기준 상대 경로 형식으로 표기한다.
- 기본 모드: read-only
- 파일 수정/명령 실행이 필요한 경우에만 권한 상승(최소 권한 원칙)
- 네트워크/외부 업로드는 기본 금지(필요 시 명시적 승인)

---

## 10. MCP 사용 원칙 (MCP 연결 시)
- MCP 기본 사용 범위:
  - 관측(Observability): 콘솔, 씬 상태, 에셋 참조 관계 조회
  - 반영(Apply): 프리팹, 씬, ScriptableObject 변경 적용
  - 검증(Verify): refresh, 콘솔 확인, 테스트 실행
- 예외: 사용자가 명시적으로 요청하면 범위를 확장할 수 있다.
- 스크립트 편집은 MCP 대상에서 제외하고 일반 파일 편집 워크플로우를 사용한다.

---

## 11. Agent 작업 절차
### 11.1 적용 원칙
- "빠른 실행"을 기본값으로 하되, 리스크가 커질 때만 절차를 강화한다.
- 절차 준수보다 결과(안정성/검증 가능성/회귀 방지)를 우선한다.

### 11.2 표준 절차(1~6)
1. 설계 목표 제시
  - 사용자가 기능/요구사항을 제시하고, Agent는 필요 시 목표를 구체화하는 질문을 추가한다.
  - 작업 시작 시 목표/요구사항을 3~5줄로 요약한다.
2. 설계 방안 논의
  - 요구사항 기반으로 설계 방안을 논의한다.
  - 고영향 변경은 최소 2개 방안을 비교(장점/리스크/선택 이유)한 뒤 진행한다.
3. 문서화
  - 필요 시 설계 방안과 논의 내용을 문서화한다.
  - ADR/TD 기준은 8장 규칙을 따른다.
4. 작업 분해
  - 작업 규모가 크면 작업을 분해하여 임시 문서로 기록한다.
  - 분해된 작업 단위별로 5~6단계를 순차 반복한다.
5. 구현 계획 점검
  - 이전 단계 설계가 명료하면 생략 가능하다.
  - 설계가 불명확하거나 파급이 큰 경우 사용자 점검을 거친다.
6. 코드 구현
  - 합의된 설계와 문서를 기준으로 구현한다.
  - 구현 중 설계 편차가 발생하면 코드를 진행하기 전에 문서를 먼저 갱신한다.

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

### 11.5 검증 및 완료 보고
- 완료 전 검증은 3장 절차를 기본으로 한다.
  - `compile -> console error 0 -> EditMode -> PlayMode 스모크`
- 완료 보고에는 아래를 포함한다.
  - 변경 내용 요약
  - 검증 결과
  - 남은 리스크 또는 미검증 사유/범위

### 11.6 수정 게이트(Explicit Approval Gate)
- 기본 원칙: Agent는 기본적으로 read-only로 동작하며, 사용자 명시 승인 전에는 문서/코드/에셋을 수정하지 않는다.
- 수정 허용 조건: 사용자가 현재 컨텍스트에서 명시적으로 수정 승인을 요청한 경우에만 수행한다.
  - 예: "플랜 승인", "이 플랜으로 수정 진행", "코드 수정해", "문서 반영해"
- 비허용 신호: 모호한 동의(예: "ㅇㅋ", "좋아"), 단순 질의, 리뷰 요청, 아이디어 논의는 수정 승인으로 간주하지 않는다.
- 승인 범위: 승인 시점에 합의된 단일 작업 범위에만 유효하며, 범위가 바뀌면 재승인을 받아야 한다.
- 승인 전 허용 작업: 분석, 설계/플랜 작성, 영향도 검토, 패치 초안(diff) 제시, 읽기 전용 명령 실행.
- 승인 후 실행 규칙: 수정 시작 전에 "승인 문구"와 "적용 범위"를 1~3줄로 재확인한 뒤 구현한다.
