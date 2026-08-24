# Developer Perspective and Claim Evidence Notes

> `Sweep and Dodge` 포트폴리오 작성을 위한 개발자 관점·역할 분담·주장 근거 원천 노트

## Metadata
- doc_id: `PORT-NOTE-001`
- type: `PortfolioSourceNote`
- status: `working`
- audience: `internal`
- last_updated: `2026-08-24`
- related_docs:
  - [../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md](../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md)
  - [PORT-001-dots-large-entity-pipeline-case-study.md](PORT-001-dots-large-entity-pipeline-case-study.md)
  - [PORT-002-ai-assisted-engineering-workflow.md](PORT-002-ai-assisted-engineering-workflow.md)
  - [PORT-003-validation-report.md](PORT-003-validation-report.md)
  - [PORT-NOTE-002-stage2-standalone-profiling-evidence.md](PORT-NOTE-002-stage2-standalone-profiling-evidence.md)

## 1. 문서 목적

이 문서는 `Sweep and Dodge`의 공개용 포트폴리오 문장이 아니다. 개발자가 프로젝트의 문제, 설계, 플레이 결과를 어떻게 이해하고 판단했는지 보존하기 위한 원천 노트다.

이후 다음 작업에서 참고한다.

- 포트폴리오 정보 구조 설계
- README와 `PORT-*` 문서 개편
- Notion 프로젝트 페이지 작성
- 면접 후속 질문 준비
- 공개할 주장과 공개하지 않을 주장의 구분

### 사용 규칙

- 현재 코드와 채택된 ADR·TD가 기술 사실의 우선 기준이다.
- TaskBoard의 `Adopted Baseline`이 포트폴리오 결정의 우선 기준이다.
- 개인 기억과 코드·문서로 확인된 사실을 구분한다.
- 제안 주체가 불명확하면 임의로 귀속하지 않는다.
- 이 문서에 기록됐다는 이유만으로 공개 주장이 되지는 않는다.
- 불확실한 수치와 사후 검증이 없는 성과는 공개 결과로 사용하지 않는다.
- 폐기한 문장 후보와 표현 대안은 별도 가치가 없으면 보존하지 않는다.

## 2. 프로젝트 출발점과 문제 선택

### 개발자의 관점

프로젝트 계획 단계부터 DOTS 학습이 목표 중 하나였다. 학습 목표에 맞는 문제로, 각자 이동·상태 갱신·상호작용 연산이 필요한 대량 개체를 가정했다.

대량 개체가 플레이어와 상호작용한다는 조건에서 먼지를 쓸어 담는 형태의 프로토타입을 계획했다. 탄환 회피와 청소·수집을 결합하면서, 대량 Entity 처리 구조를 실제 플레이 문제에 적용하려 했다.

### 공개 시 주의할 점

GameObject 방식으로 같은 문제가 불가능하다고 주장하지 않는다. 직접 비교 측정이 없기 때문이다.

다음 정도로 설명한다.

> 개별 MonoBehaviour와 Transform 중심으로 수많은 개체의 이동·조회·상태 전환을 반복하면 처리 비용과 책임 관리 부담이 커질 것으로 보았다. 동일한 컴포넌트 데이터를 일괄 처리하고 Job으로 병렬화할 수 있는 DOTS가 프로젝트의 기술 문제에 적합하다고 판단했다.

## 3. DOTS를 사용하며 경험한 설계 관점의 변화

### 개발자의 관점

GameObject 구조에서는 “이 객체가 어떤 일을 하는가”가 설계의 중심이 되는 경우가 많았다. DOTS에서는 “이 월드에서 어떤 일이 발생하는가”와 “이를 위해 어떤 데이터가 존재하는가”가 설계의 중심이 됐다.

> 설계의 핵심이 객체에서 사건과 데이터로 넘어갔다.

