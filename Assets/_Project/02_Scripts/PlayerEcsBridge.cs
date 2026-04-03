using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// <br/>Player GameObject가 "표현/입력/애니"를 담당
    /// <br/>ECS PlayerTag 엔티티(Proxy)에 입력 의도만 전달하고, 판정/상태 writer는 DOTS에서 단일 소유한다.
    /// <br/>단일 플레이어 전제: PlayerTag 싱글톤을 찾음(서브씬 로딩 타이밍 고려해 재시도 로직 포함)
    /// </summary>
    public sealed class PlayerEcsBridge : MonoBehaviour
    {
        [Header("Input")]
        public int PrimaryVacuumMouseButton = 0;   // Left Click
        public int SecondaryVacuumMouseButton = 1; // Right Click
        public PlayerCleanupActionSlotId PrimarySlot = PlayerCleanupActionSlotId.Primary;
        public PlayerCleanupActionSlotId SecondarySlot = PlayerCleanupActionSlotId.Secondary;

        [Header("Presentation Sync")]
        public bool ApplyEcsPositionToTransform = true;
        public bool ApplyEcsRotationToTransform = true;

        // Vacuum 상태 반영용 Animator (옵션)
        public Animator Animator;
        public string VacuumActiveBool = "VacuumActive";
        public string HitReactTrigger = "HitReact";
        public string VacuumBlockedTrigger = "VacuumBlocked";
        public string HazardCapturedTrigger = "HazardCaptured";
        public string HazardRemovedTrigger = "HazardRemoved";
        public string SourceStateChangedTrigger = "SourceStateChanged";

        [Header("Visual Impulse")]
        public float ImpulseSpringFrequency = 18f;
        [Range(0f, 2f)] public float ImpulseDampingRatio = 1f;
        public float ImpulseVisualBase = 0.08f;
        public float ImpulseVisualLossScale = 0.03f;
        public float ImpulseVisualPerFrameMax = 0.20f;
        public float ImpulseMaxOffset = 0.35f;

        [Header("BroomSweep Debug")]
        public bool EnableBroomSweepGizmos = true;
        public bool DrawOnlyWhenSelected = true;
        public bool DrawSearchRadius = true;
        public bool DrawStateLabel = true;
        public bool DrawCandidateBullets = false;
        [Min(1)] public int CandidateBulletMarkerLimit = 64;
        public float GizmoHeightOffset = 0.05f;
        [Min(4)] public int FullPathStepCount = 24;
        [Min(4)] public int BandStepCount = 12;

        private EntityManager _em;
        private Entity _playerEntity;
        private bool _hasPlayerEntity;
        private EntityQuery _replayQuery;
        private EntityQuery _bulletGizmoQuery;
        private Vector3 _visualImpulseOffset;
        private Vector3 _visualImpulseVelocity;
        private uint _lastUiFeedbackVersion;
        private uint _lastImpulseVersion;
        private static bool _warnedAnimatorMissingGlobal;
        private DemoShellPauseBridge _pauseBridge;

        private void Awake()
        {
            TryBind();
        }

        private void Update()
        {
            if (!_hasPlayerEntity
                || !_em.World.IsCreated
                || !_em.Exists(_playerEntity))
            {
                _hasPlayerEntity = false;
                TryBind();
                if (!_hasPlayerEntity) return;
            }

            // GO -> ECS : 입력 의도만 전달
            bool suppressLiveInput = IsReplayInputSuppressed() || IsPauseInputSuppressed();
            if (!suppressLiveInput)
            {
                var intent = _em.GetComponentData<PlayerInputIntentComponent>(_playerEntity);

                bool primaryPressed = Input.GetMouseButtonDown(PrimaryVacuumMouseButton);
                bool secondaryPressed = Input.GetMouseButtonDown(SecondaryVacuumMouseButton);
                if (primaryPressed || secondaryPressed)
                {
                    intent.VacuumRequested = 1;
                    intent.CleanupActionRequested = 1;
                    intent.RequestedCleanupActionSlot = (byte)(secondaryPressed ? SecondarySlot : PrimarySlot);
                    intent.Sequence += 1u;
                }

                _em.SetComponentData(_playerEntity, intent);
            }

            // ECS -> GO : Impulse 시각 오프셋(표현 전용, ECS 소유권 미변경)
            UpdateVisualImpulseOffset();

            // ECS -> GO : Vacuum 상태를 Animator에 반영(옵션)
            var presentSync = _em.GetComponentData<PlayerGoSyncComponent>(_playerEntity);
            if (ApplyEcsPositionToTransform)
                transform.position = (Vector3)presentSync.Position + _visualImpulseOffset;
            if (ApplyEcsRotationToTransform && presentSync.SyncRotation != 0)
                transform.rotation = presentSync.Rotation;

            if (Animator != null)
            {
                var v = _em.GetComponentData<VacuumRuntimeStateComponent>(_playerEntity);
                Animator.SetBool(VacuumActiveBool, v.IsActive != 0);
                ApplyAnimatorFeedbackTrigger();
            }
            else
            {
                WarnMissingAnimatorOnce();
            }
        }

        private void OnDrawGizmos()
        {
            if (DrawOnlyWhenSelected)
                return;

            DrawCleanupGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            DrawCleanupGizmos();
        }

        private void TryBind()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            _em = world.EntityManager;
            _replayQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ReplayInputControlComponent>());
            _bulletGizmoQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<BulletCaptureRuleComponent>(),
                ComponentType.ReadOnly<BulletActiveTag>());

            // PlayerTag 싱글톤 찾기 (서브씬 로딩 지연 대비)
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            if (q.IsEmptyIgnoreFilter)
            {
                _hasPlayerEntity = false;
                return;
            }

            _playerEntity = q.GetSingletonEntity();
            _hasPlayerEntity = _playerEntity != Entity.Null;
        }

        private bool IsReplayInputSuppressed()
        {
            return ReplayInputSuppressionUtility.IsLiveInputSuppressed(_em, _replayQuery);
        }

        private bool IsPauseInputSuppressed()
        {
            if (_pauseBridge == null)
                _pauseBridge = FindPauseBridge();

            return _pauseBridge != null && _pauseBridge.GameplayInputBlocked;
        }

        private void UpdateVisualImpulseOffset()
        {
            if (!_em.HasComponent<PlayerImpulsePresentationSnapshotComponent>(_playerEntity))
                return;

            var snapshot = _em.GetComponentData<PlayerImpulsePresentationSnapshotComponent>(_playerEntity);
            if (snapshot.Version != 0u && snapshot.Version != _lastImpulseVersion)
            {
                _lastImpulseVersion = snapshot.Version;
                var dir = new Vector3(snapshot.DirX, 0f, snapshot.DirZ);
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    int hitLoss = ResolveRecentHitLoss();
                    float magnitude = ComputeVisualImpulseMagnitude(snapshot.Magnitude, hitLoss);
                    _visualImpulseOffset += dir * magnitude;
                    _visualImpulseOffset = Vector3.ClampMagnitude(_visualImpulseOffset, Mathf.Max(0.01f, ImpulseMaxOffset));
                }
            }

            StepVisualImpulseSpring();
        }

        private void ApplyAnimatorFeedbackTrigger()
        {
            if (!_em.HasComponent<PlayerUiFeedbackPresentationSnapshotComponent>(_playerEntity))
                return;

            var snapshot = _em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(_playerEntity);
            if (snapshot.Version == 0u || snapshot.Version == _lastUiFeedbackVersion)
                return;

            _lastUiFeedbackVersion = snapshot.Version;
            string triggerName = ResolveTriggerName(snapshot.Type);
            if (string.IsNullOrEmpty(triggerName))
                return;

            Animator.SetTrigger(triggerName);
        }

        private string ResolveTriggerName(PlayerUiFeedbackEventType eventType)
        {
            return eventType switch
            {
                PlayerUiFeedbackEventType.PlayerHazardHit => HitReactTrigger,
                PlayerUiFeedbackEventType.VacuumStartBlocked => VacuumBlockedTrigger,
                PlayerUiFeedbackEventType.HazardCaptured => HazardCapturedTrigger,
                PlayerUiFeedbackEventType.HazardRemoved => HazardRemovedTrigger,
                PlayerUiFeedbackEventType.SourceStateChanged => SourceStateChangedTrigger,
                _ => null,
            };
        }

        private void WarnMissingAnimatorOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_warnedAnimatorMissingGlobal)
                return;
            _warnedAnimatorMissingGlobal = true;
            Debug.LogWarning("[PlayerEcsBridge] Animator reference is missing. Animator feedback is skipped.");
