using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Risiko3D.Runtime.Match
{
    public sealed class BoardGuidanceUiToolkit : MonoBehaviour
    {
        private GameRuntimeConfig _config;
        private HostAuthoritativeMatchLoop _loop;
        private BoardBootstrap _board;
        private Transform _anchorTransform;
        private Transform _hologramRoot;
        private Transform _hologramPlate;
        private TextMesh _hologramTitle;
        private TextMesh _hologramLine1;
        private TextMesh _hologramLine2;
        private TextMesh _hologramLine3;
        private TextMesh _hologramLine4;
        private Transform _controlChipRoot;
        private GameObject _chipQ;
        private GameObject _chipE;
        private Texture2D _mouseIcon;
        private readonly Dictionary<Transform, float> _glyphWidths = new();
        private BoardPlayerSeatAnchor _localSeatAnchor;

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
        }

        private void Start()
        {
            _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            _board = FindFirstObjectByType<BoardBootstrap>();
            EnsureWorldHologram();
            Refresh();
        }

        private void Update()
        {
            if (_loop == null)
            {
                _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            }

            if (_board == null)
            {
                _board = FindFirstObjectByType<BoardBootstrap>();
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_loop == null)
            {
                return;
            }

            RefreshHologram();
        }

        private void EnsureWorldHologram()
        {
            if (_hologramRoot != null)
            {
                return;
            }

            var rootGo = new GameObject("BoardCommandHologram");
            _hologramRoot = rootGo.transform;
            _hologramRoot.SetParent(transform, false);
            _hologramRoot.localScale = Vector3.one * Mathf.Max(0.10f, _config != null ? _config.HologramScale : 1f);

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "HoloPlate";
            plate.transform.SetParent(_hologramRoot, false);
            _hologramPlate = plate.transform;
            _hologramPlate.localScale = new Vector3(6.0f, 0.020f, 2.8f);
            var col = plate.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var plateRenderer = plate.GetComponent<Renderer>();
            if (plateRenderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                var mat = new Material(shader);
                mat.color = new Color(0.05f, 0.16f, 0.22f, 0.92f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.09f, 0.50f, 0.70f, 1f) * 0.28f);
                }

                plateRenderer.material = mat;
            }

            _hologramTitle = CreateHologramText("Title", new Vector3(-2.85f, 0.020f, 1.04f), 72, 0.062f, FontStyle.Bold, new Color(0.78f, 0.95f, 1f, 1f));
            _hologramLine1 = CreateHologramText("Line1", new Vector3(-2.85f, 0.020f, 0.56f), 64, 0.054f, FontStyle.Bold, new Color(0.88f, 0.98f, 1f, 1f));
            _hologramLine2 = CreateHologramText("Line2", new Vector3(-2.85f, 0.020f, 0.14f), 60, 0.051f, FontStyle.Bold, new Color(0.68f, 0.94f, 1f, 1f));
            _hologramLine3 = CreateHologramText("Line3", new Vector3(-2.85f, 0.020f, -0.30f), 58, 0.048f, FontStyle.Bold, new Color(1f, 0.92f, 0.70f, 1f));
            _hologramLine4 = CreateHologramText("Line4", new Vector3(-2.85f, 0.020f, -0.74f), 50, 0.032f, FontStyle.Normal, new Color(0.70f, 0.90f, 1f, 1f));
            _hologramTitle.text = "MATCH CONTROLS";
            _mouseIcon = Resources.Load<Texture2D>("UI/InputIcons/mouse");
            BuildControlChips();
            UpdateWorldHologramPose();
        }

        private TextMesh CreateHologramText(string name, Vector3 localPos, int fontSize, float charSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_hologramRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.UpperLeft;
            tm.alignment = TextAlignment.Left;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.fontStyle = style;
            tm.color = color;
            return tm;
        }

        private void BuildControlChips()
        {
            var row = new GameObject("ControlChips");
            _controlChipRoot = row.transform;
            _controlChipRoot.SetParent(_hologramRoot, false);
            _controlChipRoot.localPosition = new Vector3(-2.56f, 0.022f, -1.34f);
            _controlChipRoot.localRotation = Quaternion.identity;

            _glyphWidths.Clear();
            var lmb = CreateControlGlyph("LMB", "Select", Vector3.zero, 0.54f, _mouseIcon, _mouseIcon == null);
            _glyphWidths[lmb.transform] = 0.76f;
            _chipQ = CreateControlGlyph("Q", "Down", Vector3.zero, 0.36f, null, true);
            _glyphWidths[_chipQ.transform] = 0.52f;
            _chipE = CreateControlGlyph("E", "Up", Vector3.zero, 0.36f, null, true);
            _glyphWidths[_chipE.transform] = 0.52f;
            var enter = CreateControlGlyph("ENTER", "Confirm", Vector3.zero, 0.74f, null, true);
            _glyphWidths[enter.transform] = 0.96f;
            var next = CreateControlGlyph("N", "Next", Vector3.zero, 0.36f, null, true);
            _glyphWidths[next.transform] = 0.52f;
            var tab = CreateControlGlyph("TAB", "Names", Vector3.zero, 0.56f, null, true);
            _glyphWidths[tab.transform] = 0.78f;
            LayoutActiveControlChips();
        }

        private GameObject CreateControlGlyph(string keyText, string actionText, Vector3 localPos, float keyWidth, Texture2D iconTexture, bool showKeyText)
        {
            var root = new GameObject($"Glyph_{keyText}");
            root.transform.SetParent(_controlChipRoot, false);
            root.transform.localPosition = localPos;

            var keyBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyBack.name = "KeyBack";
            keyBack.transform.SetParent(root.transform, false);
            keyBack.transform.localScale = new Vector3(keyWidth, 0.009f, 0.26f);
            var keyBackCol = keyBack.GetComponent<Collider>();
            if (keyBackCol != null) Destroy(keyBackCol);
            AssignGlyphMaterial(keyBack, new Color(0.08f, 0.10f, 0.14f, 1f), new Color(0.10f, 0.16f, 0.28f, 1f) * 0.18f);

            var keyFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyFrame.name = "KeyFrame";
            keyFrame.transform.SetParent(root.transform, false);
            keyFrame.transform.localPosition = new Vector3(0f, 0.0025f, 0f);
            keyFrame.transform.localScale = new Vector3(keyWidth + 0.020f, 0.003f, 0.280f);
            var keyFrameCol = keyFrame.GetComponent<Collider>();
            if (keyFrameCol != null) Destroy(keyFrameCol);
            AssignGlyphMaterial(keyFrame, new Color(0.66f, 0.80f, 0.98f, 1f), new Color(0.66f, 0.80f, 0.98f, 1f) * 0.15f);

            var keyTextGo = new GameObject("KeyText");
            keyTextGo.transform.SetParent(root.transform, false);
            keyTextGo.transform.localPosition = new Vector3(0f, 0.008f, -0.01f);
            keyTextGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var keyTm = keyTextGo.AddComponent<TextMesh>();
            if (showKeyText)
            {
                keyTm.text = keyText;
                keyTm.anchor = TextAnchor.MiddleCenter;
                keyTm.alignment = TextAlignment.Center;
                keyTm.fontSize = 84;
                keyTm.characterSize = keyText.Length > 1 ? 0.020f : 0.024f;
                keyTm.fontStyle = FontStyle.Bold;
                keyTm.color = new Color(0.92f, 0.96f, 1f, 1f);
            }
            else
            {
                keyTm.text = string.Empty;
            }

            if (iconTexture != null)
            {
                var iconQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                iconQuad.name = "Icon";
                iconQuad.transform.SetParent(root.transform, false);
                iconQuad.transform.localPosition = new Vector3(0f, 0.0084f, 0.0f);
                iconQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                iconQuad.transform.localScale = new Vector3(keyWidth * 0.90f, 0.18f, 1f);
                var iconCollider = iconQuad.GetComponent<Collider>();
                if (iconCollider != null)
                {
                    Destroy(iconCollider);
                }

                var iconRenderer = iconQuad.GetComponent<Renderer>();
                if (iconRenderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null)
                    {
                        shader = Shader.Find("Unlit/Texture");
                    }

                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", iconTexture);
                    }
                    else if (mat.HasProperty("_MainTex"))
                    {
                        mat.SetTexture("_MainTex", iconTexture);
                    }

                    mat.color = new Color(0.90f, 0.96f, 1f, 1f);
                    iconRenderer.material = mat;
                }
            }

            var actionTextGo = new GameObject("ActionText");
            actionTextGo.transform.SetParent(root.transform, false);
            actionTextGo.transform.localPosition = new Vector3(0f, 0.008f, -0.24f);
            actionTextGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var actionTm = actionTextGo.AddComponent<TextMesh>();
            actionTm.text = actionText;
            actionTm.anchor = TextAnchor.MiddleCenter;
            actionTm.alignment = TextAlignment.Center;
            actionTm.fontSize = 74;
            actionTm.characterSize = 0.019f;
            actionTm.fontStyle = FontStyle.Bold;
            actionTm.color = new Color(0.88f, 0.93f, 1f, 1f);

            return root;
        }

        private static void AssignGlyphMaterial(GameObject go, Color baseColor, Color emission)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader);
            mat.color = baseColor;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }

            rend.material = mat;
        }

        private void UpdateWorldHologramPose()
        {
            if (_hologramRoot == null)
            {
                return;
            }

            var renderer = FindBoardBackRenderer();
            if (renderer == null)
            {
                _hologramRoot.position = new Vector3(0f, 0.03f, 0f);
                _hologramRoot.rotation = Quaternion.identity;
                return;
            }

            var tr = renderer.transform;
            if (_config != null && _config.EnableTurntableVisualRotation && _board != null && _hologramRoot.parent != _board.transform)
            {
                _hologramRoot.SetParent(_board.transform, true);
            }
            else if ((_config == null || !_config.EnableTurntableVisualRotation) && _hologramRoot.parent != transform)
            {
                _hologramRoot.SetParent(transform, true);
            }

            var localBounds = renderer.localBounds;
            var lossy = tr.lossyScale;
            var boardHalfWidth = Mathf.Abs(localBounds.extents.x * lossy.x);
            var boardHalfDepth = Mathf.Abs(localBounds.extents.z * lossy.z);
            var thickness = _config != null ? Mathf.Max(0.001f, _config.HologramThickness) : 0.020f;
            var sizeNorm = _config != null ? _config.HologramSizeNormalized : new Vector2(0.42f, 0.32f);
            var sizeMin = _config != null ? _config.HologramSizeMin : new Vector2(6.0f, 2.8f);
            var sizeMax = _config != null ? _config.HologramSizeMax : new Vector2(14.0f, 9.0f);
            var width = Mathf.Clamp((boardHalfWidth * 2f) * sizeNorm.x, sizeMin.x, sizeMax.x);
            var depth = Mathf.Clamp((boardHalfDepth * 2f) * sizeNorm.y, sizeMin.y, sizeMax.y);
            if (_hologramPlate != null)
            {
                _hologramPlate.localScale = new Vector3(width, thickness, depth);
            }

            _hologramRoot.localScale = Vector3.one * Mathf.Max(0.10f, _config != null ? _config.HologramScale : 1f);

            if (_config != null &&
                _config.UseHologramAnchorObject &&
                !_config.EnableTurntableVisualRotation)
            {
                if (_anchorTransform == null)
                {
                    var anchorGo = GameObject.Find(_config.HologramAnchorObjectName);
                    if (anchorGo != null)
                    {
                        _anchorTransform = anchorGo.transform;
                    }
                }

                if (_anchorTransform != null)
                {
                    _hologramRoot.position = _anchorTransform.position + _config.HologramAnchorOffset;
                    _hologramRoot.rotation = _anchorTransform.rotation;
                    return;
                }
            }

            if (_config != null &&
                _config.UseSeatAnchorsForCameraSpawn &&
                !_config.EnableTurntableVisualRotation &&
                TryResolveLocalSeatAnchor(out var seatAnchor))
            {
                var cardsAnchor = seatAnchor.ResolveCardsAnchor();
                if (cardsAnchor != null)
                {
                    var side = cardsAnchor.right;
                    var forward = cardsAnchor.forward;
                    var yOffset = _config != null ? _config.HologramAnchorOffset.y : 0.016f;
                    _hologramRoot.position = cardsAnchor.position
                        + (side * -0.90f)
                        + (forward * 0.36f)
                        + (Vector3.up * yOffset);
                    _hologramRoot.rotation = cardsAnchor.rotation;
                    return;
                }
            }

            var anchorNorm = _config != null ? _config.HologramBoardAnchorNormalized : new Vector2(0.56f, 0.20f);
            var anchorOffset = _config != null ? _config.HologramAnchorOffset : new Vector3(0f, 0.016f, 0f);
            if (TryResolvePlayableAreaAnchor(renderer, anchorNorm, anchorOffset, out var anchoredPos, out var anchoredRot))
            {
                _hologramRoot.position = anchoredPos;
                _hologramRoot.rotation = anchoredRot;
                return;
            }

            var xOffset = Mathf.Lerp(-boardHalfWidth, boardHalfWidth, anchorNorm.x);
            var zOffset = Mathf.Lerp(-boardHalfDepth, boardHalfDepth, anchorNorm.y);
            _hologramRoot.position = tr.position + (tr.right * (xOffset + anchorOffset.x)) + (tr.forward * (zOffset + anchorOffset.z)) + (tr.up * anchorOffset.y);
            _hologramRoot.rotation = tr.rotation;
        }

        private bool TryResolvePlayableAreaAnchor(Renderer boardRenderer, Vector2 anchorNorm, Vector3 anchorOffset, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            if (_board == null || _board.Nodes == null || _board.Nodes.Count == 0 || boardRenderer == null)
            {
                return false;
            }

            var tr = _board.transform;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            var count = 0;
            foreach (var kv in _board.Nodes)
            {
                var node = kv.Value;
                if (node == null)
                {
                    continue;
                }

                var local = tr.InverseTransformPoint(node.transform.position);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minZ = Mathf.Min(minZ, local.z);
                maxZ = Mathf.Max(maxZ, local.z);
                count++;
            }

            if (count <= 0 || maxX <= minX || maxZ <= minZ)
            {
                return false;
            }

            var localX = Mathf.Lerp(minX, maxX, anchorNorm.x);
            var localZ = Mathf.Lerp(minZ, maxZ, anchorNorm.y);
            localX = Mathf.Clamp(localX + anchorOffset.x, minX, maxX);
            localZ = Mathf.Clamp(localZ + anchorOffset.z, minZ, maxZ);
            worldPosition = tr.TransformPoint(new Vector3(localX, 0f, localZ));
            var surfaceY = ResolveMapSurfaceYOrFallback(boardRenderer);
            var lift = Mathf.Clamp(anchorOffset.y, -0.080f, 0.080f);
            worldPosition.y = surfaceY + lift;
            worldRotation = tr.rotation;
            return true;
        }

        private float ResolveMapSurfaceYOrFallback(Renderer boardRenderer)
        {
            if (_board != null)
            {
                var mapRenderer = _board.GetComponentInChildren<SpriteRenderer>(true);
                if (mapRenderer != null)
                {
                    return mapRenderer.bounds.max.y;
                }
            }

            return boardRenderer != null ? boardRenderer.bounds.max.y : 0f;
        }

        private bool TryResolveLocalSeatAnchor(out BoardPlayerSeatAnchor seatAnchor)
        {
            seatAnchor = null;
            if (_loop == null)
            {
                return false;
            }

            if (_localSeatAnchor != null && _localSeatAnchor.SeatNumber == (_loop.LocalPlayerIndex + 1))
            {
                seatAnchor = _localSeatAnchor;
                return true;
            }

            var anchors = Object.FindObjectsByType<BoardPlayerSeatAnchor>(FindObjectsSortMode.None);
            var seatNumber = _loop.LocalPlayerIndex + 1;
            for (var i = 0; i < anchors.Length; i++)
            {
                var candidate = anchors[i];
                if (candidate != null && candidate.SeatNumber == seatNumber)
                {
                    _localSeatAnchor = candidate;
                    seatAnchor = candidate;
                    return true;
                }
            }

            return false;
        }

        private void RefreshHologram()
        {
            if (_hologramRoot == null || _loop == null || _board == null)
            {
                return;
            }

            UpdateWorldHologramPose();
            var phaseName = ToFriendlyPhaseName(_loop.PhaseName);
            var playerName = ToFriendlyPlayerName(_loop.ActivePlayerDisplayName);
            _hologramLine1.text = $"Turn: {playerName}   |   Phase: {phaseName}";
            _hologramLine2.text = $"Status: {ToFriendlyStatus(_loop.StatusMessage)}";
            _hologramLine3.text = BuildHologramActionLine(_loop);
            _hologramLine4.text = BuildHologramControlsLine(_loop);
            var usesAdjust = _loop.PhaseName == "Attack" || _loop.PhaseName == "Fortify";
            if (_chipQ != null) { _chipQ.SetActive(usesAdjust); }
            if (_chipE != null) { _chipE.SetActive(usesAdjust); }
            LayoutActiveControlChips();
        }

        private static string BuildHologramActionLine(HostAuthoritativeMatchLoop loop)
        {
            var phase = loop.PhaseName;
            if (phase == "Attack")
            {
                if (loop.HasPendingCaptureMove)
                {
                    return $"Move armies into captured territory: {loop.PendingCaptureCurrentArmies} ({loop.PendingCaptureMinArmies}-{loop.PendingCaptureMaxArmies})";
                }

                return $"Attack: choose dice ({loop.PendingAttackDice}/{loop.PendingAttackMaxDice}) then confirm roll";
            }

            if (phase == "Fortify")
            {
                return $"Fortify: move armies ({loop.PendingFortifyArmies}/{loop.PendingFortifyMaxArmies}) between connected territories";
            }

            if (phase == "Reinforce")
            {
                return $"Reinforcements available: {loop.ActiveReinforcementPool}";
            }

            if (phase == "SetupDeploy")
            {
                return $"Setup: {loop.ActiveSetupArmiesRemaining} armies left, {loop.ActiveSetupPlacementsRemainingThisTurn} placements left this turn";
            }

            if (phase == "SetupClaim")
            {
                return "Claim phase: pick an unowned territory and confirm";
            }

            return "Follow the highlighted valid territory actions";
        }

        private static string BuildHologramControlsLine(HostAuthoritativeMatchLoop loop)
        {
            if (loop.PhaseName == "Attack" || loop.PhaseName == "Fortify")
            {
                return "Controls: use glyphs below. Q/E changes value in this phase.";
            }

            return "Controls: use glyphs below. Q/E appears only when value adjustment is available.";
        }

        private void LayoutActiveControlChips()
        {
            LayoutActiveControlChips(_controlChipRoot);
        }

        private void LayoutActiveControlChips(Transform chipRoot)
        {
            if (chipRoot == null)
            {
                return;
            }

            const float spacing = 0.18f;
            var x = 0f;
            for (var i = 0; i < chipRoot.childCount; i++)
            {
                var child = chipRoot.GetChild(i);
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }

                child.localPosition = new Vector3(x, 0f, 0f);
                if (!_glyphWidths.TryGetValue(child, out var width))
                {
                    width = 0.64f;
                }

                x += width + spacing;
            }
        }

        private static string ToFriendlyPhaseName(string phaseName)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
            {
                return "Unknown";
            }

            if (phaseName == "SetupDeploy")
            {
                return "Setup Deploy";
            }

            if (phaseName == "SetupClaim")
            {
                return "Setup Claim";
            }

            var sb = new StringBuilder(phaseName.Length + 8);
            sb.Append(char.ToUpperInvariant(phaseName[0]));
            for (var i = 1; i < phaseName.Length; i++)
            {
                var c = phaseName[i];
                var prev = phaseName[i - 1];
                if (char.IsUpper(c) && char.IsLower(prev))
                {
                    sb.Append(' ');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string ToFriendlyPlayerName(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return "Player";
            }

            var raw = playerId.Replace("player_", string.Empty).Replace("_", " ").Trim();
            if (raw.Length == 0)
            {
                return "Player";
            }

            return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
        }

        private static string ToFriendlyStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "-";
            }

            var value = status.Replace('_', ' ').Replace('-', ' ');
            var sb = new StringBuilder(value.Length + 4);
            var upperNext = true;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsLetter(c))
                {
                    sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                }
                else
                {
                    sb.Append(c);
                    if (c == ' ')
                    {
                        upperNext = true;
                    }
                }
            }

            return sb.ToString();
        }

        private static Renderer FindBoardBackRenderer()
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject == null)
                {
                    continue;
                }

                if (r.gameObject.name == "Board_Back")
                {
                    return r;
                }
            }

            return null;
        }
    }
}
