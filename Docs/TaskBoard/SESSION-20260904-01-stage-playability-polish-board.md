# SESSION-20260904-01 Stage Playability Polish Board

## Metadata
- doc_id: `SESSION-20260904-01`
- type: `SessionTaskBoard`
- status: `active`
- last_updated: `2026-09-04`
- parent_board: [SESSION-20260814-01 Portfolio Packaging and Notion Board](SESSION-20260814-01-portfolio-packaging-and-notion-board.md)
- related_docs:
  - [GD-017 Demo Stage Level Design Brief](../GameDesign/GD-017-demo-stage-level-design-brief.md)
  - [TD-034 Stage Map Editor Replacement](../TechnicalDesign/TD-034-stage-map-editor-replacement.md)
  - [대량 엔티티 누적 시나리오](../../Portfolio/Validation/large-entity-scenario/README.md)

## Session Goal
- T6 공개 자료 제작 전에 Stage 1~3의 과도한 반복과 명백한 테스트용 콘텐츠를 정리한다.
- 데이터로 판단 가능한 1차 조정과 실제 플레이 감각이 필요한 후속 조정을 분리한다.
- Stage 2의 일반 플레이는 조정하되, 무입력·무청소 상태에서 약 2.4만 Dust가 누적되는 성능 시나리오는 유지한다.

## Approved Scope
- 사용자가 제공한 플레이 관찰과 현재 serialized data를 근거로 목표 수치와 명백한 콘텐츠 연결 오류를 1차 조정한다.
- Actor 배치·밀도·등장 시점, Hazard 보상 점수와 Stage 3 특수 패턴 구성은 플레이 감각 검증 전에는 변경하지 않는다.
- runtime 코드, 전역 Player 설정, Bullet pipeline과 Stage 2의 Source·장애물·Player Start 형상은 변경하지 않는다.

## Evidence Baseline
- Player Carry 용량은 `1,500`이다.
- 약화 Dust clip의 면적당 생성률은 정상 Dust의 `40%`다(`4` 대 `10`). 약화 뒤 청소 흔적이 남아 이동량도 증가하므로 후반 체감 시간이 더 길어진다.
- Stage 2 Source 면적은 `1002 = 537셀`, `1004 = 175셀`로 약 `3.07:1`이다. 기존 완료 목표는 두 Source 모두 `3,200`이었다.
- Stage 2의 `bwc_sus_samples_wave`는 asset description상 다섯 가지 탄환 동작을 순차 확인하는 테스트 웨이브다.
- Stage 2의 `bwc_sus_weakened_hazard`는 약화 후 무작위 RateField와 무한 반복 고정 라인을 함께 생성한다.
- Stage 3은 정상 상태에 Dust sustain이 없고 Hazard sustain만 연결돼 있다. 약화 Dust는 Dust clip이지만 Lane 1에 연결돼 있다.

## Data-grounded First Pass

### Stage 1
- [x] Source `1001`: 약화/완료 목표를 `2,000/4,000`에서 `1,800/2,400`으로 변경했다.
- [x] 배치, Hazard Actor와 웨이브 구성은 유지했다.

### Stage 2
- [x] 대형 Source `1002`: 약화/완료 목표를 `1,600/3,200`에서 `1,600/2,200`으로 변경했다.
- [x] 소형 Source `1004`: 약화/완료 목표를 `1,600/3,200`에서 `600/800`으로 변경했다.
- [x] 두 Source의 정상 Lane 1에서 `bwc_sus_samples_wave` 연결을 제거했다.
- [x] 두 Source의 약화 Lane 1에서 `bwc_sus_weakened_hazard` 연결을 제거했다.
- [x] 정상/약화 Dust, Hazard Actor와 orchestration rule은 유지했다.

목표 총량은 `3,000`으로 Carry 두 회분이다. 대형 Source는 넓은 청소 흔적을 따라 이동해야 하므로 완료 목표/셀을 약 `4.10`으로, 소형 Source는 약 `4.57`로 둔다.

### Stage 3
- [x] Source `1003`: 약화/완료 목표를 `1,800/3,600`에서 `2,100/3,000`으로 변경했다.
- [x] 정상 상태 Lane 0에 `bwc_sus_normal_trash`를 추가했다.
- [x] 약화 상태의 `bwc_sus_weakened_trash` 연결 Lane을 `1`에서 `0`으로 교정했다.
- [x] 기존 정상 Hazard, 약화 이벤트, Actor 배치와 orchestration rule은 유지했다.

## Play-feel Decisions Deferred
- [ ] 공통 Hazard 수거 점수를 위험 대비 의미 있는 수준으로 상향할지 플레이로 판단한다.
- [ ] Stage 3의 두 Actor를 유지할지, 등장 시점을 나눌지, phase 전환을 늦출지 판단한다.
- [ ] Stage 3 Source와 Actor 배치를 넓혀 탄막 밀도를 분산할지 판단한다.
- [ ] 약한 추적탄과 보너스형 탄환을 Stage 3 전용 웨이브로 큐레이션할지 판단한다.
- [ ] Stage별 제한 시간과 최종 목표 수치는 1차 수정 플레이 후 확정한다.

## Stage 2 Performance Contract
- 유지 대상은 Stage 2 asset 파일의 완전한 불변이 아니라 무입력·무청소 누적 경로다.
- Source 셀 형상, 정상 Dust clip과 생성률, 장애물, Player Start, Actor 배치·발사, Bullet lifetime과 runtime pipeline은 이번 1차 조정에서 유지한다.
- 목표 수치는 `CollectedCount == 0`인 방치 경로에 참여하지 않는다.
- 테스트 웨이브 제거로 Hazard 구성은 달라지므로 기존 `Hazard mean 42.2`와 Counter 이미지는 최신 Stage 2 구성 근거가 아니게 된다.
- 최종 튜닝 뒤 무입력 Plateau를 다시 측정해 약 2.4만 Dust 규모를 확인하고 공개 표와 이미지를 갱신한다.

## Verification Plan
- [x] serialized diff가 위 1차 변경에 한정됐는지 확인했다.
- [x] StageMapDocument exporter가 기존 Source binding을 보존하는 계약을 관련 EditMode 테스트로 확인했다.
- [x] Unity Console error 0, 전체 EditMode 테스트와 전용 PlayMode smoke를 확인했다.
- 플레이 감각 검증과 Stage 2 standalone profiling 재측정은 데이터 변경 검증 뒤 별도 단계로 수행한다.

## Work Log
- `2026-09-04`: 플레이 관찰과 asset data를 대조하고 데이터 기반 1차 범위와 플레이 감각 의존 범위를 분리했다.
- `2026-09-04`: `sd_demo_1/2/3`의 목표 수치와 합의된 Sustain binding만 수정했다. StageMapDocument, layout, Actor, BulletDefinition과 runtime 코드는 변경하지 않았다.
- `2026-09-04`: StageCatalog/StageMap 관련 EditMode 25개, 전체 EditMode 533개, 전용 PlayMode smoke 39개가 모두 통과했다. 테스트 종료 후 Console error 0을 확인하고 생성된 임시 validation meta 4개를 제거했다.
