using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardVisualAnchor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public SpriteRenderer SpriteRenderer
        {
            get
            {
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = GetComponent<SpriteRenderer>();
                    if (_spriteRenderer == null)
                    {
                        _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
                    }
                }

                return _spriteRenderer;
            }
        }

        private void OnValidate()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }
            }
        }
    }
}
