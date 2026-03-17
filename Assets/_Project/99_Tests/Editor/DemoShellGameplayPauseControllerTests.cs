using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DemoShellGameplayPauseControllerTests
    {
        [Test]
        public void DemoShellGameplayPauseController_Acquire_SetsSnapshotImmediately()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("PauseController_Test");
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;

                GameplayPauseHandle handle = controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);

                Assert.That(handle.IsValid, Is.True);
                Assert.That(controller.CurrentSnapshot.IsSimulationPaused, Is.True);
                Assert.That(controller.CurrentSnapshot.IsGameplayInputBlocked, Is.True);
                Assert.That(controller.CurrentSnapshot.ActiveHandleCount, Is.EqualTo(1));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DemoShellGameplayPauseController_MultipleHandles_AreOrAggregated()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("PauseController_Test");
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;

                controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);
                controller.Acquire(
                    GameplayPauseReasonId.DialogueGate,
                    GameplayPauseFlags.BlockPauseMenuOpen | GameplayPauseFlags.ExclusivePresentationInput);

                GameplayPauseSnapshot snapshot = controller.CurrentSnapshot;
                Assert.That(snapshot.IsSimulationPaused, Is.True);
                Assert.That(snapshot.IsGameplayInputBlocked, Is.True);
                Assert.That(snapshot.IsPresentationInputExclusive, Is.True);
                Assert.That(snapshot.IsPauseMenuOpenBlocked, Is.True);
                Assert.That(snapshot.ActiveHandleCount, Is.EqualTo(2));
                Assert.That(controller.HasActiveReason(GameplayPauseReasonId.PauseMenu), Is.True);
                Assert.That(controller.HasActiveReason(GameplayPauseReasonId.DialogueGate), Is.True);
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DemoShellGameplayPauseController_ReleaseOneHandle_KeepsRemainingFlags()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("PauseController_Test");
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;

                GameplayPauseHandle menuHandle = controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);
                controller.Acquire(
                    GameplayPauseReasonId.DialogueGate,
                    GameplayPauseFlags.BlockPauseMenuOpen);

                Assert.That(controller.Release(menuHandle), Is.True);

                GameplayPauseSnapshot snapshot = controller.CurrentSnapshot;
                Assert.That(snapshot.IsSimulationPaused, Is.False);
                Assert.That(snapshot.IsGameplayInputBlocked, Is.False);
                Assert.That(snapshot.IsPauseMenuOpenBlocked, Is.True);
                Assert.That(snapshot.ActiveHandleCount, Is.EqualTo(1));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DemoShellGameplayPauseController_InvalidHandleRelease_ReturnsFalse()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("PauseController_Test");
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;

                Assert.That(controller.Release(GameplayPauseHandle.Invalid), Is.False);

                var invalid = new GameplayPauseHandle
                {
                    Id = 999,
                    Reason = GameplayPauseReasonId.Debug,
                    Flags = GameplayPauseFlags.PauseSimulation,
                    VersionToken = 1,
                };

                Assert.That(controller.Release(invalid), Is.False);
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DemoShellGameplayPauseController_OnDisable_ClearsAllState()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("PauseController_Test");
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;
                controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);

                go.SetActive(false);
                typeof(DemoShellGameplayPauseController)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .Invoke(controller, null);

                GameplayPauseSnapshot snapshot = controller.CurrentSnapshot;
                Assert.That(snapshot.IsSimulationPaused, Is.False);
                Assert.That(snapshot.IsGameplayInputBlocked, Is.False);
                Assert.That(snapshot.ActiveHandleCount, Is.EqualTo(0));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DemoShellGameplayPauseController_DuplicateController_IsNotOwner()
        {
            GameObject ownerGo = null;
            GameObject duplicateGo = null;
            try
            {
                ownerGo = new GameObject("PauseController_Owner");
                duplicateGo = new GameObject("PauseController_Duplicate");

                var owner = ownerGo.AddComponent<DemoShellGameplayPauseController>();
                owner.LogBindWarnings = false;
                var duplicate = duplicateGo.AddComponent<DemoShellGameplayPauseController>();
                duplicate.LogBindWarnings = false;

                GameplayPauseHandle ownerHandle = owner.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation);
                GameplayPauseHandle duplicateHandle = duplicate.Acquire(
                    GameplayPauseReasonId.Debug,
                    GameplayPauseFlags.BlockGameplayInput);

                Assert.That(ownerHandle.IsValid, Is.True);
                Assert.That(duplicateHandle.IsValid, Is.False);
                Assert.That(owner.CurrentSnapshot.ActiveHandleCount, Is.EqualTo(1));
                Assert.That(duplicate.CurrentSnapshot.ActiveHandleCount, Is.EqualTo(0));
            }
            finally
            {
                if (duplicateGo != null)
                    Object.DestroyImmediate(duplicateGo);
                if (ownerGo != null)
                    Object.DestroyImmediate(ownerGo);
            }
        }
    }
}
