# Portfolio Demo and Validation Guide

> 데모에서 확인할 내용, 최신 Stage 2 측정 결과, 공개 자료의 해석 범위를 안내하는 문서

## Metadata
- doc_id: `PORT-003`
- type: `Portfolio`
- status: `draft`
- last_updated: `2026-08-29`
- related_docs:
  - [README](../../README.md)
  - [DOTS Large-Entity Pipeline Case Study](PORT-001-dots-large-entity-pipeline-case-study.md)
  - [AI-assisted Engineering Workflow](PORT-002-ai-assisted-engineering-workflow.md)
  - [Stage 2 Standalone Profiling Evidence](Evidence/Stage2-Profiling/README.md)
  - [Portfolio Evidence Index](Evidence/INDEX.md)

## 1. 데모의 목적

`Sweep and Dodge`는 Unity DOTS/Entities 기반 대량 엔티티 파이프라인을 회피·청소·수집 플레이 안에서 확인하기 위한 플레이 가능한 기술 데모다.

출시 후보 게임의 콘텐츠 분량이나 최종 시청각 품질을 보여주는 것이 목적은 아니다. 기술을 고립된 stress scene으로 제시하는 대신, 외부 사용자가 Title에서 시작해 Stage를 플레이하고 Result와 Demo Complete까지 도달할 수 있는 실행 흐름 안에 배치했다.

## 2. 데모에서 확인할 내용

| Area | What to look for |
|---|---|
| Demo flow | `Title → Lobby → Stage → Result → Demo Complete` 흐름과 Retry/Next |
| Core loop | 위험 탄환 회피, Dust 청소·수집, Carry가 차면 Deposit으로 복귀, Source 고갈 |
| DOTS pipeline | spawn, simulation, request, lifecycle 실행이 분리된 fixed-tick 구조 |
| Large entity handling | 많은 Dust와 Hazard가 생성·이동·회수되는 데이터 흐름 |
| Feedback | HUD, 피격, 청소, Carry/Deposit 상태가 플레이 판단을 지원하는 범위 |
| Supporting material | 기술 사례 문서, Stage 2 측정 표, Profiler 이미지와 후속 영상 |

대량 엔티티의 설계 배경과 owner/fence/enableable 규칙은 [PORT-001](PORT-001-dots-large-entity-pipeline-case-study.md)에서 설명한다.

## 3. 최신 Stage 2 통제 시나리오

최신 공개 성능 근거는 공개 데모와 같은 Stage 2 콘텐츠를 사용한다. 시작 대화를 건너뛴 뒤 초기 위치에서 입력과 청소를 수행하지 않고, Dust가 spawn과 lifetime despawn의 균형에 도달하도록 자연스럽게 누적한다.

약 2.4만 active entity는 일반 플레이의 상시 밀도가 아니라 이 무입력·무청소 plateau에서 관찰한 값이다.

### 측정 조건

| Item | Value |
|---|---|
| Unity | 6000.3.6f1 |
| OS | Windows 10 x64 |
| CPU / GPU | Intel Core i5-10400F / NVIDIA GeForce GTX 1650 SUPER |
| Memory | 약 16GB |
| Build | Windows x64, IL2CPP, Development Build, Deep Profile Support Off |
| Display | 1024×768 windowed, VSync Off, uncapped |
| Scene / Stage | `SampleScene` / Stage 2 |
| Scenario | 시작 대화 skip 후 초기 위치, 무입력·무청소 plateau |
| Repetition | 동일 빌드·조건에서 600 frame × 3회 |

### 3-run 합산 결과

| Metric | Result |
|---|---:|
| Active Total mean | 24,148.3 |
| Active Total range | 24,077–24,236 |
| Dust / Hazard mean | 24,106.0 / 42.2 |
| Frame interval median / p95 / max | 7.291 / 9.249 / 12.872ms |
| 16.67ms 초과 interval | 0 / 1,797 |
| Tick-proxy pipeline median / p95 / max | 2.058 / 2.408 / 2.745ms |
| Spawn median / p95 / max | 0.392 / 0.618 / 0.771ms |
| `Dust + Hazard = Total` 위반 | 0 / 1,800 frame |

