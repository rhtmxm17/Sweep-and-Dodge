using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public sealed class BulletDebugHudBridge : MonoBehaviour
    {
        [Header("HUD")]
        public bool ShowHud = true;
        public Rect HudRect = new Rect(12f, 12f, 360f, 260f);

        [Header("Stress Preset")]
        public int BurstCount = 100000;
        public int SustainFrames = 300;
        public int SustainPerFrame = 2000;
        public int PreferredBulletTypeKey = -1;

        private EntityManager _em;
        private EntityQuery _hudQuery;
        private EntityQuery _stressQuery;
        private bool _isBound;

        private void Update()
        {
            TryBind();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !ShowHud)
                return;

            if (!TryBind())
                return;
            if (_hudQuery.IsEmptyIgnoreFilter || _stressQuery.IsEmptyIgnoreFilter)
                return;

            var hud = _em.GetComponentData<DebugHudMetricsComponent>(_hudQuery.GetSingletonEntity());
            var stress = _em.GetComponentData<StressSwitchStateComponent>(_stressQuery.GetSingletonEntity());

            GUILayout.BeginArea(HudRect, GUI.skin.box);
            GUILayout.Label("[Bullet Debug HUD]");
            GUILayout.Label($"frameTime(ms): {hud.FrameTimeMs:0.00}");
            GUILayout.Label($"active: {hud.ActiveBullets}");
            GUILayout.Label($"spawn/despawn: {hud.SpawnedThisFrame} / {hud.DespawnedThisFrame}");
            GUILayout.Label($"pending: {hud.PendingBacklog}");
            GUILayout.Label($"deferred(budget/pool): {hud.DeferredByBudget} / {hud.DeferredByPool}");
            GUILayout.Label($"drop/expire: {hud.DroppedThisFrame} / {hud.ExpiredThisFrame}");
            GUILayout.Space(6f);
            GUILayout.Label($"sustainRemaining: {stress.RemainingFrames}");

            if (GUILayout.Button($"Stress Burst x{BurstCount}"))
                RequestBurst();
            if (GUILayout.Button($"Stress Sustain {SustainFrames}f x{SustainPerFrame}"))
                RequestSustain();
            if (GUILayout.Button("Stop Sustain"))
                RequestStopSustain();

            GUILayout.EndArea();
        }

        private bool TryBind()
        {
            if (_isBound)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            _em = world.EntityManager;
            _hudQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<DebugHudMetricsComponent>());
            _stressQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<StressSwitchStateComponent>());
            _isBound = true;
            return true;
        }

        private void RequestBurst()
        {
            var e = _stressQuery.GetSingletonEntity();
            var state = _em.GetComponentData<StressSwitchStateComponent>(e);
            state.Mode = (byte)StressSwitchModeId.BurstOnce;
            state.BurstCount = Mathf.Max(0, BurstCount);
            state.PreferredBulletTypeKey = PreferredBulletTypeKey;
            state.RequestExecute = 1;
            _em.SetComponentData(e, state);
        }

        private void RequestSustain()
        {
            var e = _stressQuery.GetSingletonEntity();
            var state = _em.GetComponentData<StressSwitchStateComponent>(e);
            state.Mode = (byte)StressSwitchModeId.Sustain;
            state.SustainFrames = Mathf.Max(0, SustainFrames);
            state.SustainPerFrame = Mathf.Max(0, SustainPerFrame);
            state.PreferredBulletTypeKey = PreferredBulletTypeKey;
            state.RequestExecute = 1;
            _em.SetComponentData(e, state);
        }

        private void RequestStopSustain()
        {
            var e = _stressQuery.GetSingletonEntity();
            var state = _em.GetComponentData<StressSwitchStateComponent>(e);
            state.Mode = (byte)StressSwitchModeId.StopSustain;
            state.RequestExecute = 1;
            _em.SetComponentData(e, state);
        }
    }
}
