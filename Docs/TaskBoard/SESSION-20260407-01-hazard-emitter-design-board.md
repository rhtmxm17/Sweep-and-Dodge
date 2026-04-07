# SESSION-20260407-01

## Metadata
- doc_id: `SESSION-20260407-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-04-07`
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
- [ ] Plan A. `DiscreteEmit` schema/channel skeleton
  - 완료 기준: `DiscreteEmitRequestBuffer`, channel singleton, seed/helper seam, 최소 metrics/budget config type의 코드 진입점이 추가된다.
  - 검증: compile, console error 0, EditMode request 기본값/anchor mode contract 테스트

## Next
- [ ] Plan B. source discrete branch extraction
  - 완료 기준: `EventBurst + Poisson` discrete branch가 `SourceClipDiscreteEmitBuildSystem`로 분리되고, sustain/ratefield는 기존 source path에 남는다.
  - 검증: compile, console error 0, existing source event regression EditMode, PlayMode smoke
- [ ] Plan C. `DiscreteEmitExecutionSystem` 도입
  - 완료 기준: `DiscreteEmitRequestBuffer` consumer, arbitration, repeat atomic consume, budget gate가 `ExecutionBegin` 경계에 도입된다.
  - 검증: compile, console error 0, EditMode repeat consume/no-merge/budget defer 테스트, PlayMode smoke
- [ ] Plan D. `HazardEmitter` runtime path 연결
  - 완료 기준: emitter 최소 runtime/state와 `HazardEmitterEmitBuildSystem`가 `DiscreteEmit` producer로 연결된다.
  - 검증: compile, console error 0, EditMode state machine/cooldown/telegraph zero-duration 테스트, PlayMode smoke
- [ ] Plan E. integration, metrics, 문서 마감
  - 완료 기준: source discrete branch와 emitter branch가 공통 `DiscreteEmit` 경로에서 통합되고 최소 backlog/metrics 및 문서 차이가 정리된다.
  - 검증: compile, console error 0, EditMode 통합 회귀, PlayMode smoke

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

## End of Session
- 결과: 진행 중
- 남은 리스크: `RotatingSet coordinator` owner, `AnchorRef` wire shape, `SourceRelative` consume semantics는 후속 확정이 필요하다.
- 다음 세션 시작점: `Plan A` `DiscreteEmit` schema/channel skeleton 구현 세션 착수
