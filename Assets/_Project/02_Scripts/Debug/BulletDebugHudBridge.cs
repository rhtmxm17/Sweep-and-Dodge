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
        private EntityQuery _directorWeightQuery;
        private EntityQuery _stageStateQuery;
        private EntityQuery _stageGateQuery;
        private EntityQuery _stageRequestQuery;
        private EntityQuery _stageSignalQuery;
        private EntityQuery _combatMetricsQuery;
        private bool _isBound;
        private Vector2 _sourceScroll;
        private readonly List<SourceDirectorHudRow> _sourceRows = new List<SourceDirectorHudRow>(32);
        private PressureWeightSnapshot _weightSnapshot;
        private bool _showCoreMetrics = true;
        private bool _showStageState = true;
        private bool _showCombatEvent = true;
        private bool _showStressControl = true;
        private bool _showSourceDirectorState = true;

        private struct SourceDirectorHudRow
        {
            public uint StableId;
            public int EntityIndex;
            public RunDirectorSourceStateId DirectorState;
            public SourceStateId ClipState;
            public SourceStateId SourceState;
            public float DensityScale;
            public float PressureScore;
            public float OccupancyInput;
            public float HoldInputSec;
        }

        private struct PressureWeightSnapshot
        {
            public float Occupancy;
            public float HoldSec;
        }

        private void Update()
        {
            if (TryBind())
            {
                RefreshPressureWeightSnapshot();
                RefreshSourceDirectorSnapshot();
            }
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
            DrawCategoryToggleSection();

            if (_showCoreMetrics)
            {
                GUILayout.Label($"frameTime(ms): {hud.FrameTimeMs:0.00}");
                GUILayout.Label($"active: {hud.ActiveBullets}");
                GUILayout.Label($"spawn/despawn: {hud.SpawnedThisFrame} / {hud.DespawnedThisFrame}");
                GUILayout.Label($"pending: {hud.PendingBacklog}");
                GUILayout.Label($"deferred(budget/pool): {hud.DeferredByBudget} / {hud.DeferredByPool}");
                GUILayout.Label($"drop/expire: {hud.DroppedThisFrame} / {hud.ExpiredThisFrame}");
                GUILayout.Space(6f);
            }

            if (_showStressControl)
                GUILayout.Label($"sustainRemaining: {stress.RemainingFrames}");
            if (_showStageState)
                DrawStageStateSection();
            if (_showCombatEvent)
                DrawCombatEventSection();
            if (_showSourceDirectorState)
            {
                GUILayout.Label(
                    $"pressureW occ:{_weightSnapshot.Occupancy:0.00} hold:{_weightSnapshot.HoldSec:0.00}");
                DrawSourceDirectorStatesSection();
            }

            if (_showStressControl)
            {
                if (GUILayout.Button($"Stress Burst x{BurstCount}"))
                    RequestBurst();
                if (GUILayout.Button($"Stress Sustain {SustainFrames}f x{SustainPerFrame}"))
                    RequestSustain();
                if (GUILayout.Button("Stop Sustain"))
                    RequestStopSustain();
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
            _hudQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<DebugHudMetricsComponent>());
            _stressQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<StressSwitchStateComponent>());
            _sourceDirectorQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceRunDirectorStateComponent>(),
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadOnly<SourceSpawnComponent>());
            _directorWeightQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<RunDirectorPressureWeightSingletonTag>(),
                ComponentType.ReadOnly<RunDirectorPressureWeightBuffer>());
            _stageStateQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageStateComponent>());
            _stageGateQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageGateComponent>());
            _stageRequestQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageRequestComponent>());
            _stageSignalQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageSignalComponent>());
            _combatMetricsQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<CombatEventMetricsComponent>());
            _isBound = true;
            return true;
        }

        private void DrawStageStateSection()
        {
            if (_stageStateQuery.IsEmptyIgnoreFilter)
                return;

            GUILayout.Label("[Run Director / Stage State]");
            var stage = _em.GetComponentData<RunDirectorStageStateComponent>(_stageStateQuery.GetSingletonEntity());
            GUILayout.Label(
                $"stage:{ToStageStateLabel(stage.State)} elapsed:{stage.StateElapsedSec:0.00}s reason:{ToTransitionReasonLabel(stage.LastTransitionReason)}");

            if (!_stageGateQuery.IsEmptyIgnoreFilter)
            {
                var gate = _em.GetComponentData<RunDirectorStageGateComponent>(_stageGateQuery.GetSingletonEntity());
                GUILayout.Label(
                    $"gate intro:{gate.IntroPresentationDone} clear:{gate.ClearPresentationDone} " +
                    $"minIdle:{gate.MinIdleDurationElapsed} auto:{gate.AutoAdvanceTimeoutElapsed}");
            }

            if (!_stageRequestQuery.IsEmptyIgnoreFilter)
            {
                var request = _em.GetComponentData<RunDirectorStageRequestComponent>(_stageRequestQuery.GetSingletonEntity());
                GUILayout.Label($"request start:{request.StageStartRequested} confirm:{request.ConfirmPressed}");
            }

            if (!_stageSignalQuery.IsEmptyIgnoreFilter)
            {
                var signal = _em.GetComponentData<RunDirectorStageSignalComponent>(_stageSignalQuery.GetSingletonEntity());
                GUILayout.Label($"signal completed:{signal.StageRunCompleted}");
            }
        }

        private void DrawCombatEventSection()
        {
            if (_combatMetricsQuery.IsEmptyIgnoreFilter)
                return;

            var metrics = _em.GetComponentData<CombatEventMetricsComponent>(_combatMetricsQuery.GetSingletonEntity());
            GUILayout.Label("[Combat Event Channel]");
            GUILayout.Label(
                $"last hit/collect/cleanup: {metrics.LastFrameHitCount}/{metrics.LastFrameCollectCount}/{metrics.LastFrameCleanupCount}");
            GUILayout.Label(
                $"last value hit/collect/cleanup: {metrics.LastFrameHitValue}/{metrics.LastFrameCollectValue}/{metrics.LastFrameCleanupValue}");
            GUILayout.Label(
                $"total hit/collect/cleanup: {metrics.TotalHitCount}/{metrics.TotalCollectCount}/{metrics.TotalCleanupCount}");
            GUILayout.Label(
                $"total value hit/collect/cleanup: {metrics.TotalHitValue}/{metrics.TotalCollectValue}/{metrics.TotalCleanupValue}");
        }

        private void RefreshPressureWeightSnapshot()
        {
            _weightSnapshot = new PressureWeightSnapshot
            {
                Occupancy = 1.0f,
                HoldSec = 1.0f,
            };

            if (_directorWeightQuery.IsEmptyIgnoreFilter)
                return;

            using var entities = _directorWeightQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length <= 0)
                return;

            var weightBuffer = _em.GetBuffer<RunDirectorPressureWeightBuffer>(entities[0]);
            for (int i = 0; i < weightBuffer.Length; i++)
            {
                var item = weightBuffer[i];
                switch (item.Slot)
                {
                    case RunDirectorPressureInputSlotId.InfluenceOccupancy:
                        _weightSnapshot.Occupancy = item.Weight;
                        break;
                    case RunDirectorPressureInputSlotId.InfluenceHoldSec:
                        _weightSnapshot.HoldSec = item.Weight;
                        break;
                }
            }
        }

        private void RefreshSourceDirectorSnapshot()
        {
            _sourceRows.Clear();
            if (!ShouldDrawSourceDirectorState() || _sourceDirectorQuery.IsEmptyIgnoreFilter)
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
                float occupancyInput = 0f;
                float holdInput = 0f;
                float pressureScore = 0f;
                if (_em.HasBuffer<SourceDirectorPressureInputBuffer>(e))
                {
                    var inputs = _em.GetBuffer<SourceDirectorPressureInputBuffer>(e);
                    for (int j = 0; j < inputs.Length; j++)
                    {
                        var input = inputs[j];
                        float weight = ResolvePressureWeight(input.Slot);
                        pressureScore += input.Value * weight;

                        if (input.Slot == RunDirectorPressureInputSlotId.InfluenceOccupancy)
                            occupancyInput = input.Value;
                        else if (input.Slot == RunDirectorPressureInputSlotId.InfluenceHoldSec)
                            holdInput = input.Value;
                    }
                }

                _sourceRows.Add(new SourceDirectorHudRow
                {
                    StableId = stable.Value,
                    EntityIndex = e.Index,
                    DirectorState = director.State,
                    ClipState = director.SelectedClipState,
                    SourceState = source.State,
                    DensityScale = director.DensityScale,
                    PressureScore = pressureScore,
                    OccupancyInput = occupancyInput,
                    HoldInputSec = holdInput,
                });
            }
        }

        private void DrawSourceDirectorStatesSection()
        {
            if (!ShouldDrawSourceDirectorState())
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
                    $"clip:{ToSourceStateLabel(row.ClipState)}  src:{ToSourceStateLabel(row.SourceState)}  " +
                    $"x{row.DensityScale:0.00}  score:{row.PressureScore:0.00}  occ:{row.OccupancyInput:0.00}  hold:{row.HoldInputSec:0.00}");
            }
            int remaining = _sourceDirectorQuery.CalculateEntityCount() - _sourceRows.Count;
            if (remaining > 0)
                GUILayout.Label($"+{remaining} more...");
            GUILayout.EndScrollView();
        }

        private void DrawCategoryToggleSection()
        {
            GUILayout.Space(4f);
            GUILayout.Label("[Category Toggle]");
            GUILayout.BeginHorizontal();
            _showCoreMetrics = GUILayout.Toggle(_showCoreMetrics, "Core");
            _showStageState = GUILayout.Toggle(_showStageState, "Stage");
            _showCombatEvent = GUILayout.Toggle(_showCombatEvent, "Combat");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _showSourceDirectorState = GUILayout.Toggle(_showSourceDirectorState, "Source");
            _showStressControl = GUILayout.Toggle(_showStressControl, "Stress");
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private bool ShouldDrawSourceDirectorState()
        {
            return ShowSourceDirectorStates && _showSourceDirectorState;
        }

        private float ResolvePressureWeight(RunDirectorPressureInputSlotId slot)
        {
            return slot switch
            {
                RunDirectorPressureInputSlotId.InfluenceOccupancy => _weightSnapshot.Occupancy,
                RunDirectorPressureInputSlotId.InfluenceHoldSec => _weightSnapshot.HoldSec,
                _ => 0f,
            };
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

        private static string ToTransitionReasonLabel(RunDirectorStageTransitionReasonId reason)
        {
            return reason switch
            {
                RunDirectorStageTransitionReasonId.None => "None",
                RunDirectorStageTransitionReasonId.StartRequested => "StartRequested",
                RunDirectorStageTransitionReasonId.AllSourcesDepleted => "AllSourcesDepleted",
                RunDirectorStageTransitionReasonId.ConfirmPressed => "ConfirmPressed",
                RunDirectorStageTransitionReasonId.AutoAdvanceTimeout => "AutoAdvanceTimeout",
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
