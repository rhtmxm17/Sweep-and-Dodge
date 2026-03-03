# ADR-20260303-03-replay-persistence-and-schema-compatibility-policy
> 리플레이를 세션 메모리(stage)에서 영속 파일로 확장하고, 스키마 호환 정책을 구버전 완전 거부로 고정한다.

## 상태
- 제안됨
- 다음에서 일부 대체됨: [ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md](ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md)

## 배경
- 현재 리플레이는 `ReplaySessionStaging` 기반으로 단일 런타임 세션에서만 다룰 수 있다.
- 디버깅/회귀 재현을 위해 파일 저장/불러오기(persistence)가 필요하다.
- 동시에 포맷 구조가 빠르게 변경되는 단계라 버전별 마이그레이션을 유지하면 구현/검증 비용이 급격히 증가한다.

## 결정
1. 리플레이 persistence를 공식 범위로 채택한다.
- 최소 저장 단위는 `runSeed + frame snapshot buffer`로 고정한다.
- 로드 결과는 기존 staged playback 경로로 주입한다.

2. 스키마 호환 정책은 구버전 완전 거부를 채택한다.
- 파일 `ReplaySchemaVersion`이 현재 지원 버전과 다르면 즉시 실패한다.
- 부분 로드/자동 변환/암묵적 보정은 허용하지 않는다.

3. 구버전 완전 거부 근거를 비용 관점으로 고정한다.
- 구조 변경이 잦은 단계에서는 마이그레이션 효과 대비 비용이 과다하다.

4. 안정 버전 진입 시점에 마이그레이션 정책을 개시한다.
- 안정화 판단 이후 별도 ADR로 마이그레이션 범위/전략을 확정한다.

## 대안 비교
### 대안 A: persistence + 구버전 완전 거부(채택)
- 장점: 구현 단순성, 실패 원인 명확성, 테스트 행렬 축소
- 단점: 과거 리플레이 파일 재사용 불가

### 대안 B: persistence + 조건부 마이그레이션
- 장점: 일부 구버전 재사용 가능
- 단점: 변환 로직/검증 비용 증가, 오변환 리스크

### 대안 C: persistence + 전면 마이그레이션
- 장점: 사용자 관점 연속성 최대
- 단점: 현재 단계 비용 과다, 빠른 구조 변경과 충돌

## 결과
- 기대 효과:
  - 저장 가능한 재현 루프를 확보한다.
  - 버전 불일치 실패 조건이 단순해 운영/디버깅이 쉬워진다.
- 리스크:
  - 포맷 변경 시 기존 파일이 즉시 무효화된다.
- 완화:
  - 실패 로그에 `파일 버전/지원 버전`을 명시한다.
  - 안정 버전 진입 전까지 릴리스 노트에 비호환 가능성을 고지한다.

## 후속
1. TD에 Replay-IO 포맷/에러 규약 정의
2. 테스트 추가: `지원 버전 로드 성공`, `버전 불일치 로드 실패`
3. 안정 버전 진입 체크리스트 수립 후 마이그레이션 ADR 발행

## 관련 문서
- [Docs/ADR/ADR-20260303-01-replay-min-foundation-and-seed-unification.md](ADR-20260303-01-replay-min-foundation-and-seed-unification.md)
- [Docs/ADR/ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md](ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md)
