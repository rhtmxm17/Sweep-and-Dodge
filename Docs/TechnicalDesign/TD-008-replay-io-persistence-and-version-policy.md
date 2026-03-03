# TD-008 Replay IO Persistence And Version Policy

## 목적
- 리플레이 데이터를 파일로 저장/복원하는 IO 경로를 정의한다.
- 버전 불일치 처리 규칙을 명시해 로더 동작을 일관화한다.
- 저장 계약 정본을 `runSeed + tick 입력 스트림`으로 고정한다.

## 범위
- 포함:
  - Replay 파일 헤더/본문 포맷
  - 저장(write)/불러오기(read) 파이프라인
  - 버전 정책(지원/거부)과 에러 처리
- 제외:
  - 구버전 마이그레이션 구현
  - 고정 Tick 실행기 구현/DeltaTime 치환 상세
  - 크로스 플랫폼 결정론 보장 정책

## 설계 기준(정본)
1. 저장 payload는 `runSeed + tick 입력`을 기본으로 한다.
- 위치/회전 등 월드 상태 스냅샷은 정본 계약에 포함하지 않는다.
- 필요 시 디버그 보조 데이터는 별도 옵션/파일로 분리한다.

2. Replay IO는 고정 Tick 시간원 설계와 정합해야 한다.
- tick 인덱스는 로직 프레임 ID와 1:1 매핑한다.
- 시간원/치환 계획은 [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md)를 따른다.

## 데이터 계약
1. 저장 payload
- `runSeed`
- `ReplayTickInputElement[]` (명칭은 구현 시점에 확정)
  - `Tick`
  - `MoveAxis`, `AimWorldXZ`, `HasAimWorldPoint`
  - `VacuumRequested`, `CleanupActionRequested`, `RequestedCleanupActionSlot`
  - `InputSequence`

2. 파일 헤더
- `Magic` (예: `RPLY`)
- `ReplaySchemaVersion` (uint)
- `TickCount` (uint)
- `RunSeed` (uint)
- `PayloadByteLength` (uint)
- `Checksum` (uint32 또는 uint64)

## 버전 정책
- 정책:
  - 구버전 완전 거부
- 근거:
  - 구조 변경이 잦은 단계이므로 마이그레이션 효과에 비해 비용 과다
- 후속:
  - 안정 버전에 돌입했다 판단되면 마이그레이션 개시

## IO 파이프라인
1. Save
- tick 입력 버퍼 스냅샷 획득
- 헤더 작성(`ReplaySchemaVersion = Current`)
- 본문 직렬화
- 체크섬 계산 후 파일 저장

2. Load
- 헤더 파싱
- `Magic` 검증 실패 시 즉시 실패
- `ReplaySchemaVersion != Current`면 즉시 실패
- 체크섬 검증 실패 시 즉시 실패
- 본문 역직렬화 후 재생 스테이징 경로로 전달

## 에러 처리 규약
- 실패는 `false + reason code + human-readable message`로 반환
- 최소 reason:
  - `InvalidMagic`
  - `UnsupportedVersion`
  - `CorruptedPayload`
  - `IoFailure`
- `UnsupportedVersion` 메시지에 `fileVersion/currentVersion` 포함

## 테스트 계획
1. `SaveThenLoad_SameVersion_Succeeds`
- 저장 후 즉시 로드 성공
- tick count / run seed / 입력 시퀀스 일치

2. `Load_VersionMismatch_FailsFast`
- `ReplaySchemaVersion` 불일치 파일 로드 시 실패
- reason=`UnsupportedVersion`

3. `Load_InvalidMagic_FailsFast`
- 매직 값 훼손 시 실패

4. `Load_CorruptedPayload_FailsFast`
- 체크섬 불일치 시 실패

## 구현 메모
- 1차 구현은 단일 파일/단일 payload로 시작한다.
- 성능 최적화(압축/청크 분할)는 후속 단계에서 고려한다.
- 스키마 전환기에는 구버전 자동 변환을 제공하지 않는다(즉시 실패).

## 관련 문서
- [ADR-20260303-03-replay-persistence-and-schema-compatibility-policy.md](../ADR/ADR-20260303-03-replay-persistence-and-schema-compatibility-policy.md)
- [ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md](../ADR/ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md)
- [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md)
