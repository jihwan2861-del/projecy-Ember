#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace EmberPrototype.Editor
{
    public static class EmberPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/EmberPrototype.unity";
        private const string PrefabFolder = "Assets/EmberPrototype/Prefabs";

        [MenuItem("Ember Prototype/Setup Celeste Camera In Current Scene")]
        public static void SetupCelesteCameraInCurrentScene()
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("No Camera was found in the current scene.");
                return;
            }

            if (!camera.orthographic)
            {
                Undo.RecordObject(camera, "Set Orthographic Camera");
                camera.orthographic = true;
            }

            if (camera.GetComponent<CelesteRoomCamera>() == null)
            {
                Undo.AddComponent<CelesteRoomCamera>(camera.gameObject);
            }

            RemoveLegacyCameraObjects(camera);
            CameraRoom room = GetOrCreateCameraRoom(camera);
            CelesteRoomCamera controller = camera.GetComponent<CelesteRoomCamera>();
            controller.RefreshRooms();
            controller.SetStartingRoom(room);

            FlamePlayerController player = Object.FindFirstObjectByType<FlamePlayerController>();
            if (player != null)
            {
                controller.SetTarget(player.transform, false);
            }

            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = room.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Celeste-style camera is ready. Resize or duplicate Camera Room Bounds to cover each playable room.");
        }

        [MenuItem("Ember Prototype/Create Camera Room Bounds")]
        public static void CreateCameraRoomBounds()
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("No Camera was found in the current scene.");
                return;
            }

            GameObject roomObject = new GameObject("Camera Room Bounds");
            Undo.RegisterCreatedObjectUndo(roomObject, "Create Camera Room Bounds");
            CameraRoom room = Undo.AddComponent<CameraRoom>(roomObject);
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            room.SetWorldBounds(new Bounds(
                new Vector3(camera.transform.position.x, camera.transform.position.y, 0f),
                new Vector3(width, height, 0f)));
            CelesteRoomCamera controller = camera.GetComponent<CelesteRoomCamera>();
            if (controller != null) controller.RefreshRooms();
            Selection.activeGameObject = roomObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Ember Prototype/Build Minimum Prototype Scene")]
        public static void BuildScene()
        {
            CreateReusablePrefabs();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateGlobalLight();
            CreateLevel();
            Transform spawn = CreateSpawnPoint(new Vector2(-8.2f, -2.8f));
            Transform goal = CreateGoal(new Vector2(8f, 3.65f));
            CreateHazard(new Vector2(2.4f, -3.25f), new Vector2(2.2f, 0.5f));
            CreatePlayer(spawn, goal);
            new GameObject("Instructions").AddComponent<PrototypeHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Ember prototype scene created at {ScenePath}");
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            CelesteRoomCamera controller = cameraObject.AddComponent<CelesteRoomCamera>();
            cameraObject.transform.position = new Vector3(1f, 1.3f, -10f);
            GameObject roomObject = new GameObject("Camera Room Bounds");
            CameraRoom room = roomObject.AddComponent<CameraRoom>();
            room.SetWorldBounds(new Bounds(new Vector3(0f, 0f, 0f), new Vector3(22f, 12.4f, 0f)));
            controller.SetStartingRoom(room);
        }

        private static CameraRoom GetOrCreateCameraRoom(Camera camera)
        {
            CameraRoom existing = Object.FindFirstObjectByType<CameraRoom>();
            if (existing != null)
            {
                BoxCollider2D legacyCollider = existing.GetComponent<BoxCollider2D>();
                if (legacyCollider != null && legacyCollider.isTrigger)
                {
                    Undo.DestroyObjectImmediate(legacyCollider);
                }
                return existing;
            }

            GameObject roomObject = new GameObject("Camera Room Bounds");
            Undo.RegisterCreatedObjectUndo(roomObject, "Create Camera Room Bounds");
            CameraRoom room = Undo.AddComponent<CameraRoom>(roomObject);
            room.SetWorldBounds(CalculateDefaultRoomBounds(camera));
            return room;
        }

        private static Bounds CalculateDefaultRoomBounds(Camera camera)
        {
            bool foundTilemap = false;
            Bounds roomBounds = default;
            foreach (TilemapCollider2D tilemapCollider in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
            {
                if (!tilemapCollider.enabled || tilemapCollider.isTrigger) continue;
                if (!foundTilemap)
                {
                    roomBounds = tilemapCollider.bounds;
                    foundTilemap = true;
                }
                else
                {
                    roomBounds.Encapsulate(tilemapCollider.bounds);
                }
            }

            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            if (!foundTilemap)
            {
                return new Bounds(
                    new Vector3(camera.transform.position.x, camera.transform.position.y, 0f),
                    new Vector3(width, height, 0f));
            }

            Vector3 size = roomBounds.size;
            size.x = Mathf.Max(size.x, width);
            size.y = Mathf.Max(size.y, height);
            size.z = 0f;
            roomBounds.size = size;
            return roomBounds;
        }

        private static void RemoveLegacyCameraObjects(Camera camera)
        {
            Transform triggerRoot = camera.transform.Find("Camera Triggers");
            if (triggerRoot != null) Undo.DestroyObjectImmediate(triggerRoot.gameObject);

            Transform transitionBoundary = camera.transform.Find("Camera Transition Boundary");
            if (transitionBoundary != null) Undo.DestroyObjectImmediate(transitionBoundary.gameObject);

            foreach (CameraPositionGrid grid in Object.FindObjectsByType<CameraPositionGrid>(FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(grid.gameObject);
            }

            foreach (CameraShiftTrigger trigger in Object.FindObjectsByType<CameraShiftTrigger>(FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(trigger.gameObject);
            }
        }

        private static void CreateGlobalLight()
        {
            GameObject lightObject = new GameObject("Global Light 2D");
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.85f;
        }

        private static void CreateLevel()
        {
            Transform level = new GameObject("Level").transform;
            CreateStone(level, "Ground", new Vector2(0f, -4f), new Vector2(22f, 1f));
            CreateStone(level, "Left Wall", new Vector2(-10.5f, 0f), new Vector2(1f, 9f));
            CreateStone(level, "Right Wall", new Vector2(10.5f, 0f), new Vector2(1f, 9f));
            CreateStone(level, "Stone Platform", new Vector2(-3.8f, -0.8f), new Vector2(3f, 0.55f));
            CreateStone(level, "High Stone Platform", new Vector2(6.5f, 2.5f), new Vector2(4f, 0.55f));

            CreateWood(level, "Wood Start", new Vector2(-7.2f, -3.15f), new Vector2(1.1f, 0.7f), true);
            CreateWood(level, "Wood Chain 1", new Vector2(-5.9f, -3.15f), new Vector2(1.1f, 0.7f));
            CreateWood(level, "Wood Chain 2", new Vector2(-4.6f, -3.15f), new Vector2(1.1f, 0.7f));
            CreateWood(level, "Wood Platform Fire", new Vector2(-3.8f, -0.1f), new Vector2(1.1f, 0.7f));
            CreateWood(level, "Wood Midair", new Vector2(0.5f, 1.2f), new Vector2(1.1f, 0.7f));
            CreateWood(level, "Wood High", new Vector2(4.8f, 2.95f), new Vector2(1.1f, 0.7f));
            CreateWood(level, "Wood Goal", new Vector2(8f, 4.3f), new Vector2(1.1f, 0.7f));
        }

        private static GameObject CreatePlayerObject(string name)
        {
            GameObject player = new GameObject(name);
            player.transform.localScale = Vector3.one * 0.72f;
            PrototypeSprite visual = player.AddComponent<PrototypeSprite>();
            visual.Color = new Color(1f, 0.86f, 0.12f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<CircleCollider2D>();
            player.AddComponent<FlamePlayerController>();
            player.AddComponent<PlayerFlameFeedback>();
            player.AddComponent<PlayerAfterimageEffect>();
            return player;
        }

        private static void CreatePlayer(Transform spawn, Transform goal)
        {
            GameObject player = CreatePlayerObject("Flame Player");
            player.transform.position = spawn.position;
            player.AddComponent<PrototypeRoom>().Configure(spawn, goal, -6.5f);
        }

        private static Transform CreateSpawnPoint(Vector2 position)
        {
            GameObject spawn = new GameObject("Room Spawn Point");
            spawn.transform.position = position;
            spawn.AddComponent<RoomSpawnPoint>();
            return spawn.transform;
        }

        private static Transform CreateGoal(Vector2 position)
        {
            GameObject goal = CreateTriggerBlock("Room Goal", position, new Vector2(0.8f, 1.2f), new Color(0.1f, 0.9f, 1f));
            goal.AddComponent<RoomGoal>();
            return goal.transform;
        }

        private static void CreateHazard(Vector2 position, Vector2 scale)
        {
            GameObject hazard = CreateTriggerBlock("Hazard Spikes", position, scale, new Color(0.9f, 0.08f, 0.05f));
            hazard.AddComponent<RoomHazard>();
        }

        private static GameObject CreateTriggerBlock(string name, Vector2 position, Vector2 scale, Color color)
        {
            GameObject block = new GameObject(name);
            block.transform.position = position;
            block.transform.localScale = scale;
            PrototypeSprite visual = block.AddComponent<PrototypeSprite>();
            visual.Color = color;
            BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            return block;
        }

        private static void CreateReusablePrefabs()
        {
            EnsureFolder(PrefabFolder);

            SavePrefab(CreatePlayerObject("Player"), "Player.prefab");

            GameObject flammable = CreateBlock(null, "Flammable Tile", Vector2.zero, Vector2.one);
            flammable.AddComponent<FlammableTile>();
            SavePrefab(flammable, "FlammableTile.prefab");

            GameObject stone = CreateBlock(null, "Non-Flammable Tile", Vector2.zero, Vector2.one);
            stone.AddComponent<NonFlammableTile>();
            SavePrefab(stone, "NonFlammableTile.prefab");

            GameObject hazard = CreateTriggerBlock("Hazard", Vector2.zero, Vector2.one, new Color(0.9f, 0.08f, 0.05f));
            hazard.AddComponent<RoomHazard>();
            SavePrefab(hazard, "Hazard.prefab");

            GameObject goal = CreateTriggerBlock("Room Goal", Vector2.zero, Vector2.one, new Color(0.1f, 0.9f, 1f));
            goal.AddComponent<RoomGoal>();
            SavePrefab(goal, "RoomGoal.prefab");

            GameObject spawn = new GameObject("Room Spawn Point");
            spawn.AddComponent<RoomSpawnPoint>();
            SavePrefab(spawn, "RoomSpawnPoint.prefab");
        }

        private static void SavePrefab(GameObject source, string fileName)
        {
            PrefabUtility.SaveAsPrefabAsset(source, $"{PrefabFolder}/{fileName}");
            Object.DestroyImmediate(source);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets/EmberPrototype", "Prefabs");
        }

        private static void CreateStone(Transform parent, string name, Vector2 position, Vector2 scale)
        {
            GameObject tile = CreateBlock(parent, name, position, scale);
            tile.AddComponent<NonFlammableTile>();
        }

        private static void CreateWood(Transform parent, string name, Vector2 position, Vector2 scale, bool ignite = false)
        {
            GameObject tile = CreateBlock(parent, name, position, scale);
            FlammableTile flammable = tile.AddComponent<FlammableTile>();
            flammable.ConfigureStartBurning(ignite);
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector2 position, Vector2 scale)
        {
            GameObject block = new GameObject(name);
            if (parent != null)
            {
                block.transform.SetParent(parent);
            }
            block.transform.position = position;
            block.transform.localScale = scale;
            block.AddComponent<PrototypeSprite>();
            block.AddComponent<BoxCollider2D>();
            return block;
        }
    }
}
#endif
