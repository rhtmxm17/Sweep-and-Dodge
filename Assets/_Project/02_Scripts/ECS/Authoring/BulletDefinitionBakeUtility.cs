namespace SweepNDodge.DotsBullets
{
    public static class BulletDefinitionBakeUtility
    {
        public static BulletSecondarySpawnReactionRuntimeDefinition CreateRuntimeReactionDefinition(
            in BulletSecondarySpawnReactionDefinition reaction)
        {
            return new BulletSecondarySpawnReactionRuntimeDefinition
            {
                SecondaryBulletTypeKey = ResolveSecondaryBulletTypeKey(in reaction),
                SpawnCount = reaction.SpawnCount,
                Shape = reaction.Shape,
                SpreadAngleDeg = reaction.SpreadAngleDeg,
                SpawnRadius = reaction.SpawnRadius,
            };
        }

        public static int ResolveSecondaryBulletTypeKey(in BulletSecondarySpawnReactionDefinition reaction)
        {
            if (reaction.SecondaryBullet == null)
                return -1;

            return reaction.SecondaryBullet.DefinitionId;
        }
    }
}
