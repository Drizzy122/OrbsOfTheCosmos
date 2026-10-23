using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Grows the player's max health as the story and the character progress
    /// (Insomniac Spider-Man style). The player starts small and earns the rest:
    ///
    ///   max health = baseMaxHealth
    ///              + finished quests * healthPerQuestFinished   (story progress)
    ///              + (level - 1)     * healthPerLevel           (character progress)
    ///
    /// Everything is recomputed from the level and the quest states, which are
    /// already saved, so there is nothing extra to persist. Each gain is granted as
    /// current health too (Health.SetMaxHealth heals by the delta), so a milestone
    /// feels like a reward rather than a suddenly emptier bar.
    /// Lives on the Player next to Health.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHealthScaling : MonoBehaviour
    {
        [Tooltip("Max health at the very start of the game, before any quest or level.")]
        [SerializeField] float baseMaxHealth = 25f;
        [Tooltip("Max health gained for every quest the player finishes.")]
        [SerializeField] float healthPerQuestFinished = 15f;
        [Tooltip("Max health gained for every level above 1.")]
        [SerializeField] float healthPerLevel = 5f;

        Health health;
        QuestManager questManager;
        int currentLevel = 1;

        /// <summary>What max health should be right now, from story and level progress.</summary>
        public float TargetMaxHealth =>
            baseMaxHealth
            + FinishedQuestCount() * healthPerQuestFinished
            + Mathf.Max(0, currentLevel - 1) * healthPerLevel;

        void Awake()
        {
            health = GetComponent<Health>();
            questManager = FindAnyObjectByType<QuestManager>();
        }

        void OnEnable()
        {
            var events = GameEventsManager.instance;
            if (events == null) return;
            events.playerEvents.onPlayerLevelChange += OnLevelChanged;
            events.questEvents.onQuestStateChange += OnQuestStateChanged;
        }

        void OnDisable()
        {
            var events = GameEventsManager.instance;
            if (events == null) return;
            events.playerEvents.onPlayerLevelChange -= OnLevelChanged;
            events.questEvents.onQuestStateChange -= OnQuestStateChanged;
        }

        // PlayerLevelManager and QuestManager both broadcast their restored state in
        // Start, so the first few calls here bring a loaded save up to its real max.
        void OnLevelChanged(int level)
        {
            currentLevel = level;
            Apply();
        }

        void OnQuestStateChanged(Quest quest)
        {
            Apply();
        }

        void Apply()
        {
            if (health != null) health.SetMaxHealth(TargetMaxHealth);
        }

        int FinishedQuestCount()
        {
            if (questManager == null) questManager = FindAnyObjectByType<QuestManager>();
            if (questManager == null) return 0;

            int finished = 0;
            foreach (Quest quest in questManager.Quests)
            {
                if (quest != null && quest.state == QuestState.FINISHED) finished++;
            }
            return finished;
        }
    }
}
