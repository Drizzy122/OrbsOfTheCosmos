using System;
using System.Collections;
using ImprovedTimers;
using UnityEngine;
using DG.Tweening;
using KBCore.Refs;
using UnityEngine.VFX;
using Unity.Cinemachine;
using UnityEngine.Serialization;

namespace Platformer
{
    [System.Serializable]
    public class ComboSlash
    {
        public GameObject slashObj;
        public float activeDuration = 1f;
    }

    public class PlayerCombat : MonoBehaviour, IEntity
    {
        [field: Header("References")]
        [field: SerializeField, Anywhere] CinemachineImpulseSource impulseSource;
        [field: SerializeField, Anywhere] InputReader input;
        [field: SerializeField] public EnemyDetection enemyDetection;



        [field: Header("Attack Settings")]
        [field: SerializeField] [Range(0, 10)] float lightAttackDistance = 1f;
        [field: SerializeField] int lightAttackDamage = 10;
        [field: SerializeField] float knockbackTime = 0.5f;
        [Tooltip("Despite the name, this is how long the attack STATE lasts (how much of the swing animation plays). Ground combo clips are 1.6-1.9s.")]
        [field: SerializeField] float attackCoolDown = 0.9f;
        [Tooltip("How long the blast STATE lasts. The air/wave clips are ~0.85-0.9s.")]
        [field: SerializeField] float blastAttackCoolDown = 0.85f;

        [Tooltip("How long a queued attack press stays valid — covers mid-swing combo presses and presses made just before landing.")]
        [field: SerializeField] float attackBufferWindow = 0.35f;
        [Tooltip("Fraction of the swing REMAINING below which a queued press starts the next combo swing. 0.45 = the next swing can start once ~55% of the current one has played.")]
        [field: SerializeField] [Range(0f, 1f)] float comboContinuePoint = 0.45f;

        private CountdownTimer attackTimer;
        private CountdownTimer blastAttackTimer;
        float attackBufferedUntil = -1f;

        public bool IsAttacking => attackTimer != null && attackTimer.IsRunning;
        public bool IsBlastAttacking => blastAttackTimer != null && blastAttackTimer.IsRunning;

        // Melee needs a weapon in hand (unarmed punching was removed) and is
        // unavailable wherever the weapon is tucked away (see HandleWeaponVisibility)
        bool MeleeAllowed => hasWeapon &&
                             (playerMovement == null ||
                              (!playerMovement.wallClimbimg && !playerMovement.IsGliding));

       
        [field: Header("Blast Settings")]
        [field: SerializeField] SpellStragedy blastStrategy;
        [field: SerializeField] Transform blastPoint;

        [field: Header("Freeflow Settings")]
        [field: SerializeField] [Range(0, 20)] float targetingRadius = 8f;
        [field: SerializeField] float slideDuration = 0.2f;
        [field: SerializeField] float maxLungeRange = 6f;

        private Tween lungeMoveTween;
        private Tween pendingAttackCall;

        [field: Header("Camera Shake Settings")]
        [field: SerializeField] float minShakeForce = 0.5f;
        [field: SerializeField] float shakeDistanceMultiplier = 0.2f;


        [field: Header("VFX Settings")]
        [field: SerializeField] VisualEffect slashVFX;
        [field: SerializeField] ComboSlash[] comboSlashes;
        [field: SerializeField] GameObject impactVFX;

        private PlayerMovement playerMovement;
        private PlayerEquipment playerEquipment;
        private PlayerAim playerAim;

        // Derived from whatever weapon is currently IN HAND (active slot).
        public bool hasWeapon => playerEquipment != null && playerEquipment.ActiveWeapon != null;

        public int luminCharges { get; private set; }

        void OnEnable()
        {
            GameEventsManager.instance.miscEvents.onLuminCollected += OnLuminCollected;
            input.LightAttack += OnLightAttack;
            input.BlastAttack += OnBlastAttack;
            input.SwapWeapon += OnSwapWeapon;
        }

        void OnDisable()
        {
            GameEventsManager.instance.miscEvents.onLuminCollected -= OnLuminCollected;
            input.LightAttack -= OnLightAttack;
            input.BlastAttack -= OnBlastAttack;
            input.SwapWeapon -= OnSwapWeapon;
        }

