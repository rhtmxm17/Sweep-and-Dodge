# ADR-20260316-01-hazardstack-runtime-ownership-and-frame-order
> HazardStack의 단일 writer, 동프레임 `수거 확정 후 리셋`, 다음 프레임 배율 반영을 하나의 계약으로 고정

## 상태
- 합의됨 (문서 반영)

## 배경
- `HazardStack` 규칙이 `GD-004`, `GD-006`, `TD-002`, `TD-012`에 파편화되어 있었다.
- 현재 구현은 `HazardStack` 상태 자체가 없고, `Source 진행도` 누적만 `BulletVacuumRequestSystem`에 존재한다.
- `HazardCaptured`, `Hit`, `Deposit`이 각각 별도 시스템에 걸쳐 있어 직접 write를 허용하면 플레이어 리스크 상태의 writer가 분산된다.
- 같은 프레임 수거와 `Hit/Deposit`이 동시에 일어날 때 결과 순서가 문서화되지 않으면 회귀와 체감 불일치가 발생한다.

## 결정
1. `RiskMultiplier` 범위
- 이번 범위는 `1 + (HazardStack × HazardBonusRate)`만 사용한다.
- `Load / Capacity` 기반 항은 후속 범위로 미룬다.

2. HazardStack writer
- `HazardStack`은 플레이어 리스크 owner 단일 책임으로 확정한다.
- `BulletVacuumRequestSystem`, `PlayerHazardCollisionExecutionSystem`, `PlayerCarryBinDepositExecutionSystem`은 `HazardStack` 직접 write를 하지 않는다.
- 각 시스템은 증가/reset 요청만 생성하고, 최종 상태 반영은 단일 owner가 수행한다.

3. 프레임 적용 시점
- Request 단계는 프레임 시작 시점 `HazardStack` snapshot을 read-only로 사용한다.
- `HazardCaptured`로 증가한 stack은 같은 프레임 수거에 즉시 반영하지 않는다.
- 증가분은 다음 프레임부터 `Trash + HazardCaptured`의 `Source 진행도` 배율에 적용한다.

4. 동프레임 충돌 규칙
- 같은 프레임 수거와 `Hit/Deposit`이 겹치면 `수거 확정 후 리셋`을 적용한다.
- 의미:
  - 수거 결과(`Source 진행도`, `Carry`, 증가 요청)는 롤백하지 않는다.
  - 프레임 종료 시점 최종 `HazardStack`은 `Hit/Deposit` reset이 덮는다.

5. 초기화 규칙
- 스테이지 시작/재시작 시 `HazardStack = 0`으로 초기화한다.
- Deposit은 기존 요청이 실제로 발생한 경우에만 `HazardStack` 리셋을 동반한다.

## 대안
- 대안 1: `BulletVacuumRequestSystem`, `Hit`, `Deposit` 시스템이 각각 직접 write
  - 장점: 구현 시작이 빠르다.
  - 단점: writer가 분산되어 소유권이 흐려지고, 동프레임 결과가 시스템 순서에 종속된다.

- 대안 2: `HazardCaptured` 증가분을 같은 프레임에 즉시 배율 반영
  - 장점: 체감상 즉시 보상처럼 느껴질 수 있다.
  - 단점: 같은 프레임 다중 수거 순서에 따라 결과가 달라질 수 있고, 병렬 판정 안정성이 낮다.

- 대안 3: `Hit/Deposit`이 발생한 프레임의 수거 보상을 무효화
  - 장점: 규칙 문구는 단순하다.
  - 단점: 이미 확정된 수거/디스폰/진행도 반영을 되돌려야 해서 파이프라인 복잡도가 크게 증가한다.

## 결과
- `HazardStack` 상태 변경의 소유권이 플레이어 리스크 owner 단일 경로로 정렬된다.
- 같은 프레임 다중 수거와 `Hit/Deposit` 겹침에서도 결과가 결정론적으로 고정된다.
- `Source 진행도`는 정수 계약을 유지하면서도, `HazardStack` 기반 배율을 후속 프레임에 안정적으로 적용할 수 있다.

## 후속
1. `TD-018` 기준으로 player risk owner와 요청 데이터 구조를 설계/구현한다.
2. `BulletVacuumRequestSystem`의 `HazardCaptured` 경로를 증가 요청 생성 방식으로 전환한다.
3. `Hit/Deposit` 소비 시스템에 reset 요청 경로를 연결한다.
4. EditMode/PlayMode 테스트에 `next-frame apply`와 `수거 확정 후 리셋` 케이스를 추가한다.
