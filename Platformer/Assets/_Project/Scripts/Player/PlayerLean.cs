using UnityEngine;

namespace Platformer
{
    /// <summary>Cosmetic lean/bank for the player mesh. Tilts a visual child transform only —
    /// the root keeps its upright rotation so movement, aiming, dodging and the collider are
    /// untouched. Banks into turns from the root's yaw rate and leans forward with speed.</summary>
    public class PlayerLean : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Visual root to tilt (e.g. the 'Models' child). Never the player root.")]
        [SerializeField] Transform leanTarget;
        [SerializeField] PlayerMovement movement;
        [SerializeField] Rigidbody rb;

        [Header("Bank (roll into turns)")]
        [SerializeField] bool enableBank = true;
        [Tooltip("Degrees of roll per 100 deg/s of turning.")]
        [SerializeField, Range(0f, 30f)] float bankPerHundredDegrees = 8f;
        [SerializeField, Range(0f, 45f)] float maxBank = 18f;
        [SerializeField] float bankSmoothTime = 0.12f;

        [Header("Forward lean (pitch with speed)")]
        [SerializeField] bool enableForwardLean = true;
        [SerializeField, Range(0f, 30f)] float maxForwardLean = 7f;
        [Tooltip("Planar speed that counts as 'full speed' for the lean ramp.")]
        [SerializeField] float referenceSpeed = 20f;
        [SerializeField] float pitchSmoothTime = 0.15f;

        [Header("Modifiers")]
        [Tooltip("Lean multiplier while gliding — usually punchier.")]
        [SerializeField, Range(0f, 3f)] float glideMultiplier = 1.6f;
        [Tooltip("Lean multiplier while sprinting.")]
        [SerializeField, Range(0f, 3f)] float sprintMultiplier = 1.25f;
        [Tooltip("Kill the lean while aiming — PlayerAim snaps body yaw to the camera, which would read as a huge turn.")]
        [SerializeField] bool suppressWhileAiming = true;
        [Tooltip("Fade the lean out in the air (bank still works, just softer).")]
        [SerializeField, Range(0f, 1f)] float airborneScale = 0.6f;

        Quaternion baseLocalRotation;
        float lastYaw;
        float currentBank, bankVelocity;
        float currentPitch, pitchVelocity;

        void Awake()
        {
            if (!movement) movement = GetComponent<PlayerMovement>();
            if (!rb) rb = GetComponent<Rigidbody>();
            if (!leanTarget)
            {
                enabled = false;
                Debug.LogWarning($"{nameof(PlayerLean)} on {name} has no leanTarget assigned — disabling.", this);
                return;
            }

            baseLocalRotation = leanTarget.localRotation;
            lastYaw = transform.eulerAngles.y;
        }

        // LateUpdate so we write after the Animator has posed the rig for this frame.
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;

            Vector3 planar = rb ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
            float speed01 = referenceSpeed > 0f ? Mathf.Clamp01(planar.magnitude / referenceSpeed) : 0f;

            float scale = Scale();
            float targetBank = 0f;
            float targetPitch = 0f;

            if (scale > 0f)
            {
                // Only bank when actually moving — otherwise a camera spin on the spot rolls us over.
                if (enableBank)
                {
                    targetBank = Mathf.Clamp(-yawRate * 0.01f * bankPerHundredDegrees, -maxBank, maxBank)
                                 * speed01 * scale;
                }
                if (enableForwardLean) targetPitch = maxForwardLean * speed01 * scale;
            }

            currentBank = Mathf.SmoothDamp(currentBank, targetBank, ref bankVelocity, bankSmoothTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, pitchSmoothTime);

            leanTarget.localRotation = baseLocalRotation * Quaternion.Euler(currentPitch, 0f, currentBank);
        }

        float Scale()
        {
            if (!movement) return 1f;
            if (suppressWhileAiming && movement.IsAimingActive) return 0f;

            float scale = 1f;
            if (movement.IsGliding) scale *= glideMultiplier;
            else if (movement.IsSprintingActively) scale *= sprintMultiplier;
            if (!movement.IsGrounded && !movement.IsGliding) scale *= airborneScale;
            return scale;
        }
    }
}
