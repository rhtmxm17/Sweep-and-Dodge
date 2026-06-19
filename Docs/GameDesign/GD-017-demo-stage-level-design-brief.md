# Demo Stage Level Design Brief

## Metadata
- doc_id: `GD-017`
- type: `GameDesign`
- status: `draft`
- last_updated: `2026-05-18`
- related_docs:
  - [GD-008-demo-flow-design.md](./GD-008-demo-flow-design.md)
  - [GD-016-hazard-actor-blueprint-scenarios.md](./GD-016-hazard-actor-blueprint-scenarios.md)
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](../TechnicalDesign/TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [TD-032-hazard-actor-stage-placement-and-orchestration-framework.md](../TechnicalDesign/TD-032-hazard-actor-stage-placement-and-orchestration-framework.md)
  - [SESSION-20260514-01-portfolio-demo-build-board.md](../TaskBoard/SESSION-20260514-01-portfolio-demo-build-board.md)

> 공개 포트폴리오 데모의 Stage 1~3이 각각 학습, 성능/동선 선택, 최종 쇼케이스 역할을 맡도록 Source / Deposit / HazardActor 배치 의도를 정리하는 초안이다. 실제 `StageLayoutSO` / `StageDefinitionSO` 편집은 후속 T1c에서 수행한다.

## 1. 목적
- 공개 데모의 3개 스테이지가 서로 다른 역할을 갖도록 레벨 디자인 기준을 고정한다.
- Stage 2의 분리된 Source 영역을 별도 Source로 다루고, 각 Source에 다른 타입의 actor를 배치한다.
- Stage 3은 기존 작은 샘플 구조를 유지하지 않고 공개 쇼케이스용으로 사실상 신규 작성한다.
- T1c 편집자가 임의로 역할과 난이도 곡선을 재해석하지 않도록 체크리스트를 제공한다.

## 2. 적용 범위
- Stage 1~3의 플레이어 경험 역할
- Source / Deposit / player start / obstacle 동선 의도
- HazardActor 배치와 패턴 타입 의도
- 실패 유도 지점과 완화 장치
- 후속 asset 편집 체크리스트

## 3. 비범위
- ECS runtime owner, update order, Fence, enableable 규칙 변경
- `StageTopologyApplyPrepareSystem` 같은 런타임 시스템 구조 변경
- 최종 수치 튜닝, 좌표, 셀 개수, threshold 값 확정
- serialized asset 값을 exact assert로 보호하는 테스트 작성
- 최종 VFX/SFX/아트 품질 확정

## 4. 공통 설계 원칙
- 공개 데모 흐름은 `GD-008`의 `Title -> Lobby -> Stage Play -> Stage Result -> Demo Complete`를 따른다.
- `StageLayoutSO`의 grid / source region / deposit region이 layout SSOT다.
- `StageDefinitionSO.SourceBindings`는 source region stable id와 source별 패턴/actor 구성을 연결한다.
- Stage는 actor placement / orchestration 의도를 소유하지만, actor runtime lifecycle owner는 기존 source 계약을 유지한다.
- obstacle gameplay는 grid movement flag로만 다룬다. 별도 obstacle topology entity를 되살리지 않는다.
- 본 문서의 셀 수, bounds, 현재 asset 상태 메모는 T1c 편집 참고용이다. 장기 테스트 오라클로 승격하지 않는다.

## 5. 데모 난이도 곡선
| Stage | 공개 데모 역할 | 플레이어가 배워야 하거나 보아야 하는 것 |
| --- | --- | --- |
| Stage 1 | 첫 루프 학습 | 이동, 청소/수집, Carry full, Deposit, 낮은 강도의 위험 회피 |
| Stage 2 | 성능/동선 선택 대표 | 두 Source 중 우선순위 선택, 다른 actor 타입 대응, 대량 개체 처리 |
| Stage 3 | 최종 쇼케이스 | 넓은 arena, phase 변화, 탄막 반응 다양성, 영상/GIF 하이라이트 |

난이도 상승은 단순히 피해 압박을 키우는 방식보다, 선택지가 늘고 읽어야 할 패턴이 선명해지는 방식으로 만든다.

## 6. 인월드 연출 대화 기반 데모 가이드
### 6.1 목적
공개 테크 데모 단계에서는 인월드 연출 대화를 서사 전달보다 데모 실행 가이드로 우선 사용한다. 목적은 별도 튜토리얼 UI를 새로 만들지 않고, 이미 존재하는 stage-play dialogue pause 경로를 활용해 플레이어가 첫 루프에서 확인해야 할 행동을 짧게 고정하는 것이다.

이 사용법은 원래의 스토리 대화 용도를 버리는 것이 아니다. 현재 단계에서는 이야기보다 조작/규칙 이해가 우선이므로, "캐릭터가 현재 상황을 짧게 해석하고 다음 행동을 짚어주는" 혼합 톤을 기본으로 둔다.

### 6.2 톤 기준
기본 톤은 `캐릭터형 가이드톤`이다.

- Stage 1: 안내 70%, 대화 30%
- Stage 2: 안내 50%, 대화 50%
- Stage 3: 안내 30%, 대화 70%

작성 원칙:
- 한 대화는 하나의 행동 또는 관찰 포인트만 다룬다.
- 1~2개 발화 안에서 끝낸다.
- 첫 문장은 상황 반응, 둘째 문장은 행동 가이드로 둔다.
- `Hint` / `Notification`은 플레이 중 상태 알림을 맡고, guide dialogue는 첫 노출 학습만 맡는다.
- 재시도에서는 full variant를 반복하지 않고 short variant 또는 skip을 기본 후보로 둔다.

### 6.3 사용 시점
현재 T1c에서 바로 계획할 대화는 기존 `StageStart`, `StageClear`, `InterventionCarryFull`, `InterventionFirstHit`, `RetryVariant` 경로로 처리 가능한 범위에 한정한다.

| Stage | 시점 | 목적 | 반복 정책 | T1c 범위 |
| --- | --- | --- | --- | --- |
| Stage 1 | StageStart | 기본 이동, 조준 청소, 위험탄 제거 가능성을 안내 | 첫 시도 full, 재시도 short 또는 skip | 필수 |
| Stage 1 | Retry | 첫 설명 반복 대신 Carry / Deposit / 위험 회피 reminder 제공 | retry variant | 필수 |
| Stage 1 | InterventionCarryFull | Deposit 귀환 행동을 명확히 안내 | stage run 1회 우선 | 필수 |
| Stage 1 | InterventionFirstHit | 피격이 즉시 실패가 아니라 진행 지연과 회피 학습 신호임을 설명 | session 1회 우선 | 필수 |
| Stage 1 | StageClear | 첫 루프 완료를 인정하고 Stage 2 진입을 제안/응원 | 첫 클리어 full, 반복 시 short | 필수 |
| Stage 2 | StageStart | 두 Source와 서로 다른 actor 타입을 관찰하도록 안내 | 첫 시도 full, 재시도 short 또는 skip | 필수 |
| Stage 2 | Retry | 한쪽만 오래 붙잡지 말고 두 Source의 차이를 보도록 reminder 제공 | retry variant | 권장 |
| Stage 2 | StageClear | 두 Source 선택/actor 대응을 정리하고 Stage 3 진입을 제안/응원 | 첫 클리어 full, 반복 시 short | 필수 |
| Stage 3 | StageStart | 최종 쇼케이스, phase 변화, Deposit 귀환 경로 유지를 안내 | 첫 시도 full, 재시도 short 또는 skip | 필수 |
| Stage 3 | Retry | 최종 구간 관찰 포인트와 귀환 경로 reminder 제공 | retry variant | 선택 |
| Stage 3 | StageClear | 데모 완주감을 보강하고 Demo Complete 전이를 자연스럽게 연결 | 첫 클리어 full, 반복 시 short | 필수 |

후속 후보:

| 상황 | 판단 | 이유 |
| --- | --- | --- |
| 첫 Deposit 성공 | T1c 필수 범위 제외 | 첫 CarryFull 직후 다시 pause dialogue가 나오면 흐름을 연달아 끊을 수 있다. HUD / Notification으로 충분하다. |
| 첫 Source weakened | 후속 후보 | Source 약화와 actor phase 변화의 관계를 설명하기 좋지만 새 trigger가 필요하므로 T1c 필수 범위에서는 제외한다. |
| Low Time / HazardHigh | 후순위 | 전투 중 pause 개입은 피로도가 크다. HUD / Notification / VFX 우선으로 둔다. |

### 6.4 대사 예시
Stage 1 Start / 기본 조작:
```text
장비 반응은 괜찮아 보여.
WASD로 움직이고, 커서로 방향을 잡아. 클릭하면 그쪽을 청소할 수 있어.

조금 큰 덩어리도 타이밍을 맞춰 정확히 겨누면 쓸어담을 수 있을거야.
```

Stage 1 CarryFull / Deposit 귀환:
```text
이 정도면 먼지통이 꽉 찼어.
더 밀고 들어가기 전에 쓰레기통으로 돌아가서 비우자.
```

Stage 1 FirstHit / 피격 해석:
```text
윽, 모아둔 먼지가 충격으로 조금 쏟아졌어.
잠깐 거리를 벌리고, 패턴을 보고 다시 들어가자.
```

Stage 1 Retry / 축약 reminder:
```text
이번엔 너무 오래 머무르지 말자.
먼지통이 차면 비우고, 빨간 덩어리는 거리를 벌리면 돼.
```

Stage 1 Clear / Stage 2 진입 제안:
```text
슬슬 사용법에 익숙해진 것 같아.
이대로 다음 구역도 해결해보자!
```

Stage 2 Start / 두 Source와 actor 차이:
```text
이번엔 두 개의 방을 청소해야해.
방마다 위험 장치의 행동이 다르니까 조심하자.
```

Stage 2 Retry / 축약 reminder:
```text
한쪽만 붙잡고 있으면 동선이 꼬일 수 있어.
두 방의 위험 장치가 어떻게 다른지 보고 움직이자.
```

Stage 2 Clear / Stage 3 진입 제안:
```text
좋아, 두 방 모두 깔끔하게 정리됐어.
마지막 구역은 더 집중해서 피해야 할거야.
```

Stage 3 Start / 최종 쇼케이스:
```text
마지막 구역은 반응이 훨씬 크게 올 거야.
Source가 약해지는 순간 크게 날뛰니까 주의해야해.
```

Stage 3 Retry / 축약 reminder:
```text
패턴이 바뀌는 순간만 놓치지 말자.
몰리기 전에 Deposit 쪽 길을 남겨 두면 다시 들어갈 수 있어.
```

Stage 3 Clear / 완주감 보강:
```text
됐어, 마지막 Source까지 정리됐어.
이 정도면 공개 데모 루프는 끝까지 보여줄 수 있겠다.
```

### 6.5 T1c authoring 체크
- `InWorldDialogueCatalogSO`의 기존 StageStart / intervention trigger 경로를 우선 재사용한다.
- Stage 1에는 기본 조작, CarryFull, FirstHit, Retry short variant, StageClear를 우선 authoring한다.
- Stage 2에는 StageStart, Retry short variant, StageClear를 우선 authoring한다.
- Stage 3에는 StageStart, StageClear를 우선 authoring하고, Retry short variant는 필요 시 추가한다.
- Stage 2/3에는 긴 조작 설명보다 스테이지별 관찰 포인트와 다음 진입 제안을 짧게 둔다.
- dialogue active 동안 gameplay pause를 허용하되, 반복 노출은 줄인다.
- 대사 문구는 gameplay contract가 아니라 guide copy로 취급한다. serialized text exact assert로 고정하지 않는다.
- 첫 Deposit 성공과 첫 Source weakened는 T1c 필수 dialogue 범위에 넣지 않는다.

## 7. Stage 1. Baseline Learning Loop
### 7.1 역할
Stage 1은 튜토리얼 전용 모드가 아니라 공개 데모의 첫 실제 플레이 루프다. 플레이어가 자연스럽게 `Source 접근 -> 청소/수집 -> Deposit 귀환 -> Source 약화/고갈`을 한 번 이상 경험해야 한다.

### 7.2 현재 기준과 변경 방향
- 현재 `sd_demo_1`은 StageId `1`, SourceStableId `1001`, time limit `150s`를 사용한다.
- 현재 `sl_demo_1`은 중형 grid, 단일 Source, 단일 Deposit 구조다.
- 현재 Stage 1에는 actor placement 1개와 progress 기반 phase 전환 규칙이 있다. 이 actor는 2가지 탄막 패턴과 phase 후 조준 방식 변화가 있어, Stage 1의 개념 소개 / 첫 루프 학습 역할에는 복잡도가 높다.

StageId, Source, Deposit, time limit 기준은 유지 후보로 본다. 다만 HazardActor는 더 단순한 actor로 교체한다. 기존 복합 actor는 Stage 3 쇼케이스 후보로 이동시킨다.

### 7.3 Source / Deposit 의도
- Source는 플레이어가 처음 접근했을 때 목표 영역으로 바로 읽혀야 한다.
- Deposit은 시작 위치에서 가까워야 하며, 첫 Carry full 이후 귀환 방향을 쉽게 찾을 수 있어야 한다.
- Source와 Deposit 사이에는 짧은 왕복 동선이 있어야 한다.
- Source 내부는 너무 넓거나 복잡하지 않게 두고, 청소 효율이 즉시 체감되도록 한다.

### 7.4 HazardActor 의도
Stage 1 actor는 `Simple Crossing Sentry`로 둔다. 역할은 "위험 발화점이 주기적으로 길을 가로지르는 hazard를 만든다"는 개념을 소개하는 것이다.

기본 패턴:
- 고정된 방향으로 주기적으로 1발씩 발사한다.
- 플레이어가 길을 건너듯 타이밍을 보고 지나가게 만든다.
- 조준, 회전, 랜덤, 다방향 fan은 사용하지 않는다.
- telegraph는 짧고 명확하게 유지한다.

phase 변화:
- 동일한 고정 방향 패턴을 유지하되, 1발 발사를 2점사로 바꾼다.
- 실질적인 규칙 변화는 작게 유지하고, 시각적으로만 압박이 조금 커졌다고 느끼게 한다.
- 조준 방식 변경, 패턴 종류 추가, 랜덤성 추가는 Stage 1에서 금지한다.
- phase 변화는 "Source 진행도에 따라 actor가 반응한다"는 예고만 제공한다.

배치 의도:
- 발사선은 Source 접근로 또는 Source 내부 일부를 가로지르되, Deposit 귀환 경로를 장시간 봉쇄하지 않는다.
- 1발 주기는 충분히 길게 둔다. Stage 1에서는 "피했다"는 경험이 "맞았다"보다 먼저 와야 한다.
- 2점사는 "같은 리듬이 한 번 더 온다"로 읽히는 간격을 사용한다.

### 7.5 실패 유도와 완화
- 실패 유도:
  - Carry full 이후 Deposit을 찾지 못하면 시간 손실이 생긴다.
  - Source에 오래 머무르면 `Simple Crossing Sentry`의 2점사 phase가 눈에 띈다.
- 완화:
  - Deposit 귀환 거리를 짧게 둔다.
  - actor 발사선은 안전한 우회 또는 대기 지점을 남긴다.
  - obstacle은 최소화하거나 시야를 막지 않는 방향으로 둔다.
  - 첫 30~60초 HUD / toast 힌트가 Stage 1에서 과잉 반복되지 않도록 한다.

## 8. Stage 2. Split Source Performance Route
### 8.1 역할
Stage 2는 공개 데모의 대표 성능 캡처 후보이자, actor 기반 동선 선택을 보여주는 스테이지다. 플레이어는 두 Source 덩어리 중 어느 쪽을 먼저 정리할지 선택하고, 각 Source에 붙은 다른 actor 타입에 맞춰 경로와 체류 시간을 바꿔야 한다.

### 8.2 현재 기준
- 현재 `sd_demo_2`는 StageId `2`, SourceStableId `1002`, time limit `180s`를 사용한다.
- 현재 `sl_demo_2`의 Source `1002`는 실제로 두 connected component로 나뉘어 있다.
- 현재 관측 기준:
  - component A: 약 147 cells, 대략 x `8..20`, y `23..35`
  - component B: 약 103 cells, 대략 x `26..35`, y `25..36`
- 현재 `sd_demo_2`에는 HazardActor placement가 없다.

위 관측값은 T1c 편집 시작점일 뿐, validation exact assert 기준이 아니다.

### 8.3 Source 분리 의도
Stage 2는 단일 Source `1002`를 유지하는 대신, 두 connected component를 별도 Source로 분리한다.

권장 방향:
- 큰 덩어리는 기존 SourceStableId `1002`를 유지한다.
- 다른 덩어리는 신규 SourceStableId를 부여한다.
- 각 Source는 별도 `StageSourceBinding`을 가진다.
- 각 Source의 weakened / depleted 진행은 HUD에서 독립된 공략 대상으로 읽혀야 한다.

stable id 최종값은 T1c에서 asset 상태와 충돌 여부를 확인해 확정한다.

### 8.4 Actor 타입 A. Fan Sentry
Fan Sentry는 공간을 넓게 점유하는 고정형 위험 actor다.

의도:
- N-way 또는 fan 계열 발사로 Source 주변 면을 느리게 압박한다.
- 플레이어가 "이 Source는 안전한 각도와 진입 타이밍을 골라야 한다"고 읽게 한다.
- 성능 캡처에서 지속적인 탄환 밀도를 안정적으로 제공한다.

배치 기준:
- Source 중심 또는 한쪽 모서리에 붙여 Source 내부 체류를 압박한다.
- Deposit 귀환 경로를 완전히 봉쇄하지 않는다.
- Stage 1보다 밀도는 높지만, 발사 방향과 telegraph가 읽혀야 한다.

### 8.5 Actor 타입 B. Tracker
Tracker는 플레이어 위치를 읽고 짧은 연속 발사를 만드는 조준형 actor다.

의도:
- player-position aim 계열 패턴으로 직선 귀환이나 한 자리 체류를 견제한다.
- 플레이어가 Source A와 다른 회피 리듬을 요구받게 한다.
- "두 Source가 같은 문제가 아니다"라는 인상을 만든다.

배치 기준:
- Source 바깥 또는 접근 동선 옆에 배치해 진입/이탈 순간에 읽히게 한다.
- 연속 발사는 짧고 명확해야 하며, 장시간 추적 압박으로 도주 불능을 만들지 않는다.
- Deposit 근처 안전 구역을 직접 겨누는 상태가 오래 지속되지 않게 한다.

### 8.6 다른 타입 actor 구현 주의
현재 단일 actor archetype prefab이 여러 pattern slot을 포함한다면, 같은 prefab을 두 placement에 재사용하는 것만으로는 플레이어 관점의 "다른 타입"이 약할 수 있다.

T1c에서는 아래 중 하나를 선택한다.
- actor archetype prefab을 `Fan Sentry`와 `Tracker`로 분리한다.
- 또는 동일 prefab을 쓰더라도 pattern slot / initial phase / selector 구성이 asset상 명확히 다른 두 prefab variant로 분리한다.

StageDefinition의 placement가 prefab 참조 중심인 현재 계약에서는 prefab variant 분리가 가장 명확한 편집 경로다.

Stage 1에서 제거한 기존 복합 actor는 Stage 2의 두 Source actor로 그대로 재사용하지 않는 편이 낫다. Stage 2의 목적은 actor 타입 비교이므로, 복합 actor 하나보다 `Fan Sentry` / `Tracker`처럼 역할이 분리된 variant가 더 읽기 쉽다.

### 8.7 실패 유도와 완화
- 실패 유도:
  - 두 Source를 모두 무시하고 한쪽만 오래 붙잡으면 다른 actor 압박과 시간 손실이 커진다.
  - Tracker는 직선 귀환을 견제하고, Fan Sentry는 Source 내부 체류 각도를 제한한다.
- 완화:
  - Deposit은 두 Source 사이의 귀환 기준점으로 읽혀야 한다.
  - 한 Source를 정리하는 동안 다른 Source actor가 화면 밖에서 과도하게 압박하지 않게 한다.
  - 성능 캡처 후보이므로 프레임 안정성을 해칠 정도의 burst 연출은 피한다.

## 9. Stage 3. Final Showcase Rebuild
### 9.1 역할
Stage 3은 기존 작은 샘플 구조를 공개 데모 최종 스테이지로 사용하지 않고, 사실상 신규 작성한다. 목적은 최고 난도 생존 시험이 아니라, 완주 직전 하이라이트로서 탄막 반응, source progress 변화, actor phase 변화를 가장 보기 좋게 보여주는 것이다.

### 9.2 현재 기준
- 현재 `sd_demo_3`은 StageId `3`, SourceStableId `1003`, time limit `210s`, final stage 설정을 가진다.
- 현재 `sl_demo_3`은 매우 작은 grid와 단일 Source / 단일 Deposit 구조다.
- 현재 player start가 Deposit과 겹치는 샘플성 배치다.

이 구조는 공개 쇼케이스 기준으로 유지하지 않는다. StageId와 final stage 역할은 유지하되, layout과 actor 구성은 신규 설계 대상으로 본다.

### 9.3 신규 layout 방향
- 중대형 arena로 재작성한다.
- 플레이어 시작 지점과 Deposit은 명확한 귀환 기준점이 되도록 둔다.
- Source는 1개 대형 또는 2개 중형 중 하나를 선택한다.
- Source와 Deposit 사이에는 한 번 이상 위험 zone을 통과하는 동선이 있어야 한다.
- obstacle은 시야를 가리는 벽보다, 선택 가능한 회피 경로와 우회 동선을 만드는 데 사용한다.
- Stage 2보다 복잡하지만, 첫 10초 안에 목표와 안전한 귀환 방향을 읽을 수 있어야 한다.

### 9.4 Actor 쇼케이스 방향
Stage 3은 최소 2종 actor를 조합해 최종 영상/GIF 후보를 만든다.

권장 조합:
- 넓은 fan / radial 계열 actor:
  - 화면을 채우는 대표 탄막을 만든다.
  - telegraph 후 발사 방향이 명확히 읽힌다.
- player-aim / burst 계열 actor:
  - 플레이어 조작에 반응하는 위험을 만든다.
  - 연속 발사나 phase 강화로 후반 변화를 보여준다.

phase 변화는 Source progress와 연결한다. weakened 이후에는 패턴이 더 빠르거나 촘촘해져도 되지만, 즉시 실패를 강요하는 급격한 난도 상승은 피한다.

Stage 1에서 제거한 기존 복합 actor는 Stage 3의 `Reactive / Composite Actor` 후보로 둔다. 이 actor는 2가지 패턴과 phase 이후 조준 방식 변화가 있어, Stage 1보다 Stage 3의 "진행도에 따라 actor 반응이 바뀐다"는 쇼케이스 목적에 더 잘 맞는다.

### 9.5 Source / Deposit 후보
1개 대형 Source 안:
- 장점: 최종 목표가 명확하고, 한 화면에서 progress 변화와 actor 반응을 보기 쉽다.
- 리스크: 플레이어 동선이 한 구역 체류로 고착될 수 있다.

2개 중형 Source:
- 장점: Stage 2의 선택 구조를 최종전에서 확장할 수 있다.
- 리스크: 시각 쇼케이스보다 공략 복잡도가 앞설 수 있다.

현재 권장은 `1개 대형 Source + 복수 actor`다. Stage 3의 주 목표가 공략 분기보다 영상성과 완주감이기 때문이다.

### 9.6 최소 공개 후보
Stage 3 최소안의 목표는 최종 스테이지처럼 보이고, `Stage3 -> Demo Complete` 흐름을 안정적으로 검증할 수 있는 것이다. 최종 난도, 복잡한 공략 분기, 추가 actor 변형은 최소안의 필수 조건이 아니다.

채택 최소 구성:
- `1`개 대형 Source
- `1`개 Deposit
- `2`개 HazardActor
  - fan / radial 계열 1개
  - 기존 Stage 1 복합 actor 또는 player-aim / burst 계열 1개
- 넓은 arena
- Deposit 주변 safety pocket
- Source weakened 이후 actor phase 변화 1회
- Stage3 clear 후 `Demo Complete` 전이 유지

최소안에서는 `2개 Source`를 쓰지 않는다. Stage 2가 이미 두 Source 선택과 actor 비교를 담당하므로, Stage 3은 큰 Source 하나를 정리하면서 화면 변화와 탄막 쇼케이스를 보는 역할로 분리한다.

실패 판정:
- 목표가 어디인지 첫 10초 안에 읽히지 않으면 실패다.
- Deposit 귀환 경로가 탄막으로 장시간 봉쇄되면 실패다.
- actor 2개가 동시에 활성화됐을 때 피격 원인이 읽히지 않으면 실패다.
- Stage 2보다 복잡하지만 더 좋은 장면이 아니라 더 피곤한 장면으로만 느껴지면 실패다.

### 9.7 실패 유도와 완화
- 실패 유도:
  - Source 내부 장기 체류 시 fan/radial 압박이 커진다.
  - Deposit 귀환 중 player-aim burst가 직선 이동을 견제한다.
  - weakened 이후 phase 변화로 최종 구간의 긴장감을 만든다.
- 완화:
  - Deposit 주변에는 짧은 회복/판단 여유를 둔다.
  - actor telegraph는 Stage 2보다 더 분명해야 한다.
  - 플레이어가 "왜 맞았는지" 읽기 어려운 사각 발사는 피한다.

## 10. 성능 캡처 후보 조건
공개 포트폴리오의 대표 성능 캡처는 Stage 2를 기준으로 한다. Stage 2는 두 Source, 두 actor 타입, 대량 개체 처리, Deposit 왕복 동선을 동시에 보여줄 수 있다. Stage 3은 성능 수치보다 최종 영상/GIF 하이라이트 후보로 분리한다.

### 10.1 대표 캡처 시나리오
- Stage: Stage 2
- Source 상태: 두 Source 모두 active
- actor 상태: Fan Sentry와 Tracker가 모두 active
- 플레이어 위치: Deposit 근처 또는 두 Source 사이 이동 경로
- 캡처 구간: Stage 시작 후 약 30~60초 사이의 안정 구간
- 화면 조건: 탄환, 수집/청소, 디스폰, Deposit 귀환 흐름이 동시에 읽히는 구간

제외 조건:
- 시작 직후 아직 개체가 충분히 쌓이지 않은 빈 구간
- 클리어 직전 한쪽 Source만 남아 대표성이 떨어지는 구간
- pause 또는 dialogue active 구간
- 연출 burst가 프레임 안정성보다 과하게 부각되는 순간

### 10.2 기록할 관측값
- 측정 날짜
- Unity 버전
- Editor / development build / non-development build 구분
- StageId와 시나리오 설명
- active bullet 또는 active entity 규모
- frame time 또는 FPS 관측값
- GC allocation 여부
- 캡처 시점의 해석 메모

캡처 목표는 최고 숫자가 아니라 공개 데모에서 실제로 재현 가능한 대표 장면이다. 포트폴리오 문서에는 과부하 순간의 최대치보다 안정적으로 재현 가능한 구간의 관측값을 우선 기록한다.

### 10.3 Stage별 자료 역할 분리
| Stage | 자료 역할 |
| --- | --- |
| Stage 1 | 첫 루프 학습 확인용 스크린샷/짧은 클립 |
| Stage 2 | 대표 성능 수치와 대량 개체 처리 캡처 |
| Stage 3 | 최종 쇼케이스 영상/GIF 하이라이트 |

## 11. T1c 편집 체크리스트
- `StageLayoutEditingSampleV1`을 authoring SSOT로 보고 grid / region / anchor를 편집한다.
- `StageGridLayoutGenerator`로 `sl_demo_*` layout asset을 갱신한다.
- `StageDefinitionGenerator` 또는 수동 보강으로 `sd_demo_*` SourceBindings 누락을 정리한다.
- `StageCatalogComposer`로 `sc_demo` entry가 definition/layout pair를 올바르게 참조하는지 확인한다.
- Stage 1:
  - 기존 복합 actor를 제거하고 `Simple Crossing Sentry`를 배치한다.
  - 기본 패턴은 고정 방향 단발, phase 후 패턴은 동일 방향 2점사로 제한한다.
  - 조준 방식 변화, 랜덤성, 다중 패턴 선택을 넣지 않는다.
- Stage 2:
  - 두 Source component가 별도 stable id로 분리되어야 한다.
  - 각 stable id에 `StageSourceBinding`이 존재해야 한다.
  - 각 Source에 다른 actor archetype 또는 명확히 다른 prefab variant가 배치되어야 한다.
- Stage 3:
  - 기존 작은 layout을 유지하지 않는다.
  - final stage flag와 Stage3 -> Demo Complete 흐름은 유지한다.
  - 신규 layout에서 player start, Source, Deposit이 모두 validation rule을 통과해야 한다.
  - Stage 1에서 제거한 기존 복합 actor를 reactive / composite showcase 후보로 검토한다.
- 검증:
  - StageCatalog validation
  - 운영 씬 stage entry smoke
  - 필요 시 Stage 1 -> Stage 2 -> Stage 3 -> Demo Complete 순차 PlayMode smoke
- 금지:
  - 설계 예시 셀 수, 좌표, threshold를 serialized asset exact assert로 고정하지 않는다.
  - Source / Deposit gameplay를 grid authority 밖의 legacy topology로 되돌리지 않는다.

## 12. 후속 논점
- Stage 2 신규 SourceStableId 최종 번호
- Stage 2 actor prefab variant 생성 방식
- Stage 1 `Simple Crossing Sentry` prefab / emission profile 생성 방식
- Stage 3 최소안 이후 확장 범위를 어디까지 둘지
- Stage 3 영상/GIF 캡처 후보 구간을 source weakened 전후 중 어디로 잡을지
- Stage 1 온보딩 힌트와 T1c layout 편집을 같은 작업으로 묶을지 분리할지

## 13. 변경 이력
- 2026-06-19: Stage 1 HazardActor를 `Simple Crossing Sentry`로 단순화하고, 기존 복합 actor를 Stage 3 reactive / composite showcase 후보로 이동하는 기준을 추가했다.
- 2026-05-20: 인월드 연출 대화 기반 데모 가이드를 stage별 표로 정리하고, Stage 1/2 clear의 다음 스테이지 진입 제안 및 Stage 3 clear의 완주감 보강 대사 예시를 추가했다.
- 2026-05-20: Stage 3 최소 공개 후보를 `1 large Source + 2 actors + Deposit safety pocket + 1 phase change`로 고정하고, Stage 2 대표 성능 캡처 후보 조건을 추가했다.
- 2026-05-18: 인월드 연출 대화를 데모 가이드로 사용하는 운영 계획과 StageStart / CarryFull / FirstHit / Retry 대사 예시를 추가했다.
- 2026-05-18: 공개 데모 Stage 1~3 레벨 디자인 초안 작성. Stage 2 Source 분리와 Stage 3 신규 작성 결정을 반영했다.
