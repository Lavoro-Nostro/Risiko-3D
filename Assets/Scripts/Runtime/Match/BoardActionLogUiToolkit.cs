using System.Collections.Generic;
using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Board;
using UnityEngine;

namespace Risiko3D.Runtime.Match
{
    public sealed class BoardActionLogUiToolkit : MonoBehaviour
    {
        private struct LogRow
        {
            public Transform Root;
            public Renderer IconRenderer;
            public TextMesh Label;
        }

        private GameRuntimeConfig _config;
        private HostAuthoritativeMatchLoop _loop;
        private BoardBootstrap _board;
        private Transform _anchorTransform;
        private Transform _root;
        private Transform _plate;
        private TextMesh _title;
        private BoardPlayerSeatAnchor _localSeatAnchor;
        private readonly List<LogRow> _rows = new();
        private readonly Dictionary<string, Texture2D> _tankIconByColorId = new();

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
        }

        private void Start()
        {
            if (_config != null && !_config.EnableBoardGameLogPanel)
            {
                enabled = false;
                return;
            }

            _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            _board = FindFirstObjectByType<BoardBootstrap>();
            BuildPanel();
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

        private void BuildPanel()
        {
            if (_root != null)
            {
                return;
            }

            var rootGo = new GameObject("BoardGameLogPanel");
            _root = rootGo.transform;
            _root.SetParent(transform, false);
            _root.localScale = Vector3.one * Mathf.Max(0.10f, _config != null ? _config.GameLogScale : 1f);

            var plateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateGo.name = "LogPlate";
            plateGo.transform.SetParent(_root, false);
            _plate = plateGo.transform;
            _plate.localScale = new Vector3(4.2f, 0.016f, 2.4f);
            var col = plateGo.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var rend = plateGo.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                var mat = new Material(shader);
                mat.color = new Color(0.03f, 0.08f, 0.14f, 0.94f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.18f, 0.62f, 0.88f, 1f) * 0.18f);
                }

