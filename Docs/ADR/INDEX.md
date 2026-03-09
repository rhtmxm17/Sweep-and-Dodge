# ADR Index

## Scope
- 중요한 아키텍처/설계 결정과 그 근거(배경, 대안, 결과)를 기록한다.
- 모든 변경을 ADR로 남기지 않으며, 되돌리기 비용/파급 영향이 큰 결정 중심으로 기록한다.
- 포맷은 파일명 규칙을 중심으로만 필수로 두고, 본문 섹션 구성은 세션 제안(권장)으로 운영한다.

## Documents (Newest First)
- [ADR-20260309-02-stage-session-reset-and-prepare-owner.md](ADR-20260309-02-stage-session-reset-and-prepare-owner.md): 씬 리로드/Retry/Next 재진입에서 world 재생성 대신 prepare 계층의 explicit stage session reset owner를 채택
- [ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md](ADR-20260308-01-stage-topology-lifecycle-and-failure-policy.md): 장주기 스테이지를 위한 StageTopology lifecycle/failure policy와 boundary-only apply 계약 고정
- [ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md](ADR-20260306-02-dual-catalog-definition-layout-explicit-pair-entry.md): 스테이지 정의/레이아웃 분리와 StageCatalog 명시적 페어 엔트리 채택
- [ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md](ADR-20260306-01-stage-map-runtime-owner-and-bridge-input-path.md): StageMap 런타임 적용 Owner를 ExecutionBegin 단일 시스템으로 고정하고 DemoShell StageId 입력 경로를 RunDirectorStageBridge로 단일화
- [ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md](ADR-20260303-04-fixed-tick-time-source-for-replay-determinism.md): 리플레이/결정론 품질을 위한 고정 Tick 시간원 전환 결정(가변 DeltaTime 의존 축소)
- [ADR-20260303-03-replay-persistence-and-schema-compatibility-policy.md](ADR-20260303-03-replay-persistence-and-schema-compatibility-policy.md): 리플레이 persistence 도입과 구버전 완전 거부 기반 schema compatibility 정책 고정
- [ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md](ADR-20260303-02-player-ecs-authority-and-presentation-bridge-for-replay.md): 플레이어 런타임 권한을 ECS로 이전하고 GameObject를 입력 수집/표현 소비 경계로 고정
- [ADR-20260303-01-replay-min-foundation-and-seed-unification.md](ADR-20260303-01-replay-min-foundation-and-seed-unification.md): OPS-001 #10 최소 범위(동일 머신/동일 빌드) 고정 + 입력 프레임 스냅샷 + run seed 계열 RNG 일원화 결정
- [ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup.md](ADR-20260228-02-common-combat-event-channel-hit-collect-cleanup.md): 공통 전투 이벤트 채널 범위를 `Hit/Collect/Cleanup`으로 고정하고 `PlayerHazardHit`를 공통 채널 경유로 이관
- [ADR-20260227-01-run-progress-director-runtime-ownership-and-pressure-policy.md](ADR-20260227-01-run-progress-director-runtime-ownership-and-pressure-policy.md): 런 진행도 디렉터 책임 이관, Pressure(영향권 점유+유지시간) 정책, 상태별 출력 규칙 고정
- [ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md](ADR-20260226-03-eventburst-intra-timeline-and-event-anchor-fixation.md): 지속 사건형 스폰의 Emission 책임 확장(Poisson/EventBurst 공통)과 이벤트 기준점 고정 계약
- [ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md](ADR-20260226-02-nway-set-atomicity-and-emission-unit-contract.md): NWay 세트 원자성/시퀀스 보존과 밀도형·사건형 발행 단위 분리 계약
- [ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md](ADR-20260226-01-pointset-runtime-sampler-max4-local-offset.md): PointSet 런타임 샘플러 활성화와 `Max=4` 로컬 오프셋 포인트 계약 고정
- [ADR-20260225-02-wave-clip-slot-channel-contract.md](ADR-20260225-02-wave-clip-slot-channel-contract.md): WaveClipSO 기반 슬롯/채널 계약(하드 프리엠션, sustain 체인, Lane 우선순위, 결정론 RNG) 확정
- [ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness.md](ADR-20260225-01-spawn-directive-v2-contract-and-scenario-readiness.md): SpawnDirective v2 계약(레거시 제거, LineEven 중심 샘플링, 방향/버스트/우선순위 규약) 확정
- [ADR-20260220-03-ecs-file-splitting-boundaries-by-ownership.md](ADR-20260220-03-ecs-file-splitting-boundaries-by-ownership.md): ECS 파일 분리 기준을 소유권/업데이트 단계/응집도 중심으로 고정
- [ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md](ADR-20260220-02-spawn-request-aggregation-and-budgeted-carry-over.md): Spawn 요청 aggregated 단위와 Budget Cap + bounded carry-over 정책 결정
- [ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md](ADR-20260220-01-bullet-frame-pipeline-root-and-frame-counter.md): 루트 파이프라인 그룹 도입 + Request fence publish 단일화 + 프레임 카운터 기반 Frame ID 전환
- [ADR-20260219-06-cleaning-trail-request-owner-and-fast-sampling.md](ADR-20260219-06-cleaning-trail-request-owner-and-fast-sampling.md): 청소 흔적 도입의 Request 단일 writer, Top-K 샘플링, 원형 Valid 마스크 결정
- [ADR-20260219-05-carrybin-deposit-touch-request-execution.md](ADR-20260219-05-carrybin-deposit-touch-request-execution.md): CarryBin 내려놓기 접촉 기반 Request-Execution 파이프라인 결정
- [ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md](ADR-20260219-04-player-input-action-slot-mapping-and-active-input-consume.md): 입력 슬롯 매핑과 동작 중 입력 소비 정책 결정
- [ADR-20260219-03-player-cleanup-action-profile-so-externalization.md](ADR-20260219-03-player-cleanup-action-profile-so-externalization.md): 플레이어 청소 행동 프로파일의 SO 외부화 결정
- [ADR-20260219-02-cleanup-action-branching-by-profile.md](ADR-20260219-02-cleanup-action-branching-by-profile.md): 청소 행동 분기를 선택 상태 + 프로파일 구조로 분리한 결정
- [ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md](ADR-20260219-01-player-feedback-event-channels-by-consumer-boundary.md): 소비자 경계(UI/Impulse) 기반 피드백 이벤트 채널 분리 결정
- [ADR-20260212-04-carrybin-replaces-score-placeholder.md](ADR-20260212-04-carrybin-replaces-score-placeholder.md): Score 플레이스홀더를 CarryBin 파이프라인으로 대체한 결정
- [ADR-20260212-03-player-hazard-collision-request-consume.md](ADR-20260212-03-player-hazard-collision-request-consume.md): 위험탄 충돌의 태그/CellMap 분리 + Request-Execution 통합 결정
- [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](ADR-20260212-02-area-density-based-spawn-and-field-shapes.md): 면적 밀도 기반 스폰 정책과 필드 형태 확장(원형/사각형) 결정
- [ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md](ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md): Bullet/Source 설정의 SO + ECS Buffer 변환 방식 결정
- [ADR-20260211-02-bullet-type-key-pool-set.md](ADR-20260211-02-bullet-type-key-pool-set.md): Bullet Type Key 기반 풀 세트 구조 결정
- [ADR-20260211-01-source-based-spawn-and-depletion.md](ADR-20260211-01-source-based-spawn-and-depletion.md): Source 기반 스폰/고갈 메커니즘 도입 결정
- [ADR-20260210-01-bullet-active-filtering-and-despawn-request.md](ADR-20260210-01-bullet-active-filtering-and-despawn-request.md): 비활성 탄환 시뮬레이션 필터링 + 디스폰 요청 파이프라인 결정
- [ADR-20260209-01-bullet-render-parts-buffe.md](ADR-20260209-01-bullet-render-parts-buffe.md): 다중 렌더 파츠 버퍼 기반 토글 구조 결정
- [ADR-20260206-01-bullet-pipeline-ownership.md](ADR-20260206-01-bullet-pipeline-ownership.md): 탄환 파이프라인 소유권/업데이트 순서 기준 결정


