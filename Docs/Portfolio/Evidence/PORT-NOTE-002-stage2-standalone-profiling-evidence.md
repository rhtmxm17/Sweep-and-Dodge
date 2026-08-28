# Stage 2 Standalone Profiling Evidence Notes

> Stage 2 대량 엔티티 시나리오의 standalone profiling 원시 근거와 공개 해석 경계를 보존하는 내부 원천 노트

## Metadata
- doc_id: `PORT-NOTE-002`
- type: `PortfolioSourceNote`
- status: `working`
- audience: `internal`
- last_updated: `2026-08-28`
- related_docs:
  - [../../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md](../../TaskBoard/SESSION-20260814-01-portfolio-packaging-and-notion-board.md)
  - [../PORT-003-validation-report.md](../PORT-003-validation-report.md)
  - [PORT-NOTE-001-developer-perspective-and-claim-evidence.md](PORT-NOTE-001-developer-perspective-and-claim-evidence.md)
  - [../../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md](../../ADR/ADR-20260822-01-free-by-key-iterator-dequeue-and-spawn-initialization-simplification.md)

## 1. 문서 목적과 범위

이 문서는 공개 포트폴리오 문장이 아니다. Stage 2 standalone profiling에서 사용한 캡처, 분석 방식, 유효한 해석과 주장 제한을 후속 T3 작업에서 재사용하기 위한 원천 노트다.

측정 시나리오는 Editor에서 active entity 평균 약 2.4만을 직접 집계한 것과 같은 Stage 2 콘텐츠를 사용한다. 시작 대화를 건너뛴 뒤 초기 위치에서 입력과 청소를 수행하지 않고, Dust가 기존 spawn pipeline을 통해 lifetime equilibrium까지 누적되도록 했다.

2026-08-24 캡처에는 전체 active entity 수와 Dust/Hazard 구성 비율을 직접 읽을 수 있는 카운터가 보존되지 않았다. 이 한계는 Total/Dust/Hazard Profiler counter를 추가한 뒤, 최신 게임플레이 HUD와 Stage Cell 비주얼을 포함한 2026-08-25 standalone Development Build를 같은 조건으로 3회 재측정해 해소했다. 따라서 2026-08-24 결과는 Deep Profile 오버헤드와 임시 60fps cap의 진단 근거로, 2026-08-25 결과는 최신 비주얼 빌드의 active 구성과 uncapped frame 분포를 같은 캡처에서 확인한 근거로 분리한다.

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

2026-08-24 측정은 해상도·품질·세부 빌드 옵션이 완전한 manifest로 남지 않았다. 2026-08-25 재측정은 `ProfilerCaptures/Stage2-Composition-20260825-manifest.md`에 1024×768 windowed 실행, Windows x64 IL2CPP, Development Build, Deep Profile Support Off, LZ4, 단일 `SampleScene`과 build provenance를 고정했다. 품질 설정은 실행 시 별도 변경 없이 빌드 기본값을 사용했다. 내부 manifest의 checksum은 로컬 provenance로만 유지하며 공개 포트폴리오에는 기록하지 않는다.

### 2.2 Profiler 캡처

원시 캡처는 ignore된 로컬 `ProfilerCaptures/`에 있다.

| Capture | Deep Profile Support | Frame policy | Size | 판정 |
|---|---|---|---:|---|
| `Sweep and Dodge_2026-08-23_19-29-22.data` | On | 기존 설정 | 1,115,725,516 bytes | 계측 오버헤드가 큰 무효 성능 근거 |
| `Sweep and Dodge_2026-08-24_10-00-09.data` | Off | uncapped | 93,885,924 bytes | Tick/비-Tick 프레임 분리 관찰용 |
| `Sweep and Dodge_2026-08-24_11-19-35_fps cap 60.data` | Off | 임시 60fps cap | 97,799,284 bytes | 60fps frame budget 보조 근거 |
| `Stage2-Composition-20260825-Run01.data` | Off | uncapped | 123,971,376 bytes | 최신 비주얼·active 구성 동시 측정 |
| `Stage2-Composition-20260825-Run02.data` | Off | uncapped | 124,380,376 bytes | 동일 조건 반복 측정 |
| `Stage2-Composition-20260825-Run03.data` | Off | uncapped | 123,713,856 bytes | 동일 조건 반복 측정 |

