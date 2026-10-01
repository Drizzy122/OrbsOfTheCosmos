using UnityEngine;
using KBCore.Refs;
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
        [field: SerializeField] string musicName;
        [field: SerializeField] float musicValue = 1f; 
        private float pausedValue = 0f;

        // UI Toolkit Elements
        private VisualElement rootContainer;
        private Button continueButton;
        private Button loadButton;
        private Button settingsButton;
        private Button quitButton;

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

            // Bind the buttons to their actions
            continueButton.clicked += DeactivateMenu;
            quitButton.clicked += QuitGame;
            loadButton.clicked += OnLoadClicked;
            settingsButton.clicked += OnSettingsClicked;

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
        private void OnEnable() { /* input.Paused += OnPause; */ }
        private void OnDisable() { /* input.Paused -= OnPause; */ }
    }
}