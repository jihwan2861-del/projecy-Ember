using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Respawn Zone")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class RoomSpawnPoint : MonoBehaviour
    {
        [SerializeField] private Vector2 triggerSize = new Vector2(1.5f, 1.5f);

        private BoxCollider2D zoneCollider;

        private void Awake()
        {
            ConfigureCollider(true);
        }

        private void Reset()
        {
            ConfigureCollider(false);
        }

        private void OnValidate()
        {
            triggerSize.x = Mathf.Max(0.1f, triggerSize.x);
            triggerSize.y = Mathf.Max(0.1f, triggerSize.y);
            ConfigureCollider(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            FlamePlayerController player = other.GetComponentInParent<FlamePlayerController>();
            if (player == null) return;

            player.SetRespawnPoint(transform);
            PrototypeRoom room = player.GetComponent<PrototypeRoom>();
            if (room != null) room.SetSpawnPoint(transform);
        }

        private void ConfigureCollider(bool createIfMissing)
        {
            if (zoneCollider == null) zoneCollider = GetComponent<BoxCollider2D>();
            if (zoneCollider == null && createIfMissing) zoneCollider = gameObject.AddComponent<BoxCollider2D>();
            if (zoneCollider == null) return;

            zoneCollider.isTrigger = true;
            zoneCollider.size = triggerSize;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.8f);
            Gizmos.DrawWireCube(transform.position, triggerSize);
        }
    }
}
