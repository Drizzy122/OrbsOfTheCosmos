using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Single entry point for scene transitions. Survives the load it performs, so the
    /// overlay stays on screen across the swap instead of dying with the old scene.
    /// Call it as SceneLoader.Load("Game") — it falls back to a plain load when no
    /// loader exists in the scene, so nothing breaks if the object is missing.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("References")]
        [SerializeField] PanelRenderer panel;

        [Header("Pacing")]
        [Tooltip("Shortest time the overlay stays up, so quick loads don't flash it for one frame.")]
        [SerializeField] float minimumDisplayTime = 1.2f;
        [Tooltip("How fast the bar chases real progress, in bar-fractions per second.")]
        [SerializeField] float barFillSpeed = 1.4f;
        [Tooltip("Beat to hold at 100% before the new scene appears.")]
        [SerializeField] float holdAtFullTime = 0.35f;

        [Header("Flavour")]
        [SerializeField, TextArea]
        string[] tips =
        {
            "Hold jump at the apex of a glide to carry momentum further.",
            "Sprinting through a boost ring extends your glide.",
            "Wall climbs refresh your double jump.",
            "Lumin charges refill when you land a combo finisher."
        };

        // Longest single frame the bar animation will honour. Caps load-hitch spikes.
        const float MaxAnimationStep = 0.05f;

        VisualElement panelRoot;
        VisualElement root;
        VisualElement barFill;
        Label percentLabel;
        Label tipLabel;
        Label statusLabel;

        bool isLoading;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            if (panel == null) panel = GetComponent<PanelRenderer>();
            if (panel != null) panel.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDestroy()
        {
            if (panel != null) panel.UnregisterUIReloadCallback(OnUIReload);
            if (Instance == this) Instance = null;
        }

        void OnUIReload(PanelRenderer _, VisualElement uiRoot)
        {
            panelRoot = uiRoot;
            root = uiRoot.Q<VisualElement>("LoadingRoot");
            barFill = uiRoot.Q<VisualElement>("BarFill");
            percentLabel = uiRoot.Q<Label>("LoadingPercent");
            tipLabel = uiRoot.Q<Label>("LoadingTip");
            statusLabel = uiRoot.Q<Label>("LoadingStatus");

            // A rebuild mid-load must not wipe the overlay off the screen.
            if (root != null && !isLoading) HideOverlay();
        }

        /// <summary>Loads a scene behind the overlay, falling back to a direct load
        /// when no SceneLoader is present.</summary>
        public static void Load(string sceneName)
        {
            // LoadSceneAsync returns null (it does not throw) for a scene missing from the
            // active Build Profile, so check up front rather than dereferencing nothing.
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not in the active Build Profile's " +
                               "scene list. Add it via File > Build Profiles.");
                return;
            }

            if (Instance != null && !Instance.isLoading)
            {
                Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
                return;
            }
            if (Instance == null) SceneManager.LoadScene(sceneName);
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            isLoading = true;

            // The renderer is kept off between loads so this panel is not in UI Toolkit's
            // panel stack at all. Re-enabling rebuilds it, which re-fires OnUIReload and
            // re-queries every element — so wait a frame before touching any of them.
            if (panel != null) panel.enabled = true;
            yield return null;

            ShowOverlay();

            // Let the overlay actually paint at 0% before the load starts hitching the
            // main thread. Without this the first frame anyone sees is already post-stall.
            yield return null;
            yield return null;

            // Everything below runs on unscaled time: loading from a pause menu means
            // Time.timeScale is 0, which would otherwise freeze this coroutine solid.
            float shown = 0f;
            float displayed = 0f;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                // Belt and braces: never leave isLoading stuck true, or every later
                // load would silently no-op behind a permanent overlay.
                Debug.LogError($"[SceneLoader] Could not begin loading '{sceneName}'.");
                isLoading = false;
                HideOverlay();
                yield break;
            }
            op.allowSceneActivation = false;

            // Unity reports 0 -> 0.9 while loading, then parks at 0.9 waiting for
            // activation. Rescaling by 0.9 is what makes the bar mean something.
            while (true)
            {
                // Loading stalls the main thread, so unscaledDeltaTime spikes to whole
                // seconds on the frames that matter. Unclamped, a single spike fills the
                // entire bar in one step and you only ever see 100%. Real elapsed time
                // still drives minimumDisplayTime — only the animation step is capped.
                float step = Mathf.Min(Time.unscaledDeltaTime, MaxAnimationStep);
                shown += Time.unscaledDeltaTime;

                float real = Mathf.Clamp01(op.progress / 0.9f);
                bool ready = op.progress >= 0.9f;
                float target = ready ? 1f : real;

                displayed = Mathf.MoveTowards(displayed, target, barFillSpeed * step);
                Paint(displayed);

                if (ready && displayed >= 1f && shown >= minimumDisplayTime) break;
                yield return null;
            }

            if (statusLabel != null) statusLabel.text = "READY";

            float held = 0f;
            while (held < holdAtFullTime)
            {
                held += Mathf.Min(Time.unscaledDeltaTime, MaxAnimationStep);
                yield return null;
            }

            // The incoming scene should always start running, whatever the old one left behind.
            Time.timeScale = 1f;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // One frame for the new scene's Awake/Start to run before we uncover it.
            yield return null;

            isLoading = false;
            HideOverlay();
        }

        void Paint(float t)
        {
            if (barFill != null) barFill.style.width = Length.Percent(t * 100f);
            if (percentLabel != null) percentLabel.text = Mathf.RoundToInt(t * 100f) + "%";
        }

        void ShowOverlay()
        {
            if (root == null) return;

            // Taking picking back only while the overlay is actually up.
            if (panelRoot != null) panelRoot.pickingMode = PickingMode.Position;

            if (tipLabel != null && tips != null && tips.Length > 0)
            {
                tipLabel.text = tips[Random.Range(0, tips.Length)];
            }
            if (statusLabel != null) statusLabel.text = "LOADING";

            Paint(0f);
            root.style.display = DisplayStyle.Flex;
            root.style.opacity = 1f;
        }

        void HideOverlay()
        {
            if (root == null) return;
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.None;

            // Hiding the content is not enough. This panel is DontDestroyOnLoad at sorting
            // order 200, so its own root outlives every scene and sits above the menu's panel.
            // Left pickable it swallows clicks meant for whatever loaded underneath.
            if (panelRoot != null) panelRoot.pickingMode = PickingMode.Ignore;

            // Picking only governs the mouse. Keyboard and gamepad navigation are routed by
            // panel, so a live panel above the menu can still swallow those. Turning the
            // renderer off takes it out of the stack entirely. Never while a load is running.
            if (!isLoading && panel != null) panel.enabled = false;
        }
    }
}
