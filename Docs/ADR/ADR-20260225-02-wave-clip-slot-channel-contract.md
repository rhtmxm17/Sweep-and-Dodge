# ADR-20260225-02-wave-clip-slot-channel-contract
> Source 상태별 스폰을 "클립 자산 + 슬롯/채널 바인딩"으로 분리하고, 이벤트 하드 프리엠션/서스테인 체인/채널 우선순위 규약을 고정한다.

## 상태
- 합의됨(미구현)

## 배경
- 현재 `WaveTimelineSO`는 Source 단위 전체 흐름을 함께 담고 있어, 이벤트 기반 웨이브 재생/재사용 확장 시 결합도가 높다.
- 게임 설계상 `Hazard`와 `Trash`는 체감 목표가 다르며, 향후 특수 목적 채널(예: 연출/기믹) 확장을 고려해야 한다.
- 채널 용어가 탄 타입(`BulletType`)과 혼동될 수 있어, 구현 네이밍은 `SpawnLane` 계열로 검토한다.

## 결정
1. 자산 전환/레거시 처리
- 신규 `WaveClipSO`를 도입한다.
- 기존 `WaveTimelineSO`는 레거시 경로로 임시 유지한다.
- `WaveClipSO` 안정성 검증(콘솔/테스트/운영 씬 스모크) 완료 시 `WaveTimelineSO`를 제거한다.

2. Source 바인딩 방식(1차)
- 1차 구현은 별도 바인딩 SO 대신 `BulletSourceAuthoring` 직참조 배열로 시작한다.
- 추후 복잡도 증가 시 `SourceWaveSlotBindingSO` 분리 여부를 재검토한다.

3. 슬롯/채널 규약
- 슬롯 키는 `State + Phase + Lane`이다.
- Lane enum은 확장 가능 구조로 설계한다.
- 기본 운영 Lane은 `Hazard`, `Trash` 2개이며, `Sustain`에서 Lane별 활성 클립 1개씩(최대 2개 동시) 허용한다.

4. 이벤트 우선순위: 하드 프리엠션
- 이벤트 클립(`OnStateEnterOnce`) 진입 시점에 기존 `Sustain` pending 요청을 폐기한다.
- 이벤트 재생 중에는 `Sustain` 요청 생성을 중지한다.
- 이벤트 종료 후 상태 슬롯 기준으로 `Sustain` 재선택/재개한다.
- 이벤트 중복 트리거는 큐잉한다.

5. 상태 전환 처리
- `Normal -> Weakened` 등 상태 전환 시 기존 `Sustain` 클립은 즉시 중단한다.

6. 서스테인 시간축 + 체인
- `Sustain`도 시간축(`StartSec/EndSec`)을 사용한다.
- 클립이 선택될 때 로컬 시간은 0으로 리셋한다.
- 활성 클립 종료 시 같은 슬롯 후보군에서 "직전 클립 제외 완전 랜덤"으로 다음 클립을 선택한다.
- 후보가 1개뿐이면 직전 재선택을 허용한다.

7. 결정론 RNG
- 권장 시드 키: `GlobalRunSeed + SourceStableId + SlotKey(State/Phase/Lane) + SelectionSequence`.
- `Entity.Index` 단독 사용은 재현성 리스크가 있어 지양한다.

8. 채널 우선순위 규약
- 우선순위는 Lane 규칙이 최우선이며 `특수 > Hazard > Trash` 순으로 고정한다.
- 기존 `SpawnPriority` 중심 정책은 v3에서 제거를 기본안으로 한다.

9. 파이프라인/소유권 유지
- Request 단계: 활성 클립 평가 + 요청 집계 생성.
- ExecutionBegin 단계: Owner가 예산/carry/공정성 규약으로 소비.
- 프레임 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)은 유지한다.

## 데이터 스키마 초안
### SO 스키마
1. `WaveClipSO`
- `ClipId` (int, 전역 고유)
- `Phase` (`Sustain` / `OnStateEnterOnce`)
- `Lane` (확장 가능한 enum, 기본 `Hazard`/`Trash`)
- `DurationSec` (float, `> 0`)
- `Segments[]` (클립 로컬 시간축, non-overlap)
  - `StartSec`, `EndSec`
  - `Entries[]` (`Payload/Emission/Sampling/Direction`)

### Source Authoring 스키마(1차)
1. `BulletSourceAuthoring` 직참조 배열
- `SustainSlots[]`
  - `State`
  - `Lane`
  - `Clips[]`
  - `Weights[]` (옵션)
- `EventSlots[]`
  - `TriggerState`
  - `EventClips[]` (다중 허용, 큐잉 소비)

### ECS 스키마
1. `SourceClipPatternBuffer`
- `ClipId`, `Phase`, `Lane`, `TriggerState`
- `LocalStartSec`, `LocalEndSec`
- 기존 spawn directive 필드 일체

2. `SourceSustainSlotCandidateBuffer`
- `State`, `Lane`, `ClipId`, `Weight`

3. `SourceSustainRuntimeComponent`
- `ActiveClipIdByLane` (Lane별)
- `ElapsedSecByLane`
- `LastClipIdByLane`
- `SelectionSequenceByLane`

4. `SourceEventRuntimeComponent`
- `IsPlaying`
- `ActiveEventClipId`
- `TriggerState`
- `ElapsedSec`

5. `SourceEventQueueBuffer`
- `TriggerState`
- `QueuedFrame`

## 검증 규칙(초안)
- Error:
  - `ClipId` 중복
  - `DurationSec <= 0`
  - 클립 내부 segment 중첩
  - 슬롯 바인딩의 `Phase/Lane` 불일치
  - 이벤트 클립 누락
- Runtime policy:
  - sustain 슬롯 후보군 비어 있음은 런타임 skip + `Error` 로그로 처리한다.
- Warning:
  - 슬롯 후보군 1개(단조 반복 가능)
  - 가중치 배열 길이 불일치(균등 fallback)

## 결과
- Source 측 "무엇을 재생할지"와 클립 자산의 "무엇이 들어있는지" 책임이 분리된다.
- 이벤트 우선순위가 하드 프리엠션으로 고정되어 이벤트 연출 의도를 보장한다.
- Lane 기반 우선순위(`특수 > Hazard > Trash`)를 구조적으로 강제할 수 있다.

## 후속
- `TD-002`, `TD-003`, `TD-005`의 v3 초안을 본 ADR 기준으로 동기화한다.
- 데이터 마이그레이션/검증 코드(`CV03x`)/테스트 합의(12~14)는 후속 세션에서 확정한다.
- 롤아웃 목표는 해당 컨텍스트 완료 시점까지 v3 단일 경로 전환이다.
