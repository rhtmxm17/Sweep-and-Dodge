using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    public static class ObstacleGeometryUtility
    {
        public static bool ContainsPointXZ(float2 point, in LocalTransform tx, in ObstacleGeometryComponent geometry)
        {
            float3 local3 = math.rotate(math.inverse(tx.Rotation), new float3(point.x - tx.Position.x, 0f, point.y - tx.Position.z));
            float2 local = new float2(local3.x, local3.z);

            return geometry.Shape switch
            {
                ObstacleShape.Circle => math.lengthsq(local) <= math.max(0f, geometry.Radius) * math.max(0f, geometry.Radius),
                ObstacleShape.Box => math.abs(local.x) <= math.max(0f, geometry.Size.x) * 0.5f
                    && math.abs(local.y) <= math.max(0f, geometry.Size.y) * 0.5f,
                _ => false,
            };
        }

        public static bool OverlapsCircleXZ(float2 center, float radius, in LocalTransform tx, in ObstacleGeometryComponent geometry)
        {
            float safeRadius = math.max(0f, radius);
            float3 local3 = math.rotate(math.inverse(tx.Rotation), new float3(center.x - tx.Position.x, 0f, center.y - tx.Position.z));
            float2 local = new float2(local3.x, local3.z);

            return geometry.Shape switch
            {
                ObstacleShape.Circle => math.lengthsq(local) <= math.square(math.max(0f, geometry.Radius) + safeRadius),
                ObstacleShape.Box => OverlapsCircleVsBox(local, safeRadius, geometry.Size),
                _ => false,
            };
        }

        private static bool OverlapsCircleVsBox(float2 center, float radius, float2 size)
        {
            float2 half = math.max(float2.zero, size * 0.5f);
            float2 closest = math.clamp(center, -half, half);
            float2 delta = center - closest;
            return math.lengthsq(delta) <= radius * radius;
        }
    }
}
