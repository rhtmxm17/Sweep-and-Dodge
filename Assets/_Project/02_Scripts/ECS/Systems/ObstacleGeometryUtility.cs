using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    public static class Shape2DUtility
    {
        public static Shape2DComponent Normalize(in Shape2DComponent shape)
        {
            var normalized = shape;
            normalized.Radius = math.max(0f, normalized.Radius);
            normalized.Size = math.max(float2.zero, normalized.Size);
            return normalized;
        }

        public static float ComputeArea(in Shape2DComponent shape)
        {
            var normalized = Normalize(in shape);
            if (normalized.Kind == Shape2DKind.Rectangle)
                return normalized.Size.x * normalized.Size.y;

            return math.PI * normalized.Radius * normalized.Radius;
        }

        public static float2 ComputeHalfExtents(in Shape2DComponent shape)
        {
            var normalized = Normalize(in shape);
            if (normalized.Kind == Shape2DKind.Rectangle)
                return normalized.Size * 0.5f;

            return new float2(normalized.Radius, normalized.Radius);
        }

        public static bool ContainsPointXZ(float2 point, in LocalTransform tx, in Shape2DComponent shape)
        {
            float2 local = WorldToLocalXZ(point, in tx);
            var normalized = Normalize(in shape);

            if (normalized.Kind == Shape2DKind.Rectangle)
            {
                float2 half = normalized.Size * 0.5f;
                return math.abs(local.x) <= half.x && math.abs(local.y) <= half.y;
            }

            return math.lengthsq(local) <= normalized.Radius * normalized.Radius;
        }

        public static bool OverlapsCircleXZ(float2 center, float radius, in LocalTransform tx, in Shape2DComponent shape)
        {
            float safeRadius = math.max(0f, radius);
            float2 local = WorldToLocalXZ(center, in tx);
            var normalized = Normalize(in shape);

            if (normalized.Kind == Shape2DKind.Rectangle)
                return OverlapsCircleVsRectangle(local, safeRadius, normalized.Size);

            return math.lengthsq(local) <= math.square(normalized.Radius + safeRadius);
        }

        public static void ComputeBoundsXZ(in LocalTransform tx, in Shape2DComponent shape, out float2 min, out float2 max)
        {
            var normalized = Normalize(in shape);
            float2 center = new float2(tx.Position.x, tx.Position.z);
            if (normalized.Kind == Shape2DKind.Circle)
            {
                min = center - normalized.Radius;
                max = center + normalized.Radius;
                return;
            }

            float2 half = normalized.Size * 0.5f;
            quaternion planarRotation = GetPlanarRotation(tx.Rotation);
            float3 corner0 = math.rotate(planarRotation, new float3(-half.x, 0f, -half.y)) + tx.Position;
            float3 corner1 = math.rotate(planarRotation, new float3(-half.x, 0f, half.y)) + tx.Position;
            float3 corner2 = math.rotate(planarRotation, new float3(half.x, 0f, -half.y)) + tx.Position;
            float3 corner3 = math.rotate(planarRotation, new float3(half.x, 0f, half.y)) + tx.Position;

            min = new float2(
                math.min(math.min(corner0.x, corner1.x), math.min(corner2.x, corner3.x)),
                math.min(math.min(corner0.z, corner1.z), math.min(corner2.z, corner3.z)));
            max = new float2(
                math.max(math.max(corner0.x, corner1.x), math.max(corner2.x, corner3.x)),
                math.max(math.max(corner0.z, corner1.z), math.max(corner2.z, corner3.z)));
        }

        public static float3 SampleUniformXZ(ref Random random, in LocalTransform tx, in Shape2DComponent shape)
        {
            return SampleUniformXZ(ref random, tx.Position, tx.Rotation, in shape);
        }

        public static float3 SampleUniformXZ(ref Random random, float3 center, quaternion rotation, in Shape2DComponent shape)
        {
            var normalized = Normalize(in shape);
            if (normalized.Kind == Shape2DKind.Rectangle)
            {
                float2 half = normalized.Size * 0.5f;
                float2 local = new float2(
                    random.NextFloat(-half.x, half.x),
                    random.NextFloat(-half.y, half.y));
                return LocalToWorldPositionXZ(local, center, rotation);
            }

            float angle = random.NextFloat(0f, math.PI * 2f);
            float dist = math.sqrt(random.NextFloat(0f, 1f)) * normalized.Radius;
            float2 offset = new float2(math.cos(angle), math.sin(angle)) * dist;
            return LocalToWorldPositionXZ(offset, center, rotation);
        }

        public static float3 LocalToWorldPositionXZ(float2 local, in LocalTransform tx)
        {
            return LocalToWorldPositionXZ(local, tx.Position, tx.Rotation);
        }

        public static float3 LocalToWorldPositionXZ(float2 local, float3 center, quaternion rotation)
        {
            quaternion planarRotation = GetPlanarRotation(rotation);
            float3 world = math.rotate(planarRotation, new float3(local.x, 0f, local.y)) + center;
            return new float3(world.x, center.y, world.z);
        }

        public static float2 WorldToLocalXZ(float2 world, in LocalTransform tx)
        {
            quaternion inversePlanarRotation = math.inverse(GetPlanarRotation(tx.Rotation));
            float3 delta = new float3(world.x - tx.Position.x, 0f, world.y - tx.Position.z);
            float3 local = math.rotate(inversePlanarRotation, delta);
            return new float2(local.x, local.z);
        }

        public static quaternion GetPlanarRotation(quaternion rotation)
        {
            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float2 planarForward = math.normalizesafe(new float2(forward.x, forward.z), new float2(0f, 1f));
            float yaw = math.atan2(planarForward.x, planarForward.y);
            return quaternion.RotateY(yaw);
        }

        private static bool OverlapsCircleVsRectangle(float2 center, float radius, float2 size)
        {
            float2 half = math.max(float2.zero, size * 0.5f);
            float2 closest = math.clamp(center, -half, half);
            float2 delta = center - closest;
            return math.lengthsq(delta) <= radius * radius;
        }
    }

    public static class ObstacleGeometryUtility
    {
        public static bool ContainsPointXZ(float2 point, in LocalTransform tx, in Shape2DComponent shape)
        {
            return Shape2DUtility.ContainsPointXZ(point, in tx, in shape);
        }

        public static bool OverlapsCircleXZ(float2 center, float radius, in LocalTransform tx, in Shape2DComponent shape)
        {
            return Shape2DUtility.OverlapsCircleXZ(center, radius, in tx, in shape);
        }
    }
}
