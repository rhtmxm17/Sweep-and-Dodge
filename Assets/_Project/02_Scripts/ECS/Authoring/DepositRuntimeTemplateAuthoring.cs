using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public sealed class DepositRuntimeTemplateAuthoring : DepositRuntimeTemplateAuthoringBase
    {
        private sealed class Baker : Baker<DepositRuntimeTemplateAuthoring>
        {
            public override void Bake(DepositRuntimeTemplateAuthoring authoring)
            {
                BakeRuntimeTemplate(this, authoring);
            }
        }
    }
}
