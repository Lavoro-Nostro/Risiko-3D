using Risiko3D.Runtime.Input;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardCameraController : MonoBehaviour
    {
        [SerializeField] private Vector3 target = Vector3.zero;
        [SerializeField] private float distance = 20f;
        [SerializeField] private float minDistance = 4.5f;
        [SerializeField] private float maxDistance = 40f;
        [SerializeField] private float yaw = 25f;
        [SerializeField] private float pitch = 45f;
        [SerializeField] private float rotateSpeed = 130f;
        [SerializeField] private float panSpeed = 0.06f;
        [SerializeField] private float zoomSpeed = 9f;
        private BoardInputActionsAdapter _input;

        private Vector3 _defaultTarget;
        private float _defaultDistance;
        private float _defaultYaw;
        private float _defaultPitch;

        private void Awake()
        {
            _defaultTarget = target;
            _defaultDistance = distance;
            _defaultYaw = yaw;
            _defaultPitch = pitch;
        }

        private void LateUpdate()
        {
            if (_input == null || !_input.IsReady)
            {
                return;
            }

            var delta = _input.GetPointerDelta();

            if (_input.IsOrbitHeld())
            {
                yaw += delta.x * rotateSpeed * Time.deltaTime;
                pitch -= delta.y * rotateSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, 18f, 80f);
            }

            if (_input.IsPanHeld())
            {
                var right = transform.right;
                var forward = Vector3.Cross(right, Vector3.up).normalized;
                target -= right * (delta.x * panSpeed * distance * Time.deltaTime);
                target -= forward * (delta.y * panSpeed * distance * Time.deltaTime);
            }

            var wheel = _input.GetScrollY();
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                distance -= wheel * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            if (_input.WasResetPressedThisFrame())
            {
                target = _defaultTarget;
                distance = _defaultDistance;
                yaw = _defaultYaw;
                pitch = _defaultPitch;
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var offset = rotation * new Vector3(0f, 0f, -distance);
            transform.position = target + offset;
            transform.rotation = rotation;
        }

        public void SetInput(BoardInputActionsAdapter inputAdapter)
        {
            _input = inputAdapter;
        }

        public void FrameTarget(Vector3 worldPos)
        {
            target = worldPos;
        }
    }
}
