using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SweepNDodge.DotsBullets.Editor
{
    public sealed class HazardActorWorkbenchWindow : EditorWindow
    {
        private readonly List<GameObject> _prefabs = new List<GameObject>(32);
        private readonly HazardActorPreviewSession _previewSession = new HazardActorPreviewSession();
        private GameObject _actorPrefab;
        private HazardActorAuthoring _actor;
        private HazardActorWorkbenchSelection _selection;
        private ScrollView _library;
        private ScrollView _canvas;
        private ScrollView _inspector;
        private ScrollView _issues;
        private IMGUIContainer _previewSurface;
        private EnumField _scopeField;
        private Slider _progressSlider;
        private Vector3Field _targetField;

        [MenuItem("Tools/Project/Hazard Actor Workbench/Open")]
        public static void Open()
        {
            GetWindow<HazardActorWorkbenchWindow>("Hazard Actor Workbench");
        }

        public static void Open(GameObject actorPrefab)
        {
            var window = GetWindow<HazardActorWorkbenchWindow>("Hazard Actor Workbench");
            window.SetActorPrefab(actorPrefab);
        }

        public GameObject ActiveActorPrefab => _actorPrefab;
        public HazardActorWorkbenchSelection Selection => _selection;
        public HazardActorPreviewSession PreviewSession => _previewSession;

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            BuildToolbar();

            var split = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };
            _library = Panel("Archetype Library", 240);
            _canvas = Panel("Behavior / Preview", 520);
            _inspector = Panel("Contextual Inspector", 320);
            split.Add(_library);
            split.Add(_canvas);
            split.Add(_inspector);
            rootVisualElement.Add(split);

            _issues = new ScrollView { style = { minHeight = 90, maxHeight = 130 } };
            rootVisualElement.Add(_issues);

            RefreshProjectPrefabs();
            RefreshAll();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            HazardActorPreviewCoordinator.SetActiveSession(_previewSession);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            HazardActorPreviewCoordinator.ClearActiveSession(_previewSession);
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            var picker = new UnityEditor.UIElements.ObjectField { objectType = typeof(GameObject), allowSceneObjects = false, value = _actorPrefab };
            picker.RegisterValueChangedCallback(evt => SetActorPrefab((GameObject)evt.newValue));
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(() => RefreshProjectPrefabs()) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(() => SelectActor()) { text = "Actor" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(() => RestartPreview()) { text = "Restart" });
            toolbar.Add(new ToolbarButton(() => _previewSession.Play()) { text = "Play" });
            toolbar.Add(new ToolbarButton(() => _previewSession.Pause()) { text = "Pause" });
            toolbar.Add(new ToolbarButton(() => { _previewSession.Step(); Repaint(); }) { text = "Step" });
            toolbar.Add(new ToolbarButton(() => { _previewSession.CleanupOldestGhost(); Repaint(); }) { text = "CleanupRemoved" });
            rootVisualElement.Add(toolbar);
        }

        private ScrollView Panel(string title, int width)
        {
            var root = new ScrollView { style = { width = width, flexGrow = 1f, paddingLeft = 6, paddingRight = 6, paddingTop = 6 } };
            root.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            return root;
        }

        public void SetActorPrefab(GameObject prefab)
        {
            _actorPrefab = prefab;
            _actor = ResolveActor(prefab);
            _selection = _actorPrefab != null ? HazardActorWorkbenchSelection.ForActor(_actorPrefab) : HazardActorWorkbenchSelection.None;
            RefreshAll();
            RestartPreview();
        }

        private void SelectActor()
        {
            if (_actorPrefab == null)
                return;
            _selection = HazardActorWorkbenchSelection.ForActor(_actorPrefab);
            RefreshAll();
        }

        private void RefreshProjectPrefabs()
        {
            _prefabs.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (ResolveActor(prefab) != null)
                    _prefabs.Add(prefab);
            }
            _prefabs.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            RefreshLibrary();
        }

        private void RefreshAll()
        {
            _actor = ResolveActor(_actorPrefab);
            RefreshLibrary();
            RefreshCanvas();
            RefreshInspector();
            RefreshIssues();
        }

        private void RefreshLibrary()
        {
            if (_library == null)
                return;
            _library.Clear();
            _library.Add(new Label("Archetype Library") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            for (int i = 0; i < _prefabs.Count; i++)
            {
                var prefab = _prefabs[i];
                var actor = ResolveActor(prefab);
                if (actor == null)
                    continue;
                int issueCount = HazardActorPreviewSnapshotBuilder.Validate(prefab).Count(x => x.Severity == ContentValidationSeverity.Error);
                string label = $"{prefab.name}  phase:{actor.PhaseSelectorPolicies?.Length ?? 0} pattern:{actor.PatternSlots?.Length ?? 0}";
                if (issueCount > 0)
                    label += $"  error:{issueCount}";
                _library.Add(new Button(() => SetActorPrefab(prefab)) { text = label });
            }
        }

        private void RefreshCanvas()
        {
            if (_canvas == null)
                return;
            _canvas.Clear();
            _canvas.Add(new Label("Behavior Canvas") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            if (_actor == null)
            {
                _canvas.Add(new HelpBox("Select a HazardActor prefab.", HelpBoxMessageType.Info));
                return;
            }

            DrawActorSummary();
            DrawPhaseChart();
            DrawPatternCards();
            DrawPreviewControls();
        }

        private void DrawActorSummary()
        {
            var summary = new Label($"Actor {_actor.ActorId} / Initial Phase {_actor.InitialPhaseId} / Presence {_actor.InitialPresenceState}");
            summary.style.marginBottom = 4;
            _canvas.Add(summary);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Button(() =>
            {
                HazardActorWorkbenchCommandUtility.AddPhase(_actor, out int phaseId);
                _selection = HazardActorWorkbenchSelection.ForPhase(_actorPrefab, phaseId);
                RefreshAfterMutation();
            }) { text = "Add Phase" });
            row.Add(new Button(() =>
            {
                HazardActorWorkbenchCommandUtility.AddPattern(_actor, out int patternId);
                _selection = HazardActorWorkbenchSelection.ForPattern(_actorPrefab, patternId);
                RefreshAfterMutation();
            }) { text = "Add Pattern" });
            _canvas.Add(row);
        }

        private void DrawPhaseChart()
        {
            _canvas.Add(new Label("Phases") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            var policies = _actor.PhaseSelectorPolicies ?? Array.Empty<HazardActorPhaseSelectorPolicyAuthoring>();
            for (int i = 0; i < policies.Length; i++)
            {
                var policy = policies[i];
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                row.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForPhase(_actorPrefab, policy.PhaseId);
                    RefreshInspector();
                }) { text = $"Phase {policy.PhaseId} [{policy.SelectionMode}]" });
                row.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForTransition(_actorPrefab, policy.PhaseId);
                    RefreshInspector();
                }) { text = "Transition" });
                row.Add(new Button(() =>
                {
                    HazardActorWorkbenchCommandUtility.DuplicatePhase(_actor, policy.PhaseId, out int newId);
                    _selection = HazardActorWorkbenchSelection.ForPhase(_actorPrefab, newId);
                    RefreshAfterMutation();
                }) { text = "Duplicate" });
                row.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePhase(_actor, policy.PhaseId, -1); RefreshAfterMutation(); }) { text = "Up" });
                row.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePhase(_actor, policy.PhaseId, 1); RefreshAfterMutation(); }) { text = "Down" });
                row.Add(new Button(() =>
                {
                    if (!HazardActorWorkbenchCommandUtility.RemovePhase(_actor, policy.PhaseId, out string error))
                        EditorUtility.DisplayDialog("Remove Phase", error, "OK");
                    RefreshAfterMutation();
                }) { text = "Remove" });
                _canvas.Add(row);
            }
        }

        private void DrawPatternCards()
        {
            _canvas.Add(new Label("Patterns") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            var slots = _actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                string emission = slot.Emission.Profile != null ? slot.Emission.Profile.name : "(missing emission)";
                string telegraph = slot.TelegraphProfile != null ? slot.TelegraphProfile.name : "(missing telegraph)";
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                row.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForPattern(_actorPrefab, slot.PatternSlotId);
                    RefreshInspector();
                }) { text = $"Pattern {slot.PatternSlotId}: {telegraph} -> {emission}" });
                row.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForEmissionProfile(_actorPrefab, slot.PatternSlotId, slot.Emission.Profile);
                    RefreshInspector();
                }) { text = "Emission Profile" });
                row.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForTelegraphProfile(_actorPrefab, slot.PatternSlotId, slot.TelegraphProfile);
                    RefreshInspector();
                }) { text = "Telegraph" });
                row.Add(new Button(() =>
                {
                    HazardActorWorkbenchCommandUtility.DuplicatePattern(_actor, slot.PatternSlotId, out int newId);
                    _selection = HazardActorWorkbenchSelection.ForPattern(_actorPrefab, newId);
                    RefreshAfterMutation();
                }) { text = "Duplicate" });
                row.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePattern(_actor, slot.PatternSlotId, -1); RefreshAfterMutation(); }) { text = "Up" });
                row.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePattern(_actor, slot.PatternSlotId, 1); RefreshAfterMutation(); }) { text = "Down" });
                row.Add(new Button(() =>
                {
                    if (!HazardActorWorkbenchCommandUtility.RemovePattern(_actor, slot.PatternSlotId, out string error))
                        EditorUtility.DisplayDialog("Remove Pattern", error, "OK");
                    RefreshAfterMutation();
                }) { text = "Remove" });
                _canvas.Add(row);
            }
        }

        private void DrawPreviewControls()
        {
            _canvas.Add(new Label("Preview") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            _scopeField = new EnumField("Scope", HazardActorPreviewScope.Actor);
            _scopeField.RegisterValueChangedCallback(_ => RestartPreview());
            _canvas.Add(_scopeField);
            _progressSlider = new Slider("Source Progress", 0f, 1f) { value = 0f };
            _progressSlider.RegisterValueChangedCallback(_ => RestartPreview());
            _canvas.Add(_progressSlider);
            _targetField = new Vector3Field("Target") { value = new Vector3(0f, 0f, 3f) };
            _targetField.RegisterValueChangedCallback(_ => RestartPreview());
            _canvas.Add(_targetField);
            _previewSurface = new IMGUIContainer(DrawEmbeddedPreview) { style = { height = 260, marginTop = 4 } };
            _canvas.Add(_previewSurface);
        }

        private void RefreshInspector()
        {
            if (_inspector == null)
                return;
            _inspector.Clear();
            _inspector.Add(new Label("Contextual Inspector") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            if (_actor == null)
            {
                _inspector.Add(new HelpBox("No actor selected.", HelpBoxMessageType.Info));
                return;
            }

            switch (_selection.Kind)
            {
                case HazardActorWorkbenchSelectionKind.Actor:
                    BindActorFields();
                    break;
                case HazardActorWorkbenchSelectionKind.Phase:
                    BindArrayElement(nameof(HazardActorAuthoring.PhaseSelectorPolicies), FindPhaseIndex(_selection.PhaseId), $"Phase {_selection.PhaseId}");
                    break;
                case HazardActorWorkbenchSelectionKind.Transition:
                    BindTransition(_selection.TransitionFromPhaseId);
                    break;
                case HazardActorWorkbenchSelectionKind.PatternSlot:
                    BindArrayElement(nameof(HazardActorAuthoring.PatternSlots), FindPatternIndex(_selection.PatternSlotId), $"Pattern {_selection.PatternSlotId}");
                    break;
                case HazardActorWorkbenchSelectionKind.EmissionProfile:
                    BindEmissionProfile(_selection.ProfileAsset as EmissionProfileSO);
                    break;
                case HazardActorWorkbenchSelectionKind.TelegraphProfile:
                    BindProfileObject(_selection.ProfileAsset, "Telegraph Profile");
                    break;
                default:
                    _inspector.Add(new HelpBox("Select Actor, Phase, Transition, Pattern, or Profile from the canvas.", HelpBoxMessageType.Info));
                    break;
            }
        }

        private void BindActorFields()
        {
            var serialized = new SerializedObject(_actor);
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.ActorId));
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.Enabled));
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.StartSuppressed));
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.InitialPresenceState));
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.ActivationDurationSec));
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.RetireDurationSec));
            AddBoundProperty(serialized, nameof(HazardActorAuthoring.InitialPhaseId));
        }

        private void BindArrayElement(string arrayName, int index, string label)
        {
            if (index < 0)
            {
                _inspector.Add(new HelpBox("Selected identity is missing or ambiguous.", HelpBoxMessageType.Warning));
                return;
            }

            var serialized = new SerializedObject(_actor);
            var array = serialized.FindProperty(arrayName);
            var element = array.GetArrayElementAtIndex(index);
            var field = new PropertyField(element, label);
            field.Bind(serialized);
            _inspector.Add(field);
        }

        private void BindTransition(int fromPhaseId)
        {
            int index = FindTransitionIndex(fromPhaseId);
            if (index < 0)
            {
                _inspector.Add(new HelpBox("No transition exists for this source phase.", HelpBoxMessageType.Info));
                var toField = new IntegerField("To Phase Id") { value = fromPhaseId + 1 };
                _inspector.Add(toField);
                _inspector.Add(new Button(() =>
                {
                    HazardActorWorkbenchCommandUtility.AddTransition(_actor, fromPhaseId, toField.value);
                    RefreshAfterMutation();
                }) { text = "Add Transition" });
                return;
            }

            BindArrayElement(nameof(HazardActorAuthoring.PhaseProgressTransitions), index, $"Transition from {fromPhaseId}");
            _inspector.Add(new Button(() =>
            {
                HazardActorWorkbenchCommandUtility.RemoveTransition(_actor, fromPhaseId);
                RefreshAfterMutation();
            }) { text = "Remove Transition" });
        }

        private void BindEmissionProfile(EmissionProfileSO profile)
        {
            if (profile == null)
            {
                _inspector.Add(new HelpBox("Pattern has no emission profile.", HelpBoxMessageType.Warning));
                return;
            }
            _inspector.Add(new Label(AssetDatabase.GetAssetPath(profile)));
            _inspector.Add(new Label($"Project pattern users: {HazardActorWorkbenchCommandUtility.CountEmissionProfileUsers(profile)}"));
            _inspector.Add(new Button(() =>
            {
                UnityEditor.Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
            }) { text = "Open" });
            _inspector.Add(new Button(() =>
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Duplicate Emission Profile",
                    profile.name + "_copy",
                    "asset",
                    "Choose duplicate profile path.");
                if (string.IsNullOrEmpty(path))
                    return;
                if (!HazardActorWorkbenchCommandUtility.DuplicateAndAssignEmissionProfile(
                        _actor,
                        _selection.PatternSlotId,
                        path,
                        out var duplicate,
                        out string error))
                {
                    EditorUtility.DisplayDialog("Duplicate & Assign", error, "OK");
                    return;
                }
                _selection = HazardActorWorkbenchSelection.ForEmissionProfile(_actorPrefab, _selection.PatternSlotId, duplicate);
                RefreshAfterMutation();
            }) { text = "Duplicate & Assign" });
            BindProfileObject(profile, "Emission Profile");
        }

        private void BindProfileObject(UnityEngine.Object profile, string label)
        {
            if (profile == null)
            {
                _inspector.Add(new HelpBox($"{label} is not assigned.", HelpBoxMessageType.Warning));
                return;
            }

            var serialized = new SerializedObject(profile);
            var iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                    continue;
                var copy = iterator.Copy();
                var field = new PropertyField(copy);
                field.Bind(serialized);
                _inspector.Add(field);
            }
        }

        private void AddBoundProperty(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                return;
            var field = new PropertyField(property);
            field.Bind(serialized);
            _inspector.Add(field);
        }

        private void RefreshIssues()
        {
            if (_issues == null)
                return;
            _issues.Clear();
            var header = new Label("Issue Navigator") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            _issues.Add(header);
            if (_actorPrefab == null)
                return;
            var issues = HazardActorPreviewSnapshotBuilder.Validate(_actorPrefab);
            for (int i = 0; i < issues.Length; i++)
            {
                var issue = issues[i];
                _issues.Add(new Button(() =>
                {
                    _selection = issue.Target;
                    if (issue.Target.ProfileAsset != null)
                        EditorGUIUtility.PingObject(issue.Target.ProfileAsset);
                    RefreshInspector();
                }) { text = $"{issue.Severity} {issue.Code}: {issue.Message}" });
            }
        }

        private void RestartPreview()
        {
            if (_actorPrefab == null)
                return;
            HazardActorPreviewSnapshotBuilder.TryBuild(_actorPrefab, out var snapshot);
            var input = new HazardActorPreviewInput
            {
                Scope = _scopeField != null ? (HazardActorPreviewScope)_scopeField.value : HazardActorPreviewScope.Actor,
                SourceProgress01 = _progressSlider != null ? _progressSlider.value : 0f,
                TargetWorldPosition = _targetField != null ? _targetField.value : new Vector3(0f, 0f, 3f),
                ActorWorldPosition = Vector3.zero,
                ActorYawDeg = 0f,
                SpawnAtStart = true,
                ForcedPhaseId = _selection.Kind == HazardActorWorkbenchSelectionKind.Phase ? _selection.PhaseId : 0,
                ForcedPatternSlotId = _selection.Kind == HazardActorWorkbenchSelectionKind.PatternSlot ? _selection.PatternSlotId : 0,
            };
            _previewSession.Load(snapshot, input);
            HazardActorPreviewCoordinator.SetActiveSession(_previewSession);
            Repaint();
        }

        private void DrawEmbeddedPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 240f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);
            var frame = _previewSession.Frame;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 20f), $"t={_previewSession.TimeSec:0.00} presence={frame.Presence} phase={frame.PhaseId} pattern={frame.PatternSlotId} {frame.Lifecycle}");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, 20f), $"ghosts={frame.ActiveGhostCount}/{_previewSession.GhostCap} suppressed={frame.SuppressedGhostCount}");
            if (!string.IsNullOrEmpty(frame.Warning))
                GUI.Label(new Rect(rect.x + 8f, rect.y + 48f, rect.width - 16f, 20f), frame.Warning);

            Vector2 center = rect.center;
            DrawDot(center, Color.magenta, 5f);
            var ghosts = _previewSession.Ghosts;
            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector2 p = center + new Vector2(ghosts[i].Position.x, -ghosts[i].Position.z) * 24f;
                DrawDot(p, Color.cyan, 3f);
            }
        }

        private static void DrawDot(Vector2 center, Color color, float size)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void RefreshAfterMutation()
        {
            _actor = ResolveActor(_actorPrefab);
            RefreshAll();
            RestartPreview();
        }

        private int FindPhaseIndex(int phaseId)
        {
            return HazardActorWorkbenchCommandUtility.TryFindPhaseIndex(_actor, phaseId, out int index) ? index : -1;
        }

        private int FindPatternIndex(int patternSlotId)
        {
            return HazardActorWorkbenchCommandUtility.TryFindPatternIndex(_actor, patternSlotId, out int index) ? index : -1;
        }

        private int FindTransitionIndex(int fromPhaseId)
        {
            return HazardActorWorkbenchCommandUtility.TryFindTransitionIndex(_actor, fromPhaseId, out int index) ? index : -1;
        }

        private void OnUndoRedo()
        {
            RefreshAll();
            RestartPreview();
        }

        private void OnBeforeAssemblyReload()
        {
            HazardActorPreviewCoordinator.ClearActiveSession(_previewSession);
        }

        private static HazardActorAuthoring ResolveActor(GameObject prefab)
        {
            if (prefab == null)
                return null;
            var actors = prefab.GetComponentsInChildren<HazardActorAuthoring>(true);
            return actors != null && actors.Length == 1 ? actors[0] : null;
        }
    }
}
