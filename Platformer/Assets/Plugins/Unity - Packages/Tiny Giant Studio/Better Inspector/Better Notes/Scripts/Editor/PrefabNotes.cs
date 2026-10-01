using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    [FilePath("ProjectSettings/BetterInspector PrefabNotes.asset", FilePathAttribute.Location.ProjectFolder)]
    public class PrefabNotes : ScriptableSingleton<PrefabNotes>
    {
        public List<Note> notes = new();


        public void DeleteNote(string id)
        {
            Note noteToDelete = MyNote(id);

            if (noteToDelete == null) return;
            Undo.RecordObject(this, "Delete Note");
            notes.Remove(noteToDelete);
            Save(true);
        }

        public Note MyNote(string id) => notes.FirstOrDefault(note => note.id == id);

        public void SetNote(string id, string note)
        {
            Note targetNote = GetNote(id);
            if (targetNote != null)
            {
                targetNote.note = note;
                Save(true);
            }
            else
            {
                notes.Add(new Note(id, note, NoteType.Tooltip, Color.white, Color.gray, true));
            }
        }

        public void SetNote(string id, NoteType noteType)
        {
            foreach (Note t in notes.Where(t => t.id == id))
            {
                t.noteType = noteType;
                Save(true);
                return;
            }

            notes.Add(new(id, "", noteType, Color.white, Color.gray, true));
        }

        public void SetNoteShowInScene(string id, bool showInScene)
        {
            foreach (Note t in notes.Where(t => t.id == id))
            {
                t.showInSceneView = showInScene;
                Save(true);
                return;
            }

            notes.Add(new(id, "", NoteType.Tooltip, Color.white, Color.gray, showInScene));
        }

        public void SetNoteShowInPrefabInstance(string id, bool showInPrefabInstances)
        {
            foreach (Note t in notes.Where(t => t.id == id))
            {
                t.showInPrefabInstances = showInPrefabInstances;
                Save(true);
                return;
            }

            notes.Add(new(id, "", NoteType.Tooltip, Color.white, Color.gray, showInPrefabInstances));
        }

        public void SetNote(string id, Color textColor, Color backgroundColor)
        {
            foreach (Note t in notes.Where(t => t.id == id))
            {
                t.textColor = textColor;
                t.backgroundColor = backgroundColor;
                Save(true);
                return;
            }

            notes.Add(new(id, "", NoteType.Tooltip, textColor, backgroundColor, true));
        }

        public Note GetNote(string id)
        {
            foreach (Note t in notes.Where(t => t.id == id))
                return t;

            return null;
        }


        public void Save() => Save(true);
    }
}