# Sweep and Dodge

> Unity DOTS/Entities로 대량의 개체를 처리하면서 회피·청소·수집 플레이를 완성한 기술 데모입니다.

`Sweep and Dodge`는 화면에 쌓이는 먼지를 빗자루로 쓸어 수거하고, 위험한 탄환을 피하거나 제거하는 3개 스테이지의 미니게임입니다.

이 프로젝트에서는 각각 이동하고 상호작용하는 대량의 개체를 실제 플레이 안에서 처리하기 위해 Unity DOTS/Entities를 사용했습니다. 대량 생성과 회수, 충돌 후보 탐색, 여러 사건이 동시에 발생했을 때의 상태 전환을 예측 가능한 구조로 만드는 데 중점을 두었습니다.

## 프로젝트 한눈에 보기

| 항목 | 내용 |
|---|---|
| 프로젝트 성격 | 플레이 가능한 Unity 클라이언트 기술 데모 |
| 핵심 플레이 | 탄환 회피, 먼지 청소·수집, 수거함 비우기 |
| 플레이 흐름 | `Title → Lobby → Stage → Result → Demo Complete` |
| 주요 기술 | Unity 6000.3.6f1, C#, Entities/DOTS, Job System |
| 대상 플랫폼 | Windows PC |
| 담당 범위 | 게임플레이, DOTS 런타임, UI 흐름, 스테이지 콘텐츠, 제작 도구 |

## 무엇을 보여주는 프로젝트인가요?

### 대량 개체를 실제 플레이 안에서 처리했습니다

대량의 개체를 단순히 화면에 출력하는 별도의 스트레스 테스트로 만들지 않았습니다. 먼지가 계속 생성되고 사라지는 환경에서 플레이어가 이동하고, 청소하고, 위험 요소와 상호작용하는 게임 흐름에 연결했습니다.

### 데이터의 소유권과 실행 순서를 명확하게 구성했습니다

개체의 생성, 이동, 충돌 판정, 제거가 서로의 데이터를 임의로 변경하지 않도록 각 단계의 책임을 나눴습니다.

`ExecutionBegin → Simulation → Request → ExecutionEnd` 순서로 한 번의 시뮬레이션을 구성하고, 실제 생성과 반환은 풀을 소유한 시스템만 수행하도록 했습니다. 충돌 판정 시스템은 엔티티를 즉시 제거하지 않고 요청을 남기며, 마지막 단계에서 여러 요청을 정리한 뒤 상태를 변경합니다.

### 기술을 확인할 수 있는 완결된 데모 흐름을 만들었습니다

타이틀과 스테이지 선택부터 성공·실패·재시도와 최종 완료까지 이어지는 흐름을 구현했습니다. 이를 통해 DOTS 런타임이 UI와 게임플레이, 스테이지 콘텐츠 안에서 함께 동작하는 모습을 확인할 수 있습니다.

## 플레이 방식

플레이어는 위험한 탄환을 피하면서 Source 영역에서 생성되는 Dust를 빗자루로 쓸어 수거합니다. Carry가 가득 차면 Deposit으로 돌아가 비우고, 아직 청소가 끝나지 않은 Source를 찾아 다음 행동을 이어갑니다.

빗자루는 Dust를 모으는 도구이면서 위험한 탄환을 직접 제거하는 수단이기도 합니다. 더 정밀한 타이밍과 위치 선정이 필요하지만, 단순 회피 외의 능동적인 선택을 제공합니다.

## 핵심 기술 구조

```text
ExecutionBegin → Simulation → Request → ExecutionEnd
```

| 단계 | 담당하는 작업 |
|---|---|
| `ExecutionBegin` | 생성 요청을 처리하고 풀에서 엔티티를 대여합니다. |
| `Simulation` | 이동과 수명을 갱신하고 공간 탐색용 CellMap을 구성합니다. |
| `Request` | 피격과 청소 등 외부 상호작용을 판정하고 생명주기 요청을 기록합니다. |
| `ExecutionEnd` | 요청을 확정하고 엔티티를 비활성화한 뒤 풀에 반환합니다. |

