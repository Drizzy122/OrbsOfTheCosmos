using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Rebuilds the animator pieces lost in the crash: VelX/VelZ parameters,
    /// the AimLocomotion state (AimAnims 2D blend tree) and the Dodge state
    /// (DodgeAnims 2D blend tree with 8-way directional clips).
    /// Safe to re-run — existing states are replaced. Saves to disk immediately.
    /// </summary>
    public static class AimDodgeAnimatorRebuild
    {
        const string ControllerPath = "Assets/_Project/Animations/Player/PlayerAnimController.controller";

        [MenuItem("Tools/Animations/Rebuild Aim + Dodge States")]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Rebuild] Controller not found at " + ControllerPath); return; }

            // ── parameters ──
            AddFloatParam(controller, "VelX");
            AddFloatParam(controller, "VelZ");

            var sm = controller.layers[0].stateMachine;

            // ── AimLocomotion / AimAnims ──
            RemoveState(sm, "AimLocomotion");
            var aimTree = NewTree(controller, "AimAnims");
            AddClip(aimTree, "AimIdle", 0, 0);
            AddClip(aimTree, "AimWalkForward", 0, 1);
            AddClip(aimTree, "AimWalkBack", 0, -1);
            AddClip(aimTree, "AimWalkleft", -1, 0, "AimWalkLeft");
            AddClip(aimTree, "Standing Walk Right", 1, 0, "AimWalkRight");

            var aimState = sm.AddState("AimLocomotion", new Vector3(-320f, 180f, 0f));
            aimState.motion = aimTree;
            Debug.Log("[Rebuild] AimLocomotion state rebuilt (" + aimTree.children.Length + " motions)");

            // ── Dodge / DodgeAnims ──
            RemoveState(sm, "Dodge");
            var dodgeTree = NewTree(controller, "DodgeAnims");
            AddClip(dodgeTree, "1H@CombatIdle01", 0, 0, "CombatIdle");
            AddClip(dodgeTree, "Dodge_Air_F", 0, 1);
            AddClip(dodgeTree, "Dodge_Air_B", 0, -1);
            AddClip(dodgeTree, "Dodge_Air_L", -1, 0);
            AddClip(dodgeTree, "Dodge_Air_R", 1, 0);
            AddClip(dodgeTree, "Dodge_Air_F_L_45", -1, 1);
            AddClip(dodgeTree, "Dodge_Air_F_R_45", 1, 1);
            AddClip(dodgeTree, "Dodge_Air_B_L_45", -1, -1);
            AddClip(dodgeTree, "Dodge_Air_B_R_45", 1, -1);

            var dodgeState = sm.AddState("Dodge", new Vector3(-320f, 260f, 0f));
            dodgeState.motion = dodgeTree;
            Debug.Log("[Rebuild] Dodge state rebuilt (" + dodgeTree.children.Length + " motions)");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();   // write to disk NOW — crash-proof
            Debug.Log("[Rebuild] Controller saved to disk.");
        }

        static void AddFloatParam(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
            Debug.Log("[Rebuild] Added parameter " + name);
        }

        static void RemoveState(AnimatorStateMachine sm, string name)
        {
            var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == name);
            if (existing.state != null)
            {
                sm.RemoveState(existing.state);
                Debug.Log("[Rebuild] Removed old state " + name);
            }
        }

        static BlendTree NewTree(AnimatorController controller, string name)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "VelX",
                blendParameterY = "VelZ",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        static void AddClip(BlendTree tree, string clipName, float x, float y, string searchHint = null)
        {
            AnimationClip clip = FindClip(clipName, searchHint);
            if (clip == null)
            {
                Debug.LogWarning("[Rebuild] Clip NOT FOUND: '" + clipName + "' — add it to the tree manually.");
                return;
            }
            tree.AddChild(clip, new Vector2(x, y));
        }

        static AnimationClip FindClip(string exactName, string searchHint)
        {
            // search by exact name first, then by hint (file and clip names can differ)
            foreach (string term in new[] { exactName, searchHint }.Where(t => !string.IsNullOrEmpty(t)))
            {
                foreach (string guid in AssetDatabase.FindAssets(term + " t:AnimationClip"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (obj is AnimationClip clip && !clip.name.Contains("__preview__") && clip.name == exactName)
                            return clip;
                    }
                }
            }
            // last resort: hint search, take any clip whose name contains the hint
            if (!string.IsNullOrEmpty(searchHint))
            {
                foreach (string guid in AssetDatabase.FindAssets(searchHint + " t:AnimationClip"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (obj is AnimationClip clip && !clip.name.Contains("__preview__"))
                            return clip;
                    }
                }
            }
            return null;
        }
    }
}
