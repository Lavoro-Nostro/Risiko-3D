using UnityEngine;
using UnityEngine.InputSystem;

namespace Risiko3D.Runtime.Input
{
    public sealed class BoardInputActionsAdapter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActionsAsset;
        [SerializeField] private string actionMapName = "Board";

        private InputAction _point;
        private InputAction _click;
        private InputAction _orbitHold;
        private InputAction _panHold;
        private InputAction _lookDelta;
        private InputAction _scroll;
        private InputAction _resetCamera;
        private InputAction _submitCommand;
        private InputAction _captureSnapshot;
        private InputAction _simulateReconnect;
        private InputAction _endTurn;
        private InputAction _showTerritoryNames;
        private InputAction _decreaseActionValue;
        private InputAction _increaseActionValue;
        private InputActionMap _map;

        private void Awake()
        {
            if (inputActionsAsset == null)
            {
                inputActionsAsset = Resources.Load<InputActionAsset>("Input/BoardControls");
            }

            if (inputActionsAsset == null)
            {
                Debug.LogError("[Risiko3D][Input] BoardControls input asset not found at Resources/Input/BoardControls.");
                return;
            }

            _map = inputActionsAsset.FindActionMap(actionMapName, true);
            _point = _map.FindAction("Point", true);
            _click = _map.FindAction("Click", true);
            _orbitHold = _map.FindAction("OrbitHold", true);
            _panHold = _map.FindAction("PanHold", true);
            _lookDelta = _map.FindAction("LookDelta", true);
            _scroll = _map.FindAction("Scroll", true);
            _resetCamera = _map.FindAction("ResetCamera", true);
            _submitCommand = _map.FindAction("SubmitCommand", true);
            _captureSnapshot = _map.FindAction("CaptureSnapshot", true);
            _simulateReconnect = _map.FindAction("SimulateReconnect", true);
            _endTurn = _map.FindAction("EndTurn", true);
            _showTerritoryNames = _map.FindAction("ShowTerritoryNames", true);
            _decreaseActionValue = _map.FindAction("DecreaseActionValue", true);
            _increaseActionValue = _map.FindAction("IncreaseActionValue", true);
        }

        private void OnEnable()
        {
            _map?.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
        }

        public bool IsReady => _map != null;

        public bool WasPrimaryPressedThisFrame() => _click != null && _click.WasPressedThisFrame();
        public bool IsOrbitHeld() => _orbitHold != null && _orbitHold.IsPressed();
        public bool IsPanHeld() => _panHold != null && _panHold.IsPressed();
        public Vector2 GetPointerDelta() => _lookDelta != null ? _lookDelta.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 GetPointerScreenPosition() => _point != null ? _point.ReadValue<Vector2>() : Vector2.zero;
        public float GetScrollY() => _scroll != null ? _scroll.ReadValue<Vector2>().y / 120f : 0f;
        public bool WasResetPressedThisFrame() => _resetCamera != null && _resetCamera.WasPressedThisFrame();
        public bool WasSubmitCommandPressedThisFrame() => _submitCommand != null && _submitCommand.WasPressedThisFrame();
        public bool WasCaptureSnapshotPressedThisFrame() => _captureSnapshot != null && _captureSnapshot.WasPressedThisFrame();
        public bool WasSimulateReconnectPressedThisFrame() => _simulateReconnect != null && _simulateReconnect.WasPressedThisFrame();
        public bool WasEndTurnPressedThisFrame() => _endTurn != null && _endTurn.WasPressedThisFrame();
        public bool IsShowTerritoryNamesHeld() => _showTerritoryNames != null && _showTerritoryNames.IsPressed();
        public bool WasDecreaseActionValuePressedThisFrame() => _decreaseActionValue != null && _decreaseActionValue.WasPressedThisFrame();
        public bool WasIncreaseActionValuePressedThisFrame() => _increaseActionValue != null && _increaseActionValue.WasPressedThisFrame();
    }
}
