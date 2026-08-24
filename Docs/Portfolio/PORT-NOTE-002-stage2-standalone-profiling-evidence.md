# Stage 2 Standalone Profiling Evidence Notes

> Stage 2 대량 엔티티 시나리오의 standalone profiling 원시 근거와 공개 해석 경계를 보존하는 내부 원천 노트

## Metadata
- doc_id: `PORT-NOTE-002`
- type: `PortfolioSourceNote`
- status: `working`
- audience: `internal`
- last_updated: `2026-08-24`
- related_docs:
  - [../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md](../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md)
  - [PORT-003-validation-report.md](PORT-003-validation-report.md)
  - [PORT-NOTE-001-developer-perspective-and-claim-evidence.md](PORT-NOTE-001-developer-perspective-and-claim-evidence.md)
  - [../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md](../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)

## 1. 문서 목적과 범위

이 문서는 공개 포트폴리오 문장이 아니다. Stage 2 standalone profiling에서 사용한 캡처, 분석 방식, 유효한 해석과 주장 제한을 후속 T3 작업에서 재사용하기 위한 원천 노트다.

측정 시나리오는 Editor에서 active entity 평균 약 2.4만을 직접 집계한 것과 같은 Stage 2 콘텐츠를 사용한다. 시작 대화를 건너뛴 뒤 초기 위치에서 입력과 청소를 수행하지 않고, Dust가 기존 spawn pipeline을 통해 lifetime equilibrium까지 누적되도록 했다.

다만 standalone Profiler 캡처에는 전체 active entity 수와 Dust/Hazard 구성 비율을 직접 읽을 수 있는 카운터가 보존되지 않았다. 따라서 Editor의 직접 집계 수치와 standalone frame time은 서로 대응하는 근거이지만, 아직 하나의 동시 측정 결과로 결합하지 않는다.

## 2. 측정 환경과 원시 자료

### 2.1 확인된 환경

| Item | Value |
|---|---|
| Unity | 6000.3.6f1 |
| Build | Windows standalone Development Build |
| OS | Windows 10 x64 |
| CPU | Intel Core i5-10400F |
| GPU | NVIDIA GeForce GTX 1650 SUPER |
| Memory | 약 16GB |
| Scene / Stage | `SampleScene` / Stage 2 |
| Input | 시작 대화 skip 후 무입력·무청소 |

해상도, 품질 설정, Development Build의 세부 옵션 목록은 아직 완전한 측정 기록으로 고정하지 못했다. 최종 공개 벤치마크 전 보완한다.

### 2.2 Profiler 캡처

원시 캡처는 저장소의 `ProfilerCaptures/`에 있다.

| Capture | Deep Profile Support | Frame policy | Size | 판정 |
|---|---|---|---:|---|
| `Sweep and Dodge_2026-08-23_19-29-22.data` | On | 기존 설정 | 1,115,725,516 bytes | 계측 오버헤드가 큰 무효 성능 근거 |
| `Sweep and Dodge_2026-08-24_10-00-09.data` | Off | uncapped | 93,885,924 bytes | Tick/비-Tick 프레임 분리 관찰용 |
| `Sweep and Dodge_2026-08-24_11-19-35_fps cap 60.data` | Off | 임시 60fps cap | 97,799,284 bytes | 60fps frame budget 보조 근거 |

같은 이름의 PNG는 Profiler가 저장한 작은 미리보기이므로 공개용 차트나 판독 가능한 캡처로 사용하지 않는다. 원시 `.data` 파일 역시 현재는 로컬 개발 근거이며, 공개 패키지에 그대로 포함할지는 T3에서 별도로 결정한다.

## 3. 분석 방법

- 각 캡처에서 연속된 Main Thread frame의 `frameStartTimeMS` 차이로 600개 frame interval을 계산했다. 로드된 캡처의 `frameTimeMs`가 0으로 반환되어 이 값을 직접 사용하지 않았다.
- 시스템 비용은 Profiler marker의 duration을 사용했다.
- uncapped 캡처에서 `BulletFramePipelineGroup` duration이 1ms를 초과한 frame을 ECS fixed Tick이 실행된 frame의 proxy로 분류했다. spawn marker의 분포와도 대조했다.
- 이 proxy는 현재 캡처를 해석하기 위한 관찰 기준이며, fixed-step accumulator의 `HasStep`을 직접 기록한 런타임 카운터가 아니다.
- 평균 FPS 하나로 결과를 요약하지 않는다. Tick이 있는 frame과 없는 frame의 분포가 다르므로 median, p95, p99, max와 Tick frame 비율을 함께 본다.

## 4. 캡처별 결과

### 4.1 Deep Profile Support 활성 캡처

| Metric | Result |
|---|---:|
| Frames / duration | 600 / 10.113s |
| Frame interval median | 16.670ms |
| Frame interval p95 / p99 / max | 18.698 / 20.280 / 31.850ms |
| Profiler sample median | 약 85,429 samples/frame |

Deep Profile Support 비활성 캡처의 sample median이 약 1.3~1.4천인 것과 비교해 instrumentation이 크게 증가했다. 파일 크기와 frame interval 분포도 함께 비대해졌으므로 이 캡처는 실제 standalone 성능 근거에서 제외한다.

### 4.2 Deep Profile Support 비활성·uncapped 캡처

