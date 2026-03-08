using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public sealed class SourceRuntimeTemplateAuthoring : SourceRuntimeTemplateAuthoringBase
    {
        private sealed class Baker : Baker<SourceRuntimeTemplateAuthoring>
        {
            public override void Bake(SourceRuntimeTemplateAuthoring authoring)
            {
                BakeRuntimeTemplate(this, authoring);
            }
        }
    }
}
