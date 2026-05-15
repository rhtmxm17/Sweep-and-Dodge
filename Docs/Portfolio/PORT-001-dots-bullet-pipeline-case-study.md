# DOTS Bullet Pipeline Case Study

> 대량 탄환 처리를 위해 소유권, 업데이트 순서, 구조 변경 최소화 규칙을 명시적으로 분리한 기술 사례

## Metadata
- doc_id: `PORT-001`
- type: `Portfolio`
- status: `draft`
- last_updated: `2026-05-08`
- related_docs:
  - [../ADR/ADR-20260206-01-bullet-pipeline-ownership.md](../ADR/ADR-20260206-01-bullet-pipeline-ownership.md)
  - [../ADR/ADR-20260210-01-bullet-active-filtering-and-despawn-request.md](../ADR/ADR-20260210-01-bullet-active-filtering-and-despawn-request.md)
  - [../ADR/ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md](../ADR/ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md)
  - [../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](../ADR/ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md)
  - [../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)

## 1. 기술 선택 배경

이 프로젝트의 출발점은 ECS/DOTS 워크플로우를 실제 게임플레이 문제에 적용해 보는 것이었다. ECS는 대량 오브젝트 처리 문제를 학습하고 검증할 대상으로 선택한 기술이다.

그 학습 목표에 맞는 문제로 대량 탄환 시나리오를 선택했다. GameObject 중심 구조에서도 작은 규모의 탄환 처리는 충분히 구현할 수 있지만, 탄환 수가 늘어나고 생성/삭제, Transform 업데이트, 충돌/조회, 상태 전환이 프레임마다 반복되면 비용과 책임 경계가 빠르게 커진다.

따라서 이 프로젝트는 "ECS를 한번 써보기"가 아니라, GameObject 방식에서 부담이 커지는 문제를 설정하고 그 문제를 통해 ECS/DOTS의 장단점을 확인하는 방향으로 설계했다.

## 2. 문제 정의

`Sweep and Dodge`의 핵심 기술 과제는 대량 탄환을 처리하면서도 프레임 안정성, 변경 가능성, 디버깅 가능성을 유지하는 것이다.

단순한 탄환 생성/삭제 구조는 초기 구현은 쉽지만, 탄환 수가 많아질수록 다음 문제가 커진다.

- 대량 Entity 생성/삭제에 따른 structural change 비용
- 이동, 제거 판단, 풀 반납, 렌더 토글이 섞일 때 생기는 writer 책임 충돌
- FreeList, CellMap 같은 ECS 의존성 추적 밖 Native container 접근 순서 문제
- 새로운 제거 행동이나 청소 액션을 추가할 때 기존 파이프라인을 다시 흔들 위험

이 프로젝트는 이 문제를 프레임 내 책임을 고정된 단계로 나누는 것으로 해결했다.

ECS/DOTS는 이 문제에 적합한 데이터 지향 처리 모델을 제공하지만, 그것만으로 구조가 자동으로 좋아지는 것은 아니다. 실제 프로젝트에서는 update order, writer ownership, Native container dependency, enableable query semantics를 명시적으로 관리해야 했다.

## 3. 핵심 설계

탄환 처리 파이프라인은 다음 순서로 고정한다.

```text
ExecutionBegin -> Simulation -> Request -> ExecutionEnd
```

각 단계의 의미는 다음과 같다.

| Stage | Responsibility |
|---|---|
| `ExecutionBegin` | 스폰 실행, Pool/FreeList Dequeue |
| `Simulation` | 이동, 수명 갱신, CellMap build |
| `Request` | 제거 판단, read-only 조회, 요청 태그 생성 |
| `ExecutionEnd` | 디스폰 실행, 렌더 off, Pool/FreeList Enqueue |

이 구조의 목적은 "어느 시스템이 어떤 데이터를 쓸 수 있는가"를 tick 단계로 제한하는 것이다. Request 단계는 제거를 직접 실행하지 않고 요청만 남기며, 실제 비활성화와 풀 반납은 ExecutionEnd에서만 처리한다.

