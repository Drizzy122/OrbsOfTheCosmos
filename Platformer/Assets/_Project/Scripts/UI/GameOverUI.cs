using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] PanelRenderer uiDocument;
        [SerializeField] Health playerHealth;
        [SerializeField] float fadeInDuration = 1.5f;

        VisualElement root;

        void Awake()
        {
            if (uiDocument != null) uiDocument.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDestroy()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        void OnUIReload(PanelRenderer _, VisualElement newRoot)
        {
            root = newRoot.Q<VisualElement>("GameOverRoot");
            if (root == null) return;
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.None;
        }

        void OnEnable() => playerHealth.OnDeath += ShowGameOver;
        void OnDisable() => playerHealth.OnDeath -= ShowGameOver;

        void ShowGameOver()
        {
            if (root == null) return;
            root.style.display = DisplayStyle.Flex;
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                root.style.opacity = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            root.style.opacity = 1f;
        }
    }
}
