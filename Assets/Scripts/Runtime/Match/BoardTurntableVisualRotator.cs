using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Steam;
using UnityEngine;
using System.Collections.Generic;

namespace Risiko3D.Runtime.Match
{
    // Rotates a root to face the active seat. Can be used for visual-only roots or full board stacks.
    public sealed class BoardTurntableVisualRotator : MonoBehaviour
    {
        private GameRuntimeConfig _config;
        private HostAuthoritativeMatchLoop _loop;
        private Transform _visualRoot;
        private BoardBootstrap _board;
        private SteamLobbyService _lobby;
        private readonly List<Transform> _rotateRoots = new();
        private float _targetYaw;
        private float _currentYaw;
        private bool _hasTarget;
        private bool _didInitialSnap;
        private bool _verboseLogs;
        private int _lastActivePlayerIndex = -1;

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
            _verboseLogs = _config != null && _config.EnableVerboseRuntimeLogs;
        }

        private void Update()
        {
            if (_config == null || !_config.EnableTurntableVisualRotation)
            {
                return;
            }

            if (_loop == null)
            {
                _loop = Object.FindFirstObjectByType<HostAuthoritativeMatchLoop>();
                if (_loop == null)
                {
                    return;
                }
            }

            if (_lobby == null)
            {
                _lobby = Object.FindFirstObjectByType<SteamLobbyService>();
            }

            if (_visualRoot == null)
            {
                _visualRoot = ResolveVisualRoot(_config.TurntableVisualRootName);
                _board = Object.FindFirstObjectByType<BoardBootstrap>();
                RebuildRotateRoots();
                if (_rotateRoots.Count == 0)
                {
                    return;
                }

                _currentYaw = _rotateRoots[0].eulerAngles.y;
                Trace($"roots={_rotateRoots.Count} root='{_config.TurntableVisualRootName}' initialYaw={_currentYaw:0.00}");
            }

            var activePlayerIndex = ResolveTargetActivePlayerIndex();
            if (activePlayerIndex != _lastActivePlayerIndex)
            {
                _lastActivePlayerIndex = activePlayerIndex;
                if (TryComputeTargetYaw(activePlayerIndex, out var yaw))
                {
                    // Face active seat explicitly; model forward for this table is opposite of seat-facing yaw.
                    _targetYaw = yaw + 180f + _config.TurntableYawOffset;
                    _hasTarget = true;
                    Trace($"target active={activePlayerIndex} targetYaw={_targetYaw:0.00}");
                    if (!_didInitialSnap)
                    {
                        SnapImmediatelyToTarget();
                    }
                }
            }

            if (!_hasTarget)
            {
                return;
            }

            var speed = Mathf.Max(1f, _config.TurntableRotateSpeedDegPerSec);
            var next = Mathf.MoveTowardsAngle(_currentYaw, _targetYaw, speed * Time.deltaTime);
            var delta = Mathf.DeltaAngle(_currentYaw, next);
            if (Mathf.Abs(delta) > 0.0001f)
            {
                var pivotRef = _rotateRoots.Count > 0 ? _rotateRoots[0].position : Vector3.zero;
                var center = ResolveBoardCenter(pivotRef);
                for (var i = 0; i < _rotateRoots.Count; i++)
                {
                    var root = _rotateRoots[i];
                    if (root == null)
                    {
                        continue;
                    }

                    root.RotateAround(center, Vector3.up, delta);
                }

                _currentYaw = next;
            }
        }

        private int ResolveTargetActivePlayerIndex()
        {
            if (_loop == null)
            {
                return 0;
            }

            if (_lobby != null && _lobby.IsInLobby && !_lobby.IsLocalHost)
            {
                if (_lobby.TryGetActiveTurnIndex(out var synced, out _) && synced >= 0)
                {
                    return synced;
                }

                if (_lastActivePlayerIndex >= 0)
                {
                    return _lastActivePlayerIndex;
                }
            }

            return _loop.ActivePlayerIndex;
        }