        void OnSwapWeapon() => playerEquipment?.SwapActiveWeapon();

        void OnLuminCollected()
        {
            luminCharges += 5;
            GameEventsManager.instance?.playerEvents.LuminChargesChanged(luminCharges);
        }

        void OnLightAttack()
        {
            // While aiming with charges available, the attack button fires the blast
            // at the reticle (Spider-Man style) instead of swinging melee
            if (playerAim != null && playerAim.IsAiming && luminCharges > 0)
            {
                OnBlastAttack();
                return;
            }

            if (!MeleeAllowed) return;

            if (IsAttacking)
            {
                // Mid-swing press: queue the next combo swing instead of dropping the input
                attackBufferedUntil = Time.time + attackBufferWindow;
            }
            else if (playerMovement != null && !playerMovement.IsGrounded)
            {
                // Airborne press: hold it and fire on landing. Starting the timer here
                // used to lock input for 0.9s — AttackState is unreachable mid-air.
                attackBufferedUntil = Time.time + attackBufferWindow;
            }
            else
            {
                attackTimer.Start();
            }
        }

        /// <summary>Called by AttackState when it begins a swing so the attack window
        /// always spans the full swing (matters when entering from a dodge or a combo
        /// continue, where the timer was started earlier).</summary>
        public void RestartAttackWindow() => attackTimer.Start();

        /// <summary>Called by AttackState each frame: true when a queued press exists
        /// and the current swing has played far enough to flow into the next one.</summary>
        public bool TryConsumeBufferedCombo()
        {
            if (!IsAttacking || Time.time > attackBufferedUntil) return false;
            if (attackTimer.Progress > comboContinuePoint) return false;
            attackBufferedUntil = -1f;
            return true;
        }

        void OnBlastAttack()
        {
            if (!IsBlastAttacking && luminCharges > 0) blastAttackTimer.Start();
        }

        public void CancelActions()
        {
            attackTimer?.Stop();
            blastAttackTimer?.Stop();
            attackBufferedUntil = -1f;

            // Also kill any in-flight lunge and its pending hit — otherwise getting
            // hurt mid-attack still slides us in and lands the swing from HurtState.
            lungeMoveTween?.Kill();
            pendingAttackCall?.Kill();
        }

        

      



        AbilityTree abilityTree;

        void Awake()
        {
            playerMovement = GetComponent<PlayerMovement>();
            // Equipment and the ability tree live on child objects of the Player
            playerEquipment = GetComponentInChildren<PlayerEquipment>();
            playerAim = GetComponent<PlayerAim>();
            abilityTree = GetComponentInChildren<AbilityTree>();
            attackTimer = new CountdownTimer(attackCoolDown);
            blastAttackTimer = new CountdownTimer(blastAttackCoolDown);

            foreach (ComboSlash combo in comboSlashes)
            {

                if (combo != null && combo.slashObj != null)
                {
                    combo.slashObj.SetActive(false);
                }
            }
        }

        void Update()
        {
            HandleWeaponVisibility();

            if (enemyDetection != null && playerMovement != null)
            {
                enemyDetection.ScanForEnemies(playerMovement.GetAdjustedMovementDirection());
            }

            // Fire a buffered press (from mid-air or the tail of a swing) as soon as
            // a fresh swing is allowed again
            if (!IsAttacking && Time.time <= attackBufferedUntil && MeleeAllowed
                && playerMovement != null && playerMovement.IsGrounded)
            {
                attackBufferedUntil = -1f;
                attackTimer.Start();
            }
        }

        private void HandleWeaponVisibility()
        {
            if (playerEquipment == null) return;

            bool shouldHide = (playerMovement != null &&
                              (playerMovement.IsGliding || playerMovement.wallClimbimg))
                              || (playerAim != null && playerAim.IsAiming);

            // Active weapon + shield tuck away while gliding/climbing/aiming.
            var weaponVisual = playerEquipment.GetEquippedVisual(playerEquipment.ActiveWeaponSlot);
            if (weaponVisual != null) weaponVisual.SetActive(!shouldHide);

            var shieldVisual = playerEquipment.GetEquippedVisual(EquipSlot.Shield);
            if (shieldVisual != null) shieldVisual.SetActive(!shouldHide);
        }

