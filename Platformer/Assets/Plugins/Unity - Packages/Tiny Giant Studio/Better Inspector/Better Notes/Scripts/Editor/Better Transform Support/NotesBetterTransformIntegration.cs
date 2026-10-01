using TinyGiantStudio.BetterEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector
{
    public class NotesBetterTransformIntegration : Editor
    {
        [SerializeField] VisualTreeAsset visualTreeAsset;

        const string VisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Notes/Scripts/Editor/Better Transform Support/NoteOnTransform.uxml";

        const string VisualTreeAssetGuid = "0afd790708bd3d4478d55bb409317f40";

        [SerializeField] VisualTreeAsset tooltipVisualTreeAsset;

        const string TooltipVisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Notes/Scripts/Editor/Better Transform Support/NoteOnTransform Tooltip.uxml";

        const string TooltipVisualTreeAssetGuid = "abe88efe5840a024589ef4f37c14a6aa";

        [SerializeField] VisualTreeAsset notesGizmoVisualTreeAsset;

        const string NotesGizmoVisualTreeAssetFileLocation =
            "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Notes/Scripts/Editor/Better Transform Support/NoteGizmoSwitch.uxml";

        const string NotesGizmoVisualTreeAssetGuid = "d04bacdd5636bdc42843e4300ac5506a";

        BetterInspectorEditorSettings _betterInspectorEditorSettings;
        NoteSettings _noteSettings;

        public void UpdateSelection()
        {
            if (_betterInspectorEditorSettings == null)
                _betterInspectorEditorSettings = BetterInspectorEditorSettings.instance;
            if (_noteSettings == null) _noteSettings = NoteSettings.instance;

            if (_betterInspectorEditorSettings.SelectedBetterTransformEditorRoot == null) return;

            UpdateNotesUI(_betterInspectorEditorSettings.SelectedBetterTransformEditorRoot,
                _betterInspectorEditorSettings.selectedTransform);
            UpdateGizmoUI(_betterInspectorEditorSettings.SelectedBetterTransformEditorRoot,
                _betterInspectorEditorSettings.selectedTransform);
        }

        void UpdateGizmoUI(VisualElement root, Transform targetTransform)
        {
            if (targetTransform == null) return;
            if (root == null) return;

            GroupBox parent = root.Q<GroupBox>("GizmoGroupBox");
            if (parent == null) return;
            VisualElement notesGizmoGroupbox = parent.Q<VisualElement>("NoteGizmoSwitchContainer");

            if (_noteSettings == null) _noteSettings = NoteSettings.instance;
            if (!_noteSettings.showNotes)
            {
                if (notesGizmoGroupbox != null) notesGizmoGroupbox.style.display = DisplayStyle.None;
                return;
            }

            if (notesGizmoGroupbox != null) notesGizmoGroupbox.style.display = DisplayStyle.Flex;

            if (notesGizmoGroupbox != null) return;

            if (notesGizmoVisualTreeAsset == null)
                notesGizmoVisualTreeAsset = Utility.GetVisualTreeAsset(NotesGizmoVisualTreeAssetFileLocation, NotesGizmoVisualTreeAssetGuid);

            VisualElement noteGizmoSwitchContainer = new()
            {
                name = "NoteGizmoSwitchContainer"
            };
            notesGizmoVisualTreeAsset.CloneTree(noteGizmoSwitchContainer);
            noteGizmoSwitchContainer.schedule
                .Execute(() => { noteGizmoSwitchContainer.Q<GroupBox>().style.opacity = 1f; }).ExecuteLater(100);
            parent.Insert(0, noteGizmoSwitchContainer);

            Button notesGizmoOn = noteGizmoSwitchContainer.Q<Button>("NotesOn");
            Button notesGizmoOff = noteGizmoSwitchContainer.Q<Button>("NotesOff");
            notesGizmoOn.clicked += () =>
            {
                _noteSettings.showNotesGizmo = false;
                UpdateGizmoButton_Note(notesGizmoOn, notesGizmoOff);
                SceneView.RepaintAll();
            };
            notesGizmoOff.clicked += () =>
            {
                _noteSettings.showNotesGizmo = true;
                UpdateGizmoButton_Note(notesGizmoOn, notesGizmoOff);
                SceneView.RepaintAll();
            };
            UpdateGizmoButton_Note(notesGizmoOn, notesGizmoOff);
        }

        #region Update UI

        void UpdateNotesUI(VisualElement root, Transform targetTransform)
        {
            if (targetTransform == null) return;
            if (root == null) return;

            VisualElement noteContainer = root.Q<VisualElement>("NoteContainer");
            VisualElement tooltipContainer = root.Q<VisualElement>("TooltipContainer");

            if (_noteSettings == null) _noteSettings = NoteSettings.instance;
            if (!_noteSettings.showNotes)
            {
                noteContainer?.parent.Remove(noteContainer);
                tooltipContainer?.parent.Remove(tooltipContainer);

                return;
            }

            bool thisIsAnAsset = AssetDatabase.Contains(targetTransform);
            SceneNotesManager sceneNotesManager = SceneNotesManager.Instance;
            PrefabNotes prefabNotes = PrefabNotes.instance;


            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (!thisIsAnAsset && sceneNotesManager == null) return;
            if (thisIsAnAsset && prefabNotes == null) return;

            Note note = thisIsAnAsset
                ? prefabNotes.MyNote(NoteUtility.GetAssetGUID(targetTransform))
                : sceneNotesManager.MyNote(targetTransform);
            if (note == null || note.noteType == NoteType.Hidden || note.noteType == NoteType.Tooltip)
            {
                noteContainer?.parent.Remove(noteContainer);
            }
            else
            {
                if (noteContainer != null)
                    UpdateNote(note, noteContainer, root);
                else
                    CreateNoteUI(root, note);
            }

            if (note is not { noteType: NoteType.Tooltip })
            {
                tooltipContainer?.parent.Remove(noteContainer);
            }
            else
            {
                if (tooltipContainer != null)
                    UpdateNoteTooltip(note, tooltipContainer);
                else
                    CreateNoteTooltipUI(root, note);
            }
        }


        public void UpdateUI(Note note, VisualElement root)
        {
            VisualElement noteContainer = root.Q<VisualElement>("NoteContainer");
            if (note == null || note.noteType == NoteType.Hidden || note.noteType == NoteType.Tooltip)
            {
                noteContainer?.parent.Remove(noteContainer);
            }
            else
            {
                if (noteContainer != null)
                    UpdateNote(note, noteContainer, root);
                else
                    CreateNoteUI(root, note);
            }

            VisualElement tooltipContainer = root.Q<VisualElement>("TooltipContainer");
            if (note is not { noteType: NoteType.Tooltip })
            {
                tooltipContainer?.parent.Remove(tooltipContainer);
            }
            else
            {
                if (tooltipContainer != null)
                    UpdateNoteTooltip(note, tooltipContainer);
                else
                    CreateNoteTooltipUI(root, note);
            }
        }

        void CreateNoteUI(VisualElement root, Note note)
        {
            if (visualTreeAsset == null)
                visualTreeAsset = Utility.GetVisualTreeAsset(VisualTreeAssetFileLocation, VisualTreeAssetGuid);

            VisualElement noteContainer = new()
            {
                name = "NoteContainer"
            };
            visualTreeAsset.CloneTree(noteContainer);
            root.Add(noteContainer);
            UpdateNote(note, noteContainer, root);
            noteContainer.style.marginLeft = -7;
        }

        void UpdateNote(Note note, VisualElement noteContainer, VisualElement root)
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (note.noteType)
            {
                case NoteType.Bottom:
                {
                    if (noteContainer.parent.IndexOf(noteContainer) != noteContainer.parent.childCount - 1)
                        noteContainer.parent.Insert(noteContainer.parent.childCount - 1, noteContainer);
                    break;
                }
                case NoteType.Top:
                {
                    if (noteContainer.parent.IndexOf(noteContainer) != 0)
                        noteContainer.parent.Insert(0, noteContainer);
                    break;
                }
            }

            Button noteButton = noteContainer.Q<Button>("Note");
            noteButton.text = note.note;
            noteButton.schedule
                .Execute(() =>
                {
                    noteButton.style.color = note.textColor;
                    noteButton.style.backgroundColor = note.backgroundColor;
                }).ExecuteLater(100);
            UpdateOnClickAction(noteButton, root);
        }

        void CreateNoteTooltipUI(VisualElement root, Note note)
        {
            if (tooltipVisualTreeAsset == null)
                tooltipVisualTreeAsset = Utility.GetVisualTreeAsset(TooltipVisualTreeAssetFileLocation, TooltipVisualTreeAssetGuid);

            VisualElement noteContainer = new()
            {
                name = "TooltipContainer"
            };
            tooltipVisualTreeAsset.CloneTree(noteContainer);
            GroupBox container = root.Q<GroupBox>("TopGroupBox");
            container.Insert(container.childCount - 2, noteContainer);
            UpdateNoteTooltip(note, noteContainer);

            UpdateOnClickAction(noteContainer.Q<Button>(), root);
        }

        void UpdateOnClickAction(Button b, VisualElement root)
        {
            if (_noteSettings == null)
                return;

            if (_noteSettings.noteClickActions == NoteSettings.NoteClickActions.DoNothing ||
                root.parent.name == "Content")
            {
                b.AddToClassList("uninteractableTooltip");
                b.RemoveFromClassList("interactableTooltip");
            }
            else if (_noteSettings.noteClickActions == NoteSettings.NoteClickActions.OpenNoteEditor)
            {
                b.RemoveFromClassList("uninteractableTooltip");
                b.AddToClassList("interactableTooltip");
                b.clicked += () => { NoteEditor.ShowEditor(); };
            }
        }

        static void UpdateNoteTooltip(Note note, VisualElement tooltipContainer)
        {
            tooltipContainer.tooltip = note.note;
        }

        #endregion

     

        void UpdateGizmoButton_Note(Button notesGizmoOn, Button notesGizmoOff)
        {
            if (!_noteSettings.showNotes)
            {
                notesGizmoOn.style.display = DisplayStyle.None;
                notesGizmoOff.style.display = DisplayStyle.None;
                return;
            }

            if (_noteSettings.showNotesGizmo)
            {
                notesGizmoOn.style.display = DisplayStyle.Flex;
                notesGizmoOff.style.display = DisplayStyle.None;
            }
            else
            {
                notesGizmoOn.style.display = DisplayStyle.None;
                notesGizmoOff.style.display = DisplayStyle.Flex;
            }
        }
    }
}