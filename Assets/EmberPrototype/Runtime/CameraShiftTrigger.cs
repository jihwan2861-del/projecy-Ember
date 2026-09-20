using System.Collections.Generic;
using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Camera Shift Trigger")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CameraShiftTrigger : MonoBehaviour
    {
        public enum ShiftDirection
        {
            Up,
            Down,
            Left,
            Right,
            HorizontalByApproach,
            VerticalByApproach
        }

        [SerializeField] private CelesteRoomCamera cameraController;
        [SerializeField] private ShiftDirection direction = ShiftDirection.HorizontalByApproach;
        [SerializeField, Range(0.1f, 1f)] private float screenFraction = 0.5f;

        private readonly HashSet<FlamePlayerController> playersInside = new HashSet<FlamePlayerController>();
        private BoxCollider2D trigger;

        private void Awake()
        {
            CacheTrigger();
            ResolveCamera();
        }

        private void Reset()
        {
            CacheTrigger();
            trigger.isTrigger = true;
            trigger.size = direction == ShiftDirection.Left
                || direction == ShiftDirection.Right
                || direction == ShiftDirection.HorizontalByApproach
                ? new Vector2(0.5f, 8f)
                : new Vector2(14f, 0.5f);
        }

        private void OnValidate()
        {
            CacheTrigger();
            if (trigger != null) trigger.isTrigger = true;
        }

        private void OnDisable()
        {
            playersInside.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            FlamePlayerController player = other.GetComponentInParent<FlamePlayerController>();
            if (player == null || !TryGetShiftDirection(player, out Vector2 shiftDirection)) return;
            if (!playersInside.Add(player)) return;

            ResolveCamera();
            if (cameraController != null)
            {
                cameraController.ShiftByScreenFraction(shiftDirection, screenFraction);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            FlamePlayerController player = other.GetComponentInParent<FlamePlayerController>();
            if (player != null) playersInside.Remove(player);
        }

        private void ResolveCamera()
        {
            if (cameraController != null) return;
            Camera mainCamera = Camera.main;
            if (mainCamera != null) cameraController = mainCamera.GetComponent<CelesteRoomCamera>();
        }

        private Vector2 GetDirectionVector(Vector2 playerPosition)
        {
            switch (direction)
            {
                case ShiftDirection.Up: return Vector2.up;
                case ShiftDirection.Down: return Vector2.down;
                case ShiftDirection.Left: return Vector2.left;
                case ShiftDirection.HorizontalByApproach:
                    return playerPosition.x < transform.position.x ? Vector2.right : Vector2.left;
                case ShiftDirection.VerticalByApproach:
                    return playerPosition.y < transform.position.y ? Vector2.up : Vector2.down;
                default: return Vector2.right;
            }
        }

        private bool TryGetShiftDirection(FlamePlayerController player, out Vector2 shiftDirection)
        {
            shiftDirection = Vector2.zero;
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Vector2 velocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;

            if (direction == ShiftDirection.HorizontalByApproach)
            {
                if (Mathf.Abs(velocity.y) > Mathf.Abs(velocity.x)) return false;
                shiftDirection = Mathf.Abs(velocity.x) > 0.05f
                    ? (velocity.x > 0f ? Vector2.right : Vector2.left)
                    : GetDirectionVector(player.transform.position);
                return true;
            }

            if (direction == ShiftDirection.VerticalByApproach)
            {
                if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y)) return false;
                shiftDirection = Mathf.Abs(velocity.y) > 0.05f
                    ? (velocity.y > 0f ? Vector2.up : Vector2.down)
                    : GetDirectionVector(player.transform.position);
                return true;
            }

            shiftDirection = GetDirectionVector(player.transform.position);
            return true;
        }

        private void CacheTrigger()
        {
            if (trigger == null) trigger = GetComponent<BoxCollider2D>();
        }

        private void OnDrawGizmos()
        {
            CacheTrigger();
            if (trigger == null) return;

            Bounds bounds = trigger.bounds;
            Gizmos.color = new Color(0.05f, 0.85f, 0.35f, 0.9f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Vector2 directionVector = direction == ShiftDirection.HorizontalByApproach
                ? Vector2.right
                : direction == ShiftDirection.VerticalByApproach
                    ? Vector2.up
                    : GetDirectionVector(transform.position);
            Vector2 center = bounds.center;
            Vector2 tip = center + directionVector;
            Gizmos.DrawLine(center, tip);
            Vector2 side = new Vector2(-directionVector.y, directionVector.x) * 0.22f;
            Gizmos.DrawLine(tip, tip - directionVector * 0.3f + side);
            Gizmos.DrawLine(tip, tip - directionVector * 0.3f - side);

            if (direction != ShiftDirection.HorizontalByApproach && direction != ShiftDirection.VerticalByApproach) return;
            Vector2 oppositeTip = center - directionVector;
            Gizmos.DrawLine(center, oppositeTip);
            Gizmos.DrawLine(oppositeTip, oppositeTip + directionVector * 0.3f + side);
            Gizmos.DrawLine(oppositeTip, oppositeTip + directionVector * 0.3f - side);
        }
    }
}
