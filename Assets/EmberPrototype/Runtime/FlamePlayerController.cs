using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmberPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class FlamePlayerController : MonoBehaviour
    {
        private enum FlameState { Free, Bursting, Travelling, Anchored }

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

        [Header("Respawn")]
        [Tooltip("Optional. If empty, the player's position when the scene starts is used.")]
        [SerializeField] private Transform respawnPoint;

        [Header("Fire Absorb (X)")]
        [SerializeField, Range(-1f, 1f)] private float fireTargetDirectionDot = 0.35f;

        [Header("Ignition Burst (C)")]
        [SerializeField, Min(0.1f)] private float ignitionBurstRadius = 1.5f;
        [InspectorName("Ignition Burst Pause Time")]
        [SerializeField, Min(0f)] private float ignitionBurstChargeTime = 0.5f;
        [SerializeField, Min(0.01f)] private float ignitionBurstVisualTime = 0.18f;

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
        [SerializeField, Min(0.1f)] private float fireNetworkStepRange = 1.65f;
        [SerializeField, Range(-1f, 1f)] private float fireNetworkDirectionDot = 0.45f;
        [SerializeField, Min(0f)] private float launchSpeed = 15f;
        [SerializeField, Min(0f)] private float launchAfterimageDuration = 0.22f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private FlameState state;
        private FlammableTile targetFire;
        private FlammableTile travelSourceFire;
        private float horizontalInput;
        private float coyoteRemaining;
        private float jumpRemaining;
        private float launchProtection;
        private float burstChargeRemaining;
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
        private GameObject activeBurstEffect;
        private PlayerFlameFeedback flameFeedback;
        private PlayerAfterimageEffect afterimageEffect;
        private bool groundStateInitialized;
        private bool wasGrounded;
        private Vector2 fireTravelProbeSize;
        private Vector2 fireTravelProbeOffset;
        private Vector2 fireTravelWaypoint;
        private bool hasFireTravelWaypoint;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();

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
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ClearBurstEffect();
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

            if (state == FlameState.Travelling || state == FlameState.Bursting) return;

            Vector2 heldDirection = ReadHeldDirection();
            horizontalInput = heldDirection.x;
            jumpHeld = Keyboard.current.zKey.isPressed;
            if (horizontalInput != 0f) facingDirection = horizontalInput > 0f ? 1 : -1;

            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                jumpRemaining = jumpBuffer;
                jumpReleased = false;
            }
            if (Keyboard.current.zKey.wasReleasedThisFrame) jumpReleased = true;
            if (Keyboard.current.xKey.wasPressedThisFrame) TryAbsorb(heldDirection);
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
                }
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

            body.MovePosition(Vector2.MoveTowards(body.position, destination, fireTravelSpeed * Time.fixedDeltaTime));
            if (!hasFireTravelWaypoint && Vector2.SqrMagnitude(targetFire.AnchorPosition - body.position) <= 0.03f)
            {
                EnterFire();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleFlammableContact(collision.collider);

            if (state == FlameState.Travelling && CollisionBlocksFireTravel(collision))
            {
                CancelFireTravel();
                return;
            }

            TryCornerCorrection(collision);
        }

        private void OnCollisionStay2D(Collision2D collision) => TryCornerCorrection(collision);
        private void OnTriggerEnter2D(Collider2D other) => HandleFlammableContact(other);

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

        private void TryAbsorb(Vector2 inputDirection)
        {
            Vector2 direction = inputDirection == Vector2.zero ? Vector2.right * facingDirection : inputDirection;
            FlammableTile selected = FindBurningFire(direction);
            if (selected != null) BeginFireTravel(selected);
        }

        private FlammableTile FindBurningFire(Vector2 direction)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(body.position, fireTravelRange);
            FlammableTile selected = null;
            float bestScore = float.NegativeInfinity;
            foreach (Collider2D hit in hits)
            {
                if (!hit.TryGetComponent(out FlammableTile tile) || !tile.IsBurning) continue;
                Vector2 offset = tile.AnchorPosition - body.position;
                float distance = offset.magnitude;
                if (distance <= 0.05f) continue;
                float directionMatch = Vector2.Dot(offset / distance, direction);
                if (directionMatch < fireTargetDirectionDot) continue;
                if (!CanBuildFireTravelPath(body.position, null, tile)) continue;
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
            if (!burstAvailable || IsGrounded()) return;
            burstAvailable = false;
            TriggerIgnitionBurst();
            flameFeedback.PlayBurst();
            state = FlameState.Bursting;
            burstChargeRemaining = ignitionBurstChargeTime;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            normalJump = false;
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
                if (!CanBuildFireTravelPath(targetFire.AnchorPosition, targetFire, tile)) continue;
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
            FlammableTile source = state == FlameState.Anchored ? targetFire : null;
            if (!TryBuildFireTravelPath(body.position, source, destination, out Vector2 waypoint, out bool useWaypoint))
            {
                return;
            }

            jumpRemaining = coyoteRemaining = burstChargeRemaining = 0f;
            normalJump = false;
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

        private bool CollisionBlocksFireTravel(Collision2D collision)
        {
            if (targetFire == null) return true;

            Vector2 travelDirection = CurrentFireTravelDestination() - body.position;
            if (travelDirection.sqrMagnitude <= 0.0001f) return false;
            travelDirection.Normalize();

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                if (Vector2.Dot(travelDirection, contact.normal) < -0.2f) return true;
            }

            return false;
        }

        private Vector2 CurrentFireTravelDestination()
        {
            return hasFireTravelWaypoint ? fireTravelWaypoint : targetFire.AnchorPosition;
        }

        private bool CanBuildFireTravelPath(Vector2 start, FlammableTile source, FlammableTile destination)
        {
            return TryBuildFireTravelPath(start, source, destination, out _, out _);
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
        }

        private void LaunchFromFire()
        {
            Vector2 direction = ReadHeldDirection();
            if (direction == Vector2.zero) direction = Vector2.up;
            state = FlameState.Free;
            targetFire = null;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            body.gravityScale = initialGravity;
            body.collisionDetectionMode = initialCollisionDetectionMode;
            bodyCollider.enabled = true;
            transform.localScale = initialScale;
            body.position += direction * 0.7f;
            body.linearVelocity = direction * launchSpeed;
            upwardVelocityBeforeCollision = Mathf.Max(0f, body.linearVelocity.y);
            cornerCorrectionUsed = false;
            launchProtection = 0.25f;
            flameFeedback.PlayLaunch(direction);
            afterimageEffect.PlayTimedTrail(launchAfterimageDuration);
            wasGrounded = false;
        }

        public void ResetAt(Vector2 position)
        {
            if (body == null || bodyCollider == null) return;

            state = FlameState.Free;
            targetFire = null;
            travelSourceFire = null;
            hasFireTravelWaypoint = false;
            horizontalInput = jumpRemaining = coyoteRemaining = launchProtection = burstChargeRemaining = 0f;
            normalJump = jumpReleased = jumpHeld = cornerCorrectionUsed = false;
            upwardVelocityBeforeCollision = 0f;
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, ignitionBurstRadius);
        }
    }
}
