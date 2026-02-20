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
  - 
- 프로파일링:
  - Profiler(Entities Profiler 포함), Frame Debugger(필요 시)

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

## 5. DOTS/ECS 코딩 컨벤션(Conventions)
### 5.1 네이밍
- `IComponentData`: `*Component` / `*Tag`
- 굽기(MonoBehaviour): `*Authoring` + 내부 `Baker`

### 5.2 System 작성
- Entities 권장 패턴 준수
- 병렬 Job에서 Lookup 기반 **쓰기**는 지양
  - 기본: `EnabledRefRW<T>`(자기 엔티티)로 처리
  - 교차 엔티티 write 필요 시: **ECB.ParallelWriter** 또는 Owner 단일 단계로 이동
- ReadOnly/Write 의도를 어트리뷰트로 명확히 표기.
- 항상 실행되어야 하는 Job은 enableable 쿼리 함정을 피하도록 옵션/시그니처를 설계.

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
- 최소 스모크 테스트 통과(Play Mode 진입, 핵심 루프 1회 이상)
- 성능 리스크 변화시 근거(수치/캡처) 첨부
- 코드 리뷰 관점:
  - 소유권/업데이트 순서/플래그 전환 규칙이 문서·코드에서 일치

---

## 8. 문서/결정 기록(ADR)
- ADR은 모든 설계/변경에 대해 의무가 아니다.
- 아래 조건 중 하나 이상에 해당하는 **중요 결정**만 `Docs/ADR/ADR-YYYYMMDD-NN-*.md`에 기록한다:
  - 되돌리기 비용이 큰 구조/소유권/업데이트 순서 결정
  - 여러 시스템/문서에 파급되는 규칙 변경
  - 대안 비교와 근거를 남겨야 재논의 비용이 줄어드는 선택
  - 성능/안정성/운영 리스크에 직접 영향을 주는 결정
- 문서 포맷 강제는 최소화한다.
  - **필수**: 파일명 규칙, Metadata
  - **권장(세션 제안)**: 본문 섹션 구성/순서, 예시 템플릿
- ADR 본문 기본 구성(권장):
  - 문제(왜), 결정(무엇), 대안(비교), 결과(리스크/후속)
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

---

## 9. 보안/권한(코딩 에이전트 운영)
- 한국어 주석이 있는 Assets/_Project 아래 .cs 파일을 읽을 때는 UTF-8로 강제(Get-Content -Encoding UTF8).
- 기본 모드: read-only
- 파일 수정/명령 실행이 필요한 경우에만 권한 상승(최소 권한 원칙)
- 네트워크/외부 업로드는 기본 금지(필요 시 명시적 승인)
