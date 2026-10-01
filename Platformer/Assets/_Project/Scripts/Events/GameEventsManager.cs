using UnityEngine;

namespace Platformer
{
    public class GameEventsManager : MonoBehaviour
    {
        public static GameEventsManager instance { get; private set; }
        
        // Plain C# event hubs created in Awake — never serialized by Unity
        [System.NonSerialized] public MiscEvents miscEvents;
        [System.NonSerialized] public PlayerEvents playerEvents;
        [System.NonSerialized] public QuestEvents questEvents;
        [System.NonSerialized] public InputEvents inputEvents;
        [System.NonSerialized] public EnemyEvents enemyEvents;
        [System.NonSerialized] public DialogueEvents dialogueEvents;
        [System.NonSerialized] public InventoryEvents inventoryEvents;
        

      
        private void Awake()
        {
            if (instance != null)
            {
                Debug.LogError("Found more than one Game Events Manager in the scene.");
            }

            instance = this;
            
            miscEvents = new MiscEvents();
            playerEvents = new PlayerEvents();
            questEvents = new QuestEvents();
            inputEvents = new InputEvents();
            enemyEvents = new EnemyEvents();
            dialogueEvents = new DialogueEvents();
            inventoryEvents = new InventoryEvents();
        }
    }
}