대량 Entity의 상태와 게임 규칙은 ECS가 소유하고, 입력·UI·연출 같은 GameObject 영역은 필요한 상태를 읽거나 bridge를 통해 요청을 전달하도록 경계를 나눴다.

### 공개 판단

- 데이터 지향 설계를 학습하며 경험한 관점 변화로 사용한다.
- DOTS API 사용 경험보다 한 단계 깊은 회고로 활용한다.
- 객체 지향과 데이터 지향 중 하나가 항상 우월하다는 주장에는 사용하지 않는다.

## 4. 대량 엔티티 파이프라인

### 4.1 Pooling과 fixed-tick pipeline

개체의 spawn/despawn이 빈번하므로 pooling을 사용했다. 실제 대여·초기화·반환과 렌더 상태 변경은 Pool owner 영역에서만 수행하도록 책임을 제한했다.

파이프라인은 다음 순서를 가진다.

```text
ExecutionBegin → Simulation → Request → ExecutionEnd
```

- `ExecutionBegin`: 실행 가능한 spawn request 소비와 FreeList dequeue
- `Simulation`: 이동·수명·motion 완료 처리와 CellMap build
- `Request`: 플레이어 행동과 충돌 판단, lifecycle request 생성
- `ExecutionEnd`: lifecycle reaction, 비활성화, 렌더 off, FreeList enqueue

### 4.2 판정과 실행의 분리

한 fixed tick에 하나의 Entity에 여러 상호작용이 발생할 수 있다. 판정이 발생하는 즉시 Entity를 비활성화하고 풀로 반환하면 시스템 실행 순서에 따라 후속 결과가 달라질 수 있다.

따라서 각 판정 시스템은 직접 디스폰하지 않고 다음 정보를 가진 lifecycle request를 남긴다.

- 제거 사유
- 우선순위
- 관련 Entity
- 발생 Tick
- 접촉 위치와 방향

실제 비활성화와 FreeList 반환은 `ExecutionEnd` owner가 처리한다.

동일 Tick의 복수 요청은 다음 우선순위를 사용한다.

```text
PlayerHit
> VacuumCollected / CarryFullRemoved
> MotionCompleted
> StageBlocked
> LifetimeExpired
```

단순히 실행을 지연하는 것만으로 결과가 결정되는 것은 아니다. 판정과 실행을 분리하고, 복수 요청을 명시적인 우선순위로 병합함으로써 최종 결과를 선택한다.

### 공개 판단

- 4단계 pipeline과 owner 분리는 핵심 주장으로 사용한다.
- lifecycle reason 병합은 대표적인 세부 설계 사례로 사용한다.
- 각 요소의 최초 제안 주체는 주장하지 않는다. 파이프라인은 여러 작업에 걸쳐 점진적으로 형성됐다.

## 5. CellMap과 Job dependency

### 5.1 CellMap의 목적

모든 개체를 플레이어와 환경에 직접 비교하지 않고, 위치에 따라 공간 셀로 분류한다.

Simulation에서 활성 Entity를 CellMap에 기록하고, Request 시스템은 상호작용이 발생할 가능성이 있는 셀의 Entity만 조회한다. 이를 통해 실제 충돌 판정 후보를 좁힌다.

### 5.2 Fence에 대한 이해

시스템 그룹 순서는 Job을 스케줄하는 시스템의 호출 순서다. 비동기로 실행되는 Job 자체의 완료 순서까지 자동으로 보장하지 않는다.

CellMap과 FreeList는 `SharedStatic`에 저장된 ECS 외부 Native container다. ECS component dependency 추적만으로는 시스템 간 접근 순서를 연결할 수 없으므로 마지막 관련 작업의 `JobHandle`을 fence로 전달한다.

CellMap의 순서는 다음과 같다.

```text
이전 Request reader
→ CellMapFence
→ 현재 Simulation clear/build
→ CellMapFence
→ 현재 Request reader
→ 다음 Simulation을 위한 fence publish
```

