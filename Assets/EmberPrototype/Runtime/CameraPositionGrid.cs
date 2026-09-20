using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Camera Position Grid")]
    [DisallowMultipleComponent]
    public sealed class CameraPositionGrid : MonoBehaviour
    {
        [SerializeField] private Camera referenceCamera;
        [SerializeField, Min(1), InspectorName("Main Camera Columns")] private int columns = 3;
        [SerializeField, Min(1), InspectorName("Main Camera Rows")] private int rows = 3;
        [Tooltip("Number of overlap camera positions inserted between two main camera positions.")]
        [SerializeField, Min(0)] private int intermediatePositions = 1;
        [Tooltip("Distance between main camera positions. 1 means one full camera screen.")]
        [SerializeField, Range(0.5f, 2f)] private float mainCameraSpacingInScreens = 1f;
        [SerializeField] private Color mainPositionPreviewColor = new Color(0.05f, 0.75f, 1f, 0.9f);
        [SerializeField] private Color intermediatePreviewColor = new Color(0.05f, 0.9f, 0.35f, 0.45f);

        public Vector2 GetClosestCameraPosition(Vector2 worldPosition)
        {
            ResolveCamera();
            if (referenceCamera == null) return transform.position;

            Vector2 spacing = GetPositionSpacing();
            int effectiveColumns = GetEffectiveCount(columns);
            int effectiveRows = GetEffectiveCount(rows);
            float centerColumn = (effectiveColumns - 1) * 0.5f;
            float centerRow = (effectiveRows - 1) * 0.5f;
            int column = Mathf.Clamp(
                Mathf.RoundToInt((worldPosition.x - transform.position.x) / spacing.x + centerColumn),
                0,
                effectiveColumns - 1);
            int row = Mathf.Clamp(
                Mathf.RoundToInt((worldPosition.y - transform.position.y) / spacing.y + centerRow),
                0,
                effectiveRows - 1);
            return GetCellCenter(column, row, spacing);
        }

        public Vector2 GetAdjacentCameraPosition(Vector2 currentCameraPosition, Vector2 direction)
        {
            ResolveCamera();
            if (referenceCamera == null) return currentCameraPosition;

            Vector2 spacing = GetPositionSpacing();
            GetCellIndices(currentCameraPosition, spacing, out int column, out int row);
            if (!Mathf.Approximately(direction.x, 0f)) column += direction.x > 0f ? 1 : -1;
            if (!Mathf.Approximately(direction.y, 0f)) row += direction.y > 0f ? 1 : -1;
            column = Mathf.Clamp(column, 0, GetEffectiveCount(columns) - 1);
            row = Mathf.Clamp(row, 0, GetEffectiveCount(rows) - 1);
            return GetCellCenter(column, row, spacing);
        }

        public void ConfigureOverlapLayout(float mainSpacingInScreens, int intermediatePositionCount)
        {
            mainCameraSpacingInScreens = Mathf.Clamp(mainSpacingInScreens, 0.5f, 2f);
            intermediatePositions = Mathf.Max(0, intermediatePositionCount);
        }

        private int GetStride()
        {
            return intermediatePositions + 1;
        }

        private int GetEffectiveCount(int mainPositionCount)
        {
            return (mainPositionCount - 1) * GetStride() + 1;
        }

        private Vector2 GetPositionSpacing()
        {
            float height = referenceCamera.orthographicSize * 2f;
            float width = height * referenceCamera.aspect;
            return new Vector2(width, height) * (mainCameraSpacingInScreens / GetStride());
        }

        private Vector2 GetCellCenter(int column, int row, Vector2 spacing)
        {
            float centerColumn = (GetEffectiveCount(columns) - 1) * 0.5f;
            float centerRow = (GetEffectiveCount(rows) - 1) * 0.5f;
            return (Vector2)transform.position + new Vector2(
                (column - centerColumn) * spacing.x,
                (row - centerRow) * spacing.y);
        }

        private void GetCellIndices(Vector2 worldPosition, Vector2 spacing, out int column, out int row)
        {
            int effectiveColumns = GetEffectiveCount(columns);
            int effectiveRows = GetEffectiveCount(rows);
            float centerColumn = (effectiveColumns - 1) * 0.5f;
            float centerRow = (effectiveRows - 1) * 0.5f;
            column = Mathf.Clamp(
                Mathf.RoundToInt((worldPosition.x - transform.position.x) / spacing.x + centerColumn),
                0,
                effectiveColumns - 1);
            row = Mathf.Clamp(
                Mathf.RoundToInt((worldPosition.y - transform.position.y) / spacing.y + centerRow),
                0,
                effectiveRows - 1);
        }

        private void ResolveCamera()
        {
            if (referenceCamera == null) referenceCamera = Camera.main;
        }

        private void OnValidate()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            intermediatePositions = Mathf.Max(0, intermediatePositions);
            mainCameraSpacingInScreens = Mathf.Clamp(mainCameraSpacingInScreens, 0.5f, 2f);
            ResolveCamera();
        }

        private void OnDrawGizmos()
        {
            ResolveCamera();
            if (referenceCamera == null) return;

            Vector2 spacing = GetPositionSpacing();
            float viewHeight = referenceCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * referenceCamera.aspect;
            int effectiveColumns = GetEffectiveCount(columns);
            int effectiveRows = GetEffectiveCount(rows);
            int stride = GetStride();
            for (int row = 0; row < effectiveRows; row++)
            {
                for (int column = 0; column < effectiveColumns; column++)
                {
                    Vector2 center = GetCellCenter(column, row, spacing);
                    bool isMainPosition = column % stride == 0 && row % stride == 0;
                    Gizmos.color = isMainPosition ? mainPositionPreviewColor : intermediatePreviewColor;
                    Gizmos.DrawWireCube(center, new Vector3(viewWidth, viewHeight, 0f));
                    Gizmos.DrawWireSphere(center, isMainPosition ? 0.16f : 0.09f);
                }
            }
        }
    }
}
