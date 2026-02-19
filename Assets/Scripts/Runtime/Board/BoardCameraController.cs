using Risiko3D.Runtime.Input;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardCameraController : MonoBehaviour
    {
        [SerializeField] private bool preserveScenePoseOnStart = true;
        [SerializeField] private Vector3 target = Vector3.zero;
        [SerializeField] private float distance = 20f;
        [SerializeField] private float minDistance = 4.5f;
        [SerializeField] private float maxDistance = 40f;
        [SerializeField] private float yaw = 25f;
        [SerializeField] private float pitch = 45f;
        [SerializeField] private float rotateSpeed = 72f;
        [SerializeField] private float panSpeed = 0.07f;
        [SerializeField] private float zoomedInPanBoost = 2.2f;
        [SerializeField] private float verticalPanBoost = 1.45f;
        [SerializeField] private float zoomStep = 2.8f;
        [SerializeField] private float zoomSmoothing = 14f;
        [SerializeField] private float zoomedInPitch = 78f;
        [SerializeField, Range(0f, 1f)] private float zoomPitchBlend = 0.70f;
        [SerializeField] private float zoomLeanForward = 3.8f;
        [SerializeField] private float zoomLeanDown = 1.1f;
        [SerializeField] private bool useFovZoom = true;
        [SerializeField] private float maxFieldOfView = 60f;
        [SerializeField] private float minFieldOfView = 40f;
        private BoardInputActionsAdapter _input;

        private Vector3 _defaultTarget;
        private float _defaultDistance;
        private float _defaultYaw;
        private float _defaultPitch;
        private float _desiredDistance;
        private Camera _camera;

        private void Awake()
        {
            if (preserveScenePoseOnStart)
            {
                var euler = transform.rotation.eulerAngles;
                yaw = euler.y;
                pitch = NormalizePitch(euler.x);
                pitch = Mathf.Clamp(pitch, 18f, 80f);
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
                target = transform.position + (transform.forward * distance);
            }

            _defaultTarget = target;
            _defaultDistance = distance;
            _defaultYaw = yaw;
            _defaultPitch = pitch;
            _desiredDistance = distance;
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
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
                var panZoomT = Mathf.InverseLerp(maxDistance, minDistance, distance);
                var panScalar = panSpeed * distance * Mathf.Lerp(1f, zoomedInPanBoost, panZoomT);
                target -= right * (delta.x * panScalar * Time.deltaTime);
                target -= forward * (delta.y * panScalar * verticalPanBoost * Time.deltaTime);
            }

            var wheel = _input.GetScrollY();
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                _desiredDistance = Mathf.Clamp(_desiredDistance - (wheel * zoomStep), minDistance, maxDistance);
            }

            if (_input.WasResetPressedThisFrame())
            {
                target = _defaultTarget;
                distance = _defaultDistance;
                _desiredDistance = _defaultDistance;
                yaw = _defaultYaw;
                pitch = _defaultPitch;
            }

            var zoomLerp = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
            distance = Mathf.Lerp(distance, _desiredDistance, zoomLerp);
            var zoom01 = Mathf.InverseLerp(maxDistance, minDistance, distance);
            var effectivePitch = Mathf.Lerp(pitch, zoomedInPitch, zoom01 * zoomPitchBlend);
            effectivePitch = Mathf.Clamp(effectivePitch, 18f, 84f);
            var rotation = Quaternion.Euler(effectivePitch, yaw, 0f);
            var forwardFlat = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            var leanWeight = Mathf.SmoothStep(0f, 1f, zoom01);
            var leanTarget = target + (forwardFlat * (zoomLeanForward * leanWeight)) + (Vector3.down * (zoomLeanDown * leanWeight));
            transform.position = leanTarget - ((rotation * Vector3.forward) * distance);
            transform.rotation = rotation;

            if (useFovZoom && _camera != null)
            {
                _camera.fieldOfView = Mathf.Lerp(maxFieldOfView, minFieldOfView, zoom01);
            }
        }

        public void SetInput(BoardInputActionsAdapter inputAdapter)
        {
            _input = inputAdapter;
        }

        public void FrameTarget(Vector3 worldPos)
        {
            target = worldPos;
        }

        private static float NormalizePitch(float pitchDegrees)
        {
            var p = pitchDegrees;
            if (p > 180f)
            {
                p -= 360f;
            }

            return p;
        }
    }
}
