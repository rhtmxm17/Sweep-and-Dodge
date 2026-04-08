# SESSION-20260407-01

## Metadata
- doc_id: `SESSION-20260407-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-04-08`
- related_docs:
  - [../GameDesign/GD-015-hazard-emitter-design.md](../GameDesign/GD-015-hazard-emitter-design.md)
  - [../GameDesign/GD-006-hazard-conditional-capture-system.md](../GameDesign/GD-006-hazard-conditional-capture-system.md)
  - [../GameDesign/GD-002-source-based-spawn-and-depletion.md](../GameDesign/GD-002-source-based-spawn-and-depletion.md)

## Session Goal
- 한 줄 목표: `HazardEmitter`를 플레이 감정 중심 GD에서 구조 설계 대상으로 전환하고, `DiscreteEmit` 브리지 기준의 구현 작업까지 이어질 수 있도록 실행 단위를 고정한다.
- 완료 기준: 구조 축, 공통 계약 논의 순서, 기존 spawn 구조 영향 검토 시점, 구현 분해 진입 조건이 세션 보드 기준으로 흔들리지 않게 정리된다.
- 완료 기준 추가:
  - `T1~T3` 설계 문서와 ADR이 SSOT로 닫혀 있다.
  - `T4`에서 구현 범위가 Codex 플랜 모드 실행 단위로 분해되어, 각 단위가 owner/update-order/검증 기준과 함께 착수 가능한 상태다.
- 이번 세션의 작업 목표:
  - `Plan A` `DiscreteEmit` schema/channel skeleton
  - `Plan B` source discrete branch extraction
  - `Plan C` `DiscreteEmitExecutionSystem` 도입
  - `Plan D` `HazardEmitter` runtime path 연결
  - `Plan E` integration/metrics/문서 마감
- 이번 세션에서 하지 않을 것: 스테이지별 실배치 확정, 수치 밸런싱 확정, `RotatingSet coordinator` owner 최종 확정, `AnchorRef` wire shape 최종 확정, `SourceRelative` consume semantics 구현 완료

## Now
- [ ] Plan E. integration, metrics, 문서 마감
  - 완료 기준: source discrete branch와 emitter branch가 공통 `DiscreteEmit` 경로에서 통합되고 최소 backlog/metrics 및 문서 차이가 정리된다.
  - 검증: compile, console error 0, EditMode 통합 회귀, PlayMode smoke
- [ ] E2. `HazardEmitterCoordinatorSystem` 계약과 runtime orchestration 구현 계획을 고정한다.
  - 목표: `HazardEmitter`를 source의 부속 데이터가 아니라 source 소속의 runtime 위험 주체로 보고, source state 외 입력까지 포함한 activation orchestration owner를 분리한다.
  - 현재 합의:
    - 명칭은 `HazardEmitterCoordinatorSystem`
    - 입력 축은 `source state` 외에 `player distance`까지 열어 둔다.
    - coordinator는 activation/suppression gate만 결정하고, telegraph/cooldown/emit append는 계속 `HazardEmitterEmitBuildSystem`이 소유한다.
    - 최소 gate 축은 `HazardEmitterSourcePressureGate`, `HazardEmitterPlayerDistanceGate`, `HazardEmitterSourceProgressGate` 조합으로 본다.
    - 예시 규칙 1: source `Pressure` 상태 + 최소 hold 시간 + player distance 범위가 모두 만족될 때 활성화
    - 예시 규칙 2: `CollectedCount / ThresholdDepleted` 기반 `progress01`이 지정 구간에 들어오면 활성화
    - coordinator state 최소 출력은 `ActivationAllowed`, `SuppressionReasonMask`, `LastPlayerDistanceSq`다.
    - suppression reason mask 1차 축은 `DisabledByAppliedConfig`, `SuppressedByAppliedConfig`, `MissingSource`, `SourcePressureBlocked`, `SourceProgressBlocked`, `PlayerDistanceBlocked`, `MissingPlayer`, `GroupSuppressed`다.
  - 구현 방향:
    - `HazardEmitterCoordinatorStateComponent` 또는 동등한 runtime gate state 추가
    - `HazardEmitterEmitBuildSystem`은 baked/applied config 대신 coordinator 결과를 읽어 상태기계를 진행
    - 첫 단계 입력은 `stage-applied enable/suppression`, `source state`, `player distance`, `source progress01`로 제한
    - 권장 update order는 `RunProgressDirectorSystem` 이후, `HazardEmitterEmitBuildSystem` 이전이다.
  - 검증 기준: source 상태와 플레이어 거리 변화가 emit build ownership을 오염시키지 않고 emitter activation gate에 반영된다.

