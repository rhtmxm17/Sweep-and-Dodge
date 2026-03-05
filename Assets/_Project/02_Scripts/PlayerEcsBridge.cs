using Unity.Entities;
using UnityEngine;

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

        private const float VacuumGizmoDuration = 0.2f;
        private const float VacuumGizmoRadius = 2.88f;
        private const int VacuumGizmoSegments = 48;

        private EntityManager _em;
        private Entity _playerEntity;
        private bool _hasPlayerEntity;
        private EntityQuery _replayQuery;
        private float _vacuumGizmoUntilTime;
        private Vector3 _visualImpulseOffset;
        private Vector3 _visualImpulseVelocity;
        private uint _lastUiFeedbackVersion;
        private uint _lastImpulseVersion;
        private static bool _warnedAnimatorMissingGlobal;

        private void Awake()
        {
            TryBind();
        }

        private void Update()
        {
            if (!_hasPlayerEntity)
            {
                TryBind();
                if (!_hasPlayerEntity) return;
            }

            // GO -> ECS : 입력 의도만 전달
            bool suppressLiveInput = IsReplayInputSuppressed();
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
                    _vacuumGizmoUntilTime = Time.time + VacuumGizmoDuration;
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
            bool isActiveWindow = Application.isPlaying && Time.time <= _vacuumGizmoUntilTime;
            Gizmos.color = isActiveWindow ? new Color(1f, 0.35f, 0.35f) : Color.cyan;
            DrawXZCircle(transform.position, VacuumGizmoRadius, VacuumGizmoSegments);
        }

        private static void DrawXZCircle(Vector3 center, float radius, int segments)
        {
            float step = 2f * Mathf.PI / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void TryBind()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            _em = world.EntityManager;
            _replayQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ReplayInputControlComponent>());

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
    }
}
