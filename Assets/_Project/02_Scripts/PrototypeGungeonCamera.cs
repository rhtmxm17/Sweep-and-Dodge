using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 프로토타입 플레이용 탑다운 슈터 카메라.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PrototypeGungeonCamera : MonoBehaviour
    {
        [Header("Follow")]
        public Transform Target;
        public Camera TargetCamera;
        public Vector3 FollowOffset = new(0f, 15f, -2f);
        public bool PreferEcsPlayerState = true;

        [Min(0.01f)]
        public float PositionSmoothTime = 0.08f;

        [Header("Aim Bias")]
        [Min(0f)]
        public float MaxLookAhead = 2.5f;

        [Min(0f)]
        public float LookAheadDistanceFactor = 0.35f;

        [Min(0f)]
        public float LookAheadLerpSpeed = 12f;

        [Range(0f, 1f)]
        public float AimDeadZone = 0.08f;

        private readonly Plane _groundPlane = new(Vector3.up, Vector3.zero);
        private Vector3 _positionVelocity;
        private Vector3 _currentLookAhead;
        private Quaternion _fixedRotation;
        private EntityManager _em;
        private EntityQuery _playerQuery;
        private bool _ecsBound;

        private void Awake()
        {
            CacheCameraIfNeeded();
            _fixedRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            CacheCameraIfNeeded();
            if (!TryGetTargetPosition(out var targetPosition))
                return;

            var desiredLookAhead = ComputeLookAheadOffset(targetPosition);
            var lookAheadBlend = 1f - Mathf.Exp(-LookAheadLerpSpeed * Time.deltaTime);
            _currentLookAhead = Vector3.Lerp(_currentLookAhead, desiredLookAhead, lookAheadBlend);

            var desiredPosition = targetPosition + FollowOffset + _currentLookAhead;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, PositionSmoothTime);
            transform.rotation = _fixedRotation;
        }

        private void CacheCameraIfNeeded()
        {
            if (TargetCamera != null) return;

            TargetCamera = GetComponent<Camera>();
            if (TargetCamera == null) TargetCamera = Camera.main;
        }

        private void TryBindTarget()
        {
            var player = FindAnyObjectByType<PlayerWasdMovement>();
            if (player != null) Target = player.transform;
        }

        private bool TryGetTargetPosition(out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (PreferEcsPlayerState && TryGetEcsPlayerEntity(out var playerEntity))
            {
                if (_em.HasComponent<LocalTransform>(playerEntity))
                {
                    var ecsPosition = _em.GetComponentData<LocalTransform>(playerEntity).Position;
                    targetPosition = new Vector3(ecsPosition.x, ecsPosition.y, ecsPosition.z);
                    return true;
                }

                if (_em.HasComponent<PlayerGoSyncComponent>(playerEntity))
                {
                    var syncPosition = _em.GetComponentData<PlayerGoSyncComponent>(playerEntity).Position;
                    targetPosition = new Vector3(syncPosition.x, syncPosition.y, syncPosition.z);
                    return true;
                }
            }

            if (Target == null)
                TryBindTarget();
            if (Target == null)
                return false;

            targetPosition = Target.position;
            return true;
        }

        private Vector3 ComputeLookAheadOffset(Vector3 targetPosition)
        {
            if (PreferEcsPlayerState && TryGetEcsPlayerEntity(out var playerEntity))
            {
                if (!_em.HasComponent<PlayerInputIntentComponent>(playerEntity))
                    return Vector3.zero;

                var intent = _em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
                if (intent.HasAimWorldPoint == 0)
                    return Vector3.zero;

                var toAimFromIntent = new Vector3(
                    intent.AimWorldXZ.x - targetPosition.x,
                    0f,
                    intent.AimWorldXZ.y - targetPosition.z);
                return ComputeLookAheadFromDirection(toAimFromIntent);
            }

            if (TargetCamera == null || Target == null)
                return Vector3.zero;
            var ray = TargetCamera.ScreenPointToRay(Input.mousePosition);
            if (!_groundPlane.Raycast(ray, out var enter))
                return Vector3.zero;

            var hitPoint = ray.GetPoint(enter);
            var toAim = hitPoint - targetPosition;
            toAim.y = 0f;
            return ComputeLookAheadFromDirection(toAim);
        }

        private Vector3 ComputeLookAheadFromDirection(Vector3 toAim)
        {
            var distance = toAim.magnitude;
            if (distance <= Mathf.Max(0.01f, MaxLookAhead * AimDeadZone))
                return Vector3.zero;

            var lookAheadDistance = Mathf.Min(MaxLookAhead, distance * LookAheadDistanceFactor);
            return toAim / distance * lookAheadDistance;
        }

        private bool TryGetEcsPlayerEntity(out Entity playerEntity)
        {
            playerEntity = Entity.Null;
            if (!PreferEcsPlayerState)
                return false;

            if (!_ecsBound)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                    return false;

                _em = world.EntityManager;
                _playerQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
                _ecsBound = true;
            }

            if (_playerQuery.IsEmptyIgnoreFilter)
                return false;

            playerEntity = _playerQuery.GetSingletonEntity();
            return playerEntity != Entity.Null;
        }
    }
}
