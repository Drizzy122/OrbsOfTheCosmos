using KBCore.Refs;
using ImprovedTimers;
using UnityEngine;
using UnityUtils.StateMachine;

namespace Platformer
{
    public class PlayerMovement : ValidatedMonoBehaviour
    {
        #region Variables
        [field: Header("References")] 
        [field: SerializeField, Anywhere] InputReader input;
        [field: SerializeField, Self] Rigidbody rb;
        [field: SerializeField, Self] GroundChecker groundChecker;
        [field: SerializeField, Self] Animator animator;
        [field: SerializeField, Self] Health playerHealth;
        [field: SerializeField, Self]  public PlayerCombat combat;
        [field: SerializeField, Self]  public CommandManager commandManager;
        [field: SerializeField, Self] GlideStamina glideStamina;
        [field: SerializeField, Self] public PlayerInteraction interaction;
        
        [Header("Movement Settings")]
        [field: SerializeField] [Range(0,20)] float moveSpeed = 20f;
        [field: SerializeField] [Range(0,50)] float sprintSpeed = 40f;
        [field: SerializeField] [Range(0,1000)] float rotationSpeed = 1000f;
        public bool IsSprinting { get; private set; }
        [field: SerializeField] float smoothTime = 0.2f;

        [field: Header("Aim Settings")]
        [field: SerializeField] [Range(0, 10)] float aimMoveSpeed = 4f;
        [field: SerializeField] float aimBlendDamp = 0.1f;
        PlayerAim playerAim;

        // While aiming, PlayerAim owns the facing (camera yaw) — movement must not rotate us
        public bool IsAimingActive => playerAim != null && playerAim.IsAiming;

        [field: Header("Jump Settings")]
        [field: SerializeField] float jumpForce = 10f;
        [field: SerializeField] float jumpDuration = 0.5f;
        [field: HideInInspector] int jumpCount = 2;
        [field: HideInInspector] int remainingJumps = 2;
        [field: SerializeField] float gravityMultiplier = 3f;
        [field: SerializeField] int maxFallSpeed = 10;

        [field: Header("Dodge Settings")]
        [field: SerializeField] float dodgeForce = 12f;
        [Tooltip("How long the dodge state lasts. The dodge clips are 0.58s — much shorter and the animation gets cut off.")]
        [field: SerializeField] float dodgeDuration = 0.55f;
        Vector3 dodgeDirection;
        
        [field: Header("Glide Settings")] 
        [field: SerializeField] float glideMoveSpeed = 2;
        [field: SerializeField] float glideFallSpeed = 0.1f;
        
        [field: SerializeField] public float glideBoost = 1;
        [field: SerializeField] float glideBoostDecayRate = 0.02f;

        [Header("Sprint Glide Entry Boost (independent of orb/ring boost)")]
        [field: SerializeField] float sprintGlideBoost = 1.5f;
        [field: SerializeField] float sprintGlideBoostDecay = 0.02f;
        float currentSprintBoost = 1f;
        
        [field: SerializeField] GameObject defaultMesh;  
        [field: SerializeField] GameObject glidingMesh;
        
        [field: Header("Wall Climb Settings")]
        [field: SerializeField] float wallCheckDist = 1f;
        [field: SerializeField] float wallClimbMoveSpeed = 5f;
        [field: SerializeField] LayerMask wallClimbLayer;
        [HideInInspector] public bool wallClimbimg;
        [HideInInspector] bool[] wallClimbChecks;
        [HideInInspector] public Vector3 wallClimbPos;
        Vector3 wallClimbNormal;
        Vector3 wallClimbTargetPos;
       
        
        [field: Header("Locomotion Animation")]
        [Tooltip("World speed (m/s) at which the walk clip plays pure — blend tree threshold 0.5.")]
        [field: SerializeField] float animWalkSpeed = 6f;
        [Tooltip("World speed (m/s) at which the run clip plays pure — blend tree threshold 1.")]
        [field: SerializeField] float animRunSpeed = 20f;
        [Tooltip("How much to speed up clips to keep the stride planted. 0 = authored cadence (skates), 1 = fully matched to ground speed.")]
        [field: SerializeField] [Range(0f, 1f)] float strideMatching = 1f;
        [Tooltip("Cap on playback scaling, so absurd speeds don't turn into a blur.")]
        [field: SerializeField] [Range(1f, 6f)] float maxStrideMultiplier = 2.5f;
        [field: SerializeField] float locomotionBlendDamp = 0.1f;

