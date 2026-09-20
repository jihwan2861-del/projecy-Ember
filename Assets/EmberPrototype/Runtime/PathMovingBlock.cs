using UnityEngine;
using UnityEngine.Events;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Path Moving Block")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PathMovingBlock : MonoBehaviour
    {
        [SerializeField] private MovementPath path;
        [SerializeField, Min(0.01f)] private float moveSpeed = 2.5f;
        [SerializeField] private bool snapToPathStart = true;
        [SerializeField] private bool activateOnStart;
        [SerializeField] private UnityEvent onArrived = new UnityEvent();

        private Rigidbody2D body;
        private float travelledDistance;
        private bool isMoving;
        private bool hasArrived;

        public bool IsMoving => isMoving;
        public bool HasArrived => hasArrived;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            ConfigureBody();
            PreparePath();
            if (activateOnStart) Activate();
        }

        private void FixedUpdate()
        {
            if (!isMoving || path == null || !path.IsValid) return;

            travelledDistance = Mathf.MoveTowards(
                travelledDistance,
                path.TotalLength,
                moveSpeed * Time.fixedDeltaTime);
            body.MovePosition(path.GetPositionAtDistance(travelledDistance));

            if (travelledDistance < path.TotalLength) return;
            isMoving = false;
            hasArrived = true;
            onArrived.Invoke();
        }

        public void Activate()
        {
            if (hasArrived) return;
            if (!PreparePath())
            {
                Debug.LogWarning("PathMovingBlock needs a MovementPath with at least two points.", this);
                return;
            }
            isMoving = true;
        }

        public void ResetBlock()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            isMoving = false;
            hasArrived = false;
            travelledDistance = 0f;
            if (!PreparePath()) return;
            body.position = path.StartPosition;
            body.linearVelocity = Vector2.zero;
        }

        private bool PreparePath()
        {
            if (path == null) return false;
            path.Rebuild();
            if (!path.IsValid) return false;
            if (snapToPathStart && !isMoving && !hasArrived)
            {
                travelledDistance = 0f;
                body.position = path.StartPosition;
            }
            return true;
        }

        private void ConfigureBody()
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }
}
