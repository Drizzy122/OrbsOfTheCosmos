using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Spins the character-screen dummy by dragging on the preview.
    ///
    /// Turns the model rather than orbiting the camera: the dummy is a standalone mesh, so
    /// rotating it is safe, keeps the framing and lighting fixed, and means the character
    /// turns on the spot the way an inspect screen should.
    /// </summary>
    public class CharacterPreviewRotator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Panel showing the preview. Leave empty to use this GameObject's own.")]
        [SerializeField] PanelRenderer document;

        // Was 'pivot' when this orbited the camera; FormerlySerializedAs keeps the existing
        // inspector assignment rather than silently dropping it on the rename.
        [FormerlySerializedAs("pivot")]
        [Tooltip("The preview dummy to spin.")]
        [SerializeField] Transform previewModel;

        [Header("Feel")]
        [Tooltip("Degrees of spin per pixel dragged.")]
        [SerializeField] float dragSensitivity = 0.45f;
        [Tooltip("Seconds for the spin to ease to a stop after release. 0 = stop dead.")]
        [SerializeField, Min(0f)] float spinDamping = 0.12f;
        [Tooltip("Return to the starting angle each time the menu is opened.")]
        [SerializeField] bool resetOnShow = true;

        const string PreviewElementName = "CharacterPreview";

        VisualElement preview;
        Quaternion startRotation;
        float yaw;
        float spinVelocity;
        bool dragging;
        bool captured;

        void Awake()
        {
            if (document == null) document = GetComponent<PanelRenderer>();
            if (previewModel != null) startRotation = previewModel.localRotation;

            if (document != null) document.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDestroy()
        {
            if (document != null) document.UnregisterUIReloadCallback(OnUIReload);
            Unbind();
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            Unbind();

            preview = root.Q<VisualElement>(PreviewElementName);
            if (preview == null) return;

            preview.RegisterCallback<PointerDownEvent>(OnPointerDown);
            preview.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            preview.RegisterCallback<PointerUpEvent>(OnPointerUp);

            if (resetOnShow)
            {
                yaw = 0f;
                spinVelocity = 0f;
                Apply();
            }
        }

        void Unbind()
        {
            if (preview == null) return;
            preview.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            preview.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            preview.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            preview = null;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            dragging = true;
            spinVelocity = 0f;

            // Capture so a drag that leaves the box keeps feeding us moves — without this
            // the model stops turning the instant the cursor crosses the edge.
            preview.CapturePointer(evt.pointerId);
            captured = true;
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging) return;

            // Drag right turns the character's right side toward you, which is what the
            // grab-and-turn gesture implies.
            float delta = -evt.deltaPosition.x * dragSensitivity;
            yaw += delta;
            spinVelocity = delta;

            Apply();
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            dragging = false;
            if (captured)
            {
                preview.ReleasePointer(evt.pointerId);
                captured = false;
            }
            evt.StopPropagation();
        }

        void LateUpdate()
        {
            if (dragging || Mathf.Approximately(spinVelocity, 0f)) return;

            if (spinDamping <= 0f)
            {
                spinVelocity = 0f;
                return;
            }

            // Coast after release. Unscaled, because this menu runs with the game paused.
            yaw += spinVelocity;
            spinVelocity = Mathf.Lerp(spinVelocity, 0f, Time.unscaledDeltaTime / spinDamping);
            if (Mathf.Abs(spinVelocity) < 0.01f) spinVelocity = 0f;

            Apply();
        }

        void Apply()
        {
            if (previewModel == null) return;
            previewModel.localRotation = startRotation * Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