        // Measured from the clips' root motion: distance travelled / clip length.
        const float WalkClipSpeed = 1.57f;
        const float RunClipSpeed = 4.14f;

        [field: Header("More variables")]
        const float ZeroF = 0f;
        float currentSpeed;
        float velocity;
        float jumpVelocity;
        Vector3 movement;
        Transform mainCam;
        
        [field: Header("Animation Cooldown Settings")]
        [field: SerializeField] float hurtDuration = 0.4f;
        [field: SerializeField] float jumpCooldown = 0f;
        [field: SerializeField] public float glideCoolDown = 0.5f;
        [field: SerializeField] float dodgeCooldown = 0.5f;

        [field: Header("Timers")]
        CountdownTimer jumpTimer;
        CountdownTimer jumpCooldownTimer;
        CountdownTimer dodgeTimer;
        CountdownTimer dodgeCooldownTimer;
        CountdownTimer glideTimer;
        CountdownTimer hurtTimer;
      

        StateMachine stateMachine;
        
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int SpeedMult = Animator.StringToHash("SpeedMult");
        static readonly int VelX = Animator.StringToHash("VelX");
        static readonly int VelZ = Animator.StringToHash("VelZ");
        bool hasSpeedMult;

        #endregion
        void Awake()
        {
            mainCam = Camera.main.transform;
            rb.freezeRotation = true;
            glideStamina = GetComponent<GlideStamina>();
            playerAim = GetComponent<PlayerAim>();
            // Stride matching is optional — only drive SpeedMult if the controller actually declares it.
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == SpeedMult && p.type == AnimatorControllerParameterType.Float) hasSpeedMult = true;
            }
            SetupTimers();
            SetupStateMachine();
        }
        void Start() => input.EnablePlayerActions();

        void Update()
        {
            movement = new Vector3(input.Direction.x, 0f, input.Direction.y);
            stateMachine.Update();
            UpdateAnimator();
        }
        void FixedUpdate()
        {
            stateMachine.FixedUpdate();
            WallClimbCheck();
        }
        /// <summary>Drives the Locomotion blend tree from the body's ACTUAL planar speed rather
        /// than stick tilt, so sprinting reads as sprinting and gliding doesn't blow past the tree.</summary>
        void UpdateAnimator()
        {
            float worldSpeed = new Vector3(rb.linearVelocity.x, ZeroF, rb.linearVelocity.z).magnitude;

            // Map world speed onto the tree's authored thresholds: 0 idle / 0.5 walk / 1 run.
            float blend = worldSpeed <= animWalkSpeed
                ? Mathf.Lerp(ZeroF, 0.5f, animWalkSpeed > ZeroF ? worldSpeed / animWalkSpeed : ZeroF)
                : Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(animWalkSpeed, animRunSpeed, worldSpeed));
            blend = Mathf.Clamp01(blend);

            animator.SetFloat(Speed, blend, locomotionBlendDamp, Time.deltaTime);
            if (hasSpeedMult) animator.SetFloat(SpeedMult, StrideMultiplier(worldSpeed, blend));
        }

        /// <summary>The clips travel at a fixed authored pace (walk ~1.6 m/s, run ~4.1 m/s). If the
        /// body moves faster than that, the feet skate — scaling playback to the ratio plants them.</summary>
        float StrideMultiplier(float worldSpeed, float blend)
        {
            if (worldSpeed < 0.01f) return 1f;
            float clipSpeed = Mathf.Lerp(WalkClipSpeed, RunClipSpeed, Mathf.InverseLerp(0.5f, 1f, blend));
            if (clipSpeed <= ZeroF) return 1f;
            float matched = Mathf.Lerp(1f, worldSpeed / clipSpeed, strideMatching);
            return Mathf.Clamp(matched, 1f / maxStrideMultiplier, maxStrideMultiplier);
        }
       

        #region StateMachine
        private void SetupStateMachine()
        {
            // State Machine
            stateMachine = new StateMachine();

            // Declare states
            var locomotionState = new LocomotionState(this, animator);
            var sprintState = new SprintState(this, animator);
            var jumpState = new JumpState(this, animator);
            var doubleJumpState = new DoubleJumpState(this, animator);
            var glideState = new GlideState(this, animator);
            var dodgeState = new DodgeState(this, animator);
            var attackState = new AttackState(this, animator);
            var spinAttackState = new BlastAttackState(this, animator);
            var deathState = new DeathState(this, animator, playerHealth);
            var teleportState = new TeleportState(this, animator);
            var wallClimbState = new WallClimbState(this, animator);
            var hurtState = new HurtState(this, animator);
            var aimState = new AimState(this, animator);

            // Define transitions for aim strafe mode (grounded only)
            At(locomotionState, aimState, new FuncPredicate(() => IsAimingActive && groundChecker.IsGrounded));
            At(sprintState, aimState, new FuncPredicate(() => IsAimingActive));
            At(aimState, locomotionState, new FuncPredicate(() => !IsAimingActive));
            At(aimState, jumpState, new FuncPredicate(() => jumpTimer.IsRunning || !groundChecker.IsGrounded));
            At(aimState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));
            At(aimState, attackState, new FuncPredicate(() => combat.IsAttacking));
            At(aimState, spinAttackState, new FuncPredicate(() => combat.IsBlastAttacking));

            // An aim-dodge returns straight to aim (before the dodge→jump fallback
            // below, so we don't flash through the jump animation)
            At(dodgeState, aimState, new FuncPredicate(() =>
                !dodgeTimer.IsRunning && IsAimingActive && groundChecker.IsGrounded));

            // Landing while still holding aim goes straight back into AimState
            // (the locomotion catch-all skips aiming players, like it does sprinting ones)
            At(jumpState, aimState, new FuncPredicate(() =>
                groundChecker.IsGrounded && !jumpTimer.IsRunning && IsAimingActive));
            At(doubleJumpState, aimState, new FuncPredicate(() =>
                groundChecker.IsGrounded && !jumpTimer.IsRunning && IsAimingActive));

            // Define transitions for sprint
            At(locomotionState, sprintState, new FuncPredicate(() => IsSprinting && movement.sqrMagnitude > 0f));
            At(sprintState, locomotionState, new FuncPredicate(() => !IsSprinting || movement.sqrMagnitude <= 0f));
            At(sprintState, jumpState, new FuncPredicate(() => jumpTimer.IsRunning || !groundChecker.IsGrounded));
            At(sprintState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));
            At(sprintState, attackState, new FuncPredicate(() => combat.IsAttacking));
            At(sprintState, spinAttackState, new FuncPredicate(() => combat.IsBlastAttacking));

            // Landing while still holding sprint goes straight back into SprintState —
            // the locomotion catch-all intentionally skips sprinting players, so without
            // these the player would stay stuck in the jump animation after landing.
            At(jumpState, sprintState, new FuncPredicate(() =>
                groundChecker.IsGrounded && !jumpTimer.IsRunning && IsSprinting && movement.sqrMagnitude > 0f));
            At(doubleJumpState, sprintState, new FuncPredicate(() =>
                groundChecker.IsGrounded && !jumpTimer.IsRunning && IsSprinting && movement.sqrMagnitude > 0f));

            // Define transitions for jump
            At(locomotionState, jumpState, new FuncPredicate(() => jumpTimer.IsRunning));
            At(locomotionState, jumpState, new FuncPredicate(() => !groundChecker.IsGrounded));

            // Define transitions for double jump
            At(doubleJumpState, glideState, new FuncPredicate(() => glideTimer.IsRunning));
            At(jumpState, doubleJumpState, new FuncPredicate(() => jumpTimer.IsRunning && remainingJumps <= jumpCount - 2));
            At(doubleJumpState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));

            // Define transitions for Dodge
            At(locomotionState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));
            At(glideState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));
            At(jumpState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));

            // Dodge-cancel: registered BEFORE attack→locomotion so it wins the same
            // frame OnDodge cancels the swing (transitions are checked in order)
            At(attackState, dodgeState, new FuncPredicate(() => dodgeTimer.IsRunning));

            // Dodge flows into a queued attack; the jump fallback must not swallow it
            At(dodgeState, attackState, new FuncPredicate(() => !dodgeTimer.IsRunning && combat.IsAttacking));
            At(dodgeState, jumpState, new FuncPredicate(() => !dodgeTimer.IsRunning && !combat.IsAttacking));
            
            // Define transitions for attack
            At(locomotionState, attackState, new FuncPredicate(() => combat.IsAttacking));
            At(attackState, locomotionState, new FuncPredicate(() => !combat.IsAttacking));;

            At(locomotionState, spinAttackState, new FuncPredicate(() => combat.IsBlastAttacking));
            At(spinAttackState, locomotionState, new FuncPredicate(() => !combat.IsBlastAttacking));

            // Enable Spin Attack while jumping
            At(jumpState, spinAttackState, new FuncPredicate(() => combat.IsBlastAttacking));
            At(spinAttackState, jumpState, new FuncPredicate(() => !combat.IsBlastAttacking || jumpTimer.IsRunning));
            
            // Define transitions for glide
            At(jumpState, glideState, new FuncPredicate(() => glideTimer.IsRunning));
            At(dodgeState, glideState, new FuncPredicate(() => glideTimer.IsRunning));
            At(glideState, jumpState, new FuncPredicate(() => !glideTimer.IsRunning));
            
            // Define transitions for wall climb
            At(jumpState, wallClimbState, new FuncPredicate(() => wallClimbimg));
            At(doubleJumpState, wallClimbState, new FuncPredicate(() => wallClimbimg));
            At(wallClimbState, doubleJumpState, new FuncPredicate(() => !wallClimbimg));
            
            // Definne transition for teleportation
             At(teleportState, locomotionState, new FuncPredicate(() => !interaction.isTeleporting));

             // Set initial state
            Any(teleportState, new FuncPredicate(() => interaction.isTeleporting));
            Any(hurtState, new FuncPredicate(() => hurtTimer.IsRunning && !playerHealth.isDead));
            Any(deathState, new FuncPredicate(() => playerHealth.isDead));
            Any(locomotionState, new FuncPredicate(ReturnToLocomotionState));
            
            stateMachine.SetState(locomotionState);
        }

        bool ReturnToLocomotionState()
        {
            return groundChecker.IsGrounded
                   && !playerHealth.isDead
                   && !combat.IsAttacking
                   && !combat.IsBlastAttacking
                   && !jumpTimer.IsRunning
                   && !dodgeTimer.IsRunning
                   && !glideTimer.IsRunning
                   && !interaction.isTeleporting
                   && !hurtTimer.IsRunning
                   && !IsAimingActive
                   && !(IsSprinting && movement.sqrMagnitude > 0f);

        }
        void At(IState from, IState to, FuncPredicate condition) => stateMachine.AddTransition(from, to, condition);
        void Any(IState to, FuncPredicate condition) => stateMachine.AddAnyTransition(to, condition);
        #endregion

        #region Timers
        
        void SetupTimers()
        {
            // Setup timers
            jumpTimer = new CountdownTimer(jumpDuration);
            jumpCooldownTimer = new CountdownTimer(jumpCooldown);
            glideTimer = new CountdownTimer(glideCoolDown);
            dodgeTimer = new CountdownTimer(dodgeDuration);
            dodgeCooldownTimer = new CountdownTimer(dodgeCooldown);

            jumpTimer.OnTimerStart += () => jumpVelocity = jumpForce;
            jumpTimer.OnTimerStop += () => jumpCooldownTimer.Start();
            hurtTimer = new CountdownTimer(0f);

            // The whole (short) dodge is invulnerable — that's what makes it a
            // dodge and not a dash. HandlePlayerHurt stopping the timer also
            // cleanly ends the i-frames.
            dodgeTimer.OnTimerStart += () => playerHealth.SetInvulnerable(true);
            dodgeTimer.OnTimerStop += () =>
            {
                playerHealth.SetInvulnerable(false);
                dodgeCooldownTimer.Start();
            };
        }
        #endregion

        #region Movement
        public void HandleMovement()
        {
            // Rotate movement direction to match camera rotation
            var adjustedDirection = Quaternion.AngleAxis(mainCam.eulerAngles.y, Vector3.up) * movement;

            if (adjustedDirection.magnitude > ZeroF)
            {
                HandleRotation(adjustedDirection);
                if (glideTimer.IsRunning) adjustedDirection *= (glideMoveSpeed * currentSprintBoost);
                HandleHorizontalMovement(adjustedDirection * glideBoost);
                SmoothSpeed(adjustedDirection.magnitude);
            }
            else
            {
                SmoothSpeed(ZeroF);

                // Reset horizontal velocity for a snappy stop
                rb.linearVelocity = new Vector3(ZeroF, rb.linearVelocity.y, ZeroF);
            }
            
        }
        void HandleHorizontalMovement(Vector3 adjustedDirection)
        {
            // Sprint applies on the ground and through the full jump arc (ascent + descent).
            // Glide overrides it so the orb/ring boost system stays in charge while gliding.
            bool canSprint = IsSprinting && !glideTimer.IsRunning;
            float activeSpeed = canSprint ? sprintSpeed : moveSpeed;
            Vector3 velocity = adjustedDirection * activeSpeed;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
        void HandleRotation(Vector3 adjustedDirection)
        {
            // Adjust rotation to match movement Direction
            var targetRotation = Quaternion.LookRotation(adjustedDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            // transform.LookAt(transform.position + adjustedDirection);
        }

        /// <summary>Strafe movement for AimState: camera-relative walk with NO body
        /// rotation (PlayerAim keeps us facing the camera), driving the AimAnims
        /// blend tree via VelX/VelZ in body-local space.</summary>
        public void HandleAimMovement()
        {
            var adjustedDirection = Quaternion.AngleAxis(mainCam.eulerAngles.y, Vector3.up) * movement;

            if (adjustedDirection.magnitude > ZeroF)
            {
                Vector3 velocity = adjustedDirection * aimMoveSpeed;
                rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
                SmoothSpeed(adjustedDirection.magnitude);
            }
            else
            {
                SmoothSpeed(ZeroF);
                rb.linearVelocity = new Vector3(ZeroF, rb.linearVelocity.y, ZeroF);
            }

            // Blend positions are authored in body space: +Z walk forward, +X strafe right
            Vector3 local = transform.InverseTransformDirection(adjustedDirection);
            animator.SetFloat(VelX, local.x, aimBlendDamp, Time.fixedDeltaTime);
            animator.SetFloat(VelZ, local.z, aimBlendDamp, Time.fixedDeltaTime);
        }

        public void ResetAimBlend()
        {
            animator.SetFloat(VelX, ZeroF);
            animator.SetFloat(VelZ, ZeroF);
        }
        void SmoothSpeed(float value)
        {
            currentSpeed = Mathf.SmoothDamp(currentSpeed, value, ref velocity, smoothTime);
        }
        // NEW: Gets the exact direction the player is trying to move relative to the camera
        public Vector3 GetAdjustedMovementDirection()
        {
            var adjustedDirection = Quaternion.AngleAxis(mainCam.eulerAngles.y, Vector3.up) * movement;
            return adjustedDirection.normalized;
        }
        
        public void StopMovement()
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            // You'll need to use reflection or just set currentSpeed to 0 if it's accessible!
            // Since currentSpeed is private in PlayerController, we can just set it here:
            currentSpeed = 0f; 
        }
        
        public void OnSprint(bool performed)
        {
            IsSprinting = performed;
        }

        void HandlePlayerHurt(float knockbackTime)
        {
            if (!playerHealth.isDead)
            {
                // Use knockbackTime if the enemy provided one, otherwise use our new short hurtDuration!
                float stunDuration = knockbackTime > 0 ? knockbackTime : hurtDuration;
                hurtTimer.Reset(stunDuration);
                hurtTimer.Start();

                // Interrupt all active timers so HurtState gets clean physics
                jumpTimer.Stop();
                dodgeTimer.Stop();
                combat.CancelActions();
            }
        }
        
        public bool IsGliding => glideTimer != null && glideTimer.IsRunning;

        public bool IsGrounded => groundChecker.IsGrounded;

        // True only while actually sprint-moving (grounded + giving input), not just holding the key.
        // Aiming never sprints (AimState caps speed at aimMoveSpeed), so the
        // wind effect must ignore the held sprint button while aiming.
        public bool IsSprintingActively => IsSprinting && groundChecker.IsGrounded && movement.sqrMagnitude > 0.01f && !IsAimingActive;
        #endregion

        #region WallClimb
        void OnWallClimb(bool performed)
        {
            if (performed)
            {
                if (wallClimbimg)
                {
                    wallClimbimg = false;
                    if (remainingJumps > 0)
                    {
                        OnJump(true);
                    }
                    else
                    {
                        remainingJumps++;
                        OnJump(true);
                    }
                }
                else
                {
                    if (wallClimbChecks[5])
                    {
                        wallClimbimg = true;
                    }
                }
            }
        }
        public void HandleWallClimb()
        {
            rb.linearVelocity = Vector3.zero;
            transform.position = wallClimbPos;

            transform.LookAt(transform.position - wallClimbNormal);
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);

            if (movement.magnitude > 0)
            {
                if (movement.z > 0)
                {
                    wallClimbPos += transform.up * 0.01f * wallClimbMoveSpeed;
                }
                else if (movement.z < 0)
                {
                    wallClimbPos -= transform.up * 0.01f * wallClimbMoveSpeed;
                }
         
                if (movement.x > 0)
                {
                    wallClimbPos += transform.right * 0.01f * wallClimbMoveSpeed;
                }
                else if (movement.x < 0)
                {
                    wallClimbPos -= transform.right * 0.01f * wallClimbMoveSpeed;
                }

                if (!wallClimbChecks[5] && ! wallClimbChecks[2])
                {
                    wallClimbimg = false;
                }
            }

            currentSpeed = movement.magnitude;
        }
        /// <summary>
        /// Wallclimb Checks go in this order:
        /// 0 = spherecast in front to see if wallclimbing can be triggered
        /// 1 = above
        /// 2 = below
        /// 3 = right
        /// 4 = left
        /// 5 = raycast to find normal of wall found in check #0
        /// </summary>
        void WallClimbCheck()
        {
            wallClimbChecks = new bool[6];
            for (int i = 0; i < wallClimbChecks.Length; i++)
            {
                wallClimbChecks[i] = false;
            }

            wallClimbChecks[0] = (Physics.SphereCastAll(
                transform.position + transform.forward * wallCheckDist + transform.up, 
                0.5f,
                transform.forward,
                0.1f,
                wallClimbLayer).Length > 0);

            wallClimbChecks[1] = Physics.Raycast(transform.position + transform.up * 2, transform.forward, 1, wallClimbLayer);
            wallClimbChecks[2] = Physics.Raycast(transform.position - transform.up * 0.6f, transform.forward, 1, wallClimbLayer);
            
            if (wallClimbChecks[0])
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position + transform.up * 0.5f, transform.forward, out hit, 1, wallClimbLayer))
                {
                    wallClimbChecks[5] = true;
                    wallClimbTargetPos = hit.point;
                    wallClimbNormal = hit.normal;
                    
                    
                    float dot = Vector3.Dot(transform.forward, -wallClimbNormal);
                    if (!wallClimbimg && movement.magnitude > 0 && !jumpTimer.IsRunning && dot > 0.5f)
                    {
                        wallClimbimg = true;
                        wallClimbPos = hit.point + (hit.normal * 0.2f);
                    }
                }
            }
            else
            {
                wallClimbNormal = Vector3.zero;
                wallClimbTargetPos = Vector3.zero;
                if (wallClimbimg) wallClimbimg = false;
            }
        }
        #endregion

        #region Dodge

        void OnDodge(bool performed)
        {
            // A dodge is committed: it can't be held, steered, or cancelled early
            if (!performed || dodgeTimer.IsRunning || dodgeCooldownTimer.IsRunning) return;
            if (wallClimbimg || playerHealth.isDead) return;

            // Dodge-cancel: kill an in-flight swing (and its pending lunge damage).
            // Previously this fell through silently — the dodge timer ran and granted
            // i-frames without the state machine ever leaving AttackState.
            if (combat.IsAttacking) combat.CancelActions();

            // Lock in the escape direction: current input (camera-relative),
            // or straight ahead when standing still.
            Vector3 inputDir = Quaternion.AngleAxis(mainCam.eulerAngles.y, Vector3.up) * movement;
            dodgeDirection = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : transform.forward;

            // Snap to face the dodge — except while aiming, where the dodge is a
            // strafe and PlayerAim keeps the body squared to the camera.
            if (!IsAimingActive) transform.rotation = Quaternion.LookRotation(dodgeDirection);

            dodgeTimer.Start();
        }

        /// <summary>Called by DodgeState.OnEnter — AFTER the previous state's
        /// OnExit, so AimState's blend reset can't stomp these values. Picks the
        /// directional dodge clip in body-local space: facing the dodge = forward
        /// clip; aiming = 8-way strafe dodges.</summary>
        public void ApplyDodgeBlend()
        {
            Vector3 localDodge = transform.InverseTransformDirection(dodgeDirection);
            animator.SetFloat(VelX, localDodge.x);
            animator.SetFloat(VelZ, localDodge.z);
        }

        /// <summary>Called by DodgeState every physics tick: a fixed-direction
        /// burst that tapers off over the dodge (Progress runs 1 → 0), so the
        /// travel is front-loaded and the tail of the animation is recovery.</summary>
        public void HandleDodge()
        {
            float burst = dodgeForce * dodgeTimer.Progress;
            rb.linearVelocity = new Vector3(
                dodgeDirection.x * burst,
                rb.linearVelocity.y,
                dodgeDirection.z * burst);
        }
        #endregion

        #region Jump
        void OnJump(bool performed)
        {
            if (wallClimbimg)
            {
                OnWallClimb(true);
                return;
            }

            if (performed && groundChecker.IsGrounded)
            {
                remainingJumps = jumpCount;
            }

            // Every press gives the full jump arc — no holding to go higher. A press
            // mid-rise restarts the timer, which is what makes the double jump fire
            // without needing to release the button first.
            if (performed && !jumpCooldownTimer.IsRunning && remainingJumps > 0)
            {
                remainingJumps--;
                jumpTimer.Start();
                AudioManager.instance.PlayOneShot(FMODEvents.instance.playerJump, this.transform.position);
            }
        }
        public void HandleJump()
        {
            // if not jumping and grounded, keep jump velocity at 0
            if (!jumpTimer.IsRunning && groundChecker.IsGrounded)
            {
                jumpVelocity = ZeroF;
                //jumpTimer.Stop();
                return;
            }

            // if jumping or falling calculate velocity
            if (!jumpTimer.IsRunning)
            {
                // Gravity takes over
                jumpVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;
                jumpVelocity = Mathf.Clamp(jumpVelocity, -maxFallSpeed, maxFallSpeed);
            }

            // Apply velocity
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpVelocity, rb.linearVelocity.z);
        }
        #endregion

        #region Glide
        public void OnGlide(bool performed)
        {
            if (performed)
            {
                if (!glideTimer.IsRunning && !groundChecker.IsGrounded)
                {
                    glideTimer.Start();
                    glideBoost = 0;
                    currentSprintBoost = IsSprinting ? sprintGlideBoost : 1f;
                    jumpTimer.Stop();
                    glideStamina.StartGlide();
                    // Enable the gliding mesh and disable the default mesh
                    glidingMesh.SetActive(true);
                    defaultMesh.SetActive(false);
                }
            }
            else if (!performed && glideTimer.IsRunning)
            {
                glideStamina?.StopGlide();
                glideTimer.Stop();
                // Revert back to the default mesh
                glidingMesh.SetActive(false);
                defaultMesh.SetActive(true);
            }
        }
        public void HandleGlide()
        {
            if (glideBoost > 1)
            {
                glideBoost -= glideBoostDecayRate;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x * glideBoost, -glideFallSpeed, rb.linearVelocity.z * glideBoost);
            }
            else
            {
                glideBoost = 1;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -glideFallSpeed, rb.linearVelocity.z);
            }

            // Decay the sprint-into-glide entry boost back toward 1
            if (currentSprintBoost > 1f)
                currentSprintBoost = Mathf.Max(1f, currentSprintBoost - sprintGlideBoostDecay);

            if (groundChecker.IsGrounded)
            {
                glideTimer.Stop();
                glideStamina?.StopGlide();
                glidingMesh.SetActive(false);
                defaultMesh.SetActive(true);
                currentSprintBoost = 1f;
            }
        }
        #endregion
        

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                Gizmos.color = Color.grey;
                if(wallClimbChecks[0]) Gizmos.color = Color.green;
                Gizmos.DrawSphere(transform.position + transform.forward * wallCheckDist + transform.up, 0.5f);
         
                Gizmos.color = Color.yellow;
                if(wallClimbChecks[1]) Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position + transform.up * 2, transform.forward * 1);
                
                Gizmos.color = Color.yellow;
                if(wallClimbChecks[4]) Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position - transform.right * 1 + transform.up * 0.5f, transform.forward);
         
                Gizmos.color = Color.magenta;
                if(wallClimbNormal != Vector3.zero) Gizmos.DrawRay(wallClimbTargetPos, wallClimbNormal);
                
            }
        }
        
        /*
       private void OnCounter()
       {
           if (!playerHealth.isDead && !hurtTimer.IsRunning)
           {
               animator.CrossFade("Counter", 0.1f);
               combat.CounterCheck();
           }
       }*/
        
        void OnEnable()
        {
            input.Sprint += OnSprint;
            input.Jump += OnJump;
            input.Dodge += OnDodge;
            input.Wallclimb += OnWallClimb;
            input.Glide += OnGlide;
            playerHealth.OnHit += HandlePlayerHurt;
            //input.Counter += OnCounter;

        }
        void OnDisable()
        {
            input.Jump -= OnJump;
            input.Sprint -= OnSprint;
            input.Dodge -= OnDodge;
            input.Wallclimb -= OnWallClimb;
            input.Glide -= OnGlide;
            playerHealth.OnHit -= HandlePlayerHurt;
            // input.Counter -= OnCounter;
        }
    }
}