# Portfolio Demo Build Guide

> 데모 빌드에서 확인할 수 있는 내용과 함께 참고하면 좋은 기술 문서 소개

## Metadata
- doc_id: `PORT-003`
- type: `Portfolio`
- status: `draft`
- last_updated: `2026-08-22`
- related_docs:
  - [../../README.md](../../README.md)
  - [PORT-001-dots-large-entity-pipeline-case-study.md](PORT-001-dots-large-entity-pipeline-case-study.md)
  - [PORT-002-ai-assisted-engineering-workflow.md](PORT-002-ai-assisted-engineering-workflow.md)
  - [../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)
  - [../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)
  - [../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)
  - [../ProjectOps/OPS-003-public-release-readiness-plan.md](../ProjectOps/OPS-003-public-release-readiness-plan.md)

## 1. 데모 소개

`Sweep and Dodge`의 포트폴리오 데모 빌드는 Unity DOTS/Entities 기반 대량 엔티티 처리와 회피/청소/수집 루프를 짧게 확인하기 위한 기술 데모다.

이 문서는 데모 빌드에서 볼 수 있는 것, 영상/GIF와 기술 문서가 보완하는 정보, 개발 중 확보한 성과 수치를 함께 소개한다.

## 2. 데모 빌드에서 확인할 것

데모 빌드는 완성 게임 출시 후보가 아니라, 대량 Entity 처리 구조와 플레이 가능한 핵심 루프를 보여주는 포트폴리오 자료다.

| Area | What to look for |
|---|---|
| Core loop | 플레이어 이동, 위험 요소 회피, 청소/수집, 제거 요청과 디스폰 흐름 |
| DOTS pipeline | Spawn, simulation, request, despawn 단계가 분리되어 동작하는 구조 |
| Large entity handling | 많은 개체가 생성되고 이동하며 회수되는 상황에서의 프레임 안정성 |
| Feedback | HUD, 위험 피드백, 청소/수집 반응이 최소한의 플레이 판단을 돕는지 여부 |
| Supporting material | 영상/GIF, 개발 중 테스트 스냅샷, 기술 사례 문서 |

## 3. 참고자료 안내

데모 빌드는 짧은 실행 경험을 제공하고, 문서는 그 장면 뒤의 설계 의도를 설명한다.

- README: 프로젝트의 한 줄 요약, 현재 상태, 주요 문서 링크
- 영상/GIF: 실제 플레이, 대량 개체 장면, 청소/수집 반응
- 데모 빌드: 핵심 루프와 화면 피드백을 직접 확인하는 실행 자료
- `PORT-001`: ECS/DOTS 선택 배경, 대량 엔티티 파이프라인, ownership 설계
- `PORT-002`: AI coding agent를 설계, 코드 생성, 테스트, 문서화에 사용한 방식

## 4. 개발 중 성과 수치

### 4.1 역사적 자동 테스트 snapshot

기존 `OPS-001`에는 개발 중 확보한 자동 테스트와 PlayMode smoke 기록이 남아 있다. 이 값들은 공개용 데모 빌드의 최종 벤치마크가 아니라, DOTS 파이프라인이 실제 테스트 환경에서 대량 entity 흐름을 처리했다는 development snapshot이다.

Editor 자동 테스트에서는 spawn/despawn backlog와 drop/expire 지표를 관찰했다. 기록된 값은 `maxBudgetUsed=5000`, `maxPending=5000`, `maxOldestAge=0`, `dropCount=0`, `expiredByAge=0`이다. 이 수치는 스폰 요청과 디스폰 처리 흐름이 테스트 시나리오 안에서 drop/expire 없이 처리되었음을 보여준다.

PlayMode smoke에서는 전용 씬과 운영 씬에서 약 2.5만 active entity 규모의 장면을 기록했다. 전용 씬은 `maxActiveBullets=25467`, 운영 씬은 `maxActiveBullets=25514`를 기록했다. 이 값은 기존 코드/테스트 명칭의 `Bullet` 카운터를 인용한 것이며, 포트폴리오 문맥에서는 위험 요소와 수집/청소 대상을 포함한 대량 엔티티 처리 스냅샷으로 해석한다.

최종 공개 빌드 기준 수치는 이 development snapshot과 구분해서 제시한다.

### 4.2 2026-08-22 Stage 2 Editor profiling

