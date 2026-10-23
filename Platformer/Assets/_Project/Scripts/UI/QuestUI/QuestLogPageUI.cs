using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// The Quest Log tab inside the menu hub (UI Toolkit). Lists every quest the
    /// QuestManager knows about, coloured by state, and shows the selected quest's
    /// progress, rewards and requirements. Keeps itself current from the quest
    /// events, so the list is right whenever the hub opens.
    /// MenuHubUIController adds this to the hub object automatically.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class QuestLogPageUI : MonoBehaviour
    {
        const string ItemClass = "quest-item";
        const string SelectedClass = "quest-item--selected";
        static readonly string[] StateClasses = { "quest-item--locked", "quest-item--available", "quest-item--active", "quest-item--done" };

        PanelRenderer document;
        ScrollView list;
        Label emptyLabel, nameLabel, stateTag, statusLabel, goldLabel, xpLabel, levelReqLabel, prereqsLabel;

        readonly Dictionary<string, Quest> quests = new();
        readonly Dictionary<string, Button> buttons = new();
        string selectedId;
        bool uiReady;

        void OnEnable()
        {
            document = GetComponent<PanelRenderer>();
            document.RegisterUIReloadCallback(OnUIReload);

            if (GameEventsManager.instance == null) return;
            GameEventsManager.instance.questEvents.onQuestStateChange += OnQuestStateChange;
            GameEventsManager.instance.questEvents.onQuestStepStateChange += OnQuestStepStateChange;
        }

        void OnDisable()
        {
            if (document != null) document.UnregisterUIReloadCallback(OnUIReload);
            if (GameEventsManager.instance == null) return;
            GameEventsManager.instance.questEvents.onQuestStateChange -= OnQuestStateChange;
            GameEventsManager.instance.questEvents.onQuestStepStateChange -= OnQuestStepStateChange;
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            list = root.Q<ScrollView>("QuestList");
            emptyLabel = root.Q<Label>("QuestListEmpty");
            nameLabel = root.Q<Label>("QuestName");
            stateTag = root.Q<Label>("QuestStateTag");
            statusLabel = root.Q<Label>("QuestStatus");
            goldLabel = root.Q<Label>("QuestGold");
            xpLabel = root.Q<Label>("QuestXP");
            levelReqLabel = root.Q<Label>("QuestLevelReq");
            prereqsLabel = root.Q<Label>("QuestPrereqs");

            uiReady = list != null && nameLabel != null;
            if (!uiReady) return;

            if (statusLabel != null) statusLabel.enableRichText = true;   // strikethrough for finished steps
            buttons.Clear();
            list.Clear();

            // The manager may have broadcast before we were listening.
            var manager = FindFirstObjectByType<QuestManager>();
            if (manager != null)
                foreach (var quest in manager.Quests) quests[quest.info.id] = quest;

            RebuildList();

            if (selectedId != null && quests.TryGetValue(selectedId, out var selected)) ShowDetails(selected);
            else if (quests.Count > 0) Select(SortedQuests().First().info.id);
            else ShowPlaceholder();
        }

        // ── Events ───────────────────────────────────────────────────────────

        void OnQuestStateChange(Quest quest)
        {
            quests[quest.info.id] = quest;
            if (!uiReady) return;

            RebuildList();
            if (selectedId == null) Select(quest.info.id);
            else if (selectedId == quest.info.id) ShowDetails(quest);
        }

        void OnQuestStepStateChange(string questId, int stepIndex, QuestStepState state)
        {
            if (uiReady && selectedId == questId && quests.TryGetValue(questId, out var quest)) ShowDetails(quest);
        }

        // ── List ─────────────────────────────────────────────────────────────

        // Active quests first, then ones you can pick up, then locked, then done.
        static int StateOrder(QuestState state) => state switch
        {
            QuestState.IN_PROGRESS => 0,
            QuestState.CAN_FINISH => 0,
            QuestState.CAN_START => 1,
            QuestState.REQUIREMENTS_NOT_MET => 2,
            QuestState.FINISHED => 3,
            _ => 4,
        };

        IEnumerable<Quest> SortedQuests() =>
            quests.Values.OrderBy(q => StateOrder(q.state)).ThenBy(q => q.info.displayName);

        void RebuildList()
        {
            list.Clear();
            foreach (var quest in SortedQuests())
            {
                string id = quest.info.id;
                if (!buttons.TryGetValue(id, out var button))
                {
                    button = new Button { text = quest.info.displayName };
                    button.AddToClassList(ItemClass);
                    button.clicked += () => Select(id);
                    button.RegisterCallback<FocusInEvent>(_ => Select(id));
                    buttons[id] = button;
                }
                button.text = quest.info.displayName;
                ApplyStateClass(button, quest.state);
                button.EnableInClassList(SelectedClass, id == selectedId);
                list.Add(button);
            }

            if (emptyLabel != null) emptyLabel.style.display = quests.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Select(string id)
        {
            if (!quests.TryGetValue(id, out var quest)) return;
            selectedId = id;
            foreach (var kvp in buttons) kvp.Value.EnableInClassList(SelectedClass, kvp.Key == id);
            ShowDetails(quest);
        }

        // ── Details ──────────────────────────────────────────────────────────

        void ShowPlaceholder()
        {
            nameLabel.text = "Select a quest";
            SetText(stateTag, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(goldLabel, string.Empty);
            SetText(xpLabel, string.Empty);
            SetText(levelReqLabel, string.Empty);
            SetText(prereqsLabel, string.Empty);
        }

        void ShowDetails(Quest quest)
        {
            var info = quest.info;
            nameLabel.text = info.displayName;

            if (stateTag != null)
            {
                stateTag.text = StateLabel(quest.state);
                ApplyStateClass(stateTag, quest.state);
            }

            SetText(statusLabel, quest.GetFullStatusText());
            SetText(goldLabel, $"Gold: {info.goldReward}");
            SetText(xpLabel, $"Experience: {info.experienceReward}");
            SetText(levelReqLabel, $"Level {info.levelRequirement}");

            string prereqs = info.questPrerequisites != null && info.questPrerequisites.Length > 0
                ? string.Join("\n", info.questPrerequisites.Where(p => p != null).Select(p => p.displayName))
                : "No other quests required";
            SetText(prereqsLabel, prereqs);
        }

        static string StateLabel(QuestState state) => state switch
        {
            QuestState.REQUIREMENTS_NOT_MET => "LOCKED",
            QuestState.CAN_START => "AVAILABLE",
            QuestState.IN_PROGRESS => "IN PROGRESS",
            QuestState.CAN_FINISH => "READY TO TURN IN",
            QuestState.FINISHED => "COMPLETED",
            _ => string.Empty,
        };

        static void ApplyStateClass(VisualElement element, QuestState state)
        {
            foreach (var cls in StateClasses) element.RemoveFromClassList(cls);
            element.AddToClassList(state switch
            {
                QuestState.REQUIREMENTS_NOT_MET => StateClasses[0],
                QuestState.CAN_START => StateClasses[1],
                QuestState.FINISHED => StateClasses[3],
                _ => StateClasses[2],
            });
        }

        static void SetText(Label label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
