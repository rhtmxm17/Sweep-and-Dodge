# TD-008 Replay IO Persistence And Version Policy

## 목적
- 리플레이 데이터를 파일로 저장/복원하는 IO 경로를 정의한다.
- 버전 불일치 처리 규칙을 명시해 로더 동작을 일관화한다.

## 범위
- 포함:
  - Replay 파일 헤더/본문 포맷 초안
  - 저장(write)/불러오기(read) 파이프라인
  - 버전 정책(지원/거부)과 에러 처리
- 제외:
  - 구버전 마이그레이션 구현
  - 크로스 플랫폼 결정론 보장 정책

## 데이터 계약
1. 저장 payload
- `runSeed`
- `ReplayInputFrameBufferElement[]`
  - `Frame`
  - `MoveAxis`, `AimWorldXZ`, `HasAimWorldPoint`
  - `Position`, `Rotation`, `SyncRotation` (디버그/검증 보조)
  - `VacuumRequested`, `CleanupActionRequested`, `RequestedCleanupActionSlot`
  - `InputSequence`

2. 파일 헤더(초안)
- `Magic` (예: `RPLY`)
- `ReplaySchemaVersion` (uint)
- `FrameCount` (uint)
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
- Replay buffer 스냅샷 획득
- 헤더 작성(`ReplaySchemaVersion = Current`)
- 본문 직렬화
- 체크섬 계산 후 파일 저장

2. Load
- 헤더 파싱
- `Magic` 검증 실패 시 즉시 실패
- `ReplaySchemaVersion != Current`면 즉시 실패
- 체크섬 검증 실패 시 즉시 실패
- 본문 역직렬화 후 `ReplaySessionStaging.StagePlayback(...)`로 전달

## 에러 처리 규약
- 실패는 `false + reason code + human-readable message`로 반환
- 최소 reason:
  - `InvalidMagic`
  - `UnsupportedVersion`
  - `CorruptedPayload`
  - `IoFailure`
- `UnsupportedVersion` 메시지에 `fileVersion/currentVersion` 포함

## 테스트 초안
1. `SaveThenLoad_SameVersion_Succeeds`
- 저장 후 즉시 로드 성공
- frame count / run seed 일치

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
- 불러오기 성공 시 staged 프레임은 0 기반으로 재베이스된 상태를 유지한다.
