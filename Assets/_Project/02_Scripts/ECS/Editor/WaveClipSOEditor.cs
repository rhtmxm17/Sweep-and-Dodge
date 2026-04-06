using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(WaveClipSO))]
    internal sealed class WaveClipSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _clipIdProperty;
        private SerializedProperty _phaseProperty;
        private SerializedProperty _laneProperty;
        private SerializedProperty _durationProperty;
        private SerializedProperty _segmentsProperty;

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
            DrawMetadataSection();
            DrawSharedReferenceStatus(clip);
            DrawSegmentsSection(clip);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMetadataSection()
        {
            EditorGUILayout.LabelField("Clip Metadata", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_clipIdProperty);
            EditorGUILayout.PropertyField(_phaseProperty);
            EditorGUILayout.PropertyField(_laneProperty);
            EditorGUILayout.PropertyField(_durationProperty);
            EditorGUILayout.Space(4f);
        }

        private void DrawSharedReferenceStatus(WaveClipSO clip)
        {
            var issues = WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip);
            if (issues.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Shared SerializeReference graph detected in {issues.Count} slot(s). Run 'Repair Shared References' before editing duplicated segments or directives.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("No shared SerializeReference graph detected.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Shared References"))
                {
                    string message = issues.Count > 0
                        ? BuildValidationSummary(issues)
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

        private void DrawSegmentsSection(WaveClipSO clip)
        {
            EditorGUILayout.LabelField("Local Segments (Overlap Allowed)", EditorStyles.boldLabel);

            int segmentCount = _segmentsProperty?.arraySize ?? 0;
            for (int s = 0; s < segmentCount; s++)
            {
                SerializedProperty segmentProperty = _segmentsProperty.GetArrayElementAtIndex(s);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSegmentHeader(clip, s);

                    var descriptionProperty = segmentProperty.FindPropertyRelative("editorOnlyDescription");
                    if (descriptionProperty != null)
                        EditorGUILayout.PropertyField(descriptionProperty);

                    EditorGUILayout.PropertyField(segmentProperty.FindPropertyRelative(nameof(WaveClipSO.ClipSegment.StartSec)));
                    EditorGUILayout.PropertyField(segmentProperty.FindPropertyRelative(nameof(WaveClipSO.ClipSegment.EndSec)));

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

        private void DrawSegmentHeader(WaveClipSO clip, int segmentIndex)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Segment {segmentIndex}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

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
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Directive {d}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Duplicate", GUILayout.Width(60f)))
                        {
                            serializedObject.ApplyModifiedProperties();
                            DuplicateDirective(clip, segmentIndex, d);
                            GUIUtility.ExitGUI();
                        }

                        if (GUILayout.Button("Remove", GUILayout.Width(60f)))
                        {
                            serializedObject.ApplyModifiedProperties();
                            RemoveDirective(clip, segmentIndex, d);
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.PropertyField(directiveProperty, true);
                }
            }

            if (GUILayout.Button($"Add Directive To Segment {segmentIndex}"))
            {
                serializedObject.ApplyModifiedProperties();
                AppendDirective(clip, segmentIndex);
                GUIUtility.ExitGUI();
            }
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
            if (clip.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return;

            Undo.RecordObject(clip, "Add WaveClip Directive");
            var segments = clip.Segments;
            var segment = segments[segmentIndex];
            var directives = new List<WaveSpawnEntryAuthoring>(segment.Directives ?? System.Array.Empty<WaveSpawnEntryAuthoring>())
            {
                WaveClipManagedReferenceGraphUtility.CreateDefaultDirective()
            };
            segment.Directives = directives.ToArray();
            segments[segmentIndex] = segment;
            clip.Segments = segments;
            EditorUtility.SetDirty(clip);
            serializedObject.Update();
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

        private static string BuildValidationSummary(List<WaveClipSharedManagedReferenceIssue> issues)
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
}
