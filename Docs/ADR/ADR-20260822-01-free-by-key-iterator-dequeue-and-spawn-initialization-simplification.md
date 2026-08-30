# ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification
> `FreeByKey` 대여에서 iterator 기반 제거를 사용하고 Spawn 상태 초기화는 직렬 Owner 경로로 유지하는 결정

## Metadata
- doc_id: `ADR-20260822-01`
- type: `ArchitectureDecisionRecord`
- status: `accepted`
- date: `2026-08-22`
- related_docs:
  - [ADR-20260211-02-bullet-type-key-pool-set.md](ADR-20260211-02-bullet-type-key-pool-set.md)
  - [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)
  - [Large-Entity Scenario Profiling](../../Portfolio/Validation/large-entity-scenario/README.md)
  - [../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md](../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md)

## 배경

공개 빌드와 같은 Stage 2 콘텐츠에서 청소하지 않고 Dust를 누적해 약 2.5만 active entity plateau를 재현하자 Editor frame time이 33ms를 넘었다. Profiler에서 `SpawnRequestRoundRobinExecutionSystem`이 가장 큰 비중을 차지했고, 초기 측정에서 system Self time은 14.29ms였다.

초기 가설은 대여한 Entity의 상태 적용이 메인 스레드에서 직렬 실행되는 비용이었다. 이를 확인하기 위해 상태 적용을 `SpawnInitializationCommand`로 수집하고 `IJobParallelFor`로 실행했지만, 세 번의 측정에서 Spawn median은 `13.387~13.454ms`로 약 6%만 감소했다. 실제 초기화 Job의 p95도 `0.027~0.045ms`에 머물러 주 병목을 설명하지 못했다.

`FreeByKey`는 `NativeParallelMultiHashMap<int, Entity>`이며, 기존 대여 경로는 다음 순서를 사용했다.

```text
TryGetFirstValue(key, out entity, out iterator)
-> Remove(key, entity)
```

Collections 구현에서 `Remove(key, value)`는 일치 항목을 제거한 뒤에도 같은 bucket chain을 끝까지 순회한다. 같은 TypeKey에 대량의 Entity가 들어가는 Dust 풀에서는 대여마다 긴 동일-key chain을 반복해서 확인하게 된다. 반면 `TryGetFirstValue`가 이미 반환한 iterator를 제거하면 일치 항목 이후의 동일-key chain을 다시 훑지 않는다.

## 결정

- `SpawnRequestCommonUtility.TryDequeueByKey`는 `TryGetFirstValue`가 반환한 iterator를 `FreeByKey.Remove(iterator)`에 전달한다.
- Entity 대여는 기존처럼 `ExecutionBegin`의 Spawn Owner가 직렬로 수행한다.
- 대여 직후 `ApplySpawnedBulletState`도 같은 Owner 경로에서 직렬로 실행한다.
- `SpawnInitializationCommand`, persistent command list, `ApplySpawnInitializationJob`, 관련 `NativeDisableParallelForRestriction`은 유지하지 않는다.
- Pool 대여와 반환의 Owner, `PoolFence`, round-robin 요청 소비, active count와 backlog metric 갱신 계약은 변경하지 않는다.

## 대안

### 상태 초기화 병렬 Job 유지

- iterator 교정과 함께 사용한 B에서는 가장 낮은 frame time을 기록했다.
- 그러나 직렬 초기화를 복원한 C는 B 대비 Frame median `+1.82%`, p95 `+3.13%`, Spawn median `+0.134ms(+4.94%)`였다.
- 모두 사전에 정한 단순화 허용 범위 안이므로 persistent list, command 복사, Job scheduling, 다수 writable lookup과 수동 비경합 증명 비용을 유지할 근거가 부족했다.

### 풀 컨테이너 전면 교체

- 타입별 stack/list처럼 대여 의미에 더 직접적인 구조를 사용할 수 있다.
- 현재 iterator 제거만으로 주 병목이 해소됐고, 컨테이너 교체는 Bootstrap, Despawn 반납, Fence와 다중 producer 경로까지 파급되므로 이번 범위에서는 보류한다.

### 기존 `Remove(key, entity)` 유지

- 기능 결과는 동일하지만 동일 TypeKey chain이 길어질수록 불필요한 전수 순회 비용이 커진다.
- Stage 2 측정에서 지배적인 비용으로 확인되어 채택하지 않는다.

## 측정 결과

측정 조건은 Unity 6000.3.6f1 Windows Editor, SampleScene의 Stage 2, 시작 대화 skip, 무입력·무청소, warm-up 6초 후 15초 Profiler 기록이며 각 버전을 세 번 실행했다. 아래 값은 세 run 대표값이다.

| 구분 | Frame median | Frame p95 | Spawn median |
|---|---:|---:|---:|
| A: 기존 제거 API + 병렬 초기화 | 38.358ms | 42.267ms | 13.391ms |
| B: iterator 제거 + 병렬 초기화 | 26.951ms | 30.493ms | 2.714ms |
| C: iterator 제거 + 직렬 초기화 | 27.441ms | 31.448ms | 2.848ms |

최종 C는 A 대비 Frame median 28.46%, Frame p95 25.60%, Spawn median 78.73% 감소했다. Spawn system 하위 `GC.Alloc` 표본은 세 run 모두 0이었다.

## 결과와 한계

- 동일 TypeKey 대량 풀에서 대여 비용을 지배하던 불필요한 chain 순회를 제거했다.
- 상태 초기화는 직렬 Owner 경로로 되돌려 Pool ownership과 코드 복잡도를 단순하게 유지한다.
- `CountFreeByKey`의 전체 chain 순회와 풀 컨테이너 자체의 적합성은 이번 결정 범위 밖이다.
- 측정에는 Windows Editor와 Profiler 오버헤드가 포함된다. standalone/public build 성능, 60fps 보장, GameObject 방식 대비 우위를 증명하지 않는다.
