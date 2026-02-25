# ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness
> SpawnDirective v2 계약(레거시 제거, LineEven 중심 샘플링, 방향/버스트/우선순위 규약)을 확정해 샘플 시나리오 성립 기준을 고정한다

## 상태
- 반영됨

## 배경
- 스폰 모델이 `Sampling × Emission × Direction × Payload`로 확장되면서, 이행기 호환 경로와 정책 비활성 기능(`WallEven`)이 동시에 존재해 데이터/코드 의미가 분산됐다.
- 샘플 시나리오(초기/전환/전환후 3구간)를 안정적으로 재현하려면, “어떤 설정이 실제로 동작하는지”를 단일 계약으로 확정할 필요가 있다.
- 기존 파이프라인 소유권(`ExecutionBegin -> Simulation -> Request -> ExecutionEnd`)은 유지해야 한다.

## 결정
1. SpawnEntry 레거시 경로 제거
- `WaveTimelineSO.SpawnEntry`는 인라인 프로필(`Payload/Emission/Sampling/Direction`)만 사용한다.
- `UseDirectiveProfiles` 및 legacy emission fallback 필드/로직은 제거한다.

2. Sampling 1차 범위 확정
- `SamplingMode` 1차 유효 범위는 `UniformField`, `PollutionTopK`, `LineEven`, `PointSet(계약만)`으로 고정한다.
- `WallEven` 및 전용 데이터(`WallMask`, `WallInset`)는 계약/런타임/검증에서 제거한다.
- “벽 발사” 표현은 `LineEven + Direction` 조합으로 통일한다.

3. Direction/Emission 실행 규약 확정
- Direction 최종 계산은 ExecutionBegin 소비 시점에서 수행한다.
- `NWay`와 `RadialBurst`는 공통 슬롯 분배 로직으로 통합한다.
- `EventBurst`는 `carry` 소비 정책을 유지한다(미소비 샷 이월).

4. 예산/우선순위 규약 확정
- 프레임 예산은 탄종 공용 풀에서 공유한다.
- 우선순위는 요청 단위(`DirectiveId`)에서 적용하며, Trash(`StandardCollectible`)는 최하 우선순위로 처리한다.

5. 검증 계약 동기화
- `WallEven` 관련 검증(`CV025`, `CVW034`)은 제거한다.
- 유효 계약 검증은 `CV020~CV024`, `CV026`, `CVW032`, `CVW033` 중심으로 유지한다.

## 대안
- 대안 A: WallEven을 정책 비활성 상태로만 유지
  - 장점: 과거 데이터 호환 경로가 남는다.
  - 단점: “쓰면 안 되지만 존재하는 설정”이 계속 남아 authoring 혼동이 누적된다.
- 대안 B: 레거시 fallback 유지
  - 장점: 마이그레이션 중간 단계에서 편하다.
  - 단점: 런타임 계약이 이중화되어 테스트/디버깅 비용이 커진다.

## 결과
- 스폰 데이터 의미가 단일 계약으로 수렴되어 authoring/검증/런타임 해석이 일치한다.
- 샘플 시나리오 구성 시 “벽 발사”를 별도 모드가 아닌 라인 배치 + 방향 지정으로 일관되게 표현할 수 있다.
- 리스크: 과거 에셋의 잔여 직렬화 키는 무시되지만, 장기적으로는 재저장 정리가 필요하다.

## 후속
- 샘플 시나리오용 WaveTimeline authoring 프리셋(초기/전환/전환후)을 데이터 자산으로 확정한다.
- PlayMode 시나리오 스모크를 추가해 `EventBurst(0.2s x 3)`, `LineEven + Direction`, `Trash 저우선순위` 회귀를 자동 검증한다.
