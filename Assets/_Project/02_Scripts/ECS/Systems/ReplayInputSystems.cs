using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(PlayerGoSyncSystem))]
    public partial struct ReplayInputSyncSystem : ISystem
    {
        private EntityQuery _playerQuery;

        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            var replayQuery = SystemAPI.QueryBuilder()
                .WithAll<ReplayInputControlComponent>()
                .WithAll<ReplayInputCursorComponent>()
                .WithAll<ReplayInputFrameBufferElement>()
                .Build();
            if (replayQuery.IsEmptyIgnoreFilter)
            {
                var replayEntity = em.CreateEntity(
                    typeof(ReplayInputControlComponent),
                    typeof(ReplayInputCursorComponent));
                em.SetComponentData(replayEntity, new ReplayInputControlComponent
                {
                    Mode = ReplayInputModeId.Off,
                    LastRecordedFrame = 0u,
                    LastPlaybackFrame = 0u,
                    MissingFrameCount = 0,
                });
                em.SetComponentData(replayEntity, new ReplayInputCursorComponent
                {
                    NextFrameIndex = 0,
                });
                em.AddBuffer<ReplayInputFrameBufferElement>(replayEntity);
            }

            _playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag>()
                .WithAll<PlayerGoSyncComponent>()
                .Build();

            state.RequireForUpdate(_playerQuery);
            state.RequireForUpdate<ReplayInputControlComponent>();
            state.RequireForUpdate<ReplayInputCursorComponent>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_playerQuery.IsEmptyIgnoreFilter)
                return;

            uint frame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());
            Entity playerEntity = _playerQuery.GetSingletonEntity();
            Entity replayEntity = SystemAPI.GetSingletonEntity<ReplayInputControlComponent>();
            var controlRW = SystemAPI.GetComponentRW<ReplayInputControlComponent>(replayEntity);
            var cursorRW = SystemAPI.GetComponentRW<ReplayInputCursorComponent>(replayEntity);
            var frames = SystemAPI.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);

            if (controlRW.ValueRO.Mode == ReplayInputModeId.Record)
            {
                var sync = SystemAPI.GetComponent<PlayerGoSyncComponent>(playerEntity);
                var snapshot = new ReplayInputFrameBufferElement
                {
                    Frame = frame,
                    Position = sync.Position,
                    Rotation = sync.Rotation,
                    SyncRotation = sync.SyncRotation,
                    VacuumRequested = sync.VacuumRequested,
                    CleanupActionRequested = sync.CleanupActionRequested,
                    RequestedCleanupActionSlot = sync.RequestedCleanupActionSlot,
                };

                int last = frames.Length - 1;
                if (last >= 0 && frames[last].Frame == frame)
                    frames[last] = snapshot;
                else
                    frames.Add(snapshot);

                var control = controlRW.ValueRO;
                control.LastRecordedFrame = frame;
                controlRW.ValueRW = control;
                return;
            }

            if (controlRW.ValueRO.Mode != ReplayInputModeId.Playback)
                return;

            int nextFrameIndex = cursorRW.ValueRO.NextFrameIndex;
            if (nextFrameIndex < 0)
                nextFrameIndex = 0;

            while (nextFrameIndex < frames.Length && frames[nextFrameIndex].Frame < frame)
                nextFrameIndex++;

            if (nextFrameIndex < frames.Length && frames[nextFrameIndex].Frame == frame)
            {
                var snapshot = frames[nextFrameIndex];
                SystemAPI.SetComponent(playerEntity, new PlayerGoSyncComponent
                {
                    Position = snapshot.Position,
                    Rotation = snapshot.Rotation,
                    SyncRotation = snapshot.SyncRotation,
                    VacuumRequested = snapshot.VacuumRequested,
                    CleanupActionRequested = snapshot.CleanupActionRequested,
                    RequestedCleanupActionSlot = snapshot.RequestedCleanupActionSlot,
                });

                cursorRW.ValueRW = new ReplayInputCursorComponent
                {
                    NextFrameIndex = nextFrameIndex + 1,
                };

                var control = controlRW.ValueRO;
                control.LastPlaybackFrame = frame;
                controlRW.ValueRW = control;
                return;
            }

            cursorRW.ValueRW = new ReplayInputCursorComponent
            {
                NextFrameIndex = nextFrameIndex,
            };

            var missingControl = controlRW.ValueRO;
            if (missingControl.MissingFrameCount < int.MaxValue)
                missingControl.MissingFrameCount += 1;
            controlRW.ValueRW = missingControl;
        }
    }
}
