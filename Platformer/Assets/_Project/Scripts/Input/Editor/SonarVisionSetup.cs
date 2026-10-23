using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Platformer
{
    /// <summary>
    /// One-shot setup for the sonar-scan screen dim: creates the darkened
    /// post-processing profile and adds a SonarVisionVolume (weight 0) to the
    /// PlayGround and Game scenes. Safe to re-run — existing pieces are kept.
    /// </summary>
    public static class SonarVisionSetup
    {
        const string ProfilePath = "Assets/_Project/ScriptableObjects/SonarVisionProfile.asset";

        [MenuItem("Tools/Abilities/Setup Sonar Vision")]
        public static void Run()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);

                // The scan look: darker, drained of color, closed-in edges.
                // Each override must be persisted as a sub-asset of the profile,
                // otherwise the components list empties on the next save.
                var color = profile.Add<ColorAdjustments>();
                color.postExposure.Override(-2.5f);
                color.saturation.Override(-50f);
                color.name = "ColorAdjustments";
                AssetDatabase.AddObjectToAsset(color, profile);

                var vignette = profile.Add<Vignette>();
                vignette.intensity.Override(0.35f);
                vignette.smoothness.Override(0.6f);
                vignette.name = "Vignette";
                AssetDatabase.AddObjectToAsset(vignette, profile);

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SonarVision] Created profile at {ProfilePath}");
            }

            // Active scene first (usually PlayGround), then Game additively.
            Scene active = SceneManager.GetActiveScene();
            if (SetupScene(active, profile)) EditorSceneManager.SaveScene(active);

            if (active.path != "Assets/_Project/Scenes/Game.unity")
            {
                Scene game = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Additive);
                if (SetupScene(game, profile)) EditorSceneManager.SaveScene(game);
                EditorSceneManager.CloseScene(game, true);
            }
        }

        static bool SetupScene(Scene scene, VolumeProfile profile)
        {
            // Already set up?
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<SonarVisionEffect>(true) != null)
                {
                    Debug.Log($"[SonarVision] {scene.name} already has a SonarVisionEffect — skipped.");
                    return false;
                }
            }

            // Parent under the scene's Post-Processing object when there is one.
            Transform parent = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Post-Processing") { parent = root.transform; break; }
            }

            var go = new GameObject("SonarVisionVolume");
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent != null) go.transform.SetParent(parent, false);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;   // above the scene's base profile
            volume.weight = 0f;
            volume.sharedProfile = profile;

            var effect = go.AddComponent<SonarVisionEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("volume").objectReferenceValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[SonarVision] Added SonarVisionVolume to {scene.name}" + (parent != null ? " (under Post-Processing)" : ""));
            return true;
        }
    }
}
