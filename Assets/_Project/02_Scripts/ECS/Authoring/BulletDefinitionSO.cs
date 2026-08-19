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

        [Header("Gameplay")]
        public float Speed = 0.5f;
        public float Lifetime = 4.0f;
        public float Radius = 0.05f;
        public int ScoreValue = 1;

        [Header("Movement")]
        public BulletMovementFamilyId MovementFamily = BulletMovementFamilyId.Linear;
        public BulletDampedLinearDefinition DampedLinear = new BulletDampedLinearDefinition
        {
            DampingPerSec = 1f,
            StopSpeedThreshold = 0.1f,
        };
        public BulletHomingLiteDefinition HomingLite = new BulletHomingLiteDefinition
        {
            TurnRateDegPerSec = 90f,
            MaxAcquireDistance = 10f,
            MinRetargetDistance = 0.25f,
        };

#if UNITY_EDITOR
        public void Editor_SetDefinitionId(int newId)
        {
            definitionId = newId;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