        /// <summary>Damage to apply on a successful melee hit. Pulls from the ACTIVE weapon
        /// when present, plus the Combat branch bonus from the ability tree.</summary>
        int CurrentMeleeDamage()
        {
            int abilityBonus = abilityTree != null
                ? Mathf.RoundToInt(abilityTree.GetStat(AbilityTree.StatMeleeDamage))
                : 0;

            // The weapon can be swapped out between the press and the hit landing —
            // fall back to the base value rather than an unarmed-punch value
            if (!hasWeapon) return lightAttackDamage + abilityBonus;
            var weapon = playerEquipment.ActiveWeapon.data as WeaponData;
            return (weapon != null ? weapon.damage : lightAttackDamage) + abilityBonus;
        }

        

        void Start()
        {
            if (impulseSource == null)
                impulseSource = GetComponentInChildren<CinemachineImpulseSource>();

            // Initial broadcast so the HUD charge bar starts correct.
            GameEventsManager.instance?.playerEvents.LuminChargesChanged(luminCharges);
        }
        public void PlaySlashVFX(int slashIndex)
        {
            if (slashIndex >= 0 && slashIndex < comboSlashes.Length)
            {
                GameObject currentSlash = comboSlashes[slashIndex].slashObj;
                float duration = comboSlashes[slashIndex].activeDuration;

                if (currentSlash != null)
                {
                    currentSlash.SetActive(false);
                    currentSlash.SetActive(true);
                    DOVirtual.DelayedCall(duration, () =>
                    {
                        if (currentSlash != null)
                        {
                            currentSlash.SetActive(false);
                        }
                    });
                }
            }
        }

        private void TriggerCameraShake(float distance)
        {
            if (impulseSource != null)
            {
                float shakeForce = Mathf.Max(minShakeForce, shakeDistanceMultiplier * distance);
                impulseSource.GenerateImpulseWithForce(shakeForce);
            }
        }


        public void lunge(Vector3 inputDirection)
        {
            float lungeDistance = 0f;
            Transform target = enemyDetection.CurrentTarget();

            if (target != null)
            {
                lungeDistance = Vector3.Distance(transform.position, target.position);

                // Flatten BEFORE normalizing so elevation differences don't shrink the
                // stop offset, and keep our own Y so we don't get dragged to the
                // enemy's height (ground clip / float).
                Vector3 directionToTarget = target.position - transform.position;
                directionToTarget.y = 0;
                directionToTarget = directionToTarget.normalized;

                Vector3 stopPosition = target.position - (directionToTarget * 1.2f);
                stopPosition.y = transform.position.y;

                transform.DOLookAt(target.position, 0.1f, AxisConstraint.Y);

                // Only warp within a sane range — beyond it we just face the target
                if (lungeDistance <= maxLungeRange)
                {
                    lungeMoveTween = transform.DOMove(stopPosition, slideDuration).SetEase(Ease.OutQuad);
                }
            }
            else if (inputDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(inputDirection);
            }

            pendingAttackCall = DOVirtual.DelayedCall(slideDuration, () =>
            {
                AudioManager.instance.PlayOneShot(FMODEvents.instance.playerAttack, transform.position);

                int hits = LightAttack();
                if (hits > 0)
                {
                    // Hit-confirm sandwich: freeze + shake + rumble. The enemy hurt
                    // SFX and hit-flash come from Health.TakeDamage.
                    HitStop.Play();
                    TriggerCameraShake(Mathf.Max(lungeDistance, 3f));
                    RumblePulse();
                }
            });
        }




        /// <summary>Applies melee damage in front of us. Returns how many targets were hit.</summary>
        private int LightAttack()
        {
            Vector3 attackPos = transform.position + transform.forward * lightAttackDistance;
            Collider[] hitEnemies = Physics.OverlapSphere(attackPos, lightAttackDistance);

            int damage = CurrentMeleeDamage();
            int hits = 0;
            foreach (var hit in hitEnemies)
            {
                if (hit.CompareTag("Enemy"))
                {
                    if (hit.TryGetComponent<Health>(out Health enemyHealth) && !enemyHealth.isDead)
                    {
                        enemyHealth.TakeDamage(damage, knockbackTime);
                        SpawnImpactVFX(hit.ClosestPoint(attackPos));
                        hits++;
                    }
                }
                else if (hit.CompareTag("Destructable"))
                {
                    if (hit.TryGetComponent<IDamageable>(out var damageable))
                    {
                        damageable.TakeDamage(damage, knockbackTime);
                        SpawnImpactVFX(hit.ClosestPoint(attackPos));
                        hits++;
                    }
                }
            }
            return hits;
        }

