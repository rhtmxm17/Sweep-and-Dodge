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
        private ScrollView _canvas;
        private ScrollView _inspector;
        private ScrollView _issues;
        private Label _currentArchetypeLabel;
        private Label _currentArchetypeStatusLabel;
        private ToolbarButton _changeArchetypeButton;
        private HazardActorWorkbenchPreviewElement _previewSurface;
        private EnumField _scopeField;
        private Slider _progressSlider;
        private Vector3Field _targetField;
        private EnumField _previewDisplayModeField;
        private Vector2Field _previewViewCenterField;
        private Slider _previewViewHalfHeightField;
        private Label _previewStatusLabel;
        private Label _previewWarningLabel;
        private HazardActorPreviewDisplayMode _previewDisplayMode = HazardActorPreviewDisplayMode.Exact;
        private Vector2 _previewViewCenter = Vector2.zero;
        private float _previewViewHalfHeight = 8f;
        private string _observedPreviewSignature = string.Empty;
        private double _nextPreviewSignatureCheck;

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
        public HazardActorPreviewDisplayMode PreviewDisplayMode => _previewDisplayMode;

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            BuildToolbar();

            var split = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };
            _canvas = Panel("Behavior / Preview", 520, 1f);
            _inspector = Panel("Contextual Inspector", 320);
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
            HazardActorPreviewCoordinator.PreviewRepaintRequested += OnPreviewRepaintRequested;
            HazardActorPreviewCoordinator.SetActiveSession(_previewSession);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            HazardActorPreviewCoordinator.PreviewRepaintRequested -= OnPreviewRepaintRequested;
            HazardActorPreviewCoordinator.ClearActiveSession(_previewSession);
        }

        private void Update()
        {
            if (_actorPrefab == null || EditorApplication.timeSinceStartup < _nextPreviewSignatureCheck)
                return;
            _nextPreviewSignatureCheck = EditorApplication.timeSinceStartup + 0.1d;
            string signature = ComputePreviewSignature();
            if (signature == _observedPreviewSignature)
                return;

            _observedPreviewSignature = signature;
            _actor = ResolveActor(_actorPrefab);
            RefreshCanvas();
            RefreshIssues();
            RestartPreview();
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            _currentArchetypeLabel = new Label("Archetype: (none)")
            {
                style =
                {
                    minWidth = 180,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };
            _currentArchetypeStatusLabel = new Label("No HazardActor prefab selected.")
            {
                style =
                {
                    minWidth = 260,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 10,
                }
            };
            toolbar.Add(_currentArchetypeLabel);
            toolbar.Add(_currentArchetypeStatusLabel);
            _changeArchetypeButton = new ToolbarButton(ShowArchetypePicker) { text = "Change Archetype" };
            toolbar.Add(_changeArchetypeButton);
            toolbar.Add(new ToolbarButton(() => RefreshProjectPrefabs()) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(() => SelectActor()) { text = "Actor" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(() => RestartPreview()) { text = "Restart" });
            toolbar.Add(new ToolbarButton(() => _previewSession.Play()) { text = "Play" });
            toolbar.Add(new ToolbarButton(() => _previewSession.Pause()) { text = "Pause" });
            toolbar.Add(new ToolbarButton(() => { _previewSession.Step(); RefreshPreviewPresentation(); }) { text = "Step" });
            toolbar.Add(new ToolbarButton(() => { _previewSession.CleanupOldestGhost(); RefreshPreviewPresentation(); }) { text = "CleanupRemoved" });
            rootVisualElement.Add(toolbar);
        }

        private ScrollView Panel(string title, int width, float flexGrow = 0f)
        {
            var root = new ScrollView { style = { width = width, flexGrow = flexGrow, paddingLeft = 6, paddingRight = 6, paddingTop = 6 } };
            if (flexGrow > 0f)
            {
                root.style.width = StyleKeyword.Auto;
                root.style.flexGrow = flexGrow;
            }
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
            RefreshToolbarArchetypeSummary();
        }

        private void RefreshAll()
        {
            _actor = ResolveActor(_actorPrefab);
            RefreshToolbarArchetypeSummary();
            RefreshCanvas();
            RefreshInspector();
            RefreshIssues();
        }

        private void RefreshToolbarArchetypeSummary()
        {
            if (_currentArchetypeLabel == null || _currentArchetypeStatusLabel == null)
                return;

            if (_actorPrefab == null || _actor == null)
            {
                _currentArchetypeLabel.text = "Archetype: (none)";
                _currentArchetypeStatusLabel.text = "No HazardActor prefab selected.";
                _currentArchetypeStatusLabel.tooltip = string.Empty;
                return;
            }

            var summary = BuildArchetypeSummary(_actorPrefab, _actorPrefab);
            _currentArchetypeLabel.text = $"Archetype: {summary.Name}";
            _currentArchetypeStatusLabel.text =
                $"{summary.PhaseCount} phases | {summary.PatternCount} patterns | {summary.ProfileCount} profiles | {summary.IssueLabel}";
            _currentArchetypeStatusLabel.tooltip = summary.Path;
        }

        private void ShowArchetypePicker()
        {
            RefreshProjectPrefabs();
            UnityEditor.PopupWindow.Show(
                _changeArchetypeButton != null ? _changeArchetypeButton.worldBound : new Rect(0f, 0f, 1f, 1f),
                new ArchetypePickerPopup(this));
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
            var issues = HazardActorPreviewSnapshotBuilder.Validate(_actorPrefab);
            DrawTableHeader(_canvas, "Phase", "Selector", "Candidates", "Transition", "Issues", "Commands");
            for (int i = 0; i < policies.Length; i++)
            {
                var policy = policies[i];
                var summary = BuildPhaseRowSummary(_actor, policy, issues);
                var row = BuildSelectableRow(_selection.Kind == HazardActorWorkbenchSelectionKind.Phase && _selection.PhaseId == policy.PhaseId);
                row.RegisterCallback<MouseUpEvent>(_ =>
                {
                    _selection = HazardActorWorkbenchSelection.ForPhase(_actorPrefab, policy.PhaseId);
                    RefreshInspector();
                });
                row.Add(Cell(summary.PhaseLabel, 70));
                row.Add(Cell(summary.SelectorLabel, 130));
                row.Add(Cell(summary.CandidatesLabel, 160));
                row.Add(Cell(summary.TransitionLabel, 210));
                row.Add(Cell(summary.IssueLabel, 80));
                var commands = CommandCell();
                commands.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForTransition(_actorPrefab, policy.PhaseId);
                    RefreshInspector();
                }) { text = "Transition" });
                commands.Add(new Button(() =>
                {
                    HazardActorWorkbenchCommandUtility.DuplicatePhase(_actor, policy.PhaseId, out int newId);
                    _selection = HazardActorWorkbenchSelection.ForPhase(_actorPrefab, newId);
                    RefreshAfterMutation();
                }) { text = "Duplicate" });
                commands.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePhase(_actor, policy.PhaseId, -1); RefreshAfterMutation(); }) { text = "Up" });
                commands.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePhase(_actor, policy.PhaseId, 1); RefreshAfterMutation(); }) { text = "Down" });
                commands.Add(new Button(() =>
                {
                    if (!HazardActorWorkbenchCommandUtility.RemovePhase(_actor, policy.PhaseId, out string error))
                        EditorUtility.DisplayDialog("Remove Phase", error, "OK");
                    RefreshAfterMutation();
                }) { text = "Remove" });
                row.Add(commands);
                _canvas.Add(row);
            }
        }

        private void DrawPatternCards()
        {
            _canvas.Add(new Label("Patterns") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            var slots = _actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            var issues = HazardActorPreviewSnapshotBuilder.Validate(_actorPrefab);
            DrawTableHeader(_canvas, "Pattern", "Telegraph", "Emission", "Schedule", "Movement", "Issues", "Commands");
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var summary = BuildPatternRowSummary(slot, issues);
                var row = BuildSelectableRow(_selection.Kind == HazardActorWorkbenchSelectionKind.PatternSlot && _selection.PatternSlotId == slot.PatternSlotId);
                row.RegisterCallback<MouseUpEvent>(_ =>
                {
                    _selection = HazardActorWorkbenchSelection.ForPattern(_actorPrefab, slot.PatternSlotId);
                    RefreshInspector();
                });
                row.Add(Cell(summary.PatternLabel, 70));
                row.Add(Cell(summary.TelegraphLabel, 140));
                row.Add(Cell(summary.EmissionLabel, 160));
                row.Add(Cell(summary.ScheduleLabel, 180));
                row.Add(Cell(summary.MovementLabel, 110));
                row.Add(Cell(summary.IssueLabel, 80));
                var commands = CommandCell();
                commands.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForEmissionProfile(_actorPrefab, slot.PatternSlotId, slot.Emission.Profile);
                    RefreshInspector();
                }) { text = "Emission Profile" });
                commands.Add(new Button(() =>
                {
                    _selection = HazardActorWorkbenchSelection.ForTelegraphProfile(_actorPrefab, slot.PatternSlotId, slot.TelegraphProfile);
                    RefreshInspector();
                }) { text = "Telegraph" });
                commands.Add(new Button(() =>
                {
                    HazardActorWorkbenchCommandUtility.DuplicatePattern(_actor, slot.PatternSlotId, out int newId);
                    _selection = HazardActorWorkbenchSelection.ForPattern(_actorPrefab, newId);
                    RefreshAfterMutation();
                }) { text = "Duplicate" });
                commands.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePattern(_actor, slot.PatternSlotId, -1); RefreshAfterMutation(); }) { text = "Up" });
                commands.Add(new Button(() => { HazardActorWorkbenchCommandUtility.MovePattern(_actor, slot.PatternSlotId, 1); RefreshAfterMutation(); }) { text = "Down" });
                commands.Add(new Button(() =>
                {
                    if (!HazardActorWorkbenchCommandUtility.RemovePattern(_actor, slot.PatternSlotId, out string error))
                        EditorUtility.DisplayDialog("Remove Pattern", error, "OK");
                    RefreshAfterMutation();
                }) { text = "Remove" });
                row.Add(commands);
                _canvas.Add(row);
            }
        }

        public static ArchetypeLibraryRowSummary BuildArchetypeSummary(GameObject prefab, GameObject activePrefab)
        {
            var actor = ResolveActor(prefab);
            if (prefab == null || actor == null)
                return new ArchetypeLibraryRowSummary(prefab, "(missing)", string.Empty, 0, 0, 0, 0, 0, prefab == activePrefab);

            var profiles = new HashSet<UnityEngine.Object>();
            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Emission.Profile != null)
                    profiles.Add(slots[i].Emission.Profile);
                if (slots[i].TelegraphProfile != null)
                    profiles.Add(slots[i].TelegraphProfile);
            }

            var issues = HazardActorPreviewSnapshotBuilder.Validate(prefab);
            return new ArchetypeLibraryRowSummary(
                prefab,
                prefab.name,
                AssetDatabase.GetAssetPath(prefab),
                actor.PhaseSelectorPolicies?.Length ?? 0,
                slots.Length,
                profiles.Count,
                CountIssues(issues, ContentValidationSeverity.Error),
                CountIssues(issues, ContentValidationSeverity.Warning),
                prefab == activePrefab);
        }

        public static PhaseRowSummary BuildPhaseRowSummary(
            HazardActorAuthoring actor,
            HazardActorPhaseSelectorPolicyAuthoring policy,
            HazardActorWorkbenchIssue[] issues)
        {
            var candidates = policy.Candidates ?? Array.Empty<HazardActorPhaseSelectorCandidateAuthoring>();
            string candidateSummary = candidates.Length == 0
                ? "no candidates"
                : string.Join(" -> ", candidates.Select((x, index) => $"P{x.PatternSlotId}(candidate {index})"));
            var transitions = actor != null
                ? actor.PhaseProgressTransitions ?? Array.Empty<HazardActorPhaseProgressTransitionAuthoring>()
                : Array.Empty<HazardActorPhaseProgressTransitionAuthoring>();
            string transitionSummary = "no progress transition";
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].FromPhaseId != policy.PhaseId)
                    continue;
                transitionSummary = $"progress >= {transitions[i].ProgressThresholdNormalized:0.##} -> Phase {transitions[i].ToPhaseId}, lead-in {transitions[i].TransitionLeadInSec:0.##}s";
                break;
            }
            CountTargetIssues(
                issues,
                target => (target.Kind == HazardActorWorkbenchSelectionKind.Phase && target.PhaseId == policy.PhaseId)
                    || (target.Kind == HazardActorWorkbenchSelectionKind.Transition && target.TransitionFromPhaseId == policy.PhaseId),
                out int errors,
                out int warnings);
            return new PhaseRowSummary(
                $"Phase {policy.PhaseId}",
                policy.SelectionMode.ToString(),
                candidateSummary,
                transitionSummary,
                FormatIssueCount(errors, warnings));
        }

        public static PatternRowSummary BuildPatternRowSummary(
            HazardActorPatternSlotAuthoring slot,
            HazardActorWorkbenchIssue[] issues)
        {
            string telegraph = slot.TelegraphProfile != null ? $"{slot.TelegraphProfile.TelegraphDurationSec:0.##}s" : "missing";
            string repeat = $"repeat x{Mathf.Max(1, slot.Emission.EventRepeatCount)}";
            string schedule = slot.Emission.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                ? $"timed {Mathf.Max(0f, slot.Emission.EventShotIntervalSec):0.###}s"
                : "instant";
            string cooldown = $"{Mathf.Max(0f, slot.Emission.CooldownSec):0.##}s";
            string profile = slot.Emission.Profile != null ? slot.Emission.Profile.name : "missing profile";
            string movement = slot.Emission.Profile != null && EmissionProfileResolver.TryResolve(slot.Emission.Profile, out var core, out _)
                ? core.MovementFamily.ToString()
                : "unknown movement";
            CountTargetIssues(
                issues,
                target => target.PatternSlotId == slot.PatternSlotId,
                out int errors,
                out int warnings);
            return new PatternRowSummary(
                $"Pattern {slot.PatternSlotId}",
                telegraph,
                profile,
                $"{repeat} / {schedule} / cooldown {cooldown}",
                movement,
                FormatIssueCount(errors, warnings));
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

            _previewDisplayModeField = new EnumField("Display", _previewDisplayMode);
            _previewDisplayModeField.RegisterValueChangedCallback(evt =>
            {
                _previewDisplayMode = (HazardActorPreviewDisplayMode)evt.newValue;
                RefreshPreviewPresentation();
            });
            _canvas.Add(_previewDisplayModeField);
            _canvas.Add(new Label("Exact preserves every visible bullet position. Density is a diagnostic aggregation view.")
            {
                style = { whiteSpace = WhiteSpace.Normal, fontSize = 10, marginBottom = 2 }
            });

            _previewViewCenterField = new Vector2Field("View Center") { value = _previewViewCenter };
            _previewViewCenterField.RegisterValueChangedCallback(evt =>
            {
                _previewViewCenter = evt.newValue;
                RefreshPreviewPresentation();
            });
            _canvas.Add(_previewViewCenterField);
            _previewViewHalfHeightField = new Slider("View Half Height", 1f, 32f) { value = _previewViewHalfHeight };
            _previewViewHalfHeightField.RegisterValueChangedCallback(evt =>
            {
                _previewViewHalfHeight = evt.newValue;
                RefreshPreviewPresentation();
            });
            _canvas.Add(_previewViewHalfHeightField);
            _canvas.Add(new Button(FitPreviewView) { text = "Fit Active Ghosts" });

            _previewStatusLabel = new Label();
            _previewStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            _canvas.Add(_previewStatusLabel);
            _previewWarningLabel = new Label();
            _previewWarningLabel.style.whiteSpace = WhiteSpace.Normal;
            _canvas.Add(_previewWarningLabel);
            _previewSurface = new HazardActorWorkbenchPreviewElement(_previewSession)
            {
                style = { height = 240, marginTop = 4 }
            };
            _canvas.Add(_previewSurface);
            RefreshPreviewPresentation();
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
                    SelectWorkbenchTarget(issue.Target);
                    if (issue.Target.ProfileAsset != null)
                        EditorGUIUtility.PingObject(issue.Target.ProfileAsset);
                }) { text = $"{issue.Severity} {issue.Code}: {issue.Message}" });
            }
        }

        private void SelectWorkbenchTarget(HazardActorWorkbenchSelection target)
        {
            if (target.ActorPrefab != null && target.ActorPrefab != _actorPrefab)
            {
                _actorPrefab = target.ActorPrefab;
                _actor = ResolveActor(_actorPrefab);
            }

            _selection = target.Kind != HazardActorWorkbenchSelectionKind.None
                ? target
                : (_actorPrefab != null ? HazardActorWorkbenchSelection.ForActor(_actorPrefab) : HazardActorWorkbenchSelection.None);
            RefreshAll();
            RestartPreview();
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
            _observedPreviewSignature = ComputePreviewSignature();
            HazardActorPreviewCoordinator.SetActiveSession(_previewSession);
            RefreshPreviewPresentation();
        }

        private void RefreshPreviewPresentation()
        {
            var frame = _previewSession.Frame;
            if (_previewStatusLabel != null)
            {
                _previewStatusLabel.text =
                    $"t={_previewSession.TimeSec:0.00} presence={frame.Presence} phase={frame.PhaseId} " +
                    $"pattern={frame.PatternSlotId} {frame.Lifecycle} | ghosts={frame.ActiveGhostCount}/{_previewSession.GhostCap} " +
                    $"suppressed={frame.SuppressedGhostCount} | display={_previewDisplayMode}";
            }
            if (_previewWarningLabel != null)
                _previewWarningLabel.text = string.IsNullOrEmpty(frame.Warning) ? string.Empty : frame.Warning;
            if (_previewSurface != null)
            {
                _previewSurface.DisplayMode = _previewDisplayMode;
                _previewSurface.SetView(_previewViewCenter, _previewViewHalfHeight);
                _previewSurface.MarkDirtyRepaint();
            }
            Repaint();
        }

        private void FitPreviewView()
        {
            Vector3 actor = _previewSession.Input.ActorWorldPosition;
            Vector3 target = _previewSession.Input.TargetWorldPosition;
            float minX = Mathf.Min(actor.x, target.x);
            float maxX = Mathf.Max(actor.x, target.x);
            float minZ = Mathf.Min(actor.z, target.z);
            float maxZ = Mathf.Max(actor.z, target.z);
            var ghosts = _previewSession.Ghosts;
            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector3 position = ghosts[i].Position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
            }

            _previewViewCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float aspect = _previewSurface != null && _previewSurface.contentRect.height > 0f
                ? _previewSurface.contentRect.width / _previewSurface.contentRect.height
                : 2f;
            float halfWidth = Mathf.Max(0.5f, (maxX - minX) * 0.5f);
            float halfHeight = Mathf.Max(0.5f, (maxZ - minZ) * 0.5f);
            _previewViewHalfHeight = Mathf.Clamp(Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.1f, aspect)) * 1.15f, 1f, 32f);
            _previewViewCenterField?.SetValueWithoutNotify(_previewViewCenter);
            _previewViewHalfHeightField?.SetValueWithoutNotify(_previewViewHalfHeight);
            RefreshPreviewPresentation();
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

        private void OnPreviewRepaintRequested()
        {
            RefreshPreviewPresentation();
        }

        private string ComputePreviewSignature()
        {
            if (_actor == null)
                return string.Empty;

            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ EditorUtility.GetDirtyCount(_actor);
                var slots = _actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
                for (int i = 0; i < slots.Length; i++)
                {
                    hash = (hash * 397) ^ slots[i].PatternSlotId;
                    if (slots[i].TelegraphProfile != null)
                        hash = (hash * 397) ^ EditorUtility.GetDirtyCount(slots[i].TelegraphProfile);
                    if (slots[i].Emission.Profile != null)
                        hash = (hash * 397) ^ EditorUtility.GetDirtyCount(slots[i].Emission.Profile);
                }
                return hash.ToString();
            }
        }

        private static HazardActorAuthoring ResolveActor(GameObject prefab)
        {
            if (prefab == null)
                return null;
            var actors = prefab.GetComponentsInChildren<HazardActorAuthoring>(true);
            return actors != null && actors.Length == 1 ? actors[0] : null;
        }

        private static void DrawTableHeader(VisualElement parent, params string[] labels)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 3,
                    marginBottom = 2,
                    paddingBottom = 2,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                }
            };
            for (int i = 0; i < labels.Length; i++)
                row.Add(Cell(labels[i], HeaderWidth(i), FontStyle.Bold));
            parent.Add(row);
        }

        private static float HeaderWidth(int index)
        {
            switch (index)
            {
                case 0: return 70f;
                case 1: return 130f;
                case 2: return 160f;
                case 3: return 210f;
                case 4: return 110f;
                case 5: return 80f;
                default: return 300f;
            }
        }

        private static VisualElement BuildSelectableRow(bool selected)
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.NoWrap,
                    paddingTop = 2,
                    paddingBottom = 2,
                    backgroundColor = selected ? new Color(0.18f, 0.27f, 0.38f, 1f) : Color.clear,
                }
            };
        }

        private static Label Cell(string text, float width, FontStyle fontStyle = FontStyle.Normal)
        {
            return new Label(text ?? string.Empty)
            {
                tooltip = text ?? string.Empty,
                style =
                {
                    width = width,
                    minWidth = width,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    unityFontStyleAndWeight = fontStyle,
                    paddingRight = 4,
                }
            };
        }

        private static VisualElement CommandCell()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    width = 300,
                    minWidth = 300,
                }
            };
        }

        private static int CountIssues(HazardActorWorkbenchIssue[] issues, ContentValidationSeverity severity)
        {
            int count = 0;
            if (issues == null)
                return 0;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i].Severity == severity)
                    count++;
            }
            return count;
        }

        private static void CountTargetIssues(
            HazardActorWorkbenchIssue[] issues,
            Func<HazardActorWorkbenchSelection, bool> predicate,
            out int errors,
            out int warnings)
        {
            errors = 0;
            warnings = 0;
            if (issues == null || predicate == null)
                return;
            for (int i = 0; i < issues.Length; i++)
            {
                if (!predicate(issues[i].Target))
                    continue;
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    errors++;
                else
                    warnings++;
            }
        }

        private static string FormatIssueCount(int errors, int warnings)
        {
            if (errors > 0)
                return $"{errors} error";
            if (warnings > 0)
                return $"{warnings} warn";
            return "OK";
        }

        public readonly struct ArchetypeLibraryRowSummary
        {
            public ArchetypeLibraryRowSummary(
                GameObject prefab,
                string name,
                string path,
                int phaseCount,
                int patternCount,
                int profileCount,
                int errorCount,
                int warningCount,
                bool isActive)
            {
                Prefab = prefab;
                Name = name ?? string.Empty;
                Path = path ?? string.Empty;
                PhaseCount = phaseCount;
                PatternCount = patternCount;
                ProfileCount = profileCount;
                ErrorCount = errorCount;
                WarningCount = warningCount;
                IsActive = isActive;
            }

            public GameObject Prefab { get; }
            public string Name { get; }
            public string Path { get; }
            public int PhaseCount { get; }
            public int PatternCount { get; }
            public int ProfileCount { get; }
            public int ErrorCount { get; }
            public int WarningCount { get; }
            public bool IsActive { get; }
            public string IssueLabel => FormatIssueCount(ErrorCount, WarningCount);
        }

        public readonly struct PhaseRowSummary
        {
            public PhaseRowSummary(string phaseLabel, string selectorLabel, string candidatesLabel, string transitionLabel, string issueLabel)
            {
                PhaseLabel = phaseLabel ?? string.Empty;
                SelectorLabel = selectorLabel ?? string.Empty;
                CandidatesLabel = candidatesLabel ?? string.Empty;
                TransitionLabel = transitionLabel ?? string.Empty;
                IssueLabel = issueLabel ?? string.Empty;
            }

            public string PhaseLabel { get; }
            public string SelectorLabel { get; }
            public string CandidatesLabel { get; }
            public string TransitionLabel { get; }
            public string IssueLabel { get; }
        }

        public readonly struct PatternRowSummary
        {
            public PatternRowSummary(string patternLabel, string telegraphLabel, string emissionLabel, string scheduleLabel, string movementLabel, string issueLabel)
            {
                PatternLabel = patternLabel ?? string.Empty;
                TelegraphLabel = telegraphLabel ?? string.Empty;
                EmissionLabel = emissionLabel ?? string.Empty;
                ScheduleLabel = scheduleLabel ?? string.Empty;
                MovementLabel = movementLabel ?? string.Empty;
                IssueLabel = issueLabel ?? string.Empty;
            }

            public string PatternLabel { get; }
            public string TelegraphLabel { get; }
            public string EmissionLabel { get; }
            public string ScheduleLabel { get; }
            public string MovementLabel { get; }
            public string IssueLabel { get; }
        }

        private sealed class ArchetypePickerPopup : PopupWindowContent
        {
            private readonly HazardActorWorkbenchWindow _owner;
            private readonly List<ArchetypeLibraryRowSummary> _summaries = new List<ArchetypeLibraryRowSummary>();
            private string _search = string.Empty;
            private Vector2 _scroll;
            private int _selectedIndex;

            public ArchetypePickerPopup(HazardActorWorkbenchWindow owner)
            {
                _owner = owner;
                if (_owner == null)
                    return;
                for (int i = 0; i < _owner._prefabs.Count; i++)
                    _summaries.Add(BuildArchetypeSummary(_owner._prefabs[i], _owner._actorPrefab));
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(720f, 420f);
            }

            public override void OnGUI(Rect rect)
            {
                if (_owner == null)
                    return;

                EditorGUILayout.Space(4);
                GUI.SetNextControlName("HazardActorArchetypeSearch");
                _search = EditorGUILayout.TextField("Search", _search);
                EditorGUILayout.Space(4);
                DrawHeader();
                var filtered = BuildFilteredSummaries();
                HandleKeyboard(filtered);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                for (int i = 0; i < filtered.Count; i++)
                    DrawRow(filtered[i], i);
                EditorGUILayout.EndScrollView();
            }

            private List<ArchetypeLibraryRowSummary> BuildFilteredSummaries()
            {
                var result = new List<ArchetypeLibraryRowSummary>();
                string needle = _search ?? string.Empty;
                for (int i = 0; i < _summaries.Count; i++)
                {
                    var summary = _summaries[i];
                    if (!string.IsNullOrWhiteSpace(needle)
                        && summary.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0
                        && summary.Path.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    result.Add(summary);
                }
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, result.Count - 1));
                return result;
            }

            private void DrawHeader()
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("Name", EditorStyles.boldLabel, GUILayout.Width(250));
                GUILayout.Label("Phases", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("Patterns", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("Profiles", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("Issues", EditorStyles.boldLabel, GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();
            }

            private void DrawRow(ArchetypeLibraryRowSummary summary, int index)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
                if (summary.IsActive)
                    EditorGUI.DrawRect(row, new Color(0.18f, 0.27f, 0.38f, 1f));
                else if (index == _selectedIndex)
                    EditorGUI.DrawRect(row, new Color(0.16f, 0.16f, 0.16f, 1f));

                var nameRect = new Rect(row.x + 4f, row.y + 2f, 246f, EditorGUIUtility.singleLineHeight);
                var phaseRect = new Rect(nameRect.xMax, nameRect.y, 70f, nameRect.height);
                var patternRect = new Rect(phaseRect.xMax, nameRect.y, 80f, nameRect.height);
                var profileRect = new Rect(patternRect.xMax, nameRect.y, 80f, nameRect.height);
                var issueRect = new Rect(profileRect.xMax, nameRect.y, 90f, nameRect.height);
                EditorGUI.LabelField(nameRect, summary.Name);
                EditorGUI.LabelField(phaseRect, summary.PhaseCount.ToString());
                EditorGUI.LabelField(patternRect, summary.PatternCount.ToString());
                EditorGUI.LabelField(profileRect, summary.ProfileCount.ToString());
                EditorGUI.LabelField(issueRect, summary.IssueLabel);

                var current = Event.current;
                if (current.type == EventType.MouseDown && row.Contains(current.mousePosition))
                {
                    _selectedIndex = index;
                    _owner.SetActorPrefab(summary.Prefab);
                    editorWindow.Close();
                    current.Use();
                }
            }

            private void HandleKeyboard(List<ArchetypeLibraryRowSummary> filtered)
            {
                var current = Event.current;
                if (current.type != EventType.KeyDown || filtered.Count == 0)
                    return;
                if (current.keyCode == KeyCode.DownArrow)
                {
                    _selectedIndex = Mathf.Min(_selectedIndex + 1, filtered.Count - 1);
                    current.Use();
                }
                else if (current.keyCode == KeyCode.UpArrow)
                {
                    _selectedIndex = Mathf.Max(_selectedIndex - 1, 0);
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                {
                    _owner.SetActorPrefab(filtered[_selectedIndex].Prefab);
                    editorWindow.Close();
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Escape)
                {
                    editorWindow.Close();
                    current.Use();
                }
            }
        }
    }
}
