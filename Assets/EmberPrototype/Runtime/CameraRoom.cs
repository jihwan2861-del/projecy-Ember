using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Camera Room Bounds")]
    [DisallowMultipleComponent]
    public sealed class CameraRoom : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(18f, 10f);
        [SerializeField] private Vector2 centerOffset;
        [SerializeField] private Vector2 cameraOffset;
        [SerializeField] private Color previewColor = new Color(0.15f, 0.75f, 1f, 0.8f);

        public Vector2 CameraOffset => cameraOffset;

        public Bounds WorldBounds
        {
            get
            {
                Vector2 center = (Vector2)transform.position + centerOffset;
                return new Bounds(center, new Vector3(size.x, size.y, 0f));
            }
        }

        public bool Contains(Vector2 worldPosition)
        {
            Bounds bounds = WorldBounds;
            return worldPosition.x >= bounds.min.x
                && worldPosition.x <= bounds.max.x
                && worldPosition.y >= bounds.min.y
                && worldPosition.y <= bounds.max.y;
        }

        public Vector2 ClampCameraCenter(Vector2 desiredCenter, Vector2 cameraHalfExtents)
        {
            Bounds bounds = WorldBounds;
            float minX = bounds.min.x + cameraHalfExtents.x;
            float maxX = bounds.max.x - cameraHalfExtents.x;
            float minY = bounds.min.y + cameraHalfExtents.y;
            float maxY = bounds.max.y - cameraHalfExtents.y;

            float clampedX = minX <= maxX
                ? Mathf.Clamp(desiredCenter.x, minX, maxX)
                : bounds.center.x;
            float clampedY = minY <= maxY
                ? Mathf.Clamp(desiredCenter.y, minY, maxY)
                : bounds.center.y;
            return new Vector2(clampedX, clampedY);
        }

        public void SetWorldBounds(Bounds bounds)
        {
            transform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
            centerOffset = Vector2.zero;
            size = new Vector2(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.y));
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
        }

        private void OnDrawGizmos()
        {
            Bounds bounds = WorldBounds;
            Gizmos.color = previewColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.DrawWireSphere(bounds.center, 0.15f);
        }
    }
}
