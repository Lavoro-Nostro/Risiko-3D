using UnityEngine;
using System.Collections.Generic;

namespace Risiko3D.Runtime.Board
{
    public sealed class TerritoryNode : MonoBehaviour
    {
        private const float GizmoRadius = 0.08f;
        private const int SelectionRingSegments = 48;
        private static readonly Color SelectionCoreColor = new Color(1f, 0.95f, 0.62f, 0.95f);
        private static readonly Color SelectionGlowColor = new Color(1f, 0.82f, 0.28f, 0.34f);
        private static Material s_selectionMaterial;

        public string TerritoryId { get; private set; }
        public string DisplayName { get; private set; }
        public string ContinentId { get; private set; }
        public Renderer Renderer { get; private set; }
        public Color BaseColor { get; private set; }
        public bool IsSelected { get; private set; }
        public int OwnerIndex { get; private set; } = -1;
        public int Armies { get; private set; }
        public float OverlayRadius => GetComponent<SphereCollider>() != null ? GetComponent<SphereCollider>().radius * 2.15f : 0.42f * _uiScale;

        private TextMesh _nameLabel;
        private TextMesh _armyLabel;
        private Transform _alwaysNameRoot;
        private TextMesh _alwaysNameText;
        private Transform _unitVisualRoot;
        private readonly List<GameObject> _tankMarkers = new();
        private readonly List<GameObject> _flagMarkers = new();
        private GameObject _tankMarkerPrefab;
        private float _tankMarkerScale = 0.012f;
        private Vector3 _tankMarkerLocalEuler = new Vector3(-90f, 0f, 0f);
        private float _tankMarkerLift = 0.045f;
        private GameObject _flagMarkerPrefab;
        private float _flagMarkerScale = 0.020f;
        private Vector3 _flagMarkerLocalEuler = new Vector3(-90f, 0f, 0f);
        private float _flagMarkerLift = 0.060f;
        private Transform _selectionFxRoot;
        private LineRenderer _selectionCore;
        private LineRenderer _selectionGlow;
        private LineRenderer _namePointerLine;
        private float _selectionPulse;
        private static Material s_unitMaterial;
        private float _uiScale = 1f;

        public void Initialize(string territoryId, string displayName, string continentId, Color baseColor)
        {
            TerritoryId = territoryId;
            DisplayName = displayName;
            ContinentId = continentId;
            BaseColor = baseColor;
            Renderer = GetComponent<Renderer>();
            CacheLabels();
            EnsureSelectionFx();
            EnsureUnitVisualRoot();
            EnsureNamePointer();
            EnsureAlwaysNameTag();
            SetDisplayName(displayName);
            ApplyColor(baseColor);
            SetSelected(false);
            SetTerritoryNameVisible(false);
        }

        public void SetUiScale(float uiScale)
        {
            _uiScale = Mathf.Clamp(uiScale, 1.4f, 6.0f);
            if (_alwaysNameText != null)
            {
                _alwaysNameText.characterSize = 0.095f * _uiScale;
            }

            if (_alwaysNameRoot != null)
            {
                _alwaysNameRoot.localPosition = new Vector3(0f, 0.30f * _uiScale, 0f);
            }

            UpdateUnitVisualRootPose();
        }

        public void ConfigureArmyMarkerVisuals(
            GameObject tankPrefab,
            float tankScale,
            Vector3 tankLocalEuler,
            float tankLift,
            GameObject flagPrefab,
            float flagScale,
            Vector3 flagLocalEuler,
            float flagLift)
        {
            _tankMarkerPrefab = tankPrefab;
            _tankMarkerScale = Mathf.Max(0.0005f, tankScale);
            _tankMarkerLocalEuler = tankLocalEuler;
            _tankMarkerLift = Mathf.Clamp(tankLift, 0.010f, 0.300f);
            _flagMarkerPrefab = flagPrefab;
            _flagMarkerScale = Mathf.Max(0.0005f, flagScale);
            _flagMarkerLocalEuler = flagLocalEuler;
            _flagMarkerLift = Mathf.Clamp(flagLift, 0.010f, 0.500f);
            UpdateUnitVisualRootPose();
            RefreshUnitMarkers(BaseColor, Armies);
        }

