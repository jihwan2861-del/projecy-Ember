using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace EmberPrototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FlammableTile : MonoBehaviour
    {
        [Header("Fire")]
        [SerializeField, Min(0.1f)] private float spreadRadius = 1.35f;
        [SerializeField, Min(0f)] private float spreadDelay = 0.45f;
        [SerializeField] private bool startsBurning;

        [Header("Appearance")]
        [Tooltip("Optional. Uses a SpriteRenderer on this object when empty.")]
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Sprite unlitSprite;
        [SerializeField] private Sprite burningSprite;
        [SerializeField] private bool applyColorTint;
        [SerializeField] private Color unlitColor = Color.white;
        [SerializeField] private Color burningColor = new Color(1f, 0.28f, 0.03f);

        [Header("Effects")]
        [Tooltip("Optional authored effect. A lightweight fallback effect is created when empty.")]
        [SerializeField] private GameObject fireEffect;
        [SerializeField] private bool createFallbackEffect;

        [Header("Absorb Target Indicator")]
        [SerializeField] private bool showAbsorbTargetIndicator = true;
        [SerializeField, Min(0f)] private float absorbIndicatorPadding = 0.14f;
        [SerializeField, Min(0.001f)] private float absorbIndicatorLineWidth = 0.045f;
        [SerializeField, Min(0f)] private float absorbIndicatorPulseSpeed = 5f;
        [SerializeField] private Color absorbIndicatorColor = new Color(1f, 0.68f, 0.16f, 0.95f);
        [SerializeField] private int absorbIndicatorSortingOrderOffset = 20;

        [Header("Ignition Events")]
        [SerializeField] private UnityEvent onIgnited = new UnityEvent();
        [SerializeField] private UnityEvent onFireReset = new UnityEvent();

        private Coroutine spreadRoutine;
        private Coroutine absorbIndicatorRoutine;
        private LineRenderer absorbTargetIndicator;
        private Material absorbTargetMaterial;
        private Collider2D tileCollider;
        private bool isAbsorbTarget;

        public bool IsBurning { get; private set; }
        public Vector2 AnchorPosition => transform.position;
        public event Action<FlammableTile> FireStateChanged;

        private void Awake()
        {
            tileCollider = GetComponent<Collider2D>();
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            ApplyAppearance(false);
            if (fireEffect != null)
            {
                fireEffect.SetActive(false);
            }

            if (startsBurning)
            {
                TryIgnite();
            }
        }

        private void OnDisable()
        {
            if (spreadRoutine != null)
            {
                StopCoroutine(spreadRoutine);
                spreadRoutine = null;
            }
            HideAbsorbTargetIndicator();
        }

        private void OnDestroy()
        {
            if (absorbTargetMaterial != null) Destroy(absorbTargetMaterial);
        }

        public bool TryIgnite()
        {
            if (IsBurning)
            {
                return false;
            }

            IsBurning = true;
            ApplyAppearance(true);
            ActivateFireEffect();
            onIgnited.Invoke();
            spreadRoutine = StartCoroutine(SpreadAfterDelay());
            return true;
        }

        public void ConfigureStartBurning(bool value)
        {
            startsBurning = value;
        }

        public void SetAbsorbTarget(bool isTarget)
        {
            isAbsorbTarget = isTarget && IsBurning && showAbsorbTargetIndicator;
            if (!isAbsorbTarget)
            {
                HideAbsorbTargetIndicator();
                return;
            }

            EnsureAbsorbTargetIndicator();
            if (absorbTargetIndicator == null || absorbIndicatorRoutine != null) return;
            absorbTargetIndicator.enabled = true;
            absorbIndicatorRoutine = StartCoroutine(AnimateAbsorbTargetIndicator());
        }

        public void ResetFire()
        {
            SetAbsorbTarget(false);
            StopAllCoroutines();
            spreadRoutine = null;
            IsBurning = false;
            ApplyAppearance(false);
            if (fireEffect != null) fireEffect.SetActive(false);
            onFireReset.Invoke();
            if (startsBurning && isActiveAndEnabled) TryIgnite();
        }

        private void ApplyAppearance(bool burning)
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (visual == null) return;

            Sprite sprite = burning ? burningSprite : unlitSprite;
            if (sprite != null) visual.sprite = sprite;
            if (applyColorTint) visual.color = burning ? burningColor : unlitColor;
        }

        private void ActivateFireEffect()
        {
            if (fireEffect != null)
            {
                fireEffect.SetActive(true);
                return;
            }

            if (!createFallbackEffect)
            {
                return;
            }

            FireVisualEffect effect = FireVisualEffect.Create(transform);
            fireEffect = effect.gameObject;
        }

        private void EnsureAbsorbTargetIndicator()
        {
            if (absorbTargetIndicator != null) return;

            GameObject indicatorObject = new GameObject("Absorb Target Indicator");
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.localPosition = Vector3.zero;
            absorbTargetIndicator = indicatorObject.AddComponent<LineRenderer>();
            absorbTargetIndicator.useWorldSpace = true;
            absorbTargetIndicator.loop = true;
            absorbTargetIndicator.positionCount = 40;
            absorbTargetIndicator.numCornerVertices = 2;
            absorbTargetIndicator.numCapVertices = 2;
            absorbTargetIndicator.widthMultiplier = absorbIndicatorLineWidth;
            absorbTargetIndicator.sharedMaterial = CreateAbsorbTargetMaterial();
            absorbTargetIndicator.sortingLayerID = visual != null ? visual.sortingLayerID : 0;
            absorbTargetIndicator.sortingOrder = (visual != null ? visual.sortingOrder : 0) + absorbIndicatorSortingOrderOffset;
            SetAbsorbTargetRingPoints();
            absorbTargetIndicator.enabled = false;
        }

        private Material CreateAbsorbTargetMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            absorbTargetMaterial = new Material(shader) { name = "Absorb Target Indicator (Runtime)" };
            return absorbTargetMaterial;
        }

        private void SetAbsorbTargetRingPoints()
        {
            if (tileCollider == null) tileCollider = GetComponent<Collider2D>();
            Bounds bounds = tileCollider != null ? tileCollider.bounds : new Bounds(transform.position, Vector3.one);
            float radiusX = bounds.extents.x + absorbIndicatorPadding;
            float radiusY = bounds.extents.y + absorbIndicatorPadding;
            for (int i = 0; i < absorbTargetIndicator.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / absorbTargetIndicator.positionCount;
                absorbTargetIndicator.SetPosition(i, new Vector3(
                    bounds.center.x + Mathf.Cos(angle) * radiusX,
                    bounds.center.y + Mathf.Sin(angle) * radiusY,
                    transform.position.z - 0.01f));
            }
        }

        private IEnumerator AnimateAbsorbTargetIndicator()
        {
            float elapsed = 0f;
            while (isAbsorbTarget && absorbTargetIndicator != null)
            {
                elapsed += Time.deltaTime;
                float pulse = 0.78f + (Mathf.Sin(elapsed * absorbIndicatorPulseSpeed) + 1f) * 0.11f;
                Color color = absorbIndicatorColor;
                color.a *= pulse;
                absorbTargetIndicator.startColor = color;
                absorbTargetIndicator.endColor = color;
                absorbTargetIndicator.widthMultiplier = absorbIndicatorLineWidth * (0.9f + pulse * 0.2f);
                yield return null;
            }

            absorbIndicatorRoutine = null;
            if (absorbTargetIndicator != null) absorbTargetIndicator.enabled = false;
        }

        private void HideAbsorbTargetIndicator()
        {
            isAbsorbTarget = false;
            if (absorbIndicatorRoutine != null)
            {
                StopCoroutine(absorbIndicatorRoutine);
                absorbIndicatorRoutine = null;
            }
            if (absorbTargetIndicator != null) absorbTargetIndicator.enabled = false;
        }

        private IEnumerator SpreadAfterDelay()
        {
            yield return new WaitForSeconds(spreadDelay);
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, spreadRadius);
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap.TryGetComponent(out FlammableTile tile) && tile != this)
                {
                    tile.TryIgnite();
                }
            }

            spreadRoutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, spreadRadius);
        }
    }
}
