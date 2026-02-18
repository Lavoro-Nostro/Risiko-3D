using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public sealed class BoardVisualAnchor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public SpriteRenderer SpriteRenderer
        {
            get
            {
                if (spriteRenderer != null)
                {
                    return spriteRenderer;
                }

                spriteRenderer = GetComponent<SpriteRenderer>();
                return spriteRenderer;
            }
        }
    }
}
