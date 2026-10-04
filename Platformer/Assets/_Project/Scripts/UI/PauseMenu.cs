using UnityEngine;
using KBCore.Refs;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor; // Changed from UnityEngine.UI

namespace Platformer
{
    public class PauseMenu : ValidatedMonoBehaviour
    {
        [field: Header("Configs")] 
        [field: SerializeField, Anywhere] InputReader input;
        
        // Swapped GameObject for UIDocument
        [field: SerializeField] PanelRenderer pauseDocument;
        [field: SerializeField] private SaveSlotsMenu saveSlotsMenu;
        
        [field: SerializeField, Anywhere] PlayerMovement playerMovement;
        
        [field: SerializeField] bool isPaused = false;

        /// <summary>Lets MenuHubUIController refuse to open on top of the pause screen.</summary>
        public bool IsPaused => isPaused;

        [Tooltip("Found automatically when left empty. Used only to refuse pausing while the hub is open.")]
        [field: SerializeField] MenuHubUIController menuHub;
        [field: Header("Save Thumbnail")]
        [Tooltip("Camera the thumbnail is rendered from. Falls back to Camera.main.")]
        [field: SerializeField] Camera thumbnailCamera;
        [Tooltip("Layers kept out of the shot - HUD, menus, anything screen-space.")]
        [field: SerializeField] LayerMask thumbnailExcludeLayers;

        // Grabbed the instant the player pauses, before any menu is drawn.
        private byte[] pendingThumbnail;

        [field: SerializeField] string musicName;
        [field: SerializeField] float musicValue = 1f; 
        private float pausedValue = 0f;

        // UI Toolkit Elements
        private VisualElement rootContainer;
        private Button continueButton;
        private Button loadButton;
        private Button settingsButton;
        private Button quitButton;
        private Button quickSaveButton;
        private Button loadLastSaveButton;
        private Label quickSaveFeedback;

