# HazardActor Stage Placement and Orchestration Framework

## Metadata
- doc_id: `TD-032`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-04-13`
- related_docs:
  - [./TD-030-hazard-actor-hierarchy-and-stage-application.md](./TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [./TD-031-hazard-actor-behavior-runtime.md](./TD-031-hazard-actor-behavior-runtime.md)
  - [../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md](../GameDesign/GD-016-hazard-actor-blueprint-scenarios.md)
  - [../TaskBoard/SESSION-20260413-01-hazard-actor-stage-placement-board.md](../TaskBoard/SESSION-20260413-01-hazard-actor-stage-placement-board.md)

> `HazardActor`를 재사용 가능한 actor archetype content 단위로 보고, stage는 이를 source에 attach하는 placement/orchestration owner로 다룬다. 이 문서는 schema/wire shape를 확정하는 구현 문서가 아니라, 이후 실행 플랜의 기준이 되는 용어/책임/시나리오/비범위 SSOT다.

## 1. 문제 정의
- 현재 구현은 `TD-030` 기준으로 `Source -> HazardActor -> HazardEmitter` hierarchy와 stage apply/reset owner를 이미 가진다.
- `TD-031` 기준으로 actor 내부 behavior runtime도 `presence + phase-aware selector + escalation staging`까지 닫혔다.
- 그러나 현재 `StageDefinitionSO.HazardActorBinding`은 아래 성격에 머문다.
  - baseline actor roster의 on/off
  - start suppressed
  - emitter-level local offset / single-slot profile override
- 이 구조는 “스테이지별로 다른 actor archetype을 선택해서 source에 배치하고, instance별 등장/phase 전환/소멸을 orchestration한다”는 content-delivery 프레임을 표현하기 어렵다.
- 결과적으로 현재 구조만으로는 아래 요구를 자연스럽게 수용하기 어렵다.
  - 같은 actor archetype을 여러 stage/source에서 재사용
  - 같은 stage 안에서 같은 archetype의 여러 instance를 서로 다른 규칙으로 운용
  - source prefab duplication 없이 stage variation 구성

## 2. 목표 / 비목표
### 2.1 목표
- `HazardActor`를 재사용 가능한 `actor archetype` content 단위로 정의한다.
- stage가 actor instance를 source에 attach하는 `placement` 책임을 소유하는 방향을 고정한다.
- stage가 배치된 actor instance의 등장/phase 전환/소멸을 orchestration하는 별도 책임을 가진다는 점을 고정한다.
- 동일 actor archetype을 여러 stage/source에서 재사용 가능하게 하는 방향을 문서 기준으로 고정한다.
- 동일 stage 안에서 동일 archetype의 여러 instance가 서로 다른 phase schedule을 가질 수 있어야 한다는 요구를 명시한다.

### 2.2 비목표
- actor 내부 behavior schema 재설계
- actor motion/path contract 확정
- VFX/SFX presentation bridge 설계
- direct prefab reference vs catalog key의 최종 결정
- placement/orchestration data schema와 wire shape의 최종 확정

## 3. 용어 정의
### 3.1 Actor Archetype
- 재사용 가능한 actor 원형 content 단위다.
- actor가 가질 수 있는 phase 집합과 각 phase의 행동 양상, selector policy, emitter 구성, 표현 baseline을 가진다.
- archetype은 “이 개체가 무엇을 할 수 있는가”를 정의한다.

### 3.2 Actor Placement
- stage가 어떤 actor archetype을 어느 source에 attach할지 정하는 content layer다.
- source local placement, 시작 enabled/suppressed 같은 배치성 옵션을 가진다.
- placement는 “이 개체를 어디에 둘 것인가”를 정의한다.

### 3.3 Actor Orchestration
- stage가 배치된 actor instance에게 언제 등장/phase 전환/소멸을 시킬지 정하는 content layer다.
- orchestration은 “이 개체에게 언제 무엇을 시킬 것인가”를 정의한다.

### 3.4 Placement Instance
- stage 안에 배치된 actor의 개별 인스턴스를 가리키는 식별 단위다.
- 같은 archetype을 여러 번 배치하더라도 orchestration은 개별 instance를 구분할 수 있어야 한다.

### 3.5 Source-owned Actor Lifecycle
- actor를 content authoring 관점에서 몬스터형 개체처럼 취급하더라도, runtime lifecycle owner는 여전히 source다.
- source reset/reapply/teardown이 attach된 actor hierarchy 전체의 lifecycle을 소유한다.

## 4. 책임 분리
### 4.1 Actor Archetype이 소유하는 것
- phase 집합
- 각 phase의 행동 양상
- selector policy
- emitter 구성
- 표현 baseline

### 4.2 Stage Placement가 소유하는 것
- 어떤 actor archetype을 어느 source에 attach할지
- source local 좌표/anchor 등 배치성 값
- 시작 enabled/suppressed 같은 초기 배치 옵션

### 4.3 Stage Orchestration이 소유하는 것
- 등장 조건
- phase 전환 조건
- 소멸 조건
- instance 또는 instance group 단위 제어

### 4.4 Stage가 소유하지 않는 것
- actor 내부 phase behavior 편집
- selector policy 자체 재정의
- emitter slot content 재정의
- actor archetype 내부 baseline 값의 stage-side 직접 mutation

## 5. Runtime Ownership / Lifecycle
- runtime hierarchy의 기준은 계속 `Source -> HazardActor -> HazardEmitter`다.
- source는 attach된 actor hierarchy의 lifecycle owner다.
- actor는 자기 내부 behavior의 owner다.
- stage system은 actor placement/orchestration의 owner다.
- 따라서 아래 두 문장은 동시에 성립한다.
  - actor는 content authoring 관점에서 “몬스터처럼 취급하는 개체 원형”이다.
  - actor runtime은 source-owned hierarchy로 apply/reset/teardown 된다.
- 현재 `HazardActorBinding`은 “기존 stage apply binding seam”으로 유지하되, 장기 방향의 최종 placement/orchestration 표현으로 간주하지 않는다.

## 6. 대표 요구 시나리오
### 6.1 추가 출현 시나리오
- 처음에는 actor 2개가 출현한다.
- source progress가 절반 정도에 도달하면 actor 1개가 추가로 출현한다.

### 6.2 분리 phase 전환 시나리오
- 처음에는 같은 archetype actor 4개가 출현한다.
- source progress가 1/3에 도달하면 그중 2개만 phase 2로 전환한다.
- source progress가 2/3에 도달하면 나머지 2개도 phase 2로 전환한다.

### 6.3 재배치 시나리오
- 같은 actor archetype을 서로 다른 stage/source에 재사용한다.
- stage마다 서로 다른 source local offset과 배치 조건을 가질 수 있다.

## 7. First-pass 제약
### 7.1 이 문서에서 고정하는 것
- actor archetype / placement / orchestration은 서로 다른 content 책임이다.
- stage는 actor behavior SSOT가 아니라 placement/orchestration SSOT다.
- 같은 archetype의 여러 instance를 stage가 별도 orchestration할 수 있어야 하므로 instance 식별 개념이 필요하다.
- runtime ownership은 계속 source-owned hierarchy를 유지한다.
- placement와 orchestration은 분리된 schema로 간주한다.
- orchestration의 first-pass target은 instance-only로 제한한다.
- actor archetype은 1개 이상의 phase를 가질 수 있다.
- stage orchestration의 `PhaseSet`은 target instance를 archetype이 정의한 임의의 valid phase로 전환 요청할 수 있다.
- first-pass는 phase chain 구조를 강제하지 않는다.

### 7.2 아직 결정하지 않는 것
- actor lookup을 direct prefab reference로 할지, catalog key/id로 할지
- orchestration targeting에 group 개념을 언제 도입할지
- progress trigger 외에 어떤 trigger 축을 first-pass에 열지

## 8. 중간 수준 설계 고정점
### 8.1 Placement
- placement는 정적 배치 데이터로 본다.
- placement는 아래 의미를 가진다.
  - 어떤 actor archetype을 어느 source에 attach할지
  - source local placement를 어떻게 둘지
- placement는 actor 내부 phase behavior나 phase timeline을 소유하지 않는다.
- placement first-pass 최소 정보는 아래로 본다.
  - `PlacementInstanceId`
  - `ActorArchetypeRef`
  - `SourceStableId`
  - `LocalOffset`
- `PlacementInstanceId`는 stage 전체에서 유일한 식별자로 본다.
- placement transform authority는 `LocalOffset` 하나로 제한한다.
- placement는 초기 등장 제어를 소유하지 않는다.
  - actor attach와 실제 등장 시작은 분리된 문제로 본다.

### 8.2 Orchestration
- orchestration은 배치된 instance를 대상으로 request를 발행하는 동적 content layer다.
- orchestration first-pass action set은 아래 3개로 둔다.
  - `Spawn`
  - `PhaseSet`
  - `Retire`
- 각 action의 의미는 request semantic으로 고정한다.
  - `Spawn`: target instance의 등장 시작을 요청한다.
  - `PhaseSet`: target instance를 특정 valid phase로 전환하도록 요청한다.
  - `Retire`: target instance의 퇴장 시작을 요청한다.
- orchestration 레이어는 actor가 실제로 어떤 내부 상태 전이를 거치는지 알 필요가 없다.
- 실제 상태 progression과 signal/presentation은 actor runtime owner가 수행한다.

### 8.3 Presence / Phase와의 관계
- first-pass에서 `Spawn`은 structural instantiate를 의미하지 않는다.
- first-pass에서 `Retire`는 structural destroy를 의미하지 않는다.
- 현재 의미는 아래로 본다.
  - `Spawn` = actor presence flow의 등장 시작 request
  - `Retire` = actor presence flow의 퇴장 시작 request
  - `PhaseSet` = actor phase flow의 target phase 전환 request
- 따라서 actor hierarchy의 attach/detach와 presence/phase orchestration은 분리된 문제로 본다.

### 8.4 Trigger 모델
- orchestration first-pass 공통 trigger 모델은 아래 2개로 제한한다.
  - `OnStageStart`
  - `OnSourceProgressAtOrAbove`
- `OnSourceProgressAtOrAbove`는 placement instance가 attach된 source의 progress truth를 읽는 것으로 본다.
- 다른 source의 progress를 참조하는 cross-source orchestration은 first-pass 범위에서 배제한다.
- first-pass에서 source progress truth는 existing normalized source progress를 재사용한다.

### 8.5 Rule 발화 규칙
- orchestration first-pass rule은 one-shot이다.
- 같은 stage run에서 같은 rule은 한 번만 발화한다.
- trigger truth의 평가 owner는 stage orchestration system이다.
- stage orchestration system은 trigger를 평가하고 request만 발행한다.
- actor runtime owner는 request를 해석하고 실제 전이를 수행한다.
- first-pass orchestration target은 instance-only로 제한한다.
- 같은 frame에 같은 instance에 여러 rule이 동시에 eligible이어도, instance당 frame당 최대 1개 request만 발행한다.
- 동시에 eligible인 rule은 declaration order 기준으로 직렬화한다.
- 해당 frame에 발행되지 않은 eligible rule은 버려지지 않으며, 다음 frame에 다시 평가된다.
- one-shot rule은 실제 request를 발행한 시점에만 consumed로 본다.
- 같은 instance에 대해 충돌하는 rule authoring은 validation error가 아니라 warning으로 보고한다.

### 8.6 Delivery / Resolve Seam
- actor archetype delivery first-pass는 direct prefab reference + stage apply 시 pre-attach를 전제로 한다.
- source apply 시 placement instance마다 actor archetype hierarchy를 source-owned runtime에 attach한다.
- attach된 actor는 아직 등장하지 않았더라도 runtime에는 존재한다.
- orchestration target은 actor entity가 아니라 `PlacementInstanceId`다.
- actor root runtime은 `PlacementInstanceId`를 직접 가진다.
- source는 placement instance resolve를 위해 별도 placement ref buffer를 가진다.
  - first-pass 최소 필드는 `PlacementInstanceId`, `ActorEntity`다.
- 기존 `SourceHazardActorRefBuffer`는 first-pass에서 유지한다.
- stage-driven content 재해석이 완료되면 `SourceHazardActorRefBuffer`를 placement seam으로 대체할 수 있는지 별도 점검한다.

### 8.7 Request Signal / Runtime Ownership
- actor root에는 unified orchestration request signal 1개를 둔다.
- request signal은 versioned signal로 본다.
- request 최소 의미는 아래로 본다.
  - `Version`
  - `ActionType`
  - `TargetPhaseId` (`PhaseSet`일 때만 의미 있음)
- actor root는 owner별 last-consumed version을 가진다.
  - presence owner
  - phase owner
- presence owner는 `Spawn / Retire`만 소비한다.
- phase owner는 `PhaseSet`만 소비한다.
- `PhaseSet`은 direct phase mutation이 아니라 existing phase runtime owner에 대한 request로 해석한다.
- orchestration rule baseline과 fired-state runtime은 분리한다.
- fired-state owner는 source-owned runtime이다.
- first-pass fired-state는 `RuleId + HasFired` 수준의 rule별 buffer로 시작한다.

## 9. Open Questions
- placement entry lookup 방식은 무엇이 적절한가
- spawn / phase / retire 규칙을 하나의 통합 schema로 둘지, 타입별로 나눌지
- validation은 기존 stage catalog rule과 actor archetype authoring rule 사이를 어떻게 분리할지
- existing `SourceHazardActorRefBuffer`를 placement resolve seam으로 완전히 대체하는 시점과 조건은 무엇인가

## 10. 구현 단계 분리 초안
- `SP-1. Actor Archetype Delivery`
  - stage가 actor archetype을 source-owned hierarchy에 attach하는 프레임
- `SP-2. Placement Instance Schema`
  - placement instance의 정적 배치 정보와 runtime identity 정리
- `SP-3. Instance Orchestration`
  - instance 대상 `Spawn / PhaseSet / Retire` request와 trigger 모델 정리
- `SP-4. Validation / Sample / Migration`
  - validation, sample content, migration/closeout 정리

## 11. 운영 메모
- 이 TD는 구현 schema를 바로 닫는 문서가 아니다.
- 이 TD만으로 구현을 시작하지 않는다.
- 다음 단계는 별도 실행 플랜에서 아래를 decision-complete로 닫는 것이다.
  - placement/orchestration schema
  - lookup/reference 방식
  - validation 경계
  - apply/reset owner와 runtime instantiation 흐름
