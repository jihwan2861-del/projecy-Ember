using EmberPrototype;
using UnityEditor;
using UnityEngine;

namespace EmberPrototype.Editor
{
    /// <summary>Creates the editable ring effect once; later Inspector edits are left alone.</summary>
    public static class LaunchRingParticlePrefabSetup
    {
        private const string PrefabPath = "Assets/prefab/player.prefab";
        private const string RingName = "Launch Ring Particles";

        [InitializeOnLoadMethod]
        private static void QueueInitialSetup()
        {
            EditorApplication.delayCall += TryInitialSetup;
        }

        [MenuItem("Tools/Ember/Setup Launch Ring Particles")]
        public static void SetupIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                PlayerFlameFeedback feedback = prefabRoot.GetComponent<PlayerFlameFeedback>();
                if (feedback == null)
                {
                    Debug.LogWarning("Launch ring setup skipped: player prefab has no PlayerFlameFeedback.");
                    return;
                }

                Transform ringTransform = prefabRoot.transform.Find(RingName);
                bool created = ringTransform == null;
                if (created)
                {
                    GameObject ringObject = new GameObject(RingName);
                    ringObject.layer = prefabRoot.layer;
                    ringObject.transform.SetParent(prefabRoot.transform, false);
                    ringTransform = ringObject.transform;
                }

                ParticleSystem ring = ringTransform.GetComponent<ParticleSystem>();
                if (ring == null)
                {
                    ring = ringTransform.gameObject.AddComponent<ParticleSystem>();
                    ConfigureRing(ring, prefabRoot.GetComponent<SpriteRenderer>());
                    created = true;
                }

                SerializedObject serializedFeedback = new SerializedObject(feedback);
                SerializedProperty ringReference = serializedFeedback.FindProperty("launchRingParticles");
                if (ringReference == null)
                {
                    Debug.LogError("Launch ring setup: launchRingParticles field was not found.");
                    return;
                }

                bool needsReference = ringReference.objectReferenceValue != ring;
                if (needsReference)
                {
                    ringReference.objectReferenceValue = ring;
                    serializedFeedback.ApplyModifiedPropertiesWithoutUndo();
                }

                if (!created && !needsReference) return;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log("Player prefab launch ring Particle System is ready for Inspector editing.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void TryInitialSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.update -= TryInitialSetup;
                EditorApplication.update += TryInitialSetup;
                return;
            }

            EditorApplication.update -= TryInitialSetup;
            SetupIfNeeded();
        }

        private static void ConfigureRing(ParticleSystem ring, SpriteRenderer playerSprite)
        {
            var main = ring.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.32f;
            main.startSpeed = 0f;
            main.startSize = 0.075f;
            main.startColor = new Color(1f, 0.72f, 0.24f, 0.85f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ring.emission;
            emission.enabled = true;
            emission.rateOverTime = 90f;

            var shape = ring.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;

            ParticleSystemRenderer renderer = ring.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_FlameParticle.mat");
            renderer.sortingLayerID = playerSprite != null ? playerSprite.sortingLayerID : 0;
            renderer.sortingOrder = (playerSprite != null ? playerSprite.sortingOrder : 0) + 5;

            ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
