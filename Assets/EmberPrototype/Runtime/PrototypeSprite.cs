using UnityEngine;

namespace EmberPrototype
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PrototypeSprite : MonoBehaviour
    {
        private static Sprite sharedSquare;

        [SerializeField] private Color color = Color.white;
        [SerializeField] private int sortingOrder;

        public Color Color
        {
            get => color;
            set
            {
                color = value;
                Apply();
            }
        }

        public void SetAppearance(Sprite sprite, Color tint)
        {
            color = tint;
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : GetSquare();
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
        }

        private void Awake()
        {
            Apply();
        }

        private void Apply()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = GetSquare();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetSquare()
        {
            if (sharedSquare != null)
            {
                return sharedSquare;
            }

            Texture2D texture = new Texture2D(1, 1)
            {
                name = "Prototype Square",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sharedSquare = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sharedSquare.name = "Prototype Square";
            sharedSquare.hideFlags = HideFlags.HideAndDontSave;
            return sharedSquare;
        }
    }
}
