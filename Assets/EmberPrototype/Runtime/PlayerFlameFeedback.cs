using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Player Flame Feedback")]
    [DisallowMultipleComponent]
    public sealed class PlayerFlameFeedback : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Light2D flameLight;
        [SerializeField] private ParticleSystem ambientParticles;
        [SerializeField] private ParticleSystem flameParticles;
        [SerializeField] private ParticleSystem actionBurstParticles;

        [Header("Light Flicker")]
        [SerializeField, Min(0f)] private float baseIntensity = 1.25f;
        [SerializeField, Min(0f)] private float baseOuterRadius = 3.4f;
        [SerializeField, Min(0f)] private float flickerIntensity = 0.14f;
        [SerializeField, Min(0f)] private float flickerRadius = 0.12f;
        [SerializeField, Min(0.01f)] private float flickerSpeed = 7f;

        [Header("Action Burst Counts")]
        [SerializeField, Min(0)] private int jumpParticles = 6;
        [SerializeField, Min(0)] private int airJumpParticles = 10;
        [SerializeField, Min(0)] private int landParticles = 8;
        [SerializeField, Min(0)] private int launchParticles = 14;
        [SerializeField, Min(0)] private int ignitionBurstParticles = 20;

        private float noiseOffset;
        private float pulseRemaining;
        private float pulseDuration;
        private float pulseIntensity;
        private float pulseRadius;

        private void Awake()
        {
            ResolveReferences();
            noiseOffset = Random.value * 100f;
            if (actionBurstParticles != null)
            {
                actionBurstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Reset() => ResolveReferences();
        private void OnValidate() => ResolveReferences();

        private void Update()
        {
            if (flameLight == null) return;

            float noise = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed) * 2f - 1f;
            float pulse = 0f;
            if (pulseRemaining > 0f)
            {
                pulseRemaining = Mathf.Max(0f, pulseRemaining - Time.deltaTime);
                float normalized = pulseDuration > 0f ? pulseRemaining / pulseDuration : 0f;
                pulse = normalized * normalized;
            }

            flameLight.intensity = Mathf.Max(0f, baseIntensity + noise * flickerIntensity + pulse * pulseIntensity);
            flameLight.pointLightOuterRadius = Mathf.Max(0f, baseOuterRadius + noise * flickerRadius + pulse * pulseRadius);
        }

        public void PlayJump(bool airJump)
        {
            TriggerPulse(airJump ? 0.2f : 0.15f, airJump ? 1.25f : 0.8f, airJump ? 0.75f : 0.45f);
            EmitActionParticles(airJump ? airJumpParticles : jumpParticles);
        }

        public void PlayLand()
        {
            TriggerPulse(0.13f, 0.65f, 0.35f);
            EmitActionParticles(landParticles);
        }

        public void PlayLaunch(Vector2 direction)
        {
            TriggerPulse(0.22f, 1.55f, 0.95f);
            EmitActionParticles(launchParticles);
        }

        public void PlayBurst()
        {
            TriggerPulse(0.28f, 2.1f, 1.35f);
            EmitActionParticles(ignitionBurstParticles);
        }

        public void ResetFeedback()
        {
            pulseRemaining = 0f;
            if (actionBurstParticles != null)
            {
                actionBurstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EmitActionParticles(int count)
        {
            if (actionBurstParticles != null && count > 0) actionBurstParticles.Emit(count);
        }

        private void TriggerPulse(float duration, float intensity, float radius)
        {
            if (duration <= 0f) return;
            if (pulseRemaining > 0f && pulseIntensity > intensity) return;
            pulseDuration = pulseRemaining = duration;
            pulseIntensity = intensity;
            pulseRadius = radius;
        }

        private void ResolveReferences()
        {
            Light2D[] lights = GetComponentsInChildren<Light2D>(true);
            if (flameLight == null) flameLight = FindNamed(lights, "Flame Light");
            if (flameLight == null && lights.Length > 0) flameLight = lights[0];

            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            if (ambientParticles == null) ambientParticles = FindNamed(particles, "Particle System");
            if (flameParticles == null) flameParticles = FindNamed(particles, "Flame Particles");
            if (actionBurstParticles == null) actionBurstParticles = FindNamed(particles, "Action Burst Particles");
        }

        private static T FindNamed<T>(T[] components, string objectName) where T : Component
        {
            foreach (T component in components)
            {
                if (component.gameObject.name == objectName) return component;
            }

            return null;
        }
    }
}
