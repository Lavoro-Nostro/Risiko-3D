using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Risiko3D.Runtime.Board
{
    // Runtime legend renderer. Uses Unity UI fallback to guarantee visibility in Game view.
    public sealed class BoardLegendUiToolkit : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float PanelPadding = 12f;

        private Canvas _canvas;
        private RectTransform _panel;
        private Font _font;

        public void Build(MapData map, IReadOnlyDictionary<string, Color> continentColors)
        {
            if (map?.continents == null || map.continents.Length == 0)
            {
                return;
            }

            EnsureCanvas();
            if (_panel == null)
            {
                return;
            }

            ClearChildren(_panel);

            var panelImage = _panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.06f, 0.07f, 0.11f, 0.90f);
            }

            EnsurePanelDecor(_panel);

            var title = CreateText(
                _panel,
                "Reinforcements by Continent",
                18,
                FontStyle.Bold,
                new Color(1f, 0.95f, 0.80f),
                TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            title.rectTransform.sizeDelta = new Vector2(0f, 26f);
            AddTextOutline(title, new Color(0f, 0f, 0f, 0.55f));

            var subtitle = CreateText(
                _panel,
                "Control all territories in a continent to gain bonus armies.",
                10,
                FontStyle.Italic,
                new Color(0.85f, 0.88f, 0.93f, 0.84f),
                TextAnchor.MiddleLeft);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 18f);

            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(_panel, false);
            var dividerRect = (RectTransform)divider.transform;
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.anchoredPosition = new Vector2(0f, -50f);
            dividerRect.sizeDelta = new Vector2(0f, 2f);
            divider.GetComponent<Image>().color = new Color(1f, 0.80f, 0.42f, 0.55f);

            var gridGo = new GameObject("LegendGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            gridGo.transform.SetParent(_panel, false);
            var gridRect = (RectTransform)gridGo.transform;
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = new Vector2(0f, -58f);
            gridRect.sizeDelta = new Vector2(0f, 0f);

            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2((PanelWidth - (PanelPadding * 2f) - 10f) / 2f, 54f);
            grid.spacing = new Vector2(10f, 8f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            var fitter = gridGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var continent in map.continents)
            {
                if (continent == null || string.IsNullOrWhiteSpace(continent.id))
                {
                    continue;
                }

                var cardGo = new GameObject("Card_" + continent.id, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                cardGo.transform.SetParent(gridRect, false);
                var cardImage = cardGo.GetComponent<Image>();
                var baseColor = continentColors != null && continentColors.TryGetValue(continent.id, out var c)
                    ? c
                    : new Color(0.8f, 0.8f, 0.8f);
                cardImage.color = new Color(baseColor.r * 0.28f, baseColor.g * 0.28f, baseColor.b * 0.28f, 0.56f);
                AddOutline(cardImage, new Color(1f, 1f, 1f, 0.10f), new Vector2(1f, -1f));

                var v = cardGo.GetComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(8, 8, 6, 6);
                v.spacing = 2f;
                v.childAlignment = TextAnchor.UpperLeft;
                v.childControlHeight = false;
                v.childForceExpandHeight = false;

                var nameRow = new GameObject("NameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                nameRow.transform.SetParent(cardGo.transform, false);
                var h = nameRow.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 6f;
                h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlHeight = false;
                h.childForceExpandHeight = false;
                ((RectTransform)nameRow.transform).sizeDelta = new Vector2(0f, 16f);

                var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image));
                strip.transform.SetParent(nameRow.transform, false);
                var stripRect = (RectTransform)strip.transform;
                stripRect.sizeDelta = new Vector2(7f, 14f);
                strip.GetComponent<Image>().color = baseColor;

                var name = CreateText(
                    (RectTransform)nameRow.transform,
                    ToDisplayName(continent.id),
                    12,
                    FontStyle.Bold,
                    new Color(0.97f, 0.97f, 0.97f),
                    TextAnchor.MiddleLeft);
                name.rectTransform.sizeDelta = new Vector2(120f, 16f);
                AddTextOutline(name, new Color(0f, 0f, 0f, 0.35f));

                var bonusBg = new GameObject("BonusBg", typeof(RectTransform), typeof(Image));
                bonusBg.transform.SetParent(cardGo.transform, false);
                var bonusBgRect = (RectTransform)bonusBg.transform;
                bonusBgRect.sizeDelta = new Vector2(0f, 22f);
                bonusBg.GetComponent<Image>().color = new Color(1f, 0.75f, 0.28f, 0.23f);

                var bonus = CreateText(
                    bonusBgRect,
                    $"+{continent.bonus}",
                    17,
                    FontStyle.Bold,
                    new Color(1f, 0.98f, 0.90f),
                    TextAnchor.MiddleCenter);
                bonus.rectTransform.sizeDelta = new Vector2(0f, 22f);
                AddTextOutline(bonus, new Color(0f, 0f, 0f, 0.45f));
            }

            var foot = CreateText(
                _panel,
                "Bonus applies when controlling every territory in that continent.",
                10,
                FontStyle.Normal,
                new Color(0.88f, 0.88f, 0.88f, 0.72f),
                TextAnchor.UpperLeft);
            foot.rectTransform.anchorMin = new Vector2(0f, 0f);
            foot.rectTransform.anchorMax = new Vector2(1f, 0f);
            foot.rectTransform.pivot = new Vector2(0.5f, 0f);
            foot.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            foot.rectTransform.sizeDelta = new Vector2(0f, 26f);
            foot.horizontalOverflow = HorizontalWrapMode.Wrap;
            foot.verticalOverflow = VerticalWrapMode.Truncate;
            AddTextOutline(foot, new Color(0f, 0f, 0f, 0.35f));
        }

        private void EnsureCanvas()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                {
                    var tmp = new GameObject("LegendFontFallback");
                    var text = tmp.AddComponent<Text>();
                    _font = text.font;
                    if (Application.isPlaying)
                    {
                        Object.Destroy(tmp);
                    }
                    else
                    {
                        Object.DestroyImmediate(tmp);
                    }
                }
            }

            _canvas = GetComponentInChildren<Canvas>(true);
            if (_canvas == null)
            {
                var go = new GameObject("LegendCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                _canvas = go.GetComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 600;
            _canvas.pixelPerfect = false;

            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelTransform = _canvas.transform.Find("LegendPanel");
            if (panelTransform == null)
            {
                var panelGo = new GameObject("LegendPanel", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(_canvas.transform, false);
                _panel = panelGo.GetComponent<RectTransform>();
                panelGo.GetComponent<Image>().color = new Color(0.04f, 0.055f, 0.086f, 0.82f);
            }
            else
            {
                _panel = panelTransform as RectTransform;
            }

            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0f, 0f);
            _panel.anchoredPosition = new Vector2(14f, 14f);
            _panel.sizeDelta = new Vector2(PanelWidth, 280f);
        }

        private Text CreateText(RectTransform parent, string value, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.text = value;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static void EnsurePanelDecor(RectTransform panel)
        {
            if (panel.GetComponent<Shadow>() == null)
            {
                var shadow = panel.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
                shadow.effectDistance = new Vector2(3f, -3f);
            }
        }

        private static void AddTextOutline(Text text, Color color)
        {
            var outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            var outline = graphic.GetComponent<Outline>();
            if (outline == null)
            {
                outline = graphic.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Object.Destroy(child.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static string ToDisplayName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Unknown";
            }

            var chunks = id.Split('_');
            for (var i = 0; i < chunks.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(chunks[i]))
                {
                    continue;
                }

                chunks[i] = char.ToUpperInvariant(chunks[i][0]) + chunks[i].Substring(1);
            }

            return string.Join(" ", chunks);
        }
    }
}