같은 이름의 PNG는 Profiler가 저장한 작은 미리보기이므로 공개용 차트나 판독 가능한 캡처로 사용하지 않는다. 원시 `.data` 파일은 로컬 개발 근거로 유지하고 기본 공개 패키지에는 포함하지 않는다. 공개에는 `Evidence/Stage2-Profiling/`의 판독 가능한 CPU Timeline과 counter 이미지를 사용한다.

각 2026-08-25 raw capture에서 frame CSV를 생성했으며, 실행별 수치와 3-run 합산은 `ProfilerCaptures/Stage2-Composition-20260825-3Run-summary.md`에 보존한다. 편집 전 연속 시각 근거는 `ProfilerCaptures/Stage2-Composition-full.mp4`에 보존하며 1024×768, `25.784533초`다. 사용자가 Stage 2 누적 과정과 15초 이상 plateau 유지가 포함되도록 측정 절차에 따라 확보했다. 공개에는 초반 Stage 선택 구간을 덜어낸 약 22초 `Stage2-Composition.mp4`를 사용한다. 영상은 연속 유지와 HUD 가독성의 시각 근거이며 frame-time 통계의 원천은 아니다.

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

이 cap은 ECS Tick과 GameObject Update의 비용을 보수적으로 함께 관찰하기 위한 측정 조건이지, 실제 데모의 영구 운영 정책이 아니다. 측정 후 임시 코드는 원복했고 uncapped 기본 정책을 유지한다. 또한 현재 fixed-step accumulator는 render frame당 최대 한 Tick을 소비하므로, render와 simulation이 완전히 독립적으로 실행된다고 표현하지 않는다.

### 4.4 2026-08-25 최신 비주얼 빌드·uncapped 3회 반복

게임플레이 HUD와 Stage Cell 비주얼 변경 이후 새 Windows x64 IL2CPP Development Build를 고정하고, 같은 Stage 2 초기 위치·무입력·무청소 plateau에서 600 frame을 3회 기록했다. 세 캡처 모두 Total/Dust/Hazard counter와 frame/pipeline/spawn marker를 함께 포함한다.

| Metric | Run01 | Run02 | Run03 | 3-run 합산 |
|---|---:|---:|---:|---:|
| Frames | 600 | 600 | 600 | 1,800 |
| Frame interval median | 7.476ms | 7.396ms | 7.042ms | 7.291ms |
| Frame interval p95 / max | 9.371 / 12.638ms | 9.320 / 12.872ms | 8.974 / 11.508ms | 9.249 / 12.872ms |
| 16.67ms 초과 interval | 0 | 0 | 0 | 0 / 1,797 |
| Active Total mean | 24,153.2 | 24,151.9 | 24,139.6 | 24,148.3 |
| Active Total range | 24,110–24,236 | 24,099–24,221 | 24,077–24,194 | 24,077–24,236 |
| Dust mean | 24,107.0 | 24,119.2 | 24,091.9 | 24,106.0 |
| Hazard mean | 46.2 | 32.7 | 47.7 | 42.2 |
| Tick proxy frames | 270 | 266 | 259 | 795 |
| Pipeline median / p95 / max | 2.092 / 2.449 / 2.740ms | 2.049 / 2.381 / 2.703ms | 2.040 / 2.362 / 2.745ms | 2.058 / 2.408 / 2.745ms |
| Spawn median / p95 / max | 0.402 / 0.660 / 0.771ms | 0.390 / 0.510 / 0.644ms | 0.384 / 0.606 / 0.768ms | 0.392 / 0.618 / 0.771ms |

1,800개 모든 frame에서 `Dust + Hazard = Total`이 성립했고 `WaitForTargetFPS`는 0이었다. 실행 간 Total mean spread는 13.6 entity, frame median spread는 0.434ms, pipeline median spread는 0.052ms, spawn median spread는 0.018ms였다.

각 raw Profiler 캡처의 연속 구간은 약 4.3–4.5초이므로 세 캡처를 합쳐 하나의 15초 연속 증거로 취급하지 않는다. 연속 plateau 유지와 누적 과정은 편집 전 25.784533초 HUD 영상과 공개용 약 22초 편집본으로 보완한다.

## 5. 해석과 공개 주장 경계

### 사용할 수 있는 해석

