using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Input;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardBootstrap : MonoBehaviour
    {
        public event Action<TerritoryNode> TerritorySelected;

        private readonly Dictionary<string, TerritoryNode> _nodes = new();
        private readonly Dictionary<string, HashSet<string>> _adjacency = new();
        private readonly Dictionary<string, string> _localizedNames = new();
        private readonly Dictionary<string, Vector3> _territoryPositions = new();
        private readonly Dictionary<string, List<List<Vector2>>> _territoryShapePolygons = new();
        private readonly Dictionary<string, Vector2> _territoryShapeCentroids = new();
        private readonly Dictionary<string, List<List<Vector2>>> _territoryShapePolygonsLocal = new();
        private readonly Dictionary<string, Vector2> _territoryShapeCentroidsLocal = new();
        private readonly Dictionary<string, Color> _continentColors = new()
        {
            { "north_america", new Color(0.90f, 0.42f, 0.35f) },
            { "south_america", new Color(0.93f, 0.72f, 0.36f) },
            { "europe", new Color(0.35f, 0.62f, 0.92f) },
            { "africa", new Color(0.40f, 0.80f, 0.42f) },
            { "asia", new Color(0.70f, 0.50f, 0.88f) },
            { "australia", new Color(0.89f, 0.56f, 0.74f) },
        };

        private GameRuntimeConfig _config;
        private MapData _map;
        private TerritoryNode _selected;
        private Camera _camera;
        private BoardCameraController _cameraController;
        private BoardInputActionsAdapter _input;
        private GameObject _adjacencyRoot;
        private BoardVisualLayer _visualLayer;
        private TerritorySelectionOverlay _selectionOverlay;
        private Transform _worldLegendRoot;
        private Transform _worldLegendBg;
        private Transform _worldLegendFrameRoot;
        private Renderer _worldLegendBoardBack;
        private TextMesh _worldLegendTitle;
        private readonly List<TextMesh> _worldLegendRows = new();
        private TextMesh _worldLegendSelected;
        private float _nextWorldLegendRefreshAt;
        private Renderer _tableRenderer;
        private bool _hasTerritoryShapes;
        private float _territoryPlaneY = 0.5f;
        private float _boardSurfaceY = 0.03f;
        private float _territoryUiScale = 1f;
        private bool _showTerritoryNames;
        private Func<bool> _territorySelectionGate;
        private bool _verboseLogs;
        private const float SeaConnectionDistanceThreshold = 0.28f;
        private const float TerritoryOverlayHeight = 0.05f;
        private static readonly Quaternion TextFacingFlip = Quaternion.Euler(0f, 180f, 0f);
        private bool _turntableMode;

        private static readonly Dictionary<string, Vector3> ContinentAnchors = new()
        {
            { "north_america", new Vector3(-9f, 0.5f, 5f) },
            { "south_america", new Vector3(-9f, 0.5f, -6f) },
            { "europe", new Vector3(0f, 0.5f, 5f) },
            { "africa", new Vector3(0f, 0.5f, -3f) },
            { "asia", new Vector3(9f, 0.5f, 3f) },
            { "australia", new Vector3(9f, 0.5f, -7f) },
        };

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
        }

        public TerritoryNode SelectedTerritory => _selected;
        public BoardInputActionsAdapter InputAdapter => _input;
        public IReadOnlyDictionary<string, TerritoryNode> Nodes => _nodes;

        public void SetTerritorySelectionGate(Func<bool> selectionGate)
        {
            _territorySelectionGate = selectionGate;
            if (!CanSelectTerritories() && _selected != null)
            {
                ClearSelection();
            }
        }

        private void Start()
        {
            if (_config == null)
            {
                Debug.LogError("[Risiko3D][Board] Missing runtime config.");
                return;
            }

            _verboseLogs = _config.EnableVerboseRuntimeLogs;
            var mapJson = ReadProjectFile(_config.MapJsonPath);
            if (string.IsNullOrWhiteSpace(mapJson))
            {
                Debug.LogError($"[Risiko3D][Board] Failed reading map: {_config.MapJsonPath}");
                return;
            }

            _map = JsonUtility.FromJson<MapData>(mapJson);
            if (_map == null || _map.territories == null || _map.territories.Length == 0)
            {
                Debug.LogError("[Risiko3D][Board] Invalid map json.");
                return;
            }

            ParseLocalization(ReadProjectFile(_config.MapLocalizationItPath));
            _turntableMode = _config != null && _config.EnableTurntableVisualRotation;
            Trace("Board", $"start map={_config.MapJsonPath}, turntable={_turntableMode}, visualSize={_config.BoardVisualWorldSize}");
            BuildAdjacency();
            ParsePositionManifest(ReadProjectFile(_config.TerritoryPositionsPath));
            EnsureCamera();
            EnsureInput();
            CreateTable();
            EnsureVisualLayer();
            CreateTerritories();
            BuildTerritoryShapesFromSvg();
            AlignTerritoryNodesToShapeCentroids();
            AlignAlwaysVisibleNamesToTerritoryShapes();
            EnsureSelectionOverlay();
            EnsureWorldLegendUi();
            CreateAdjacencyLines();
            ApplyVisualMode();
            ApplyTerritoryNameVisibility(false);
            Debug.Log($"[Risiko3D][Board] Spawned {_nodes.Count} territories for map {_map.id}.");
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            if (_input == null || !_input.IsReady)
            {
                return;
            }

            ApplyTerritoryNameVisibility(_input.IsShowTerritoryNamesHeld());

            if (_input.WasPrimaryPressedThisFrame() && !_input.IsPanHeld() && !_input.IsOrbitHeld())
            {
                if (CanSelectTerritories())
                {
                    var pointer = _input.GetPointerScreenPosition();
                    Trace("Select", $"primary pointer={pointer.x:0.0},{pointer.y:0.0} mode={(_hasTerritoryShapes ? "shape" : "raycast")}");
                    var handled = false;
                    if (_hasTerritoryShapes)
                    {
                        if (TryPickTerritoryFromShapes(pointer, out var shapeNode))
                        {
                            Select(shapeNode);
                            Trace("Select", $"shape hit territory={shapeNode.TerritoryId}");
                            handled = true;
                        }
                    }

                    if (!handled)
                    {
                        var ray = _camera.ScreenPointToRay(pointer);
                        var hits = Physics.RaycastAll(ray, 200f);
                        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                        for (var i = 0; i < hits.Length; i++)
                        {
                            var hit = hits[i];
                            var node = hit.collider != null ? hit.collider.GetComponentInParent<TerritoryNode>() : null;
                            if (node != null)
                            {
                                Select(node);
                                Trace("Select", $"ray hit territory={node.TerritoryId}");
                                handled = true;
                                break;
                            }
                        }

                        if (!handled && TryPickNearestNodeFromBoardSurface(pointer, out var nearestNode))
                        {
                            Select(nearestNode);
                            Trace("Select", $"fallback-nearest territory={nearestNode.TerritoryId}");
                            handled = true;
                        }
                    }
                }
                else
                {
                    Trace("Select", "blocked by turn gate");
                }
            }

            RefreshWorldLegendUi();
        }

        private bool CanSelectTerritories()
        {
            return _territorySelectionGate == null || _territorySelectionGate.Invoke();
        }

        private void ApplyTerritoryNameVisibility(bool visible)
        {
            if (_showTerritoryNames == visible)
            {
                return;
            }

            _showTerritoryNames = visible;
            foreach (var node in _nodes.Values)
            {
                if (node == null)
                {
                    continue;
                }

                node.SetTerritoryNameVisible(visible);
            }
        }

        private void LateUpdate()
        {
            UpdateLabelBillboards();
            TrackSelectionOverlayWhileTurntableRotates();
        }

        private void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var camObject = new GameObject("Main Camera");
                _camera = camObject.AddComponent<Camera>();
                camObject.tag = "MainCamera";
            }

            _cameraController = _camera.GetComponent<BoardCameraController>();
            if (_cameraController == null)
            {
                _cameraController = _camera.gameObject.AddComponent<BoardCameraController>();
            }

            if (_cameraController != null)
            {
                _cameraController.enabled = true;
            }
        }

        private void EnsureInput()
        {
            _input = GetComponent<BoardInputActionsAdapter>();
            if (_input == null)
            {
                _input = gameObject.AddComponent<BoardInputActionsAdapter>();
            }

            if (_cameraController != null)
            {
                _cameraController.SetInput(_input);
            }
        }

        private void EnsureVisualLayer()
        {
            _visualLayer = GetComponent<BoardVisualLayer>();
            if (_visualLayer == null)
            {
                _visualLayer = gameObject.AddComponent<BoardVisualLayer>();
            }

            _visualLayer.Build(_config);
            if (_tableRenderer != null)
            {
                _boardSurfaceY = _tableRenderer.bounds.max.y + 0.005f;
            }
            else
            {
                _boardSurfaceY = (_config != null ? _config.BoardVisualPosition.y : 0.02f) + 0.02f;
            }

            if (_config != null)
            {
                var sx = Mathf.Max(0.1f, _config.BoardVisualWorldSize.x / 24f);
                var sy = Mathf.Max(0.1f, _config.BoardVisualWorldSize.y / 16f);
                _territoryUiScale = Mathf.Clamp(Mathf.Max(sx, sy), 0.7f, 3.2f);
            }
        }

        private void EnsureSelectionOverlay()
        {
            _selectionOverlay = GetComponent<TerritorySelectionOverlay>();
            if (_selectionOverlay == null)
            {
                _selectionOverlay = gameObject.AddComponent<TerritorySelectionOverlay>();
            }

            // Keep overlay on the same visual plane where tank markers sit.
            _selectionOverlay.Configure(ComputeOverlayPlaneY());
            Trace("Overlay", $"configure y={ComputeOverlayPlaneY():0.000}");
        }

        private float ComputeOverlayPlaneY()
        {
            var tankLift = _config != null ? Mathf.Clamp(_config.TankMarkerLift, 0.010f, 0.300f) : 0.045f;
            var uiScale = Mathf.Clamp(_territoryUiScale, 1.4f, 6.0f);
            var tankPlane = _territoryPlaneY + (0.022f * uiScale) + (tankLift * uiScale);
            return tankPlane + 0.002f;
        }

        private void EnsureWorldLegendUi()
        {
            if (_worldLegendRoot != null)
            {
                return;
            }

            var rootGo = new GameObject("BoardWorldLegend");
            rootGo.transform.SetParent(transform, false);
            _worldLegendRoot = rootGo.transform;
            _worldLegendRoot.localScale = Vector3.one;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bg.name = "LegendBg";
            bg.transform.SetParent(_worldLegendRoot, false);
            _worldLegendBg = bg.transform;
            bg.transform.localScale = new Vector3(1.8f, 0.02f, 1.1f);
            var bgCollider = bg.GetComponent<Collider>();
            if (bgCollider != null)
            {
                Destroy(bgCollider);
            }

            var bgRenderer = bg.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                var mat = new Material(shader);
                mat.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
                bgRenderer.material = mat;
            }

            _worldLegendFrameRoot = new GameObject("LegendFrame").transform;
            _worldLegendFrameRoot.SetParent(_worldLegendRoot, false);
            CreateLegendFramePiece("Top");
            CreateLegendFramePiece("Bottom");
            CreateLegendFramePiece("Left");
            CreateLegendFramePiece("Right");

            _worldLegendTitle = CreateLegendText("LegendTitle", Vector3.zero, 56, 0.050f, FontStyle.Bold, new Color(1f, 0.95f, 0.80f, 1f));
            _worldLegendTitle.text = "CONTINENT BONUSES";

            for (var i = 0; i < 6; i++)
            {
                var row = CreateLegendText(
                    $"LegendRow_{i + 1}",
                    Vector3.zero,
                    48,
                    0.043f,
                    FontStyle.Bold,
                    Color.white);
                row.text = string.Empty;
                _worldLegendRows.Add(row);
            }

            _worldLegendSelected = CreateLegendText(
                "LegendSelected",
                Vector3.zero,
                46,
                0.040f,
                FontStyle.Bold,
                new Color(0.90f, 0.95f, 1f, 1f));

            PlaceWorldLegendBottomLeft();
            ApplyWorldLegendLayout();
            _nextWorldLegendRefreshAt = 0f;
        }

        private TextMesh CreateLegendText(string name, Vector3 localPos, int fontSize, float characterSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_worldLegendRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one;
            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.UpperLeft;
            tm.alignment = TextAlignment.Left;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.fontStyle = style;
            tm.color = color;
            return tm;
        }

        private GameObject CreateLegendFramePiece(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Frame_" + name;
            go.transform.SetParent(_worldLegendFrameRoot, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                var mat = new Material(shader);
                mat.color = new Color(0.28f, 0.18f, 0.11f, 1f);
                renderer.material = mat;
            }

            return go;
        }

        private void PlaceWorldLegendBottomLeft()
        {
            if (_worldLegendRoot == null)
            {
                return;
            }

            _worldLegendBoardBack = FindBoardBackRenderer();
            if (_worldLegendBoardBack == null || _worldLegendBg == null)
            {
                _worldLegendRoot.position = new Vector3(0f, _boardSurfaceY + 0.02f, 0f);
                _worldLegendRoot.rotation = Quaternion.identity;
                _worldLegendRoot.localScale = Vector3.one;
                return;
            }

            var tr = _worldLegendBoardBack.transform;
            var localBounds = _worldLegendBoardBack.localBounds;
            var lossy = tr.lossyScale;
            var boardHalfWidth = Mathf.Abs(localBounds.extents.x * lossy.x);
            var boardHalfDepth = Mathf.Abs(localBounds.extents.z * lossy.z);
            var panelWidthWorld = Mathf.Clamp((boardHalfWidth * 2f) * 0.22f, (boardHalfWidth * 2f) * 0.14f, (boardHalfWidth * 2f) * 0.30f);
            var panelDepthWorld = Mathf.Clamp((boardHalfDepth * 2f) * 0.26f, (boardHalfDepth * 2f) * 0.16f, (boardHalfDepth * 2f) * 0.34f);
            _worldLegendBg.localScale = new Vector3(panelWidthWorld, 0.010f, panelDepthWorld);

            var insetX = Mathf.Max(0.04f, (boardHalfWidth * 2f) * 0.03f);
            var insetZ = Mathf.Max(0.04f, (boardHalfDepth * 2f) * 0.03f);
            var xOffset = -boardHalfWidth + insetX + (panelWidthWorld * 0.5f);
            var zOffset = -boardHalfDepth + insetZ + (panelDepthWorld * 0.5f);
            var pos = tr.position
                + (tr.right * xOffset)
                + (tr.forward * zOffset)
                + (tr.up * 0.008f);

            _worldLegendRoot.SetParent(transform, true);
            _worldLegendRoot.position = pos;
            _worldLegendRoot.rotation = tr.rotation;
            _worldLegendRoot.localScale = Vector3.one;
        }

        private void ApplyWorldLegendLayout()
        {
            if (_worldLegendBg == null)
            {
                return;
            }

            var width = _worldLegendBg.localScale.x;
            var depth = _worldLegendBg.localScale.z;
            var left = -width * 0.46f;
            var top = depth * 0.40f;
            var contentTop = depth * 0.24f;
            var contentBottom = -depth * 0.20f;
            var rowStep = _worldLegendRows.Count > 1
                ? (contentTop - contentBottom) / (_worldLegendRows.Count - 1)
                : depth * 0.10f;
            var charScale = Mathf.Clamp(Mathf.Min(width, depth) * 0.010f, 0.028f, 0.070f);
            var textLift = 0.0125f;

            if (_worldLegendTitle != null)
            {
                _worldLegendTitle.transform.localPosition = new Vector3(left, textLift, top);
                _worldLegendTitle.characterSize = charScale * 1.06f;
            }

            for (var i = 0; i < _worldLegendRows.Count; i++)
            {
                var row = _worldLegendRows[i];
                if (row == null)
                {
                    continue;
                }

                row.transform.localPosition = new Vector3(left, textLift, contentTop - (rowStep * i));
                row.characterSize = charScale * 0.94f;
            }

            if (_worldLegendSelected != null)
            {
                _worldLegendSelected.transform.localPosition = new Vector3(left, textLift, -depth * 0.36f);
                _worldLegendSelected.characterSize = charScale * 0.82f;
            }

            if (_worldLegendFrameRoot != null)
            {
                var border = Mathf.Clamp(Mathf.Min(width, depth) * 0.05f, 0.04f, 0.12f);
                var frameHeight = 0.018f;
                var frameY = 0.012f;
                var topEdge = _worldLegendFrameRoot.Find("Frame_Top");
                var bottomEdge = _worldLegendFrameRoot.Find("Frame_Bottom");
                var leftEdge = _worldLegendFrameRoot.Find("Frame_Left");
                var rightEdge = _worldLegendFrameRoot.Find("Frame_Right");

                if (topEdge != null)
                {
                    topEdge.localScale = new Vector3(width + (border * 2f), frameHeight, border);
                    topEdge.localPosition = new Vector3(0f, frameY, (depth * 0.5f) + (border * 0.5f));
                }

                if (bottomEdge != null)
                {
                    bottomEdge.localScale = new Vector3(width + (border * 2f), frameHeight, border);
                    bottomEdge.localPosition = new Vector3(0f, frameY, (-depth * 0.5f) - (border * 0.5f));
                }

                if (leftEdge != null)
                {
                    leftEdge.localScale = new Vector3(border, frameHeight, depth);
                    leftEdge.localPosition = new Vector3((-width * 0.5f) - (border * 0.5f), frameY, 0f);
                }

                if (rightEdge != null)
                {
                    rightEdge.localScale = new Vector3(border, frameHeight, depth);
                    rightEdge.localPosition = new Vector3((width * 0.5f) + (border * 0.5f), frameY, 0f);
                }
            }
        }

        private static float GetAxisScale(float sx, float sy, float sz, int axis)
        {
            return axis switch
            {
                0 => sx,
                1 => sy,
                _ => sz
            };
        }

        private static float GetWorldAxisLength(Bounds localBounds, float sx, float sy, float sz, int axis)
        {
            return axis switch
            {
                0 => localBounds.size.x * sx,
                1 => localBounds.size.y * sy,
                _ => localBounds.size.z * sz
            };
        }

        private static Vector3 GetAxisVector(int axis)
        {
            return axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };
        }

        private static Renderer FindBoardBackRenderer()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.gameObject == null)
                {
                    continue;
                }

                if (renderer.gameObject.name == "Board_Back")
                {
                    return renderer;
                }
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.gameObject == null)
                {
                    continue;
                }

                if (renderer.gameObject.name.IndexOf("board_back", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return renderer;
                }
            }

            return null;
        }

        private void RefreshWorldLegendUi()
        {
            if (_worldLegendRoot == null)
            {
                return;
            }

            PlaceWorldLegendBottomLeft();
            ApplyWorldLegendLayout();

            if (Time.time < _nextWorldLegendRefreshAt)
            {
                return;
            }

            _nextWorldLegendRefreshAt = Time.time + 0.20f;
            PlaceWorldLegendBottomLeft();
            ApplyWorldLegendLayout();

            var orderedContinents = _map?.continents != null
                ? _map.continents.Where(c => c != null && !string.IsNullOrWhiteSpace(c.id)).ToList()
                : new List<ContinentData>();
            for (var i = 0; i < _worldLegendRows.Count; i++)
            {
                var row = _worldLegendRows[i];
                if (row == null)
                {
                    continue;
                }

                if (i < orderedContinents.Count)
                {
                    var continent = orderedContinents[i];
                    row.color = _continentColors.TryGetValue(continent.id, out var continentColor)
                        ? new Color(continentColor.r, continentColor.g, continentColor.b, 1f)
                        : new Color(0.90f, 0.90f, 0.90f, 1f);
                    var owner = GetContinentOwnerIndex(continent);
                    var ownerText = owner >= 0 ? $"  (P{owner + 1})" : string.Empty;
                    row.text = $"{ToDisplayName(continent.id)}: +{Mathf.Max(0, continent.bonus)}{ownerText}";
                }
                else
                {
                    row.text = string.Empty;
                }
            }

            if (orderedContinents.Count == 0 && _worldLegendRows.Count > 0 && _worldLegendRows[0] != null)
            {
                _worldLegendRows[0].color = new Color(0.85f, 0.88f, 0.92f, 1f);
                _worldLegendRows[0].text = "No continent data loaded.";
            }

            if (_worldLegendSelected == null)
            {
                return;
            }

            _worldLegendSelected.text = "Control all territories in a continent\nto receive the listed reinforcement bonus.";
        }

        private int GetContinentOwnerIndex(ContinentData continent)
        {
            if (continent?.territories == null || continent.territories.Length == 0)
            {
                return -1;
            }

            var owner = -1;
            for (var i = 0; i < continent.territories.Length; i++)
            {
                var territoryId = continent.territories[i];
                if (string.IsNullOrWhiteSpace(territoryId) || !_nodes.TryGetValue(territoryId, out var node) || node == null || node.OwnerIndex < 0)
                {
                    return -1;
                }

                if (owner < 0)
                {
                    owner = node.OwnerIndex;
                    continue;
                }

                if (node.OwnerIndex != owner)
                {
                    return -1;
                }
            }

            return owner;
        }

        private Dictionary<int, int> ComputeContinentBonusByOwner()
        {
            var result = new Dictionary<int, int>();
            if (_map?.continents == null || _map.continents.Length == 0)
            {
                return result;
            }

            foreach (var continent in _map.continents)
            {
                if (continent?.territories == null || continent.territories.Length == 0)
                {
                    continue;
                }

                var owner = -1;
                var allOwnedBySamePlayer = true;
                for (var i = 0; i < continent.territories.Length; i++)
                {
                    var territoryId = continent.territories[i];
                    if (string.IsNullOrWhiteSpace(territoryId) || !_nodes.TryGetValue(territoryId, out var node) || node == null || node.OwnerIndex < 0)
                    {
                        allOwnedBySamePlayer = false;
                        break;
                    }

                    if (owner < 0)
                    {
                        owner = node.OwnerIndex;
                        continue;
                    }

                    if (node.OwnerIndex != owner)
                    {
                        allOwnedBySamePlayer = false;
                        break;
                    }
                }

                if (!allOwnedBySamePlayer || owner < 0)
                {
                    continue;
                }

                if (!result.ContainsKey(owner))
                {
                    result[owner] = 0;
                }

                result[owner] += Mathf.Max(0, continent.bonus);
            }

            return result;
        }

        private void CreateTable()
        {
            var sceneTable = FindSceneTableRenderer();
            if (sceneTable != null)
            {
                _tableRenderer = sceneTable;
                var tableBounds = sceneTable.bounds;
                var boardY = tableBounds.max.y + 0.005f;
                if (_config != null)
                {
                    _config.BoardVisualPosition = new Vector3(tableBounds.center.x, boardY - 0.02f, tableBounds.center.z);
                }

                return;
            }

            // If no explicit table renderer is found, use board-back surface height instead of spawning a plane at world origin.
            var boardBack = FindBoardBackRenderer();
            if (boardBack != null)
            {
                _tableRenderer = boardBack;
                return;
            }

            var table = GameObject.CreatePrimitive(PrimitiveType.Plane);
            table.name = "BoardTable";
            table.transform.SetParent(transform, false);
            table.transform.localScale = new Vector3(3.2f, 1f, 2.4f);
            table.transform.position = Vector3.zero;
            var renderer = table.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.18f, 0.22f, 0.20f)
            };
            _tableRenderer = renderer;
        }

        private Renderer FindSceneTableRenderer()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Renderer best = null;
            var bestScore = 0f;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.transform == null)
                {
                    continue;
                }

                if (renderer.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!IsLikelyTableCandidate(renderer))
                {
                    continue;
                }

                var score = ScoreTableCandidate(renderer);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = renderer;
            }

            return best;
        }

        private static bool IsLikelyTableCandidate(Renderer renderer)
        {
            var name = renderer.gameObject.name ?? string.Empty;
            if (name.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var parentName = renderer.transform.parent != null ? renderer.transform.parent.name : string.Empty;
            if (parentName.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var b = renderer.bounds;
                var footprint = b.size.x * b.size.z;
                return b.center.y > 0.35f && footprint >= 0.30f;
            }

            if (parentName.IndexOf("furniture", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var bounds = renderer.bounds;
                var footprint = bounds.size.x * bounds.size.z;
                var thickness = bounds.size.y;
                return bounds.center.y > 0.35f &&
                       footprint >= 0.40f &&
                       thickness <= Mathf.Max(0.30f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.35f);
            }

            return false;
        }

        private static float ScoreTableCandidate(Renderer renderer)
        {
            var bounds = renderer.bounds;
            var footprint = bounds.size.x * bounds.size.z;
            var thickness = Mathf.Max(0.05f, bounds.size.y);
            var flatness = footprint / thickness;
            var heightBonus = Mathf.Clamp01((bounds.center.y - 0.35f) / 0.8f);
            return flatness * (1f + (0.35f * heightBonus));
        }

        private void CreateTerritories()
        {
            foreach (var territory in _map.territories)
            {
                if (territory == null || string.IsNullOrWhiteSpace(territory.id))
                {
                    continue;
                }

                var worldPos = TryGetManifestPosition(territory.id, out var pos)
                    ? pos
                    : GetFallbackPositionFromContinent(territory.continent, territory.id);
                CreateTerritoryNode(territory.id, territory.continent, worldPos);
            }
        }

        private bool TryGetManifestPosition(string territoryId, out Vector3 position)
        {
            if (_territoryPositions.TryGetValue(territoryId, out var raw))
            {
                position = _config.TerritoryPositionOffset + (raw * _config.TerritoryPositionScale);
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private Vector3 GetFallbackPositionFromContinent(string continentId, string territoryId)
        {
            var anchor = ContinentAnchors.TryGetValue(continentId, out var pos) ? pos : Vector3.zero;
            var continent = Array.Find(_map.continents, c => c != null && c.id == continentId);
            if (continent == null || continent.territories == null || continent.territories.Length == 0)
            {
                return anchor;
            }

            var idx = Array.IndexOf(continent.territories, territoryId);
            if (idx < 0)
            {
                idx = 0;
            }

            var radius = Mathf.Lerp(1.5f, 3.2f, Mathf.Clamp01(continent.territories.Length / 12f));
            var angle = (idx / (float)continent.territories.Length) * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            return anchor + offset;
        }

        private void CreateAdjacencyLines()
        {
            var root = new GameObject("AdjacencyLines");
            root.transform.SetParent(transform, false);

            var edgeSet = new HashSet<string>();
            var seaMaterial = new Material(Shader.Find("Sprites/Default"));
            foreach (var t in _map.territories)
            {
                if (t == null || t.neighbors == null)
                {
                    continue;
                }

                foreach (var n in t.neighbors)
                {
                    if (!_nodes.ContainsKey(t.id) || !_nodes.ContainsKey(n))
                    {
                        continue;
                    }

                    var a = string.CompareOrdinal(t.id, n) <= 0 ? t.id : n;
                    var b = string.CompareOrdinal(t.id, n) <= 0 ? n : t.id;
                    var key = $"{a}|{b}";
                    if (!edgeSet.Add(key))
                    {
                        continue;
                    }

                    if (!IsSeaConnection(a, b))
                    {
                        continue;
                    }

                    var n1 = _nodes[a].transform.position;
                    var n2 = _nodes[b].transform.position;
                    var p1 = new Vector3(n1.x, _boardSurfaceY, n1.z);
                    var p2 = new Vector3(n2.x, _boardSurfaceY, n2.z);
                    CreateDottedLine(root.transform, $"SeaEdge_{a}_{b}", p1, p2, seaMaterial, new Color(0.35f, 0.74f, 1f, 0.72f));
                }
            }

            _adjacencyRoot = root;
        }

        private void ApplyVisualMode()
        {
            if (_config == null)
            {
                return;
            }

            var showDebugNodes = _config.VisualMode != BoardVisualMode.VisualOnly;
            var showDebugLines = _config.VisualMode == BoardVisualMode.VisualWithDebug || _config.VisualMode == BoardVisualMode.DebugOnly;

            foreach (var node in _nodes.Values)
            {
                var renderers = node.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    // Keep text renderers (territory name + armies) visible in VisualOnly mode.
                    var text = r.GetComponent<TextMesh>();
                    if (text != null)
                    {
                        r.enabled = true;
                        continue;
                    }

                    r.enabled = showDebugNodes;
                }

                var labels = node.GetComponentsInChildren<TextMesh>(true);
                foreach (var l in labels)
                {
                    l.gameObject.SetActive(true);
                }
            }

            if (_adjacencyRoot != null)
            {
                _adjacencyRoot.SetActive(showDebugLines);
            }
        }

        private void CreateTerritoryNode(string territoryId, string continentId, Vector3 position)
        {
            var nodeObj = new GameObject($"Territory_{territoryId}");
            nodeObj.name = $"Territory_{territoryId}";
            nodeObj.transform.SetParent(transform, false);
            position.y = _boardSurfaceY + TerritoryOverlayHeight;
            nodeObj.transform.position = position;
            _territoryPlaneY = position.y;

            var collider = nodeObj.AddComponent<SphereCollider>();
            collider.radius = 0.18f * _territoryUiScale;

            var baseColor = _continentColors.TryGetValue(continentId, out var c) ? c : new Color(0.8f, 0.8f, 0.8f);

            var displayName = _localizedNames.TryGetValue(territoryId, out var localized)
                ? localized
                : ToDisplayName(territoryId);

            var node = nodeObj.AddComponent<TerritoryNode>();
            node.Initialize(territoryId, displayName, continentId, baseColor);
            node.SetUiScale(_territoryUiScale);
            node.ConfigureArmyMarkerVisuals(
                _config != null ? _config.TankMarkerPrefab : null,
                _config != null ? _config.TankMarkerScale : 0.012f,
                _config != null ? _config.TankMarkerLocalEuler : new Vector3(-90f, 0f, 0f),
                _config != null ? _config.TankMarkerLift : 0.045f,
                _config != null ? _config.FlagMarkerPrefab : null,
                _config != null ? _config.FlagMarkerScale : 0.020f,
                _config != null ? _config.FlagMarkerLocalEuler : new Vector3(-90f, 0f, 0f),
                _config != null ? _config.FlagMarkerLift : 0.060f);
            _nodes[territoryId] = node;
        }

        private bool TryPickTerritoryFromShapes(Vector2 pointerScreenPosition, out TerritoryNode node)
        {
            node = null;
            if (!_hasTerritoryShapes || _camera == null)
            {
                return false;
            }

            var p = Vector2.zero;
            var polygons = _territoryShapePolygons;
            var centroids = _territoryShapeCentroids;

            if (_turntableMode && _territoryShapePolygonsLocal.Count > 0)
            {
                var boardRenderer = FindBoardBackRenderer();
                if (boardRenderer == null)
                {
                    return false;
                }

            if (!TryGetBoardSurfaceFrame(boardRenderer, out var boardCenter, out var boardRight, out var boardForward, out var boardNormal, true))
            {
                return false;
            }
            Trace("Select", $"board frame center={boardCenter} right={boardRight} fwd={boardForward} normal={boardNormal}");

                var ray = _camera.ScreenPointToRay(pointerScreenPosition);
                var plane = new Plane(boardNormal, boardCenter);
                if (!plane.Raycast(ray, out var enter))
                {
                    return false;
                }

                var worldPoint = ray.GetPoint(enter);
                var toPoint = worldPoint - boardCenter;
                p = new Vector2(
                    Vector3.Dot(toPoint, boardRight),
                    Vector3.Dot(toPoint, boardForward));
                polygons = _territoryShapePolygonsLocal;
                centroids = _territoryShapeCentroidsLocal;
            }
            else
            {
                var ray = _camera.ScreenPointToRay(pointerScreenPosition);
                var plane = new Plane(Vector3.up, new Vector3(0f, _territoryPlaneY, 0f));
                if (!plane.Raycast(ray, out var enter))
                {
                    return false;
                }

                var worldPoint = ray.GetPoint(enter);
                p = new Vector2(worldPoint.x, worldPoint.z);
            }

            var bestDistance = float.MaxValue;
            string bestTerritoryId = null;
            foreach (var kv in polygons)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                {
                    continue;
                }

                var inside = false;
                foreach (var polygon in kv.Value)
                {
                    if (polygon == null || polygon.Count < 3)
                    {
                        continue;
                    }

                    if (IsPointInsidePolygon(polygon, p))
                    {
                        inside = true;
                        break;
                    }
                }

                if (!inside)
                {
                    continue;
                }

                var centroid = centroids.TryGetValue(kv.Key, out var c) ? c : p;
                var sqrDistance = (centroid - p).sqrMagnitude;
                if (sqrDistance < bestDistance)
                {
                    bestDistance = sqrDistance;
                    bestTerritoryId = kv.Key;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestTerritoryId) && _nodes.TryGetValue(bestTerritoryId, out var picked))
            {
                node = picked;
                return true;
            }

            if (TryFindNearestTerritoryByCentroid(p, 0.85f, centroids, out var nearestId) && _nodes.TryGetValue(nearestId, out var nearest))
            {
                node = nearest;
                return true;
            }

            return false;
        }

        private void BuildTerritoryShapesFromSvg()
        {
            _territoryShapePolygons.Clear();
            _territoryShapeCentroids.Clear();
            _hasTerritoryShapes = false;

            var svgPath = ResolveBoardSvgPath();
            if (string.IsNullOrWhiteSpace(svgPath) || !File.Exists(svgPath))
            {
                return;
            }

            var svgText = File.ReadAllText(svgPath);
            if (string.IsNullOrWhiteSpace(svgText))
            {
                return;
            }

            var rawShapes = ParseSvgTerritoryShapes(svgText, out var viewBox);
            if (rawShapes.Count == 0)
            {
                return;
            }

            if (!TryBuildSvgToWorldTransform(viewBox, out var scaleX, out var offsetX, out var scaleY, out var offsetY))
            {
                Debug.LogWarning("[Risiko3D][Board] Could not calibrate SVG territory shapes to board positions.");
                return;
            }

            var hasSpriteMapper = TryResolveSpriteSpaceMapper(rawShapes, viewBox, out var spriteRenderer, out var flipY);
            if (hasSpriteMapper)
            {
                Debug.Log($"[Risiko3D][Board] SVG overlay uses board sprite-space mapping (flipY={flipY}).");
            }

            foreach (var kv in rawShapes)
            {
                var worldPolygons = new List<List<Vector2>>(kv.Value.Count);
                foreach (var polygon in kv.Value)
                {
                    var worldPolygon = new List<Vector2>(polygon.Count);
                    foreach (var p in polygon)
                    {
                        var nudge = _config != null ? _config.TerritoryShapeWorldNudge : Vector2.zero;
                        if (hasSpriteMapper && TryMapSvgPointWithSpriteRenderer(p, viewBox, spriteRenderer, flipY, out var mapped))
                        {
                            worldPolygon.Add(new Vector2(mapped.x + nudge.x, mapped.y + nudge.y));
                        }
                        else
                        {
                            worldPolygon.Add(new Vector2((p.x * scaleX) + offsetX + nudge.x, (p.y * scaleY) + offsetY + nudge.y));
                        }
                    }

                    if (worldPolygon.Count >= 3)
                    {
                        worldPolygons.Add(worldPolygon);
                    }
                }

                if (worldPolygons.Count == 0)
                {
                    continue;
                }

                _territoryShapePolygons[kv.Key] = worldPolygons;
                _territoryShapeCentroids[kv.Key] = ComputeMultiPolygonCentroid(worldPolygons);
            }

            AutoAlignTerritoryShapesToMapEdges();
            _hasTerritoryShapes = _territoryShapePolygons.Count > 0;
            if (_hasTerritoryShapes)
            {
                RebuildTerritoryShapeLocalCache();
                Debug.Log($"[Risiko3D][Board] SVG territory picking enabled for {_territoryShapePolygons.Count} territories.");
            }
        }

        private void AutoAlignTerritoryShapesToMapEdges()
        {
            if (_territoryShapePolygons.Count == 0 || _visualLayer == null || _visualLayer.SpriteRenderer == null)
            {
                return;
            }

            if (!TryCreateReadableSpriteTexture(_visualLayer.SpriteRenderer, out var readable, out var spriteRect))
            {
                return;
            }

            var aligned = 0;
            foreach (var territoryId in _territoryShapePolygons.Keys.ToList())
            {
                if (!_territoryShapePolygons.TryGetValue(territoryId, out var polygons) || polygons == null || polygons.Count == 0)
                {
                    continue;
                }

                if (!TryFindBestTerritoryOffset(_visualLayer.SpriteRenderer, readable, spriteRect, polygons, out var bestOffset))
                {
                    continue;
                }

                if (bestOffset.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                for (var i = 0; i < polygons.Count; i++)
                {
                    for (var j = 0; j < polygons[i].Count; j++)
                    {
                        polygons[i][j] += bestOffset;
                    }
                }

                _territoryShapeCentroids[territoryId] = ComputeMultiPolygonCentroid(polygons);
                aligned++;
            }

            if (aligned > 0)
            {
                Debug.Log($"[Risiko3D][Board] Edge-fit alignment applied to {aligned} territories.");
            }
        }

        private bool TryFindBestTerritoryOffset(
            SpriteRenderer spriteRenderer,
            Color32[] pixels,
            RectInt spriteRect,
            List<List<Vector2>> polygons,
            out Vector2 bestOffset)
        {
            bestOffset = Vector2.zero;
            var baseScore = EvaluatePolygonEdgeScore(spriteRenderer, pixels, spriteRect, polygons, Vector2.zero);
            var bestScore = baseScore;

            // Coarse search.
            const float coarseRange = 0.35f;
            const float coarseStep = 0.05f;
            for (var ox = -coarseRange; ox <= coarseRange + 0.0001f; ox += coarseStep)
            {
                for (var oy = -coarseRange; oy <= coarseRange + 0.0001f; oy += coarseStep)
                {
                    var score = EvaluatePolygonEdgeScore(spriteRenderer, pixels, spriteRect, polygons, new Vector2(ox, oy));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOffset = new Vector2(ox, oy);
                    }
                }
            }

            // Fine search around coarse best.
            var coarseBest = bestOffset;
            const float fineRange = 0.06f;
            const float fineStep = 0.01f;
            for (var ox = coarseBest.x - fineRange; ox <= coarseBest.x + fineRange + 0.0001f; ox += fineStep)
            {
                for (var oy = coarseBest.y - fineRange; oy <= coarseBest.y + fineRange + 0.0001f; oy += fineStep)
                {
                    var score = EvaluatePolygonEdgeScore(spriteRenderer, pixels, spriteRect, polygons, new Vector2(ox, oy));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOffset = new Vector2(ox, oy);
                    }
                }
            }

            // Require measurable improvement to avoid drift.
            return (bestScore - baseScore) > 0.03f;
        }

        private float EvaluatePolygonEdgeScore(
            SpriteRenderer spriteRenderer,
            Color32[] pixels,
            RectInt spriteRect,
            List<List<Vector2>> polygons,
            Vector2 offset)
        {
            var hit = 0f;
            var total = 0f;
            const float worldSampleStep = 0.065f;

            foreach (var polygon in polygons)
            {
                if (polygon == null || polygon.Count < 2)
                {
                    continue;
                }

                for (var i = 0; i < polygon.Count; i++)
                {
                    var p0 = polygon[i] + offset;
                    var p1 = polygon[(i + 1) % polygon.Count] + offset;
                    var len = Vector2.Distance(p0, p1);
                    var steps = Mathf.Max(1, Mathf.CeilToInt(len / worldSampleStep));
                    for (var s = 0; s <= steps; s++)
                    {
                        var t = s / (float)steps;
                        var p = Vector2.Lerp(p0, p1, t);
                        if (TryWorldToSpritePixel(spriteRenderer, spriteRect, p, out var px, out var py))
                        {
                            total += 1f;
                            var c = pixels[(py * spriteRect.width) + px];
                            if (IsLikelyBorderColor(c))
                            {
                                hit += 1f;
                            }
                        }
                    }
                }
            }

            return total > 0.0001f ? hit / total : 0f;
        }

        private static bool TryWorldToSpritePixel(
            SpriteRenderer spriteRenderer,
            RectInt spriteRect,
            Vector2 worldXZ,
            out int px,
            out int py)
        {
            px = py = 0;
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return false;
            }

            var world = new Vector3(worldXZ.x, spriteRenderer.transform.position.y, worldXZ.y);
            var local = spriteRenderer.transform.InverseTransformPoint(world);
            var size = spriteRenderer.sprite.bounds.size;
            if (size.x <= 0.0001f || size.y <= 0.0001f)
            {
                return false;
            }

            var u = (local.x / size.x) + 0.5f;
            var v = (local.y / size.y) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return false;
            }

            px = Mathf.Clamp(Mathf.RoundToInt(u * (spriteRect.width - 1)), 0, spriteRect.width - 1);
            py = Mathf.Clamp(Mathf.RoundToInt(v * (spriteRect.height - 1)), 0, spriteRect.height - 1);
            return true;
        }

        private static bool IsLikelyBorderColor(Color32 c)
        {
            var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max >= 170 && (max - min) <= 72;
        }

        private static bool TryCreateReadableSpriteTexture(
            SpriteRenderer spriteRenderer,
            out Color32[] pixels,
            out RectInt spriteRect)
        {
            pixels = null;
            spriteRect = new RectInt(0, 0, 0, 0);
            if (spriteRenderer == null || spriteRenderer.sprite == null || spriteRenderer.sprite.texture == null)
            {
                return false;
            }

            var src = spriteRenderer.sprite.texture;
            var rect = spriteRenderer.sprite.textureRect;
            spriteRect = new RectInt(0, 0, Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
            if (spriteRect.width <= 0 || spriteRect.height <= 0)
            {
                return false;
            }

            try
            {
                // Fast path when source texture is readable.
                pixels = src.GetPixels32();
                if (pixels != null && pixels.Length == src.width * src.height)
                {
                    // Crop to sprite rect into compact buffer.
                    var cropped = new Color32[spriteRect.width * spriteRect.height];
                    var srcX = Mathf.RoundToInt(rect.x);
                    var srcY = Mathf.RoundToInt(rect.y);
                    for (var y = 0; y < spriteRect.height; y++)
                    {
                        Array.Copy(
                            pixels,
                            (srcY + y) * src.width + srcX,
                            cropped,
                            y * spriteRect.width,
                            spriteRect.width);
                    }

                    pixels = cropped;
                    return true;
                }
            }
            catch
            {
                // Fallback below.
            }

            var rt = RenderTexture.GetTemporary(spriteRect.width, spriteRect.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var readableTex = new Texture2D(spriteRect.width, spriteRect.height, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0f, 0f, spriteRect.width, spriteRect.height), 0, 0);
            readableTex.Apply(false, false);
            pixels = readableTex.GetPixels32();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (Application.isPlaying)
            {
                Destroy(readableTex);
            }
            else
            {
                DestroyImmediate(readableTex);
            }

            return pixels != null && pixels.Length == spriteRect.width * spriteRect.height;
        }

        private bool TryResolveSpriteSpaceMapper(
            Dictionary<string, List<List<Vector2>>> rawShapes,
            Rect viewBox,
            out SpriteRenderer spriteRenderer,
            out bool flipY)
        {
            spriteRenderer = _visualLayer != null ? _visualLayer.SpriteRenderer : null;
            flipY = true;
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return false;
            }

            // SVG often uses top-left origin; verify orientation against territory anchors.
            var errorFlip = ComputeMapperFitError(rawShapes, viewBox, spriteRenderer, true);
            var errorNoFlip = ComputeMapperFitError(rawShapes, viewBox, spriteRenderer, false);
            flipY = errorFlip <= errorNoFlip;
            return true;
        }

        private float ComputeMapperFitError(
            Dictionary<string, List<List<Vector2>>> rawShapes,
            Rect viewBox,
            SpriteRenderer spriteRenderer,
            bool flipY)
        {
            if (rawShapes == null || rawShapes.Count == 0 || spriteRenderer == null)
            {
                return float.MaxValue;
            }

            float sum = 0f;
            var count = 0;
            foreach (var kv in rawShapes)
            {
                if (!_nodes.TryGetValue(kv.Key, out var node) || node == null)
                {
                    continue;
                }

                var centroid = ComputeMultiPolygonCentroid(kv.Value);
                if (!TryMapSvgPointWithSpriteRenderer(centroid, viewBox, spriteRenderer, flipY, out var mapped))
                {
                    continue;
                }

                var nodePos = node.transform.position;
                sum += (new Vector2(nodePos.x, nodePos.z) - mapped).sqrMagnitude;
                count++;
            }

            return count > 0 ? (sum / count) : float.MaxValue;
        }

        private static bool TryMapSvgPointWithSpriteRenderer(
            Vector2 svgPoint,
            Rect viewBox,
            SpriteRenderer spriteRenderer,
            bool flipY,
            out Vector2 worldXZ)
        {
            worldXZ = Vector2.zero;
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return false;
            }

            var safeWidth = Mathf.Abs(viewBox.width) > 0.0001f ? viewBox.width : 1f;
            var safeHeight = Mathf.Abs(viewBox.height) > 0.0001f ? viewBox.height : 1f;
            var u = (svgPoint.x - viewBox.x) / safeWidth;
            var v = (svgPoint.y - viewBox.y) / safeHeight;
            if (flipY)
            {
                v = 1f - v;
            }

            var spriteSize = spriteRenderer.sprite.bounds.size;
            var localX = (u - 0.5f) * spriteSize.x;
            var localY = (v - 0.5f) * spriteSize.y;
            var world = spriteRenderer.transform.TransformPoint(new Vector3(localX, localY, 0f));
            worldXZ = new Vector2(world.x, world.z);
            return true;
        }

        private string ResolveBoardSvgPath()
        {
            var root = Directory.GetParent(Application.dataPath);
            if (root == null)
            {
                return string.Empty;
            }

            var candidates = new List<string>(4);
            if (_config != null && !string.IsNullOrWhiteSpace(_config.BoardMapSpriteResourcePath))
            {
                var resourcePath = _config.BoardMapSpriteResourcePath.TrimStart('/', '\\');
                var withExtension = resourcePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    ? resourcePath
                    : $"{resourcePath}.svg";
                candidates.Add(Path.Combine("Assets", "Resources", withExtension.Replace("/", Path.DirectorySeparatorChar.ToString())));
            }

            candidates.Add(Path.Combine("Assets", "Resources", "Map", "world_classic_map.svg"));
            candidates.Add(Path.Combine("Assets", "Art", "Map", "map.svg"));

            for (var i = 0; i < candidates.Count; i++)
            {
                var fullPath = Path.Combine(root.FullName, candidates[i]);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            var resourcesMapDir = Path.Combine(root.FullName, "Assets", "Resources", "Map");
            if (Directory.Exists(resourcesMapDir))
            {
                var svgs = Directory.GetFiles(resourcesMapDir, "*.svg", SearchOption.TopDirectoryOnly);
                if (svgs.Length > 0)
                {
                    return svgs[0];
                }
            }

            return string.Empty;
        }

        private bool TryBuildSvgToWorldTransform(
            Rect viewBox,
            out float scaleX,
            out float offsetX,
            out float scaleY,
            out float offsetY)
        {
            var safeWidth = Mathf.Abs(viewBox.width) > 0.0001f ? viewBox.width : 1f;
            var safeHeight = Mathf.Abs(viewBox.height) > 0.0001f ? viewBox.height : 1f;
            scaleX = _config.BoardVisualWorldSize.x / safeWidth;
            scaleY = _config.BoardVisualWorldSize.y / safeHeight;
            offsetX = _config.BoardVisualPosition.x - ((viewBox.x + (safeWidth * 0.5f)) * scaleX);
            offsetY = _config.BoardVisualPosition.z - ((viewBox.y + (safeHeight * 0.5f)) * scaleY);
            return true;
        }

        private static Dictionary<string, List<List<Vector2>>> ParseSvgTerritoryShapes(string svgText, out Rect viewBox)
        {
            viewBox = new Rect(0f, 0f, 1f, 1f);
            var shapes = new Dictionary<string, List<List<Vector2>>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(svgText))
            {
                return shapes;
            }

            XDocument svgDoc;
            try
            {
                svgDoc = XDocument.Parse(svgText);
            }
            catch
            {
                return shapes;
            }

            var root = svgDoc.Root;
            if (root == null)
            {
                return shapes;
            }

            var viewBoxValue = root.Attribute("viewBox")?.Value;
            if (!string.IsNullOrWhiteSpace(viewBoxValue))
            {
                var components = Regex.Split(viewBoxValue.Trim(), @"\s+");
                if (components.Length == 4
                    && float.TryParse(components[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var vx)
                    && float.TryParse(components[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var vy)
                    && float.TryParse(components[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var vw)
                    && float.TryParse(components[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var vh))
                {
                    viewBox = new Rect(vx, vy, vw, vh);
                }
            }

            var ns = root.Name.Namespace;
            foreach (var group in root.Descendants(ns + "g"))
            {
                var territoryId = group.Attribute("id")?.Value;
                if (string.IsNullOrWhiteSpace(territoryId))
                {
                    continue;
                }

                var polygons = new List<List<Vector2>>();
                foreach (var path in group.Descendants(ns + "path"))
                {
                    var d = path.Attribute("d")?.Value;
                    if (string.IsNullOrWhiteSpace(d))
                    {
                        continue;
                    }

                    polygons.AddRange(ParseSvgPathToPolygons(d));
                }

                if (polygons.Count > 0)
                {
                    shapes[territoryId] = polygons;
                }
            }

            return shapes;
        }

        private static List<List<Vector2>> ParseSvgPathToPolygons(string d)
        {
            var polygons = new List<List<Vector2>>();
            if (string.IsNullOrWhiteSpace(d))
            {
                return polygons;
            }

            var tokens = Regex.Matches(d, @"[MLZmlz]|-?\d+(?:\.\d+)?")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .ToList();

            var command = ' ';
            var cursor = Vector2.zero;
            var current = new List<Vector2>();
            var pendingX = float.NaN;
            var isMovePair = false;

            foreach (var token in tokens)
            {
                if (token.Length == 1 && char.IsLetter(token[0]))
                {
                    command = token[0];
                    pendingX = float.NaN;
                    isMovePair = command == 'M' || command == 'm';
                    if (command == 'Z' || command == 'z')
                    {
                        if (current.Count >= 3)
                        {
                            polygons.Add(current);
                        }

                        current = new List<Vector2>();
                    }

                    continue;
                }

                if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                {
                    continue;
                }

                if (float.IsNaN(pendingX))
                {
                    pendingX = numeric;
                    continue;
                }

                var rawX = pendingX;
                var rawY = numeric;
                pendingX = float.NaN;

                var absolute = command == 'M' || command == 'L';
                var next = absolute
                    ? new Vector2(rawX, rawY)
                    : cursor + new Vector2(rawX, rawY);

                if (isMovePair && current.Count > 0)
                {
                    if (current.Count >= 3)
                    {
                        polygons.Add(current);
                    }

                    current = new List<Vector2>();
                }

                current.Add(next);
                cursor = next;
                if (isMovePair)
                {
                    isMovePair = false;
                    command = command == 'M' ? 'L' : command == 'm' ? 'l' : command;
                }
            }

            if (current.Count >= 3)
            {
                polygons.Add(current);
            }

            return polygons;
        }

        private static bool IsPointInsidePolygon(List<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                if (DistancePointToSegment(point, pj, pi) <= 0.045f)
                {
                    return true;
                }

                var intersects = ((pi.y > point.y) != (pj.y > point.y))
                    && (point.x < ((pj.x - pi.x) * (point.y - pi.y) / ((pj.y - pi.y) + 0.000001f)) + pi.x);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private bool TryFindNearestTerritoryByCentroid(
            Vector2 point,
            float maxDistance,
            Dictionary<string, Vector2> centroidSet,
            out string territoryId)
        {
            territoryId = null;
            var best = maxDistance * maxDistance;
            foreach (var kv in centroidSet)
            {
                var d = (kv.Value - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    territoryId = kv.Key;
                }
            }

            return !string.IsNullOrWhiteSpace(territoryId);
        }

        private void RebuildTerritoryShapeLocalCache()
        {
            _territoryShapePolygonsLocal.Clear();
            _territoryShapeCentroidsLocal.Clear();

            var boardRenderer = FindBoardBackRenderer();
            if (boardRenderer == null || _territoryShapePolygons.Count == 0)
            {
                return;
            }

            if (!TryGetBoardSurfaceFrame(boardRenderer, out var boardCenter, out var boardRight, out var boardForward, out _, true))
            {
                return;
            }

            foreach (var kv in _territoryShapePolygons)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                {
                    continue;
                }

                var localPolygons = new List<List<Vector2>>(kv.Value.Count);
                foreach (var polygon in kv.Value)
                {
                    if (polygon == null || polygon.Count < 3)
                    {
                        continue;
                    }

                    var localPolygon = new List<Vector2>(polygon.Count);
                    for (var i = 0; i < polygon.Count; i++)
                    {
                        var world = new Vector3(polygon[i].x, _territoryPlaneY, polygon[i].y);
                        var toPoint = world - boardCenter;
                        localPolygon.Add(new Vector2(
                            Vector3.Dot(toPoint, boardRight),
                            Vector3.Dot(toPoint, boardForward)));
                    }

                    if (localPolygon.Count >= 3)
                    {
                        localPolygons.Add(localPolygon);
                    }
                }

                if (localPolygons.Count == 0)
                {
                    continue;
                }

                _territoryShapePolygonsLocal[kv.Key] = localPolygons;
                _territoryShapeCentroidsLocal[kv.Key] = ComputeMultiPolygonCentroid(localPolygons);
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var sqrMag = ab.sqrMagnitude;
            if (sqrMag <= 0.000001f)
            {
                return Vector2.Distance(point, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / sqrMag);
            var projection = a + (ab * t);
            return Vector2.Distance(point, projection);
        }

        private bool TryPickNearestNodeFromBoardSurface(Vector2 pointerScreenPosition, out TerritoryNode node)
        {
            node = null;
            if (_camera == null)
            {
                return false;
            }

            var boardRenderer = FindBoardBackRenderer();
            if (boardRenderer == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(pointerScreenPosition);
            var plane = new Plane(boardRenderer.transform.up, boardRenderer.bounds.center);
            if (!plane.Raycast(ray, out var enter))
            {
                return false;
            }

            var worldPoint = ray.GetPoint(enter);
            var bestNode = (TerritoryNode)null;
            var bestDist = float.MaxValue;
            foreach (var candidate in _nodes.Values)
            {
                if (candidate == null)
                {
                    continue;
                }

                var dist = (candidate.transform.position - worldPoint).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNode = candidate;
                }
            }

            if (bestNode == null)
            {
                return false;
            }

            node = bestNode;
            return true;
        }

        private bool IsSeaConnection(string territoryA, string territoryB)
        {
            if (!_territoryShapePolygons.TryGetValue(territoryA, out var polyA)
                || !_territoryShapePolygons.TryGetValue(territoryB, out var polyB)
                || polyA == null || polyB == null)
            {
                return false;
            }

            var minDistance = GetMinMultiPolygonDistance(polyA, polyB);
            return minDistance > SeaConnectionDistanceThreshold;
        }

        private static float GetMinMultiPolygonDistance(List<List<Vector2>> a, List<List<Vector2>> b)
        {
            var best = float.MaxValue;
            foreach (var pa in a)
            {
                if (pa == null || pa.Count < 2)
                {
                    continue;
                }

                foreach (var pb in b)
                {
                    if (pb == null || pb.Count < 2)
                    {
                        continue;
                    }

                    var d = GetMinPolygonDistance(pa, pb);
                    if (d < best)
                    {
                        best = d;
                    }

                    if (best <= 0.0001f)
                    {
                        return 0f;
                    }
                }
            }

            return best;
        }

        private static float GetMinPolygonDistance(List<Vector2> polygonA, List<Vector2> polygonB)
        {
            if (polygonA.Count >= 3 && polygonB.Count >= 3)
            {
                if (IsPointInsidePolygon(polygonA, polygonB[0]) || IsPointInsidePolygon(polygonB, polygonA[0]))
                {
                    return 0f;
                }
            }

            for (int i = 0, iPrev = polygonA.Count - 1; i < polygonA.Count; iPrev = i++)
            {
                for (int j = 0, jPrev = polygonB.Count - 1; j < polygonB.Count; jPrev = j++)
                {
                    if (SegmentsIntersect(polygonA[iPrev], polygonA[i], polygonB[jPrev], polygonB[j]))
                    {
                        return 0f;
                    }
                }
            }

            var best = float.MaxValue;
            for (var i = 0; i < polygonA.Count; i++)
            {
                for (int j = 0, jPrev = polygonB.Count - 1; j < polygonB.Count; jPrev = j++)
                {
                    best = Mathf.Min(best, DistancePointToSegment(polygonA[i], polygonB[jPrev], polygonB[j]));
                }
            }

            for (var i = 0; i < polygonB.Count; i++)
            {
                for (int j = 0, jPrev = polygonA.Count - 1; j < polygonA.Count; jPrev = j++)
                {
                    best = Mathf.Min(best, DistancePointToSegment(polygonB[i], polygonA[jPrev], polygonA[j]));
                }
            }

            return best;
        }

        private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            var o1 = Orientation(p1, p2, q1);
            var o2 = Orientation(p1, p2, q2);
            var o3 = Orientation(q1, q2, p1);
            var o4 = Orientation(q1, q2, p2);

            if (o1 != o2 && o3 != o4)
            {
                return true;
            }

            if (o1 == 0 && OnSegment(p1, q1, p2)) return true;
            if (o2 == 0 && OnSegment(p1, q2, p2)) return true;
            if (o3 == 0 && OnSegment(q1, p1, q2)) return true;
            if (o4 == 0 && OnSegment(q1, p2, q2)) return true;
            return false;
        }

        private static int Orientation(Vector2 a, Vector2 b, Vector2 c)
        {
            var val = ((b.y - a.y) * (c.x - b.x)) - ((b.x - a.x) * (c.y - b.y));
            if (Mathf.Abs(val) < 0.000001f) return 0;
            return val > 0f ? 1 : 2;
        }

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 c)
        {
            return b.x <= Mathf.Max(a.x, c.x) + 0.000001f
                && b.x + 0.000001f >= Mathf.Min(a.x, c.x)
                && b.y <= Mathf.Max(a.y, c.y) + 0.000001f
                && b.y + 0.000001f >= Mathf.Min(a.y, c.y);
        }

        private static void CreateDottedLine(Transform parent, string name, Vector3 from, Vector3 to, Material material, Color color)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);

            var segmentCount = 16;
            var dotRatio = 0.36f;
            var direction = to - from;
            for (var i = 0; i < segmentCount; i++)
            {
                var startT = i / (float)segmentCount;
                var endT = Mathf.Min(1f, startT + (dotRatio / segmentCount));
                var p0 = from + (direction * startT);
                var p1 = from + (direction * endT);

                var go = new GameObject($"dot_{i}");
                go.transform.SetParent(container.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = material;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.04f;
                lr.endWidth = 0.04f;
                lr.startColor = color;
                lr.endColor = color;
                lr.SetPosition(0, p0);
                lr.SetPosition(1, p1);
            }
        }

        private static Vector2 ComputeMultiPolygonCentroid(List<List<Vector2>> polygons)
        {
            if (polygons == null || polygons.Count == 0)
            {
                return Vector2.zero;
            }

            var weightedCentroidSum = Vector2.zero;
            var totalWeight = 0f;
            foreach (var polygon in polygons)
            {
                var centroid = ComputePolygonCentroid(polygon, out var signedArea);
                var weight = Mathf.Abs(signedArea);
                if (weight < 0.000001f)
                {
                    weight = Mathf.Max(1f, polygon?.Count ?? 1);
                }

                weightedCentroidSum += centroid * weight;
                totalWeight += weight;
            }

            return totalWeight > 0.000001f ? weightedCentroidSum / totalWeight : polygons[0][0];
        }

        private static Vector2 ComputePolygonCentroid(List<Vector2> polygon, out float signedArea)
        {
            signedArea = 0f;
            if (polygon == null || polygon.Count == 0)
            {
                return Vector2.zero;
            }

            if (polygon.Count < 3)
            {
                var fallback = Vector2.zero;
                foreach (var p in polygon)
                {
                    fallback += p;
                }

                return fallback / polygon.Count;
            }

            float cx = 0f;
            float cy = 0f;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var p0 = polygon[j];
                var p1 = polygon[i];
                var cross = (p0.x * p1.y) - (p1.x * p0.y);
                signedArea += cross;
                cx += (p0.x + p1.x) * cross;
                cy += (p0.y + p1.y) * cross;
            }

            signedArea *= 0.5f;
            if (Mathf.Abs(signedArea) < 0.000001f)
            {
                var fallback = Vector2.zero;
                foreach (var p in polygon)
                {
                    fallback += p;
                }

                return fallback / polygon.Count;
            }

            return new Vector2(cx / (6f * signedArea), cy / (6f * signedArea));
        }

        private static void CreateLabel(Transform parent, string text)
        {
            var label = new GameObject("Label");
            label.name = "NameLabel";
            label.transform.SetParent(parent, false);
            // Stack labels vertically in camera-facing space so name and army never overlap.
            label.transform.localPosition = new Vector3(0f, 0.130f, 0f);

            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.060f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.98f, 0.98f, 0.94f);

            var army = new GameObject("ArmyLabel");
            army.transform.SetParent(parent, false);
            army.transform.localPosition = new Vector3(0f, 0.050f, 0f);
            var armyTm = army.AddComponent<TextMesh>();
            armyTm.text = "0";
            armyTm.characterSize = 0.085f;
            armyTm.fontSize = 64;
            armyTm.anchor = TextAnchor.UpperCenter;
            armyTm.alignment = TextAlignment.Center;
            armyTm.color = Color.white;
            armyTm.fontStyle = FontStyle.Bold;
        }

        private void UpdateLabelBillboards()
        {
            if (_camera == null)
            {
                return;
            }

            foreach (var node in _nodes.Values)
            {
                if (node == null)
                {
                    continue;
                }

                var labels = node.GetComponentsInChildren<TextMesh>(true);
                foreach (var text in labels)
                {
                    var tr = text.transform;
                    var directionToCamera = _camera.transform.position - tr.position;
                    if (directionToCamera.sqrMagnitude <= 0.0001f)
                    {
                        continue;
                    }

                    var targetRotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up) * TextFacingFlip;
                    tr.rotation = Quaternion.Slerp(tr.rotation, targetRotation, 18f * Time.deltaTime);
                }
            }
        }

        private void Select(TerritoryNode node)
        {
            if (!CanSelectTerritories())
            {
                return;
            }

            if (_selected == node)
            {
                return;
            }

            if (_selected != null)
            {
                _selected.SetSelected(false);
                _selected.ApplyColor(_selected.BaseColor);
            }

            _selected = node;
            _selected.SetSelected(true);
            _selected.ApplyColor(Color.white);
            Trace("Select", $"selected territory={_selected.TerritoryId}");
            UpdateSelectionOverlay();
            TerritorySelected?.Invoke(node);
        }

        private void ClearSelection()
        {
            if (_selected != null)
            {
                _selected.SetSelected(false);
                _selected.ApplyColor(_selected.BaseColor);
            }

            _selected = null;
            if (_selectionOverlay != null)
            {
                _selectionOverlay.Hide();
            }
        }

        private void UpdateSelectionOverlay()
        {
            if (_selectionOverlay == null)
            {
                return;
            }

            if (_selected == null || string.IsNullOrWhiteSpace(_selected.TerritoryId))
            {
                _selectionOverlay.Hide();
                return;
            }

            if (!_turntableMode &&
                _territoryShapePolygons.TryGetValue(_selected.TerritoryId, out var polygons)
                && polygons != null
                && polygons.Count > 0)
            {
                _selectionOverlay.Show(polygons);
                Trace("Overlay", $"show world polygons territory={_selected.TerritoryId} loops={polygons.Count}");
                if (_selectionOverlay.TryGetCurrentCenter(out var center))
                {
                    _selected.SetOverlayAnchorWorld(center);
                }
                return;
            }

            if (_turntableMode &&
                _territoryShapePolygonsLocal.TryGetValue(_selected.TerritoryId, out var localPolygons)
                && localPolygons != null
                && localPolygons.Count > 0)
            {
                var worldPolygons = ConvertLocalPolygonsToWorld(localPolygons);
                if (worldPolygons != null && worldPolygons.Count > 0)
                {
                    _selectionOverlay.Show(worldPolygons);
                    Trace("Overlay", $"show local->world polygons territory={_selected.TerritoryId} loops={worldPolygons.Count}");
                    if (_selectionOverlay.TryGetCurrentCenter(out var localCenter))
                    {
                        _selected.SetOverlayAnchorWorld(localCenter);
                    }
                    return;
                }
            }

            // Fallback when SVG polygon binding is unavailable: keep selection visible on resized maps.
            _selectionOverlay.ShowFallback(_selected.transform.position, _selected.OverlayRadius);
            Trace("Overlay", $"fallback territory={_selected.TerritoryId} pos={_selected.transform.position} radius={_selected.OverlayRadius:0.000}");
            if (_selectionOverlay.TryGetCurrentCenter(out var fallbackCenter))
            {
                _selected.SetOverlayAnchorWorld(fallbackCenter);
            }
        }

        private void Trace(string category, string message)
        {
            // Board trace logs intentionally disabled to reduce runtime spam.
        }

        private void TrackSelectionOverlayWhileTurntableRotates()
        {
            if (!_turntableMode || _selectionOverlay == null || _selected == null)
            {
                return;
            }

            if (!_selectionOverlay.TryGetCurrentCenter(out var center))
            {
                UpdateSelectionOverlay();
                return;
            }

            var selectedPos = _selected.transform.position;
            var dx = center.x - selectedPos.x;
            var dz = center.z - selectedPos.z;
            if ((dx * dx) + (dz * dz) > 0.0002f)
            {
                UpdateSelectionOverlay();
            }
        }

        private List<List<Vector2>> ConvertLocalPolygonsToWorld(List<List<Vector2>> localPolygons)
        {
            var boardRenderer = FindBoardBackRenderer();
            if (boardRenderer == null || localPolygons == null || localPolygons.Count == 0)
            {
                return null;
            }

            if (!TryGetBoardSurfaceFrame(boardRenderer, out var boardCenter, out var boardRight, out var boardForward, out _, true))
            {
                return null;
            }

            var worldPolygons = new List<List<Vector2>>(localPolygons.Count);
            for (var i = 0; i < localPolygons.Count; i++)
            {
                var localPolygon = localPolygons[i];
                if (localPolygon == null || localPolygon.Count < 3)
                {
                    continue;
                }

                var worldPolygon = new List<Vector2>(localPolygon.Count);
                for (var j = 0; j < localPolygon.Count; j++)
                {
                    var local = localPolygon[j];
                    var world = boardCenter + (boardRight * local.x) + (boardForward * local.y);
                    worldPolygon.Add(new Vector2(world.x, world.z));
                }

                if (worldPolygon.Count >= 3)
                {
                    worldPolygons.Add(worldPolygon);
                }
            }

            return worldPolygons;
        }

        private static bool TryGetBoardSurfaceFrame(
            Renderer boardRenderer,
            out Vector3 center,
            out Vector3 right,
            out Vector3 forward,
            out Vector3 normal,
            bool flattenToWorldUp = false)
        {
            center = Vector3.zero;
            right = Vector3.right;
            forward = Vector3.forward;
            normal = Vector3.up;

            if (boardRenderer == null)
            {
                return false;
            }

            center = boardRenderer.bounds.center;
            if (flattenToWorldUp)
            {
                normal = Vector3.up;
                var projectedRight = Vector3.ProjectOnPlane(boardRenderer.transform.right, normal);
                if (projectedRight.sqrMagnitude <= 0.0001f)
                {
                    projectedRight = Vector3.ProjectOnPlane(boardRenderer.transform.forward, normal);
                }

                if (projectedRight.sqrMagnitude <= 0.0001f)
                {
                    projectedRight = Vector3.right;
                }

                right = projectedRight.normalized;
                forward = Vector3.Cross(normal, right).normalized;
                if (forward.sqrMagnitude <= 0.0001f)
                {
                    forward = Vector3.forward;
                }

                return true;
            }

            normal = boardRenderer.transform.up.normalized;
            var candidateRight = boardRenderer.transform.right;
            if (candidateRight.sqrMagnitude <= 0.0001f)
            {
                candidateRight = Vector3.right;
            }

            right = candidateRight.normalized;
            forward = Vector3.Cross(normal, right).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            return true;
        }

        private void AlignAlwaysVisibleNamesToTerritoryShapes()
        {
            if (_territoryShapeCentroids == null || _territoryShapeCentroids.Count == 0)
            {
                return;
            }

            foreach (var kv in _territoryShapeCentroids)
            {
                if (!_nodes.TryGetValue(kv.Key, out var node) || node == null)
                {
                    continue;
                }

                var center = new Vector3(kv.Value.x, _territoryPlaneY, kv.Value.y);
                node.SetAlwaysNameAnchorWorld(center);
            }
        }

        private void AlignTerritoryNodesToShapeCentroids()
        {
            if (_territoryShapeCentroids == null || _territoryShapeCentroids.Count == 0)
            {
                return;
            }

            var moved = 0;
            foreach (var kv in _territoryShapeCentroids)
            {
                if (!_nodes.TryGetValue(kv.Key, out var node) || node == null)
                {
                    continue;
                }

                var t = node.transform;
                var target = new Vector3(kv.Value.x, _territoryPlaneY, kv.Value.y);
                if ((t.position - target).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                t.position = target;
                moved++;
            }

            if (moved > 0)
            {
                Debug.Log($"[Risiko3D][Board] Aligned {moved} territory nodes to SVG centroids.");
            }
        }

        private void ParseLocalization(string json)
        {
            _localizedNames.Clear();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var idSet = new HashSet<string>();
            foreach (var t in _map.territories)
            {
                idSet.Add(t.id);
            }

            var matches = Regex.Matches(json, "\"([a-z_]+)\"\\s*:\\s*\"([^\"]*)\"");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Groups.Count < 3)
                {
                    continue;
                }

                var key = m.Groups[1].Value;
                if (!idSet.Contains(key))
                {
                    continue;
                }

                _localizedNames[key] = m.Groups[2].Value;
            }
        }

        private string ReadProjectFile(string relativePath)
        {
            var root = Directory.GetParent(Application.dataPath);
            if (root == null)
            {
                return string.Empty;
            }

            var fullPath = Path.Combine(root.FullName, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        private static string ToDisplayName(string id)
        {
            var chunks = id.Split('_');
            for (var i = 0; i < chunks.Length; i++)
            {
                if (string.IsNullOrEmpty(chunks[i]))
                {
                    continue;
                }

                chunks[i] = char.ToUpperInvariant(chunks[i][0]) + chunks[i].Substring(1);
            }

            return string.Join(" ", chunks);
        }

        public bool AreAdjacent(string a, string b)
        {
            return _adjacency.TryGetValue(a, out var set) && set.Contains(b);
        }

        public bool TrySetTerritoryState(string id, int ownerIndex, Color ownerColor, int armies)
        {
            if (!_nodes.TryGetValue(id, out var node))
            {
                return false;
            }

            node.SetOwnerAndArmies(ownerIndex, ownerColor, armies);
            if (_selected == node)
            {
                node.ApplyColor(Color.white);
            }

            return true;
        }

        private void BuildAdjacency()
        {
            _adjacency.Clear();
            if (_map?.territories == null)
            {
                return;
            }

            foreach (var t in _map.territories)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.id))
                {
                    continue;
                }

                if (!_adjacency.ContainsKey(t.id))
                {
                    _adjacency[t.id] = new HashSet<string>();
                }

                if (t.neighbors == null)
                {
                    continue;
                }

                foreach (var n in t.neighbors)
                {
                    if (!string.IsNullOrWhiteSpace(n))
                    {
                        _adjacency[t.id].Add(n);
                    }
                }
            }
        }

        private void ParsePositionManifest(string json)
        {
            _territoryPositions.Clear();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            TerritoryPositionManifest manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<TerritoryPositionManifest>(json);
            }
            catch (ArgumentException)
            {
                // Recover from locale-exported decimals like 3,57 by rewriting number commas to dots.
                var normalized = Regex.Replace(json, @"(?<=\d),(?=\d)", ".");
                try
                {
                    manifest = JsonUtility.FromJson<TerritoryPositionManifest>(normalized);
                    Debug.LogWarning("[Risiko3D][Board] Position manifest had locale decimals, auto-normalized to JSON format.");
                }
                catch (ArgumentException ex2)
                {
                    Debug.LogError($"[Risiko3D][Board] Invalid territory positions manifest: {ex2.Message}");
                    return;
                }
            }

            if (manifest?.positions == null)
            {
                return;
            }

            foreach (var p in manifest.positions)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.territoryId))
                {
                    continue;
                }

                _territoryPositions[p.territoryId] = new Vector3(p.x, p.y, p.z);
            }
        }
    }
}
