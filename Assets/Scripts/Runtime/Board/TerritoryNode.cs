using UnityEngine;

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

        private TextMesh _nameLabel;
        private TextMesh _armyLabel;
        private Transform _selectionFxRoot;
        private LineRenderer _selectionCore;
        private LineRenderer _selectionGlow;
        private float _selectionPulse;

        public void Initialize(string territoryId, string displayName, string continentId, Color baseColor)
        {
            TerritoryId = territoryId;
            DisplayName = displayName;
            ContinentId = continentId;
            BaseColor = baseColor;
            Renderer = GetComponent<Renderer>();
            CacheLabels();
            EnsureSelectionFx();
            SetDisplayName(displayName);
            ApplyColor(baseColor);
            SetSelected(false);
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
