# DOTS로 대량 엔티티의 생명주기를 설계한 과정

`Sweep and Dodge`에는 피해야 하는 탄환과 청소해야 하는 먼지가 대량으로 등장합니다. 각 개체는 이동하고, 수명이 변하며, 플레이어·장애물·청소 범위와 상호작용합니다.

개체 수와 상호작용의 종류가 늘어나면서 단순한 생성·갱신·제거 순서만으로는 데이터의 변경 주체와 최종 결과를 예측하기 어려워졌습니다. 특히 하나의 엔티티에 같은 Tick 동안 여러 사건이 발생하면, 어떤 시스템이 먼저 실행되었는지에 따라 제거 이유나 후속 효과가 달라질 수 있었습니다.

이 글에서는 이러한 문제를 해결하기 위해 생명주기 처리를 네 단계로 나누고, 공유 데이터의 소유권과 작업 의존성을 명시적으로 연결한 과정을 설명합니다.

## 해결해야 했던 문제

초기 구조에서 중요했던 문제는 단순히 많은 엔티티를 빠르게 순회하는 것만이 아니었습니다.

- 풀에서 엔티티를 대여하고 반환할 수 있는 시스템을 제한해야 했습니다.
- 이동과 충돌 판정에서 함께 사용하는 공간 데이터를 안전한 순서로 읽고 써야 했습니다.
- 플레이어 피격, 청소, 수명 종료처럼 여러 제거 조건이 겹쳐도 하나의 결과로 정리해야 했습니다.
- 반복되는 활성·비활성 전환에서 불필요한 구조 변경을 줄여야 했습니다.
- 여러 렌더 파츠를 가진 엔티티도 생성과 제거 시 일관되게 표시해야 했습니다.

이를 위해 각 시스템이 즉시 엔티티를 제거하는 대신, 먼저 사건을 요청으로 기록하고 실행 단계에서 최종 결과를 반영하는 구조를 선택했습니다.

## 왜 DOTS를 선택했나요?

이 프로젝트는 완성된 GameObject 게임에 사후 최적화를 적용한 사례가 아닙니다. 계획 단계부터 Unity ECS/DOTS를 학습하고 실제 게임플레이 문제에 적용하는 것이 목표였습니다.

학습 과제로는 각각 이동하고 상태를 갱신하며 상호작용해야 하는 대량의 개체를 선택했습니다. 같은 컴포넌트 데이터를 묶어서 순회하고 Job으로 스케줄링하는 방식이 이 문제와 잘 맞는다고 판단했습니다.

GameObject 방식과 직접 비교 측정을 수행하지 않았기 때문에 두 방식의 보편적인 성능 차이를 주장하지는 않습니다. 이 사례의 초점은 DOTS를 선택한 뒤 데이터 흐름과 책임을 어떻게 구성했는지에 있습니다.

## 선택한 구조

한 번의 fixed Tick을 다음 네 단계로 구성했습니다.

```text
ExecutionBegin → Simulation → Request → ExecutionEnd
```

| 단계 | 담당하는 작업 |
|---|---|
| `ExecutionBegin` | 생성 요청을 처리하고 Pool·FreeList에서 엔티티를 대여합니다. |
| `Simulation` | 이동과 수명을 갱신하고 공간 탐색용 CellMap을 구성합니다. |
| `Request` | 피격과 청소 등 외부 상호작용을 판정하고 생명주기 요청을 기록합니다. |
| `ExecutionEnd` | 요청을 확정하고 엔티티와 렌더 상태를 비활성화한 뒤 풀에 반환합니다. |

시스템 그룹은 Job을 예약하는 순서를 표현합니다. 비동기로 실행되는 Job의 실제 완료 순서까지 자동으로 보장하지는 않기 때문에, ECS 외부 공유 컨테이너에는 별도의 의존성 연결이 필요했습니다.

## 판정과 실행을 분리했습니다

Simulation과 Request 단계의 시스템은 엔티티를 즉시 비활성화하거나 풀에 반환하지 않습니다. 대신 Enableable Component로 만든 요청 태그를 활성화하고, 제거 원인과 우선순위 등의 정보를 기록합니다.

