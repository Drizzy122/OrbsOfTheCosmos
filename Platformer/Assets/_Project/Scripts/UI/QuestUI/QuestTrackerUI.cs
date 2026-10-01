using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Quest popup banner. The MissionBoxContainer pops in whenever quest info
    /// changes (started, step advanced, ready to turn in), stays on screen for
    /// displayDuration seconds, then closes with an exit animation.
    /// The animation itself lives in HUD.uss (.hud-popup classes) via HUDPopup.
    /// </summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        [Header("UI Toolkit")] [SerializeField]
        private PanelRenderer uiDocument;

        [Header("UXML Element Names")] [SerializeField]
        private string rootContainerName = "MissionBoxContainer";

        [SerializeField] private string questTitleName = "QuestTitle";
        [SerializeField] private string questStatusName = "QuestStatus";

        [Header("Popup")]
        [Tooltip("Seconds the box stays fully visible before closing itself.")]
        [SerializeField] private float displayDuration = 5f;

        private Label questTitleLabel;
        private Label questStatusLabel;
        private HUDPopup popup;

        // Keep track of the currently displayed quest
        private Quest trackedQuest;
        private QuestState lastTrackedState;

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
            questTitleLabel = root.Q<Label>(questTitleName);
            questStatusLabel = root.Q<Label>(questStatusName);
            popup = new HUDPopup(root.Q<VisualElement>(rootContainerName), displayDuration);
        }

        private void OnEnable()
        {
            GameEventsManager.instance.questEvents.onQuestStateChange += QuestStateChange;
            GameEventsManager.instance.questEvents.onQuestStepStateChange += QuestStepStateChange;
        }

        private void OnDisable()
        {
            if (GameEventsManager.instance == null) return;
            GameEventsManager.instance.questEvents.onQuestStateChange -= QuestStateChange;
            GameEventsManager.instance.questEvents.onQuestStepStateChange -= QuestStepStateChange;
        }

        private void QuestStateChange(Quest quest)
        {
            if (quest.state == QuestState.IN_PROGRESS || quest.state == QuestState.CAN_FINISH)
            {
                // QuestManager re-broadcasts the current state on every step
                // update (each kill/collect), so only pop on real transitions:
                // quest started, or quest became ready to turn in.
                bool newQuest = trackedQuest == null || !trackedQuest.info.id.Equals(quest.info.id);
                bool stateChanged = newQuest || quest.state != lastTrackedState;

                trackedQuest = quest;
                lastTrackedState = quest.state;

                if (stateChanged) ShowPopup();
                else RefreshTextIfVisible();
            }
            // The tracked quest finishing closes the banner right away
            else if (quest.state == QuestState.FINISHED && trackedQuest != null &&
                     quest.info.id == trackedQuest.info.id)
            {
                trackedQuest = null;
                popup?.Close();
            }
        }

        private void QuestStepStateChange(string questId, int stepIndex, QuestStepState questStepState)
        {
            // Step progress (e.g. another enemy killed) shouldn't re-pop the
            // banner mid-combat — just keep its text fresh if it's on screen.
            if (trackedQuest != null && trackedQuest.info.id == questId)
            {
                RefreshTextIfVisible();
            }
        }

        private void ShowPopup()
        {
            if (popup == null) return;   // UI not built yet — quest events can fire first
            RefreshText();
            popup.Show();
        }

        private void RefreshTextIfVisible()
        {
            if (popup != null && popup.IsVisible) RefreshText();
        }

        private void RefreshText()
        {
            questTitleLabel.text = trackedQuest.info.displayName;
            questStatusLabel.text = trackedQuest.GetFullStatusText();
        }
    }
}
