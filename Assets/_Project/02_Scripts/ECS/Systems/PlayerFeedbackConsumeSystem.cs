using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// UI/HUD/VFX 피드백 소비 지점.
    /// - 실제 브리지 적용 위치를 이 시스템으로 고정한다.
    /// - 현재는 소비 확장 전 단계이므로 clear만 수행한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct PlayerUiFeedbackConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerUiFeedbackEventBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var uiBuffer = SystemAPI.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
            if (uiBuffer.Length == 0)
                return;

            for (int i = 0; i < uiBuffer.Length; i++)
            {
                var evt = uiBuffer[i];
                Debug.Log($"[PlayerUiFeedbackConsume] i={i}, type={evt.Type}, reason={evt.Reason}, value={evt.Value}, related={evt.RelatedEntity}, frame={evt.Frame}, seq={evt.Sequence}");
            }
            Debug.Log($"[PlayerUiFeedbackConsume] consumed={uiBuffer.Length}");

            uiBuffer.Clear();
        }
    }

    /// <summary>
    /// Impulse 피드백 소비 지점.
    /// - GO Bridge/컨트롤러 연동 시 이 시스템에서 소비한다.
    /// - 현재는 소비 확장 전 단계이므로 clear만 수행한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct PlayerImpulseConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerImpulseEventBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var impulseBuffer = SystemAPI.GetBuffer<PlayerImpulseEventBufferElement>(playerEntity);
            if (impulseBuffer.Length == 0)
                return;

            for (int i = 0; i < impulseBuffer.Length; i++)
            {
                var evt = impulseBuffer[i];
                Debug.Log($"[PlayerImpulseConsume] i={i}, reason={evt.Reason}, dir=({evt.DirX:0.###},{evt.DirZ:0.###}), magnitude={evt.Magnitude:0.###}, frame={evt.Frame}, seq={evt.Sequence}");
            }
            Debug.Log($"[PlayerImpulseConsume] consumed={impulseBuffer.Length}");

            impulseBuffer.Clear();
        }
    }
}
