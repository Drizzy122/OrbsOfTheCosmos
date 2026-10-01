using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Catches the player when they leave the level geometry entirely. Continuously
    /// remembers the last spot they stood on, so a respawn puts them somewhere they
    /// can actually stand rather than at a fixed level start.
    /// </summary>
    public class FallOutOfWorld : MonoBehaviour
    {
        public enum FallResponse { Respawn, Kill }

        [Header("References")]
        [SerializeField] Rigidbody rb;
        [SerializeField] Health health;
        [SerializeField] GroundChecker groundChecker;

        [Header("Trigger")]
        [Tooltip("Falling below this world Y counts as out of bounds. Put it well under the lowest real floor.")]
        [SerializeField] float killHeight = -50f;

        [Header("Response")]
        [SerializeField] FallResponse response = FallResponse.Respawn;
        [Tooltip("Damage dealt on a respawn. At or above max health this kills instead, so keep it below.")]
        [SerializeField, Min(0f)] float respawnDamage = 20f;
        [Tooltip("Ignore further falls for this long, so one fall can't fire twice.")]
        [SerializeField, Min(0f)] float retriggerCooldown = 1f;

        [Header("Safe-spot sampling")]
        [Tooltip("How often to record the player's footing, in seconds.")]
        [SerializeField, Min(0.05f)] float sampleInterval = 0.4f;
        [Tooltip("Lift the respawn point slightly so they don't spawn inside the floor.")]
        [SerializeField] float respawnYOffset = 0.6f;

        Vector3 lastSafePosition;
        float nextSampleTime;
        float ignoreUntil;

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (health == null) health = GetComponent<Health>();
            if (groundChecker == null) groundChecker = GetComponent<GroundChecker>();

            // Until they touch ground, wherever they started is the only safe spot we know.
            lastSafePosition = transform.position;
        }

        void Update()
        {
            if (health != null && health.isDead) return;

            SampleFooting();

            if (transform.position.y > killHeight) return;
            if (Time.time < ignoreUntil) return;

            ignoreUntil = Time.time + retriggerCooldown;

            if (response == FallResponse.Kill) KillPlayer();
            else RespawnPlayer();
        }

        void SampleFooting()
        {
            if (groundChecker == null || !groundChecker.IsGrounded) return;
            if (Time.time < nextSampleTime) return;

            // Never record a spot that is itself out of bounds.
            if (transform.position.y <= killHeight) return;

            nextSampleTime = Time.time + sampleInterval;
            lastSafePosition = transform.position;
        }

        void KillPlayer()
        {
            if (health == null)
            {
                RespawnPlayer();
                return;
            }
            StopFalling();
            health.HandleDeath();
        }

        void RespawnPlayer()
        {
            Vector3 target = lastSafePosition + Vector3.up * respawnYOffset;

            StopFalling();

            // Move the body, not just the transform — with interpolation on, setting the
            // transform alone leaves the Rigidbody to smear the player back across the map.
            if (rb != null) rb.position = target;
            transform.position = target;

            if (health != null && respawnDamage > 0f) health.TakeDamage(respawnDamage);
        }

        void StopFalling()
        {
            if (rb == null) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        void OnDrawGizmosSelected()
        {
            // Draw the plane they must not cross, centred on wherever the player is now.
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Vector3 c = new Vector3(transform.position.x, killHeight, transform.position.z);
            Gizmos.DrawWireCube(c, new Vector3(60f, 0.05f, 60f));
        }
    }
}
