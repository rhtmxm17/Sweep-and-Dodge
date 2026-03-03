# ADR-20260303-04-fixed-tick-time-source-for-replay-determinism
> 리플레이/결정론 품질을 위해 로직 시간원을 가변 DeltaTime에서 고정 Tick 기반으로 전환한다.

## 상태
- 제안됨

## 배경
- 현재 플레이어/탄환/스폰 일부 로직이 `SystemAPI.Time.DeltaTime`에 직접 의존한다.
- 실런타임 프레임 시간 변동(렌더 부하, 에디터 부하, VSYNC 상태)에 따라 누적 오차가 발생할 수 있다.
- 리플레이에서 입력/시드가 동일해도 위치/상태가 기록 시점과 어긋나는 사례가 확인되었다.

## 결정
1. 로직 시간원을 고정 Tick으로 통일한다.
- 결정론 대상 시스템은 `SystemAPI.Time.DeltaTime` 대신 `FixedTickDelta`를 사용한다.
- Tick 카운터(`BulletFrameCounter`)를 로직 프레임의 기준 ID로 유지한다.

2. 표현 계층과 로직 계층의 시간축을 분리한다.
- 카메라 damping, Animator 블렌딩 등 표현 디테일은 가변 프레임을 허용한다.
- 판정/스폰/이동/수명 등 로직은 고정 Tick만 사용한다.

3. 리플레이 계약은 `runSeed + tick별 입력` 중심으로 유지한다.
- 고정 Tick 경로를 기준으로 재생한다.
- 필요 시 디버그용 `Pause/Step(1 tick)`를 지원한다.

## 대안 비교
### 대안 A: 고정 Tick 시간원 도입 (채택)
- 장점: 결정론/재현성 강화, 리플레이 품질 안정, 1-tick step 디버깅 용이
- 단점: DeltaTime 의존 시스템 치환 비용, 초기 전환 작업량

### 대안 B: 기록 시점 DeltaTime 저장 후 재생 시 주입
- 장점: 기존 구조 변경 폭이 작아 보임
- 단점: 모든 로직 경로에 주입선 유지 필요, 장기 유지비/누락 리스크 큼

### 대안 C: 엔티티 상태 전체 스냅샷 리플레이
- 장점: 재생 시점 결과 일치성 자체는 높음
- 단점: 대량 탄환에서 저장 용량/IO/메모리 비용 과다

## 결과
- 기대 효과:
  - 동일 입력/시드에서 재생 일치성 개선
  - 프레임 스텝 단위 디버깅 가능
- 리스크:
  - 치환 전환 중 일부 시스템의 시간축 혼재
  - 서브스텝/누적기 설계 미흡 시 과실행 또는 스킵
- 완화:
  - 치환 우선순위를 결정론 영향도 기준으로 단계화
  - 시스템별 `DeltaTime` 직접 참조 금지 규칙을 점진 적용

## 후속
1. TD 발행: 시간원 구조/서브스텝/치환 순서 상세화
2. 치환 대상 시스템 인벤토리 확정 및 단계별 WU 분해
3. `SameSeed + SameInput + FixedTick` 동일성 테스트 추가

## 관련 문서
- [ADR-20260303-03-replay-persistence-and-schema-compatibility-policy.md](ADR-20260303-03-replay-persistence-and-schema-compatibility-policy.md)
- [TD-008-replay-io-persistence-and-version-policy.md](../TechnicalDesign/TD-008-replay-io-persistence-and-version-policy.md)
