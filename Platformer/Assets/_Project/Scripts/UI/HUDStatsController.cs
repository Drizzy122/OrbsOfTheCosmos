using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Drives the HUD's XP bar, level label, and lumin charge ("projectile") bar.
    /// Attach to the HUD GameObject (the one whose UIDocument shows HUD.uxml).
    /// Listens to the global player events — no direct player references needed.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class HUDStatsController : MonoBehaviour
    {
        [SerializeField] PanelRenderer document;

        [Tooltip("Charge count that renders the bar completely full.")]
        [SerializeField, Min(1)] int chargesFullBar = 10;

        [Header("Health bar growth (Spider-Man style)")]
        [Tooltip("Health bar width at level 1, in px.")]
        [SerializeField] float baseHealthBarWidth = 260f;
        [Tooltip("Extra bar width per level gained.")]
        [SerializeField] float healthBarWidthPerLevel = 8f;

        [Header("Level-up popup")]
        [Tooltip("Seconds the LEVEL UP toast stays on screen.")]
        [SerializeField] float levelUpDuration = 4f;

        [Header("XP bar visibility")]
        [Tooltip("Seconds the XP bar stays on screen after the last XP gain.")]
        [SerializeField] float xpBarLingerTime = 3f;

        VisualElement xpBar;
        VisualElement xpContainer;
        Label levelLabel;
        VisualElement chargesFill;
        Label chargesLabel;
        VisualElement healthContainer;
        Label comboCount;
        HUDPopup levelUpPopup;
        IVisualElementScheduledItem xpHideItem;

        // Last level we displayed; used to pop the toast only on real
        // level-ups, not on the initial broadcast at game start.
        int lastSeenLevel = -1;

        // Last XP value we displayed; the bar only reveals itself when the
        // value actually changes (skips the initial broadcast too).
        int lastSeenXp = int.MinValue;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable()
        {
            document.RegisterUIReloadCallback(OnUIReload);

            var events = GameEventsManager.instance.playerEvents;
            events.onPlayerExperienceChange += OnExperienceChanged;
            events.onPlayerLevelChange += OnLevelChanged;
            events.onLuminChargesChanged += OnChargesChanged;
            events.onComboChanged += OnComboChanged;
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            xpBar = root.Q<VisualElement>("XPBar");
            xpContainer = root.Q<VisualElement>("XPContainer");
            levelLabel = root.Q<Label>("XPLevelLabel");
            chargesFill = root.Q<VisualElement>("ChargesFill");
            chargesLabel = root.Q<Label>("ChargesLabel");
            healthContainer = root.Q<VisualElement>("HealthContainer");
            comboCount = root.Q<Label>("ComboCount");

            // XP bar starts invisible and only fades in when XP is gained
            if (xpContainer != null)
            {
                xpContainer.style.opacity = 0f;
                xpHideItem = xpContainer.schedule.Execute(HideXpBar);
                xpHideItem.Pause();
            }
            lastSeenXp = int.MinValue;

            var levelUpElement = root.Q<VisualElement>("LevelUpNotification");
            if (levelUpElement != null) levelUpPopup = new HUDPopup(levelUpElement, levelUpDuration);
        }

        void OnDisable()
        {
            document.UnregisterUIReloadCallback(OnUIReload);
            if (GameEventsManager.instance == null) return;
            var events = GameEventsManager.instance.playerEvents;
            events.onPlayerExperienceChange -= OnExperienceChanged;
            events.onPlayerLevelChange -= OnLevelChanged;
            events.onLuminChargesChanged -= OnChargesChanged;
            events.onComboChanged -= OnComboChanged;
        }

        void OnExperienceChanged(int currentXp)
        {
            if (xpBar != null)
            {
                float pct = Mathf.Clamp01((float)currentXp / GlobalConstants.experienceToLevelUp) * 100f;
                xpBar.style.width = Length.Percent(pct);
            }

            // Reveal the bar only on genuine changes — the very first broadcast
            // is just PlayerLevelManager syncing state on scene load.
            bool firstBroadcast = lastSeenXp == int.MinValue;
            bool changed = currentXp != lastSeenXp;
            lastSeenXp = currentXp;
            if (firstBroadcast || !changed) return;

            ShowXpBar();
        }

        void ShowXpBar()
        {
            if (xpContainer == null) return;
            xpContainer.style.opacity = 1f;
            xpHideItem?.ExecuteLater((long)(xpBarLingerTime * 1000f));
        }

        void HideXpBar()
        {
            if (xpContainer != null) xpContainer.style.opacity = 0f;
        }

        void OnLevelChanged(int level)
        {
            if (levelLabel != null) levelLabel.text = $"You are now level {level}";

            // The health bar physically widens with each level, matching the
            // max-health growth from PlayerHealthScaling.
            if (healthContainer != null)
                healthContainer.style.width = baseHealthBarWidth + (level - 1) * healthBarWidthPerLevel;

            // Pop the LEVEL UP toast only on genuine level-ups; the very first
            // broadcast is just PlayerLevelManager syncing the starting level.
            if (lastSeenLevel > 0 && level > lastSeenLevel)
                levelUpPopup?.Show();
            lastSeenLevel = level;
        }

        void OnComboChanged(int combo)
        {
            if (comboCount != null) comboCount.text = combo.ToString();
        }

        void OnChargesChanged(int charges)
        {
            if (chargesFill != null)
            {
                float pct = Mathf.Clamp01((float)charges / chargesFullBar) * 100f;
                chargesFill.style.width = Length.Percent(pct);
            }
            if (chargesLabel != null) chargesLabel.text = $"x{charges}";
        }
    }
}
