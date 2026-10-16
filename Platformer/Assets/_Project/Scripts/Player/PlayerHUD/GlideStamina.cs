using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Glide stamina: drains while gliding, regenerates otherwise, and drives the
    /// HUD stamina bar (HUDBar.shader through HUDBarView). The bar dims when
    /// full so it only draws the eye while it matters.
    /// </summary>
    public class GlideStamina : MonoBehaviour
    {
        const string FullClass = "is-full";

        [Header("Settings")]
        [SerializeField] private float staminaRegenRate = 1f;

        [Header("Events")]
        [SerializeField] private FloatEventChannel staminaEventChannel;

        [Header("UI Toolkit Integration")]
        [SerializeField] private PanelRenderer uiDocument;
        [SerializeField] private string targetElementID = "StaminaBar";
        [SerializeField] private string containerElementID = "StaminaContainer";

        private float currentStamina;
        private bool isGliding;
        private PlayerMovement _playerMovement;

        private HUDBarView bar;
        private VisualElement container;
        private float lastPublishedFraction = -1f;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();

            if (_playerMovement != null)
                currentStamina = _playerMovement.glideCoolDown;
        }

        private void Start()
        {
            // The HUD panel hands us its root via the reload callback
            if (uiDocument != null)
            {
                uiDocument.RegisterUIReloadCallback(OnUIReload);
            }
            else
            {
                Debug.LogWarning("Panel renderer is not assigned in GlideStamina!");
            }
        }

        private void OnDestroy()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            var fill = root.Q<VisualElement>(targetElementID);
            container = root.Q<VisualElement>(containerElementID);

            // Stamina drains continuously, so the chip hugs the fill instead of lagging.
            bar?.Release();
            bar = fill != null
                ? new HUDBarView(fill, HUDBarView.LoadMaterial("UI/HUDStaminaBar")) { fillSpeed = 14f, chipDelay = 0f, chipSpeed = 3f }
                : null;

            lastPublishedFraction = -1f;
            bar?.Snap(CurrentFraction());
            PublishStamina();
        }

        private void Update()
        {
            if (isGliding)
            {
                DrainStamina();
            }
            else
            {
                RegenerateStamina();
            }

            PublishStamina();
        }

        private float CurrentFraction()
        {
            if (_playerMovement == null || _playerMovement.glideCoolDown <= 0f) return 0f;
            return currentStamina / _playerMovement.glideCoolDown;
        }

        // Only broadcasts when the value actually moved — no per-frame event
        // spam or style writes while the bar is sitting full.
        private void PublishStamina()
        {
            float fraction = CurrentFraction();
            if (Mathf.Abs(fraction - lastPublishedFraction) < 0.0005f) return;
            lastPublishedFraction = fraction;

            if (staminaEventChannel != null)
            {
                staminaEventChannel.Invoke(fraction);
            }

            bar?.SetTarget(fraction);
            container?.EnableInClassList(FullClass, fraction >= 0.999f);
        }

        public void StartGlide()
        {
            if (_playerMovement != null && currentStamina > 0f)
            {
                isGliding = true;
            }
        }

        public void StopGlide()
        {
            isGliding = false;
        }

        private void DrainStamina()
        {
            if (currentStamina > 0f && _playerMovement != null)
            {
                currentStamina -= Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0f, _playerMovement.glideCoolDown);

                if (currentStamina <= 0f)
                {
                    StopGlideExternally();
                }
            }
        }

        private void RegenerateStamina()
        {
            if (_playerMovement != null && currentStamina < _playerMovement.glideCoolDown)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0f, _playerMovement.glideCoolDown);
            }
        }

        private void StopGlideExternally()
        {
            StopGlide();
            if (_playerMovement != null)
            {
                _playerMovement.OnGlide(false);
            }
        }
    }
}
