# ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay
> 플레이어 런타임 권한을 ECS로 이전하고, GameObject는 입력 수집/표현 소비 경계로 고정해 리플레이 재현성과 로직 소유권을 강화한다.

## 상태
- 반영됨

## 배경
- 현재 구조는 `GameObject`가 플레이어 이동/회전을 계산하고, ECS는 동기화된 값을 소비한다.
- `OPS-001 #10` 최소 리플레이 기반은 구축됐지만, 플레이어 상태 Writer가 ECS 바깥에 있으면 입력/시뮬레이션 재현 범위가 좁아진다.
- 팀 합의로, Animator 블렌딩/카메라 damping 같은 비-로직 표현 디테일은 완전 재현 우선순위가 낮다.

## 결정
1. 플레이어 런타임 권한을 ECS 단일 Writer로 고정한다.
- 플레이어 위치/회전/행동 상태는 ECS 시스템만 수정한다.
- `GameObject`는 해당 상태를 역으로 쓰지 않는다.

2. `GameObject` 책임을 입력 수집 + 표현 소비로 한정한다.
- 입력은 프레임 경계에서 ECS 입력 컴포넌트/버퍼로 전달한다.
- Animator/카메라/FX는 ECS 상태/이벤트를 읽어 표현만 수행한다.

3. 리플레이 기록 단위를 "플레이어 상태"가 아닌 "입력 의도"로 고정한다.
- Record/Playback은 ECS 입력 경로에 주입한다.
- Playback 중 라이브 입력과 수동 핫키 입력은 차단한다.

4. 이벤트 채널은 연출 트리거 전달 전용으로 사용한다.
- 로직 상태는 컴포넌트 상태로 보존하고, 이벤트 채널은 `Frame`, `Sequence`를 포함한 파생 신호만 전달한다.
- 예: `VacuumStart`, `CleanupTriggered`, `HazardHit`, `StateChanged`.

## 대안 비교
### 대안 A: 현행 하이브리드 유지(GO 이동 권한 유지)
- 장점: 변경량 최소
- 단점: 동일 seed+입력에서 플레이어 궤적 차이 발생 가능, 리플레이 디버깅 비용 증가

### 대안 B: ECS로 플레이어 권한 이전 + GO 표현 소비(채택)
- 장점: 소유권 명확화, 로직 재현성/테스트 용이성 개선, 기존 Animator/카메라 자산 재사용 가능
- 단점: 입력/브리지 경계 재정비와 초기 회귀 대응 비용 발생

### 대안 C: 플레이어+카메라+Animator까지 완전 ECS화
- 장점: 가장 높은 결정성
- 단점: 현재 범위 대비 비용 과다, 표현 파이프라인 재구축 부담 큼

### 채택안 선택 이유
- 리플레이와 로직 소유권 문제를 해결하면서도, 표현 파이프라인은 점진 이행으로 유지할 수 있는 비용/효과 균형이 가장 적절하다.

## 결과
- 기대 효과:
  - 동일 `runSeed + 입력`에서 플레이어 상태 재현성이 개선된다.
  - GO/ECS 이중 Writer 충돌 리스크가 줄어든다.
  - EditMode/PlayMode에서 플레이어 상태 트랙 검증이 쉬워진다.
- 리스크:
  - 조작감 변화 가능성(업데이트 순서, delta 처리, 브리지 타이밍)
  - 전환기 동안 누락된 입력 경계/차단 규칙으로 회귀 발생 가능
- 완화:
  - 단계적 이행: 입력 경계 고정 -> ECS 이동 권한 이전 -> GO Writer 제거
  - Replay/PlayMode 스모크에 "동일 입력 궤적" 검증 항목 추가

## 후속
1. 플레이어 이동/회전 ECS 시스템을 Writer로 확정하고 GO 이동 write 제거
2. 입력 컴포넌트 계약(이동축/조준/행동/프레임/시퀀스) 확정 및 리플레이 버퍼 정렬
3. Animator 브리지를 ECS 상태/이벤트 소비형으로 전환
4. 카메라는 ECS 플레이어 상태 추종 Reader로 고정(로직 write 금지)
5. `same seed + same replay input => same player track` 테스트 추가

## 관련 문서
- [Docs/ADR/ADR-20260303-01-replay-min-foundation-and-seed-unification.md](ADR-20260303-01-replay-min-foundation-and-seed-unification.md)
- [Docs/ADR/ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md)
- [Docs/ADR/ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md](ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md)
