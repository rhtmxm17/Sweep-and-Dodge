using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Bullet Definition", fileName = "bd_")]
    public class BulletDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private int definitionId = 0;
        public int DefinitionId => definitionId;

        [Header("Pool / Visual")]
        public GameObject Prefab;
        public int PoolSize = 1024;
        public BulletCaptureRuleId CaptureRule = BulletCaptureRuleId.StandardCollectible;

#if UNITY_EDITOR
        public void Editor_SetDefinitionId(int newId)
        {
            definitionId = newId;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
