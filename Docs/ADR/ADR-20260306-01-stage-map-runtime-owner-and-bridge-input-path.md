# ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path
> Stage 런타임 적용 Owner를 ExecutionBegin 단일 시스템으로 고정하고, DemoShell StageId 입력 경로를 RunDirectorStageBridge 단일화한 결정

## 배경
- `TD-015` 1차 구현은 StageMap 데이터/에디터 생성/검증 파이프라인만 완료된 상태였다.
- 런타임 적용 경로가 없어서 StageId에 따라 Source/Deposit 레이아웃을 교체할 수 없었고, DemoShell stage 전이와 ECS 맵 적용의 연결도 분리되어 있었다.
- AGENTS.md 원칙상 소유권/업데이트 순서는 명시적으로 단일 책임을 가져야 하며, 중복 writer를 허용하지 않는다.

## 결정
- Stage 런타임 적용 Owner를 `StageCatalogApplyExecutionBeginSystem`으로 고정한다.
  - 위치: `BulletExecutionBeginGroup`
  - 순서: `BulletPoolOwnerBootstrapSystem` 이후, `BulletFieldAreaUpdateSystem` 이전
- 입력 경로를 `RunDirectorStageBridge` 단일화한다.
  - `RequestStageApply(int stageId)`가 `RunDirectorStageRequestComponent`에 one-shot 요청을 기록한다.
  - DemoShell은 ECS 직접 쓰기를 하지 않고 Bridge API만 호출한다.
- 매핑 정책은 `StableId` 엄격 1:1을 채택한다.
  - 런타임 duplicate stable id는 경고 후 해당 키 skip
  - stage 존재 시 미매핑 runtime entity는 자동 비활성화
- 누락 StageId 정책은 “경고 후 계속”으로 둔다.

## 대안
- 대안 A: DemoShell이 ECS(EntityManager)에 직접 StageId/맵 요청 write
  - 장점: 브리지 API 추가가 불필요
  - 단점: GO/ECS 경계가 무너지고 writer 경합 가능성이 증가
  - 기각 사유: 소유권 단일화 원칙 위반
- 대안 B: StageMap 전용 별도 브리지 추가
  - 장점: 기능 분리 명확
  - 단점: Stage 관련 입력 접점이 2개로 분산되어 운영 복잡도 증가
  - 기각 사유: 단일 입력 경로 목표와 충돌
- 대안 C: 인덱스 기반 매핑
  - 장점: authoring stable id 관리 부담 감소
  - 단점: 씬 구조 변경에 취약하고 의도치 않은 매핑 가능성 큼
  - 기각 사유: 안전성/유지보수성 저하

## 결과
- 긍정 효과
  - Stage apply write 경로가 ExecutionBegin 단일 시스템으로 고정되어 파이프라인 해석이 단순해졌다.
  - DemoShell StageId 입력 경로가 Bridge 단일화되어 중복/경합 쓰기 리스크가 줄었다.
  - StageId 누락/duplicate stable id 상황에서 fail-safe(경고 + 안전 스킵/비활성화) 동작을 확보했다.
- 트레이드오프
  - `Obstacle/Visual` 런타임 적용은 다음 페이즈로 이월된다.
  - “경고 후 계속” 정책은 운영 빌드에서 fail-fast보다 느슨하므로 추후 정책 재평가가 필요하다.

## 후속
- 다음 페이즈에서 `Obstacle/Visual` 런타임 소비 Owner를 정의하고, 필요 시 StageId 누락 정책을 빌드 타입별로 분기한다.
