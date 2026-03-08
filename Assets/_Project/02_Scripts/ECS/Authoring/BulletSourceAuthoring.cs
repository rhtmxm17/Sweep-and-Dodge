using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Legacy runtime template alias. Use SourceRuntimeTemplateAuthoring for new prefabs.
    /// </summary>
    public sealed class BulletSourceAuthoring : SourceRuntimeTemplateAuthoringBase
    {
        private sealed class Baker : Baker<BulletSourceAuthoring>
        {
            public override void Bake(BulletSourceAuthoring authoring)
            {
                BakeRuntimeTemplate(this, authoring);
            }
        }
    }
}
