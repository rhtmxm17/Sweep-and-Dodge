using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// GO-only stage grid visual owner.
    /// - Applied stage의 StageLayoutSO.GridVisualPrefab을 런타임 위치에 생성한다.
    /// - runtime topology를 read-only로 조회하며 ECS write는 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageGridVisualController : MonoBehaviour
    {
        [Header("References")]
        public StageCatalogSO StageCatalog;
        public StageTopologyBridge TopologyBridge;

        private int _lastAppliedStageId;
        private bool _lastReady;
        private GameObject _currentInstance;

        public int LastAppliedStageId => _lastAppliedStageId;
        public bool LastReady => _lastReady;
        public GameObject CurrentInstance => _currentInstance;

        private void Reset()
        {
            if (TopologyBridge == null)
                TopologyBridge = GetComponent<StageTopologyBridge>();
        }

        private void OnEnable()
        {
            EnsureReferences();
        }

        private void Update()
        {
            Tick();
        }

        private void OnDisable()
        {
            ClearVisual();
        }

        public void Tick()
        {
            EnsureReferences();

            if (TopologyBridge == null || !TopologyBridge.TryGetTopologyState(out var topologyState))
            {
                if (_lastReady)
                    ClearVisual();

                return;
            }

            if (topologyState.Ready == 0 || topologyState.AppliedStageId <= 0)
            {
                if (_lastReady)
                    ClearVisual();

                return;
            }

            if (_lastReady && _lastAppliedStageId == topologyState.AppliedStageId)
                return;

            ApplyVisual(topologyState.AppliedStageId);
        }

        public void ClearVisual()
        {
            if (_currentInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_currentInstance);
                else
                    DestroyImmediate(_currentInstance);
            }

            _currentInstance = null;
            _lastReady = false;
            _lastAppliedStageId = 0;
        }

        private void ApplyVisual(int stageId)
        {
            ClearVisual();

            if (!TryResolveLayout(stageId, out var layout) || layout.GridVisualPrefab == null)
            {
                _lastAppliedStageId = stageId;
                _lastReady = true;
                return;
            }

            _currentInstance = Instantiate(layout.GridVisualPrefab, layout.Grid.Origin, layout.GridVisualPrefab.transform.rotation, transform);
            _currentInstance.name = layout.GridVisualPrefab.name;
            _lastAppliedStageId = stageId;
            _lastReady = true;
        }

        private bool TryResolveLayout(int stageId, out StageLayoutSO layout)
        {
            layout = null;
            if (StageCatalog == null || StageCatalog.Entries == null)
                return false;

            for (int i = 0; i < StageCatalog.Entries.Length; i++)
            {
                var entry = StageCatalog.Entries[i];
                if (!entry.Enabled || entry.Layout == null)
                    continue;
                if (entry.Layout.StageId != stageId)
                    continue;

                layout = entry.Layout;
                return true;
            }

            return false;
        }

        private void EnsureReferences()
        {
            if (TopologyBridge == null)
                TopologyBridge = GetComponent<StageTopologyBridge>();
            if (StageCatalog == null && TopologyBridge != null)
                StageCatalog = TopologyBridge.StageCatalog;
        }
    }
}
