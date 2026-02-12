# ADR-20260212-area-density-based-spawn-and-field-shapes
> Source 스폰 정책을 면적 밀도 기준으로 전환하고 BulletField 형태를 원형/사각형으로 확장한 결정

## 배경
- 기존 Source 스폰 데이터는 `SpawnRatePerSec`, `MaxActive` 같은 절대치 기반이었다.
- 동일 프로파일을 사용해도 Source 영역 크기(`Radius`)가 달라지면 체감 밀도가 크게 흔들려 레벨 디자인 반복 비용이 높았다.
- 테스트/기획 단계에서 최소 2종의 필드 형태(원형/사각형)가 필요했다.

## 결정
- Source 스폰률을 절대치에서 면적 밀도 기준으로 변경한다.
  - `SpawnDensityPerSecPerArea` (면적당 초당 스폰 수)
  - `MaxActiveDensityPerArea` (면적당 최대 동시 활성 수)
- Source 영역은 `BulletFieldAreaComponent`로 분리한다.
  - `Shape`: `Circle`, `Rectangle`
  - `Radius`(원형), `Size`(사각형), `ComputedArea`(런타임 캐시)
- `BulletFieldAreaUpdateSystem`이 스폰 직전(ExecutionBegin) 영역 면적을 갱신한다.
- `BulletSpawnFromPoolSystem`은 면적을 읽어 스폰/캡 수량을 계산한다.
  - `spawn = density * area * dt + accumulator`
  - `maxActive = floor(maxActiveDensity * area)` (Cap 모드)
- 스폰 모드 enum 명칭을 의미에 맞게 정리한다.
  - `FixedDensity`
  - `CapAndMaxDensity`

## 대안
- 기존 절대치(`SpawnRatePerSec`, `MaxActive`) 유지 + 디자이너 수동 보정
  - 장점: 코드 변경량이 적다.
  - 단점: 영역 크기 변경 시 난이도 일관성을 유지하기 어렵다.
- Source별 보정 계수만 추가
  - 장점: 기존 자산 호환이 쉽다.
  - 단점: 근본 단위가 절대치라 데이터 의미가 모호해지고 튜닝 복잡도가 높다.

## 결과
- 장점
  - 동일 프로파일의 체감 밀도를 영역 크기와 무관하게 일관되게 유지할 수 있다.
  - 원형/사각형 필드를 기획 의도에 맞게 즉시 선택 가능하다.
- 리스크
  - 기존 SO 값 해석이 바뀌므로 밸런싱 데이터 재튜닝이 필요하다.
  - Rectangle 회전 대응은 현재 Gizmo만 반영되며, 스폰은 로컬 축 기준 사각형 샘플링이다.

## 후속
- 에디터 검증 강화
  - `CapAndMaxDensity`에서 `MaxActiveDensityPerArea <= 0` 경고
  - `Rectangle`에서 `Size.x == 0` 또는 `Size.y == 0` 경고
- 디버그 표시 개선
  - Source Gizmo에 면적/예상 초당 스폰량 표시