        public void ApplyColor(Color color)
        {
            if (Renderer != null && Renderer.material != null)
            {
                Renderer.material.color = color;
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_selectionFxRoot != null)
            {
                _selectionFxRoot.gameObject.SetActive(selected);
            }

            if (_namePointerLine != null)
            {
                _namePointerLine.enabled = selected;
            }

            if (!selected)
            {
                if (_nameLabel != null)
                {
                    _nameLabel.color = new Color(0.98f, 0.98f, 0.94f);
                    _nameLabel.fontStyle = FontStyle.Normal;
                }

                if (_armyLabel != null)
                {
                    _armyLabel.color = Color.white;
                    _armyLabel.fontStyle = FontStyle.Bold;
                }
            }
        }

        public void SetDisplayName(string text)
        {
            DisplayName = text;
            if (_nameLabel != null)
            {
                _nameLabel.text = text;
            }

            if (_alwaysNameText != null)
            {
                _alwaysNameText.text = text;
            }

        }

        public void SetTerritoryNameVisible(bool visible)
        {
            if (_alwaysNameRoot != null)
            {
                _alwaysNameRoot.gameObject.SetActive(visible);
            }

            if (_nameLabel != null)
            {
                _nameLabel.gameObject.SetActive(visible);
            }
        }

        public void SetOwnerAndArmies(int ownerIndex, Color ownerColor, int armies)
        {
            OwnerIndex = ownerIndex;
            Armies = armies;
            BaseColor = ownerColor;
            ApplyColor(ownerColor);
            if (_armyLabel != null)
            {
                _armyLabel.text = armies.ToString();
            }

            RefreshUnitMarkers(ownerColor, armies);
        }

        private void CacheLabels()
        {
            var all = GetComponentsInChildren<TextMesh>(true);
            foreach (var tm in all)
            {
                if (tm.name == "NameLabel")
                {
                    _nameLabel = tm;
                }
                else if (tm.name == "ArmyLabel")
                {
                    _armyLabel = tm;
                }
            }
        }

        private void EnsureSelectionFx()
        {
            if (_selectionFxRoot != null)
            {
                return;
            }

            _selectionFxRoot = new GameObject("SelectionFx").transform;
            _selectionFxRoot.SetParent(transform, false);
            _selectionFxRoot.localPosition = new Vector3(0f, -0.040f, 0f);
            _selectionFxRoot.localRotation = Quaternion.identity;

            _selectionGlow = CreateRing("GlowRing", 0.205f, 0.055f, SelectionGlowColor, 2, 2);
            _selectionCore = CreateRing("CoreRing", 0.185f, 0.030f, SelectionCoreColor, 3, 3);
        }

        private void EnsureNamePointer()
        {
            if (_namePointerLine != null)
            {
                return;
            }

            var pointer = new GameObject("NamePointer");
            pointer.transform.SetParent(transform, false);
            pointer.transform.localPosition = Vector3.zero;

            _namePointerLine = pointer.AddComponent<LineRenderer>();
            _namePointerLine.useWorldSpace = false;
            _namePointerLine.loop = false;
            _namePointerLine.positionCount = 2;
            _namePointerLine.startWidth = 0.032f * _uiScale;
            _namePointerLine.endWidth = 0.020f * _uiScale;
            _namePointerLine.numCapVertices = 3;
            _namePointerLine.numCornerVertices = 3;
            _namePointerLine.material = s_selectionMaterial ??= new Material(Shader.Find("Sprites/Default"));
            _namePointerLine.startColor = new Color(1f, 0.92f, 0.64f, 0.92f);
            _namePointerLine.endColor = new Color(1f, 0.92f, 0.64f, 0.35f);
            _namePointerLine.SetPosition(0, new Vector3(0f, 0.028f * _uiScale, 0f));
            _namePointerLine.SetPosition(1, new Vector3(0f, 0.280f * _uiScale, 0f));
            _namePointerLine.enabled = false;
        }

