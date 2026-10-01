using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager instance { get; private set; }

        [field: Header("Notification UI")]
        [SerializeField] PanelRenderer hudDocument;

        private VisualElement NotificationContainer;
        private Label notificationText;
        
        private Queue<NotificationSO> notificationQueue = new Queue<NotificationSO>();
        private bool isDisplayingNotification = false;

        private void Awake()
        {
            // 1. Fixed Singleton Logic
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Debug.LogWarning("There is more than one NotificationManager in the scene");
                Destroy(gameObject);
                return;
            }

            if (hudDocument == null)
                hudDocument = GetComponent<PanelRenderer>();

            hudDocument.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (hudDocument != null) hudDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            NotificationContainer = root.Q<VisualElement>("NotificationContainer");
            notificationText = root.Q<Label>("NotificationText");

            if (NotificationContainer != null && notificationText != null)
            {
                notificationText.style.display = DisplayStyle.None;
                NotificationContainer.style.display = DisplayStyle.None;
                NotificationContainer.style.opacity = 0f;
            }
            else
            {
                // This will warn you in the console if the names don't match exactly!
                Debug.LogError("Could not find UI elements! Check the names in UI Builder.");
            }
        }

        public void ShowNotification(NotificationSO notificationData)
        {
            notificationQueue.Enqueue(notificationData);
            if (!isDisplayingNotification)
            {
                StartCoroutine(DisplayNotification());
            }
        }

        private IEnumerator DisplayNotification()
        {
            isDisplayingNotification = true;
            
            // Turn on the display, but keep it invisible (opacity 0)
            notificationText.style.display = DisplayStyle.Flex;
            NotificationContainer.style.display = DisplayStyle.Flex;

            while (notificationQueue.Count > 0)
            {
                NotificationSO data = notificationQueue.Dequeue();

                // Apply SO data
                notificationText.text = data.Message;
                
                // Fade In
                yield return StartCoroutine(FadeOpacity(0f, 1f, data.FadeDuration));
                
                // Display for the duration
                yield return new WaitForSeconds(data.DisplayDuration);
                
                // Fade Out
                yield return StartCoroutine(FadeOpacity(1f, 0f, data.FadeDuration));
            }

            // 2. Fixed Stuck Queue: Turn off display and reset flag when done
            notificationText.style.display = DisplayStyle.None;
            NotificationContainer.style.display = DisplayStyle.None;
            isDisplayingNotification = false;
        }

        // 3. New Coroutine for fading UI Toolkit elements
        private IEnumerator FadeOpacity(float startOpacity, float endOpacity, float duration)
        {
            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float currentOpacity = Mathf.Lerp(startOpacity, endOpacity, elapsedTime / duration);
                NotificationContainer.style.opacity = currentOpacity;
                yield return null; // Wait for next frame
            }
            // Ensure it hits exactly the target at the end
            NotificationContainer.style.opacity = endOpacity; 
        }
    }
}