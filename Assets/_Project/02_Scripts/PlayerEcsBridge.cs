using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// <br/>Player GameObject가 "표현/입력/애니"를 담당
    /// <br/>ECS PlayerTag 엔티티(Proxy)에 Transform/입력을 밀어넣어 판정은 DOTS에서 처리
    /// <br/>단일 플레이어 전제: PlayerTag 싱글톤을 찾음(서브씬 로딩 타이밍 고려해 재시도 로직 포함)
    /// </summary>
    public sealed class PlayerEcsBridge : MonoBehaviour
    {
        [Header("Input")]
        public int PrimaryVacuumMouseButton = 0;   // Left Click
        public int SecondaryVacuumMouseButton = 1; // Right Click
        public PlayerCleanupActionSlotId PrimarySlot = PlayerCleanupActionSlotId.Primary;
        public PlayerCleanupActionSlotId SecondarySlot = PlayerCleanupActionSlotId.Secondary;

        [Header("Sync")]
        public bool SyncRotation = true;

        // Vacuum 상태 반영용 Animator (옵션)
        public Animator Animator;
        public string VacuumActiveBool = "VacuumActive";

        private const float VacuumGizmoDuration = 0.2f;
        private const float VacuumGizmoRadius = 2.88f;
        private const int VacuumGizmoSegments = 48;

        private EntityManager _em;
        private Entity _playerEntity;
        private bool _hasPlayerEntity;
        private EntityQuery _replayQuery;
        private float _vacuumGizmoUntilTime;

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

            // GO -> ECS 동기화
            bool suppressLiveInput = IsReplayInputSuppressed();
            if (!suppressLiveInput)
            {
                var sync = _em.GetComponentData<PlayerGoSyncComponent>(_playerEntity);
                sync.Position = transform.position;
                sync.SyncRotation = (byte)(SyncRotation ? 1 : 0);
                if (SyncRotation) sync.Rotation = transform.rotation;

                bool primaryPressed = Input.GetMouseButtonDown(PrimaryVacuumMouseButton);
                bool secondaryPressed = Input.GetMouseButtonDown(SecondaryVacuumMouseButton);
                if (primaryPressed || secondaryPressed)
                {
                    sync.VacuumRequested = 1;
                    sync.CleanupActionRequested = 1;
                    sync.RequestedCleanupActionSlot = (byte)(secondaryPressed ? SecondarySlot : PrimarySlot);
                    _vacuumGizmoUntilTime = Time.time + VacuumGizmoDuration;
                }

                _em.SetComponentData(_playerEntity, sync);
            }

            // ECS -> GO : Vacuum 상태를 Animator에 반영(옵션)
            if (Animator != null)
            {
                var v = _em.GetComponentData<VacuumRuntimeStateComponent>(_playerEntity);
                Animator.SetBool(VacuumActiveBool, v.IsActive != 0);
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
            if (ReplaySessionStaging.IsPlaybackStartupPending)
                return true;
            if (!_hasPlayerEntity || _replayQuery.IsEmptyIgnoreFilter)
                return false;

            var control = _em.GetComponentData<ReplayInputControlComponent>(_replayQuery.GetSingletonEntity());
            return control.Mode == ReplayInputModeId.Playback;
        }
    }
}
