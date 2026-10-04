using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        
        [Header("Start Gate")]
        [Tooltip("Hold the menu behind a 'press any button' prompt on first show.")]
        [SerializeField] private bool useStartGate = true;
        [Tooltip("Post-processing volume in this scene. Leave empty to skip the vignette.")]
        [SerializeField] private UnityEngine.Rendering.Volume introVolume;
        [Tooltip("Vignette strength while the prompt is up.")]
        [SerializeField, Range(0f, 1f)] private float gateVignette = 0.55f;
        [Tooltip("Vignette strength once the menu is revealed.")]
        [SerializeField, Range(0f, 1f)] private float menuVignette = 0.22f;
        [SerializeField] private float vignetteFadeTime = 1.4f;

        [Header("Support Links")]
        [Tooltip("Leave any of these blank and that link hides itself — useful while the Steam page is still unpublished.")]
        [SerializeField] string discordUrl = "";
        [SerializeField] string youTubeUrl = "";
        [SerializeField] string steamWishlistUrl = "";

        [field: Header("UI Elements")]
        private VisualElement rootContainer;
        private VisualElement supportPanel;
        private Button supportButton;
        private Button discordButton;
        private Button youTubeButton;
        private Button steamButton;
        private Button supportBackButton;
        private Button newGameButton;
        private Button continueGameButton;
        private Button loadGameButton;
        private Button settingsButton;
        private Button quitButton;
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

            // The menu owns its cursor state rather than inheriting whatever gameplay left
            // behind. Nothing else in this scene touches Cursor, so without this a locked
            // cursor carried in from the Game scene stays locked here.
            // Fully qualified: UnityEngine.UIElements also defines a Cursor type, and this
            // file imports that namespace.
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            SetupStartGate();
            SetupSupport();

            DisableButtonsDependingOnData();

            // While the gate is up the items are display:none and cannot take focus.
            // ClearStartGate asks for focus once they are visible.
            if (gateCleared) SetInitialFocus();
        }

        private void Start()
        {
            // The uGUI original called this unconditionally from Start, because serialized
            // Button refs are never null. Here the panel builds asynchronously, so bailing on
            // a null button meant the check could be skipped for good — and when OnUIReload
            // did run it might land before DataPersistenceManager had loaded, leaving Continue
            // and Load disabled with nothing re-evaluating them. Re-check until data exists.
            StartCoroutine(RefreshUntilDataReady());
        }

        /// <summary>Button state depends on two things that arrive in no guaranteed order: the
        /// panel being built, and the save being loaded. Poll briefly for both.</summary>
        private IEnumerator RefreshUntilDataReady()
        {
            const float timeout = 3f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (newGameButton != null && DataPersistenceManager.instance != null)
                {
                    DisableButtonsDependingOnData();
                    if (DataPersistenceManager.instance.HasGameData()) yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void DisableButtonsDependingOnData()
        {
            bool hasData = DataPersistenceManager.instance.HasGameData();

            // Set both ways — a one-directional disable can never recover if data
            // appears while this menu instance is still alive.
            continueGameButton.SetEnabled(hasData);
            loadGameButton.SetEnabled(hasData);

        }

        #region Start gate

        private VisualElement startGate;
        private VisualElement menuItemsRoot;
        private VisualElement menuFooterRoot;
        private Label startPrompt;
        private bool gateCleared;
        private UnityEngine.Rendering.Universal.Vignette vignette;

        private VisualElement screenVeil;

        /// <summary>Opens on black and fades off. The veil starts opaque in USS, so the class
        /// has to be added a frame later — set in the same frame, the transition is skipped.</summary>
        private IEnumerator ClearVeil()
        {
            yield return null;
            screenVeil?.AddToClassList("veil--clear");
        }

        private void SetupStartGate()
        {
            screenVeil = rootContainer.Q<VisualElement>("ScreenVeil");
            StartCoroutine(ClearVeil());

            startGate = rootContainer.Q<VisualElement>("StartGate");
            startPrompt = rootContainer.Q<Label>("StartPrompt");
            menuItemsRoot = rootContainer.Q<VisualElement>("MenuItems");
            menuFooterRoot = rootContainer.Q<VisualElement>("MenuFooter");

            // The scene already has a Post Processing Volume, so fall back to it rather than
            // making this a required inspector slot. A global volume is the one that affects
            // the whole screen; a local one only applies inside its collider.
            if (introVolume == null)
            {
                foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!v.isGlobal) continue;
                    if (introVolume == null || v.priority > introVolume.priority) introVolume = v;
                }
            }

            // Pull the vignette off a runtime copy of the profile. Volume.profile instantiates;
            // sharedProfile would edit the asset on disk and the change would survive play mode.
            if (introVolume != null && introVolume.profile != null)
            {
                if (!introVolume.profile.TryGet(out vignette))
                {
                    vignette = introVolume.profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
                }
                vignette.intensity.overrideState = true;
            }

            if (!useStartGate || startGate == null)
            {
                gateCleared = true;
                startGate?.AddToClassList("gated");
                SetVignette(menuVignette);
                return;
            }

            gateCleared = false;
            menuItemsRoot?.AddToClassList("gated");
            menuFooterRoot?.AddToClassList("gated");
            SetVignette(gateVignette);
        }

        /// <summary>Any key, any pad button, or a click. Sticks are axes, not buttons, so
        /// resting stick drift cannot trip this.</summary>
        private static bool AnyButtonPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;

            if (Gamepad.current != null)
            {
                foreach (InputControl control in Gamepad.current.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame) return true;
                }
            }
            return false;
        }

        private void ClearStartGate()
        {
            gateCleared = true;

            startGate?.AddToClassList("gated");
            menuItemsRoot?.RemoveFromClassList("gated");
            menuFooterRoot?.RemoveFromClassList("gated");

            StartCoroutine(PopItemsIn());

            // Focus is requested only now. SetInitialFocus waits a frame before focusing, so
            // the press that opened the menu cannot also submit the button it lands on.
            SetInitialFocus();
            StartCoroutine(FadeVignette(menuVignette));
        }

        /// <summary>Reveals the items one at a time. The stagger lives here rather than as a
        /// USS transition-delay, because a delay on .button would also delay every hover and
        /// focus transition for the rest of the session.</summary>
        private IEnumerator PopItemsIn()
        {
            const float stagger = 0.06f;

            Button[] order =
            {
                newGameButton, continueGameButton, loadGameButton,
                settingsButton, supportButton, quitButton
            };

            // One frame after the items stop being display:none, so they have a resolved
            // layout to transition from — otherwise the first item snaps in with no animation.
            yield return null;

            foreach (Button item in order)
            {
                item?.RemoveFromClassList("pop");

                float t = 0f;
                while (t < stagger) { t += Time.unscaledDeltaTime; yield return null; }
            }
        }

        private void SetVignette(float intensity)
        {
            if (vignette != null) vignette.intensity.value = intensity;
        }

        private IEnumerator FadeVignette(float target)
        {
            if (vignette == null) yield break;

            float from = vignette.intensity.value;
            float t = 0f;

            while (t < vignetteFadeTime)
            {
                t += Time.unscaledDeltaTime;
                SetVignette(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / vignetteFadeTime)));
                yield return null;
            }
            SetVignette(target);
        }

        private void Update()
        {
            if (gateCleared || startGate == null) return;

            if (AnyButtonPressed())
            {
                ClearStartGate();
                return;
            }

            // Slow breathing pulse so the prompt reads as waiting rather than frozen.
            if (startPrompt != null)
            {
                startPrompt.style.opacity = Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.unscaledTime * 0.8f, 1f));
            }
        }

        #endregion

        #region Support

        private void SetupSupport()
        {
            supportPanel = rootContainer.Q<VisualElement>("SupportPanel");
            supportButton = rootContainer.Q<Button>("SupportButton");
            discordButton = rootContainer.Q<Button>("DiscordButton");
            youTubeButton = rootContainer.Q<Button>("YouTubeButton");
            steamButton = rootContainer.Q<Button>("SteamButton");
            supportBackButton = rootContainer.Q<Button>("SupportBackButton");

            if (supportButton != null)
            {
                supportButton.clicked += OpenSupport;
                AudioManager.instance.RegisterButtonAudio(supportButton);
            }

            if (supportBackButton != null)
            {
                supportBackButton.clicked += CloseSupport;
                AudioManager.instance.RegisterButtonAudio(supportBackButton);
            }

            BindLink(discordButton, discordUrl);
            BindLink(youTubeButton, youTubeUrl);
            BindLink(steamButton, steamWishlistUrl);

            CloseSupport();
        }

        /// <summary>A link with no URL yet is removed from the layout rather than shown dead —
        /// the Steam page won't exist until the store page is published.</summary>
        private void BindLink(Button button, string url)
        {
            if (button == null) return;

            if (string.IsNullOrWhiteSpace(url))
            {
                button.style.display = DisplayStyle.None;
                return;
            }

            button.style.display = DisplayStyle.Flex;
            button.clicked += () => Application.OpenURL(url);
            AudioManager.instance.RegisterButtonAudio(button);
        }

        private void OpenSupport()
        {
            if (supportPanel == null) return;

            supportPanel.style.display = DisplayStyle.Flex;
            // One frame before adding the class, or the transition has nothing to animate
            // from — display and opacity changing together skips the fade.
            supportPanel.schedule.Execute(() => supportPanel.AddToClassList("support--open")).ExecuteLater(0);

            FirstVisibleLink()?.Focus();
        }

        private void CloseSupport()
        {
            if (supportPanel == null) return;

            supportPanel.RemoveFromClassList("support--open");
            supportPanel.style.display = DisplayStyle.None;
            supportButton?.Focus();
        }

        private Button FirstVisibleLink()
        {
            if (discordButton != null && discordButton.style.display != DisplayStyle.None) return discordButton;
            if (youTubeButton != null && youTubeButton.style.display != DisplayStyle.None) return youTubeButton;
            if (steamButton != null && steamButton.style.display != DisplayStyle.None) return steamButton;
            return supportBackButton;
        }

        #endregion

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
        
        private Coroutine focusRoutine;

        private void SetInitialFocus()
        {
            // OnUIReload and ActivateMenu can both ask for focus in the same frame; two
            // retry loops racing would fight over the focused element.
            if (focusRoutine != null) StopCoroutine(focusRoutine);
            focusRoutine = StartCoroutine(FocusAfterDelay());
        }
        
        private IEnumerator FocusAfterDelay()
        {
            // The uGUI original re-selected its first button from Menu.OnEnable, which fired
            // every time the menu was shown via SetActive. Showing a panel with style.display
            // fires nothing, and Focus() silently no-ops while the element is not yet attached
            // to a panel — so one frame was a guess. Keep trying until the focus actually takes.
            const int maxFrames = 30;

            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;

                Button target = PreferredFocusTarget();
                if (target == null) continue;

                // Not attached yet: Focus() would do nothing and we would never retry.
                if (target.panel == null) continue;

                target.Focus();

                if (target.panel.focusController?.focusedElement == target)
                {
                    Debug.Log($"[MainMenuFocus] focused '{target.name}' on frame {frame}.");
                    yield break;
                }
            }

            // TEMPORARY DIAGNOSTIC — remove once this bug is closed.
            Button t = PreferredFocusTarget();
            Debug.LogWarning(
                $"[MainMenuFocus] gave up after {maxFrames} frames. " +
                $"target={(t == null ? "NULL" : t.name)} " +
                $"attached={(t?.panel != null)} " +
                $"newGameEnabled={newGameButton?.enabledSelf} " +
                $"continueEnabled={continueGameButton?.enabledSelf} " +
                $"focusedNow={(t?.panel?.focusController?.focusedElement as VisualElement)?.name ?? "none"} " +
                $"rootDisplay={rootContainer?.resolvedStyle.display}");
        }

        private Button PreferredFocusTarget()
        {
            if (continueGameButton != null && continueGameButton.enabledSelf) return continueGameButton;
            if (newGameButton != null && newGameButton.enabledSelf) return newGameButton;
            return null;
        }
    }
}