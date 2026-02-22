using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardVisualLayer : MonoBehaviour
    {
        private GameObject _visualObject;
        private SpriteRenderer _spriteRenderer;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;

        public void Build(GameRuntimeConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (config.VisualMode == BoardVisualMode.DebugOnly)
            {
                return;
            }

            // Reuse a manually placed scene visual if present (preferred for authored table setups).
            var manualRenderer = FindManualMapSpriteRenderer();
            if (manualRenderer != null)
            {
                _visualObject = manualRenderer.gameObject;
                _spriteRenderer = manualRenderer;
                _spriteRenderer.sortingOrder = -10;
                RaiseVisualSlightlyAboveBoardBack();
                return;
            }

            var sprite = Resources.Load<Sprite>(config.BoardMapSpriteResourcePath);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(config.BoardMapSpriteResourcePath);
                if (texture == null)
                {
                    Debug.LogWarning($"[Risiko3D][Board] Map sprite/texture not found in Resources at '{config.BoardMapSpriteResourcePath}'.");
                    return;
                }

                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            _visualObject = new GameObject("BoardMapVisual");
            _visualObject.transform.SetParent(transform, false);
            _visualObject.transform.localPosition = config.BoardVisualPosition;
            _visualObject.transform.localRotation = Quaternion.Euler(config.BoardVisualRotation);

            _spriteRenderer = _visualObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = sprite;
            _spriteRenderer.sortingOrder = -10;
            _spriteRenderer.color = Color.white;

            var bounds = sprite.bounds.size;
            if (bounds.x > 0f && bounds.y > 0f)
            {
                _visualObject.transform.localScale = new Vector3(
                    config.BoardVisualWorldSize.x / bounds.x,
                    config.BoardVisualWorldSize.y / bounds.y,
                    1f);
            }

            RaiseVisualSlightlyAboveBoardBack();
        }

        private SpriteRenderer FindManualMapSpriteRenderer()
        {
            // Preferred path: explicit anchor in scene.
            var anchor = Object.FindFirstObjectByType<BoardVisualAnchor>();
            if (anchor != null && anchor.SpriteRenderer != null)
            {
                return anchor.SpriteRenderer;
            }

            if (transform == null)
            {
                return null;
            }

            var manual = transform.Find("ManualBoardMapVisual");
            if (manual != null)
            {
                var manualRenderer = manual.GetComponentInChildren<SpriteRenderer>(true);
                if (manualRenderer != null)
                {
                    return manualRenderer;
                }
            }

            var renderers = transform.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject == null)
                {
                    continue;
                }

                if (r.gameObject.name.IndexOf("manualboardmapvisual", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return r;
                }
            }

            // Last-resort global scan, in case manual map is not parented under Board root.
            var allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < allRenderers.Length; i++)
            {
                var r = allRenderers[i];
                if (r == null || r.gameObject == null)
                {
                    continue;
                }

                if (r.gameObject.name.IndexOf("manualboardmapvisual", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return r;
                }
            }

            return null;
        }

        private void RaiseVisualSlightlyAboveBoardBack()
        {
            if (_visualObject == null)
            {
                return;
            }

            var boardBack = FindBoardBackRenderer();
            if (boardBack == null)
            {
                return;
            }

            var p = _visualObject.transform.position;
            var topY = boardBack.bounds.max.y + 0.0035f;
            if (p.y < topY)
            {
                _visualObject.transform.position = new Vector3(p.x, topY, p.z);
            }
        }

        private static Renderer FindBoardBackRenderer()
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

            return null;
        }
    }
}
