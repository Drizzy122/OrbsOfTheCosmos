using System;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    public static class FindAllTransformEditors
    {
        [MenuItem("Tools/Tiny Giant Studio/Utility/Log All Custom Editors for Transform in Project",false,99999)]
        static void Find()
        {
            FindAllEditorsForComponent.Find(typeof(Transform), 
                "TinyGiantStudio.BetterInspector.BetterTransformEditor", 
                new []{"UnityEditor.TransformInspector"});
        }
    }
}