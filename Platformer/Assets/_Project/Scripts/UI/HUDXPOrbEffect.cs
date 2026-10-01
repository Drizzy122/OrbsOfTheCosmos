using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// When an XP orb is collected in the world, spawns a small gold dot on the HUD
    /// at the orb's screen position and flies it into the XP bar. The XP is granted
    /// when the dot ARRIVES, so the bar visibly ticks up as dots land.
    /// Attach to the HUD GameObject (same one as HUDStatsController).
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class HUDXPOrbEffect : MonoBehaviour
    {
        static HUDXPOrbEffect instance;

        [SerializeField] PanelRenderer document;
        [SerializeField] float flightDuration = 0.55f;
        [SerializeField] float dotSize = 14f;

        VisualElement root;
        VisualElement xpContainer;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable()
        {
            instance = this;
            document.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            document.UnregisterUIReloadCallback(OnUIReload);
            if (instance == this) instance = null;
        }

        void OnUIReload(PanelRenderer _, VisualElement newRoot)
        {
            root = newRoot;
            xpContainer = root.Q<VisualElement>("XPContainer");
        }

        /// <summary>
        /// Fly a dot from a world position into the XP bar, granting the XP on arrival.
        /// Returns false when the effect can't run (no HUD / no camera) —
        /// the caller must grant the XP directly instead.
        /// </summary>
        public static bool TryFly(Vector3 worldPosition, int xpAmount)
        {
            if (instance == null || instance.root == null || instance.root.panel == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;

            instance.SpawnDot(worldPosition, xpAmount, cam);
            return true;
        }

        void SpawnDot(Vector3 worldPosition, int xpAmount, Camera cam)
        {
            Vector2 start = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, worldPosition, cam);
            Vector2 target = xpContainer != null
                ? xpContainer.worldBound.center
                : new Vector2(160f, 98f);   // roughly where the bar sits, as a fallback

            var dot = new VisualElement();
            dot.pickingMode = PickingMode.Ignore;
            dot.style.position = Position.Absolute;
            dot.style.width = dotSize;
            dot.style.height = dotSize;
            dot.style.borderTopLeftRadius = dotSize;
            dot.style.borderTopRightRadius = dotSize;
            dot.style.borderBottomLeftRadius = dotSize;
            dot.style.borderBottomRightRadius = dotSize;
            dot.style.backgroundColor = new Color(0.84f, 0.67f, 0.25f);   // gold, matches the bar
            dot.style.left = start.x - dotSize * 0.5f;
            dot.style.top = start.y - dotSize * 0.5f;
            root.Add(dot);

            float startTime = Time.unscaledTime;
            dot.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - startTime) / flightDuration);
                float eased = t * t;   // accelerate INTO the bar
                Vector2 pos = Vector2.Lerp(start, target, eased);
                dot.style.left = pos.x - dotSize * 0.5f;
                dot.style.top = pos.y - dotSize * 0.5f;

                if (t >= 1f)
                {
                    dot.RemoveFromHierarchy();
                    GameEventsManager.instance.playerEvents.ExperienceGained(xpAmount);
                    GameEventsManager.instance.miscEvents.XPCollected();
                }
            }).Every(16).Until(() => dot.panel == null);
        }
    }
}
