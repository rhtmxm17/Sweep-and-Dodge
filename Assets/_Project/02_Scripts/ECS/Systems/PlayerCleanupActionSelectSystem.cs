using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 행동 분기 선택 상태와 resolved profile snapshot을 확정한다.
    /// - 외부 입력/선택 경로는 PendingProfileKey만 기록한다.
    /// - 실제 key resolve/fallback/resolved snapshot 갱신은 Request 그룹 시작 지점에서 단일 책임으로 수행한다.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateBefore(typeof(BulletVacuumRequestSystem))]
    public partial struct PlayerCleanupActionSelectSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerCleanupActionStateComponent>();
            state.RequireForUpdate<PlayerCleanupActionSelectionConfigComponent>();
            state.RequireForUpdate<PlayerCleanupResolvedProfileComponent>();
            state.RequireForUpdate<PlayerCleanupActionProfileBufferElement>();
            state.RequireForUpdate<VacuumRuntimeStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (actionState, selectionConfig, resolvedProfile, vacuum, entity) in
                     SystemAPI.Query<
                         RefRW<PlayerCleanupActionStateComponent>,
                         RefRO<PlayerCleanupActionSelectionConfigComponent>,
                         RefRW<PlayerCleanupResolvedProfileComponent>,
                         RefRO<VacuumRuntimeStateComponent>>()
                     .WithAll<PlayerTag>()
                     .WithEntityAccess())
            {
                var profiles = SystemAPI.GetBuffer<PlayerCleanupActionProfileBufferElement>(entity);
                if (profiles.Length <= 0)
                    continue;

                var selectedKey = ResolveProfileKeyOrFallback(
                    profiles,
                    actionState.ValueRO.SelectedProfileKey,
                    selectionConfig.ValueRO.DefaultProfileKey,
                    out var selectedProfile);

                if (!actionState.ValueRO.SelectedProfileKey.Equals(selectedKey))
                    actionState.ValueRW.SelectedProfileKey = selectedKey;

                ApplyResolvedProfile(
                    ref resolvedProfile.ValueRW,
                    selectedProfile,
                    actionState.ValueRO.Version);

                if (PlayerCleanupActionContractUtility.IsEmptyProfileKey(actionState.ValueRO.PendingProfileKey))
                    continue;

                var pendingKey = ResolveProfileKeyOrFallback(
                    profiles,
                    actionState.ValueRO.PendingProfileKey,
                    selectionConfig.ValueRO.DefaultProfileKey,
                    out var pendingProfile);

                if (vacuum.ValueRO.IsActive != 0)
                {
                    actionState.ValueRW.PendingProfileKey = default;
                    continue;
                }

                if (pendingKey.Equals(selectedKey))
                {
                    actionState.ValueRW.PendingProfileKey = default;
                    continue;
                }

                actionState.ValueRW.SelectedProfileKey = pendingKey;
                actionState.ValueRW.PendingProfileKey = default;
                actionState.ValueRW.Version++;
                uint version = actionState.ValueRW.Version;
                ApplyResolvedProfile(ref resolvedProfile.ValueRW, pendingProfile, version);
                Debug.Log($"[CleanupActionSelect] selectedKey={pendingKey}, actionKind={pendingProfile.ActionId}, version={version}");
            }
        }

        private static FixedString64Bytes ResolveProfileKeyOrFallback(
            DynamicBuffer<PlayerCleanupActionProfileBufferElement> profiles,
            FixedString64Bytes requestedKey,
            FixedString64Bytes defaultKey,
            out PlayerCleanupActionProfileBufferElement resolvedProfile)
        {
            if (!PlayerCleanupActionContractUtility.IsEmptyProfileKey(requestedKey)
                && TryFindProfileByKey(profiles, requestedKey, out resolvedProfile))
            {
                return resolvedProfile.ProfileKey;
            }

            if (!PlayerCleanupActionContractUtility.IsEmptyProfileKey(defaultKey)
                && TryFindProfileByKey(profiles, defaultKey, out resolvedProfile))
            {
                return resolvedProfile.ProfileKey;
            }

            resolvedProfile = PlayerCleanupActionContractUtility.SanitizeProfile(profiles[0]);
            return resolvedProfile.ProfileKey;
        }

        private static bool TryFindProfileByKey(
            DynamicBuffer<PlayerCleanupActionProfileBufferElement> profiles,
            FixedString64Bytes key,
            out PlayerCleanupActionProfileBufferElement profile)
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                var candidate = PlayerCleanupActionContractUtility.SanitizeProfile(profiles[i]);
                if (candidate.ProfileKey.Equals(key))
                {
                    profile = candidate;
                    return true;
                }
            }

            profile = default;
            return false;
        }

        private static void ApplyResolvedProfile(
            ref PlayerCleanupResolvedProfileComponent resolvedProfile,
            PlayerCleanupActionProfileBufferElement sourceProfile,
            uint version)
        {
            resolvedProfile = PlayerCleanupActionContractUtility.CreateResolvedProfile(sourceProfile, version);
        }
    }
}
