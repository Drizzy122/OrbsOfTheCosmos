using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    public class QuestManager : MonoBehaviour, IDataPersistence
    {
        [Header("Config")]
        [Tooltip("Untick to always start with fresh quests (ignores saved quest data) — handy for testing.")]
        [SerializeField] private bool loadQuestState = true;
        
        // quest start requirements
        private int currentPlayerLevel;
        
        private Dictionary<string, Quest> questMap;

        /// <summary>Every quest the game knows about, for UI that opens after the initial broadcast.</summary>
        public IEnumerable<Quest> Quests => questMap != null ? questMap.Values : System.Array.Empty<Quest>();

        private void Awake()
        {
            questMap = CreateQuestMap();
        }
        private void OnEnable()
        {
            GameEventsManager.instance.questEvents.onStartQuest += StartQuest;
            GameEventsManager.instance.questEvents.onAdvanceQuest += AdvanceQuest;
            GameEventsManager.instance.questEvents.onFinishQuest += FinishQuest;

            GameEventsManager.instance.questEvents.onQuestStepStateChange += QuestStepStateChange;
            GameEventsManager.instance.playerEvents.onPlayerLevelChange += PlayerLevelChange;
            
        }

        private void OnDisable()
        {
            GameEventsManager.instance.questEvents.onStartQuest -= StartQuest;
            GameEventsManager.instance.questEvents.onAdvanceQuest -= AdvanceQuest;
            GameEventsManager.instance.questEvents.onFinishQuest -= FinishQuest;
            
            GameEventsManager.instance.questEvents.onQuestStepStateChange -= QuestStepStateChange;

            GameEventsManager.instance.playerEvents.onPlayerLevelChange -= PlayerLevelChange;
        }

        private void Start()
        {
            foreach (Quest quest in questMap.Values)
            {
                // initialize any loaded quest steps
                if (quest.state == QuestState.IN_PROGRESS)
                {
                    quest.InstantiateCurrentQuestStep(this.transform);
                }
                // broadcast the initial state of all quests on startup
                GameEventsManager.instance.questEvents.QuestStateChange(quest);
            }
        }
        
        private void ChangeQuestState(string id, QuestState state)
        {
            Quest quest = GetQuestById(id);
            quest.state = state;
            GameEventsManager.instance.questEvents.QuestStateChange(quest);
        }
        
        private bool CheckRequirementsMet(Quest quest)
        {
            // start true and prove to be false
            bool meetsRequirements = true;

            // check player level requirements
            if (currentPlayerLevel < quest.info.levelRequirement)
            {
                meetsRequirements = false;
            }

            // check quest prerequisites for completion
            foreach (QuestInfoSO prerequisiteQuestInfo in quest.info.questPrerequisites)
            {
                if (GetQuestById(prerequisiteQuestInfo.id).state != QuestState.FINISHED)
                {
                    meetsRequirements = false;
                    // add this break statement here so that we don't continue on to the next quest, since we've proven meetsRequirements to be false at this point.
                    break;
                }
            }

            return meetsRequirements;
        }
        
        private void Update()
        {
            // loop through ALL quests
            foreach (Quest quest in questMap.Values)
            {
                // if we're now meeting the requirements, switch over to the CAN_START state
                if (quest.state == QuestState.REQUIREMENTS_NOT_MET && CheckRequirementsMet(quest))
                {
                    ChangeQuestState(quest.info.id, QuestState.CAN_START);
                }
            }
        }
        private void PlayerLevelChange(int level)
        {
            currentPlayerLevel = level;
        }

        
        private void StartQuest(string id) 
        {
            Quest quest = GetQuestById(id);
            quest.InstantiateCurrentQuestStep(this.transform);
            ChangeQuestState(quest.info.id, QuestState.IN_PROGRESS);
            Debug.Log("Start Quest: " + id);
        }

        private void AdvanceQuest(string id)
        {
            Quest quest = GetQuestById(id);

            // move on to the next step
            quest.MoveToNextStep();

            // if there are more steps, instantiate the next one
            if (quest.CurrentStepExists())
            {
                quest.InstantiateCurrentQuestStep(this.transform);
            }
            // if there are no more steps, then we've finished all of them for this quest
            else
            {
                ChangeQuestState(quest.info.id, QuestState.CAN_FINISH);
            }
            Debug.Log("Advancing Quest: " + id);
        }

        private void FinishQuest(string id)
        {
            Quest quest = GetQuestById(id);
            ClaimRewards(quest);
            ChangeQuestState(quest.info.id, QuestState.FINISHED);
            Debug.Log("Finishing Quest: " + id);
        }
        
        private void ClaimRewards(Quest quest)
        {
            for (int i = 0; i < quest.info.goldReward; i++)
            {
                GameEventsManager.instance.miscEvents.CoinCollected();
            }

            GameEventsManager.instance.playerEvents.ExperienceGained(quest.info.experienceReward);
        }
        
        private void QuestStepStateChange(string id, int stepIndex, QuestStepState questStepState)
        {
            Quest quest = GetQuestById(id);
            quest.StoreQuestStepState(questStepState, stepIndex);
            ChangeQuestState(id, quest.state);
        }

        private Dictionary<string, Quest> CreateQuestMap()
        {
            // loads all QuestInfoSO Scriptable Objects under the Assets/Resources/Quests folder
            QuestInfoSO[] allQuests = Resources.LoadAll<QuestInfoSO>("Quest");
            // Create the quest map with fresh quests — saved progress is applied
            // afterwards by LoadData (the save system runs before Start).
            Dictionary<string, Quest> idToQuestMap = new Dictionary<string, Quest>();
            foreach (QuestInfoSO questInfo in allQuests)
            {
                if (idToQuestMap.ContainsKey(questInfo.id))
                {
                    Debug.LogWarning("Duplicate ID found when creating quest map: " + questInfo.id);
                }
                idToQuestMap.Add(questInfo.id, new Quest(questInfo));
            }
            return idToQuestMap;
        }
        private Quest GetQuestById(string id)
        {
            Quest quest = questMap[id];
            if (quest == null)
            {
                Debug.LogError("ID not found in the Quest Map: " + id);
            }
            return quest;
        }
        // ── IDataPersistence ──────────────────────────────────────────────────
        // Runs through DataPersistenceManager like everything else (inventory,
        // abilities, level) — one save file for the whole game. LoadData fires
        // after Awake and before Start, so the fresh quest map from Awake gets
        // saved progress applied just in time for Start's broadcasts.

        public void LoadData(GameData data)
        {
            if (!loadQuestState || data.questData == null) return;

            // snapshot the entries so we can replace map values while iterating
            List<Quest> quests = new List<Quest>(questMap.Values);
            foreach (Quest quest in quests)
            {
                if (!data.questData.TryGetValue(quest.info.id, out string serializedData)) continue;
                if (string.IsNullOrEmpty(serializedData)) continue;

                try
                {
                    QuestData questData = JsonUtility.FromJson<QuestData>(serializedData);
                    questMap[quest.info.id] = new Quest(quest.info, questData.state,
                        questData.questStepIndex, questData.questStepStates);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to load quest with id " + quest.info.id + ": " + e);
                }
            }
        }

        public void SaveData(GameData data)
        {
            foreach (Quest quest in questMap.Values)
            {
                try
                {
                    data.questData[quest.info.id] = JsonUtility.ToJson(quest.GetQuestData());
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to save quest with id " + quest.info.id + ": " + e);
                }
            }
        }
    }
}