using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    public static class NoteUtility
    {
        public static string GetUserFriendlyID(Transform prefab)
        {
            return AssetDatabase.Contains(prefab) ? GetAssetGUID(prefab) : GetID(prefab);
        }

        static string GetID(Transform prefab)
        {
            return prefab.
#if UNITY_6000_5_OR_NEWER
                GetEntityId().ToString();
#else
                GetInstanceID().ToString();
#endif
        }

        /// <summary>
        /// Use "if (!AssetDatabase.Contains(prefab))" to choose between GetAssetGUID and GetObjectEntityID
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static string GetAssetGUID(Transform obj)
        {
            if (!AssetDatabase.Contains(obj)) return GetID(obj);
            
            // ReSharper disable once NotAccessedOutParameterVariable
            long localID; //Required in Unity 2022 and earlier
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out localID);
            return guid;
        }


    }
}