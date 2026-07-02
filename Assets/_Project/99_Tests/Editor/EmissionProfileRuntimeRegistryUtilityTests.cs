using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class EmissionProfileRuntimeRegistryUtilityTests
    {
        [Test]
        public void RebuildFromStageDefinition_CollectsSourceHazardAndRecursiveTriggerProfiles()
        {
            using var world = new World("EmissionProfileRuntimeRegistry_Rebuild");
            var em = world.EntityManager;

            var rootBullet = CreateBulletDefinition(5101);
            var targetBullet = CreateBulletDefinition(5102);
            var hazardBullet = CreateBulletDefinition(5103);
            var rootProfile = CreateProfile("ep_registry_root", rootBullet);
            var targetProfile = CreateProfile("ep_registry_target", targetBullet);
            var hazardProfile = CreateProfile("ep_registry_hazard", hazardBullet);
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var stageDefinition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var hazardPrefab = new GameObject("hazard_prefab");

            try
            {
                rootProfile.LifecycleTriggers.MotionCompleted.Enabled = true;
                rootProfile.LifecycleTriggers.MotionCompleted.TargetProfile = targetProfile;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        DurationSec = 1f,
                        Directives = new[]
                        {
                            new WaveSpawnEntryAuthoring { Profile = rootProfile },
                            new WaveSpawnEntryAuthoring { Profile = rootProfile },
                        },
                    },
                };

                var actor = hazardPrefab.AddComponent<HazardActorAuthoring>();
                actor.PatternSlots = new[]
                {
                    new HazardActorPatternSlotAuthoring
                    {
                        PatternSlotId = 1,
                        Emission = new HazardActorEmissionAuthoring
                        {
                            Profile = hazardProfile,
                            EventRepeatCount = 1,
                        },
                    },
                };

                stageDefinition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1u,
                        SustainSlots = new[]
                        {
                            new SustainSlotBinding
                            {
                                State = SourceStateId.Normal,
                                Lane = SourceSpawnLaneId.Hazard,
                                Clips = new[] { clip },
                            },
                        },
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 1,
                                ActorArchetypePrefab = hazardPrefab,
                            },
                        },
                    },
                };

                var registryEntity = em.CreateEntity(typeof(EmissionProfileRuntimeRegistryTag));
                var registry = em.AddBuffer<EmissionProfileRuntimeRegistryBuffer>(registryEntity);

                EmissionProfileRuntimeRegistryUtility.RebuildFromStageDefinition(em, stageDefinition, registry);

                Assert.That(registry.Length, Is.EqualTo(3));
                Assert.That(ContainsProfile(registry, rootProfile.GetInstanceID()), Is.True);
                Assert.That(ContainsProfile(registry, targetProfile.GetInstanceID()), Is.True);
                Assert.That(ContainsProfile(registry, hazardProfile.GetInstanceID()), Is.True);
                Assert.That(CountProfile(registry, rootProfile.GetInstanceID()), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(hazardPrefab);
                Object.DestroyImmediate(stageDefinition);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(rootProfile);
                Object.DestroyImmediate(targetProfile);
                Object.DestroyImmediate(hazardProfile);
                Object.DestroyImmediate(rootBullet.Prefab);
                Object.DestroyImmediate(targetBullet.Prefab);
                Object.DestroyImmediate(hazardBullet.Prefab);
                Object.DestroyImmediate(rootBullet);
                Object.DestroyImmediate(targetBullet);
                Object.DestroyImmediate(hazardBullet);
            }
        }

        private static BulletDefinitionSO CreateBulletDefinition(int definitionId)
        {
            var definition = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            definition.Editor_SetDefinitionId(definitionId);
            definition.Prefab = new GameObject($"bullet_{definitionId}");
            return definition;
        }

        private static EmissionProfileSO CreateProfile(string name, BulletDefinitionSO bullet)
        {
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();
            profile.name = name;
            profile.Bullet = bullet;
            return profile;
        }

        private static bool ContainsProfile(DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry, int profileRefId)
        {
            return CountProfile(registry, profileRefId) > 0;
        }

        private static int CountProfile(DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry, int profileRefId)
        {
            int count = 0;
            for (int i = 0; i < registry.Length; i++)
            {
                if (registry[i].ProfileRefId == profileRefId)
                    count++;
            }

            return count;
        }
    }
}
