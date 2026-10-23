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
        [Tooltip("Geometry the lunge must not pass through. Without this the warp goes straight through walls to an enemy on the other side.")]
        [field: SerializeField] LayerMask lungeObstacles;
        [Tooltip("Skip the lunge when the target is further than this above or below us — otherwise an enemy on a ledge drags the warp into thin air.")]
        [field: SerializeField] float maxLungeHeightDifference = 3f;

        private Tween lungeMoveTween;
        private Tween pendingAttackCall;

        [field: Header("Combat Feel")]
        [Tooltip("Forward step taken by a swing that has no target, so attacks don't feel rooted. Blocked by Lunge Obstacles.")]
        [field: SerializeField] float noTargetStepDistance = 1.2f;
        [Tooltip("Seconds into a swing after which holding a direction (with no attack queued) cancels the recovery into movement.")]
        [field: SerializeField] float recoveryCancelTime = 0.32f;
        [Tooltip("Hit-stop for a normal hit, in real seconds.")]
        [field: SerializeField] float hitStopLight = 0.06f;
        [Tooltip("Hit-stop for the last swing of the combo.")]
        [field: SerializeField] float hitStopFinisher = 0.12f;
        [Tooltip("Damage multiplier on the last swing of the combo.")]
        [field: SerializeField] float finisherDamageMultiplier = 1.5f;
        [Tooltip("Knockback-time multiplier on the last swing: enemies fly further and stay down longer.")]
        [field: SerializeField] float finisherKnockbackMultiplier = 1.8f;
        [Tooltip("How long the attack state lasts on the last swing, so the power attack animation plays out instead of being cut at Attack Cool Down. Power_Attack_End slams at 1.0 s.")]
        [field: SerializeField] float finisherAttackDuration = 1.4f;
        [Tooltip("Animator playback speed during the last swing (1 = normal). Below 1 gives the power attack weight.")]
        [field: SerializeField] float finisherAnimationSpeed = 1f;
        [Tooltip("Seconds into the last swing when its damage lands (the slam), instead of the normal Slide Duration.")]
        [field: SerializeField] float finisherHitDelay = 1.0f;
        Animator animator;
        bool animatorSpeedOverridden;
        bool currentSwingIsFinisher;
        [Tooltip("Time scale during the slow-motion that follows a killing blow.")]
        [field: SerializeField] float killSlowMoScale = 0.35f;
        [Tooltip("Real seconds of slow-motion after a killing blow.")]
        [field: SerializeField] float killSlowMoDuration = 0.3f;
        [Tooltip("Real seconds of slow-motion when the kill empties the arena.")]
        [field: SerializeField] float lastKillSlowMoDuration = 0.7f;

        [field: Header("Perfect Dodge")]
        [Tooltip("Seconds after dodging through an attack during which the next swing is a counter strike.")]
        [field: SerializeField] float perfectDodgeWindow = 1.5f;
        [field: SerializeField] float perfectDodgeSlowMoScale = 0.3f;
        [field: SerializeField] float perfectDodgeSlowMoDuration = 0.35f;
        [Tooltip("Damage multiplier of the counter strike after a perfect dodge.")]
        [field: SerializeField] float counterStrikeDamageMultiplier = 2f;
        [Tooltip("How much further the counter strike may lunge, as a multiplier of Max Lunge Range.")]
        [field: SerializeField] float counterStrikeLungeMultiplier = 1.6f;

        float perfectDodgeUntil = -1f;
        float swingStartedAt;
        bool swingHitLanded;

        /// <summary>True while the next swing counts as a counter strike.</summary>
        public bool CounterStrikeReady => Time.time <= perfectDodgeUntil;

        [field: Header("Camera Shake Settings")]
        [field: SerializeField] float minShakeForce = 0.5f;
        [field: SerializeField] float shakeDistanceMultiplier = 0.2f;


        [field: Header("VFX Settings")]
        [field: SerializeField] VisualEffect slashVFX;
        [Tooltip("Slash effect objects. No longer played on swings (the blade trail replaced them); they are only switched off at start.")]
        [field: SerializeField] ComboSlash[] comboSlashes;
        [field: SerializeField] GameObject impactVFX;
        [Tooltip("Draw a ribbon from the blade's real motion on every swing (BladeTrail component, added automatically). Always in sync with the animation.")]
        [field: SerializeField] bool useBladeTrail = true;
        [Tooltip("Seconds the blade trail keeps emitting after a slash event.")]
        [field: SerializeField] float bladeTrailSeconds = 0.25f;
        [Tooltip("The trail also starts from code this many seconds into every swing (the Air Wave swings begin at 0.05-0.17 s), so it shows even on a clip without events.")]
        [field: SerializeField] float bladeTrailSwingDelay = 0.06f;
        BladeTrail bladeTrail;
        Tween pendingTrailCall;

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
            input.LockOn += OnLockOn;
        }

        void OnDisable()
        {
            GameEventsManager.instance.miscEvents.onLuminCollected -= OnLuminCollected;
            input.LightAttack -= OnLightAttack;
            input.BlastAttack -= OnBlastAttack;
            input.SwapWeapon -= OnSwapWeapon;
            input.LockOn -= OnLockOn;
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
        /// continue, where the timer was started earlier). Normal swings use
        /// Attack Cool Down; lunge() stretches it again for the finisher.</summary>
        public void RestartAttackWindow()
        {
            RestoreAnimatorSpeed();
            attackTimer.Reset(attackCoolDown);
            attackTimer.Start();
        }

        /// <summary>Called by AttackState each frame: true when a queued press exists
        /// and the current swing has played far enough to flow into the next one.</summary>
        public bool TryConsumeBufferedCombo()
        {
            if (!IsAttacking || Time.time > attackBufferedUntil) return false;
            if (attackTimer.Progress > comboContinuePoint) return false;
            // The power attack commits: no chaining out of it until its slam has landed.
            if (currentSwingIsFinisher && !swingHitLanded) return false;
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
            swingHitLanded = false;
            currentSwingIsFinisher = false;
            RestoreAnimatorSpeed();

            // Also kill any in-flight lunge and its pending hit — otherwise getting
            // hurt mid-attack still slides us in and lands the swing from HurtState.
            lungeMoveTween?.Kill();
            pendingAttackCall?.Kill();
            pendingTrailCall?.Kill();
        }

        

      



        AbilityTree abilityTree;

        void Awake()
        {
            EnsureLockOnCamera();
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

        // Hard lock-on toggle: pins the target until released, the enemy dies, or
        // it gets out of range. The HUD reticle turns gold while locked.
        void OnLockOn()
        {
            if (enemyDetection != null) enemyDetection.ToggleLockOn();
        }

        // The lock-on camera steers the orbit toward the locked enemy. Added here
        // so the prefab needs no manual wiring; it resolves its references itself.
        void EnsureLockOnCamera()
        {
            if (GetComponent<LockOnCamera>() == null) gameObject.AddComponent<LockOnCamera>();
            bladeTrail = GetComponent<BladeTrail>();
            if (bladeTrail == null) bladeTrail = gameObject.AddComponent<BladeTrail>();
            finalBlowCamera = GetComponent<FinalBlowCamera>();
            if (finalBlowCamera == null) finalBlowCamera = gameObject.AddComponent<FinalBlowCamera>();
        }

        // Close-up slow-motion camera on the kill that empties the arena (Mix and Jam style).
        FinalBlowCamera finalBlowCamera;

        void RestoreAnimatorSpeed()
        {
            if (!animatorSpeedOverridden) return;
            animatorSpeedOverridden = false;
            if (animator != null) animator.speed = 1f;
        }

        void Update()
        {
            HandleWeaponVisibility();

            if (animatorSpeedOverridden && !IsAttacking) RestoreAnimatorSpeed();

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

            // Recovery cancel: once the hit has landed and the swing has played past
            // the cancel point, holding a direction with nothing queued drops the
            // rest of the recovery and hands control back to movement.
            if (IsAttacking && swingHitLanded && Time.time > attackBufferedUntil
                && Time.time - swingStartedAt >= recoveryCancelTime
                && playerMovement != null
                && playerMovement.GetAdjustedMovementDirection().sqrMagnitude > 0.25f)
            {
                lungeMoveTween?.Kill();
                attackTimer.Stop();
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
        /// <summary>Animation event on the attack clips: shows the slash object for
        /// that swing and starts the blade trail.</summary>
        /// <summary>Animation event on the attack clips. The slash effect objects are
        /// no longer played; each event just (re)starts the blade trail, which is
        /// drawn from the sword itself and so always matches the swing.</summary>
        public void PlaySlashVFX(int slashIndex)
        {
            // Some clips are shared with the blast attack; only a melee swing shows the trail.
            if (!IsAttacking || IsBlastAttacking) return;
            if (useBladeTrail && bladeTrail != null) bladeTrail.Emit(bladeTrailSeconds);
        }

        private void TriggerCameraShake(float distance)
        {
            if (impulseSource != null)
            {
                float shakeForce = Mathf.Max(minShakeForce, shakeDistanceMultiplier * distance);
                impulseSource.GenerateImpulseWithForce(shakeForce);
            }
        }


        /// <summary>Gates the warp. The lunge is a DOMove straight to a point beside the enemy,
        /// so without these checks it passes through walls and yanks us off ledges — it ignores
        /// the collider entirely because it writes the transform rather than moving the body.</summary>
        private bool CanLungeTo(Transform target, Vector3 flatDirection, Vector3 stopPosition)
        {
            // An enemy on a balcony is a valid target to face, but not one to warp to.
            if (Mathf.Abs(target.position.y - transform.position.y) > maxLungeHeightDifference)
            {
                return false;
            }

            if (lungeObstacles.value == 0) return true;

            // Cast from chest height so floor geometry doesn't count as a blocker.
            Vector3 origin = transform.position + Vector3.up;
            float distanceToStop = Vector3.Distance(transform.position, stopPosition);

            if (Physics.Raycast(origin, flatDirection, out RaycastHit hit, distanceToStop, lungeObstacles,
                    QueryTriggerInteraction.Ignore))
            {
                // Something solid is nearer than where we would land, so stay put and swing.
                return false;
            }

            return true;
        }

        public void lunge(Vector3 inputDirection)
        {
            float lungeDistance = 0f;
            Transform target = enemyDetection.CurrentTarget();

            swingStartedAt = Time.time;
            swingHitLanded = false;

            // A kick on every swing, whether or not it lands; hits add their own on top.
            // DualSense motors are weak, so this is stronger than it reads.
            RumblePulse(0.45f, 0.7f, 0.12f);

            // Swing weight: the chain's last swing is the finisher; a swing thrown
            // right after a perfect dodge is a counter strike.
            var command = playerMovement != null ? playerMovement.commandManager.LightAttackCommand : null;
            bool finisher = command != null && command.IsFinisher;
            currentSwingIsFinisher = finisher;
            bool counterStrike = CounterStrikeReady;
            if (counterStrike) perfectDodgeUntil = -1f;   // consumed

            float lungeRange = counterStrike ? maxLungeRange * counterStrikeLungeMultiplier : maxLungeRange;

            // The power attack gets its full animation: a longer attack window and
            // slower playback. Both are undone when the attack ends.
            if (finisher)
            {
                attackTimer.Reset(finisherAttackDuration);
                attackTimer.Start();
                if (animator == null) animator = GetComponent<Animator>();
                if (animator != null && finisherAnimationSpeed > 0f)
                {
                    animator.speed = finisherAnimationSpeed;
                    animatorSpeedOverridden = true;
                }
            }

            // Blade trail from code on every swing, so it never depends on the clip
            // carrying an event. Slower finisher playback pushes the start later.
            if (useBladeTrail && bladeTrail != null)
            {
                pendingTrailCall?.Kill();
                float speed = finisher && finisherAnimationSpeed > 0f ? finisherAnimationSpeed : 1f;
                float delay = bladeTrailSwingDelay / speed;
                float emitFor = (bladeTrailSeconds + 0.1f) / speed;
                pendingTrailCall = DOVirtual.DelayedCall(delay, () => bladeTrail.Emit(emitFor));
            }

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
                if (lungeDistance <= lungeRange && CanLungeTo(target, directionToTarget, stopPosition))
                {
                    lungeMoveTween = transform.DOMove(stopPosition, slideDuration).SetEase(Ease.OutQuad);
                }
            }
            else
            {
                if (inputDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(inputDirection);
                }

                // No target: step into the swing so it carries weight instead of
                // chopping the air on the spot. Same obstacle check as the lunge.
                if (noTargetStepDistance > 0f)
                {
                    Vector3 stepDir = transform.forward;
                    Vector3 stepTo = transform.position + stepDir * noTargetStepDistance;
                    if (CanLungeTo(transform, stepDir, stepTo))
                        lungeMoveTween = transform.DOMove(stepTo, slideDuration).SetEase(Ease.OutQuad);
                }
            }

            float damageMultiplier = 1f;
            float knockback = knockbackTime;
            if (finisher)
            {
                damageMultiplier *= finisherDamageMultiplier;
                knockback *= finisherKnockbackMultiplier;
            }
            if (counterStrike)
            {
                damageMultiplier *= counterStrikeDamageMultiplier;
                knockback *= finisherKnockbackMultiplier;
            }

            // The power attack's damage lands on its slam, well after the lunge.
            float hitDelay = finisher ? finisherHitDelay / Mathf.Max(0.01f, finisherAnimationSpeed) : slideDuration;

            pendingAttackCall = DOVirtual.DelayedCall(hitDelay, () =>
            {
                AudioManager.instance.PlayOneShot(FMODEvents.instance.playerAttack, transform.position);

                int hits = LightAttack(damageMultiplier, knockback, out bool killed, out bool arenaCleared, out Vector3 killedAt);
                swingHitLanded = true;
                if (hits == 0) return;

                // Hit-confirm sandwich: freeze + shake + rumble, scaled by how much
                // the hit mattered. The enemy hurt SFX and hit-flash come from
                // Health.TakeDamage.
                float shake = Mathf.Max(lungeDistance, 3f);
                if (killed && arenaCleared && finalBlowCamera != null)
                {
                    // Last enemy down: the final blow camera owns the slow-motion.
                    finalBlowCamera.Play(killedAt);
                    TriggerCameraShake(shake * 2f);
                    RumblePulse(0.7f, 1f, 0.3f);
                }
                else if (killed)
                {
                    HitStop.Play(killSlowMoDuration, killSlowMoScale);
                    TriggerCameraShake(shake * 2f);
                    RumblePulse(0.6f, 0.9f, 0.22f);
                }
                else if (finisher || counterStrike)
                {
                    HitStop.Play(hitStopFinisher);
                    TriggerCameraShake(shake * 1.6f);
                    RumblePulse(0.5f, 0.8f, 0.16f);
                }
                else
                {
                    HitStop.Play(hitStopLight);
                    TriggerCameraShake(shake);
                    RumblePulse();
                }
            });
        }

        /// <summary>
        /// Called by an enemy whose attack passed through the player's dodge
        /// i-frames: time dips and the next swing becomes a counter strike.
        /// </summary>
        public void OnPerfectDodge(Enemy enemy)
        {
            perfectDodgeUntil = Time.time + perfectDodgeWindow;
            HitStop.Play(perfectDodgeSlowMoDuration, perfectDodgeSlowMoScale);
            RumblePulse(0.2f, 0.5f, 0.1f);
            if (enemy != null) SpawnImpactVFX(enemy.transform.position + Vector3.up);
        }




        // Targets already damaged by the current swing, so an object with several
        // colliders (ragdoll parts, dummy segments) takes one hit, not one per collider.
        readonly System.Collections.Generic.HashSet<Component> damagedThisSwing = new();

        /// <summary>Applies melee damage in front of us. Returns how many targets were hit,
        /// and whether any of them died (and whether that emptied the arena).</summary>
        private int LightAttack(float damageMultiplier, float knockback, out bool killedSomething, out bool arenaCleared, out Vector3 killedAt)
        {
            killedSomething = false;
            arenaCleared = false;
            killedAt = transform.position + transform.forward * 2f;

            Vector3 attackPos = transform.position + transform.forward * lightAttackDistance;
            Collider[] hitEnemies = Physics.OverlapSphere(attackPos, lightAttackDistance);

            int damage = Mathf.RoundToInt(CurrentMeleeDamage() * damageMultiplier);
            int hits = 0;
            damagedThisSwing.Clear();

            foreach (var hit in hitEnemies)
            {
                // Colliders usually sit on child objects while the tag and the damage
                // component live on the parent, so resolve both up the hierarchy.
                var enemyHealth = hit.GetComponentInParent<Health>();
                if (enemyHealth != null)
                {
                    bool isEnemy = hit.CompareTag("Enemy") || enemyHealth.CompareTag("Enemy");
                    if (!isEnemy || enemyHealth.isDead || !damagedThisSwing.Add(enemyHealth)) continue;

                    enemyHealth.TakeDamage(damage, knockback);
                    SpawnImpactVFX(hit.ClosestPoint(attackPos));
                    hits++;

                    if (enemyHealth.isDead)
                    {
                        killedSomething = true;
                        killedAt = enemyHealth.transform.position;
                        // The dying enemy is still registered until its death state
                        // runs, so "one left" means this kill clears the arena.
                        if (EnemyManager.instance != null && EnemyManager.instance.AliveEnemyCount() <= 1)
                            arenaCleared = true;
                    }
                    continue;
                }

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable is Component damageableObject)
                {
                    bool isDestructable = hit.CompareTag("Destructable") || damageableObject.CompareTag("Destructable");
                    if (!isDestructable || !damagedThisSwing.Add(damageableObject)) continue;

                    damageable.TakeDamage(damage, knockback);
                    SpawnImpactVFX(hit.ClosestPoint(attackPos));
                    hits++;
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

        void RumblePulse(float lowFrequency = 0.35f, float highFrequency = 0.6f, float seconds = 0.12f)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            // TEMP diagnostic: remove once rumble is confirmed working.
            Debug.Log(pad == null
                ? "[Rumble] no gamepad is current (Gamepad.current is null)"
                : $"[Rumble] {pad.displayName} ({pad.GetType().Name}) low={lowFrequency} high={highFrequency} for {seconds}s");
            if (pad == null) return;
            pad.SetMotorSpeeds(lowFrequency, highFrequency);
            DOVirtual.DelayedCall(seconds, () => pad.SetMotorSpeeds(0f, 0f)).SetUpdate(true);
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
