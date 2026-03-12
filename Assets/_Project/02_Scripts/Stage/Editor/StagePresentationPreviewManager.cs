using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StagePresentationPreviewScope
    {
        SelectedStageOnly = 0,
        AllStages = 1,
    }

    [InitializeOnLoad]
    public static class StagePresentationPreviewManager
    {
        private sealed class PreviewStageRecord
        {
            public StageLayoutStageMarker Stage;
            public GameObject Root;
            public ulong Signature;
            public readonly List<GameObject> Instances = new();
        }

        private static readonly Dictionary<int, PreviewStageRecord> Records = new();
        private static StageLayoutStageMarker _activeStage;
        private static bool _dirty = true;
        private static StagePresentationPreviewScope _scope = StagePresentationPreviewScope.SelectedStageOnly;

        static StagePresentationPreviewManager()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            SyncActiveStageFromSelection();
        }

        public static StagePresentationPreviewScope Scope => _scope;

        public static int GetPreviewStageCount()
        {
            return Records.Count;
        }

        public static int GetPreviewInstanceCount()
        {
            int count = 0;
            foreach (var pair in Records)
            {
                count += pair.Value.Instances.Count;
            }

            return count;
        }

        public static bool HasPreviewForStage(StageLayoutStageMarker stageMarker)
        {
            return stageMarker != null && Records.ContainsKey(stageMarker.GetInstanceID());
        }

        public static GameObject FindPreviewInstance(StageLayoutStageMarker stageMarker, uint stableId)
        {
            if (stageMarker == null || !Records.TryGetValue(stageMarker.GetInstanceID(), out var record))
                return null;

            string expectedSuffix = $"_{stableId}";
            for (int i = 0; i < record.Instances.Count; i++)
            {
                var instance = record.Instances[i];
                if (instance != null && instance.name.EndsWith(expectedSuffix, StringComparison.Ordinal))
                    return instance;
            }

            return null;
        }

        public static void SetPreviewScope(StagePresentationPreviewScope scope)
        {
            if (_scope == scope)
                return;

            _scope = scope;
            _dirty = true;
        }

        public static void ForceRefresh()
        {
            SyncActiveStageFromSelection();
            _dirty = true;
            RefreshNow();
        }

        public static void ClearAllPreviews()
        {
            ClearAllPreviewRecords();
            _dirty = false;
        }

        [MenuItem("Tools/Project/Stage Presentation Preview/Selected Stage Only")]
        private static void SetSelectedStageOnlyMenu()
        {
            SetPreviewScope(StagePresentationPreviewScope.SelectedStageOnly);
            ForceRefresh();
        }

        [MenuItem("Tools/Project/Stage Presentation Preview/Selected Stage Only", true)]
        private static bool ValidateSelectedStageOnlyMenu()
        {
            Menu.SetChecked("Tools/Project/Stage Presentation Preview/Selected Stage Only", _scope == StagePresentationPreviewScope.SelectedStageOnly);
            return true;
        }

        [MenuItem("Tools/Project/Stage Presentation Preview/All Stages")]
        private static void SetAllStagesMenu()
        {
            SetPreviewScope(StagePresentationPreviewScope.AllStages);
            ForceRefresh();
        }

        [MenuItem("Tools/Project/Stage Presentation Preview/All Stages", true)]
        private static bool ValidateAllStagesMenu()
        {
            Menu.SetChecked("Tools/Project/Stage Presentation Preview/All Stages", _scope == StagePresentationPreviewScope.AllStages);
            return true;
        }

        [MenuItem("Tools/Project/Stage Presentation Preview/Refresh")]
        private static void RefreshMenu()
        {
            ForceRefresh();
        }

        private static void OnSelectionChanged()
        {
            SyncActiveStageFromSelection();
            _dirty = true;
        }

        private static void OnHierarchyChanged()
        {
            _dirty = true;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            _dirty = true;
        }

        private static void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            ClearAllPreviewRecords();
            _dirty = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearAllPreviewRecords();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SyncActiveStageFromSelection();
                _dirty = true;
            }
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_activeStage == null && _scope == StagePresentationPreviewScope.SelectedStageOnly)
                SyncActiveStageFromSelection();

            if (!_dirty && !NeedsRefreshFromSignature())
                return;

            RefreshNow();
        }

        private static void RefreshNow()
        {
            var targetStages = ResolveTargetStages();
            var targetIds = new HashSet<int>();
            for (int i = 0; i < targetStages.Count; i++)
                targetIds.Add(targetStages[i].GetInstanceID());

            var staleIds = new List<int>();
            foreach (var pair in Records)
            {
                if (!targetIds.Contains(pair.Key) || pair.Value.Stage == null)
                    staleIds.Add(pair.Key);
            }

            for (int i = 0; i < staleIds.Count; i++)
                DestroyRecord(staleIds[i]);

            for (int i = 0; i < targetStages.Count; i++)
            {
                var stage = targetStages[i];
                ulong signature = ComputeStageSignature(stage);
                int id = stage.GetInstanceID();
                if (!Records.TryGetValue(id, out var record) || record.Root == null || record.Signature != signature)
                    RebuildStage(stage, signature);
            }

            _dirty = false;
        }

        private static bool NeedsRefreshFromSignature()
        {
            var targetStages = ResolveTargetStages();
            var targetIds = new HashSet<int>();
            for (int i = 0; i < targetStages.Count; i++)
            {
                var stage = targetStages[i];
                targetIds.Add(stage.GetInstanceID());
                ulong signature = ComputeStageSignature(stage);
                if (!Records.TryGetValue(stage.GetInstanceID(), out var record) || record.Root == null || record.Signature != signature)
                    return true;
            }

            foreach (var pair in Records)
            {
                if (!targetIds.Contains(pair.Key))
                    return true;
            }

            return false;
        }

        private static List<StageLayoutStageMarker> ResolveTargetStages()
        {
            var stages = new List<StageLayoutStageMarker>();
            if (_scope == StagePresentationPreviewScope.AllStages)
            {
                var allStages = UnityEngine.Object.FindObjectsByType<StageLayoutStageMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Array.Sort(allStages, (a, b) => (a != null ? a.GetInstanceID() : 0).CompareTo(b != null ? b.GetInstanceID() : 0));
                for (int i = 0; i < allStages.Length; i++)
                {
                    if (allStages[i] != null)
                        stages.Add(allStages[i]);
                }

                return stages;
            }

            if (_activeStage != null)
                stages.Add(_activeStage);

            return stages;
        }

        private static void SyncActiveStageFromSelection()
        {
            var selected = Selection.activeTransform != null ? Selection.activeTransform.GetComponentInParent<StageLayoutStageMarker>() : null;
            if (selected != null)
            {
                _activeStage = selected;
                return;
            }

            if (_activeStage == null)
                return;

            if (_activeStage.gameObject == null)
                _activeStage = null;
        }

        private static ulong ComputeStageSignature(StageLayoutStageMarker stage)
        {
            if (stage == null)
                return 0ul;

            ulong hash = 1469598103934665603ul;
            AddHash(ref hash, stage.GetInstanceID());

            var markers = StagePresentationEditorUtility.GetPresentationMarkers(stage);
            Array.Sort(markers, (a, b) => (a != null ? a.GetInstanceID() : 0).CompareTo(b != null ? b.GetInstanceID() : 0));
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                AddHash(ref hash, marker.GetInstanceID());
                AddHash(ref hash, (int)marker.StableId);
                AddHash(ref hash, marker.Active ? 1 : 0);
                AddHash(ref hash, (int)marker.PlacementMode);
                AddHash(ref hash, marker.PresentationKey ?? string.Empty);
                AddHash(ref hash, marker.transform.position);
                AddHash(ref hash, marker.transform.rotation);
                AddHash(ref hash, marker.transform.localScale);

                if (StagePresentationEditorUtility.TryResolveCatalog(marker, out var catalog))
                {
                    AddHash(ref hash, catalog.GetInstanceID());
                    if (StagePresentationEditorUtility.TryResolveEntry(catalog, marker.PresentationKey, out var entry))
                    {
                        AddHash(ref hash, entry.Prefab != null ? entry.Prefab.GetInstanceID() : 0);
                        AddHash(ref hash, (int)entry.Usage);
                    }
                }

                if (StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out var linkKind, out var linkedStableId, out var linkedParent))
                {
                    AddHash(ref hash, (int)linkKind);
                    AddHash(ref hash, (int)linkedStableId);
                    AddHash(ref hash, linkedParent != null ? linkedParent.GetInstanceID() : 0);
                    if (linkedParent != null)
                    {
                        AddHash(ref hash, linkedParent.position);
                        AddHash(ref hash, linkedParent.rotation);
                        AddHash(ref hash, linkedParent.localScale);
                    }
                }
            }

            return hash;
        }

        private static void RebuildStage(StageLayoutStageMarker stage, ulong signature)
        {
            if (stage == null)
                return;

            int id = stage.GetInstanceID();
            DestroyRecord(id);

            var record = new PreviewStageRecord
            {
                Stage = stage,
                Signature = signature,
            };

            var markers = StagePresentationEditorUtility.GetPresentationMarkers(stage);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || !marker.Active)
                    continue;
                if (StagePresentationEditorUtility.HasTopologyOnSelf(marker))
                    continue;
                if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent
                    && !StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out _, out _, out _))
                    continue;
                if (marker.PlacementMode == StagePresentationPlacementMode.Standalone
                    && StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out _, out _, out _))
                    continue;
                if (!StagePresentationEditorUtility.TryResolveCatalog(marker, out var catalog))
                    continue;
                if (!StagePresentationEditorUtility.TryResolveEntry(catalog, marker.PresentationKey, out var entry))
                    continue;
                if (entry.Prefab == null)
                    continue;

                if (record.Root == null)
                {
                    record.Root = new GameObject($"__StagePresentationPreviewRoot__{stage.name}");
                    ApplyHideFlagsRecursive(record.Root);
                }

                var instance = UnityEngine.Object.Instantiate(entry.Prefab);
                instance.name = $"__Preview__{marker.PresentationKey}_{marker.StableId}";
                ApplyHideFlagsRecursive(instance);
                MakeInstanceInert(instance);
                instance.transform.SetParent(record.Root.transform, worldPositionStays: false);
                instance.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
                instance.transform.localScale = marker.transform.lossyScale;
                record.Instances.Add(instance);
            }

            if (record.Root != null)
                Records[id] = record;
        }

        private static void DestroyRecord(int stageId)
        {
            if (!Records.TryGetValue(stageId, out var record))
                return;

            Records.Remove(stageId);
            if (record.Root != null)
                UnityEngine.Object.DestroyImmediate(record.Root);
        }

        private static void ClearAllPreviewRecords()
        {
            var ids = new List<int>(Records.Keys);
            for (int i = 0; i < ids.Count; i++)
                DestroyRecord(ids[i]);
        }

        private static void ApplyHideFlagsRecursive(GameObject root)
        {
            if (root == null)
                return;

            foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var components = transform.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null)
                        components[i].hideFlags = HideFlags.HideAndDontSave;
                }
            }
        }

        private static void MakeInstanceInert(GameObject root)
        {
            if (root == null)
                return;

            var colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            var colliders2D = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
            for (int i = 0; i < colliders2D.Length; i++)
                colliders2D[i].enabled = false;

            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }

            var rigidbodies2D = root.GetComponentsInChildren<Rigidbody2D>(includeInactive: true);
            for (int i = 0; i < rigidbodies2D.Length; i++)
            {
                rigidbodies2D[i].simulated = false;
            }

            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particleSystems.Length; i++)
                particleSystems[i].Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var behaviours = root.GetComponentsInChildren<Behaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;
        }

        private static void AddHash(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211ul;
            }
        }

        private static void AddHash(ref ulong hash, string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                {
                    hash ^= 0u;
                    hash *= 1099511628211ul;
                    return;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 1099511628211ul;
                }
            }
        }

        private static void AddHash(ref ulong hash, Vector3 value)
        {
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.x));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.y));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.z));
        }

        private static void AddHash(ref ulong hash, Quaternion value)
        {
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.x));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.y));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.z));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value.w));
        }
    }
}
