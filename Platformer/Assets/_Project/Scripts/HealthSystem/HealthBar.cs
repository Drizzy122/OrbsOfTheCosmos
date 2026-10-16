using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Player health on the HUD. The bar itself is drawn by HUDBar.shader through
    /// HUDBarView (smoothed fill, lingering damage chip, hit flash). This script
    /// adds the numeric readout, the plate nudge on a hit, and the low-health
    /// state: red frame, shader pulse and the screen-edge vignette.
    /// UpdateTargetFill is still the entry point the FloatEventListener calls.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        const string PlateHitClass = "hud-plate--hit";
        const string PlateLowClass = "hud-plate--low";
        const string VignetteOnClass = "hud-vignette--on";
        const long PlateHitMs = 70;

        [field: Header("UI Configuration")]
        [field: SerializeField] private PanelRenderer uiDocument;
        [Tooltip("How quickly the fill chases the real value (higher = snappier).")]
        [field: SerializeField] private float lerpSpeed = 5f;

        [Header("Low health")]
        [Tooltip("Health fraction at or below which the low-health state kicks in.")]
        [SerializeField, Range(0f, 1f)] float lowHealthThreshold = 0.3f;
        [Tooltip("Seconds per vignette pulse.")]
        [SerializeField] float pulseInterval = 0.7f;

        HUDBarView bar;
        VisualElement plate;
        VisualElement vignette;
        Label healthLabel;
        IVisualElementScheduledItem pulseItem;
        IVisualElementScheduledItem plateHitItem;

        float targetFill = 1f;
        float lastCurrent = -1f;
        float lastMax = -1f;
        bool isLow;
        bool pulseOn;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<PanelRenderer>();
            if (uiDocument != null) uiDocument.RegisterUIReloadCallback(OnUIReload);

            if (GameEventsManager.instance == null) return;
            var events = GameEventsManager.instance.playerEvents;
            events.onPlayerHit += OnPlayerHit;
            events.onPlayerHealthChanged += OnPlayerHealthChanged;
        }

        private void OnDisable()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);

            if (GameEventsManager.instance == null) return;
            var events = GameEventsManager.instance.playerEvents;
            events.onPlayerHit -= OnPlayerHit;
            events.onPlayerHealthChanged -= OnPlayerHealthChanged;
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            var fill = root.Q<VisualElement>("HealthBar");
            plate = root.Q<VisualElement>("PlayerPlate");
            vignette = root.Q<VisualElement>("LowHealthVignette");
            healthLabel = root.Q<Label>("HealthLabel");

            bar?.Release();
            bar = fill != null
                ? new HUDBarView(fill, HUDBarView.LoadMaterial("UI/HUDHealthBar")) { fillSpeed = lerpSpeed * 2f }
                : null;
            bar?.Snap(targetFill);

            if (vignette != null)
            {
                pulseItem = vignette.schedule.Execute(TogglePulse).Every((long)(pulseInterval * 1000f));
                pulseItem.Pause();
                vignette.RemoveFromClassList(VignetteOnClass);
            }

            if (plate != null)
            {
                plateHitItem = plate.schedule.Execute(() => plate.RemoveFromClassList(PlateHitClass));
                plateHitItem.Pause();
                plate.RemoveFromClassList(PlateHitClass);
            }

            isLow = false;
            pulseOn = false;
            UpdateLowState();
            RefreshLabel();
        }

        /// <summary>Called by the FloatEventListener with the 0..1 health fraction.</summary>
        public void UpdateTargetFill(float percentage)
        {
            targetFill = Mathf.Clamp01(percentage);
            bar?.SetTarget(targetFill);
            UpdateLowState();
        }

        private void OnPlayerHit(float _)
        {
            bar?.Flash();
            if (plate == null || plateHitItem == null) return;
            plate.AddToClassList(PlateHitClass);
            plateHitItem.ExecuteLater(PlateHitMs);
        }

        // Second route into the bar, independent of the FloatEventListener wiring
        // in the scene: Health broadcasts hit points on every change.
        private void OnPlayerHealthChanged(float current, float max)
        {
            lastCurrent = current;
            lastMax = max;
            RefreshLabel();
            if (max > 0f) UpdateTargetFill(current / max);
        }

        private void RefreshLabel()
        {
            if (healthLabel == null) return;
            if (lastMax <= 0f)
            {
                healthLabel.text = string.Empty;
                return;
            }
            healthLabel.text = $"{Mathf.CeilToInt(lastCurrent)} / {Mathf.RoundToInt(lastMax)}";
        }

        private void UpdateLowState()
        {
            bool low = targetFill <= lowHealthThreshold;
            if (low == isLow) return;
            isLow = low;

            bar?.SetLow(low ? 1f : 0f);
            plate?.EnableInClassList(PlateLowClass, low);

            if (pulseItem == null) return;
            if (low)
            {
                pulseOn = false;
                TogglePulse();
                pulseItem.Resume();
            }
            else
            {
                pulseItem.Pause();
                pulseOn = false;
                vignette?.RemoveFromClassList(VignetteOnClass);
            }
        }

        private void TogglePulse()
        {
            pulseOn = !pulseOn;
            vignette?.EnableInClassList(VignetteOnClass, pulseOn);
        }
    }
}
