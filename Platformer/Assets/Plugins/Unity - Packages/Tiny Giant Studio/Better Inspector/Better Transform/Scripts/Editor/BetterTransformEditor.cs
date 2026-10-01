using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TinyGiantStudio.BetterEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using static TinyGiantStudio.BetterInspector.BetterMath;
using static UnityEngine.Mathf;
using Debug = UnityEngine.Debug;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable ForCanBeConvertedToForeach

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    ///     Methods containing the word Update can be called multiple times to update to reflect changes.
    ///     Methods containing the word Setup are called once when the inspector is created.
    ///     Note to self:
    ///     KEEP ALL FOLDOUTS HIDDEN BY DEFAULT IN THE UXML FILE
    ///     MAYBE:
    ///     1. Randomize Rotation, Position, and Scale
    ///     TODO
    ///     1. Move size paste,reset to QuickActions.cs
    ///     2. Default inspector for multi target
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Transform))]
    public class BetterTransformEditor : Editor
    {
        #region Variable Declaration

        #region Referenced in the Inspector

        [SerializeField] VisualTreeAsset visualTreeAsset; // If reference is lost, retrieved from file location

        const string VisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Transform/Scripts/Editor/BetterTransform.uxml";

        const string VisualTreeAssetGuid = "e8eee4a8330502c40b42313f6a99d0b6";

        [SerializeField] VisualTreeAsset folderTemplate; // If reference is lost, retrieved from file location

        const string FolderTemplateFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Transform/Scripts/Editor/Templates/CustomFoldoutTemplate.uxml";

        const string FolderTemplateGuid = "ce465cbac9f131241acfd8b7127846a4";

        [SerializeField] VisualTreeAsset settingsTemplate;

        const string SettingsTemplateFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Transform/Scripts/Editor/Templates/Settings.uxml";

        const string SettingsTemplateGuid = "892731edae4e3934aaddd13b03e5dd15";

        [SerializeField] StyleSheet betterTransformStyleSheet1;
        [SerializeField] StyleSheet betterTransformStyleSheet2;
        [SerializeField] StyleSheet betterTransformStyleSheet3;

        #endregion Referenced in the Inspector

        const string AssetLink =
            "https://assetstore.unity.com/packages/tools/utilities/better-transform-size-notes-global-local-workspace-parent-child--321300?aid=1011ljxWe";

        const string PublisherLink = "https://assetstore.unity.com/publishers/45848?aid=1011ljxWe";
        const string DocumentationLink = "https://ferdowsur.gitbook.io/better-transform/";

        VisualElement _root;

        public Transform transform;
        SerializedObject _soTarget;

        BetterInspectorEditorSettings _inspectorEditorSettings;
        BetterTransformSettings _betterTransformSettings;


        Editor _originalEditor;
        readonly List<Editor> _otherBetterTransformEditors = new List<Editor>();

        SizeCalculation _sizeCalculator;
        QuickActions _quickActions;

        GroupBox _topGroupBox;

        Button _pingSelfButton;
        Button _sizeOutlineGizmoOnButton;
        Button _sizeOutlineGizmoOffButton;
        Button _sizeLabelsGizmoOn;
        Button _sizeLabelsGizmoOff;

        Label _sizeFoldoutWarning;

        /// <summary>
        /// It's a small label with the text - "Showing World Space in Play Mode on fast-changing objects may reduce performance."
        /// </summary>
        Label _worldSpaceWarning;

        GroupBox _performanceLoggingGroupBox;
        Stopwatch _stopwatch;

        const string PrefabOverrideLabelUSSClass = "prefab_override_label";

        #region Performance Logging

        float _time;
        float _totalMS;
        bool _logPerformance;
        bool _logDetailedPerformance;

        #endregion

        #region WarningLabels

        const string WarningStringShowingCustomFieldsInPlaymode =
            "Use the Default Inspector for fast-updating objects in Play Mode to reduce performance impact. This option is available in the Main Settings foldout.";

        const string WarningStringShowingWorldSpaceInPlaymode =
            "Showing World Space in Play Mode on fast-changing objects may reduce performance.";

        #endregion

        #endregion Variable Declaration


        #region Unity Stuff

        static bool _domainReloaded;

        [InitializeOnLoadMethod]
        static void MyInitializationMethod()
        {
            _domainReloaded = true;
        }

        void Awake()
        {
            //On domain reload
            if (!_domainReloaded) return;

            _domainReloaded = false;

            _sizeSetupDone = false;
            _settingsFieldSetupDone = false;
            originalTransform = null;
        }

        void OnEnable()
        {
            //On domain reload
            if (!_domainReloaded) return;

            _domainReloaded = false;

            _sizeSetupDone = false;
            _settingsFieldSetupDone = false;
            originalTransform = null;
        }

        void OnDisable()
        {
            for (int i = 0; i < _otherBetterTransformEditors.Count; i++)
                DestroyImmediate(_otherBetterTransformEditors[i]);

            if (_originalEditor != null)
                DestroyImmediate(_originalEditor);
        }

        /// <summary>
        ///     CreateInspectorGUI is called for UIToolkit inspectors.
        ///     OnInspectorGUI is called for IMGUI.
        ///     Note: This is called each time something else is selected with this one locked.
        /// </summary>
        /// <returns>What should be shown on the inspector</returns>
        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            if (target == null)
                return _root;

            ////These are to test different cultures where DecimalSeparator is different.
            ////It is required for copy-pasting
            //Debug.Log(System.Globalization.CultureInfo.CurrentCulture);
            //System.Globalization.CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            //Debug.Log(System.Globalization.CultureInfo.CurrentCulture);
            //Debug.Log(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);

            _inspectorEditorSettings = BetterInspectorEditorSettings.instance;
            _betterTransformSettings = BetterTransformSettings.instance;
            _logPerformance = _betterTransformSettings.logPerformance;
            _logDetailedPerformance = _betterTransformSettings.logDetailedPerformance;

            _sizeSetupDone = false;

            if (_logPerformance)
            {
                if (_stopwatch != null) _stopwatch.Reset();
                else _stopwatch = new Stopwatch();

                _stopwatch.Start();
            }

            LogDetailedPerformance("Start");
            _domainReloaded = false;

            transform = target as Transform;
            _soTarget = new SerializedObject(target);

            LogDetailedPerformance("Serializing target time");

            //In-case reference to the asset is lost, retrieve it from the file location
            if (visualTreeAsset == null)
                visualTreeAsset = Utility.GetVisualTreeAsset(VisualTreeAssetFileLocation, VisualTreeAssetGuid);

            // If the Better Transform UXML still can't be found,
            // fall back to the default inspector.
            if (visualTreeAsset == null)
            {
                LoadDefaultEditor(_root);

                if (!_logPerformance) return _root;
                Log("Total time spent", _stopwatch.ElapsedMilliseconds);
                _stopwatch.Stop();
                return _root;
            }

            visualTreeAsset.CloneTree(_root);

            LogPerformance("Cloning visual asset tree");

            FirstTimeSetup();

            UpdateStyleSheets();

            _pingSelfButton = _root.Q<Button>("PingSelfButton");
            if (targets.Length == 1 && _betterTransformSettings.pingSelfButton && originalTransform == null)
            {
                _pingSelfButton.style.display = DisplayStyle.Flex;
                _pingSelfButton.clicked += () =>
                {
                    //Multi ping is commented out because PingObject only pings the last one.
                    EditorGUIUtility.PingObject(transform);
                };
            }
            else
            {
                _pingSelfButton.style.display = DisplayStyle.None;
            }

            SetupGizmoToggles();

            StartSizeSchedule();


            if (originalTransform == null)
            {
                _root.schedule.Execute(() =>
                {
                    _inspectorEditorSettings.selectedTransform = transform;
                    _inspectorEditorSettings.SelectedBetterTransformEditorRoot = _root;
                }).ExecuteLater(100);
            }

            //Finish code above this line------------------
            if (!_logPerformance) return _root;
            Log("Total time spent", _stopwatch.ElapsedMilliseconds);
            _stopwatch.Stop();
            return _root;
        }

        void UpdateStyleSheets()
        {
            StyleSheetsManager.UpdateStyleSheet(_root);

            switch (_inspectorEditorSettings.selectedFoldoutStyle)
            {
                case 1:
                    _root.styleSheets.Add(betterTransformStyleSheet1);
                    _root.styleSheets.Remove(betterTransformStyleSheet2);
                    _root.styleSheets.Remove(betterTransformStyleSheet3);
                    break;
                case 2:
                    _root.styleSheets.Remove(betterTransformStyleSheet1);
                    _root.styleSheets.Add(betterTransformStyleSheet2);
                    _root.styleSheets.Remove(betterTransformStyleSheet3);
                    break;
                case 3:
                    _root.styleSheets.Remove(betterTransformStyleSheet1);
                    _root.styleSheets.Remove(betterTransformStyleSheet2);
                    _root.styleSheets.Add(betterTransformStyleSheet3);
                    break;
            }
        }

        void SetupGizmoToggles()
        {
            GroupBox gizmoTogglesGroupBox = _root.Q<GroupBox>("GizmoToggles");
            if (_thisIsAnAsset || originalTransform != null)
                gizmoTogglesGroupBox.style.display = DisplayStyle.None;

            _sizeOutlineGizmoOnButton = _root.Q<Button>("GizmoOn");
            _sizeOutlineGizmoOffButton = _root.Q<Button>("GizmoOff");
            _sizeOutlineGizmoOnButton.clicked += () =>
            {
                _betterTransformSettings.ShowSizeOutlineGizmo = false;
                UpdateGizmoButton_Outline();
                SceneView.RepaintAll();
            };
            _sizeOutlineGizmoOffButton.clicked += () =>
            {
                _betterTransformSettings.ShowSizeOutlineGizmo = true;
                UpdateGizmoButton_Outline();
                SceneView.RepaintAll();
            };
            UpdateGizmoButton_Outline();

            _sizeLabelsGizmoOn = gizmoTogglesGroupBox.Q<Button>("SizeLabelsGizmoOn");
            _sizeLabelsGizmoOff = gizmoTogglesGroupBox.Q<Button>("SizeLabelsGizmoOff");
            _sizeLabelsGizmoOn.clicked += () =>
            {
                _betterTransformSettings.ShowSizeLabelGizmo = false;
                UpdateGizmoButton_sizeLabel();
                SceneView.RepaintAll();
            };
            _sizeLabelsGizmoOff.clicked += () =>
            {
                _betterTransformSettings.ShowSizeLabelGizmo = true;
                UpdateGizmoButton_sizeLabel();
                SceneView.RepaintAll();
            };
            UpdateGizmoButton_sizeLabel();
        }

        void UpdateGizmoButton_Outline()
        {
            if (!_betterTransformSettings.ShowSizeFoldout && !_betterTransformSettings.ShowSizeInLine)
            {
                _sizeOutlineGizmoOnButton.style.display = DisplayStyle.None;
                _sizeOutlineGizmoOffButton.style.display = DisplayStyle.None;
                return;
            }

            if (_betterTransformSettings.ShowSizeOutlineGizmo)
            {
                _sizeOutlineGizmoOnButton.style.display = DisplayStyle.Flex;
                _sizeOutlineGizmoOffButton.style.display = DisplayStyle.None;
            }
            else
            {
                _sizeOutlineGizmoOnButton.style.display = DisplayStyle.None;
                _sizeOutlineGizmoOffButton.style.display = DisplayStyle.Flex;
            }
        }

        void UpdateGizmoButton_sizeLabel()
        {
            if (!_betterTransformSettings.ShowSizeFoldout && !_betterTransformSettings.ShowSizeInLine)
            {
                _sizeLabelsGizmoOn.style.display = DisplayStyle.None;
                _sizeLabelsGizmoOff.style.display = DisplayStyle.None;
                return;
            }

            if (_betterTransformSettings.ShowSizeLabelGizmo)
            {
                _sizeLabelsGizmoOn.style.display = DisplayStyle.Flex;
                _sizeLabelsGizmoOff.style.display = DisplayStyle.None;
            }
            else
            {
                _sizeLabelsGizmoOn.style.display = DisplayStyle.None;
                _sizeLabelsGizmoOff.style.display = DisplayStyle.Flex;
            }
        }


        VisualElement _sizeUpdateScheduleHolder;

        void StartSizeSchedule()
        {
            RemoveSizeUpdateScheduler();

            if (!_betterTransformSettings.ConstantSizeUpdate)
                return;

            _sizeUpdateScheduleHolder = new VisualElement();
            _root.Add(_sizeUpdateScheduleHolder);

            _sizeUpdateScheduleHolder.schedule.Execute(() => UpdateSize(true)).Every(3000)
                .ExecuteLater(3000); //1000 ms = 1 s
        }

        void RemoveSizeUpdateScheduler()
        {
            _sizeUpdateScheduleHolder?.RemoveFromHierarchy();
        }

        void FirstTimeSetup()
        {
            if (_root == null) return;

            _sizeCalculator ??= new SizeCalculation(_betterTransformSettings);
            _topGroupBox = _root.Q<GroupBox>("TopGroupBox");
            _toolbarsGroupBox = _root.Q<GroupBox>("ToolbarsGroupBox");

            _sizeFoldoutWarning = _root.Q<Label>("SizeUpdateWarning");
            _worldSpaceWarning = _root.Q<Label>("WorldSpaceWarning");

            UpdatePerformanceLoggingGroupBox();

            LogDetailedPerformance("Prerequisite");

            SetupSizeCommon();

            if (targets.Length == 1)
            {
                LogPerformance("Settings");

                if (_betterTransformSettings.ShowSizeFoldout || _betterTransformSettings.ShowSizeInLine)
                    SetupSize();
                else
                    HideSize();


                LogPerformance("Size (Hidden or visible)");
            }
            else
            {
                HideSize();
            }

            SetupMainControls();
            UpdateMainControls();
            LogPerformance("Position/Rotation/Size Fields");

            _quickActions = new QuickActions(targets, _root, this);
            LogPerformance("Quick Action Buttons");

            SetupGuid();
            LogDetailedPerformance("GUID");

            SetupParentChild();
            LogPerformance("Parent & Child GameObject Information");

            SetupMenu();
            LogDetailedPerformance("Settings");

            SetupAnimatorCompability();
            LogDetailedPerformance("Animator Compatibility");

            SetupViewWidthAdaption();
            LogDetailedPerformance("View Width Adaption");

            SetupInspectorColor();
            LogDetailedPerformance("Inspector Color");
        }

        /// <summary>
        /// Will pass to Debug Log whatever msg is given
        /// </summary>
        /// <param name="msg">The message shown in Debug Log</param>
        /// <param name="time">The time in milliseconds</param>
        static void Log(string msg, float time) => Debug.Log(msg + " : <color=yellow>" + time + "</color> ms");

        /// <summary>
        /// Logs that are only shown when log performance is turned on. Otherwise, it will not print
        /// </summary>
        /// <param name="msg">The message shown in Debug Log</param>
        void LogPerformance(string msg)
        {
            if (!_logPerformance) return;

            _time = _stopwatch.ElapsedMilliseconds - _totalMS;
            _totalMS += _time;
            Log(msg, _time);
        }


        /// <summary>
        /// Logs that are only shown when both log performance and log detailed performance is on.
        /// </summary>
        /// <param name="msg">The message shown in Debug Log</param>
        void LogDetailedPerformance(string msg)
        {
            if (!_logPerformance) return;
            if (!_logDetailedPerformance) return;

            _time = _stopwatch.ElapsedMilliseconds - _totalMS;
            _totalMS += _time;
            Log(msg, _time);
        }

        void UpdatePerformanceLoggingGroupBox()
        {
            if (_betterTransformSettings.logPerformance)
                TurnOnPerformanceLogging();
            else if (_performanceLoggingGroupBox != null)
                TurnOffPerformanceLogging();
        }

        void TurnOnPerformanceLogging()
        {
            //In case of a domain reload, the element is not deleted but reference is lost.
            _performanceLoggingGroupBox ??= _root.Q<GroupBox>("PerformanceLoggingGroup");

            //If one doesn't exist, create it
            if (_performanceLoggingGroupBox == null)
                CreatePerformanceLoggingGroupBox();

            // ReSharper disable once PossibleNullReferenceException
            _performanceLoggingGroupBox.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        ///     Unity 2023.2.18f1 seems to be slow at loading large UXML files, so, removing some less used stuff from UXML to C#
        /// </summary>
        void CreatePerformanceLoggingGroupBox()
        {
            _performanceLoggingGroupBox = new GroupBox();

            Button button = new Button
            {
                text = "Turn Off performance logging"
            };
            button.clicked += TurnOffPerformanceLogging;
            _performanceLoggingGroupBox.Add(button);

            _performanceLoggingGroupBox.Add(new HelpBox(
                "Please note that console logs can negatively impact performance of the inspector. So, the delays you will see here will be higher than normal usage. However, these logs serve a crucial purpose—they assist in identifying resource-intensive features. Just remember to turn it off when not needed.",
                HelpBoxMessageType.Info));

            _root.Add(_performanceLoggingGroupBox);
        }

        void TurnOffPerformanceLogging()
        {
            _betterTransformSettings.logPerformance = false;

            _performanceLoggingGroupBox.style.display = DisplayStyle.None;
            _betterTransformSettings.Save();
            Toggle performanceLoggingToggle = _root.Q<Toggle>("PerformanceLoggingToggle");
            if (performanceLoggingToggle != null) performanceLoggingToggle.value = false;
        }

        /// <summary>
        ///  This is assigned for secondary editors only. Secondary editors are parent/child editors that are drawn in foldouts. If this value is null, this editor is the original editor.
        /// </summary>
        public Transform originalTransform;

        // ReSharper disable once MemberCanBePrivate.Global
        public VisualElement CreateInspectorInsideAnother(Transform newOriginal)
        {
            originalTransform = newOriginal;
            return CreateInspectorGUI();
        }

        #endregion Unity Stuff

        #region Main Controls

        #region Variables

        Button _worldSpaceButton;
        Label _worldSpaceLabel;
        Button _localSpaceButton;
        Label _localSpaceLabel;

        GroupBox _defaultEditorGroupBox;
        GroupBox _customEditorGroupBox;
        GroupBox _toolbarsGroupBox;

        const string PositionProperty = "m_LocalPosition";
        GroupBox _positionGroupBox;
        Label _positionLabel;
        Vector3Field _localPositionField;
        Vector3Field _worldPositionField;
        VisualElement _positionPrefabOverrideMark;
        VisualElement _positionDefaultPrefabOverrideMark;

        const string RotationProperty = "m_LocalRotation";
        GroupBox _rotationGroupBox;
        Label _rotationLabel;
        Vector3Field _localRotationField;

        /// <summary>
        ///     transform.eulerAngles
        /// </summary>
        Vector3Field _worldRotationField;

        /// <summary>
        ///     An internal editor only property that used to store the value you set in the local rotation field.
        /// </summary>
        SerializedProperty _serializedEulerHint;

        SerializedProperty _rotationSerializedProperty;
        PropertyField _quaternionRotationPropertyField;

        VisualElement _rotationPrefabOverrideMark;
        VisualElement _rotationDefaultPrefabOverrideMark;

        const string ScaleProperty = "m_LocalScale";
        GroupBox _scaleGroupBox;
        GroupBox _scaleLabelGroupbox;
        Label _scaleLabel;
        Vector3Field _boundLocalScaleField;
        Vector3Field _localScaleField;
        Vector3Field _worldScaleField;

        //private SerializedProperty m_ConstrainProportionsScaleProperty; //doesn't work

        Button _copyScaleButton;
        Button _pasteScaleButton;
        Button _resetScaleButton;
        VisualElement _scalePrefabOverrideMark;
        Button _scaleAspectRatioLocked;
        Button _scaleAspectRatioUnlocked;

        const string WorldPositionReadOnlyTooltip = "World position is readonly if multiple object is selected.";
        const string WorldRotationReadOnlyTooltip = "World rotation is readonly if multiple object is selected.";
        const string WorldScaleReadOnlyTooltip = "World scale is readonly if multiple object is selected.";

        #endregion Variables

        IVisualElementScheduledItem _scheduledItem;

        //Called when the inspector window first shows up
        void SetupMainControls()
        {
            SetupWorkSpace();

            _scheduledItem = _worldSpaceButton.schedule.Execute(UpdateWorldSpaceFields_WhenInWorldSpaceWorkspace)
                .Every(1000).StartingIn(5000);

            _defaultEditorGroupBox = _root.Q<GroupBox>("DefaultUnityInspector");
            _customEditorGroupBox = _root.Q<GroupBox>("CustomEditorGroupBox");

            if (targets.Length > 1)
                LoadDefaultEditor(_defaultEditorGroupBox);

            SetupPosition();
            SetupRotation();
            SetupScale();
            UpdateToolbarVisibility();
        }

        //Called during various instances where the inspector window is updated
        void UpdateMainControls()
        {
            UpdateWorkSpaceButtons();

            UpdatePosition();

            UpdateRotation();

            UpdateScale();


            if (_betterTransformSettings.ShowSizeInLine || _betterTransformSettings.ShowSizeFoldout)
                UpdateSize(true);

            UpdateAutoRefreshButton();

            if (_betterTransformSettings.LoadDefaultInspector || targets.Length > 1)
            {
                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (_betterTransformSettings.CurrentWorkSpace)
                {
                    case BetterTransformSettings.WorkSpace.Local:
                        _customEditorGroupBox.style.display = DisplayStyle.None;
                        _defaultEditorGroupBox.style.display = DisplayStyle.Flex;

                        if (_defaultEditorGroupBox.childCount == 0) LoadDefaultEditor(_defaultEditorGroupBox);

                        if (Application.isPlaying)
                        {
                            if (ShouldShowSize())
                            {
                                if (!_betterTransformSettings.ConstantSizeUpdate)
                                {
                                    if (_betterTransformSettings.autoRefreshSizeInLocalSpaceInPlaymode)
                                    {
                                        _sizeFoldoutWarning.style.display = DisplayStyle.Flex;
                                        _sizeFoldoutWarning.text =
                                            "Auto-refreshing size may impact performance in Play Mode.";
                                        BindFields();
                                    }
                                    else
                                    {
                                        _sizeFoldoutWarning.style.display = DisplayStyle.None;
                                        UnBindFields();
                                    }
                                }
                                else
                                {
                                    UnBindFields();
                                    _sizeFoldoutWarning.style.display = DisplayStyle.Flex;
                                    _sizeFoldoutWarning.text =
                                        "Size is being rechecked every few seconds.";
                                    _sizeFoldoutWarning.tooltip =
                                        "This is rarely necessary and can be turned off in the settings to improve performance.";
                                }
                            }
                            else
                            {
                                UnBindFields();
                            }
                        }
                        else
                        {
                            if (ShouldShowSize() && _betterTransformSettings.autoRefreshSize) BindFields();
                            else UnBindFields();

                            UpdateSizeFoldoutWarnings();
                        }

                        _root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.None;
                        _root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.None;
                        _worldSpaceWarning.style.display = DisplayStyle.None;

                        break;

                    case BetterTransformSettings.WorkSpace.World:
                        _customEditorGroupBox.style.display = DisplayStyle.Flex;
                        _defaultEditorGroupBox.style.display = DisplayStyle.None;

                        if (_defaultEditorGroupBox.childCount != 0)
                        {
                            DestroyImmediate(_originalEditor);
                            _defaultEditorGroupBox.Clear();
                        }

                        BindFields();
                        UpdateSizeFoldoutWarnings();

                        _root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.None;
                        _root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.None;

                        if (Application.isPlaying) _worldSpaceWarning.style.display = DisplayStyle.Flex;
                        else _worldSpaceWarning.style.display = DisplayStyle.None;

                        break;

                    case BetterTransformSettings.WorkSpace.Both:
                        _customEditorGroupBox.style.display = DisplayStyle.Flex;
                        _defaultEditorGroupBox.style.display = DisplayStyle.Flex;

                        if (_defaultEditorGroupBox.childCount == 0) LoadDefaultEditor(_defaultEditorGroupBox);

                        BindFields();
                        UpdateSizeFoldoutWarnings();

                        _root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.Flex;
                        _root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.Flex;

                        if (Application.isPlaying) _worldSpaceWarning.style.display = DisplayStyle.Flex;
                        else _worldSpaceWarning.style.display = DisplayStyle.None;

                        break;
                }
            }
            else //Not using default inspector and one target is selected
            {
                if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
                {
                    _customEditorGroupBox.style.display = DisplayStyle.Flex;
                    _defaultEditorGroupBox.style.display = DisplayStyle.Flex;

                    if (_defaultEditorGroupBox.childCount == 0) LoadDefaultEditor(_defaultEditorGroupBox);

                    BindFields();
                    UpdateSizeFoldoutWarnings();

                    _root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.Flex;
                    _root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.Flex;

                    if (Application.isPlaying)
                    {
                        _worldSpaceWarning.style.display = DisplayStyle.Flex;
                        _worldSpaceWarning.text = WarningStringShowingCustomFieldsInPlaymode;
                    }
                    else _worldSpaceWarning.style.display = DisplayStyle.None;
                }
                else
                {
                    _customEditorGroupBox.style.display = DisplayStyle.Flex;
                    _defaultEditorGroupBox.style.display = DisplayStyle.None;

                    BindFields();
                    UpdateSizeFoldoutWarnings();

                    _root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.None;
                    _root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.None;

                    if (Application.isPlaying)
                    {
                        _worldSpaceWarning.style.display = DisplayStyle.Flex;
                        if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                            _worldSpaceWarning.text = WarningStringShowingWorldSpaceInPlaymode;
                        else //Local work space
                            _worldSpaceWarning.text = WarningStringShowingCustomFieldsInPlaymode;
                    }
                    else _worldSpaceWarning.style.display = DisplayStyle.None;
                }
            }

            if (_betterTransformSettings.CurrentWorkSpace is BetterTransformSettings.WorkSpace.World
                or BetterTransformSettings.WorkSpace.Both)
                _scheduledItem.Resume();
            else
                _scheduledItem.Pause();
        }

        /// <summary>
        /// Returns if this should show size or not
        /// </summary>
        /// <returns></returns>
        bool ShouldShowSize()
        {
            return (_betterTransformSettings.ShowSizeFoldout || _betterTransformSettings.ShowSizeInLine) &&
                   targets.Length < 2;
        }

        /// <summary>
        /// The copy, paste and rotate buttons
        /// </summary>
        //This used to be in UpdateMainControls. Moved to SetupMainControls. Watch out for bugs
        void UpdateToolbarVisibility(float width = 1000)
        {
            if (_betterTransformSettings.ShowCopyPasteButtons && width > 220)
            {
                _toolbarsGroupBox.style.display = DisplayStyle.Flex;
                if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
                    _toolbarsGroupBox.Q<GroupBox>("BothSpaceToolbarForLocalSpace").style.display = DisplayStyle.Flex;
                else
                    _toolbarsGroupBox.Q<GroupBox>("BothSpaceToolbarForLocalSpace").style.display = DisplayStyle.None;


                if (_sizeToolbar != null) _sizeToolbar.style.display = DisplayStyle.Flex;
                if (_sizeCenterFoldoutGroup != null)
                    _sizeCenterFoldoutGroup.Q<GroupBox>("SizeCenterToolbar").style.display = DisplayStyle.Flex;
            }
            else
            {
                _toolbarsGroupBox.style.display = DisplayStyle.None;
                if (_sizeToolbar != null) _sizeToolbar.style.display = DisplayStyle.None;
                if (_sizeCenterFoldoutGroup != null)
                    _sizeCenterFoldoutGroup.Q<GroupBox>("SizeCenterToolbar").style.display = DisplayStyle.None;
            }
        }

        void UpdateSizeFoldoutWarnings()
        {
            if ((_betterTransformSettings.ShowSizeFoldout || _betterTransformSettings.ShowSizeInLine) &&
                _betterTransformSettings.ConstantSizeUpdate)
            {
                _sizeFoldoutWarning.style.display = DisplayStyle.Flex;
                _sizeFoldoutWarning.text =
                    "Size is being rechecked every few seconds.";
                _sizeFoldoutWarning.tooltip =
                    "This is rarely necessary and can be turned off in the settings to improve performance.";
            }
            else
            {
                _sizeFoldoutWarning.style.display = DisplayStyle.None;
            }
        }

        void UnBindFields()
        {
            _localPositionField.Unbind();
            _localPositionField.bindingPath = null;
            _quaternionRotationPropertyField.Unbind();
            _quaternionRotationPropertyField.bindingPath = null;
            _boundLocalScaleField.Unbind();
            _boundLocalScaleField.bindingPath = null;
        }

        void BindFields()
        {
            _localPositionField.bindingPath = PositionProperty;
            _localPositionField.Bind(_soTarget);
            _quaternionRotationPropertyField.bindingPath = RotationProperty;
            _quaternionRotationPropertyField.Bind(_soTarget);
            _boundLocalScaleField.bindingPath = ScaleProperty;
            _boundLocalScaleField.Bind(_soTarget);
        }

        #region Workspace

        Toggle _sizeFoldoutToggle;

        Label _siblingIndexLabel;
        Label _siblingIndex;

        /// <summary>
        ///     These are the local/global workspace button at the top of the transform
        /// </summary>
        void SetupWorkSpace()
        {
            _worldSpaceButton = _topGroupBox.Q<Button>("WorldSpaceButton");
            _localSpaceButton = _topGroupBox.Q<Button>("LocalSpaceButton");

            _localSpaceLabel = _topGroupBox.Q<Label>("LocalSpaceLabel");
            _worldSpaceLabel = _topGroupBox.Q<Label>("WorldSpaceLabel");

            //worldSpaceButton.clickable = null; //Not needed since this is called only once and at the beginning
            _worldSpaceButton.clicked += () =>
            {
                _betterTransformSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.Local;
                UpdateMainControls();
                UpdateSize();
                UpdateInspectorColor();

                _quickActions.WorkspaceChanged();
            };
            //localSpaceButton.clickable = null; //Not needed since this is called only once and at the beginning
            _localSpaceButton.clicked += () =>
            {
                _betterTransformSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.World;
                UpdateMainControls();
                UpdateSize();
                UpdateInspectorColor();

                _quickActions.WorkspaceChanged();
            };

            _sizeFoldout ??= _root.Q<GroupBox>("SizeFoldout");
            _sizeLabelGroupBox = _root.Q<GroupBox>("SizeLabelGroupBox");
            _sizeToolbar = _sizeFoldout.Q<GroupBox>("SizeToolbar");

            if (_betterTransformSettings.ShowSizeFoldout)
            {
                if (_sizeFoldoutToggle == null)
                {
                    _sizeFoldoutToggle = _sizeFoldout.Q<Toggle>("FoldoutToggle");
                    _sizeFoldoutToggle.tooltip =
                        "World size calculates the size of an object based off of world axis.\n\n" +
                        "Local size is the size of the object in 0 angle local rotation. \n" +
                        "This can be impacted by it's parent's rotation and scale.\n" +
                        "Only local size is shown when both world space and local space is shown.";
                }
            }

            _siblingIndexLabel = _topGroupBox.Q<Label>("SiblingIndexLabel");
            _siblingIndex = _topGroupBox.Q<Label>("SiblingIndex");

            if (_betterTransformSettings.showSiblingIndex && transform.parent)
                UpdateSiblingIndex(transform, _siblingIndex);
            else
            {
                _siblingIndexLabel.style.display = DisplayStyle.None;
                _siblingIndex.style.display = DisplayStyle.None;
            }
        }

        void UpdateWorkSpaceButtons()
        {
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (_betterTransformSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    _worldSpaceButton.style.display = DisplayStyle.None;
                    _localSpaceButton.style.display = DisplayStyle.Flex;


                    SceneView.RepaintAll();
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    _worldSpaceButton.style.display = DisplayStyle.Flex;
                    _localSpaceButton.style.display = DisplayStyle.None;


                    SceneView.RepaintAll();
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    _localSpaceButton.style.display = DisplayStyle.None;
                    _worldSpaceButton.style.display = DisplayStyle.None;

                    SceneView.RepaintAll();
                    break;
            }

            if (_autoRefreshSizeButton != null) UpdateAutoRefreshButton();
        }


        void UpdateWorldSpaceFields_WhenInWorldSpaceWorkspace()
        {
            if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                UpdateWorldSpaceFields();
        }

        void UpdateWorldSpaceFields()
        {
            //Not sure why, but a user reported they received a null reference error for target here
            //Due to version difference, couldn't confirm the line.
            //Remove this later after further testing.
            if (transform == null)
                transform = target as Transform;

            if (transform == null)
                return;

            if (_betterTransformSettings.roundPositionField)
                _worldPositionField.SetValueWithoutNotify(RoundedVector3(transform.position));
            else
                _worldPositionField.SetValueWithoutNotify(transform.position);

            if (_betterTransformSettings.roundRotationField)
                _worldRotationField.SetValueWithoutNotify(RoundedVector3(transform.eulerAngles));
            else
                _worldRotationField.SetValueWithoutNotify(TrimVectorNoise(transform.eulerAngles));

            if (_betterTransformSettings.roundScaleField)
                _worldScaleField.SetValueWithoutNotify(RoundedVector3(transform.lossyScale));
            else
                _worldScaleField.SetValueWithoutNotify(transform.lossyScale);
        }

        #endregion Workspace

        #region Position

        void SetupPosition()
        {
            _positionGroupBox = _customEditorGroupBox.Q<GroupBox>("Position");

            _positionLabel = _positionGroupBox.Q<Label>("PositionLabel");

            SetupPosition_fields();

            _positionPrefabOverrideMark = _positionGroupBox.Q<VisualElement>("PrefabOverrideMark");
            _positionDefaultPrefabOverrideMark = _positionGroupBox.Q<VisualElement>("DefaultPrefabOverrideMark");
        }

        void UpdatePosition()
        {
            UpdatePosition_label();
            UpdatePosition_fields();
            UpdatePosition_prefabOverrideIndicator();
        }

        void UpdatePosition_label()
        {
            if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
            {
                _positionLabel.tooltip = "The local position of this GameObject relative to the parent.";
            }
            else //If world space or both is chosen, the custom label will show world position
            {
                _positionLabel.tooltip = "The world position of this GameObject.";
                if (targets.Length > 1)
                {
                    _positionLabel.tooltip += "\n" + WorldPositionReadOnlyTooltip;
                    _positionLabel.SetEnabled(false);
                }
            }

            UpdatePositionLabelContextMenu();
        }

        /// <summary>
        ///     The right click menu on the position label.
        /// </summary>
        void UpdatePositionLabelContextMenu()
        {
            //Remove the old context menu
            if (_contextualMenuManipulatorForPositionLabel != null)
                _positionLabel.RemoveManipulator(_contextualMenuManipulatorForPositionLabel);

            UpdateContextMenuForPosition();

            _positionLabel.AddManipulator(_contextualMenuManipulatorForPositionLabel);
            return;

            void UpdateContextMenuForPosition()
            {
                _contextualMenuManipulatorForPositionLabel = new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Position :", _ => { }, DropdownMenuAction.AlwaysDisabled);
                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy property path",
                        _ => { EditorGUIUtility.systemCopyBuffer = PositionProperty; },
                        DropdownMenuAction.AlwaysEnabled);

                    // if (_betterTransformSettings.roundPositionField)
                    //     evt.menu.AppendAction("Round out field values for the inspector",
                    //         _ => TogglePositionFieldRounding(), DropdownMenuAction.Status.Checked);
                    // else
                    //     evt.menu.AppendAction("Round out field values for the inspector",
                    //         // ReSharper disable once RedundantArgumentDefaultValue
                    //         _ => TogglePositionFieldRounding(), DropdownMenuAction.Status.Normal);

                    if (HasPrefabOverride_position())
                    {
                        evt.menu.AppendSeparator();
                        if (HasPrefabOverride_position(true))
                            evt.menu.AppendAction(
                                "Apply to Prefab '" +
                                PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'",
                                _ => ApplyPositionChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                        else
                            evt.menu.AppendAction(
                                "Apply to Prefab '" +
                                PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'",
                                _ => ApplyPositionChangeToPrefab(), DropdownMenuAction.AlwaysDisabled);

                        evt.menu.AppendAction("Revert", _ => RevertPositionChangeToPrefab(),
                            DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy", _ => _quickActions.CopyPosition());
                    if (_quickActions.HasVector3ValueToPaste())
                        evt.menu.AppendAction("Paste", _ => _quickActions.PastePosition(),
                            DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", _ => _quickActions.PastePosition(),
                            DropdownMenuAction.AlwaysDisabled);

                    evt.menu.AppendAction("Reset", _ => _quickActions.ResetPosition());
                });
            }

            void ApplyPositionChangeToPrefab()
            {
                if (_soTarget.FindProperty(PositionProperty).prefabOverride)
                    PrefabUtility.ApplyPropertyOverride(_soTarget.FindProperty(PositionProperty),
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform), InteractionMode.UserAction);
            }

            void RevertPositionChangeToPrefab()
            {
                if (_soTarget.FindProperty("m_LocalPosition").prefabOverride)
                    PrefabUtility.RevertPropertyOverride(_soTarget.FindProperty(PositionProperty),
                        InteractionMode.UserAction);
            }
        }

        void TogglePositionFieldRounding()
        {
            _betterTransformSettings.roundPositionField = !_betterTransformSettings.roundPositionField;
            _betterTransformSettings.Save();

            if (_betterTransformSettings.roundPositionField)
            {
                _localPositionField.SetValueWithoutNotify(RoundedVector3(transform.localPosition));
                _worldPositionField.SetValueWithoutNotify(RoundedVector3(transform.position));
            }
            else
            {
                _localPositionField.SetValueWithoutNotify(transform.localPosition);
                _worldPositionField.SetValueWithoutNotify(transform.position);
            }

            UpdatePositionLabelContextMenu();

            _roundPositionFieldToggle?.SetValueWithoutNotify(_betterTransformSettings.roundPositionField);
        }

        /// <summary>
        ///     This is the right click menu on the label
        /// </summary>
        ContextualMenuManipulator _contextualMenuManipulatorForPositionLabel;

        HelpBox _bigNumberWarning;
        bool _isPositionUpdatedByWorldField;

        void SetupPosition_fields()
        {
            _localPositionField = _positionGroupBox.Q<Vector3Field>("LocalPosition");
            _worldPositionField = _positionGroupBox.Q<Vector3Field>("WorldPosition");

            if (targets.Length > 1)
            {
                _worldPositionField.SetEnabled(false);
                _worldPositionField.tooltip = WorldPositionReadOnlyTooltip;
            }

            //Because the bound local position field updates this, the field needs to be re-rounded after a single frame to not be ignored when the binding updates this
            //that is done in the RegisterLocalPositionFieldValueChangedCallBack() method
            if (_betterTransformSettings.roundPositionField)
                _localPositionField.SetValueWithoutNotify(RoundedVector3(transform.localPosition));

            //This makes sure the binding operation is done before the callback is registered to avoid it calling the change
            _localPositionField.schedule.Execute(RegisterLocalPositionFieldValueChangedCallBack).ExecuteLater(100);

            _worldPositionField.schedule.Execute(() =>
            {
                _worldPositionField.RegisterValueChangedCallback(ev =>
                {
                    //This doesn't work with the recorder: //Undo isn't required here because the transform position update will record the Undo
                    Undo.RecordObject(transform, "Position change on " + transform.gameObject.name);

                    _isPositionUpdatedByWorldField = true;
                    transform.position = ev.newValue;

                    if (_betterTransformSettings.roundPositionField)
                        _worldPositionField.SetValueWithoutNotify(RoundedVector3(ev.newValue));
                });
            }).ExecuteLater(0);

            _bigNumberWarning = _root.Q<HelpBox>("BigNumberWarning");
        }

        void RegisterLocalPositionFieldValueChangedCallBack()
        {
            //This is also called by world position field update
            _localPositionField.RegisterValueChangedCallback(ev =>
            {
                if (ev.newValue == ev.previousValue) return;

                //A true "fromBinding" value means the change came from a script
                bool fromBinding = ev.target == _localPositionField && ev.currentTarget == _localPositionField;

                if (!_isPositionUpdatedByWorldField) UpdateWorldPositionField();

                if (!Application.isPlaying)
                    Undo.RecordObject(transform, "Position change on " + transform.gameObject.name);

                if (!fromBinding)
                    _soTarget.Update();

                UpdatePosition_prefabOverrideIndicator();
                UpdatePosition_label();

                AutoUpdateSizeAfterValueChanged();
                // UpdateSize();

                UpdateWarningIfRequired();

                if (_betterTransformSettings.roundPositionField)
                    _localPositionField.SetValueWithoutNotify(RoundedVector3(ev.newValue));

                _localPositionField.schedule.Execute(UpdateAnimatorState_PositionFields)
                    .ExecuteLater(100); //1000 ms = 1 s
            });

            //Because the bound local position field updates this, the field needs to be rounded after a single frame to not be ignored when the binding updates this
            if (_betterTransformSettings.roundPositionField)
                _localPositionField.SetValueWithoutNotify(RoundedVector3(transform.localPosition));
        }

        void UpdateWorldPositionField()
        {
            if (_betterTransformSettings.roundPositionField)
                _worldPositionField.SetValueWithoutNotify(RoundedVector3(transform.position));
            else
                _worldPositionField.SetValueWithoutNotify(transform.position);

            if (targets.Length == 1)
                return;

            float commonX = transform.position.x;
            bool isCommonX = true;
            float commonY = transform.position.y;
            bool isCommonY = true;
            float commonZ = transform.position.z;
            bool isCommonZ = true;

            foreach (Transform t in targets.Cast<Transform>())
            {
                if (isCommonX)
                    if (!Approximately(t.position.x, commonX))
                        isCommonX = false;

                if (isCommonY)
                    if (!Approximately(t.position.y, commonY))
                        isCommonY = false;

                if (isCommonZ)
                    if (!Approximately(t.position.z, commonZ))
                        isCommonZ = false;

                if (!isCommonX && !isCommonY && !isCommonZ)
                    break;
            }

            FloatField xField = _worldPositionField.Q<FloatField>("unity-x-input");
            if (!isCommonX)
                xField.showMixedValue = true;
            else
                xField.showMixedValue = false;
            //xField.RemoveFromClassList(mixedValueLabelClass);
            FloatField yField = _worldPositionField.Q<FloatField>("unity-y-input");
            if (!isCommonY)
                yField.showMixedValue = true;
            else
                yField.showMixedValue = false;
            //yField.RemoveFromClassList(mixedValueLabelClass);
            //yField.value = transform.position.y;
            FloatField zField = _worldPositionField.Q<FloatField>("unity-z-input");
            if (!isCommonZ)
                zField.showMixedValue = true;
            else
                zField.showMixedValue = false;
        }

        void UpdatePosition_fields()
        {
            UpdateWorldPositionField();
            //worldPositionField.SetValueWithoutNotify(transform.position);

            //Don't need to set local position field because it is a bound field created in the UIBuilder

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (_betterTransformSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    _localPositionField.style.display = DisplayStyle.Flex;
                    _worldPositionField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    _localPositionField.style.display = DisplayStyle.None;
                    _worldPositionField.style.display = DisplayStyle.Flex;
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    _localPositionField.style.display =
                        DisplayStyle.None; //The default inspector will be used to show local fields
                    _worldPositionField.style.display = DisplayStyle.Flex;
                    break;
            }

            UpdateWarningIfRequired();
        }

        void UpdateWarningIfRequired()
        {
            if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local &&
                _betterTransformSettings.LoadDefaultInspector)
            {
                if (_bigNumberWarning == null) return;
                _bigNumberWarning.style.display = DisplayStyle.None;
                _bigNumberWarning.parent.Remove(_bigNumberWarning);
                _bigNumberWarning = null;
                return;
            }

            if (Abs(transform.position.x) > 100000 || Abs(transform.position.y) > 100000 ||
                Abs(transform.position.z) > 100000)
            {
                if (_bigNumberWarning != null)
                    _bigNumberWarning.style.display = DisplayStyle.Flex;
                else
                    CreateBigNumberWarning();
            }
            else
            {
                if (_bigNumberWarning != null)
                    _bigNumberWarning.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Adds this text HelpBox "Due to floating-point precision limitations, it is recommended to bring the world coordinates within a smaller range"
        /// </summary>
        void CreateBigNumberWarning()
        {
            _bigNumberWarning =
                new HelpBox(
                    "Due to floating-point precision limitations, it is recommended to bring the world coordinates of the GameObject within a smaller range.",
                    HelpBoxMessageType.Warning)
                {
                    style =
                    {
                        marginLeft = 0,
                        marginRight = 0
                    }
                };
            _defaultEditorGroupBox.parent.Add(_bigNumberWarning);
        }


        void UpdatePosition_prefabOverrideIndicator()
        {
            if (!HasPrefabOverride_position())
            {
                _positionPrefabOverrideMark.style.display = DisplayStyle.None;
                _positionDefaultPrefabOverrideMark.style.display = DisplayStyle.None;

                _positionLabel.RemoveFromClassList(PrefabOverrideLabelUSSClass);
                _worldPositionField.RemoveFromClassList(PrefabOverrideLabelUSSClass);
            }
            else
            {
                if (!HasPrefabOverride_position(true))
                {
                    _positionDefaultPrefabOverrideMark.style.display = DisplayStyle.Flex;
                    _positionPrefabOverrideMark.style.display = DisplayStyle.None;
                }
                else
                {
                    _positionPrefabOverrideMark.style.display = DisplayStyle.Flex;
                    _positionDefaultPrefabOverrideMark.style.display = DisplayStyle.None;
                }

                _positionLabel.AddToClassList(PrefabOverrideLabelUSSClass);
                _worldPositionField.AddToClassList(PrefabOverrideLabelUSSClass);
            }
        }


        /// <summary>
        /// </summary>
        /// <param name="checkDefaultOverride">
        ///     Certain properties on the root GameObject of a Prefab instance are considered default overrides.
        ///     These are overridden by default and are usually rarely applied or reverted.
        ///     Most apply and revert operations will ignore default overrides.
        ///     https://docs.unity3d.com/ScriptReference/PrefabUtility.IsDefaultOverride.html
        /// </param>
        /// <returns></returns>
        bool HasPrefabOverride_position(bool checkDefaultOverride = false)
        {
            if (!_soTarget.FindProperty(PositionProperty).prefabOverride) return false;
            if (!checkDefaultOverride) return true;
            return !_soTarget.FindProperty(PositionProperty).isDefaultOverride;
        }

        #endregion Position

        #region Rotation

        void SetupRotation()
        {
            _rotationGroupBox = _customEditorGroupBox.Q<GroupBox>("Rotation");
            _rotationLabel = _rotationGroupBox.Q<Label>("RotationLabel");

            SetupRotation_fields();

            _rotationPrefabOverrideMark = _rotationGroupBox.Q<VisualElement>("PrefabOverrideMark");
            _rotationDefaultPrefabOverrideMark = _rotationGroupBox.Q<VisualElement>("DefaultPrefabOverrideMark");
        }

        void UpdateRotation()
        {
            UpdateRotation_label();
            UpdateRotation_fields();
            UpdateRotation_prefabOverrideIndicator();
        }

        void UpdateRotation_label()
        {
            if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
            {
                _rotationLabel.tooltip = "The local rotation of this GameObject relative to the parent.\n\n" +
                                         "Unity uses quaternions to store rotations, but displays them as Euler angles in the Inspector to make it easier for people to use.\n\n" +
                                         "An internal editor only property is used to store the value you set in the field.";
            }
            else
            {
                _rotationLabel.tooltip = "The world rotation of this GameObject.\n\n" +
                                         "Unity uses quaternions internally to store rotations, but displays them as Euler angles in the Inspector to make it easier for people to use.\n\n" +
                                         "The value you set in the field for global rotation isn't saved anywhere. " +
                                         "That's why it is retrieved from the quaternion rotation of the transform and although it is effectively the value you set, it can often look different.";

                if (targets.Length > 1)
                {
                    _rotationLabel.tooltip += "\n" + WorldRotationReadOnlyTooltip;
                    _rotationLabel.SetEnabled(false);
                }
            }

            UpdateRotationLabelContextMenu();
        }

        void UpdateRotationLabelContextMenu()
        {
            if (_contextualMenuManipulatorForRotationLabel != null)
                _rotationLabel.RemoveManipulator(_contextualMenuManipulatorForRotationLabel);

            UpdateContextMenuForRotation();

            _rotationLabel.AddManipulator(_contextualMenuManipulatorForRotationLabel);
            return;

            void UpdateContextMenuForRotation()
            {
                _contextualMenuManipulatorForRotationLabel = new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Rotation :", _ => { }, DropdownMenuAction.AlwaysDisabled);
                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy property path",
                        _ => { EditorGUIUtility.systemCopyBuffer = RotationProperty; },
                        DropdownMenuAction.AlwaysEnabled);

                    // if (_betterTransformSettings.roundRotationField)
                    //     evt.menu.AppendAction("Round out field values for the inspector",
                    //         _ => ToggleRotationFieldRounding(), DropdownMenuAction.Status.Checked);
                    // else
                    //     evt.menu.AppendAction("Round out field values for the inspector",
                    //         // ReSharper disable once RedundantArgumentDefaultValue
                    //         _ => ToggleRotationFieldRounding(), DropdownMenuAction.Status.Normal);

                    if (HasPrefabOverride_rotation())
                    {
                        evt.menu.AppendSeparator();
                        if (HasPrefabOverride_rotation(true))
                            evt.menu.AppendAction(
                                "Apply to Prefab '" +
                                PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'",
                                _ => ApplyRotationChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                        else
                            evt.menu.AppendAction(
                                "Apply to Prefab '" +
                                PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'",
                                _ => ApplyRotationChangeToPrefab(), DropdownMenuAction.AlwaysDisabled);

                        evt.menu.AppendAction("Revert", _ => RevertRotationChangeToPrefab(),
                            DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy Euler Angles", _ => _quickActions.CopyRotationEulerAngles());
                    evt.menu.AppendAction("Copy Quaternion", _ => _quickActions.CopyRotationQuaternion());

                    if (_quickActions.HasVector3ValueToPaste() || _quickActions.HasQuaternionValueToPaste())
                        evt.menu.AppendAction("Paste", _ => _quickActions.PasteRotation());
                    else
                        evt.menu.AppendAction("Paste", _ => _quickActions.PasteRotation(),
                            DropdownMenuAction.Status.Disabled);

                    evt.menu.AppendAction("Reset", _ => _quickActions.ResetRotation());
                });
            }


            void ApplyRotationChangeToPrefab()
            {
                if (_soTarget.FindProperty(RotationProperty).prefabOverride)
                    PrefabUtility.ApplyPropertyOverride(_soTarget.FindProperty(RotationProperty),
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform), InteractionMode.UserAction);
            }

            void RevertRotationChangeToPrefab()
            {
                if (_soTarget.FindProperty(RotationProperty).prefabOverride)
                    PrefabUtility.RevertPropertyOverride(_soTarget.FindProperty(RotationProperty),
                        InteractionMode.UserAction);
            }
        }

        void ToggleRotationFieldRounding()
        {
            _betterTransformSettings.roundRotationField = !_betterTransformSettings.roundRotationField;
            _betterTransformSettings.Save();

            if (_betterTransformSettings.roundRotationField)
            {
                _localRotationField.SetValueWithoutNotify(RoundedVector3(_serializedEulerHint.vector3Value));
                _worldRotationField.SetValueWithoutNotify(RoundedVector3(transform.eulerAngles));
            }
            else
            {
                _localRotationField.SetValueWithoutNotify(_serializedEulerHint.vector3Value);
                _worldRotationField.SetValueWithoutNotify(transform.eulerAngles);
            }

            UpdateRotationLabelContextMenu();
            _roundRotationFieldToggle?.SetValueWithoutNotify(_betterTransformSettings.roundRotationField);
        }

        /// <summary>
        ///     This is the right click menu on the label
        /// </summary>
        ContextualMenuManipulator _contextualMenuManipulatorForRotationLabel;


        bool _isRotateUpdatedByLocalField;
        bool _isRotationUpdatedByWorldField;

        void SetupRotation_fields()
        {
            _serializedEulerHint = _soTarget.FindProperty("m_LocalEulerAnglesHint");

            _localRotationField = _rotationGroupBox.Q<Vector3Field>("LocalRotation");

            if (_betterTransformSettings.roundRotationField)
                _localRotationField.SetValueWithoutNotify(RoundedVector3(_serializedEulerHint.vector3Value));
            else
                _localRotationField.SetValueWithoutNotify(TrimVectorNoise(_serializedEulerHint.vector3Value));

            //Setting the fields in the codes above should be unnecessary. Remove them later after testing.
            _localRotationField.schedule.Execute(ScheduleUpdateRotationField).ExecuteLater(0);

            _localRotationField.schedule.Execute(() =>
            {
                _localRotationField.RegisterValueChangedCallback(ev =>
                {
                    Undo.RecordObject(transform, "Rotation change on " + transform.gameObject.name);

                    _isRotateUpdatedByLocalField = true;

                    _serializedEulerHint.vector3Value = ev.newValue; //This doesn't change the rotation
                    _soTarget
                        .ApplyModifiedProperties(); //Can't update rotation if this is called after setting transform.localRotation or before setting serializedEulerHint

                    transform.localRotation = Quaternion.Euler(ev.newValue);

                    UpdateRotation_prefabOverrideIndicator();

                    if (_betterTransformSettings.roundRotationField)
                        _localRotationField.SetValueWithoutNotify(RoundedVector3(ev.newValue));
                });
            }).ExecuteLater(100);

            _worldRotationField = _rotationGroupBox.Q<Vector3Field>("WorldRotation");
            if (targets.Length > 1)
            {
                _worldRotationField.SetEnabled(false);
                _worldRotationField.tooltip = WorldRotationReadOnlyTooltip;
            }

            _worldRotationField.schedule.Execute(() =>
            {
                _worldRotationField.RegisterValueChangedCallback(ev =>
                {
                    _isRotationUpdatedByWorldField = true;

                    Undo.RecordObject(transform, "Rotation change on " + transform.gameObject.name);
                    transform.eulerAngles = ev.newValue;
                    //The fields are updated by the quaternionRotation
                });
            }).ExecuteLater(500);

            _quaternionRotationPropertyField = _root.Q<PropertyField>("QuaternionRotation");

            //This is the hidden rotation field that tracks the actual rotation.
            _rotationSerializedProperty = _soTarget.FindProperty(RotationProperty);
            _quaternionRotationPropertyField.TrackPropertyValue(_rotationSerializedProperty, RotationUpdated);
        }

        /// <summary>
        ///     This is only called once during setup.
        ///     This is called after a single frame update to overwrite the binding's value update and apply rounding if required
        /// </summary>
        void ScheduleUpdateRotationField()
        {
            if (_betterTransformSettings.roundRotationField)
                _localRotationField.SetValueWithoutNotify(RoundedVector3(_serializedEulerHint.vector3Value));
            else
                _localRotationField.SetValueWithoutNotify(TrimVectorNoise(_serializedEulerHint.vector3Value));
        }

        Vector3 _rotationVector3Cached;

        /// <summary>
        /// This is called only if the rotation is updated by code
        /// </summary>
        /// <param name="property"></param>
        void RotationUpdated(SerializedProperty property)
        {
            if (_temporarilyRotatedToCheckSize)
            {
                _temporarilyRotatedToCheckSize = false;
                //This is required because of the Undo function.
                if (_rotationVector3Cached == TrimVectorNoise(transform.localRotation.eulerAngles))
                    return;
            }

            //First update the target
            _soTarget.ApplyModifiedProperties();
            _soTarget.Update();

            _rotationVector3Cached = TrimVectorNoise(transform.localRotation.eulerAngles);

            //Then update fields
            if (!_isRotateUpdatedByLocalField)
            {
                if (_betterTransformSettings.roundRotationField)
                    _localRotationField.SetValueWithoutNotify(RoundedVector3(_rotationVector3Cached));
                else
                    _localRotationField.SetValueWithoutNotify(_rotationVector3Cached);

                _serializedEulerHint.vector3Value = _rotationVector3Cached;
            }
            else
            {
                _isRotateUpdatedByLocalField = false;
            }

            if (!_isRotationUpdatedByWorldField)
                UpdateWorldRotationField();
            //worldRotationField.SetValueWithoutNotify(TrimVectorNoise(transform.eulerAngles));
            else
                _isRotationUpdatedByWorldField = false;

            UpdateRotation_label();
            UpdateRotation_prefabOverrideIndicator();

            // UpdateSize();
            AutoUpdateSizeAfterValueChanged();

            _quaternionRotationPropertyField.schedule.Execute(UpdateAnimatorState_RotationFields)
                .ExecuteLater(100); //1000 ms = 1 s
        }

        void UpdateWorldRotationField()
        {
            if (_betterTransformSettings.roundRotationField)
                _worldRotationField.SetValueWithoutNotify(RoundedVector3(transform.eulerAngles));
            else
                _worldRotationField.SetValueWithoutNotify(TrimVectorNoise(transform.eulerAngles));

            if (targets.Length == 1)
                return;

            float commonX = transform.rotation.x;
            bool isCommonX = true;
            float commonY = transform.rotation.y;
            bool isCommonY = true;
            float commonZ = transform.rotation.z;
            bool isCommonZ = true;

            foreach (Transform t in targets.Cast<Transform>())
            {
                if (isCommonX)
                    if (!Approximately(t.rotation.x, commonX))
                        isCommonX = false;

                if (isCommonY)
                    if (!Approximately(t.rotation.y, commonY))
                        isCommonY = false;

                if (isCommonZ)
                    if (!Approximately(t.rotation.z, commonZ))
                        isCommonZ = false;

                if (!isCommonX && !isCommonY && !isCommonZ)
                    break;
            }

            FloatField xField = _worldRotationField.Q<FloatField>("unity-x-input");
            if (!isCommonX)
                xField.showMixedValue = true;
            else
                xField.showMixedValue = false;

            FloatField yField = _worldRotationField.Q<FloatField>("unity-y-input");
            if (!isCommonY)
                yField.showMixedValue = true;
            else
                yField.showMixedValue = false;

            FloatField zField = _worldRotationField.Q<FloatField>("unity-z-input");
            if (!isCommonZ)
                zField.showMixedValue = true;
            else
                zField.showMixedValue = false;
        }

        void UpdateRotation_fields()
        {
            UpdateWorldRotationField();
            //worldRotationField.SetValueWithoutNotify(transform.eulerAngles);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (_betterTransformSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    _localRotationField.style.display = DisplayStyle.Flex;
                    _worldRotationField.style.display = DisplayStyle.None;
                    break;

                // ReSharper disable once DuplicatedSwitchSectionBodies
                case BetterTransformSettings.WorkSpace.World:
                    _worldRotationField.style.display = DisplayStyle.Flex;
                    _localRotationField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    _worldRotationField.style.display = DisplayStyle.Flex;
                    _localRotationField.style.display = DisplayStyle.None;
                    break;
            }
        }


        void UpdateRotation_prefabOverrideIndicator()
        {
            if (!HasPrefabOverride_rotation())
            {
                _rotationPrefabOverrideMark.style.display = DisplayStyle.None;
                _rotationDefaultPrefabOverrideMark.style.display = DisplayStyle.None;

                _rotationLabel.RemoveFromClassList(PrefabOverrideLabelUSSClass);
                _localRotationField.RemoveFromClassList(PrefabOverrideLabelUSSClass);
                _worldRotationField.RemoveFromClassList(PrefabOverrideLabelUSSClass);
            }
            else
            {
                if (!HasPrefabOverride_position(true) && PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                {
                    _rotationDefaultPrefabOverrideMark.style.display = DisplayStyle.Flex;
                    _rotationPrefabOverrideMark.style.display = DisplayStyle.None;
                }
                else
                {
                    _rotationDefaultPrefabOverrideMark.style.display = DisplayStyle.None;
                    _rotationPrefabOverrideMark.style.display = DisplayStyle.Flex;
                }

                _rotationLabel.AddToClassList(PrefabOverrideLabelUSSClass);
                _localRotationField.AddToClassList(PrefabOverrideLabelUSSClass);
                _worldRotationField.AddToClassList(PrefabOverrideLabelUSSClass);
            }
        }

        bool HasPrefabOverride_rotation(bool checkDefaultOverride = false)
        {
            if (!_soTarget.FindProperty(RotationProperty).prefabOverride) return false;
            if (!checkDefaultOverride) return true;
            return !_soTarget.FindProperty(RotationProperty).isDefaultOverride;
        }

        #endregion Rotation

        #region Scale

        void SetupScale()
        {
            _scaleGroupBox = _customEditorGroupBox.Q<GroupBox>("Scale");
            _scaleLabelGroupbox = _scaleGroupBox.Q<GroupBox>("ScaleLabelGroupbox");
            _scaleLabel = _scaleGroupBox.Q<Label>("ScaleLabel");

            SetupScale_fields();

            _scalePrefabOverrideMark = _scaleGroupBox.Q<VisualElement>("PrefabOverrideMark");

            _scaleAspectRatioLocked = _scaleGroupBox.Q<Button>("AspectRatioLocked");
            _scaleAspectRatioLocked.clicked += () =>
            {
                _betterTransformSettings.LockSizeAspectRatio = false;
                //m_ConstrainProportionsScaleProperty.boolValue = false;
                UpdateScaleAspectRationButton();
                UpdateSize_AspectRationButton();
            };
            _scaleAspectRatioUnlocked = _scaleGroupBox.Q<Button>("AspectRatioUnlocked");
            _scaleAspectRatioUnlocked.clicked += () =>
            {
                _betterTransformSettings.LockSizeAspectRatio = true;
                //m_ConstrainProportionsScaleProperty.boolValue = true;
                UpdateScaleAspectRationButton();
                UpdateSize_AspectRationButton();
            };

            UpdateScaleAspectRationButton();
        }

        void SetupScale_fields()
        {
            _localScaleField = _scaleGroupBox.Q<Vector3Field>("LocalScale");
            _worldScaleField = _scaleGroupBox.Q<Vector3Field>("LossyScale");

            if (targets.Length > 1)
            {
                _worldScaleField.SetEnabled(false);
                _worldScaleField.tooltip = WorldScaleReadOnlyTooltip;
            }

            _localScaleField.schedule.Execute(() => { _localScaleField.RegisterValueChangedCallback(ev => { SetLocalScale(ev.newValue); }); }).ExecuteLater(100);

            _worldScaleField.schedule.Execute(() => { _worldScaleField.RegisterValueChangedCallback(ev => { SetWorldScale(transform, ev.newValue); }); }).ExecuteLater(100);

            _boundLocalScaleField = _scaleGroupBox.Q<Vector3Field>("BoundLocalScale");
            //This makes sure the binding operation is done before the callback is registered to avoid it calling the change
            _boundLocalScaleField.schedule.Execute(() =>
            {
                _boundLocalScaleField.RegisterValueChangedCallback(ev =>
                {
                    if (ev.newValue == ev.previousValue) return;

                    //A true "fromBinding" value means the change came from a script
                    bool fromBinding = ev.target == _boundLocalScaleField && ev.currentTarget == _boundLocalScaleField;

                    if (!Application.isPlaying)
                        Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);

                    if (_betterTransformSettings.roundScaleField)
                        _localScaleField.SetValueWithoutNotify(RoundedVector3(transform.localScale));
                    else
                        _localScaleField.SetValueWithoutNotify(TrimVectorNoise(transform.localScale));

                    SetWorldScaleField();

                    if (!fromBinding) _soTarget.Update(); //If changed by UI field

                    UpdateScale_prefabOverrideIndicator();
                    UpdateScale_label();
                    EditorUtility.SetDirty(transform);

                    if (!_scaleBeingUpdatedBySize)
                    {
                        // UpdateSize();
                        AutoUpdateSizeAfterValueChanged();
                    }

                    _scaleBeingUpdatedBySize = false;

                    _boundLocalScaleField.schedule.Execute(UpdateAnimatorState_ScaleFields)
                        .ExecuteLater(100); //1000 ms = 1 s
                });
            }).ExecuteLater(500);

            _scaleBeingUpdatedBySize = false;
        }

        void SetWorldScaleField()
        {
            if (_betterTransformSettings.roundScaleField)
                _worldScaleField.SetValueWithoutNotify(RoundedVector3(transform.lossyScale));
            else
                _worldScaleField.SetValueWithoutNotify(TrimVectorNoise(transform.lossyScale));

            if (targets.Length == 1)
                return;

            float commonX = transform.lossyScale.x;
            bool isCommonX = true;
            float commonY = transform.lossyScale.y;
            bool isCommonY = true;
            float commonZ = transform.lossyScale.z;
            bool isCommonZ = true;

            foreach (Transform t in targets.Cast<Transform>())
            {
                if (isCommonX)
                    if (!Approximately(t.lossyScale.x, commonX))
                        isCommonX = false;

                if (isCommonY)
                    if (!Approximately(t.lossyScale.y, commonY))
                        isCommonY = false;

                if (isCommonZ)
                    if (!Approximately(t.lossyScale.z, commonZ))
                        isCommonZ = false;

                if (!isCommonX && !isCommonY && !isCommonZ)
                    break;
            }

            FloatField xField = _worldScaleField.Q<FloatField>("unity-x-input");
            if (!isCommonX)
                xField.showMixedValue = true;
            else
                xField.showMixedValue = false;

            FloatField yField = _worldScaleField.Q<FloatField>("unity-y-input");
            if (!isCommonY)
                yField.showMixedValue = true;
            else
                yField.showMixedValue = false;

            FloatField zField = _worldScaleField.Q<FloatField>("unity-z-input");
            if (!isCommonZ)
                zField.showMixedValue = true;
            else
                zField.showMixedValue = false;
        }


        const string LockedAspectRatioDisabledFieldTooltip =
            "Can't change field value from zero when aspect ratio is locked. Please unlock and change it.";

        void UpdateScaleAspectRationButton()
        {
            FloatField scaleFieldXLocal = _localScaleField.Q<FloatField>("unity-x-input");
            FloatField scaleFieldXWorld = _worldScaleField.Q<FloatField>("unity-x-input");

            FloatField scaleFieldYLocal = _localScaleField.Q<FloatField>("unity-y-input");
            FloatField scaleFieldYWorld = _worldScaleField.Q<FloatField>("unity-y-input");

            FloatField scaleFieldZLocal = _localScaleField.Q<FloatField>("unity-z-input");
            FloatField scaleFieldZWorld = _worldScaleField.Q<FloatField>("unity-z-input");

            if (_betterTransformSettings.LockSizeAspectRatio)
            {
                _scaleAspectRatioLocked.style.display = DisplayStyle.Flex;
                _scaleAspectRatioUnlocked.style.display = DisplayStyle.None;

                if (targets.Length != 1) return;
                Vector3 localScale = transform.localScale;
                if (localScale.x == 0)
                {
                    scaleFieldXLocal.SetEnabled(false);
                    scaleFieldXLocal.tooltip = LockedAspectRatioDisabledFieldTooltip;
                    scaleFieldXWorld.SetEnabled(false);
                    scaleFieldXWorld.tooltip = LockedAspectRatioDisabledFieldTooltip;
                }

                if (localScale.y == 0)
                {
                    scaleFieldYLocal.SetEnabled(false);
                    scaleFieldYLocal.tooltip = LockedAspectRatioDisabledFieldTooltip;
                    scaleFieldYWorld.SetEnabled(false);
                    scaleFieldYWorld.tooltip = LockedAspectRatioDisabledFieldTooltip;
                }

                if (localScale.z != 0) return;
                scaleFieldZLocal.SetEnabled(false);
                scaleFieldZLocal.tooltip = LockedAspectRatioDisabledFieldTooltip;
                scaleFieldZWorld.SetEnabled(false);
                scaleFieldZWorld.tooltip = LockedAspectRatioDisabledFieldTooltip;
            }
            else
            {
                _scaleAspectRatioLocked.style.display = DisplayStyle.None;
                _scaleAspectRatioUnlocked.style.display = DisplayStyle.Flex;

                scaleFieldXLocal.SetEnabled(true);
                scaleFieldXLocal.tooltip = string.Empty;

                scaleFieldXWorld.SetEnabled(true);
                scaleFieldXWorld.tooltip = string.Empty;

                scaleFieldYLocal.SetEnabled(true);
                scaleFieldYLocal.tooltip = string.Empty;

                scaleFieldYWorld.SetEnabled(true);
                scaleFieldYWorld.tooltip = string.Empty;

                scaleFieldZLocal.SetEnabled(true);
                scaleFieldZLocal.tooltip = string.Empty;

                scaleFieldZWorld.SetEnabled(true);
                scaleFieldZWorld.tooltip = string.Empty;
            }
        }

        void UpdateScale()
        {
            UpdateScale_label();
            UpdateScale_fields();
            UpdateScale_prefabOverrideIndicator();
        }

        void UpdateScale_label()
        {
            if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
            {
                _scaleLabel.tooltip = "The local scaling of this GameObject relative to the parent.";
            }
            else
            {
                _scaleLabel.tooltip = "The world scaling of this GameObject.";
                if (targets.Length > 1)
                {
                    _scaleLabel.tooltip += "\n" + WorldScaleReadOnlyTooltip;
                    _scaleLabel.SetEnabled(false);
                }
            }

            UpdateScaleLabelContextMenu();
        }

        void UpdateScale_fields()
        {
            if (_betterTransformSettings.roundScaleField)
                _localScaleField.SetValueWithoutNotify(RoundedVector3(transform.localScale));
            else
                _localScaleField.SetValueWithoutNotify(TrimVectorNoise(transform.localScale));

            SetWorldScaleField();

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (_betterTransformSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    _localScaleField.style.display = DisplayStyle.Flex;
                    _worldScaleField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    _localScaleField.style.display = DisplayStyle.None;
                    _worldScaleField.style.display = DisplayStyle.Flex;
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    _localScaleField.style.display =
                        DisplayStyle.None; //Uses the default inspector's local field for this when showing both
                    _worldScaleField.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        /// <summary>
        ///     This is the right click menu on the label
        /// </summary>
        ContextualMenuManipulator _contextualMenuManipulatorForScaleLabel;

        void UpdateScaleLabelContextMenu()
        {
            if (_contextualMenuManipulatorForScaleLabel != null)
                _scaleLabel.RemoveManipulator(_contextualMenuManipulatorForScaleLabel);

            UpdateContextMenuForScale();

            _scaleLabel.AddManipulator(_contextualMenuManipulatorForScaleLabel);
            return;

            void UpdateContextMenuForScale()
            {
                _contextualMenuManipulatorForScaleLabel = new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Scale :", _ => { },
                        DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendSeparator();


                    evt.menu.AppendAction("Copy property path",
                        _ => { EditorGUIUtility.systemCopyBuffer = ScaleProperty; },
                        DropdownMenuAction.AlwaysEnabled);

                    if (_betterTransformSettings.roundScaleField)
                        evt.menu.AppendAction("Round out field values for the inspector",
                            _ => ToggleScaleFieldRounding(), DropdownMenuAction.Status.Checked);
                    else
                        evt.menu.AppendAction("Round out field values for the inspector",
                            // ReSharper disable once RedundantArgumentDefaultValue
                            _ => ToggleScaleFieldRounding(), DropdownMenuAction.Status.Normal);

                    if (HasPrefabOverride_scale())
                    {
                        evt.menu.AppendSeparator();
                        evt.menu.AppendAction(
                            "Apply to Prefab '" +
                            PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'",
                            _ => ApplyScaleChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                        evt.menu.AppendAction("Revert", _ => RevertScaleChangeToPrefab(),
                            DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy", _ => _quickActions.CopyScale());
                    if (_quickActions.HasVector3ValueToPaste())
                        evt.menu.AppendAction("Paste", _ => _quickActions.PasteScale());
                    else
                        evt.menu.AppendAction("Paste", _ => _quickActions.PasteScale(),
                            DropdownMenuAction.Status.Disabled);

                    evt.menu.AppendAction("Reset", _ => _quickActions.ResetScale());
                });
            }


            void ApplyScaleChangeToPrefab()
            {
                if (_soTarget.FindProperty(ScaleProperty).prefabOverride)
                    PrefabUtility.ApplyPropertyOverride(_soTarget.FindProperty(ScaleProperty),
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform), InteractionMode.UserAction);
            }

            void RevertScaleChangeToPrefab()
            {
                if (_soTarget.FindProperty(ScaleProperty).prefabOverride)
                    PrefabUtility.RevertPropertyOverride(_soTarget.FindProperty(ScaleProperty),
                        InteractionMode.UserAction);
            }
        }

        bool HasPrefabOverride_scale() => _soTarget.FindProperty(ScaleProperty).prefabOverride;


        void ToggleScaleFieldRounding()
        {
            _betterTransformSettings.roundScaleField = !_betterTransformSettings.roundScaleField;
            _betterTransformSettings.Save();

            if (_betterTransformSettings.roundScaleField)
            {
                _localScaleField.SetValueWithoutNotify(RoundedVector3(transform.localScale));
                _worldScaleField.SetValueWithoutNotify(RoundedVector3(transform.lossyScale));
            }
            else
            {
                _localScaleField.SetValueWithoutNotify(transform.localScale);
                _worldScaleField.SetValueWithoutNotify(transform.lossyScale);
            }

            UpdateScaleLabelContextMenu();

            _roundScaleFieldToggle?.SetValueWithoutNotify(_betterTransformSettings.roundScaleField);
        }

        bool _scaleBeingUpdatedBySize;

        void UpdateScale_prefabOverrideIndicator()
        {
            if (!HasPrefabOverride_scale())
            {
                _scalePrefabOverrideMark.style.display = DisplayStyle.None;
                _scaleLabel.RemoveFromClassList(PrefabOverrideLabelUSSClass);
                _localScaleField.RemoveFromClassList(PrefabOverrideLabelUSSClass);
                _worldScaleField.RemoveFromClassList(PrefabOverrideLabelUSSClass);
            }
            else
            {
                _scalePrefabOverrideMark.style.display = DisplayStyle.Flex;
                _scaleLabel.AddToClassList(PrefabOverrideLabelUSSClass);
                _localScaleField.AddToClassList(PrefabOverrideLabelUSSClass);
                _worldScaleField.AddToClassList(PrefabOverrideLabelUSSClass);
            }
        }

        void SetLocalScale(Vector3 newLocalScale)
        {
            Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);
            if (_betterTransformSettings.LockSizeAspectRatio)
                transform.localScale = AspectRatioAppliedLocalScale(transform.localScale, newLocalScale);
            else
                transform.localScale = newLocalScale;
        }

        public void SetWorldScale(Transform t, Vector3 newLossyScale, bool ignoreAspectRatioLock = false)
        {
            Undo.RecordObject(t, "Scale change on " + t.gameObject.name);
            if (!_betterTransformSettings.LockSizeAspectRatio || ignoreAspectRatioLock)
            {
                if (t.parent == null)
                {
                    t.localScale = newLossyScale;
                    return;
                }

                Vector3 originalLocalScale = t.localScale;
                Vector3 oldLossyScale = t.lossyScale;

                float newX = originalLocalScale.x;
                float newY = originalLocalScale.y;
                float newZ = originalLocalScale.z;

                if (!Approximately(newLossyScale.x, oldLossyScale.x))
                    newX = newLossyScale.x / t.parent.lossyScale.x;

                if (!Approximately(newLossyScale.y, oldLossyScale.y))
                    newY = newLossyScale.y / t.parent.lossyScale.y;

                if (!Approximately(newLossyScale.z, oldLossyScale.z))
                    newZ = newLossyScale.z / t.parent.lossyScale.z;

                Vector3 newScale = new Vector3(newX, newY, newZ);
                if (IsInfinity(newScale))
                    Debug.LogWarning(
                        "<color=yellow>Unable to set world scale</color> because the target world scale :" + newScale +
                        " contains infinity. The most common cause of this issue is any object in it's parent hierarchy has scale with 0 value in any axis.",
                        transform);
                else
                    t.localScale = new Vector3(newX, newY, newZ);
            }
            else
            {
                //Vector3 newLocalScale = Multiply(t.localScale, Divide(newLossyScale, t.lossyScale));
                t.localScale = AspectRatioAppliedWorldScale(newLossyScale);
            }
        }

        Vector3 _nonZeroValue = Vector3.one;

        Vector3 AspectRatioAppliedLocalScale(Vector3 currentLocalScale, Vector3 newLocalScale)
        {
            float multiplier;
            if (!Approximately(currentLocalScale.x, newLocalScale.x))
            {
                if (newLocalScale.x == 0)
                    _nonZeroValue = currentLocalScale;

                multiplier = newLocalScale.x / currentLocalScale.x;

                if (float.IsFinite(multiplier))
                    return new Vector3(newLocalScale.x, currentLocalScale.y * multiplier,
                        currentLocalScale.z * multiplier);
                multiplier = newLocalScale.x / _nonZeroValue.x;
                return new Vector3(newLocalScale.x, _nonZeroValue.y * multiplier, _nonZeroValue.z * multiplier);
            }

            if (!Approximately(currentLocalScale.y, newLocalScale.y))
            {
                if (newLocalScale.y == 0)
                    _nonZeroValue = currentLocalScale;

                multiplier = newLocalScale.y / currentLocalScale.y;
                if (float.IsFinite(multiplier))
                    return new Vector3(currentLocalScale.x * multiplier, newLocalScale.y,
                        currentLocalScale.z * multiplier);
                Debug.Log(newLocalScale);
                multiplier = newLocalScale.y / _nonZeroValue.y;
                return new Vector3(_nonZeroValue.x * multiplier, newLocalScale.y, _nonZeroValue.z * multiplier);
            }

            if (Approximately(currentLocalScale.z, newLocalScale.z)) return newLocalScale;
            if (newLocalScale.z == 0)
                _nonZeroValue = currentLocalScale;

            multiplier = newLocalScale.z / currentLocalScale.z;
            if (float.IsFinite(multiplier))
                return new Vector3(currentLocalScale.x * multiplier, currentLocalScale.y * multiplier,
                    newLocalScale.z);
            multiplier = newLocalScale.z / _nonZeroValue.z;
            return new Vector3(_nonZeroValue.x * multiplier, _nonZeroValue.y * multiplier, newLocalScale.z);
        }

        Vector3 AspectRatioAppliedWorldScale(Vector3 newLossyScale)
        {
            Vector3 currentLossyScale = transform.lossyScale;

            Vector3 currentLocalScale = transform.localScale;
            Vector3 newLocalScale = Multiply(transform.localScale, Divide(newLossyScale, transform.lossyScale));
            float multiplier;
            if (!Approximately(currentLossyScale.x, newLossyScale.x))
            {
                if (newLossyScale.x == 0)
                    _nonZeroValue = currentLossyScale;

                multiplier = newLossyScale.x / currentLossyScale.x;

                if (float.IsFinite(multiplier))
                    return new Vector3(newLocalScale.x, currentLocalScale.y * multiplier,
                        currentLocalScale.z * multiplier);

                multiplier = newLossyScale.x / _nonZeroValue.x;
                return new Vector3(newLossyScale.x, _nonZeroValue.y * multiplier, _nonZeroValue.z * multiplier);
            }

            if (!Approximately(currentLossyScale.y, newLossyScale.y))
            {
                if (newLossyScale.y == 0)
                    _nonZeroValue = currentLossyScale;

                multiplier = newLossyScale.y / currentLossyScale.y;

                if (float.IsFinite(multiplier))
                    return new Vector3(currentLocalScale.x * multiplier, newLocalScale.y,
                        currentLocalScale.z * multiplier);

                multiplier = newLossyScale.y / _nonZeroValue.y;
                return new Vector3(_nonZeroValue.x * multiplier, newLossyScale.y, _nonZeroValue.z * multiplier);
            }

            if (Approximately(currentLossyScale.z, newLossyScale.z)) return newLocalScale;
            if (newLossyScale.z == 0)
                _nonZeroValue = currentLossyScale;

            multiplier = newLossyScale.z / currentLossyScale.z;

            if (float.IsFinite(multiplier))
                return new Vector3(currentLocalScale.x * multiplier, currentLocalScale.y * multiplier,
                    newLocalScale.z);

            multiplier = newLossyScale.z / _nonZeroValue.z;
            return new Vector3(_nonZeroValue.x * multiplier, _nonZeroValue.y * multiplier, newLossyScale.z);
        }

        #endregion Scale

        #endregion Main Controls

        #region Size

        #region Variable

        GroupBox _sizeFoldout;

        /// <summary>
        /// Contains the label and lock, unlock aspect ratio buttons
        /// </summary>
        GroupBox _sizeLabelGroupBox;

        /// <summary>
        /// Contains buttons for refresh, unit selection and other options to control size 
        /// </summary>
        GroupBox _sizeToolbox;

        DropdownField _unitDropDownField;
        Vector3Field _sizeFoldoutField;
        GroupBox _sizeCenterFoldoutGroup; //This is inside the size foldout
        Vector3Field _sizeCenterFoldoutField;
        public Bounds currentBound;

        Button _hierarchySizeButton;
        Button _selfSizeButton;

        Button _sizeAspectRatioUnlocked;
        Button _sizeAspectRatioLocked;

        IntegerField _maxChildCountForSizeCalculation;

        /// <summary>
        /// The text is updated with workspace
        /// </summary>
        Button _autoRefreshSizeButton;

        Button _refreshSizeButton;

        Button _rendererSizeButton;
        Button _filterSizeButton;

        GroupBox _inlineSizeGroupBox;
        GroupBox _inlineSizeButtonsGroupBox;

        Button _manualUpdateButton;

        /// <summary>
        /// Contains the size copy, paste and rotation buttons
        /// </summary>
        GroupBox _sizeToolbar;

        Label _sizeFoldoutInformationLabel;

        Vector3 _targetVector3;

        bool _sizeSetupDone;

        #endregion Variable

        void SetupSizeCommon()
        {
            _sizeFoldout = _root.Q<GroupBox>("SizeFoldout");

            _sizeFoldoutField = _sizeFoldout.Q<Vector3Field>("SizeFoldoutField");
            _sizeCenterFoldoutGroup = _sizeFoldout.Q<GroupBox>("SizeCenterFoldoutGroup");
            _sizeCenterFoldoutField = _sizeCenterFoldoutGroup.Q<Vector3Field>("CenterFoldoutField");
            _sizeToolbox = _sizeFoldout.Q<GroupBox>("SizeToolbox");
        }

        void HideSize()
        {
            _sizeFoldout.style.display = DisplayStyle.None;

            _inlineSizeGroupBox ??= _root.Q<GroupBox>("InlineSizeGroupBox");
            _inlineSizeGroupBox.style.display = DisplayStyle.None;

            _inlineSizeButtonsGroupBox ??= _root.Q<GroupBox>("InlineSizeButtonsGroupBox");
            _inlineSizeButtonsGroupBox.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Doesn't do anything if size has already been set up
        /// </summary>
        void SetupSize()
        {
            if (_sizeSetupDone) return;
            _sizeSetupDone = true;

            CustomFoldout.SetupFoldout(_sizeFoldout);

            _sizeFoldoutInformationLabel = _root.Q<Label>("sizeInformationLabel");

            if (_betterTransformSettings.ShowSizeFoldout && targets.Length == 1)
                _sizeFoldout.style.display = DisplayStyle.Flex;
            else _sizeFoldout.style.display = DisplayStyle.None;

            _sizeFoldoutField.schedule.Execute(() => { _sizeFoldoutField.RegisterValueChangedCallback(_ => { _sizeFoldoutField.schedule.Execute(SetSizeAsync).ExecuteLater(500); }); }).ExecuteLater(100);

            _sizeCenterFoldoutGroup.SetEnabled(false);
            _sizeAspectRatioLocked = _sizeFoldout.Q<Button>("AspectRatioLocked");
            _sizeAspectRatioUnlocked = _sizeFoldout.Q<Button>("AspectRatioUnlocked");

            _sizeAspectRatioLocked.clicked += () =>
            {
                _betterTransformSettings.LockSizeAspectRatio = false;
                UpdateSize_AspectRationButton();
                UpdateScaleAspectRationButton();
            };

            _sizeAspectRatioUnlocked.clicked += () =>
            {
                _betterTransformSettings.LockSizeAspectRatio = true;
                UpdateSize_AspectRationButton();
                UpdateScaleAspectRationButton();
            };
            UpdateSize_AspectRationButton();

            SetupSizeToolbar();

            SetupSizeToolbox();

            _inlineSizeGroupBox = _root.Q<GroupBox>("InlineSizeGroupBox");
            _inlineSizeButtonsGroupBox = _root.Q<GroupBox>("InlineSizeButtonsGroupBox");
            UpdateSizeViewType();
        }

        void AutoUpdateSizeAfterValueChanged()
        {
            if (originalTransform) //This is a subinspector
                return;
            if (Application.isPlaying)
            {
                if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                {
                    if (!_betterTransformSettings.autoRefreshSizeInLocalSpaceInPlaymode)
                        return;
                    UpdateSize();
                }
            }

            if (_betterTransformSettings.autoRefreshSize) UpdateSize();
        }

        void SetupSizeToolbox()
        {
            _refreshSizeButton = _sizeToolbox.Q<Button>("RefreshSizeButton");
            _autoRefreshSizeButton = _sizeToolbox.Q<Button>("AutoRefreshSizeButton");
            UpdateAutoRefreshButton();

            _hierarchySizeButton = _sizeToolbox.Q<Button>("HierarchySize");
            _selfSizeButton = _sizeToolbox.Q<Button>("SelfSize");

            _rendererSizeButton = _sizeToolbox.Q<Button>("RendererSizeButton");
            _filterSizeButton = _sizeToolbox.Q<Button>("FilterSizeButton");

            _sizeToolbox.schedule.Execute(() =>
            {
                _autoRefreshSizeButton.clicked += ToggleAutoRefreshSize;
                _refreshSizeButton.clicked += () => { UpdateSize(); };

                _hierarchySizeButton.clicked += () =>
                {
                    _betterTransformSettings.IncludeChildBounds = false;
                    UpdateSizeInclusionButtons();

                    SceneView.RepaintAll();
                };
                _selfSizeButton.clicked += () =>
                {
                    _betterTransformSettings.IncludeChildBounds = true;
                    UpdateSizeInclusionButtons();

                    SceneView.RepaintAll();
                };
                _rendererSizeButton.clicked += () =>
                {
                    _betterTransformSettings.CurrentSizeType = BetterTransformSettings.SizeType.Filter;
                    UpdateSizeTypeButtons();
                    UpdateSize();
                    SceneView.RepaintAll();
                };
                _filterSizeButton.clicked += () =>
                {
                    _betterTransformSettings.CurrentSizeType = BetterTransformSettings.SizeType.Renderer;
                    UpdateSizeTypeButtons();
                    UpdateSize();
                    SceneView.RepaintAll();
                };
            }).ExecuteLater(1000);


            if (_betterTransformSettings.IncludeChildBounds)
            {
                _hierarchySizeButton.style.display = DisplayStyle.Flex;
                _selfSizeButton.style.display = DisplayStyle.None;
            }
            else
            {
                _hierarchySizeButton.style.display = DisplayStyle.None;
                _selfSizeButton.style.display = DisplayStyle.Flex;
            }

            if (_betterTransformSettings.ShowSizeCenter)
                _sizeCenterFoldoutGroup.style.display = DisplayStyle.Flex;
            else
                _sizeCenterFoldoutGroup.style.display = DisplayStyle.None;


            UpdateSizeTypeButtons();

            SetupUnitDropDownField();
        }

        void UpdateAutoRefreshButton()
        {
            if (originalTransform != null) //This is the original transform
            {
                UpdateAutoRefreshButtonVisual(false);
                _autoRefreshSizeButton.tooltip = "Can't auto refresh size on sub inspectors";
                _autoRefreshSizeButton.SetEnabled(false);
                return;
            }

            if (Application.isPlaying)
            {
                if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                {
                    UpdateAutoRefreshButtonVisual(_betterTransformSettings.autoRefreshSizeInLocalSpaceInPlaymode);
                    return;
                }
            }

            UpdateAutoRefreshButtonVisual(_betterTransformSettings.autoRefreshSize);
        }

        void UpdateAutoRefreshButtonVisual(bool state)
        {
            if (_autoRefreshSizeButton == null) return;
            if (state)
            {
                _autoRefreshSizeButton.text = "Auto Refresh";
                _autoRefreshSizeButton.style.opacity = 1;
            }
            else
            {
                _autoRefreshSizeButton.text = "A̶u̶t̶o̶ ̶R̶e̶f̶r̶e̶s̶h̶";
                _autoRefreshSizeButton.style.opacity = 0.35f;
            }
        }

        void ToggleAutoRefreshSize()
        {
            if (Application.isPlaying)
            {
                if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                {
                    _betterTransformSettings.autoRefreshSizeInLocalSpaceInPlaymode =
                        !_betterTransformSettings.autoRefreshSizeInLocalSpaceInPlaymode;
                    // UpdateAutoRefreshButton();
                    UpdateMainControls(); //This calls the UpdateAutoRefreshButton(); as well.
                    return;
                }
            }

            _betterTransformSettings.autoRefreshSize = !_betterTransformSettings.autoRefreshSize;
            UpdateAutoRefreshButton();
        }

        void SetupSizeToolbar()
        {
            //Added these two because a user reported null reference error. Couldn't replicate the source of the bug
            _sizeFoldout ??= _root.Q<GroupBox>("SizeFoldout");
            _sizeToolbar ??= _sizeFoldout.Q<GroupBox>("SizeToolbar");

            if (_sizeToolbar == null) return;

            _sizeToolbar.Q<Button>("Paste").clicked += () =>
            {
                QuickActions.GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                if (!exists)
                    return;

                Undo.RecordObject(transform, "Size Paste on " + transform.gameObject.name);
                SetSize(new Vector3(x, y, z));
                EditorUtility.SetDirty(transform);

                float unitMultiplier = ScalesManager.instance.CurrentUnitValue();
                _sizeFoldoutField.SetValueWithoutNotify(TrimVectorNoise(currentBound.size * unitMultiplier));
                _sizeCenterFoldoutField.SetValueWithoutNotify(TrimVectorNoise(currentBound.center * unitMultiplier));
            };

            _sizeToolbar.Q<Button>("Reset").clicked += () =>
            {
                Undo.RecordObject(transform, "Size Reset on " + transform.gameObject.name);
                SetSize(Vector3.one);
                EditorUtility.SetDirty(transform);

                float unitMultiplier = ScalesManager.instance.CurrentUnitValue();
                _sizeFoldoutField.SetValueWithoutNotify(TrimVectorNoise(Vector3.one * unitMultiplier));
                _sizeCenterFoldoutField.SetValueWithoutNotify(TrimVectorNoise(currentBound.center * unitMultiplier));
            };
        }

        void SetupUnitDropDownField()
        {
            _unitDropDownField = _sizeFoldout.Q<DropdownField>("UnitsDropDownField");
            if (ScalesManager.instance.GetAvailableUnits().ToList().Count == 0) ScalesManager.instance.Reset();
            _unitDropDownField.choices = ScalesManager.instance.GetAvailableUnits().ToList();
            _unitDropDownField.index = ScalesManager.instance.SelectedUnit;
            _unitDropDownField.schedule.Execute(() =>
            {
                _unitDropDownField.RegisterValueChangedCallback(_ =>
                {
                    ScalesManager myScales = ScalesManager.instance;
                    myScales.SelectedUnit = _unitDropDownField.index;
                    EditorUtility.SetDirty(myScales);

                    UpdateSize();
                });
            }).ExecuteLater(100);
        }

        bool _manuallyUpdatedSize;

        void CreateTooManyChildForAutoSizeCalculationWarning()
        {
            _manualUpdateButton = new Button
            {
                text = "Check Size",
                tooltip =
                    "Too many child objects for automatic size calculation. You can change the amount from setting."
            };
            _manualUpdateButton.clicked += () =>
            {
                _manuallyUpdatedSize = true;
                UpdateSize();
            };

            VisualElement rootHolder = _root.Q<VisualElement>("RootHolder");
            int index = rootHolder.IndexOf(_sizeFoldout);
            rootHolder.Insert(index, _manualUpdateButton);
            //root.Add(manualUpdateButton);
        }

        void UpdateSizeTypeButtons()
        {
            Button rendererSizeButton = _root.Q<Button>("RendererSizeButton");
            Button filterSizeButton = _root.Q<Button>("FilterSizeButton");

            if (_betterTransformSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter)
            {
                rendererSizeButton.style.display = DisplayStyle.None;
                filterSizeButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                rendererSizeButton.style.display = DisplayStyle.Flex;
                filterSizeButton.style.display = DisplayStyle.None;
            }
        }


        void UpdateSizeFoldout()
        {
            if (_betterTransformSettings.ShowSizeFoldout && targets.Length == 1)
                _sizeFoldout.style.display = DisplayStyle.Flex;
            else
                _sizeFoldout.style.display = DisplayStyle.None;

            if (_betterTransformSettings.ShowSizeFoldout || _betterTransformSettings.ShowSizeInLine)
            {
                if (ScalesManager.instance.GetAvailableUnits().ToList().Count == 0) ScalesManager.instance.Reset();
                _unitDropDownField.choices = ScalesManager.instance.GetAvailableUnits().ToList();

                _unitDropDownField.index = ScalesManager.instance.SelectedUnit;

                UpdateSizeInclusionButtons();
            }

            if (_betterTransformSettings.ShowSizeCenter)
                _sizeCenterFoldoutGroup.style.display = DisplayStyle.Flex;
            else
                _sizeCenterFoldoutGroup.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Toggles between inline and foldout size view
        /// </summary>
        void UpdateSizeViewType()
        {
            if (_betterTransformSettings.ShowSizeInLine)
            {
                _inlineSizeGroupBox.Add(_sizeFoldoutField.parent.parent);
                _inlineSizeButtonsGroupBox.Add(_sizeToolbox);
            }
            else //show size in foldout
            {
                GroupBox content = _sizeFoldout.Q<GroupBox>("Content");
                content.Insert(0, _sizeFoldoutField.parent.parent);
                GroupBox header = _sizeFoldout.Q<GroupBox>("Header");
                header.Add(_sizeToolbox);
            }
        }

        void UpdateSize_AspectRationButton()
        {
            if (_betterTransformSettings.LockSizeAspectRatio)
            {
                _sizeAspectRatioLocked.style.display = DisplayStyle.Flex;
                _sizeAspectRatioUnlocked.style.display = DisplayStyle.None;
            }
            else
            {
                _sizeAspectRatioLocked.style.display = DisplayStyle.None;
                _sizeAspectRatioUnlocked.style.display = DisplayStyle.Flex;
            }
        }

        void UpdateSizeInclusionButtons()
        {
            if (_betterTransformSettings.IncludeChildBounds)
            {
                _hierarchySizeButton.style.display = DisplayStyle.Flex;
                _selfSizeButton.style.display = DisplayStyle.None;
            }
            else
            {
                _hierarchySizeButton.style.display = DisplayStyle.None;
                _selfSizeButton.style.display = DisplayStyle.Flex;
            }

            UpdateSize();
        }


        void UpdateSize(bool showWarningIfTooManyChild = false)
        {
            if (targets.Length != 1) return;

            //If not showing the size foldout, no need to update it
            if (!_betterTransformSettings.ShowSizeFoldout && !_betterTransformSettings.ShowSizeInLine) return;

            //On domain reload, the reference is lost.
            if (_sizeFoldout == null)
            {
                _sizeSetupDone = false;

                SetupSize();
                //Debug.Log("size foldout setup was required.");
            }

            currentBound = CheckSize(transform, showWarningIfTooManyChild);

            //Do not show size foldout if the object's size is zero
            if (currentBound.size == Vector3.zero)
            {
                _sizeFoldoutInformationLabel ??= _root.Q<Label>("sizeInformationLabel");
                if (_sizeFoldoutInformationLabel != null) //If it is still not found for some reason.
                {
                    if (_betterTransformSettings.showWhySizeIsHiddenLabel)
                    {
                        _sizeFoldoutInformationLabel.style.display = DisplayStyle.Flex;

                        if (!_betterTransformSettings.IncludeChildBounds)
                        {
                            if (transform.childCount > 0)
                            {
                                _sizeFoldoutInformationLabel.text =
                                    "Size is hidden because this object has no mesh with size and child object's size is ignored because self size is selected";
                            }
                            else
                            {
                                _sizeFoldoutInformationLabel.text = _betterTransformSettings.CurrentSizeType switch
                                {
                                    BetterTransformSettings.SizeType.Filter when transform.GetComponent<Renderer>() !=
                                                                                 null =>
                                        "Size is hidden because size type of filter is selected and this object has a renderer.",
                                    BetterTransformSettings.SizeType.Renderer when
                                        _betterTransformSettings.ignoreParticleAndVFXInSizeCalculation &&
                                        (transform.GetComponent<ParticleSystem>() != null ||
                                         transform.GetComponent<VisualEffect>()) =>
                                        "Size is hidden because the size of particle systems and visual effect are ignored in setting.",
                                    _ => "Size is hidden because this object has no mesh with size."
                                };
                            }
                        }
                        else
                        {
                            if (_manuallyUpdatedSize) //Pressed the check size button
                            {
                                _sizeFoldoutInformationLabel.text =
                                    "Size is hidden because this object and it's child objects have no mesh with size.";
                            }
                            else
                            {
                                if (transform.childCount > 0)
                                {
                                    _sizeFoldoutInformationLabel.text =
                                        "Size is hidden because this object and it's child objects have no mesh with size or size needs to be updated.";
                                }
                                else
                                {
                                    _sizeFoldoutInformationLabel.text = _betterTransformSettings.CurrentSizeType switch
                                    {
                                        BetterTransformSettings.SizeType.Filter when
                                            transform.GetComponent<Renderer>() != null =>
                                            "Size is hidden because size type of filter is selected and this object has a renderer.",
                                        BetterTransformSettings.SizeType.Renderer when _betterTransformSettings
                                                                                           .ignoreParticleAndVFXInSizeCalculation &&
                                                                                       (transform.GetComponent<ParticleSystem>() != null ||
                                                                                        transform.GetComponent<VisualEffect>()) =>
                                            "Size is hidden because the size of particle systems and visual effect are ignored in setting.",
                                        _ => "Size is hidden because this object has no mesh with size."
                                    };
                                }
                            }
                        }
                    }
                    else
                    {
                        _sizeFoldoutInformationLabel.style.display = DisplayStyle.None;
                    }
                }

                HideSize();

                return;
            }

            _sizeFoldoutInformationLabel ??= _root.Q<Label>("sizeInformationLabel");
            if (_sizeFoldoutInformationLabel != null) //Paranoid if statement. Its 5AM and I am still working
                _sizeFoldoutInformationLabel.style.display = DisplayStyle.None;

            if (_betterTransformSettings.ShowSizeFoldout && targets.Length == 1)
            {
                // ReSharper disable once PossibleNullReferenceException
                _sizeFoldout.style.display = DisplayStyle.Flex;
            }
            else if (_betterTransformSettings.ShowSizeInLine && targets.Length == 1)
            {
                if (_inlineSizeButtonsGroupBox == null)
                    _inlineSizeGroupBox = _root.Q<GroupBox>("InlineSizeGroupBox");
                _inlineSizeGroupBox.style.display = DisplayStyle.Flex;

                _inlineSizeButtonsGroupBox ??= _root.Q<GroupBox>("InlineSizeButtonsGroupBox");
                _inlineSizeButtonsGroupBox.style.display = DisplayStyle.Flex;
            }

            float unitMultiplier = ScalesManager.instance.CurrentUnitValue();

            if (_sizeFoldoutField == null)
                SetupSize();

            // ReSharper disable once PossibleNullReferenceException
            _sizeFoldoutField.SetValueWithoutNotify(TrimVectorNoise(currentBound.size * unitMultiplier));
            _sizeCenterFoldoutField.SetValueWithoutNotify(TrimVectorNoise(currentBound.center * unitMultiplier));
        }

        void SetSizeAsync()
        {
            _targetVector3 = _sizeFoldoutField.value;

            const float minimum = 0.1f;
            if (_targetVector3.x <= minimum) _targetVector3.x = minimum;
            if (_targetVector3.y <= minimum) _targetVector3.y = minimum;
            if (_targetVector3.z <= minimum) _targetVector3.z = minimum;

            SetSize(_targetVector3);
        }

        /// <summary>
        ///     Settings the size
        /// </summary>
        /// <param name="newSize"></param>
        void SetSize(Vector3 newSize)
        {
            float unitMultiplier = ScalesManager.instance.CurrentUnitValue();

            currentBound = CheckSize(transform);
            Vector3 originalSize = currentBound.size * unitMultiplier;

            if (newSize == originalSize)
                return;

            _scaleBeingUpdatedBySize = true;

            Vector3 newLocalScale = Multiply(transform.localScale, Divide(newSize, originalSize));

            if (_betterTransformSettings.LockSizeAspectRatio)
            {
                Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);
                transform.localScale = AspectRatioAppliedLocalScale(transform.localScale, newLocalScale);
                EditorUtility.SetDirty(transform);

                currentBound = CheckSize(transform);

                //If the size field is not updated to avoid losing focus,
                //the old size x that wasn't changed impacts the value you are trying to put in Y
                _sizeFoldoutField.SetValueWithoutNotify(currentBound.size);
            }
            else
            {
                Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);
                transform.localScale = newLocalScale;
                EditorUtility.SetDirty(transform);

                currentBound.size = newSize;
            }

            _sizeCenterFoldoutField.schedule.Execute(JustUpdateCenter).ExecuteLater(200);

            //Update the gizmo
            SceneView.RepaintAll();
        }

        //void SizeFoldoutUnfocusedAfterSettingLockedAspectRatioSize()
        //{
        //    currentBound = CheckSize(transform);
        //    sizeFoldoutField.SetValueWithoutNotify(currentBound.size);
        //}

        void JustUpdateCenter()
        {
            currentBound.center = CheckSize(transform).center;
            _sizeCenterFoldoutField.SetValueWithoutNotify(currentBound.center);
        }

        #region Check Size

        bool _temporarilyRotatedToCheckSize;

        /// <summary>
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="showWarningIfTooManyChild">
        ///     When true, the update is skipped if the number of child objects is too high;
        ///     otherwise, the update always runs.
        /// </param>
        /// <returns></returns>
        Bounds CheckSize(Transform targetTransform, bool showWarningIfTooManyChild = false)
        {
#if UNITY_2021_1_OR_NEWER
            if (_betterTransformSettings.IncludeChildBounds)
                return GetSizeWithChildren(targetTransform, showWarningIfTooManyChild);

            if (_manualUpdateButton != null)
                _manualUpdateButton.style.display = DisplayStyle.None;

            return _sizeCalculator?.GetSelfBounds(targetTransform) ?? new Bounds();
#else
           if (editorSettings.IncludeChildBounds)
                return GetSizeWithChildren(targetTransform);
            else
                return GetRendererSelfBoundsForOlderUnityVersion(targetTransform);
            return new Bounds();
#endif
        }

#if !UNITY_2021_1_OR_NEWER
        Bounds GetRendererSelfBoundsForOlderUnityVersion(Transform target)
        {
            if (target.GetComponent<Renderer>() == null)
                return new Bounds(Vector3.zero, Vector3.zero);

            Quaternion currentRotation;
            if (target.parent == null)
            {
                currentRotation = target.localRotation;
                target.localRotation = Quaternion.Euler(Vector3.zero);
            }
            else
            {
                currentRotation = target.rotation;
                target.rotation = Quaternion.Euler(Vector3.zero);
            }

            Bounds bounds = target.GetComponent<Renderer>().bounds;
            bounds.center -= target.position;

            if (target.parent == null)
                target.localRotation = currentRotation;
            else
                target.rotation = currentRotation;

            return bounds;
        }
#endif

        /// <summary>
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="showWarningIfTooManyChild">
        ///     When true, the update is skipped if the number of child objects is too high;
        ///     otherwise, the update always runs.
        /// </param>
        /// <returns></returns>
        Bounds GetSizeWithChildren(Transform targetTransform, bool showWarningIfTooManyChild = false)
        {
            if (_betterTransformSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer)
                return _betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World
                    ? GetRendererWorldBounds(targetTransform, showWarningIfTooManyChild)
                    : GetRendererLocalBounds(targetTransform, showWarningIfTooManyChild);

            return CheckFilterBoundsWithChildren(targetTransform, showWarningIfTooManyChild);
        }

        /// <summary>
        ///     The difference between local and world space size is, the world space size checks axis in world space and local
        ///     takes targetTransform rotation into consideration
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="showWarningIfTooManyChild">
        ///     When true, the update is skipped if the number of child objects is too high;
        ///     otherwise, the update always runs.
        /// </param>
        /// <returns></returns>
        Bounds GetRendererWorldBounds(Transform targetTransform, bool showWarningIfTooManyChild = false)
        {
            if (_manualUpdateButton != null)
                _manualUpdateButton.style.display = DisplayStyle.None;

            Bounds newBound = new Bounds();

            Transform[] transforms = targetTransform.GetComponentsInChildren<Transform>()
                .Where(t =>
                    !_betterTransformSettings.ignoreParticleAndVFXInSizeCalculation ||
                    (t.GetComponent<ParticleSystem>() == null && t.GetComponent<VisualEffect>() == null))
                .ToArray();

            if (transforms.Length == 0)
                return newBound;

            if (showWarningIfTooManyChild &&
                transforms.Length > _betterTransformSettings.MaxChildCountForSizeCalculation)
            {
                if (_manualUpdateButton != null)
                    _manualUpdateButton.style.display = DisplayStyle.Flex;
                else
                    CreateTooManyChildForAutoSizeCalculationWarning();

                return newBound;
            }

            if (_manualUpdateButton != null)
                _manualUpdateButton.style.display = DisplayStyle.None;

            bool firstRenderer = true;
            for (int i = 0; i < transforms.Length; ++i)
            {
                if (transforms[i].GetComponent<Renderer>() == null)
                    continue;

                if (firstRenderer)
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= transforms[i].position;
                    if (transforms[i] != transform)
                        bounds.center += transforms[i].localPosition;

                    //There's no such thing as an "empty" Bounds,
                    //you must create it with the "first" one. That's why the declared bound is replaced,
                    //otherwise, bound will always count the declared (0,0,0) position as a valid one;
                    newBound = bounds;
                    firstRenderer = false;
                }
                else
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= targetTransform.position;
                    newBound.Encapsulate(bounds);
                }
            }

            return newBound;
        }

        /// <summary>
        ///     The difference between local and world space size is, the world space size checks axis in world space and local
        ///     takes targetTransform rotation into consideration
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="showWarningIfTooManyChild">
        ///     When true, the update is skipped if the number of child objects is too high;
        ///     otherwise, the update always runs.
        /// </param>
        /// <returns></returns>
        Bounds GetRendererLocalBounds(Transform targetTransform, bool showWarningIfTooManyChild = false)
        {
            Bounds newBound = new Bounds();

            Transform[] transforms = targetTransform.GetComponentsInChildren<Transform>()
                .Where(t =>
                    !_betterTransformSettings.ignoreParticleAndVFXInSizeCalculation ||
                    (t.GetComponent<ParticleSystem>() == null && t.GetComponent<VisualEffect>() == null))
                .ToArray();

            if (transforms.Length == 0)
                return newBound;

            if (showWarningIfTooManyChild &&
                transforms.Length > _betterTransformSettings.MaxChildCountForSizeCalculation)
            {
                if (_manualUpdateButton != null)
                    _manualUpdateButton.style.display = DisplayStyle.Flex;
                else
                    CreateTooManyChildForAutoSizeCalculationWarning();

                return newBound;
            }

            if (_manualUpdateButton != null)
                _manualUpdateButton.style.display = DisplayStyle.None;

            _temporarilyRotatedToCheckSize = true;

            Quaternion currentRotation;
            if (targetTransform.parent == null)
            {
                currentRotation = targetTransform.localRotation;
                targetTransform.localRotation = Quaternion.Euler(Vector3.zero);
            }
            else
            {
                currentRotation = targetTransform.rotation;
                targetTransform.rotation = Quaternion.Euler(Vector3.zero);
            }

            bool firstRenderer = true;
            for (int i = 0; i < transforms.Length; ++i)
            {
                if (transforms[i].GetComponent<Renderer>() == null)
                    continue;

                if (firstRenderer)
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= transforms[i].position;
                    if (transforms[i] != transform)
                        bounds.center += transforms[i].localPosition;

                    //There's no such thing as an "empty" Bounds,
                    //you must create it with the "first" one. That's why the declared bound is replaced,
                    //otherwise, bound will always count the declared (0,0,0) position as a valid one;
                    newBound = bounds;
                    firstRenderer = false;
                }
                else
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= targetTransform.position;
                    newBound.Encapsulate(bounds);
                }
            }

            if (targetTransform.parent == null)
                targetTransform.localRotation = currentRotation;
            else
                targetTransform.rotation = currentRotation;

            return newBound;
        }

        /// <summary>
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="showWarningIfTooManyChild"></param>
        /// <returns></returns>
        Bounds CheckFilterBoundsWithChildren(Transform targetTransform, bool showWarningIfTooManyChild = false)
        {
            Transform[] transforms = targetTransform.GetComponentsInChildren<Transform>();
            if (showWarningIfTooManyChild)
                if (transforms.Length > _betterTransformSettings.MaxChildCountForSizeCalculation)
                {
                    if (_manualUpdateButton != null)
                        _manualUpdateButton.style.display = DisplayStyle.Flex;
                    else
                        CreateTooManyChildForAutoSizeCalculationWarning();
                    return new Bounds();
                }

            //This button is never created unless required.
            if (_manualUpdateButton != null)
                _manualUpdateButton.style.display = DisplayStyle.None;

            MeshFilter[] meshFilters = targetTransform.GetComponentsInChildren<MeshFilter>();
            Matrix4x4 worldToTargetLocal = targetTransform.worldToLocalMatrix;

            Bounds bounds = new Bounds();
            bool firstBounds = true;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;

                if (mesh == null || mesh.vertexCount == 0) continue;

                Matrix4x4 meshToWorld = meshFilter.transform.localToWorldMatrix;

                Vector3[] vertices = mesh.vertices;
                foreach (Vector3 vertex in vertices)
                {
                    // Convert vertex to world space, applying position, rotation, and scale
                    Vector3 worldVertex = meshToWorld.MultiplyPoint3x4(vertex);

                    // Convert world space vertex back to targetTransform's local space
                    Vector3 localVertex = worldToTargetLocal.MultiplyPoint3x4(worldVertex);

                    if (firstBounds)
                    {
                        bounds = new Bounds(localVertex, Vector3.zero); // Initialize bounds in local space
                        firstBounds = false;
                    }
                    else
                    {
                        bounds.Encapsulate(localVertex);
                    }
                }
            }

            bounds.size = Multiply(bounds.size, transform.lossyScale);
            bounds.center = Multiply(bounds.center, transform.lossyScale);
            return bounds;
        }

        #endregion Check Size

        #endregion Size

        #region Notes

        bool _thisIsAnAsset;

        void SetupGuid()
        {
            _thisIsAnAsset = AssetDatabase.Contains(transform);

            Label assetGuidLabel = _root.Q<Label>("GUID");
            assetGuidLabel.style.display = DisplayStyle.None;

            if (!_thisIsAnAsset || !_betterTransformSettings.showAssetGuid) return;
            string myGuid = NoteUtility.GetUserFriendlyID(transform);
            assetGuidLabel.text = myGuid;
            assetGuidLabel.tooltip = "GUID\n" + myGuid;
            assetGuidLabel.style.display = DisplayStyle.Flex;
        }

        #endregion Notes

        #region Parent Child

        GroupBox _parentGroupBox;
        GroupBox _childGroupBox;
        bool _alreadySetupChildren;

        void SetupParentChild()
        {
            _childGroupBox = _root.Q<GroupBox>("ChildGroupBox");
            if (!_betterTransformSettings.ShowParentChildTransform || targets.Length > 1)
            {
                _childGroupBox.style.display = DisplayStyle.None;
                return;
            }

            if (folderTemplate == null)
                folderTemplate = Utility.GetVisualTreeAsset(FolderTemplateFileLocation, FolderTemplateGuid);

            if (folderTemplate == null)
                return;

            GroupBox rootHolder = _root.Q<GroupBox>("RootHolder");

            SetupParent(rootHolder);
            SetupChildren(rootHolder);
        }

        void UpdateSetupParentChildFoldouts()
        {
            GroupBox rootHolder = _root.Q<GroupBox>("RootHolder");
            if (!_betterTransformSettings.ShowParentChildTransform)
            {
                _childGroupBox.style.display = DisplayStyle.None;

                if (_parentGroupBox != null)
                    _parentGroupBox.style.display = DisplayStyle.None;
            }
            else
            {
                if (transform.childCount > 0)
                {
                    _childGroupBox.style.display = DisplayStyle.Flex;
                    if (!_alreadySetupChildren) SetupChildren(rootHolder);
                }

                if (!transform.parent) return;
                if (_parentGroupBox != null)
                    _parentGroupBox.style.display = DisplayStyle.Flex;

                if (!_alreadySetupParent) SetupParent(rootHolder);
            }
        }


        void SetupChildren(GroupBox rootHolder)
        {
            _alreadySetupChildren = true;
            GroupBox childGroupBox = rootHolder.Q<GroupBox>("ChildGroupBox");

            if (transform.childCount > 0)
            {
                childGroupBox.style.display = DisplayStyle.Flex;
                CustomFoldout.SetupFoldout(childGroupBox);

                GroupBox warning = rootHolder.Q<GroupBox>("TooManyChildForInspectorCreation");
                GroupBox childEditorHolder = childGroupBox.Q<GroupBox>("Content");
                if (transform.childCount <= _betterTransformSettings.MaxChildInspector)
                {
                    warning.style.display = DisplayStyle.None;
                    foreach (Transform child in transform)
                        if (child != originalTransform)
                            CreateEditor(childEditorHolder,
                                child.GetSiblingIndex() + ". " + child.gameObject.name, child, false, false);
                        else
                            childEditorHolder.Add(new Label("   ✖ " + child.GetSiblingIndex() + ". " +
                                                            child.gameObject.name + " <i>[Selected Object]</i>"));
                }
                else
                {
                    warning.style.display = DisplayStyle.Flex;
                }

                childGroupBox.Q<Label>("Label1").text = "Child count: " + transform.childCount;
                Transform[] transforms = transform.GetComponentsInChildren<Transform>();
                Label label2 = childGroupBox.Q<Label>("Label2");
                if (transforms.Length - 1 != transform.childCount)
                {
                    label2.text = "Recursive child count: " + (transforms.Length - 1);
                    label2.style.display = DisplayStyle.Flex;
                }
                else
                {
                    label2.style.display = DisplayStyle.None;
                }
            }
            else
            {
                childGroupBox.style.display = DisplayStyle.None;
            }
        }

        bool _alreadySetupParent;

        void SetupParent(GroupBox rootHolder)
        {
            if (!transform.parent) return;
            _alreadySetupParent = true;
            if (transform.parent == originalTransform) return;
            if (_parentGroupBox == null)
            {
                _parentGroupBox = new GroupBox
                {
                    style =
                    {
                        marginTop = 0,
                        marginRight = 0,
                        marginBottom = 0,
                        paddingTop = 0,
                        paddingBottom = 0
                    }
                };
                // int index = Math.Abs(rootHolder.childCount - 2);

                int index = Math.Abs(_childGroupBox.parent.IndexOf(_childGroupBox));
                rootHolder.Insert(index, _parentGroupBox);
            }

            CreateEditor(_parentGroupBox, "(Parent) " + transform.parent.gameObject.name,
                transform.parent, true);
        }

        void CreateEditor(GroupBox rootHolder, string foldoutName,
            Transform targetTransform, bool margin = false, bool showSiblingIndex = true)
        {
            VisualElement visualElement = new VisualElement();
            rootHolder.Add(visualElement);
            folderTemplate.CloneTree(visualElement);
            GroupBox container = visualElement.Q<GroupBox>("TemplateRoot");
            if (margin)
            {
                container.style.marginLeft = -7;
                container.style.marginRight = 0;
            }

            CustomFoldout.SetupFoldout(container);

            GroupBox content = container.Q<GroupBox>("Content");

            container.Q<Button>("Ping").clicked += () =>
            {
                if (targetTransform == null) return;
                EditorGUIUtility.PingObject(targetTransform.gameObject);
            };

            container.Q<Button>("Select").clicked += () =>
            {
                if (targetTransform == null) return;
                Selection.activeGameObject = targetTransform.gameObject;
            };

            Button openEditorButton = container.Q<Button>("OpenEditorButton");
            openEditorButton.style.display = DisplayStyle.Flex;

            Toggle toggle = container.Q<Toggle>("FoldoutToggle");
            toggle.text = foldoutName;
            toggle.schedule.Execute(() =>
            {
                toggle.RegisterValueChangedCallback(_ =>
                {
                    if (openEditorButton.style.display != DisplayStyle.Flex) return;
                    openEditorButton.style.display = DisplayStyle.None;
                    OpenEditor(targetTransform, content);
                });
            }).ExecuteLater(500);

            if (showSiblingIndex)
                UpdateSiblingIndex(targetTransform, container.Q<Label>("SiblingIndex"), "Sibling Index: ");
            else
                container.Q<Label>("SiblingIndex").style.display = DisplayStyle.None;
        }

        static void UpdateSiblingIndex(Transform targetTransform, Label label, string prefix = "")
        {
            if (targetTransform.parent)
            {
                int siblingIndex = targetTransform.GetSiblingIndex();
                label.text = prefix + siblingIndex;
                label.tooltip = "Sibling index " + siblingIndex + " means,\n" +
                                "The game object \"" + targetTransform.gameObject.name + "\" is the number " +
                                (siblingIndex + 1) + " child object of \"" + targetTransform.parent.gameObject.name +
                                "\".\n\n" +
                                "If a GameObject shares a parent with other GameObjects and are on the same level (i.e. they share the same direct parent), these GameObjects are known as siblings.\n" +
                                "The sibling index shows where each GameObject sits in this sibling hierarchy.";
            }
            else
            {
                label.style.display = DisplayStyle.None;
            }
        }

        void OpenEditor(Transform targetTransform, GroupBox container)
        {
            BetterTransformEditor newEditor =
                (BetterTransformEditor)CreateEditor(targetTransform);

            // if (originalTransform == null) originalTransform = transform;
            newEditor.originalTransform = transform;
            VisualElement inspector = newEditor.CreateInspectorInsideAnother(transform);

            _otherBetterTransformEditors.Add(newEditor);
            container.Add(inspector);
        }

        #endregion Parent Child

        #region Add Functionality

        #region Variables

        Button _settingsButton;
        GenericDropdownMenu _settingsMenuButton;

        #endregion Variables

        void SetupMenu()
        {
            _settingsButton = _topGroupBox.Q<Button>("Settings");

            if (targets.Length != 1 ||
                originalTransform !=
                null) //No settings when multiple objects are selected and when this inside another editor.
            {
                _settingsButton.style.display = DisplayStyle.None;
                return;
            }

            _settingsButton.clicked += OpenContextMenu_settings;
        }

        void OpenContextMenu_settings()
        {
            UpdateContextMenu_settings();
#if UNITY_6000_3_OR_NEWER
            _settingsMenuButton.DropDown(GetMenuRect(_settingsButton), _settingsButton, DropdownMenuSizeMode.Auto);
#else
            _settingsMenuButton.DropDown(GetMenuRect(_settingsButton), _settingsButton, true);
#endif
        }

        void UpdateContextMenu_settings()
        {
            _settingsMenuButton = new GenericDropdownMenu();

            if (_inspectorEditorSettings != null)
            {
                _inspectorEditorSettings.BetterTransformContextMenuItems ??= new Dictionary<string, Action>();

                foreach (KeyValuePair<string, Action> item in _inspectorEditorSettings.BetterTransformContextMenuItems)
                    _settingsMenuButton.AddItem(item.Key, false, item.Value);
            }

            _settingsMenuButton.AddSeparator("");


            _settingsMenuButton.AddItem("Settings", false, ToggleSettings);

            _settingsMenuButton.AddSeparator("");

            if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
            {
                _settingsMenuButton.AddItem("Both Space Together", true, () =>
                {
                    _betterTransformSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.Local;
                    UpdateMainControls();
                    UpdateSize();
                });
                _settingsMenuButton.AddDisabledItem("Default Inspector for Local Fields", true);
            }
            else
            {
                _settingsMenuButton.AddItem("Both Space Together", false, () =>
                {
                    _betterTransformSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.Both;
                    UpdateMainControls();
                    UpdateSize();
                });
                _settingsMenuButton.AddItem("Default Inspector for Local Fields",
                    _betterTransformSettings.LoadDefaultInspector, () =>
                    {
                        _betterTransformSettings.LoadDefaultInspector = !_betterTransformSettings.LoadDefaultInspector;
                        UpdateMainControls();
                    });
            }


            if (!_betterTransformSettings.ShowSizeFoldout && !_betterTransformSettings.ShowSizeInLine) return;
            _settingsMenuButton.AddSeparator("");

            bool hierarchySize = _betterTransformSettings.IncludeChildBounds;
            _settingsMenuButton.AddItem("Hierarchy Size", hierarchySize, () =>
            {
                _betterTransformSettings.IncludeChildBounds = true;
                UpdateSizeInclusionButtons();
                SceneView.RepaintAll();
            });
            _settingsMenuButton.AddItem("Self Size", !hierarchySize, () =>
            {
                _betterTransformSettings.IncludeChildBounds = false;
                UpdateSizeInclusionButtons();
                SceneView.RepaintAll();
            });

            _settingsMenuButton.AddSeparator("");

            _settingsMenuButton.AddItem("Renderer Size",
                _betterTransformSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer, () =>
                {
                    _betterTransformSettings.CurrentSizeType = BetterTransformSettings.SizeType.Renderer;
                    UpdateSizeTypeButtons();
                    UpdateSize();
                });

            _settingsMenuButton.AddItem("Mesh Filter Only Size",
                _betterTransformSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter, () =>
                {
                    _betterTransformSettings.CurrentSizeType = BetterTransformSettings.SizeType.Filter;
                    UpdateSizeTypeButtons();
                    UpdateSize();
                });

            _settingsMenuButton.AddSeparator("");

            if (_betterTransformSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter)
                _settingsMenuButton.AddDisabledItem("Ignore Particle and VFX Renderer",
                    _betterTransformSettings.ignoreParticleAndVFXInSizeCalculation);
            else
                _settingsMenuButton.AddItem("Ignore Particle and VFX Renderer",
                    _betterTransformSettings.ignoreParticleAndVFXInSizeCalculation, () =>
                    {
                        _betterTransformSettings.ignoreParticleAndVFXInSizeCalculation =
                            !_betterTransformSettings.ignoreParticleAndVFXInSizeCalculation;
                        _betterTransformSettings.Save();
                        UpdateSize(true);
                        SceneView.RepaintAll();
                    });
        }

        static Rect GetMenuRect(VisualElement anchor)
        {
            Rect worldBound = anchor.worldBound;
            worldBound.xMin -= 250;
            worldBound.xMax += 0;
            return worldBound;
        }

        #endregion Add Functionality

        #region Settings

        /// <summary>
        ///     Registering callbacks for every field in the settings is unnecessary when most fields remain unused.
        ///     This can be used to determine whether setup has already occurred; if not, invoke SetupSettingsField() to complete
        ///     it.
        /// </summary>
        bool _settingsFieldSetupDone;

        Toggle _roundPositionFieldToggle;
        Toggle _roundRotationFieldToggle;
        Toggle _roundScaleFieldToggle;


        GroupBox _settingsFoldout;

        void SetupSettingsFoldouts()
        {
            CustomFoldout.SetupFoldout(_settingsFoldout);
            CustomFoldout.SetupFoldout(_settingsFoldout.Q<GroupBox>("InspectorCustomizationSettings"));
            CustomFoldout.SetupFoldout(_settingsFoldout.Q<GroupBox>("MainInformationSettings"));
            CustomFoldout.SetupFoldout(_settingsFoldout.Q<GroupBox>("SizeGroupBox"));
            CustomFoldout.SetupFoldout(_settingsFoldout.Q<GroupBox>("GizmoSettingsGroupBox"));
            CustomFoldout.SetupFoldout(_settingsFoldout.Q<GroupBox>("GizmoLabelSettingsGroupBox"),
                "FoldoutToggle", "GizmoLabel");
            CustomFoldout.SetupFoldout(_settingsFoldout.Q<GroupBox>("UtilitySettings"));

            Toggle foldoutToggle = _settingsFoldout.Q<Toggle>("FoldoutToggle");
            foldoutToggle.SetValueWithoutNotify(false);
            foldoutToggle.schedule.Execute(() => { foldoutToggle.RegisterValueChangedCallback(ev => { ToggleSettings(ev.newValue); }); }).ExecuteLater(1000);
        }

        /// <summary>
        /// </summary>
        /// <param name="value">Turn on or off</param>
        void ToggleSettings(bool value)
        {
            Toggle foldoutToggle = _settingsFoldout.Q<Toggle>("FoldoutToggle");
            GroupBox content = _settingsFoldout.Q<GroupBox>("Content");

            //When on, this is called AFTER foldout value has been set. If this causes issue, schedule the binding
            ToggleSettings(_settingsFoldout, foldoutToggle, content, value);
        }

        void ToggleSettings()
        {
            if (_settingsFoldout == null || !_rootHolder.Contains(_settingsFoldout.parent))
                _settingsFieldSetupDone = false;

            if (!_settingsFieldSetupDone)
            {
                if (settingsTemplate == null)
                    settingsTemplate = Utility.GetVisualTreeAsset(SettingsTemplateFileLocation, SettingsTemplateGuid);
                if (settingsTemplate == null) return;

                TemplateContainer settingsTemplateContainer = settingsTemplate.CloneTree();
                _rootHolder.Insert(1, settingsTemplateContainer);
                _settingsFoldout = settingsTemplateContainer.Q<GroupBox>("Settings");
                SetupSettingsFoldouts();
                SetupSettingsFields();
            }

            Toggle foldoutToggle = _settingsFoldout.Q<Toggle>("FoldoutToggle");
            GroupBox content = _settingsFoldout.Q<GroupBox>("Content");

            // ReSharper disable once PossibleNullReferenceException
            bool turnedOn = _settingsFoldout.style.display == DisplayStyle.Flex;
            //When on, this is called AFTER foldout value has been set. If this causes issue, schedule the binding
            ToggleSettings(_settingsFoldout, foldoutToggle, content, !turnedOn);
        }

        static void ToggleSettings(GroupBox settings, Toggle foldoutToggle, GroupBox content, bool turnOn)
        {
            CustomFoldout.SwitchContent(content, turnOn);

            //Turn on settings
            if (turnOn)
            {
                foldoutToggle.SetValueWithoutNotify(true);
                settings.style.display = DisplayStyle.Flex;
            }
            //Turn off settings
            else
            {
                foldoutToggle.schedule.Execute(() => TurnOffSettings(settings, foldoutToggle)).ExecuteLater(200);
            }
        }

        //TODO: Q the content
        //This is only called after the settings button is clicked.
        //This is to reduce workload when something is clicked and not unnecessarily assign stuff.
        void SetupSettingsFields()
        {
            _settingsFieldSetupDone = true;

            GroupBox settingsFoldoutContent = _settingsFoldout.Q<GroupBox>("Content");

            SettingsFilePathManager.SetupSettingsPathConfigurationUI(settingsFoldoutContent);

            GroupBox settingsFoldoutHeader = _settingsFoldout.Q<GroupBox>("Header");

            settingsFoldoutContent.Add(new HelpBox(
                "Some fields and labels are automatically hidden according to the width of the inspector.\n" +
                "Resize the inspector to check it out.",
                HelpBoxMessageType.Info));

            settingsFoldoutContent.Q<ToolbarButton>("AssetLink").clicked += () => Application.OpenURL(AssetLink);
            settingsFoldoutContent.Q<ToolbarButton>("Documentation").clicked +=
                () => Application.OpenURL(DocumentationLink);
            settingsFoldoutContent.Q<ToolbarButton>("OtherAssetsLink").clicked +=
                () => Application.OpenURL(PublisherLink);

            Toggle defaultUnityInspectorToggle = settingsFoldoutContent.Q<Toggle>("DefaultUnityInspectorToggle");
            defaultUnityInspectorToggle.SetValueWithoutNotify(_betterTransformSettings.LoadDefaultInspector);
            defaultUnityInspectorToggle.schedule.Execute(() =>
            {
                defaultUnityInspectorToggle.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.LoadDefaultInspector = e.newValue;
                    UpdateMainControls();
                });
            }).ExecuteLater(100);

            Toggle foldoutAnimationsToggle = settingsFoldoutContent.Q<Toggle>("FoldoutAnimationsToggle");
            foldoutAnimationsToggle.SetValueWithoutNotify(_betterTransformSettings.animatedFoldout);
            foldoutAnimationsToggle.schedule.Execute(() =>
            {
                foldoutAnimationsToggle.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.animatedFoldout = e.newValue;
                    _betterTransformSettings.Save();

                    StyleSheetsManager.UpdateStyleSheet(_root);
                });
            }).ExecuteLater(50);

            ColorField foldoutColorField = settingsFoldoutContent.Q<ColorField>("FoldoutColorField");
            Toggle overrideFoldoutColorToggle = settingsFoldoutContent.Q<Toggle>("OverrideFoldoutColorToggle");

            Toggle overrideInspectorColor = settingsFoldoutContent.Q<Toggle>("OverrideInspectorColorToggle");
            ColorField inspectorLocalSpaceColorField =
                settingsFoldoutContent.Q<ColorField>("InspectorLocalSpaceColorField");
            ColorField inspectorWorldSpaceColorField =
                settingsFoldoutContent.Q<ColorField>("InspectorWorldSpaceColorField");
            SetupInspectorColorSettings(overrideFoldoutColorToggle, foldoutColorField, overrideInspectorColor,
                inspectorLocalSpaceColorField, inspectorWorldSpaceColorField);

            SliderInt foldoutStyle = settingsFoldoutContent.Q<SliderInt>("FoldoutStyle");
            foldoutStyle.SetValueWithoutNotify(_inspectorEditorSettings.selectedFoldoutStyle);
            foldoutStyle.schedule.Execute(() =>
            {
                foldoutStyle.RegisterValueChangedCallback(ev =>
                {
                    _inspectorEditorSettings.selectedFoldoutStyle = ev.newValue;
                    UpdateStyleSheets();
                });
            }).ExecuteLater(75);

            SliderInt buttonStyle = settingsFoldoutContent.Q<SliderInt>("ButtonStyle");
            buttonStyle.SetValueWithoutNotify(_inspectorEditorSettings.selectedButtonStyle);
            buttonStyle.schedule.Execute(() =>
            {
                buttonStyle.RegisterValueChangedCallback(ev =>
                {
                    _inspectorEditorSettings.selectedButtonStyle = ev.newValue;
                    UpdateStyleSheets();
                });
            }).ExecuteLater(100);

            Toggle copyPasteButtonsToggle = settingsFoldoutContent.Q<Toggle>("CopyPasteButtonsToggle");
            copyPasteButtonsToggle.SetValueWithoutNotify(_betterTransformSettings.ShowCopyPasteButtons);
            copyPasteButtonsToggle.schedule.Execute(() =>
            {
                copyPasteButtonsToggle.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.ShowCopyPasteButtons = ev.newValue;
                    UpdateToolbarVisibility();
                });
            }).ExecuteLater(100);


            IntegerField fieldRoundingField = settingsFoldoutContent.Q<IntegerField>("FieldRounding");
            fieldRoundingField.value = _betterTransformSettings.FieldRoundingAmount;
            fieldRoundingField.schedule.Execute(() =>
            {
                fieldRoundingField.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.FieldRoundingAmount = ev.newValue;
                    UpdateMainControls();
                });
            }).ExecuteLater(150);

            _roundPositionFieldToggle = settingsFoldoutContent.Q<Toggle>("RoundPositionFieldToggle");
            _roundPositionFieldToggle.SetValueWithoutNotify(_betterTransformSettings.roundPositionField);
            _roundPositionFieldToggle.schedule.Execute(() => { _roundPositionFieldToggle.RegisterValueChangedCallback(_ => { TogglePositionFieldRounding(); }); }).ExecuteLater(200);

            _roundRotationFieldToggle = settingsFoldoutContent.Q<Toggle>("RoundRotationFieldToggle");
            _roundRotationFieldToggle.SetValueWithoutNotify(_betterTransformSettings.roundRotationField);
            _roundRotationFieldToggle.schedule.Execute(() => { _roundRotationFieldToggle.RegisterValueChangedCallback(_ => { ToggleRotationFieldRounding(); }); }).ExecuteLater(250);

            _maxChildCountForSizeCalculation =
                settingsFoldoutContent.Q<IntegerField>("MaxChildCountForSizeCalculation");
            _maxChildCountForSizeCalculation.SetValueWithoutNotify(_betterTransformSettings
                .MaxChildCountForSizeCalculation);
            _maxChildCountForSizeCalculation.schedule.Execute(() => { _maxChildCountForSizeCalculation.RegisterValueChangedCallback(ev => { _betterTransformSettings.MaxChildCountForSizeCalculation = ev.newValue; }); }).ExecuteLater(1000);

            _roundScaleFieldToggle = settingsFoldoutContent.Q<Toggle>("RoundScaleFieldToggle");
            _roundScaleFieldToggle.SetValueWithoutNotify(_betterTransformSettings.roundScaleField);
            _roundScaleFieldToggle.schedule.Execute(() => { _roundScaleFieldToggle.RegisterValueChangedCallback(_ => { ToggleScaleFieldRounding(); }); }).ExecuteLater(300);

            Toggle sizeFoldoutToggle = settingsFoldoutContent.Q<Toggle>("ShowSizeFoldoutToggle");
            SetupSizeFoldoutToggle(settingsFoldoutContent, sizeFoldoutToggle);

            Toggle showWhySizeFoldoutIsHidden = settingsFoldoutContent.Q<Toggle>("ShowWhySizeFoldoutIsHidden");
            showWhySizeFoldoutIsHidden.SetValueWithoutNotify(_betterTransformSettings.showWhySizeIsHiddenLabel);
            showWhySizeFoldoutIsHidden.schedule.Execute(() =>
            {
                showWhySizeFoldoutIsHidden.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.showWhySizeIsHiddenLabel = e.newValue;
                    _betterTransformSettings.Save();
                    UpdateSize();
                });
            }).ExecuteLater(350);

            Toggle ignoreParticleAndVFX = settingsFoldoutContent.Q<Toggle>("IgnoreParticleAndVFX");
            ignoreParticleAndVFX.SetValueWithoutNotify(_betterTransformSettings.ignoreParticleAndVFXInSizeCalculation);
            ignoreParticleAndVFX.schedule.Execute(() =>
            {
                ignoreParticleAndVFX.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.ignoreParticleAndVFXInSizeCalculation = e.newValue;
                    _betterTransformSettings.Save();
                    UpdateSize(true);
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(400);

            #region Gizmo

            FloatField sizeGizmoOutlineThickness = settingsFoldoutContent.Q<FloatField>("OutlineThickness");
            sizeGizmoOutlineThickness.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineThickness);
            sizeGizmoOutlineThickness.schedule.Execute(() =>
            {
                sizeGizmoOutlineThickness.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue < 1) return;
                    _betterTransformSettings.SizeGizmoOutlineThickness = e.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(450);

            ColorField sizeGizmoOutlineColorX = settingsFoldoutContent.Q<ColorField>("OutlineColorX");
            sizeGizmoOutlineColorX.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineColorX);
            sizeGizmoOutlineColorX.schedule.Execute(() =>
            {
                sizeGizmoOutlineColorX.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoOutlineColorX = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(500);

            ColorField sizeGizmoOutlineColorY = settingsFoldoutContent.Q<ColorField>("OutlineColorY");
            sizeGizmoOutlineColorY.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineColorY);
            sizeGizmoOutlineColorY.schedule.Execute(() =>
            {
                sizeGizmoOutlineColorY.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoOutlineColorY = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(550);

            ColorField sizeGizmoOutlineColorZ = settingsFoldoutContent.Q<ColorField>("OutlineColorZ");
            sizeGizmoOutlineColorZ.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineColorZ);
            sizeGizmoOutlineColorZ.schedule.Execute(() =>
            {
                sizeGizmoOutlineColorZ.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoOutlineColorZ = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(600);

            IntegerField sizeGizmoLabelSizeField = settingsFoldoutContent.Q<IntegerField>("SizeGizmoLabelSize");
            sizeGizmoLabelSizeField.value = _betterTransformSettings.SizeGizmoLabelSize;
            sizeGizmoLabelSizeField.schedule.Execute(() =>
            {
                sizeGizmoLabelSizeField.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelSize = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(650);

            ColorField sizeGizmoLabelColorX = settingsFoldoutContent.Q<ColorField>("GizmoLabelColorX");
            sizeGizmoLabelColorX.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoLabelColorX);
            sizeGizmoLabelColorX.schedule.Execute(() =>
            {
                sizeGizmoLabelColorX.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelColorX = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(700);

            ColorField sizeGizmoLabelColorY = settingsFoldoutContent.Q<ColorField>("GizmoLabelColorY");
            sizeGizmoLabelColorY.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoLabelColorY);
            sizeGizmoLabelColorY.schedule.Execute(() =>
            {
                sizeGizmoLabelColorY.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelColorY = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(750);

            ColorField sizeGizmoLabelColorZ = settingsFoldoutContent.Q<ColorField>("GizmoLabelColorZ");
            sizeGizmoLabelColorZ.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoLabelColorZ);
            sizeGizmoLabelColorZ.schedule.Execute(() =>
            {
                sizeGizmoLabelColorZ.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelColorZ = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(800);

            ColorField sizeGizmoLabelBackgroundColorX =
                settingsFoldoutContent.Q<ColorField>("GizmoLabelBackgroundColorX");
            sizeGizmoLabelBackgroundColorX.SetValueWithoutNotify(_betterTransformSettings
                .SizeGizmoLabelBackgroundColorX);
            sizeGizmoLabelBackgroundColorX.schedule.Execute(() =>
            {
                sizeGizmoLabelBackgroundColorX.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelBackgroundColorX = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(850);

            ColorField sizeGizmoLabelBackgroundColorY =
                settingsFoldoutContent.Q<ColorField>("GizmoLabelBackgroundColorY");
            sizeGizmoLabelBackgroundColorY.SetValueWithoutNotify(_betterTransformSettings
                .SizeGizmoLabelBackgroundColorY);
            sizeGizmoLabelBackgroundColorY.schedule.Execute(() =>
            {
                sizeGizmoLabelBackgroundColorY.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelBackgroundColorY = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(900);

            ColorField sizeGizmoLabelBackgroundColorZ =
                settingsFoldoutContent.Q<ColorField>("GizmoLabelBackgroundColorZ");
            sizeGizmoLabelBackgroundColorZ.SetValueWithoutNotify(_betterTransformSettings
                .SizeGizmoLabelBackgroundColorZ);
            sizeGizmoLabelBackgroundColorZ.schedule.Execute(() =>
            {
                sizeGizmoLabelBackgroundColorZ.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.SizeGizmoLabelBackgroundColorZ = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(950);

            Toggle showAxisOnLabel = settingsFoldoutContent.Q<Toggle>("AxisOnLabel");
            showAxisOnLabel.SetValueWithoutNotify(_betterTransformSettings.ShowAxisOnLabel);
            showAxisOnLabel.schedule.Execute(() =>
            {
                showAxisOnLabel.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.ShowAxisOnLabel = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1000);

            Toggle unitOnLabel = settingsFoldoutContent.Q<Toggle>("UnitOnLabel");
            unitOnLabel.SetValueWithoutNotify(_betterTransformSettings.ShowUnitOnLabel);
            unitOnLabel.schedule.Execute(() =>
            {
                unitOnLabel.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.ShowUnitOnLabel = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1005);

            FloatField labelOffset = settingsFoldoutContent.Q<FloatField>("LabelOffset");
            labelOffset.SetValueWithoutNotify(_betterTransformSettings.LabelOffset);
            labelOffset.schedule.Execute(() =>
            {
                labelOffset.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.LabelOffset = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1010);

            RadioButton positionLabelAtCenter = settingsFoldoutContent.Q<RadioButton>("PositionLabelAtCenter");
            positionLabelAtCenter.SetValueWithoutNotify(_betterTransformSettings.PositionLabelAtCenter);
            positionLabelAtCenter.schedule.Execute(() =>
            {
                positionLabelAtCenter.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.PositionLabelAtCenter = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1020);

            RadioButton closestAxis = settingsFoldoutContent.Q<RadioButton>("ClosestAxis");
            closestAxis.SetValueWithoutNotify(_betterTransformSettings.PositionLabelAtClosestAxis);
            closestAxis.schedule.Execute(() =>
            {
                closestAxis.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.PositionLabelAtClosestAxis = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1030);

            RadioButton cornerAxis = settingsFoldoutContent.Q<RadioButton>("CornerAxis");
            cornerAxis.SetValueWithoutNotify(_betterTransformSettings.PositionLabelAtCornerAxis);
            cornerAxis.schedule.Execute(() =>
            {
                cornerAxis.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.PositionLabelAtCornerAxis = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1040);

            Toggle sizeGizmoLabelBothSideToggle = settingsFoldoutContent.Q<Toggle>("SizeGizmoLabelBothSide");
            sizeGizmoLabelBothSideToggle.SetValueWithoutNotify(_betterTransformSettings.ShowSizeGizmoLabelOnBothSide);
            sizeGizmoLabelBothSideToggle.schedule.Execute(() =>
            {
                sizeGizmoLabelBothSideToggle.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.ShowSizeGizmoLabelOnBothSide = ev.newValue;
                    SceneView.RepaintAll();
                    UpdateDoubleSidedLabelSettings(settingsFoldoutContent);
                });
            }).ExecuteLater(1050);


            IntegerField minimumSizeForDoubleLabel =
                settingsFoldoutContent.Q<IntegerField>("MinimumSizeForDoubleLabel");
            minimumSizeForDoubleLabel.SetValueWithoutNotify(_betterTransformSettings.MinimumSizeForDoubleSidedLabel);
            copyPasteButtonsToggle.schedule.Execute(() =>
            {
                minimumSizeForDoubleLabel.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.MinimumSizeForDoubleSidedLabel = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1060);


            IntegerField gizmoMaximumDecimalPoints =
                settingsFoldoutContent.Q<IntegerField>("GizmoMaximumDecimalPoints");
            gizmoMaximumDecimalPoints.SetValueWithoutNotify(_betterTransformSettings.GizmoMaximumDecimalPoints);
            copyPasteButtonsToggle.schedule.Execute(() =>
            {
                gizmoMaximumDecimalPoints.RegisterValueChangedCallback(ev =>
                {
                    if (ev.newValue < 0)
                    {
                        gizmoMaximumDecimalPoints.SetValueWithoutNotify(0);
                        _betterTransformSettings.GizmoMaximumDecimalPoints = 0;
                        SceneView.RepaintAll();
                        return;
                    }

                    _betterTransformSettings.GizmoMaximumDecimalPoints = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1070);


            Toggle labelHandlesToggle = settingsFoldoutContent.Q<Toggle>("LabelHandles");
            labelHandlesToggle.SetValueWithoutNotify(_betterTransformSettings.ShowSizeGizmosLabelHandle);
            labelHandlesToggle.schedule.Execute(() =>
            {
                labelHandlesToggle.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.ShowSizeGizmosLabelHandle = ev.newValue;
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1080);

            #endregion Gizmo

            Toggle showSiblingIndexToggle = settingsFoldoutContent.Q<Toggle>("ShowSiblingIndexToggle");
            showSiblingIndexToggle.SetValueWithoutNotify(_betterTransformSettings.showSiblingIndex);
            showSiblingIndexToggle.schedule.Execute(() =>
            {
                showSiblingIndexToggle.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.showSiblingIndex = ev.newValue;

                    if (_betterTransformSettings.showSiblingIndex && transform.parent)
                    {
                        _siblingIndexLabel.style.display = DisplayStyle.Flex;
                        _siblingIndex.style.display = DisplayStyle.Flex;
                        UpdateSiblingIndex(transform, _siblingIndex);
                    }
                    else
                    {
                        _siblingIndexLabel.style.display = DisplayStyle.None;
                        _siblingIndex.style.display = DisplayStyle.None;
                    }
                });
            }).ExecuteLater(1090);

            Toggle showAssetGuid = settingsFoldoutContent.Q<Toggle>("ShowAssetGUID");
            showAssetGuid.SetValueWithoutNotify(_betterTransformSettings.showAssetGuid);
            showAssetGuid.schedule.Execute(() =>
            {
                showAssetGuid.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.showAssetGuid = ev.newValue;
                    _betterTransformSettings.Save();

                    Label idLabel = _root.Q<Label>("GUID");
                    idLabel.style.display = DisplayStyle.None;
                    if (!_betterTransformSettings.showAssetGuid) return;
                    if (!_thisIsAnAsset) return;
                    idLabel.style.display = DisplayStyle.Flex;

                    string id = NoteUtility.GetUserFriendlyID(transform);
                    idLabel.text = id;
                    idLabel.tooltip = "GUID\n" + id;
                });
            }).ExecuteLater(1100);

            Toggle parentChildTransformsToggle = settingsFoldoutContent.Q<Toggle>("ParentChildTransformsToggle");
            parentChildTransformsToggle.value = _betterTransformSettings.ShowParentChildTransform;
            parentChildTransformsToggle.schedule.Execute(() =>
            {
                parentChildTransformsToggle.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.ShowParentChildTransform = ev.newValue;
                    UpdateSetupParentChildFoldouts();
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1110);

            Toggle pingSelfButtonToggle = settingsFoldoutContent.Q<Toggle>("PingSelfButton");
            pingSelfButtonToggle.SetValueWithoutNotify(_betterTransformSettings.pingSelfButton);
            pingSelfButtonToggle.schedule.Execute(() =>
            {
                pingSelfButtonToggle.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.pingSelfButton = e.newValue;

                    if (e.newValue)
                    {
                        _pingSelfButton.style.display = DisplayStyle.Flex;
                        _pingSelfButton.clicked += () => { EditorGUIUtility.PingObject(transform); };
                    }
                    else
                    {
                        _pingSelfButton.style.display = DisplayStyle.None;
                    }
                });
            }).ExecuteLater(1150);

            IntegerField maxChildInspector = settingsFoldoutContent.Q<IntegerField>("MaxChildInspector");
            maxChildInspector.value = _betterTransformSettings.MaxChildInspector;
            maxChildInspector.schedule.Execute(() =>
            {
                maxChildInspector.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.MaxChildInspector = ev.newValue;
                    UpdateSetupParentChildFoldouts();
                    SceneView.RepaintAll();
                });
            }).ExecuteLater(1200);


            Button scaleSettingsButton = settingsFoldoutContent.Q<Button>("ScaleSettingsButton");
            scaleSettingsButton.clicked += () => { SettingsService.OpenProjectSettings("Project/Tiny Giant Studio/Scale Settings"); };

            Toggle autoSizeUpdate = settingsFoldoutContent.Q<Toggle>("ConstantlyUpdateSize");
            autoSizeUpdate.SetValueWithoutNotify(_betterTransformSettings.ConstantSizeUpdate);
            autoSizeUpdate.schedule.Execute(() =>
            {
                autoSizeUpdate.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.ConstantSizeUpdate = e.newValue;
                    if (e.newValue)
                        StartSizeSchedule();
                    else
                        RemoveSizeUpdateScheduler();
                    UpdateSizeFoldoutWarnings();
                });
            }).ExecuteLater(1210);


            //UpdateSizeViewType();
            //UpdateSizeLabelSettings(inspectorSettingsFoldout);
            UpdateDoubleSidedLabelSettings(settingsFoldoutContent);

            Toggle performanceLoggingToggle = settingsFoldoutContent.Q<Toggle>("PerformanceLoggingToggle");
            Toggle detailedPerformanceLoggingToggle =
                settingsFoldoutContent.Q<Toggle>("DetailedPerformanceLoggingToggle");

            performanceLoggingToggle.SetValueWithoutNotify(_betterTransformSettings.logPerformance);
            performanceLoggingToggle.schedule.Execute(() =>
            {
                performanceLoggingToggle.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.logPerformance = e.newValue;
                    _betterTransformSettings.Save();

                    if (_betterTransformSettings.logPerformance)
                        detailedPerformanceLoggingToggle.style.display = DisplayStyle.Flex;
                    else
                        detailedPerformanceLoggingToggle.style.display = DisplayStyle.None;

                    UpdatePerformanceLoggingGroupBox();
                });
            }).ExecuteLater(1300);

            if (_betterTransformSettings.logPerformance)
                detailedPerformanceLoggingToggle.style.display = DisplayStyle.Flex;
            else
                detailedPerformanceLoggingToggle.style.display = DisplayStyle.None;

            detailedPerformanceLoggingToggle.SetValueWithoutNotify(_betterTransformSettings.logDetailedPerformance);
            detailedPerformanceLoggingToggle.schedule.Execute(() =>
            {
                detailedPerformanceLoggingToggle.RegisterValueChangedCallback(e =>
                {
                    _betterTransformSettings.logDetailedPerformance = e.newValue;
                    _betterTransformSettings.Save();
                    UpdatePerformanceLoggingGroupBox();
                });
            }).ExecuteLater(1310);

            settingsFoldoutHeader.Q<Button>("ResetInspectorSettingsToMinimal").clicked += () =>
            {
                _betterTransformSettings.ResetToMinimal();
                Reset();
            };
            settingsFoldoutHeader.Q<Button>("ResetButton").clicked += () =>
            {
                _betterTransformSettings.ResetToDefault();
                BetterInspectorEditorSettings.instance.Reset();
                //There is no need to call the methods to update since the fields call them on value changes automatically.
                Reset();
            };
            return;

            void Reset()
            {
                settingsFoldoutContent.Q<Toggle>("DefaultUnityInspectorToggle")
                    .SetValueWithoutNotify(_betterTransformSettings.LoadDefaultInspector);

                overrideFoldoutColorToggle.value = _betterTransformSettings.OverrideFoldoutColor;
                foldoutColorField.value = _betterTransformSettings.FoldoutColor;
                overrideInspectorColor.value = _betterTransformSettings.OverrideInspectorColor;
                inspectorLocalSpaceColorField.value = _betterTransformSettings.InspectorColorInLocalSpace;
                foldoutStyle.SetValueWithoutNotify(BetterInspectorEditorSettings.instance.selectedFoldoutStyle);
                buttonStyle.SetValueWithoutNotify(BetterInspectorEditorSettings.instance.selectedButtonStyle);
                inspectorWorldSpaceColorField.value = _betterTransformSettings.InspectorColorInWorldSpace;
                StyleSheetsManager.UpdateStyleSheet(_root);

                copyPasteButtonsToggle.value = _betterTransformSettings.ShowCopyPasteButtons;
                fieldRoundingField.value = _betterTransformSettings.FieldRoundingAmount;

                sizeFoldoutToggle.value = _betterTransformSettings.ShowSizeFoldout;

                #region Gizmo

                sizeGizmoOutlineThickness.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineThickness);
                sizeGizmoOutlineColorX.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineColorX);
                sizeGizmoOutlineColorY.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineColorY);
                sizeGizmoOutlineColorZ.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoOutlineColorZ);

                sizeGizmoLabelSizeField.value =
                    _betterTransformSettings.SizeGizmoLabelSize; //Trigger the label styles to update
                sizeGizmoLabelColorX.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoLabelColorX);
                sizeGizmoLabelColorY.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoLabelColorY);
                sizeGizmoLabelColorZ.SetValueWithoutNotify(_betterTransformSettings.SizeGizmoLabelColorZ);

                sizeGizmoLabelBackgroundColorX.value = _betterTransformSettings.SizeGizmoLabelBackgroundColorX;
                sizeGizmoLabelBackgroundColorY.value = _betterTransformSettings.SizeGizmoLabelBackgroundColorY;
                sizeGizmoLabelBackgroundColorZ.value = _betterTransformSettings.SizeGizmoLabelBackgroundColorZ;

                showAxisOnLabel.SetValueWithoutNotify(_betterTransformSettings.ShowAxisOnLabel);
                unitOnLabel.SetValueWithoutNotify(_betterTransformSettings.ShowUnitOnLabel);

                positionLabelAtCenter.SetValueWithoutNotify(_betterTransformSettings.PositionLabelAtCenter);
                closestAxis.SetValueWithoutNotify(_betterTransformSettings.PositionLabelAtClosestAxis);
                cornerAxis.SetValueWithoutNotify(_betterTransformSettings.PositionLabelAtCornerAxis);

                sizeGizmoLabelBothSideToggle.value = _betterTransformSettings.ShowSizeGizmoLabelOnBothSide;
                minimumSizeForDoubleLabel.SetValueWithoutNotify(_betterTransformSettings
                    .MinimumSizeForDoubleSidedLabel);
                gizmoMaximumDecimalPoints.value = _betterTransformSettings.GizmoMaximumDecimalPoints;
                labelHandlesToggle.SetValueWithoutNotify(_betterTransformSettings.ShowSizeGizmosLabelHandle);
                labelOffset.SetValueWithoutNotify(_betterTransformSettings.LabelOffset);

                #endregion Gizmo

                parentChildTransformsToggle.value = _betterTransformSettings.ShowParentChildTransform;
                maxChildInspector.value = _betterTransformSettings.MaxChildInspector;
                _maxChildCountForSizeCalculation.value = _betterTransformSettings.MaxChildCountForSizeCalculation;

                showSiblingIndexToggle.value = _betterTransformSettings.showSiblingIndex;
                showAssetGuid.value = _betterTransformSettings.showAssetGuid;

                performanceLoggingToggle.value = _betterTransformSettings.logPerformance;

                SceneView.RepaintAll();
            }
        }

        void SetupInspectorColorSettings(Toggle overrideFoldoutColorToggle, ColorField foldoutColorField,
            Toggle overrideInspectorColor, ColorField inspectorLocalSpaceColorField,
            ColorField inspectorWorldSpaceColorField)
        {
            overrideFoldoutColorToggle.value = _betterTransformSettings.OverrideFoldoutColor;
            overrideFoldoutColorToggle.schedule.Execute(() =>
            {
                overrideFoldoutColorToggle.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.OverrideFoldoutColor = ev.newValue;
                    if (!_betterTransformSettings.OverrideFoldoutColor)
                        foldoutColorField.SetEnabled(false);
                    else
                        foldoutColorField.SetEnabled(true);
                    UpdateInspectorColor();
                });
            }).ExecuteLater(300);

            if (!_betterTransformSettings.OverrideFoldoutColor)
                foldoutColorField.SetEnabled(false);
            else
                foldoutColorField.SetEnabled(true);

            foldoutColorField.SetValueWithoutNotify(_betterTransformSettings.FoldoutColor);
            foldoutColorField.schedule.Execute(() =>
            {
                foldoutColorField.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.FoldoutColor = ev.newValue;
                    UpdateInspectorColor();
                });
            }).ExecuteLater(400);


            overrideInspectorColor.value = _betterTransformSettings.OverrideInspectorColor;
            overrideInspectorColor.schedule.Execute(() =>
            {
                overrideInspectorColor.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.OverrideInspectorColor = ev.newValue;
                    if (!_betterTransformSettings.OverrideInspectorColor)
                    {
                        inspectorLocalSpaceColorField.SetEnabled(false);
                        inspectorWorldSpaceColorField.SetEnabled(false);
                    }
                    else
                    {
                        inspectorLocalSpaceColorField.SetEnabled(true);
                        inspectorWorldSpaceColorField.SetEnabled(true);
                    }

                    UpdateInspectorColor();
                });
            }).ExecuteLater(200);

            inspectorLocalSpaceColorField.SetValueWithoutNotify(_betterTransformSettings.InspectorColorInLocalSpace);
            inspectorWorldSpaceColorField.SetValueWithoutNotify(_betterTransformSettings.InspectorColorInWorldSpace);

            if (!_betterTransformSettings.OverrideInspectorColor)
            {
                inspectorWorldSpaceColorField.SetEnabled(false);
                inspectorLocalSpaceColorField.SetEnabled(false);
            }
            else
            {
                inspectorWorldSpaceColorField.SetEnabled(true);
                inspectorLocalSpaceColorField.SetEnabled(true);
            }

            inspectorLocalSpaceColorField.schedule.Execute(() =>
            {
                inspectorLocalSpaceColorField.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.InspectorColorInLocalSpace = ev.newValue;
                    UpdateInspectorColor();
                });
            }).ExecuteLater(200);

            inspectorWorldSpaceColorField.schedule.Execute(() =>
            {
                inspectorWorldSpaceColorField.RegisterValueChangedCallback(ev =>
                {
                    _betterTransformSettings.InspectorColorInWorldSpace = ev.newValue;
                    UpdateInspectorColor();
                });
            }).ExecuteLater(200);
        }


        void SetupSizeFoldoutToggle(GroupBox settingsFoldout, Toggle sizeFoldoutToggle)
        {
            Toggle showSizeInLineToggle = settingsFoldout.Q<Toggle>("ShowSizeInlineToggle");
            GroupBox gizmoSettingsGroupBox = settingsFoldout.Q<GroupBox>("GizmoSettingsGroupBox");
            Toggle showSizeCenterToggle = settingsFoldout.Q<Toggle>("ShowSizeCenterToggle");

            sizeFoldoutToggle.SetValueWithoutNotify(_betterTransformSettings.ShowSizeFoldout);
            sizeFoldoutToggle.schedule.Execute(() =>
            {
                sizeFoldoutToggle.RegisterValueChangedCallback(ev =>
                {
                    SetupSize();
                    SetupViewWidthAdaptionForSize();

                    _betterTransformSettings.ShowSizeFoldout = ev.newValue;
                    UpdateSizeFoldout();
                    UpdateSizeViewType();

                    if (_betterTransformSettings.ShowSizeFoldout)
                        showSizeInLineToggle.SetEnabled(false);
                    else
                        showSizeInLineToggle.SetEnabled(true);

                    if (!_betterTransformSettings.ShowSizeInLine && !_betterTransformSettings.ShowSizeFoldout)
                    {
                        gizmoSettingsGroupBox.SetEnabled(false);
                        showSizeCenterToggle.SetEnabled(false);
                        _maxChildCountForSizeCalculation.SetEnabled(false);
                    }
                    else
                    {
                        gizmoSettingsGroupBox.SetEnabled(true);
                        showSizeCenterToggle.SetEnabled(true);
                        _maxChildCountForSizeCalculation.SetEnabled(true);
                    }

                    UpdateGizmoButton_Outline();
                    UpdateGizmoButton_sizeLabel();

                    SceneView.RepaintAll();
                });
            }).ExecuteLater(100);

            if (_betterTransformSettings.ShowSizeFoldout)
            {
                showSizeInLineToggle.SetEnabled(false);
                showSizeCenterToggle.SetEnabled(true);
            }

            showSizeInLineToggle.SetValueWithoutNotify(_betterTransformSettings.ShowSizeInLine);
            showSizeInLineToggle.schedule.Execute(() =>
            {
                showSizeInLineToggle.RegisterValueChangedCallback(ev =>
                {
                    SetupSize();
                    SetupViewWidthAdaptionForSize();

                    _betterTransformSettings.ShowSizeInLine = ev.newValue;
                    UpdateSizeFoldout();
                    UpdateSizeViewType();

                    if (_betterTransformSettings.ShowSizeInLine)
                    {
                        sizeFoldoutToggle.SetEnabled(false);
                        showSizeCenterToggle.SetEnabled(false);
                    }
                    else
                    {
                        sizeFoldoutToggle.SetEnabled(true);
                        showSizeCenterToggle.SetEnabled(true);
                    }

                    if (!_betterTransformSettings.ShowSizeInLine && !_betterTransformSettings.ShowSizeFoldout)
                        gizmoSettingsGroupBox.SetEnabled(false);
                    else
                        gizmoSettingsGroupBox.SetEnabled(true);

                    UpdateGizmoButton_Outline();
                    UpdateGizmoButton_sizeLabel();

                    SceneView.RepaintAll();
                });
            }).ExecuteLater(200);

            if (_betterTransformSettings.ShowSizeInLine)
            {
                sizeFoldoutToggle.SetEnabled(false);
                showSizeCenterToggle.SetEnabled(false);
            }

            showSizeCenterToggle.SetValueWithoutNotify(_betterTransformSettings.ShowSizeCenter);
            showSizeCenterToggle.schedule.Execute(() =>
            {
                showSizeCenterToggle.RegisterValueChangedCallback(_ =>
                {
                    _betterTransformSettings.ShowSizeCenter = showSizeCenterToggle.value;
                    UpdateSizeFoldout();
                });
            }).ExecuteLater(1000);

            if (!_betterTransformSettings.ShowSizeInLine && !_betterTransformSettings.ShowSizeFoldout)
            {
                gizmoSettingsGroupBox.SetEnabled(false);
                showSizeCenterToggle.SetEnabled(false);
            }
            else
            {
                gizmoSettingsGroupBox.SetEnabled(true);

                if (_betterTransformSettings.ShowSizeInLine)
                    showSizeCenterToggle.SetEnabled(false);
                else
                    showSizeCenterToggle.SetEnabled(true);
            }
        }


        /// <summary>
        ///     The difference between this and updateInspectorColor is this doesn't need to remove the color keywords since they
        ///     weren't added.
        /// </summary>
        void SetupInspectorColor()
        {
            if (_betterTransformSettings.OverrideFoldoutColor)
            {
                List<GroupBox> customFoldoutGroups = _root.Query<GroupBox>(className: "custom-foldout").ToList();
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = _betterTransformSettings.FoldoutColor;
            }

            UpdateInspectorRootColor();
        }

        void UpdateInspectorColor()
        {
            List<GroupBox> customFoldoutGroups = _root.Query<GroupBox>(className: "custom-foldout").ToList();
            if (_betterTransformSettings.OverrideFoldoutColor)
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = _betterTransformSettings.FoldoutColor;
            else
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = StyleKeyword.Null;

            UpdateInspectorRootColor();
        }

        GroupBox _rootHolder;

        void UpdateInspectorRootColor()
        {
            //Using the null check, the line causes error
            //Because it still references a root holder that doesn't exit. Not sure how
            //But, because of that, neither the color nor the settings work correctly.
            // _rootHolder ??= _root.Q<GroupBox>("RootHolder"); //If it is null, find it. 
            _rootHolder = _root.Q<GroupBox>("RootHolder");

            if (_betterTransformSettings.OverrideInspectorColor)
            {
                if (_betterTransformSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    _rootHolder.style.backgroundColor = _betterTransformSettings.InspectorColorInWorldSpace;
                else
                    _rootHolder.style.backgroundColor = _betterTransformSettings.InspectorColorInLocalSpace;
            }
            else
            {
                _rootHolder.style.backgroundColor = StyleKeyword.Null;
            }
        }

        static void TurnOffSettings(GroupBox settings, Toggle toggle)
        {
            toggle.SetValueWithoutNotify(false);
            settings.style.display = DisplayStyle.None;
        }

        void UpdateDoubleSidedLabelSettings(GroupBox settings)
        {
            IntegerField minimumSizeForDoubleLabel = settings.Q<IntegerField>("MinimumSizeForDoubleLabel");
            if (_betterTransformSettings.ShowSizeGizmoLabelOnBothSide)
                minimumSizeForDoubleLabel.SetEnabled(true);
            else
                minimumSizeForDoubleLabel.SetEnabled(false);
        }

        #endregion Settings

        #region Default Editor

        /// <summary>
        ///     If the UXML file is missing for any reason,
        ///     Instead of showing an empty inspector,
        ///     This loads the default one.
        ///     This shouldn't ever happen.
        /// </summary>
        void LoadDefaultEditor(VisualElement container)
        {
            if (_originalEditor != null)
                DestroyImmediate(_originalEditor);

            _originalEditor = CreateEditor(targets, Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
            IMGUIContainer inspectorContainer = new IMGUIContainer(OnGUICallback);
            container.Add(inspectorContainer);
        }

        void OnGUICallback()
        {
            if (target == null)
                return;
            if (_originalEditor == null)
                return;

            if (_inspectorWidth > WorkSpaceLabelThreshold) EditorGUIUtility.labelWidth = 65;
            else EditorGUIUtility.labelWidth = 0.001f;

            EditorGUI.BeginChangeCheck();
            _originalEditor.OnInspectorGUI();
            EditorGUI.EndChangeCheck();
        }

        #endregion Default Editor

        #region Animator

        #region Variables

        //Since it is not possible to get the current animator state like is it in recording mode in the current Unity version,
        //The state is retrieved from bound fields and then applied to the non-bound field.
        //Example: Copy state from localPosition field to worldPositionField, where localPositionField is bound and automatically updated by Animator
        VisualElement _animatorStateIndicatorPosition;
        VisualElement _animatorStateIndicatorRotation;
        VisualElement _animatorStateIndicatorScale;

        //The class applied to fields to indicate to user that the animator is playing a recorded state
        const string AnimatedFieldClass = "animatedField";

        //The class applied to fields to indicate to user that the animator is recording
        const string RecordingFieldClass = "animationRecordingField";

        //The class applied by unity to fields that are being recorded
        const string AnimationRecordingUssClass = "unity-binding--animation-recorded";

        #endregion Variables

        /// <summary>
        ///     Since the custom inspector uses a lot of non bou
        /// </summary>
        void SetupAnimatorCompability()
        {
            VerifyStateIndicatorReferences();
            SetupAnimatorState();

            _root.schedule.Execute(UpdateAnimatorState).Every(5000).StartingIn(10000); //1000 ms = 1 s
        }

        void SetupAnimatorState()
        {
            if (IsNotInValidAnimationMode()) return;

            UpdateAnimatorState_PositionFields();
            UpdateAnimatorState_RotationFields();
            UpdateAnimatorState_ScaleFields();
        }

        void UpdateAnimatorState()
        {
            if (_betterTransformSettings.logPerformance && _betterTransformSettings.logDetailedPerformance)
            {
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
            }

            if (IsNotInValidAnimationMode())
            {
                if (!_betterTransformSettings.logPerformance ||
                    !_betterTransformSettings.logDetailedPerformance) return;
                Log("(Running on loop) Animator State Update", _stopwatch.ElapsedMilliseconds);
                _stopwatch.Stop();

                return;
            }

            UpdateAnimatorState_PositionFields();
            UpdateAnimatorState_RotationFields();
            UpdateAnimatorState_ScaleFields();

            if (!_betterTransformSettings.logPerformance || !_betterTransformSettings.logDetailedPerformance) return;
            Log("(Running on loop) Animator State Update", _stopwatch.ElapsedMilliseconds);
            _stopwatch.Stop();
        }

        static bool IsNotInValidAnimationMode()
        {
            return EditorApplication.isPlaying || !AnimationMode.InAnimationMode();
        }

        void VerifyStateIndicatorReferences()
        {
            _animatorStateIndicatorPosition ??= _localPositionField.Q<FloatField>().Children().ElementAt(1);

            //This is written this way because it is a property field and binding takes time, sometimes this is called before the binding is done
            if (_animatorStateIndicatorRotation == null)
            {
                Toggle t = _quaternionRotationPropertyField.Q<Toggle>();
                if (t != null)
                    _animatorStateIndicatorRotation = t.Children().First();
            }

            _animatorStateIndicatorScale ??= _boundLocalScaleField.Q<FloatField>().Children().ElementAt(1);
        }

        bool _addedPositionAnimatorStateIndicatorClasses;

        void UpdateAnimatorState_PositionFields()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
            {
                if (!_addedPositionAnimatorStateIndicatorClasses) return;
                _worldPositionField.RemoveFromClassList(AnimatedFieldClass);
                _worldPositionField.RemoveFromClassList(RecordingFieldClass);
                _addedPositionAnimatorStateIndicatorClasses = false;

                return;
            }

            //if (animator_stateIndicator_position == null)
            //    animator_stateIndicator_position = localPositionField.Q<FloatField>().Children().ElementAt(1);

            bool isPositionAnimated = AnimationMode.IsPropertyAnimated(target, PositionProperty);
            if (isPositionAnimated)
            {
                if (_animatorStateIndicatorPosition != null &&
                    _animatorStateIndicatorPosition.ClassListContains(AnimationRecordingUssClass))
                {
                    _worldPositionField.RemoveFromClassList(AnimatedFieldClass);
                    _worldPositionField.AddToClassList(RecordingFieldClass);
                }
                else
                {
                    _worldPositionField.AddToClassList(AnimatedFieldClass);
                    _worldPositionField.RemoveFromClassList(RecordingFieldClass);
                }

                _addedPositionAnimatorStateIndicatorClasses = true;
            }
            else
            {
                if (!_addedPositionAnimatorStateIndicatorClasses) return;
                _worldPositionField.RemoveFromClassList(AnimatedFieldClass);
                _worldPositionField.RemoveFromClassList(RecordingFieldClass);
                _addedPositionAnimatorStateIndicatorClasses = false;
            }
        }

        bool _addedRotationAnimatorStateIndicatorClasses;

        void UpdateAnimatorState_RotationFields()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
            {
                if (!_addedRotationAnimatorStateIndicatorClasses) return;
                _localRotationField.RemoveFromClassList(AnimatedFieldClass);
                _worldRotationField.RemoveFromClassList(AnimatedFieldClass);

                _localRotationField.RemoveFromClassList(RecordingFieldClass);
                _worldRotationField.RemoveFromClassList(RecordingFieldClass);

                _addedRotationAnimatorStateIndicatorClasses = false;

                return;
            }

            if (_animatorStateIndicatorRotation == null)
            {
                Toggle t = _quaternionRotationPropertyField.Q<Toggle>();
                if (t != null)
                    _animatorStateIndicatorRotation = t.Children().First();
            }

            bool isRotationAnimated = AnimationMode.IsPropertyAnimated(target, RotationProperty);
            if (isRotationAnimated)
            {
                if (_animatorStateIndicatorRotation != null &&
                    _animatorStateIndicatorRotation.ClassListContains(AnimationRecordingUssClass))
                {
                    _localRotationField.RemoveFromClassList(AnimatedFieldClass);
                    _worldRotationField.RemoveFromClassList(AnimatedFieldClass);

                    _localRotationField.AddToClassList(RecordingFieldClass);
                    _worldRotationField.AddToClassList(RecordingFieldClass);
                }
                else
                {
                    _localRotationField.AddToClassList(AnimatedFieldClass);
                    _worldRotationField.AddToClassList(AnimatedFieldClass);

                    _localRotationField.RemoveFromClassList(RecordingFieldClass);
                    _worldRotationField.RemoveFromClassList(RecordingFieldClass);
                }

                _addedRotationAnimatorStateIndicatorClasses = true;
            }
            else
            {
                if (!_addedRotationAnimatorStateIndicatorClasses) return;

                _localRotationField.RemoveFromClassList(AnimatedFieldClass);
                _worldRotationField.RemoveFromClassList(AnimatedFieldClass);

                _localRotationField.RemoveFromClassList(RecordingFieldClass);
                _worldRotationField.RemoveFromClassList(RecordingFieldClass);

                _addedRotationAnimatorStateIndicatorClasses = false;
            }
        }

        bool _addedScaleAnimatorStateIndicatorClasses;

        void UpdateAnimatorState_ScaleFields()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
            {
                if (!_addedScaleAnimatorStateIndicatorClasses) return;

                _localScaleField.RemoveFromClassList(AnimatedFieldClass);
                _worldScaleField.RemoveFromClassList(AnimatedFieldClass);

                _localScaleField.RemoveFromClassList(RecordingFieldClass);
                _worldScaleField.RemoveFromClassList(RecordingFieldClass);

                _addedScaleAnimatorStateIndicatorClasses = false;

                return;
            }

            _animatorStateIndicatorScale ??= _boundLocalScaleField.Q<FloatField>().Children().ElementAt(1);

            bool isLocalScaleAnimated = AnimationMode.IsPropertyAnimated(target, ScaleProperty);

            if (isLocalScaleAnimated)
            {
                if (_animatorStateIndicatorScale != null &&
                    _animatorStateIndicatorScale.ClassListContains(AnimationRecordingUssClass))
                {
                    _localScaleField.RemoveFromClassList(AnimatedFieldClass);
                    _worldScaleField.RemoveFromClassList(AnimatedFieldClass);

                    _localScaleField.AddToClassList(RecordingFieldClass);
                    _worldScaleField.AddToClassList(RecordingFieldClass);
                }
                else
                {
                    _localScaleField.AddToClassList(AnimatedFieldClass);
                    _worldScaleField.AddToClassList(AnimatedFieldClass);

                    _localScaleField.RemoveFromClassList(RecordingFieldClass);
                    _worldScaleField.RemoveFromClassList(RecordingFieldClass);
                }

                _addedScaleAnimatorStateIndicatorClasses = true;
            }
            else
            {
                if (!_addedScaleAnimatorStateIndicatorClasses) return;
                _localScaleField.RemoveFromClassList(AnimatedFieldClass);
                _worldScaleField.RemoveFromClassList(AnimatedFieldClass);

                _localScaleField.RemoveFromClassList(RecordingFieldClass);
                _worldScaleField.RemoveFromClassList(RecordingFieldClass);

                _addedScaleAnimatorStateIndicatorClasses = false;
            }
        }

        #endregion Animator

        #region Adapt to view width

        /// <summary>
        ///     This updates the inspector based off of the size of the inspector.
        /// </summary>
        void SetupViewWidthAdaption()
        {
            if (_betterTransformSettings.ShowSizeFoldout || _betterTransformSettings.ShowSizeInLine)
                SetupViewWidthAdaptionForSize();

            _root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (_betterTransformSettings.logPerformance && _betterTransformSettings.logDetailedPerformance)
                    Debug.Log("Inspector geometry updated.");

                Adapt(evt.newRect.width);
            });
        }

        void SetupViewWidthAdaptionForSize()
        {
            _topGroupBox.schedule.Execute(() =>
            {
                SetupViewWidthAdaptionForSizeMain();
                Adapt(_root.contentRect.width);
            }).ExecuteLater(0);
        }

        void SetupViewWidthAdaptionForSizeMain()
        {
            if (targets.Length != 1) return;
            if (!_betterTransformSettings.ShowSizeFoldout && !_betterTransformSettings.ShowSizeInLine) return;
            if (_refreshSizeButton != null) return;

            _sizeSetupDone = false;
            SetupSize();
        }


        float _inspectorWidth;

        //TODO: Null reference check for everything is a bit paranoid. Change these later. But since this will run in edge case sceneries for 1 frame, one extra if statement even if unnecessary isn't that bad.
        void Adapt(float width)
        {
            _inspectorWidth = width;
            AdaptSizeUI(width);
            AdaptMainInformationLabels(width);
            UpdateToolbarVisibility(width);
        }

        const int WorkSpaceLabelThreshold = 200;

        void AdaptMainInformationLabels(float width)
        {
            if (_positionLabel == null || _rotationLabel == null || _scaleLabelGroupbox == null ||
                _localSpaceLabel == null || _worldSpaceLabel == null)
                return;

            _localSpaceLabel.style.display = width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;
            _worldSpaceLabel.style.display = width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;
            _positionLabel.style.display = width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;
            _rotationLabel.style.display = width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;
            _scaleLabelGroupbox.style.display = width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;
        }


        void AdaptSizeUI(float width)
        {
            if (_sizeLabelGroupBox != null)
                _sizeLabelGroupBox.style.display =
                    width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sizeCenterFoldoutGroup != null)
                _sizeCenterFoldoutGroup.Q<Label>("SizeCenterLabel").style.display =
                    width > WorkSpaceLabelThreshold ? DisplayStyle.Flex : DisplayStyle.None;

            if (_betterTransformSettings.ShowSizeFoldout)
                AdaptSizeUIForFoldout(width);
            else if (_betterTransformSettings.ShowSizeInLine)
                AdaptSizeUIForInLine(width);
        }

        void AdaptSizeUIForInLine(float width)
        {
            if (_pingSelfButton != null && _betterTransformSettings.pingSelfButton)
                _pingSelfButton.style.display = width > 250 ? DisplayStyle.Flex : DisplayStyle.None;

            if (_betterTransformSettings.showSiblingIndex && _siblingIndexLabel != null && transform.parent)
            {
                _siblingIndexLabel.style.display = width > 470 ? DisplayStyle.Flex : DisplayStyle.None;
                _siblingIndex.style.display = width > 210 ? DisplayStyle.Flex : DisplayStyle.None;
            }


            if (_sizeToolbox != null)
            {
                if (width < 135)
                {
                    _sizeToolbox.style.display = DisplayStyle.None;
                    return;
                }

                _sizeToolbox.style.display = DisplayStyle.Flex;
            }


            if (_autoRefreshSizeButton != null)
            {
                _autoRefreshSizeButton.style.maxWidth = width switch
                {
                    < 450 => 8,
                    < 550 => 30,
                    _ => 80
                };
            }

            // ApplyResponsiveComboStyle(width, _refreshSizeButton, RefreshThreshold);
            ApplyResponsiveComboStyle(width, _rendererSizeButton, 440);
            ApplyResponsiveComboStyle(width, _filterSizeButton, 440);
            ApplyResponsiveComboStyle(width, _hierarchySizeButton, 405);
            ApplyResponsiveComboStyle(width, _selfSizeButton, 405);

            // Unit Selection
            if (_unitDropDownField == null) return;
            int threshold = _betterTransformSettings?.ShowSizeInLine ?? false
                ? UnitFieldInlineThreshold
                : UnitFieldDefaultThreshold;

            if (width > threshold)
                _unitDropDownField.RemoveFromClassList("unity-popup-field-shortened");
            else
                _unitDropDownField.AddToClassList("unity-popup-field-shortened");
        }

        const int UnitFieldInlineThreshold = 295;
        const int UnitFieldDefaultThreshold = 235;

        void AdaptSizeUIForFoldout(float width)
        {
            if (_betterTransformSettings.showSiblingIndex && _siblingIndexLabel != null && transform.parent)
                _siblingIndexLabel.style.display = width > 250 ? DisplayStyle.Flex : DisplayStyle.None;

            if (_pingSelfButton != null && _betterTransformSettings.pingSelfButton)
                _pingSelfButton.style.display = width > 85 ? DisplayStyle.Flex : DisplayStyle.None;

            if (_sizeFoldoutToggle != null)
            {
                _sizeFoldoutToggle.text = width switch
                {
                    < 120 => "Size",
                    < 150 => "",
                    _ => "Size"
                };
            }

            if (_sizeToolbox != null)
            {
                if (width < 120)
                {
                    _sizeToolbox.style.display = DisplayStyle.None;
                    return;
                }

                _sizeToolbox.style.display = DisplayStyle.Flex;
            }


            if (_autoRefreshSizeButton != null)
            {
                _autoRefreshSizeButton.style.maxWidth = width switch
                {
                    < 200 => 9,
                    < 250 => 30,
                    _ => 80
                };
            }


            // ApplyResponsiveComboStyle(width, _refreshSizeButton, RefreshThreshold);
            ApplyResponsiveComboStyle(width, _rendererSizeButton, 360);
            ApplyResponsiveComboStyle(width, _filterSizeButton, 360);
            ApplyResponsiveComboStyle(width, _hierarchySizeButton, 335);
            ApplyResponsiveComboStyle(width, _selfSizeButton, 335);

            // Unit Selection
            if (_unitDropDownField == null) return;
            int threshold = _betterTransformSettings?.ShowSizeInLine ?? false
                ? UnitFieldInlineThreshold
                : UnitFieldDefaultThreshold;

            if (width > threshold)
                _unitDropDownField.RemoveFromClassList("unity-popup-field-shortened");
            else
                _unitDropDownField.AddToClassList("unity-popup-field-shortened");
        }

        static void ApplyResponsiveComboStyle(float width, VisualElement element, int threshold)
        {
            if (element == null) return;
            if (width > threshold)
                ConvertToNormalComboButton(element);
            else
                ConvertToShortComboButton(element);
        }

        static void ConvertToNormalComboButton(VisualElement element)
        {
            if (element == null) return;

            element.AddToClassList("toolbarComboButton-normal");
            element.RemoveFromClassList("toolbarComboButton-shortened");
        }

        static void ConvertToShortComboButton(VisualElement element)
        {
            if (element == null) return;

            element.RemoveFromClassList("toolbarComboButton-normal");
            element.AddToClassList("toolbarComboButton-shortened");
        }

        #endregion Adapt to view width
    }
}