using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum BulletMovementFamilyId : byte
    {
        Linear = 0,
        DampedLinear = 1,
        HomingLite = 2,
    }

    public enum BulletCaptureRuleId : byte
    {
        StandardCollectible = 0,
        RiskTimedResolve = 1
    }

    public enum BulletLifecycleReasonId : byte
    {
        None = 0,
        LifetimeExpired = 1,
        StageBlocked = 2,
        VacuumCollected = 3,
        CarryFullRemoved = 4,
        PlayerHit = 5,
        MotionCompleted = 6,
    }

    public struct BulletVelocityComponent : IComponentData
    {
        public float2 Value;
    }

    public struct BulletRadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct BulletSpeedComponent : IComponentData
    {
        public float Value;
    }

    public struct BulletLifetimeMaxComponent : IComponentData
    {
        public float Value;
    }

    public struct BulletScoreValueComponent : IComponentData
    {
        public int Value;
    }

    public struct BulletTypeKeyComponent : IComponentData
    {
        public int Value;
    }

    public struct BulletCaptureRuleComponent : IComponentData
    {
        public BulletCaptureRuleId Value;
    }

    public struct BulletLifetimeComponent : IComponentData
    {
        public float Value;
    }

    [System.Serializable]
    public struct BulletDampedLinearDefinition
    {
        public float DampingPerSec;
        public float StopSpeedThreshold;
    }

    [System.Serializable]
    public struct BulletHomingLiteDefinition
    {
        public float TurnRateDegPerSec;
        public float MaxAcquireDistance;
        public float MinRetargetDistance;
    }

    public struct BulletSecondarySpawnReactionRuntimeDefinition
    {
        public int SecondaryBulletTypeKey;
        public int SpawnCount;
        public BulletSecondarySpawnShapeId Shape;
        public float SpreadAngleDeg;
        public float SpawnRadius;
    }

    public struct BulletDampedMotionComponent : IComponentData
    {
        public float DampingPerSec;
        public float StopSpeedThreshold;
    }

    public struct BulletHomingLiteMotionComponent : IComponentData
    {
        public float TurnRateDegPerSec;
        public float MaxAcquireDistance;
        public float MinRetargetDistance;
    }

    public struct BulletOnMotionCompletedExplodeReactionComponent : IComponentData
    {
        public int SecondaryBulletTypeKey;
        public int SpawnCount;
        public BulletSecondarySpawnShapeId Shape;
        public float SpreadAngleDeg;
        public float SpawnRadius;
    }

    public struct BulletOnCleanupRemovedSpawnSecondaryReactionComponent : IComponentData
    {
        public int SecondaryBulletTypeKey;
        public int SpawnCount;
        public BulletSecondarySpawnShapeId Shape;
        public float SpreadAngleDeg;
        public float SpawnRadius;
    }

    public struct BulletLifecycleRequestComponent : IComponentData
    {
        public BulletLifecycleReasonId Reason;
        public byte Priority;
        public Entity RelatedEntity;
        public uint Frame;
    }

    public struct BulletLifecycleContactComponent : IComponentData
    {
        public float2 PositionXZ;
        public float2 DirectionXZ;
    }

    // Source 기반 스폰/고갈 추적을 위한 출처 참조
    public struct BulletSourceRefComponent : IComponentData
    {
        public Entity Value;
    }

    // Enableable Tag (활성/비활성 토글용)
    public struct BulletActiveTag : IComponentData, IEnableableComponent { }

    // 제거 행동 시스템들이 디스폰을 직접 하지 않고, BulletExecutionGroup에 위임하기 위해 남기는 요청 태그.
    public struct BulletDespawnRequestTag : IComponentData, IEnableableComponent { }

    // 위험탄 분류 태그.
    // 생성/풀 초기화 시 CaptureRule 기반으로 enable 상태를 고정한다.
    public struct BulletHazardTag : IComponentData, IEnableableComponent { }

    // Render 관련 버퍼 (엔티티 활성/비활성 시 참조용)
    [InternalBufferCapacity(4)]
    public struct EntityRenderElementBuffer : IBufferElementData
    {
        public Entity Value;
    }

    public struct BulletPoolRegistryTag : IComponentData { }

    [InternalBufferCapacity(8)]
    public struct BulletPoolDefinitionBuffer : IBufferElementData
    {
        public int TypeKey;
        public Entity Prefab;
        public int PoolSize;
        public BulletCaptureRuleId CaptureRule;
        public float Speed;
        public float Lifetime;
        public float Radius;
        public int ScoreValue;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;
        public BulletSecondarySpawnReactionRuntimeDefinition OnMotionCompletedExplode;
        public BulletSecondarySpawnReactionRuntimeDefinition OnCleanupRemovedSpawnSecondary;
    }

    // 프레임 파이프라인 기준 단조 증가 프레임 ID.
    // ExecutionBegin에서 프레임당 1회 증가한다.
    public struct BulletFrameCounterComponent : IComponentData
    {
        public uint Value;
    }

    // 고정 Tick 시간원 런타임 상태.
    // C2 단계에서는 골격/토글 저장소로만 사용하고, 실제 고정 tick 루프 적용은 후속 단계에서 진행한다.
    public struct FixedTickTimeComponent : IComponentData
    {
        public byte EnableFixedTick;
        public byte PauseRequested;
        public byte StepRequested;
        public byte Reserved;
        public int MaxSubSteps;
        public float FixedDeltaTime;
        public float Accumulator;
        public uint Tick;
    }

    // 현재 프레임 로직 실행에 사용할 해석된 시간원.
    // FixedTickRootGroup OrderFirst에서 프레임당 1회 계산하고, 로직 시스템은 이 값을 읽어 사용한다.
    public struct FixedTickStepRuntimeComponent : IComponentData
    {
        public float FrameDeltaTime;
        public float LogicDeltaTime;
        public int LogicStepCount;
        public byte HasStep;
        public byte UsingFixedTick;
        public uint CurrentLogicFrame;
    }

    public struct GameplayPauseStateComponent : IComponentData
    {
        public GameplayPauseFlags Flags;
        public uint ReasonMask;
        public uint Version;
    }

    public struct StageGameplayClockComponent : IComponentData
    {
        public float ElapsedSec;
        public uint Version;
    }

    // Spawn/Despawn 프레임 추적 스탬프.
    // 렌더-활성 상태 불일치(고스트 표시) 진단 시 근거 데이터로 사용한다.
    public struct BulletLifecycleTraceComponent : IComponentData
    {
        public uint LastSpawnFrame;
        public uint LastDespawnFrame;
    }
}
