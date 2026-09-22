using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmberPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class FlamePlayerController : MonoBehaviour
    {
        private enum FlameState { Free, Bursting, BurstDashing, Travelling, Anchored }

        [Header("Platforming")]
        [SerializeField, Min(0f)] private float moveSpeed = 7f;
        [SerializeField, Min(0f)] private float jumpSpeed = 12f;
        [Tooltip("Number of extra jumps available after leaving the ground.")]
        [SerializeField, Min(0)] private int maxAirJumps = 1;
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.12f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBuffer = 0.12f;
        [SerializeField, Min(0f)] private float groundAcceleration = 65f;
        [SerializeField, Min(0f)] private float groundDeceleration = 85f;
        [SerializeField, Min(0f)] private float airAcceleration = 22f;
        [SerializeField, Min(0f)] private float airDeceleration = 14f;
        [SerializeField, Min(0f)] private float turnAcceleration = 95f;
        [SerializeField, Min(0f)] private float maxFallSpeed = 22f;

        [Header("Celeste-Style Jump Feel")]
        [SerializeField, Range(0.1f, 1f)] private float apexGravityMultiplier = 0.5f;
        [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.3f;
        [SerializeField, Min(0f)] private float apexVelocityThreshold = 2.2f;
        [SerializeField, Min(0f)] private float cornerCorrectionDistance = 0.24f;
        [SerializeField, Min(0.01f)] private float cornerCorrectionStep = 0.04f;
        [Tooltip("Briefly restores horizontal speed if the player clears a wall just after hitting it.")]
        [SerializeField, Min(0f)] private float wallSpeedRetentionTime = 0.06f;

        [Header("Respawn")]
        [Tooltip("Optional. If empty, the player's position when the scene starts is used.")]
        [SerializeField] private Transform respawnPoint;

        [Header("Fire Absorb (X)")]
        [SerializeField, Range(-1f, 1f)] private float fireTargetDirectionDot = 0.35f;

        [Header("Fire Absorb Target Line")]
        [SerializeField] private bool showAbsorbTargetLine = true;
        [SerializeField, Min(0.001f)] private float absorbTargetLineWidth = 0.045f;
        [SerializeField] private Color absorbTargetLineColor = new Color(1f, 0.68f, 0.16f, 0.9f);
        [SerializeField] private Material absorbTargetLineMaterial;
        [SerializeField, Min(0)] private int absorbTargetLineSortingOrderOffset = 10;
        [Tooltip("More segments make the animated flame guide curve smoother.")]
        [SerializeField, Range(4, 24)] private int absorbTargetLineSegments = 14;
        [Tooltip("Maximum sideways movement of the flame guide, in world units.")]
        [SerializeField, Min(0f)] private float absorbTargetLineWobbleAmount = 0.035f;
        [Tooltip("Speed of the smooth, irregular flame motion.")]
        [SerializeField, Min(0.01f)] private float absorbTargetLineWobbleSpeed = 2.8f;
        [Tooltip("Speed that the flame texture drifts along the guide.")]
        [SerializeField, Min(0f)] private float absorbTargetLineTextureScrollSpeed = 0.6f;
        [Tooltip("Width of the soft outer glow relative to the main line.")]
        [SerializeField, Range(2f, 5f)] private float absorbTargetLineGlowWidthMultiplier = 3.5f;

        [Header("Ignition Burst (C)")]
        [SerializeField, Min(0.1f)] private float ignitionBurstRadius = 1.5f;
        [InspectorName("Ignition Burst Pause Time")]
        [SerializeField, Min(0f)] private float ignitionBurstChargeTime = 0.5f;
        [SerializeField, Min(0.01f)] private float ignitionBurstVisualTime = 0.18f;

        [Header("Burst Dash (C, then Z)")]
        [SerializeField, Min(0f)] private float burstDashSpeed = 16f;
        [SerializeField, Min(0.01f)] private float burstDashDuration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float burstDashEndSpeedMultiplier = 0.55f;
        [Tooltip("Optional smaller collider used only during burst dash. Leave empty to keep the normal collider.")]
        [SerializeField] private Collider2D burstDashCollider;
        [Tooltip("Short look-ahead used to nudge a dash around nearby corners.")]
        [SerializeField, Min(0f)] private float burstDashObstacleAssistDistance = 0.45f;
        [SerializeField, Range(0f, 90f)] private float burstDashObstacleAssistAngle = 22f;
        [SerializeField, Range(0, 4)] private int burstDashObstacleAssistSteps = 3;

        [Header("Fire Network")]
        [SerializeField, Min(0f)] private float fireTravelSpeed = 20f;
        [SerializeField, Min(0f)] private float fireTravelRange = 9f;
        [Tooltip("Solid layers that block travel between the player and a fire. Nothing falls back to all layers for safety.")]
        [SerializeField] private LayerMask fireTravelObstacleMask = ~0;
        [Tooltip("Uses a slightly smaller copy of the player's collider so standing on the floor does not block travel.")]
        [SerializeField, Range(0.25f, 1f)] private float fireTravelCollisionScale = 0.75f;
        [Tooltip("Maximum sideways correction used to flow around a small corner. Set to 0 to disable assist.")]
        [SerializeField, Min(0f)] private float fireTravelCornerAssistDistance = 1f;
        [SerializeField, Min(0.05f)] private float fireTravelCornerAssistStep = 0.25f;
        [Tooltip("Speed retained after fire travel hits a solid obstacle.")]
        [SerializeField, Min(0f)] private float fireTravelBounceSpeed = 15f;
        [SerializeField, Min(1f)] private float fireTravelGroundBounceMultiplier = 1.2f;
        [SerializeField, Range(0f, 1f)] private float fireTravelWallUpwardBias = 0.25f;
        [SerializeField, Min(0f)] private float fireTravelBounceAfterimageDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float fireNetworkStepRange = 1.65f;
        [SerializeField, Range(-1f, 1f)] private float fireNetworkDirectionDot = 0.45f;
        [SerializeField, Min(0f)] private float launchSpeed = 15f;
        [SerializeField, Min(0f)] private float launchAfterimageDuration = 0.22f;
        [Tooltip("Small sideways adjustment allowed when leaving a fire beside a wall.")]
        [SerializeField, Min(0f)] private float fireLaunchCornerAssistDistance = 0.24f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private FlameState state;
        private FlammableTile targetFire;
        private FlammableTile absorbTargetPreview;
        private LineRenderer absorbTargetLine;
        private LineRenderer absorbTargetLineGlow;
        private LineRenderer absorbTargetLineCore;
        private Material runtimeAbsorbTargetLineMaterial;
        private Texture2D runtimeAbsorbTargetLineTexture;
        private Vector3[] absorbTargetLinePositions;
        private FlammableTile travelSourceFire;
        private float horizontalInput;
        private float coyoteRemaining;
        private float jumpRemaining;
        private float launchProtection;
        private float burstChargeRemaining;
        private float burstDashRemaining;
        private Vector2 burstDashDirection;
        private bool jumpReleased;
        private bool jumpHeld;
        private bool normalJump;
        private bool cornerCorrectionUsed;
        private int airJumpsRemaining;
        private bool burstAvailable = true;
        private int facingDirection = 1;
        private Vector3 initialScale;
        private float initialGravity;
        private CollisionDetectionMode2D initialCollisionDetectionMode;
        private Vector2 initialSpawnPosition;
        private float upwardVelocityBeforeCollision;
        private float horizontalVelocityBeforeCollision;
        private float retainedWallSpeedX;
        private float wallSpeedRetentionRemaining;
        private GameObject activeBurstEffect;
        private PlayerFlameFeedback flameFeedback;
        private PlayerAfterimageEffect afterimageEffect;
        private bool groundStateInitialized;
        private bool wasGrounded;
        private Vector2 fireTravelProbeSize;
        private Vector2 fireTravelProbeOffset;
        private Vector2 fireTravelWaypoint;
        private bool hasFireTravelWaypoint;
        private readonly RaycastHit2D[] fireTravelCastHits = new RaycastHit2D[8];
        private readonly RaycastHit2D[] wallClearanceCastHits = new RaycastHit2D[8];
        private Collider2D[] fireTargetHits = new Collider2D[32];

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            if (burstDashCollider == null)
            {
                Transform dashColliderTransform = transform.Find("Dash Collider");
                if (dashColliderTransform != null) burstDashCollider = dashColliderTransform.GetComponent<Collider2D>();
            }
            if (burstDashCollider != null) burstDashCollider.enabled = false;

            if (bodyCollider == null)
            {
                Debug.LogError("FlamePlayerController requires a Collider2D on the same GameObject.", this);
                enabled = false;
                return;
            }

            initialScale = transform.localScale;
            initialGravity = body.gravityScale;
            initialCollisionDetectionMode = body.collisionDetectionMode;
            initialSpawnPosition = transform.position;
            Bounds colliderBounds = bodyCollider.bounds;
            fireTravelProbeSize = colliderBounds.size;
            fireTravelProbeOffset = (Vector2)colliderBounds.center - body.position;
            airJumpsRemaining = maxAirJumps;
            flameFeedback = GetComponent<PlayerFlameFeedback>();
            if (flameFeedback == null) flameFeedback = gameObject.AddComponent<PlayerFlameFeedback>();
            afterimageEffect = GetComponent<PlayerAfterimageEffect>();
            if (afterimageEffect == null) afterimageEffect = gameObject.AddComponent<PlayerAfterimageEffect>();
            CreateAbsorbTargetLine();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ClearBurstEffect();
            SetBurstDashCollider(false);
            SetAbsorbTargetPreview(null);
            flameFeedback?.HideLaunchRing();
            afterimageEffect?.StopTrail();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (state == FlameState.Anchored)
            {
                body.linearVelocity = Vector2.zero;
                if (Keyboard.current.zKey.wasPressedThisFrame)
                {
                    LaunchFromFire();
                    return;
                }

                Vector2 direction = ReadPressedDirection();
                if (direction != Vector2.zero) TryMoveInsideFire(direction);
                return;
            }

            if (state == FlameState.Bursting)
            {
                if (Keyboard.current.zKey.wasPressedThisFrame)
                    StartBurstDash();
                return;
            }

            if (state == FlameState.Travelling || state == FlameState.BurstDashing) return;

            Vector2 heldDirection = ReadHeldDirection();
            horizontalInput = heldDirection.x;
            jumpHeld = Keyboard.current.zKey.isPressed;
            if (horizontalInput != 0f) facingDirection = horizontalInput > 0f ? 1 : -1;
            UpdateAbsorbTargetPreview(heldDirection);

            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                jumpRemaining = jumpBuffer;
                jumpReleased = false;
            }
            if (Keyboard.current.zKey.wasReleasedThisFrame) jumpReleased = true;
            if (Keyboard.current.xKey.wasPressedThisFrame) TryAbsorb();
            if (Keyboard.current.cKey.wasPressedThisFrame) TryIgnitionBurst();
        }

        private void FixedUpdate()
        {
            if (state == FlameState.Bursting)
            {
                body.linearVelocity = Vector2.zero;
                burstChargeRemaining -= Time.fixedDeltaTime;
                if (burstChargeRemaining <= 0f)
                {
                    state = FlameState.Free;
                    body.gravityScale = initialGravity;
                    flameFeedback.HideLaunchRing();
                }
                return;
            }

            if (state == FlameState.BurstDashing)
            {
                if (TryCastFireTravelStep(burstDashDirection,
                    burstDashSpeed * Time.fixedDeltaTime, out _))
                {
                    EndBurstDash(true);
                    return;
                }

                body.linearVelocity = burstDashDirection * burstDashSpeed;
                burstDashRemaining -= Time.fixedDeltaTime;
                if (burstDashRemaining <= 0f) EndBurstDash(false);
                return;
            }

            if (state == FlameState.Free)
            {
                bool grounded = body.linearVelocity.y <= 0.1f && IsGrounded();
                if (!groundStateInitialized)
                {
                    groundStateInitialized = true;
                    wasGrounded = grounded;
                }
                else if (grounded && !wasGrounded)
                {
                    flameFeedback.PlayLand();
                }

                if (grounded)
                {
                    coyoteRemaining = coyoteTime;
                    airJumpsRemaining = maxAirJumps;
                    burstAvailable = true;
                    cornerCorrectionUsed = false;
                    wallSpeedRetentionRemaining = 0f;
                }
                else coyoteRemaining -= Time.fixedDeltaTime;

                Vector2 velocity = body.linearVelocity;
                bool jumpedThisStep = false;
                launchProtection -= Time.fixedDeltaTime;
                if (launchProtection <= 0f)
                {
                    float targetSpeed = horizontalInput * moveSpeed;
                    float acceleration = SelectHorizontalAcceleration(velocity.x, targetSpeed, grounded);
                    velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
                    ApplyWallSpeedRetention(ref velocity);
                }
                if (jumpRemaining > 0f && coyoteRemaining > 0f)
                {
                    velocity.y = jumpSpeed;
                    jumpRemaining = coyoteRemaining = 0f;
                    normalJump = true;
                    cornerCorrectionUsed = false;
                    jumpedThisStep = true;
                    flameFeedback.PlayJump(false);
                }
                else if (jumpRemaining > 0f && airJumpsRemaining > 0)
                {
                    velocity.y = jumpSpeed;
                    jumpRemaining = 0f;
                    airJumpsRemaining--;
                    normalJump = true;
                    cornerCorrectionUsed = false;
                    jumpedThisStep = true;
                    flameFeedback.PlayJump(true);
                }
                jumpRemaining -= Time.fixedDeltaTime;
                if (normalJump && jumpReleased && velocity.y > 0f)
                {
                    velocity.y *= 0.5f;
                    normalJump = false;
                }
                if (velocity.y <= 0f) normalJump = false;
                velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
                UpdateJumpGravity(velocity.y, grounded);
                upwardVelocityBeforeCollision = Mathf.Max(0f, velocity.y);
                horizontalVelocityBeforeCollision = velocity.x;
                body.linearVelocity = velocity;
                wasGrounded = grounded && !jumpedThisStep;
                return;
            }

            if (state != FlameState.Travelling || targetFire == null) return;
            Vector2 destination = CurrentFireTravelDestination();
            if (hasFireTravelWaypoint && Vector2.SqrMagnitude(destination - body.position) <= 0.03f)
            {
                hasFireTravelWaypoint = false;
                destination = targetFire.AnchorPosition;
            }

            Vector2 displacement = destination - body.position;
            float stepDistance = Mathf.Min(displacement.magnitude, fireTravelSpeed * Time.fixedDeltaTime);
            if (stepDistance > 0.001f
                && TryCastFireTravelStep(displacement / displacement.magnitude, stepDistance, out Vector2 surfaceNormal))
            {
                BounceFromFireTravel(surfaceNormal);
                return;
            }

            body.MovePosition(Vector2.MoveTowards(body.position, destination, stepDistance));
            if (!hasFireTravelWaypoint && Vector2.SqrMagnitude(targetFire.AnchorPosition - body.position) <= 0.03f)
            {
                EnterFire();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleFlammableContact(collision.collider);

            if (state == FlameState.BurstDashing && IsDashBlocked(collision))
            {
                EndBurstDash(true);
                return;
            }

            if (state == FlameState.Travelling
                && TryGetFireTravelBlockingNormal(collision, out Vector2 blockingNormal))
            {
                BounceFromFireTravel(blockingNormal);
                return;
            }

            TryCornerCorrection(collision);
            RememberWallSpeed(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (state == FlameState.BurstDashing && IsDashBlocked(collision))
            {
                EndBurstDash(true);
                return;
            }

            if (state == FlameState.Travelling
                && TryGetFireTravelBlockingNormal(collision, out Vector2 blockingNormal))
            {
                BounceFromFireTravel(blockingNormal);
                return;
            }

            TryCornerCorrection(collision);
        }
        private void OnTriggerEnter2D(Collider2D other) => HandleFlammableContact(other);

        private bool IsDashBlocked(Collision2D collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Vector2.Dot(burstDashDirection, collision.GetContact(i).normal) < -0.2f)
                    return true;
            }
            return false;
        }

        private void RememberWallSpeed(Collision2D collision)
        {
            if (state != FlameState.Free || wallSpeedRetentionTime <= 0f
                || Mathf.Abs(horizontalVelocityBeforeCollision) < 0.1f) return;

            float direction = Mathf.Sign(horizontalVelocityBeforeCollision);
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.x * direction > -0.5f) continue;
                retainedWallSpeedX = horizontalVelocityBeforeCollision;
                wallSpeedRetentionRemaining = wallSpeedRetentionTime;
                return;
            }
        }

        private void ApplyWallSpeedRetention(ref Vector2 velocity)
        {
            if (wallSpeedRetentionRemaining <= 0f) return;
            wallSpeedRetentionRemaining -= Time.fixedDeltaTime;
            if (Mathf.Abs(horizontalInput) < 0.1f) return;
            if (Mathf.Sign(horizontalInput) != Mathf.Sign(retainedWallSpeedX))
            {
                wallSpeedRetentionRemaining = 0f;
                return;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = false;
            int count = bodyCollider.Cast(Vector2.right * Mathf.Sign(retainedWallSpeedX),
                filter, wallClearanceCastHits, 0.05f);
            for (int i = 0; i < count; i++)
            {
                if (wallClearanceCastHits[i].normal.x * Mathf.Sign(retainedWallSpeedX) < -0.5f) return;
            }

            if (Mathf.Abs(velocity.x) < Mathf.Abs(retainedWallSpeedX))
                velocity.x = retainedWallSpeedX;
            wallSpeedRetentionRemaining = 0f;
        }

        private float SelectHorizontalAcceleration(float currentSpeed, float targetSpeed, bool grounded)
        {
            if (Mathf.Approximately(targetSpeed, 0f))
            {
                return grounded ? groundDeceleration : airDeceleration;
            }

            bool reversing = !Mathf.Approximately(currentSpeed, 0f)
                && Mathf.Sign(currentSpeed) != Mathf.Sign(targetSpeed);
            if (reversing) return turnAcceleration;
            return grounded ? groundAcceleration : airAcceleration;
        }

        private void UpdateJumpGravity(float verticalSpeed, bool grounded)
        {
            if (grounded)
            {
                body.gravityScale = initialGravity;
                return;
            }

            if (jumpHeld && Mathf.Abs(verticalSpeed) <= apexVelocityThreshold)
            {
                body.gravityScale = initialGravity * apexGravityMultiplier;
            }
            else if (verticalSpeed < 0f)
            {
                body.gravityScale = initialGravity * fallGravityMultiplier;
            }
            else
            {
                body.gravityScale = initialGravity;
            }
        }

        private void TryCornerCorrection(Collision2D collision)
        {
            if (state != FlameState.Free || cornerCorrectionUsed || upwardVelocityBeforeCollision <= 0f) return;

            bool hitCeiling = false;
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.65f)
                {
                    hitCeiling = true;
                    break;
                }
            }
            if (!hitCeiling) return;

            float preferredDirection = horizontalInput != 0f ? Mathf.Sign(horizontalInput) : facingDirection;
            int stepCount = Mathf.CeilToInt(cornerCorrectionDistance / cornerCorrectionStep);
            for (int step = 1; step <= stepCount; step++)
            {
                float distance = Mathf.Min(step * cornerCorrectionStep, cornerCorrectionDistance);
                if (TryApplyCornerOffset(preferredDirection * distance)) return;
                if (TryApplyCornerOffset(-preferredDirection * distance)) return;
            }
        }

        private bool TryApplyCornerOffset(float horizontalOffset)
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 testCenter = (Vector2)bounds.center + new Vector2(horizontalOffset, 0.03f);
            Vector2 testSize = new Vector2(bounds.size.x * 0.88f, bounds.size.y * 0.94f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(testCenter, testSize, 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit != bodyCollider && !hit.isTrigger) return false;
            }

            body.position += new Vector2(horizontalOffset, 0.03f);
            Vector2 velocity = body.linearVelocity;
            velocity.y = upwardVelocityBeforeCollision;
            body.linearVelocity = velocity;
            cornerCorrectionUsed = true;
            return true;
        }

        private void HandleFlammableContact(Collider2D other)
        {
            if (!other.TryGetComponent(out FlammableTile tile)) return;
            tile.TryIgnite();
        }

        private bool IsGrounded()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
            RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, new Vector2(bounds.size.x * 0.8f, 0.05f), 0f, Vector2.down, groundProbeDistance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != bodyCollider && !hit.collider.isTrigger) return true;
            }
            return false;
        }

        private Vector2 ReadHeldDirection()
        {
            Vector2 direction = Vector2.zero;
            if (Keyboard.current.leftArrowKey.isPressed) direction.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) direction.x += 1f;
            if (Keyboard.current.upArrowKey.isPressed) direction.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed) direction.y -= 1f;
            return direction == Vector2.zero ? Vector2.zero : direction.normalized;
        }

        private Vector2 ReadPressedDirection()
        {
            Vector2 direction = Vector2.zero;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) direction.x -= 1f;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) direction.x += 1f;
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) direction.y += 1f;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame) direction.y -= 1f;
            return direction == Vector2.zero ? Vector2.zero : direction.normalized;
        }

        private void LateUpdate()
        {
            if (state != FlameState.Free || (absorbTargetPreview != null && !absorbTargetPreview.IsBurning))
                SetAbsorbTargetPreview(null);
            UpdateAbsorbTargetLine();
        }

        private void TryAbsorb()
        {
            if (absorbTargetPreview != null && absorbTargetPreview.IsBurning)
                BeginFireTravel(absorbTargetPreview);
        }

        private void UpdateAbsorbTargetPreview(Vector2 inputDirection)
        {
            Vector2 direction = inputDirection == Vector2.zero ? Vector2.right * facingDirection : inputDirection;
            SetAbsorbTargetPreview(FindBurningFire(direction));
        }

        private void SetAbsorbTargetPreview(FlammableTile preview)
        {
            if (absorbTargetPreview == preview) return;
            absorbTargetPreview = preview;
            SetAbsorbTargetLineEnabled(preview != null);
            UpdateAbsorbTargetLine();
        }

        private void CreateAbsorbTargetLine()
        {
            if (!showAbsorbTargetLine) return;

            runtimeAbsorbTargetLineMaterial = CreateAbsorbTargetLineMaterial();
            absorbTargetLineSegments = Mathf.Clamp(absorbTargetLineSegments, 4, 24);
            absorbTargetLinePositions = new Vector3[absorbTargetLineSegments + 1];

            SpriteRenderer playerVisual = GetComponentInChildren<SpriteRenderer>();
            int sortingLayerId = 0;
            int baseSortingOrder = absorbTargetLineSortingOrderOffset;
            if (playerVisual != null)
            {
                sortingLayerId = playerVisual.sortingLayerID;
                baseSortingOrder += playerVisual.sortingOrder;
            }

            Color glowColor = absorbTargetLineColor;
            glowColor.a *= 0.18f;
            Color coreColor = Color.Lerp(absorbTargetLineColor, Color.white, 0.72f);

            absorbTargetLineGlow = CreateAbsorbTargetLineRenderer(
                "Absorb Target Line Glow", absorbTargetLineWidth * absorbTargetLineGlowWidthMultiplier,
                glowColor, sortingLayerId, baseSortingOrder);
            absorbTargetLine = CreateAbsorbTargetLineRenderer(
                "Absorb Target Line", absorbTargetLineWidth,
                absorbTargetLineColor, sortingLayerId, baseSortingOrder + 1);
            absorbTargetLineCore = CreateAbsorbTargetLineRenderer(
                "Absorb Target Line Core", absorbTargetLineWidth * 0.32f,
                coreColor, sortingLayerId, baseSortingOrder + 2);
            SetAbsorbTargetLineEnabled(false);
        }

        private LineRenderer CreateAbsorbTargetLineRenderer(
            string objectName, float width, Color color, int sortingLayerId, int sortingOrder)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = absorbTargetLinePositions.Length;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.startColor = color;
            line.endColor = color;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = Vector2.one;
            line.sharedMaterial = runtimeAbsorbTargetLineMaterial;
            line.sortingLayerID = sortingLayerId;
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return line;
        }

        private Material CreateAbsorbTargetLineMaterial()
        {
            if (absorbTargetLineMaterial != null)
            {
                runtimeAbsorbTargetLineMaterial = new Material(absorbTargetLineMaterial)
                {
                    name = "Absorb Target Line (Runtime)"
                };
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) return null;

                runtimeAbsorbTargetLineMaterial = new Material(shader)
                {
                    name = "Absorb Target Line (Runtime)"
                };
            }

            if (runtimeAbsorbTargetLineMaterial.mainTexture == null)
            {
                runtimeAbsorbTargetLineTexture = CreateAbsorbTargetLineTexture();
                runtimeAbsorbTargetLineMaterial.mainTexture = runtimeAbsorbTargetLineTexture;
            }

            return runtimeAbsorbTargetLineMaterial;
        }

        private static Texture2D CreateAbsorbTargetLineTexture()
        {
            const int textureWidth = 64;
            const int textureHeight = 16;
            var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "Absorb Target Flame Texture (Runtime)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[textureWidth * textureHeight];
            var random = new System.Random(9281);
            for (int x = 0; x < textureWidth; x++)
            {
                float edgeJitter = (float)random.NextDouble() * 0.22f - 0.11f;
                for (int y = 0; y < textureHeight; y++)
                {
                    float across = Mathf.Abs((y + 0.5f) / textureHeight * 2f - 1f);
                    float noise = Mathf.PerlinNoise(x * 0.31f, y * 0.47f);
                    float edge = 0.78f + edgeJitter + (noise - 0.5f) * 0.22f;
                    float alpha = 1f - Mathf.SmoothStep(edge - 0.12f, edge + 0.08f, across);
                    alpha *= Mathf.Lerp(0.58f, 1f, noise);
                    if (random.NextDouble() < 0.035) alpha *= 0.25f;

                    float core = 1f - Mathf.SmoothStep(0.08f, 0.76f, across);
                    Color color = Color.Lerp(
                        new Color(1f, 0.22f, 0.025f),
                        new Color(1f, 0.96f, 0.54f),
                        core);
                    pixels[y * textureWidth + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void UpdateAbsorbTargetLine()
        {
            if (runtimeAbsorbTargetLineMaterial != null
                && runtimeAbsorbTargetLineMaterial.HasProperty("_MainTex"))
            {
                runtimeAbsorbTargetLineMaterial.mainTextureOffset =
                    Vector2.left * (Time.time * absorbTargetLineTextureScrollSpeed);
            }

            if (absorbTargetLine == null) return;
            if (state != FlameState.Free || absorbTargetPreview == null || !absorbTargetPreview.IsBurning)
            {
                SetAbsorbTargetLineEnabled(false);
                return;
            }

            Vector2 start = body.position;
            Vector2 end = absorbTargetPreview.AnchorPosition;
            Vector2 direction = end - start;
            Vector2 perpendicular = direction.sqrMagnitude > 0.0001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            float time = Time.time * absorbTargetLineWobbleSpeed;
            for (int i = 0; i < absorbTargetLinePositions.Length; i++)
            {
                float t = i / (float)(absorbTargetLinePositions.Length - 1);
                Vector2 point = Vector2.Lerp(start, end, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float broadNoise = Mathf.PerlinNoise(12.7f + i * 0.39f, time) * 2f - 1f;
                float fineNoise = Mathf.PerlinNoise(41.3f + i * 0.93f, time * 1.7f) * 2f - 1f;
                float offset = (broadNoise * 0.72f + fineNoise * 0.28f)
                    * absorbTargetLineWobbleAmount * envelope;
                point += perpendicular * offset;
                absorbTargetLinePositions[i] = new Vector3(point.x, point.y, transform.position.z);
            }

            absorbTargetLine.SetPositions(absorbTargetLinePositions);
            absorbTargetLineGlow.SetPositions(absorbTargetLinePositions);
            absorbTargetLineCore.SetPositions(absorbTargetLinePositions);
            SetAbsorbTargetLineEnabled(true);
        }

        private void SetAbsorbTargetLineEnabled(bool enabled)
        {
            if (absorbTargetLine != null) absorbTargetLine.enabled = enabled;
            if (absorbTargetLineGlow != null) absorbTargetLineGlow.enabled = enabled;
            if (absorbTargetLineCore != null) absorbTargetLineCore.enabled = enabled;
        }

        private void OnDestroy()
        {
            if (runtimeAbsorbTargetLineMaterial != null) Destroy(runtimeAbsorbTargetLineMaterial);
            if (runtimeAbsorbTargetLineTexture != null) Destroy(runtimeAbsorbTargetLineTexture);
        }

        private FlammableTile FindBurningFire(Vector2 direction)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(body.position, fireTravelRange, fireTargetHits);
            if (hitCount == fireTargetHits.Length)
            {
                System.Array.Resize(ref fireTargetHits, fireTargetHits.Length * 2);
                hitCount = Physics2D.OverlapCircleNonAlloc(body.position, fireTravelRange, fireTargetHits);
            }
            FlammableTile selected = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = fireTargetHits[i];
                if (hit == null) continue;
                if (!hit.TryGetComponent(out FlammableTile tile) || !tile.IsBurning) continue;
                Vector2 offset = tile.AnchorPosition - body.position;
                float distance = offset.magnitude;
                if (distance <= 0.05f) continue;
                float directionMatch = Vector2.Dot(offset / distance, direction);
                if (directionMatch < fireTargetDirectionDot) continue;
                float score = directionMatch * 2f - distance / fireTravelRange;
                if (score > bestScore)
                {
                    bestScore = score;
                    selected = tile;
                }
            }
            return selected;
        }

        private void TryIgnitionBurst()
        {
            if (!burstAvailable) return;
            SetAbsorbTargetPreview(null);
            burstAvailable = false;
            TriggerIgnitionBurst();
            flameFeedback.PlayBurst();
            state = FlameState.Bursting;
            burstChargeRemaining = ignitionBurstChargeTime;
            jumpRemaining = coyoteRemaining = 0f;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            normalJump = false;
            flameFeedback.ShowTimedLaunchRing(ignitionBurstChargeTime);
        }

        private void StartBurstDash()
        {
            if (burstChargeRemaining <= 0f) return;

            burstDashDirection = ReadHeldDirection();
            if (burstDashDirection == Vector2.zero) burstDashDirection = Vector2.up;
            SetBurstDashCollider(true);
            burstDashDirection = FindDashAssistDirection(burstDashDirection);
            burstChargeRemaining = 0f;
            burstDashRemaining = burstDashDuration;
            state = FlameState.BurstDashing;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearVelocity = burstDashDirection * burstDashSpeed;
            flameFeedback.HideLaunchRing();
            flameFeedback.PlayLaunch(burstDashDirection);
            Camera.main?.GetComponent< CelesteRoomCamera >()?.ShakeDash();
            afterimageEffect.BeginTrail();
        }

        private void EndBurstDash(bool blocked)
        {
            if (state != FlameState.BurstDashing) return;

            state = FlameState.Free;
            body.gravityScale = initialGravity;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            SetBurstDashCollider(false);
            body.linearVelocity = blocked
                ? Vector2.zero
                : burstDashDirection * (burstDashSpeed * burstDashEndSpeedMultiplier);
            launchProtection = blocked ? 0f : 0.08f;
            afterimageEffect.StopTrail();
            burstDashRemaining = 0f;
            wasGrounded = false;
        }

        private void TriggerIgnitionBurst()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(body.position, ignitionBurstRadius);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out FlammableTile tile)) tile.TryIgnite();
            }

            CreateIgnitionBurstEffect();
        }

        private void CreateIgnitionBurstEffect()
        {
            ClearBurstEffect();

            activeBurstEffect = new GameObject("Ignition Burst Effect");
            LineRenderer line = activeBurstEffect.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) line.sharedMaterial = new Material(shader);
            line.loop = true;
            line.useWorldSpace = true;
            line.positionCount = 48;
            line.numCornerVertices = 3;
            line.widthMultiplier = 0.12f;
            line.sortingOrder = 20;
            StartCoroutine(AnimateBurstCircle(line, body.position));
        }

        private IEnumerator AnimateBurstCircle(LineRenderer line, Vector2 center)
        {
            const int pointCount = 48;
            Vector3[] points = new Vector3[pointCount];
            float elapsed = 0f;

            while (elapsed < ignitionBurstVisualTime && line != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / ignitionBurstVisualTime);
                float radius = Mathf.Lerp(0.2f, ignitionBurstRadius, progress);
                Color color = new Color(1f, Mathf.Lerp(0.9f, 0.25f, progress), 0.05f, 1f - progress);
                line.startColor = line.endColor = color;

                for (int i = 0; i < pointCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / pointCount;
                    points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                line.SetPositions(points);
                yield return null;
            }

            if (line != null && activeBurstEffect == line.gameObject) ClearBurstEffect();
        }

        private void ClearBurstEffect()
        {
            if (activeBurstEffect == null) return;
            LineRenderer line = activeBurstEffect.GetComponent<LineRenderer>();
            Material material = line != null ? line.sharedMaterial : null;
            Destroy(activeBurstEffect);
            if (material != null) Destroy(material);
            activeBurstEffect = null;
        }

        private void TryMoveInsideFire(Vector2 direction)
        {
            if (targetFire == null || !targetFire.IsBurning) return;
            Collider2D[] nearby = Physics2D.OverlapCircleAll(targetFire.AnchorPosition, fireNetworkStepRange);
            FlammableTile bestTile = null;
            float bestScore = float.NegativeInfinity;
            foreach (Collider2D hit in nearby)
            {
                if (!hit.TryGetComponent(out FlammableTile tile) || tile == targetFire || !tile.IsBurning) continue;
                Vector2 offset = tile.AnchorPosition - targetFire.AnchorPosition;
                float distance = offset.magnitude;
                if (distance <= 0.001f || distance > fireNetworkStepRange) continue;
                float match = Vector2.Dot(offset / distance, direction);
                if (match < fireNetworkDirectionDot) continue;
                float score = match * 2f - distance / fireNetworkStepRange;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTile = tile;
                }
            }
            if (bestTile != null) BeginFireTravel(bestTile);
        }

        private void BeginFireTravel(FlammableTile destination)
        {
            SetAbsorbTargetPreview(null);
            flameFeedback.HideLaunchRing();
            Camera.main?.GetComponent<CelesteRoomCamera>()?.ShakeAbsorb();
            FlammableTile source = state == FlameState.Anchored ? targetFire : null;
            bool hasSafeRoute = TryBuildFireTravelPath(
                body.position,
                source,
                destination,
                out Vector2 waypoint,
                out bool useWaypoint);
            if (!hasSafeRoute)
            {
                // A blocked target is still valid: the player travels into the obstacle and ricochets.
                waypoint = default;
                useWaypoint = false;
            }

            jumpRemaining = coyoteRemaining = burstChargeRemaining = 0f;
            normalJump = false;
            wallSpeedRetentionRemaining = 0f;
            travelSourceFire = source;
            targetFire = destination;
            fireTravelWaypoint = waypoint;
            hasFireTravelWaypoint = useWaypoint;
            state = FlameState.Travelling;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = true;
            afterimageEffect.BeginTrail();
        }

        private bool TryGetFireTravelBlockingNormal(Collision2D collision, out Vector2 blockingNormal)
        {
            blockingNormal = Vector2.zero;
            if (targetFire == null) return false;

            Vector2 travelDirection = CurrentFireTravelDestination() - body.position;
            if (travelDirection.sqrMagnitude <= 0.0001f) return false;
            travelDirection.Normalize();

            float mostBlockingDot = -0.2f;
            bool found = false;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                float dot = Vector2.Dot(travelDirection, contact.normal);
                if (dot >= mostBlockingDot) continue;
                mostBlockingDot = dot;
                blockingNormal = contact.normal;
                found = true;
            }

            return found;
        }

        private bool TryCastFireTravelStep(Vector2 direction, float distance, out Vector2 surfaceNormal)
        {
            surfaceNormal = Vector2.zero;
            int obstacleMask = fireTravelObstacleMask.value == 0
                ? Physics2D.AllLayers
                : fireTravelObstacleMask.value;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleMask);
            filter.useTriggers = false;

            Collider2D castCollider = state == FlameState.BurstDashing && burstDashCollider != null
                ? burstDashCollider : bodyCollider;
            int hitCount = castCollider.Cast(direction, filter, fireTravelCastHits, distance + 0.03f);
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = fireTravelCastHits[i];
                if (hit.collider == null || hit.distance <= 0.001f) continue;
                if (Vector2.Dot(direction, hit.normal) >= -0.2f) continue;
                if (hit.distance >= closestDistance) continue;

                closestDistance = hit.distance;
                surfaceNormal = hit.normal;
            }

            return closestDistance < float.PositiveInfinity;
        }

        private Vector2 FindDashAssistDirection(Vector2 desired)
        {
            if (burstDashObstacleAssistDistance <= 0f || burstDashObstacleAssistSteps <= 0)
                return desired;

            if (IsDashDirectionClear(desired)) return desired;
            for (int step = 1; step <= burstDashObstacleAssistSteps; step++)
            {
                float angle = burstDashObstacleAssistAngle * step;
                Vector2 left = Rotate(desired, angle);
                if (IsDashDirectionClear(left)) return left;
                Vector2 right = Rotate(desired, -angle);
                if (IsDashDirectionClear(right)) return right;
            }
            return desired;
        }

        private bool IsDashDirectionClear(Vector2 direction)
        {
            int obstacleMask = fireTravelObstacleMask.value == 0 ? Physics2D.AllLayers : fireTravelObstacleMask.value;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleMask);
            filter.useTriggers = false;
            Collider2D dashCollider = burstDashCollider != null ? burstDashCollider : bodyCollider;
            return dashCollider.Cast(direction, filter, fireTravelCastHits, burstDashObstacleAssistDistance) == 0;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos).normalized;
        }

        private void BounceFromFireTravel(Vector2 surfaceNormal)
        {
            Vector2 incomingDirection = CurrentFireTravelDestination() - body.position;
            if (incomingDirection.sqrMagnitude <= 0.0001f) incomingDirection = -surfaceNormal;
            incomingDirection.Normalize();

            Vector2 bounceDirection = Vector2.Reflect(incomingDirection, surfaceNormal).normalized;
            if (Mathf.Abs(surfaceNormal.x) > 0.5f)
            {
                bounceDirection.y = Mathf.Max(bounceDirection.y, fireTravelWallUpwardBias);
                bounceDirection.Normalize();
            }

            float speed = fireTravelBounceSpeed;
            if (surfaceNormal.y > 0.5f) speed *= fireTravelGroundBounceMultiplier;

            afterimageEffect.StopTrail();
            state = FlameState.Free;
            targetFire = null;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            body.gravityScale = initialGravity;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            SetBurstDashCollider(false);
            bodyCollider.enabled = true;
            transform.localScale = initialScale;
            body.linearVelocity = bounceDirection * speed;
            upwardVelocityBeforeCollision = Mathf.Max(0f, body.linearVelocity.y);
            normalJump = false;
            cornerCorrectionUsed = false;
            wallSpeedRetentionRemaining = 0f;
            launchProtection = 0.16f;
            wasGrounded = false;
            flameFeedback.PlayLaunch(bounceDirection);
            afterimageEffect.PlayTimedTrail(fireTravelBounceAfterimageDuration);
        }

        private Vector2 CurrentFireTravelDestination()
        {
            return hasFireTravelWaypoint ? fireTravelWaypoint : targetFire.AnchorPosition;
        }

        private bool TryBuildFireTravelPath(
            Vector2 start,
            FlammableTile source,
            FlammableTile destination,
            out Vector2 waypoint,
            out bool useWaypoint)
        {
            waypoint = default;
            useWaypoint = false;
            if (destination == null) return false;

            Vector2 end = destination.AnchorPosition;
            if (HasClearFireTravelSegment(start, end, source, destination)) return true;
            if (fireTravelCornerAssistDistance <= 0f) return false;

            Vector2 displacement = end - start;
            float distance = displacement.magnitude;
            if (distance <= 0.001f) return true;

            Vector2 perpendicular = new Vector2(-displacement.y, displacement.x) / distance;
            float step = Mathf.Max(0.05f, fireTravelCornerAssistStep);
            float bestLength = float.PositiveInfinity;
            bool found = false;

            for (float offset = step; offset <= fireTravelCornerAssistDistance + 0.001f; offset += step)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int midpointIndex = 0; midpointIndex < 3; midpointIndex++)
                    {
                        float midpoint = 0.35f + midpointIndex * 0.15f;
                        Vector2 candidate = Vector2.Lerp(start, end, midpoint) + perpendicular * (offset * side);
                        if (!HasClearFireTravelSegment(start, candidate, source, destination)) continue;
                        if (!HasClearFireTravelSegment(candidate, end, source, destination)) continue;

                        float routeLength = Vector2.Distance(start, candidate) + Vector2.Distance(candidate, end);
                        if (routeLength >= bestLength) continue;
                        bestLength = routeLength;
                        waypoint = candidate;
                        found = true;
                    }
                }

                // The first successful offset is the least intrusive correction.
                if (found) break;
            }

            useWaypoint = found;
            return found;
        }

        private bool HasClearFireTravelSegment(
            Vector2 start,
            Vector2 end,
            FlammableTile source,
            FlammableTile destination)
        {
            Vector2 displacement = end - start;
            float distance = displacement.magnitude;
            if (distance <= 0.001f) return true;

            Vector2 probeSize = fireTravelProbeSize * fireTravelCollisionScale;
            Vector2 probeOrigin = start + fireTravelProbeOffset;
            int obstacleMask = fireTravelObstacleMask.value == 0
                ? Physics2D.AllLayers
                : fireTravelObstacleMask.value;
            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                probeOrigin,
                probeSize,
                0f,
                displacement / distance,
                distance,
                obstacleMask);

            foreach (RaycastHit2D hit in hits)
            {
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || hitCollider == bodyCollider || hitCollider.isTrigger) continue;

                FlammableTile hitFire = hitCollider.GetComponentInParent<FlammableTile>();
                if (hitFire == source || hitFire == destination) continue;
                return false;
            }

            return true;
        }

        private void CancelFireTravel()
        {
            afterimageEffect.StopTrail();
            state = FlameState.Free;
            targetFire = null;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            wallSpeedRetentionRemaining = 0f;
            body.gravityScale = initialGravity;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = true;
            transform.localScale = initialScale;
        }

        private void EnterFire()
        {
            afterimageEffect.StopTrail();
            state = FlameState.Anchored;
            body.position = targetFire.AnchorPosition;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = false;
            transform.localScale = Vector3.one * 0.45f;
            airJumpsRemaining = maxAirJumps;
            burstAvailable = true;
            SetAbsorbTargetPreview(null);
            flameFeedback.ShowAnchoredLaunchRing();
        }

        private void LaunchFromFire()
        {
            Vector2 direction = ReadHeldDirection();
            if (direction == Vector2.zero) direction = Vector2.up;
            if (!TryFindSafeFireLaunchPosition(direction, out Vector2 launchPosition)) return;

            flameFeedback.HideLaunchRing();
            state = FlameState.Free;
            targetFire = null;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            wallSpeedRetentionRemaining = 0f;
            body.gravityScale = initialGravity;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            bodyCollider.enabled = true;
            transform.localScale = initialScale;
            body.position = launchPosition;
            body.linearVelocity = direction * launchSpeed;
            upwardVelocityBeforeCollision = Mathf.Max(0f, body.linearVelocity.y);
            cornerCorrectionUsed = false;
            launchProtection = 0.25f;
            flameFeedback.PlayLaunch(direction);
            Camera.main?.GetComponent<CelesteRoomCamera>()?.ShakeFireLaunch();
            afterimageEffect.PlayTimedTrail(launchAfterimageDuration);
            wasGrounded = false;
        }

        private bool TryFindSafeFireLaunchPosition(Vector2 direction, out Vector2 launchPosition)
        {
            Vector2 start = body.position;
            Vector2 side = new Vector2(-direction.y, direction.x);
            launchPosition = start;

            if (IsSafeFireLaunchPosition(start, start + direction * 0.7f))
            {
                launchPosition = start + direction * 0.7f;
                return true;
            }

            for (float offset = 0.08f; offset <= fireLaunchCornerAssistDistance + 0.001f; offset += 0.08f)
            {
                Vector2 first = start + direction * 0.7f + side * offset;
                Vector2 second = start + direction * 0.7f - side * offset;
                if (IsSafeFireLaunchPosition(start, first))
                {
                    launchPosition = first;
                    return true;
                }
                if (IsSafeFireLaunchPosition(start, second))
                {
                    launchPosition = second;
                    return true;
                }
            }

            return false;
        }

        private bool IsSafeFireLaunchPosition(Vector2 start, Vector2 end)
        {
            Vector2 displacement = end - start;
            Vector2 size = fireTravelProbeSize;
            Vector2 origin = start + fireTravelProbeOffset;
            Vector2 destination = end + fireTravelProbeOffset;
            RaycastHit2D[] sweepHits = Physics2D.BoxCastAll(
                origin, size, 0f, displacement.normalized, displacement.magnitude);
            foreach (RaycastHit2D hit in sweepHits)
            {
                if (hit.collider == null || hit.collider == bodyCollider || hit.collider.isTrigger) continue;
                if (hit.distance > 0.01f) return false;
            }

            Collider2D[] overlaps = Physics2D.OverlapBoxAll(destination, size, 0f);
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap != bodyCollider && !overlap.isTrigger) return false;
            }
            return true;
        }

        public void ResetAt(Vector2 position)
        {
            if (body == null || bodyCollider == null) return;

            state = FlameState.Free;
            targetFire = null;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            horizontalInput = jumpRemaining = coyoteRemaining = launchProtection = burstChargeRemaining = burstDashRemaining = 0f;
            burstDashDirection = Vector2.zero;
            normalJump = jumpReleased = jumpHeld = cornerCorrectionUsed = false;
            upwardVelocityBeforeCollision = 0f;
            horizontalVelocityBeforeCollision = retainedWallSpeedX = wallSpeedRetentionRemaining = 0f;
            airJumpsRemaining = maxAirJumps;
            burstAvailable = true;
            groundStateInitialized = false;
            wasGrounded = false;
            StopAllCoroutines();
            ClearBurstEffect();
            flameFeedback?.ResetFeedback();
            afterimageEffect?.StopTrail(true);
            transform.localScale = initialScale;
            body.gravityScale = initialGravity;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            bodyCollider.enabled = true;
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public void KillAndRespawn()
        {
            Vector2 position = respawnPoint != null
                ? (Vector2)respawnPoint.position
                : initialSpawnPosition;
            ResetAt(position);
        }

        public void SetRespawnPoint(Transform newRespawnPoint)
        {
            respawnPoint = newRespawnPoint;
        }

        private void SetBurstDashCollider(bool dashActive)
        {
            if (burstDashCollider == null) return;
            bodyCollider.enabled = !dashActive;
            burstDashCollider.enabled = dashActive;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, ignitionBurstRadius);
        }
    }
}
