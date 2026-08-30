# 검증 자료 안내

`Sweep and Dodge`의 검증 자료는 프로젝트의 기술적 주장을 보조하고, 각 결과를 어디까지 해석할 수 있는지 설명하기 위해 제공합니다.

성능 측정 하나만으로 프로젝트 전체의 품질을 증명하려 하지 않았습니다. 실행 환경의 프로파일링, 자동화된 계약 검사와 직접 플레이 확인을 서로 다른 목적의 검증으로 구분했습니다.

## 어떤 자료를 확인할 수 있나요?

### [대량 엔티티 누적 시나리오 프로파일링 결과](large-entity-scenario/README.md)

최신 게임플레이 비주얼을 포함한 Windows standalone Development Build에서 Dust를 청소하지 않고 누적하는 시나리오를 동일 조건으로 3회 측정한 결과입니다.

다음 내용을 확인할 수 있습니다.

- 측정에 사용한 하드웨어와 빌드 설정
- 무입력·무청소 상태로 Dust를 누적한 통제 시나리오
- Active Entity 구성과 Frame Interval 집계
- Fixed Tick이 실행된 Frame의 CPU Timeline
- 같은 Frame의 Total·Dust·Hazard Counter
- 결과를 일반화하지 않기 위한 해석 범위

## 대량 엔티티 누적 시나리오 결과

공개 데모 콘텐츠의 시작 위치에서 입력과 청소를 수행하지 않고 Dust가 Spawn과 Lifetime Despawn의 균형에 도달하도록 누적했습니다. 동일 빌드와 조건에서 600 Frame씩 3회 기록했습니다.

| 항목 | 결과 |
|---|---:|
| Active Total 평균 | 24,148.3 |
| Active Total 범위 | 24,077–24,236 |
| Dust / Hazard 평균 | 24,106.0 / 42.2 |
| Frame Interval median / p95 / max | 7.291 / 9.249 / 12.872ms |
| 16.67ms 초과 Interval | 0 / 1,797 |

약 2.4만 개의 Active Entity는 일반 플레이에서 항상 유지되는 개체 수가 아니라, Dust를 청소하지 않고 누적한 통제 시나리오의 Plateau입니다. 정확한 측정 조건과 나머지 지표는 [상세 결과](large-entity-scenario/README.md)를 기준으로 합니다.

## 자동 검증은 무엇을 확인했나요?

EditMode 계약 테스트와 PlayMode Smoke는 다음과 같이 자동으로 관찰할 수 있는 일부 동작의 회귀를 찾는 보조 수단으로 사용했습니다.

- 시스템의 배치와 Update Order
- 생명주기 요청과 우선순위 동작
- Authoring 데이터에서 Runtime 데이터로 이어지는 변환
- Scene·GameObject Bridge와 ECS Runtime의 통합 동작

자동 테스트는 플레이 감각, 재미, 시청각 완성도나 모든 성능 상황을 보장하지 않습니다. 테스트 전략 자체보다, 설계 결정을 후속 작업에서 다시 확인하는 2차 Guardrail로 활용한 점에 의미를 두었습니다.

## 직접 플레이하며 확인한 범위

자동화만으로 판단하기 어려운 다음 항목은 실제 빌드와 Editor에서 직접 확인했습니다.

- Title부터 Demo Complete까지 이어지는 3개 Stage의 흐름
- 탄환 회피, Dust 청소·수집과 Deposit 복귀
- BroomSweep의 방향 교대와 이동·방향 제약
- HUD와 상호작용 Feedback의 가독성
- Stage Map Editor의 편집·Validation·Preview 흐름

직접 플레이한 결과는 기능이 실제 사용자 흐름 안에서 연결되는지 확인하는 Smoke 성격의 근거입니다. 모든 콘텐츠와 플랫폼의 장기 품질을 보장하는 결과로 확대하지 않습니다.

## 공개 자료와 원본 자료의 경계

저장소에는 공개 주장을 확인하는 데 필요한 측정 조건, 집계 결과와 판독 가능한 Profiler 이미지를 보존합니다.

Raw Profiler Data, Run별 Frame CSV, Player·Build Log와 내부 Manifest는 재분석을 위한 로컬 자료로 유지합니다. 이 원본 자료는 포트폴리오 독자가 결과를 이해하는 데 필수적이지 않으며 기본 공개 자료에는 포함하지 않습니다.

## 결과를 읽을 때의 범위

- 대량 엔티티 누적 결과는 명시한 장비와 통제 시나리오의 Development Build 측정입니다.
- Uncapped Render Frame에는 Fixed Tick이 실행된 Frame과 실행되지 않은 Frame이 함께 포함됩니다.
- 전체 Frame의 Median을 ECS Fixed Tick 하나의 실행 비용으로 표현하지 않습니다.
- 이 결과를 최종 Release Build, 모든 하드웨어의 60fps 또는 GameObject 방식 대비 우위로 일반화하지 않습니다.
- 자동 테스트와 수동 Smoke는 관찰한 계약과 실행 흐름만을 확인하며, 출시 품질 인증을 의미하지 않습니다.
