using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
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
            _hologramLine4 = CreateHologramText("Line4", new Vector3(-2.85f, 0.020f, -0.78f), 54, 0.043f, FontStyle.Normal, new Color(0.70f, 0.90f, 1f, 1f));
            _hologramTitle.text = "MATCH CONTROLS";
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
            _controlChipRoot.localPosition = new Vector3(-2.56f, 0.022f, -1.08f);
            _controlChipRoot.localRotation = Quaternion.identity;

            CreateControlGlyph("LMB", "Select", new Vector3(0.00f, 0f, 0f), 0.48f);
            _chipQ = CreateControlGlyph("Q", "Down", new Vector3(0.92f, 0f, 0f), 0.36f);
            _chipE = CreateControlGlyph("E", "Up", new Vector3(1.46f, 0f, 0f), 0.36f);
            CreateControlGlyph("ENTER", "Confirm", new Vector3(2.18f, 0f, 0f), 0.68f);
            CreateControlGlyph("N", "Next", new Vector3(3.28f, 0f, 0f), 0.36f);
            CreateControlGlyph("TAB", "Names", new Vector3(3.90f, 0f, 0f), 0.52f);
        }

        private GameObject CreateControlGlyph(string keyText, string actionText, Vector3 localPos, float keyWidth)
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
            keyTextGo.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            keyTextGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var keyTm = keyTextGo.AddComponent<TextMesh>();
            keyTm.text = keyText;
            keyTm.anchor = TextAnchor.MiddleCenter;
            keyTm.alignment = TextAlignment.Center;
            keyTm.fontSize = 84;
            keyTm.characterSize = keyText.Length > 1 ? 0.020f : 0.024f;
            keyTm.fontStyle = FontStyle.Bold;
            keyTm.color = new Color(0.92f, 0.96f, 1f, 1f);

            var actionTextGo = new GameObject("ActionText");
            actionTextGo.transform.SetParent(root.transform, false);
            actionTextGo.transform.localPosition = new Vector3(0f, 0.008f, -0.20f);
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

            var b = renderer.bounds;
            var thickness = _config != null ? Mathf.Max(0.001f, _config.HologramThickness) : 0.020f;
            var sizeNorm = _config != null ? _config.HologramSizeNormalized : new Vector2(0.42f, 0.32f);
            var sizeMin = _config != null ? _config.HologramSizeMin : new Vector2(6.0f, 2.8f);
            var sizeMax = _config != null ? _config.HologramSizeMax : new Vector2(14.0f, 9.0f);
            var width = Mathf.Clamp(b.size.x * sizeNorm.x, sizeMin.x, sizeMax.x);
            var depth = Mathf.Clamp(b.size.z * sizeNorm.y, sizeMin.y, sizeMax.y);
            if (_hologramPlate != null)
            {
                _hologramPlate.localScale = new Vector3(width, thickness, depth);
            }

            _hologramRoot.localScale = Vector3.one * Mathf.Max(0.10f, _config != null ? _config.HologramScale : 1f);

            if (_config != null && _config.UseHologramAnchorObject)
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

            var anchorNorm = _config != null ? _config.HologramBoardAnchorNormalized : new Vector2(0.56f, 0.20f);
            _hologramRoot.position = new Vector3(
                Mathf.Lerp(b.min.x, b.max.x, anchorNorm.x),
                b.max.y + (_config != null ? _config.HologramAnchorOffset.y : 0.016f),
                Mathf.Lerp(b.min.z, b.max.z, anchorNorm.y));
            _hologramRoot.rotation = Quaternion.identity;
        }

        private void RefreshHologram()
        {
            if (_hologramRoot == null || _loop == null || _board == null)
            {
                return;
            }

            UpdateWorldHologramPose();
            var phaseName = ToFriendlyPhaseName(_loop.PhaseName);
            var playerName = ToFriendlyPlayerName(_loop.ActivePlayerId);
            _hologramLine1.text = $"Turn: {playerName}   |   Phase: {phaseName}";
            _hologramLine2.text = $"Status: {ToFriendlyStatus(_loop.StatusMessage)}";
            _hologramLine3.text = BuildHologramActionLine(_loop);
            _hologramLine4.text = BuildHologramControlsLine(_loop);
            var usesAdjust = _loop.PhaseName == "Attack" || _loop.PhaseName == "Fortify";
            if (_chipQ != null) { _chipQ.SetActive(usesAdjust); }
            if (_chipE != null) { _chipE.SetActive(usesAdjust); }
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
                return "Use key glyphs below. Q/E changes value in this phase.";
            }

            return "Use key glyphs below. Q/E appears only when value adjustment is available.";
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
