using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Death Hazard")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class RoomHazard : MonoBehaviour
    {
        private BoxCollider2D hazardCollider;

        private void Awake()
        {
            ConfigureCollider();
        }

        private void Reset()
        {
            ConfigureCollider();
        }

        private void OnValidate()
        {
            ConfigureCollider();
        }

        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);

        private void OnCollisionEnter2D(Collision2D collision) => TryKill(collision.collider);

        private void TryKill(Collider2D other)
        {
            FlamePlayerController player = other.GetComponentInParent<FlamePlayerController>();
            if (player == null) return;

            PrototypeRoom room = player.GetComponent<PrototypeRoom>();
            if (room != null)
            {
                room.Restart();
                return;
            }

            player.KillAndRespawn();
        }

        private void ConfigureCollider()
        {
            if (hazardCollider == null) hazardCollider = GetComponent<BoxCollider2D>();
            if (hazardCollider != null) hazardCollider.isTrigger = true;
        }
    }
}
