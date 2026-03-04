# 런 진행도 디렉터 설계

## Metadata
- doc_id: `TD-006`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-03-04`
- related_docs:
  - [OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)
  - [GD-007-data-driven-bullet-pattern-definition.md](../GameDesign/GD-007-data-driven-bullet-pattern-definition.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-005-spawn-directive-settings-reference.md](./TD-005-spawn-directive-settings-reference.md)

> 복수 Source 스테이지에서 `Source 진행도` 중심으로 패턴 선택을 지휘하는 `런 진행도 디렉터`의 책임 경계와 연동 계약을 정의한다.

## 1. 합의된 전제
- 기능 명칭은 `런 진행도 디렉터`로 고정한다.
- 한 스테이지는 `1~3개 Source`를 전제로 운영한다.
- 모든 Source는 항상 작동하거나, 최소한 플레이어에게 그렇게 보이도록 설계한다.
- 플레이 구간 진행 기준은 시간축 단독이 아니라 `Source별 진행도`를 우선으로 한다.
- `OPS-001` 기준(스테이지 2~3분, 캠페인 무실패 클리어 15~20분, 재도전 미포함)을 가드레일로 유지한다.
- `WaveClipSO`는 현재 스키마를 유지한다.
- `BulletSource`는 외부 요청을 받아 출력하는 구조로 확장한다.
- 디렉터는 각 Source에 `단일 활성 클립`을 할당하고, Source는 할당된 클립을 재생한다.

## 2. 역할
- 현재 플레이 구간 상태를 감지한다.
- 상태 전환 조건을 감지한다.
- 특정 이벤트 발생을 감지한다.
- 감지 결과에 따라 실행할 탄막 패턴을 선택한다.
- Source를 켜고 끄기보다, Source별 출력 비중과 체감 톤을 조정한다.

## 3. 책임
- `Source 진행도/상태/이벤트`를 해석해 패턴 선택 결정을 만든다.
- 선택 결과를 실행 단계가 소비할 수 있는 요청 형태로 전달한다.
- 시간 목표는 진행 기준이 아니라 가드레일로만 사용한다.
- 복수 Source가 동시에 살아 있는 상태에서, 어떤 Source를 얼마나 강하게 전면에 내세울지 조정한다.
- Source별로 재생할 `단일 활성 클립`을 결정한다.

## 4. 비책임
- 탄환 스폰/디스폰 실행.
- 풀/CellMap/렌더 토글 처리.
- 탄환 시뮬레이션/충돌/수명 처리.
- `WaveClipSO` 데이터 모델 변경.
- Source 내부의 클립 선택 결정.

## 5. 네이밍 가드 (합의 범위)
- Source 고갈 상태 용어(`SourceState`)와 런 진행 상태 용어는 분리한다.
- 탄막 라인 용어는 기존 `Lane`을 유지한다.
- 본 문서에서 확정하지 않은 신규 타입/버퍼/시스템명은 후속 합의 전까지 사용하지 않는다.

## 6. 디렉터 2계층 상태 모델
### 6.1 기본 원칙
- 상태 모델은 "어떤 Source를 끄는가"가 아니라 "모든 Source가 살아 있는 상태에서 무엇을 전면에 내세우는가"를 표현해야 한다.
- 상태 전환은 Source 활성/비활성 전환보다 출력 밀도, 위험도, 이벤트 빈도, 패턴 강조점 변화에 가깝다.
- 디렉터 상태는 `SourceState`의 별칭이 아니라, "현재 무엇을 전면에 내세우는가"를 표현하는 별도 축이다.

### 6.2 상태 축
- `1계층: Stage 상태`
  - `Idle`
  - `Running`
  - `ClearReady`
  - `Completed`
- `2계층: Source별 Director 상태`
  - `Baseline`
  - `Pressure`
  - `Finish`

### 6.3 1계층: Stage 상태 의미
- `Idle`
  - 스테이지 시작 전 상태.
- `Running`
  - 스테이지 진행 중 상태.
  - 실제 패턴 선택/강조 조정은 이 상태에서만 수행한다.
- `ClearReady`
  - 클리어 조건이 충족되어 종료 처리를 기다리는 상태.
- `Completed`
  - 스테이지 종료 상태.

#### 1계층 전이 계약 (이벤트 + 게이트)
- `Idle -> Running`
  - 이벤트: `StageStartRequested`
  - 게이트:
    - `MinIdleDurationElapsed == true`
    - `IntroPresentationDone == true`
  - 전이식: `StageStartRequested && MinIdleDurationElapsed && IntroPresentationDone`
- `ClearReady -> Completed`
  - 이벤트: `ConfirmPressed` 또는 `AutoAdvanceTimeoutElapsed`
  - 게이트:
    - `ClearPresentationDone == true`
  - 전이식: `(ConfirmPressed || AutoAdvanceTimeoutElapsed) && ClearPresentationDone`

#### 게이트 갱신 주체 (초안)
- `StageStartRequested`: 상위 `StageFlow`
- `MinIdleDurationElapsed`: 런 진행도 디렉터 내부 타이머 시스템
- `IntroPresentationDone`: 진입 연출/UI 시스템(브리지 통해 ECS 반영)
- `ConfirmPressed`: 리절트 UI 입력 시스템(브리지 통해 ECS 반영)
- `AutoAdvanceTimeoutElapsed`: 런 진행도 디렉터 내부 타이머 시스템
- `ClearPresentationDone`: 클리어 연출/UI 시스템(브리지 통해 ECS 반영)

#### 비고: 상위 StageFlow 가정
- 본 상태 모델은 상위 `StageFlow`가 씬/맵/Source 초기 준비를 완료한 뒤, 준비 완료 이벤트를 통해 런 진행도 디렉터 상태머신을 시작한다는 전제를 둔다.
- `StageReady`는 디렉터 전이식의 개별 게이트가 아니라 상위 `StageFlow`의 준비 보장 조건으로 본다.
  - 내부 체크 예시: `PlayerReady`, `DirectorConfigReady`, `SourceBindReady`
- 런 진행도 디렉터는 스테이지 내부 상태(`Idle -> Running -> ClearReady -> Completed`)만 책임진다.
- 씬 전환, 스테이지 출입, 다음 스테이지 로딩과 같은 상위 라이프사이클 처리는 `StageFlow` 책임으로 분리한다.
- `Completed` 이후 전환은 디렉터가 직접 수행하지 않고, 완료 이벤트를 상위 `StageFlow`에 전달해 후속 흐름을 진행한다.

#### 월드 바인딩 시점 비교 (GO 브리지)
- `Awake` 고정 바인딩
  - 장점: 구현 단순
  - 영향: `DefaultGameObjectInjectionWorld`/SubScene 준비 지연 시 초기 바인딩 실패 가능성이 높다.
- `Start` 고정 바인딩
  - 장점: `Awake`보다 늦어 초기화 성공률이 다소 높다.
  - 영향: 여전히 월드/싱글톤 생성 타이밍과 경합할 수 있다.
- `OnEnable 1차 시도 + Update 재시도` (권장)
  - 장점: 활성화 직후 빠른 연결을 시도하면서, 준비 지연 프레임에서도 자동 복구 가능하다.
  - 영향: 바인딩 재시도 분기가 필요하지만 런타임 안정성이 가장 높다.

#### 미연동 기본 모드 정책
- `StageFlow/UI`가 연결되지 않은 테스트/레거시 환경 호환을 위해 기본 `InitialStageState`는 `Running`으로 둔다.
- 엄격 모드가 필요하면 `InitialStageState = Idle`로 설정하고, 브리지를 통해 `StageStartRequested`를 주입해 전이한다.
- Demo Shell 연동(S1) 이후 임시 호출 주체는 제거되었고, `Title/Lobby/Result/DemoComplete` 전이는 Shell Owner가 담당한다.

#### GO 브리지 런타임 계약 (`RunDirectorStageBridge`)
- 브리지 1개가 `GO -> ECS`(요청/게이트 반영)와 `ECS -> GO`(완료 신호 발행)를 모두 담당한다.
- 업데이트 타이밍은 `Update` 단일 루프로 통일한다.
- 씬당 활성 브리지는 1개만 허용한다.
  - 중복 브리지는 소유권 획득에 실패하며, 요청/반영은 `no-op` 처리한다.
  - 중복 감지는 경고 1회만 출력한다.
- ECS 싱글톤 조회 실패 시 브리지는 `no-op` 처리하고 경고 1회만 출력한다.
- one-shot 요청 규칙:
  - 브리지/외부 입력은 요청 값을 `set`만 한다.
  - 요청 소비/리셋(`reset`)은 ECS 전이 시스템이 담당한다.
- 완료 신호(`StageRunCompleted`)는 브리지가 소비 후 즉시 `reset`한다.
- `OnStageRunCompleted` 이벤트는 프레임당 1회만 발행한다(중복 가드).

### 6.4 2계층: Source별 Director 상태 의미
- `Baseline`
  - 모든 Source가 작동 중인 기본 운영 상태.
  - "살아 있는 Source"임을 보여주되, 가장 정확하거나 가장 강한 작동을 요구하지는 않는다.
  - 필요 시 부하 가중 구간에서 최적화 우선 대상이 될 수 있다.
  - `Pressure`와 Clip을 교체하지 않으며, 동일 Clip 위에 밀도 기반 스폰 배율만 낮춰 적용한다.
  - `hazard/event` 요소는 패턴 정보 왜곡을 막기 위해 디렉터 배율로 조정하지 않는다.
- `Pressure`
  - 특정 Source의 존재감을 더 강하게 밀어 올리는 상태.
  - 탄막 밀도, Hazard 비율, 이벤트성 패턴, 공간 압박이 상대적으로 강화된다.
  - 추가 요소가 없으면 배율은 `1.0`을 사용한다.
  - `Baseline`과 동일 Clip을 재생하되, 디렉터 배율 축소 없이 원래 출력을 유지한다.
- `Finish`
  - 해당 Source의 정복 완료 이후 마무리 상태.
  - 기존 Source 고갈 이후의 체감/연출 맥락을 이어받는다.
  - `Source` 고갈 상태 전환(`Depleted`)과 함께 강제 진입한다.
  - 진입 시 Clip은 `스폰 중단` 또는 `고갈 연출용 미량 스폰 Clip`으로 교체한다.
  - `Finish` 상태에서 지속 재생되는 Clip은 `Trash Lane`만 사용한다.

### 6.5 해석 예시
- A, B, C 세 Source가 있는 스테이지 진입 직후:
  - A: `Baseline`
  - B: `Baseline`
  - C: `Baseline`
- 플레이어가 A를 주로 상대하는 구간:
  - A: `Pressure`
  - B: `Baseline`
  - C: `Baseline`
- A 정복 이후:
  - A: `Finish`
  - B: `Baseline`
  - C: `Baseline`

### 6.6 전환 관점
- 전환 판단의 중심은 시간 경과보다 `Source 진행도`, `Source 상태 변화`, `특정 이벤트 발생`이다.
- 시간은 상태 전환을 강제하는 주 기준이 아니라, 과도한 체류를 막는 가드레일로만 사용한다.
- 복수 Source가 있을 때도 상태 전환은 "주도 Source 변경"보다 "강조 비중 변경"에 가깝다.
- `Pressure` 선정 기준은 `Source 영향권 점유 + 유지 시간`으로 한다.
  - 플레이어가 Source 영향권 안에 있으면 해당 Source는 즉시 `Pressure` 후보가 된다.
  - 영향권 이탈 후에는 `유지 시간(PressureHoldSec)`이 만료될 때까지 `Pressure`를 유지할 수 있다.
  - 단순 진입 이벤트 1회가 아니라, 최근 점유/이탈과 유지 시간을 반영해 주도 Source를 결정한다.
  - Pressure 점수 입력 슬롯은 `InfluenceOccupancy`, `InfluenceHoldSec` 2개만 사용한다.
- `Finish`는 고갈/정복 완료 이벤트 발생 후 유지 상태로 본다.
  - 연출 잔향 시간에 별도 제한을 두지 않는다.
  - `Finish` 진입 이벤트는 1회성으로 처리하고, 중복 진입은 무시한다.

## 7. 후속 논의 항목 (TBD)
- `Finish` 전환 시점의 1회성 연출에 무엇을 추가할지.
- 디렉터 입력/출력 데이터 구조의 구체 타입명.
- 기존 시스템과의 상세 업데이트 순서.
- 마이그레이션 단계와 테스트 고정 방식.

## 8. 클립 전환 규칙 메모
- `단일 활성 클립` 교체 시점과 재생 중단/이어받기 규칙은, 기존 Source Clip 선택/전환 규칙의 형태를 유지한 채 디렉터 책임으로 이관한다.
- 즉, Clip을 선택하는 주체는 Source가 아니라 런 진행도 디렉터이며, Source는 디렉터가 할당한 Clip을 기존 재생/전환 규칙 형태로 실행한다.
- 이 항목은 새로운 전환 규칙을 추가 설계하지 않고, 기존 경로를 재사용하는 것을 기본 방침으로 한다.
- 예외: `Finish` 진입 시점은 `SourceState -> Depleted` 전환과 결합된 강제 교체 시점으로 본다.
  - `Baseline <-> Pressure` 전환에서는 Clip을 교체하지 않는다.
  - `Finish` 진입에서만 Clip을 `중단` 또는 `미량 연출` 용도로 교체한다.
  - `Finish` 지속 Clip은 `Trash Lane`만 허용한다.
  - `Finish` 지속 Clip이 없으면 `스폰 중단` 경로를 허용한다.
  - `Finish` 전환 시점의 1회성 연출은 추후 결정(TBD)한다.

## 9. 변경 이력
- 2026-03-04: Demo Shell S1 연동 완료 기준으로 임시 호출 주체(`RunDirectorStageTempFlowDriver`) 방침을 제거하고, 전이 책임을 Shell Owner 기준으로 갱신했다.
- 2026-02-28: StageFlow/UI 미구현 구간 검증을 위해 임시 호출 주체(`RunDirectorStageTempFlowDriver`) 사용 방침을 추가했다.
- 2026-02-28: `RunDirectorStageBridge` 런타임 계약(씬당 1개 소유권, 중복/싱글톤 미존재 시 `no-op + warning 1회`, one-shot set-only/소비 리셋, 완료 이벤트 프레임당 1회 가드, `Update` 단일 루프)을 추가했다.
- 2026-02-27: StageFlow/UI 브리지 구현 기준을 위해 월드 바인딩 시점 비교(권장: `OnEnable + Update 재시도`)와 미연동 기본 모드(`InitialStageState=Running`) 정책을 추가했다.
- 2026-02-27: `1계층 Stage 상태` 전이를 `이벤트 + 게이트` 계약으로 구체화하고, `Idle -> Running`, `ClearReady -> Completed` 전이식 및 게이트 갱신 주체 초안을 추가했다.
- 2026-02-27: 문서 상태를 `active`로 전환하고, 런 디렉터 책임 이관 기준에 맞춰 표현을 정리했다.
- 2026-02-26: 사용자와 상세 합의되지 않은 구체 타입명/버퍼명/시스템 순서/마이그레이션 세부안을 제거하고, 합의된 범위만 남기도록 문서를 축소했다.
- 2026-02-26: `1~3개 Source가 항상 작동하거나 그렇게 보이는 스테이지` 전제를 반영해 상태 모델 방향성을 재정리했다.
- 2026-02-26: `Pressure` 선정 기준을 `Source 영향권 점유 + 유지 시간`으로 고정하고, `Finish`는 별도 시간 제한 없이 유지되는 상태로 정리했다.
- 2026-02-26: 디렉터가 Source별 `단일 활성 클립`을 할당하고, Source는 할당된 클립을 재생하는 책임 경계로 정리했다.
- 2026-02-26: Clip 선택 주체는 디렉터로 이관하되, 교체 시점/재생 중단 규칙은 기존 Source Clip 선택/전환 규칙의 형태를 유지하기로 정리했다.
- 2026-02-26: `Baseline / Pressure`는 동일 Clip을 재생하고, `Baseline`에서는 밀도 기반 스폰만 곱셈 배율로 축소하며 `hazard/event`는 디렉터 배율로 조정하지 않기로 정리했다.
- 2026-02-26: `Finish`는 `SourceState -> Depleted` 전환과 함께 강제 진입하며, 이 시점에 Clip을 `스폰 중단` 또는 `고갈 연출용 미량 스폰`으로 교체하기로 정리했다.
- 2026-02-26: `Finish` 지속 Clip은 `Trash Lane`만 허용하고, 전환 시점 1회성 연출은 추후 결정(TBD)으로 정리했다.
