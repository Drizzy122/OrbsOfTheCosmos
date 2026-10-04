using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using KBCore.Refs;
using Cursor = UnityEngine.Cursor;

namespace Platformer
{
    public enum MenuTab { Map, QuestLog, Character, Inventory, Abilities }

    /// <summary>
    /// Owns the menu hub shell: toggle via pause button, cycle tabs with LB/RB,
    /// pause behavior (timescale, cursor, disable player input + movement).
    /// TabView handles the visual tab switching itself.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class MenuHubUIController : ValidatedMonoBehaviour
    {
        [Header("References")]
        [SerializeField] PanelRenderer document;
        [SerializeField, Anywhere] InputReader input;
        [SerializeField, Anywhere] PlayerMovement playerMovement;

        [Header("Default")]
        [SerializeField] MenuTab defaultTab = MenuTab.Character;

        [Header("Return To Menu")]
        [SerializeField] string mainMenuSceneName = "MainMenu";

        // LB/RB cycle through this order. Must match the Tab order in the UXML.
        static readonly MenuTab[] TabOrder =
        {
            MenuTab.Map, MenuTab.QuestLog, MenuTab.Character, MenuTab.Inventory, MenuTab.Abilities
        };

        VisualElement panel;
        TabView tabView;

        MenuTab activeTab;
        bool isOpen;

        /// <summary>Lets PauseMenu refuse to open on top of this screen.</summary>
        public bool IsOpen => isOpen;

        [Tooltip("Found automatically when left empty. Used only to refuse opening while paused.")]
        [SerializeField] PauseMenu pauseMenu;

        // Context to restore on close — pausing mid-dialogue must return to
        // DIALOGUE, not DEFAULT, so NPCs don't react to menu button presses.
        InputEventContext contextBeforeOpen = InputEventContext.DEFAULT;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable()
        {
            // PanelRenderer hands us the UI via the reload callback: it fires when
            // the panel is first built (or immediately if it already exists), and
            // again whenever the UI rebuilds — so wiring lives in OnUIReload.
            document.RegisterUIReloadCallback(OnUIReload);
            SubscribeInput();
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            panel = root.Q<VisualElement>("menu-hub");
            tabView = root.Q<TabView>("main-tabs");

            // The settings tree hides this button by default (it's shared with the
            // main menu's settings, where it makes no sense) — the in-game hub
            // reveals and wires it.
            var returnButton = root.Q<Button>("ReturnToMenuButton");
            if (returnButton != null)
            {
                returnButton.style.display = DisplayStyle.Flex;
                returnButton.clicked += ReturnToMainMenu;
            }

            SetVisible(isOpen);
            SetActiveTab(isOpen ? activeTab : defaultTab);
        }

        void ReturnToMainMenu()
        {
            // Persist progress before leaving gameplay
            if (DataPersistenceManager.instance != null) DataPersistenceManager.instance.SaveGame();

            // Undo the pause state by hand — Close() locks the cursor for gameplay,
            // but the main menu wants it free
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (input != null) input.EnablePlayerMap();

            // Clear this before leaving. The scene stays alive behind the loading screen and
            // is torn down at activation, where OnDisable calls Close() if we are still open —
            // and Close() re-locks the cursor for gameplay we are in the middle of leaving.
            isOpen = false;

            SceneLoader.Load(mainMenuSceneName);
        }

        void OnDisable()
        {
            document.UnregisterUIReloadCallback(OnUIReload);
            UnsubscribeInput();
            if (isOpen) Close();
        }

        // ---- input ----

        void SubscribeInput()
        {
            if (input == null) return;
            // The tabbed hub is a gameplay screen, not the system menu — it opens on its own
            // button (I / touchpad) so PauseMenu can own Escape and Start.
            input.CharacterMenu += Toggle;
            input.PreviousTab += OnPreviousTab;
            input.NextTab     += OnNextTab;
        }

        void UnsubscribeInput()
        {
            if (input == null) return;
            input.CharacterMenu -= Toggle;
            input.PreviousTab -= OnPreviousTab;
            input.NextTab     -= OnNextTab;
        }

        void OnPreviousTab()
        {
            if (!isOpen) return;
            SetActiveTab(CycleTab(-1));
        }

        void OnNextTab()
        {
            if (!isOpen) return;
            SetActiveTab(CycleTab(+1));
        }

        MenuTab CycleTab(int delta)
        {
            int idx = System.Array.IndexOf(TabOrder, activeTab);
            int next = (idx + delta + TabOrder.Length) % TabOrder.Length;
            return TabOrder[next];
        }

        // ---- open / close ----

        void Toggle()
        {
            if (isOpen) { Close(); return; }

            // The two screens are both full-frame overlays; opening one over the other
            // leaves both visible and both taking input.
            if (pauseMenu == null) pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pauseMenu != null && pauseMenu.IsPaused) return;

            Open();
        }

        void Open()
        {
            if (isOpen) return;
            isOpen = true;
            SetVisible(true);

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var inputEvents = GameEventsManager.instance != null ? GameEventsManager.instance.inputEvents : null;
            if (inputEvents != null)
            {
                contextBeforeOpen = inputEvents.inputEventContext;
                inputEvents.ChangeInputEventContext(InputEventContext.MENU);
            }

            if (input != null) input.DisablePlayerMap();
            if (playerMovement != null) playerMovement.enabled = false;
        }

        void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            SetVisible(false);

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (GameEventsManager.instance != null)
                GameEventsManager.instance.inputEvents.ChangeInputEventContext(contextBeforeOpen);

            // Paused mid-dialogue: leave gameplay input/movement to DialogueManager —
            // it re-enables both when the conversation actually ends.
            bool inDialogue = contextBeforeOpen == InputEventContext.DIALOGUE;
            if (input != null && !inDialogue) input.EnablePlayerMap();
            if (playerMovement != null && !inDialogue) playerMovement.enabled = true;
        }

        void SetVisible(bool show) => panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        // ---- tabs ----

        void SetActiveTab(MenuTab tab)
        {
            activeTab = tab;
            if (tabView == null) return;
            int idx = System.Array.IndexOf(TabOrder, tab);
            if (idx >= 0) tabView.selectedTabIndex = idx;
        }
    }
}
