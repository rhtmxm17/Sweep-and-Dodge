# ADR-20260225-02-wave-clip-slot-channel-contract
> Source 상태별 스폰을 "클립 자산 + 슬롯/채널 바인딩"으로 분리하고, 이벤트-서스테인 우선순위와 서스테인 클립 체인 규약을 고정한다.

## 상태
- 합의됨(미구현)

## 배경
- 현재 `WaveTimelineSO`는 Source 단위의 전체 흐름을 함께 담고 있으며, `Sustain`/`OnStateEnterOnce`를 같은 자산 안에서 분기해 사용한다.
- 차후 "특정 이벤트 발생 시 지정 웨이브 실행" 요구가 증가하면, Source 내부 단일 타임라인 구조는 재사용성/조합성이 급격히 떨어진다.
- 게임 설계상 `Hazard`와 `Trash`는 체감/밸런싱 목표가 달라 별도 채널 운영이 필요하다.

## 결정
1. 클립-바인딩 분리
- `WaveTimelineSO`의 역할을 "단일 클립 자산"으로 전환한다.
- Source는 "상태/페이즈/채널 슬롯"에 클립 후보군을 바인딩한다.

2. 슬롯/채널 규약
- 슬롯 키는 `State + Phase + Channel`이다.
- `Channel`은 `Hazard`, `Trash` 2개를 기본값으로 고정한다.
- `Sustain`에서는 동일 State에 대해 채널별 활성 클립 1개씩 허용하여 최대 2개 동시 실행을 허용한다.

3. 이벤트 우선순위
- 이벤트 클립(`OnStateEnterOnce`) 재생 중에는 `Sustain` 요청 생성을 전면 중지한다.
- 이벤트 재생 종료 후 `Sustain`는 상태 기준 슬롯으로 복귀한다.

4. 서스테인 시간축 + 클립 체인
- `Sustain`도 `StartSec/EndSec` 시간축을 사용한다.
- 활성 `Sustain` 클립이 종료되면 같은 슬롯(State+Sustain+Channel)의 후보군에서 다음 클립을 무작위 선택해 연속 실행한다.
- 후보가 1개면 같은 클립을 반복 재생한다.

5. 결정론 RNG
- 클립 선택 RNG는 결정론 시드로 고정한다.
- 권장 시드 입력: `SourceEntity`, `State`, `Channel`, `SelectionSequence`.

6. 파이프라인/소유권 유지
- Request 단계: 활성 클립 평가 및 요청 집계 생성.
- ExecutionBegin 단계: 기존 Owner가 예산/우선순위/carry 규약으로 소비.
- 기존 프레임 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)은 유지한다.

## 데이터 스키마 초안
### SO 스키마
1. `WaveTimelineSO` (클립 자산)
- `ClipId` (int, 전역 고유)
- `Phase` (`Sustain` / `OnStateEnterOnce`)
- `Channel` (`Hazard` / `Trash`)
- `DurationSec` (float, `> 0`)
- `Segments[]` (클립 로컬 시간축, non-overlap)
  - `StartSec`, `EndSec`
  - `Entries[]` (`Payload/Emission/Sampling/Direction`)

2. `SourceWaveSlotBindingSO` (Source 바인딩 자산)
- `SustainSlots[]`
  - `State`
  - `HazardClips[]` (클립 참조 배열)
  - `TrashClips[]` (클립 참조 배열)
  - `HazardWeights[]` (선택 가중치, 옵션)
  - `TrashWeights[]` (선택 가중치, 옵션)
- `EventSlots[]`
  - `TriggerState`
  - `EventClips[]` (기본 1개, 확장 시 다중 허용 가능)

### ECS 스키마
1. `SourceClipPatternBuffer`
- `ClipId`, `Phase`, `Channel`, `TriggerState`
- `LocalStartSec`, `LocalEndSec`
- 기존 spawn directive 필드(`Emission/Sampling/Direction/Payload`) 일체

2. `SourceSustainSlotCandidateBuffer`
- `State`, `Channel`, `ClipId`, `Weight`

3. `SourceSustainRuntimeComponent`
- `State`
- `ActiveHazardClipId`, `HazardElapsedSec`
- `ActiveTrashClipId`, `TrashElapsedSec`
- `SelectionSequence`

4. `SourceEventRuntimeComponent` (기존 OpeningWave 런타임 대체/확장)
- `IsPlaying`
- `ActiveEventClipId`
- `TriggerState`
- `ElapsedSec`

## 검증 규칙(초안)
- Error:
  - 슬롯 후보군 비어 있음(`State+Channel` 기준)
  - 클립 `DurationSec <= 0`
  - 클립 내부 segment 중첩
  - 슬롯 바인딩된 클립의 `Phase`/`Channel` 불일치
  - `ClipId` 중복
- Warning:
  - 슬롯 후보군 1개(반복 단조 리스크)
  - 가중치 배열 길이 불일치(기본 균등으로 fallback)

## 대안
- 대안 A: 기존 Source 단일 타임라인 유지
  - 장점: 단기 구현 비용이 낮다.
  - 단점: 이벤트 트리거 확장/재사용성이 낮고 데이터 결합도가 커진다.
- 대안 B: 채널 구분 없이 단일 클립만 운용
  - 장점: 런타임 단순화.
  - 단점: Trash/Hazard 튜닝 자유도가 낮아지고 설계 의도 분리가 어렵다.

## 결과
- Source에서 "언제 어떤 웨이브를 재생할지"와 "웨이브 내용"의 책임이 분리된다.
- 이벤트 연출과 서스테인 배경 패턴의 우선순위를 명시적으로 제어할 수 있다.
- 런타임/검증/authoring이 공통 슬롯 모델을 공유해 변경 비용을 낮출 수 있다.

## 후속
- `TD-002`, `TD-003`, `TD-005`에 본 ADR 규약을 v3 초안으로 반영한다.
- 콘텐츠 검증 규칙(`CV03x` 신규 코드)과 PlayMode 시나리오 스모크를 추가한다.
- 구현 단계에서 기존 `SourceOpeningWave*`와 `SourceSpawnPattern*`를 점진적으로 마이그레이션한다.