        void SpawnImpactVFX(Vector3 position)
        {
            if (impactVFX == null) return;
            var vfx = Instantiate(impactVFX, position + Vector3.up * 0.2f, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        void RumblePulse()
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return;
            pad.SetMotorSpeeds(0.35f, 0.6f);
            DOVirtual.DelayedCall(0.12f, () => pad.SetMotorSpeeds(0f, 0f)).SetUpdate(true);
        }

        public void BlastAttack()
        {
            if (luminCharges <= 0) return;
            luminCharges--;
            GameEventsManager.instance?.playerEvents.LuminChargesChanged(luminCharges);

            AudioManager.instance.PlayOneShot(FMODEvents.instance.blastAttack, transform.position);

            Transform origin = blastPoint != null ? blastPoint : transform;

            if (playerAim != null && playerAim.IsAiming)
            {
                // Aim the shot at the reticle point, then restore the muzzle's local
                // rotation so unaimed quick-fire keeps shooting straight ahead
                Quaternion savedLocalRotation = origin.localRotation;
                Vector3 toTarget = playerAim.GetAimPoint() - origin.position;
                if (toTarget.sqrMagnitude > 0.001f)
                    origin.rotation = Quaternion.LookRotation(toTarget.normalized);

                blastStrategy?.CastSpell(origin, transform);
                origin.localRotation = savedLocalRotation;
            }
            else
            {
                blastStrategy?.CastSpell(origin, transform);
            }
        }

        void IEntity.Attack(Vector3 direction) => lunge(direction);
        void IEntity.Attack2() => BlastAttack();
        void IEntity.Attack3() => CounterCheck();


        public void CounterCheck()
        {
            Enemy bestCounterTarget = null;
            float closestDistance = float.MaxValue;

            // 1. Efficient Scan: Check the Manager's list of active fighters
            if (EnemyManager.instance != null)
            {
                foreach (Enemy enemy in EnemyManager.instance.engagedEnemies)
                {
                    // Check if they are preparing to lunge AND within your targeting radius
                    if (enemy.isPreparingAttack)
                    {
                        float distance = Vector3.Distance(transform.position, enemy.transform.position);
                        if (distance < targetingRadius && distance < closestDistance)
                        {
                            closestDistance = distance;
                            bestCounterTarget = enemy;
                        }
                    }
                }
            }

            if (bestCounterTarget != null)
            {
                // 2. STOP THE ENEMY: Call the new CancelAttack() we wrote in Enemy.cs
                bestCounterTarget.CancelAttack();

                // 3. SNAPPY MOVEMENT: Spin and snap to the enemy position
                Vector3 dirToTarget = bestCounterTarget.transform.position - transform.position;
                dirToTarget.y = 0;
                dirToTarget = dirToTarget.normalized;
                Vector3 stopPosition = bestCounterTarget.transform.position - (dirToTarget * 1.5f);
                stopPosition.y = transform.position.y;

                transform.DOLookAt(bestCounterTarget.transform.position, 0.1f, AxisConstraint.Y);
                lungeMoveTween = transform.DOMove(stopPosition, slideDuration).SetEase(Ease.OutQuad);

                // 4. FEEDBACK: Sound and Camera Effects
                AudioManager.instance.PlayOneShot(FMODEvents.instance.playerAttack, transform.position);

                if (bestCounterTarget.enemyHealth != null)
                {
                    // Deal heavy damage and trigger that beautiful Final Blow camera
                    bestCounterTarget.enemyHealth.TakeDamage(CurrentMeleeDamage() * 2, knockbackTime);
                    HitStop.Play(0.09f);
                    SpawnImpactVFX(bestCounterTarget.transform.position + Vector3.up);
                    TriggerCameraShake(10f);
                    RumblePulse();
                }
            }
        }


        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.forward * lightAttackDistance, lightAttackDistance);
           

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, targetingRadius);
        }
    }
}
