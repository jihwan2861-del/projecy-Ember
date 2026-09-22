using UnityEngine;
using UnityEngine.InputSystem;

namespace EmberPrototype
{
    // Place this on the player and assign optional scene markers in the Inspector.
    public sealed class PrototypeRoom : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform goalPoint;
        [SerializeField] private float goalRadius = 0.8f;
        [SerializeField] private float deathHeight = -7f;
        private Vector2 initialPosition;
        private FlamePlayerController player;
        public bool Completed { get; private set; }

        private void Awake()
        {
            player = GetComponent<FlamePlayerController>();
            initialPosition = transform.position;
        }

        private void Start()
        {
            if (spawnPoint != null) player.ResetAt(spawnPoint.position);
        }

        private void Update()
        {
            if (transform.position.y < deathHeight ||
                (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
                Restart();
            if (!Completed && goalPoint != null && Vector2.Distance(transform.position, goalPoint.position) <= goalRadius)
                CompleteRoom();
        }

        public void Configure(Transform spawn, Transform goal, float fallHeight)
        {
            spawnPoint = spawn;
            goalPoint = goal;
            deathHeight = fallHeight;
        }

        public void SetSpawnPoint(Transform newSpawnPoint)
        {
            spawnPoint = newSpawnPoint;
        }

        public void CompleteRoom()
        {
            Completed = true;
        }

        public void Restart()
        {
            if (player == null) return;
            Completed = false;
            foreach (var tile in FindObjectsByType<FlammableTile>())
                tile.ResetFire();
            foreach (var ember in FindObjectsByType<EmberProjectile>())
                Destroy(ember.gameObject);
            player.ResetAt(spawnPoint != null ? (Vector2)spawnPoint.position : initialPosition);
        }

        private void OnGUI()
        {
            if (Completed) GUI.Box(new Rect(Screen.width / 2f - 150, 20, 300, 45), "ROOM COMPLETE — R to restart");
        }
    }
}
