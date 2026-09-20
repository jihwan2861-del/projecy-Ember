using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EmberPrototype
{
    public sealed class FireVisualEffect : MonoBehaviour
    {
        private const int MaxParticles = 24;

        [SerializeField, Min(0f)] private float baseLightIntensity = 1.25f;
        [SerializeField, Min(0f)] private float lightFlicker = 0.28f;

        private Light2D fireLight;
        private float flickerOffset;

        public static FireVisualEffect Create(Transform owner)
        {
            GameObject effectObject = new GameObject("Fire Effect (Fallback)");
            effectObject.transform.SetParent(owner, false);
            effectObject.transform.localPosition = new Vector3(0f, 0.55f, -0.1f);
            effectObject.transform.localScale = new Vector3(
                1f / Mathf.Max(0.01f, owner.lossyScale.x),
                1f / Mathf.Max(0.01f, owner.lossyScale.y),
                1f);

            FireVisualEffect effect = effectObject.AddComponent<FireVisualEffect>();
            effect.Build();
            return effect;
        }

        private void Awake()
        {
            flickerOffset = Random.value * 100f;
        }

        private void Update()
        {
            if (fireLight == null)
            {
                return;
            }

            float noise = Mathf.PerlinNoise(flickerOffset, Time.time * 7f) * 2f - 1f;
            fireLight.intensity = baseLightIntensity + noise * lightFlicker;
        }

        private void Build()
        {
            if (fireLight != null)
            {
                return;
            }

            fireLight = gameObject.AddComponent<Light2D>();
            fireLight.lightType = Light2D.LightType.Point;
            fireLight.color = new Color(1f, 0.34f, 0.06f);
            fireLight.intensity = baseLightIntensity;
            fireLight.pointLightInnerRadius = 0.35f;
            fireLight.pointLightOuterRadius = 3.2f;

            ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.25f, 0.02f),
                new Color(1f, 0.92f, 0.2f));
            main.gravityModifier = -0.18f;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 15f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 0.08f, 0.05f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.12f), 0f),
                    new GradientColorKey(new Color(1f, 0.18f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sortingOrder = 20;
            particles.Play();
        }
    }
}