                rend.material = mat;
            }

            _title = CreateText("Title", new Vector3(-1.90f, 0.012f, 0.88f), 66, 0.030f, FontStyle.Bold, new Color(0.86f, 0.96f, 1f, 1f));
            _title.text = "GAME LOG";
            BuildRows();
            UpdatePanelPose();
        }

        private TextMesh CreateText(string name, Vector3 localPos, int fontSize, float charSize, FontStyle style, Color color, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
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

        private void Refresh()
        {
            if (_root == null || _loop == null)
            {
                return;
            }

            UpdatePanelPose();
            var rowCount = _rows.Count;
            var lines = _loop.GetRecentActionFeed(rowCount);
            if (lines == null || lines.Count == 0)
            {
                for (var i = 0; i < _rows.Count; i++)
                {
                    SetRow(_rows[i], "neutral", i == 0 ? "Waiting for actions..." : string.Empty);
                }
                return;
            }

            var rowIndex = 0;
            for (var i = lines.Count - 1; i >= 0 && rowIndex < _rows.Count; i--, rowIndex++)
            {
                var line = lines[i];
                ParseFeedLine(line, out var colorId, out var message);
                SetRow(_rows[rowIndex], colorId, HumanizeLogMessage(message));
            }

            for (; rowIndex < _rows.Count; rowIndex++)
            {
                SetRow(_rows[rowIndex], "neutral", string.Empty);
            }
        }

        private void UpdatePanelPose()
        {
            if (_root == null)
            {
                return;
            }

            var boardRenderer = FindBoardBackRenderer();
            if (boardRenderer == null)
            {
                _root.position = new Vector3(0f, 0.02f, 0f);
                _root.rotation = Quaternion.identity;
                return;
            }

            var tr = boardRenderer.transform;
            if (_config != null && _config.EnableTurntableVisualRotation && _board != null && _root.parent != _board.transform)
            {
                _root.SetParent(_board.transform, true);
            }
            else if ((_config == null || !_config.EnableTurntableVisualRotation) && _root.parent != transform)
            {
                _root.SetParent(transform, true);
            }

            var localBounds = boardRenderer.localBounds;
            var lossy = tr.lossyScale;
            var boardHalfWidth = Mathf.Abs(localBounds.extents.x * lossy.x);
            var boardHalfDepth = Mathf.Abs(localBounds.extents.z * lossy.z);
            var thickness = _config != null ? Mathf.Max(0.001f, _config.GameLogThickness) : 0.016f;
            var sizeNorm = _config != null ? _config.GameLogSizeNormalized : new Vector2(0.30f, 0.24f);
            var sizeMin = _config != null ? _config.GameLogSizeMin : new Vector2(4.0f, 2.0f);
            var sizeMax = _config != null ? _config.GameLogSizeMax : new Vector2(10.0f, 6.0f);
            var width = Mathf.Clamp((boardHalfWidth * 2f) * sizeNorm.x, sizeMin.x, sizeMax.x);
            var depth = Mathf.Clamp((boardHalfDepth * 2f) * sizeNorm.y, sizeMin.y, sizeMax.y);
            if (_plate != null)
            {
                _plate.localScale = new Vector3(width, thickness, depth);
            }

            _root.localScale = Vector3.one * Mathf.Max(0.10f, _config != null ? _config.GameLogScale : 1f);
            ApplyPanelLayout(width, depth);

            if (_config != null &&
                _config.UseGameLogAnchorObject &&
                !_config.EnableTurntableVisualRotation)
            {
                if (_anchorTransform == null)
                {
                    var anchorGo = GameObject.Find(_config.GameLogAnchorObjectName);
                    if (anchorGo != null)
                    {
                        _anchorTransform = anchorGo.transform;
                    }
                }

                if (_anchorTransform != null)
                {
                    _root.position = _anchorTransform.position + _config.GameLogAnchorOffset;
                    _root.rotation = _anchorTransform.rotation;
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
                    var yOffset = _config != null ? _config.GameLogAnchorOffset.y : 0.012f;
                    _root.position = cardsAnchor.position
                        + (side * 0.92f)
                        + (forward * 0.34f)
                        + (Vector3.up * yOffset);
                    _root.rotation = cardsAnchor.rotation;
                    return;
                }
            }

            var anchorNorm = _config != null ? _config.GameLogBoardAnchorNormalized : new Vector2(0.16f, 0.16f);
            var anchorOffset = _config != null ? _config.GameLogAnchorOffset : new Vector3(0f, 0.012f, 0f);
            if (TryResolvePlayableAreaAnchor(boardRenderer, anchorNorm, anchorOffset, out var anchoredPos, out var anchoredRot))
            {
                _root.position = anchoredPos;
                _root.rotation = anchoredRot;
                return;
            }

            var xOffset = Mathf.Lerp(-boardHalfWidth, boardHalfWidth, anchorNorm.x);
            var zOffset = Mathf.Lerp(-boardHalfDepth, boardHalfDepth, anchorNorm.y);
            _root.position = tr.position + (tr.right * (xOffset + anchorOffset.x)) + (tr.forward * (zOffset + anchorOffset.z)) + (tr.up * anchorOffset.y);
            _root.rotation = tr.rotation;
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

        private void BuildRows()
        {
            _rows.Clear();
            LoadTankIcons();
            var visible = _config != null ? Mathf.Clamp(_config.GameLogVisibleLines, 4, 12) : 8;
            for (var i = 0; i < visible; i++)
            {
                var rowGo = new GameObject($"Row_{i + 1}");
                var rowRoot = rowGo.transform;
                rowRoot.SetParent(_root, false);
                rowRoot.localPosition = Vector3.zero;
                rowRoot.localRotation = Quaternion.identity;

                var iconQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                iconQuad.name = "TankIcon";
                iconQuad.transform.SetParent(rowRoot, false);
                iconQuad.transform.localPosition = Vector3.zero;
                iconQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                iconQuad.transform.localScale = new Vector3(0.10f, 0.08f, 1f);
                var iconCol = iconQuad.GetComponent<Collider>();
                if (iconCol != null)
                {
                    Destroy(iconCol);
                }

                var label = CreateText($"RowLabel_{i + 1}", new Vector3(0.16f, 0.002f, 0f), 58, 0.017f, FontStyle.Bold, new Color(0.90f, 0.95f, 1f, 1f), rowRoot);
                label.anchor = TextAnchor.MiddleLeft;
                label.alignment = TextAlignment.Left;
                label.text = string.Empty;

                _rows.Add(new LogRow
                {
                    Root = rowRoot,
                    IconRenderer = iconQuad.GetComponent<Renderer>(),
                    Label = label
                });
            }

            var panelSize = _plate != null ? _plate.localScale : new Vector3(4.2f, 0.016f, 2.4f);
            ApplyPanelLayout(panelSize.x, panelSize.z);
        }

        private void ApplyPanelLayout(float width, float depth)
        {
            if (_title != null)
            {
                _title.transform.localPosition = new Vector3(-width * 0.46f, 0.012f, depth * 0.41f);
                _title.characterSize = Mathf.Clamp(Mathf.Min(width, depth) * 0.0080f, 0.017f, 0.036f);
            }

            if (_rows.Count == 0)
            {
                return;
            }

            var left = -width * 0.46f;
            var top = depth * 0.26f;
            var bottom = -depth * 0.36f;
            var spacing = _rows.Count > 1 ? (top - bottom) / (_rows.Count - 1) : 0f;
            var iconW = Mathf.Clamp(width * 0.024f, 0.08f, 0.17f);
            var iconH = Mathf.Clamp(depth * 0.040f, 0.07f, 0.13f);
            var labelSize = Mathf.Clamp(Mathf.Min(width, depth) * 0.0048f, 0.013f, 0.024f);

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Root != null)
                {
                    row.Root.localPosition = new Vector3(left, 0.012f, top - (spacing * i));
                }

                if (row.IconRenderer != null)
                {
                    var icon = row.IconRenderer.transform;
                    icon.localPosition = Vector3.zero;
                    icon.localScale = new Vector3(iconW, iconH, 1f);
                }

                if (row.Label != null)
                {
                    row.Label.transform.localPosition = new Vector3(iconW + 0.08f, 0.002f, 0f);
                    row.Label.characterSize = labelSize;
                }
            }
        }

        private void LoadTankIcons()
        {
            _tankIconByColorId.Clear();
            _tankIconByColorId["red"] = Resources.Load<Texture2D>("UI/TankIcons/tank_red");
            _tankIconByColorId["blue"] = Resources.Load<Texture2D>("UI/TankIcons/tank_blue");
            _tankIconByColorId["green"] = Resources.Load<Texture2D>("UI/TankIcons/tank_green");
            _tankIconByColorId["yellow"] = Resources.Load<Texture2D>("UI/TankIcons/tank_yellow");
            _tankIconByColorId["purple"] = Resources.Load<Texture2D>("UI/TankIcons/tank_purple");
            _tankIconByColorId["black"] = Resources.Load<Texture2D>("UI/TankIcons/tank_black");
        }

        private void SetRow(LogRow row, string colorId, string text)
        {
            if (row.Label != null)
            {
                row.Label.text = text;
            }

            if (row.IconRenderer == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                row.IconRenderer.enabled = false;
                return;
            }

            row.IconRenderer.enabled = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            var mat = new Material(shader);
            if (_tankIconByColorId.TryGetValue(colorId, out var icon) && icon != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", icon);
                }
                else if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", icon);
                }

                mat.color = Color.white;
            }
            else
            {
                mat.color = ResolveFallbackColor(colorId);
            }

            row.IconRenderer.material = mat;
        }

        private static Color ResolveFallbackColor(string colorId)
        {
            return colorId switch
            {
                "red" => new Color(0.93f, 0.34f, 0.34f),
                "blue" => new Color(0.34f, 0.56f, 0.94f),
                "green" => new Color(0.32f, 0.84f, 0.42f),
                "yellow" => new Color(0.92f, 0.84f, 0.26f),
                "purple" => new Color(0.67f, 0.42f, 0.90f),
                "black" => new Color(0.25f, 0.25f, 0.25f),
                _ => new Color(0.70f, 0.76f, 0.86f)
            };
        }

        private static void ParseFeedLine(string line, out string colorId, out string message)
        {
            colorId = "neutral";
            message = line;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var phaseEnd = line.IndexOf("] ", System.StringComparison.Ordinal);
            var colon = line.IndexOf(':');
            if (phaseEnd >= 0 && colon > phaseEnd + 2)
            {
                var playerId = line.Substring(phaseEnd + 2, colon - (phaseEnd + 2)).Trim();
                if (playerId.StartsWith("player_", System.StringComparison.OrdinalIgnoreCase))
                {
                    colorId = playerId.Substring("player_".Length).ToLowerInvariant();
                }

                message = line.Substring(colon + 1).Trim();
            }
        }

        private static string HumanizeLogMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            var m = message.ToLowerInvariant();
            if (m.Contains("setup deploy applied"))
            {
                return "placed 1 setup army";
            }

            if (m.StartsWith("claimed "))
            {
                return "claimed a territory";
            }

            if (m.StartsWith("selected "))
            {
                return string.Empty;
            }

            if (m.StartsWith("attack:"))
            {
                return "attacked: battle resolved";
            }

            if (m.StartsWith("capture:"))
            {
                return "won attack and captured territory";
            }

            if (m.Contains("fortify applied"))
            {
                return "fortified armies and ended turn";
            }

            if (m.Contains("invalid"))
            {
                return "invalid action";
            }

            if (m.Contains("phase -> fortify"))
            {
                return "moved to fortify phase";
            }

            if (m.Contains("reinforce complete"))
            {
                return "reinforcement complete";
            }

            if (m.StartsWith("turn summary ->"))
            {
                return message.Substring("turn summary ->".Length).Trim();
            }

            if (m.Contains("ended turn with no major actions"))
            {
                return "ended turn with no major actions";
            }

            return message;
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
