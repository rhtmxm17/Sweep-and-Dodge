using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(HazardActorAuthoring))]
    public sealed class HazardActorAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var actor = (HazardActorAuthoring)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Actor Id", actor.ActorId);
                EditorGUILayout.Toggle("Enabled", actor.Enabled);
                EditorGUILayout.Toggle("Start Suppressed", actor.StartSuppressed);
                EditorGUILayout.EnumPopup("Initial Presence", actor.InitialPresenceState);
                EditorGUILayout.IntField("Initial Phase Id", actor.InitialPhaseId);
                EditorGUILayout.IntField("Phase Count", actor.PhaseSelectorPolicies?.Length ?? 0);
                EditorGUILayout.IntField("Transition Count", actor.PhaseProgressTransitions?.Length ?? 0);
                EditorGUILayout.IntField("Pattern Slot Count", actor.PatternSlots?.Length ?? 0);
            }

            var prefab = ResolvePrefab(actor);
            var issues = HazardActorPreviewSnapshotBuilder.Validate(prefab != null ? prefab : actor.gameObject);
            int errorCount = issues.Count(x => x.Severity == ContentValidationSeverity.Error);
            int warningCount = issues.Count(x => x.Severity == ContentValidationSeverity.Warning);
            MessageType messageType = errorCount > 0 ? MessageType.Error : warningCount > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(
                errorCount > 0 || warningCount > 0
                    ? $"Validation: errors={errorCount}, warnings={warningCount}. Open Workbench for target navigation."
                    : "Validation: no errors.",
                messageType);

            if (GUILayout.Button("Open HazardActor Workbench"))
                HazardActorWorkbenchWindow.Open(prefab != null ? prefab : actor.gameObject);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "HazardActor raw arrays are read-only in the default Inspector. Use the Workbench for official actor, phase, transition, pattern, telegraph, and emission profile editing.",
                MessageType.Info);
        }

        private static GameObject ResolvePrefab(HazardActorAuthoring actor)
        {
            if (actor == null)
                return null;
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(actor.gameObject);
            if (prefab != null)
                return prefab;
            return actor.gameObject;
        }
    }
}