#endif
        }

        private int ResolveRecentHitLoss()
        {
            if (!_em.HasComponent<PlayerUiFeedbackPresentationSnapshotComponent>(_playerEntity))
                return 0;

            var uiSnapshot = _em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(_playerEntity);
            if (uiSnapshot.Type != PlayerUiFeedbackEventType.PlayerHazardHit)
                return 0;
            if (uiSnapshot.RemainingSec <= 0f)
                return 0;

            return Mathf.Max(0, uiSnapshot.Value);
        }

        private float ComputeVisualImpulseMagnitude(float gameplayMagnitude, int hitLoss)
        {
            float safeGameplayMagnitude = Mathf.Max(0f, gameplayMagnitude);
            float safeHitLoss = Mathf.Max(0f, hitLoss);
            float gain = Mathf.Max(0f, ImpulseVisualBase + ImpulseVisualLossScale * Mathf.Log(1f + safeHitLoss));
            float magnitude = safeGameplayMagnitude * gain;
            return Mathf.Min(magnitude, Mathf.Max(0f, ImpulseVisualPerFrameMax));
        }

        private void StepVisualImpulseSpring()
        {
            float dt = Mathf.Max(0f, Time.deltaTime);
            if (dt <= 0f)
                return;

            // 고주파 스프링 + 가변 frame dt에서의 수치 발산을 막기 위해 sub-step 적분한다.
            int subSteps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 120f)), 1, 8);
            float subDt = dt / subSteps;

            float angularFrequency = Mathf.Max(0.01f, ImpulseSpringFrequency);
            float dampingRatio = Mathf.Clamp(ImpulseDampingRatio, 0f, 4f);
            float spring = angularFrequency * angularFrequency;
            float damping = 2f * dampingRatio * angularFrequency;

            for (int i = 0; i < subSteps; i++)
            {
                Vector3 accel = (-spring * _visualImpulseOffset) - (damping * _visualImpulseVelocity);
                _visualImpulseVelocity += accel * subDt;
                _visualImpulseOffset += _visualImpulseVelocity * subDt;
            }

            float maxOffset = Mathf.Max(0.01f, ImpulseMaxOffset);
            if (_visualImpulseOffset.sqrMagnitude > maxOffset * maxOffset)
            {
                Vector3 normal = _visualImpulseOffset.normalized;
                _visualImpulseOffset = normal * maxOffset;
                float outwardVelocity = Vector3.Dot(_visualImpulseVelocity, normal);
                if (outwardVelocity > 0f)
                    _visualImpulseVelocity -= normal * outwardVelocity;
            }

            if (_visualImpulseOffset.sqrMagnitude <= 1e-5f && _visualImpulseVelocity.sqrMagnitude <= 1e-4f)
            {
                _visualImpulseOffset = Vector3.zero;
                _visualImpulseVelocity = Vector3.zero;
            }
        }

        private void DrawCleanupGizmos()
        {
            if (!EnableBroomSweepGizmos || !Application.isPlaying)
                return;

            if (!_hasPlayerEntity
                || !_em.World.IsCreated
                || !_em.Exists(_playerEntity))
            {
                _hasPlayerEntity = false;
                TryBind();
                if (!_hasPlayerEntity)
                    return;
            }

            if (!TryBuildCleanupGizmoContext(out var context))
                return;

            if (DrawSearchRadius && context.Geometry.SearchRadius > 0f)
            {
                Gizmos.color = Color.white;
                DrawXZCircle(context.Center, context.Geometry.SearchRadius, Mathf.Max(8, FullPathStepCount * 2));
            }

            if (context.ActionId != PlayerCleanupActionId.BroomSweep)
            {
                if (DrawStateLabel)
                    DrawStateLabelAt(
                        context.Center,
                        $"Legacy Compatibility Action | Action={context.ActionId} | Active={context.VacuumState.IsActive}");
                return;
            }

            bool hasValidSweepState = context.Geometry.CaptureReady != 0;
            if (hasValidSweepState)
                DrawLockedFacingAxes(context);

            if (context.VacuumState.IsActive != 0 && !hasValidSweepState)
            {
                if (DrawStateLabel)
                    DrawStateLabelAt(
                        context.Center,
                        $"INVALID SWEEP STATE | Active={context.VacuumState.IsActive} | Dir={context.SweepState.ActiveSweepDirectionSign} | Lock={context.SweepState.HasLockedFacing}");
                return;
            }

            if (hasValidSweepState)
            {
                DrawTrashSweepFullPath(context);
                DrawTrashSweepCurrentBand(context);
                DrawHazardFocusRect(context);

                if (DrawCandidateBullets)
                    DrawCandidateBulletMarkers(context);
            }

            if (DrawStateLabel)
                DrawStateLabelAt(context.Center, BuildStateLabel(context));
        }

        private bool TryBuildCleanupGizmoContext(out CleanupGizmoContext context)
        {
            context = default;

            if (!_em.HasComponent<PlayerCleanupActionStateComponent>(_playerEntity)
                || !_em.HasComponent<VacuumActivationConfigComponent>(_playerEntity)
                || !_em.HasComponent<VacuumRuntimeStateComponent>(_playerEntity)
                || !_em.HasComponent<PlayerCleanupSweepRuntimeStateComponent>(_playerEntity)
                || !_em.HasComponent<PlayerGoSyncComponent>(_playerEntity)
                || !_em.HasBuffer<PlayerCleanupActionProfileBufferElement>(_playerEntity))
                return false;

            var goSync = _em.GetComponentData<PlayerGoSyncComponent>(_playerEntity);
            var actionState = _em.GetComponentData<PlayerCleanupActionStateComponent>(_playerEntity);
            var vacuumState = _em.GetComponentData<VacuumRuntimeStateComponent>(_playerEntity);
            var vacuumConfig = _em.GetComponentData<VacuumActivationConfigComponent>(_playerEntity);
            var sweepState = _em.GetComponentData<PlayerCleanupSweepRuntimeStateComponent>(_playerEntity);
            var actionId = PlayerCleanupActionContractUtility.NormalizeRuntimeActionId(actionState.SelectedActionId, allowNone: true);
            var profiles = _em.GetBuffer<PlayerCleanupActionProfileBufferElement>(_playerEntity);
            var profile = PlayerCleanupActionDebugGeometryUtility.ResolveActionProfile(profiles, actionId);
            var geometry = PlayerCleanupActionDebugGeometryUtility.ResolveBroomSweepFrameGeometry(
                actionId,
                in vacuumConfig,
                in vacuumState,
                in sweepState,
                in profile);

            context = new CleanupGizmoContext
            {
                Center = new Vector3(goSync.Position.x, goSync.Position.y, goSync.Position.z) + (Vector3.up * GizmoHeightOffset),
                ActionId = actionId,
                Profile = profile,
                VacuumState = vacuumState,
                SweepState = sweepState,
                Geometry = geometry,
            };
            return true;
        }

        private void DrawLockedFacingAxes(in CleanupGizmoContext context)
        {
            float axisLength = Mathf.Max(0.75f, context.Geometry.SearchRadius * 0.35f);
            Vector3 center = context.Center;
            Vector3 forward = ToVector3XZ(context.Geometry.LockedForwardXZ) * axisLength;
            Vector3 right = ToVector3XZ(context.Geometry.LockedRightXZ) * axisLength;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center, center + forward);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(center, center + right);
        }

        private void DrawTrashSweepFullPath(in CleanupGizmoContext context)
        {
            ResolveSweepPathAngles(in context, out float startAngleDeg, out float endAngleDeg);
            Color pathColor = context.SweepState.ActiveSweepDirectionSign >= 0
                ? new Color(1f, 0.85f, 0.2f, 1f)
                : new Color(1f, 0.55f, 0.2f, 1f);

            Gizmos.color = pathColor;
            DrawSweepArc(
                context.Center,
                context.Profile.TrashSweepInnerRadius,
                startAngleDeg,
                endAngleDeg,
                Mathf.Max(4, FullPathStepCount),
                in context.Geometry);
            DrawSweepArc(
                context.Center,
                context.Profile.TrashSweepOuterRadius,
                startAngleDeg,
                endAngleDeg,
                Mathf.Max(4, FullPathStepCount),
                in context.Geometry);

            Vector3 outerStart = GetSweepPoint(context.Center, context.Profile.TrashSweepOuterRadius, startAngleDeg, in context.Geometry);
            Vector3 innerStart = GetSweepPoint(context.Center, context.Profile.TrashSweepInnerRadius, startAngleDeg, in context.Geometry);
            Vector3 outerEnd = GetSweepPoint(context.Center, context.Profile.TrashSweepOuterRadius, endAngleDeg, in context.Geometry);
            Vector3 innerEnd = GetSweepPoint(context.Center, context.Profile.TrashSweepInnerRadius, endAngleDeg, in context.Geometry);
            Gizmos.DrawLine(innerStart, outerStart);
            Gizmos.DrawLine(innerEnd, outerEnd);
        }

        private void DrawTrashSweepCurrentBand(in CleanupGizmoContext context)
        {
            float startAngleDeg = context.Geometry.CurrentSweepCenterAngleDeg - context.Profile.TrashSweepHalfAngleDeg;
            float endAngleDeg = context.Geometry.CurrentSweepCenterAngleDeg + context.Profile.TrashSweepHalfAngleDeg;

#if UNITY_EDITOR
            DrawFilledSweepBand(
                context.Center,
                context.Profile.TrashSweepInnerRadius,
                context.Profile.TrashSweepOuterRadius,
                startAngleDeg,
                endAngleDeg,
                Mathf.Max(4, BandStepCount),
                new Color(0.2f, 1f, 0.35f, 0.16f),
                in context.Geometry);
#endif

            Gizmos.color = new Color(0.2f, 1f, 0.35f, 1f);
            DrawSweepArc(
                context.Center,
                context.Profile.TrashSweepInnerRadius,
                startAngleDeg,
                endAngleDeg,
                Mathf.Max(4, BandStepCount),
                in context.Geometry);
            DrawSweepArc(
                context.Center,
                context.Profile.TrashSweepOuterRadius,
                startAngleDeg,
                endAngleDeg,
                Mathf.Max(4, BandStepCount),
                in context.Geometry);
            Gizmos.DrawLine(
                GetSweepPoint(context.Center, context.Profile.TrashSweepInnerRadius, startAngleDeg, in context.Geometry),
                GetSweepPoint(context.Center, context.Profile.TrashSweepOuterRadius, startAngleDeg, in context.Geometry));
            Gizmos.DrawLine(
                GetSweepPoint(context.Center, context.Profile.TrashSweepInnerRadius, endAngleDeg, in context.Geometry),
                GetSweepPoint(context.Center, context.Profile.TrashSweepOuterRadius, endAngleDeg, in context.Geometry));
        }

        private void DrawHazardFocusRect(in CleanupGizmoContext context)
        {
            Vector3 forward = ToVector3XZ(context.Geometry.LockedForwardXZ);
            Vector3 right = ToVector3XZ(context.Geometry.LockedRightXZ);
            float length = Mathf.Max(0f, context.Profile.HazardRectLength);
            float halfWidth = Mathf.Max(0f, context.Profile.HazardRectHalfWidth);

            Vector3 backLeft = context.Center - (right * halfWidth);
            Vector3 backRight = context.Center + (right * halfWidth);
            Vector3 frontLeft = context.Center + (forward * length) - (right * halfWidth);
            Vector3 frontRight = context.Center + (forward * length) + (right * halfWidth);

#if UNITY_EDITOR
            if (context.Geometry.HazardWindowActive != 0)
                DrawFilledQuad(backLeft, backRight, frontRight, frontLeft, new Color(1f, 0.2f, 0.2f, 0.18f));
#endif

            Gizmos.color = context.Geometry.HazardWindowActive != 0
                ? new Color(1f, 0.2f, 0.2f, 1f)
                : new Color(0.55f, 0.15f, 0.15f, 1f);
            Gizmos.DrawLine(backLeft, backRight);
            Gizmos.DrawLine(backRight, frontRight);
            Gizmos.DrawLine(frontRight, frontLeft);
            Gizmos.DrawLine(frontLeft, backLeft);
        }

        private void DrawCandidateBulletMarkers(in CleanupGizmoContext context)
        {
            if (_bulletGizmoQuery == default || _bulletGizmoQuery.IsEmptyIgnoreFilter)
                return;

            using var bullets = _bulletGizmoQuery.ToEntityArray(Allocator.Temp);
            int drawn = 0;
            float searchRangeSq = context.Geometry.SearchRadius * context.Geometry.SearchRadius;

            for (int i = 0; i < bullets.Length && drawn < Mathf.Max(1, CandidateBulletMarkerLimit); i++)
            {
                var bullet = bullets[i];
                if (_em.HasComponent<BulletDespawnRequestTag>(bullet) && _em.IsComponentEnabled<BulletDespawnRequestTag>(bullet))
                    continue;

                var tx = _em.GetComponentData<LocalTransform>(bullet);
                float dxp = tx.Position.x - context.Center.x;
                float dzp = tx.Position.z - context.Center.z;
                float distSq = dxp * dxp + dzp * dzp;
                if (distSq > searchRangeSq)
                    continue;

                float bulletRadius = _em.HasComponent<BulletRadiusComponent>(bullet)
                    ? math.max(0f, _em.GetComponentData<BulletRadiusComponent>(bullet).Value)
                    : 0f;
                var captureRule = _em.GetComponentData<BulletCaptureRuleComponent>(bullet).Value;

                Color markerColor = new Color(0.45f, 0.45f, 0.45f, 1f);
                if (captureRule == BulletCaptureRuleId.StandardCollectible)
                {
                    bool isTrashHit = PlayerCleanupActionDebugGeometryUtility.EvaluateBroomTrashCapture(
                        distSq,
                        dxp,
                        dzp,
                        bulletRadius,
                        in context.Profile,
                        in context.Geometry);
                    if (isTrashHit)
                        markerColor = new Color(0.2f, 1f, 0.35f, 1f);
                }
                else if (captureRule == BulletCaptureRuleId.RiskTimedResolve)
                {
                    bool isHazardHit = PlayerCleanupActionDebugGeometryUtility.EvaluateBroomHazardCapture(
                        dxp,
                        dzp,
                        bulletRadius,
                        in context.Profile,
                        in context.Geometry);
                    if (isHazardHit)
                        markerColor = new Color(1f, 0.2f, 0.2f, 1f);
                }

                Gizmos.color = markerColor;
                Gizmos.DrawSphere(new Vector3(tx.Position.x, tx.Position.y, tx.Position.z) + (Vector3.up * GizmoHeightOffset), 0.06f);
                drawn++;
            }
        }

        private string BuildStateLabel(in CleanupGizmoContext context)
        {
            if (context.ActionId != PlayerCleanupActionId.BroomSweep)
                return $"Action={context.ActionId} | Active={context.VacuumState.IsActive}";

            if (context.VacuumState.IsActive == 0)
            {
                int previewDirection = context.SweepState.NextSweepDirectionSign < 0 ? -1 : 1;
                return $"BroomSweep | Active=0 | NextDir={previewDirection:+0;-0}";
            }

            if (context.Geometry.CaptureReady == 0)
                return $"BroomSweep | Active=1 | INVALID SWEEP STATE";

            return $"BroomSweep | Active=1 | Dir={context.SweepState.ActiveSweepDirectionSign:+0;-0} | Progress={context.Geometry.Progress01:0.00} | Angle={context.Geometry.CurrentSweepCenterAngleDeg:0.0} | HazardWindow={context.Geometry.HazardWindowActive}";
        }

        private void DrawStateLabelAt(Vector3 center, string label)
        {
#if UNITY_EDITOR
            Handles.Label(center + (Vector3.up * 0.2f), label);
#endif
        }

        private static void ResolveSweepPathAngles(in CleanupGizmoContext context, out float startAngleDeg, out float endAngleDeg)
        {
            int directionSign = context.SweepState.ActiveSweepDirectionSign;
            if (directionSign == 0)
                directionSign = context.SweepState.NextSweepDirectionSign < 0 ? -1 : 1;

            if (directionSign > 0)
            {
                startAngleDeg = context.Profile.TrashSweepStartAngleDeg;
                endAngleDeg = context.Profile.TrashSweepEndAngleDeg;
            }
            else
            {
                startAngleDeg = -context.Profile.TrashSweepStartAngleDeg;
                endAngleDeg = -context.Profile.TrashSweepEndAngleDeg;
            }
        }

        private static Vector3 GetSweepPoint(Vector3 center, float radius, float angleDeg, in BroomSweepFrameGeometry geometry)
        {
            float radians = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float2 dir = (geometry.LockedForwardXZ * cos) + (geometry.LockedRightXZ * sin);
            return center + new Vector3(dir.x, 0f, dir.y) * radius;
        }

        private static Vector3 ToVector3XZ(float2 vector)
        {
            return new Vector3(vector.x, 0f, vector.y);
        }

        private static void DrawSweepArc(
            Vector3 center,
            float radius,
            float startAngleDeg,
            float endAngleDeg,
            int segments,
            in BroomSweepFrameGeometry geometry)
        {
            segments = Mathf.Max(1, segments);
            Vector3 prev = GetSweepPoint(center, radius, startAngleDeg, in geometry);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angleDeg = Mathf.Lerp(startAngleDeg, endAngleDeg, t);
                Vector3 next = GetSweepPoint(center, radius, angleDeg, in geometry);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static void DrawXZCircle(Vector3 center, float radius, int segments)
        {
            float step = 2f * Mathf.PI / Mathf.Max(3, segments);
            Vector3 prev = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

#if UNITY_EDITOR
        private static void DrawFilledSweepBand(
            Vector3 center,
            float innerRadius,
            float outerRadius,
            float startAngleDeg,
            float endAngleDeg,
            int segments,
            Color color,
            in BroomSweepFrameGeometry geometry)
        {
            segments = Mathf.Max(1, segments);
            Handles.color = color;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                float angle0 = Mathf.Lerp(startAngleDeg, endAngleDeg, t0);
                float angle1 = Mathf.Lerp(startAngleDeg, endAngleDeg, t1);
                Vector3 inner0 = GetSweepPoint(center, innerRadius, angle0, in geometry);
                Vector3 outer0 = GetSweepPoint(center, outerRadius, angle0, in geometry);
                Vector3 outer1 = GetSweepPoint(center, outerRadius, angle1, in geometry);
                Vector3 inner1 = GetSweepPoint(center, innerRadius, angle1, in geometry);
                Handles.DrawAAConvexPolygon(inner0, outer0, outer1, inner1);
            }
        }

        private static void DrawFilledQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Handles.color = color;
            Handles.DrawAAConvexPolygon(a, b, c, d);
        }
#endif

        private static DemoShellPauseBridge FindPauseBridge()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DemoShellPauseBridge>();
#else
            return Object.FindObjectOfType<DemoShellPauseBridge>();
#endif
        }

        private struct CleanupGizmoContext
        {
            public Vector3 Center;
            public PlayerCleanupActionId ActionId;
            public PlayerCleanupActionProfileBufferElement Profile;
            public VacuumRuntimeStateComponent VacuumState;
            public PlayerCleanupSweepRuntimeStateComponent SweepState;
            public BroomSweepFrameGeometry Geometry;
        }
    }
}
