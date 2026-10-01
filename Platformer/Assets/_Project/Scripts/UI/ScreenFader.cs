using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] PanelRenderer uiDocument;
        [SerializeField] float fadeDuration = 1f;

        VisualElement overlay;
        bool hasFaded;

        void Start()
        {
            if (uiDocument != null) uiDocument.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDestroy()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            overlay = root.Q<VisualElement>("FadeOverlay");
            if (overlay == null) return;

            // Fade in once, when the UI first exists; later rebuilds stay clear
            if (!hasFaded) { hasFaded = true; StartCoroutine(FadeIn()); }
            else { overlay.style.opacity = 0f; overlay.style.display = DisplayStyle.None; }
        }

        IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                overlay.style.opacity = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            overlay.style.opacity = 0f;
            overlay.style.display = DisplayStyle.None;
        }
    }
}
