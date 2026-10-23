using UnityEngine;
using UnityEngine.UIElements;
using System;
using KBCore.Refs;

namespace Platformer
{
    [RequireComponent(typeof(PanelRenderer))]
    public class ConfirmationPopUpMenu : MonoBehaviour
    {
        [field: Header("Components")]
        [field: SerializeField] PanelRenderer confirmationPopUpRenderer;
       
        [field: Header("UI Elements")]
        private VisualElement rootContainer;
        private Label displayText;
        private Button confirmButton;
        private Button cancelButton;

        // We need to store these so we can unsubscribe from them later to prevent memory leaks/double-clicks
        private Action currentConfirmAction;
        private Action currentCancelAction;

        private void Awake()
        {
            confirmationPopUpRenderer = GetComponent<PanelRenderer>();
            confirmationPopUpRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (confirmationPopUpRenderer != null) confirmationPopUpRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            rootContainer = root.Q<VisualElement>("ConfirmationPopupMenuContainer");
            if (rootContainer == null) return;

            displayText = rootContainer.Q<Label>("DisplayText");
            confirmButton = rootContainer.Q<Button>("ConfirmButton");
            cancelButton = rootContainer.Q<Button>("CancelButton");

            // Hide FIRST. The UXML shows the dialog by default, and this callback can run
            // synchronously inside Awake (panel already built) before AudioManager.Awake has
            // run - anything that can throw must come after the hide or the dialog is left
            // on screen with its placeholder text.
            DeactivateMenu();

            if (AudioManager.instance != null) RegisterButtonAudio();
            else if (isActiveAndEnabled) StartCoroutine(RegisterButtonAudioWhenReady());
        }

        private void RegisterButtonAudio()
        {
            AudioManager.instance.RegisterButtonAudio(confirmButton);
            AudioManager.instance.RegisterButtonAudio(cancelButton, isCloseAction: true);
        }

        /// <summary>AudioManager may initialise after this panel; wait for it rather than throw.</summary>
        private System.Collections.IEnumerator RegisterButtonAudioWhenReady()
        {
            while (AudioManager.instance == null) yield return null;
            if (confirmButton != null && cancelButton != null) RegisterButtonAudio();
        }

        public void ActivateMenu(string text, Action confirmAction, Action cancelAction)
        {
            if (rootContainer == null) return;   // UI not built yet
            rootContainer.style.display = DisplayStyle.Flex;
            this.displayText.text = text;

            // Unsubscribe from old events if they exist
            if (currentConfirmAction != null) confirmButton.clicked -= currentConfirmAction;
            if (currentCancelAction != null) cancelButton.clicked -= currentCancelAction;

            // Assign new actions that include closing the menu
            currentConfirmAction = () => {
                DeactivateMenu();
                confirmAction();
            };
            
            currentCancelAction = () => {
                DeactivateMenu();
                cancelAction();
            };

            // Subscribe to the buttons
            confirmButton.clicked += currentConfirmAction;
            cancelButton.clicked += currentCancelAction;
            
            // Focus the cancel button by default for safety
            cancelButton.Focus();
        }

        // Change this from private to public!
        public void DeactivateMenu()
        {
            // Guarded: SaveSlotsMenu can call this before our UI has built
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
        }
    }
}