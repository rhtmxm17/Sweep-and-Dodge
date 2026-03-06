# WaveTimelineSO -> WaveClipSO 마이그레이션 완료 아카이브

## Metadata
- doc_id: `TD-004`
- type: `TechnicalDesign`
- status: `archived`
- last_updated: `2026-02-25`
- related_docs:
  - [ADR-20260225-02-wave-clip-slot-channel-contract.md](../ADR/ADR-20260225-02-wave-clip-slot-channel-contract.md)
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [TD-005-spawn-directive-settings-reference.md](./TD-005-spawn-directive-settings-reference.md)

> 본 문서는 `WaveTimelineSO` 기반 authoring/runtime 경로를 `WaveClipSO` 기반 경로로 전환한 결과를 기록한 히스토리 문서다. 현재 Source 정의의 운영 SSOT는 `StageDefinitionSO`이며, 여기서 언급되는 `BulletSourceAuthoring` clip 슬롯은 legacy authoring 기록으로만 취급한다.

## 1. 전환 결과 요약
- `WaveClipSO` 도입: 완료.
- `BulletSourceAuthoring` 바인딩 전환(`SustainClipSlots[]`, `EventClipSlots[]`): 완료.
  - 단, 현재 운영 경로에서는 `StageDefinitionSO`가 clip binding SSOT이며 `BulletSourceAuthoring` 슬롯 필드는 deprecated migration data다.
- `WaveTimelineSO` 타입 제거: 완료.
- `Assets/_Project/03_Datas/WaveTimelines` 데이터 제거 및 `WaveClips` 경로 사용: 완료.
- v3 단일 경로(클립/슬롯/채널) 전환: 완료.

## 2. 데이터/코드 매핑
| 레거시 개념 | 전환 후 개념 | 반영 상태 |
| --- | --- | --- |
| Source 단위 통합 `WaveTimelineSO` | 재사용 가능한 `WaveClipSO` 단위 자산 | 완료 |
| `WaveTimelineSO.SpawnEntry` | `WaveClipSO.Segments[].Entries[]` | 완료 |
| Source 단일 Wave 참조 | `SustainClipSlots[]`, `EventClipSlots[]` 배열 바인딩 | 완료 |
| `SpawnPriority` 중심 우선순위 | Lane 우선순위(`특수 > Hazard > Trash`) | 완료 |
| 상태 진입 연출 + sustain 공존 | 이벤트 하드 프리엠션 + 큐잉 | 완료 |
| 상태 전환 시 sustain 유지 | 상태 전환 시 sustain 즉시 중단 | 완료 |

## 3. 마이그레이션 시 적용한 정책
1. `WaveClipSO`의 `ClipId/Phase/Lane/DurationSec/Segments`를 기준 스키마로 고정했다.
2. sustain 체인은 "직전 클립 제외 랜덤"을 사용하되, 후보가 1개면 재선택을 허용했다.
3. 이벤트 트리거 중복은 큐잉하며, 이벤트 진입 시 기존 sustain pending을 폐기했다.
4. 결정론 RNG 키는 `GlobalRunSeed + SourceStableId + SlotKey + SelectionSequence`로 고정했다.

## 4. 검증/게이트 동기화
- 콘텐츠 검증 입력을 `WaveClipSO` 기준으로 전환했다.
- 주요 규칙:
  - `CV006`: Source에 WaveClip 바인딩 없음
  - `CV008`: Segments 비어 있음
  - `CV009`: `ClipId` 중복
  - `CV010`, `CV011`, `CV012`~`CV024`, `CV026`: 구간/엔트리 파라미터 오류
- 테스트:
  - EditMode 계약 테스트: 전환 후 규칙 기준 통과
  - PlayMode 스모크: 전환 후 기본 시나리오 통과

## 5. 정리된 레거시 자산
- 제거된 타입:
  - `Assets/_Project/02_Scripts/ECS/Authoring/WaveTimelineSO.cs`
- 제거된 데이터 경로:
  - `Assets/_Project/03_Datas/WaveTimelines/*`
- 운영 데이터 경로:
  - `Assets/_Project/03_Datas/WaveClips/*`

## 6. 현재 기준 문서
- 정책/결정: `ADR-20260225-02`
- 런타임 계약: `TD-002`
- 스폰 모델: `TD-003`
- 설정 레퍼런스: `TD-005`
