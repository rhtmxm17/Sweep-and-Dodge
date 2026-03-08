# ADR-20260308-01-stage-topology-lifecycle-and-failure-policy
> 장주기 스테이지를 위한 StageTopology lifecycle/failure policy와 boundary-only apply 계약 고정

## 배경
- `StageTopology v1`은 `StageCatalogSO + StageLayoutSO + StageDefinitionSO` 기반으로 `Source/Deposit` topology를 runtime template reconcile로 생성/재사용할 수 있게 만들었다.
- 하지만 topology apply를 언제 허용하는지, apply 실패 시 기존 topology를 유지할지 비활성화할지, reconcile 결과로 남는 owned entity를 어떤 lifecycle로 관리할지는 문서와 테스트에 완전히 고정돼 있지 않았다.
- 본 프로젝트의 스테이지는 2분 이상의 장주기를 전제로 하므로, 플레이 도중 topology churn은 사실상 강제 리셋과 같은 체감 리스크를 가진다.

## 결정
- topology apply는 stage 경계에서만 허용한다.
  - 허용: `Idle`, `Completed`, 초기 비플레이 경계
  - 비허용: `Running`, `ClearReady`
- 비허용 시점 요청은 one-shot 요청만 consume하고, 현재 applied topology와 `StageTopologyStateComponent`는 유지한다.
- topology infrastructure failure 시 기본 정책은 `현재 applied topology 유지`다.
  - `SelectedStageId`는 새 요청으로 갱신 가능
  - `AppliedStageId`는 마지막 성공 적용 상태를 유지
  - `Ready=0`으로 두어 새 start만 hard gate한다.
- topology-owned entity lifecycle은 `disable-to-pool`을 기본으로 한다.
  - instantiate -> reuse -> mapped-active -> pooled-disabled
  - 성공 apply 후 현재 stage에 매핑되지 않은 owned entity는 전부 pooled-disabled로 전환한다.
  - destroy 기반 회수는 도입하지 않는다.
- data mismatch는 `warn + partial apply`, infrastructure failure는 `Ready=0 hard gate`로 구분한다.

## 대안
- 대안 A: `Running` 중에도 즉시 topology reapply 허용
  - 장점: hot-reload나 동적 스테이지 전환이 단순해진다.
  - 단점: 장주기 스테이지에서 source/deposit entity churn이 플레이 리셋 수준의 리스크를 만든다.
  - 기각 사유: 현재 데모 목적과 안정성 우선순위에 맞지 않다.
- 대안 B: topology apply 실패 시 현재 owned entity를 전부 비활성화
  - 장점: 잘못된 상태 잔존을 빠르게 제거한다.
  - 단점: 실패 한 번으로 현재 플레이를 즉시 파괴한다.
  - 기각 사유: 장주기 스테이지 UX와 회귀 리스크가 너무 크다.
- 대안 C: 실패 원인별로 유지/비활성화를 세분화
  - 장점: 정책을 미세하게 최적화할 수 있다.
  - 단점: 구현/테스트/문서 복잡도가 커지고 확장 규칙이 흐려진다.
  - 기각 사유: `Obstacle/Visual` 확장 전 hardening 단계에서는 과하다.

## 결과
- 긍정 효과
  - 장주기 스테이지 중 topology apply 요청이 현재 플레이를 흔들지 않게 된다.
  - `Ready=0 hard gate`와 `warn + partial apply`의 경계가 명확해져 디버깅과 테스트가 쉬워진다.
  - `Obstacle/Visual`을 이후 같은 lifecycle 규칙 위에 얹을 수 있다.
- 트레이드오프
  - mid-run live topology edit/debug는 기본 계약에서 제외된다.
  - failure 시 이전 topology를 유지하므로, 잘못된 최신 요청 대신 직전 적용 결과가 남는 보수 정책을 택한다.

## 후속
- `TD-015`, `TD-010`에 lifecycle/failure policy와 boundary-only apply 규칙을 반영한다.
- `Obstacle/Visual` 확장 전 H4에서 template catalog 일반화와 kind별 optionality 정책을 정리한다.
