using UnityEngine;
using UnityEngine.UIElements;
namespace Platformer
{
    public class HealthBar : MonoBehaviour
    {
        [field: Header("UI Configuration")]
        [field: SerializeField] private PanelRenderer uiDocument;
        [field: SerializeField] private float lerpSpeed = 5f; // Kept from your original script

        private VisualElement healthBarFill;
        private float targetFill = 1f;
        private float currentFill = 1f;

        private void OnEnable()
        {
            if (uiDocument == null) return;
            uiDocument.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            healthBarFill = root.Q<VisualElement>("HealthBar");
        }

        // Your FloatEventListener will call this method
        public void UpdateTargetFill(float percentage) 
        {
            targetFill = percentage;
        }

        private void Update()
        {
            if (healthBarFill == null) return;
            currentFill = Mathf.Lerp(currentFill, targetFill, lerpSpeed * Time.deltaTime);
            healthBarFill.style.width = new Length(currentFill * 100f, LengthUnit.Percent);
        }
    }
}