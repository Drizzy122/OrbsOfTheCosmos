using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    public class DialoguePanelUI : MonoBehaviour
    {
        [Header("UI Toolkit")]
        [SerializeField] private PanelRenderer uiDocument;

        [Header("UXML Element Names")]
        [SerializeField] private string rootElementName = "DialogueContainer";
        [SerializeField] private string dialogueTextName = "DialogueText";
        [SerializeField] private string choicesContainerName = "ChoicesContainer";

        private VisualElement rootContainer;
        private Label dialogueTextLabel;
        private VisualElement choicesContainer;

        private void Awake()
        {
            if (uiDocument != null) uiDocument.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            rootContainer = root.Q<VisualElement>(rootElementName);
            dialogueTextLabel = root.Q<Label>(dialogueTextName);
            choicesContainer = root.Q<VisualElement>(choicesContainerName);

            // Ensure the whole panel is hidden whenever the UI (re)builds
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
        }

        private void OnEnable()
        {
            GameEventsManager.instance.dialogueEvents.onDialogueStarted += DialogueStarted;
            GameEventsManager.instance.dialogueEvents.onDialogueFinished += DialogueFinished;
            GameEventsManager.instance.dialogueEvents.onDisplayDialogue += DisplayDialogue;
        }

        private void OnDisable()
        {
            GameEventsManager.instance.dialogueEvents.onDialogueStarted -= DialogueStarted;
            GameEventsManager.instance.dialogueEvents.onDialogueFinished -= DialogueFinished;
            GameEventsManager.instance.dialogueEvents.onDisplayDialogue -= DisplayDialogue;
        }

        private void DialogueStarted()
        {
            // Show the main panel when dialogue begins
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.Flex;
        }

        private void DialogueFinished()
        {
            // Hide the main panel when dialogue ends
            if (rootContainer == null) return;
            rootContainer.style.display = DisplayStyle.None;
            dialogueTextLabel.text = "";
            choicesContainer.Clear();
        }

        private void DisplayDialogue(string dialogueLine, List<string> dialogueChoices)
        {
            dialogueTextLabel.text = dialogueLine;
            choicesContainer.Clear();

            // 2. Hide the choices container if there are no choices!
            if (dialogueChoices.Count == 0)
            {
                choicesContainer.style.display = DisplayStyle.None;
                return; // Stop here, no buttons to create
            }

            // Otherwise, show the choices container
            choicesContainer.style.display = DisplayStyle.Flex;

            for (int i = 0; i < dialogueChoices.Count; i++)
            {
                int choiceIndex = i;

                Button choiceButton = new Button();
                choiceButton.text = dialogueChoices[i];
                choiceButton.AddToClassList("dialogue-choice-button"); // For your USS styling

                // Keyboard/Gamepad focus
                choiceButton.RegisterCallback<FocusEvent>(ev => 
                {
                    GameEventsManager.instance.dialogueEvents.UpdateChoiceIndex(choiceIndex);
                });

                // Mouse hover
                choiceButton.RegisterCallback<PointerEnterEvent>(ev =>
                {
                    choiceButton.Focus(); 
                });

                choicesContainer.Add(choiceButton);

                if (i == 0)
                {
                    choiceButton.schedule.Execute(() => {
                        choiceButton.Focus();
                        GameEventsManager.instance.dialogueEvents.UpdateChoiceIndex(0);
                    });
                }
            }
        }
    }
}