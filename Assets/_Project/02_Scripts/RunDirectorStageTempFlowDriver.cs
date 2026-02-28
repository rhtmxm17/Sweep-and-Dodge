using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// StageFlow/UI 부재 시 임시 호출 주체.
    /// - Idle 진입 시 시작 요청/인트로 완료 게이트를 브리지로 반영
    /// - ClearReady 진입 시 클리어 연출 완료/확인 입력을 브리지로 반영
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunDirectorStageBridge))]
    public sealed class RunDirectorStageTempFlowDriver : MonoBehaviour
    {
        [Header("References")]
        public RunDirectorStageBridge StageBridge;

        [Header("Auto Flow")]
        public bool AutoRequestStartInIdle = true;
        public bool AutoSetIntroDoneInIdle = true;
        public bool AutoSetClearDoneInClearReady = true;
        public bool AutoConfirmInClearReady = true;
        [Min(0f)] public float AutoConfirmDelaySec = 0f;

        [Header("Manual Input")]
        public bool EnableManualHotkeys = true;
        public KeyCode RequestStartKey = KeyCode.F5;
        public KeyCode SetIntroDoneKey = KeyCode.F6;
        public KeyCode SetClearDoneKey = KeyCode.F7;
        public KeyCode RequestConfirmKey = KeyCode.F8;

        [Header("Debug")]
        public bool LogStateChanges = false;
        public bool LogBridgeCallFailures = false;

        private EntityManager _em;
        private EntityQuery _stageStateQuery;
        private bool _isBound;
        private bool _warnedNoBridge;

        private RunDirectorStageStateId _lastState = (RunDirectorStageStateId)255;
        private uint _lastEnteredFrame = uint.MaxValue;
        private float _clearReadyElapsedSec;
        private bool _idleStartRequested;
        private bool _idleIntroDoneSet;
        private bool _clearDoneSet;
        private bool _clearConfirmRequested;

        private void Update()
        {
            if (!TryBind())
                return;

            EnsureBridgeReference();
            ProcessManualHotkeys();
            ProcessAutoFlow();
        }

        private bool TryBind()
        {
            if (_isBound)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            _em = world.EntityManager;
            _stageStateQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageStateComponent>());
            _isBound = true;
            return true;
        }

        private void EnsureBridgeReference()
        {
            if (StageBridge != null)
                return;

#if UNITY_2023_1_OR_NEWER
            StageBridge = FindFirstObjectByType<RunDirectorStageBridge>();
#else
            StageBridge = FindObjectOfType<RunDirectorStageBridge>();
#endif
            if (StageBridge != null || _warnedNoBridge)
                return;

            _warnedNoBridge = true;
            Debug.LogWarning("[RunDirectorStageTempFlowDriver] RunDirectorStageBridge was not found in scene.");
        }

        private void ProcessManualHotkeys()
        {
            if (!EnableManualHotkeys || StageBridge == null)
                return;

            if (Input.GetKeyDown(RequestStartKey))
                TryBridgeCall(StageBridge.RequestStageStart, "RequestStageStart");
            if (Input.GetKeyDown(SetIntroDoneKey))
                TryBridgeCall(() => StageBridge.SetIntroPresentationDone(true), "SetIntroPresentationDone");
            if (Input.GetKeyDown(SetClearDoneKey))
                TryBridgeCall(() => StageBridge.SetClearPresentationDone(true), "SetClearPresentationDone");
            if (Input.GetKeyDown(RequestConfirmKey))
                TryBridgeCall(StageBridge.RequestConfirm, "RequestConfirm");
        }

        private void ProcessAutoFlow()
        {
            if (StageBridge == null || _stageStateQuery.IsEmptyIgnoreFilter)
                return;

            var stage = _em.GetComponentData<RunDirectorStageStateComponent>(_stageStateQuery.GetSingletonEntity());
            bool entered = stage.State != _lastState || stage.EnteredFrame != _lastEnteredFrame;
            if (entered)
            {
                _lastState = stage.State;
                _lastEnteredFrame = stage.EnteredFrame;
                _clearReadyElapsedSec = 0f;
                _idleStartRequested = false;
                _idleIntroDoneSet = false;
                _clearDoneSet = false;
                _clearConfirmRequested = false;

                if (LogStateChanges)
                {
                    Debug.Log($"[RunDirectorStageTempFlowDriver] Stage entered {stage.State} (frame={stage.EnteredFrame})");
                }
            }

            switch (stage.State)
            {
                case RunDirectorStageStateId.Idle:
                    if (AutoSetIntroDoneInIdle && !_idleIntroDoneSet)
                    {
                        if (TryBridgeCall(() => StageBridge.SetIntroPresentationDone(true), "SetIntroPresentationDone"))
                            _idleIntroDoneSet = true;
                    }

                    if (AutoRequestStartInIdle && !_idleStartRequested)
                    {
                        if (TryBridgeCall(StageBridge.RequestStageStart, "RequestStageStart"))
                            _idleStartRequested = true;
                    }
                    break;

                case RunDirectorStageStateId.ClearReady:
                    _clearReadyElapsedSec = Mathf.Max(0f, stage.StateElapsedSec);
                    if (AutoSetClearDoneInClearReady && !_clearDoneSet)
                    {
                        if (TryBridgeCall(() => StageBridge.SetClearPresentationDone(true), "SetClearPresentationDone"))
                            _clearDoneSet = true;
                    }

                    if (AutoConfirmInClearReady
                        && !_clearConfirmRequested
                        && _clearReadyElapsedSec >= Mathf.Max(0f, AutoConfirmDelaySec))
                    {
                        if (TryBridgeCall(StageBridge.RequestConfirm, "RequestConfirm"))
                            _clearConfirmRequested = true;
                    }
                    break;
            }
        }

        private bool TryBridgeCall(System.Func<bool> call, string callName)
        {
            bool ok = call();
            if (!ok && LogBridgeCallFailures)
            {
                Debug.LogWarning($"[RunDirectorStageTempFlowDriver] Bridge call failed: {callName}");
            }

            return ok;
        }
    }
}
