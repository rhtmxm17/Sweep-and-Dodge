# SESSION-20260417-01

## Metadata
- doc_id: `SESSION-20260417-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-04-17`
- related_docs:
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../ADR/ADR-20260413-01-hazard-actor-stage-placement-and-orchestration.md](../ADR/ADR-20260413-01-hazard-actor-stage-placement-and-orchestration.md)
  - [../ADR/ADR-20260415-01-orchestration-rule-under-source-binding.md](../ADR/ADR-20260415-01-orchestration-rule-under-source-binding.md)
  - [./SESSION-20260413-01-hazard-actor-stage-placement-board.md](./SESSION-20260413-01-hazard-actor-stage-placement-board.md)

## Session Goal
- 한 줄 목표: HazardActor 씬 마커 기반 툴링을 완성하고, 데이터 관계가 직관적으로 드러나지 않는 영역을 발굴하여 개선 후보를 우선순위 기준으로 정리한다.
- 완료 기준:
  - `StageHazardActorMarker` / `HazardActorSourceAuthoringMarker` 마커 MonoBehaviour 2종 구현 완료
  - `StageDefinitionGenerator`가 마커에서 placement/orchestration 데이터를 수집하여 `StageDefinitionSO`에 반영
  - `SeedSourceHazardActorOrchestration`이 다중 타겟 규칙을 seed time에 단일 타겟 버퍼 항목으로 확장
  - `StageLayoutEditingSampleV1.unity` Stage01에 HazardActor 샘플 배치 완료
  - 이번 세션에서 발굴된 개선 후보가 우선순위와 함께 Parking Lot에 기록

## Inherited Context
- `SESSION-20260413-01` 기준 placement/orchestration 프레임(`HazardActorPlacements + HazardActorOrchestrationRules`)을 구현/검증 완료한 상태에서 시작했다.
- `StageDefinitionSO`에 placement/orchestration 데이터를 직접 Inspector에서 입력하는 방식은 작업 편의성이 낮다는 문제가 식별되어 있었다.
- 씬 오브젝트 Transform 위치를 LocalOffset으로 자동 수집하는 마커 기반 툴링이 필요하다는 설계 방향이 이미 합의된 상태였다.

## Now
- 없음

## Next
- 없음

## Parking Lot

- [x] P1. **[HIGH]** Phase→Pattern 연결 투명화
  - 해결: Emitter 계층 제거로 근본 문제 해소. `PhaseSelectorPolicies[].Candidates[].PatternSlotId`가 Actor 직접 소유 `PatternSlots[]` 내 슬롯을 1단계로 참조한다.
  - 변경: `HazardEmitterAuthoring` 제거, `HazardActorAuthoring.PatternSlots[]` 도입, `EmitterId` 개념 전면 제거

- [ ] P3. **[MEDIUM]** TargetPhaseId 드롭다운/유효성
  - 문제: `HazardActorSourceAuthoringMarker.Rules[].TargetPhaseId`에 정수를 직접 입력하는데, 해당 `ActorArchetypePrefab`의 `HazardActorArchetypeAuthoring`이 정의하는 유효 Phase 목록이 편집 시점에 보이지 않는다. 범위 밖 PhaseId를 입력해도 편집기 수준에서 피드백이 없다.
  - 개선 방향: `HazardActorOrchestrationRuleBinding` PropertyDrawer에서 `ActorArchetypePrefab`을 참조하여 유효 PhaseId 드롭다운을 제공. 참조 불가 시 정수 입력 폴백.
  - 근거: 이번 세션 탐색 중 식별된 편집 시점 유효성 피드백 결여

- [ ] P4. **[LOW]** `ProgressThresholdNormalized` 소스 진행률 맥락 명시
  - 문제: `HazardActorOrchestrationRuleBinding.TriggerThresholdNormalized` (또는 PhaseProgressTransition의 `ProgressThresholdNormalized`)가 "어떤 메트릭의 0..1 정규화 값인가"(총 누적 피해량 대비 ThresholdDepleted 비율 등)가 코드 주석이나 Tooltip 없이는 명확하지 않다.
  - 개선 방향: 필드에 `[Tooltip]` 어트리뷰트 또는 `summary` XML 주석으로 메트릭 정의를 명시. 데이터/로직 변경 없음.
  - 근거: 이번 세션 탐색 중 식별된 암묵적 단위 정의 결여

## Done

- [x] D1. `HazardActorOrchestrationRuleBinding` 다중 타겟 스키마 변경
  - `TargetPlacementInstanceId` (int 필드) → `TargetPlacementInstanceIds` (int[]) 변경
  - backward-compat computed property(`TargetPlacementInstanceId` get/set) 추가 — Unity는 필드만 직렬화하므로 런타임 호환성 영향 없음
  - 기존 테스트 4종은 computed property setter를 통해 그대로 컴파일/동작

- [x] D2. `StageHazardActorMarker` MonoBehaviour 신규 생성
  - `[DisallowMultipleComponent]`, 필드: `[Min(1)] PlacementInstanceId`, `ActorArchetypePrefab`, `LocalYawDeg`
  - 배치 방법: 소스 오브젝트(`SourceRuntimeTemplateAuthoringBase` 부착 GameObject)의 자식 GameObject에 부착; Transform 월드 위치가 LocalOffset 입력 소스

