# ADR-20260226-01-pointset-runtime-sampler-max4-local-offset
> PointSet 샘플링을 런타임 활성화하고, 최대 4개 로컬 오프셋 포인트 계약으로 고정한 결정

## 상태
- 반영됨

## 배경
- `PointSet`이 계약/enum 수준으로만 존재하고 ExecutionBegin 샘플러에서 `UniformField` fallback으로 처리되어, "특정 위치 3곳에서 동시에 소용돌이 탄막" 시나리오를 재현할 수 없었다.
- Sampling/Direction 최종 평가는 ExecutionBegin owner 단계에서 수행하는 기존 파이프라인 원칙을 유지해야 한다.

## 결정
1. PointSet 런타임 활성화
- `SamplingMode=PointSet`일 때 지정 포인트셋에서 직접 위치를 샘플링한다.
- 좌표계는 `CenterMode`로 해석된 중심 기준의 로컬 오프셋만 허용한다(월드 절대 포인트 미지원).

2. 포인트 수 상한 고정
- `PointCount` 최대값을 `4`로 고정한다.
- Authoring/Buffer 계약은 `PointCount + Point0..Point3` 고정 필드로 유지한다.

3. 샘플/방향 시퀀스 규약
- 위치 샘플은 `SpawnSequence % PointCount` round-robin으로 선택한다.
- `PointSet + Spiral/NWay/RadialBurst` 조합에서는 방향 계산에 포인트별 로컬 시퀀스를 사용한다.
  - `localSequence = SpawnSequence / PointCount`

4. 검증 규칙 갱신
- `CV028`(Error): `PointSet`에서 `PointCount <= 0`
- `CVW033`(Warning): `PointCount > 4` 입력은 clamp 경고

## 대안
- 동적 길이 Point 버퍼(`DynamicBuffer`) 기반 설계
  - 장점: 포인트 수 확장 유연성
  - 단점: Request/Execution 전달 계약 및 테스트 복잡도 증가, owner 경계 관리 비용 증가
- 월드 절대 포인트 지원
  - 장점: 일부 authoring 편의
  - 단점: `CenterMode` 체계와 의미 중복, 설정 해석 분기 증가

## 결과
- 3지점 동시 소용돌이 패턴이 PointSet 단일 계약으로 재현 가능해진다.
- 고정 상한 설계로 런타임 데이터 레이아웃과 요청 집계 경로를 단순하게 유지한다.
- 후속 확장(4개 초과)이 필요해지면 PointSet 자산화(`PointSetAsset + Id`)를 별도 ADR로 검토한다.
