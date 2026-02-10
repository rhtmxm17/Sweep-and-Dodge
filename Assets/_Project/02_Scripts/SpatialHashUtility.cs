using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    // Grid 기반 공간 해시 유틸리티
    public static class SpatialHashUtility
    {
        // 간단한 정수 해시(셀 충돌 확률이 낮도록 prime 사용)
        public static int Hash(int2 cell)
        {
            return (cell.x * 73856093) ^ (cell.y * 19349663);
        }

        public static int2 ToCell(float3 pos, float invCellSize)
        {
            // XZ 평면 사용
            return (int2)math.floor(new float2(pos.x, pos.z) * invCellSize);
        }
    }
}