같은 Tick에 여러 사건이 발생하면 다음 우선순위로 최종 원인을 정리합니다.

```text
PlayerHit
> VacuumCollected / CarryFullRemoved
> MotionCompleted
> StageBlocked
> LifetimeExpired
```

더 높은 우선순위의 사건은 기존 요청을 갱신하고, 같거나 낮은 우선순위의 사건은 먼저 기록된 결과를 유지합니다. 실제 비활성화와 풀 반환은 `ExecutionEnd`의 소유 시스템만 수행합니다.

이 구조를 통해 판정 시스템이 실행 책임까지 가져가지 않도록 했으며, 시스템 순서에 따라 서로 다른 후속 결과가 만들어질 가능성을 줄였습니다.

## 데이터의 소유권을 한곳에 모았습니다

### Pool과 FreeList

엔티티를 실제로 대여하는 작업은 `ExecutionBegin`의 Spawn 시스템이 담당하고, 반환은 `ExecutionEnd`의 Despawn 시스템이 담당합니다. Simulation과 Request 단계에서는 풀을 직접 변경하지 않습니다.

렌더 상태도 같은 실행 경계에서 전환합니다. 루트 엔티티 하나에 Renderer가 있다고 가정하지 않고, Bake 단계에서 수집한 렌더 파츠 목록을 순회해 여러 파츠를 함께 켜고 끕니다.

### CellMap

CellMap은 활성 엔티티를 공간 셀에 분류하여 충돌 가능성이 있는 인접 후보만 조회하기 위한 자료 구조입니다. `Simulation`이 CellMap을 비우고 다시 구성하는 유일한 Writer이며, `Request` 시스템은 필요한 셀을 읽기 전용으로 조회합니다.

각 상호작용 시스템이 모든 활성 엔티티를 다시 확인하지 않아도 되므로 플레이어, 빗자루, 장애물 충돌의 후보 범위를 줄일 수 있습니다.

### Fence

FreeList와 CellMap은 ECS가 컴포넌트 의존성을 통해 자동으로 추적하는 영역 밖에 있습니다. 따라서 앞선 Job의 `JobHandle`을 다음 접근 작업의 dependency에 결합하는 Fence를 사용했습니다.

```text
CellMap: 이전 Request 읽기 → Simulation 쓰기 → 현재 Request 읽기
Pool:    ExecutionEnd 반환 → 다음 ExecutionBegin 대여
```

Fence는 공유 데이터의 사용 권한을 나타내는 Lock이 아닙니다. 앞 단계의 작업이 끝난 뒤 다음 작업이 접근하도록 `JobHandle`의 흐름을 연결하는 경계로 사용했습니다.

## Enableable Component로 상태를 전환했습니다

엔티티의 활성 상태와 생명주기 요청은 `IEnableableComponent`로 표현했습니다. 컴포넌트를 반복해서 추가하고 제거하는 대신 Enabled 상태를 변경하여, 대량 엔티티의 잦은 Archetype 변경을 피했습니다.

요청을 만드는 Job은 요청 태그가 비활성인 엔티티뿐 아니라 이미 낮은 우선순위의 요청을 가진 엔티티도 처리해야 합니다. 현재 엔티티의 요청 태그 양쪽 상태를 다루는 경로에서는 다음 계약을 사용합니다.

- `[WithPresent(typeof(BulletDespawnRequestTag))]`
- `EnabledRefRW<BulletDespawnRequestTag>`
- 같은 엔티티에 있는 생명주기 데이터의 `ref` 접근

이 규칙을 통해 비활성 요청을 처음 활성화하는 경우와 기존 요청을 더 높은 우선순위로 갱신하는 경우를 하나의 Job에서 처리합니다.

## 프로파일링 결과에 따라 Spawn 경로를 단순화했습니다

Dust를 청소하지 않고 누적해 약 2.4만 개의 활성 엔티티가 유지되는 시나리오에서, Editor Profiler로 Spawn 실행 시스템이 큰 비용을 차지하는 것을 확인했습니다.

