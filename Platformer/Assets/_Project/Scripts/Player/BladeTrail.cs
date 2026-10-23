using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Platformer
{
    /// <summary>
    /// Sword trail built from the blade's actual motion: every frame while
    /// emitting it samples the hilt and tip of the equipped weapon's mesh and
    /// stitches them into a fading ribbon. Because it is drawn from the real
    /// sword position, it always matches the animation, whatever its speed.
    /// PlayerCombat adds this to the Player and calls Emit() from the slash
    /// animation events.
    /// </summary>
    public class BladeTrail : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Hot colour along the tip edge. HDR values above 1 bloom under post-processing.")]
        [SerializeField, ColorUsage(true, true)] Color coreColor = new Color(3f, 2.6f, 2f, 1f);
        [Tooltip("Soft colour across the rest of the blade.")]
        [SerializeField, ColorUsage(true, true)] Color glowColor = new Color(1.2f, 0.35f, 2.2f, 1f);
        [Tooltip("Whole-trail brightness multiplier.")]
        [SerializeField, Range(0f, 4f)] float intensity = 1f;
        [Tooltip("Seconds a sample stays visible before it fades out.")]
        [SerializeField] float lifetime = 0.18f;
        [Tooltip("How much the ribbon narrows toward the hilt as it ages (0 = none, 1 = collapses to the tip).")]
        [SerializeField, Range(0f, 1f)] float taper = 0.55f;
        [Tooltip("Where along the blade the ribbon starts: 0 = hand, 1 = tip.")]
        [SerializeField, Range(0f, 0.9f)] float startAlongBlade = 0.2f;
        [Tooltip("Extra ribbon length past the blade tip, in metres.")]
        [SerializeField] float tipExtension = 0.15f;
        [Tooltip("Interpolated points inserted between frames so fast swings stay smooth.")]
        [SerializeField, Range(0, 6)] int smoothing = 3;
        [Tooltip("Optional material (needs vertex colour support, e.g. URP Particles/Unlit). Left empty, an additive one is created.")]
        [SerializeField] Material materialOverride;

        struct Sample
        {
            public Vector3 hilt;
            public Vector3 tip;
            public float time;
        }

        readonly List<Sample> samples = new();
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<Vector2> uvs = new();
        readonly List<int> tris = new();

        PlayerEquipment equipment;
        Transform blade;
        Vector3 hiltLocal, tipLocal;
        GameObject trailObject;
        Mesh mesh;
        MeshRenderer trailRenderer;
        float emitUntil = -1f;
        bool hadSampleLastFrame;

        public bool IsEmitting => Time.time <= emitUntil;

        void Awake()
        {
            equipment = GetComponentInChildren<PlayerEquipment>();

            trailObject = new GameObject("BladeTrail");
            trailObject.hideFlags = HideFlags.DontSave;
            var filter = trailObject.AddComponent<MeshFilter>();
            trailRenderer = trailObject.AddComponent<MeshRenderer>();
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sharedMaterial = materialOverride != null ? materialOverride : CreateMaterial();
            ApplyLook();
            trailRenderer.enabled = false;

            mesh = new Mesh { name = "BladeTrail" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }

        void OnDestroy()
        {
            if (trailObject != null) Destroy(trailObject);
            if (mesh != null) Destroy(mesh);
        }

        void OnValidate()
        {
            if (Application.isPlaying && trailRenderer != null) ApplyLook();
        }

        // Pushes the inspector colours into the material (only when it is ours, not an override).
        void ApplyLook()
        {
            if (materialOverride != null || trailRenderer == null) return;
            var m = trailRenderer.sharedMaterial;
            if (m == null) return;
            if (m.HasProperty("_CoreColor")) m.SetColor("_CoreColor", coreColor);
            if (m.HasProperty("_GlowColor")) m.SetColor("_GlowColor", glowColor);
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
        }

        /// <summary>Draw the trail for the next few seconds (call when a swing starts).</summary>
        public void Emit(float seconds)
        {
            emitUntil = Time.time + Mathf.Max(0.02f, seconds);
            ResolveBlade();
        }

        public void Stop() => emitUntil = -1f;

        // Finds the equipped weapon's mesh and the hilt/tip points in its local space:
        // the blade runs along the mesh's longest axis, and the tip is the end
        // furthest from the mesh origin (the hand).
        void ResolveBlade()
        {
            blade = null;
            if (equipment == null) return;
            var visual = equipment.GetEquippedVisual(equipment.ActiveWeaponSlot);
            if (visual == null) return;

            var filter = visual.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            Bounds b = filter.sharedMesh.bounds;
            Vector3 e = b.extents;
            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            float extent = Mathf.Max(e.x, Mathf.Max(e.y, e.z));

            Vector3 endA = b.center + axis * extent;
            Vector3 endB = b.center - axis * extent;
            Vector3 tip = endA.sqrMagnitude >= endB.sqrMagnitude ? endA : endB;
            Vector3 hilt = tip == endA ? endB : endA;

            // Extend the tip a little in world units (convert through the lossy scale).
            float worldPerLocal = Mathf.Max(0.0001f, filter.transform.lossyScale.magnitude / Mathf.Sqrt(3f));
            Vector3 dir = (tip - hilt).normalized;
            tipLocal = tip + dir * (tipExtension / worldPerLocal);
            hiltLocal = Vector3.Lerp(hilt, tip, startAlongBlade);
            blade = filter.transform;
        }

        void LateUpdate()
        {
            bool emitting = IsEmitting;

            if (emitting)
            {
                if (blade == null) ResolveBlade();
                if (blade != null)
                {
                    AddSample(blade.TransformPoint(hiltLocal), blade.TransformPoint(tipLocal));
                    hadSampleLastFrame = true;
                }
            }
            else
            {
                hadSampleLastFrame = false;
            }

            float cutoff = Time.time - lifetime;
            while (samples.Count > 0 && samples[0].time < cutoff) samples.RemoveAt(0);

            if (samples.Count < 2)
            {
                trailRenderer.enabled = false;
                return;
            }

            BuildMesh();
            trailRenderer.enabled = true;
        }

        void AddSample(Vector3 hilt, Vector3 tip)
        {
            float now = Time.time;
            int n = samples.Count;

            // Catmull-Rom between the previous two samples and this one keeps the
            // ribbon curved instead of faceted when the blade covers a lot of arc in one frame.
            if (hadSampleLastFrame && smoothing > 0 && n >= 2)
            {
                Sample p0 = samples[n - 2], p1 = samples[n - 1];
                Vector3 p3h = tip + (tip - p1.tip);   // extrapolated next point
                Vector3 p3t = hilt + (hilt - p1.hilt);
                for (int i = 1; i <= smoothing; i++)
                {
                    float t = i / (float)(smoothing + 1);
                    samples.Add(new Sample
                    {
                        hilt = CatmullRom(p0.hilt, p1.hilt, hilt, p3t, t),
                        tip = CatmullRom(p0.tip, p1.tip, tip, p3h, t),
                        time = Mathf.Lerp(p1.time, now, t),
                    });
                }
            }

            samples.Add(new Sample { hilt = hilt, tip = tip, time = now });
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void BuildMesh()
        {
            verts.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();

            int n = samples.Count;
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                var s = samples[i];
                float age = Mathf.Clamp01((now - s.time) / lifetime);
                float alpha = Mathf.Pow(1f - age, 1.5f);
                float u = 1f - age;   // the shader reads 1 = newest, 0 = oldest

                // Older samples narrow toward the tip, so the tail thins out.
                Vector3 hilt = Vector3.Lerp(s.hilt, s.tip, taper * age);
                Color c = new Color(1f, 1f, 1f, alpha);

                verts.Add(hilt); verts.Add(s.tip);
                colors.Add(c * new Color(0.7f, 0.7f, 0.7f, 1f)); colors.Add(c);   // dimmer near the hand
                uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(u, 1f));

                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        static Material CreateMaterial()
        {
            // The material asset in Resources keeps the ribbon shader in builds
            // (a shader only found by name gets stripped). Copy it so the inspector
            // colours never write back into the asset.
            var asset = Resources.Load<Material>("VFX/BladeTrail");
            if (asset != null) return new Material(asset) { name = "BladeTrail (runtime)" };

            // Editor-only fallback: the shader by name.
            var trailShader = Shader.Find("Orbs/Blade Trail");
            if (trailShader != null) return new Material(trailShader) { name = "BladeTrail (runtime)" };

            // Fallback: a plain additive particle material if the shader failed to compile.
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = "BladeTrail (runtime fallback)" };

            // Additive, double-sided, no depth write — a classic glowing sword trail.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAMODULATE_ON");
            m.SetColor("_BaseColor", Color.white);
            m.renderQueue = (int)RenderQueue.Transparent + 10;
            return m;
        }
    }
}