Pool의 순서는 다음과 같다.

```text
ExecutionEnd FreeList enqueue
→ PoolFence
→ 다음 ExecutionBegin FreeList dequeue
```

Fence는 lock이나 접근 권한을 얻는 키가 아니다. 앞 작업의 완료를 다음 작업 dependency에 전달하는 handle chain으로 이해한다.

### 공개 판단

- “ECS 외부 Native container의 dependency를 명시적으로 연결했다”는 심화 근거로 사용한다.
- 메인 페이지의 독립적인 성과 카드로는 사용하지 않는다.
- 멀티스레딩 전반의 전문성을 포괄적으로 주장하지 않는다.

## 6. Structural change와 Enableable component

### 개발자의 이해

DOTS는 동일한 컴포넌트 조합을 가진 Entity를 archetype으로 분류하고, archetype chunk 안에 컴포넌트별 배열로 저장한다.

컴포넌트를 추가하거나 제거하면 Entity의 archetype이 달라진다. 기존 chunk에서 다른 archetype의 chunk로 데이터를 이동하고 Entity 위치 정보를 갱신해야 하므로, 대량 Entity의 빈번한 상태 전환에 사용하면 부담이 커질 수 있다.

활성 상태와 request 상태는 `IEnableableComponent`로 표현한다. 이를 통해 컴포넌트를 제거하지 않고 enabled 상태만 변경하며 archetype 변경을 피한다.

### Query semantics

- enableable component를 일반 query 조건에 넣으면 enabled Entity만 포함된다.
- 양쪽 상태를 모두 다뤄야 하면 `[WithPresent(typeof(T))]`와 `EnabledRefRW<T>`를 사용한다.
- disabled 상태만 처리하면 `[WithDisabled(typeof(T))]`를 사용한다.
- `IgnoreComponentEnabledState`는 query의 다른 enableable 필터까지 모두 무시해야 할 때만 사용한다.
- 다른 Entity 접근은 `ComponentLookup<T>` 경로로 분리한다.
- `NativeDisableParallelForRestriction`은 disabled Entity를 query에 포함하는 기능이 아니다. 병렬 lookup write의 안전성 검사를 완화하므로 비경합 소유권을 별도로 증명해야 한다.

lifecycle request는 이미 enabled된 낮은 우선순위 요청이 더 높은 우선순위 요청으로 승격될 수 있다. 따라서 disabled request만 조회하면 기존 우선순위 계약을 훼손한다.

### 공개 판단

- Enableable component로 빈번한 structural change를 줄인 설계는 핵심 주장으로 사용한다.
- query attribute와 병렬 lookup 세부사항은 기술 면접용 심화 내용으로 둔다.

## 7. 플레이 가능한 데모

### 7.1 핵심 플레이 루프

```text
위험 탄환 회피
→ Source 영역에서 청소 대상 수거
→ Carry가 차면 Deposit으로 복귀
→ 비운 뒤 아직 활성인 Source로 재진입
→ Source를 약화·고갈시키며 스테이지 완료
```

청소가 끝난 영역에서는 대상이 더 이상 생성되지 않으므로, 플레이어는 아직 활성인 Source로 이동한다.

위험 탄환은 일반 청소 대상보다 정밀한 행동을 요구한다. 스윕이 정면을 지나는 짧은 타이밍에 직접 제거할 수 있으며, 이를 위해 더 가까이 접근하는 위험을 감수해야 한다.

### 7.2 데모 흐름

```text
Title → Lobby → Stage → Result → Demo Complete
```

대량 Entity가 생성되는 기술 샌드박스에서 끝내지 않고, 외부 사용자가 빌드를 실행해 스테이지를 선택하고 성공·실패·재시도와 최종 완료까지 경험할 수 있도록 구성했다.

### 공개 판단

