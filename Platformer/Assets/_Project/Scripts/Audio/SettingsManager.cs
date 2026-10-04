using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using KBCore.Refs; // Assuming this is a custom attribute library you use

namespace Platformer
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager instance { get; private set; }

        [field: Header("UI Document")]
        [field: SerializeField] private PanelRenderer document;
        private VisualElement rootContainer;
        // Every panel we are listening to, so OnDestroy can let go of all of them.
        private readonly List<PanelRenderer> listening = new List<PanelRenderer>();

        [field: Header("Window Mode")]
        [field: SerializeField] private string[] windowModes = { "Fullscreen", "Borderless", "Maximized", "Windowed" };
        private int currentWindowMode = 0;

        [field: Header("Resolution")]
        private Resolution[] filteredResolutions;
        private int currentResolutionIndex = 0;

        [Header("Graphics Quality")]
        private string[] qualityLevels;
        private int currentQualityIndex = 0;

        [Header("Graphics: Post Processing")]
        [field: SerializeField, Anywhere] private Volume globalVolume;
        private ColorAdjustments colorAdjustments;
        private MotionBlur motionBlur;

        // UI References
        private Label windowModeText;
        private Label resolutionText;
        private Label qualityText;
        private Slider gammaSlider;
        private Toggle motionBlurToggle;
        // Input UI References
        private Slider controllerSensitivitySlider;
        private Slider mouseSensitivitySlider;
        private Toggle invertControllerToggle;
        private Toggle invertMouseToggle;

        private Action backButtonCallback;

        // ── Controller / tab navigation ──────────────────────────────────────
        private TabView settingsTabView;
        private readonly List<VisualElement> tabHeaders = new List<VisualElement>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Found more than one SettingsManager in the scene. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            // Settings data doesn't need the UI; the UI wiring runs from the
            // PanelRenderer reload callback (fires on first build and immediately
            // if the panel already exists).
            InitializeSettingsData();

            if (document == null) document = GetComponent<PanelRenderer>();
            Listen(document);

            // The settings tree is not always carried by the panel this component sits on -
            // in the Game scene it rides along inside the pause panel - and a PanelRenderer
            // exposes no root outside its reload callback. So listen to every panel in the
            // scene and keep whichever one answers to SettingsContainer.
            foreach (var panel in FindObjectsByType<PanelRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Listen(panel);
            }

            StartCoroutine(WarnIfUnbound());
        }

        private void Listen(PanelRenderer panel)
        {
            if (panel == null || listening.Contains(panel)) return;
            listening.Add(panel);
            panel.RegisterUIReloadCallback(OnUIReload);
        }

        /// <summary>Panels can build a frame late, so the complaint waits a frame before
        /// deciding that nothing in the scene hosts the settings tree at all.</summary>
        private System.Collections.IEnumerator WarnIfUnbound()
        {
            yield return null;
            if (rootContainer == null)
            {
                Debug.LogWarning("SettingsManager: no panel in this scene contains 'SettingsContainer'. " +
                                 "Instance SettingsVisualTree into one of the scene's UI panels.");
            }
        }

        private void OnDestroy()
        {
            foreach (var panel in listening)
            {
                if (panel != null) panel.UnregisterUIReloadCallback(OnUIReload);
            }
            listening.Clear();
        }

        private void OnUIReload(PanelRenderer panel, VisualElement root)
        {
            var container = root.Q<VisualElement>("SettingsContainer");

            // Not this panel's tree. Stay registered: the hosting panel may not have built yet.
            if (container == null) return;

            // Already bound to a different panel - first one to answer wins.
            if (rootContainer != null && document != panel) return;

            document = panel;
            rootContainer = container;

            InitializeUIElements();
            BindUIEvents();
            RefreshAllUI();

            // Hidden until something asks for it. Every host opens Settings through
            // ActivateMenu(), so it never wants to be visible straight off a reload.
            DeactivateMenu();
        }

        #region Initialization Methods
        private void InitializeUIElements()
        {
            windowModeText = rootContainer.Q<Label>("WindowValueText");
            resolutionText = rootContainer.Q<Label>("ResValueText");
            qualityText = rootContainer.Q<Label>("QualityValueText");
            gammaSlider = rootContainer.Q<Slider>("GammaSlider");
            motionBlurToggle = rootContainer.Q<Toggle>("MotionBlurToggle");


            controllerSensitivitySlider = rootContainer.Q<Slider>("ControllerSensitivity");
            mouseSensitivitySlider = rootContainer.Q<Slider>("MouseSensitivity");

            invertControllerToggle = rootContainer.Q<Toggle>("InvertControllerY");
            invertMouseToggle = rootContainer.Q<Toggle>("InvertMouseY");

            settingsTabView = rootContainer.Q<TabView>();

            // Give the TabView one frame to build its internal header buttons before we wire them up
            rootContainer.schedule.Execute(SetupTabNavigation);
        }

        /// <summary>
        /// Makes TabView tabs navigable with a gamepad.
        /// Unity's TabView headers are not focusable by default and don't forward
        /// NavigationMoveEvent/NavigationSubmitEvent, so we do it manually.
        /// </summary>
        private void SetupTabNavigation()
        {
            tabHeaders.Clear();

            var headerContainer = rootContainer.Q(className: "unity-tab-view__header-container");
            if (headerContainer == null) return;

            foreach (VisualElement child in headerContainer.Children())
            {
                child.focusable = true;   // allow D-pad focus
                tabHeaders.Add(child);
            }

            for (int i = 0; i < tabHeaders.Count; i++)
            {
                int idx = i;

                // Up / Down cycles through the tab list and switches the active tab
                tabHeaders[i].RegisterCallback<NavigationMoveEvent>(evt =>
                {
                    if (evt.direction == NavigationMoveEvent.Direction.Down)
                    {
                        FocusAndActivateTab((idx + 1) % tabHeaders.Count);
                        evt.StopPropagation();
                    }
                    else if (evt.direction == NavigationMoveEvent.Direction.Up)
                    {
                        FocusAndActivateTab((idx - 1 + tabHeaders.Count) % tabHeaders.Count);
                        evt.StopPropagation();
                    }
                    // Left / Right: let Unity's spatial navigation find the content area
                });

                // Pressing × / A while a header is focused also activates that tab
                tabHeaders[i].RegisterCallback<NavigationSubmitEvent>(evt =>
                {
                    FocusAndActivateTab(idx);
                });
            }
        }

        private void FocusAndActivateTab(int index)
        {
            if (settingsTabView == null || index < 0 || index >= tabHeaders.Count) return;

            settingsTabView.selectedTabIndex = index;
            tabHeaders[index].Focus();
        }

        private void InitializeSettingsData()
        {
            // Resolutions Setup
            SetupResolutions();

            // Quality Setup
            qualityLevels = QualitySettings.names;
            currentQualityIndex = QualitySettings.GetQualityLevel();

            // Post Processing Setup
            if (globalVolume != null)
            {
                globalVolume.profile.TryGet(out colorAdjustments);
                globalVolume.profile.TryGet(out motionBlur);
            }
            else
            {
                Debug.LogWarning("Global Volume is missing! Post-processing settings will be disabled.");
            }
        }

        private void SetupResolutions()
        {
            Resolution[] allResolutions = Screen.resolutions;
            List<Resolution> uniqueResolutions = new List<Resolution>();
            
            foreach (Resolution res in allResolutions)
            {
                if (!uniqueResolutions.Exists(x => x.width == res.width && x.height == res.height))
                {
                    uniqueResolutions.Add(res);
                }
            }
            filteredResolutions = uniqueResolutions.ToArray();
            
            // Find current resolution
            for (int i = 0; i < filteredResolutions.Length; i++)
            {
                if (filteredResolutions[i].width == Screen.currentResolution.width &&
                    filteredResolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// Wires a cycle button (◄ ►) so it responds to both mouse clicks and
        /// controller NavigationSubmitEvent. Using Button.clicked covers both;
        /// RegisterCallback&lt;ClickEvent&gt; misses controller submit when the button
        /// is nested inside a Label row element.
        /// </summary>
        private void BindCycleButton(string buttonName, System.Action action)
        {
            Button btn = rootContainer.Q<Button>(buttonName);
            if (btn == null) return;
            btn.clicked += action;
            AudioManager.instance.RegisterButtonAudio(btn);
        }

        private void BindUIEvents()
        {
            Button backButton = rootContainer.Q<Button>("BackButton");
            if (backButton != null)
            {
                AudioManager.instance.RegisterButtonAudio(backButton, isCloseAction: true);
                backButton.clicked += () =>
                {
                    DeactivateMenu();
                    backButtonCallback?.Invoke();
                };
            }

            // Resolution Buttons
            // Use .clicked instead of ClickEvent so controller NavigationSubmitEvent also triggers these
            // (ClickEvent alone doesn't fire reliably when the button is a child of a Label row)
            BindCycleButton("ResPrevButton",    () => CycleResolution(-1));
            BindCycleButton("ResNextButton",    () => CycleResolution(1));

            // Window Mode Buttons
            BindCycleButton("WindowPrevButton", () => CycleWindowMode(-1));
            BindCycleButton("WindowNextButton", () => CycleWindowMode(1));

            // Quality Buttons
            BindCycleButton("QualityPrevButton", () => CycleQuality(-1));
            BindCycleButton("QualityNextButton", () => CycleQuality(1));

            // Post Processing
            if (colorAdjustments != null && gammaSlider != null)
                gammaSlider.RegisterValueChangedCallback(evt => ApplyGamma(evt.newValue));

            if (motionBlur != null && motionBlurToggle != null)
            {
                motionBlurToggle.value = motionBlur.active;
                // CHANGED: .label is now .text
                motionBlurToggle.text = motionBlur.active ? "Enabled" : "Disabled"; 
                motionBlurToggle.RegisterValueChangedCallback(evt => ApplyMotionBlur(evt.newValue));
            }
            
           
            // Controller Sensitivity
            if (controllerSensitivitySlider != null)
                controllerSensitivitySlider.RegisterValueChangedCallback(evt => {
                    if (CameraManager.instance != null) CameraManager.instance.SetControllerSensitivity(evt.newValue);
                });

            // Mouse Sensitivity
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.RegisterValueChangedCallback(evt => {
                    if (CameraManager.instance != null) CameraManager.instance.SetMouseSensitivity(evt.newValue);
                });
            
            // Controller Invert Toggle
            if (invertControllerToggle != null)
                invertControllerToggle.RegisterValueChangedCallback(evt => {
                    if (CameraManager.instance != null) CameraManager.instance.SetControllerInvertY(evt.newValue);
                    invertControllerToggle.text = evt.newValue ? "Enabled" : "Disabled";
                });

            // Mouse Invert Toggle
            if (invertMouseToggle != null)
                invertMouseToggle.RegisterValueChangedCallback(evt => {
                    if (CameraManager.instance != null) CameraManager.instance.SetMouseInvertY(evt.newValue);
                    invertMouseToggle.text = evt.newValue ? "Enabled" : "Disabled";
                });
        }
        #endregion

        #region Setting Logic & Application
        private void CycleResolution(int direction)
        {
            currentResolutionIndex = (currentResolutionIndex + direction + filteredResolutions.Length) % filteredResolutions.Length;
            ApplyResolution();
        }

        private void CycleWindowMode(int direction)
        {
            currentWindowMode = (currentWindowMode + direction + windowModes.Length) % windowModes.Length;
            ApplyWindowMode();
        }

        private void CycleQuality(int direction)
        {
            currentQualityIndex = (currentQualityIndex + direction + qualityLevels.Length) % qualityLevels.Length;
            ApplyQuality();
        }

        private void ApplyWindowMode()
        {
            string selectedMode = windowModes[currentWindowMode];
            switch (selectedMode)
            {
                case "Fullscreen": Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
                case "Borderless": Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
                case "Maximized": Screen.fullScreenMode = FullScreenMode.MaximizedWindow; break;
                case "Windowed": Screen.SetResolution(1280, 720, FullScreenMode.Windowed); break;
            }
            UpdateWindowModeUI();
        }

        private void ApplyResolution()
        {
            Resolution res = filteredResolutions[currentResolutionIndex];
            Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
            UpdateResolutionUI();
        }

        private void ApplyQuality()
        {
            QualitySettings.SetQualityLevel(currentQualityIndex, true);
            UpdateQualityUI();
        }

        private void ApplyGamma(float sliderValue)
        {
            if (colorAdjustments == null) return;
            colorAdjustments.postExposure.overrideState = true; 
            colorAdjustments.postExposure.value = Mathf.Lerp(-2f, 2f, sliderValue);
        }

        private void ApplyMotionBlur(bool isEnabled)
        {
            if (motionBlur == null) return;
            motionBlur.active = isEnabled;
            
            if (motionBlurToggle != null) 
                motionBlurToggle.text = isEnabled ? "Enabled" : "Disabled";
        }
        #endregion

        #region UI Updates & Menu Control
        private void RefreshAllUI()
        {
            UpdateWindowModeUI();
            UpdateResolutionUI();
            UpdateQualityUI();
        }

        private void UpdateWindowModeUI() { if (windowModeText != null) windowModeText.text = windowModes[currentWindowMode]; }
        private void UpdateResolutionUI() { if (resolutionText != null) resolutionText.text = $"{filteredResolutions[currentResolutionIndex].width}x{filteredResolutions[currentResolutionIndex].height}"; }
        private void UpdateQualityUI() { if (qualityText != null && qualityLevels.Length > 0) qualityText.text = qualityLevels[currentQualityIndex].ToUpper(); }

        public void ActivateMenu(Action backCallback = null)
        {
            backButtonCallback = backCallback;
            if (rootContainer == null) return;

            rootContainer.style.display = DisplayStyle.Flex;

            // Focus the first tab header so the controller has an immediate entry point
            rootContainer.schedule.Execute(() =>
            {
                if (tabHeaders.Count > 0)
                    tabHeaders[0].Focus();
            });
        }

        public void DeactivateMenu()
        {
            if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
        }
        #endregion
    }
}
