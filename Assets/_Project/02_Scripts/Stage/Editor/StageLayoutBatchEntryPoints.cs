using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageLayoutBatchEntryPoints
    {
        private const string SampleScenePath = "Assets/_Project/01_Scenes/StageLayoutEditingSampleV1.unity";
        private const string PendingCommandPath = "Temp/StageLayoutBatchEntryPoints.command";
        private const string RegenerateCommand = "regenerate-stage-catalog";
        private static bool s_isExecuting;

        [InitializeOnLoadMethod]
        private static void InstallAutoRunner()
        {
            EditorApplication.update -= PollPendingCommand;
            EditorApplication.update += PollPendingCommand;
        }

        public static void RegenerateStageCatalogFromOpenScenes()
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            EnsureSamplePlayerStartMarkers();
            EditorSceneManager.SaveOpenScenes();
            StageLayoutCatalogGenerator.GenerateStageLayoutsFromOpenScenes(saveAssets: true);
            StageDefinitionGenerator.SyncDefinitionsFromOpenScenes(saveAssets: true);
            StageCatalogComposer.ComposeCatalogsFromOpenScenes(saveAssets: true);
            AssetDatabase.SaveAssets();
        }

        private static void PollPendingCommand()
        {
            if (s_isExecuting || !File.Exists(PendingCommandPath))
                return;

            string command = null;
            try
            {
                command = File.ReadAllText(PendingCommandPath).Trim();
                File.Delete(PendingCommandPath);
                if (!string.Equals(command, RegenerateCommand, StringComparison.Ordinal))
                    return;

                s_isExecuting = true;
                RegenerateStageCatalogFromOpenScenes();
                Debug.Log("[StageLayoutBatch] RegenerateStageCatalogFromOpenScenes completed from pending command.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageLayoutBatch] Pending command failed. command={command ?? "(null)"} error={ex}");
            }
            finally
            {
                s_isExecuting = false;
            }
        }

        private static void EnsureSamplePlayerStartMarkers()
        {
            var stageNodes = UnityEngine.Object.FindObjectsByType<StageLayoutStageMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < stageNodes.Length; i++)
            {
                var stageNode = stageNodes[i];
                if (stageNode == null)
                    continue;
                if (!stageNode.TryGetComponent(out StageGridAuthoring authoring) || authoring == null)
                    continue;

                NormalizeSampleAnchors(stageNode, authoring);

                if (!TryResolveSamplePlayerStart(stageNode.StageId, out var anchorCell, out var yawDeg))
                    continue;

                var markers = stageNode.GetComponentsInChildren<StagePlayerStartMarker>(includeInactive: true);
                StagePlayerStartMarker marker = null;
                for (int j = 0; j < markers.Length; j++)
                {
                    if (markers[j] == null)
                        continue;

                    if (marker == null)
                    {
                        marker = markers[j];
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(markers[j].gameObject);
                }

                if (marker == null)
                {
                    var go = new GameObject("PlayerStart");
                    go.transform.SetParent(stageNode.transform, worldPositionStays: false);
                    marker = go.AddComponent<StagePlayerStartMarker>();
                }

                marker.Active = true;
                marker.AnchorCell = anchorCell;
                marker.AnchorOffset = Vector2.zero;
                marker.YawDeg = yawDeg;

                var previewGrid = authoring.BuildEditorPreviewGridSpec();
                marker.transform.position = StageRuntimeGridUtility.GetAnchorWorldPosition(
                    in previewGrid,
                    new Unity.Mathematics.int2(anchorCell.x, anchorCell.y),
                    Unity.Mathematics.float2.zero,
                    authoring.GetEditorPreviewPlaneY());
                marker.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
                EditorUtility.SetDirty(marker);
                EditorUtility.SetDirty(marker.gameObject);
            }
        }

        private static void NormalizeSampleAnchors(StageLayoutStageMarker stageNode, StageGridAuthoring authoring)
        {
            if (stageNode == null || authoring == null)
                return;

            var anchors = stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var marker = anchors[i];
                if (marker == null)
                    continue;

                if (!TryResolveSampleAnchor(stageNode.StageId, marker.RegionKind, marker.StableId, out var anchorCell))
                    continue;

                marker.AnchorCell = anchorCell;
                var previewGrid = authoring.BuildEditorPreviewGridSpec();
                marker.transform.position = StageRuntimeGridUtility.GetAnchorWorldPosition(
                    in previewGrid,
                    new Unity.Mathematics.int2(anchorCell.x, anchorCell.y),
                    new Unity.Mathematics.float2(marker.AnchorOffset.x, marker.AnchorOffset.y),
                    authoring.GetEditorPreviewPlaneY());
                EditorUtility.SetDirty(marker);
                EditorUtility.SetDirty(marker.gameObject);
            }
        }

        private static bool TryResolveSamplePlayerStart(int stageId, out Vector2Int anchorCell, out float yawDeg)
        {
            switch (stageId)
            {
                case 1:
                    anchorCell = new Vector2Int(7, 5);
                    yawDeg = 90f;
                    return true;
                case 2:
                    anchorCell = new Vector2Int(12, 11);
                    yawDeg = 0f;
                    return true;
                case 3:
                    anchorCell = new Vector2Int(6, 0);
                    yawDeg = 180f;
                    return true;
                default:
                    anchorCell = default;
                    yawDeg = 0f;
                    return false;
            }
        }

        private static bool TryResolveSampleAnchor(int stageId, StageRegionKind regionKind, uint stableId, out Vector2Int anchorCell)
        {
            if (stageId == 2 && regionKind == StageRegionKind.Source && stableId == 1002u)
            {
                anchorCell = new Vector2Int(12, 12);
                return true;
            }

            if (stageId == 2 && regionKind == StageRegionKind.Deposit && stableId == 2001u)
            {
                anchorCell = new Vector2Int(16, 10);
                return true;
            }

            anchorCell = default;
            return false;
        }
    }
}
