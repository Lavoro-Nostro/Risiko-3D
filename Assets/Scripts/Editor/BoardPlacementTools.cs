#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
using UnityEditor;
using UnityEngine;

namespace Risiko3D.EditorTools
{
    public static class BoardPlacementTools
    {
        private const string RuntimeConfigPath = "Assets/Resources/Runtime/GameRuntimeConfig.asset";
        private const float BoardPaddingMultiplier = 1.03f;

        [MenuItem("Risiko3D/Board/Snap Board To Selected Table")]
        public static void SnapBoardToSelectedTable()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"[Risiko3D][BoardTools] Config not found at {RuntimeConfigPath}");
                return;
            }

            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Select a table object in Scene view first.");
                return;
            }

            var renderer = selection.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Selected object has no Renderer.");
                return;
            }

            var candidate = FindBestTableRenderer(selection.transform);
            if (candidate != null)
            {
                renderer = candidate;
            }

            var bounds = renderer.bounds;
            var boardY = bounds.max.y + 0.005f;
            cfg.BoardVisualPosition = new Vector3(bounds.center.x, boardY - 0.02f, bounds.center.z);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Risiko3D][BoardTools] Board snapped to '{selection.name}' at {cfg.BoardVisualPosition}.");
        }

        [MenuItem("Risiko3D/Board/Create Manual Board Visual From Config")]
        public static void CreateManualBoardVisualFromConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"[Risiko3D][BoardTools] Config not found at {RuntimeConfigPath}");
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<BoardVisualAnchor>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log($"[Risiko3D][BoardTools] Manual board anchor already exists: '{existing.gameObject.name}'.");
                return;
            }

            var sprite = LoadBoardSpriteFromConfig(cfg, out var loadedFrom);

            if (sprite == null)
            {
                Debug.LogError(
                    $"[Risiko3D][BoardTools] Could not load map visual from '{cfg.BoardMapSpriteResourcePath}'. " +
                    "Expected under Resources or as importable asset.");
                return;
            }

            var go = new GameObject("ManualBoardMapVisual");
            Undo.RegisterCreatedObjectUndo(go, "Create Manual Board Visual");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -10;
            sr.color = Color.white;

            go.transform.position = cfg.BoardVisualPosition;
            go.transform.rotation = Quaternion.Euler(cfg.BoardVisualRotation);
            var bounds = sprite.bounds.size;
            if (bounds.x > 0f && bounds.y > 0f)
            {
                go.transform.localScale = new Vector3(
                    cfg.BoardVisualWorldSize.x / bounds.x,
                    cfg.BoardVisualWorldSize.y / bounds.y,
                    1f);
            }

            go.AddComponent<BoardVisualAnchor>();
            Selection.activeGameObject = go;
            Debug.Log($"[Risiko3D][BoardTools] Manual board visual created from '{loadedFrom}'. Move/scale it, then bake.");
        }

        [MenuItem("Risiko3D/Board/Set Selected As Manual Board Anchor")]
        public static void SetSelectedAsManualBoardAnchor()
        {
            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Select a map visual object first.");
                return;
            }

            var renderer = selection.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = selection.GetComponentInChildren<SpriteRenderer>();
            }

            if (renderer == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Selected object has no SpriteRenderer.");
                return;
            }

            var existing = selection.GetComponent<BoardVisualAnchor>();
            if (existing == null)
            {
                existing = Undo.AddComponent<BoardVisualAnchor>(selection);
            }

            Selection.activeGameObject = selection;
            Debug.Log($"[Risiko3D][BoardTools] Manual board anchor set on '{selection.name}'.");
        }

        [MenuItem("Risiko3D/Board/Bake Selected Map Visual To Config")]
        public static void BakeSelectedMapVisualToConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"[Risiko3D][BoardTools] Config not found at {RuntimeConfigPath}");
                return;
            }

            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Select a map visual object first.");
                return;
            }

            var spriteRenderer = selection.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = selection.GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Selected map visual needs a SpriteRenderer with a sprite.");
                return;
            }

            var t = spriteRenderer.transform;
            var spriteSize = spriteRenderer.sprite.bounds.size;
            var worldWidth = Mathf.Abs(spriteSize.x * t.lossyScale.x);
            var worldHeight = Mathf.Abs(spriteSize.y * t.lossyScale.y);

            cfg.BoardVisualPosition = t.position;
            cfg.BoardVisualRotation = t.rotation.eulerAngles;
            cfg.BoardVisualWorldSize = new Vector2(worldWidth, worldHeight);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Risiko3D][BoardTools] Baked map visual '{selection.name}' to config: " +
                $"pos={cfg.BoardVisualPosition}, rot={cfg.BoardVisualRotation}, size={cfg.BoardVisualWorldSize}");
        }

        [MenuItem("Risiko3D/Board/Create Board Base Under Selected Map")]
        public static void CreateBoardBaseUnderSelectedMap()
        {
            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Select a map visual object first.");
                return;
            }

            var spriteRenderer = selection.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = selection.GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Selected map visual needs a SpriteRenderer.");
                return;
            }

            var mapTransform = spriteRenderer.transform;
            var mapBounds = spriteRenderer.bounds;
            var spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : Vector3.zero;
            var boardSizeX = Mathf.Abs(spriteSize.x * mapTransform.lossyScale.x) * BoardPaddingMultiplier;
            var boardSizeZ = Mathf.Abs(spriteSize.y * mapTransform.lossyScale.y) * BoardPaddingMultiplier;
            if (boardSizeX <= 0.0001f || boardSizeZ <= 0.0001f)
            {
                boardSizeX = Mathf.Max(0.1f, mapBounds.size.x * BoardPaddingMultiplier);
                boardSizeZ = Mathf.Max(0.1f, mapBounds.size.z * BoardPaddingMultiplier);
            }

            var table = FindBestSceneTableRenderer(mapTransform);
            if (table != null)
            {
                var maxX = Mathf.Max(0.2f, table.bounds.size.x * 0.94f);
                var maxZ = Mathf.Max(0.2f, table.bounds.size.z * 0.94f);
                var beforeX = boardSizeX;
                var beforeZ = boardSizeZ;
                boardSizeX = Mathf.Min(boardSizeX, maxX);
                boardSizeZ = Mathf.Min(boardSizeZ, maxZ);
                if (!Mathf.Approximately(beforeX, boardSizeX) || !Mathf.Approximately(beforeZ, boardSizeZ))
                {
                    Debug.Log($"[Risiko3D][BoardTools] Board size clamped to table footprint: ({boardSizeX:0.###}, {boardSizeZ:0.###}).");
                }
            }

            var boardThickness = Mathf.Clamp(Mathf.Min(boardSizeX, boardSizeZ) * 0.045f, 0.03f, 0.18f);
            var boardCenter = new Vector3(
                mapTransform.position.x,
                mapBounds.min.y - (boardThickness * 0.5f) - 0.0025f,
                mapTransform.position.z);

            var existing = mapTransform.parent != null
                ? mapTransform.parent.Find("GameBoardBase")
                : null;
            GameObject boardObject;
            if (existing != null)
            {
                boardObject = existing.gameObject;
            }
            else
            {
                boardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardObject.name = "GameBoardBase";
                Undo.RegisterCreatedObjectUndo(boardObject, "Create Board Base");
            }

            RemoveCollider(boardObject);

            boardObject.transform.SetParent(mapTransform.parent, true);
            boardObject.transform.position = boardCenter;
            boardObject.transform.rotation = Quaternion.Euler(0f, mapTransform.eulerAngles.y, 0f);
            boardObject.transform.localScale = new Vector3(boardSizeX, boardThickness, boardSizeZ);

            StyleBoardBase(boardObject, boardSizeX, boardThickness, boardSizeZ);

            Debug.Log($"[Risiko3D][BoardTools] Board base created/updated under '{selection.name}'.");
        }

        [MenuItem("Risiko3D/Board/Create/Update Legend Plaque On Board Base")]
        public static void CreateOrUpdateLegendPlaqueOnBoardBase()
        {
            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Select the map visual or GameBoardBase first.");
                return;
            }

            var mapRenderer = selection.GetComponent<SpriteRenderer>() ?? selection.GetComponentInChildren<SpriteRenderer>();
            var boardBase = selection.name == "GameBoardBase" ? selection : null;

            if (boardBase == null && selection.transform.parent != null && selection.transform.parent.name == "GameBoardBase")
            {
                boardBase = selection.transform.parent.gameObject;
            }

            if (boardBase == null)
            {
                boardBase = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Select(t => t.gameObject)
                    .FirstOrDefault(go => go.name == "GameBoardBase");
            }

            if (boardBase == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] GameBoardBase not found. Create board base first.");
                return;
            }

            if (mapRenderer == null)
            {
                var anchor = UnityEngine.Object.FindFirstObjectByType<BoardVisualAnchor>();
                if (anchor != null)
                {
                    mapRenderer = anchor.SpriteRenderer;
                }
            }

            if (mapRenderer == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Could not find a map SpriteRenderer.");
                return;
            }

            var t = boardBase.transform;
            var boardSizeX = Mathf.Abs(t.localScale.x);
            var boardThickness = Mathf.Abs(t.localScale.y);
            var boardSizeZ = Mathf.Abs(t.localScale.z);
            CreateOrUpdateLegendPlaque(boardBase, mapRenderer.transform, boardSizeX, boardThickness, boardSizeZ);
            Debug.Log("[Risiko3D][BoardTools] Legend plaque created/updated on board base.");
        }

        [MenuItem("Risiko3D/Board/Finalize Manual Board Setup (Selected Map)")]
        public static void FinalizeManualBoardSetup()
        {
            SetSelectedAsManualBoardAnchor();
            BakeSelectedMapVisualToConfig();
            CreateBoardBaseUnderSelectedMap();
            RebuildTerritoryPositionsForSelectedMap();
            Debug.Log("[Risiko3D][BoardTools] Finalized manual board setup (anchor + bake + board base + territory remap).");
        }

        [MenuItem("Risiko3D/Board/Rebuild Territory Positions To Selected Map")]
        public static void RebuildTerritoryPositionsForSelectedMap()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"[Risiko3D][BoardTools] Config not found at {RuntimeConfigPath}");
                return;
            }

            var spriteRenderer = ResolveSelectedSpriteRenderer();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogError("[Risiko3D][BoardTools] Select a map visual object with SpriteRenderer first.");
                return;
            }

            var oldPosition = cfg.BoardVisualPosition;
            var oldRotation = cfg.BoardVisualRotation;
            var oldSize = cfg.BoardVisualWorldSize;

            var t = spriteRenderer.transform;
            var spriteSize = spriteRenderer.sprite.bounds.size;
            var newWidth = Mathf.Abs(spriteSize.x * t.lossyScale.x);
            var newHeight = Mathf.Abs(spriteSize.y * t.lossyScale.y);
            var newPosition = t.position;
            var newRotation = t.rotation.eulerAngles;

            if (oldSize.x <= 0.0001f || oldSize.y <= 0.0001f || newWidth <= 0.0001f || newHeight <= 0.0001f)
            {
                Debug.LogError("[Risiko3D][BoardTools] Invalid old/new board sizes for territory remap.");
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                Debug.LogError("[Risiko3D][BoardTools] Could not resolve project root.");
                return;
            }

            var positionsPath = cfg.TerritoryPositionsPath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(projectRoot, positionsPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[Risiko3D][BoardTools] Territory positions file not found: {fullPath}");
                return;
            }

            var json = File.ReadAllText(fullPath);
            var manifest = JsonUtility.FromJson<TerritoryPositionManifest>(json);
            if (manifest == null || manifest.positions == null || manifest.positions.Length == 0)
            {
                Debug.LogError($"[Risiko3D][BoardTools] Invalid territory positions json: {cfg.TerritoryPositionsPath}");
                return;
            }

            var oldMatrix = Matrix4x4.TRS(oldPosition, Quaternion.Euler(oldRotation), Vector3.one);
            var oldInverse = oldMatrix.inverse;
            var newMatrix = Matrix4x4.TRS(newPosition, Quaternion.Euler(newRotation), Vector3.one);
            var oldHalfW = Mathf.Max(0.0001f, oldSize.x * 0.5f);
            var oldHalfH = Mathf.Max(0.0001f, oldSize.y * 0.5f);
            var newHalfW = newWidth * 0.5f;
            var newHalfH = newHeight * 0.5f;

            for (var i = 0; i < manifest.positions.Length; i++)
            {
                var entry = manifest.positions[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.territoryId))
                {
                    continue;
                }

                var oldWorld = new Vector3(entry.x, entry.y, entry.z);
                var oldLocal = oldInverse.MultiplyPoint3x4(oldWorld);
                var normalizedX = oldLocal.x / oldHalfW;
                var normalizedY = oldLocal.y / oldHalfH;
                var newLocal = new Vector3(normalizedX * newHalfW, normalizedY * newHalfH, 0f);
                var newWorld = newMatrix.MultiplyPoint3x4(newLocal);
                entry.x = newWorld.x;
                entry.y = newWorld.y;
                entry.z = newWorld.z;
            }

            File.WriteAllText(fullPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.Refresh();

            cfg.BoardVisualPosition = newPosition;
            cfg.BoardVisualRotation = newRotation;
            cfg.BoardVisualWorldSize = new Vector2(newWidth, newHeight);
            cfg.TerritoryPositionOffset = Vector3.zero;
            cfg.TerritoryPositionScale = 1f;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Risiko3D][BoardTools] Rebuilt territory positions for selected map. " +
                $"oldSize={oldSize} -> newSize=({newWidth:0.###},{newHeight:0.###}), file={cfg.TerritoryPositionsPath}");
        }

        private static Renderer FindBestTableRenderer(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Renderer best = null;
            var bestScore = 0f;
            foreach (var renderer in renderers)
            {
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

        private static Renderer FindBestSceneTableRenderer(Transform ignoreRoot)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Renderer best = null;
            var bestScore = 0f;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.transform == null)
                {
                    continue;
                }

                if (ignoreRoot != null && renderer.transform.IsChildOf(ignoreRoot))
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
            if (parentName.IndexOf("furniture", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var bounds = renderer.bounds;
            var footprint = bounds.size.x * bounds.size.z;
            var thickness = bounds.size.y;
            return bounds.center.y > 0.35f &&
                   footprint >= 0.40f &&
                   thickness <= Mathf.Max(0.30f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.35f);
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

        private static Sprite LoadBoardSpriteFromConfig(GameRuntimeConfig cfg, out string loadedFrom)
        {
            loadedFrom = string.Empty;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.BoardMapSpriteResourcePath))
            {
                return null;
            }

            var sprite = Resources.Load<Sprite>(cfg.BoardMapSpriteResourcePath);
            if (sprite != null)
            {
                loadedFrom = $"Resources/{cfg.BoardMapSpriteResourcePath}";
                return sprite;
            }

            var texture = Resources.Load<Texture2D>(cfg.BoardMapSpriteResourcePath);
            if (texture != null)
            {
                loadedFrom = $"Resources/{cfg.BoardMapSpriteResourcePath}";
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }

            var fileName = Path.GetFileName(cfg.BoardMapSpriteResourcePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var guids = AssetDatabase.FindAssets($"{fileName} t:Sprite");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null)
                {
                    loadedFrom = path;
                    return s;
                }
            }

            guids = AssetDatabase.FindAssets($"{fileName} t:Texture2D");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t != null)
                {
                    loadedFrom = path;
                    return Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            var fallbackPng = "Assets/GameData/Map/world_classic_map.png";
            var fallbackTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPng);
            if (fallbackTexture != null)
            {
                loadedFrom = fallbackPng;
                return Sprite.Create(
                    fallbackTexture,
                    new Rect(0f, 0f, fallbackTexture.width, fallbackTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            var absoluteFallback = Path.Combine(Directory.GetCurrentDirectory(), fallbackPng);
            if (File.Exists(absoluteFallback))
            {
                var bytes = File.ReadAllBytes(absoluteFallback);
                var runtimeTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (runtimeTexture.LoadImage(bytes, false))
                {
                    loadedFrom = absoluteFallback;
                    return Sprite.Create(
                        runtimeTexture,
                        new Rect(0f, 0f, runtimeTexture.width, runtimeTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            return null;
        }

        private static SpriteRenderer ResolveSelectedSpriteRenderer()
        {
            var selection = Selection.activeGameObject;
            if (selection == null)
            {
                return null;
            }

            var spriteRenderer = selection.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = selection.GetComponentInChildren<SpriteRenderer>();
            }

            return spriteRenderer;
        }

        private static void StyleBoardBase(GameObject boardObject, float sizeX, float thickness, float sizeZ)
        {
            if (boardObject == null)
            {
                return;
            }

            var baseRenderer = boardObject.GetComponent<Renderer>();
            if (baseRenderer != null)
            {
                baseRenderer.sharedMaterial = CreateLitMaterial(new Color(0.40f, 0.30f, 0.20f, 1f));
            }

            var surface = EnsureChildCube(boardObject.transform, "BoardTopSurface");
            surface.transform.localScale = new Vector3(sizeX * 0.985f, Mathf.Max(0.01f, thickness * 0.25f), sizeZ * 0.985f);
            surface.transform.localPosition = new Vector3(0f, (thickness * 0.38f), 0f);
            var surfaceRenderer = surface.GetComponent<Renderer>();
            if (surfaceRenderer != null)
            {
                surfaceRenderer.sharedMaterial = CreateLitMaterial(new Color(0.14f, 0.14f, 0.14f, 1f));
            }

            var frameThickness = Mathf.Clamp(Mathf.Min(sizeX, sizeZ) * 0.02f, 0.03f, 0.16f);
            var frameHeight = Mathf.Max(0.02f, thickness * 0.95f);
            var halfX = (sizeX * 0.5f) - (frameThickness * 0.5f);
            var halfZ = (sizeZ * 0.5f) - (frameThickness * 0.5f);

            var north = EnsureChildCube(boardObject.transform, "BoardFrame_North");
            north.transform.localScale = new Vector3(sizeX, frameHeight, frameThickness);
            north.transform.localPosition = new Vector3(0f, thickness * 0.08f, halfZ);

            var south = EnsureChildCube(boardObject.transform, "BoardFrame_South");
            south.transform.localScale = new Vector3(sizeX, frameHeight, frameThickness);
            south.transform.localPosition = new Vector3(0f, thickness * 0.08f, -halfZ);

            var west = EnsureChildCube(boardObject.transform, "BoardFrame_West");
            west.transform.localScale = new Vector3(frameThickness, frameHeight, sizeZ);
            west.transform.localPosition = new Vector3(-halfX, thickness * 0.08f, 0f);

            var east = EnsureChildCube(boardObject.transform, "BoardFrame_East");
            east.transform.localScale = new Vector3(frameThickness, frameHeight, sizeZ);
            east.transform.localPosition = new Vector3(halfX, thickness * 0.08f, 0f);

            var woodMaterial = CreateLitMaterial(new Color(0.22f, 0.14f, 0.09f, 1f));
            ApplyMaterialToCube(north, woodMaterial);
            ApplyMaterialToCube(south, woodMaterial);
            ApplyMaterialToCube(west, woodMaterial);
            ApplyMaterialToCube(east, woodMaterial);
        }

        private static void CreateOrUpdateLegendPlaque(GameObject boardObject, Transform mapTransform, float boardSizeX, float boardThickness, float boardSizeZ)
        {
            if (boardObject == null || mapTransform == null)
            {
                return;
            }

            var cfg = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (cfg == null)
            {
                return;
            }

            var plaque = EnsureChildCube(boardObject.transform, "BoardLegendPlaque");
            var plaqueSizeX = Mathf.Clamp(boardSizeX * 0.34f, 1.8f, 5.8f);
            var plaqueSizeZ = Mathf.Clamp(boardSizeZ * 0.20f, 1.2f, 3.0f);
            var plaqueThickness = Mathf.Clamp(boardThickness * 0.35f, 0.01f, 0.05f);
            var marginX = Mathf.Max(0.12f, boardSizeX * 0.04f);
            var marginZ = Mathf.Max(0.12f, boardSizeZ * 0.04f);
            plaque.transform.localScale = new Vector3(plaqueSizeX, plaqueThickness, plaqueSizeZ);
            plaque.transform.localPosition = new Vector3(
                (-boardSizeX * 0.5f) + (plaqueSizeX * 0.5f) + marginX,
                (boardThickness * 0.5f) + (plaqueThickness * 0.5f) + 0.002f,
                (-boardSizeZ * 0.5f) + (plaqueSizeZ * 0.5f) + marginZ);
            ApplyMaterialToCube(plaque, CreateLitMaterial(new Color(0.06f, 0.08f, 0.11f, 1f)));

            var textRoot = EnsureChildObject(plaque.transform, "LegendTextRoot");
            textRoot.transform.localPosition = new Vector3((-plaqueSizeX * 0.5f) + 0.08f, (plaqueThickness * 0.5f) + 0.001f, (plaqueSizeZ * 0.5f) - 0.06f);
            textRoot.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            textRoot.transform.localScale = Vector3.one;

            var lines = BuildLegendLines(cfg);
            var lineHeight = Mathf.Clamp(plaqueSizeZ * 0.085f, 0.06f, 0.14f);
            var textScale = Mathf.Clamp(plaqueSizeX * 0.0045f, 0.005f, 0.012f);
            for (var i = 0; i < lines.Count; i++)
            {
                var lineObj = EnsureChildObject(textRoot.transform, $"Line_{i:00}");
                lineObj.transform.localPosition = new Vector3(0f, -i * lineHeight, 0f);
                lineObj.transform.localRotation = Quaternion.identity;
                var text = lineObj.GetComponent<TextMesh>();
                if (text == null)
                {
                    text = Undo.AddComponent<TextMesh>(lineObj);
                }

                text.text = lines[i];
                text.fontSize = 64;
                text.characterSize = textScale;
                text.anchor = TextAnchor.UpperLeft;
                text.alignment = TextAlignment.Left;
                text.color = i == 0 ? new Color(1f, 0.93f, 0.75f, 1f) : new Color(0.92f, 0.95f, 0.98f, 1f);
                text.fontStyle = i == 0 ? FontStyle.Bold : FontStyle.Normal;
            }

            CleanupUnusedLegendLines(textRoot.transform, lines.Count);
        }

        private static List<string> BuildLegendLines(GameRuntimeConfig cfg)
        {
            var lines = new List<string> { "REINFORCEMENTS" };
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.MapJsonPath))
            {
                lines.Add("Load map.json failed.");
                return lines;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                lines.Add("Project root missing.");
                return lines;
            }

            var mapPath = Path.Combine(projectRoot, cfg.MapJsonPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(mapPath))
            {
                lines.Add("Map json not found.");
                return lines;
            }

            var json = File.ReadAllText(mapPath);
            var map = JsonUtility.FromJson<MapData>(json);
            if (map?.continents == null || map.continents.Length == 0)
            {
                lines.Add("No continent data.");
                return lines;
            }

            foreach (var continent in map.continents.OrderBy(c => c.id))
            {
                if (continent == null || string.IsNullOrWhiteSpace(continent.id))
                {
                    continue;
                }

                lines.Add($"{ToDisplayName(continent.id)}  +{continent.bonus}");
            }

            return lines;
        }

        private static void CleanupUnusedLegendLines(Transform textRoot, int keepCount)
        {
            if (textRoot == null)
            {
                return;
            }

            var toDelete = new List<GameObject>();
            for (var i = 0; i < textRoot.childCount; i++)
            {
                var child = textRoot.GetChild(i);
                if (!child.name.StartsWith("Line_", StringComparison.Ordinal))
                {
                    continue;
                }

                var suffix = child.name.Substring("Line_".Length);
                if (!int.TryParse(suffix, out var index) || index < keepCount)
                {
                    continue;
                }

                toDelete.Add(child.gameObject);
            }

            foreach (var go in toDelete)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static GameObject EnsureChildCube(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            GameObject go;
            if (child != null)
            {
                go = child.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = childName;
                Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
                go.transform.SetParent(parent, false);
            }

            RemoveCollider(go);
            return go;
        }

        private static GameObject EnsureChildObject(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child.gameObject;
            }

            var go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Material CreateLitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private static void ApplyMaterialToCube(GameObject cube, Material material)
        {
            if (cube == null || material == null)
            {
                return;
            }

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
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
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static string ToDisplayName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Unknown";
            }

            var parts = id.Split('_');
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    continue;
                }

                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return string.Join(" ", parts);
        }
    }
}
#endif
