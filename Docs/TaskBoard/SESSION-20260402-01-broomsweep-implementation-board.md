# SESSION-20260402-01

## Metadata
- doc_id: `SESSION-20260402-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-04-03`
- related_docs:
  - [../TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md](../TechnicalDesign/TD-012-player-cleanup-action-runtime-contract.md)
  - [../GameDesign/GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [../ADR/ADR-20260402-01-broomsweep-default-cleanup-action.md](../ADR/ADR-20260402-01-broomsweep-default-cleanup-action.md)

## Session Goal
- 한 줄 목표: `BroomSweep` 변경안을 코드에 반영하되, 구현 범위를 `단일 기본 액션 + 활성 중 방향 잠금/이동 감속 + Trash/Hazard 판정 교체`로 고정한다.
- 완료 기준:
  - `BroomSweep`가 기본 청소 동작으로 동작한다.
  - `Trash`는 스윕 부채꼴 판정, `Hazard`는 정면 직사각형 타이밍 판정으로 분리된다.
  - 활성 중 방향 잠금과 이동 감속이 설정 기반으로 적용된다.
  - compile / console error 0 / EditMode / PlayMode smoke를 통과한다.
- 이번 세션에서 하지 않을 것:
  - 신규 청소 액션 추가
  - 슬롯 UI/연출 구조 재설계
  - VFX/사운드 최종 튜닝
  - 스윕 easing, 정면 판정 지속형 여부 같은 밸런싱 확장 결정

## Now
- 없음

## Next
- 없음

## Blocked
- 없음

## Parking Lot
- [ ] P1. 슬롯 UI를 단일 기본 액션 기준으로 축소할지 여부는 구현 1차 후 재판단한다.
  - 근거: 현재 합의 범위는 런타임 동작 고정이지 UI 구조 재설계가 아니다.
- [ ] P2. 스윕 easing, 정면 판정 지속 창, 정면 타이밍 표시 강도는 구현 1차 후 체감 확인 뒤 튜닝한다.
  - 근거: 현재 문서에는 권장 범위만 있고 최종 밸런스 결정은 없다.
- [ ] P3. `RadialRing`, `ForwardFanLine`의 완전 삭제 여부는 구현 1차 후 호환 비용을 보고 판단한다.
  - 근거: 이번 범위는 "기본 경로에서 내린다"까지이며, 즉시 완전 삭제는 필수 합의가 아니다.

## Done
- [x] D1. `BroomSweep` 방향으로 GameDesign/TechnicalDesign/ADR 문서를 갱신했다.
  - 검증 결과: `GD-006`, `TD-012`, `ADR-20260402-01`이 같은 기준(`Trash` 부채꼴, `Hazard` 정면 직사각형, 활성 중 방향 잠금/이동 감속)으로 정렬되었다.
- [x] D2. 구현 범위를 4단계로 분해했다.
  - 검증 결과: 계약 뼈대 -> 이동/방향 제약 -> 판정 교체 -> 레거시 정리/검증 순서가 세션 합의로 고정되었다.
- [x] D3. T1. `BroomSweep` 데이터 계약 뼈대와 기본 asset/fallback 정리를 완료했다.
  - 변경 결과:
    - `PlayerCleanupActionId`에 `BroomSweep`를 추가했다.
    - profile buffer에 broom 전용 필드와 sanitize/fallback 유틸리티를 추가했다.
    - `PlayerCleanupSweepRuntimeStateComponent`, `PlayerCleanupMotionConstraintConfigComponent`를 추가했다.
    - `PlayerCleanupActionSetSO`, `PlayerProxyAuthoring`, `pas_default.asset`를 `BroomSweep` 기본값 기준으로 정리했다.
    - `PlayerCleanupActionSelectSystem`, `BulletVacuumRequestSystem`에 T1 shim normalize/fallback 경로를 추가했다.
    - 계약/기본값 회귀를 검증하는 EditMode 테스트를 추가했다.
  - 검증 결과:
    - Unity compile 요청 후 console error 0
    - EditMode 전체 376/376 통과
    - PlayMode smoke 1/1 통과
- [x] D4. T2. `BroomSweep` 활성 중 방향 잠금/이동 감속을 완료했다.
  - 변경 결과:
    - `PlayerIntentMovementSystem`에 `BroomSweep` 활성 중 이동 감속과 방향 잠금 적용을 추가했다.
    - `BulletVacuumRequestSystem`에 activation-edge 기준 전방 스냅샷 기록과 비활성 시 lock state 초기화를 추가했다.
    - `StageSessionResetPrepareSystem`에 `VacuumRuntimeStateComponent`, `PlayerCleanupSweepRuntimeStateComponent` reset을 추가했다.
    - `PlayerIntentMovementSystem` 단위 테스트를 추가하고, vacuum/stage reset 계약 테스트를 확장했다.
    - vacuum 시스템을 직접 호출하는 legacy fixture들에 sweep/config component를 보강했다.
  - 검증 결과:
    - Unity compile 요청 후 console error 0
    - EditMode 전체 383/383 통과
    - PlayMode smoke 1/1 통과
- [x] D5. T3. `BulletVacuumRequestSystem`를 `BroomSweep` 진행률 기반 판정으로 교체했다.
  - 변경 결과:
    - `BroomSweep` 활성 진입 edge에서 `ActiveSweepDirectionSign`이 `NextSweepDirectionSign`을 소비하고, 다음 스윕 방향을 반전하도록 런타임 상태 소비를 확정했다.
    - `BulletVacuumRequestSystem`가 `Linear` 진행률 기반 스윕 중심각을 계산하고, `Trash`를 환형 부채꼴 띠 판정으로 처리하도록 교체했다.
    - `Hazard`를 정면 직사각형 + 짧은 각도 창 판정으로 교체했고, `HasLockedFacing` 또는 `ActiveSweepDirectionSign`이 유효하지 않으면 캡처를 실패시키도록 했다.
    - vacuum 계약 테스트와 smoke/stress 테스트를 `BroomSweep` 기본 fixture와 기하 기준으로 갱신했다.
  - 검증 결과:
    - Unity compile 요청 후 console error 0
    - EditMode 전체 386/386 통과
    - PlayMode smoke 통과
    - 테스트 임시 산출물 `__GeneratedStageCatalogValidation*.meta` 정리 완료
- [x] D6. T4. 레거시 기본 경로 정리와 회귀 검증을 완료했다.
  - 변경 결과:
    - 기본 경로 보호용 smoke fixture에 `BroomSweep` 기본값 계약 테스트를 추가했다.
    - `PlayerCleanupActionComponents`, `PlayerCleanupActionSetSO`의 legacy 주석을 현재 운영 기준에 맞게 정리했다.
    - `BulletCollectedSecondaryReactionTests`와 대응 PlayMode fixture에 legacy compatibility 의도를 주석/헬퍼 이름으로 명시했다.
    - legacy PlayMode fixture에도 `PlayerCleanupSweepRuntimeStateComponent`, `PlayerCleanupMotionConstraintConfigComponent`를 보강해 현재 vacuum 계약과 맞췄다.
    - `TD-012`, `GD-006`을 "기본 경로는 BroomSweep, legacy는 compatibility layer" 기준으로 정렬했다.
  - 검증 결과:
    - Unity compile 요청 후 console error 0
    - EditMode 전체 통과
    - PlayMode smoke 통과
    - 테스트 임시 산출물 정리 완료

## End of Session
- 결과: `BroomSweep` 기본 경로 고정, legacy compatibility fixture 구분, 문서/테스트 의미 정렬까지 완료했다.
- 남은 리스크: `RadialRing`/`ForwardFanLine` 완전 삭제 여부는 별도 범위로 다시 판단해야 한다.
- 다음 세션 시작점: 후속 밸런싱 또는 legacy 삭제 여부 판단
