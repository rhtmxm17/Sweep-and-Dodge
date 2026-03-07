using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Legacy runtime template alias. Use DepositRuntimeTemplateAuthoring for new prefabs.
    /// </summary>
    public sealed class DepositPointAuthoring : DepositRuntimeTemplateAuthoringBase
    {
        private sealed class Baker : Baker<DepositPointAuthoring>
        {
            public override void Bake(DepositPointAuthoring authoring)
            {
                BakeRuntimeTemplate(this, authoring);
            }
        }
    }
}