- [x] D3. `HazardActorSourceAuthoringMarker` MonoBehaviour 신규 생성
  - `[DisallowMultipleComponent]`, 필드: `Rules[]`(`HazardActorOrchestrationRuleBinding[]`)
  - 배치 방법: `SourceRuntimeTemplateAuthoringBase`와 동일 GameObject에 sibling component로 부착

- [x] D4. `StageDefinitionGenerator` 마커 기반 데이터 수집 추가
  - `BuildHazardActorPlacements`: `authoring.GetComponentsInChildren<StageHazardActorMarker>()`, `LocalOffset = marker.transform.position - authoring.transform.position`
  - `BuildHazardActorOrchestrationRules`: `authoring.GetComponent<HazardActorSourceAuthoringMarker>()`, `marker.Rules` 직접 반환
  - `BuildBindingFromAuthoring`에서 두 헬퍼 연결

- [x] D5. `SeedSourceHazardActorOrchestration` 다중 타겟 확장
  - 단일 `TargetPlacementInstanceId` 참조 → `int[] TargetPlacementInstanceIds` 루프로 전환
  - seed time에 autoRuleId(1..N) 자동 할당으로 N개 단일 타겟 버퍼 항목 생성
  - 런타임 `SourceHazardActorOrchestrationRuleBuffer` 구조 및 `HazardActorOrchestrationSystems` 변경 없음

- [x] D6. `StageCatalogValidationRules` STC040 다중 타겟 검증으로 업데이트
  - null/empty `TargetPlacementInstanceIds` 감지 추가
  - `BuildOrchestrationConflictKey` 시그니처에 `targetPlacementInstanceId` 명시적 파라미터 추가
  - STC040~STC045 검증을 targetId 루프 단위로 재구성

- [x] D7. `StageLayoutEditingSampleV1.unity` Stage01 HazardActor 샘플 배치
  - `Source_1001`에 `HazardActorSourceAuthoringMarker` 추가(Rules×2: Spawn/OnStageStart, PhaseSet@0.6)
  - 자식 `HazardActor_Placement_1`에 `StageHazardActorMarker` 추가(id=1, prefab=pf_stage_hazard_actor_archetype, yaw=0)
  - `sd_demo_1.asset`을 Generator로 재생성하여 마커 기반 데이터 반영 확인

- [x] D8. 데이터 관계 가시성 개선 후보 탐색 및 우선순위 분류
  - Phase→Pattern 연결(HIGH), SustainSlot 구간 표시(HIGH), TargetPhaseId 유효성(MEDIUM), ProgressThreshold 맥락(LOW) 4건 식별

- [x] D9. `SourceRuntimeTemplateAuthoringBase` SustainSlot 상태 구간 요약 Inspector 추가
  - `SourceRuntimeTemplateAuthoringBase` 전용 Custom Inspector를 추가하고 `SourceRuntimeTemplateAuthoring` / legacy `BulletSourceAuthoring` 둘 다 child class 대상으로 적용
  - `ThresholdWeakened` / `ThresholdDepleted` / `InitialState` 아래에 `Normal/Weakened/Depleted` 3행 고정 구간 요약을 read-only로 렌더링
  - 구간 계산은 runtime/generator와 동일하게 `effectiveWeakened = max(0, ThresholdWeakened)`, `effectiveDepleted = max(effectiveWeakened, ThresholdDepleted)` 규칙을 사용
  - 상태별 slot summary는 authored `SustainClipSlots[]` 배열 인덱스를 lane 기준으로 묶어 표시하고, 빈 상태는 `none`으로 유지
  - EditMode 테스트 `SourceRuntimeTemplateAuthoringEditorSummaryUtilityTests` 5건 추가 및 통과 확인

- [x] D10. HazardEmitter 계층 제거 — HazardActor 직접 발사 구조 전환 (세션 외 구현)
  - `HazardEmitterAuthoring` / Emitter 런타임 컴포넌트 / 시스템 경로 전면 제거
  - `HazardActorAuthoring.PatternSlots[]` 직접 소유, `HazardActorEmitSystem` 신규 도입
  - `HazardActorComponents`, `HazardActorPatternSelectorSystem`, `StageTopologyTemplateFactory`, `StageTopologyApplyPrepareSystem`, `StageTopologyBridge`, 프리팹/테스트 픽스처 일괄 전환
  - `DiscreteEmitProducerKind.HazardEmitter` → `HazardActor` 정리 포함
  - 영향 파일: 34개 (6,456줄 삭제, 598줄 추가)

## End of Session
- 결과: 마커 기반 HazardActor 툴링 구현 완료 + HazardEmitter 계층 제거로 Phase→Pattern 1단계 참조 구조 전환 완료. P2(SustainSlot 구간 표시) 구현/검증 완료. Parking Lot 잔여: P3(TargetPhaseId 드롭다운), P4(ProgressThreshold Tooltip).
- 검증: 세션 내 EditMode/PlayMode 통과 확인. D10 구현은 외부 수행 후 점검 완료.
- 다음 세션 시작점: P3(TargetPhaseId 드롭다운/유효성) PropertyDrawer 구현.
