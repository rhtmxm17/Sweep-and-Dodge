using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.ECS
{
    // Spawner
    public enum BulletPatternType : int
    {
        Ring = 0,
        Spiral = 1,
        Aimed = 2, // skeleton (targeting not wired in yet)
    }

    public struct BulletSpawnerComponent : IComponentData
    {
        public Entity BulletPrefab;

        public float FireInterval;
        public float Timer;

        public int BulletsPerShot;
        public float BulletSpeed;

        public float StartAngleRad;
        public float AngularSpeedRad;

        public float CurrentAngleRad; // 패턴에 사용

        public int Pattern; // BulletPatternType
    }
}