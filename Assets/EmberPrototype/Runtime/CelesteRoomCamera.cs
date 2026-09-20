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

        private Camera viewCamera;
        private CameraRoom[] rooms;
        private CameraRoom currentRoom;
        private float cameraDepth;

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

        private void OnValidate()
        {
            remainingDistanceAfterOneSecond = Mathf.Clamp(remainingDistanceAfterOneSecond, 0.0001f, 1f);
        }
    }
}
