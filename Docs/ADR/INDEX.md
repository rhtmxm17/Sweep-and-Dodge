# ADR Index

## Scope
- 중요한 아키텍처/설계 결정과 그 근거(배경, 대안, 결과)를 기록한다.
- 모든 변경을 ADR로 남기지 않으며, 되돌리기 비용/파급 영향이 큰 결정 중심으로 기록한다.
- 포맷은 파일명 규칙을 중심으로만 필수로 두고, 본문 섹션 구성은 세션 제안(권장)으로 운영한다.

## Documents (Newest First)
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
