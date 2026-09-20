using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Player Afterimage Effect")]
    [DisallowMultipleComponent]
    public sealed class PlayerAfterimageEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Color afterimageColor = new Color(1f, 0.32f, 0.06f, 0.62f);
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.045f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.2f;
        [SerializeField, Range(1, 24)] private int poolSize = 10;
        [SerializeField] private int sortingOrderOffset = -1;
        [Tooltip("Prevents afterimages from being hidden behind the level Tilemap.")]
        [SerializeField] private int minimumSortingOrder = 1;

        private SpriteRenderer[] pool;
        private float[] remainingTimes;
        private Color[] startColors;
        private Transform poolRoot;
        private float spawnRemaining;
        private int nextIndex;
        private bool emitting;
        private float emissionRemaining = -1f;

        private void Awake()
        {
            ResolveSourceRenderer();
            BuildPool();
        }

        private void OnDisable()
        {
            StopTrail(true);
        }

        private void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot.gameObject);
        }

        private void Update()
        {
            UpdateAfterimages();
            if (emitting && emissionRemaining >= 0f)
            {
                emissionRemaining -= Time.deltaTime;
                if (emissionRemaining <= 0f) emitting = false;
            }
            if (emitting && (sourceRenderer == null || sourceRenderer.sprite == null)) ResolveSourceRenderer();
            if (!emitting || sourceRenderer == null || sourceRenderer.sprite == null) return;

            spawnRemaining -= Time.deltaTime;
            if (spawnRemaining <= 0f)
            {
                SpawnAfterimage();
                spawnRemaining += spawnInterval;
            }
        }

        public void BeginTrail()
        {
            emitting = true;
            emissionRemaining = -1f;
            SpawnImmediately();
        }

        public void PlayTimedTrail(float duration)
        {
            if (duration <= 0f) return;
            emitting = true;
            emissionRemaining = duration;
            SpawnImmediately();
        }

        public void StopTrail(bool clearImmediately = false)
        {
            emitting = false;
            emissionRemaining = 0f;
            if (!clearImmediately || pool == null) return;

            for (int i = 0; i < pool.Length; i++)
            {
                remainingTimes[i] = 0f;
                pool[i].enabled = false;
            }
        }

        private void BuildPool()
        {
            poolRoot = new GameObject($"{name} Afterimages").transform;
            pool = new SpriteRenderer[poolSize];
            remainingTimes = new float[poolSize];
            startColors = new Color[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                GameObject afterimage = new GameObject($"Afterimage {i + 1}");
                afterimage.transform.SetParent(poolRoot, false);
                afterimage.layer = gameObject.layer;
                SpriteRenderer renderer = afterimage.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                pool[i] = renderer;
            }
        }

        private void SpawnAfterimage()
        {
            SpriteRenderer renderer = pool[nextIndex];
            Transform snapshot = renderer.transform;
            Transform source = sourceRenderer.transform;

            snapshot.SetPositionAndRotation(source.position, source.rotation);
            snapshot.localScale = source.lossyScale;
            renderer.sprite = sourceRenderer.sprite;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.flipX = sourceRenderer.flipX;
            renderer.flipY = sourceRenderer.flipY;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(
                sourceRenderer.sortingOrder + sortingOrderOffset,
                minimumSortingOrder);

            Color sourceColor = sourceRenderer.color;
            Color color = new Color(
                sourceColor.r * afterimageColor.r,
                sourceColor.g * afterimageColor.g,
                sourceColor.b * afterimageColor.b,
                sourceColor.a * afterimageColor.a);
            renderer.color = color;
            renderer.enabled = true;
            startColors[nextIndex] = color;
            remainingTimes[nextIndex] = lifetime;
            nextIndex = (nextIndex + 1) % pool.Length;
        }

        private void SpawnImmediately()
        {
            ResolveSourceRenderer();
            if (sourceRenderer != null && sourceRenderer.sprite != null) SpawnAfterimage();
            spawnRemaining = spawnInterval;
        }

        private void ResolveSourceRenderer()
        {
            if (sourceRenderer != null && sourceRenderer.sprite != null) return;

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.sprite == null) continue;
                sourceRenderer = renderer;
                return;
            }
        }

        private void UpdateAfterimages()
        {
            if (pool == null) return;

            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].enabled) continue;
                remainingTimes[i] -= Time.deltaTime;
                if (remainingTimes[i] <= 0f)
                {
                    pool[i].enabled = false;
                    continue;
                }

                Color color = startColors[i];
                color.a *= remainingTimes[i] / lifetime;
                pool[i].color = color;
            }
        }
    }
}
