using UnityEngine;
using UnityEngine.UIElements; // Required for UI Toolkit

namespace Platformer
{
    public class GlideStamina : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float staminaRegenRate = 1f;
        
        [Header("Events")]
        [SerializeField] private FloatEventChannel staminaEventChannel; // Kept in case you still want to broadcast

        [Header("UI Toolkit Integration")]
        [SerializeField] private PanelRenderer uiDocument;
        [SerializeField] private string targetElementID = "StaminaBar";

        private float currentStamina;
        private bool isGliding;
        private PlayerMovement _playerMovement;

        // UI Toolkit Variables
        private VisualElement barFill;

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
            barFill = root.Q<VisualElement>(targetElementID);
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

            // Broadcast and update UI every frame it changes
            PublishStamina();
        }

        private void PublishStamina()
        {
            float fraction = 0f;
            if (_playerMovement != null && _playerMovement.glideCoolDown > 0f)
            {
                // Calculate the 0-1 percentage
                fraction = currentStamina / _playerMovement.glideCoolDown;
            }

            // 1. Broadcast to Event Bus (Optional, kept from your original script)
            if (staminaEventChannel != null)
            {
                staminaEventChannel.Invoke(fraction);
            }

            // 2. Direct UI Toolkit Update
            if (barFill != null)
            {
                // Snap the width (Rely on USS transitions for smoothing)
                barFill.style.width = new Length(fraction * 100f, LengthUnit.Percent);
            }
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