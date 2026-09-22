using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Player Flame Feedback")]
    [DisallowMultipleComponent]
    public sealed class PlayerFlameFeedback : MonoBehaviour
    {
        private enum LaunchRingMode { Hidden, Timed, Anchored }

        [Header("Scene References")]
        [SerializeField] private Light2D flameLight;
        [SerializeField] private ParticleSystem ambientParticles;
        [SerializeField] private ParticleSystem flameParticles;
        [SerializeField] private ParticleSystem actionBurstParticles;
        [Tooltip("Particle System on the player prefab used for the launch-ready ring.")]
        [SerializeField] private ParticleSystem launchRingParticles;

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
        private LaunchRingMode launchRingMode;
        private float launchRingDuration;
        private float launchRingRemaining;
        private float launchRingEmissionRate;

        private void Awake()
        {
            ResolveReferences();
            EnsureLaunchRingParticles();
            if (launchRingParticles != null)
                launchRingEmissionRate = launchRingParticles.emission.rateOverTime.constant;
            if (launchRingEmissionRate <= 0f) launchRingEmissionRate = 90f;
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
            UpdateLaunchRing();
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

        private void OnDisable() => HideLaunchRing();

        public void ShowTimedLaunchRing(float duration)
        {
            launchRingMode = LaunchRingMode.Timed;
            launchRingDuration = launchRingRemaining = Mathf.Max(0.01f, duration);
            PlayLaunchRing();
        }

        public void ShowAnchoredLaunchRing()
        {
            launchRingMode = LaunchRingMode.Anchored;
            PlayLaunchRing();
        }

        public void HideLaunchRing()
        {
            launchRingMode = LaunchRingMode.Hidden;
            if (launchRingParticles != null)
                launchRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            HideLaunchRing();
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

        private void PlayLaunchRing()
        {
            EnsureLaunchRingParticles();
            if (launchRingParticles == null) return;
            launchRingParticles.Clear(true);
            var emission = launchRingParticles.emission;
            emission.rateOverTime = launchRingEmissionRate;
            launchRingParticles.gameObject.SetActive(true);
            launchRingParticles.Emit(36);
            launchRingParticles.Play(true);
        }

        private void UpdateLaunchRing()
        {
            if (launchRingMode == LaunchRingMode.Hidden || launchRingParticles == null) return;

            if (launchRingMode == LaunchRingMode.Timed)
            {
                launchRingRemaining -= Time.deltaTime;
                if (launchRingRemaining <= 0f)
                {
                    HideLaunchRing();
                    return;
                }
            }

            Transform ringTransform = launchRingParticles.transform;
            // X(anchored) and C(timed) deliberately share the exact same authored visual.
            // The particle child stays at the prefab-authored local position.
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
            if (launchRingParticles == null) launchRingParticles = FindNamed(particles, "Launch Ring Particles");
        }

        private void EnsureLaunchRingParticles()
        {
            if (launchRingParticles != null) return;

            Transform existing = transform.Find("Launch Ring Particles");
            GameObject ringObject = existing != null ? existing.gameObject : new GameObject("Launch Ring Particles");
            if (existing == null)
            {
                ringObject.layer = gameObject.layer;
                ringObject.transform.SetParent(transform, false);
            }

            launchRingParticles = ringObject.GetComponent<ParticleSystem>();
            if (launchRingParticles == null) launchRingParticles = ringObject.AddComponent<ParticleSystem>();
            var main = launchRingParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = 0.075f;
            main.startColor = new Color(1f, 0.55f, 0.1f, 0.95f);
            main.maxParticles = 72;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = launchRingParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 90f;
            var shape = launchRingParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;

            ParticleSystemRenderer renderer = launchRingParticles.GetComponent<ParticleSystemRenderer>();
            ParticleSystemRenderer sourceRenderer = actionBurstParticles != null
                ? actionBurstParticles.GetComponent<ParticleSystemRenderer>() : null;
            if (sourceRenderer != null) renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.sortingOrder = 20;
            launchRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