## Next
- [ ] E3. `HazardEmitterBinding`을 반영하는 TD/ADR/TaskBoard 차이 정리를 먼저 수행한다.
  - 이유: 현재 `TD-028/029`는 emitter 공통 계약과 discrete emit bridge까지만 닫혀 있고, stage-applied emitter override seam은 아직 문서 SSOT가 아니다.
- [ ] E4. `HazardEmitterBinding`과 `HazardEmitterCoordinatorSystem`의 구현 선후를 정한다.
  - 현재 판단: `HazardEmitterBinding`이 먼저, `HazardEmitterCoordinatorSystem`이 다음이다.
  - 이유: coordinator는 stage-applied baseline이 먼저 있어야 입력 합성 책임을 깔끔하게 가져갈 수 있다.
- [ ] E5. `HazardEmitterCoordinatorSystem`의 update order와 suppression reason mask를 확정한다.
  - 이유: `Pressure/player distance/progress` gate를 실제 구현으로 옮기려면 source/director state update 이후, emit build 이전의 정확한 write owner 경계가 먼저 닫혀야 한다.

## Blocked
- 없음

## Parking Lot
- [ ] P1. `PlacementIntent`별 콘텐츠 배치 규칙과 스테이지 실배치안은 구조 설계 고정 후 별도 단계에서 정리한다.
  - 근거: 현재는 기능 구조와 spawn 경계가 우선이며, 목적 기준 상세화는 런타임 타입 설계를 흐릴 수 있다.
- [ ] P2. `DynamicObject` 계열의 실제 움직임 규칙과 전용 전조 연출은 공통 계약과 spawn 구조가 정리된 뒤 후속 논의로 미룬다.
  - 근거: 확장 가능성은 고려하되, 현재 세션에서 필요한 것은 확장 슬롯 확보이지 구체 구현안 확정이 아니다.
- [ ] P3. 구현 slice별 테스트/검증 계획 상세는 설계 확정 후 별도 문서 또는 후속 업데이트에서 채운다.
  - 근거: 현재 시점의 구현 분해는 `Codex 플랜 모드` 착수 기준까지만 필요하고, fixture/scene/metric threshold의 세부값은 구현 세션에서 채운다.

## Done
- [x] D1. `GD-015` 문서 포맷을 최근 GD 문서 형식에 맞춰 정리했다.
  - 검증 결과: 메타데이터, 요약 블록, 적용 범위/비범위, 후속 논의 섹션이 정리되었고 기존 기획 내용은 유지됐다.
- [x] D2. 구조 설계의 핵심축은 `배치 목적 기준`이 아니라 `유형 기준`으로 간다는 방향을 합의했다.
  - 검증 결과: `PlacementIntent`는 콘텐츠/레벨디자인 축으로 분리하고, 구조 설계는 작동 방식 기준으로 다루기로 정리됐다.
- [x] D3. 현재 `GD-015`의 유형 분류에는 `Form`과 `행동 정책`이 섞여 있다는 점을 식별했다.
  - 검증 결과: `고정 오브젝트형/국소 지점형`은 형태 축, `상태 변화형/순회·간헐 활성형`은 활성 정책 축으로 재해석해야 한다는 기준이 정리됐다.
- [x] D4. 향후 `비고정 오브젝트 Form` 추가 가능성을 전제로 권장안을 보정했다.
  - 검증 결과: 현재 지원 범위는 `FixedObject`, `LocalPoint`로 두되, 구조 체계는 확장 가능한 `Form/Anchor/Mobility` 축으로 열어 두는 방향이 합의됐다.