        private void Awake()
        {
            if (pauseDocument == null) pauseDocument = GetComponent<PanelRenderer>();
            pauseDocument.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (pauseDocument != null) pauseDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            rootContainer = root.Q<VisualElement>("PauseMenuContents");
            if (rootContainer == null) return;

            continueButton = rootContainer.Q<Button>("ContinueGameButton");
            loadButton = rootContainer.Q<Button>("LoadGameButton");
            settingsButton = rootContainer.Q<Button>("SettingsButton");
            quitButton = rootContainer.Q<Button>("QuitButton");
            quickSaveButton = rootContainer.Q<Button>("QuickSaveButton");
            loadLastSaveButton = rootContainer.Q<Button>("LoadLastSaveButton");
            quickSaveFeedback = rootContainer.Q<Label>("QuickSaveFeedback");

            // Bind the buttons to their actions
            continueButton.clicked += DeactivateMenu;
            quitButton.clicked += QuitGame;
            loadButton.clicked += OnLoadClicked;
            settingsButton.clicked += OnSettingsClicked;

            if (quickSaveButton != null)
            {
                quickSaveButton.clicked += OnQuickSaveClicked;
                AudioManager.instance.RegisterButtonAudio(quickSaveButton);
            }
            if (loadLastSaveButton != null)
            {
                loadLastSaveButton.clicked += OnLoadLastSaveClicked;
                AudioManager.instance.RegisterButtonAudio(loadLastSaveButton);
            }

            AudioManager.instance.RegisterButtonAudio(continueButton);
            AudioManager.instance.RegisterButtonAudio(loadButton);
            AudioManager.instance.RegisterButtonAudio(settingsButton);
            AudioManager.instance.RegisterButtonAudio(quitButton, isCloseAction: true);

            // Re-apply state whenever the UI (re)builds
            rootContainer.style.display = isPaused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Start()
        {
            if (!isPaused)
            {
                Time.timeScale = 1;
                // Hide the menu using UI Toolkit display style (may not be built yet —
                // OnUIReload applies the hidden state again once it is)
                if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
                isPaused = false;
                AudioManager.instance.SetMusicParameter(musicName, musicValue);
                AudioManager.instance.SetAmbienceParameter(musicName, musicValue);
            }
        }

        private void OnPause()
        {
            // Refuse to pause over the character hub - both are full-frame overlays, and
            // stacking them leaves two menus visible with both consuming navigation.
            if (!isPaused)
            {
                if (menuHub == null) menuHub = FindFirstObjectByType<MenuHubUIController>(FindObjectsInactive.Include);
                if (menuHub != null && menuHub.IsOpen) return;

                // Grab the shot now, while the world is still the only thing on screen.
                // Capturing at save time instead would photograph the pause menu.
                CaptureThumbnail();
            }

            isPaused = !isPaused;
            if (isPaused)
            {
                ActivateMenu();
            }
            else
            {
                DeactivateMenu();
            }
        }
        
        private void OnSettingsClicked()
        {
            // Hide the pause menu visually
            rootContainer.style.display = DisplayStyle.None; 
    
            // Open the Settings menu, and tell it to show the Pause Menu again when "Back" is clicked
            SettingsManager.instance.ActivateMenu(() => rootContainer.style.display = DisplayStyle.Flex);
        }

        void ActivateMenu()
        {
            Time.timeScale = 0;
            // Show the menu
            rootContainer.style.display = DisplayStyle.Flex; 
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Disable the PlayerController script
            if (playerMovement != null) playerMovement.enabled = false;
            
            AudioManager.instance.SetMusicParameter(musicName, pausedValue);
            AudioManager.instance.SetAmbienceParameter(musicName, pausedValue);
            AudioManager.instance.PlayOneShot(FMODEvents.instance.uiopen, this.transform.position);

            // Replaces the old EventSystem.SetSelectedGameObject
            continueButton.Focus(); 
        }

        public void DeactivateMenu()
        {
            Time.timeScale = 1;
            // Hide the menu
            rootContainer.style.display = DisplayStyle.None; 
            
            // THE FIX: Tell the Save Slots menu to hide itself too!
            if (saveSlotsMenu != null)
            {
                saveSlotsMenu.DeactivateMenu();
            }
            // Ensure state matches if the player clicked the 'Continue' button instead of pressing the pause key
            isPaused = false; 
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // ADD THIS LINE: Instantly shuts down the Settings Menu
            SettingsManager.instance.DeactivateMenu();
            
            // Re-enable the PlayerController script
            if (playerMovement != null) playerMovement.enabled = true;
            
            AudioManager.instance.SetMusicParameter(musicName, musicValue);
            AudioManager.instance.SetAmbienceParameter(musicName, musicValue);
            AudioManager.instance.PlayOneShot(FMODEvents.instance.uiclose, this.transform.position);
        }
        
        /// <summary>Writes the current profile where it stands. No slot picker, no scene
        /// change — the point of a quicksave is that it costs one button press.</summary>
        private void OnQuickSaveClicked()
        {
            if (DataPersistenceManager.instance == null) return;

            DataPersistenceManager.instance.SaveGame();

            // Written after the save, so a thumbnail never exists for a profile with no data.
            string profileId = DataPersistenceManager.instance.GetSelectedProfileId();
            if (pendingThumbnail != null) SaveThumbnail.Write(profileId, pendingThumbnail);

            ShowQuickSaveFeedback();
        }

        private void CaptureThumbnail()
        {
            Camera source = thumbnailCamera != null ? thumbnailCamera : Camera.main;
            if (source == null) return;

            pendingThumbnail = SaveThumbnail.Capture(source, thumbnailExcludeLayers);
        }

        private void ShowQuickSaveFeedback()
        {
            if (quickSaveFeedback == null) return;

            quickSaveFeedback.text = "SAVED  ·  " + System.DateTime.Now.ToString("HH:mm");
            quickSaveFeedback.AddToClassList("pause-feedback--shown");

            // Unscaled: the game is frozen while this menu is up.
            quickSaveFeedback.schedule
                .Execute(() => quickSaveFeedback.RemoveFromClassList("pause-feedback--shown"))
                .StartingIn(2000);
        }

        /// <summary>Reloads the scene rather than just re-reading the file. LoadGame alone
        /// restores the player and inventory but leaves enemies, pickups and world state as
        /// they are — a scene reload resets those, and OnSceneLoaded re-applies the save.</summary>
        private void OnLoadLastSaveClicked()
        {
            if (DataPersistenceManager.instance == null) return;
            if (!DataPersistenceManager.instance.HasGameData()) return;

            DisableMenuButtons();

            // Restore time before leaving, or the next scene starts frozen.
            Time.timeScale = 1f;
            isPaused = false;

            SceneLoader.Load(SceneManager.GetActiveScene().name);
        }

        private void DisableMenuButtons()
        {
            quickSaveButton?.SetEnabled(false);
            loadLastSaveButton?.SetEnabled(false);
            loadButton?.SetEnabled(false);
            continueButton?.SetEnabled(false);
        }

        private void OnLoadClicked()
        {
            // Hide the pause menu visually
            rootContainer.style.display = DisplayStyle.None; 
    
            // Open the Save menu, and tell it to show the Pause Menu again when "Back" is clicked
            saveSlotsMenu.ActivateMenu(true, () => rootContainer.style.display = DisplayStyle.Flex);
        }
        public void QuitGame()
        {
            Debug.Log("Quitting Game");
            #if UNITY_EDITOR
            // Stop Play Mode in the Editor
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            // Close the application in a build
            Application.Quit();
            #endif
        }

        // Temporarily disabled while building the new MenuHub.
        // Once PauseMenu's buttons are folded into the Settings tab, this can be deleted.
        private void OnEnable()  { if (input != null) input.Paused += OnPause; }
        private void OnDisable() { if (input != null) input.Paused -= OnPause; }
    }
}