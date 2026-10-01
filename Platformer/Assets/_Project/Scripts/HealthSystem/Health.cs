using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using KBCore.Refs;
using Unity.Cinemachine;
using UnityEngine.VFX;
using System;

namespace Platformer
{
    public enum EntityHealth
    {
        Player,
        Enemy
    }
    public class Health : ValidatedMonoBehaviour, IDamageable
    {
        [field: Header("Configuration")]
        [field: SerializeField] EntityHealth entityHealth;
        [field: SerializeField, Anywhere] FloatEventChannel healthEventChannel;
        
        [field: Header("Health")] 
        [field: SerializeField] float maxHealth = 100f;
        
        [field: Header("Player Settings")]
        [field: SerializeField, Anywhere] CinemachineOrbitalFollow freeLookCam;
        [field: SerializeField, Anywhere] Renderer playerMeshRenderer;
        [field: SerializeField] public float iFramesDuration { get; private set; } = 2f;
        [field: SerializeField] int numberOfFlashes = 10;
        
        [field: Header("Enemy Settings")]
        [field: SerializeField] SkinnedMeshRenderer enemySkinnedMesh;
        [field: SerializeField] VisualEffect VFXGraph;
        [field: SerializeField] float dissolveRate = 0.0125f;
        [field: SerializeField] float refreshRate = 0.025f;
        [field: SerializeField] float hitFlashDuration = 0.08f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Coroutine hitFlashRoutine;
        
        [field: Header("State")]
        public float currentHealth { get; private set; }
        public bool isDead { get; private set; }
        public bool IsInvulnerable { get; private set; }

        // 0..1 health fraction. Safe against a zero maxHealth.
        public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        private Material[] skinnedMaterials;
        public event Action<float> OnHit;
        public event Action OnDeath;

        private void OnDisable()
        {
            Physics.IgnoreLayerCollision(6, 7, false);
            IsInvulnerable = false;
        }

        private void Awake()
        {
            currentHealth = maxHealth;
            
            if (entityHealth == EntityHealth.Player)
            {
                if (playerMeshRenderer == null) 
                    playerMeshRenderer = GetComponentInChildren<Renderer>();
            }
            else if (entityHealth == EntityHealth.Enemy)
            {
                if (enemySkinnedMesh != null)
                    skinnedMaterials = enemySkinnedMesh.materials;
                
                if (VFXGraph != null) 
                    VFXGraph.Stop();
            }
        }

        private void Start() => PublishHealthPercentage();
        
        public void AddHealth(float value)
        {
            currentHealth = Mathf.Clamp(currentHealth + value, 0, maxHealth);
            PublishHealthPercentage();
        }

        /// <summary>Change max health (e.g. level-up scaling). When growing and
        /// healByDelta is true, the gained chunk is granted as current health too —
        /// leveling up feels like a reward, not a suddenly-emptier bar.</summary>
        public void SetMaxHealth(float newMax, bool healByDelta = true)
        {
            float delta = newMax - maxHealth;
            maxHealth = Mathf.Max(1f, newMax);
            if (healByDelta && delta > 0f) currentHealth += delta;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            PublishHealthPercentage();
        }
        
        private void PublishHealthPercentage()
        {
            if (healthEventChannel != null)
                healthEventChannel.Invoke(currentHealth / maxHealth);
        }

        /// <summary>External i-frame control (e.g. dodge). Player-only — enemies
        /// never get invulnerability. Reuses the same player/enemy collision-ignore
        /// trick as the post-hit invulnerability.</summary>
        public void SetInvulnerable(bool value)
        {
            if (entityHealth != EntityHealth.Player) return;
            IsInvulnerable = value;
            Physics.IgnoreLayerCollision(6, 7, value);
        }
        
        AbilityTree abilityTree;

        public void TakeDamage(float damage, float knockBackTime = 0f)
        {
            // Defense branch of the ability tree — flat damage reduction, never below 1.
            // Only the player has an AbilityTree component; enemies are unaffected.
            if (abilityTree == null) abilityTree = GetComponentInChildren<AbilityTree>();
            if (abilityTree != null && damage > 0f)
                damage = Mathf.Max(1f, damage - abilityTree.GetStat(AbilityTree.StatDefense));

            currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
            if (currentHealth > 0)
            {
                HandleDamage(knockBackTime);
            }
            else
            {
                HandleDeath();
            }
            PublishHealthPercentage();
        }

