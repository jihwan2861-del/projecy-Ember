using UnityEngine;

namespace EmberPrototype
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class AllTorchesDoor : MonoBehaviour
    {
        [Tooltip("Every assigned torch must be burning to open this door.")]
        [SerializeField] private FlammableTile[] requiredTorches = new FlammableTile[2];
        [Tooltip("Optional. Defaults to the SpriteRenderer on this object.")]
        [SerializeField] private SpriteRenderer doorVisual;

        private BoxCollider2D doorCollider;

        private void Awake()
        {
            doorCollider = GetComponent<BoxCollider2D>();
            if (doorVisual == null) doorVisual = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (requiredTorches != null)
            {
                foreach (FlammableTile torch in requiredTorches)
                {
                    if (torch != null) torch.FireStateChanged += OnTorchFireStateChanged;
                }
            }

            RefreshDoor();
        }

        private void OnDisable()
        {
            if (requiredTorches == null) return;
            foreach (FlammableTile torch in requiredTorches)
            {
                if (torch != null) torch.FireStateChanged -= OnTorchFireStateChanged;
            }
        }

        private void OnTorchFireStateChanged(FlammableTile torch)
        {
            RefreshDoor();
        }

        private void RefreshDoor()
        {
            bool allBurning = requiredTorches != null && requiredTorches.Length > 0;
            if (allBurning)
            {
                foreach (FlammableTile torch in requiredTorches)
                {
                    if (torch != null && torch.IsBurning) continue;
                    allBurning = false;
                    break;
                }
            }

            doorCollider.enabled = !allBurning;
            if (doorVisual != null) doorVisual.enabled = !allBurning;
        }
    }
}
