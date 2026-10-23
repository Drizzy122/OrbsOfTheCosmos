using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer
{
    /// <summary>
    /// Camera + body response to the hard lock-on (EnemyDetection.IsLockedOn).
    ///  • Camera: every frame the orbital camera's horizontal axis is nudged so the
    ///    locked enemy drifts back behind the player. The nudge is a damped fraction of
    ///    the remaining yaw error, so the player's own look input still works and simply
    ///    gets eased back when they let go.
    ///  • Body: while attacking or standing still the player turns to face the locked
    ///    enemy. While running, PlayerMovement keeps owning the facing, so normal
    ///    locomotion is untouched. PlayerAim owns facing while aiming.
    /// Add this to the Player (next to EnemyDetection / PlayerCombat). Every reference
    /// is optional: anything left empty is found at runtime.
    /// </summary>
    [DefaultExecutionOrder(50)] // after PlayerMovement / PlayerCombat / PlayerAim Update()
    public class LockOnCamera : MonoBehaviour
    {
        [Header("References (auto-found when empty)")]
        [Tooltip("Lock-on source. Found on this object / parents / scene when empty.")]
        [SerializeField] EnemyDetection enemyDetection;
        [Tooltip("The free-look orbital body to steer. When empty, the OrbitalFollow whose Follow target is this player is used (falls back to the first one in the scene).")]
        [SerializeField] CinemachineOrbitalFollow orbitalFollow;
        [Tooltip("Used to know when an attack is running. Found on this object when empty.")]
        [SerializeField] PlayerCombat combat;
        [Tooltip("Facing is left alone while aiming. Found on this object when empty.")]
        [SerializeField] PlayerAim playerAim;
        [Tooltip("Read for move/look input. When empty, CameraManager.instance.InputReader is used, then raw device polling.")]
        [SerializeField] InputReader input;
        [Tooltip("Used to detect standing still from planar speed. Found on this object when empty.")]
        [SerializeField] Rigidbody rb;

        [Header("Camera Steering")]
        [SerializeField] bool steerCamera = true;
        [Tooltip("How hard the camera is pulled toward the target, per second. 0 = off, ~2 = lazy, ~6 = firm. Exponential ease, so it never snaps.")]
        [SerializeField, Range(0f, 15f)] float pullStrength = 6f;
        [Tooltip("Hard cap on how fast the lock may turn the camera (degrees per second).")]
        [SerializeField, Range(10f, 720f)] float maxTurnSpeed = 180f;
        [Tooltip("Yaw error (degrees) below which the camera is left alone, so it does not jitter around the target.")]
        [SerializeField, Range(0f, 15f)] float deadZone = 2f;
        [Tooltip("0 = the lock ignores the player's look input, 1 = the pull fully pauses while the player is actively looking around.")]
        [SerializeField, Range(0f, 1f)] float lookInputYield = 0.5f;
        [Tooltip("Seconds after the last look input before the pull returns to full strength.")]
        [SerializeField, Range(0f, 3f)] float lookInputGrace = 0.6f;
        [Tooltip("Look input magnitude that counts as the player looking around.")]
        [SerializeField] float lookInputThreshold = 0.05f;
        [Tooltip("PlayerToEnemy = classic framing, enemy behind the player. CameraToEnemy = enemy dead centre, player may sit off to the side.")]
        [SerializeField] CenterMode centerOn = CenterMode.PlayerToEnemy;
        [Tooltip("Flip if the camera turns away from the enemy instead of toward it (depends on orbit setup).")]
        [SerializeField] bool invertAxis = false;

        [Header("Player Facing")]
        [SerializeField] bool faceTarget = true;
        [Tooltip("Turn toward the target during melee / blast attacks.")]
        [SerializeField] bool faceWhileAttacking = true;
        [Tooltip("Turn toward the target when there is no move input and (almost) no planar velocity.")]
        [SerializeField] bool faceWhileIdle = true;
        [Tooltip("Degrees per second the body turns toward the target.")]
        [SerializeField, Range(60f, 1440f)] float faceTurnSpeed = 540f;
        [Tooltip("Move input below this counts as standing still.")]
        [SerializeField] float idleInputThreshold = 0.1f;
        [Tooltip("Planar speed (m/s) below this counts as standing still.")]
        [SerializeField] float idleSpeedThreshold = 0.3f;

        public enum CenterMode { PlayerToEnemy, CameraToEnemy }

        /// <summary>True when this component actively moved the camera axis this frame.</summary>
        public bool IsSteering { get; private set; }

        Transform mainCam;
        float lastLookInputTime = -10f;
        float nextRefSearch;
        bool subscribedToInput;
        EnemyDetection subscribedDetection;

        void Awake()
        {
            ResolveLocalReferences();
        }

        void OnEnable()
        {
            SubscribeInput();
            SubscribeDetection();
        }

        void OnDisable()
        {
            if (subscribedToInput && input != null) input.Look -= OnLookInput;
            subscribedToInput = false;
            if (subscribedDetection != null) subscribedDetection.OnLockChanged -= HandleLockChanged;
            subscribedDetection = null;
            IsSteering = false;
        }

        void Update()
        {
            if (enemyDetection == null || orbitalFollow == null || !orbitalFollow.isActiveAndEnabled)
                ResolveSceneReferences();

            if (enemyDetection == null || !enemyDetection.IsLockedOn)
            {
                IsSteering = false;
                return;
            }

            Transform target = enemyDetection.LockedTarget;
            if (target == null) { IsSteering = false; return; }

            PollDeviceLookInput();

            if (steerCamera) SteerCamera(target);
            else IsSteering = false;

            if (faceTarget) FaceTarget(target);
        }

        // ---------------------------------------------------------------- camera

        void SteerCamera(Transform target)
        {
            if (orbitalFollow == null || pullStrength <= 0f) { IsSteering = false; return; }
            if (mainCam == null)
            {
                mainCam = Camera.main != null ? Camera.main.transform : null;
                if (mainCam == null) { IsSteering = false; return; }
            }

            Vector3 origin = centerOn == CenterMode.CameraToEnemy ? mainCam.position : transform.position;
            Vector3 toTarget = target.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) { IsSteering = false; return; }

            Vector3 camForward = mainCam.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.0001f) camForward = transform.forward;

            float error = Vector3.SignedAngle(camForward, toTarget, Vector3.up);
            if (Mathf.Abs(error) <= deadZone) { IsSteering = false; return; }

            // Yield to the player's own look input: weakest right after input, full strength once the grace has elapsed.
            float strength = pullStrength;
            float sinceLook = Time.time - lastLookInputTime;
            if (sinceLook < lookInputGrace)
            {
                float t = lookInputGrace > 0f ? sinceLook / lookInputGrace : 1f;
                strength *= Mathf.Lerp(1f - lookInputYield, 1f, t);
            }

            float dt = Time.deltaTime;
            float step = error * (1f - Mathf.Exp(-strength * dt));   // damped approach toward zero error
            step = Mathf.Clamp(step, -maxTurnSpeed * dt, maxTurnSpeed * dt);
            if (invertAxis) step = -step;

            var axis = orbitalFollow.HorizontalAxis;                  // struct: edit a copy, write it back
            axis.CancelRecentering();
            axis.Value = axis.ClampValue(axis.Value + step);
            orbitalFollow.HorizontalAxis = axis;
            IsSteering = true;
        }

        // ---------------------------------------------------------------- body

        void FaceTarget(Transform target)
        {
            if (playerAim != null && playerAim.IsAiming) return;    // PlayerAim owns facing while aiming

            bool attacking = combat != null && (combat.IsAttacking || combat.IsBlastAttacking);
            bool wants = (faceWhileAttacking && attacking) || (faceWhileIdle && IsStandingStill());
            if (!wants) return;

            Vector3 flat = target.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, faceTurnSpeed * Time.deltaTime);
        }

        bool IsStandingStill()
        {
            if (input != null && input.Direction.sqrMagnitude > idleInputThreshold * idleInputThreshold) return false;
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity;
                v.y = 0f;
                if (v.sqrMagnitude > idleSpeedThreshold * idleSpeedThreshold) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- input

        void OnLookInput(Vector2 look)
        {
            if (look.sqrMagnitude > lookInputThreshold * lookInputThreshold) lastLookInputTime = Time.time;
        }

        /// <summary>Fallback when no InputReader is available: watch the devices like CameraManager does.</summary>
        void PollDeviceLookInput()
        {
            if (subscribedToInput) return;
            float thr = lookInputThreshold * lookInputThreshold;
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > thr) lastLookInputTime = Time.time;
            else if (Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > thr) lastLookInputTime = Time.time;
        }

        void HandleLockChanged(Transform target)
        {
            // Fresh lock: start pulling right away instead of waiting out a look-input grace.
            if (target != null) lastLookInputTime = -10f;
            else IsSteering = false;
        }

        // ---------------------------------------------------------------- wiring

        void ResolveLocalReferences()
        {
            if (enemyDetection == null) enemyDetection = GetComponentInParent<EnemyDetection>();
            if (combat == null) combat = GetComponentInParent<PlayerCombat>();
            if (enemyDetection == null && combat != null) enemyDetection = combat.enemyDetection;
            if (playerAim == null) playerAim = GetComponentInParent<PlayerAim>();
            if (rb == null) rb = GetComponentInParent<Rigidbody>();
            if (input == null && CameraManager.instance != null) input = CameraManager.instance.InputReader;
            ResolveSceneReferences(force: true);
        }

        /// <summary>Cheap periodic search for anything still missing (scene objects can be spawned or swapped later).</summary>
        void ResolveSceneReferences(bool force = false)
        {
            if (!force && Time.unscaledTime < nextRefSearch) return;
            nextRefSearch = Time.unscaledTime + 0.5f;

            if (enemyDetection == null)
            {
                enemyDetection = FindAnyObjectByType<EnemyDetection>();
                SubscribeDetection();
            }

            if (input == null && CameraManager.instance != null)
            {
                input = CameraManager.instance.InputReader;
                SubscribeInput();
            }

            if (orbitalFollow == null || !orbitalFollow.isActiveAndEnabled)
            {
                var all = FindObjectsByType<CinemachineOrbitalFollow>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                CinemachineOrbitalFollow best = null;
                foreach (var of in all)
                {
                    if (best == null) best = of;
                    Transform follow = of.FollowTarget;
                    if (follow != null && (follow == transform || follow.IsChildOf(transform) || transform.IsChildOf(follow)))
                    {
                        best = of;
                        break;
                    }
                }
                if (best != null) orbitalFollow = best;
            }
        }

        void SubscribeInput()
        {
            if (subscribedToInput || input == null || !isActiveAndEnabled) return;
            input.Look += OnLookInput;
            subscribedToInput = true;
        }

        void SubscribeDetection()
        {
            if (enemyDetection == null || !isActiveAndEnabled || subscribedDetection == enemyDetection) return;
            if (subscribedDetection != null) subscribedDetection.OnLockChanged -= HandleLockChanged;
            enemyDetection.OnLockChanged += HandleLockChanged;
            subscribedDetection = enemyDetection;
        }
    }
}
