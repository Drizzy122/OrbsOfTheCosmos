using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Drives one shader-backed HUD bar (HUDBar.shader) on a VisualElement.
    /// The fill element always spans its whole track; the shader draws the
    /// background, a pale "chip" layer that lingers where the value used to be,
    /// the smoothed fill, a hit flash and a low-value pulse from the material
    /// properties this class sets. Ticks itself through the element scheduler,
    /// so callers only push target values.
    ///
    /// Each bar gets its own Material instance. Values are written straight to
    /// that material AND re-sent through the element's material definition,
    /// alternating between two definition handles so the style system always
    /// sees a change and re-uploads the property values.
    /// Falls back to animating the element width when no material is available.
    /// </summary>
    public class HUDBarView
    {
        static readonly Dictionary<string, Material> materialCache = new();

        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int ChipId = Shader.PropertyToID("_Chip");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int LowId = Shader.PropertyToID("_Low");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        /// <summary>Loads a material from a Resources folder, cached per path.</summary>
        public static Material LoadMaterial(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (materialCache.TryGetValue(resourcePath, out var cached) && cached != null) return cached;
            var mat = Resources.Load<Material>(resourcePath);
            if (mat == null) Debug.LogWarning($"[HUDBarView] Material not found in Resources: {resourcePath}. The bar will fall back to a plain width fill.");
            else materialCache[resourcePath] = mat;
            return mat;
        }

        /// <summary>How fast the fill chases its target (higher = snappier).</summary>
        public float fillSpeed = 10f;
        /// <summary>Seconds the chip layer waits before draining after a loss.</summary>
        public float chipDelay = 0.4f;
        /// <summary>Chip drain speed in bar-fractions per second.</summary>
        public float chipSpeed = 0.9f;
        /// <summary>Flash fade-out speed.</summary>
        public float flashDecay = 6f;

        readonly VisualElement fill;
        readonly bool hasMaterial;
        readonly Material material;          // this bar's own instance
        MaterialDefinition definitionA;
        MaterialDefinition definitionB;
        bool useA;

        float current = 1f;
        float target = 1f;
        float chip = 1f;
        float chipHold;
        float flash;
        float low;
        float aspect = 12f;
        bool dirty = true;
        bool released;

        public float Current => current;
        public float Target => target;
        public bool HasMaterial => hasMaterial;

        public HUDBarView(VisualElement fillElement, Material sharedMaterial)
        {
            fill = fillElement;

            if (sharedMaterial != null)
            {
                material = new Material(sharedMaterial) { name = sharedMaterial.name + " (bar instance)" };
                if (material.HasProperty(AspectId)) aspect = material.GetFloat(AspectId);
                definitionA = new MaterialDefinition(material);
                definitionB = new MaterialDefinition(material);
                hasMaterial = true;
                Apply();
            }

            fill.RegisterCallback<GeometryChangedEvent>(_ => UpdateAspect());
            fill.schedule.Execute(Tick).Every(16);
        }

        /// <summary>Destroys this bar's material instance. Call before replacing the view.</summary>
        public void Release()
        {
            if (released) return;
            released = true;
            if (material != null) Object.Destroy(material);
        }

        /// <summary>Jump straight to a value with no chip or smoothing.</summary>
        public void Snap(float value)
        {
            value = Mathf.Clamp01(value);
            current = target = chip = value;
            chipHold = 0f;
            dirty = true;
        }

        /// <summary>Set the value the fill should animate towards.</summary>
        public void SetTarget(float value)
        {
            value = Mathf.Clamp01(value);
            if (value < target) chipHold = chipDelay;       // loss: hold the chip, then drain
            else if (value > chip) chip = value;            // gain: the chip never lags behind
            target = value;
            dirty = true;
        }

        /// <summary>White flash over the fill, fading out over a few frames.</summary>
        public void Flash(float amount = 1f)
        {
            flash = Mathf.Max(flash, Mathf.Clamp01(amount));
            dirty = true;
        }

        /// <summary>0 = normal, 1 = full low-value pulse in the shader.</summary>
        public void SetLow(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(low, value)) return;
            low = value;
            dirty = true;
        }

        void Tick(TimerState state)
        {
            if (released) return;
            float dt = Mathf.Min(state.deltaTime / 1000f, 0.1f);

            if (!Mathf.Approximately(current, target))
            {
                current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-fillSpeed * dt));
                if (Mathf.Abs(current - target) < 0.0005f) current = target;
                dirty = true;
            }

            if (chip > current)
            {
                if (chipHold > 0f) chipHold -= dt;
                else chip = Mathf.MoveTowards(chip, current, chipSpeed * dt);
                dirty = true;
            }
            else if (chip < current)
            {
                chip = current;
                dirty = true;
            }

            if (flash > 0f)
            {
                flash = Mathf.MoveTowards(flash, 0f, flashDecay * dt);
                dirty = true;
            }

            if (dirty) Apply();
        }

        void Apply()
        {
            dirty = false;

            if (!hasMaterial)
            {
                fill.style.width = Length.Percent(current * 100f);
                return;
            }

            // Route 1: the material instance itself, read by the shader at draw time.
            material.SetFloat(FillId, current);
            material.SetFloat(ChipId, chip);
            material.SetFloat(FlashId, flash);
            material.SetFloat(LowId, low);
            material.SetFloat(AspectId, aspect);

            // Route 2: the element's material definition. Alternate handles so the
            // style system registers a change and re-uploads the values.
            useA = !useA;
            var def = useA ? definitionA : definitionB;
            def.SetFloat("_Fill", current);
            def.SetFloat("_Chip", chip);
            def.SetFloat("_Flash", flash);
            def.SetFloat("_Low", low);
            def.SetFloat("_Aspect", aspect);
            fill.style.unityMaterial = new StyleMaterialDefinition(def);
        }

        // The shader rounds and skews the bar in pixel terms, so it needs the
        // element's real width/height ratio whenever layout changes.
        void UpdateAspect()
        {
            if (!hasMaterial) return;
            float w = fill.resolvedStyle.width;
            float h = fill.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || h <= 0f) return;
            float newAspect = w / h;
            if (Mathf.Approximately(newAspect, aspect)) return;
            aspect = newAspect;
            dirty = true;
        }
    }
}
