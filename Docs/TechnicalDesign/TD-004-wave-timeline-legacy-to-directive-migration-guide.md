# WaveTimeline Legacy -> SpawnDirective 마이그레이션 가이드

## Metadata
- doc_id: `TD-004`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-02-23`
- related_docs:
  - [TD-002-pattern-wave-progress-runtime-contract.md](./TD-002-pattern-wave-progress-runtime-contract.md)
  - [TD-003-spawn-directive-model.md](./TD-003-spawn-directive-model.md)
  - [OPS-001-prototype-core-capability-priority-matrix.md](../ProjectOps/OPS-001-prototype-core-capability-priority-matrix.md)

> `WaveTimelineSO.SpawnEntry`의 legacy 필드에서 `Payload/Emission/Sampling` 신규 프로필로 이행할 때의 실무 기준 문서.

## 1. 적용 대상
- 대상: `SpawnEntry.UseDirectiveProfiles == false`인 기존 엔트리
- 목표: 체감 밀도/활성 캡을 유지한 상태로 신규 계약(`TD-003`)을 우선 사용하도록 전환

## 2. 필드 매핑표 (legacy -> directive)
| legacy 필드 | directive 필드 | 매핑 규칙 |
| --- | --- | --- |
| `Bullet` | `Payload.Bullet` | 동일 참조 복사 |
| `SpawnMode` | `Emission.SpawnMode` | 동일 enum 값 복사 |
| `SpawnDensityPerSecPerArea` | `Emission.RatePerSecPerArea` | 동일 값 복사 |
| `MaxActiveDensityPerArea` | `Emission.MaxActiveDensityPerArea` | 동일 값 복사 |
| `(legacy에 없음)` | `Emission.EmissionMode` | 기본 `RateField(0)` |
| `(legacy에 없음)` | `Emission.MeanEventsPerSec` | `0` (RateField 유지) |
| `(legacy에 없음)` | `Sampling.SamplingMode` | legacy 동등값 `PollutionTopK(1)` |
| `(legacy에 없음)` | `Sampling.CenterMode` | 기본 `SourceCenter(0)` |
| `(legacy에 없음)` | `Sampling.FixedPoint` | `{x:0, y:0}` |
| `(legacy에 없음)` | `Sampling.SpawnOffset` | `{x:0, y:0}` |
| `(legacy에 없음)` | `Sampling.SpawnSampleBudget` | 기본 `16` |
| `(legacy에 없음)` | `Sampling.PlayerNoSpawnRadius` | 기본 `0` |
| `UseDirectiveProfiles` | `UseDirectiveProfiles` | `true`로 전환 |

## 3. 권장 기본값
- `SpawnSampleBudget`: `16`
- `CenterMode`: `SourceCenter`
- `SamplingMode`: `PollutionTopK`

위 기본값은 현재 legacy resolve 동작(`WaveTimelineSO.Resolve*`)과 동등한 초기값이다.

## 4. Poisson 도입 시 주의사항
- `EmissionMode=Poisson`으로 전환할 때 `MeanEventsPerSec`를 0보다 큰 값으로 명시한다.
- Poisson 도입 엔트리에서 `RatePerSecPerArea`는 체감 비교 기준으로만 유지하고, 운영 판단은 `MeanEventsPerSec` 중심으로 한다.
- 동일 Wave 안에서 RateField/Poisson을 혼합하면 체감 분산이 커지므로 1차 마이그레이션에서는 혼합 도입을 피한다.
- 검증 규칙:
  - `MeanEventsPerSec < 0`이면 `CV017` 에러
  - `SpawnSampleBudget < 0`이면 `CV018` 에러
  - `PlayerNoSpawnRadius < 0`이면 `CV019` 에러

## 5. 권장 마이그레이션 절차
1. 대상 `WaveTimeline`을 열고 엔트리 단위로 매핑표를 적용한다.
2. `UseDirectiveProfiles`를 `true`로 설정한다.
3. legacy 필드는 즉시 삭제하지 않고 fallback 검증 구간 동안 유지한다.
4. 컴파일/검증 루프 실행:
   - `refresh_unity(compile=request, wait_for_ready=true)`
   - `read_console(action=get, types=["error"], include_stacktrace=true)`
   - `EditMode` 테스트
   - `PlayMode` 전용 스모크

## 6. 이번 반영 샘플
- `Assets/_Project/03_Datas/WaveTimelines/bwt_sample_stage01.asset`
- `Assets/_Project/03_Datas/WaveTimelines/bwt_from_bsp_default.asset`
- `Assets/_Project/03_Datas/WaveTimelines/bwt_from_bsp_dust_only.asset`

세 에셋 모두 `Emission=RateField`, `Sampling=PollutionTopK + SourceCenter + budget 16`으로 전환해 legacy 체감을 유지했다.
