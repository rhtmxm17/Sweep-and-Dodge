using Unity.Entities;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(PlayerFixedStepGroup), OrderFirst = true)]
    public partial struct ReplayTickInputApplySystem : ISystem
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
            Entity replayEntity;
            if (replayQuery.IsEmptyIgnoreFilter)
            {
                replayEntity = em.CreateEntity(
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
            else
            {
                replayEntity = replayQuery.GetSingletonEntity();
            }

            if (!em.HasComponent<ReplayTickInputQueueStateComponent>(replayEntity))
            {
                em.AddComponentData(replayEntity, new ReplayTickInputQueueStateComponent
                {
                    LastEnqueuedTick = 0u,
                    LastConsumedTick = 0u,
                    LastEnqueuedSequence = 0u,
                    LastConsumedSequence = 0u,
                    PendingCount = 0,
                });
            }
            if (!em.HasBuffer<ReplayTickInputQueueBufferElement>(replayEntity))
                em.AddBuffer<ReplayTickInputQueueBufferElement>(replayEntity);

            _playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag>()
                .WithAll<PlayerGoSyncComponent>()
                .WithAll<PlayerInputIntentComponent>()
                .WithAll<PlayerResolvedInputSnapshotComponent>()
                .Build();

            state.RequireForUpdate(_playerQuery);
            state.RequireForUpdate<ReplayInputControlComponent>();
            state.RequireForUpdate<ReplayInputCursorComponent>();
            state.RequireForUpdate<ReplayTickInputQueueStateComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_playerQuery.IsEmptyIgnoreFilter)
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            uint frame = fixedTickRuntime.CurrentLogicFrame;
            Entity playerEntity = _playerQuery.GetSingletonEntity();
            Entity replayEntity = SystemAPI.GetSingletonEntity<ReplayInputControlComponent>();
            var controlRW = SystemAPI.GetComponentRW<ReplayInputControlComponent>(replayEntity);
            var cursorRW = SystemAPI.GetComponentRW<ReplayInputCursorComponent>(replayEntity);
            var frames = SystemAPI.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
            var queueStateRW = SystemAPI.GetComponentRW<ReplayTickInputQueueStateComponent>(replayEntity);
            var queue = SystemAPI.GetBuffer<ReplayTickInputQueueBufferElement>(replayEntity);

            bool playbackStartedThisUpdate = false;
            if (ReplaySessionStaging.TryConsumePlayback(frames, out uint stagedRunSeed))
            {
                cursorRW.ValueRW = new ReplayInputCursorComponent
                {
                    NextFrameIndex = 0,
                };
                controlRW.ValueRW = new ReplayInputControlComponent
                {
                    Mode = ReplayInputModeId.Playback,
                    LastRecordedFrame = 0u,
                    LastPlaybackFrame = 0u,
                    MissingFrameCount = 0,
                };

                if (SystemAPI.TryGetSingletonEntity<SpawnRunSeedComponent>(out var runSeedEntity))
                {
                    SystemAPI.SetComponent(runSeedEntity, new SpawnRunSeedComponent
                    {
                        Value = stagedRunSeed > 0u ? stagedRunSeed : 1u,
                    });
                }

                if (SystemAPI.TryGetSingletonEntity<BulletFrameCounterComponent>(out var frameCounterEntity))
                {
                    SystemAPI.SetComponent(frameCounterEntity, new BulletFrameCounterComponent
                    {
                        Value = 0u,
                    });
                }

                frame = frames.Length > 0 ? frames[0].Frame : frame;
                queue.Clear();
                queueStateRW.ValueRW = new ReplayTickInputQueueStateComponent
                {
                    LastEnqueuedTick = 0u,
                    LastConsumedTick = 0u,
                    LastEnqueuedSequence = 0u,
                    LastConsumedSequence = 0u,
                    PendingCount = 0,
                };
                playbackStartedThisUpdate = true;
            }

            if (controlRW.ValueRO.Mode != ReplayInputModeId.Playback)
            {
                var queueState = queueStateRW.ValueRO;
                CaptureAndConsumeLiveInputQueue(frame, playerEntity, ref queueState, queue, state.EntityManager);
                queueStateRW.ValueRW = queueState;
                SyncResolvedInputSnapshot(playerEntity, state.EntityManager);
                return;
            }

            int nextFrameIndex = cursorRW.ValueRO.NextFrameIndex;
            if (nextFrameIndex < 0)
                nextFrameIndex = 0;

            while (nextFrameIndex < frames.Length && frames[nextFrameIndex].Frame < frame)
                nextFrameIndex++;

            if (nextFrameIndex < frames.Length && frames[nextFrameIndex].Frame == frame)
            {
                var snapshot = frames[nextFrameIndex];
                ApplyPlaybackSnapshot(playerEntity, in snapshot, state.EntityManager);

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

            if (playbackStartedThisUpdate)
                return;

            var missingControl = controlRW.ValueRO;
            if (missingControl.MissingFrameCount < int.MaxValue)
                missingControl.MissingFrameCount += 1;
            controlRW.ValueRW = missingControl;
        }

        private static void CaptureAndConsumeLiveInputQueue(
            uint tick,
            Entity playerEntity,
            ref ReplayTickInputQueueStateComponent queueState,
            DynamicBuffer<ReplayTickInputQueueBufferElement> queue,
            EntityManager em)
        {
            var intent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
            var input = new ReplayTickInputQueueBufferElement
            {
                Tick = tick,
                MoveAxis = intent.MoveAxis,
                AimWorldXZ = intent.AimWorldXZ,
                HasAimWorldPoint = intent.HasAimWorldPoint,
                VacuumRequested = intent.VacuumRequested,
                CleanupActionRequested = intent.CleanupActionRequested,
                RequestedCleanupActionSlot = intent.RequestedCleanupActionSlot,
                InputSequence = intent.Sequence,
            };

            EnqueueOrReplaceByTick(input, queue);
            queueState.LastEnqueuedTick = tick;
            if (queueState.LastEnqueuedSequence < input.InputSequence)
                queueState.LastEnqueuedSequence = input.InputSequence;

            int consumeIndex = -1;
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i].Tick <= tick)
                    consumeIndex = i;
                else
                    break;
            }

            if (consumeIndex < 0)
            {
                queueState.PendingCount = queue.Length;
                SyncResolvedInputSnapshot(playerEntity, em);
                return;
            }

            var consumed = queue[consumeIndex];
            bool duplicateSameTickInput = consumed.Tick == queueState.LastConsumedTick &&
                                          consumed.InputSequence == queueState.LastConsumedSequence;
            if (duplicateSameTickInput)
            {
                consumed.VacuumRequested = 0;
                consumed.CleanupActionRequested = 0;
                consumed.RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None;
            }

            var intentAfter = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
            intentAfter.MoveAxis = consumed.MoveAxis;
            intentAfter.AimWorldXZ = consumed.AimWorldXZ;
            intentAfter.HasAimWorldPoint = consumed.HasAimWorldPoint;
            intentAfter.VacuumRequested = consumed.VacuumRequested;
            intentAfter.CleanupActionRequested = consumed.CleanupActionRequested;
            intentAfter.RequestedCleanupActionSlot = consumed.RequestedCleanupActionSlot;
            intentAfter.Sequence = consumed.InputSequence;
            em.SetComponentData(playerEntity, intentAfter);
            SyncResolvedInputSnapshot(playerEntity, em);

            queue.RemoveRange(0, consumeIndex + 1);
            queueState.LastConsumedTick = consumed.Tick;
            queueState.LastConsumedSequence = consumed.InputSequence;
            queueState.PendingCount = queue.Length;
        }

        private static void EnqueueOrReplaceByTick(
            in ReplayTickInputQueueBufferElement input,
            DynamicBuffer<ReplayTickInputQueueBufferElement> queue)
        {
            int last = queue.Length - 1;
            if (last >= 0 && queue[last].Tick == input.Tick)
            {
                queue[last] = input;
                return;
            }

            queue.Add(input);
        }

        private static void ApplyPlaybackSnapshot(Entity playerEntity, in ReplayInputFrameBufferElement snapshot, EntityManager em)
        {
            em.SetComponentData(playerEntity, new PlayerInputIntentComponent
            {
                MoveAxis = snapshot.MoveAxis,
                AimWorldXZ = snapshot.AimWorldXZ,
                HasAimWorldPoint = snapshot.HasAimWorldPoint,
                VacuumRequested = snapshot.VacuumRequested,
                CleanupActionRequested = snapshot.CleanupActionRequested,
                RequestedCleanupActionSlot = snapshot.RequestedCleanupActionSlot,
                Sequence = snapshot.InputSequence,
            });
            em.SetComponentData(playerEntity, new PlayerResolvedInputSnapshotComponent
            {
                MoveAxis = snapshot.MoveAxis,
                AimWorldXZ = snapshot.AimWorldXZ,
                HasAimWorldPoint = snapshot.HasAimWorldPoint,
                VacuumRequested = snapshot.VacuumRequested,
                CleanupActionRequested = snapshot.CleanupActionRequested,
                RequestedCleanupActionSlot = snapshot.RequestedCleanupActionSlot,
                Sequence = snapshot.InputSequence,
            });
            em.SetComponentData(playerEntity, new PlayerGoSyncComponent
            {
                Position = snapshot.Position,
                Rotation = snapshot.Rotation,
                SyncRotation = snapshot.SyncRotation,
                VacuumRequested = snapshot.VacuumRequested,
                CleanupActionRequested = snapshot.CleanupActionRequested,
                RequestedCleanupActionSlot = snapshot.RequestedCleanupActionSlot,
            });
            if (em.HasComponent<LocalTransform>(playerEntity))
            {
                var tx = em.GetComponentData<LocalTransform>(playerEntity);
                em.SetComponentData(playerEntity, LocalTransform.FromPositionRotationScale(
                    snapshot.Position,
                    snapshot.Rotation,
                    tx.Scale));
            }
        }

        private static void SyncResolvedInputSnapshot(Entity playerEntity, EntityManager em)
        {
            var intent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
            em.SetComponentData(playerEntity, new PlayerResolvedInputSnapshotComponent
            {
                MoveAxis = intent.MoveAxis,
                AimWorldXZ = intent.AimWorldXZ,
                HasAimWorldPoint = intent.HasAimWorldPoint,
                VacuumRequested = intent.VacuumRequested,
                CleanupActionRequested = intent.CleanupActionRequested,
                RequestedCleanupActionSlot = intent.RequestedCleanupActionSlot,
                Sequence = intent.Sequence,
            });
        }
    }

    [UpdateInGroup(typeof(PlayerFixedStepGroup), OrderLast = true)]
    public partial struct ReplayTickRecordSystem : ISystem
    {
        private EntityQuery _playerQuery;

        public void OnCreate(ref SystemState state)
        {
            _playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag>()
                .WithAll<PlayerGoSyncComponent>()
                .WithAll<PlayerResolvedInputSnapshotComponent>()
                .Build();

            state.RequireForUpdate(_playerQuery);
            state.RequireForUpdate<ReplayInputControlComponent>();
            state.RequireForUpdate<ReplayInputCursorComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            Entity playerEntity = _playerQuery.GetSingletonEntity();
            Entity replayEntity = SystemAPI.GetSingletonEntity<ReplayInputControlComponent>();
            var controlRW = SystemAPI.GetComponentRW<ReplayInputControlComponent>(replayEntity);
            if (controlRW.ValueRO.Mode != ReplayInputModeId.Record)
                return;

            uint frame = fixedTickRuntime.CurrentLogicFrame;
            var frames = SystemAPI.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
            var sync = SystemAPI.GetComponent<PlayerGoSyncComponent>(playerEntity);
            var input = SystemAPI.GetComponent<PlayerResolvedInputSnapshotComponent>(playerEntity);
            var snapshot = new ReplayInputFrameBufferElement
            {
                Frame = frame,
                MoveAxis = input.MoveAxis,
                AimWorldXZ = input.AimWorldXZ,
                HasAimWorldPoint = input.HasAimWorldPoint,
                Position = sync.Position,
                Rotation = sync.Rotation,
                SyncRotation = sync.SyncRotation,
                VacuumRequested = input.VacuumRequested,
                CleanupActionRequested = input.CleanupActionRequested,
                RequestedCleanupActionSlot = input.RequestedCleanupActionSlot,
                InputSequence = input.Sequence,
            };

            int last = frames.Length - 1;
            if (last >= 0 && frames[last].Frame == frame)
                frames[last] = snapshot;
            else
                frames.Add(snapshot);

            var control = controlRW.ValueRO;
            control.LastRecordedFrame = frame;
            controlRW.ValueRW = control;
        }
    }
}
