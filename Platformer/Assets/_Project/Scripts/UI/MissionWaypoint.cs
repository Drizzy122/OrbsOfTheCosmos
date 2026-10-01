using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Screen-space waypoint icon for mission targets. Shows itself whenever it
    /// has a target to track, walks through the targets array as the player
    /// reaches each one, and hides after the final target. Show()/Hide() stay
    /// public for scripted control (e.g. restarting the route from a quest).
    /// </summary>
    public class MissionWaypoint : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("Add waypoints in order. The last one is the final destination.")]
        [SerializeField] Transform[] targets;
        [Tooltip("How close the player must be to advance to the next target.")]
        [SerializeField] float reachRadius = 3f;
        public Vector3 offset;

        [Header("Player")]
        [Tooltip("Drag the Player transform here so distance is measured from the player, not this object.")]
        [SerializeField] Transform player;

        [Header("UI Toolkit")]
        public PanelRenderer uiDocument;

        // UI Elements queried from the document
        private VisualElement waypointContainer;
        private Label meterLabel;

        private int currentIndex = 0;

        private Transform CurrentTarget
        {
            get
            {
                if (targets == null || targets.Length == 0) return null;
                if (currentIndex >= targets.Length) return null;
                return targets[currentIndex];
            }
        }

        private bool IsOnFinalTarget => targets != null && currentIndex == targets.Length - 1;

        private void Start()
        {
            if (uiDocument != null) uiDocument.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDestroy()
        {
            if (uiDocument != null) uiDocument.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer _, VisualElement root)
        {
            waypointContainer = root.Q<VisualElement>("WaypointContainer");
            meterLabel = root.Q<Label>("MeterLabel");

            // Start hidden; Update reveals it once a target exists and the icon
            // has been positioned. Visibility (not display) keeps the element
            // laid out, so it can be placed correctly before it first appears.
            Hide();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Restart tracking from the first target.</summary>
        public void Show()
        {
            currentIndex = 0;
            SetVisible(true);
        }

        /// <summary>Immediately hide the waypoint without changing the target list.</summary>
        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (waypointContainer == null) return;
            waypointContainer.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        // ── Update ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (waypointContainer == null) return;

            // Nothing to track → stay hidden
            if (CurrentTarget == null)
            {
                Hide();
                return;
            }

            // Use player position for distance; fall back to this transform if unassigned
            Vector3 playerPos = player != null ? player.position : transform.position;

            float dist = Vector3.Distance(CurrentTarget.position, playerPos);

            if (!IsOnFinalTarget)
            {
                // Advance through intermediate targets when close enough
                if (dist <= reachRadius)
                {
                    AdvanceToNextTarget();
                    dist = Vector3.Distance(CurrentTarget.position, playerPos);
                }
            }
            else if (dist <= reachRadius)
            {
                // Final target reached → done tracking
                currentIndex++;
                Hide();
                return;
            }

            // Wait for the container's first layout pass before positioning
            if (float.IsNaN(waypointContainer.layout.width)) return;

            // ── Screen-space projection ───────────────────────────────────────
            float width  = waypointContainer.layout.width;
            float height = waypointContainer.layout.height;

            float minX = width  / 2f;
            float maxX = Screen.width  - minX;
            float minY = height / 2f;
            float maxY = Screen.height - minY;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(CurrentTarget.position + offset);

            // Behind the camera the projection mirrors — flip it back and pin
            // the icon to the screen edge on the target's side.
            if (screenPos.z < 0f)
            {
                screenPos.x = Screen.width  - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
                screenPos.x = screenPos.x < Screen.width / 2f ? minX : maxX;
            }

            // UI Toolkit Y is flipped vs Camera
            screenPos.y = Screen.height - screenPos.y;

            screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);
            screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);

            waypointContainer.style.left = screenPos.x - (width  / 2f);
            waypointContainer.style.top  = screenPos.y - (height / 2f);

            meterLabel.text = Mathf.RoundToInt(dist) + "m";

            // Positioned and tracking → make sure it's on screen
            SetVisible(true);
        }

        private void AdvanceToNextTarget()
        {
            currentIndex++;
            Debug.Log($"[MissionWaypoint] Advanced to target {currentIndex}" +
                      (IsOnFinalTarget ? " (FINAL)" : ""));
        }

        private void OnDrawGizmosSelected()
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;

                Gizmos.color = (i == targets.Length - 1) ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(targets[i].position, reachRadius);

                if (i < targets.Length - 1 && targets[i + 1] != null)
                    Gizmos.DrawLine(targets[i].position, targets[i + 1].position);
            }
        }
    }
}
