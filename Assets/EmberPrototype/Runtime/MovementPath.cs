using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Movement Path")]
    [DisallowMultipleComponent]
    public sealed class MovementPath : MonoBehaviour
    {
        public enum PathShape
        {
            Linear,
            Smooth
        }

        [SerializeField] private PathShape shape = PathShape.Linear;
        [SerializeField] private bool autoCollectChildPoints = true;
        [SerializeField] private Transform[] points = Array.Empty<Transform>();
        [SerializeField, Range(4, 32)] private int smoothSamplesPerSegment = 12;
        [SerializeField] private Color pathColor = Color.black;

        private Vector2[] bakedPositions = Array.Empty<Vector2>();
        private float[] bakedDistances = Array.Empty<float>();

        public int PointCount => points != null ? points.Length : 0;
        public float TotalLength { get; private set; }
        public bool IsValid => bakedPositions.Length >= 2 && TotalLength > 0.001f;
        public Vector2 StartPosition => PointCount > 0 && points[0] != null
            ? (Vector2)points[0].position
            : (Vector2)transform.position;

        private void Awake()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            smoothSamplesPerSegment = Mathf.Max(4, smoothSamplesPerSegment);
            if (autoCollectChildPoints) CollectChildPoints();
        }

        [ContextMenu("Collect Child Points")]
        public void CollectChildPoints()
        {
            List<Transform> children = new List<Transform>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                children.Add(transform.GetChild(i));
            }
            points = children.ToArray();
        }

        public void Rebuild()
        {
            if (autoCollectChildPoints) CollectChildPoints();
            if (PointCount < 2)
            {
                bakedPositions = Array.Empty<Vector2>();
                bakedDistances = Array.Empty<float>();
                TotalLength = 0f;
                return;
            }

            int sampleCount = shape == PathShape.Linear
                ? PointCount
                : (PointCount - 1) * smoothSamplesPerSegment + 1;
            bakedPositions = new Vector2[sampleCount];
            bakedDistances = new float[sampleCount];

            if (shape == PathShape.Linear)
            {
                for (int i = 0; i < PointCount; i++) bakedPositions[i] = points[i].position;
            }
            else
            {
                bakedPositions[0] = points[0].position;
                int index = 1;
                for (int segment = 0; segment < PointCount - 1; segment++)
                {
                    for (int sample = 1; sample <= smoothSamplesPerSegment; sample++)
                    {
                        float t = sample / (float)smoothSamplesPerSegment;
                        bakedPositions[index++] = EvaluateSmoothSegment(segment, t);
                    }
                }
            }

            TotalLength = 0f;
            bakedDistances[0] = 0f;
            for (int i = 1; i < bakedPositions.Length; i++)
            {
                TotalLength += Vector2.Distance(bakedPositions[i - 1], bakedPositions[i]);
                bakedDistances[i] = TotalLength;
            }
        }

        public Vector2 GetPositionAtDistance(float distance)
        {
            if (bakedPositions.Length == 0) return transform.position;
            if (distance <= 0f) return bakedPositions[0];
            if (distance >= TotalLength) return bakedPositions[bakedPositions.Length - 1];

            int upper = Array.BinarySearch(bakedDistances, distance);
            if (upper >= 0) return bakedPositions[upper];
            upper = ~upper;
            int lower = upper - 1;
            float segmentLength = bakedDistances[upper] - bakedDistances[lower];
            float t = segmentLength > 0f ? (distance - bakedDistances[lower]) / segmentLength : 0f;
            return Vector2.LerpUnclamped(bakedPositions[lower], bakedPositions[upper], t);
        }

        private Vector2 EvaluateSmoothSegment(int segment, float t)
        {
            Vector2 p0 = points[Mathf.Max(0, segment - 1)].position;
            Vector2 p1 = points[segment].position;
            Vector2 p2 = points[segment + 1].position;
            Vector2 p3 = points[Mathf.Min(PointCount - 1, segment + 2)].position;
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private void OnDrawGizmos()
        {
            if (autoCollectChildPoints) CollectChildPoints();
            if (PointCount < 2) return;

            Gizmos.color = pathColor;
            for (int i = 0; i < PointCount; i++)
            {
                if (points[i] != null) Gizmos.DrawWireSphere(points[i].position, 0.12f);
            }

            if (shape == PathShape.Linear)
            {
                for (int i = 1; i < PointCount; i++) Gizmos.DrawLine(points[i - 1].position, points[i].position);
                return;
            }

            Vector2 previous = points[0].position;
            for (int segment = 0; segment < PointCount - 1; segment++)
            {
                for (int sample = 1; sample <= smoothSamplesPerSegment; sample++)
                {
                    Vector2 current = EvaluateSmoothSegment(segment, sample / (float)smoothSamplesPerSegment);
                    Gizmos.DrawLine(previous, current);
                    previous = current;
                }
            }
        }
    }
}
