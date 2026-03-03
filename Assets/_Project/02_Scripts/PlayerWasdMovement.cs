using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 프로토타입 플레이용 간단한 WASD 이동 컴포넌트.
    /// </summary>
    public sealed class PlayerWasdMovement : MonoBehaviour
    {
        [Min(0f)]
        public float MoveSpeed = 6f;
        public Camera TargetCamera;

        [Header("Authority (Transition)")]
        public bool EnableLegacyTransformWrite = false;

        private EntityManager _em;
        private EntityQuery _playerQuery;
        private EntityQuery _replayQuery;
        private bool _isReplayBound;

        private void Update()
        {
            if (IsReplayInputSuppressed())
                return;

            var moveX = 0f;
            var moveZ = 0f;

            if (Input.GetKey(KeyCode.A)) moveX -= 1f;
            if (Input.GetKey(KeyCode.D)) moveX += 1f;
            if (Input.GetKey(KeyCode.S)) moveZ -= 1f;
            if (Input.GetKey(KeyCode.W)) moveZ += 1f;

            var move = new Vector3(moveX, 0f, moveZ);
            if (move.sqrMagnitude > 1f) move.Normalize();

            var cameraToUse = TargetCamera != null ? TargetCamera : Camera.main;
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            bool hasAimPoint = false;
            var aimPoint = Vector3.zero;
            if (cameraToUse != null)
            {
                var ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
                if (groundPlane.Raycast(ray, out var enter))
                {
                    hasAimPoint = true;
                    aimPoint = ray.GetPoint(enter);
                }
            }

            PublishInputIntent(new float2(move.x, move.z), hasAimPoint, new float2(aimPoint.x, aimPoint.z));

            if (!EnableLegacyTransformWrite)
                return;

            transform.position += move * (MoveSpeed * Time.deltaTime);

            if (!hasAimPoint)
                return;

            var lookDirection = aimPoint - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude <= 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        private bool IsReplayInputSuppressed()
        {
            if (!_isReplayBound)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                    return false;

                _em = world.EntityManager;
                _playerQuery = _em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadWrite<PlayerInputIntentComponent>());
                _replayQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ReplayInputControlComponent>());
                _isReplayBound = true;
            }

            return ReplayInputSuppressionUtility.IsLiveInputSuppressed(_em, _replayQuery);
        }

        private void PublishInputIntent(float2 moveAxis, bool hasAimPoint, float2 aimWorldXZ)
        {
            if (!_isReplayBound || _playerQuery.IsEmptyIgnoreFilter)
                return;

            var playerEntity = _playerQuery.GetSingletonEntity();
            var intent = _em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
            intent.MoveAxis = moveAxis;
            intent.AimWorldXZ = aimWorldXZ;
            intent.HasAimWorldPoint = (byte)(hasAimPoint ? 1 : 0);
            _em.SetComponentData(playerEntity, intent);
        }
    }
}
