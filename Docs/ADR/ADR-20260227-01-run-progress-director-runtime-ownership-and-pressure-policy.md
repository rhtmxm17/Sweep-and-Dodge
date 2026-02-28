# ADR-20260227-01-run-progress-director-runtime-ownership-and-pressure-policy
> 런 진행도 디렉터를 도입해 Source별 상태 해석/클립 선택 책임을 이관하고, Pressure 선정 정책(영향권 점유 + 유지 시간)을 고정한다.

## 상태
- 합의됨 (문서/코드 반영, 테스트 통과)

## 배경
- 스테이지는 1~3개 Source가 동시에 작동하는 구성을 전제로 하며, 시간축보다 Source 진행도 중심의 운영이 필요하다.
- 기존 Source 중심 클립 선택 구조는 "누가 상태를 해석하고 패턴을 지휘하는가" 경계가 모호했다.
- Baseline/Pressure/Finish 상태 정책과 클립/스폰 제약을 한 곳에서 관리하지 않으면 변경 비용과 회귀 위험이 커진다.

## 결정
1. 책임 경계 고정
- 런 진행도 디렉터가 Source별 상태(`Baseline/Pressure/Finish`)를 결정한다.
- `SourceClipRequestBuildSystem`은 디렉터 상태를 소비해 클립/스폰 출력을 조정한다.
- Source는 클립 선택 주체가 아니라, 디렉터 결정 결과를 실행하는 주체로 둔다.
- 레거시 경로(디렉터 미존재 시 SourceState 폴백)는 유지하지 않는다.

2. 상태 전환/운영 정책
- `Pressure`는 플레이어가 Source 영향권 안에 들어오면 즉시 진입한다.
- 영향권 이탈 후 `PressureHoldSec`이 만료될 때까지 Pressure를 유지하고, 만료 시 Baseline으로 복귀한다.
- `Finish`는 `SourceState == Depleted`와 함께 강제 진입한다.
- `Baseline <-> Pressure` 전환에서는 클립을 교체하지 않고, 동일 클립 위에서 출력 강도만 조정한다.

3. 상태별 출력 규칙
- `Baseline`: `Sustain + Trash + RateField` 밀도만 배율 적용.
- `Pressure`: 별도 추가 요소가 없으면 배율 `1.0`.
- `Finish`: `Trash Lane` 외 sustain 재생/요청 차단.

4. 데이터/설정 계약
- 디렉터 설정은 `RunProgressDirectorSettingsAuthoring`로 인스펙터에서 조정한다.
- `Pressure` 입력 슬롯은 `InfluenceOccupancy`, `InfluenceHoldSec` 두 개만 유지한다.
- 설정 Authoring이 없는 경우 부트스트랩 기본 싱글톤으로 fallback한다.

5. StageFlow/UI 브리지 운영 계약
- 브리지는 `RunDirectorStageBridge` 단일 타입으로 GO->ECS 요청/게이트 반영과 ECS->GO 완료 신호 발행을 함께 담당한다.
- 브리지 업데이트 타이밍은 `Update` 단일 루프를 사용한다.
- 씬당 활성 브리지는 1개만 허용한다.
  - 중복 브리지는 소유권 획득 실패로 `no-op` 처리한다.
  - 중복 경고는 1회만 출력한다.
- ECS 싱글톤 미존재 시 브리지는 `no-op` 처리하고 경고 1회만 출력한다.
- one-shot 요청은 브리지가 `set only`로 기록하고, 소비/리셋은 ECS 전이 시스템이 담당한다.
- `StageRunCompleted`는 브리지가 소비 후 즉시 리셋한다.
- `OnStageRunCompleted` 발행은 프레임당 1회로 제한한다.

## 대안
- Source 내부가 계속 클립 선택 주체로 유지:
  - 장점: 초기 변경량이 작음.
  - 단점: 상태 해석/출력 제어 책임이 분산되어 회귀 추적이 어려움.
- 시간축 중심 디렉터:
  - 장점: 구현 단순.
  - 단점: Source 진행도 중심 기획과 불일치.

## 결과
- 상태 해석/출력 제어 책임이 디렉터로 수렴되어 변경 지점이 명확해졌다.
- Baseline/Pressure/Finish 정책이 시스템/문서/테스트에서 동일하게 검증 가능해졌다.
- Debug HUD에서 Source별 디렉터 상태와 Pressure 입력/점수를 직접 확인할 수 있게 됐다.
- StageFlow/UI 연동 경계가 브리지 단일 계약으로 고정되어, 중복 브리지/미연동/초기화 지연 상황에서의 런타임 동작이 예측 가능해졌다.

## 검증
- Unity compile error 0건.
- EditMode 테스트 통과.
- PlayMode 스모크 통과.

## 후속
- Source 진행도 배율(예: Hazard 수집/충돌/Deposit)은 런 디렉터 점수와 분리해 Source 진행도 책임으로 유지한다.
- 본 결정 이후 상세 튜닝 규칙은 `TD-006`, `TD-005`에서 관리한다.
