using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Lock-on reticle for the targeting system. Follows EnemyDetection.CurrentTarget():
    /// four corner brackets that fit the enemy's bounds, glide between targets and
    /// snap in on acquire. Soft targets (sphere-cast in the input direction) draw
    /// thin white brackets; a hard lock-on (toggle input) turns them gold and
    /// thicker. When the target leaves the screen, an edge arrow points the way
    /// back to it. Enemy health stays on the enemy's own world-space bar.
    /// HUDStatsController adds this component to the HUD object automatically; the
    /// player's EnemyDetection is found at runtime if not assigned.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class HUDTargetMarker : MonoBehaviour
    {
        const string HardClass = "hud-lock--hard";
        const string AcquireClass = "hud-lock--acquire";
        const string HintOnClass = "hud-lock-hint--on";
        const long AcquireMs = 30;

        [Header("References")]
        [SerializeField] PanelRenderer document;
        [Tooltip("Found on the player automatically when left empty.")]
        [SerializeField] EnemyDetection enemyDetection;

        [Header("Feel")]
        [Tooltip("How quickly the brackets glide between targets (higher = snappier).")]
        [SerializeField] float glideSpeed = 16f;
        [Tooltip("How quickly the brackets resize to the enemy's bounds.")]
        [SerializeField] float sizeGlideSpeed = 12f;
        [Tooltip("Pixels of breathing room around the enemy's bounds.")]
        [SerializeField] float padding = 18f;
        [SerializeField] float minSize = 60f;
        [SerializeField] float maxSize = 340f;
        [Tooltip("Distance from the screen edge for the off-screen arrow.")]
        [SerializeField] float edgeMargin = 56f;

        VisualElement marker;
        VisualElement offscreen;
        VisualElement arrow;
        VisualElement lockHint;
        bool hintOn;

        Transform currentTarget;
        Collider targetCollider;
        Vector2 markerPos;
        Vector2 markerSize;
        bool markerVisible;
        bool offscreenVisible;
        bool hardLock;
        float nextDetectionSearch;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable()
        {
            if (document == null) document = GetComponent<PanelRenderer>();
            if (document != null) document.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            if (document != null) document.UnregisterUIReloadCallback(OnUIReload);
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            marker = root.Q<VisualElement>("TargetMarker");
            offscreen = root.Q<VisualElement>("TargetOffscreen");
            arrow = root.Q<VisualElement>("TargetOffscreenArrow");
            lockHint = root.Q<VisualElement>("LockHint");
            hintOn = true;
            SetHint(false);

            // Force both SetVisible calls through their early-outs.
            markerVisible = true;
            SetMarkerVisible(false);
            offscreenVisible = true;
            SetOffscreenVisible(false);

            currentTarget = null;
            hardLock = false;
            marker?.RemoveFromClassList(HardClass);
        }

        void LateUpdate()
        {
            if (marker == null) return;

            if (enemyDetection == null)
            {
                if (Time.time < nextDetectionSearch) return;
                nextDetectionSearch = Time.time + 1f;
                enemyDetection = FindFirstObjectByType<EnemyDetection>();
                if (enemyDetection == null) return;
            }

            var target = enemyDetection.CurrentTarget();
            var cam = Camera.main;
            var panel = marker.panel;

            if (target == null || cam == null || panel == null)
            {
                ClearTarget();
                return;
            }

            if (target != currentTarget) Acquire(target, cam, panel);

            bool locked = enemyDetection.IsLockedOn;
            if (locked != hardLock)
            {
                hardLock = locked;
                marker.EnableInClassList(HardClass, locked);
                PlayAcquire();
            }

            // Prompt the lock-on button while an enemy is soft-targeted but not locked.
            SetHint(!locked);

            Bounds bounds = TargetBounds(target);
            Vector3 viewport = cam.WorldToViewportPoint(bounds.center);
            bool onScreen = viewport.z > 0f
                            && viewport.x > -0.02f && viewport.x < 1.02f
                            && viewport.y > -0.02f && viewport.y < 1.02f;

            if (!onScreen)
            {
                SetMarkerVisible(false);
                ShowOffscreen(bounds.center, viewport.z < 0f, cam, panel);
                return;
            }

            SetOffscreenVisible(false);

            Rect rect = ProjectBounds(bounds, cam, panel);
            Vector2 goalSize = new Vector2(
                Mathf.Clamp(rect.width + padding * 2f, minSize, maxSize),
                Mathf.Clamp(rect.height + padding * 2f, minSize, maxSize));

            float dt = Time.deltaTime;
            markerPos = Vector2.Lerp(markerPos, rect.center, 1f - Mathf.Exp(-glideSpeed * dt));
            markerSize = Vector2.Lerp(markerSize, goalSize, 1f - Mathf.Exp(-sizeGlideSpeed * dt));

            marker.style.left = markerPos.x - markerSize.x * 0.5f;
            marker.style.top = markerPos.y - markerSize.y * 0.5f;
            marker.style.width = markerSize.x;
            marker.style.height = markerSize.y;
            SetMarkerVisible(true);
        }

        // ── Target bookkeeping ───────────────────────────────────────────────

        void Acquire(Transform target, Camera cam, IPanel panel)
        {
            currentTarget = target;
            targetCollider = target.GetComponentInChildren<Collider>();
            if (targetCollider == null) targetCollider = target.GetComponentInParent<Collider>();

            // Fresh acquire (was hidden): start on the enemy instead of gliding
            // in from a stale position across the screen.
            if (!markerVisible)
            {
                Rect rect = ProjectBounds(TargetBounds(target), cam, panel);
                markerPos = rect.center;
                markerSize = new Vector2(
                    Mathf.Clamp(rect.width + padding * 2f, minSize, maxSize),
                    Mathf.Clamp(rect.height + padding * 2f, minSize, maxSize));
            }

            PlayAcquire();
        }

        void ClearTarget()
        {
            currentTarget = null;
            targetCollider = null;
            SetMarkerVisible(false);
            SetOffscreenVisible(false);
            SetHint(false);
            if (hardLock)
            {
                hardLock = false;
                marker.RemoveFromClassList(HardClass);
            }
        }

        // Snap the brackets in from large: the acquire class has no transition,
        // removing it a frame later plays the ease back to normal size.
        void PlayAcquire()
        {
            if (marker == null) return;
            marker.AddToClassList(AcquireClass);
            marker.schedule.Execute(() => marker.RemoveFromClassList(AcquireClass)).ExecuteLater(AcquireMs);
        }

        Bounds TargetBounds(Transform target)
        {
            if (targetCollider != null) return targetCollider.bounds;
            return new Bounds(target.position + Vector3.up, new Vector3(1.5f, 2f, 1.5f));
        }

        // ── Projection helpers ───────────────────────────────────────────────

        // Panel-space rectangle covering the eight corners of a world-space bounds.
        static Rect ProjectBounds(Bounds bounds, Camera cam, IPanel panel)
        {
            Vector3 min = bounds.min, max = bounds.max;
            float left = float.MaxValue, top = float.MaxValue;
            float right = float.MinValue, bottom = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, corner, cam);
                left = Mathf.Min(left, p.x);
                right = Mathf.Max(right, p.x);
                top = Mathf.Min(top, p.y);
                bottom = Mathf.Max(bottom, p.y);
            }

            return Rect.MinMaxRect(left, top, right, bottom);
        }

        void ShowOffscreen(Vector3 worldPos, bool behindCamera, Camera cam, IPanel panel)
        {
            if (offscreen == null) return;

            Rect panelRect = panel.visualTree.layout;
            Vector2 center = new Vector2(panelRect.width * 0.5f, panelRect.height * 0.5f);
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPos, cam);

            // Behind the camera the projection mirrors — flip it back through the center.
            if (behindCamera) p = center * 2f - p;

            Vector2 dir = p - center;
            if (dir.sqrMagnitude < 1e-3f) dir = Vector2.down;
            dir.Normalize();

            // Intersect the direction with the screen rect inset by the margin.
            float halfW = Mathf.Max(1f, panelRect.width * 0.5f - edgeMargin);
            float halfH = Mathf.Max(1f, panelRect.height * 0.5f - edgeMargin);
            float tx = Mathf.Abs(dir.x) > 1e-4f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-4f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 pos = center + dir * Mathf.Min(tx, ty);

            float w = offscreen.resolvedStyle.width;
            float h = offscreen.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f) w = 40f;
            if (float.IsNaN(h) || h <= 0f) h = 40f;

            offscreen.style.left = pos.x - w * 0.5f;
            offscreen.style.top = pos.y - h * 0.5f;

            // The arrow art points up; panel Y grows downward, so measure the
            // angle from "up" and rotate clockwise.
            if (arrow != null)
            {
                float angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
                arrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }

            SetOffscreenVisible(true);
        }

        // ── Visibility ───────────────────────────────────────────────────────

        void SetMarkerVisible(bool show)
        {
            if (markerVisible == show || marker == null) return;
            markerVisible = show;
            marker.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetOffscreenVisible(bool show)
        {
            if (offscreenVisible == show || offscreen == null) return;
            offscreenVisible = show;
            offscreen.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetHint(bool show)
        {
            if (hintOn == show || lockHint == null) return;
            hintOn = show;
            lockHint.EnableInClassList(HintOnClass, show);
        }
    }
}
