# ADR-20260211-01-source-based-spawn-and-depletion
> 탄환 스폰을 전역 랜덤에서 Source 독립 스폰 + 수거 기반 고갈(통상/약화/고갈)로 전환

## 상태
- 반영됨

## 배경
- 기존 프로토타입은 전역 랜덤 스폰이라, 플레이어가 한 지점에 머물러도 자원 수급이 지속되는 문제가 있었다.
- 공간 청소 체감(청소된 영역이 실제로 비어 보이는 상태)을 만들기 위해 Source 중심 스폰과 고갈 상태 전이가 필요했다.
- 기존 파이프라인(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`) 소유권을 깨지 않는 방향이 요구되었다.
- Source 스폰 Job에서 Source 위치를 `LocalTransform`(RO)로 읽고, 탄환 위치를 `ComponentLookup<LocalTransform>`(RW)로 쓰는 조합은 Entities aliasing 제약과 충돌할 수 있다.

## 결정
- Source 상태 단계를 `Normal / Weakened / Depleted` 3단계로 고정한다.
- 임계치 기준은 외부 입력 가능한 형태로 유지한다(Authoring 초기값 주입 가능).
- Source 선택 스폰 분배는 도입하지 않고, Source별 독립 스폰 루프로 처리한다.

## 구현 메모
- `SourceSpawnComponent`, `SourceSpawnRuntimeComponent`를 추가해 Source 고정 설정과 런타임 스폰 누적치를 분리했다.
- Source 위치 읽기 경로를 `SourceAnchorComponent`로 분리해 `LocalTransform` RO/RW aliasing 충돌을 회피했다.
- `BulletSpawnFromPoolSystem`은 Source 엔티티를 순회하여 반경 내부에서만 탄환을 스폰한다.
- 탄환에 `BulletSourceRefComponent`를 저장해 출처 Source를 기록한다.
- `BulletVacuumRequestSystem`에서 새로 요청된 탄환에 한해 Source별 `CollectedCount`를 누적하고 상태를 전이한다.
- Lifetime 만료 디스폰은 Request 단계의 수거 누적에 포함되지 않는다.

## 대안
- 전역 예산을 Source에 분배하는 방식
  - 장점: 전체 발사량 제어가 단순
  - 단점: Source 독립성을 약화하고, Source별 체감 밀도 설계 의도와 충돌
- 시간 경과 기반 자동 회복
  - 장점: 장기 루프 유지 용이
  - 단점: "청소 완료" 체감을 약화하고 제자리 파밍 재발 가능

## 결과
- 스폰의 공간적 의미가 강화되고, Source 고갈이 플레이어 이동 동기를 만든다.
- 기존 Owner/Fence 구조를 유지해 풀/디스폰 소유권이 깨지지 않는다.
- Source 파라미터(반경, 스폰율, 임계치)를 외부에서 조정 가능해 후속 실험 비용이 낮다.

## 후속
- Play Mode에서 Source별 고갈 전이 시점과 밀도 체감 확인.
- Entities Profiler로 Source 수 증가 시 `ExecutionBegin` 비용 변화 측정.
- Source 이동이 필요해지면 `SourceAnchorComponent` 갱신 시스템을 추가하거나, 스폰을 2단계(요청 생성/소비)로 분리하는 확장을 검토한다.