        private void RebuildRotateRoots()
        {
            _rotateRoots.Clear();
            AddRotateRoot(_visualRoot);
            AddRotateRoot(_board != null ? _board.transform : null);
            PruneDescendantRoots();
        }

        private void AddRotateRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = 0; i < _rotateRoots.Count; i++)
            {
                if (_rotateRoots[i] == root)
                {
                    return;
                }
            }

            _rotateRoots.Add(root);
        }

        private void PruneDescendantRoots()
        {
            for (var i = _rotateRoots.Count - 1; i >= 0; i--)
            {
                var child = _rotateRoots[i];
                if (child == null)
                {
                    _rotateRoots.RemoveAt(i);
                    continue;
                }

                for (var j = 0; j < _rotateRoots.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var parent = _rotateRoots[j];
                    if (parent != null && child.IsChildOf(parent))
                    {
                        _rotateRoots.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private bool TryComputeTargetYaw(int activePlayerIndex, out float yaw)
        {
            yaw = 0f;
            var seatNumber = Mathf.Clamp(activePlayerIndex + 1, 1, 6);
            var seatAnchors = Object.FindObjectsByType<BoardPlayerSeatAnchor>(FindObjectsSortMode.None);
            if (seatAnchors == null || seatAnchors.Length == 0)
            {
                return false;
            }

            Transform seatAnchor = null;
            for (var i = 0; i < seatAnchors.Length; i++)
            {
                var seat = seatAnchors[i];
                if (seat != null && seat.SeatNumber == seatNumber)
                {
                    seatAnchor = seat.ResolveCameraAnchor() ?? seat.transform;
                    break;
                }
            }

            if (seatAnchor == null)
            {
                return false;
            }

            var center = ResolveBoardCenter(seatAnchor.position);
            var toSeat = seatAnchor.position - center;
            toSeat.y = 0f;
            if (toSeat.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            yaw = Mathf.Atan2(toSeat.x, toSeat.z) * Mathf.Rad2Deg;
            return true;
        }

        private static Transform ResolveVisualRoot(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                return null;
            }

            var go = GameObject.Find(nameOrPath);
            if (go != null)
            {
                return go.transform;
            }

            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name == nameOrPath)
                {
                    return t;
                }
            }

            return null;
        }

        private static Vector3 ResolveBoardCenter(Vector3 fromPosition)
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var found = false;
            var bestDistance = float.MaxValue;
            var bestCenter = Vector3.zero;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject == null || r.gameObject.name != "Board_Back")
                {
                    continue;
                }

                var center = r.bounds.center;
                var d = (center - fromPosition).sqrMagnitude;
                if (!found || d < bestDistance)
                {
                    found = true;
                    bestDistance = d;
                    bestCenter = center;
                }
            }

            if (found)
            {
                return bestCenter;
            }

            var board = Object.FindFirstObjectByType<BoardBootstrap>();
            return board != null ? board.transform.position : Vector3.zero;
        }

        private void SnapImmediatelyToTarget()
        {
            if (!_hasTarget)
            {
                return;
            }

            var delta = Mathf.DeltaAngle(_currentYaw, _targetYaw);
            if (Mathf.Abs(delta) > 0.0001f)
            {
                var pivotRef = _rotateRoots.Count > 0 ? _rotateRoots[0].position : Vector3.zero;
                var center = ResolveBoardCenter(pivotRef);
                for (var i = 0; i < _rotateRoots.Count; i++)
                {
                    var root = _rotateRoots[i];
                    if (root == null)
                    {
                        continue;
                    }

                    root.RotateAround(center, Vector3.up, delta);
                }
            }

            _currentYaw = _targetYaw;
            _didInitialSnap = true;
            Trace($"initial snap complete yaw={_currentYaw:0.00}");
        }

        private void Trace(string message)
        {
            // Turntable trace intentionally disabled to keep logs focused on authoritative match state.
        }
    }
}
