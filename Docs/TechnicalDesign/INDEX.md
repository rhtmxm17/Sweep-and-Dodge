# TechnicalDesign Index

## Scope
- 시스템 구조, 이벤트 채널, 데이터 흐름, 업데이트 순서 설계안

## Documents
- [TD-001-player-feedback-event-channel.md](TD-001-player-feedback-event-channel.md): ECS 피드백 이벤트 채널 설계 정리
- [TD-002-pattern-wave-progress-runtime-contract.md](TD-002-pattern-wave-progress-runtime-contract.md): Pattern/Wave/Progress 런타임 데이터/수식/검증 계약
- [TD-003-spawn-directive-model.md](TD-003-spawn-directive-model.md): SpawnDirective 분해 모델(Sampling/Emission/Payload)과 모드 조합 규칙
- [TD-005-spawn-directive-settings-reference.md](TD-005-spawn-directive-settings-reference.md): WaveClipSO SpawnEntry 인라인 프로필 설정 의미/운영 규칙 레퍼런스
- [TD-006-run-progress-director-design.md](TD-006-run-progress-director-design.md): 런 진행도 디렉터 책임 경계/상태 모델/연동 계약
- [TD-007-common-combat-event-channel.md](TD-007-common-combat-event-channel.md): 공통 전투 이벤트 채널 범위(`Hit/Collect/Cleanup`)와 소유권/집계 계약
- [TD-008-replay-io-persistence-and-version-policy.md](TD-008-replay-io-persistence-and-version-policy.md): Replay 파일 입출력과 `runSeed + tick 입력` 저장 계약, 버전 불일치 즉시 실패 정책
- [TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md](TD-009-fixed-tick-time-source-and-deltatime-replacement-plan.md): 고정 Tick 시간원 도입과 DeltaTime 의존 시스템 단계 치환 계획
- [TD-010-demo-shell-flow-and-bridge-contract.md](TD-010-demo-shell-flow-and-bridge-contract.md): Demo Shell 화면 전이/브리지 단일 접점/씬 재진입(staging) 계약
- [TD-011-runtime-player-hud-contract.md](TD-011-runtime-player-hud-contract.md): 플레이 HUD 스냅샷 writer/read-only 브리지, Source/Hit/Stage 표시 계약
- [TD-012-player-cleanup-action-runtime-contract.md](TD-012-player-cleanup-action-runtime-contract.md): 플레이어 청소 액션 분기/슬롯 매핑/활성 중 입력 소비 계약
- [TD-013-player-feedback-presentation-bridge-contract.md](TD-013-player-feedback-presentation-bridge-contract.md): S4 피드백 소비자 브리지(Animator/HUD feed/Impulse offset)와 dedupe/cooldown 계약
- [TD-014-demo-audio-runtime-contract.md](TD-014-demo-audio-runtime-contract.md): S5 데모 오디오 브리지(버스/큐/옵션/중복 억제) 런타임 계약
- [TD-016-runtime-ui-shell-and-navigation-contract.md](TD-016-runtime-ui-shell-and-navigation-contract.md): Runtime UI의 `uGUI` 단일 스택, `RuntimeUiRoot`, presenter/read-only 경계, KB+Mouse 우선 내비게이션 계약
- [TD-017-kb-mouse-input-options-accessibility-baseline.md](TD-017-kb-mouse-input-options-accessibility-baseline.md): 공개 빌드 1차의 `KB+Mouse` 입력 기준, UI 입력/옵션/접근성 최소선, `Input System UI + 기존 gameplay 입력` 분리 계약
- [TD-018-hazardstack-runtime-contract.md](TD-018-hazardstack-runtime-contract.md): HazardStack 단일 owner, 동프레임 `수거 확정 후 리셋`, 다음 프레임 배율 반영 계약
- [TD-020-hint-notification-runtime-contract.md](TD-020-hint-notification-runtime-contract.md): 하단 중앙 `Notification` / `Hint` 2레인의 책임 분리, resolver/seen-state, 재노출 정책, V2 구현 계약
- [TD-021-hazardstack-hud-contract.md](TD-021-hazardstack-hud-contract.md): Carry 인접 `HazardStack` 보조층, `RiskMultiplier` 보조 텍스트, `HazardStackMax` 비표시 HUD 계약