Stage 2의 최신 대량 entity 시나리오는 별도 stress preset이 아니라 공개 데모와 같은 SampleScene 콘텐츠에서 재현했다. Title에서 Lobby를 거쳐 Stage 2로 진입하고 시작 대화를 건너뛴 뒤, 플레이어 입력과 청소를 수행하지 않아 Dust가 lifetime equilibrium까지 자연스럽게 누적되도록 했다.

측정 환경과 절차는 다음과 같다.

| Item | Value |
|---|---|
| Unity | 6000.3.6f1, Windows Editor |
| OS | Windows 10 x64 |
| CPU | Intel Core i5-10400F |
| GPU | NVIDIA GeForce GTX 1650 SUPER |
| Memory | 약 16GB |
| Scene / Stage | `SampleScene` / Stage 2 |
| Input | 시작 대화 skip 후 무입력·무청소 |
| Timing | warm-up 6초 후 15초 기록 |
| Repetition | 버전별 3회, run당 600 frame |
| Profiler | Deep Profile 및 allocation callstack 비활성 |

병목 조사와 단순화 비교에서는 다음 세 버전을 측정했다. A와 B는 상태 초기화 Job을 유지해 제거 API만 비교했고, C는 iterator 제거를 유지하면서 상태 초기화를 직렬 Owner 경로로 복원한 최종 버전이다.

| 구분 | Frame median | Frame p95 | Spawn median |
|---|---:|---:|---:|
| A: 기존 제거 API + 병렬 초기화 | 38.358ms | 42.267ms | 13.391ms |
| B: iterator 제거 + 병렬 초기화 | 26.951ms | 30.493ms | 2.714ms |
| C: iterator 제거 + 직렬 초기화 | 27.441ms | 31.448ms | 2.848ms |

최종 C는 A 대비 Frame median 28.46%, Frame p95 25.60%, `SpawnRequestRoundRobinExecutionSystem` median 78.73% 감소했다. B와 비교한 C의 증가는 Frame median 1.82%, p95 3.13%, Spawn median `0.134ms(4.94%)`로 사전에 정한 단순화 범위 안이었다. 따라서 최종 구현은 초기화 Job을 유지하지 않고 iterator 제거와 직렬 상태 초기화를 사용한다.

최종 C의 run별 plateau 결과는 다음과 같다.

| Run | Frame median | Frame p95 | Spawn median | Active average | Active range | Drop / Expire | Spawn 하위 `GC.Alloc` 표본 |
|---|---:|---:|---:|---:|---:|---:|---:|
| C1 | 27.441ms | 32.088ms | 2.832ms | 24,158.6 | 24,085–24,205 | 0 / 0 | 0 |
| C2 | 27.358ms | 31.448ms | 2.848ms | 24,161.4 | 24,088–24,206 | 0 / 0 | 0 |
| C3 | 27.442ms | 31.344ms | 2.852ms | 24,159.1 | 24,084–24,208 | 0 / 0 | 0 |

여기서 약 2.5만 active entity는 일반 플레이의 상시 밀도가 아니라, Stage 2 초기 위치에서 청소하지 않고 Dust를 누적한 통제 시나리오의 plateau다. 기존 코드 명칭의 active Bullet 카운터는 위험 요소와 청소/수집 대상을 함께 포함한다.

이 결과에는 Windows Editor와 Profiler 기록 오버헤드가 포함된다. standalone 또는 공개 빌드의 FPS, 60fps 보장, GameObject 방식 대비 성능 우위를 의미하지 않는다. 공개 빌드 기준 수치와 Dust/Hazard 구성 비율은 T3 후속 측정에서 별도로 확보한다.

## 5. 기술 데모로서의 범위

이 빌드는 출시 후보 빌드가 아니라 포트폴리오 기술 데모다. 따라서 다음 항목은 이 문서의 설명 범위에 포함하지 않는다.

- 스토어용 최종 앱 메타데이터 확정
- 스토어 배포 준비
- 최종 아트/사운드 품질
- 전체 출시형 콘텐츠 분량
- 모든 옵션/입력 장치 지원
- 플랫폼별 장기 벤치마크

이 문서에서 강조하는 것은 게임의 최종 완성도가 아니라, ECS/DOTS 학습 목표를 실제 게임플레이 문제로 연결하고, 대량 엔티티 처리 구조와 검증 근거를 포트폴리오 자료로 설명하는 방식이다.
