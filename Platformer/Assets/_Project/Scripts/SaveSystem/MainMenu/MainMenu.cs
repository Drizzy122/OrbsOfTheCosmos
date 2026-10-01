using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Collections;
using KBCore.Refs;

namespace Platformer
{
    [RequireComponent(typeof(PanelRenderer))]
    public class MainMenu : MonoBehaviour
    {
        [field: Header("Components")]
        [field: SerializeField, Self] PanelRenderer mainMenuRenderer;
        [field: SerializeField, Anywhere] SaveSlotsMenu saveSlotsMenu;
        [field: SerializeField, Anywhere] SettingsManager settingsMenu;
        
        [Header("Scene")]
        [Tooltip("Scene Name to load to.")]
        [field: SerializeField] string gameSceneName = "Game";
        
        [field: Header("UI Elements")]
        private VisualElement rootContainer;
        private Button newGameButton;
        private Button continueGameButton;
        private Button loadGameButton;
        private Button settingsButton;
        private Button quitButton;
        private Label continueMeta;
        private Label versionLabel;

        private void Awake()
        {
            mainMenuRenderer = GetComponent<PanelRenderer>();
            mainMenuRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (mainMenuRenderer != null) mainMenuRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            rootContainer = root;

            // Query buttons
            newGameButton = rootContainer.Q<Button>("NewGameButton");
            continueGameButton = rootContainer.Q<Button>("ContinueGameButton");
            loadGameButton = rootContainer.Q<Button>("LoadGameButton");
            settingsButton = rootContainer.Q<Button>("SettingsButton");
            quitButton = rootContainer.Q<Button>("QuitButton");
            continueMeta = rootContainer.Q<Label>("ContinueMeta");
            versionLabel = rootContainer.Q<Label>("VersionLabel");

            // Bind clicks
            newGameButton.clicked += OnNewGameClicked;
            continueGameButton.clicked += OnContinueGameClicked;
            loadGameButton.clicked += OnLoadGameClicked;
            settingsButton.clicked += OnSettingsClicked; // Add this when you make a settings menu!
            if (quitButton != null) quitButton.clicked += OnQuitClicked;

            AudioManager.instance.RegisterButtonAudio(newGameButton);
            AudioManager.instance.RegisterButtonAudio(continueGameButton);
            AudioManager.instance.RegisterButtonAudio(loadGameButton);
            AudioManager.instance.RegisterButtonAudio(settingsButton);
            if (quitButton != null) AudioManager.instance.RegisterButtonAudio(quitButton);

            if (versionLabel != null) versionLabel.text = "v" + Application.version;

            DisableButtonsDependingOnData();
            SetInitialFocus();
        }

        private void Start()
        {
            // UI may not be built yet on the very first frames — OnUIReload
            // repeats this once it is. When it already ran, it also already
            // set focus, so don't start a second competing coroutine.
            if (newGameButton == null) return;
            DisableButtonsDependingOnData();
        }

        private void DisableButtonsDependingOnData()
        {
            bool hasData = DataPersistenceManager.instance.HasGameData();

            // Set both ways — a one-directional disable can never recover if data
            // appears while this menu instance is still alive.
            continueGameButton.SetEnabled(hasData);
            loadGameButton.SetEnabled(hasData);

            RefreshContinueMeta(hasData);
        }

        /// <summary>Shows what Continue would resume into, so the most-pressed
        /// button on the menu isn't a blind jump.</summary>
        private void RefreshContinueMeta(bool hasData)
        {
            if (continueMeta == null) return;

            GameData data = hasData ? DataPersistenceManager.instance.GetSelectedGameData() : null;
            if (data == null)
            {
                continueMeta.text = string.Empty;
                continueMeta.style.display = DisplayStyle.None;
                return;
            }

            string saved = System.DateTime.FromBinary(data.lastUpdated).ToString("dd MMM yyyy");
            continueMeta.text = $"LV {data.playerLevel}  ·  {data.GetPercentageComplete()}% COMPLETE  ·  {saved}";
            continueMeta.style.display = DisplayStyle.Flex;
        }

        public void OnNewGameClicked() 
        {
            saveSlotsMenu.ActivateMenu(false, () => this.ActivateMenu());
            this.DeactivateMenu();
        }

        public void OnLoadGameClicked() 
        {
            saveSlotsMenu.ActivateMenu(true, () => this.ActivateMenu());
            this.DeactivateMenu();
        }

        public void OnSettingsClicked()
        {
            // We must pass the instruction "() => this.ActivateMenu()" into the settings menu!
            settingsMenu.ActivateMenu(() => this.ActivateMenu()); 
            this.DeactivateMenu();
        }

        public void OnContinueGameClicked() 
        {
            DisableMenuButtons();
            DataPersistenceManager.instance.SaveGame();
            SceneLoader.Load(gameSceneName);
        }

        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DisableMenuButtons()
        {
            newGameButton.SetEnabled(false);
            continueGameButton.SetEnabled(false);
        }

        public void ActivateMenu()
        {
            if (rootContainer == null) return;
            rootContainer.style.display = DisplayStyle.Flex;
            DisableButtonsDependingOnData();
            SetInitialFocus();
        }

        public void DeactivateMenu()
        {
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
        }
        
        private void SetInitialFocus()
        {
            StartCoroutine(FocusAfterDelay());
        }
        
        private IEnumerator FocusAfterDelay()
        {
            // Wait exactly one frame so UI Toolkit can finish building the menu
            yield return null;

            // If the Continue button is active, focus it first (matching your comment!)
            if (continueGameButton.enabledSelf) 
            {
                continueGameButton.Focus();
            }
            // Otherwise, focus the New Game button
            else if (newGameButton.enabledSelf) 
            {
                newGameButton.Focus();
            }
        }
    }
}