이 구조와 함께 다음 설계 요소를 사용했습니다.

- Pool과 FreeList를 이용한 대량 엔티티 재사용
- 인접한 공간의 후보만 조회하기 위한 CellMap
- 구조 변경을 줄이기 위한 Enableable Component
- ECS가 자동 추적하지 않는 공유 컨테이너의 작업 순서를 연결하는 Fence
- 여러 제거 원인이 겹쳤을 때 결과를 결정하는 생명주기 우선순위

자세한 설계 과정은 [DOTS로 대량 엔티티의 생명주기를 설계한 과정](Portfolio/CaseStudies/large-entity-pipeline.md)에서 확인할 수 있습니다.

## 실행 환경에서 확인한 결과

최신 게임플레이 비주얼을 포함한 Windows standalone Development Build에서 Dust를 청소하지 않고 누적하는 대량 엔티티 시나리오를 같은 조건으로 600 frame씩 3회 측정했습니다.

- Active Total 평균: `24,148.3`
- Active Total 범위: `24,077–24,236`
- Frame interval median/p95/max: `7.291/9.249/12.872ms`
- `16.67ms` 초과 interval: `0/1,797`

이 수치는 지정한 장비와 통제 시나리오에서 대량 엔티티 파이프라인이 실행되는 모습을 확인한 보조 근거입니다. 일반 플레이의 상시 개체 수나 모든 환경의 60fps를 의미하지 않습니다.

[측정 조건, 전체 결과와 Profiler 이미지 보기](Portfolio/Validation/large-entity-scenario/README.md)

## AI coding agent와의 개발 방식

AI coding agent를 코드베이스 탐색, 대안 비교, 구현, 반복 검증과 문서화에 일상적인 개발 도구로 사용했습니다.

개발자는 목표와 제약, 최종 설계, 플레이 감각과 공개 범위를 판단했습니다. Agent는 관련 맥락을 탐색하고 실행 계획을 구체화하며 코드 구현과 자동 검증의 왕복을 가속했습니다. 반복해서 발견한 프로젝트 지식과 오류 패턴은 소유권, 업데이트 순서, 검증 규칙과 같은 재사용 가능한 프로젝트 계약으로 정리했습니다.

BroomSweep과 Stage Map Editor 사례에서 역할을 나눈 방식은 [AI coding agent와 함께 기능을 설계하고 검증한 방식](Portfolio/CaseStudies/ai-assisted-development.md)에 정리했습니다.

## 더 살펴보기

- [기술 포트폴리오 안내](Portfolio/README.md)
- [DOTS로 대량 엔티티의 생명주기를 설계한 과정](Portfolio/CaseStudies/large-entity-pipeline.md)
- [AI coding agent와 함께 기능을 설계하고 검증한 방식](Portfolio/CaseStudies/ai-assisted-development.md)
- [검증 자료 안내](Portfolio/Validation/README.md)
- [대량 엔티티 누적 시나리오 프로파일링 결과](Portfolio/Validation/large-entity-scenario/README.md)

## 프로젝트 범위

이 프로젝트는 Unity DOTS를 실제 게임플레이 문제에 적용한 기술 데모입니다. 상용 출시 수준의 콘텐츠 분량이나 최종 아트·사운드 품질, 모든 하드웨어의 성능 보장을 목표로 하지는 않았습니다.

현재 청소와 피격에 대한 시청각 피드백, 반복 플레이의 선택지와 장기적인 재미에는 개선할 부분이 있습니다. 공개 문서에서는 구현한 기술과 확인 가능한 결과를 중심으로 설명하고, 직접 측정하지 않은 비교 성능이나 출시 품질은 주장하지 않습니다.
