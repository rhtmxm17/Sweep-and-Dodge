using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DemoShellFlowControllerStageCatalogTests
    {
        private static readonly MethodInfo EnsureStageProfilesMethod = typeof(DemoShellFlowController)
            .GetMethod("EnsureStageProfiles", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void EnsureStageProfiles_LoadsFromCatalog_WithEntryOrderAndEnabledFilter()
        {
            var created = new List<ScriptableObject>();
            GameObject go = null;
            try
            {
                var stage3Definition = CreateDefinition(created, stageId: 3, displayName: "Gamma", isFinal: false, timeLimitSec: 95f);
                var stage2Definition = CreateDefinition(created, stageId: 2, displayName: "Beta", isFinal: false, timeLimitSec: 70f);
                var stage7Definition = CreateDefinition(created, stageId: 7, displayName: string.Empty, isFinal: true, timeLimitSec: 140f);

                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_03",
                        Definition = stage3Definition,
                        Layout = CreateLayout(created, 3),
                    },
                    new StageCatalogEntry
                    {
                        Enabled = false,
                        EntryKey = "stage_02",
                        Definition = stage2Definition,
                        Layout = CreateLayout(created, 2),
                    },
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_07",
                        Definition = stage7Definition,
                        Layout = CreateLayout(created, 7),
                    },
                };
                created.Add(catalog);

                go = new GameObject("DemoShellFlowController_Test");
                go.SetActive(false);
                var controller = go.AddComponent<DemoShellFlowController>();
                var bridge = go.GetComponent<RunDirectorStageBridge>();
                if (bridge != null)
                    bridge.LogBindWarnings = false;

                controller.StageCatalog = catalog;
                controller.StageProfiles = new[]
                {
                    new DemoShellStageProfile
                    {
                        StageId = 999,
                        DisplayName = "Legacy",
                        IsFinalStage = false,
                        StageTimeLimitSec = 1f,
                    }
                };

                InvokeEnsureStageProfiles(controller);

                Assert.That(controller.StageProfiles, Is.Not.Null);
                Assert.That(controller.StageProfiles.Length, Is.EqualTo(2));

                Assert.That(controller.StageProfiles[0].StageId, Is.EqualTo(3));
                Assert.That(controller.StageProfiles[0].DisplayName, Is.EqualTo("Gamma"));
                Assert.That(controller.StageProfiles[0].IsFinalStage, Is.False);
                Assert.That(controller.StageProfiles[0].StageTimeLimitSec, Is.EqualTo(95f).Within(1e-4f));

                Assert.That(controller.StageProfiles[1].StageId, Is.EqualTo(7));
                Assert.That(controller.StageProfiles[1].DisplayName, Is.EqualTo("Stage 7"));
                Assert.That(controller.StageProfiles[1].IsFinalStage, Is.True);
                Assert.That(controller.StageProfiles[1].StageTimeLimitSec, Is.EqualTo(140f).Within(1e-4f));
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
                DestroyAll(created);
            }
        }

        [Test]
        public void EnsureStageProfiles_WhenCatalogIsMissing_FallsBackToSerializedProfiles()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("DemoShellFlowController_Fallback");
                go.SetActive(false);
                var controller = go.AddComponent<DemoShellFlowController>();
                var bridge = go.GetComponent<RunDirectorStageBridge>();
                if (bridge != null)
                    bridge.LogBindWarnings = false;

                controller.StageCatalog = null;
                controller.StageProfiles = new[]
                {
                    new DemoShellStageProfile
                    {
                        StageId = 42,
                        DisplayName = "Custom Stage",
                        IsFinalStage = true,
                        StageTimeLimitSec = 60f,
                    },
                    new DemoShellStageProfile
                    {
                        StageId = 0,
                        DisplayName = "",
                        IsFinalStage = false,
                        StageTimeLimitSec = 0f,
                    },
                };

                InvokeEnsureStageProfiles(controller);

                Assert.That(controller.StageProfiles.Length, Is.EqualTo(2));
                Assert.That(controller.StageProfiles[0].StageId, Is.EqualTo(42));
                Assert.That(controller.StageProfiles[0].DisplayName, Is.EqualTo("Custom Stage"));
                Assert.That(controller.StageProfiles[0].StageTimeLimitSec, Is.EqualTo(60f).Within(1e-4f));

                Assert.That(controller.StageProfiles[1].StageId, Is.EqualTo(2));
                Assert.That(controller.StageProfiles[1].DisplayName, Is.EqualTo("Stage 2"));
                Assert.That(controller.StageProfiles[1].StageTimeLimitSec, Is.EqualTo(180f).Within(1e-4f));
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RequestSelectStageById_IssuesTopologyApplyAndStageStartInSameStep()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            var created = new List<ScriptableObject>();
            try
            {
                world = new World("DemoShellFlowController_StageEntryWorld");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(StageTopologyRequestComponent));
                em.CreateEntity(typeof(StageTopologyStateComponent));
                em.CreateEntity(typeof(StageTopologyPrefabCatalogComponent));
                em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                var gateEntity = em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.SetComponentData(gateEntity, default(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                var definition = CreateDefinition(created, stageId: 2, displayName: "Stage 2", isFinal: false, timeLimitSec: 60f);
                var layout = CreateLayout(created, 2);
                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_02",
                        Definition = definition,
                        Layout = layout,
                    },
                };
                created.Add(catalog);

                go = new GameObject("DemoShellFlowController_StageEntry");
                go.SetActive(false);
                var controller = go.AddComponent<DemoShellFlowController>();
                controller.StageCatalog = catalog;
                controller.StageProfiles = new[]
                {
                    new DemoShellStageProfile
                    {
                        StageId = 2,
                        DisplayName = "Stage 2",
                        IsFinalStage = false,
                        StageTimeLimitSec = 60f,
                    },
                };

                var stageBridge = go.GetComponent<RunDirectorStageBridge>();
                var topologyBridge = go.GetComponent<StageTopologyBridge>();
                stageBridge.LogBindWarnings = false;
                topologyBridge.LogBindWarnings = false;

                go.SetActive(true);

                Assert.That(controller.RequestStartFromTitle(), Is.True);
                Assert.That(controller.RequestSelectStageById(2), Is.True);

                var topologyRequest = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>())
                    .GetSingleton<StageTopologyRequestComponent>();
                var stageRequest = em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageRequestComponent>())
                    .GetSingleton<RunDirectorStageRequestComponent>();
                var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);

                Assert.That(controller.CurrentScreen, Is.EqualTo(DemoShellScreenId.StagePlay));
                Assert.That(controller.CurrentStageId, Is.EqualTo(2));
                Assert.That(controller.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.Starting));
                Assert.That(topologyRequest.RequestedStageId, Is.EqualTo(2));
                Assert.That(topologyRequest.ApplyRequested, Is.EqualTo(1));
                Assert.That(stageRequest.StageStartRequested, Is.EqualTo(1));
                Assert.That(gate.IntroPresentationDone, Is.EqualTo(1));
                Assert.That(gate.ClearPresentationDone, Is.EqualTo(0));
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
                DestroyAll(created);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        private static void InvokeEnsureStageProfiles(DemoShellFlowController controller)
        {
            Assert.That(EnsureStageProfilesMethod, Is.Not.Null, "EnsureStageProfiles method not found.");
            EnsureStageProfilesMethod.Invoke(controller, null);
        }

        private static StageDefinitionSO CreateDefinition(
            List<ScriptableObject> created,
            int stageId,
            string displayName,
            bool isFinal,
            float timeLimitSec)
        {
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = stageId;
            definition.DisplayName = displayName;
            definition.IsFinalStage = isFinal;
            definition.StageTimeLimitSec = timeLimitSec;
            definition.SourceBindings = Array.Empty<StageSourceBinding>();
            created.Add(definition);
            return definition;
        }

        private static StageLayoutSO CreateLayout(List<ScriptableObject> created, int stageId)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = stageId;
            layout.Sources = Array.Empty<StageSourceLayoutData>();
            layout.Deposits = Array.Empty<StageDepositLayoutData>();
            layout.Obstacles = Array.Empty<StageObstacleLayoutData>();
            layout.Presentations = Array.Empty<StagePresentationLayoutData>();
            created.Add(layout);
            return layout;
        }

        private static void DestroyAll(List<ScriptableObject> created)
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    UnityEngine.Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }
    }
}