기존 FreeList 대여 경로는 첫 엔티티와 Iterator를 얻은 뒤에도 Key와 Entity를 다시 이용해 제거했습니다. 같은 Key에 많은 엔티티가 연결된 상황에서는 이미 찾은 위치를 다시 순회하는 비용이 발생했습니다. 이를 현재 Iterator를 직접 제거하는 방식으로 교정했습니다.

| 비교 경로 | Frame median | Frame p95 | Spawn median |
|---|---:|---:|---:|
| 기존 제거 API와 병렬 초기화 | 38.358ms | 42.267ms | 13.391ms |
| Iterator 제거와 직렬 초기화 | 27.441ms | 31.448ms | 2.848ms |

동일한 Editor 측정 조건에서 최종 경로의 Frame median은 28.46%, Spawn median은 78.73% 낮았습니다. Iterator 교정 이후에는 별도의 상태 초기화 Job이 제공하는 추가 이득이 작았고, Command Buffer와 Writable Lookup의 복잡도를 유지할 근거가 부족했습니다.

따라서 현재 구현은 Pool 소유 시스템이 Dequeue와 상태 초기화를 한 번에 처리하는 직렬 경로를 사용합니다. 이 비교 수치는 특정 Editor 측정 조건에서 구현 대안을 선택하기 위한 자료이며, standalone FPS를 나타내지는 않습니다.

## 실행 환경에서 다시 확인했습니다

최신 게임플레이 비주얼을 포함한 Windows standalone Development Build에서 Dust를 무입력·무청소 상태로 누적하는 대량 엔티티 시나리오를 동일 조건으로 600 frame씩 3회 측정했습니다.

- Active Total 평균: `24,148.3`
- Active Total 범위: `24,077–24,236`
- Frame interval median/p95/max: `7.291/9.249/12.872ms`
- `16.67ms` 초과 interval: `0/1,797`

이 결과는 명시한 장비와 통제 시나리오에서 파이프라인이 실제 빌드 환경에서도 동작하는지 확인한 보조 근거입니다. 자세한 조건과 전체 표, Profiler 이미지는 [대량 엔티티 누적 시나리오 프로파일링 결과](../Validation/large-entity-scenario/README.md)에서 확인할 수 있습니다.

## 트레이드오프와 남은 과제

- 명시적인 단계와 소유권은 단순한 샘플보다 구조가 복잡하며, DOTS와 Job dependency에 익숙하지 않은 독자에게 추가 설명이 필요합니다.
- CellMap과 FreeList는 ECS의 자동 의존성 추적 밖에 있으므로 프로젝트가 Fence 연결 규칙을 계속 유지해야 합니다.
- 현재 fixed Tick 파이프라인은 Render Frame과 완전히 독립된 시뮬레이션 루프로 운영되지는 않습니다.
- 자동 테스트와 PlayMode Smoke는 일부 설계 계약의 회귀를 확인하지만 플레이 감각과 모든 성능 상황을 보장하지는 않습니다.
- 최신 공개 측정은 Development Build의 통제 시나리오이며 최종 Release Build나 모든 하드웨어의 성능을 대표하지 않습니다.

이 사례의 핵심은 DOTS가 구조를 자동으로 해결했다는 데 있지 않습니다. 대량 데이터 흐름에 맞춰 Writer, 실행 단계, 작업 의존성과 상태 전환 규칙을 명시하고, 측정 결과에 따라 구현 복잡도를 조정한 경험에 있습니다.

## 관련 설계 기록

더 구체적인 코드 계약과 변경 배경이 필요하다면 다음 개발 기록을 참고할 수 있습니다.

- [Bullet 파이프라인 소유권](../../Docs/ADR/ADR-20260206-01-bullet-pipeline-ownership.md)
- [Fixed Tick 파이프라인 루트](../../Docs/ADR/ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md)
- [같은 엔티티의 생명주기 요청 Query](../../Docs/ADR/ADR-20260819-01-bullet-self-lifecycle-request-with-present.md)
- [FreeByKey Iterator Dequeue와 Spawn 초기화 단순화](../../Docs/ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)
