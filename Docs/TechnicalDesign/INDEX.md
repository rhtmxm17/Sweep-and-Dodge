# TechnicalDesign Index

## Scope
- 시스템 구조, 이벤트 채널, 데이터 흐름, 업데이트 순서 설계안

## Documents
- [TD-001-player-feedback-event-channel.md](TD-001-player-feedback-event-channel.md): ECS 피드백 이벤트 채널 설계 정리
- [TD-002-pattern-wave-progress-runtime-contract.md](TD-002-pattern-wave-progress-runtime-contract.md): WaveClip runtime buffer/request/consume 계약과 profile-resolved apply 기준
- [TD-003-spawn-directive-model.md](TD-003-spawn-directive-model.md): Wave directive의 `Profile + SourceEmission + Sampling` authoring 모델
- [TD-005-spawn-directive-settings-reference.md](TD-005-spawn-directive-settings-reference.md): superseded. Wave directive 설정 관련 현재 TD 링크
- [TD-006-run-progress-director-design.md](TD-006-run-progress-director-design.md): 런 진행도 디렉터 책임 경계/상태 모델/연동 계약
- [TD-007-common-combat-event-channel.md](TD-007-common-combat-event-channel.md): 공통 전투 이벤트 채널 범위(`Hit/Collect/Cleanup`)와 소유권/집계 계약
- [TD-008-replay-io-persistence-and-version-policy.md](TD-008-replay-io-persistence-and-version-policy.md): Replay 파일 입출력과 `runSeed + tick 입력` 저장 계약, 버전 불일치 즉시 실패 정책
- [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md): 고정 Tick 시간원 도입과 DeltaTime 의존 시스템 단계 치환 계획
- [TD-010-demo-shell-flow-and-bridge-contract.md](TD-010-demo-shell-flow-and-bridge-contract.md): Demo Shell 화면 전이/브리지 단일 접점/씬 재진입(staging) 계약
- [TD-011-runtime-player-hud-contract.md](TD-011-runtime-player-hud-contract.md): 플레이 HUD 스냅샷 writer/read-only 브리지, Source/Hit/Stage 표시 계약
- [TD-012-player-cleanup-action-runtime-contract.md](TD-012-player-cleanup-action-runtime-contract.md): `BroomSweep` 기본 청소 동작, `Trash`/`Hazard` 서브 판정, 활성 중 방향 잠금/이동 제한 계약
- [TD-013-player-feedback-presentation-bridge-contract.md](TD-013-player-feedback-presentation-bridge-contract.md): S4 피드백 소비자 브리지(Animator/HUD feed/Impulse offset)와 dedupe/cooldown 계약
- [TD-014-demo-audio-runtime-contract.md](TD-014-demo-audio-runtime-contract.md): S5 데모 오디오 브리지(버스/큐/옵션/중복 억제) 런타임 계약
- [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](TD-015-stage-map-layout-authoring-and-catalog-pipeline.md): 스테이지 dual catalog 중 layout SSOT를 grid-authoritative schema로 운영하는 authoring/runtime 계약
- [TD-016-runtime-ui-shell-and-navigation-contract.md](TD-016-runtime-ui-shell-and-navigation-contract.md): Runtime UI의 `uGUI` 단일 스택, `RuntimeUiRoot`, presenter/read-only 경계, KB+Mouse 우선 내비게이션 계약
- [TD-017-kb-mouse-input-options-accessibility-baseline.md](TD-017-kb-mouse-input-options-accessibility-baseline.md): 공개 빌드 1차의 `KB+Mouse` 입력 기준, UI 입력/옵션/접근성 최소선, `Input System UI + 기존 gameplay 입력` 분리 계약
- [TD-018-hazardstack-runtime-contract.md](TD-018-hazardstack-runtime-contract.md): HazardStack 단일 owner, 동프레임 `수거 확정 후 리셋`, 다음 프레임 배율 반영 계약
- [TD-020-hint-notification-runtime-contract.md](TD-020-hint-notification-runtime-contract.md): 하단 중앙 `Notification` / `Hint` 2레인의 책임 분리, resolver/seen-state, 재노출 정책, V2 구현 계약
- [TD-021-hazardstack-hud-contract.md](TD-021-hazardstack-hud-contract.md): Carry 인접 `HazardStack` 보조층, `RiskMultiplier` 보조 텍스트, `HazardStackMax` 비표시 HUD 계약
- [TD-022-in-world-dialogue-runtime-contract.md](TD-022-in-world-dialogue-runtime-contract.md): 인월드 연출 대화의 shell owner, `StageStart=overlay`, `StageClear=pre-result clear gate`, `PresentationLayer`/anchor 재사용 계약
- [TD-023-common-gameplay-pause-contract.md](TD-023-common-gameplay-pause-contract.md): `StagePlay` fixed tick authority, `Acquire/Release` 기반 공통 gameplay pause owner, simulation/input/presentation 분리 계약
- [TD-024-stageplay-intervention-dialogue-contract.md](TD-024-stageplay-intervention-dialogue-contract.md): `StagePlay` 도중 특수 조건에서 끼어드는 개입형 인월드 대화의 owner, trigger 우선순위, seen-state, hint/pause 정합성 계약
- [TD-025-stage-player-start-position-contract.md](TD-025-stage-player-start-position-contract.md): 스테이지별 플레이어 시작 위치의 layout 소유, prepare owner, authoring/runtime 적용 계약
- [TD-026-source-pollution-recovery-wave-contract.md](TD-026-source-pollution-recovery-wave-contract.md): `GD-014` 청소 흔적 복구를 `active/inactive + recovery wave` runtime 계약으로 정리
- [TD-027-hazard-bullet-extension-contract.md](TD-027-hazard-bullet-extension-contract.md): superseded. Hazard bullet/lifecycle emission 관련 현재 TD 링크
- [TD-028-hazard-emitter-common-contract.md](TD-028-hazard-emitter-common-contract.md): superseded. HazardActor emit ownership 관련 현재 TD 링크
- [TD-029-discrete-emit-spawn-bridge-contract.md](TD-029-discrete-emit-spawn-bridge-contract.md): `WaveClip` discrete branch와 `HazardActor` 직접 발사를 공통 `DiscreteEmit` 브리지로 묶는 현재 producer/request/update-order 계약
- [TD-030-hazard-actor-hierarchy-and-stage-application.md](TD-030-hazard-actor-hierarchy-and-stage-application.md): 현재 `Source -> HazardActor` hierarchy, actor-owned pattern/emit runtime, stage apply/reset/cleanup 계약
- [TD-031-hazard-actor-behavior-runtime.md](TD-031-hazard-actor-behavior-runtime.md): `Presence + PhaseTransition + PatternSelector + actor-owned emit runtime` 기준의 현재 behavior SSOT
- [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](TD-032-hazard-actor-stage-placement-and-orchestration-framework.md): stage가 actor archetype을 actor-only placement/orchestration seam으로 attach/reset/cleanup 하는 current content-delivery 계약
- [TD-033-emission-profile-common-schema.md](TD-033-emission-profile-common-schema.md): Source/Hazard/Triggered 공통 `EmissionProfileSO` authoring/runtime schema
- [TD-034-stage-map-editor-replacement.md](TD-034-stage-map-editor-replacement.md): 기존 Inspector/Tilemap/Marker 기반 stage authoring 툴을 `StageMapDocument` 중심 실무형 맵 에디터로 대체하는 설계