- [x] D5. 기존 spawn 구조 변경 논의는 공통 계약의 최소 범위가 보이는 즉시 이어서 진행해야 한다는 순서를 정리했다.
  - 검증 결과: 공통 계약 전체를 끝까지 세부 확정한 뒤가 아니라, `Emitter`의 출력 경계가 보이는 시점에 spawn 구조 논의로 들어가야 한다는 작업 순서가 합의됐다.
- [x] D6. `HazardEmitter` 최소 공통 계약을 `TD-028` 초안으로 고정했다.
  - 검증 결과: `ActivationPolicy` 4종, 공통 상태기계 `Dormant -> Telegraph -> Emit -> Cooldown`, `ProfileRef` 중심 표현, `Emit 1회 request append` 출력 경계가 기술 문서 기준으로 정리됐다.
- [x] D7. `HazardEmitter`와 `WaveClip EventBurst/Poisson`를 공통 `DiscreteEmit` 브리지로 내리는 `T2` 구조를 `TD-029` 초안으로 고정했다.
  - 검증 결과: `SourceClipDiscreteEmitBuildSystem`, `HazardEmitterEmitBuildSystem`, `DiscreteEmitExecutionSystem`의 ownership과 `DiscreteEmitRequest/Seed` 경계, `ExecutionBegin` 순서, budget 분리 기준이 기술 문서 기준으로 정리됐다.
- [x] D8. `T2`의 ownership/update-order 분리 결정을 ADR로 승격하고 `T3` 문서 반영 범위를 정리했다.
  - 검증 결과: `ADR-20260407-01`에 `DiscreteEmit` 브리지 채택, producer/execution ownership 분리, `ExecutionBegin` 순서, budget 분리 기준이 기록됐고 관련 TD와 인덱스가 연결됐다.
- [x] D9. `T4` 구현 범위를 Codex 플랜 모드 실행 단위로 분해했다.
  - 검증 결과:
    - `Plan A` `DiscreteEmit` schema/channel skeleton
    - `Plan B` source discrete branch extraction
    - `Plan C` `DiscreteEmitExecutionSystem` 도입
    - `Plan D` `HazardEmitter` runtime path 연결
    - `Plan E` integration/metrics/문서 마감
    로 선후 관계와 검증 루프가 정리됐다.
- [x] D10. Plan A. `DiscreteEmit` schema/channel skeleton을 구현하고 검증했다.
  - 검증 결과:
    - `DiscreteEmitRequestBuffer`, `DiscreteEmitPolicyComponent`, `DiscreteEmitBacklogMetricsComponent`, `DiscreteEmitChannelSingletonTag`가 runtime schema에 추가됐다.
    - `DiscreteEmitRequestSeed`와 `CreateDiscreteEmitRequest(in DiscreteEmitRequestSeed seed, uint frame)` helper가 추가됐다.
    - `BulletPoolOwnerBootstrapSystem`가 `DiscreteEmit` singleton buffer/policy/metrics를 보장하도록 확장됐다.
    - `EditMode 442/442`, `PlayMode 43/43`, console error 0을 통과했다.
- [x] D11. Plan B. source discrete branch extraction을 compat bridge로 구현하고 검증했다.
  - 검증 결과:
    - `SourceClipDiscreteEmitBuildSystem`가 `EventBurst + Poisson`의 event lifecycle과 discrete occurrence 해석을 소유하도록 분리됐다.
    - 기존 `SourceClipRequestBuildSystem`는 sustain/ratefield path와 `SpawnBacklogMetricsComponent` 최종 집계 owner로 축소됐다.
    - `SourceSustainRuntimeComponent.ActiveState` writer는 discrete branch로 이동했고, 기존 source path는 `SourceEventRuntimeComponent.IsPlaying`을 read-only gate로 사용한다.
    - Plan C 전까지는 accepted discrete occurrence를 legacy `SourceSpawnRequestBuffer`로 mirror하는 compat bridge를 유지한다.
    - compat bridge 제거 시점은 `Plan C` 또는 마감 단계다.
    - `EditMode 447/447`, `PlayMode 43/43`, console error 0 기준으로 회귀 없이 통과했다.
