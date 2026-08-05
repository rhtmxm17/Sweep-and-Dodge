# ADR-20260805-01 HazardActor Workbench and Preview Ownership
> `HazardActorAuthoring` prefab을 행동 SSOT로 유지하면서 전용 Workbench와 `StageMapDocument` 기반 Encounter 편집, 비권위 분석형 Preview Core를 채택하는 결정

## Metadata
- doc_id: `ADR-20260805-01`
- type: `ArchitectureDecisionRecord`
- status: `accepted`
- date: `2026-08-05`
- related_docs:
  - [../TechnicalDesign/TD-035-hazard-actor-authoring-workbench-and-preview.md](../TechnicalDesign/TD-035-hazard-actor-authoring-workbench-and-preview.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [../TechnicalDesign/TD-034-stage-map-editor-replacement.md](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md](ADR-20260804-01-stage-map-editor-document-ssot-and-legacy-replacement.md)

## 배경
- 현재 HazardActor 행동은 actor prefab의 `HazardActorAuthoring`, 공유 `EmissionProfileSO`, telegraph profile에 나뉘어 있다.
- stage별 차이는 `StageMapDocument`의 placement와 `StageDefinitionSO`의 source-local orchestration rule에 분리되어 있다.
- 데이터 책임 자체는 유효하지만 기본 Inspector의 중첩 배열만으로는 Phase, selector, pattern, telegraph, emit, cooldown의 실제 결과를 한눈에 이해하기 어렵다.
- 배치 시점에도 위치와 archetype 참조만 보이며, 실제 위치와 yaw에서 어떤 탄막과 phase 전환이 발생하는지 확인하려면 Play Mode에 진입해야 한다.
- 실무형 제작 흐름에는 행동 구조를 보여주는 전용 편집기, 선택한 배치의 즉시 미리보기, stage orchestration과 함께 보는 encounter 미리보기가 필요하다.

## 결정
- HazardActor 행동 authoring SSOT는 기존 `HazardActorAuthoring` prefab으로 유지한다.
  - 신규 editor-only Blueprint/Document를 추가하지 않는다.
  - `EmissionProfileSO`와 telegraph profile의 기존 공유 소유권도 유지한다.
- `HazardActor Workbench`를 actor behavior의 공식 사용자-facing 편집 표면으로 채택한다.
  - 기존 HazardActor Inspector는 read-only 요약, validation 상태, `Open Workbench` 진입점만 제공하는 legacy/debug 경로로 격하한다.
  - actor와 공유 profile의 편집은 Workbench에서 `SerializedObject + Undo + dirty`로 즉시 반영한다.
- stage별 HazardActor placement와 orchestration authoring SSOT는 `StageMapDocument`로 통합한다.
  - rule identity는 `OwningSourceStableId + RuleId`다.
  - target identity는 `OwningSourceStableId + PlacementInstanceId`다.
  - `StageDefinitionSO`는 document export 결과이며 신규 편집 SSOT가 아니다.
- Workbench와 Stage Map Editor는 동일한 editor-only 분석형 Preview Core를 공유한다.
  - preview state, time, progress, target, 표시 설정은 transient session data다.
  - preview는 runtime 또는 authoring SSOT가 아니며 asset을 자동 수정하지 않는다.
- v1 preview는 runtime authoring resolver와 동일한 resolved data를 입력으로 사용하는 고정-step simulator로 구현한다.
  - 격리된 ECS Preview World는 정확도 검증용 후속 기능으로 둔다.
  - Play Mode preview는 최종 runtime smoke에만 사용한다.
- Workbench는 UI Toolkit 기반으로 구현하고, 범용 노드 그래프 대신 현재 runtime 구조에 대응하는 Phase/Pattern 상태 차트와 timing timeline을 사용한다.
- Stage Map Editor에는 source별 Encounter Track과 Scene View preview transport를 통합한다.

## 대안
- 대안 A: 신규 `HazardActorBlueprintDocument`를 편집 SSOT로 만들고 prefab을 생성 산출물로 둔다.
  - 장점: pure data asset, migration, diff/apply 흐름을 독립적으로 설계할 수 있다.
  - 단점: blueprint, generated prefab, visual prefab 사이의 새로운 동기화와 identity 계약이 필요하다.
  - 기각 사유: 현재 prefab은 baker가 직접 소비하는 완결된 archetype 단위이며, 이번 문제는 데이터 저장 형식보다 편집·가시화 표면의 부재가 핵심이다.
- 대안 B: 기존 Inspector를 확장하고 별도 Workbench를 만들지 않는다.
  - 장점: 구현 표면이 작고 기존 SerializedProperty 흐름을 그대로 쓸 수 있다.
  - 단점: Phase/Pattern 관계, timing, preview, issue navigation을 중첩 배열 Inspector 안에서 통합하기 어렵다.
  - 기각 사유: 제작자가 실제 행동을 시각적으로 조립하고 검증하는 작업 모델을 제공하지 못한다.
- 대안 C: v1부터 격리된 ECS World에서 실제 runtime system을 실행한다.
  - 장점: runtime과 가장 가까운 결과를 제공한다.
  - 단점: Source progress, Player target, singleton, system order, preview entity와 World 수명주기를 Editor가 별도로 구성해야 한다.
  - 기각 사유: 선택 즉시 반응하는 저부하 편집 미리보기의 안정성과 수명주기 비용에 맞지 않는다.
- 대안 D: orchestration은 `StageDefinitionSO`에 남기고 Stage Map Editor는 읽기 전용 preview만 제공한다.
  - 장점: StageMapDocument schema 변경이 없다.
  - 단점: placement와 rule이 다시 다른 SSOT에 남고, 신규 editor만으로 encounter 제작을 완료할 수 없다.
  - 기각 사유: `StageMapDocument` 중심 사용자-facing authoring 원칙과 충돌한다.

## 결과
- 긍정 효과
  - runtime/baker 계약을 유지하면서 HazardActor 제작 경로를 전용 Workbench로 교체할 수 있다.
  - actor behavior, 실제 stage 배치, source orchestration을 각 소유권에 맞게 편집하면서 하나의 Preview Core로 결과를 확인할 수 있다.
  - preview 조작이 asset dirty나 runtime World에 영향을 주지 않는다.
- 트레이드오프
  - 분석형 simulator가 지원하는 기능은 runtime resolver와 계약 테스트로 동기화해야 한다.
  - `StageMapDocument` schema migration과 기존 `StageDefinitionSO` rule의 명시적 import가 필요하다.
  - 공유 `EmissionProfileSO` 편집은 사용처 가시성과 명시적 복제 동작을 제공해야 한다.
- 후속
  - 구현 기준과 지원/비지원 preview 범위는 `TD-035`를 따른다.
  - runtime behavior/update order는 `TD-031`, placement/runtime orchestration은 `TD-032`를 따른다.
  - ECS Sandbox 정밀 preview, actor motion/path authoring, 임의 조건 graph는 후속 설계로 둔다.