        private void HandleDamage(float knockBackTime)
        {
            switch (entityHealth)
            {
                case EntityHealth.Player:
                    OnHit?.Invoke(knockBackTime);
                    GameEventsManager.instance.playerEvents.PlayerHit(knockBackTime);
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.playerHurt, transform.position);
                    StartCoroutine(Invunerability());
                    break;

                case EntityHealth.Enemy:
                    OnHit?.Invoke(knockBackTime);
                    GameEventsManager.instance.enemyEvents.EnemyHit(knockBackTime);
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.enemyHurt, transform.position);

                    if (enemySkinnedMesh != null)
                    {
                        if (hitFlashRoutine != null) StopCoroutine(hitFlashRoutine);
                        hitFlashRoutine = StartCoroutine(HitFlash());
                    }
                    break;
            }
        }

        public void HandleDeath()
        {
            if (!isDead)
            {
                isDead = true;
                OnDeath?.Invoke();
                switch (entityHealth)
                {
                    case EntityHealth.Player:
                        GameEventsManager.instance.playerEvents.PlayerDeath();
                        AudioManager.instance.PlayOneShot(FMODEvents.instance.playerDeath, transform.position);
                        AudioManager.instance.StopMusic();
                        if (freeLookCam != null) freeLookCam.gameObject.SetActive(false);
                        Invoke(nameof(RestartScene), 5f);
                        break;

                    case EntityHealth.Enemy:
                        AudioManager.instance.PlayOneShot(FMODEvents.instance.enemyDeath, transform.position);
                        GameEventsManager.instance.enemyEvents.EnemyDeath();
                        StartCoroutine(DissolveCo());
                        break;
                }
            }
        }

        private IEnumerator Invunerability()
        {
            IsInvulnerable = true;
            Physics.IgnoreLayerCollision(6, 7, true);
            for (int i = 0; i < numberOfFlashes; i++)
            {
                playerMeshRenderer.material.color = new Color(1, 0, 0, 0.1f);
                yield return new WaitForSeconds(iFramesDuration / (numberOfFlashes * 2));
                playerMeshRenderer.material.color = Color.white;
                yield return new WaitForSeconds(iFramesDuration / (numberOfFlashes * 2));
            }

            Physics.IgnoreLayerCollision(6, 7, false);
            IsInvulnerable = false;
        }

        // Overbright white flash via MaterialPropertyBlock — no material instancing,
        // resets cleanly, and silently does nothing if the shader has no color property.
        private IEnumerator HitFlash()
        {
            var mat = enemySkinnedMesh.sharedMaterial;
            int prop = mat != null && mat.HasProperty(BaseColorId) ? BaseColorId
                     : mat != null && mat.HasProperty(ColorId) ? ColorId : -1;
            if (prop == -1) yield break;

            var block = new MaterialPropertyBlock();
            enemySkinnedMesh.GetPropertyBlock(block);
            block.SetColor(prop, Color.white * 4f);
            enemySkinnedMesh.SetPropertyBlock(block);

            // Realtime so the flash isn't stretched by hitstop
            yield return new WaitForSecondsRealtime(hitFlashDuration);

            block.Clear();
            enemySkinnedMesh.SetPropertyBlock(block);
            hitFlashRoutine = null;
        }

        private void RestartScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        
        IEnumerator DissolveCo()
        {
            if (VFXGraph != null)
            {
                VFXGraph.Play();
            }
            if (skinnedMaterials.Length > 0)
            {
                float counter = 0;
                while (skinnedMaterials[0].GetFloat("_DissolveAmount") < 1)
                {
                    counter += dissolveRate;
                    for (int i = 0; i < skinnedMaterials.Length; i++)
                    {
                        skinnedMaterials[i].SetFloat("_DissolveAmount", counter);
                    }
                    yield return new WaitForSeconds(refreshRate);
                }
            }
        }
    }
}