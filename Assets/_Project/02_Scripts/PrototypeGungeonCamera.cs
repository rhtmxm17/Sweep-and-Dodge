using UnityEngine;

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

        private void Awake()
        {
            CacheCameraIfNeeded();
            _fixedRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                TryBindTarget();
                if (Target == null) return;
            }

            CacheCameraIfNeeded();

            var desiredLookAhead = ComputeLookAheadOffset();
            var lookAheadBlend = 1f - Mathf.Exp(-LookAheadLerpSpeed * Time.deltaTime);
            _currentLookAhead = Vector3.Lerp(_currentLookAhead, desiredLookAhead, lookAheadBlend);

            var desiredPosition = Target.position + FollowOffset + _currentLookAhead;
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

        private Vector3 ComputeLookAheadOffset()
        {
            if (TargetCamera == null) return Vector3.zero;

            var ray = TargetCamera.ScreenPointToRay(Input.mousePosition);
            if (!_groundPlane.Raycast(ray, out var enter)) return Vector3.zero;

            var hitPoint = ray.GetPoint(enter);
            var toAim = hitPoint - Target.position;
            toAim.y = 0f;

            var distance = toAim.magnitude;
            if (distance <= Mathf.Max(0.01f, MaxLookAhead * AimDeadZone)) return Vector3.zero;

            var lookAheadDistance = Mathf.Min(MaxLookAhead, distance * LookAheadDistanceFactor);
            return toAim / distance * lookAheadDistance;
        }
    }
}
