using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(WaveClipSO))]
    internal sealed class WaveClipSOEditor : UnityEditor.Editor
    {
        private const string DenseModeSessionKey = "WaveClipSOEditor.DenseMode";
        private const string SegmentFoldoutKeyPrefix = "WaveClipSOEditor.SegmentFoldout.";
        private const string DirectiveFoldoutKeyPrefix = "WaveClipSOEditor.DirectiveFoldout.";
        private const string DenseSectionFoldoutKeyPrefix = "WaveClipSOEditor.DenseSectionFoldout.";

        private SerializedProperty _clipIdProperty;
        private SerializedProperty _phaseProperty;
        private SerializedProperty _laneProperty;
        private SerializedProperty _durationProperty;
        private SerializedProperty _segmentsProperty;

        private Vector2 _validationScroll;
        private List<ContentValidationIssue> _validatedIssues = new List<ContentValidationIssue>();

        private int ClipInstanceId => target != null ? target.GetInstanceID() : 0;

        private bool DenseMode
        {
            get => SessionState.GetBool(DenseModeSessionKey, false);
            set => SessionState.SetBool(DenseModeSessionKey, value);
        }

        private void OnEnable()
        {
            _clipIdProperty = serializedObject.FindProperty(nameof(WaveClipSO.ClipId));
            _phaseProperty = serializedObject.FindProperty(nameof(WaveClipSO.Phase));
            _laneProperty = serializedObject.FindProperty(nameof(WaveClipSO.Lane));
            _durationProperty = serializedObject.FindProperty(nameof(WaveClipSO.DurationSec));
            _segmentsProperty = serializedObject.FindProperty(nameof(WaveClipSO.Segments));
        }

        public override void OnInspectorGUI()
        {
            if (targets.Length != 1)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            var clip = (WaveClipSO)target;
            DrawToolbar(clip);
            DrawMetadataSection();
            DrawSharedReferenceStatus(clip);
            DrawValidationResults(clip);
            DrawSegmentsSection(clip);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawToolbar(WaveClipSO clip)
        {
            EditorGUILayout.LabelField("Clip Metadata", EditorStyles.boldLabel);
            bool denseMode = EditorGUILayout.ToggleLeft("Dense Mode", DenseMode);
            if (denseMode != DenseMode)
                DenseMode = denseMode;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Current Clip"))
                {
                    _validatedIssues = WaveClipEditorPresentationUtility.ValidateCurrentClip(clip);
                    if (_validatedIssues.Count == 0)
                        EditorUtility.DisplayDialog("Validate Current Clip", "No validation issues detected.", "OK");
                }

                if (GUILayout.Button("Clear Validation Results"))
                    _validatedIssues.Clear();
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawMetadataSection()
        {
            EditorGUILayout.PropertyField(_clipIdProperty);
            EditorGUILayout.PropertyField(_phaseProperty);
            EditorGUILayout.PropertyField(_laneProperty);
            EditorGUILayout.PropertyField(_durationProperty);
            EditorGUILayout.Space(4f);
        }

        private void DrawSharedReferenceStatus(WaveClipSO clip)
        {
            var issues = WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip);
            if (issues.Count == 0)
                return;

            EditorGUILayout.LabelField("Shared SerializeReference Status", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                $"Shared SerializeReference graph detected in {issues.Count} slot(s). Run 'Repair Shared References' before editing duplicated segments or directives.",
                MessageType.Error);

            int maxRows = Mathf.Min(issues.Count, DenseMode ? 3 : 5);
            for (int i = 0; i < maxRows; i++)
                DrawSharedReferenceIssueRow(issues[i]);

            if (issues.Count > maxRows)
            {
                EditorGUILayout.LabelField($"... and {issues.Count - maxRows} more shared collisions.", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Shared References"))
                {
                    string message = issues.Count > 0
                        ? BuildSharedReferenceSummary(issues)
                        : "No shared SerializeReference graph detected.";
                    EditorUtility.DisplayDialog("WaveClip Shared Reference Validation", message, "OK");
                }

                if (GUILayout.Button("Repair Shared References"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(clip, "Repair WaveClip Shared References");
                    bool changed = WaveClipManagedReferenceGraphUtility.RepairSharedManagedReferences(clip);
                    if (changed)
                    {
                        EditorUtility.SetDirty(clip);
                        serializedObject.Update();
                    }

                    EditorUtility.DisplayDialog(
                        "WaveClip Shared Reference Repair",
                        changed ? "Shared managed references were repaired." : "No shared managed references were detected.",
                        "OK");
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawSharedReferenceIssueRow(in WaveClipSharedManagedReferenceIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{issue.SlotName}: {issue.FirstLocation} <-> {issue.DuplicateLocation}", EditorStyles.miniLabel);
                if (GUILayout.Button("Jump", GUILayout.Width(50f)))
                    JumpToLocation(issue.DuplicateLocation);
            }
        }

        private void DrawValidationResults(WaveClipSO clip)
        {
            if (_validatedIssues == null || _validatedIssues.Count == 0)
                return;

            int errorCount = 0;
            for (int i = 0; i < _validatedIssues.Count; i++)
            {
                if (_validatedIssues[i].Severity == ContentValidationSeverity.Error)
                    errorCount++;
            }

            EditorGUILayout.LabelField("Current Clip Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Validation issues: {_validatedIssues.Count} (Errors={errorCount}, Warnings={_validatedIssues.Count - errorCount})",
                errorCount > 0 ? MessageType.Warning : MessageType.Info);

            float maxHeight = DenseMode ? 120f : 180f;
            _validationScroll = EditorGUILayout.BeginScrollView(_validationScroll, GUILayout.MaxHeight(maxHeight));
            for (int i = 0; i < _validatedIssues.Count; i++)
                DrawValidationIssueRow(clip, _validatedIssues[i]);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(6f);
        }

        private void DrawValidationIssueRow(WaveClipSO clip, in ContentValidationIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string severityLabel = issue.Severity == ContentValidationSeverity.Error ? "Error" : "Warn";
                EditorGUILayout.LabelField($"{severityLabel} {issue.Code}", GUILayout.Width(90f));
                EditorGUILayout.LabelField(WaveClipEditorPresentationUtility.FormatIssueLabel(issue), EditorStyles.wordWrappedMiniLabel);

                if (WaveClipEditorPresentationUtility.TryParseJumpTarget(issue.Location, out _, out _)
                    && GUILayout.Button("Jump", GUILayout.Width(50f)))
                {
                    JumpToLocation(issue.Location);
                    EditorGUIUtility.PingObject(clip);
                }
            }
        }

        private void DrawSegmentsSection(WaveClipSO clip)
        {
            EditorGUILayout.LabelField("Local Segments (Overlap Allowed)", EditorStyles.boldLabel);

            int segmentCount = _segmentsProperty?.arraySize ?? 0;
            for (int s = 0; s < segmentCount; s++)
            {
                SerializedProperty segmentProperty = _segmentsProperty.GetArrayElementAtIndex(s);
                bool expanded = GetSegmentFoldout(s);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSegmentHeader(clip, s, ref expanded);
                    SetSegmentFoldout(s, expanded);

                    string summary = s < clip.Segments.Length
                        ? WaveClipEditorPresentationUtility.BuildSegmentSummary(clip.Segments[s])
                        : "Segment";
                    EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                    if (!expanded)
                        continue;

                    var descriptionProperty = segmentProperty.FindPropertyRelative("editorOnlyDescription");
                    if (descriptionProperty != null && (!DenseMode || !string.IsNullOrWhiteSpace(descriptionProperty.stringValue)))
                        EditorGUILayout.PropertyField(descriptionProperty);

                    EditorGUILayout.PropertyField(segmentProperty.FindPropertyRelative(nameof(WaveClipSO.ClipSegment.StartSec)));
                    EditorGUILayout.PropertyField(segmentProperty.FindPropertyRelative(nameof(WaveClipSO.ClipSegment.DurationSec)));

                    SerializedProperty directivesProperty = segmentProperty.FindPropertyRelative(nameof(WaveClipSO.ClipSegment.Directives));
                    DrawDirectivesSection(clip, s, directivesProperty);
                }
            }

            if (GUILayout.Button("Add Segment"))
            {
                serializedObject.ApplyModifiedProperties();
                AppendSegment(clip);
                GUIUtility.ExitGUI();
            }
        }

        private void DrawSegmentHeader(WaveClipSO clip, int segmentIndex, ref bool expanded)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, $"Segment {segmentIndex}", true);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(segmentIndex <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(40f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        MoveSegment(clip, segmentIndex, segmentIndex - 1);
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(segmentIndex >= (_segmentsProperty?.arraySize ?? 0) - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(50f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        MoveSegment(clip, segmentIndex, segmentIndex + 1);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Duplicate", GUILayout.Width(80f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    DuplicateSegment(clip, segmentIndex);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    RemoveSegment(clip, segmentIndex);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawDirectivesSection(WaveClipSO clip, int segmentIndex, SerializedProperty directivesProperty)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Directives", EditorStyles.boldLabel);

            int directiveCount = directivesProperty?.arraySize ?? 0;
            for (int d = 0; d < directiveCount; d++)
            {
                SerializedProperty directiveProperty = directivesProperty.GetArrayElementAtIndex(d);
                bool expanded = GetDirectiveFoldout(segmentIndex, d);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawDirectiveHeader(clip, segmentIndex, d, ref expanded);
                    SetDirectiveFoldout(segmentIndex, d, expanded);

                    var directive = clip.Segments != null
                        && segmentIndex >= 0
                        && segmentIndex < clip.Segments.Length
                        && clip.Segments[segmentIndex].Directives != null
                        && d >= 0
                        && d < clip.Segments[segmentIndex].Directives.Length
                        ? clip.Segments[segmentIndex].Directives[d]
                        : null;

                    EditorGUILayout.LabelField(WaveClipEditorPresentationUtility.BuildDirectiveSummary(directive), EditorStyles.miniLabel);

                    var warnings = WaveClipEditorPresentationUtility.CollectInlineWarnings(directive);
                    if (warnings.Count > 0)
                        EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);

                    if (!expanded)
                        continue;

                    if (DenseMode)
                    {
                        DrawDenseDirectiveBody(directiveProperty, segmentIndex, d);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(directiveProperty, true);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Default Directive"))
                {
                    serializedObject.ApplyModifiedProperties();
                    AppendDirective(clip, segmentIndex);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Add From Preset"))
                {
                    ShowDirectivePresetMenu(clip, segmentIndex);
                }
            }
        }

        private void DrawDirectiveHeader(WaveClipSO clip, int segmentIndex, int directiveIndex, ref bool expanded)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, $"Directive {directiveIndex}", true);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(directiveIndex <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(40f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        MoveDirective(clip, segmentIndex, directiveIndex, directiveIndex - 1);
                        GUIUtility.ExitGUI();
                    }
                }

                int directiveCount = clip.Segments != null
                    && segmentIndex >= 0
                    && segmentIndex < clip.Segments.Length
                    && clip.Segments[segmentIndex].Directives != null
                    ? clip.Segments[segmentIndex].Directives.Length
                    : 0;
                using (new EditorGUI.DisabledScope(directiveIndex >= directiveCount - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(50f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        MoveDirective(clip, segmentIndex, directiveIndex, directiveIndex + 1);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Duplicate", GUILayout.Width(70f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    DuplicateDirective(clip, segmentIndex, directiveIndex);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    RemoveDirective(clip, segmentIndex, directiveIndex);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawDenseDirectiveBody(SerializedProperty directiveProperty, int segmentIndex, int directiveIndex)
        {
            var payloadProperty = directiveProperty.FindPropertyRelative(nameof(WaveSpawnEntryAuthoring.Payload));
            if (payloadProperty != null)
                EditorGUILayout.PropertyField(payloadProperty, true);

            DrawDenseDirectiveSection(
                segmentIndex,
                directiveIndex,
                nameof(WaveSpawnEntryAuthoring.Emission),
                directiveProperty.FindPropertyRelative(nameof(WaveSpawnEntryAuthoring.Emission)));
            DrawDenseDirectiveSection(
                segmentIndex,
                directiveIndex,
                nameof(WaveSpawnEntryAuthoring.Sampling),
                directiveProperty.FindPropertyRelative(nameof(WaveSpawnEntryAuthoring.Sampling)));
            DrawDenseDirectiveSection(
                segmentIndex,
                directiveIndex,
                nameof(WaveSpawnEntryAuthoring.PositionPattern),
                directiveProperty.FindPropertyRelative(nameof(WaveSpawnEntryAuthoring.PositionPattern)));
            DrawDenseDirectiveSection(
                segmentIndex,
                directiveIndex,
                nameof(WaveSpawnEntryAuthoring.Aim),
                directiveProperty.FindPropertyRelative(nameof(WaveSpawnEntryAuthoring.Aim)));
            DrawDenseDirectiveSection(
                segmentIndex,
                directiveIndex,
                nameof(WaveSpawnEntryAuthoring.ShotPattern),
                directiveProperty.FindPropertyRelative(nameof(WaveSpawnEntryAuthoring.ShotPattern)));
        }

        private void DrawDenseDirectiveSection(int segmentIndex, int directiveIndex, string sectionName, SerializedProperty property)
        {
            if (property == null)
                return;

            string key = $"{DenseSectionFoldoutKeyPrefix}{ClipInstanceId}.{segmentIndex}.{directiveIndex}.{sectionName}";
            bool expanded = SessionState.GetBool(key, false);
            expanded = EditorGUILayout.Foldout(expanded, ObjectNames.NicifyVariableName(sectionName), true);
            SessionState.SetBool(key, expanded);
            if (!expanded)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        private void ShowDirectivePresetMenu(WaveClipSO clip, int segmentIndex)
        {
            var menu = new GenericMenu();
            AddPresetMenuItem(menu, clip, segmentIndex, "Single Hazard", WaveClipDirectivePresetId.SingleHazard);
            AddPresetMenuItem(menu, clip, segmentIndex, "Fan Burst", WaveClipDirectivePresetId.FanBurst);
            AddPresetMenuItem(menu, clip, segmentIndex, "Radial Burst", WaveClipDirectivePresetId.RadialBurst);
            AddPresetMenuItem(menu, clip, segmentIndex, "Line Normal Fan", WaveClipDirectivePresetId.LineNormalFan);
            menu.ShowAsContext();
        }

        private void AddPresetMenuItem(GenericMenu menu, WaveClipSO clip, int segmentIndex, string label, WaveClipDirectivePresetId preset)
        {
            menu.AddItem(new GUIContent(label), false, () =>
            {
                serializedObject.ApplyModifiedProperties();
                AppendDirectiveFromPreset(clip, segmentIndex, preset);
            });
        }

        private void AppendSegment(WaveClipSO clip)
        {
            Undo.RecordObject(clip, "Add WaveClip Segment");
            var segments = new List<WaveClipSO.ClipSegment>(clip.Segments ?? System.Array.Empty<WaveClipSO.ClipSegment>())
            {
                WaveClipManagedReferenceGraphUtility.CreateDefaultSegment()
            };
            clip.Segments = segments.ToArray();
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
        }

        private void DuplicateSegment(WaveClipSO clip, int segmentIndex)
        {
            if (clip.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return;

            Undo.RecordObject(clip, "Duplicate WaveClip Segment");
            var segments = new List<WaveClipSO.ClipSegment>(clip.Segments);
            segments.Insert(segmentIndex + 1, WaveClipManagedReferenceGraphUtility.CloneSegment(clip.Segments[segmentIndex]));
            clip.Segments = segments.ToArray();
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
            SetSegmentFoldout(segmentIndex + 1, true);
        }

        private void MoveSegment(WaveClipSO clip, int fromIndex, int toIndex)
        {
            Undo.RecordObject(clip, "Move WaveClip Segment");
            if (!WaveClipManagedReferenceGraphUtility.MoveSegment(clip, fromIndex, toIndex))
                return;

            EditorUtility.SetDirty(clip);
            serializedObject.Update();
            SetSegmentFoldout(toIndex, true);
        }

        private void RemoveSegment(WaveClipSO clip, int segmentIndex)
        {
            if (clip.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return;

            Undo.RecordObject(clip, "Remove WaveClip Segment");
            var segments = new List<WaveClipSO.ClipSegment>(clip.Segments);
            segments.RemoveAt(segmentIndex);
            clip.Segments = segments.ToArray();
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
        }

        private void AppendDirective(WaveClipSO clip, int segmentIndex)
        {
            AppendDirectiveCore(clip, segmentIndex, WaveClipManagedReferenceGraphUtility.CreateDefaultDirective(), "Add WaveClip Directive");
        }

        private void AppendDirectiveFromPreset(WaveClipSO clip, int segmentIndex, WaveClipDirectivePresetId preset)
        {
            AppendDirectiveCore(clip, segmentIndex, WaveClipManagedReferenceGraphUtility.CreatePresetDirective(preset), $"Add WaveClip Directive ({preset})");
        }

        private void AppendDirectiveCore(WaveClipSO clip, int segmentIndex, WaveSpawnEntryAuthoring directive, string undoLabel)
        {
            if (clip.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return;

            Undo.RecordObject(clip, undoLabel);
            var segments = clip.Segments;
            var segment = segments[segmentIndex];
            var directives = new List<WaveSpawnEntryAuthoring>(segment.Directives ?? System.Array.Empty<WaveSpawnEntryAuthoring>())
            {
                directive
            };
            segment.Directives = directives.ToArray();
            segments[segmentIndex] = segment;
            clip.Segments = segments;
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
            SetDirectiveFoldout(segmentIndex, directives.Count - 1, true);
        }

        private void DuplicateDirective(WaveClipSO clip, int segmentIndex, int directiveIndex)
        {
            if (clip.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return;

            var segments = clip.Segments;
            var segment = segments[segmentIndex];
            if (segment.Directives == null || directiveIndex < 0 || directiveIndex >= segment.Directives.Length)
                return;

            Undo.RecordObject(clip, "Duplicate WaveClip Directive");
            var directives = new List<WaveSpawnEntryAuthoring>(segment.Directives);
            directives.Insert(directiveIndex + 1, WaveClipManagedReferenceGraphUtility.CloneDirective(segment.Directives[directiveIndex]));
            segment.Directives = directives.ToArray();
            segments[segmentIndex] = segment;
            clip.Segments = segments;
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
            SetDirectiveFoldout(segmentIndex, directiveIndex + 1, true);
        }

        private void MoveDirective(WaveClipSO clip, int segmentIndex, int fromIndex, int toIndex)
        {
            Undo.RecordObject(clip, "Move WaveClip Directive");
            if (!WaveClipManagedReferenceGraphUtility.MoveDirective(clip, segmentIndex, fromIndex, toIndex))
                return;

            EditorUtility.SetDirty(clip);
            serializedObject.Update();
            SetDirectiveFoldout(segmentIndex, toIndex, true);
        }

        private void RemoveDirective(WaveClipSO clip, int segmentIndex, int directiveIndex)
        {
            if (clip.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return;

            var segments = clip.Segments;
            var segment = segments[segmentIndex];
            if (segment.Directives == null || directiveIndex < 0 || directiveIndex >= segment.Directives.Length)
                return;

            Undo.RecordObject(clip, "Remove WaveClip Directive");
            var directives = new List<WaveSpawnEntryAuthoring>(segment.Directives);
            directives.RemoveAt(directiveIndex);
            segment.Directives = directives.ToArray();
            segments[segmentIndex] = segment;
            clip.Segments = segments;
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
        }

        private void JumpToLocation(string location)
        {
            if (!WaveClipEditorPresentationUtility.TryParseJumpTarget(location, out int segmentIndex, out int directiveIndex))
                return;

            SetSegmentFoldout(segmentIndex, true);
            if (directiveIndex >= 0)
                SetDirectiveFoldout(segmentIndex, directiveIndex, true);

            Repaint();
        }

        private bool GetSegmentFoldout(int segmentIndex)
        {
            return SessionState.GetBool($"{SegmentFoldoutKeyPrefix}{ClipInstanceId}.{segmentIndex}", true);
        }

        private void SetSegmentFoldout(int segmentIndex, bool value)
        {
            SessionState.SetBool($"{SegmentFoldoutKeyPrefix}{ClipInstanceId}.{segmentIndex}", value);
        }

        private bool GetDirectiveFoldout(int segmentIndex, int directiveIndex)
        {
            return SessionState.GetBool($"{DirectiveFoldoutKeyPrefix}{ClipInstanceId}.{segmentIndex}.{directiveIndex}", true);
        }

        private void SetDirectiveFoldout(int segmentIndex, int directiveIndex, bool value)
        {
            SessionState.SetBool($"{DirectiveFoldoutKeyPrefix}{ClipInstanceId}.{segmentIndex}.{directiveIndex}", value);
        }

        private static string BuildSharedReferenceSummary(List<WaveClipSharedManagedReferenceIssue> issues)
        {
            if (issues.Count == 0)
                return "No shared SerializeReference graph detected.";

            int maxLines = Mathf.Min(issues.Count, 8);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Shared managed references detected: {issues.Count}");
            for (int i = 0; i < maxLines; i++)
            {
                var issue = issues[i];
                lines.AppendLine($"- {issue.SlotName}: {issue.FirstLocation} <-> {issue.DuplicateLocation}");
            }

            if (issues.Count > maxLines)
                lines.AppendLine($"... and {issues.Count - maxLines} more.");

            return lines.ToString();
        }
    }

    public static class WaveClipEditorPresentationUtility
    {
        public static string BuildSegmentSummary(in WaveClipSO.ClipSegment segment)
        {
            float duration = Mathf.Max(0f, segment.DurationSec);
            int directiveCount = segment.Directives?.Length ?? 0;
            float endSec = segment.StartSec + duration;
            string summary = $"{segment.StartSec:0.###}s ~ {endSec:0.###}s (dur {duration:0.###}s) | Directives={directiveCount}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrWhiteSpace(segment.EditorOnlyDescription))
                summary += $" | {Truncate(segment.EditorOnlyDescription.Trim(), 48)}";
#endif
            return summary;
        }

        public static string BuildDirectiveSummary(WaveSpawnEntryAuthoring entry)
        {
            if (entry == null)
                return "Null Directive";

            return $"Bullet={FormatBullet(entry.Payload.Bullet)}"
                + $" | {FormatEmission(entry.Emission)}"
                + $" | {FormatSampling(entry.Sampling)}"
                + $" | {FormatPositionPattern(entry.PositionPattern)}"
                + $" | {FormatAim(entry.Aim)}"
                + $" | {FormatShotPattern(entry.ShotPattern)}";
        }

        public static List<string> CollectInlineWarnings(WaveSpawnEntryAuthoring entry)
        {
            var warnings = new List<string>();
            if (entry == null)
                return warnings;

            if (entry.Aim is LineNormalAimAuthoring && entry.PositionPattern is not LineEvenPositionPatternAuthoring)
                warnings.Add("CV042: LineNormalAim requires LineEven PositionPattern.");

            if (entry.ShotPattern is NWayShotPatternAuthoring nWay && (nWay.ShotCount < 2 || nWay.AngleSpacingDeg <= 0f))
                warnings.Add("CV023: NWay ShotPattern requires ShotCount >= 2 and AngleSpacingDeg > 0.");

            if (entry.ShotPattern is RadialShotPatternAuthoring radial && radial.ShotCount < 2)
                warnings.Add("CV024: Radial ShotPattern requires ShotCount >= 2.");

            if (entry.Emission is PoissonEmissionAuthoring poisson
                && poisson.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                && poisson.EventShotIntervalSec <= 0f)
            {
                warnings.Add("CV025: Timed EventShotSchedule requires EventShotIntervalSec > 0.");
            }

            if (entry.Emission is EventBurstEmissionAuthoring eventBurst
                && eventBurst.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                && eventBurst.EventShotIntervalSec <= 0f)
            {
                warnings.Add("CV025: Timed EventShotSchedule requires EventShotIntervalSec > 0.");
            }

            if (entry.PositionPattern is PointSetPositionPatternAuthoring pointSet
                && (pointSet.Points == null || pointSet.Points.Length <= 0))
            {
                warnings.Add("CV028: PointSet PositionPattern requires at least one point.");
            }

            return warnings;
        }

        public static List<ContentValidationIssue> ValidateCurrentClip(WaveClipSO clip)
        {
            var definitions = new List<ContentValidationRecord<BulletDefinitionSO>>();
            var seenDefinitionIds = new HashSet<int>();

            if (clip?.Segments != null)
            {
                for (int s = 0; s < clip.Segments.Length; s++)
                {
                    var directives = clip.Segments[s].Directives;
                    if (directives == null)
                        continue;

                    for (int d = 0; d < directives.Length; d++)
                    {
                        var bullet = directives[d]?.Payload.Bullet;
                        if (bullet == null)
                            continue;

                        int instanceId = bullet.GetInstanceID();
                        if (!seenDefinitionIds.Add(instanceId))
                            continue;

                        definitions.Add(new ContentValidationRecord<BulletDefinitionSO>(bullet, $"clip/Definitions[{definitions.Count}]"));
                    }
                }
            }

            var input = new ContentValidationInput(
                definitions,
                new[] { new ContentValidationRecord<WaveClipSO>(clip, "clip") },
                null,
                null,
                null);

            return ContentValidationRules.Validate(input);
        }

        public static bool TryParseJumpTarget(string location, out int segmentIndex, out int directiveIndex)
        {
            segmentIndex = -1;
            directiveIndex = -1;

            if (string.IsNullOrEmpty(location))
                return false;

            segmentIndex = TryExtractBracketedIndex(location, "Segments[");
            directiveIndex = TryExtractBracketedIndex(location, "Directives[");
            return segmentIndex >= 0;
        }

        public static string FormatIssueLabel(in ContentValidationIssue issue)
        {
            return $"{issue.Code} | {issue.Location} | {issue.Message}";
        }

        private static string FormatBullet(BulletDefinitionSO bullet)
        {
            if (bullet == null)
                return "None";

            if (!string.IsNullOrWhiteSpace(bullet.name))
                return bullet.name;

            return bullet.DefinitionId > 0 ? $"Def#{bullet.DefinitionId}" : "Bullet";
        }

        private static string FormatEmission(WaveEmissionAuthoringBase emission)
        {
            return emission switch
            {
                RateFieldEmissionAuthoring => "RateField",
                PoissonEmissionAuthoring poisson => $"Poisson x{poisson.EventRepeatCount} {poisson.EventShotSchedule}",
                EventBurstEmissionAuthoring burst => $"EventBurst x{burst.EventRepeatCount} {burst.EventShotSchedule}",
                null => "Emission=None",
                _ => emission.GetType().Name,
            };
        }

        private static string FormatSampling(WaveSamplingAuthoring sampling)
        {
            if (sampling == null)
                return "Sampling=None";

            return $"{FormatSamplingAnchor(sampling.Anchor)}+{FormatAreaSampler(sampling.AreaSampler)}";
        }

        private static string FormatSamplingAnchor(WaveSamplingAnchorAuthoringBase anchor)
        {
            return anchor switch
            {
                SourceCenterSamplingAnchorAuthoring => "SourceCenter",
                FixedPointSamplingAnchorAuthoring => "FixedPoint",
                PlayerRelativeSamplingAnchorAuthoring => "PlayerRelative",
                null => "NoAnchor",
                _ => anchor.GetType().Name,
            };
        }

        private static string FormatAreaSampler(WaveAreaSamplerAuthoringBase areaSampler)
        {
            return areaSampler switch
            {
                CenterPointAreaSamplerAuthoring => "CenterPoint",
                UniformFieldAreaSamplerAuthoring => "UniformField",
                PollutionTopKAreaSamplerAuthoring => "PollutionTopK",
                null => "NoAreaSampler",
                _ => areaSampler.GetType().Name,
            };
        }

        private static string FormatPositionPattern(WavePositionPatternAuthoringBase pattern)
        {
            return pattern switch
            {
                SinglePointPositionPatternAuthoring => "SinglePoint",
                LineEvenPositionPatternAuthoring => "LineEven",
                PointSetPositionPatternAuthoring pointSet => $"PointSet({pointSet.Points?.Length ?? 0})",
                null => "Position=None",
                _ => pattern.GetType().Name,
            };
        }

        private static string FormatAim(WaveAimAuthoringBase aim)
        {
            return aim switch
            {
                RandomAimAuthoring => "Random",
                FixedAimAuthoring fixedAim => $"Fixed({fixedAim.BaseAngleDeg:0.###})",
                LineNormalAimAuthoring lineNormal => $"LineNormal({FormatNormalSide(lineNormal.NormalSide)},{FormatSigned(lineNormal.AngleOffsetDeg)})",
                SpiralAimAuthoring spiral => $"Spiral({spiral.BaseAngleDeg:0.###},{FormatSigned(spiral.SpiralStepDeg)})",
                PlayerPositionAimAuthoring playerPosition => $"PlayerPosition({playerPosition.SnapshotTiming},{FormatSigned(playerPosition.AngleOffsetDeg)})",
                null => "Aim=None",
                _ => aim.GetType().Name,
            };
        }

        private static string FormatShotPattern(WaveShotPatternAuthoringBase shotPattern)
        {
            return shotPattern switch
            {
                SingleShotPatternAuthoring => "Single",
                NWayShotPatternAuthoring nWay => $"NWay({nWay.ShotCount}@{nWay.AngleSpacingDeg:0.###})",
                RadialShotPatternAuthoring radial => $"Radial({radial.ShotCount})",
                null => "ShotPattern=None",
                _ => shotPattern.GetType().Name,
            };
        }

        private static string FormatNormalSide(WaveLineNormalSideId side)
        {
            return side == WaveLineNormalSideId.Right ? "R" : "L";
        }

        private static string FormatSigned(float value)
        {
            return value >= 0f ? $"+{value:0.###}" : $"{value:0.###}";
        }

        private static int TryExtractBracketedIndex(string location, string marker)
        {
            int markerIndex = location.IndexOf(marker, System.StringComparison.Ordinal);
            if (markerIndex < 0)
                return -1;

            markerIndex += marker.Length;
            int endIndex = location.IndexOf(']', markerIndex);
            if (endIndex < 0)
                return -1;

            return int.TryParse(location.Substring(markerIndex, endIndex - markerIndex), out int value)
                ? value
                : -1;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, maxLength - 3) + "...";
        }
    }
}
