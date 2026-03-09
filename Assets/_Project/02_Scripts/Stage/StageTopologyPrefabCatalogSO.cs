using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Topology Prefab Catalog", fileName = "stpc_")]
    public class StageTopologyPrefabCatalogSO : ScriptableObject
    {
        public GameObject SourceTemplatePrefab;
        public GameObject DepositTemplatePrefab;
        public GameObject ObstacleTemplatePrefab;
    }
}
