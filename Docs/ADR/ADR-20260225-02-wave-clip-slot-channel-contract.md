# ADR-20260225-02-wave-clip-slot-channel-contract
> Source 상태별 스폰을 "클립 자산 + 슬롯/채널 바인딩"으로 분리하고, 이벤트 하드 프리엠션/서스테인 체인/채널 우선순위 규약을 고정한다.

## 상태
- 반영됨(구현 완료)
- 현행 운영 보충: Source 정의 SSOT는 이후 `StageDefinitionSO`로 이동했고, `BulletSourceAuthoring` 직참조 슬롯은 legacy migration data로 축소됐다.

## 배경
- 기존 `WaveTimelineSO`는 Source 단위 전체 흐름을 함께 담고 있어, 이벤트 기반 웨이브 재생/재사용 확장 시 결합도가 높았다.
- 게임 설계상 `Hazard`와 `Trash`는 체감 목표가 다르며, 향후 특수 목적 채널(예: 연출/기믹) 확장을 고려해야 했다.
- 채널 용어가 탄 타입(`BulletType`)과 혼동될 수 있어, 구현 네이밍은 `SpawnLane` 계열로 유지했다.

## 결정
1. 자산 전환/레거시 제거
- `WaveClipSO`를 도입했다.
- `WaveTimelineSO` 타입/데이터/참조를 제거했다.
- 데이터 경로를 `Assets/_Project/03_Datas/WaveTimelines`에서 `Assets/_Project/03_Datas/WaveClips`로 전환했다.

2. Source 바인딩 방식
- 당시 결정은 별도 바인딩 SO 없이 `BulletSourceAuthoring` 직참조 배열로 운영하는 것이었다.
- 현재 운영 경로는 `StageDefinitionSO.SourceBindings`가 SSOT이며, `BulletSourceAuthoring` 슬롯 필드는 migration/seed 용도로만 남아 있다.

3. 슬롯/채널 규약
- 슬롯 키는 `State + Phase + Lane`이다.
- Lane enum(`SourceSpawnLaneId`)은 확장 가능 구조다.
- 기본 운영은 `Hazard`, `Trash` 2개 Lane이며, `Sustain`에서 Lane별 활성 클립 1개씩(최대 2개 동시) 허용한다.

4. 이벤트 우선순위: 하드 프리엠션
- 이벤트 클립(`OnStateEnterOnce`) 진입 시 기존 `Sustain` pending 요청을 폐기한다.
- 이벤트 재생 중 `Sustain` 요청 생성을 중지한다.
- 이벤트 중복 트리거는 큐잉한다.
- 이벤트 종료 후 상태 슬롯 기준으로 `Sustain`를 재선택/재개한다.

5. 상태 전환 처리
- `Normal -> Weakened` 등 상태 전환 시 기존 `Sustain` 클립을 즉시 중단한다.

6. 서스테인 시간축 + 체인
- `Sustain`도 `StartSec/EndSec` 시간축을 사용한다.
- 클립 선택 시 로컬 시간을 0으로 리셋한다.
- 활성 클립 종료 시 동일 슬롯 후보군에서 "직전 클립 제외 완전 랜덤"으로 다음 클립을 선택한다.
- 후보가 1개뿐이면 직전 재선택을 허용한다.

7. Clip 내부 segment 중첩 정책
- 동일 `WaveClipSO` 내부에서 segment 시간축 중첩을 허용한다.
- 런타임은 해당 로컬 시간 구간에 걸친 segment들을 모두 평가해 요청을 생성한다.
- 검증은 `EndSec > StartSec`만 강제하고, 중첩 자체는 오류로 취급하지 않는다.

8. 결정론 RNG
- 선택 RNG 키는 `GlobalRunSeed + SourceStableId + SlotKey(State/Phase/Lane) + SelectionSequence`를 사용한다.
- `Entity.Index` 단독 사용은 재현성 리스크로 지양한다.

9. 채널 우선순위 규약
- 우선순위는 Lane 규칙이 최우선이며 `특수 > Hazard > Trash` 순으로 고정한다.
- `SpawnPriority` 중심 정책은 v3 클립 경로에서 사용하지 않는다.

10. 파이프라인/소유권 유지
- Request 단계: 활성 클립 평가 + 요청 집계 생성.
- ExecutionBegin 단계: Owner가 예산/carry/공정성 규약으로 소비.
- 프레임 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)은 유지한다.

## 데이터 스키마 (현행)
### SO 스키마
1. `WaveClipSO`
- `ClipId` (int, 전역 고유)
- `Phase` (`Sustain` / `OnStateEnterOnce`)
- `Lane` (`SourceSpawnLaneId`, 확장 가능)
- `DurationSec` (float, `> 0`)
- `Segments[]`
  - `StartSec`, `EndSec`
  - `Entries[]` (`Payload/Emission/Sampling/Direction`)

### Source Authoring 스키마
1. `BulletSourceAuthoring` 직참조 배열
- `SustainClipSlots[]`
  - `State`
  - `Lane`
  - `Clips[]`
  - `Weights[]` (옵션)
- `EventClipSlots[]`
  - `TriggerState`
  - `EventClips[]` (다중 허용, 큐잉 소비)

### ECS 스키마
1. `SourceClipPatternBuffer`
- `ClipId`, `Phase`, `Lane`, `TriggerState`
- `LocalStartSec`, `LocalEndSec`
- 기존 spawn directive 필드 일체

2. `SourceSustainSlotCandidateBuffer`
- `State`, `Lane`, `ClipId`, `Weight`

3. `SourceSustainRuntimeLaneBuffer`
- `Lane`
- `ActiveClipId`
- `ElapsedSec`
- `LastClipId`
- `SelectionSequence`

4. `SourceEventRuntimeComponent`
- `IsPlaying`
- `ActiveEventClipId`
- `TriggerState`
- `ElapsedSec`
- `SelectionSequence`

5. `SourceEventQueueBuffer`
- `TriggerState`
- `QueuedFrame`

## 검증 규칙 (현행)
- Error:
  - `CV006`: Source에 WaveClip 바인딩 없음
  - `CV008`: `WaveClipSO.Segments` 비어 있음
  - `CV009`: `ClipId` 중복
  - `CV010`: segment 구간 오류(`EndSec <= StartSec`)
  - `CV012`: segment 엔트리 비어 있음
  - `CV013`: 엔트리 `Bullet == null`
  - `CV014`: 미등록 `DefinitionId` 참조
  - `CV015~CV024`, `CV026`: emission/sampling/direction 파라미터 오류
- Warning:
  - `CVW032`: `SpiralStepDeg` 근접 0
  - `CVW033`: `PointSet` 사용(1차 Uniform fallback)
- Runtime policy:
  - sustain 슬롯 후보군 비어 있음은 런타임 skip + `Error` 로그로 처리한다.

## 결과
- Source 측 "무엇을 재생할지"와 클립 자산의 "무엇이 들어있는지" 책임이 분리됐다.
- 이벤트 하드 프리엠션과 큐잉 규약이 런타임에 반영됐다.
- Lane 기반 우선순위(`특수 > Hazard > Trash`)를 구조적으로 강제할 수 있게 됐다.

## 후속
- `TD-002`, `TD-003`, `TD-005` 동기화: 완료.
- `12~14` 항목(데이터 마이그레이션 일반화, 검증 심각도 정책 확장, 테스트 합의 확장)은 별도 세션에서 확정한다.
