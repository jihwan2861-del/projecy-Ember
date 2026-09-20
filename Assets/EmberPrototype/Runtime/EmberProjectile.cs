using UnityEngine;

namespace EmberPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PrototypeSprite))]
    public sealed class EmberProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 2.5f;

        private float remainingLifetime;

        public void Launch(Vector2 velocity)
        {
            remainingLifetime = lifetime;
            GetComponent<Rigidbody2D>().linearVelocity = velocity;
        }

        private void Update()
        {
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out FlammableTile tile))
            {
                tile.TryIgnite();
            }

            if (!other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