        private void EnsureAlwaysNameTag()
        {
            if (_alwaysNameRoot != null)
            {
                return;
            }

            _alwaysNameRoot = new GameObject("AlwaysNameTag").transform;
            _alwaysNameRoot.SetParent(transform, false);
            _alwaysNameRoot.localPosition = new Vector3(0f, 0.30f * _uiScale, 0f);
            _alwaysNameRoot.localRotation = Quaternion.identity;

            var textGo = new GameObject("AlwaysNameText");
            textGo.transform.SetParent(_alwaysNameRoot, false);
            _alwaysNameText = textGo.AddComponent<TextMesh>();
            _alwaysNameText.anchor = TextAnchor.MiddleCenter;
            _alwaysNameText.alignment = TextAlignment.Center;
            _alwaysNameText.fontSize = 72;
            _alwaysNameText.characterSize = 0.095f * _uiScale;
            _alwaysNameText.color = new Color(1f, 0.98f, 0.90f, 1f);
            _alwaysNameText.fontStyle = FontStyle.Bold;
            _alwaysNameText.text = string.IsNullOrWhiteSpace(DisplayName) ? TerritoryId : DisplayName;
        }

        public void SetOverlayAnchorWorld(Vector3 centerWorld)
        {
            if (_namePointerLine == null)
            {
                return;
            }

            _namePointerLine.SetPosition(0, new Vector3(0f, 0.028f * _uiScale, 0f));
            _namePointerLine.SetPosition(1, transform.InverseTransformPoint(centerWorld + new Vector3(0f, 0.14f * _uiScale, 0f)));
        }

        public void SetAlwaysNameAnchorWorld(Vector3 worldPosition)
        {
            if (_alwaysNameRoot == null)
            {
                return;
            }

            var world = worldPosition + new Vector3(0f, 0.30f * _uiScale, 0f);
            _alwaysNameRoot.localPosition = transform.InverseTransformPoint(world);
        }

        private void EnsureUnitVisualRoot()
        {
            if (_unitVisualRoot != null)
            {
                return;
            }

            _unitVisualRoot = new GameObject("UnitVisuals").transform;
            _unitVisualRoot.SetParent(transform, false);
            _unitVisualRoot.localPosition = new Vector3(0f, 0.022f * _uiScale, 0f);
            _unitVisualRoot.localRotation = Quaternion.identity;
        }

        private void UpdateUnitVisualRootPose()
        {
            if (_unitVisualRoot == null)
            {
                return;
            }

            _unitVisualRoot.localPosition = new Vector3(0f, 0.022f * _uiScale, 0f);
        }

        private void RefreshUnitMarkers(Color ownerColor, int armies)
        {
            EnsureUnitVisualRoot();
            if (_unitVisualRoot == null)
            {
                return;
            }

            var flagCount = Mathf.Max(0, armies / 10);
            var tankCount = Mathf.Max(0, armies % 10);

            EnsureMarkerCount(_flagMarkers, flagCount, CreateFlagMarker);
            EnsureMarkerCount(_tankMarkers, tankCount, CreateTankMarker);

            var markerColor = BoostMarkerColor(ownerColor);
            for (var i = 0; i < _flagMarkers.Count; i++)
            {
                var go = _flagMarkers[i];
                if (go == null)
                {
                    continue;
                }

                SetMarkerColor(go, markerColor);
                go.transform.localPosition = ComputeFlagLocalPosition(i, flagCount);
                go.transform.localRotation = Quaternion.Euler(0f, -22f + (i * 18f), 0f);
            }

            for (var i = 0; i < _tankMarkers.Count; i++)
            {
                var go = _tankMarkers[i];
                if (go == null)
                {
                    continue;
                }

                SetMarkerColor(go, markerColor);
                go.transform.localPosition = ComputeTankLocalPosition(i, tankCount);
                go.transform.localRotation = Quaternion.Euler(0f, (i * 31f) % 360f, 0f);
            }
        }

