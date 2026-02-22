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
        [SerializeField] private float zoomSensitivityMultiplier = 3.2f;
        [SerializeField] private float zoomSmoothing = 14f;
        [SerializeField] private float zoomedInPitch = 78f;
        [SerializeField, Range(0f, 1f)] private float zoomPitchBlend = 0.70f;
        [SerializeField] private float zoomLeanForward = 3.8f;
        [SerializeField] private float zoomLeanDown = 1.1f;
        [SerializeField] private bool useFovZoom = true;
        [SerializeField] private float maxFieldOfView = 60f;
        [SerializeField] private float minFieldOfView = 40f;
        [SerializeField] private float headTurnSpeed = 0.14f;
        [SerializeField] private float maxHeadYaw = 70f;
        [SerializeField] private float maxHeadPitch = 55f;
        [SerializeField] private float defaultDownLookPitch = 18f;
        private BoardInputActionsAdapter _input;

        private Vector3 _defaultTarget;
        private float _defaultDistance;
        private float _defaultYaw;
        private float _defaultPitch;
        private float _desiredDistance;
        private Camera _camera;
        private bool _anchorLockedMode;
        private Transform _anchorTransform;
        private float _headYaw;
        private float _headPitch;
        private float _anchorBaseDistance;
        private Vector3 _anchorLookTarget;

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

            if (_anchorLockedMode && _anchorTransform != null)
            {
                UpdateAnchorLockedMode();
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
                _desiredDistance = Mathf.Clamp(_desiredDistance - (wheel * zoomStep * zoomSensitivityMultiplier), minDistance, maxDistance);
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

        private void UpdateAnchorLockedMode()
        {
            var delta = _input.GetPointerDelta();
            if (_input.IsPanHeld() || _input.IsOrbitHeld())
            {
                _headYaw += delta.x * headTurnSpeed;
                _headPitch -= delta.y * headTurnSpeed;
                _headYaw = Mathf.Clamp(_headYaw, -maxHeadYaw, maxHeadYaw);
                _headPitch = Mathf.Clamp(_headPitch, -maxHeadPitch, maxHeadPitch);
            }

            var wheel = _input.GetScrollY();
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                _desiredDistance = Mathf.Clamp(_desiredDistance - (wheel * zoomStep * zoomSensitivityMultiplier), minDistance, maxDistance);
            }

            if (_input.WasResetPressedThisFrame())
            {
                _headYaw = 0f;
                _headPitch = 0f;
                _desiredDistance = _defaultDistance;
                distance = _defaultDistance;
            }

            var zoomLerp = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
            distance = Mathf.Lerp(distance, _desiredDistance, zoomLerp);
            var zoom01 = Mathf.InverseLerp(maxDistance, minDistance, distance);

            // Keep camera always level in world-space (roll = 0), independent from anchor skew/roll.
            var toTarget = _anchorLookTarget - _anchorTransform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                toTarget = _anchorTransform.forward;
            }

            var baseYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            var baseFlat = new Vector2(toTarget.x, toTarget.z).magnitude;
            var basePitch = Mathf.Atan2(toTarget.y, baseFlat) * Mathf.Rad2Deg;
            var worldRotation = Quaternion.Euler(basePitch + defaultDownLookPitch + _headPitch, baseYaw + _headYaw, 0f);

            // Anchor-locked zoom: combine FOV with a small forward dolly so zoom is clearly visible.
            var dolly = Mathf.Max(0f, _anchorBaseDistance - distance);
            var worldPosition = _anchorTransform.position + (worldRotation * Vector3.forward * (dolly * 0.72f));
            transform.SetPositionAndRotation(worldPosition, worldRotation);

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

        public void ApplySpawnPose(Vector3 worldPosition, Quaternion worldRotation, Vector3 worldTarget)
        {
            _anchorLockedMode = false;
            _anchorTransform = null;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            target = worldTarget;

            var toCamera = worldPosition - worldTarget;
            var spawnDistance = Mathf.Max(0.01f, toCamera.magnitude);
            // Keep seat-anchor spawn exact even when anchors are farther than default zoom range.
            if (spawnDistance > maxDistance)
            {
                maxDistance = spawnDistance;
            }

            if (spawnDistance < minDistance)
            {
                minDistance = Mathf.Max(0.01f, spawnDistance * 0.5f);
            }

            distance = spawnDistance;
            _desiredDistance = distance;

            var euler = worldRotation.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.Clamp(NormalizePitch(euler.x), 18f, 80f);

            _defaultTarget = target;
            _defaultDistance = distance;
            _defaultYaw = yaw;
            _defaultPitch = pitch;
        }

        public void AttachToAnchor(Transform anchor, Vector3 worldLookTarget)
        {
            if (anchor == null)
            {
                return;
            }

            _anchorLockedMode = true;
            _anchorTransform = anchor;
            _anchorLookTarget = worldLookTarget;
            transform.SetParent(anchor, true);
            _headYaw = 0f;
            _headPitch = 0f;

            target = worldLookTarget;
            var spawnDistance = Mathf.Max(0.01f, Vector3.Distance(anchor.position, worldLookTarget));
            if (spawnDistance > maxDistance)
            {
                maxDistance = spawnDistance;
            }

            if (spawnDistance < minDistance)
            {
                minDistance = Mathf.Max(0.01f, spawnDistance * 0.5f);
            }

            _anchorBaseDistance = spawnDistance;
            distance = spawnDistance;
            _desiredDistance = spawnDistance;
            _defaultTarget = target;
            _defaultDistance = distance;
            _defaultYaw = 0f;
            _defaultPitch = 0f;
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
