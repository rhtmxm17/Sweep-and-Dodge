using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StageMapHazardPlacementPreviewVisualState : byte
    {
        NotInPreview = 0,
        WaitingForSpawn = 1,
        Active = 2,
        Retired = 3,
    }

    public sealed class StageMapEditorWindow : EditorWindow
    {
        private readonly StageMapEditingSession _session = new StageMapEditingSession();
        private readonly List<ContentValidationIssue> _issues = new List<ContentValidationIssue>(32);
        private readonly List<StageMapDocumentIssue> _documentIssues = new List<StageMapDocumentIssue>(32);
        private readonly HazardActorPreviewOwnerToken _hazardPreviewOwner = new HazardActorPreviewOwnerToken("Stage Map Editor");
        private Vector2 _documentScroll;
        private Vector2 _rightScroll;
        private Vector2 _issueScroll;
        private Vector2 _diffScroll;
        private Vector2 _encounterScroll;
        private bool _showRawDocument;
        private bool _showProjectDocuments = true;
        private StageMapDocument _document;
        private StageMapApplyPlan _applyPlan;
        private StageMapDocumentMigrationPlan _migrationPlan;
        private StageMapGridResizePlan _gridResizePlan;
        private StageGridSpec _pendingGrid;
        private readonly List<StageMapDocument> _projectDocuments = new List<StageMapDocument>(16);
        private Vector2 _documentListScroll;
        private bool _projectDocumentsDirty = true;
        private StageMapOverlayCache _overlayCache = new StageMapOverlayCache();
        private readonly Vector3[] _selectedCellOutline = new Vector3[5];
        private int _observedDocumentDirtyCount;
        private string _observedDocumentSignature = string.Empty;
        private Vector2Int _navigatorCell;
        private HazardActorPreviewSession _hazardActorPreviewSession;
        private HazardActorEncounterPreviewSession _hazardEncounterPreviewSession;
        private StageMapHazardPreviewBinding _hazardPreviewBinding;
        private string _hazardPreviewError = string.Empty;
        private bool _hazardPreviewReconcileQueued;
        private bool _hazardPreviewReconcileClaimsOwner;
        private GUIStyle _inactiveHazardPreviewLabelStyle;
        private int _draggingEncounterRuleId;
        private uint _draggingEncounterSourceId;

        public StageMapDocument ActiveDocument => _document;
        public StageMapEditingSession Session => _session;
        public StageMapOverlayCache OverlayCache => _overlayCache;
        public StageMapApplyPlan CurrentApplyPlan => _applyPlan;
        public StageMapInspectorSection CurrentInspectorSection => _session.GetInspectorSection();
        public IReadOnlyList<StageMapDocument> ProjectDocuments => _projectDocuments;
        public StageMapHazardPreviewBinding HazardPreviewBinding => _hazardPreviewBinding;
        public bool IsHazardPreviewActive => HazardActorPreviewCoordinator.IsActiveOwner(_hazardPreviewOwner);
        public string HazardPreviewError => _hazardPreviewError;

        public bool ShouldDrawAuthoringGizmoLabelsForTests() => ShouldDrawAuthoringGizmoLabels();

        [MenuItem("Tools/Project/Stage Map Editor/Open")]
        public static void Open()
        {
            GetWindow<StageMapEditorWindow>("Stage Map Editor");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.delayCall -= FlushHazardPreviewReconcile;
            ReleaseHazardPreview(clearBinding: true);
            _overlayCache.Dispose();
            _session.Dispose();
        }

        private void OnGUI()
        {
            SynchronizeExternalDocumentChanges();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var nextDocument = (StageMapDocument)EditorGUILayout.ObjectField(_document, typeof(StageMapDocument), false, GUILayout.MinWidth(240f));
                if (nextDocument != _document)
                    LoadDocument(nextDocument);

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_document == null))
                {
                    if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        Validate();
                    if (GUILayout.Button("Dry Run", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        BuildDryRun();
                    using (new EditorGUI.DisabledScope(_applyPlan == null || _applyPlan.HasErrors))
                    {
                        if (GUILayout.Button("Apply", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                            Apply();
                    }
                }
            }

            if (_document == null)
            {
                EditorGUILayout.HelpBox("Select a StageMapDocument asset.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawDocumentPanel();
                    DrawRightPanel();
                }

                DrawHazardPreviewStrip();
                DrawEncounterTrack();
            }
        }

        public void LoadDocument(StageMapDocument document)
        {
            EditorApplication.delayCall -= FlushHazardPreviewReconcile;
            _hazardPreviewReconcileQueued = false;
            _hazardPreviewReconcileClaimsOwner = false;
            ReleaseHazardPreview(clearBinding: true);
            _document = document;
            _applyPlan = null;
            _migrationPlan = null;
            _gridResizePlan = null;
            _issues.Clear();
            _documentIssues.Clear();
            _session.Load(document);
            _navigatorCell = Vector2Int.zero;
            _pendingGrid = document != null ? document.Grid : default;
            _observedDocumentDirtyCount = document != null ? EditorUtility.GetDirtyCount(document) : 0;
            _observedDocumentSignature = StageMapApplyPlanner.ComputeSignature(document);
            _hazardPreviewError = string.Empty;
            InvalidateOverlayCache();
        }

        private void DrawDocumentPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(360f)))
            {
                EditorGUILayout.LabelField("Document", EditorStyles.boldLabel);
                DrawProjectDocumentList();
                _documentScroll = EditorGUILayout.BeginScrollView(_documentScroll);

                DrawStageHeader();
                DrawDocumentSettings();
                DrawGridSettings();
                DrawMigrationPanel();
                _showRawDocument = EditorGUILayout.Foldout(_showRawDocument, "Raw Document Data", true);
                if (_showRawDocument)
                {
                    var serializedDocument = new SerializedObject(_document);
                    serializedDocument.Update();
                    var iterator = serializedDocument.GetIterator();
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (iterator.propertyPath == "m_Script")
                        {
                            using (new EditorGUI.DisabledScope(true))
                                EditorGUILayout.PropertyField(iterator, true);
                            continue;
                        }

                        EditorGUILayout.PropertyField(iterator, true);
                    }

                    if (serializedDocument.ApplyModifiedProperties())
                    {
                        RefreshAfterDocumentMutation(markDirty: true);
                    }
                }

                EditorGUILayout.Space(8f);
                if (GUILayout.Button("Validate"))
                    Validate();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProjectDocumentList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showProjectDocuments = EditorGUILayout.Foldout(_showProjectDocuments, "Project StageMapDocuments", true);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64f)))
                        RefreshProjectDocuments();
                }

                if (!_showProjectDocuments)
                    return;

                if (_projectDocumentsDirty)
                    RefreshProjectDocuments();

                _documentListScroll = EditorGUILayout.BeginScrollView(_documentListScroll, GUILayout.MinHeight(58f), GUILayout.MaxHeight(120f));
                for (int i = 0; i < _projectDocuments.Count; i++)
                {
                    var candidate = _projectDocuments[i];
                    if (candidate == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string label = $"{candidate.StageId:00}  {StageMapDocumentExporter.BuildCatalogEntryKey(candidate)}";
                        if (GUILayout.Toggle(candidate == _document, label, EditorStyles.miniButtonLeft))
                        {
                            if (candidate != _document)
                                LoadDocument(candidate);
                        }

                        EditorGUILayout.LabelField(candidate.DisplayName, EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        public void RefreshProjectDocuments()
        {
            _projectDocuments.Clear();
            string[] guids = AssetDatabase.FindAssets("t:StageMapDocument");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var document = AssetDatabase.LoadAssetAtPath<StageMapDocument>(path);
                if (document != null)
                    _projectDocuments.Add(document);
            }

            _projectDocuments.Sort((a, b) =>
            {
                int stageOrder = a.StageId.CompareTo(b.StageId);
                if (stageOrder != 0)
                    return stageOrder;
                return string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
            });
            _projectDocumentsDirty = false;
        }

        private void DrawStageHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Stage {Mathf.Max(1, _document.StageId)}", EditorStyles.boldLabel);
                string displayName = string.IsNullOrWhiteSpace(_document.DisplayName) ? "(unnamed)" : _document.DisplayName;
                EditorGUILayout.LabelField(displayName);
                EditorGUILayout.LabelField($"Grid {_document.Grid.Width} x {_document.Grid.Height}, cell {_document.Grid.CellSize:0.###}");
                EditorGUILayout.LabelField(_session.Dirty ? "Dirty" : "Clean");
            }
        }

        private void DrawDocumentSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Stage & Targets", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                int stageId = EditorGUILayout.IntField("Stage Id", _document.StageId);
                string displayName = EditorGUILayout.TextField("Display Name", _document.DisplayName);
                bool isFinalStage = EditorGUILayout.Toggle("Final Stage", _document.IsFinalStage);
                float timeLimit = EditorGUILayout.FloatField("Time Limit Sec", _document.StageTimeLimitSec);
                var targetLayout = (StageLayoutSO)EditorGUILayout.ObjectField("Target Layout", _document.TargetLayout, typeof(StageLayoutSO), false);
                var targetDefinition = (StageDefinitionSO)EditorGUILayout.ObjectField("Target Definition", _document.TargetDefinition, typeof(StageDefinitionSO), false);
                var targetCatalog = (StageCatalogSO)EditorGUILayout.ObjectField("Target Catalog", _document.TargetCatalog, typeof(StageCatalogSO), false);
                var presentationCatalog = (StagePresentationCatalogSO)EditorGUILayout.ObjectField("Presentation Catalog", _document.PresentationCatalog, typeof(StagePresentationCatalogSO), false);
                bool includeInCatalog = EditorGUILayout.Toggle("Include In Catalog", _document.IncludeInCatalog);
                bool enabledInCatalog = EditorGUILayout.Toggle("Enabled In Catalog", _document.EnabledInCatalog);
                string entryKey = EditorGUILayout.TextField("Catalog Entry Key", _document.CatalogEntryKey);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Last Applied Entry", _document.LastAppliedCatalogEntryKey);

                if (EditorGUI.EndChangeCheck())
                {
                    RecordAndApply(
                        "Edit Stage Map Metadata And Targets",
                        () =>
                        {
                            _document.StageId = Mathf.Max(1, stageId);
                            _document.DisplayName = displayName;
                            _document.IsFinalStage = isFinalStage;
                            _document.StageTimeLimitSec = Mathf.Max(0.01f, timeLimit);
                            _document.TargetLayout = targetLayout;
                            _document.TargetDefinition = targetDefinition;
                            _document.TargetCatalog = targetCatalog;
                            _document.PresentationCatalog = presentationCatalog;
                            _document.IncludeInCatalog = includeInCatalog;
                            _document.EnabledInCatalog = enabledInCatalog;
                            _document.CatalogEntryKey = entryKey != null ? entryKey.Trim() : string.Empty;
                            return true;
                        });
                }
            }
        }

        private void DrawGridSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Grid Resize", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.Grid)))
                {
                    _pendingGrid.Width = EditorGUILayout.IntField("Width", _pendingGrid.Width);
                    _pendingGrid.Height = EditorGUILayout.IntField("Height", _pendingGrid.Height);
                    _pendingGrid.CellSize = EditorGUILayout.FloatField("Cell Size", _pendingGrid.CellSize);
                    _pendingGrid.Origin = EditorGUILayout.Vector3Field("Origin", _pendingGrid.Origin);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Reset"))
                        {
                            _pendingGrid = _document.Grid;
                            _gridResizePlan = null;
                        }

                        if (GUILayout.Button("Preview Resize"))
                            _gridResizePlan = StageMapGridResizeUtility.BuildPreview(_document, _pendingGrid);

                        using (new EditorGUI.DisabledScope(_gridResizePlan == null || _gridResizePlan.HasErrors))
                        {
                            if (GUILayout.Button("Apply Resize"))
                                ApplyGridResize();
                        }
                    }
                }

                if (_gridResizePlan != null)
                {
                    EditorGUILayout.LabelField(
                        $"Changes {_gridResizePlan.Changes.Count}, cropped cells {_gridResizePlan.CroppedNonDefaultCellCount}, visual keys {_gridResizePlan.CroppedVisualKeyCount}",
                        EditorStyles.wordWrappedMiniLabel);
                    for (int i = 0; i < _gridResizePlan.Issues.Count; i++)
                        EditorGUILayout.HelpBox(_gridResizePlan.Issues[i].Message, MessageType.Error);
                }
            }
        }

        private void DrawMigrationPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Schema v{_document.SchemaVersion} -> v{StageMapDocument.CurrentSchemaVersion}", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview Migration"))
                        _migrationPlan = StageMapDocumentMigrationUtility.BuildPreview(_document);
                    using (new EditorGUI.DisabledScope(_migrationPlan == null || _migrationPlan.HasErrors || !_migrationPlan.HasChanges))
                    {
                        if (GUILayout.Button("Apply Migration"))
                            ApplyMigration();
                    }
                }

                if (_migrationPlan != null)
                {
                    EditorGUILayout.LabelField($"Migration Changes ({_migrationPlan.Changes.Count})", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < _migrationPlan.Issues.Count; i++)
                        EditorGUILayout.HelpBox(_migrationPlan.Issues[i].Message, MessageType.Error);
                }
            }
        }

        private void ApplyGridResize()
        {
            bool confirmed = !_gridResizePlan.RequiresConfirmation
                || EditorUtility.DisplayDialog(
                    "Resize Stage Map Grid",
                    $"This resize removes {_gridResizePlan.CroppedNonDefaultCellCount} non-default cells and {_gridResizePlan.CroppedVisualKeyCount} visual keys.",
                    "Apply",
                    "Cancel");
            if (!confirmed)
                return;

            if (!StageMapGridResizeUtility.TryApply(_gridResizePlan, true, out string error))
            {
                EditorUtility.DisplayDialog("Resize Stage Map Grid", error, "OK");
                _gridResizePlan = StageMapGridResizeUtility.BuildPreview(_document, _pendingGrid);
                return;
            }

            _pendingGrid = _document.Grid;
            RefreshAfterDocumentMutation(markDirty: true);
        }

        private void ApplyMigration()
        {
            if (!StageMapDocumentMigrationUtility.TryApply(_migrationPlan, false, out string error))
            {
                EditorUtility.DisplayDialog("Migrate Stage Map Document", error, "OK");
                _migrationPlan = StageMapDocumentMigrationUtility.BuildPreview(_document);
                return;
            }

            RefreshAfterDocumentMutation(markDirty: true);
        }

        private void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(320f), GUILayout.MaxWidth(520f)))
            {
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                DrawSessionToolbar();
                DrawSelectionNavigator();
                DrawContextualInspector();
                DrawIssues();
                DrawDiff();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSessionToolbar()
        {
            EditorGUILayout.LabelField("Editing Session", EditorStyles.boldLabel);
            _session.SelectedTool = (StageMapEditorToolMode)EditorGUILayout.EnumPopup("Tool", _session.SelectedTool);
            _session.SelectedLayer = (StageMapEditorLayer)EditorGUILayout.EnumPopup("Active Layer", _session.SelectedLayer);
            DrawLayerVisibility();
            DrawLayerLocks();
            DrawPalette();
        }

        private void DrawLayerVisibility()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Visibility", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _session.ShowGridLayer = GUILayout.Toggle(_session.ShowGridLayer, "Grid", EditorStyles.miniButton);
                _session.ShowMovementLayer = GUILayout.Toggle(_session.ShowMovementLayer, "Movement", EditorStyles.miniButton);
                _session.ShowSourceLayer = GUILayout.Toggle(_session.ShowSourceLayer, "Source", EditorStyles.miniButton);
                _session.ShowDepositLayer = GUILayout.Toggle(_session.ShowDepositLayer, "Deposit", EditorStyles.miniButton);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _session.ShowAnchorLayer = GUILayout.Toggle(_session.ShowAnchorLayer, "Anchors", EditorStyles.miniButton);
                _session.ShowPlayerStartLayer = GUILayout.Toggle(_session.ShowPlayerStartLayer, "Player", EditorStyles.miniButton);
                _session.ShowHazardActorLayer = GUILayout.Toggle(_session.ShowHazardActorLayer, "Hazards", EditorStyles.miniButton);
                _session.ShowPresentationLayer = GUILayout.Toggle(_session.ShowPresentationLayer, "Links", EditorStyles.miniButton);
            }
        }

        private void DrawLayerLocks()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Locks", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _session.LockGridLayer = GUILayout.Toggle(_session.LockGridLayer, "Grid", EditorStyles.miniButton);
                _session.LockMovementLayer = GUILayout.Toggle(_session.LockMovementLayer, "Movement", EditorStyles.miniButton);
                _session.LockSourceLayer = GUILayout.Toggle(_session.LockSourceLayer, "Source", EditorStyles.miniButton);
                _session.LockDepositLayer = GUILayout.Toggle(_session.LockDepositLayer, "Deposit", EditorStyles.miniButton);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _session.LockAnchorLayer = GUILayout.Toggle(_session.LockAnchorLayer, "Anchors", EditorStyles.miniButton);
                _session.LockPlayerStartLayer = GUILayout.Toggle(_session.LockPlayerStartLayer, "Player", EditorStyles.miniButton);
                _session.LockHazardActorLayer = GUILayout.Toggle(_session.LockHazardActorLayer, "Hazards", EditorStyles.miniButton);
                _session.LockPresentationLayer = GUILayout.Toggle(_session.LockPresentationLayer, "Links", EditorStyles.miniButton);
            }
        }

        private void DrawPalette()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            switch (_session.SelectedTool)
            {
                case StageMapEditorToolMode.PaintMovement:
                    _session.MovementBrush = (StageCellMovementFlags)EditorGUILayout.EnumFlagsField("Movement Flags", _session.MovementBrush);
                    break;
                case StageMapEditorToolMode.PaintRegion:
                    _session.RegionBrushKind = (StageRegionKind)EditorGUILayout.EnumPopup("Region Kind", _session.RegionBrushKind);
                    _session.RegionBrushStableId = DrawUIntField("Stable Id", _session.RegionBrushStableId);
                    break;
                case StageMapEditorToolMode.PlaceAnchor:
                    _session.AnchorBrushKind = (StageRegionKind)EditorGUILayout.EnumPopup("Region Kind", _session.AnchorBrushKind);
                    _session.AnchorBrushStableId = DrawUIntField("Stable Id", _session.AnchorBrushStableId);
                    DrawCenterRegionAnchorPreference();
                    break;
                case StageMapEditorToolMode.PlacePlayerStart:
                    _session.PlayerStartYawDeg = EditorGUILayout.FloatField("Yaw Deg", _session.PlayerStartYawDeg);
                    DrawCenterPlayerStartPreference();
                    break;
                case StageMapEditorToolMode.PlaceHazardActor:
                    uint sourceId = ResolveEncounterSourceId();
                    _session.HazardActorSourceStableId = DrawUIntField("Source Stable Id", sourceId);
                    DrawHazardActorArchetypePickerButton();
                    _session.HazardActorLocalYawDeg = EditorGUILayout.FloatField("Local Yaw Deg", _session.HazardActorLocalYawDeg);
                    break;
                case StageMapEditorToolMode.PlacePresentationLink:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _session.PresentationStableId = DrawUIntField("Stable Id", _session.PresentationStableId);
                        if (GUILayout.Button("Next", GUILayout.Width(48f)))
                            _session.PresentationStableId = StageMapDocumentCommandUtility.GetNextPresentationStableId(_document);
                    }

                    _session.PresentationKey = EditorGUILayout.TextField("Presentation Key", _session.PresentationKey);
                    _session.PresentationPlacementMode = (StagePresentationPlacementMode)EditorGUILayout.EnumPopup("Placement Mode", _session.PresentationPlacementMode);
                    if (_session.PresentationPlacementMode == StagePresentationPlacementMode.LinkedToParent)
                    {
                        _session.PresentationLinkKind = (StagePresentationLinkKind)EditorGUILayout.EnumPopup("Link Kind", _session.PresentationLinkKind);
                        _session.PresentationLinkedStableId = DrawUIntField("Linked Stable Id", _session.PresentationLinkedStableId);
                    }

                    _session.PresentationEuler = EditorGUILayout.Vector3Field("Euler", _session.PresentationEuler);
                    _session.PresentationScale = EditorGUILayout.Vector3Field("Scale", _session.PresentationScale == Vector3.zero ? Vector3.one : _session.PresentationScale);
                    break;
            }
        }

        private void DrawHazardActorArchetypePickerButton()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string label = _session.HazardActorArchetypePrefab != null
                    ? _session.HazardActorArchetypePrefab.name
                    : "(none)";
                EditorGUILayout.LabelField("Actor Archetype", label, EditorStyles.miniLabel);
                Rect buttonRect = GUILayoutUtility.GetRect(new GUIContent("Change"), EditorStyles.miniButton, GUILayout.Width(72f));
                if (GUI.Button(buttonRect, "Change", EditorStyles.miniButton))
                {
                    PopupWindow.Show(
                        buttonRect,
                        new HazardArchetypePickerPopup(
                            _session.HazardActorArchetypePrefab,
                            selected =>
                            {
                                _session.HazardActorArchetypePrefab = selected;
                                Repaint();
                            }));
                }
            }

            if (_session.HazardActorArchetypePrefab == null)
                EditorGUILayout.HelpBox("Choose a HazardActor archetype before placing in Scene View.", MessageType.Info);
        }

        internal static StageMapHazardArchetypeSummary[] BuildHazardArchetypeSummaries()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var result = new List<StageMapHazardArchetypeSummary>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var actor = prefab != null ? prefab.GetComponentInChildren<HazardActorAuthoring>(true) : null;
                if (actor == null)
                    continue;

                result.Add(new StageMapHazardArchetypeSummary(
                    prefab,
                    prefab.name,
                    actor.PhaseSelectorPolicies?.Length ?? 0,
                    actor.PatternSlots?.Length ?? 0,
                    HazardActorAuthoringValidationUtility.TryValidateStandalone(actor, out _, out _, out _, out _) ? 0 : 1));
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result.ToArray();
        }

        private void DrawCenterRegionAnchorPreference()
        {
            bool next = EditorGUILayout.Toggle("Lock Offset To Cell Center", _session.CenterRegionAnchors);
            if (next == _session.CenterRegionAnchors)
                return;
            TrySetCenterRegionAnchors(next);
        }

        private void DrawCenterPlayerStartPreference()
        {
            bool next = EditorGUILayout.Toggle("Lock Offset To Cell Center", _session.CenterPlayerStart);
            if (next == _session.CenterPlayerStart)
                return;
            TrySetCenterPlayerStart(next);
        }

        public bool TrySetCenterRegionAnchors(bool enabled)
        {
            return RecordSessionAndDocumentAndApply(
                "Set Region Anchor Center Lock",
                () => StageMapEditorMutationUtility.TrySetCenterRegionAnchors(_session, enabled, out _));
        }

        public bool TrySetCenterPlayerStart(bool enabled)
        {
            return RecordSessionAndDocumentAndApply(
                "Set PlayerStart Center Lock",
                () => StageMapEditorMutationUtility.TrySetCenterPlayerStart(_session, enabled, out _));
        }

        private void DrawSelectionNavigator()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Selection Navigator", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    StageMapSelectionUtility.GetSelectionSummary(_document, _session.Selection),
                    EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_session.Selection.Kind == StageMapSelectionKind.None))
                    {
                        if (GUILayout.Button("Frame"))
                            FrameSelection(_session.Selection);
                        if (GUILayout.Button("Clear Selection"))
                            _session.Select(StageMapSelection.None);
                    }
                }

                EditorGUILayout.Space(3f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _navigatorCell = EditorGUILayout.Vector2IntField("Cell", _navigatorCell);
                    if (GUILayout.Button("Select", GUILayout.Width(56f)))
                        TrySelect(StageMapSelection.ForCell(_navigatorCell), frame: false);
                }

                DrawRegionNavigator(_document.SourceRegions, StageRegionKind.Source);
                DrawRegionNavigator(_document.DepositRegions, StageRegionKind.Deposit);

                if (_document.PlayerStart.Active && GUILayout.Button("PlayerStart", EditorStyles.miniButton))
                    TrySelect(StageMapSelection.ForPlayerStart(), frame: false);

                EditorGUILayout.LabelField("Hazard Actors", EditorStyles.miniBoldLabel);
                var placements = _document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
                for (int i = 0; i < placements.Length; i++)
                {
                    var placement = placements[i];
                    string label = $"Source {placement.OwningSourceStableId} / Placement {placement.PlacementInstanceId}";
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        TrySelect(
                            StageMapSelection.ForHazard(placement.OwningSourceStableId, placement.PlacementInstanceId),
                            frame: false);
                    }
                }

                EditorGUILayout.LabelField("Hazard Rules", EditorStyles.miniBoldLabel);
                var rules = _document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
                for (int i = 0; i < rules.Length; i++)
                {
                    var rule = rules[i];
                    string label = $"Source {rule.OwningSourceStableId} / Rule {rule.RuleId} / {rule.ActionType}";
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                        TrySelect(StageMapSelection.ForHazardRule(rule.OwningSourceStableId, rule.RuleId), frame: false);
                }

                EditorGUILayout.LabelField("Presentation Links", EditorStyles.miniBoldLabel);
                var links = _document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
                for (int i = 0; i < links.Length; i++)
                {
                    string label = $"{links[i].StableId} / {links[i].PresentationKey}";
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                        TrySelect(StageMapSelection.ForPresentation(links[i].StableId), frame: false);
                }
            }
        }

        private void DrawRegionNavigator(StageMapRegionData[] regions, StageRegionKind kind)
        {
            EditorGUILayout.LabelField($"{kind} Regions / Anchors", EditorStyles.miniBoldLabel);
            regions = regions ?? Array.Empty<StageMapRegionData>();
            for (int i = 0; i < regions.Length; i++)
            {
                uint stableId = regions[i].StableId;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{kind} {stableId}", EditorStyles.miniLabel);
                    if (GUILayout.Button("Region", EditorStyles.miniButton, GUILayout.Width(56f)))
                        TrySelect(StageMapSelection.ForRegion(kind, stableId), frame: false);
                    if (GUILayout.Button("Anchor", EditorStyles.miniButton, GUILayout.Width(56f)))
                        TrySelect(StageMapSelection.ForAnchor(kind, stableId), frame: false);
                }
            }
        }

        public bool TrySelect(StageMapSelection selection, bool frame)
        {
            _session.Select(selection);
            _session.ReconcileSelection(_document);
            if (_session.Selection.Kind == StageMapSelectionKind.None)
                return false;
            SyncEncounterSourceFromSelection(_session.Selection);
            PrepareHazardPreviewForSelection(play: false);
            if (frame)
                FrameSelection(_session.Selection);
            Repaint();
            return true;
        }

        private void FrameSelection(StageMapSelection selection)
        {
            if (selection.Kind == StageMapSelectionKind.TargetAsset)
            {
                if (selection.TargetAsset != null)
                    EditorGUIUtility.PingObject(selection.TargetAsset);
                return;
            }

            if (!StageMapSelectionUtility.TryGetSelectionWorld(_document, selection, out Vector3 world))
                return;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                float size = Mathf.Max(1f, _document.Grid.CellSize * 2f);
                sceneView.Frame(new Bounds(world, Vector3.one * size), false);
            }
            SceneView.RepaintAll();
        }

        private void DrawContextualInspector()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Contextual Inspector", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                switch (_session.GetInspectorSection())
                {
                    case StageMapInspectorSection.Cell:
                        DrawSelectedCellInspector();
                        break;
                    case StageMapInspectorSection.RegionOrAnchor:
                        DrawSelectedRegionInspector();
                        break;
                    case StageMapInspectorSection.PlayerStart:
                        DrawPlayerStartInspector();
                        break;
                    case StageMapInspectorSection.HazardActor:
                        DrawHazardActorInspector();
                        break;
                    case StageMapInspectorSection.HazardActorRule:
                        DrawHazardActorRuleInspector();
                        break;
                    case StageMapInspectorSection.Presentation:
                        DrawPresentationLinkInspector();
                        break;
                    case StageMapInspectorSection.Document:
                        EditorGUILayout.ObjectField("Document", _document, typeof(StageMapDocument), false);
                        break;
                    case StageMapInspectorSection.TargetAsset:
                        EditorGUILayout.ObjectField("Target Asset", _session.Selection.TargetAsset, typeof(UnityEngine.Object), false);
                        break;
                    default:
                        EditorGUILayout.HelpBox("No selection. Choose a target in Selection Navigator or Scene View.", MessageType.Info);
                        break;
                }
            }
        }

        private void DrawSelectedCellInspector()
        {
            Vector2Int cell = _session.Selection.Cell;
            if (!StageMapDocumentCommandUtility.TryGetCellIndex(_document, cell, out int index)
                || _document.Cells == null
                || index >= _document.Cells.Length)
            {
                EditorGUILayout.HelpBox("Selected cell is outside the current grid.", MessageType.Warning);
                return;
            }

            var data = _document.Cells[index];
            EditorGUILayout.LabelField("Cell", $"({cell.x}, {cell.y})");
            EditorGUI.BeginChangeCheck();
            StageCellMovementFlags movementFlags;
            uint sourceId;
            uint depositId;
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.Movement)))
                movementFlags = (StageCellMovementFlags)EditorGUILayout.EnumFlagsField("Movement", data.MovementFlags);
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.Source)))
                sourceId = DrawUIntField("Source Id", data.SourceRegionId);
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.Deposit)))
                depositId = DrawUIntField("Deposit Id", data.DepositRegionId);
            if (EditorGUI.EndChangeCheck())
            {
                RecordAndApply(
                    "Edit Stage Map Cell",
                    () =>
                    {
                        bool changed = false;
                        if (movementFlags != data.MovementFlags)
                            changed |= StageMapEditorMutationUtility.TrySetCellMovement(_session, cell, movementFlags, out _);
                        if (sourceId != data.SourceRegionId)
                            changed |= StageMapEditorMutationUtility.TrySetCellRegion(_session, cell, StageRegionKind.Source, sourceId, out _);
                        if (depositId != data.DepositRegionId)
                            changed |= StageMapEditorMutationUtility.TrySetCellRegion(_session, cell, StageRegionKind.Deposit, depositId, out _);
                        return changed;
                    });
            }
        }

        private void DrawSelectedRegionInspector()
        {
            if (_session.Selection.Kind == StageMapSelectionKind.SourceAnchor
                || _session.Selection.Kind == StageMapSelectionKind.SourceRegion)
            {
                DrawRegionInspector(StageRegionKind.Source, _session.Selection.StableId);
                return;
            }

            if (_session.Selection.Kind == StageMapSelectionKind.DepositAnchor
                || _session.Selection.Kind == StageMapSelectionKind.DepositRegion)
            {
                DrawRegionInspector(StageRegionKind.Deposit, _session.Selection.StableId);
                return;
            }

            EditorGUILayout.HelpBox("The selected target is not a region or anchor.", MessageType.Warning);
        }

        private void DrawRegionInspector(StageRegionKind kind, uint stableId)
        {
            if (stableId == 0u)
                return;

            var regions = kind == StageRegionKind.Source
                ? _document.SourceRegions ?? Array.Empty<StageMapRegionData>()
                : _document.DepositRegions ?? Array.Empty<StageMapRegionData>();
            int index = FindRegionIndex(regions, stableId);
            if (index < 0)
            {
                EditorGUILayout.HelpBox($"{kind} region {stableId} is referenced by the selected cell but no region record exists.", MessageType.Warning);
                return;
            }

            var region = regions[index];
            EditorGUILayout.Space(4f);
            string targetLabel = _session.Selection.Kind == StageMapSelectionKind.SourceAnchor
                || _session.Selection.Kind == StageMapSelectionKind.DepositAnchor
                ? "Anchor"
                : "Region";
            EditorGUILayout.LabelField($"{kind} {targetLabel} {stableId}", EditorStyles.miniBoldLabel);
            DrawCenterRegionAnchorPreference();
            bool canMutate = StageMapEditingPolicy.CanMutateAnchor(_session, kind, out _);
            using (new EditorGUI.DisabledScope(!canMutate))
            {
                EditorGUI.BeginChangeCheck();
                bool active = EditorGUILayout.Toggle("Active", region.Active);
                Vector2Int anchorCell = EditorGUILayout.Vector2IntField("Anchor Cell", region.AnchorCell);
                Vector2 anchorOffset;
                using (new EditorGUI.DisabledScope(_session.CenterRegionAnchors))
                {
                    anchorOffset = EditorGUILayout.Vector2Field(
                        "Anchor Offset",
                        _session.CenterRegionAnchors ? Vector2.zero : region.AnchorOffset);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    RecordAndApply(
                        $"Edit Stage {kind} Region",
                        () => StageMapEditorMutationUtility.TryUpdateRegion(
                            _session,
                            kind,
                            stableId,
                            new StageMapRegionData
                            {
                                StableId = stableId,
                                Active = active,
                                AnchorCell = anchorCell,
                                AnchorOffset = anchorOffset,
                            },
                            out _));
                }
            }
        }

        private void DrawPlayerStartInspector()
        {
            EditorGUILayout.LabelField("PlayerStart", EditorStyles.miniBoldLabel);
            DrawCenterPlayerStartPreference();
            var player = _document.PlayerStart;
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.PlayerStart)))
            {
                EditorGUI.BeginChangeCheck();
                bool active = EditorGUILayout.Toggle("Active", player.Active);
                Vector2Int anchorCell = EditorGUILayout.Vector2IntField("Anchor Cell", player.AnchorCell);
                Vector2 anchorOffset;
                using (new EditorGUI.DisabledScope(_session.CenterPlayerStart))
                {
                    anchorOffset = EditorGUILayout.Vector2Field(
                        "Anchor Offset",
                        _session.CenterPlayerStart ? Vector2.zero : player.AnchorOffset);
                }
                float yawDeg = EditorGUILayout.FloatField("Yaw Deg", player.YawDeg);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordAndApply(
                        "Edit Stage PlayerStart",
                        () => StageMapEditorMutationUtility.TryUpdatePlayerStart(
                            _session,
                            new StagePlayerStartLayoutData
                            {
                                Active = active,
                                AnchorCell = anchorCell,
                                AnchorOffset = anchorOffset,
                                YawDeg = yawDeg,
                            },
                            out _));
                }
            }
        }

        private void DrawHazardActorInspector()
        {
            EditorGUILayout.LabelField("Hazard Actor", EditorStyles.miniBoldLabel);
            var placements = _document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            StageMapSelection identity = _session.Selection;
            if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    placements,
                    identity.OwningSourceStableId,
                    identity.PlacementInstanceId,
                    out int index))
            {
                EditorGUILayout.HelpBox("Selected HazardActor identity is missing or ambiguous.", MessageType.Warning);
                return;
            }

            var placement = placements[index];
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.HazardActors)))
            {
                EditorGUI.BeginChangeCheck();
                int placementId = EditorGUILayout.IntField("Placement Id", placement.PlacementInstanceId);
                uint sourceId = DrawUIntField("Source Id", placement.OwningSourceStableId);
                var prefab = (GameObject)EditorGUILayout.ObjectField("Actor Prefab", placement.ActorArchetypePrefab, typeof(GameObject), false);
                Vector3 offset = EditorGUILayout.Vector3Field("Source Offset", placement.SourceLocalOffset);
                float yaw = EditorGUILayout.FloatField("Yaw Deg", placement.LocalYawDeg);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordAndApply(
                        "Edit Stage Hazard Actor",
                        () => StageMapEditorMutationUtility.TryUpdateHazard(
                            _session,
                            identity,
                            new StageMapHazardActorPlacementData
                            {
                                PlacementInstanceId = placementId,
                                OwningSourceStableId = sourceId,
                                ActorArchetypePrefab = prefab,
                                SourceLocalOffset = offset,
                                LocalYawDeg = yaw,
                            },
                            out _));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(placement.ActorArchetypePrefab == null))
                    {
                        if (GUILayout.Button("Open in Workbench"))
                            HazardActorWorkbenchWindow.Open(placement.ActorArchetypePrefab);
                        if (GUILayout.Button("Actor Preview"))
                            PrepareHazardPreviewForSelection(play: true);
                    }
                    if (GUILayout.Button(new GUIContent("X", "Delete Hazard Actor"), GUILayout.Width(32f)))
                        RecordAndApply("Delete Stage Hazard Actor", () => StageMapEditorMutationUtility.TryDeleteSelection(_session, out _));
                }
            }
        }

        private void DrawHazardActorRuleInspector()
        {
            EditorGUILayout.LabelField("Hazard Actor Rule", EditorStyles.miniBoldLabel);
            StageMapSelection identity = _session.Selection;
            if (!StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(
                    _document,
                    identity.OwningSourceStableId,
                    identity.RuleId,
                    out int index))
            {
                EditorGUILayout.HelpBox("Selected HazardActor rule identity is missing or ambiguous.", MessageType.Warning);
                return;
            }

            var rule = _document.HazardActorOrchestrationRules[index];
            int[] commonPhaseIds = StageMapHazardActorOrchestrationUtility.GetCommonPhaseIds(_document, rule);
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.HazardActors)))
            {
                EditorGUI.BeginChangeCheck();
                uint sourceId = DrawUIntField("Source Id", rule.OwningSourceStableId);
                int ruleId = EditorGUILayout.IntField("Rule Id", rule.RuleId);
                string targetCsv = EditorGUILayout.TextField("Target Placement Ids", JoinInts(rule.TargetPlacementInstanceIds));
                var action = (HazardActorOrchestrationActionId)EditorGUILayout.EnumPopup("Action", rule.ActionType);
                var trigger = (HazardActorOrchestrationTriggerId)EditorGUILayout.EnumPopup("Trigger", rule.TriggerType);
                float threshold = EditorGUILayout.Slider("Progress", rule.TriggerThresholdNormalized, 0f, 1f);
                int targetPhaseId = DrawPhaseIdField(rule.TargetPhaseId, commonPhaseIds);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordAndApply(
                        "Edit Stage Hazard Actor Rule",
                        () => StageMapEditorMutationUtility.TryUpdateHazardRule(
                            _session,
                            identity,
                            new StageMapHazardActorOrchestrationRuleData
                            {
                                OwningSourceStableId = sourceId,
                                RuleId = Mathf.Max(1, ruleId),
                                TargetPlacementInstanceIds = ParseInts(targetCsv),
                                ActionType = action,
                                TriggerType = trigger,
                                TriggerThresholdNormalized = Mathf.Clamp01(threshold),
                                TargetPhaseId = Mathf.Max(1, targetPhaseId),
                            },
                            out _));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("C", "Duplicate Rule"), GUILayout.Width(32f)))
                    {
                        RecordAndApply(
                            "Duplicate Stage Hazard Actor Rule",
                            () =>
                            {
                                bool changed = StageMapHazardActorOrchestrationUtility.DuplicateRule(_document, rule.OwningSourceStableId, rule.RuleId, out int newRuleId);
                                if (changed)
                                    _session.Select(StageMapSelection.ForHazardRule(rule.OwningSourceStableId, newRuleId));
                                return changed;
                            });
                    }
                    if (GUILayout.Button(new GUIContent("X", "Delete Rule"), GUILayout.Width(32f)))
                        RecordAndApply("Delete Stage Hazard Actor Rule", () => StageMapEditorMutationUtility.TryDeleteSelection(_session, out _));
                }
            }
        }

        private void DrawPresentationLinkInspector()
        {
            EditorGUILayout.LabelField("Presentation Link", EditorStyles.miniBoldLabel);
            var links = _document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
            StageMapSelection identity = _session.Selection;
            if (!StageMapSelectionUtility.TryFindUniquePresentationIndex(links, identity.StableId, out int index))
            {
                EditorGUILayout.HelpBox("Selected Presentation identity is missing or ambiguous.", MessageType.Warning);
                return;
            }

            var link = links[index];
            using (new EditorGUI.DisabledScope(IsLayerLocked(StageMapEditorLayer.Presentations)))
            {
                EditorGUI.BeginChangeCheck();
                uint stableId = DrawUIntField("Stable Id", link.StableId);
                bool active = EditorGUILayout.Toggle("Active", link.Active);
                string key = EditorGUILayout.TextField("Presentation Key", link.PresentationKey);
                var placementMode = (StagePresentationPlacementMode)EditorGUILayout.EnumPopup("Placement Mode", link.PlacementMode);
                var linkKind = (StagePresentationLinkKind)EditorGUILayout.EnumPopup("Link Kind", link.LinkKind);
                uint linkedStableId = DrawUIntField("Linked Stable Id", link.LinkedStableId);
                Vector3 position = EditorGUILayout.Vector3Field("Position", link.Position);
                Vector3 euler = EditorGUILayout.Vector3Field("Euler", link.Euler);
                Vector3 scale = EditorGUILayout.Vector3Field("Scale", link.Scale == Vector3.zero ? Vector3.one : link.Scale);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordAndApply(
                        "Edit Stage Presentation Link",
                        () =>
                        {
                            bool linked = placementMode == StagePresentationPlacementMode.LinkedToParent;
                            return StageMapEditorMutationUtility.TryUpdatePresentation(
                                _session,
                                identity,
                                new StageMapPresentationLinkData
                                {
                                    StableId = stableId,
                                    Active = active,
                                    PresentationKey = key != null ? key.Trim() : string.Empty,
                                    PlacementMode = placementMode,
                                    LinkKind = linked ? linkKind : StagePresentationLinkKind.None,
                                    LinkedStableId = linked ? linkedStableId : 0u,
                                    Position = position,
                                    Euler = euler,
                                    Scale = scale == Vector3.zero ? Vector3.one : scale,
                                },
                                out _);
                        });
                }

                if (GUILayout.Button("Delete Presentation Link"))
                    RecordAndApply("Delete Stage Presentation Link", () => StageMapEditorMutationUtility.TryDeleteSelection(_session, out _));
            }
        }

        private void DrawHazardPreviewStrip()
        {
            if (!_hazardPreviewBinding.IsValid)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool active = HazardActorPreviewCoordinator.IsActiveOwner(_hazardPreviewOwner);
                bool prepared = _hazardActorPreviewSession != null || _hazardEncounterPreviewSession != null;
                bool playing = _hazardActorPreviewSession != null
                    ? _hazardActorPreviewSession.Playing
                    : _hazardEncounterPreviewSession != null && _hazardEncounterPreviewSession.Playing;
                string scope = _hazardPreviewBinding.Kind == StageMapHazardPreviewBindingKind.Actor
                    ? $"Actor #{_hazardPreviewBinding.PlacementInstanceId}"
                    : $"Encounter Source {_hazardPreviewBinding.SourceStableId}";
                string status = !prepared ? "Invalid" : active ? (playing ? "Playing" : "Paused") : "Inactive";

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Hazard Preview", EditorStyles.boldLabel, GUILayout.Width(110f));
                    EditorGUILayout.LabelField(scope, EditorStyles.miniBoldLabel, GUILayout.Width(170f));
                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel, GUILayout.Width(64f));
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!prepared))
                    {
                        if (GUILayout.Button(playing && active ? "Pause" : "Play", EditorStyles.miniButtonLeft, GUILayout.Width(54f))
                            && ClaimPreparedHazardPreview())
                        {
                            if ((_hazardActorPreviewSession != null && _hazardActorPreviewSession.Playing)
                                || (_hazardEncounterPreviewSession != null && _hazardEncounterPreviewSession.Playing))
                            {
                                HazardActorPreviewCoordinator.Pause(_hazardPreviewOwner);
                            }
                            else
                            {
                                HazardActorPreviewCoordinator.Play(_hazardPreviewOwner);
                            }
                        }
                        if (GUILayout.Button("Step", EditorStyles.miniButtonMid, GUILayout.Width(46f))
                            && ClaimPreparedHazardPreview())
                        {
                            HazardActorPreviewCoordinator.Step(_hazardPreviewOwner);
                        }
                        if (GUILayout.Button("Restart", EditorStyles.miniButtonRight, GUILayout.Width(62f))
                            && ClaimPreparedHazardPreview())
                        {
                            HazardActorPreviewCoordinator.Restart(_hazardPreviewOwner);
                        }
                    }
                }

                EditorGUI.BeginChangeCheck();
                float progress = EditorGUILayout.Slider(
                    "Source Progress",
                    _session.HazardPreviewSourceProgress01,
                    0f,
                    1f);
                if (EditorGUI.EndChangeCheck())
                {
                    _session.HazardPreviewSourceProgress01 = Mathf.Clamp01(progress);
                    ClaimPreparedHazardPreview();
                    _hazardActorPreviewSession?.SetSourceProgress(_session.HazardPreviewSourceProgress01);
                    _hazardEncounterPreviewSession?.SetSourceProgress(_session.HazardPreviewSourceProgress01);
                    SceneView.RepaintAll();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    string mode = _session.HazardPreviewTargetMode == StageMapHazardPreviewTargetMode.FollowPlayerStart
                        ? "Follow PlayerStart"
                        : "Custom";
                    Vector3 target = _session.HazardPreviewTargetWorldPosition;
                    EditorGUILayout.LabelField(
                        $"Target: {mode} ({target.x:0.##}, {target.z:0.##})",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("Reset Target", EditorStyles.miniButton, GUILayout.Width(92f)))
                    {
                        SetHazardPreviewTarget(
                            GetCurrentHazardPreviewPlayerTarget(),
                            StageMapHazardPreviewTargetMode.FollowPlayerStart,
                            claimOwner: true);
                    }
                }

                if (!string.IsNullOrEmpty(_hazardPreviewError))
                    EditorGUILayout.HelpBox(_hazardPreviewError, MessageType.Warning);
            }
        }

        private void DrawEncounterTrack()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(180f)))
            {
                uint sourceId = ResolveEncounterSourceId();
                DrawEncounterTrackHeader(sourceId);

                var placements = (_document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>())
                    .Where(x => x.OwningSourceStableId == sourceId)
                    .OrderBy(x => x.PlacementInstanceId)
                    .ToArray();
                var rules = (_document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>())
                    .Where(x => x.OwningSourceStableId == sourceId)
                    .OrderBy(x => x.TriggerThresholdNormalized)
                    .ThenBy(x => x.RuleId)
                    .ToArray();

                if (sourceId == 0u || !SourceExists(sourceId))
                {
                    EditorGUILayout.HelpBox("No active Source is available for Hazard Encounter editing.", MessageType.Warning);
                    return;
                }

                if (placements.Length == 0)
                {
                    EditorGUILayout.HelpBox("This Source has no HazardActor placements. Select PlaceHazardActor, choose an archetype, then click in Scene View.", MessageType.Info);
                    return;
                }

                DrawEncounterTimeline(sourceId, placements, rules);
            }
        }

        private void DrawEncounterTrackHeader(uint sourceId)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Hazard Encounter", EditorStyles.boldLabel, GUILayout.Width(130f));
                GUILayout.Label($"Source {sourceId}", EditorStyles.miniBoldLabel, GUILayout.Width(88f));
                bool pin = GUILayout.Toggle(_session.PinHazardEncounterSource, "Pin Source", EditorStyles.miniButton, GUILayout.Width(82f));
                if (pin != _session.PinHazardEncounterSource)
                {
                    _session.PinHazardEncounterSource = pin;
                    _session.PinnedHazardEncounterSourceStableId = pin ? sourceId : 0u;
                }
                GUILayout.FlexibleSpace();
            }

        }

        private void DrawEncounterTimeline(
            uint sourceId,
            StageMapHazardActorPlacementData[] placements,
            StageMapHazardActorOrchestrationRuleData[] rules)
        {
            const float LabelWidth = 170f;
            const float RowHeight = 28f;
            Rect header = EditorGUILayout.GetControlRect(false, 18f);
            Rect axis = new Rect(header.x + LabelWidth, header.y + 2f, Mathf.Max(80f, header.width - LabelWidth - 8f), 12f);
            EditorGUI.LabelField(new Rect(header.x, header.y, LabelWidth - 4f, header.height), "Placement", EditorStyles.miniBoldLabel);
            EditorGUI.DrawRect(new Rect(axis.x, axis.center.y, axis.width, 1f), new Color(0.45f, 0.45f, 0.45f, 1f));
            EditorGUI.LabelField(new Rect(axis.x - 4f, header.y, 24f, header.height), "0", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(axis.xMax - 16f, header.y, 24f, header.height), "1", EditorStyles.miniLabel);

            _encounterScroll = EditorGUILayout.BeginScrollView(_encounterScroll, GUILayout.MinHeight(96f), GUILayout.MaxHeight(260f));
            var rowByPlacement = new Dictionary<int, Rect>(placements.Length);
            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
                bool selected = _session.Selection.Kind == StageMapSelectionKind.HazardActor
                    && _session.Selection.OwningSourceStableId == sourceId
                    && _session.Selection.PlacementInstanceId == placement.PlacementInstanceId;
                DrawEncounterPlacementRow(sourceId, placement, row, LabelWidth, selected);
                rowByPlacement[placement.PlacementInstanceId] = row;
            }

            for (int i = 0; i < rules.Length; i++)
                DrawEncounterRuleMarker(sourceId, rules[i], rowByPlacement, LabelWidth);

            EditorGUILayout.EndScrollView();
        }

        private void DrawEncounterPlacementRow(
            uint sourceId,
            StageMapHazardActorPlacementData placement,
            Rect row,
            float labelWidth,
            bool selected)
        {
            Color rowColor = selected
                ? new Color(0.25f, 0.42f, 0.78f, 0.28f)
                : new Color(0.18f, 0.18f, 0.18f, 0.18f);
            EditorGUI.DrawRect(row, rowColor);
            float timelineXMin = row.x + labelWidth;
            float timelineXMax = row.xMax - 92f;
            EditorGUI.DrawRect(
                new Rect(Mathf.Lerp(timelineXMin, timelineXMax, Mathf.Clamp01(_session.HazardPreviewSourceProgress01)), row.y + 2f, 1f, row.height - 4f),
                new Color(0.45f, 0.85f, 1f, 0.85f));

            Rect label = new Rect(row.x + 4f, row.y + 3f, labelWidth - 8f, row.height - 6f);
            string actorName = placement.ActorArchetypePrefab != null ? placement.ActorArchetypePrefab.name : "(missing prefab)";
            if (GUI.Button(label, $"#{placement.PlacementInstanceId}  {actorName}", EditorStyles.miniButtonLeft))
                TrySelect(StageMapSelection.ForHazard(sourceId, placement.PlacementInstanceId), frame: true);

            Rect commands = new Rect(row.xMax - 82f, row.y + 3f, 78f, row.height - 6f);
            if (GUI.Button(new Rect(commands.x, commands.y, 24f, commands.height), new GUIContent("+S", "Add Spawn rule")))
                AddEncounterRule(sourceId, placement.PlacementInstanceId, HazardActorOrchestrationActionId.Spawn);
            if (GUI.Button(new Rect(commands.x + 26f, commands.y, 24f, commands.height), new GUIContent("+P", "Add PhaseSet rule")))
                AddEncounterRule(sourceId, placement.PlacementInstanceId, HazardActorOrchestrationActionId.PhaseSet);
            if (GUI.Button(new Rect(commands.x + 52f, commands.y, 24f, commands.height), new GUIContent("+R", "Add Retire rule")))
                AddEncounterRule(sourceId, placement.PlacementInstanceId, HazardActorOrchestrationActionId.Retire);
        }

        private void DrawEncounterRuleMarker(
            uint sourceId,
            StageMapHazardActorOrchestrationRuleData rule,
            Dictionary<int, Rect> rowByPlacement,
            float labelWidth)
        {
            var targets = rule.TargetPlacementInstanceIds ?? Array.Empty<int>();
            if (targets.Length == 0)
                return;

            var targetRows = new List<Rect>(targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                if (rowByPlacement.TryGetValue(targets[i], out Rect row))
                    targetRows.Add(row);
            }
            if (targetRows.Count == 0)
                return;

            Rect firstRow = targetRows[0];
            float xMin = firstRow.x + labelWidth;
            float xMax = firstRow.xMax - 92f;
            float progress = rule.TriggerType == HazardActorOrchestrationTriggerId.OnStageStart
                ? 0f
                : Mathf.Clamp01(rule.TriggerThresholdNormalized);
            float markerX = Mathf.Lerp(xMin, xMax, progress);
            Color color = GetEncounterActionColor(rule.ActionType);

            if (targetRows.Count > 1 && Event.current != null && Event.current.type == EventType.Repaint)
            {
                float yMin = targetRows.Min(x => x.center.y);
                float yMax = targetRows.Max(x => x.center.y);
                Handles.BeginGUI();
                Handles.color = new Color(color.r, color.g, color.b, 0.75f);
                Handles.DrawLine(new Vector3(markerX, yMin, 0f), new Vector3(markerX, yMax, 0f));
                Handles.EndGUI();
            }

            for (int i = 0; i < targetRows.Count; i++)
            {
                Rect row = targetRows[i];
                Rect marker = new Rect(markerX - 7f, row.y + 5f, 14f, 18f);
                EditorGUI.DrawRect(marker, color);
                GUIContent content = new GUIContent(
                    GetEncounterActionGlyph(rule.ActionType),
                    $"{rule.ActionType} Rule {rule.RuleId} @ {rule.TriggerThresholdNormalized:0.###}");
                if (GUI.Button(marker, content, EditorStyles.miniButton))
                    TrySelect(StageMapSelection.ForHazardRule(sourceId, rule.RuleId), frame: true);
                if (rule.TriggerType != HazardActorOrchestrationTriggerId.OnStageStart)
                    HandleEncounterRuleMarkerDrag(sourceId, rule.RuleId, marker, xMin, xMax);
            }
        }

        private void HandleEncounterRuleMarkerDrag(
            uint sourceId,
            int ruleId,
            Rect markerRect,
            float xMin,
            float xMax)
        {
            Event evt = Event.current;
            if (evt == null || IsLayerLocked(StageMapEditorLayer.HazardActors))
                return;

            if (evt.type == EventType.MouseDown && evt.button == 0 && markerRect.Contains(evt.mousePosition))
            {
                _draggingEncounterSourceId = sourceId;
                _draggingEncounterRuleId = ruleId;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag
                && evt.button == 0
                && _draggingEncounterSourceId == sourceId
                && _draggingEncounterRuleId == ruleId)
            {
                float progress = Mathf.InverseLerp(xMin, xMax, evt.mousePosition.x);
                TrySetEncounterRuleProgress(sourceId, ruleId, progress);
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp
                && _draggingEncounterSourceId == sourceId
                && _draggingEncounterRuleId == ruleId)
            {
                _draggingEncounterSourceId = 0u;
                _draggingEncounterRuleId = 0;
                evt.Use();
            }
        }

        private void AddEncounterRule(uint sourceId, int placementId, HazardActorOrchestrationActionId action)
        {
            RecordAndApply(
                $"Add Hazard Actor {action} Rule",
                () =>
                {
                    bool changed = StageMapHazardActorOrchestrationUtility.AddRule(_document, sourceId, action, placementId, out int ruleId);
                    if (changed && action != HazardActorOrchestrationActionId.Spawn)
                    {
                        StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(_document, sourceId, ruleId, out int index);
                        var rule = _document.HazardActorOrchestrationRules[index];
                        rule.TriggerThresholdNormalized = Mathf.Clamp01(_session.HazardPreviewSourceProgress01);
                        StageMapHazardActorOrchestrationUtility.UpdateRule(_document, sourceId, ruleId, rule);
                    }
                    if (changed)
                        _session.Select(StageMapSelection.ForHazardRule(sourceId, ruleId));
                    return changed;
                });
            PrepareHazardPreviewForSelection(play: false);
        }

        public bool TrySetEncounterRuleProgress(uint sourceId, int ruleId, float progress01)
        {
            return RecordAndApply(
                "Move Hazard Encounter Rule Marker",
                () =>
                {
                    if (!StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(_document, sourceId, ruleId, out int index))
                        return false;
                    var rule = _document.HazardActorOrchestrationRules[index];
                    if (rule.TriggerType == HazardActorOrchestrationTriggerId.OnStageStart)
                        return false;
                    float next = Mathf.Clamp01(progress01);
                    if (Mathf.Approximately(rule.TriggerThresholdNormalized, next))
                        return false;
                    rule.TriggerThresholdNormalized = next;
                    return StageMapHazardActorOrchestrationUtility.UpdateRule(_document, sourceId, ruleId, rule);
                });
        }

        private uint ResolveEncounterSourceId()
        {
            if (_session.PinHazardEncounterSource
                && _session.PinnedHazardEncounterSourceStableId > 0u
                && SourceExists(_session.PinnedHazardEncounterSourceStableId))
            {
                _session.HazardActorSourceStableId = _session.PinnedHazardEncounterSourceStableId;
                return _session.PinnedHazardEncounterSourceStableId;
            }

            if (TryGetSourceIdFromSelection(_session.Selection, out uint selectedSourceId)
                && SourceExists(selectedSourceId))
            {
                _session.HazardActorSourceStableId = selectedSourceId;
                return selectedSourceId;
            }

            if (_session.HazardActorSourceStableId > 0u && SourceExists(_session.HazardActorSourceStableId))
                return _session.HazardActorSourceStableId;

            var sources = _document.SourceRegions ?? Array.Empty<StageMapRegionData>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].Active && sources[i].StableId > 0u)
                {
                    _session.HazardActorSourceStableId = sources[i].StableId;
                    return sources[i].StableId;
                }
            }
            return 0u;
        }

        public uint ResolveEncounterSourceIdForTests()
        {
            return ResolveEncounterSourceId();
        }

        private void SyncEncounterSourceFromSelection(StageMapSelection selection)
        {
            if (_session.PinHazardEncounterSource)
                return;
            if (!TryGetSourceIdFromSelection(selection, out uint sourceId) || !SourceExists(sourceId))
                return;
            _session.HazardActorSourceStableId = sourceId;
        }

        public bool PrepareHazardPreviewForSelection(bool play)
        {
            if (_document == null)
                return false;

            StageMapSelection selection = _session.Selection;
            if (selection.Kind == StageMapSelectionKind.HazardActorRule
                && StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(
                    _document,
                    selection.OwningSourceStableId,
                    selection.RuleId,
                    out int ruleIndex))
            {
                _session.HazardPreviewSourceProgress01 = Mathf.Clamp01(
                    _document.HazardActorOrchestrationRules[ruleIndex].TriggerThresholdNormalized);
            }

            bool prepared = ReconcileHazardPreview(claimOwner: true);
            if (prepared && play)
                HazardActorPreviewCoordinator.Play(_hazardPreviewOwner);
            return prepared;
        }

        public StageMapHazardPreviewBinding ResolveHazardPreviewBindingForTests()
        {
            return ResolveHazardPreviewBinding(_session.Selection);
        }

        private StageMapHazardPreviewBinding ResolveHazardPreviewBinding(StageMapSelection selection)
        {
            switch (selection.Kind)
            {
                case StageMapSelectionKind.HazardActor:
                    return StageMapHazardActorOrchestrationUtility.TryFindPlacement(
                        _document,
                        selection.OwningSourceStableId,
                        selection.PlacementInstanceId,
                        out _)
                        ? StageMapHazardPreviewBinding.ForActor(
                            selection.OwningSourceStableId,
                            selection.PlacementInstanceId)
                        : StageMapHazardPreviewBinding.None;
                case StageMapSelectionKind.HazardActorRule:
                    return StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(
                        _document,
                        selection.OwningSourceStableId,
                        selection.RuleId,
                        out _)
                        ? StageMapHazardPreviewBinding.ForEncounter(selection.OwningSourceStableId)
                        : StageMapHazardPreviewBinding.None;
                case StageMapSelectionKind.SourceRegion:
                case StageMapSelectionKind.SourceAnchor:
                    return SourceExists(selection.StableId)
                        ? StageMapHazardPreviewBinding.ForEncounter(selection.StableId)
                        : StageMapHazardPreviewBinding.None;
                default:
                    return StageMapHazardPreviewBinding.None;
            }
        }

        private bool ReconcileHazardPreview(bool claimOwner)
        {
            StageMapHazardPreviewBinding binding = ResolveHazardPreviewBinding(_session.Selection);
            if (!binding.IsValid)
            {
                ReleaseHazardPreview(clearBinding: true);
                return false;
            }

            if (_session.HazardPreviewTargetMode == StageMapHazardPreviewTargetMode.FollowPlayerStart)
                _session.HazardPreviewTargetWorldPosition = GetCurrentHazardPreviewPlayerTarget();
            else
                _session.HazardPreviewTargetWorldPosition = ProjectHazardPreviewTargetToGridPlane(
                    _session.HazardPreviewTargetWorldPosition);

            _hazardPreviewBinding = binding;
            _hazardPreviewError = string.Empty;
            switch (binding.Kind)
            {
                case StageMapHazardPreviewBindingKind.Actor:
                    if (!StageMapHazardActorOrchestrationUtility.TryFindPlacement(
                            _document,
                            binding.SourceStableId,
                            binding.PlacementInstanceId,
                            out var placement))
                    {
                        return FailHazardPreview("The selected HazardActor placement no longer exists.");
                    }
                    return BeginPlacementPreview(placement, claimOwner);
                case StageMapHazardPreviewBindingKind.Encounter:
                    return BuildEncounterPreviewForSource(binding.SourceStableId, claimOwner);
                default:
                    return false;
            }
        }

        private bool SourceExists(uint sourceId)
        {
            var sources = _document != null ? _document.SourceRegions : null;
            if (sources == null || sourceId == 0u)
                return false;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].StableId == sourceId && sources[i].Active)
                    return true;
            }
            return false;
        }

        private static bool TryGetSourceIdFromSelection(StageMapSelection selection, out uint sourceId)
        {
            sourceId = 0u;
            switch (selection.Kind)
            {
                case StageMapSelectionKind.SourceRegion:
                case StageMapSelectionKind.SourceAnchor:
                    sourceId = selection.StableId;
                    return sourceId > 0u;
                case StageMapSelectionKind.HazardActor:
                case StageMapSelectionKind.HazardActorRule:
                    sourceId = selection.OwningSourceStableId;
                    return sourceId > 0u;
                default:
                    return false;
            }
        }

        private bool BeginPlacementPreview(StageMapHazardActorPlacementData placement, bool claimOwner)
        {
            if (placement.ActorArchetypePrefab == null)
                return FailHazardPreview("The selected HazardActor placement has no archetype prefab.");
            if (!HazardActorPreviewSnapshotBuilder.TryBuild(placement.ActorArchetypePrefab, out var snapshot))
                return FailHazardPreview("The selected HazardActor archetype could not produce a preview snapshot.");
            if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(_document.HazardActorPlacements, placement.OwningSourceStableId, placement.PlacementInstanceId, out int hazardIndex))
                return FailHazardPreview("The selected HazardActor placement identity is ambiguous.");
            Vector3 world = StageMapSelectionUtility.GetHazardActorWorld(_document, hazardIndex);
            var session = new HazardActorPreviewSession();
            session.Load(snapshot, new HazardActorPreviewInput
            {
                Scope = HazardActorPreviewScope.Actor,
                OwningSourceStableId = placement.OwningSourceStableId,
                PlacementInstanceId = placement.PlacementInstanceId,
                SourceProgress01 = _session.HazardPreviewSourceProgress01,
                ActorWorldPosition = world,
                ActorYawDeg = placement.LocalYawDeg,
                TargetWorldPosition = _session.HazardPreviewTargetWorldPosition,
                SpawnAtStart = true,
            });
            ReplaceHazardActorPreviewSession(session, claimOwner);
            return true;
        }

        public bool BeginEncounterPreviewForSource(uint sourceId)
        {
            return BeginEncounterPreviewForSource(sourceId, play: true);
        }

        public bool BeginEncounterPreviewForSource(uint sourceId, bool play)
        {
            if (_document == null || !SourceExists(sourceId))
                return false;
            _hazardPreviewBinding = StageMapHazardPreviewBinding.ForEncounter(sourceId);
            if (_session.HazardPreviewTargetMode == StageMapHazardPreviewTargetMode.FollowPlayerStart)
                _session.HazardPreviewTargetWorldPosition = GetCurrentHazardPreviewPlayerTarget();
            bool prepared = BuildEncounterPreviewForSource(sourceId, claimOwner: true);
            if (prepared && play)
                HazardActorPreviewCoordinator.Play(_hazardPreviewOwner);
            return prepared;
        }

        private bool BuildEncounterPreviewForSource(uint sourceId, bool claimOwner)
        {
            var placements = _document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            int matchingCount = placements.Count(x => x.OwningSourceStableId == sourceId && x.ActorArchetypePrefab != null);
            if (matchingCount <= 0)
                return FailHazardPreview($"Source {sourceId} has no previewable HazardActor placements.");

            int capPerActor = Mathf.Max(1, HazardActorPreviewSession.EncounterGhostCap / matchingCount);
            var encounter = new HazardActorEncounterPreviewSession();
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].OwningSourceStableId != sourceId || placements[i].ActorArchetypePrefab == null)
                    continue;
                if (!TryBuildEncounterPreviewInput(placements[i], capPerActor, out var input))
                    continue;
                if (!HazardActorPreviewSnapshotBuilder.TryBuild(placements[i].ActorArchetypePrefab, out var snapshot))
                    continue;
                encounter.AddActorPlan(
                    placements[i].PlacementInstanceId,
                    snapshot,
                    input,
                    BuildEncounterRulePreviews(sourceId, placements[i].PlacementInstanceId));
            }

            encounter.SetSourceProgress(_session.HazardPreviewSourceProgress01);

            if (encounter.PlannedActorCount <= 0)
            {
                encounter.Dispose();
                return FailHazardPreview($"Source {sourceId} could not build an Encounter preview.");
            }

            ReplaceHazardEncounterPreviewSession(encounter, claimOwner);
            return true;
        }

        private bool TryBuildEncounterPreviewInput(
            StageMapHazardActorPlacementData placement,
            int capPerActor,
            out HazardActorPreviewInput input)
        {
            input = default;
            if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    _document.HazardActorPlacements,
                    placement.OwningSourceStableId,
                    placement.PlacementInstanceId,
                    out int hazardIndex))
            {
                return false;
            }

            Vector3 world = StageMapSelectionUtility.GetHazardActorWorld(_document, hazardIndex);
            input = new HazardActorPreviewInput
            {
                Scope = HazardActorPreviewScope.Encounter,
                OwningSourceStableId = placement.OwningSourceStableId,
                PlacementInstanceId = placement.PlacementInstanceId,
                GhostCapOverride = capPerActor,
                SourceProgress01 = _session.HazardPreviewSourceProgress01,
                ActorWorldPosition = world,
                ActorYawDeg = placement.LocalYawDeg,
                TargetWorldPosition = _session.HazardPreviewTargetWorldPosition,
                SpawnAtStart = true,
            };
            return true;
        }

        private void ReplaceHazardActorPreviewSession(HazardActorPreviewSession session, bool claimOwner)
        {
            HazardActorPreviewSession previousActor = _hazardActorPreviewSession;
            HazardActorEncounterPreviewSession previousEncounter = _hazardEncounterPreviewSession;
            _hazardActorPreviewSession = session;
            _hazardEncounterPreviewSession = null;
            if (claimOwner || HazardActorPreviewCoordinator.IsActiveOwner(_hazardPreviewOwner))
                HazardActorPreviewCoordinator.ActivateActor(_hazardPreviewOwner, session);
            previousActor?.Dispose();
            previousEncounter?.Dispose();
            _hazardPreviewError = string.Empty;
            SceneView.RepaintAll();
            Repaint();
        }

        private void ReplaceHazardEncounterPreviewSession(HazardActorEncounterPreviewSession session, bool claimOwner)
        {
            HazardActorPreviewSession previousActor = _hazardActorPreviewSession;
            HazardActorEncounterPreviewSession previousEncounter = _hazardEncounterPreviewSession;
            _hazardActorPreviewSession = null;
            _hazardEncounterPreviewSession = session;
            if (claimOwner || HazardActorPreviewCoordinator.IsActiveOwner(_hazardPreviewOwner))
                HazardActorPreviewCoordinator.ActivateEncounter(_hazardPreviewOwner, session);
            previousActor?.Dispose();
            previousEncounter?.Dispose();
            _hazardPreviewError = string.Empty;
            SceneView.RepaintAll();
            Repaint();
        }

        private bool FailHazardPreview(string error)
        {
            _hazardPreviewError = error ?? "Hazard preview could not be prepared.";
            ReleaseHazardPreview(clearBinding: false);
            Repaint();
            return false;
        }

        private void ReleaseHazardPreview(bool clearBinding)
        {
            HazardActorPreviewCoordinator.ClearOwner(_hazardPreviewOwner);
            _hazardActorPreviewSession?.Dispose();
            _hazardEncounterPreviewSession?.Dispose();
            _hazardActorPreviewSession = null;
            _hazardEncounterPreviewSession = null;
            if (clearBinding)
            {
                _hazardPreviewBinding = StageMapHazardPreviewBinding.None;
                _hazardPreviewError = string.Empty;
            }
            SceneView.RepaintAll();
        }

        private bool ClaimPreparedHazardPreview()
        {
            switch (_hazardPreviewBinding.Kind)
            {
                case StageMapHazardPreviewBindingKind.Actor when _hazardActorPreviewSession != null:
                    HazardActorPreviewCoordinator.ActivateActor(_hazardPreviewOwner, _hazardActorPreviewSession);
                    return true;
                case StageMapHazardPreviewBindingKind.Encounter when _hazardEncounterPreviewSession != null:
                    HazardActorPreviewCoordinator.ActivateEncounter(_hazardPreviewOwner, _hazardEncounterPreviewSession);
                    return true;
                default:
                    return ReconcileHazardPreview(claimOwner: true);
            }
        }

        private void QueueHazardPreviewReconcile(bool claimOwner)
        {
            _hazardPreviewReconcileClaimsOwner |= claimOwner;
            if (_hazardPreviewReconcileQueued)
                return;
            _hazardPreviewReconcileQueued = true;
            EditorApplication.delayCall += FlushHazardPreviewReconcile;
        }

        private void FlushHazardPreviewReconcile()
        {
            EditorApplication.delayCall -= FlushHazardPreviewReconcile;
            if (!_hazardPreviewReconcileQueued)
                return;
            bool claimOwner = _hazardPreviewReconcileClaimsOwner;
            _hazardPreviewReconcileQueued = false;
            _hazardPreviewReconcileClaimsOwner = false;
            ReconcileHazardPreview(claimOwner);
        }

        public void FlushHazardPreviewReconcileForTests()
        {
            FlushHazardPreviewReconcile();
        }

        private Vector3 GetCurrentHazardPreviewPlayerTarget()
        {
            return ProjectHazardPreviewTargetToGridPlane(StageMapSelectionUtility.GetPlayerStartWorld(_document));
        }

        private Vector3 ProjectHazardPreviewTargetToGridPlane(Vector3 target)
        {
            target.y = _document != null ? _document.Grid.Origin.y : 0f;
            return target;
        }

        public void SetHazardPreviewTargetForTests(Vector3 target, bool followPlayerStart)
        {
            SetHazardPreviewTarget(
                target,
                followPlayerStart
                    ? StageMapHazardPreviewTargetMode.FollowPlayerStart
                    : StageMapHazardPreviewTargetMode.Custom,
                claimOwner: true);
        }

        private void SetHazardPreviewTarget(
            Vector3 target,
            StageMapHazardPreviewTargetMode mode,
            bool claimOwner)
        {
            if (_document == null)
                return;
            _session.HazardPreviewTargetMode = mode;
            _session.HazardPreviewTargetWorldPosition = mode == StageMapHazardPreviewTargetMode.FollowPlayerStart
                ? GetCurrentHazardPreviewPlayerTarget()
                : ProjectHazardPreviewTargetToGridPlane(target);
            if (claimOwner)
                ClaimPreparedHazardPreview();
            _hazardActorPreviewSession?.SetTargetWorldPosition(_session.HazardPreviewTargetWorldPosition);
            _hazardEncounterPreviewSession?.SetTargetWorldPosition(_session.HazardPreviewTargetWorldPosition);
            SceneView.RepaintAll();
            Repaint();
        }

        private HazardActorEncounterRulePreview[] BuildEncounterRulePreviews(uint sourceId, int placementId)
        {
            var rules = _document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            var previews = new List<HazardActorEncounterRulePreview>(4);
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule.OwningSourceStableId != sourceId || !ContainsTarget(rule.TargetPlacementInstanceIds, placementId))
                    continue;
                previews.Add(new HazardActorEncounterRulePreview(
                    rule.RuleId,
                    placementId,
                    rule.ActionType,
                    rule.TriggerType,
                    rule.TriggerThresholdNormalized,
                    rule.TargetPhaseId));
            }
            previews.Sort((a, b) =>
            {
                int threshold = a.TriggerThresholdNormalized.CompareTo(b.TriggerThresholdNormalized);
                return threshold != 0 ? threshold : a.RuleId.CompareTo(b.RuleId);
            });
            return previews.ToArray();
        }

        private static bool ContainsTarget(int[] targets, int placementId)
        {
            if (targets == null)
                return false;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == placementId)
                    return true;
            }
            return false;
        }

        private static Color GetEncounterActionColor(HazardActorOrchestrationActionId action)
        {
            switch (action)
            {
                case HazardActorOrchestrationActionId.Spawn: return new Color(0.25f, 0.85f, 0.35f, 1f);
                case HazardActorOrchestrationActionId.PhaseSet: return new Color(0.35f, 0.65f, 1f, 1f);
                case HazardActorOrchestrationActionId.Retire: return new Color(1f, 0.45f, 0.25f, 1f);
                default: return new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }

        private static string GetEncounterActionGlyph(HazardActorOrchestrationActionId action)
        {
            switch (action)
            {
                case HazardActorOrchestrationActionId.Spawn: return "S";
                case HazardActorOrchestrationActionId.PhaseSet: return "P";
                case HazardActorOrchestrationActionId.Retire: return "R";
                default: return "?";
            }
        }

        private void DrawIssues()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Issues ({_issues.Count})", EditorStyles.boldLabel);
            _issueScroll = EditorGUILayout.BeginScrollView(_issueScroll, GUILayout.MinHeight(140f), GUILayout.MaxHeight(220f));
            for (int i = 0; i < _issues.Count; i++)
            {
                var issue = _issues[i];
                StageMapIssueTarget target = i < _documentIssues.Count
                    ? _documentIssues[i].Target
                    : StageMapDocumentIssueMapper.ResolveTarget(_document, issue);
                MessageType messageType = issue.Severity == ContentValidationSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox($"{issue.Code} {issue.Location}\n{issue.Message}", messageType);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(72f)))
                            NavigateToIssue(i);

                        if (!string.IsNullOrEmpty(target.FixId)
                            && StageMapDocumentFixUtility.TryBuildFixPreview(_document, issue, out var fixPreview))
                        {
                            bool canApply = StageMapEditorMutationUtility.CanApplyFix(_session, issue, target, out _);
                            using (new EditorGUI.DisabledScope(!canApply))
                            {
                                if (GUILayout.Button("Apply Fix", GUILayout.Width(80f)))
                                    ApplyIssueFix(i);
                            }
                            EditorGUILayout.LabelField(fixPreview.Summary, EditorStyles.wordWrappedMiniLabel);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDiff()
        {
            EditorGUILayout.Space(6f);
            int changeCount = _applyPlan != null ? _applyPlan.Changes.Count : 0;
            EditorGUILayout.LabelField($"Diff Summary ({changeCount})", EditorStyles.boldLabel);
            _diffScroll = EditorGUILayout.BeginScrollView(_diffScroll, GUILayout.MinHeight(140f));
            if (_applyPlan != null)
            {
                for (int i = 0; i < _applyPlan.Changes.Count; i++)
                {
                    var change = _applyPlan.Changes[i];
                    EditorGUILayout.LabelField($"{change.Kind}: {change.Target}.{change.Field}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(change.Description, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space(3f);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void Validate()
        {
            _issues.Clear();
            _session.ValidationSnapshot.Clear();
            StageMapDocumentValidationRules.ValidateDocument(_document, AssetDatabase.GetAssetPath(_document), _issues);
            _session.ValidationSnapshot.AddRange(_issues);
            StageMapDocumentIssueMapper.Map(_document, _issues, _documentIssues);
            Repaint();
        }

        public void BuildDryRun()
        {
            _applyPlan = StageMapApplyPlanner.BuildPlan(_document);
            _issues.Clear();
            _issues.AddRange(_applyPlan.ValidationIssues);
            _session.ValidationSnapshot.Clear();
            _session.ValidationSnapshot.AddRange(_issues);
            StageMapDocumentIssueMapper.Map(_document, _issues, _documentIssues);
            Repaint();
        }

        private void Apply()
        {
            if (_applyPlan == null)
                return;

            bool confirmed = !_applyPlan.RequiresConfirmation
                || EditorUtility.DisplayDialog("Apply Stage Map Document", "The apply plan contains destructive changes.", "Apply", "Cancel");
            if (!confirmed)
            {
                return;
            }

            if (!StageMapApplyPlanner.TryApplyPlan(_applyPlan, saveAssets: true, confirmed: confirmed, out string error))
            {
                EditorUtility.DisplayDialog("Apply Stage Map Document", error, "OK");
                BuildDryRun();
                return;
            }

            _session.Dirty = false;
            BuildDryRun();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_document == null || _document.Grid.Width <= 0 || _document.Grid.Height <= 0 || _document.Grid.CellSize <= 0f)
                return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (Event.current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(controlId);

            DrawDocumentSceneView();
            DrawSelectionHandle();
            DrawHazardPreviewTargetHandle();
            HandleDeleteShortcut(Event.current);
            HandleSceneInput(Event.current);
        }

        private void DrawSelectionHandle()
        {
            if (!CanDrawSelectionHandle())
                return;

            StageMapSelection selection = _session.Selection;
            if (!TryGetSelectionPose(selection, out Vector3 world, out Quaternion rotation, out bool supportsRotation))
                return;

            EditorGUI.BeginChangeCheck();
            Vector3 nextWorld = Handles.PositionHandle(world, rotation);
            Quaternion nextRotation = supportsRotation ? Handles.RotationHandle(rotation, nextWorld) : rotation;
            if (!EditorGUI.EndChangeCheck())
                return;

            float yaw = nextRotation.eulerAngles.y;
            Vector3 euler = nextRotation.eulerAngles;
            RecordAndApply(
                "Move Stage Map Selection",
                () => StageMapEditorMutationUtility.TryMoveSelection(_session, nextWorld, yaw, euler, out _));
        }

        private void DrawHazardPreviewTargetHandle()
        {
            if (!_hazardPreviewBinding.IsValid
                || !HazardActorPreviewCoordinator.IsActiveOwner(_hazardPreviewOwner)
                || (_hazardActorPreviewSession == null && _hazardEncounterPreviewSession == null))
            {
                return;
            }

            Vector3 target = ProjectHazardPreviewTargetToGridPlane(_session.HazardPreviewTargetWorldPosition);
            float size = Mathf.Max(0.04f, HandleUtility.GetHandleSize(target) * 0.08f);
            Color previousColor = Handles.color;
            Handles.color = new Color(0.15f, 0.9f, 1f, 0.95f);
            Handles.DrawWireDisc(target, Vector3.up, size * 1.6f);
            Handles.DrawLine(target + Vector3.left * size, target + Vector3.right * size);
            Handles.DrawLine(target + Vector3.back * size, target + Vector3.forward * size);
            Handles.Label(target + Vector3.up * (size * 1.8f), "Preview Target");

            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.FreeMoveHandle(target, size, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                SetHazardPreviewTarget(
                    next,
                    StageMapHazardPreviewTargetMode.Custom,
                    claimOwner: true);
            }
            Handles.color = previousColor;
        }

        public bool CanDrawSelectionHandle()
        {
            StageMapSelection selection = _session.Selection;
            return selection.Kind != StageMapSelectionKind.None
                && StageMapEditingPolicy.IsSelectionVisible(_session, selection)
                && StageMapEditingPolicy.CanMutateSelection(_session, selection, out _)
                && TryGetSelectionPose(selection, out _, out _, out _);
        }

        private bool TryGetSelectionPose(
            StageMapSelection selection,
            out Vector3 world,
            out Quaternion rotation,
            out bool supportsRotation)
        {
            world = default;
            rotation = Quaternion.identity;
            supportsRotation = false;
            switch (selection.Kind)
            {
                case StageMapSelectionKind.SourceAnchor:
                    world = StageMapSelectionUtility.GetRegionAnchorWorld(_document, StageRegionKind.Source, selection.StableId);
                    return true;
                case StageMapSelectionKind.DepositAnchor:
                    world = StageMapSelectionUtility.GetRegionAnchorWorld(_document, StageRegionKind.Deposit, selection.StableId);
                    return true;
                case StageMapSelectionKind.PlayerStart:
                    world = StageMapSelectionUtility.GetPlayerStartWorld(_document);
                    rotation = Quaternion.Euler(0f, _document.PlayerStart.YawDeg, 0f);
                    supportsRotation = true;
                    return true;
                case StageMapSelectionKind.HazardActor:
                    if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                            _document.HazardActorPlacements,
                            selection.OwningSourceStableId,
                            selection.PlacementInstanceId,
                            out int hazardIndex))
                    {
                        return false;
                    }
                    world = StageMapSelectionUtility.GetHazardActorWorld(_document, hazardIndex);
                    rotation = Quaternion.Euler(0f, _document.HazardActorPlacements[hazardIndex].LocalYawDeg, 0f);
                    supportsRotation = true;
                    return true;
                case StageMapSelectionKind.Presentation:
                    if (!StageMapSelectionUtility.TryFindUniquePresentationIndex(
                            _document.PresentationLinks,
                            selection.StableId,
                            out int presentationIndex))
                    {
                        return false;
                    }
                    world = StageMapSelectionUtility.GetPresentationWorld(_document, presentationIndex);
                    rotation = Quaternion.Euler(_document.PresentationLinks[presentationIndex].Euler);
                    supportsRotation = true;
                    return true;
                default:
                    return false;
            }
        }

        private void HandleDeleteShortcut(Event evt)
        {
            if (evt == null
                || evt.type != EventType.KeyDown
                || (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace))
            {
                return;
            }

            if (RecordAndApply(
                    "Delete Stage Map Selection",
                    () => StageMapEditorMutationUtility.TryDeleteSelection(_session, out _)))
            {
                evt.Use();
            }
        }

        private void DrawDocumentSceneView()
        {
            var grid = _document.Grid;
            if (_session.ShowMovementLayer || _session.ShowSourceLayer || _session.ShowDepositLayer)
                DrawCellOverlays();
            if (_session.ShowGridLayer)
                DrawDocumentGrid();
            if (_session.ShowAnchorLayer)
                DrawRegionAnchors();
            if (_session.ShowPlayerStartLayer)
                DrawPlayerStart();
            if (_session.ShowHazardActorLayer)
                DrawHazardActors();
            if (_session.ShowPresentationLayer)
                DrawPresentationLinks();
            DrawSelectedCell();
        }

        private void DrawDocumentGrid()
        {
            var grid = _document.Grid;
            Handles.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            float width = grid.Width * grid.CellSize;
            float height = grid.Height * grid.CellSize;
            var origin = grid.Origin;
            for (int x = 0; x <= grid.Width; x++)
            {
                float worldX = origin.x + (x * grid.CellSize);
                Handles.DrawLine(new Vector3(worldX, origin.y, origin.z), new Vector3(worldX, origin.y, origin.z + height));
            }

            for (int y = 0; y <= grid.Height; y++)
            {
                float worldZ = origin.z + (y * grid.CellSize);
                Handles.DrawLine(new Vector3(origin.x, origin.y, worldZ), new Vector3(origin.x + width, origin.y, worldZ));
            }
        }

        private void DrawCellOverlays()
        {
            EnsureOverlayCache();
            _overlayCache.Draw(
                _session.ShowMovementLayer,
                _session.ShowSourceLayer,
                _session.ShowDepositLayer);
        }

        private void EnsureOverlayCache()
        {
            _overlayCache.EnsureBuilt(_document);
        }

        private void InvalidateOverlayCache()
        {
            _overlayCache.Invalidate();
            SceneView.RepaintAll();
        }

        private void DrawRegionAnchors()
        {
            DrawRegionAnchorArray(_document.SourceRegions, StageRegionKind.Source, new Color(0.1f, 0.85f, 1f, 1f));
            DrawRegionAnchorArray(_document.DepositRegions, StageRegionKind.Deposit, new Color(1f, 0.75f, 0.1f, 1f));
        }

        private void DrawRegionAnchorArray(StageMapRegionData[] regions, StageRegionKind kind, Color color)
        {
            if (regions == null)
                return;

            float radius = Mathf.Max(0.04f, _document.Grid.CellSize * 0.12f);
            for (int i = 0; i < regions.Length; i++)
            {
                if (!regions[i].Active || !TryGetRegionAnchorWorld(kind, regions[i].StableId, out var world))
                    continue;

                Handles.color = color;
                Handles.DrawSolidDisc(world, Vector3.up, radius);
                if (ShouldDrawAuthoringGizmoLabels())
                    Handles.Label(world + Vector3.up * radius, $"{kind} {regions[i].StableId}");
            }
        }

        private void DrawPlayerStart()
        {
            if (!_document.PlayerStart.Active)
                return;
            Vector3 world = StageMapDocumentCommandUtility.GetCellCenterWorld(_document, _document.PlayerStart.AnchorCell)
                + new Vector3(_document.PlayerStart.AnchorOffset.x * _document.Grid.CellSize, 0f, _document.PlayerStart.AnchorOffset.y * _document.Grid.CellSize);
            Handles.color = new Color(0.25f, 1f, 0.35f, 1f);
            Handles.DrawSolidDisc(world, Vector3.up, _document.Grid.CellSize * 0.14f);
            Vector3 forward = Quaternion.Euler(0f, _document.PlayerStart.YawDeg, 0f) * Vector3.forward;
            Handles.DrawLine(world, world + forward * (_document.Grid.CellSize * 0.45f));
            if (ShouldDrawAuthoringGizmoLabels())
                Handles.Label(world + Vector3.up * (_document.Grid.CellSize * 0.12f), "PlayerStart");
        }

        private void DrawHazardActors()
        {
            var placements = _document.HazardActorPlacements;
            if (placements == null)
                return;

            uint activeSourceId = ResolveEncounterSourceId();
            for (int i = 0; i < placements.Length; i++)
            {
                Vector3 baseWorld = TryGetRegionAnchorWorld(StageRegionKind.Source, placements[i].OwningSourceStableId, out var anchor)
                    ? anchor
                    : _document.Grid.Origin;
                Vector3 world = baseWorld + placements[i].SourceLocalOffset;
                bool activeSource = placements[i].OwningSourceStableId == activeSourceId;
                StageMapHazardPlacementPreviewVisualState previewState = GetHazardPlacementPreviewVisualState(
                    placements[i].OwningSourceStableId,
                    placements[i].PlacementInstanceId);
                if (previewState == StageMapHazardPlacementPreviewVisualState.NotInPreview)
                {
                    Handles.color = activeSource
                        ? new Color(1f, 0.25f, 0.75f, 1f)
                        : new Color(1f, 0.25f, 0.75f, 0.28f);
                    Handles.DrawWireCube(world, Vector3.one * (_document.Grid.CellSize * 0.25f));
                    if (activeSource && ShouldDrawAuthoringGizmoLabels())
                        Handles.Label(world + Vector3.up * (_document.Grid.CellSize * 0.12f), $"Hazard {placements[i].PlacementInstanceId}");
                    continue;
                }

                if (previewState != StageMapHazardPlacementPreviewVisualState.Active)
                {
                    DrawInactiveHazardPreviewPlacement(
                        world,
                        placements[i].PlacementInstanceId,
                        previewState,
                        ShouldLabelInactiveHazardPreviewPlacement(
                            placements[i].OwningSourceStableId,
                            placements[i].PlacementInstanceId));
                }
            }
        }

        public StageMapHazardPlacementPreviewVisualState GetHazardPlacementPreviewVisualStateForTests(
            uint owningSourceStableId,
            int placementInstanceId)
        {
            return GetHazardPlacementPreviewVisualState(owningSourceStableId, placementInstanceId);
        }

        private StageMapHazardPlacementPreviewVisualState GetHazardPlacementPreviewVisualState(
            uint owningSourceStableId,
            int placementInstanceId)
        {
            if (owningSourceStableId == 0u || placementInstanceId <= 0)
                return StageMapHazardPlacementPreviewVisualState.NotInPreview;
            if (!HazardActorPreviewCoordinator.IsActiveOwner(_hazardPreviewOwner))
                return StageMapHazardPlacementPreviewVisualState.NotInPreview;

            if (_hazardPreviewBinding.Kind == StageMapHazardPreviewBindingKind.Actor
                && _hazardActorPreviewSession != null
                && MatchesPreviewInput(_hazardActorPreviewSession.Input, owningSourceStableId, placementInstanceId))
            {
                return StageMapHazardPlacementPreviewVisualState.Active;
            }

            if (_hazardPreviewBinding.Kind != StageMapHazardPreviewBindingKind.Encounter
                || _hazardEncounterPreviewSession == null
                || _hazardPreviewBinding.SourceStableId != owningSourceStableId
                || !_hazardEncounterPreviewSession.TryGetPlacementState(
                    owningSourceStableId,
                    placementInstanceId,
                    out var state))
            {
                return StageMapHazardPlacementPreviewVisualState.NotInPreview;
            }

            switch (state)
            {
                case HazardActorEncounterPlacementPreviewState.WaitingForSpawn:
                    return StageMapHazardPlacementPreviewVisualState.WaitingForSpawn;
                case HazardActorEncounterPlacementPreviewState.Retired:
                    return StageMapHazardPlacementPreviewVisualState.Retired;
                default:
                    return StageMapHazardPlacementPreviewVisualState.Active;
            }
        }

        private void DrawInactiveHazardPreviewPlacement(
            Vector3 world,
            int placementInstanceId,
            StageMapHazardPlacementPreviewVisualState state,
            bool drawLabel)
        {
            float radius = _document.Grid.CellSize * 0.15f;
            Vector3 markerWorld = world + Vector3.up * (_document.Grid.CellSize * 0.015f);
            Color markerColor = state == StageMapHazardPlacementPreviewVisualState.Retired
                ? new Color(0.55f, 0.58f, 0.62f, 0.22f)
                : new Color(0.58f, 0.72f, 0.78f, 0.24f);
            Handles.color = markerColor;
            Handles.DrawWireDisc(markerWorld, Vector3.up, radius);
            markerColor.a = 0.52f;
            Handles.color = markerColor;

            if (state == StageMapHazardPlacementPreviewVisualState.Retired)
            {
                Handles.DrawWireDisc(markerWorld, Vector3.up, radius * 0.48f);
            }
            else
            {
                Handles.DrawSolidDisc(markerWorld, Vector3.up, radius * 0.18f);
            }

            if (drawLabel)
            {
                string status = state == StageMapHazardPlacementPreviewVisualState.Retired ? "Retired" : "Waiting";
                Handles.Label(
                    world + Vector3.up * (_document.Grid.CellSize * 0.08f),
                    $"#{placementInstanceId} · {status}",
                    GetInactiveHazardPreviewLabelStyle());
            }
        }

        private GUIStyle GetInactiveHazardPreviewLabelStyle()
        {
            if (_inactiveHazardPreviewLabelStyle == null)
            {
                _inactiveHazardPreviewLabelStyle = new GUIStyle(EditorStyles.miniLabel);
                _inactiveHazardPreviewLabelStyle.normal.textColor = new Color(0.68f, 0.74f, 0.78f, 0.62f);
            }
            return _inactiveHazardPreviewLabelStyle;
        }

        private bool ShouldDrawAuthoringGizmoLabels()
        {
            return !IsHazardPreviewActive;
        }

        private bool ShouldLabelInactiveHazardPreviewPlacement(uint owningSourceStableId, int placementInstanceId)
        {
            StageMapSelection selection = _session.Selection;
            if (selection.Kind == StageMapSelectionKind.HazardActor)
            {
                return selection.OwningSourceStableId == owningSourceStableId
                    && selection.PlacementInstanceId == placementInstanceId;
            }

            if (selection.Kind != StageMapSelectionKind.HazardActorRule
                || selection.OwningSourceStableId != owningSourceStableId
                || !StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(
                    _document,
                    selection.OwningSourceStableId,
                    selection.RuleId,
                    out int ruleIndex))
            {
                return false;
            }

            int[] targets = _document.HazardActorOrchestrationRules[ruleIndex].TargetPlacementInstanceIds
                ?? Array.Empty<int>();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == placementInstanceId)
                    return true;
            }
            return false;
        }

        private static bool MatchesPreviewInput(
            HazardActorPreviewInput input,
            uint owningSourceStableId,
            int placementInstanceId)
        {
            return input.OwningSourceStableId == owningSourceStableId
                && input.PlacementInstanceId == placementInstanceId;
        }

        private void DrawPresentationLinks()
        {
            var links = _document.PresentationLinks;
            if (links == null)
                return;

            for (int i = 0; i < links.Length; i++)
            {
                if (!links[i].Active)
                    continue;

                Vector3 baseWorld = _document.Grid.Origin;
                if (links[i].PlacementMode == StagePresentationPlacementMode.LinkedToParent)
                {
                    if (links[i].LinkKind == StagePresentationLinkKind.Source)
                        TryGetRegionAnchorWorld(StageRegionKind.Source, links[i].LinkedStableId, out baseWorld);
                    else if (links[i].LinkKind == StagePresentationLinkKind.Deposit)
                        TryGetRegionAnchorWorld(StageRegionKind.Deposit, links[i].LinkedStableId, out baseWorld);
                }

                Vector3 world = baseWorld + links[i].Position;
                Handles.color = new Color(0.55f, 0.45f, 1f, 1f);
                Handles.DrawWireDisc(world, Vector3.up, _document.Grid.CellSize * 0.16f);
                if (ShouldDrawAuthoringGizmoLabels())
                    Handles.Label(world + Vector3.up * (_document.Grid.CellSize * 0.12f), $"Link {links[i].StableId}");
            }
        }

        private void DrawSelectedCell()
        {
            if (_session.Selection.Kind != StageMapSelectionKind.Cell
                || !_session.ShowGridLayer
                || !StageMapDocumentCommandUtility.TryGetCellIndex(_document, _session.Selection.Cell, out _))
                return;
            DrawCellOutline(_session.Selection.Cell, new Color(1f, 1f, 1f, 0.9f));
        }

        private void DrawCellOutline(Vector2Int cell, Color color)
        {
            var grid = _document.Grid;
            float x0 = grid.Origin.x + (cell.x * grid.CellSize);
            float z0 = grid.Origin.z + (cell.y * grid.CellSize);
            float x1 = x0 + grid.CellSize;
            float z1 = z0 + grid.CellSize;
            float y = grid.Origin.y + 0.02f;
            _selectedCellOutline[0] = new Vector3(x0, y, z0);
            _selectedCellOutline[1] = new Vector3(x1, y, z0);
            _selectedCellOutline[2] = new Vector3(x1, y, z1);
            _selectedCellOutline[3] = new Vector3(x0, y, z1);
            _selectedCellOutline[4] = _selectedCellOutline[0];
            Handles.color = color;
            Handles.DrawAAPolyLine(2f, _selectedCellOutline);
        }

        private void HandleSceneInput(Event evt)
        {
            if (evt == null || evt.button != 0 || evt.alt)
                return;
            if (evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag)
                return;
            Vector2Int cell;
            if (!TryGetSceneMouseWorld(evt.mousePosition, out Vector3 worldPosition)
                || !StageMapDocumentCommandUtility.TryWorldToCell(_document, worldPosition, out cell))
            {
                return;
            }

            bool continuous = evt.type == EventType.MouseDrag;
            if (TryExecuteSceneTool(cell, worldPosition, continuous))
            {
                evt.Use();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        public bool TryExecuteSceneTool(Vector2Int cell, Vector3 worldPosition, bool continuous)
        {
            if (!StageMapDocumentCommandUtility.TryGetCellIndex(_document, cell, out int cellIndex))
                return false;

            if (_session.SelectedTool == StageMapEditorToolMode.Select)
            {
                if (continuous)
                    return false;

                if (TrySelectElementAtWorldPosition(worldPosition))
                    return true;

                TrySelect(StageMapSelection.ForCell(cell), frame: false);
                return true;
            }

            if (_session.SelectedTool == StageMapEditorToolMode.PaintMovement)
            {
                return RecordAndApply(
                    "Paint Stage Movement",
                    () => StageMapEditorMutationUtility.TryPaintMovement(_session, cell, _session.MovementBrush, out _));
            }

            if (_session.SelectedTool == StageMapEditorToolMode.PaintRegion)
            {
                return RecordAndApply(
                    "Paint Stage Region",
                    () => StageMapEditorMutationUtility.TryPaintRegion(_session, cell, _session.RegionBrushKind, _session.RegionBrushStableId, out _));
            }

            if (continuous)
                return false;

            if (_session.SelectedTool == StageMapEditorToolMode.PlaceAnchor)
            {
                bool changed = RecordAndApply(
                    "Place Stage Region Anchor",
                    () => StageMapEditorMutationUtility.TryPlaceAnchor(_session, _session.AnchorBrushKind, _session.AnchorBrushStableId, worldPosition, out _));
                if (changed)
                    TrySelect(StageMapSelection.ForAnchor(_session.AnchorBrushKind, _session.AnchorBrushStableId), frame: false);
                return changed;
            }

            if (_session.SelectedTool == StageMapEditorToolMode.PlacePlayerStart)
            {
                bool changed = RecordAndApply(
                    "Place Stage Player Start",
                    () => StageMapEditorMutationUtility.TryPlacePlayerStart(_session, worldPosition, _session.PlayerStartYawDeg, out _));
                if (changed)
                    TrySelect(StageMapSelection.ForPlayerStart(), frame: false);
                return changed;
            }

            if (_session.SelectedTool == StageMapEditorToolMode.PlaceHazardActor)
            {
                bool changed = RecordAndApply(
                    "Place Stage Hazard Actor",
                    () => StageMapEditorMutationUtility.TryPlaceHazardActor(
                        _session,
                        _session.HazardActorSourceStableId,
                        _session.HazardActorArchetypePrefab,
                        worldPosition,
                        _session.HazardActorLocalYawDeg,
                        out _,
                        out _));
                if (changed)
                {
                    int index = (_document.HazardActorPlacements?.Length ?? 0) - 1;
                    if (index >= 0)
                    {
                        var placement = _document.HazardActorPlacements[index];
                        TrySelect(
                            StageMapSelection.ForHazard(placement.OwningSourceStableId, placement.PlacementInstanceId),
                            frame: false);
                    }
                }
                return changed;
            }

            if (_session.SelectedTool == StageMapEditorToolMode.PlacePresentationLink)
            {
                bool changed = RecordAndApply(
                    "Place Stage Presentation Link",
                    () => StageMapEditorMutationUtility.TryPlacePresentation(
                        _session,
                        _session.PresentationStableId,
                        _session.PresentationKey,
                        _session.PresentationPlacementMode,
                        _session.PresentationLinkKind,
                        _session.PresentationLinkedStableId,
                        worldPosition,
                        _session.PresentationEuler,
                        _session.PresentationScale,
                        out _));
                if (changed)
                {
                    TrySelect(StageMapSelection.ForPresentation(_session.PresentationStableId), frame: false);
                    _session.PresentationStableId = StageMapDocumentCommandUtility.GetNextPresentationStableId(_document);
                }
                return changed;
            }

            return false;
        }

        private bool RecordAndApply(string undoName, System.Func<bool> apply)
        {
            if (_document == null || apply == null)
                return false;

            Undo.RecordObject(_document, undoName);
            if (!apply())
                return false;

            EditorUtility.SetDirty(_document);
            RefreshAfterDocumentMutation(markDirty: true);
            return true;
        }

        private bool RecordSessionAndDocumentAndApply(string undoName, System.Func<bool> apply)
        {
            if (_document == null || apply == null)
                return false;

            string documentBefore = StageMapApplyPlanner.ComputeSignature(_document);
            Undo.RecordObjects(new[] { _document, _session.UndoTarget }, undoName);
            if (!apply())
                return false;

            bool documentChanged = documentBefore != StageMapApplyPlanner.ComputeSignature(_document);
            if (documentChanged)
            {
                EditorUtility.SetDirty(_document);
                RefreshAfterDocumentMutation(markDirty: true);
            }
            else
            {
                _session.ReconcileSelection(_document);
                SceneView.RepaintAll();
                Repaint();
            }
            return true;
        }

        private void RefreshAfterDocumentMutation(bool markDirty, bool claimPreviewOwner = true)
        {
            if (_document == null)
                return;

            _session.Dirty = markDirty;
            _applyPlan = null;
            _migrationPlan = null;
            _gridResizePlan = null;
            _pendingGrid = _document.Grid;
            ClampSelection();
            _observedDocumentDirtyCount = EditorUtility.GetDirtyCount(_document);
            _observedDocumentSignature = StageMapApplyPlanner.ComputeSignature(_document);
            InvalidateOverlayCache();
            Validate();
            QueueHazardPreviewReconcile(claimPreviewOwner);
        }

        public void ReconcileAfterExternalMutation()
        {
            RefreshAfterDocumentMutation(markDirty: true, claimPreviewOwner: false);
        }

        private void OnUndoRedo()
        {
            if (_document == null)
                return;
            int dirtyCount = EditorUtility.GetDirtyCount(_document);
            string signature = StageMapApplyPlanner.ComputeSignature(_document);
            if (dirtyCount != _observedDocumentDirtyCount || signature != _observedDocumentSignature)
            {
                RefreshAfterDocumentMutation(markDirty: true);
                return;
            }

            _session.ReconcileSelection(_document);
            SceneView.RepaintAll();
            Repaint();
        }

        private void SynchronizeExternalDocumentChanges()
        {
            if (_document == null)
                return;

            int dirtyCount = EditorUtility.GetDirtyCount(_document);
            if (dirtyCount == _observedDocumentDirtyCount)
                return;

            RefreshAfterDocumentMutation(markDirty: true, claimPreviewOwner: false);
        }

        private void OnProjectChanged()
        {
            if (_document != null && _hazardPreviewBinding.IsValid)
                QueueHazardPreviewReconcile(claimOwner: false);
        }

        private void ClampSelection()
        {
            _session.ReconcileSelection(_document);
        }

        private bool IsLayerLocked(StageMapEditorLayer layer)
        {
            return StageMapEditingPolicy.IsLayerLocked(_session, layer);
        }

        private bool TrySelectElementAtWorldPosition(Vector3 worldPosition)
        {
            float maxDistance = Mathf.Max(0.1f, _document.Grid.CellSize * 0.35f);
            if (!StageMapSelectionUtility.TryHitTest(_document, _session, worldPosition, maxDistance, out var selection))
                return false;

            return TrySelect(selection, frame: false);
        }


        private static int FindPresentationIndex(StageMapPresentationLinkData[] links, uint stableId)
        {
            if (links == null)
                return -1;

            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].StableId == stableId)
                    return i;
            }

            return -1;
        }

        private static int FindRegionIndex(StageMapRegionData[] regions, uint stableId)
        {
            if (regions == null)
                return -1;

            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i].StableId == stableId)
                    return i;
            }

            return -1;
        }

        private bool TryGetSceneMouseWorld(Vector2 mousePosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, _document.Grid.Origin.y, 0f));
            if (!plane.Raycast(ray, out float distance))
                return false;

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private bool TryGetRegionAnchorWorld(StageRegionKind kind, uint stableId, out Vector3 world)
        {
            world = default;
            var regions = kind == StageRegionKind.Source ? _document.SourceRegions : _document.DepositRegions;
            if (regions == null)
                return false;

            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i].StableId != stableId)
                    continue;

                world = StageMapDocumentCommandUtility.GetCellCenterWorld(_document, regions[i].AnchorCell)
                    + new Vector3(regions[i].AnchorOffset.x * _document.Grid.CellSize, 0f, regions[i].AnchorOffset.y * _document.Grid.CellSize);
                return true;
            }

            return false;
        }

        private void NavigateToIssue(int issueIndex)
        {
            if (issueIndex < 0 || issueIndex >= _issues.Count)
                return;

            _session.SelectedIssueIndex = issueIndex;
            StageMapIssueTarget target = issueIndex < _documentIssues.Count
                ? _documentIssues[issueIndex].Target
                : StageMapDocumentIssueMapper.ResolveTarget(_document, _issues[issueIndex]);
            TryNavigateToIssueTarget(target);
        }

        public bool TryNavigateToIssueTarget(StageMapIssueTarget target)
        {
            if (!StageMapIssueNavigationUtility.TryResolve(
                    _document,
                    target,
                    out var selection,
                    out _,
                    out UnityEngine.Object asset))
            {
                return false;
            }

            if (!TrySelect(selection, frame: true))
                return false;
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
            return true;
        }

        private void ApplyIssueFix(int issueIndex)
        {
            if (issueIndex < 0 || issueIndex >= _issues.Count)
                return;

            var issue = _issues[issueIndex];
            StageMapIssueTarget target = issueIndex < _documentIssues.Count
                ? _documentIssues[issueIndex].Target
                : StageMapDocumentIssueMapper.ResolveTarget(_document, issue);
            if (!StageMapDocumentFixUtility.TryBuildFixPreview(_document, issue, out var preview))
                return;

            if (!StageMapEditorMutationUtility.CanApplyFix(_session, issue, target, out var lockIssue))
            {
                EditorUtility.DisplayDialog("Apply Stage Map Fix", lockIssue.Message, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Apply Stage Map Fix", preview.Details, "Apply", "Cancel"))
                return;

            RecordAndApply(
                $"Apply Stage Map Fix: {preview.FixId}",
                () => StageMapEditorMutationUtility.TryApplyFix(_session, issue, target, out _));
        }

        private static uint DrawUIntField(string label, uint value)
        {
            long next = EditorGUILayout.LongField(label, value);
            if (next <= 0L)
                return 0u;
            if (next >= uint.MaxValue)
                return uint.MaxValue;
            return (uint)next;
        }

        private static int DrawPhaseIdField(int current, int[] commonPhaseIds)
        {
            if (commonPhaseIds == null || commonPhaseIds.Length == 0)
                return EditorGUILayout.IntField("Target Phase Id", current);

            string[] labels = commonPhaseIds.Select(x => x.ToString()).ToArray();
            int selected = Array.IndexOf(commonPhaseIds, current);
            if (selected < 0)
            {
                EditorGUILayout.HelpBox($"Current TargetPhaseId {current} is not common to all selected target archetypes.", MessageType.Error);
                return EditorGUILayout.IntField("Target Phase Id", current);
            }
            int next = EditorGUILayout.Popup("Target Phase Id", selected, labels);
            return commonPhaseIds[Mathf.Clamp(next, 0, commonPhaseIds.Length - 1)];
        }

        private static string JoinInts(int[] values)
        {
            return values == null || values.Length == 0 ? string.Empty : string.Join(",", values);
        }

        private static int[] ParseInts(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return Array.Empty<int>();
            string[] parts = csv.Split(',');
            var values = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int value) && value > 0)
                    values.Add(value);
            }
            return values.ToArray();
        }

        internal readonly struct StageMapHazardArchetypeSummary
        {
            public StageMapHazardArchetypeSummary(GameObject prefab, string name, int phaseCount, int patternCount, int issueCount)
            {
                Prefab = prefab;
                Name = name ?? string.Empty;
                PhaseCount = phaseCount;
                PatternCount = patternCount;
                IssueCount = issueCount;
            }

            public GameObject Prefab { get; }
            public string Name { get; }
            public int PhaseCount { get; }
            public int PatternCount { get; }
            public int IssueCount { get; }
        }

        private sealed class HazardArchetypePickerPopup : PopupWindowContent
        {
            private readonly GameObject _current;
            private readonly Action<GameObject> _select;
            private Vector2 _scroll;
            private string _search = string.Empty;
            private StageMapHazardArchetypeSummary[] _summaries;

            public HazardArchetypePickerPopup(GameObject current, Action<GameObject> select)
            {
                _current = current;
                _select = select;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(520f, 360f);
            }

            public override void OnOpen()
            {
                _summaries = BuildHazardArchetypeSummaries();
            }

            public override void OnGUI(Rect rect)
            {
                if (_summaries == null)
                    _summaries = BuildHazardArchetypeSummaries();

                EditorGUILayout.LabelField("Hazard Actor Archetypes", EditorStyles.boldLabel);
                _search = EditorGUILayout.TextField("Search", _search);
                DrawHeader();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                for (int i = 0; i < _summaries.Length; i++)
                {
                    var summary = _summaries[i];
                    if (!MatchesSearch(summary, _search))
                        continue;
                    DrawRow(summary);
                }
                EditorGUILayout.EndScrollView();
            }

            private void DrawHeader()
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Name", EditorStyles.miniBoldLabel, GUILayout.MinWidth(220f));
                    EditorGUILayout.LabelField("Phases", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
                    EditorGUILayout.LabelField("Patterns", EditorStyles.miniBoldLabel, GUILayout.Width(64f));
                    EditorGUILayout.LabelField("Issues", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                }
            }

            private void DrawRow(StageMapHazardArchetypeSummary summary)
            {
                bool selected = summary.Prefab == _current;
                using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
                {
                    if (GUILayout.Button(summary.Name, EditorStyles.miniButtonLeft, GUILayout.MinWidth(220f)))
                    {
                        _select?.Invoke(summary.Prefab);
                        editorWindow.Close();
                    }
                    EditorGUILayout.LabelField(summary.PhaseCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(56f));
                    EditorGUILayout.LabelField(summary.PatternCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(64f));
                    EditorGUILayout.LabelField(summary.IssueCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(52f));
                }
            }

            private static bool MatchesSearch(StageMapHazardArchetypeSummary summary, string search)
            {
                return string.IsNullOrWhiteSpace(search)
                    || summary.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
