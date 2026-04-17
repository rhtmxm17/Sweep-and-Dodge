using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(SourceRuntimeTemplateAuthoringBase), true)]
    internal sealed class SourceRuntimeTemplateAuthoringEditor : UnityEditor.Editor
    {
        private SerializedProperty _stableIdOverrideProperty;
        private SerializedProperty _shapeProperty;
        private SerializedProperty _radiusProperty;
        private SerializedProperty _sizeProperty;
        private SerializedProperty _sustainClipSlotsProperty;
        private SerializedProperty _eventClipSlotsProperty;
        private SerializedProperty _thresholdWeakenedProperty;
        private SerializedProperty _thresholdDepletedProperty;
        private SerializedProperty _initialCollectedCountProperty;
        private SerializedProperty _initialStateProperty;
        private SerializedProperty _pollutionCellSizeProperty;
        private SerializedProperty _pollutionMinProperty;
        private SerializedProperty _pollutionMaxProperty;
        private SerializedProperty _pollutionRegenPerSecProperty;
        private SerializedProperty _pollutionDropPerCollectProperty;
        private SerializedProperty _pollutionTopKSampleCountProperty;
        private SerializedProperty _pollutionActiveRatioThresholdProperty;
        private SerializedProperty _pollutionRecoveryCooldownFramesProperty;
        private SerializedProperty _pollutionRecoveryWaveSeedCountProperty;
        private SerializedProperty _pollutionRecoveryWaveClusterSizeProperty;
        private SerializedProperty _pollutionRecoveryRestoreValueProperty;
        private SerializedProperty _pollutionRecoveryRecentCleanBiasFramesProperty;
        private SerializedProperty _drawGizmoProperty;
        private SerializedProperty _drawGizmoWhenNotSelectedProperty;

        private void OnEnable()
        {
            _stableIdOverrideProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.StableIdOverride));
            _shapeProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.Shape));
            _radiusProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.Radius));
            _sizeProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.Size));
            _sustainClipSlotsProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.SustainClipSlots));
            _eventClipSlotsProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.EventClipSlots));
            _thresholdWeakenedProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.ThresholdWeakened));
            _thresholdDepletedProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.ThresholdDepleted));
            _initialCollectedCountProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.InitialCollectedCount));
            _initialStateProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.InitialState));
            _pollutionCellSizeProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionCellSize));
            _pollutionMinProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionMin));
            _pollutionMaxProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionMax));
            _pollutionRegenPerSecProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionRegenPerSec));
            _pollutionDropPerCollectProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionDropPerCollect));
            _pollutionTopKSampleCountProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionTopKSampleCount));
            _pollutionActiveRatioThresholdProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionActiveRatioThreshold));
            _pollutionRecoveryCooldownFramesProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionRecoveryCooldownFrames));
            _pollutionRecoveryWaveSeedCountProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionRecoveryWaveSeedCount));
            _pollutionRecoveryWaveClusterSizeProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionRecoveryWaveClusterSize));
            _pollutionRecoveryRestoreValueProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionRecoveryRestoreValue));
            _pollutionRecoveryRecentCleanBiasFramesProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.PollutionRecoveryRecentCleanBiasFrames));
            _drawGizmoProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.DrawGizmo));
            _drawGizmoWhenNotSelectedProperty = serializedObject.FindProperty(nameof(SourceRuntimeTemplateAuthoringBase.DrawGizmoWhenNotSelected));
        }

        public override void OnInspectorGUI()
        {
            if (targets.Length != 1)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            DrawIdentitySection();
            DrawSourceFieldSection();
            DrawSourceDefinitionSeedSection();
            DrawCleaningTrailSection();
            DrawDebugSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_stableIdOverrideProperty);
            EditorGUILayout.Space(4f);
        }

        private void DrawSourceFieldSection()
        {
            EditorGUILayout.LabelField("Source Field", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_shapeProperty);
            EditorGUILayout.PropertyField(_radiusProperty);
            EditorGUILayout.PropertyField(_sizeProperty);
            EditorGUILayout.Space(4f);
        }

        private void DrawSourceDefinitionSeedSection()
        {
            EditorGUILayout.LabelField("Source Definition Seed (Deprecated)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sustainClipSlotsProperty, includeChildren: true);
            EditorGUILayout.PropertyField(_eventClipSlotsProperty, includeChildren: true);
            EditorGUILayout.PropertyField(_thresholdWeakenedProperty);
            EditorGUILayout.PropertyField(_thresholdDepletedProperty);
            EditorGUILayout.PropertyField(_initialCollectedCountProperty);
            EditorGUILayout.PropertyField(_initialStateProperty);

            serializedObject.ApplyModifiedProperties();
            DrawSustainStateSummary((SourceRuntimeTemplateAuthoringBase)target);
            serializedObject.Update();

            EditorGUILayout.Space(4f);
        }

        private static void DrawSustainStateSummary(SourceRuntimeTemplateAuthoringBase authoring)
        {
            if (authoring == null)
                return;

            var rows = SourceRuntimeTemplateAuthoringEditorSummaryUtility.BuildRows(
                authoring.InitialState,
                authoring.ThresholdWeakened,
                authoring.ThresholdDepleted,
                authoring.SustainClipSlots);

            EditorGUILayout.LabelField("Sustain State Summary", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string stateLabel = row.IsInitialState
                            ? $"{row.State} [Initial]"
                            : row.State.ToString();
                        EditorGUILayout.LabelField(stateLabel, GUILayout.Width(130f));
                        EditorGUILayout.LabelField(row.RangeLabel, GUILayout.Width(96f));
                        EditorGUILayout.LabelField(row.SlotSummary, EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
        }

        private void DrawCleaningTrailSection()
        {
            EditorGUILayout.LabelField("Cleaning Trail (Pollution Grid)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_pollutionCellSizeProperty);
            EditorGUILayout.PropertyField(_pollutionMinProperty);
            EditorGUILayout.PropertyField(_pollutionMaxProperty);
            EditorGUILayout.PropertyField(_pollutionRegenPerSecProperty);
            EditorGUILayout.PropertyField(_pollutionDropPerCollectProperty);
            EditorGUILayout.PropertyField(_pollutionTopKSampleCountProperty);
            EditorGUILayout.PropertyField(_pollutionActiveRatioThresholdProperty);
            EditorGUILayout.PropertyField(_pollutionRecoveryCooldownFramesProperty);
            EditorGUILayout.PropertyField(_pollutionRecoveryWaveSeedCountProperty);
            EditorGUILayout.PropertyField(_pollutionRecoveryWaveClusterSizeProperty);
            EditorGUILayout.PropertyField(_pollutionRecoveryRestoreValueProperty);
            EditorGUILayout.PropertyField(_pollutionRecoveryRecentCleanBiasFramesProperty);
            EditorGUILayout.Space(4f);
        }

        private void DrawDebugSection()
        {
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_drawGizmoProperty);
            EditorGUILayout.PropertyField(_drawGizmoWhenNotSelectedProperty);
        }
    }

    public static class SourceRuntimeTemplateAuthoringEditorSummaryUtility
    {
        public readonly struct SustainStateSummaryRow
        {
            public SustainStateSummaryRow(
                SourceStateId state,
                int minInclusive,
                int? maxExclusive,
                bool isInitialState,
                string slotSummary)
            {
                State = state;
                MinInclusive = minInclusive;
                MaxExclusive = maxExclusive;
                IsInitialState = isInitialState;
                SlotSummary = slotSummary ?? "none";
            }

            public SourceStateId State { get; }
            public int MinInclusive { get; }
            public int? MaxExclusive { get; }
            public bool IsInitialState { get; }
            public string SlotSummary { get; }

            public string RangeLabel => MaxExclusive.HasValue
                ? $"[{MinInclusive}, {MaxExclusive.Value})"
                : $"[{MinInclusive}, +inf)";
        }

        public static SustainStateSummaryRow[] BuildRows(
            SourceStateId initialState,
            int thresholdWeakened,
            int thresholdDepleted,
            SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring[] sustainSlots)
        {
            int effectiveWeakened = Mathf.Max(0, thresholdWeakened);
            int effectiveDepleted = Mathf.Max(effectiveWeakened, thresholdDepleted);

            return new[]
            {
                BuildRow(SourceStateId.Normal, 0, effectiveWeakened, initialState, sustainSlots),
                BuildRow(SourceStateId.Weakened, effectiveWeakened, effectiveDepleted, initialState, sustainSlots),
                BuildRow(SourceStateId.Depleted, effectiveDepleted, null, initialState, sustainSlots),
            };
        }

        private static SustainStateSummaryRow BuildRow(
            SourceStateId state,
            int minInclusive,
            int? maxExclusive,
            SourceStateId initialState,
            SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring[] sustainSlots)
        {
            string slotSummary = BuildSlotSummary(state, sustainSlots);
            return new SustainStateSummaryRow(
                state,
                minInclusive,
                maxExclusive,
                state == initialState,
                slotSummary);
        }

        private static string BuildSlotSummary(
            SourceStateId state,
            SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring[] sustainSlots)
        {
            if (sustainSlots == null || sustainSlots.Length == 0)
                return "none";

            var slotIndicesByLane = new Dictionary<SourceSpawnLaneId, List<int>>();
            var laneOrder = new List<SourceSpawnLaneId>();
            for (int i = 0; i < sustainSlots.Length; i++)
            {
                if (sustainSlots[i].State != state)
                    continue;

                SourceSpawnLaneId lane = sustainSlots[i].Lane;
                if (!slotIndicesByLane.TryGetValue(lane, out var slotIndices))
                {
                    slotIndices = new List<int>();
                    slotIndicesByLane.Add(lane, slotIndices);
                    laneOrder.Add(lane);
                }

                slotIndices.Add(i);
            }

            if (laneOrder.Count == 0)
                return "none";

            var builder = new StringBuilder(64);
            for (int i = 0; i < laneOrder.Count; i++)
            {
                if (i > 0)
                    builder.Append(" | ");

                SourceSpawnLaneId lane = laneOrder[i];
                builder.Append(lane);
                builder.Append(": ");

                var slotIndices = slotIndicesByLane[lane];
                for (int j = 0; j < slotIndices.Count; j++)
                {
                    if (j > 0)
                        builder.Append(", ");

                    builder.Append("slot");
                    builder.Append(slotIndices[j]);
                }
            }

            return builder.ToString();
        }
    }
}
