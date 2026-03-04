# 데모 플레이 흐름 설계 (GD-008)

## Metadata
- doc_id: `GD-008`
- type: `GameDesign`
- status: `draft`
- last_updated: `2026-03-04`
- related_docs:
  - [GD-001-campaign-loop-design.md](./GD-001-campaign-loop-design.md)
  - [OPS-002-demo-playable-polish-and-delivery-plan.md](../ProjectOps/OPS-002-demo-playable-polish-and-delivery-plan.md)

> 데모에서 플레이어가 `Title -> Lobby -> Stage -> Result`를 반복하며 3개 스테이지를 체험하도록 하는 경험 흐름을 정의한다. 구현 계약은 TD 문서에서 다룬다.

## 1. 목적
- 데모 플레이 시작부터 종료까지의 외부 흐름을 명확히 한다.
- 스테이지 개별 체험과 순차 체험을 모두 지원한다.
- 짧은 진입, 쉬운 재도전, 명확한 종료감을 동시에 제공한다.

## 2. 적용 범위
- 화면 단위 역할(`Title`, `Simple Lobby`, `Stage Play`, `Stage Result`, `Demo Complete`)
- 화면 간 전이 규칙과 선택지
- 대표 플레이 시나리오(순차 플레이/단일 스테이지 반복)

## 3. 비범위
- UI 아트, 애니메이션, 사운드 연출 상세
- 점수 계산식, 내부 전투 규칙, 데이터 구조
- ECS 시스템 배치/소유권/업데이트 순서 등 구현 계약

## 4. 경험 목표
| 목표 | 의도 |
| --- | --- |
| 빠른 진입 | `Title -> Lobby -> Stage`를 최소 단계로 유지 |
| 쉬운 재도전 | `Stage Result`/`Lobby`에서 즉시 재시작 또는 재진입 가능 |
| 명확한 완료감 | `Stage3` 완료 후 `Demo Complete`로 마무리 경험 제공 |

## 5. 화면 구성
| Screen | 플레이어 관점 역할 | 핵심 액션 |
| --- | --- | --- |
| Title | 시작 진입점 | 아무 키 입력으로 로비 진입 |
| Simple Lobby | 스테이지 선택 허브 | Stage 1~3 선택, Quit |
| Stage Play | 실제 플레이 구간 | 클리어/실패 발생 |
| Stage Result | 결과 확인 및 다음 선택 | Next Stage, Retry, Return to Lobby |
| Demo Complete | 데모 종료 경험 | Restart Demo, Return to Lobby, Quit |

## 6. 기본 흐름
```text
Boot
-> Title
-> Simple Lobby
-> Stage Select
-> Stage Play
-> Stage Result
-> (Next Stage | Retry | Return to Lobby)
```

보조 종료 흐름:
```text
Stage3 Result (Next Stage)
-> Demo Complete
-> (Restart Demo | Return to Lobby | Quit)
```

## 7. 전이 규칙
### 7.1 Stage Result 선택지
| 선택 | 전이 |
| --- | --- |
| Next Stage | 다음 번호 스테이지 진입 (`Stage1 -> Stage2 -> Stage3`) |
| Retry | 동일 스테이지 즉시 재시작 |
| Return to Lobby | `Simple Lobby` 복귀 |

### 7.2 마지막 스테이지 처리
- `Stage3`에서 `Next Stage`를 선택하면 `Demo Complete`로 전이한다.
- `Demo Complete`의 `Restart Demo`와 `Return to Lobby`는 모두 `Simple Lobby`로 이동한다.

## 8. 대표 플레이 시나리오
### 8.1 순차 플레이
```text
Title -> Lobby -> Stage1 -> Result -> Next Stage
-> Stage2 -> Result -> Next Stage
-> Stage3 -> Result -> Demo Complete -> Lobby
```

### 8.2 단일 스테이지 반복 플레이
```text
Title -> Lobby -> Stage2 -> Result -> Retry (반복) -> Lobby
```

## 9. UX 가이드
- 첫 화면 액션은 항상 1차 행동(시작/선택)만 강조한다.
- `Stage Result`는 진행/재시도/이탈의 3개 선택지만 고정 제공한다.
- 데모 범위에서는 잠금 해제, 메타 성장, 복잡한 옵션 분기를 넣지 않는다.
- 카피는 짧고 즉시 해석 가능한 동사 중심으로 유지한다.

## 10. 구현 경계 (최소)
- 본 문서는 플레이 경험 흐름만 고정한다.
- 시스템 책임/업데이트 순서/데이터 계약은 TechnicalDesign 문서에서 관리한다.
- 구현 세부가 바뀌어도 `화면 역할`과 `전이 규칙`은 본 문서를 기준으로 유지한다.

## 11. 오픈 이슈
- `Next Stage` 버튼 노출 조건(클리어 전용 여부) 최종 확정 필요
- `Demo Complete`에서 노출할 요약 지표(클리어 시간, 피격 수) 범위 결정 필요

## 12. 변경 이력
- 2026-03-04: 템플릿 기준으로 섹션/헤더/표현을 재정렬
- 2026-03-04: 기술 구현 상세를 축소하고 경험 설계 중심으로 재작성
