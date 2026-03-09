using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// StageFlow/UI(GameObject) <-> Run Director Stage 상태(ECS) 브리지.
    /// - GO -> ECS: Start/Confirm 요청, Intro/Clear 연출 완료 게이트 반영
    /// - ECS -> GO: StageRunCompleted 신호를 이벤트로 전달
    /// </summary>
    public sealed class RunDirectorStageBridge : MonoBehaviour
    {
        private static readonly Dictionary<int, RunDirectorStageBridge> SceneOwnerByHandle = new();

        [Header("Bridge")]
        public bool LogBindWarnings = true;

        [Header("Events")]
        public UnityEvent OnStageRunCompleted;

        public event Action StageRunCompleted;

        private EntityManager _em;
        private Entity _stageStateEntity;
        private Entity _stageRequestEntity;
        private Entity _stageGateEntity;
        private Entity _stageSignalEntity;
        private bool _isBound;
        private bool _warnedBindFailure;
        private int _lastCompletedNotifiedFrame = -1;
        private bool _isSceneOwner;

        private void OnEnable()
        {
            TryAcquireSceneOwnership();
            TryBind();
        }

        private void OnDisable()
        {
            ReleaseSceneOwnership();
        }

        private void Update()
        {
            Tick();
        }

        public void Tick()
        {
            if (!TryBind())
                return;

            PublishStageCompletedEventIfNeeded();
        }

        public bool RequestStageStart()
        {
            if (!TryBind())
                return false;

            var request = _em.GetComponentData<RunDirectorStageRequestComponent>(_stageRequestEntity);
            request.StageStartRequested = 1;
            _em.SetComponentData(_stageRequestEntity, request);
            return true;
        }

        public bool TryGetStageState(out RunDirectorStageStateComponent stageState)
        {
            stageState = default;
            if (!TryBind())
                return false;

            if (_stageStateEntity == Entity.Null || !_em.Exists(_stageStateEntity))
                return false;

            stageState = _em.GetComponentData<RunDirectorStageStateComponent>(_stageStateEntity);
            return true;
        }

        public bool RequestConfirm()
        {
            if (!TryBind())
                return false;

            var request = _em.GetComponentData<RunDirectorStageRequestComponent>(_stageRequestEntity);
            request.ConfirmPressed = 1;
            _em.SetComponentData(_stageRequestEntity, request);
            return true;
        }

        public bool RequestForceClearReady()
        {
            if (!TryBind())
                return false;

            var request = _em.GetComponentData<RunDirectorStageRequestComponent>(_stageRequestEntity);
            request.ForceClearReadyRequested = 1;
            _em.SetComponentData(_stageRequestEntity, request);
            return true;
        }

        public bool SetIntroPresentationDone(bool done)
        {
            if (!TryBind())
                return false;

            var gate = _em.GetComponentData<RunDirectorStageGateComponent>(_stageGateEntity);
            gate.IntroPresentationDone = (byte)(done ? 1 : 0);
            _em.SetComponentData(_stageGateEntity, gate);
            return true;
        }

        public bool SetClearPresentationDone(bool done)
        {
            if (!TryBind())
                return false;

            var gate = _em.GetComponentData<RunDirectorStageGateComponent>(_stageGateEntity);
            gate.ClearPresentationDone = (byte)(done ? 1 : 0);
            _em.SetComponentData(_stageGateEntity, gate);
            return true;
        }

        private bool TryBind()
        {
            if (!EnsureSceneOwnership())
                return false;

            if (_isBound
                && _em.World.IsCreated
                && _em.Exists(_stageRequestEntity)
                && _em.Exists(_stageGateEntity)
                && _em.Exists(_stageSignalEntity))
            {
                return true;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                WarnBindFailureOnce("DefaultGameObjectInjectionWorld is not ready.");
                _isBound = false;
                return false;
            }

            _em = world.EntityManager;
            using var requestQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageRequestComponent>());
            using var gateQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageGateComponent>());
            using var signalQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageSignalComponent>());
            using var stateQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageStateComponent>());
            if (requestQuery.IsEmptyIgnoreFilter
                || gateQuery.IsEmptyIgnoreFilter
                || signalQuery.IsEmptyIgnoreFilter)
            {
                WarnBindFailureOnce("RunDirector stage singleton(s) were not found.");
                _isBound = false;
                return false;
            }

            _stageRequestEntity = ResolveFirstEntity(requestQuery);
            _stageGateEntity = ResolveFirstEntity(gateQuery);
            _stageSignalEntity = ResolveFirstEntity(signalQuery);
            _stageStateEntity = stateQuery.IsEmptyIgnoreFilter ? Entity.Null : ResolveFirstEntity(stateQuery);
            _isBound = _stageRequestEntity != Entity.Null && _stageGateEntity != Entity.Null && _stageSignalEntity != Entity.Null;
            if (_isBound)
                _warnedBindFailure = false;

            return _isBound;
        }

        private void PublishStageCompletedEventIfNeeded()
        {
            var signal = _em.GetComponentData<RunDirectorStageSignalComponent>(_stageSignalEntity);
            if (signal.StageRunCompleted == 0)
                return;

            int frame = Time.frameCount;
            if (_lastCompletedNotifiedFrame == frame)
                return;

            _lastCompletedNotifiedFrame = frame;
            StageRunCompleted?.Invoke();
            OnStageRunCompleted?.Invoke();

            signal.StageRunCompleted = 0;
            _em.SetComponentData(_stageSignalEntity, signal);
        }

        private void WarnBindFailureOnce(string message)
        {
            if (!LogBindWarnings || _warnedBindFailure)
                return;

            _warnedBindFailure = true;
            Debug.LogWarning($"[RunDirectorStageBridge] {message}");
        }

        private static Entity ResolveFirstEntity(EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        private bool TryAcquireSceneOwnership()
        {
            var scene = gameObject.scene;
            int sceneHandle = scene.IsValid() ? scene.handle : int.MinValue;
            if (!SceneOwnerByHandle.TryGetValue(sceneHandle, out var owner) || owner == null)
            {
                SceneOwnerByHandle[sceneHandle] = this;
                _isSceneOwner = true;
                return true;
            }

            if (owner == this)
            {
                _isSceneOwner = true;
                return true;
            }

            _isSceneOwner = false;
            string sceneName = scene.IsValid() ? scene.name : "(invalid-scene)";
            WarnBindFailureOnce($"Duplicate bridge in scene '{sceneName}'. Only one RunDirectorStageBridge is allowed per scene.");
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
    }
}
