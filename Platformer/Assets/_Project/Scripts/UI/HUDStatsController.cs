using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Drives the HUD's XP bar, level badge, lumin charge pips, combo counter and
    /// the level-up toast. Attach to the HUD GameObject (the one whose
    /// PanelRenderer shows HUD.uxml). Listens to the global player events — no
    /// direct player references needed.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class HUDStatsController : MonoBehaviour
    {
        const string XpActiveClass = "is-active";
        const string PipClass = "hud-pip";
        const string PipOnClass = "hud-pip--on";
        const string PipPopClass = "hud-pip--pop";
        const string ComboHiddenClass = "hud-combo--hidden";
        const string ComboPopClass = "hud-combo--pop";
        const long PopMs = 60;

        [SerializeField] PanelRenderer document;

        [Tooltip("Number of charge pips drawn. Charges beyond this still count in the label.")]
        [SerializeField, Min(1)] int chargesFullBar = 10;

        [Header("Health bar growth (Spider-Man style)")]
        [Tooltip("Fallback level-1 width in px. Normally the width comes from HUD.uss (.hud-bar--health).")]
        [SerializeField] float baseHealthBarWidth = 340f;
        [Tooltip("Extra bar width per level gained.")]
        [SerializeField] float healthBarWidthPerLevel = 8f;

        [Header("Level-up popup")]
        [Tooltip("Seconds the LEVEL UP toast stays on screen.")]
        [SerializeField] float levelUpDuration = 4f;

        [Header("XP bar")]
        [Tooltip("Seconds the XP bar stays bright after the last XP gain.")]
        [SerializeField] float xpBarLingerTime = 3f;

        HUDBarView xpBar;
        VisualElement xpContainer;
        Label levelLabel;
        Label levelBadgeLabel;
        VisualElement chargesContainer;
        readonly List<VisualElement> pips = new();
        Label chargesLabel;
        VisualElement healthContainer;
        VisualElement comboContainer;
        Label comboCount;
        HUDPopup levelUpPopup;
        IVisualElementScheduledItem xpDimItem;
        IVisualElementScheduledItem comboPopItem;

        // Last level we displayed; used to pop the toast only on real
        // level-ups, not on the initial broadcast at game start.
        int lastSeenLevel = -1;

        // Last XP value we displayed; the bar only brightens when the
        // value actually changes (skips the initial broadcast too).
        int lastSeenXp = int.MinValue;

        int lastCharges;
        int lastCombo;

        // Level-1 bar width as laid out by the stylesheet; read once after the
        // first layout so USS stays the single place that sets the look.
        float styleBaseWidth = -1f;

        void Reset() => document = GetComponent<PanelRenderer>();

        void Awake()
        {
            if (document == null) document = GetComponent<PanelRenderer>();

            // The lock-on reticle lives on this same HUD object. Added here so the
            // scene needs no extra wiring; it finds the player's EnemyDetection itself.
            if (GetComponent<HUDTargetMarker>() == null) gameObject.AddComponent<HUDTargetMarker>();
        }

        void OnEnable()
        {
            document.RegisterUIReloadCallback(OnUIReload);

            var events = GameEventsManager.instance.playerEvents;
            events.onPlayerExperienceChange += OnExperienceChanged;
            events.onPlayerLevelChange += OnLevelChanged;
            events.onLuminChargesChanged += OnChargesChanged;
            events.onComboChanged += OnComboChanged;
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

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            var xpFill = root.Q<VisualElement>("XPBar");
            xpContainer = root.Q<VisualElement>("XPContainer");
            levelLabel = root.Q<Label>("XPLevelLabel");
            levelBadgeLabel = root.Q<Label>("LevelBadgeLabel");
            chargesContainer = root.Q<VisualElement>("ChargesContainer");
            chargesLabel = root.Q<Label>("ChargesLabel");
            healthContainer = root.Q<VisualElement>("HealthContainer");
            comboContainer = root.Q<VisualElement>("ComboContainer");
            comboCount = root.Q<Label>("ComboCount");

            xpBar?.Release();
            xpBar = xpFill != null
                ? new HUDBarView(xpFill, HUDBarView.LoadMaterial("UI/HUDXPBar")) { fillSpeed = 6f, chipDelay = 0f, chipSpeed = 4f }
                : null;

            if (xpContainer != null)
            {
                xpContainer.RemoveFromClassList(XpActiveClass);
                xpDimItem = xpContainer.schedule.Execute(() => xpContainer.RemoveFromClassList(XpActiveClass));
                xpDimItem.Pause();
            }
            lastSeenXp = int.MinValue;

            BuildPips();
            ApplyCharges(lastCharges, pop: false);

            if (comboContainer != null)
            {
                comboPopItem = comboContainer.schedule.Execute(() => comboContainer.RemoveFromClassList(ComboPopClass));
                comboPopItem.Pause();
                comboContainer.EnableInClassList(ComboHiddenClass, lastCombo <= 0);
                if (comboCount != null) comboCount.text = lastCombo.ToString();
            }

            if (lastSeenLevel > 0 && levelBadgeLabel != null) levelBadgeLabel.text = lastSeenLevel.ToString();

            if (healthContainer != null)
            {
                styleBaseWidth = -1f;
                healthContainer.style.width = StyleKeyword.Null;   // let USS lay it out first
                healthContainer.RegisterCallback<GeometryChangedEvent>(OnHealthBarFirstLayout);
            }

            var levelUpElement = root.Q<VisualElement>("LevelUpNotification");
            if (levelUpElement != null) levelUpPopup = new HUDPopup(levelUpElement, levelUpDuration);
        }

        // ── XP ───────────────────────────────────────────────────────────────

        void OnExperienceChanged(int currentXp)
        {
            float pct = Mathf.Clamp01((float)currentXp / GlobalConstants.experienceToLevelUp);

            // Brighten the bar only on genuine changes — the very first broadcast
            // is just PlayerLevelManager syncing state on scene load.
            bool firstBroadcast = lastSeenXp == int.MinValue;
            bool changed = currentXp != lastSeenXp;
            lastSeenXp = currentXp;

            if (firstBroadcast)
            {
                xpBar?.Snap(pct);
                return;
            }

            xpBar?.SetTarget(pct);
            if (changed) BrightenXpBar();
        }

        void BrightenXpBar()
        {
            if (xpContainer == null) return;
            xpContainer.AddToClassList(XpActiveClass);
            xpDimItem?.ExecuteLater((long)(xpBarLingerTime * 1000f));
        }

        // ── Level ────────────────────────────────────────────────────────────

        float HealthBarWidthFor(int level)
        {
            float baseWidth = styleBaseWidth > 0f ? styleBaseWidth : baseHealthBarWidth;
            return baseWidth + (Mathf.Max(level, 1) - 1) * healthBarWidthPerLevel;
        }

        void OnHealthBarFirstLayout(GeometryChangedEvent evt)
        {
            healthContainer.UnregisterCallback<GeometryChangedEvent>(OnHealthBarFirstLayout);
            float w = healthContainer.resolvedStyle.width;
            if (!float.IsNaN(w) && w > 0f) styleBaseWidth = w;
            if (lastSeenLevel > 1) healthContainer.style.width = HealthBarWidthFor(lastSeenLevel);
        }

        void OnLevelChanged(int level)
        {
            if (levelLabel != null) levelLabel.text = $"You are now level {level}";
            if (levelBadgeLabel != null) levelBadgeLabel.text = level.ToString();

            // The health bar physically widens with each level, matching the
            // max-health growth from PlayerHealthScaling. Level 1 keeps the
            // stylesheet width untouched.
            if (healthContainer != null && level > 1) healthContainer.style.width = HealthBarWidthFor(level);

            // Pop the LEVEL UP toast only on genuine level-ups; the very first
            // broadcast is just PlayerLevelManager syncing the starting level.
            if (lastSeenLevel > 0 && level > lastSeenLevel)
                levelUpPopup?.Show();
            lastSeenLevel = level;
        }

        // ── Combo ────────────────────────────────────────────────────────────

        void OnComboChanged(int combo)
        {
            bool grew = combo > lastCombo;
            lastCombo = combo;

            if (comboCount != null) comboCount.text = combo.ToString();
            if (comboContainer == null) return;

            comboContainer.EnableInClassList(ComboHiddenClass, combo <= 0);
            if (combo > 0 && grew && comboPopItem != null)
            {
                comboContainer.AddToClassList(ComboPopClass);
                comboPopItem.ExecuteLater(PopMs);
            }
        }

        // ── Charges ──────────────────────────────────────────────────────────

        void BuildPips()
        {
            pips.Clear();
            if (chargesContainer == null) return;
            chargesContainer.Clear();
            for (int i = 0; i < chargesFullBar; i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList(PipClass);
                chargesContainer.Add(pip);
                pips.Add(pip);
            }
        }

        void OnChargesChanged(int charges)
        {
            bool gained = charges > lastCharges;
            lastCharges = charges;
            ApplyCharges(charges, pop: gained);
        }

        void ApplyCharges(int charges, bool pop)
        {
            if (chargesLabel != null) chargesLabel.text = $"x{charges}";

            for (int i = 0; i < pips.Count; i++)
            {
                var pip = pips[i];
                bool on = i < charges;
                bool wasOn = pip.ClassListContains(PipOnClass);
                pip.EnableInClassList(PipOnClass, on);

                if (pop && on && !wasOn)
                {
                    pip.AddToClassList(PipPopClass);
                    pip.schedule.Execute(() => pip.RemoveFromClassList(PipPopClass)).ExecuteLater(PopMs);
                }
            }
        }
    }
}
