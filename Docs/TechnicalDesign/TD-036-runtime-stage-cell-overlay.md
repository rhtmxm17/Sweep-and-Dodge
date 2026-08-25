# Runtime Stage Cell Overlay

## Metadata
- doc_id: `TD-036`
- type: `TechnicalDesign`
- status: `active`
- last_updated: `2026-08-25`
- related_docs:
  - [TD-015-stage-map-layout-authoring-and-catalog-pipeline.md](./TD-015-stage-map-layout-authoring-and-catalog-pipeline.md)
  - [TD-026-source-pollution-recovery-wave-contract.md](./TD-026-source-pollution-recovery-wave-contract.md)
  - [TD-034-stage-map-editor-replacement.md](./TD-034-stage-map-editor-replacement.md)
- related_adr: none

> 테크 데모 플레이 중 Stage Cell의 이동 가능성, Source, Deposit 의미와 Source spawn 가능 상태를 런타임 빌드에서도 읽을 수 있게 하는 표현 계약이다.

## 1. 목표/비목표
- 목표:
  - `StageLayoutSO` 기반 정적 Cell 의미를 런타임 절차적 메시로 표시한다.
  - 실제 `SourcePollutionGridComponent`와 `SourcePollutionCellBuffer.IsActive`를 read-only로 표시한다.
  - Cell별 GameObject/Prefab/Sprite 없이 동작하고 steady state managed allocation을 0 B로 유지한다.
  - Stage Editor 팔레트와 의미를 맞추되 실제 플레이 화면에서 과도하게 난잡하지 않게 한다.
- 비목표:
  - pollution `Value` heatmap.
  - HUD 범례, 텍스처/Sprite 제작, Deposit 동적 상태.
  - Stage Editor overlay 구현 변경.
  - pollution writer, ECS update order, Fence 규칙 변경.

## 2. 표현 계약
- 일반 이동 가능 Cell은 옅은 중립 grid만 표시한다.
- 이동 차단만 방향성 패턴을 사용한다.
  - `BlockPlayer`: 적색 `/` hatch.
  - `BlockBullet`: 자홍색 `\` hatch.
  - 둘 다 차단: 어두운 적색 바탕과 cross hatch.
- Source와 Deposit에는 hatch 또는 `X`를 사용하지 않는다.
  - Source: cyan `(0.1, 0.85, 1)` 면과 region 바깥 둘레 실선.
  - Deposit: amber `(1, 0.75, 0.1)` 면 alpha `0.16`과 region 바깥 둘레 실선 alpha `0.32`.
  - region 내부 Cell 경계는 외곽선으로 만들지 않는다.
- Source 동적 면:
  - `IsValid == 0`: 표시하지 않는다.
  - `IsActive != 0`: alpha `0.33`.
  - `IsActive == 0`: alpha `0.05`.
  - `SourceStateId.Depleted`: 모든 valid Cell alpha `0.04`.
  - active -> inactive fade-out `0.20s`, inactive -> active fade-in `0.35s`.
  - 정적 Source region 외곽선은 alpha `0.28`로 유지한다.

## 3. 소유권과 데이터 흐름
- `StageGridVisualController`가 GO 표현 계층의 단일 owner다.
  - 기존 `GridVisualPrefab` instance.
  - 정적 Stage Cell 통합 mesh.
  - Source별 동적 pollution mesh와 fade cache.
- 정적 geometry authority는 applied stage의 `StageLayoutSO.Grid/Cells`다.
- 동적 geometry/state authority는 Source entity의 아래 read-only 데이터다.
  - `SourceStableIdComponent`
  - `SourceSpawnComponent.State`
  - `SourcePollutionGridComponent`
  - `SourcePollutionCellBuffer`
- `SourcePollutionUpdateSystem`의 단일 writer 계약은 바꾸지 않는다.
- controller는 `Topology Ready` 전체 구간에 표시하며 Ready 해제, stage 변경, disable에서 모든 transient object와 mesh를 정리한다.

## 4. 업데이트 순서/동기화
- ECS 파이프라인 `ExecutionBegin -> Simulation -> Request -> ExecutionEnd`은 변경하지 않는다.
- 별도 ECS presentation system을 추가하지 않는다.
- Source entity 목록은 topology 적용 후 캐시하고, 상태는 unscaled time 기준 `0.1s`마다 polling한다.
- polling 전 Source query dependency만 완료한다. `EntityManager.CompleteAllTrackedJobs()`는 사용하지 않는다.
- ECS 데이터는 read-only이며 새 Fence 또는 Enableable 전환을 추가하지 않는다.
- polling 사이에는 cached target alpha를 frame마다 보간한다.

## 5. 렌더링/성능 제약
- Cell별 GameObject를 만들지 않는다.
- 정적 grid/movement/region geometry는 통합 mesh 하나를 사용한다.
- Source는 Source별 동적 mesh 하나를 사용한다.
- URP unlit vertex-color transparent shader와 공유 Material 하나를 사용한다.
- vertex/color/index 배열은 topology 적용 시 생성하고 재사용한다.
- 색상 buffer는 target 변경 또는 fade 진행 중에만 갱신한다.
- 워밍업 이후 steady state와 fade update의 managed allocation 목표는 `0 B/frame`이다.
- polling에서 의미 있는 main-thread dependency wait가 측정되면 임의로 snapshot system을 추가하지 않고 후속 설계로 보고한다.

## 6. 작업 분해/진행 상태
- `P1` static overlay geometry와 URP shader/material: `completed`
- `P2` Source pollution polling/fade: `completed`
- `P3` scene binding과 legacy Gizmo bootstrap 제거: `completed`
- `P4` EditMode/PlayMode/Player build 검증: `completed`

## 7. 검증 계획/합격 기준
- EditMode:
  - movement 패턴, Source/Deposit 면과 region perimeter geometry를 검증한다.
  - Source/Deposit 내부에 diagonal/`X` geometry가 없음을 검증한다.
  - 실제 pollution grid origin/cell size와 valid mask mapping을 검증한다.
  - active/inactive/depleted alpha와 fade 시간을 검증한다.
  - Ready/stage change/disable lifecycle과 managed allocation을 검증한다.
- PlayMode:
  - 전용 sample verification scene에서 topology Ready 이후 overlay 생성을 검증한다.
  - active -> inactive -> active -> depleted 표시 전환을 검증한다.
  - Game View capture로 가독성, z-fighting, 진입 불가 오인 여부를 확인한다.
- 공통 gate:
  - `compile -> console error 0 -> EditMode -> dedicated PlayMode smoke -> Windows x64 Development Build`.

### 7.1 2026-08-25 검증 결과
- Unity compile 성공, 최종 Console `error` 0건.
- EditMode `531/531` 통과.
- 전용 PlayMode sample verification `2/2` 통과.
- Game View capture에서 Source cyan/Deposit amber 면과 region 외곽선, movement-only hatch, z-fighting 부재를 확인했다.
- overlay 전용 allocation test에서 warm steady/fade tick managed allocation `0 B`를 확인했다.
- Game View frame timing 표본: CPU `14.08 ms`, GPU `0.48 ms`.
- Windows x64 IL2CPP Development Player build 성공 후 12초 startup smoke에서 오류 없이 실행 상태를 유지했다.

## 8. 변경 이력
- 2026-08-25: Runtime Stage Cell overlay v1 채택안 작성.
- 2026-08-25: static/dynamic overlay, scene binding, legacy runtime Gizmo bootstrap 제거 및 자동 검증 반영.
