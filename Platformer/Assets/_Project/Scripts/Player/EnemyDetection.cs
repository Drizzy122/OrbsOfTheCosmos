using System;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Enemy targeting for the player. Two layers:
    ///  • Soft target — the Mix &amp; Jam sphere cast along the movement direction,
    ///    refreshed every frame by PlayerCombat. Attacks lunge at it.
    ///  • Hard lock-on — ToggleLockOn() picks the best enemy in range (closest to
    ///    the screen center, then nearest) and pins CurrentTarget() to it until the
    ///    player toggles again, the enemy dies, or it gets too far away.
    /// The HUD reticle (HUDTargetMarker) reads CurrentTarget() and IsLockedOn.
    /// </summary>
    public class EnemyDetection : MonoBehaviour
    {
        [field: Header("Targeting Settings")]
        public LayerMask layerMask;

        [Tooltip("Radius of the soft-target sphere cast.")]
        [SerializeField] float softTargetRadius = 3f;
        [Tooltip("Reach of the soft-target sphere cast.")]
        [SerializeField] float softTargetRange = 10f;

        [Header("Lock-On")]
        [Tooltip("How far away an enemy can be when the lock is first acquired.")]
        [SerializeField] float lockOnRange = 18f;
        [Tooltip("The lock breaks once the enemy is further than this.")]
        [SerializeField] float lockBreakRange = 26f;
        [Tooltip("0 = pick the nearest enemy, 1 = pick the one closest to the screen center.")]
        [SerializeField, Range(0f, 1f)] float screenCenterWeight = 0.6f;

        [SerializeField] private Vector3 inputDirection;
        [SerializeField] private Transform currentTarget;

        Transform lockedTarget;
        Health lockedHealth;

        static readonly Collider[] overlapBuffer = new Collider[32];

        /// <summary>True while a hard lock-on is active.</summary>
        public bool IsLockedOn => lockedTarget != null;

        /// <summary>The hard-locked enemy, or null.</summary>
        public Transform LockedTarget => lockedTarget;

        /// <summary>Raised with the new target when a lock starts, and with null when it ends.</summary>
        public event Action<Transform> OnLockChanged;

        // We will call this from your PlayerController's Update loop
        public void ScanForEnemies(Vector3 inputDir)
        {
            inputDirection = inputDir;

            // If we aren't pressing anything, cast the laser straight ahead
            if (inputDirection == Vector3.zero)
            {
                inputDirection = transform.forward;
            }

            // A hard lock owns the target until it stops being valid.
            if (IsLockedOn)
            {
                if (LockStillValid())
                {
                    currentTarget = lockedTarget;
                    return;
                }
                ClearLock();
            }

            // The exact Mix and Jam SphereCast! (Start, Radius, Direction, out hit, Distance, Layer)
            if (Physics.SphereCast(transform.position, softTargetRadius, inputDirection, out RaycastHit info, softTargetRange, layerMask))
            {
                if (IsTargetable(info.collider))
                {
                    currentTarget = info.collider.transform;
                }
                else
                {
                    // Hit something that isn't targetable — drop the stale target
                    // instead of silently keeping whatever we locked onto before
                    currentTarget = null;
                }
            }
            else
            {
                // Clear the target if we point away into empty space
                currentTarget = null;
            }
        }

        /// <summary>Lock onto the best enemy in range, or release the current lock.</summary>
        public void ToggleLockOn()
        {
            if (IsLockedOn)
            {
                ClearLock();
                return;
            }

            Transform best = FindLockTarget();
            if (best != null) SetLock(best);
        }

        /// <summary>Release the hard lock (no-op when not locked).</summary>
        public void ClearLock()
        {
            if (lockedTarget == null) return;
            lockedTarget = null;
            lockedHealth = null;
            OnLockChanged?.Invoke(null);
        }

        // Getters to match the Mix & Jam setup
        public Transform CurrentTarget()
        {
            return currentTarget;
        }

        public void SetCurrentTarget(Transform target)
        {
            currentTarget = target;
        }

        public float InputMagnitude()
        {
            return inputDirection.magnitude;
        }

        void SetLock(Transform target)
        {
            lockedTarget = target;
            lockedHealth = target.GetComponentInParent<Health>();
            currentTarget = target;
            OnLockChanged?.Invoke(target);
        }

        bool LockStillValid()
        {
            if (lockedTarget == null || !lockedTarget.gameObject.activeInHierarchy) return false;
            if (lockedHealth != null && lockedHealth.isDead) return false;
            return Vector3.Distance(transform.position, lockedTarget.position) <= lockBreakRange;
        }

        Transform FindLockTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, lockOnRange, overlapBuffer, layerMask, QueryTriggerInteraction.Ignore);
            Camera cam = Camera.main;

            Transform best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = overlapBuffer[i];
                if (col == null || !IsTargetable(col)) continue;

                var health = col.GetComponentInParent<Health>();
                if (health != null && health.isDead) continue;

                Transform t = col.transform;
                float distance01 = Vector3.Distance(transform.position, t.position) / lockOnRange;

                float center01 = 0.5f;
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(col.bounds.center);
                    if (vp.z <= 0f) continue;   // behind the camera — never lock on blind
                    center01 = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)) / 0.7071f;
                }

                float score = Mathf.Lerp(distance01, center01, screenCenterWeight);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best;
        }

        // Villagers and animals share the "Enemy" tag and layer with real enemies,
        // so the tag alone isn't enough: a hostile target must carry an Enemy
        // component (or be a Destructable prop).
        static bool IsTargetable(Collider col)
        {
            if (col.CompareTag("Destructable")) return true;
            return col.CompareTag("Enemy") && col.GetComponentInParent<Enemy>() != null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.black;
            Gizmos.DrawRay(transform.position, inputDirection * 5f);
            Gizmos.DrawWireSphere(transform.position, 1f);
            if (CurrentTarget() != null)
            {
                Gizmos.color = IsLockedOn ? Color.yellow : Color.black;
                Gizmos.DrawSphere(CurrentTarget().position + (Vector3.up * 1f), 0.5f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, lockOnRange);
        }
    }
}
