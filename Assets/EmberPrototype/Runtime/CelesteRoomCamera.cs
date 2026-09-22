using UnityEngine;

namespace EmberPrototype
{
    [AddComponentMenu("Ember Prototype/Celeste Room Camera")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CelesteRoomCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [Tooltip("Optional room to use at startup. The camera automatically changes rooms afterward.")]
        [SerializeField] private CameraRoom startingRoom;

        [Header("Following")]
        [SerializeField] private Vector2 globalCameraOffset;
        [Tooltip("How much distance remains after one second. Celeste uses approximately 0.01.")]
        [SerializeField, Range(0.0001f, 1f)] private float remainingDistanceAfterOneSecond = 0.01f;
        [SerializeField] private bool snapToTargetOnStart = true;

        [Header("Screen Shake")]
        [Tooltip("Dash shake duration in seconds.")]
        [SerializeField, Min(0f)] private float dashShakeDuration = 0.12f;
        [Tooltip("Dash shake amplitude in world units.")]
        [SerializeField, Min(0f)] private float dashShakeMagnitude = 0.12f;
        [Tooltip("Fire absorption shake duration in seconds.")]
        [SerializeField, Min(0f)] private float absorbShakeDuration = 0.08f;
        [Tooltip("Fire absorption shake amplitude in world units.")]
        [SerializeField, Min(0f)] private float absorbShakeMagnitude = 0.08f;
        [Tooltip("Shake when launching out of a burning tile with Z.")]
        [SerializeField, Min(0f)] private float fireLaunchShakeDuration = 0.12f;
        [SerializeField, Min(0f)] private float fireLaunchShakeMagnitude = 0.12f;

        private Camera viewCamera;
        private CameraRoom[] rooms;
        private CameraRoom currentRoom;
        private float cameraDepth;
        private float shakeRemaining;
        private float shakeDuration;
        private float shakeMagnitude;
        private float shakeSeed;

        public CameraRoom CurrentRoom => currentRoom;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            cameraDepth = transform.position.z;
            if (!viewCamera.orthographic)
            {
                Debug.LogError("CelesteRoomCamera requires an orthographic Camera.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            ResolveTarget();
            RefreshRooms();
            currentRoom = startingRoom != null ? startingRoom : FindRoomContaining(TargetPosition);
            if (snapToTargetOnStart && target != null)
            {
                SetCameraPosition(CalculateTargetPosition());
            }
        }

        private void LateUpdate()
        {
            ResolveTarget();
            if (target == null) return;

            UpdateCurrentRoom();
            Vector2 from = transform.position;
            Vector2 destination = CalculateTargetPosition();
            float blend = 1f - Mathf.Pow(remainingDistanceAfterOneSecond, Time.deltaTime);
            SetCameraPosition(Vector2.LerpUnclamped(from, destination, blend));
            ApplyShake();
        }

        public void ShakeDash() => Shake(dashShakeDuration, dashShakeMagnitude);
        public void ShakeAbsorb() => Shake(absorbShakeDuration, absorbShakeMagnitude);
        public void ShakeFireLaunch() => Shake(fireLaunchShakeDuration, fireLaunchShakeMagnitude);

        public void Shake(float duration, float magnitude)
        {
            if (duration <= 0f || magnitude <= 0f) return;
            if (magnitude < shakeMagnitude && shakeRemaining > 0f) return;
            shakeDuration = duration;
            shakeRemaining = duration;
            shakeMagnitude = magnitude;
            shakeSeed = Random.value * 1000f;
        }

        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;
            if (target == null) return;

            UpdateCurrentRoom();
            if (snapImmediately) SetCameraPosition(CalculateTargetPosition());
        }

        public void SetStartingRoom(CameraRoom room, bool snapImmediately = false)
        {
            startingRoom = room;
            currentRoom = room;
            if (snapImmediately && target != null) SetCameraPosition(CalculateTargetPosition());
        }

        public void ShiftByScreenFraction(Vector2 direction, float screenFraction)
        {
            // Retained only so legacy CameraShiftTrigger components can deserialize safely.
        }

        public void RefreshRooms()
        {
            rooms = FindObjectsByType<CameraRoom>(FindObjectsSortMode.None);
            if (currentRoom != null && !currentRoom.isActiveAndEnabled) currentRoom = null;
        }

        private void ResolveTarget()
        {
            if (target != null) return;
            FlamePlayerController player = FindFirstObjectByType<FlamePlayerController>();
            if (player != null) target = player.transform;
        }

        private Vector2 TargetPosition => target != null ? target.position : transform.position;

        private void UpdateCurrentRoom()
        {
            Vector2 playerPosition = TargetPosition;
            if (currentRoom != null && currentRoom.isActiveAndEnabled && currentRoom.Contains(playerPosition)) return;
            currentRoom = FindRoomContaining(playerPosition);
        }

        private CameraRoom FindRoomContaining(Vector2 position)
        {
            if (rooms == null) RefreshRooms();

            CameraRoom bestRoom = null;
            float bestArea = float.PositiveInfinity;
            foreach (CameraRoom room in rooms)
            {
                if (room == null || !room.isActiveAndEnabled || !room.Contains(position)) continue;

                Bounds bounds = room.WorldBounds;
                float area = bounds.size.x * bounds.size.y;
                if (area >= bestArea) continue;
                bestRoom = room;
                bestArea = area;
            }
            return bestRoom;
        }

        private Vector2 CalculateTargetPosition()
        {
            Vector2 desiredCenter = TargetPosition + globalCameraOffset;
            if (currentRoom == null) return desiredCenter;

            desiredCenter += currentRoom.CameraOffset;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            return currentRoom.ClampCameraCenter(desiredCenter, new Vector2(halfWidth, halfHeight));
        }

        private void SetCameraPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, cameraDepth);
        }

        private void ApplyShake()
        {
            if (shakeRemaining <= 0f) return;
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
            float progress = shakeDuration > 0f ? shakeRemaining / shakeDuration : 0f;
            float x = Mathf.PerlinNoise(shakeSeed, Time.time * 48f) * 2f - 1f;
            float y = Mathf.PerlinNoise(shakeSeed + 17.31f, Time.time * 48f) * 2f - 1f;
            transform.position += new Vector3(x, y, 0f) * (shakeMagnitude * progress);
            if (shakeRemaining <= 0f) shakeMagnitude = 0f;
        }

        private void OnValidate()
        {
            remainingDistanceAfterOneSecond = Mathf.Clamp(remainingDistanceAfterOneSecond, 0.0001f, 1f);
            dashShakeDuration = Mathf.Max(0f, dashShakeDuration);
            dashShakeMagnitude = Mathf.Max(0f, dashShakeMagnitude);
            absorbShakeDuration = Mathf.Max(0f, absorbShakeDuration);
            absorbShakeMagnitude = Mathf.Max(0f, absorbShakeMagnitude);
            fireLaunchShakeDuration = Mathf.Max(0f, fireLaunchShakeDuration);
            fireLaunchShakeMagnitude = Mathf.Max(0f, fireLaunchShakeMagnitude);
        }
    }
}
