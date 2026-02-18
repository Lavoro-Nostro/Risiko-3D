using System.Collections.Generic;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    // Animated dashed border for currently selected territory.
    public sealed class TerritorySelectionOverlay : MonoBehaviour
    {
        private sealed class DashVisual
        {
            public LineRenderer Core;
            public LineRenderer Glow;
        }

        private sealed class LoopDashData
        {
            public Vector3[] Points;
            public float[] Cumulative;
            public float TotalLength;
            public List<DashVisual> Dashes;
        }

        private readonly List<LoopDashData> _loops = new();
        private Transform _root;
        private Material _dashMaterial;

        private float _overlayY = 0.08f;
        private float _phaseDistance;
        private bool _visible;
        private float _pulseTime;

        private const float DashLength = 0.26f;
        private const float GapLength = 0.12f;
        private const float ScrollSpeed = 0.70f;
        private const int DashCurveSamples = 8;
        private const float PulseSpeed = 2.4f;
        private const float GlowWidthBase = 0.082f;
        private const float GlowWidthPulse = 0.020f;
        private const float BorderInset = 0.000f;

        public void Configure(float overlayY)
        {
            _overlayY = overlayY;
            EnsureRoot();
        }

        public void Show(List<List<Vector2>> polygons)
        {
            EnsureRoot();
            ClearCurrent();

            if (polygons == null || polygons.Count == 0)
            {
                Hide();
                return;
            }

            foreach (var polygon in polygons)
            {
                if (polygon == null || polygon.Count < 3)
                {
                    continue;
                }

                var loop = BuildLoop(polygon);
                if (loop != null)
                {
                    _loops.Add(loop);
                }
            }

            _phaseDistance = 0f;
            _visible = _loops.Count > 0;
            _root.gameObject.SetActive(_visible);
        }

        public void Hide()
        {
            _visible = false;
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_visible || _loops.Count == 0)
            {
                return;
            }

            var pattern = DashLength + GapLength;
            _phaseDistance += ScrollSpeed * Time.deltaTime;
            _pulseTime += PulseSpeed * Time.deltaTime;
            _phaseDistance = RepeatDistance(_phaseDistance, pattern);

            foreach (var loop in _loops)
            {
                UpdateLoopDashPositions(loop);
            }
        }

        private void EnsureRoot()
        {
            if (_root == null)
            {
                var rootGo = new GameObject("SelectionOverlay");
                rootGo.transform.SetParent(transform, false);
                _root = rootGo.transform;
            }

            if (_dashMaterial == null)
            {
                _dashMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void ClearCurrent()
        {
            _loops.Clear();
            if (_root == null)
            {
                return;
            }

            for (var i = _root.childCount - 1; i >= 0; i--)
            {
                var child = _root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private LoopDashData BuildLoop(List<Vector2> polygon)
        {
            var sampled = SamplePolygon(polygon, _overlayY);
            if (sampled.Length < 3)
            {
                return null;
            }

            var cumulative = BuildCumulativeLengths(sampled, out var totalLength);
            if (totalLength <= 0.001f)
            {
                return null;
            }

            var pattern = DashLength + GapLength;
            var dashCount = Mathf.Clamp(Mathf.CeilToInt(totalLength / pattern) + 1, 12, 220);
            var loopRoot = new GameObject("DashLoop");
            loopRoot.transform.SetParent(_root, false);

            var dashRenderers = new List<DashVisual>(dashCount);
            for (var i = 0; i < dashCount; i++)
            {
                var dashGo = new GameObject($"Dash_{i:D3}");
                dashGo.transform.SetParent(loopRoot.transform, false);

                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(dashGo.transform, false);
                var glow = glowGo.AddComponent<LineRenderer>();
                ConfigureRenderer(glow, GlowWidthBase, new Color(1f, 0.85f, 0.20f, 0.22f), new Color(1f, 0.95f, 0.48f, 0.08f), 2, 2);

                var coreGo = new GameObject("Core");
                coreGo.transform.SetParent(dashGo.transform, false);
                var core = coreGo.AddComponent<LineRenderer>();
                ConfigureRenderer(core, 0.045f, new Color(1f, 0.97f, 0.70f, 0.96f), new Color(1f, 0.82f, 0.28f, 0.96f), 3, 3);

                dashRenderers.Add(new DashVisual
                {
                    Core = core,
                    Glow = glow
                });
            }

            var loop = new LoopDashData
            {
                Points = sampled,
                Cumulative = cumulative,
                TotalLength = totalLength,
                Dashes = dashRenderers
            };

            UpdateLoopDashPositions(loop);
            return loop;
        }

        private void UpdateLoopDashPositions(LoopDashData loop)
        {
            var dashCount = loop.Dashes.Count;
            if (dashCount == 0 || loop.TotalLength <= 0.001f)
            {
                return;
            }

            var pulse = 0.5f + (0.5f * Mathf.Sin(_pulseTime));
            var glowWidth = GlowWidthBase + (GlowWidthPulse * pulse);
            var glowAlphaA = Mathf.Lerp(0.16f, 0.30f, pulse);
            var glowAlphaB = Mathf.Lerp(0.05f, 0.14f, pulse);
            var pattern = DashLength + GapLength;
            for (var i = 0; i < dashCount; i++)
            {
                var startDistanceRaw = (i * pattern) + _phaseDistance;
                var startDistance = RepeatDistance(startDistanceRaw, loop.TotalLength);
                var dash = loop.Dashes[i];
                var core = dash.Core;
                var glow = dash.Glow;

                // Avoid seam jumps: never draw a dash that crosses the loop endpoint.
                var availableLength = loop.TotalLength - startDistance;
                var segmentLength = Mathf.Min(DashLength, availableLength);
                var visible = startDistanceRaw <= (loop.TotalLength + DashLength) && segmentLength > 0.015f;
                var coreColorA = visible ? new Color(1f, 0.97f, 0.70f, 0.96f) : new Color(0f, 0f, 0f, 0f);
                var coreColorB = visible ? new Color(1f, 0.82f, 0.28f, 0.96f) : new Color(0f, 0f, 0f, 0f);
                core.startColor = coreColorA;
                core.endColor = coreColorB;
                glow.startWidth = glowWidth;
                glow.endWidth = glowWidth;
                glow.startColor = visible ? new Color(1f, 0.86f, 0.26f, glowAlphaA) : new Color(0f, 0f, 0f, 0f);
                glow.endColor = visible ? new Color(1f, 0.96f, 0.60f, glowAlphaB) : new Color(0f, 0f, 0f, 0f);

                for (var s = 0; s < DashCurveSamples; s++)
                {
                    var t = DashCurveSamples == 1 ? 0f : s / (float)(DashCurveSamples - 1);
                    var d = startDistance + (segmentLength * t);
                    var p = SampleAtDistance(loop.Points, loop.Cumulative, loop.TotalLength, d);
                    core.SetPosition(s, p);
                    glow.SetPosition(s, p);
                }
            }
        }

        private void ConfigureRenderer(
            LineRenderer lr,
            float width,
            Color startColor,
            Color endColor,
            int capVertices,
            int cornerVertices)
        {
            lr.material = _dashMaterial;
            lr.positionCount = DashCurveSamples;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = startColor;
            lr.endColor = endColor;
            lr.numCapVertices = capVertices;
            lr.numCornerVertices = cornerVertices;
        }

        private static Vector3[] SamplePolygon(List<Vector2> polygon, float y)
        {
            var points = new List<Vector3>(polygon.Count + 1);
            var centroid = ComputeCentroid(polygon);
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var v = new Vector2(p.x, p.y);
                var inset = Vector2.Lerp(v, centroid, BorderInset);
                points.Add(new Vector3(inset.x, y, inset.y));
            }

            if (points.Count > 0 && Vector3.Distance(points[0], points[points.Count - 1]) > 0.001f)
            {
                points.Add(points[0]);
            }

            return SmoothClosedPolyline(points.ToArray(), 0);
        }

        private static float[] BuildCumulativeLengths(Vector3[] points, out float total)
        {
            var cumulative = new float[points.Length];
            total = 0f;
            cumulative[0] = 0f;
            for (var i = 1; i < points.Length; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                cumulative[i] = total;
            }

            return cumulative;
        }

        private static Vector3 SampleAtDistance(Vector3[] points, float[] cumulative, float total, float distance)
        {
            if (points.Length == 0)
            {
                return Vector3.zero;
            }

            if (points.Length == 1 || total <= 0.0001f)
            {
                return points[0];
            }

            distance = RepeatDistance(distance, total);
            for (var i = 1; i < cumulative.Length; i++)
            {
                if (distance > cumulative[i])
                {
                    continue;
                }

                var segStart = cumulative[i - 1];
                var segLen = Mathf.Max(0.00001f, cumulative[i] - segStart);
                var t = Mathf.Clamp01((distance - segStart) / segLen);
                return Vector3.Lerp(points[i - 1], points[i], t);
            }

            return points[points.Length - 1];
        }

        private static float RepeatDistance(float value, float length)
        {
            if (length <= 0.0001f)
            {
                return 0f;
            }

            value %= length;
            if (value < 0f)
            {
                value += length;
            }

            return value;
        }

        private static Vector3[] SmoothClosedPolyline(Vector3[] points, int iterations)
        {
            if (points == null || points.Length < 4 || iterations <= 0)
            {
                return points;
            }

            var current = points;
            for (var it = 0; it < iterations; it++)
            {
                var next = new List<Vector3>(current.Length * 2);
                var last = current.Length - 1;
                for (var i = 0; i < last; i++)
                {
                    var p0 = current[i];
                    var p1 = current[i + 1];
                    var q = Vector3.Lerp(p0, p1, 0.25f);
                    var r = Vector3.Lerp(p0, p1, 0.75f);
                    next.Add(q);
                    next.Add(r);
                }

                if (next.Count > 0 && Vector3.Distance(next[0], next[next.Count - 1]) > 0.0001f)
                {
                    next.Add(next[0]);
                }

                current = next.ToArray();
                if (current.Length < 4)
                {
                    break;
                }
            }

            return current;
        }

        private static Vector2 ComputeCentroid(List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count == 0)
            {
                return Vector2.zero;
            }

            var sum = Vector2.zero;
            foreach (var p in polygon)
            {
                sum += p;
            }

            return sum / polygon.Count;
        }
    }
}
