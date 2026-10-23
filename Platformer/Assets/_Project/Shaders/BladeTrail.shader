// Blade trail ribbon. UV.x runs along the swing (0 = oldest, 1 = newest),
// UV.y across the blade (0 = hilt, 1 = tip). Vertex colour alpha carries the
// per-sample fade from BladeTrail.cs. Additive, premultiplied in the fragment.
Shader "Orbs/Blade Trail"
{
    Properties
    {
        [HDR] _CoreColor("Core (HDR)", Color) = (3, 2.6, 2, 1)
        [HDR] _GlowColor("Glow (HDR)", Color) = (1.2, 0.35, 2.2, 1)
        _CoreWidth("Core Width (toward tip)", Range(0, 1)) = 0.3
        _TipEdge("Tip Edge Glow", Range(0, 3)) = 1.2
        _HiltFade("Hilt Fade", Range(0, 1)) = 0.35
        _StreakFrequency("Streak Frequency", Float) = 26
        _StreakStrength("Streak Strength", Range(0, 1)) = 0.45
        _Erosion("Tail Erosion", Range(0, 1)) = 0.6
        _Intensity("Overall Intensity", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _CoreColor;
            float4 _GlowColor;
            float _CoreWidth;
            float _TipEdge;
            float _HiltFade;
            float _StreakFrequency;
            float _StreakStrength;
            float _Erosion;
            float _Intensity;
            CBUFFER_END

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = saturate(i.uv.x);     // along the swing, 1 = newest
                float v = saturate(i.uv.y);     // across the blade, 1 = tip
                float age = 1.0 - u;

                // Across the blade: fade in from the hilt, a bright core toward
                // the tip, and a thin hot line right on the tip edge.
                float hilt = smoothstep(0.0, max(_HiltFade, 0.001), v);
                float core = smoothstep(1.0 - _CoreWidth, 1.0, v);
                float tipEdge = exp(-pow((1.0 - v) * 16.0, 2.0)) * _TipEdge;

                // Streaks following the swing, broken up with a little noise.
                float wobble = vnoise(float2(v * 6.0, u * 3.0)) * 6.2832;
                float streak = 0.5 + 0.5 * sin(v * _StreakFrequency + wobble);
                streak = lerp(1.0, streak, _StreakStrength);

                // The tail dissolves instead of just dimming.
                float n = vnoise(float2(v * 5.0, u * 9.0));
                float erode = smoothstep(age - 0.25, age + 0.1, n + (1.0 - _Erosion));

                float alpha = i.color.a * hilt * streak * erode;

                float3 col = lerp(_GlowColor.rgb, _CoreColor.rgb, core) + _CoreColor.rgb * tipEdge;
                col *= i.color.rgb * _Intensity;

                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
