using System;
using System.Collections.Generic;
using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Match
{
    public sealed class BoardPhysicalCardsToolkit : MonoBehaviour
    {
        private sealed class CardVisual
        {
            public Transform Root;
            public Renderer FrontRenderer;
            public Renderer BackRenderer;
        }

        private sealed class PlayerSeatVisual
        {
            public int PlayerIndex;
            public string PlayerId;
            public string DisplayName;
            public Color Color;
            public Transform Root;
            public CardVisual ObjectiveCard;
            public TextMesh ObjectiveLabel;
            public CardVisual HandCard;
            public TextMesh HandLabel;
            public TextMesh NameLabel;
            public string LastObjectiveId = string.Empty;
            public string LastHandTopCardId = string.Empty;
            public int LastHandCount = -1;
        }

        private static readonly Vector2[] SeatAnchorsNormalized =
        {
            new Vector2(0.50f, 0.10f),
            new Vector2(0.86f, 0.28f),
            new Vector2(0.86f, 0.72f),
            new Vector2(0.50f, 0.90f),
            new Vector2(0.14f, 0.72f),
            new Vector2(0.14f, 0.28f)
        };

        private readonly List<PlayerSeatVisual> _seats = new();
        private readonly Dictionary<string, Sprite> _objectiveSprites = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _territorySprites = new(StringComparer.Ordinal);
        private readonly Dictionary<int, BoardPlayerSeatAnchor> _sceneSeatAnchorsByIndex = new();
        private readonly Dictionary<Sprite, Material> _spriteMaterialCache = new();

        private GameRuntimeConfig _config;
        private HostAuthoritativeMatchLoop _loop;
        private Transform _root;
        private Renderer _boardRenderer;
        private Sprite _objectiveBackSprite;
        private Sprite _territoryBackSprite;
        private Material _fallbackObjectiveFront;
        private Material _fallbackObjectiveBack;
        private Material _fallbackTerritoryFront;
        private Material _fallbackTerritoryBack;
        private double _nextRefreshAt;

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
        }

        private void Start()
        {
            if (_config != null && !_config.EnableBoardPhysicalCards)
            {
                enabled = false;
                return;
            }

            _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            Build();
            Refresh();
        }

        private void Update()
        {
            if (_loop == null)
            {
                _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            }

            if (_loop == null || _root == null)
            {
                return;
            }

            var now = Time.unscaledTimeAsDouble;
            if (now < _nextRefreshAt)
            {
                return;
            }

            _nextRefreshAt = now + 0.15d;
            Refresh();
        }

        private void Build()
        {
            if (_root != null)
            {
                return;
            }

            _boardRenderer = FindBoardBackRenderer();
            if (_boardRenderer == null)
            {
                enabled = false;
                return;
            }

            LoadCardSprites();
            BuildFallbackMaterials();
            CacheSceneSeatAnchors();

            var rootGo = new GameObject("BoardPhysicalCards");
            _root = rootGo.transform;
            _root.SetParent(transform, false);
            _root.localScale = Vector3.one * Mathf.Max(0.1f, _config != null ? _config.PhysicalCardScale : 1f);

            var players = _loop.GetPlayerVisualData();
            _seats.Clear();
            for (var i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var seat = BuildSeat(p.Index, p.PlayerId, p.DisplayName, p.Color);
                _seats.Add(seat);
            }
        }

        private void Refresh()
        {
            if (_boardRenderer == null)
            {
                _boardRenderer = FindBoardBackRenderer();
                if (_boardRenderer == null)
                {
                    return;
                }
            }

            var bounds = _boardRenderer.bounds;
            var baseY = bounds.max.y + (_config != null ? _config.PhysicalCardLift : 0.014f);
            var activePlayerIndex = _loop.ActivePlayerIndex;

            for (var i = 0; i < _seats.Count; i++)
            {
                var seat = _seats[i];
                if (_sceneSeatAnchorsByIndex.TryGetValue(seat.PlayerIndex, out var sceneAnchor) && sceneAnchor != null)
                {
                    var cardsAnchor = sceneAnchor.ResolveCardsAnchor();
                    if (cardsAnchor != null)
                    {
                        seat.Root.position = cardsAnchor.position;
                        seat.Root.rotation = cardsAnchor.rotation;
                    }
                }
                else
                {
                    var anchor = SeatAnchorsNormalized[Mathf.Clamp(i, 0, SeatAnchorsNormalized.Length - 1)];
                    seat.Root.position = new Vector3(
                        Mathf.Lerp(bounds.min.x, bounds.max.x, anchor.x),
                        baseY,
                        Mathf.Lerp(bounds.min.z, bounds.max.z, anchor.y));
                    seat.Root.rotation = Quaternion.identity;
                }

                if (seat.Root.position.y < baseY)
                {
                    var p = seat.Root.position;
                    seat.Root.position = new Vector3(p.x, baseY, p.z);
                }

                var revealFront = ShouldRevealPlayerCards(seat, activePlayerIndex);
                UpdateObjectiveCard(seat, revealFront);
                UpdateHandCardStack(seat, revealFront);
            }
        }

        private PlayerSeatVisual BuildSeat(int playerIndex, string playerId, string displayName, Color color)
        {
            var seatGo = new GameObject($"PlayerSeat_{playerId}");
            var seatRoot = seatGo.transform;
            seatRoot.SetParent(_root, false);

            var nameTm = CreateLabel("PlayerName", seatRoot, new Vector3(-0.66f, 0.002f, 1.20f), 54, 0.022f, FontStyle.Bold, new Color(color.r, color.g, color.b, 1f));
            nameTm.text = ToFriendlyPlayerName(displayName);

            var objective = CreateTwoSidedCardVisual(
                "ObjectiveCard",
                seatRoot,
                Vector3.zero,
                _config != null ? _config.PhysicalObjectiveCardSize : new Vector2(1.45f, 2.05f),
                _config != null ? _config.PhysicalObjectiveCardPrefab : null);
            var objectiveLabel = CreateLabel("ObjectiveLabel", seatRoot, new Vector3(-0.66f, 0.002f, -1.16f), 50, 0.016f, FontStyle.Bold, new Color(0.90f, 0.96f, 1f, 1f));
            objectiveLabel.text = "Objective";

            var hand = CreateTwoSidedCardVisual(
                "TerritoryHandTop",
                seatRoot,
                new Vector3(1.10f, 0f, 0.34f),
                _config != null ? _config.PhysicalTerritoryCardSize : new Vector2(1.10f, 1.55f),
                _config != null ? _config.PhysicalTerritoryCardPrefab : null);
            var handLabel = CreateLabel("HandLabel", seatRoot, new Vector3(0.50f, 0.002f, -0.92f), 50, 0.016f, FontStyle.Bold, new Color(0.90f, 0.96f, 1f, 1f));
            handLabel.text = "Territory Cards: 0";

            var seat = new PlayerSeatVisual
            {
                PlayerIndex = playerIndex,
                PlayerId = playerId,
                DisplayName = displayName,
                Color = color,
                Root = seatRoot,
                ObjectiveCard = objective,
                ObjectiveLabel = objectiveLabel,
                HandCard = hand,
                HandLabel = handLabel,
                NameLabel = nameTm
            };

            SetCardFace(seat.ObjectiveCard, false, null, _objectiveBackSprite, _fallbackObjectiveFront, _fallbackObjectiveBack);
            SetCardFace(seat.HandCard, false, null, _territoryBackSprite, _fallbackTerritoryFront, _fallbackTerritoryBack);
            return seat;
        }

        private CardVisual CreateTwoSidedCardVisual(string name, Transform parent, Vector3 localPosition, Vector2 size, GameObject prefab)
        {
            if (prefab != null)
            {
                var instance = Instantiate(prefab, parent);
                instance.name = name;
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = new Vector3(size.x, 1f, size.y);
                return BuildCardVisualFromRenderers(instance.transform, size);
            }

            return CreateDefaultTwoSidedCard(name, parent, localPosition, size);
        }

        private CardVisual CreateDefaultTwoSidedCard(string name, Transform parent, Vector3 localPosition, Vector2 size)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.identity;

            var halfThickness = Mathf.Max(0.0002f, (_config != null ? _config.PhysicalCardThickness : 0.002f) * 0.5f);
            var front = GameObject.CreatePrimitive(PrimitiveType.Quad);
            front.name = "FrontFace";
            front.transform.SetParent(root, false);
            front.transform.localPosition = new Vector3(0f, halfThickness, 0f);
            front.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            front.transform.localScale = new Vector3(size.x, size.y, 1f);
            RemoveCollider(front);

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "BackFace";
            back.transform.SetParent(root, false);
            back.transform.localPosition = new Vector3(0f, -halfThickness, 0f);
            back.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            back.transform.localScale = new Vector3(size.x, size.y, 1f);
            RemoveCollider(back);

            return new CardVisual
            {
                Root = root,
                FrontRenderer = front.GetComponent<Renderer>(),
                BackRenderer = back.GetComponent<Renderer>()
            };
        }

        private static void RemoveCollider(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
        }

        private CardVisual BuildCardVisualFromRenderers(Transform root, Vector2 fallbackSize)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Renderer front = null;
            Renderer back = null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                RemoveCollider(r.gameObject);
                var n = r.gameObject.name.ToLowerInvariant();
                if (front == null && n.Contains("front"))
                {
                    front = r;
                    continue;
                }

                if (back == null && n.Contains("back"))
                {
                    back = r;
                }
            }

            if (front == null && renderers.Length > 0)
            {
                front = renderers[0];
            }

            if (back == null && renderers.Length > 1)
            {
                back = renderers[1];
            }

            if (front == null || back == null)
            {
                var parent = root.parent;
                var localPos = root.localPosition;
                var name = root.name;
                Destroy(root.gameObject);
                return CreateDefaultTwoSidedCard(name, parent, localPos, fallbackSize);
            }

            return new CardVisual
            {
                Root = root,
                FrontRenderer = front,
                BackRenderer = back
            };
        }

        private static TextMesh CreateLabel(string name, Transform parent, Vector3 localPosition, int fontSize, float charSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = string.Empty;
            tm.anchor = TextAnchor.MiddleLeft;
            tm.alignment = TextAlignment.Left;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.fontStyle = style;
            tm.color = color;
            return tm;
        }

        private void UpdateObjectiveCard(PlayerSeatVisual seat, bool revealFront)
        {
            var objectiveId = _loop.GetPlayerObjectiveCardBaseId(seat.PlayerIndex);
            if (string.Equals(seat.LastObjectiveId, objectiveId, StringComparison.Ordinal))
            {
                // Continue to apply reveal state even if card id did not change.
            }

            seat.LastObjectiveId = objectiveId;
            if (revealFront)
            {
                var sprite = ResolveObjectiveSprite(objectiveId);
                SetCardFace(seat.ObjectiveCard, true, sprite, _objectiveBackSprite, _fallbackObjectiveFront, _fallbackObjectiveBack);

                seat.ObjectiveLabel.text = $"Objective: {ToFriendlyObjectiveName(objectiveId)}";
            }
            else
            {
                SetCardFace(seat.ObjectiveCard, false, null, _objectiveBackSprite, _fallbackObjectiveFront, _fallbackObjectiveBack);

                seat.ObjectiveLabel.text = "Objective: Hidden";
            }
        }

        private void UpdateHandCardStack(PlayerSeatVisual seat, bool revealFront)
        {
            var handIds = _loop.GetPlayerHandCardIds(seat.PlayerIndex);
            var handCount = handIds != null ? handIds.Count : 0;
            if (handCount <= 0)
            {
                var setupCards = _loop.GetPlayerAssignedTerritoryCardIds(seat.PlayerIndex);
                handCount = setupCards != null ? setupCards.Count : 0;
            }

            var topId = handCount > 0 && handIds != null && handIds.Count > 0 ? handIds[handIds.Count - 1] : string.Empty;
            if (seat.HandCard?.Root != null)
            {
                seat.HandCard.Root.gameObject.SetActive(handCount > 0);
            }
            seat.HandLabel.text = $"Territory Cards: {handCount}";

            if (handCount <= 0)
            {
                seat.LastHandCount = 0;
                seat.LastHandTopCardId = string.Empty;
                return;
            }

            if (revealFront && !string.IsNullOrWhiteSpace(topId))
            {
                var topSprite = ResolveTerritorySprite(topId);
                SetCardFace(seat.HandCard, true, topSprite, _territoryBackSprite, _fallbackTerritoryFront, _fallbackTerritoryBack);
            }
            else
            {
                SetCardFace(seat.HandCard, false, null, _territoryBackSprite, _fallbackTerritoryFront, _fallbackTerritoryBack);
            }

            seat.LastHandCount = handCount;
            seat.LastHandTopCardId = topId;
        }

        private bool ShouldRevealPlayerCards(PlayerSeatVisual seat, int activePlayerIndex)
        {
            if (_config != null && !string.IsNullOrWhiteSpace(_config.LocalPerspectivePlayerId))
            {
                return string.Equals(_config.LocalPerspectivePlayerId, seat.PlayerId, StringComparison.OrdinalIgnoreCase);
            }

            if (_loop != null && !string.IsNullOrWhiteSpace(_loop.LocalPlayerId))
            {
                return string.Equals(_loop.LocalPlayerId, seat.PlayerId, StringComparison.OrdinalIgnoreCase);
            }

            return seat.PlayerIndex == activePlayerIndex;
        }

        private void SetCardFace(CardVisual card, bool showFront, Sprite frontSprite, Sprite backSprite, Material fallbackFront, Material fallbackBack)
        {
            if (card == null)
            {
                return;
            }

            if (card.FrontRenderer != null)
            {
                card.FrontRenderer.enabled = showFront;
                if (showFront)
                {
                    if (frontSprite != null)
                    {
                        ApplySpriteToRenderer(card.FrontRenderer, frontSprite);
                    }
                    else
                    {
                        ApplyCardMaterial(card.FrontRenderer, fallbackFront);
                    }
                }
            }

            if (card.BackRenderer != null)
            {
                card.BackRenderer.enabled = !showFront;
                if (!showFront)
                {
                    if (backSprite != null)
                    {
                        ApplySpriteToRenderer(card.BackRenderer, backSprite);
                    }
                    else
                    {
                        ApplyCardMaterial(card.BackRenderer, fallbackBack);
                    }
                }
            }
        }

        private void ApplySpriteToRenderer(Renderer renderer, Sprite sprite)
        {
            if (renderer == null || sprite == null)
            {
                return;
            }

            if (!_spriteMaterialCache.TryGetValue(sprite, out var mat) || mat == null)
            {
                mat = CreateSpriteMaterial(sprite);
                _spriteMaterialCache[sprite] = mat;
            }

            renderer.material = mat;
        }

        private static Material CreateSpriteMaterial(Sprite sprite)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            var mat = new Material(shader);
            var tex = sprite.texture;
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
            }
            else if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
            }

            var rect = sprite.rect;
            var invW = 1f / tex.width;
            var invH = 1f / tex.height;
            mat.mainTextureScale = new Vector2(rect.width * invW, rect.height * invH);
            mat.mainTextureOffset = new Vector2(rect.x * invW, rect.y * invH);
            mat.color = Color.white;
            return mat;
        }

        private static void ApplyCardMaterial(Renderer renderer, Material source)
        {
            if (renderer == null || source == null)
            {
                return;
            }

            renderer.material = source;
        }

        private void LoadCardSprites()
        {
            _objectiveSprites.Clear();
            _territorySprites.Clear();

            var objectiveSprites = Resources.LoadAll<Sprite>("Cards/Objective");
            for (var i = 0; i < objectiveSprites.Length; i++)
            {
                var s = objectiveSprites[i];
                if (s == null)
                {
                    continue;
                }

                var key = NormalizeCardId(s.name).Replace("_", "-");
                if (!_objectiveSprites.ContainsKey(key))
                {
                    _objectiveSprites[key] = s;
                }
            }

            var territorySprites = Resources.LoadAll<Sprite>("Cards/Territory");
            for (var i = 0; i < territorySprites.Length; i++)
            {
                var s = territorySprites[i];
                if (s == null)
                {
                    continue;
                }

                var key = NormalizeCardId(s.name);
                if (!_territorySprites.ContainsKey(key))
                {
                    _territorySprites[key] = s;
                }
            }

            _objectiveBackSprite = Resources.Load<Sprite>("Cards/Backs/objective_back");
            _territoryBackSprite = Resources.Load<Sprite>("Cards/Backs/territory_back");
        }

        private void BuildFallbackMaterials()
        {
            _fallbackObjectiveFront = CreateLitCardMaterial(new Color(0.30f, 0.22f, 0.08f, 1f));
            _fallbackObjectiveBack = CreateLitCardMaterial(new Color(0.10f, 0.12f, 0.18f, 1f));
            _fallbackTerritoryFront = CreateLitCardMaterial(new Color(0.13f, 0.19f, 0.11f, 1f));
            _fallbackTerritoryBack = CreateLitCardMaterial(new Color(0.11f, 0.11f, 0.11f, 1f));
        }

        private static Material CreateLitCardMaterial(Color color)
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

        private Sprite ResolveObjectiveSprite(string objectiveBaseId)
        {
            if (string.IsNullOrWhiteSpace(objectiveBaseId))
            {
                return null;
            }

            var key = NormalizeCardId(objectiveBaseId).Replace("_", "-");
            return _objectiveSprites.TryGetValue(key, out var sprite) ? sprite : null;
        }

        private Sprite ResolveTerritorySprite(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            if (cardId.StartsWith("joker", StringComparison.OrdinalIgnoreCase))
            {
                return Resources.Load<Sprite>("Cards/Joker/joker");
            }

            var key = NormalizeCardId(cardId);
            return _territorySprites.TryGetValue(key, out var sprite) ? sprite : null;
        }

        private static Renderer FindBoardBackRenderer()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r != null && r.gameObject != null && r.gameObject.name == "Board_Back")
                {
                    return r;
                }
            }

            return null;
        }

        private void CacheSceneSeatAnchors()
        {
            _sceneSeatAnchorsByIndex.Clear();
            var anchors = UnityEngine.Object.FindObjectsByType<BoardPlayerSeatAnchor>(FindObjectsSortMode.None);
            if (anchors == null || anchors.Length == 0)
            {
                return;
            }

            for (var i = 0; i < anchors.Length; i++)
            {
                var a = anchors[i];
                if (a == null)
                {
                    continue;
                }

                var idx = Mathf.Clamp(a.SeatNumber - 1, 0, 5);
                if (!_sceneSeatAnchorsByIndex.ContainsKey(idx))
                {
                    _sceneSeatAnchorsByIndex[idx] = a;
                }
            }
        }

        private static string NormalizeCardId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
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

        private static string ToFriendlyObjectiveName(string objectiveBaseId)
        {
            if (string.IsNullOrWhiteSpace(objectiveBaseId))
            {
                return "Unknown";
            }

            return objectiveBaseId
                .Replace("obj-", string.Empty)
                .Replace("-", " ");
        }
    }
}