- 게임플레이·UI·콘텐츠 흐름을 연결한 플레이 가능한 데모는 핵심 주장으로 사용한다.
- 출시 후보나 상용 게임 수준으로 표현하지 않는다.

## 8. 플레이 기반 개선 사례: BroomSweep

### 관찰한 문제

기존 청소 동작은 기계적인 수준을 넘어 초현실적으로 느껴졌다. 현실의 빗자루질과 연결되는 동작으로 읽히지 않았다.

### 개발자가 제안한 방향

- 현실의 빗자루질처럼 좌우 스윕을 교대
- 스윕 중 이동 속도 제한
- 스윕 중 방향을 크게 바꿔 청소 범위를 비정상적으로 넓히는 문제를 막기 위해 방향 잠금
- AI agent에게 추가 개선안 요청

### 역할 분담

개발자:

- 플레이 중 문제 발견
- 개선 목표와 주요 제약 제시
- 적용할 설계안 선택
- 구현 후 조작감 확인
- 스윕 시작 각도의 어색함을 직접 조정

AI agent:

- 추가 개선안 제안
- 설계 문서와 구현 계획 작성
- 선택이 필요한 설계안에 대한 승인 요청
- 승인된 계획에 따른 코드 구현
- 자동 테스트와 실패 수정 반복
- 최종 검증 결과 보고

스윕 속도가 선형에서 점감형으로 변경된 제안 주체는 기억이 불확실하다. 최종 동작은 설명할 수 있지만 제안 주체는 공개 역할 분담에 사용하지 않는다.

### 공개 서사

> 개발자가 플레이 경험에서 문제와 개선 목표를 정의하고 최종 설계를 선택했으며, AI agent가 이를 실행 가능한 계약으로 구체화해 구현·자동 검증했다. 이후 개발자가 다시 플레이해 자동 검증으로 판단할 수 없는 조작 감각을 조정했다.

## 9. 목표 기반 개발 사례: Stage Map Editor

### 9.1 기존 제작 방식의 문제

기존 Scene·Tilemap·Marker 기반 제작 방식은 Unity 사용자에게 익숙한 UI를 사용한다는 장점이 있었다. 그러나 프로젝트 전용 Marker 구성과 작업 순서를 암기해야 했고, 맵 편집을 위해 무엇을 해야 하는지 발견하기 어려웠다.

원하는 데이터를 찾아가기도 어려웠으며, 편집 결과의 미리보기와 전체 구조 파악도 부족했다.

개발자는 일반적으로 사용되는 맵 에디터의 작업 흐름과 비교해 프로젝트 도구에 부족한 기능을 탐색하도록 추가 요청했다.

### 9.2 개발자가 직접 요구한 내용

- 전용 Editor Window
- Scene View Tool과 Window를 함께 사용하는 편집 방식
- 이동 가능성, Source, Deposit 타일 편집
- Player Start, HazardActor, Anchor 배치
- HazardActor archetype과 발사 pattern은 별도 전용 도구로 분리
- 기존 runtime asset과 runtime ECS 계약 유지
- 기존 Stage 1~3 데이터 migration
- Source 진행도에 따른 HazardActor Spawn·Phase·Retire preview

### 9.3 Agent 제안 후 개발자가 선택한 내용

- `StageMapDocument`를 authoring SSOT로 사용
- validation issue 목록과 대상 이동
- dry-run diff 후 Apply
- stale plan 거부
- Undo 가능한 적용

### 9.4 데이터 경계

```text
StageMapDocument
→ validation
→ dry-run diff
→ Apply
→ StageLayoutSO / StageDefinitionSO / StageCatalogSO
→ 기존 runtime ECS
```

새로운 document가 runtime 구조를 대체한 것이 아니다. 제작자가 직접 편집하는 입력을 통합하고, 기존 runtime asset을 출력으로 유지했다.

### 9.5 실제 사용 후 요구한 UX 개선