| Metric | Result |
|---|---:|
| Frames / duration | 600 / 3.371s |
| Frame interval median | 5.509ms |
| Frame interval p95 / p99 / max | 6.928 / 7.332 / 8.793ms |
| Tick proxy frames | 202 / 600 |
| Pipeline Tick-frame median / p95 / max | 2.121 / 2.388 / 2.852ms |
| Spawn Tick-frame median / p95 / max | 0.404 / 0.663 / 0.730ms |
| Profiler sample median | 약 1,285 samples/frame |

render/update frame보다 fixed Tick 주기가 느리므로 600개 중 약 3분의 1인 202개 frame에서만 대량 엔티티 pipeline이 실행됐다. 따라서 전체 frame 평균이나 약 178fps에 해당하는 uncapped median만으로 ECS Tick 비용 또는 60fps 안정성을 주장하지 않는다.

### 4.3 Deep Profile Support 비활성·임시 60fps cap 캡처

`Application.targetFrameRate = 60`을 측정용으로 일시 적용하여 render/update frame마다 fixed Tick이 실행되는 조건을 만들었다.

| Metric | Result |
|---|---:|
| Frames / duration | 600 / 10.026s |
| Frame interval min / median | 16.399 / 16.670ms |
| Frame interval p95 / p99 / max | 17.022 / 17.147 / 17.402ms |
| Frame interval mean | 16.738ms, 약 59.74fps |
| Frames over 20 / 25 / 33.33ms | 0 / 0 / 0 |
| Pipeline frames | 600 / 600 |
| Pipeline median / p95 / p99 / max | 2.220 / 2.602 / 2.814 / 3.242ms |
| Spawn median / p95 / p99 / max | 0.421 / 0.661 / 0.702 / 0.719ms |
| `WaitForTargetFPS` median / p95 / max | 9.116 / 9.654 / 10.068ms |
| Profiler sample median | 약 1,449 samples/frame |

600개 모든 frame에서 pipeline marker가 관찰됐고, 실제 작업 이후에는 `WaitForTargetFPS`가 frame budget의 상당 부분을 차지했다. 이 캡처는 해당 테스트 장비와 통제 시나리오에서 대량 엔티티 pipeline을 매 frame 실행해도 60fps frame budget 안에 들어왔다는 보조 근거로 사용한다.

이 cap은 ECS Tick과 GameObject Update의 비용을 보수적으로 함께 관찰하기 위한 측정 조건이지, 실제 데모의 영구 운영 정책이 아니다. 측정 후 코드는 원복해야 한다. 또한 현재 fixed-step accumulator는 render frame당 최대 한 Tick을 소비하므로, render와 simulation이 완전히 독립적으로 실행된다고 표현하지 않는다.

## 5. 해석과 공개 주장 경계

### 사용할 수 있는 해석

- Deep Profile Support가 최초 standalone 캡처에 큰 계측 오버헤드를 만들었으며, 비활성화 재측정으로 이를 확인했다.
- uncapped 환경에서는 ECS fixed Tick이 있는 frame과 없는 frame의 비용 차이가 명확했다.
- 테스트 장비의 Stage 2 무입력·무청소 시나리오에서 임시 60fps cap을 적용한 600 frame은 median `16.670ms`, p95 `17.022ms`, max `17.402ms`였고 20ms 초과 frame은 없었다.
- 최종 spawn 실행 경로는 같은 cap 측정에서 median `0.421ms`, p95 `0.661ms`였다. 이 결과만으로 spawn 요청의 전면 병렬화를 포트폴리오 근거 확보를 위해 추가 추진할 필요는 없다.

### 사용하지 않을 주장

- 모든 하드웨어와 모든 플레이 상황에서 60fps를 보장한다.
- standalone 캡처에서 active entity가 정확히 24,000대였다고 직접 측정했다.
- 현재 결과가 최종 공개 Release Build의 성능이다.
- GameObject 방식보다 정량적으로 우수하다.
- render/update와 ECS simulation이 완전히 독립된 60Hz loop로 동작한다.
- 한 번의 측정으로 포괄적인 성능 안정성이나 성능 엔지니어링 전문성을 증명했다.

### 공개 문장 초안

> Editor에서 Stage 2를 무입력·무청소 상태로 유지해 평균 약 2.4만 active entity plateau를 직접 확인했습니다. 같은 Stage 2 콘텐츠의 standalone Development Build에서는 Deep Profile Support를 끄고 임시 60fps cap을 적용한 600 frame을 측정했으며, frame interval median 16.67ms, p95 17.02ms, max 17.40ms를 기록했습니다. 두 결과는 각각 entity 규모와 frame budget을 확인한 대응 근거이며, 최종 공개 빌드의 보편적인 60fps 보장을 의미하지 않습니다.

## 6. 남은 증거 공백

- standalone 측정과 동시에 기록한 전체 active entity 수와 Dust/Hazard 구성 비율
- 해상도, 품질 설정, Development Build 옵션을 포함한 완전한 측정 manifest
- 동일 조건 반복 측정과 최종 공개 후보 빌드 smoke
- 임시 `Application.targetFrameRate = 60` 코드 원복 확인
- 공개용 표, 판독 가능한 Profiler 캡처, 누적 과정 영상의 최종 형식
- raw `.data` 파일을 공개 패키지에 포함할지, 요약 CSV와 캡처만 제공할지 결정