- [x] D12. Plan C. `DiscreteEmitExecutionSystem`을 도입하고 source discrete hard cutover를 완료했다.
  - 검증 결과:
    - `SourceClipDiscreteEmitBuildSystem`가 legacy `SourceSpawnRequestBuffer` mirror를 제거하고 `DiscreteEmitRequestBuffer` append로 전환됐다.
    - wave discrete branch의 anchor sampling은 producer 단계에서 fixed-world anchor로 resolve되고, deterministic random은 source runtime 상태 없이 pure hash로 고정됐다.
    - `DiscreteEmitExecutionSystem`가 `ExecutionBegin`에서 `SecondarySpawnExecutionSystem` 다음, `SpawnRequestRoundRobinExecutionSystem` 앞의 dedicated consumer로 추가됐다.
    - `DiscreteEmit` backlog/policy/metrics는 bullet-equivalent 기준으로 집계되고, repeat atomic consume / priority arbitration / budget gate / pool gate가 분리됐다.
    - legacy `SourceClipRequestBuildSystem`는 sustain/ratefield owner를 유지하면서 discrete pending을 cap 계산에 포함하도록 보정됐다.
    - `EditMode 452/452`, `PlayMode 43/43`, console error 0 기준으로 통과했다.
- [x] D13. Plan D. `HazardEmitter` runtime path를 `DiscreteEmit` producer로 연결했다.
  - 검증 결과:
    - `HazardEmitterAuthoring`, `HazardEmitterTelegraphProfileSO`, `HazardEmitterEmissionProfileSO`가 추가됐고, source child authoring 기준 bake seam이 도입됐다.
    - `HazardEmitterComponent` 계열 runtime config/state와 `HazardEmitterEmitBuildSystem`이 추가되어 `AlwaysCycle + Telegraph -> Emit -> Cooldown` 최소 상태기계가 동작한다.
    - emitter는 direct spawn하지 않고 `BuildDiscreteEmitSeedFromEmitter(...) -> CreateDiscreteEmitRequest(...)` 경로로 `DiscreteEmitRequestBuffer`를 append한다.
    - emitter end-to-end를 확인하는 PlayMode smoke가 추가됐다.
    - `EditMode 457/457`, `PlayMode 44/44`, console error 0 기준으로 통과했다.
- [x] D14. `HazardEmitter` 샘플을 source template 경로에 연결해 sample runtime 경로를 정적 구성 기준으로 정리했다.
  - 검증 결과:
    - sample topology catalog는 emitter child가 포함된 source template prefab을 참조하도록 조정됐다.
    - sample emitter는 telegraph/emission profile asset 2개만 추가하는 최소 자산 경로로 구성됐다.
    - sample verification PlayMode 테스트는 `HazardEmitter` entity 생성과 `Cooldown` 진입을 관측하도록 보강됐다.
    - Unity MCP/batchmode 제약으로 자동 compile/EditMode/PlayMode 재검증은 이번 세션에서 완료하지 못했고, 정적 참조 경로와 테스트 코드까지 반영된 상태로 남아 있다.
- [x] E1. `HazardEmitterBinding` stage-applied seam을 구현하고 검증했다.
  - 검증 결과:
    - `HazardEmitter` runtime 데이터가 `structural identity / baked baseline / applied snapshot / runtime mutable`로 분리됐다.
    - `StageSourceBinding.HazardEmitterBindings[]`, `SourceHazardEmitterRefBuffer`, `StageTopologyApplyPrepareSystem`의 emitter apply/reset 경로가 추가됐다.
    - `HazardEmitterEmitBuildSystem`은 applied snapshot만 읽도록 전환됐다.
    - `StageDefinitionGenerator`는 새 필드를 빈 배열 기본값으로 유지한다.
    - `EditMode 460/460`, `PlayMode 44/44` 통과, console error 추가 없음 기준으로 회귀 없이 통과했다.

## End of Session
- 결과: 진행 중
- 남은 리스크: `RotatingSet coordinator` owner, `AnchorRef` wire shape, `SourceRelative` consume semantics, `HazardEmitterCoordinatorSystem` 구현 세부와 stage-applied gate 확장 여부는 후속 확정이 필요하다.
- 다음 세션 시작점: `E2` coordinator/runtime orchestration 구현 계획 고정 후 `E5` update order/reason mask를 코드 수준으로 닫는다.