## 4. 결과를 읽는 범위

- 이 결과는 명시한 장비와 Stage 2 통제 시나리오의 Development Build 측정이다.
- uncapped render frame에는 fixed Tick이 실행된 frame과 실행되지 않은 frame이 섞여 있다.
- 전체 frame median을 ECS fixed Tick 하나의 비용으로 표현하지 않는다.
- 모든 하드웨어와 플레이 상황의 60fps, 최종 Release Build 성능, GameObject 방식 대비 우위를 주장하지 않는다.
- 기존 코드의 `Bullet` active 명칭은 위험 요소와 청소·수집 대상을 함께 포함한다. 최신 근거에서는 Total을 Dust와 Hazard로 나눠 기록했다.

과거 자동 테스트와 PlayMode smoke의 약 2.5만 기록은 개발 중 snapshot으로 남아 있지만, 현재 측정의 성능 수치나 회귀 보장으로 사용하지 않는다.

## 5. 공개 Evidence

[Stage 2 Standalone Profiling Evidence](Evidence/Stage2-Profiling/README.md)는 다음 자료의 공개 기준이다.

- 동일 조건 3-run 합산 표
- fixed Tick이 관찰된 대표 frame 299의 CPU Timeline
- 같은 frame의 Total/Dust/Hazard counter 이미지
- 측정 조건과 해석 한계

단일 Profiler 이미지는 pipeline 구조와 entity 구성의 예시다. 전체 분포의 통계 원천은 1,800 frame 집계표다.

active 누적과 15초 이상 plateau 유지를 보여주는 약 22초 HUD 영상도 확보되어 있으나, 최종 공개 호스팅 링크는 아직 연결하지 않았다. raw Profiler `.data`, run별 CSV, 로그, 내부 manifest와 편집 전 영상은 로컬 재분석 근거로 유지하며 기본 공개 패키지에는 포함하지 않는다.

## 6. 자동 검증의 위치

EditMode 계약 테스트와 PlayMode smoke는 화면 동작만 확인하는 대신 다음과 같이 자동 관찰 가능한 일부 계약의 회귀를 찾는 보조 수단으로 사용한다.

- 시스템 배치와 update order
- lifecycle request와 priority 동작
- authoring 데이터와 runtime 변환
- Scene·GameObject bridge·ECS runtime 통합

이 테스트가 플레이 감각, 재미, 시청각 완성도 또는 모든 성능 상황을 보장하지는 않는다. 테스트 자동화 자체도 이 프로젝트의 독립적인 핵심 전문성 주장으로 사용하지 않는다.

## 7. 공개 빌드와 영상 상태

Windows x64 공개 압축 패키지와 최종 실행 안내는 후속 작업에서 준비한다. 패키지는 다음 구성을 기준으로 한다.

- 실행 파일과 Unity 데이터 폴더
- 기본 조작법과 실행 안내
- 알려진 제한
- debug HUD 기본 비활성
- uncapped frame policy
- raw Profiler 자료 제외

대표 플레이, BroomSweep, Stage Map Editor 보조 영상은 Notion 프로젝트 페이지의 레이아웃과 설명 흐름에 맞춰 별도 촬영한다. 현재 문서에는 존재하지 않는 다운로드나 영상 URL을 임시 링크로 만들지 않는다.

## 8. 기술 데모의 현재 한계

- 청소·피격·위험 탄환 제거의 시청각 피드백이 제한적이다.
- 청소와 Deposit을 반복하는 구조의 장기적인 재미와 선택지는 범위가 작다.
- 최종 공개 Windows x64 압축 패키지와 전달 smoke가 아직 남아 있다.
- 최신 수치는 Development Build의 통제 시나리오이며 최종 공개 후보 Release Build 측정이 아니다.
- 스토어 배포, 모든 입력 장치 지원, 플랫폼별 장기 벤치마크는 범위에 포함하지 않는다.

이 문서의 역할은 기술 데모를 출시 품질 보증서처럼 제시하는 것이 아니라, 무엇을 직접 확인할 수 있고 각 검증 자료를 어디까지 해석할 수 있는지 명확히 안내하는 것이다.