- Deep Profile Support가 최초 standalone 캡처에 큰 계측 오버헤드를 만들었으며, 비활성화 재측정으로 이를 확인했다.
- uncapped 환경에서는 ECS fixed Tick이 있는 frame과 없는 frame의 비용 차이가 명확했다.
- 테스트 장비의 Stage 2 무입력·무청소 시나리오에서 임시 60fps cap을 적용한 600 frame은 median `16.670ms`, p95 `17.022ms`, max `17.402ms`였고 20ms 초과 frame은 없었다.
- 최종 spawn 실행 경로는 같은 cap 측정에서 median `0.421ms`, p95 `0.661ms`였다. 이 결과만으로 spawn 요청의 전면 병렬화를 포트폴리오 근거 확보를 위해 추가 추진할 필요는 없다.
- 최신 비주얼 Development Build의 uncapped 600-frame 측정 3회에서 active Total 평균은 실행별 `24,153.2 / 24,151.9 / 24,139.6`이었고, Total/Dust/Hazard 구성을 frame interval과 같은 캡처에 직접 기록했다.
- 세 실행을 합친 1,797개 frame interval에서 median `7.291ms`, p95 `9.249ms`, max `12.872ms`였고 16.67ms를 넘은 interval은 없었다. 다만 uncapped render frame에는 fixed Tick 실행 frame과 비실행 frame이 섞여 있으므로 이 분포를 ECS Tick 비용 하나로 축약하지 않는다.

### 사용하지 않을 주장

- 모든 하드웨어와 모든 플레이 상황에서 60fps를 보장한다.
- standalone에서 정확히 2.5만 active entity와 고정 60fps를 항상 동시에 유지한다.
- 현재 결과가 최종 공개 Release Build의 성능이다.
- GameObject 방식보다 정량적으로 우수하다.
- render/update와 ECS simulation이 완전히 독립된 60Hz loop로 동작한다.
- 한 번의 측정으로 포괄적인 성능 안정성이나 성능 엔지니어링 전문성을 증명했다.

### 공개 문장 초안

> 최신 게임플레이 비주얼을 포함한 Stage 2 standalone Development Build에서 무입력·무청소 plateau의 600 frame을 같은 조건으로 3회 기록했습니다. active Total 평균은 실행별 약 2.414만이었고 Total/Dust/Hazard 구성과 frame interval을 같은 캡처에 남겼습니다. 3회 합산 frame interval은 median 7.29ms, p95 9.25ms, max 12.87ms였으며 16.67ms 초과 interval은 없었습니다. 이 수치는 명시한 테스트 장비와 통제 시나리오의 uncapped Development Build 결과이며, 최종 공개 빌드나 모든 플레이 상황의 60fps 보장을 의미하지 않습니다.

## 6. 공개 증거 형식 결정

- 공개 표는 동일 빌드·조건의 3-run 합산을 중심으로 제시하고 실행별 상세 수치는 이 원천 노트에 보존한다.
- 판독 가능한 정지 캡처는 `Evidence/Stage2-Profiling/`의 CPU Timeline과 Total/Dust/Hazard counter 이미지 두 장을 사용한다.
- CPU 이미지는 fixed Tick marker가 관찰된 대표 frame 299의 실행 구조 예시이며 통계 표본을 대표하는 단일 수치로 사용하지 않는다.
- counter 이미지는 같은 frame에서 Dust 약 24.14k, Hazard 64, Total 약 24.21k를 판독하는 보완 근거다.
- raw `.data`, run별 frame CSV, 로그, 내부 manifest와 편집 전 영상은 로컬 보존하며 기본 공개 패키지에는 포함하지 않는다.
- 공개 문서와 Notion에는 이미지·영상·CSV SHA-256을 기록하지 않는다.
- 한글 폰트 문제는 폰트 추가로 해결했고 사용하지 않는 Feel Asset Store 패키지와 설치 심볼은 제거했다.
- 이후 현재 상태 빌드와 가벼운 수동 플레이 smoke에서 직접 드러나는 오류는 발견되지 않았다. 폰트 추가와 미사용 패키지 제거는 측정한 ECS runtime 경로·Stage 2 콘텐츠·품질 설정을 바꾸지 않으므로 추가 profiling은 수행하지 않는다. 이 조건이 달라지면 재측정 필요성을 다시 판단한다.
