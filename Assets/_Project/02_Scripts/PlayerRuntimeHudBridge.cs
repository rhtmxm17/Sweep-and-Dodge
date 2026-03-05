using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 플레이어 HUD 렌더러.
    /// - ECS 스냅샷을 읽어 OnGUI로 표시한다.
    /// - Stage 메타는 DemoShellFlowController를 read-only로 조회한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRuntimeHudBridge : MonoBehaviour
    {
        [Header("HUD")]
        public bool ShowHud = true;
        public Rect HudRect = new Rect(12f, 332f, 420f, 188f);

        [Header("Stage Meta (Read-only)")]
        public DemoShellFlowController DemoShell;

        private EntityManager _em;
        private EntityQuery _snapshotQuery;
        private bool _isBound;
        private bool _warnedBindFailure;

        private PlayerHudSnapshotComponent _lastSnapshot;
        private bool _hasSnapshot;
        private int _lastStageId;
        private DemoShellScreenId _lastScreen;

        public bool HasSnapshot => _hasSnapshot;
        public int LastStageId => _lastStageId;
        public DemoShellScreenId LastScreen => _lastScreen;
        public bool IsHitFlashVisible => _lastSnapshot.HitFlashRemainingSec > 0f && _lastSnapshot.LastHitLossValue > 0;

        public bool TryGetLastSnapshot(out PlayerHudSnapshotComponent snapshot)
        {
            snapshot = _lastSnapshot;
            return _hasSnapshot;
        }

        private void OnEnable()
        {
            EnsureDemoShellReference();
            TryBind();
        }

        private void Update()
        {
            EnsureDemoShellReference();

            if (!TryBind())
                return;

            var snapshotEntity = ResolveFirstEntity(_snapshotQuery);
            if (snapshotEntity == Entity.Null)
                return;

            _lastSnapshot = _em.GetComponentData<PlayerHudSnapshotComponent>(snapshotEntity);
            _hasSnapshot = true;

            if (DemoShell != null)
            {
                _lastStageId = Mathf.Max(0, DemoShell.CurrentStageId);
                _lastScreen = DemoShell.CurrentScreen;
            }
            else
            {
                _lastStageId = 0;
                _lastScreen = DemoShellScreenId.Title;
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !ShowHud || !_hasSnapshot)
                return;

            GUILayout.BeginArea(HudRect, GUI.skin.box);
            GUILayout.Label("[Player HUD]");
            GUILayout.Label(
                $"Stage #{_lastStageId} / screen:{_lastScreen} / state:{ToStageStateLabel(_lastSnapshot.StageState)} / t:{_lastSnapshot.StageStateElapsedSec:0.00}s");

            int carryCap = Mathf.Max(0, _lastSnapshot.CarryCapacity);
            int carryLoad = Mathf.Clamp(_lastSnapshot.CarryLoad, 0, carryCap <= 0 ? int.MaxValue : carryCap);
            float carryRatio = carryCap <= 0 ? 0f : Mathf.Clamp01((float)carryLoad / carryCap);
            GUILayout.Label($"CarryBin {carryLoad}/{carryCap} ({carryRatio * 100f:0}%)");

            GUILayout.Label($"Source Depleted {_lastSnapshot.DepletedSourceCount}/{_lastSnapshot.TotalSourceCount}");
            if (_lastSnapshot.PressureSourceStableId > 0)
            {
                GUILayout.Label(
                    $"Pressure Source #{_lastSnapshot.PressureSourceStableId} " +
                    $"{_lastSnapshot.PressureSourceCollected}/{_lastSnapshot.PressureSourceThresholdDepleted} " +
                    $"({_lastSnapshot.PressureSourceProgress01 * 100f:0}%)");
            }
            else
            {
                GUILayout.Label("Pressure Source -");
            }

            if (IsHitFlashVisible)
            {
                float fade = Mathf.Clamp01(_lastSnapshot.HitFlashRemainingSec / 0.6f);
                var prevColor = GUI.color;
                GUI.color = Color.Lerp(new Color(1f, 0.35f, 0.35f, 0.7f), new Color(1f, 0.35f, 0.35f, 1f), fade);
                GUILayout.Label($"HIT -{_lastSnapshot.LastHitLossValue}");
                GUI.color = prevColor;
            }

            GUILayout.EndArea();
        }

        private bool TryBind()
        {
            if (_isBound)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            _em = world.EntityManager;
            _snapshotQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<PlayerHudSnapshotComponent>());
            _isBound = true;
            _warnedBindFailure = false;
            return true;
        }

        private void EnsureDemoShellReference()
        {
            if (DemoShell != null)
                return;

            DemoShell = GetComponent<DemoShellFlowController>();
            if (DemoShell != null)
                return;

#if UNITY_2023_1_OR_NEWER
            DemoShell = FindFirstObjectByType<DemoShellFlowController>();
#else
            DemoShell = FindObjectOfType<DemoShellFlowController>();
#endif

            if (DemoShell == null && !_warnedBindFailure)
            {
                _warnedBindFailure = true;
                Debug.LogWarning("[PlayerRuntimeHudBridge] DemoShellFlowController was not found. Stage meta will be hidden.");
            }
        }

        private Entity ResolveFirstEntity(EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        private static string ToStageStateLabel(RunDirectorStageStateId state)
        {
            return state switch
            {
                RunDirectorStageStateId.Idle => "Idle",
                RunDirectorStageStateId.Running => "Running",
                RunDirectorStageStateId.ClearReady => "ClearReady",
                RunDirectorStageStateId.Completed => "Completed",
                _ => "Unknown",
            };
        }
    }
}
