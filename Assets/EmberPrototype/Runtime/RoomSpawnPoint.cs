using UnityEngine;

namespace EmberPrototype
{
    public sealed class RoomSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.8f);
        }
    }
}
