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

            var anchor = Object.FindFirstObjectByType<BoardVisualAnchor>();
            if (anchor != null && anchor.SpriteRenderer != null)
            {
                _visualObject = anchor.SpriteRenderer.gameObject;
                _spriteRenderer = anchor.SpriteRenderer;
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
        }
    }
}
