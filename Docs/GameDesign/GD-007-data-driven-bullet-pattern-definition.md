# 데이터 주도 탄막 패턴 방향성 (Pattern / Wave / Progress)

## Metadata
- doc_id: `GD-007`
- type: `GameDesign`
- status: `active`
- last_updated: `2026-02-23`
- related_adr:
  - [ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md](../ADR/ADR-20260212-01-so-based-bullet-definition-and-source-state-spawn-profile.md)
  - [ADR-20260212-02-area-density-based-spawn-and-field-shapes.md](../ADR/ADR-20260212-02-area-density-based-spawn-and-field-shapes.md)

> Pattern/Wave/Progress의 기획 의도와 플레이 경험 목표를 정의한다. 구현 계약은 TD 문서로 분리한다.

## 1. 목적
- 플레이어 경험 관점에서 Pattern/Wave/Progress의 역할 경계를 확정한다.
- 15~20분 캠페인 완주를 위한 Stage 스케일 앵커를 제시한다.
- 본 문서의 15~20분은 전체 캠페인 클리어 시간(재도전 미포함) 기준이다.

## 2. 적용 범위
- 패턴 철학, 감정 곡선, 리스크-보상 의도
- 수거/피격/Deposit의 진행도 해석 원칙
- Stage 단위 목표 시간과 난이도 배율 방향

## 3. 비범위
- 필드 단위 데이터 스키마, 수식, 검증 규칙 상세
- ECS 컴포넌트 매핑, 런타임 처리 순서, 소유권 계약
- 연출/UI 상세, 튜토리얼/학습 설계
- 메타 정산/영구 성장 시스템

## 4. 가정/제약
- Pattern은 리듬/압력을 소유하고, Wave는 공간/공정성을 소유한다.
- Reward는 점수 중심이 아니라 `Source Remaining` 감소 기반 진행도로 해석한다.
- Deposit은 보상 정산이 아니라 리스크 리셋 행동으로 해석한다.
- 캠페인 감정 곡선은 아래 루프를 반복한다.

```text
진입
→ 수거(즉시 Progress)
→ Load 증가
→ HazardStack 축적
→ 리스크 증가
→ Deposit(리스크 리셋)
→ 재진입
```

## 5. 경험 설계 원칙
### 5.1 캠페인 감정 곡선
- 기본 구조는 `안정적 청소 -> 리스크 축적 -> 압박 피크 -> Deposit으로 안도`의 반복이다.
- 각 스테이지는 최소 1회의 긴장 상승 구간과 1회의 완화 구간을 포함한다.

### 5.2 패턴 철학
- 난해한 회피 퍼즐형보다 `동선 유도형 밀도 탄막`을 우선한다.
- 플레이어가 "정답 회피 경로"를 찾기보다, 위험을 관리하며 청소 효율을 최적화하도록 설계한다.

### 5.3 리스크-보상 의도
- 안전 플레이만으로도 진행은 가능해야 한다.
- 고위험 유지 플레이는 클리어 시간을 단축하는 방향으로 보상한다.
- 피격은 즉시 게임오버가 아니라, 효율 저하와 재정비 기회로 해석한다.

### 5.4 스케일 앵커 (프로토타입)
- Progress 기준: Trash = 1, Hazard = 5
- Source 상태 전환: 2000 → 약화, 4000 → 고갈
- Stage 1 목표: 2.5~3분, 필요 Progress 6000~8000
- 캠페인 목표: 전체 15~20분(재도전 미포함)

스폰 밀도 예시:

| 상태 | Trash | Hazard |
| --- | --- | --- |
| Normal | 초당 10 | 초당 0.05 |
| Weakened | 초당 4 | 초당 0.08 |

Stage 권장 Progress 배율:

| Stage | 배율 |
| --- | --- |
| 1 | 1.0 |
| 2 | 1.2 |
| 3 | 1.5 |
| 4 | 1.8 |
| 5 | 2.0 |
| Final | 1.2~1.5 |

### 5.5 밸런싱 지표
| 지표 | 목적 |
| --- | --- |
| StageClearTime / TargetTime | 목표 시간 대비 난이도 판정 |
| SpawnSkipRate01 | 공정성/스폰 실패 감지 |
| HitRatePerMin | 리스크 과다 여부 |
| VacuumLockUptime01 | 조작 답답함 추적 |
| PeakActiveBullets | 성능 안정성 확인 |

## 6. 문서 경계
- 필드 단위 스키마, 계산식, 검증 규칙, 런타임 반영 절차는 `TD-002`에서 관리한다.
- 본 문서는 밸런싱 방향과 플레이 경험 의도를 고정하는 기준 문서로 유지한다.

## 7. 오픈 이슈
- Stage별 목표 시간/Progress 배율을 실제 플레이 로그로 재조정 필요
- PlayerRelative 피크 Stage에서 공정성(SpawnSkipRate01) 악화를 막는 가드 값 확정 필요

## 8. 변경 이력
- 2026-02-23: 기존 `데이터 주도 탄막 패턴 정의` 문서를 `GD-007` 규칙(파일명 + Metadata)으로 정리하고 구조화
- 2026-02-23: `RiskMultiplier` 연산자 표기 오류를 곱셈(`*`)에서 합산(`+`)으로 정정하고, 캠페인 시간 정의를 재도전 미포함 기준으로 명시
- 2026-02-23: 기획 방향성(GD)과 구현 계약(TD) 경계를 분리하고, 상세 스키마/수식/검증 규칙을 `TD-002`로 이관