- Tile Paint palette를 드롭다운에서 라디오 버튼 기반으로 변경
- 동일 구조 데이터 목록을 단순 문자열에서 열이 구분된 표 형태로 변경
- Source 진행도에 따른 HazardActor encounter preview 추가

### 공개 배치

- 메인 프로젝트 페이지: 짧은 대표 보조 사례
- 심화 자료: document SSOT, validation, dry-run/Apply, migration
- 선택적 심화 자료: HazardActor Workbench와 encounter preview 세부 구현

“실무 표준 맵 에디터를 구현했다”고 표현하지 않는다. 일반적인 편집기 패턴을 참고해 프로젝트의 발견성·탐색성·미리보기·안전한 적용 절차를 보완했다고 설명한다.

## 10. AI-assisted engineering workflow

AI 사용은 방어적으로 정당화할 대상이 아니라 일상적인 개발 도구 체계의 일부다.

### 10.1 플레이 기반 개선

```text
개발자가 플레이 문제 발견
→ 개선 목표와 제약 정의
→ agent가 대안과 구현안 제안
→ 개발자가 최종안 선택
→ agent가 문서·코드·자동 검증 수행
→ 개발자가 플레이 감각 재검증
```

### 10.2 목표 기반 개발

```text
개발자가 문제·범위·비변경 경계 정의
→ agent가 구조적 대안 제안
→ 개발자가 설계 선택
→ agent가 계획·코드·검증 수행
→ 개발자가 실제 workflow를 사용하고 UX 개선
```

Guardrail은 위험한 AI를 통제하기 위한 장치로 설명하지 않는다. 프로젝트 지식과 반복 오류 패턴을 ownership, update order, validation rule과 실행 가능한 계약으로 구조화한 엔지니어링 체계로 설명한다.

## 11. 테스트 자동화에 대한 관점

테스트 자동화는 이 프로젝트 이전의 경험 영역이나 초기 학습 목표가 아니었다. 프로젝트 안정화를 위해 AI agent가 제안한 방식을 수용하면서 도입했다.

이 프로젝트에서 테스트의 실질적인 역할은 다음과 같다.

> 설계 결정의 결과를 문서 외에도 실행 가능한 계약으로 일부 고정하고, 후속 구현이 이를 위반할 때 발견하는 2차 방지 장치.

AI agent가 후속 코드를 생성하면서 기존 결정을 놓친 경우, 테스트 실패를 통해 위반이 발견된 사례가 있다. agent는 실패한 테스트의 목적과 보호 대상 계약을 다시 확인한 뒤 구현을 교정했다.

### 대표적인 역할

- 계약 테스트: 시스템 배치와 update order 회귀 탐지
- 합성 EditMode smoke: 제한된 ECS behavior를 빠르게 반복
- PlayMode smoke: Scene·GameObject bridge·ECS runtime 통합 확인

### 공개하지 않을 주장

- 테스트 주도 개발을 수행했다.
- 체계적인 테스트 전략을 직접 설계했다.
- 테스트 자동화 전문성이 있다.
- 전체 테스트 개수가 품질을 증명한다.
- 테스트가 조작감·재미·성능을 보장한다.

테스트는 핵심 역량이 아니라 프로젝트 결과를 보조하는 근거와 AI-assisted workflow의 guardrail 사례로 사용한다.

## 12. 성능 진단 사례: 장애물 충돌

### 12.1 문제 발견

탄환을 가로막는 장애물 기능 구현 후, 기능은 정상적으로 동작했지만 현저한 프레임 저하가 발생했다.

개발자가 Unity Profiler를 확인해 `BulletObstacleHitRequestSystem`이 지배적인 시간을 사용하고 있음을 특정했다. 당시 사용자 프롬프트에는 다음 값이 남아 있다.

```text
BulletObstacleHitRequestSystem: 175.12ms
BulletSimulationSystem: 0.024ms
```

이 값은 원본 Profiler 캡처가 남아 있지 않은 내부 기억 자료다.

