using UnityEngine;

namespace EmberPrototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class RoomGoal : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PrototypeRoom room))
            {
                room.CompleteRoom();
            }
        }
    }
}
