using System;
using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Match
{
    public sealed class BoardSeatCameraSpawner : MonoBehaviour
    {
        private GameRuntimeConfig _config;
        private HostAuthoritativeMatchLoop _loop;
        private bool _applied;

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
        }

        private void Update()
        {
            if (_config != null && !_config.UseSeatAnchorsForCameraSpawn)
            {
                return;
            }

            if (_applied)
            {
                return;
            }

            if (_loop == null)
            {
                _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
                if (_loop == null)
                {
                    return;
                }
            }

            var seatAnchors = UnityEngine.Object.FindObjectsByType<BoardPlayerSeatAnchor>(FindObjectsSortMode.None);
            if (seatAnchors == null || seatAnchors.Length == 0)
            {
                return;
            }

            var seatIndex = ResolveLocalSeatIndex(_loop, _config);
            var seatNumber = seatIndex + 1;
            for (var i = 0; i < seatAnchors.Length; i++)
            {
                var seat = seatAnchors[i];
                if (seat == null || seat.SeatNumber != seatNumber)
                {
                    continue;
                }

                var cameraAnchor = seat.ResolveCameraAnchor();
                if (cameraAnchor == null)
                {
                    return;
                }

                ApplyCameraPose(cameraAnchor, ResolveBoardCenter(cameraAnchor.position));
                _applied = true;
                return;
            }
        }

        private static void ApplyCameraPose(Transform anchor, Vector3 boardCenter)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var toBoard = boardCenter - anchor.position;
            Quaternion rotation;
            if (toBoard.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(toBoard.normalized, Vector3.up);
            }
            else
            {
                rotation = anchor.rotation;
            }

            var controller = cam.GetComponent<BoardCameraController>();
            if (controller != null)
            {
                controller.AttachToAnchor(anchor, boardCenter);
            }
            else
            {
                cam.transform.SetParent(anchor, false);
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.Inverse(anchor.rotation) * rotation;
            }
        }

        private static Vector3 ResolveBoardCenter(Vector3 fromPosition)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var found = false;
            var bestDistance = float.MaxValue;
            var bestCenter = Vector3.zero;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject == null)
                {
                    continue;
                }

                if (r.gameObject.name == "Board_Back")
                {
                    var center = r.bounds.center;
                    var d = (center - fromPosition).sqrMagnitude;
                    if (!found || d < bestDistance)
                    {
                        found = true;
                        bestDistance = d;
                        bestCenter = center;
                    }
                }
            }

            if (found)
            {
                return bestCenter;
            }

            var board = UnityEngine.Object.FindFirstObjectByType<BoardBootstrap>();
            if (board != null)
            {
                return board.transform.position;
            }

            return Vector3.zero;
        }

        private static int ResolveLocalSeatIndex(HostAuthoritativeMatchLoop loop, GameRuntimeConfig config)
        {
            if (loop == null)
            {
                return 0;
            }

            var players = loop.GetPlayerVisualData();
            if (players == null || players.Count == 0)
            {
                return 0;
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.LocalPerspectivePlayerId))
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (string.Equals(players[i].PlayerId, config.LocalPerspectivePlayerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return Mathf.Clamp(i, 0, 5);
                    }
                }
            }

            return Mathf.Clamp(loop.LocalPlayerIndex, 0, Mathf.Max(0, players.Count - 1));
        }
    }
}
