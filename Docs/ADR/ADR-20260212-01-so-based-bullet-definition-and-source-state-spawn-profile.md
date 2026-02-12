# ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile
> Bullet/BulletSource 설정을 ScriptableObject + ECS Buffer 변환 방식으로 전환한 결정

## 배경
- 기존 구조는 `BulletVisualPrefabAuthoring`의 인라인 배열(`BulletPoolEntryAuthoring`)과 `BulletSourceAuthoring`의 개별 수치 필드(`SpawnRateNormal`, hazard ratio 등)에 의존했다.
- 레벨 디자인/밸런싱 반복 시 다음 문제가 있었다.
  - 탄환 데이터 재사용성이 낮고, Source별 상태 전환(통상/약화/고갈) 구성이 분산됨
  - Source별 상태별 조합이 복잡해질수록 인스펙터 단일 MonoBehaviour 필드가 비대해짐
  - DOTS 런타임(Burst Job)에서 SO 직접 접근은 적합하지 않음

## 결정
- Bullet 정의는 `BulletDefinitionSO`로 관리하고, Bake 시 `BulletPoolDefinitionBuffer`로 변환한다.
- Source 스폰 정책은 `BulletSourceProfileSO`에서 상태별 엔트리 배열로 관리하고, Bake 시 다음 ECS 버퍼로 변환한다.
  - `SourceSpawnPatternBuffer` : 상태/탄환/스폰 모드/속도 설정
  - `SourceActiveBulletCountBuffer` : Source-탄환별 활성수 카운트
- 스폰 모드는 2종으로 분리한다.
  - `FixedRate`
  - `CapAndMaxRate` (동시 활성수 상한 + 최대 스폰 속도)
- DOTS 소유권 원칙을 유지한다.
  - 활성수 증가는 `BulletSpawnFromPoolSystem`에서 처리
  - 활성수 감소는 `BulletDespawnExecutionSystem`에서 디스폰 실행과 함께 처리

## 대안
- 기존 Authoring 필드 확장(상태별 배열을 MonoBehaviour에 직접 추가)
  - 장점: 코드 변경량이 적다.
  - 단점: 데이터 재사용성/프리셋 관리가 약하고, Source 수가 늘수록 유지보수 비용이 증가한다.
- SO를 런타임에서 직접 참조
  - 장점: 구현이 단순해 보인다.
  - 단점: Burst/ECS Job 경로와 맞지 않고, 데이터 접근 일관성이 약해진다.

## 결과
- 장점
  - 레벨 디자이너가 Bullet 정의를 독립 자산으로 재사용 가능해짐
  - Source 상태별 스폰 테이블 구성과 튜닝이 명확해짐
  - 런타임은 ECS 버퍼만 사용하므로 Burst 친화성을 유지
- 리스크
  - Source별 활성수 카운트 업데이트 경로가 추가되어 실행 경로가 복잡해짐
  - SO/프로파일 참조 누락 시 스폰이 0이 될 수 있어 에디터 검증 보완이 필요함

## 후속
- 에디터 검증 추가
  - 중복 `DefinitionId`와 미할당 프로파일 경고 강화
  - `CapAndMaxRate`에서 `MaxActive == 0`인 엔트리 검증
- 필요 시 Source별 스폰 엔트리 우선순위/가중치 정책 확장


