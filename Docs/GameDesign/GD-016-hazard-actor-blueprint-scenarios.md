# HazardActor Blueprint Scenarios

## Metadata
- doc_id: `GD-016`
- type: `GameDesign`
- status: `draft`
- last_updated: `2026-04-09`
- related_docs:
  - [./GD-015-hazard-emitter-design.md](./GD-015-hazard-emitter-design.md)
  - [../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md](../TechnicalDesign/TD-030-hazard-actor-hierarchy-and-stage-application.md)
  - [../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md](../TechnicalDesign/TD-031-hazard-actor-behavior-runtime.md)
  - [../TaskBoard/SESSION-20260409-01-hazard-actor-behavior-board.md](../TaskBoard/SESSION-20260409-01-hazard-actor-behavior-board.md)

> `HazardActor`를 플레이어가 "비공격 대상 몬스터처럼 읽는 위험 개체"로 확장하기 위한 청사진 시나리오와 일반화 범위를 정리하는 문서.

## 1. 목적
- 행동 확장 논의의 출발점이 된 청사진 시나리오를 기획 문서 기준으로 고정한다.
- `HazardEmitter`의 gameplay-facing 개념과 `HazardActor`의 구현 상위 개념이 어떤 경험 목표를 함께 달성해야 하는지 정리한다.
- 특정 예시 상태 분기에 매몰되지 않고, actor behavior가 받아야 하는 일반화 범위를 함께 남긴다.

## 2. 적용 범위
- actor의 존재 연출, 패턴 반복, 상태 강화가 어떤 플레이 경험을 만들어야 하는지
- 청사진 시나리오와 그 일반화 가능한 변형
- 이후 `Presence`, `PatternSelector`, `Emitter execution seam`으로 번역할 gameplay 목표

## 3. 비범위
- ECS runtime owner, update order, component schema
- selector 수식, pattern-slot runtime wire shape
- 최종 VFX/SFX asset 명세
- 샘플 씬의 구체 배치 좌표와 serialized 수치 확정

## 4. 용어 정리
- gameplay-facing design term:
  - `HazardEmitter`
  - 플레이어가 보는 위험 발화점, 위험 오브젝트, 위험 지점
- implementation upper concept:
  - `HazardActor`
  - presence, activation, pattern selection, future motion을 소유하는 상위 개체
- 관계:
  - 플레이어는 대체로 `HazardEmitter`를 인지하지만, 구현은 `HazardActor`가 상위 owner이고 `HazardEmitter`는 그 actor의 발사 ability slice로 본다.

## 5. 발화점이 된 청사진 시나리오
### 5.1 시나리오 1. 플라스크형 고정 actor
- 플레이어가 청소를 위해 방(`Source`)에 처음 진입했을 때, 구석에 배치된 플라스크형 고정 actor가 강한 존재 연출을 보인다.
  - 목적:
    - actor의 존재를 강조해 플레이어가 빠르게 인지하게 한다.
    - 아직 공격은 시작하지 않고, "활성화되고 있다"는 감각을 준다.
- 그 이후 actor는 두 종류의 공격 패턴을 반복한다.
  - 패턴 A:
    - `90도 범위 4-way 탄 흩뿌리기`
    - 2회 반복
  - 패턴 B:
    - 플레이어 조준 고정 방향 발사
    - 탄환 3발 연속 발사
- 각 패턴 시작 전에는 가벼운 warning sign 성격의 telegraph가 출력된다.
- 방 청소 진행도가 절반을 넘으면, actor가 다시 강한 상태 변화 연출을 보인다.
  - 이후 조준 패턴은 `3발 -> 7발`로 강화된다.
  - 강화 후 조준 패턴은 강화 이전과 달리 발사 도중에도 계속 플레이어를 향해 조준이 갱신된다(Snapshot Timing: Event Start -> Per Shot).

### 5.2 시나리오 1의 경험 목표
- actor는 "장치"가 아니라 "이 방에서 문제를 일으키는 존재"처럼 읽혀야 한다.
- 진입 직후 존재 연출과 패턴 시작 telegraph는 서로 다른 층위의 신호여야 한다.
  - 존재 연출:
    - actor의 상태 변화와 방 분위기 전달
  - 패턴 telegraph:
    - 개별 공격의 시작 예고
- 상태 강화는 단순한 난이도 상승보다 "방이 정리될수록 이 위험 개체가 예민해진다"는 감각을 줘야 한다.

## 6. 일반화 범위
위 청사진은 특정 사례일 뿐이며, actor behavior는 아래 범위를 받아야 한다.

### 6.1 단순형
- 특정 진행도 구간에서만 출현
- 단일 패턴만 반복
- `Source` 정복 시 소멸

### 6.2 반응형
- `Pressure`, `Source` progress, player distance에 따라 활성/비활성
- 특정 threshold를 넘을 때만 강화
- 방 후반부에만 더 공격적이거나 더 예민한 상태로 전환

### 6.3 선택형
- 여러 패턴을 소유
- 순차/랜덤/가중치 기반으로 패턴 선택
- 플레이어 거리, 진행도, actor 상태에 따라 패턴 선택 기준 변경

### 6.4 확장형
- 지정 경로 순회
- 여러 emitter ability 조합
- actor 상태에 따라 존재 연출, 공격 패턴, 패턴군 자체가 바뀜

## 7. 이번 확장에서 유지해야 할 핵심 방향
- actor behavior는 특정 상태 분기 하나를 hardcode하는 방향으로 좁아지면 안 된다.
- 청사진은 구현 목표를 설명하는 예시이지, runtime core state machine 자체가 되어서는 안 된다.
- 확장 목표는 아래를 모두 수용하는 것이다.
  - 좁은 단일 패턴 actor
  - 다중 패턴 선택 actor
  - 후반 강화 actor
  - future motion/path actor

## 8. 구현 번역 힌트
- 존재 연출은 `Presence` 축으로 번역한다.
- 패턴 반복과 선택은 `PatternSelector + PatternSet` 축으로 번역한다.
- 개별 발사는 `HazardEmitter` execution owner가 유지한다.
- 강화/전환은 actor-level state escalation 또는 selector policy 변경 seam으로 번역한다.

## 9. 후속 논의 후보
- room-entry activation presentation을 `PresenceState`와 어떻게 연결할지
- 패턴 시작 telegraph와 actor 존재 연출을 어떤 레이어로 분리할지
- 상태 강화가 selector policy 변경인지, actor-level presentation + slot-set swap인지
- 경로 순회 actor를 같은 behavior 문서에서 계속 다룰지 별도 문서로 뺄지
