using TinyGiantStudio.BetterEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector
{
    public class NoteEditor : EditorWindow
    {
        [SerializeField] VisualTreeAsset visualTreeAsset;

        const string VisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Notes/Scripts/Editor/Note Editor/NoteEditor.uxml";

        const string VisualTreeAssetGuid = "741b910dfc93af7438e66a985f0b2e65";

        SceneNotesManager _sceneNotesManager;

        VisualElement _root;

        Transform _targetTransform;
        VisualElement _targetRoot;

        TextField _noteTextField;
        ColorField _noteTextColorField;
        ColorField _noteBackgroundColorField;
        Toggle _showInSceneToggle;
        EnumField _noteTypeField;
        Toggle _showInPrefabInstancesToggle;
        GroupBox _prefabInformation;
        Label _prefabNoteLabel;
        Button _prefabNoteCopy;
        ObjectField _targetField;

        Note _note;

        BetterInspectorEditorSettings _editorSettings;
        bool _thisIsAnAsset;

        PrefabNotes _prefabNotes;
        NotesBetterTransformIntegration _transformIntegration;

        [MenuItem("Tools/Tiny Giant Studio/Edit Notes", false, 1)]
        // ReSharper disable once Unity.IncorrectMethodSignature
        public static NoteEditor ShowEditor()
        {
            NoteEditor noteEditorWindow = GetWindow<NoteEditor>();
            noteEditorWindow.titleContent = new("Note Editor");
            noteEditorWindow.minSize = new(300, 350);
            return noteEditorWindow;
        }

        public void CreateGUI()
        {
            if (visualTreeAsset == null)
                visualTreeAsset = Utility.GetVisualTreeAsset(VisualTreeAssetFileLocation, VisualTreeAssetGuid);
            
            _root = visualTreeAsset.Instantiate();
            rootVisualElement.Add(_root);
            rootVisualElement.hierarchy[0].style.flexGrow = 1;

            GetRandomTip();

            _targetField = _root.Q<ObjectField>("Target");
            _targetField.SetEnabled(false);

            _noteTextField = _root.Q<TextField>("NoteText");
            _noteTextField.RegisterValueChangedCallback(e =>
            {
                if (_targetTransform == null) return;

                if (_thisIsAnAsset) _prefabNotes.SetNote(NoteUtility.GetAssetGUID(_targetTransform), e.newValue);
                else _sceneNotesManager.SetNote(_targetTransform, e.newValue);

                _note.note = e.newValue;

                if (_targetRoot == null)
                    return;

                if (_transformIntegration == null)
                    _transformIntegration = CreateInstance<NotesBetterTransformIntegration>();

                _transformIntegration.UpdateUI(_note, _targetRoot);
            });

            _noteTextColorField = _root.Q<ColorField>("NoteTextColor");
            _noteTextColorField.RegisterValueChangedCallback(e =>
            {
                if (_targetTransform == null) return;
                if (_note == null) return;

                _note.textColor = e.newValue;

                if (_thisIsAnAsset) _prefabNotes.SetNote(NoteUtility.GetAssetGUID(_targetTransform), _note.textColor, _note.backgroundColor);
                else _sceneNotesManager.SetNote(_targetTransform, _note.textColor, _note.backgroundColor);

                if (_targetRoot == null)
                    return;

                if (_transformIntegration == null)
                    _transformIntegration = CreateInstance<NotesBetterTransformIntegration>();

                _transformIntegration.UpdateUI(_note, _targetRoot);
            });

            _noteBackgroundColorField = _root.Q<ColorField>("NoteBackgroundColor");
            _noteBackgroundColorField.RegisterValueChangedCallback(e =>
            {
                if (_targetTransform == null) return;
                if (_note == null) return;

                _note.backgroundColor = e.newValue;

                if (_thisIsAnAsset) _prefabNotes.SetNote(NoteUtility.GetAssetGUID(_targetTransform), _note.textColor, _note.backgroundColor);
                else _sceneNotesManager.SetNote(_targetTransform, _note.textColor, _note.backgroundColor);

                if (_targetRoot == null)
                    return;

                if (_transformIntegration == null)
                    _transformIntegration = CreateInstance<NotesBetterTransformIntegration>();

                _transformIntegration.UpdateUI(_note, _targetRoot);
            });

            _showInSceneToggle = _root.Q<Toggle>("ShowInSceneToggle");
            _showInSceneToggle.RegisterValueChangedCallback(e =>
            {
                if (_targetTransform == null) return;
                if (_note == null) return;

                _note.showInSceneView = e.newValue;

                if (_thisIsAnAsset) _prefabNotes.SetNoteShowInScene(NoteUtility.GetAssetGUID(_targetTransform), e.newValue);
                else _sceneNotesManager.SetNote(_targetTransform, e.newValue);
            });

            _noteTypeField = _root.Q<EnumField>("NoteTypeField");
            _noteTypeField.RegisterValueChangedCallback(e =>
            {
                if (_targetTransform == null) return;

                if (_thisIsAnAsset) _prefabNotes.SetNote(NoteUtility.GetAssetGUID(_targetTransform), (NoteType)e.newValue);
                else _sceneNotesManager.SetNote(_targetTransform, (NoteType)e.newValue);

                _note.noteType = (NoteType)e.newValue;

                if (_targetRoot == null)
                    return;

                if (_transformIntegration == null)
                    _transformIntegration = CreateInstance<NotesBetterTransformIntegration>();

                _transformIntegration.UpdateUI(_note, _targetRoot);
            });

            _showInPrefabInstancesToggle = _root.Q<Toggle>("ShowInPrefabInstancesToggle");
            _showInPrefabInstancesToggle.RegisterValueChangedCallback(e =>
            {
                if (_targetTransform == null) return;

                _prefabNotes.SetNoteShowInPrefabInstance(NoteUtility.GetAssetGUID(_targetTransform), e.newValue);
            });

            _prefabInformation = _root.Q<GroupBox>("PrefabInformation");
            _prefabNoteLabel = _root.Q<Label>("PrefabNote");
            _prefabNoteCopy = _root.Q<Button>("CopyPrefabNote");
            _prefabNoteCopy.clicked += () =>
            {
                if (_targetTransform == null) return;
                if (_sceneNotesManager == null) return;

                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(_targetTransform.gameObject);
                if (prefab == null) return;

                Note prefabNote = _prefabNotes.MyNote(NoteUtility.GetAssetGUID(prefab.transform));
                _sceneNotesManager.SetNote(_targetTransform, prefabNote);

                _note = _sceneNotesManager.MyNote(_targetTransform);
                _noteTextField.SetValueWithoutNotify(_note.note);
                _noteTextColorField.SetValueWithoutNotify(_note.textColor);
                _noteBackgroundColorField.SetValueWithoutNotify(_note.backgroundColor);
                _showInSceneToggle.SetValueWithoutNotify(_note.showInSceneView);
                _noteTypeField.SetValueWithoutNotify(_note.noteType);

                if (_targetRoot == null)
                    return;

                if (_transformIntegration == null)
                    _transformIntegration = CreateInstance<NotesBetterTransformIntegration>();

                _transformIntegration.UpdateUI(_note, _targetRoot);
            };

            _root.Q<Button>("NoteDeleteButton").clicked += () =>
            {
                if (_thisIsAnAsset)
                    _prefabNotes.DeleteNote(NoteUtility.GetAssetGUID(_targetTransform));
                else
                    _sceneNotesManager.DeleteNote(_targetTransform);


                if (_targetRoot == null)
                    return;

                if (_transformIntegration == null)
                    _transformIntegration = CreateInstance<NotesBetterTransformIntegration>();

                _transformIntegration.UpdateUI(null, _targetRoot);

                Close();
            };

            if (_targetTransform != null && _targetRoot != null)
                UpdateFieldsAfterTargetChange();
            else
                NothingSelected();

            _root.Q<Button>("NoteSettings").clicked += () => { NoteSettingsWindow.ShowEditor(); };

            _root.schedule.Execute(UpdateTarget).Every(100).ExecuteLater(100);
        }

        void UpdateTarget()
        {
            _sceneNotesManager = SceneNotesManager.Instance;
            _editorSettings = BetterInspectorEditorSettings.instance;
            _prefabNotes = PrefabNotes.instance;

            if (_editorSettings == null)
            {
                NothingSelected();
                return;
            }

            Object selected = Selection.activeObject;
            if (selected != null)
            {
                if (selected is not GameObject go) return;
                Transform t = go.transform;

                if (_targetTransform != Selection.activeTransform || !_noteTextField.enabledSelf)
                    UpdateFieldsAfterTargetChange(t);

                return;
            }

            if (_targetRoot == null)
            {
                if (_editorSettings.SelectedBetterTransformEditorRoot == null)
                {
                    NothingSelected();
                    return;
                }
            }

            if (_targetTransform == null)
            {
                if (_editorSettings.selectedTransform == null)
                {
                    NothingSelected();
                    return;
                }
            }


            if (_targetTransform != _editorSettings.selectedTransform || !_noteTextField.enabledSelf)
                UpdateFieldsAfterTargetChange();
        }

        void UpdateFieldsAfterTargetChange(Transform t = null)
        {
            if (_editorSettings.selectedTransform != null && _editorSettings.SelectedBetterTransformEditorRoot != null)
            {
                _targetTransform = _editorSettings.selectedTransform;
                _targetRoot = _editorSettings.SelectedBetterTransformEditorRoot;
            }
            else
            {
                _targetTransform = t;
                _targetRoot = null;
            }

            _targetField.value = _targetTransform;

            _thisIsAnAsset = AssetDatabase.Contains(_targetTransform);
            //Debug.Log(thisIsAnAsset);
            if (_prefabNotes == null) _prefabNotes = PrefabNotes.instance;
            if (_sceneNotesManager == null) _sceneNotesManager = SceneNotesManager.Instance;
            if (_sceneNotesManager == null)
            {
                GameObject sceneNoteManagerObject = new GameObject("Better Transform Scene Notes Manager");
                _sceneNotesManager = sceneNoteManagerObject.AddComponent<SceneNotesManager>();
            }

            if (!_thisIsAnAsset && _sceneNotesManager == null) return;

            _targetField.value = _targetTransform;

            //Try to find existing note
            _note = _thisIsAnAsset ? _prefabNotes.MyNote(NoteUtility.GetAssetGUID(_targetTransform)) : _sceneNotesManager.MyNote(_targetTransform);
            //If no note exists, create one
            _note ??= new("", NoteType.Tooltip, Color.white, Color.gray, true);

            _noteTextField.SetValueWithoutNotify(_note.note);
            _noteTextField.SetEnabled(true);

            _noteTextColorField.SetValueWithoutNotify(_note.textColor);
            _noteTextColorField.SetEnabled(true);

            _noteBackgroundColorField.SetValueWithoutNotify(_note.backgroundColor);
            _noteBackgroundColorField.SetEnabled(true);

            _showInSceneToggle.SetValueWithoutNotify(_note.showInSceneView);
            _showInSceneToggle.SetEnabled(true);

            _noteTypeField.SetValueWithoutNotify(_note.noteType);
            _noteTypeField.SetEnabled(true);

            if (_thisIsAnAsset)
            {
                _showInPrefabInstancesToggle.style.display = DisplayStyle.Flex;
                _showInPrefabInstancesToggle.SetValueWithoutNotify(_note.showInPrefabInstances);
                _showInPrefabInstancesToggle.SetEnabled(true);

                _prefabInformation.style.display = DisplayStyle.None;
            }
            else
            {
                _showInPrefabInstancesToggle.style.display = DisplayStyle.None;

                if (_targetTransform == null) return;
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(_targetTransform.gameObject);
                if (prefab != null)
                {
                    Note prefabNote = _prefabNotes.MyNote(NoteUtility.GetAssetGUID(prefab.transform));
                    if (prefabNote != null)
                    {
                        _prefabInformation.style.display = DisplayStyle.Flex;
                        _prefabInformation.SetEnabled(true);
                        _prefabNoteLabel.text = prefabNote.note;
                        _prefabNoteCopy.SetEnabled(true);
                    }
                    else
                        _prefabInformation.style.display = DisplayStyle.None;
                }
                else
                    _prefabInformation.style.display = DisplayStyle.None;
            }
        }

        void NothingSelected()
        {
            _noteTextField.SetEnabled(false);
            _noteTextColorField.SetEnabled(false);
            _noteBackgroundColorField.SetEnabled(false);
            _showInSceneToggle.SetEnabled(false);
            _noteTypeField.SetEnabled(false);
            _showInPrefabInstancesToggle.SetEnabled(false);
            _prefabInformation.SetEnabled(false);
            _prefabNoteCopy.SetEnabled(false);
        }


        void GetRandomTip()
        {
            rootVisualElement.Q<Label>("Tip").text = "Tip: " + _tips[Random.Range(0, _tips.Length)];
        }

        readonly string[] _tips =
        {
            "You need to save the scene to save the changes to the note, unless it's a prefab.",
            "If you delete an object with note, the note will be deleted after Unity editor restarts. This is to support Undo."
        };
    }
}