### 12.2 개선 방향

당시 장애물 충돌은 활성 탄환을 폭넓게 다시 순회했다. 개발자는 이미 존재하던 CellMap을 재사용해 장애물과 충돌할 가능성이 있는 셀의 탄환만 조회하도록 수정할 것을 요청했다.

Git 이력에는 `CellMap 기반 장애물-탄환 상호작용` 변경이 남아 있다.

CellMap 적용 후 플레이에서 명백한 프레임 저하 해소를 확인했지만 동일 조건의 frame time이나 FPS를 다시 측정하지 않았다.

### 12.3 이후 구조 변화

이후 grid-authoritative stage 구조를 도입하면서 기존 `BulletObstacleHitRequestSystem`을 제거했다.

현재 구조는 다음과 같다.

- 장애물 판정을 `BulletSimulationSystem`의 이동 pass에 통합
- `prevXZ → nextXZ` swept-path grid query
- 충돌 시 `StageBlocked` lifecycle request 생성
- Request 단계의 별도 탄환 전체 재순회 제거

### 공개 판단

정성적인 병목 진단 사례로 사용한다.

> 플레이 중 성능 저하를 감지하고 Profiler로 병목 시스템을 특정한 뒤, 전체 순회 대신 기존 공간 분할 구조를 재사용하도록 개선 방향을 결정했다.

다음은 공개 성과로 사용하지 않는다.

- 정확한 개선 전·후 수치
- 개선 배율
- 사후 FPS
- 현재 장애물 충돌이 CellMap을 사용한다는 설명
- 포괄적인 성능 엔지니어링 전문성

### 회고

병목을 측정하고 원인을 특정했지만, 동일 조건의 사후 측정 결과를 남기지 못했다. 공개 빌드 측정에서는 실행 조건과 결과를 함께 보존해야 한다.

## 13. 현재 한계

기술 데모의 시스템과 완료 흐름은 구성됐지만 다음 한계가 있다.

- 청소·피격·위험 탄환 제거의 시청각 피드백 부족
- 탄환 제거 시 보너스 UI 외의 즉각적인 반응이 약함
- 피격 시 캐릭터 반응 외의 피드백이 제한적
- 청소–Deposit 반복 구조의 장기적인 재미와 선택지 부족
- Stage 2 standalone Development Build 측정은 확보했지만 active 수와 Dust/Hazard 구성 비율을 같은 캡처에 직접 결합하지 못함
- 최종 공개 후보 빌드의 반복 측정과 완전한 환경 manifest는 미확보
- 최종 영상·GIF·빌드 전달 자료 부재

이 한계는 실패를 숨기는 항목이 아니라 기술 데모의 현재 범위와 다음 개선 방향으로 설명한다.

## 14. 주장–근거–노출 매트릭스

| 주장 | 설명 가능성 | 주요 근거 | 노출 수준 |
|---|---|---|---|
| DOTS 대량 Entity 처리 | 충분 | 코드, ADR, 실제 데모 | 핵심 |
| 4단계 fixed-tick pipeline | 충분 | 시스템 그룹, ADR, 계약 테스트 | 핵심 |
| Pool/lifecycle owner 분리 | 충분 | 코드, lifecycle utility | 핵심 |
| CellMap 후보 축소 | 충분 | 코드, ADR, Git 사례 | 핵심 |
| Enableable 상태 전환 | 충분 | 코드, query 계약 | 핵심 |
| Fence dependency | 보조 설명 가능 | 코드, 계약 테스트 | 심화 |
| 플레이 가능한 3 Stage 데모 | 충분 | 운영 Scene, flow, PlayMode smoke | 핵심 |
| BroomSweep 플레이 개선 | 충분 | TD, 코드, 개발자 경험 | 대표 사례 |
| Stage Map Editor | 충분 | ADR, TD, 코드, asset, migration | 대표 보조 사례 |
| AI-assisted workflow | 충분 | BroomSweep, Stage Map Editor | 독립 심화 섹션 |
| 자동 테스트 전문성 | 주장하지 않음 | 테스트는 존재 | 비공개 |
| 테스트 기반 계약 보조 | 충분 | 계약·smoke 사례 | 보조 |
| 장애물 Profiler 진단 | 사례 설명 가능 | 사용자 프롬프트, Git 이력 | 정성적 보조 |
| 2.5만 전후 active Entity | Editor 통제 시나리오에서 평균 약 2.4만 재현 | `PORT-003` Stage 2 profiling | 보조 |
| standalone Development Build frame budget | 조건부 설명 가능 | `PORT-003`, `PORT-NOTE-002`, Profiler capture | 보조 |
| 최종 공개 후보 빌드 성능 | 아직 없음 | T3에서 확보 예정 | 보류 |
| GameObject 대비 성능 우위 | 직접 비교 없음 | 없음 | 비공개 |

