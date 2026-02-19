using UnityEngine;

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

        private void Update()
        {
            var moveX = 0f;
            var moveZ = 0f;

            if (Input.GetKey(KeyCode.A)) moveX -= 1f;
            if (Input.GetKey(KeyCode.D)) moveX += 1f;
            if (Input.GetKey(KeyCode.S)) moveZ -= 1f;
            if (Input.GetKey(KeyCode.W)) moveZ += 1f;

            var move = new Vector3(moveX, 0f, moveZ);
            if (move.sqrMagnitude > 1f) move.Normalize();

            transform.position += move * (MoveSpeed * Time.deltaTime);

            var cameraToUse = TargetCamera != null ? TargetCamera : Camera.main;
            if (cameraToUse == null) return;

            var ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out var enter)) return;

            var hitPoint = ray.GetPoint(enter);
            var lookDirection = hitPoint - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude <= 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}