## 4. 대표 설계 결정

### Pool/FreeList ownership

Pool/FreeList는 Pool owner 영역에서만 접근한다. Spawn은 `ExecutionBegin`, Despawn 반납은 `ExecutionEnd`가 담당한다.

FreeList는 ECS 의존성 추적 밖의 컨테이너이므로, 접근 시스템은 `FreeListFence`를 결합해 순서를 강제한다. 이 규칙은 "그룹 순서상 괜찮아 보이는 코드"가 실제 JobHandle 경쟁을 만들지 않도록 하는 운영 기준이다.

### SpatialHash/CellMap writer 단일화

CellMap은 Simulation 단계가 writer다. Request 단계는 CellMap을 read-only로 조회해 제거 후보를 좁히고, 직접 반납이나 렌더 상태 변경을 수행하지 않는다.

이 분리는 충돌/조회 로직을 추가하더라도 CellMap build 책임이 분산되지 않게 만든다.

### Enableable 기반 상태 전환

대량 탄환 처리에서 Add/RemoveComponent 기반 상태 전환은 비용이 크다. 이 프로젝트는 활성 여부와 요청 상태를 enableable component로 표현해 구조 변경을 줄인다.

요청/소비 규칙은 다음처럼 운영한다.

- enable: 제거 요청 생성
- consume: ExecutionEnd에서 실행 후 요청 플래그 disable
- 병렬 Job에서 자기 엔티티 플래그는 `EnabledRefRW<T>` 중심으로 처리
- 교차 엔티티 write가 필요한 경우 ECB 또는 owner 단일 단계로 이동

### Render toggle ownership

Simulation/Request 단계는 렌더 상태를 직접 변경하지 않는다. 렌더 on/off는 Spawn/Despawn/Bootstrap owner가 담당한다.

다중 렌더 파츠를 고려해 루트 Entity에 단일 renderer가 있다고 가정하지 않고, bake 단계에서 수집된 render element buffer를 owner 단계가 순회하는 방향을 기준으로 둔다.

## 5. 품질 관리 관점

이 구조의 품질 관리는 단순히 "탄환이 보인다"가 아니라 다음 계약이 유지되는지 보는 방식으로 이루어진다.

- Pool/FreeList 접근 owner가 분산되지 않았는가
- CellMap writer가 Simulation으로 유지되는가
- Request 단계가 직접 디스폰/렌더 토글을 하지 않는가
- enableable query가 disabled 상태를 놓치는 방식으로 작성되지 않았는가
- fence 결합 규칙이 Shared Native container 접근 경로마다 유지되는가

기존 `OPS-001`에는 Editor/PlayMode 기반 스모크와 스트레스 관측값이 기록되어 있다. 이 프로젝트의 검증 문서는 기능 동작뿐 아니라 owner/update order/fence 계약이 유지되는지를 함께 보여주는 근거로 사용된다.

## 6. Trade-offs

이 설계는 단순 샘플보다 복잡하다. 특히 DOTS나 Job dependency에 익숙하지 않은 리뷰어에게는 문서 보조가 필요할 것이다.

대신 다음 장점이 있다.

- 시스템별 writer/owner가 명확해진다.
- 제거 행동 확장 시 Request 시스템 추가 중심으로 확장할 수 있다.
- 대량 Entity 처리에서 structural change를 줄일 수 있다.
- AI-assisted workflow에서도 agent가 따라야 할 경계가 문서화된다.

## 7. 현재 한계

현재 문서는 파이프라인 설계와 개발 중 검증 근거를 설명한다. 공개용 빌드 스냅샷의 최신 성능 수치, 영상, 빌드 패키지는 별도 공개 준비 단계에서 정리되는 영역이다.

이 문서는 "최종 출시 품질 보증서"가 아니라, 대량 Entity 처리 구조를 어떻게 설계하고 관리했는지 보여주는 기술 설명 문서이다.
