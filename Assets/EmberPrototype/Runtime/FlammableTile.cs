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

        [Header("Ignition Events")]
        [SerializeField] private UnityEvent onIgnited = new UnityEvent();
        [SerializeField] private UnityEvent onFireReset = new UnityEvent();

        private Coroutine spreadRoutine;

        public bool IsBurning { get; private set; }
        public Vector2 AnchorPosition => transform.position;

        private void Awake()
        {
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

        public void ResetFire()
        {
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
