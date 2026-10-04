using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Sets up the standing character-screen dummy: puts it on a layer only the preview
    /// camera renders, and holds it in an idle pose.
    ///
    /// The dummy is a separate mesh-and-animator object rather than the live player, so the
    /// preview never shows the character mid-jump, mid-swing, or leaning into a turn.
    /// </summary>
    public class CharacterPreviewRig : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The preview dummy — mesh and animator only. Leave empty to use this object's first child.")]
        [SerializeField] Transform previewModel;
        [Tooltip("Camera rendering into CharacterPreviewTexture.")]
        [SerializeField] Camera previewCamera;

        [Header("Preview Layer")]
        [Tooltip("Layer only the preview camera renders. Create it in Project Settings > Tags and Layers.")]
        [SerializeField] string previewLayerName = "CharacterPreview";
        [Tooltip("Also hide this layer from every other camera in the scene.")]
        [SerializeField] bool hideFromOtherCameras = true;

        [Header("Pose")]
        [Tooltip("Animator state to hold. Leave empty to let the controller do whatever it does.")]
        [SerializeField] string idleStateName = "Locomotion";
        [Tooltip("Driven to zero so a locomotion blend tree settles on its idle clip.")]
        [SerializeField] string speedParameter = "Speed";

        int previewLayer = -1;

        void Awake()
        {
            if (previewModel == null && transform.childCount > 0)
            {
                previewModel = transform.GetChild(0);
            }

            previewLayer = LayerMask.NameToLayer(previewLayerName);
            if (previewLayer < 0)
            {
                Debug.LogWarning(
                    $"[CharacterPreviewRig] No layer named '{previewLayerName}'. Add it in " +
                    "Project Settings > Tags and Layers, or the dummy shows up in the game view too.", this);
            }

            ApplyLayer();
            ApplyCameraMask();
            PoseIdle();
        }

        void ApplyLayer()
        {
            if (previewLayer < 0 || previewModel == null) return;
            SetLayerRecursively(previewModel);
        }

        void SetLayerRecursively(Transform root)
        {
            root.gameObject.layer = previewLayer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i));
        }

        void ApplyCameraMask()
        {
            if (previewLayer < 0) return;

            // The preview camera sees only the dummy — nothing of the world leaks into the
            // render texture, so the character reads cleanly whatever is behind it.
            if (previewCamera != null) previewCamera.cullingMask = 1 << previewLayer;

            if (!hideFromOtherCameras) return;

            // And every other camera stops seeing the dummy. Without this it stands in the
            // level in plain sight, since it is a real object sitting in the scene.
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam == previewCamera) continue;
                cam.cullingMask &= ~(1 << previewLayer);
            }
        }

        /// <summary>Holds the dummy still. Its animator shares the player's controller, which
        /// would otherwise idle in whatever state the controller defaults to.</summary>
        void PoseIdle()
        {
            if (previewModel == null) return;

            Animator animator = previewModel.GetComponentInChildren<Animator>(true);
            if (animator == null) return;

            animator.applyRootMotion = false;

            if (!string.IsNullOrEmpty(speedParameter) && HasParameter(animator, speedParameter))
            {
                animator.SetFloat(speedParameter, 0f);
            }
            if (!string.IsNullOrEmpty(idleStateName))
            {
                animator.Play(idleStateName, 0, 0f);
            }
        }

        static bool HasParameter(Animator animator, string name)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == name) return true;
            }
            return false;
        }
    }
}