        private void EnsureMarkerCount(List<GameObject> cache, int targetCount, System.Func<GameObject> createFunc)
        {
            while (cache.Count < targetCount)
            {
                cache.Add(createFunc());
            }

            while (cache.Count > targetCount)
            {
                var last = cache[cache.Count - 1];
                cache.RemoveAt(cache.Count - 1);
                if (last != null)
                {
                    Destroy(last);
                }
            }

            for (var i = 0; i < cache.Count; i++)
            {
                if (cache[i] != null)
                {
                    cache[i].SetActive(true);
                }
            }
        }

        private GameObject CreateTankMarker()
        {
            var root = new GameObject("TankMarker");
            root.transform.SetParent(_unitVisualRoot, false);

            if (_tankMarkerPrefab != null)
            {
                var model = Instantiate(_tankMarkerPrefab, root.transform, false);
                model.name = "TankModel";
                model.transform.localPosition = new Vector3(0f, _tankMarkerLift * _uiScale, 0f);
                model.transform.localRotation = Quaternion.Euler(_tankMarkerLocalEuler);
                model.transform.localScale = Vector3.one * (_tankMarkerScale * _uiScale);
                RemoveCollidersRecursive(model.transform);
                return root;
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.13f, 0.032f, 0.08f) * _uiScale;
            body.transform.localPosition = new Vector3(0f, (0.016f + _tankMarkerLift), 0f) * _uiScale;
            RemoveCollider(body);
            ApplyMarkerMaterial(body);

            var turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turret.name = "Turret";
            turret.transform.SetParent(root.transform, false);
            turret.transform.localScale = new Vector3(0.032f, 0.013f, 0.032f) * _uiScale;
            turret.transform.localPosition = new Vector3(0.012f, (0.036f + _tankMarkerLift), 0f) * _uiScale;
            RemoveCollider(turret);
            ApplyMarkerMaterial(turret);

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localScale = new Vector3(0.068f, 0.010f, 0.010f) * _uiScale;
            barrel.transform.localPosition = new Vector3(0.048f, (0.036f + _tankMarkerLift), 0f) * _uiScale;
            RemoveCollider(barrel);
            ApplyMarkerMaterial(barrel);

            return root;
        }

        private GameObject CreateFlagMarker()
        {
            var root = new GameObject("FlagMarker");
            root.transform.SetParent(_unitVisualRoot, false);

            if (_flagMarkerPrefab != null)
            {
                var model = Instantiate(_flagMarkerPrefab, root.transform, false);
                model.name = "FlagModel";
                model.transform.localPosition = new Vector3(0f, _flagMarkerLift * _uiScale, 0f);
                model.transform.localRotation = Quaternion.Euler(_flagMarkerLocalEuler);
                model.transform.localScale = Vector3.one * (_flagMarkerScale * _uiScale);
                RemoveCollidersRecursive(model.transform);
                return root;
            }

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.010f, 0.100f, 0.010f) * _uiScale;
            pole.transform.localPosition = new Vector3(0f, 0.100f, 0f) * _uiScale;
            RemoveCollider(pole);
            ApplyMarkerMaterial(pole);

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Flag";
            flag.transform.SetParent(root.transform, false);
            flag.transform.localScale = new Vector3(0.090f, 0.050f, 0.008f) * _uiScale;
            flag.transform.localPosition = new Vector3(0.045f, 0.150f, 0f) * _uiScale;
            RemoveCollider(flag);
            ApplyMarkerMaterial(flag);

            return root;
        }

        private Vector3 ComputeTankLocalPosition(int index, int total)
        {
            if (total <= 0)
            {
                return Vector3.zero;
            }

            var footprint = Mathf.Clamp((_tankMarkerScale * 16f) * _uiScale, 0.16f * _uiScale, 0.52f * _uiScale);
            var spacing = footprint * 1.35f;
            var cols = Mathf.CeilToInt(Mathf.Sqrt(total));
            var rows = Mathf.CeilToInt(total / (float)cols);
            var col = index % cols;
            var row = index / cols;
            var offsetX = (col - ((cols - 1) * 0.5f)) * spacing;
            var offsetZ = (row - ((rows - 1) * 0.5f)) * spacing;
            var y = (row + col) * 0.004f * _uiScale;
            return new Vector3(offsetX, y, offsetZ);
        }

