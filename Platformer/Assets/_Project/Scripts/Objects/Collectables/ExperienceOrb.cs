using UnityEngine;

namespace Platformer
{
    public class ExperienceOrb : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int experienceGained = 10;

        private SphereCollider sphereCollider;
        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
        }

        private void Start()
        {
            // Register with the player's magnet so runtime-spawned orbs
            // (enemy drops) get pulled in — the magnet only tag-scans once at Start.
            var magnet = FindAnyObjectByType<Magnet>();
            if (magnet != null) magnet.Register(gameObject);
        }
        private void CollectExperience()
        {
            sphereCollider.enabled = false;
            gameObject.SetActive(false);

            // Fly a HUD dot into the XP bar; the XP is granted when it arrives.
            // If the HUD effect isn't available, grant instantly instead.
            if (!HUDXPOrbEffect.TryFly(transform.position, experienceGained))
            {
                GameEventsManager.instance.playerEvents.ExperienceGained(experienceGained);
                GameEventsManager.instance.miscEvents.XPCollected();
            }
        }
        private void OnTriggerEnter(Collider otherCollider) 
        {
            if (otherCollider.CompareTag("Player"))
            {
                CollectExperience();
                Destroy(gameObject,6f);
            }
        }
    }
}