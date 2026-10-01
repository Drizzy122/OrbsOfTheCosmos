using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Lock-on indicator for the freeflow targeting system. Follows
    /// EnemyDetection.CurrentTarget(): sits above the targeted enemy, GLIDES to the
    /// next enemy when the stick swaps targets, pulses when a target is acquired,
    /// and hides when nothing is targeted.
    /// Attach to the HUD GameObject; drag the player's EnemyDetection in.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class HUDTargetMarker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PanelRenderer document;
        [SerializeField] EnemyDetection enemyDetection;

        [Header("Feel")]
        [Tooltip("How quickly the marker glides between targets (higher = snappier).")]
        [SerializeField] float glideSpeed = 14f;
        [Tooltip("Extra height above the target collider's top, in world units.")]
        [SerializeField] float heightOffset = 0.35f;
        [SerializeField] float pulseDuration = 0.18f;
        [SerializeField] float pulseScale = 1.6f;

        VisualElement marker;
        Transform currentTarget;
        Collider targetCollider;
        Vector2 markerPos;
        float pulseTime = 1f;   // normalized; >= 1 means no pulse running
        bool visible;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable() => document.RegisterUIReloadCallback(OnUIReload);

        void OnDisable() => document.UnregisterUIReloadCallback(OnUIReload);

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            marker = root.Q<VisualElement>("TargetMarker");
            if (marker == null) return;
            visible = true;          // force SetVisible through its early-out
            SetVisible(false);
        }

        void LateUpdate()
        {
            if (marker == null || enemyDetection == null) return;

            var target = enemyDetection.CurrentTarget();
            var cam = Camera.main;

            if (target == null || cam == null)
            {
                currentTarget = null;
                SetVisible(false);
                return;
            }

            // Target changed → cache its collider and pulse the marker.
            if (target != currentTarget)
            {
                bool hadTarget = currentTarget != null;
                currentTarget = target;
                targetCollider = target.GetComponentInChildren<Collider>();
                pulseTime = 0f;

                // Fresh acquire (was hidden): snap to the enemy instead of gliding
                // in from a stale position across the screen.
                if (!hadTarget) markerPos = PanelPositionFor(target, cam);
            }

            Vector3 worldAnchor = WorldAnchorFor(target);

            // Behind the camera → hide (projection would mirror to nonsense).
            if (cam.WorldToViewportPoint(worldAnchor).z <= 0f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            Vector2 goal = RuntimePanelUtils.CameraTransformWorldToPanel(
                marker.panel, worldAnchor, cam);
            markerPos = Vector2.Lerp(markerPos, goal, glideSpeed * Time.deltaTime);

            marker.style.left = markerPos.x - marker.resolvedStyle.width * 0.5f;
            marker.style.top = markerPos.y - marker.resolvedStyle.height * 0.5f;

            // Acquire/swap pulse: scale eases from pulseScale back to 1.
            if (pulseTime < 1f)
            {
                pulseTime = Mathf.Min(1f, pulseTime + Time.deltaTime / pulseDuration);
                float s = Mathf.Lerp(pulseScale, 1f, pulseTime);
                marker.style.scale = new Scale(new Vector2(s, s));
            }
        }

        Vector3 WorldAnchorFor(Transform target)
        {
            if (targetCollider != null)
            {
                var bounds = targetCollider.bounds;
                return new Vector3(bounds.center.x, bounds.max.y + heightOffset, bounds.center.z);
            }
            return target.position + Vector3.up * (2f + heightOffset);
        }

        Vector2 PanelPositionFor(Transform target, Camera cam)
        {
            return RuntimePanelUtils.CameraTransformWorldToPanel(
                marker.panel, WorldAnchorFor(target), cam);
        }

        void SetVisible(bool show)
        {
            if (visible == show) return;
            visible = show;
            marker.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
