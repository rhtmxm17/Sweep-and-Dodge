// Bullet 파이프라인 코어 정의
// - 업데이트 그룹 파이프라인 구성
//   BulletFramePipelineGroup   : 탄막 파이프라인 루트 그룹
//   BulletExecutionBeginGroup  : 풀 Dequeue(스폰 실행)
//   BulletSimulationGroup      : Move/Lifetime + SpatialHash(Build) 단일 소유(Write)
//   BulletRequestGroup         : 제거 행동(예: 탄환 흡입)
//                                - 외부에서 정의될 요청 시스템의 위치
//                                - SpatialHash ReadOnly 조회로 디스폰 요청 태그 enable
//   BulletExecutionEndGroup    : 디스폰 실행 + 풀 Enqueue(반납 실행)
// - SharedStatic 저장소
//   - FreeByKey: Begin/End Owner 영역 접근
//   - CellMap/HazardCellMap: Simulation Write / Request ReadOnly

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace SweepNDodge.DotsBullets
{
    // ----------------------------------------------------------------------
    // Pipeline Groups
    // ----------------------------------------------------------------------

    // 탄막 필드 파이프라인 그룹들.
    // - 루트 그룹(BulletFramePipelineGroup) 아래에서 순서를 강제한다.

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FixedTickRootGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(FixedTickRootGroup))]
    public partial class BulletFramePipelineGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateBefore(typeof(BulletSimulationGroup))]
    public partial class BulletExecutionBeginGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateAfter(typeof(BulletExecutionBeginGroup))]
    public partial class BulletSimulationGroup : ComponentSystemGroup { }

    /// <summary>
    /// Bullet에 대한 외부 요청 시스템들이 위치할 그룹
    /// </summary>
    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateAfter(typeof(BulletSimulationGroup))]
    public partial class BulletRequestGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateAfter(typeof(BulletRequestGroup))]
    public partial class BulletExecutionEndGroup : ComponentSystemGroup { }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedTickRootGroup), OrderFirst = true)]
    public partial struct FixedTickBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            var fixedTickQuery = SystemAPI.QueryBuilder().WithAll<FixedTickTimeComponent>().Build();
            if (fixedTickQuery.IsEmptyIgnoreFilter)
            {
                var e = em.CreateEntity(typeof(FixedTickTimeComponent));
                em.SetComponentData(e, new FixedTickTimeComponent
                {
                    EnableFixedTick = 0,
                    PauseRequested = 0,
                    StepRequested = 0,
                    Reserved = 0,
                    MaxSubSteps = 4,
                    FixedDeltaTime = 1f / 60f,
                    Accumulator = 0f,
                    Tick = 0u,
                });
            }

            var frameQuery = SystemAPI.QueryBuilder().WithAll<BulletFrameCounterComponent>().Build();
            if (frameQuery.IsEmptyIgnoreFilter)
            {
                var e = em.CreateEntity(typeof(BulletFrameCounterComponent));
                em.SetComponentData(e, new BulletFrameCounterComponent { Value = 0 });
            }

            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<FixedTickTimeComponent>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fixedTick = SystemAPI.GetSingletonRW<FixedTickTimeComponent>();
            if (SystemAPI.TryGetSingleton<BulletFrameCounterComponent>(out var frameCounter))
            {
                var value = fixedTick.ValueRO;
                value.Tick = frameCounter.Value;
                fixedTick.ValueRW = value;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup), OrderFirst = true)]
    public partial struct BulletFrameCounterAdvanceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasRuntime = SystemAPI.TryGetSingleton<FixedTickStepRuntimeComponent>(out var fixedTickRuntime);
            if (!FixedTickTimeUtility.ShouldRunLogicStep(
                    hasRuntime,
                    in fixedTickRuntime,
                    SystemAPI.Time.DeltaTime))
                return;

            var counter = SystemAPI.GetSingletonRW<BulletFrameCounterComponent>();
            counter.ValueRW.Value += 1;
        }
    }

    // ----------------------------------------------------------------------
    // Shared (Pool + SpatialHash)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Burst/Job에서 사용 가능한 정적 공유 저장소.
    /// - FreeByKey(키 기반 풀): Begin/End Owner 영역에서만 접근
    /// - CellMap(SpatialHash): 전체 활성 탄 (Simulation Write / Request ReadOnly)
    /// - HazardCellMap(SpatialHash): 위험탄 전용 (Simulation Write / Request ReadOnly)
    ///
    /// 주의: SharedStatic은 프로세스 전역이므로, 멀티 월드 사용 시 관리 전략이 추가로 필요할 수 있다.
    /// (현재 스코프에서는 단일 월드 전제로 사용)
    /// </summary>
    public static class BulletFieldShared
    {
        private struct FlagsKey { }
        private struct FreeByKeyKey { }
        private struct PoolFenceKey { }
        private struct CellMapKey { }
        private struct HazardCellMapKey { }
        private struct CellMapFenceKey { }

        private static readonly SharedStatic<byte> _flags = SharedStatic<byte>.GetOrCreate<FlagsKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _freeByKey = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<FreeByKeyKey>();
        private static readonly SharedStatic<JobHandle> _poolFence = SharedStatic<JobHandle>.GetOrCreate<PoolFenceKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _cellMap = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<CellMapKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _hazardCellMap = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<HazardCellMapKey>();
        private static readonly SharedStatic<JobHandle> _cellMapFence = SharedStatic<JobHandle>.GetOrCreate<CellMapFenceKey>();

        public static bool IsInitialized => _flags.Data != 0;

        public static ref NativeParallelMultiHashMap<int, Entity> FreeByKey => ref _freeByKey.Data;
        public static ref JobHandle PoolFence => ref _poolFence.Data;

        public static ref NativeParallelMultiHashMap<int, Entity> CellMap => ref _cellMap.Data;
        public static ref NativeParallelMultiHashMap<int, Entity> HazardCellMap => ref _hazardCellMap.Data;
        public static ref JobHandle CellMapFence => ref _cellMapFence.Data;

        public static void MarkInitialized() => _flags.Data = 1;
        public static void MarkUninitialized() => _flags.Data = 0;
    }

}
