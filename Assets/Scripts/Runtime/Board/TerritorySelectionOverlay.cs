using System.Collections.Generic;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    // Modern selection contour: soft aura + crisp edge + moving pulse bead.
    public sealed class TerritorySelectionOverlay : MonoBehaviour
    {
        private sealed class LoopVisual
        {
            public Vector3[] Points;
            public float[] Cumulative;
            public float TotalLength;
            public LineRenderer Aura;
            public LineRenderer Edge;
            public Transform Pulse;
        }

        private readonly List<LoopVisual> _loops = new();
        private Transform _root;
        private Material _lineMaterial;
        private Material _pulseMaterial;

        private float _overlayY = 0.08f;
        private bool _visible;
        private float _time;
        private Vector3 _lastCenterWorld;
        private bool _hasLastCenter;

        private const float BorderInset = 0.0025f;
        private const float PulseSpeed = 1.1f;
        private const float AuraWidth = 0.420f;
        private const float EdgeWidth = 0.200f;

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

            _hasLastCenter = TryComputeCenterFromLoops(out _lastCenterWorld);

            _time = 0f;
            _visible = _loops.Count > 0;
            _root.gameObject.SetActive(_visible);
            UpdateVisuals(0f);
        }

        public void ShowFallback(Vector3 worldPosition, float radius)
        {
            EnsureRoot();
            ClearCurrent();

            var polygon = new List<Vector2>(32);
            var safeRadius = Mathf.Max(0.28f, radius * 1.65f);
            for (var i = 0; i < 32; i++)
            {
                var t = i / 32f;
                var a = t * Mathf.PI * 2f;
                polygon.Add(new Vector2(
                    worldPosition.x + (Mathf.Cos(a) * safeRadius),
                    worldPosition.z + (Mathf.Sin(a) * safeRadius)));
            }

            var previousY = _overlayY;
            _overlayY = worldPosition.y + 0.018f;
            var loop = BuildLoop(polygon);
            _overlayY = previousY;
            if (loop != null)
            {
                _loops.Add(loop);
            }
            _lastCenterWorld = new Vector3(worldPosition.x, _overlayY, worldPosition.z);
            _hasLastCenter = true;

            _time = 0f;
            _visible = _loops.Count > 0;
            _root.gameObject.SetActive(_visible);
            UpdateVisuals(0f);
        }

        public bool TryGetCurrentCenter(out Vector3 centerWorld)
        {
            centerWorld = _lastCenterWorld;
            return _visible && _hasLastCenter;
        }

        public void Hide()
        {
            _visible = false;
            _hasLastCenter = false;
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

            _time += Time.deltaTime;
            UpdateVisuals(Time.deltaTime);
        }

        private void UpdateVisuals(float _)
        {
            var pulse = 0.5f + (0.5f * Mathf.Sin(_time * 3.2f));
            foreach (var loop in _loops)
            {
                if (loop.Aura != null)
                {
                    loop.Aura.startWidth = AuraWidth + (0.110f * pulse);
                    loop.Aura.endWidth = loop.Aura.startWidth;
                    loop.Aura.startColor = new Color(1f, 0.82f, 0.22f, Mathf.Lerp(0.60f, 0.90f, pulse));
                    loop.Aura.endColor = new Color(1f, 0.95f, 0.55f, Mathf.Lerp(0.36f, 0.62f, pulse));
                }

                if (loop.Edge != null)
                {
                    loop.Edge.startColor = new Color(1f, 0.97f, 0.80f, 1.00f);
                    loop.Edge.endColor = new Color(1f, 0.74f, 0.20f, 1.00f);
                }

                if (loop.Pulse != null && loop.TotalLength > 0.001f)
                {
                    var d = RepeatDistance(_time * PulseSpeed * loop.TotalLength, loop.TotalLength);
                    var p = SampleAtDistance(loop.Points, loop.Cumulative, loop.TotalLength, d);
                    loop.Pulse.position = p + new Vector3(0f, 0.030f, 0f);
                    var s = 0.280f + (0.080f * pulse);
                    loop.Pulse.localScale = new Vector3(s, s, s);
                }
            }
        }

        private LoopVisual BuildLoop(List<Vector2> polygon)
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

            var loopRoot = new GameObject("SelectionLoop");
            loopRoot.transform.SetParent(_root, false);

            var auraGo = new GameObject("Aura");
            auraGo.transform.SetParent(loopRoot.transform, false);
            var aura = auraGo.AddComponent<LineRenderer>();
            ConfigureLoopRenderer(aura, AuraWidth, new Color(1f, 0.82f, 0.22f, 0.20f), new Color(1f, 0.95f, 0.55f, 0.08f), 2, 2);
            aura.positionCount = sampled.Length;
            aura.SetPositions(sampled);

            var edgeGo = new GameObject("Edge");
            edgeGo.transform.SetParent(loopRoot.transform, false);
            var edge = edgeGo.AddComponent<LineRenderer>();
            ConfigureLoopRenderer(edge, EdgeWidth, new Color(1f, 0.96f, 0.75f, 0.94f), new Color(1f, 0.74f, 0.20f, 0.90f), 4, 3);
            edge.positionCount = sampled.Length;
            edge.SetPositions(sampled);

            var pulse = CreatePulse(loopRoot.transform);
            return new LoopVisual
            {
                Points = sampled,
                Cumulative = cumulative,
                TotalLength = totalLength,
                Aura = aura,
                Edge = edge,
                Pulse = pulse
            };
        }

        private Transform CreatePulse(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pulse";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_pulseMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                    {
                        shader = Shader.Find("Standard");
                    }

                    _pulseMaterial = new Material(shader);
                    _pulseMaterial.color = new Color(1f, 0.95f, 0.65f, 1f);
                }

                renderer.material = _pulseMaterial;
            }

            return go.transform;
        }

        private void ConfigureLoopRenderer(
            LineRenderer lr,
            float width,
            Color startColor,
            Color endColor,
            int capVertices,
            int cornerVertices)
        {
            lr.material = _lineMaterial;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = startColor;
            lr.endColor = endColor;
            lr.numCapVertices = capVertices;
            lr.numCornerVertices = cornerVertices;
        }

        private void EnsureRoot()
        {
            if (_root == null)
            {
                var rootGo = new GameObject("SelectionOverlay");
                rootGo.transform.SetParent(transform, false);
                _root = rootGo.transform;
            }

            if (_lineMaterial == null)
            {
                _lineMaterial = new Material(Shader.Find("Sprites/Default"));
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

            if (points.Count > 0 && Vector3.Distance(points[0], points[^1]) > 0.001f)
            {
                points.Add(points[0]);
            }

            return SmoothClosedPolyline(points.ToArray(), 1);
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

            return points[^1];
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

                if (next.Count > 0 && Vector3.Distance(next[0], next[^1]) > 0.0001f)
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

        private bool TryComputeCenterFromLoops(out Vector3 center)
        {
            center = Vector3.zero;
            var count = 0;
            for (var i = 0; i < _loops.Count; i++)
            {
                var points = _loops[i].Points;
                if (points == null || points.Length == 0)
                {
                    continue;
                }

                for (var p = 0; p < points.Length; p++)
                {
                    center += points[p];
                    count++;
                }
            }

            if (count <= 0)
            {
                return false;
            }

            center /= count;
            return true;
        }
    }
}
