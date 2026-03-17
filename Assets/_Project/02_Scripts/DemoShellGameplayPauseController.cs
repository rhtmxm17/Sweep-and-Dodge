using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Scene-local aggregate owner for gameplay pause requests.
    /// - Requester가 pause reason/flags를 acquire/release하면 same-frame snapshot을 계산한다.
    /// - 실제 ECS fixed tick 반영은 GameplayPauseApplySystem 단일 writer가 수행한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoShellGameplayPauseController : MonoBehaviour
    {
        private static readonly Dictionary<int, DemoShellGameplayPauseController> SceneOwnerByHandle = new();

        [Header("Policy")]
        public bool LogBindWarnings = true;

        private readonly List<ActivePauseEntry> _activeEntries = new(4);

        private GameplayPauseSnapshot _currentSnapshot;
        private int _nextHandleId = 1;
        private uint _nextVersionToken = 1;
        private bool _warnedBindFailure;
        private bool _isSceneOwner;

        private struct ActivePauseEntry
        {
            public int Id;
            public GameplayPauseReasonId Reason;
            public GameplayPauseFlags Flags;
            public uint VersionToken;
        }

        public GameplayPauseSnapshot CurrentSnapshot => _currentSnapshot;

        public static bool TryGetActiveOwner(out DemoShellGameplayPauseController controller)
        {
            controller = null;

            var activeScene = SceneManager.GetActiveScene();
            int activeSceneHandle = activeScene.IsValid() ? activeScene.handle : int.MinValue;
            if (TryGetOwnerForSceneHandle(activeSceneHandle, out controller))
                return true;

            foreach (var pair in SceneOwnerByHandle)
            {
                if (pair.Key == activeSceneHandle)
                    continue;

                var candidate = pair.Value;
                if (candidate == null || !candidate._isSceneOwner)
                    continue;

                controller = candidate;
                return true;
            }

            return false;
        }

        private void OnEnable()
        {
            TryAcquireSceneOwnership();
            RecalculateSnapshot();
        }

        private void OnDisable()
        {
            ResetState();
            ReleaseSceneOwnership();
        }

        private void OnDestroy()
        {
            ResetState();
            ReleaseSceneOwnership();
        }

        public GameplayPauseHandle Acquire(GameplayPauseReasonId reason, GameplayPauseFlags flags)
        {
            if (!EnsureSceneOwnership())
                return GameplayPauseHandle.Invalid;

            var handle = new GameplayPauseHandle
            {
                Id = _nextHandleId++,
                Reason = reason,
                Flags = flags,
                VersionToken = _nextVersionToken++,
            };

            _activeEntries.Add(new ActivePauseEntry
            {
                Id = handle.Id,
                Reason = handle.Reason,
                Flags = handle.Flags,
                VersionToken = handle.VersionToken,
            });

            RecalculateSnapshot();
            return handle;
        }

        public bool Release(in GameplayPauseHandle handle)
        {
            if (!EnsureSceneOwnership() || !handle.IsValid)
                return false;

            for (int i = 0; i < _activeEntries.Count; i++)
            {
                ActivePauseEntry entry = _activeEntries[i];
                if (entry.Id != handle.Id || entry.VersionToken != handle.VersionToken)
                    continue;

                _activeEntries.RemoveAt(i);
                RecalculateSnapshot();
                return true;
            }

            return false;
        }

        public bool HasActiveReason(GameplayPauseReasonId reason)
        {
            for (int i = 0; i < _activeEntries.Count; i++)
            {
                if (_activeEntries[i].Reason == reason)
                    return true;
            }

            return false;
        }

        private void RecalculateSnapshot()
        {
            var next = new GameplayPauseSnapshot
            {
                Version = _currentSnapshot.Version + 1u,
                ActiveHandleCount = _activeEntries.Count,
            };

            for (int i = 0; i < _activeEntries.Count; i++)
            {
                ActivePauseEntry entry = _activeEntries[i];
                next.ReasonMask |= 1u << (int)entry.Reason;
                next.IsSimulationPaused |= (entry.Flags & GameplayPauseFlags.PauseSimulation) != 0;
                next.IsGameplayInputBlocked |= (entry.Flags & GameplayPauseFlags.BlockGameplayInput) != 0;
                next.IsPresentationInputExclusive |= (entry.Flags & GameplayPauseFlags.ExclusivePresentationInput) != 0;
                next.IsPauseMenuOpenBlocked |= (entry.Flags & GameplayPauseFlags.BlockPauseMenuOpen) != 0;
            }

            _currentSnapshot = next;
        }

        private void ResetState()
        {
            _activeEntries.Clear();
            RecalculateSnapshot();
        }

        private bool TryAcquireSceneOwnership()
        {
            var scene = gameObject.scene;
            int sceneHandle = scene.IsValid() ? scene.handle : int.MinValue;
            if (!SceneOwnerByHandle.TryGetValue(sceneHandle, out var owner) || owner == null)
            {
                SceneOwnerByHandle[sceneHandle] = this;
                _isSceneOwner = true;
                _warnedBindFailure = false;
                return true;
            }

            if (owner == this)
            {
                _isSceneOwner = true;
                _warnedBindFailure = false;
                return true;
            }

            _isSceneOwner = false;
            WarnBindFailureOnce(scene.IsValid() ? scene.name : "(invalid-scene)");
            return false;
        }

        private bool EnsureSceneOwnership()
        {
            if (_isSceneOwner)
                return true;

            return TryAcquireSceneOwnership();
        }

        private void ReleaseSceneOwnership()
        {
            if (!_isSceneOwner)
                return;

            var scene = gameObject.scene;
            int sceneHandle = scene.IsValid() ? scene.handle : int.MinValue;
            if (SceneOwnerByHandle.TryGetValue(sceneHandle, out var owner) && owner == this)
                SceneOwnerByHandle.Remove(sceneHandle);

            _isSceneOwner = false;
        }

        private void WarnBindFailureOnce(string sceneName)
        {
            if (!LogBindWarnings || _warnedBindFailure)
                return;

            _warnedBindFailure = true;
            Debug.LogWarning($"[DemoShellGameplayPauseController] Duplicate controller in scene '{sceneName}'. Only one DemoShellGameplayPauseController is allowed per scene.");
        }

        private static bool TryGetOwnerForSceneHandle(int sceneHandle, out DemoShellGameplayPauseController controller)
        {
            controller = null;
            if (!SceneOwnerByHandle.TryGetValue(sceneHandle, out var owner) || owner == null || !owner._isSceneOwner)
                return false;

            controller = owner;
            return true;
        }
    }
}