        private Vector3 ComputeFlagLocalPosition(int index, int total)
        {
            if (total <= 0)
            {
                return new Vector3(0f, 0f, -0.10f);
            }

            var spacing = 0.08f * _uiScale;
            var start = -((total - 1) * spacing * 0.5f);
            return new Vector3(start + (index * spacing), 0f, -0.10f * _uiScale);
        }

        private static void RemoveCollider(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private static void RemoveCollidersRecursive(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null)
                {
                    Destroy(collider);
                }
            }
        }

        private static void ApplyMarkerMaterial(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (s_unitMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                s_unitMaterial = new Material(shader);
            }

            renderer.material = s_unitMaterial;
        }

        private static void SetMarkerColor(GameObject marker, Color color)
        {
            if (marker == null)
            {
                return;
            }

            var renderers = marker.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r != null && r.material != null)
                {
                    var material = r.material;
                    material.color = color;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }

                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", color);
                    }

                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", color * 0.45f);
                    }
                }
            }
        }

        private static Color BoostMarkerColor(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            s = Mathf.Clamp01(s * 1.22f + 0.06f);
            v = Mathf.Clamp01(v * 1.06f + 0.02f);
            var boosted = Color.HSVToRGB(h, s, v);
            boosted.a = 1f;
            return boosted;
        }

        private LineRenderer CreateRing(string name, float radius, float width, Color color, int capVertices, int cornerVertices)
        {
            var ring = new GameObject(name);
            ring.transform.SetParent(_selectionFxRoot, false);
            var lr = ring.AddComponent<LineRenderer>();
            if (s_selectionMaterial == null)
            {
                s_selectionMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            lr.material = s_selectionMaterial;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = SelectionRingSegments;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = capVertices;
            lr.numCornerVertices = cornerVertices;

            for (var i = 0; i < SelectionRingSegments; i++)
            {
                var t = i / (float)SelectionRingSegments;
                var a = t * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }

            return lr;
        }

        private void Update()
        {
            if (!IsSelected || _selectionFxRoot == null)
            {
                return;
            }

            _selectionPulse += Time.deltaTime * 2.6f;
            var pulse = 0.5f + (0.5f * Mathf.Sin(_selectionPulse));
            var glowAlpha = Mathf.Lerp(0.18f, 0.42f, pulse);
            var coreAlpha = Mathf.Lerp(0.75f, 0.98f, pulse);

            if (_selectionGlow != null)
            {
                _selectionGlow.startWidth = Mathf.Lerp(0.045f, 0.065f, pulse);
                _selectionGlow.endWidth = _selectionGlow.startWidth;
                _selectionGlow.startColor = new Color(SelectionGlowColor.r, SelectionGlowColor.g, SelectionGlowColor.b, glowAlpha);
                _selectionGlow.endColor = _selectionGlow.startColor;
            }

            if (_selectionCore != null)
            {
                _selectionCore.startWidth = Mathf.Lerp(0.026f, 0.034f, pulse);
                _selectionCore.endWidth = _selectionCore.startWidth;
                _selectionCore.startColor = new Color(SelectionCoreColor.r, SelectionCoreColor.g, SelectionCoreColor.b, coreAlpha);
                _selectionCore.endColor = _selectionCore.startColor;
            }

            _selectionFxRoot.Rotate(0f, 55f * Time.deltaTime, 0f, Space.Self);

            if (_nameLabel != null)
            {
                _nameLabel.color = Color.Lerp(new Color(0.98f, 0.98f, 0.94f), new Color(1f, 0.96f, 0.72f), pulse);
                _nameLabel.fontStyle = FontStyle.Bold;
            }

            if (_armyLabel != null)
            {
                _armyLabel.color = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.62f), pulse);
                _armyLabel.fontStyle = FontStyle.Bold;
            }
        }

        private void OnDrawGizmos()
        {
            var c = IsSelected ? Color.white : BaseColor;
            c.a = 0.95f;
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, GizmoRadius);
            Gizmos.color = new Color(0f, 0f, 0f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, GizmoRadius);
        }
    }
}