## 15. 주장하지 않을 내용

- GameObject 방식보다 정량적으로 우수하다는 주장
- 테스트 장비와 통제 시나리오를 벗어나 2.5만 Entity에서 보편적인 60fps를 보장한다는 주장
- 개발 중 snapshot을 최종 공개 빌드 성능으로 표현
- 테스트 전략 또는 테스트 자동화 전문성
- 광범위한 성능 엔지니어링 전문성
- 개발자가 모든 코드를 직접 작성했다는 표현
- 기억이 불확실한 제안 주체
- 출시 수준의 시청각 완성도
- 장기적인 게임 재미와 콘텐츠 깊이

## 16. T3로 넘길 증거 공백

- standalone 측정과 동시에 기록한 전체 active Entity 수와 Dust/Hazard 구성 비율
- 해상도·품질·빌드 옵션을 포함한 측정 manifest와 동일 조건 반복 측정
- Editor 직접 집계, standalone Development Build profiling, 최종 public benchmark의 조건·해석 분리
- 대표 플레이 영상과 GIF
- Stage Map Editor 화면 자료
- 공개 빌드 기동·완주 smoke
- 구체적인 성능 캡처와 증거 자료의 보존 형식

## 17. Stage 2 standalone profiling 판단

Deep Profile Support를 활성화한 최초 standalone 캡처는 계측 sample과 파일 크기가 크게 증가해 성능 근거에서 제외했다. 비활성화한 uncapped 캡처에서는 ECS fixed Tick이 있는 frame과 없는 frame의 차이를 확인했으며, Tick이 일부 frame에서만 실행되므로 전체 평균 FPS를 대표 수치로 사용하지 않는다.

이후 `Application.targetFrameRate = 60`을 측정용으로 일시 적용한 캡처에서는 600개 모든 frame에서 대량 엔티티 pipeline이 실행됐고, frame interval median `16.670ms`, p95 `17.022ms`, max `17.402ms`, 20ms 초과 0개를 기록했다. 이는 해당 테스트 장비와 통제 시나리오에서 60fps frame budget을 충족했다는 보조 근거다.

60fps cap은 실제 데모 운영 정책이 아니라 ECS Tick과 GameObject Update가 같은 frame에 실행되는 조건을 만들기 위한 임시 측정 설정이다. 또한 현재 fixed-step accumulator는 render frame당 최대 한 Tick을 소비하므로 두 loop가 완전히 독립적으로 실행된다고 주장하지 않는다.

Editor의 약 2.4만 active entity 직접 집계와 standalone frame time은 같은 Stage 2 콘텐츠에 대응하지만 동시 측정이 아니다. 공개 시에는 두 근거를 구분하며, standalone active 수와 Dust/Hazard 구성 비율을 직접 확보하기 전까지 `약 2.5만에서 60fps`를 하나의 무조건적 성능 문구로 축약하지 않는다.
