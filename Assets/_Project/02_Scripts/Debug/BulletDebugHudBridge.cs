using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

namespace SweepNDodge.DotsBullets
{
    public sealed class BulletDebugHudBridge : MonoBehaviour
    {
        [Header("HUD")]
        public bool ShowHud = true;
        public Rect HudRect = new Rect(12f, 12f, 520f, 420f);
        public bool ShowSourceDirectorStates = true;
        public int MaxSourceRows = 24;
        public float SourceListHeight = 180f;

        [Header("Stress Preset")]
        public int BurstCount = 100000;
        public int SustainFrames = 300;
        public int SustainPerFrame = 2000;
        public int PreferredBulletTypeKey = -1;

        private EntityManager _em;
        private EntityQuery _hudQuery;
        private EntityQuery _stressQuery;
        private EntityQuery _sourceDirectorQuery;
        private bool _isBound;
        private Vector2 _sourceScroll;
        private readonly List<SourceDirectorHudRow> _sourceRows = new List<SourceDirectorHudRow>(32);

        private struct SourceDirectorHudRow
        {
            public uint StableId;
            public int EntityIndex;
            public RunDirectorSourceStateId DirectorState;
            public SourceStateId ClipState;
            public SourceStateId SourceState;
            public float DensityScale;
        }

        private void Update()
        {
            if (TryBind())
                RefreshSourceDirectorSnapshot();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !ShowHud)
                return;

            if (!TryBind())
                return;
            if (_hudQuery.IsEmptyIgnoreFilter || _stressQuery.IsEmptyIgnoreFilter)
                return;

            var hud = _em.GetComponentData<DebugHudMetricsComponent>(_hudQuery.GetSingletonEntity());
            var stress = _em.GetComponentData<StressSwitchStateComponent>(_stressQuery.GetSingletonEntity());

            GUILayout.BeginArea(HudRect, GUI.skin.box);
            GUILayout.Label("[Bullet Debug HUD]");
            GUILayout.Label($"frameTime(ms): {hud.FrameTimeMs:0.00}");
            GUILayout.Label($"active: {hud.ActiveBullets}");
            GUILayout.Label($"spawn/despawn: {hud.SpawnedThisFrame} / {hud.DespawnedThisFrame}");
            GUILayout.Label($"pending: {hud.PendingBacklog}");
            GUILayout.Label($"deferred(budget/pool): {hud.DeferredByBudget} / {hud.DeferredByPool}");
            GUILayout.Label($"drop/expire: {hud.DroppedThisFrame} / {hud.ExpiredThisFrame}");
            GUILayout.Space(6f);
            GUILayout.Label($"sustainRemaining: {stress.RemainingFrames}");
            DrawSourceDirectorStatesSection();

            if (GUILayout.Button($"Stress Burst x{BurstCount}"))
                RequestBurst();
            if (GUILayout.Button($"Stress Sustain {SustainFrames}f x{SustainPerFrame}"))
                RequestSustain();
            if (GUILayout.Button("Stop Sustain"))
                RequestStopSustain();

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
            _hudQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<DebugHudMetricsComponent>());
            _stressQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<StressSwitchStateComponent>());
            _sourceDirectorQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceRunDirectorStateComponent>(),
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadOnly<SourceSpawnComponent>());
            _isBound = true;
            return true;
        }

        private void RefreshSourceDirectorSnapshot()
        {
            _sourceRows.Clear();
            if (!ShowSourceDirectorStates || _sourceDirectorQuery.IsEmptyIgnoreFilter)
                return;

            int maxRows = Mathf.Max(1, MaxSourceRows);
            using var entities = _sourceDirectorQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            int takeCount = Mathf.Min(maxRows, entities.Length);
            for (int i = 0; i < takeCount; i++)
            {
                var e = entities[i];
                var stable = _em.GetComponentData<SourceStableIdComponent>(e);
                var director = _em.GetComponentData<SourceRunDirectorStateComponent>(e);
                var source = _em.GetComponentData<SourceSpawnComponent>(e);
                _sourceRows.Add(new SourceDirectorHudRow
                {
                    StableId = stable.Value,
                    EntityIndex = e.Index,
                    DirectorState = director.State,
                    ClipState = director.SelectedClipState,
                    SourceState = source.State,
                    DensityScale = director.DensityScale,
                });
            }
        }

        private void DrawSourceDirectorStatesSection()
        {
            if (!ShowSourceDirectorStates)
                return;

            GUILayout.Space(8f);
            GUILayout.Label("[Run Director / Source States]");
            if (_sourceDirectorQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("no sources");
                return;
            }

            _sourceScroll = GUILayout.BeginScrollView(_sourceScroll, GUILayout.Height(Mathf.Max(60f, SourceListHeight)));
            for (int i = 0; i < _sourceRows.Count; i++)
            {
                var row = _sourceRows[i];
                GUILayout.Label(
                    $"#{row.StableId} (E{row.EntityIndex})  dir:{ToDirectorStateLabel(row.DirectorState)}  " +
                    $"clip:{ToSourceStateLabel(row.ClipState)}  src:{ToSourceStateLabel(row.SourceState)}  x{row.DensityScale:0.00}");
            }
            int remaining = _sourceDirectorQuery.CalculateEntityCount() - _sourceRows.Count;
            if (remaining > 0)
                GUILayout.Label($"+{remaining} more...");
            GUILayout.EndScrollView();
        }

        private static string ToDirectorStateLabel(RunDirectorSourceStateId state)
        {
            return state switch
            {
                RunDirectorSourceStateId.Baseline => "Baseline",
                RunDirectorSourceStateId.Pressure => "Pressure",
                RunDirectorSourceStateId.Finish => "Finish",
                _ => "Unknown",
            };
        }

        private static string ToSourceStateLabel(SourceStateId state)
        {
            return state switch
            {
                SourceStateId.Normal => "Normal",
                SourceStateId.Weakened => "Weakened",
                SourceStateId.Depleted => "Depleted",
                _ => "Unknown",
            };
        }

        private void RequestBurst()
        {
            var e = _stressQuery.GetSingletonEntity();
            var state = _em.GetComponentData<StressSwitchStateComponent>(e);
            state.Mode = (byte)StressSwitchModeId.BurstOnce;
            state.BurstCount = Mathf.Max(0, BurstCount);
            state.PreferredBulletTypeKey = PreferredBulletTypeKey;
            state.RequestExecute = 1;
            _em.SetComponentData(e, state);
        }

        private void RequestSustain()
        {
            var e = _stressQuery.GetSingletonEntity();
            var state = _em.GetComponentData<StressSwitchStateComponent>(e);
            state.Mode = (byte)StressSwitchModeId.Sustain;
            state.SustainFrames = Mathf.Max(0, SustainFrames);
            state.SustainPerFrame = Mathf.Max(0, SustainPerFrame);
            state.PreferredBulletTypeKey = PreferredBulletTypeKey;
            state.RequestExecute = 1;
            _em.SetComponentData(e, state);
        }

        private void RequestStopSustain()
        {
            var e = _stressQuery.GetSingletonEntity();
            var state = _em.GetComponentData<StressSwitchStateComponent>(e);
            state.Mode = (byte)StressSwitchModeId.StopSustain;
            state.RequestExecute = 1;
            _em.SetComponentData(e, state);
        }
    }
}
