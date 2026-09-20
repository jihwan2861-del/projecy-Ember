using UnityEngine;

namespace EmberPrototype
{
    [RequireComponent(typeof(Collider2D), typeof(PrototypeSprite))]
    public sealed class NonFlammableTile : MonoBehaviour
    {
        [SerializeField] private Color stoneColor = new Color(0.22f, 0.25f, 0.3f);

        private void Awake()
        {
            GetComponent<PrototypeSprite>().Color = stoneColor;
        }
    }
}
