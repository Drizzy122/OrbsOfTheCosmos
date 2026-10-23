using ImprovedTimers;
using KBCore.Refs;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer
{
    public class PlayerAbilities : ValidatedMonoBehaviour
    {
        [field: Header("References")]
        [field: SerializeField, Anywhere] InputReader input;

        [field: Header("Equipped Ability")]
        // Instead of hardcoding Sonar Pulse, you drag ANY ability Strategy here!
        [SerializeField] private AbilityStrategy activeAbility;

        [Header("Cooldowns")]
        [Tooltip("The one and only ability cooldown, in seconds.")]
        [SerializeField] float sonarPulseCooldown = 0.5f;

        [Header("Combat")]
        [Tooltip("Sonar shares R3 with Lock On on a gamepad. While in combat (locked on, an enemy in lock range, mid-attack, or hit in the last few seconds) the press is left to Lock On and the ability does not fire.")]
        [SerializeField] bool blockedDuringCombat = true;

        // Seconds after the player's last attack / last hit taken that still count as combat.
        const float CombatLinger = 3f;

        private CountdownTimer pulseTimer;
        EnemyDetection enemyDetection;
        PlayerCombat combat;
        Health health;
        float lastCombatTime = -10f;

        void Awake()
        {
            pulseTimer = new CountdownTimer(sonarPulseCooldown);
            combat = GetComponentInParent<PlayerCombat>();
            health = GetComponentInParent<Health>();
            ResolveEnemyDetection();
        }

        // EnemyDetection lives on its own child object in the Player prefab, so a parent-only
        // lookup misses it. Prefer PlayerCombat's wired reference, then children, then the scene.
        void ResolveEnemyDetection()
        {
            if (enemyDetection != null) return;
            if (combat != null) enemyDetection = combat.enemyDetection;
            if (enemyDetection == null) enemyDetection = GetComponentInChildren<EnemyDetection>(true);
            if (enemyDetection == null) enemyDetection = GetComponentInParent<EnemyDetection>();
            if (enemyDetection == null) enemyDetection = FindAnyObjectByType<EnemyDetection>();
        }

        void OnHit(float _) => lastCombatTime = Time.time;

        /// <summary>Combat = a lock is active or possible, an attack is running, or one ran / landed on us recently.</summary>
        public bool IsInCombat
        {
            get
            {
                if (enemyDetection == null) ResolveEnemyDetection();
                if (enemyDetection != null && (enemyDetection.IsLockedOn || enemyDetection.HasEnemyInLockRange())) return true;
                if (combat != null && (combat.IsAttacking || combat.IsBlastAttacking)) lastCombatTime = Time.time;
                return Time.time - lastCombatTime < CombatLinger;
            }
        }

        void OnSonarPulse(bool performed)
        {
            if (performed && blockedDuringCombat && IsInCombat) return;

            if (performed && !pulseTimer.IsRunning)
            {
                ExecuteActiveAbility();
                pulseTimer.Start();
                AudioManager.instance.PlayOneShot(FMODEvents.instance.playerEcolocation, transform.position);
            }
        }

        // The pulseTimer above is the single cooldown gate — strategies no
        // longer carry their own.
        public void ExecuteActiveAbility()
        {
            if (activeAbility != null)
            {
                activeAbility.Execute(this.gameObject);
            }
            else
            {
                Debug.LogWarning("No ability equipped!");
            }
        }

        #region X-Ray Helper
        // We leave this helper here because MonoBehaviour handles Invoke/Coroutines better than ScriptableObjects
        private ScriptableRendererFeature currentXRay;

        public void TriggerXRay(ScriptableRendererFeature feature, float duration)
        {
            if (feature == null) return;

            currentXRay = feature;
            currentXRay.SetActive(true);

            CancelInvoke(nameof(DisableXRay));
            Invoke(nameof(DisableXRay), duration);
        }

        private void DisableXRay()
        {
            if (currentXRay != null) currentXRay.SetActive(false);
        }
        #endregion
        
        void OnEnable()
        {
            input.SonarPulse += OnSonarPulse;
            if (health != null) health.OnHit += OnHit;
        }

        void OnDisable()
        {
            input.SonarPulse -= OnSonarPulse;
            if (health != null) health.OnHit -= OnHit;
        }
    }
}
