// HUD Bar — a UI Toolkit custom shader (URP) that draws a complete stat bar
// procedurally from the element's layout UV: rounded background track, a pale
// "chip" layer that lingers where health used to be, the fill itself with a
// horizontal gradient, segment ticks, a slow travelling shine, a white hit
// flash and a low-health pulse.
//
// Every bar on the HUD (health, stamina, XP, enemy) uses this shader with a
// different material. HUDBarView.cs drives _Fill / _Chip / _Flash / _Low /
// _Aspect per element through MaterialDefinition.
//
// The file mirrors what Shader Graph generates for the "UI" (UI Toolkit)
// material type: same includes, same struct layout, and it hands the vertex and
// fragment work to UITKPass.hlsl, which calls SurfaceDescriptionFunction below.
Shader "UI Toolkit/HUD Bar"
{
    Properties
    {
        _Fill("Fill", Range(0, 1)) = 1
        _Chip("Chip", Range(0, 1)) = 1
        _Flash("Flash", Range(0, 1)) = 0
        _Low("Low", Range(0, 1)) = 0
        _Aspect("Aspect (width / height)", Float) = 12
        _Rounding("Corner Rounding", Range(0, 0.5)) = 0.5
        _Skew("Skew (lean, in bar heights)", Range(-1.5, 1.5)) = 0
        _Gap("Segment Gap (0 = tick marks instead)", Range(0, 0.2)) = 0
        _Segments("Segments", Float) = 10
        _SegmentStrength("Segment Strength", Range(0, 1)) = 0.35
        _ShineSpeed("Shine Speed", Float) = 0.35
        _ShineStrength("Shine Strength", Range(0, 1)) = 0.25
        _FillColor("Fill Color", Color) = (0.86, 0.14, 0.2, 1)
        _FillColorDark("Fill Color Dark", Color) = (0.42, 0.03, 0.09, 1)
        _ChipColor("Chip Color", Color) = (1, 0.86, 0.78, 0.9)
        _BackColor("Background Color", Color) = (0.03, 0.02, 0.09, 0.8)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "isCustomUITKShader" = "true"
        }

        Pass
        {
            Name "Default"

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex uie_custom_vert
            #pragma fragment uie_custom_frag

            // Variants UI Toolkit switches on at draw time.
            #pragma multi_compile_local _ _UIE_FORCE_GAMMA
            #pragma multi_compile_local _ _UIE_TEXTURE_SLOT_COUNT_4 _UIE_TEXTURE_SLOT_COUNT_2 _UIE_TEXTURE_SLOT_COUNT_1
            #pragma multi_compile_local _ _UIE_RENDER_TYPE_SOLID _UIE_RENDER_TYPE_TEXTURE _UIE_RENDER_TYPE_TEXT _UIE_RENDER_TYPE_GRADIENT

            #define UITK_SHADERGRAPH
            #define UITK_AUTOMATIC_OPACITY 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define SHADERPASS_CUSTOM_UI (100)
            #define SHADERPASS SHADERPASS_CUSTOM_UI
            #define VARYINGS_NEED_OPACITY

            // Pre-graph includes, in the order the UI Shader Graph target emits them.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/UIShim.hlsl"

            // --------------------------------------------------
            // Structs (same layout UITKPass.hlsl expects)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;      // .xy = uv, .zw = layoutUV
                uint4 uv4 : TEXCOORD4;       // packed element / clip / texture ids
                float4 uv5 : TEXCOORD5;      // arc outer / inner
                #if UNITY_ANY_INSTANCING_ENABLED
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord4;            // uvClip
                float4 texCoord5;            // typeTexSettings
                float4 texCoord6;            // textCoreLoc.xy, layoutUV.zw
                float4 texCoord7;            // circle
                float4 color;
                float opacity;
                #if UNITY_ANY_INSTANCING_ENABLED
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
                float4 color;
                float4 typeTexSettings;
                float2 textCoreLoc;
                float4 circle;
                float4 uvClip;
                float2 layoutUV;
                float opacity;
                float3 TimeParameters;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord4 : INTERP0;
                float4 texCoord5 : INTERP1;
                float4 texCoord6 : INTERP2;
                float4 texCoord7 : INTERP3;
                float4 color : INTERP4;
                float opacity : INTERP5;
                #if UNITY_ANY_INSTANCING_ENABLED
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                output.texCoord4 = input.texCoord4;
                output.texCoord5 = input.texCoord5;
                output.texCoord6 = input.texCoord6;
                output.texCoord7 = input.texCoord7;
                output.color = input.color;
                output.opacity = input.opacity;
                #if UNITY_ANY_INSTANCING_ENABLED
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                ZERO_INITIALIZE(Varyings, output);
                output.positionCS = input.positionCS;
                output.texCoord4 = input.texCoord4;
                output.texCoord5 = input.texCoord5;
                output.texCoord6 = input.texCoord6;
                output.texCoord7 = input.texCoord7;
                output.color = input.color;
                output.opacity = input.opacity;
                #if UNITY_ANY_INSTANCING_ENABLED
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                return output;
            }

            // --------------------------------------------------
            // Material properties

            CBUFFER_START(UnityPerMaterial)
            float _Fill;
            float _Chip;
            float _Flash;
            float _Low;
            float _Aspect;
            float _Rounding;
            float _Skew;
            float _Gap;
            float _Segments;
            float _SegmentStrength;
            float _ShineSpeed;
            float _ShineStrength;
            float4 _FillColor;
            float4 _FillColorDark;
            float4 _ChipColor;
            float4 _BackColor;
            CBUFFER_END

            // --------------------------------------------------
            // Bar drawing

            // Anti-aliased rounded rectangle mask. p, center and halfSize are in
            // "aspect space": x spans 0.._Aspect, y spans 0..1, so a radius of 0.5
            // is a full semicircle end cap.
            float RoundedMask(float2 p, float2 center, float2 halfSize, float radius, float aa)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p - center) - (halfSize - radius);
                float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                return 1.0 - smoothstep(-aa, aa, dist);
            }

            float4 Over(float4 dst, float4 src)
            {
                float a = src.a + dst.a * (1.0 - src.a);
                float3 rgb = (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / max(a, 1e-4);
                return float4(rgb, a);
            }

            struct SurfaceDescription
            {
                float3 BaseColor;
                float Alpha;
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                float2 uv = IN.layoutUV;                       // (0,0) bottom-left, (1,1) top-right
                float aspect = max(_Aspect, 0.05);

                // Shear in aspect space: a positive skew leans the top edge to the
                // right. The sheared shape is inset so it still fits the quad.
                float shear = (uv.y - 0.5) * _Skew;
                float2 p = float2(uv.x * aspect - shear, uv.y);
                float2 dp = fwidth(p);
                float aa = max(max(dp.x, dp.y), 1e-4);
                float radius = _Rounding;

                float inset = abs(_Skew) * 0.5;
                float trackW = max(aspect - 2.0 * inset, 0.05);
                float trackStart = inset;
                float ux = saturate((p.x - trackStart) / trackW);   // 0..1 along the sheared track

                float fill01 = saturate(_Fill);
                float chip01 = max(saturate(_Chip), fill01);
                float fillW = fill01 * trackW;
                float chipW = chip01 * trackW;

                float backMask = RoundedMask(p, float2(trackStart + trackW * 0.5, 0.5), float2(trackW * 0.5, 0.5), radius, aa);
                float chipMask = RoundedMask(p, float2(trackStart + chipW * 0.5, 0.5), float2(chipW * 0.5, 0.5), radius, aa) * step(0.002, chipW);
                float fillMask = RoundedMask(p, float2(trackStart + fillW * 0.5, 0.5), float2(fillW * 0.5, 0.5), radius, aa) * step(0.002, fillW);

                // Segments: either separated chunks (gap > 0) or thin tick marks.
                float segments = max(_Segments, 1.0);
                float segPos = frac(ux * segments);
                float boundaryIndex = round(ux * segments);
                bool interior = boundaryIndex > 0.5 && boundaryIndex < segments - 0.5;
                float edgeDist = min(segPos, 1.0 - segPos) / segments;  // distance to the nearest boundary, in ux units
                float aaU = aa / trackW;

                float gapMask = 1.0;
                float tick = 0.0;
                if (_Gap > 0.0)
                {
                    gapMask = interior ? smoothstep(_Gap * 0.5 - aaU, _Gap * 0.5 + aaU, edgeDist) : 1.0;
                }
                else
                {
                    float tickHalfWidth = max(aaU * 1.2, 0.004);
                    tick = interior ? 1.0 - smoothstep(0.0, tickHalfWidth, edgeDist) : 0.0;
                }
                tick *= step(1.5, segments);                   // no ticks when _Segments <= 1
                backMask *= gapMask;
                chipMask *= gapMask;
                fillMask *= gapMask;

                // Fill: dark -> bright across the filled part, lighter towards the top,
                // with a soft gloss band in the upper third.
                float gx = saturate(ux / max(fill01, 0.001));
                float3 fillCol = lerp(_FillColorDark.rgb, _FillColor.rgb, gx);
                fillCol *= lerp(0.80, 1.12, smoothstep(0.0, 1.0, uv.y));
                fillCol += smoothstep(0.62, 0.72, uv.y) * 0.08;
                fillCol *= 1.0 - tick * _SegmentStrength;

                // Travelling shine sweep.
                float t = frac(IN.TimeParameters.x * _ShineSpeed);
                float sweepX = lerp(-0.3, 1.3, t);
                float shine = exp(-pow((ux - sweepX) * 9.0, 2.0)) * _ShineStrength;
                fillCol += shine;

                // Low-health pulse: throb toward hot red.
                float pulse = 0.5 + 0.5 * sin(IN.TimeParameters.x * 7.0);
                fillCol = lerp(fillCol, float3(1.0, 0.32, 0.26), _Low * pulse * 0.6);

                // Compose back -> chip -> fill, then the hit flash on the fill only.
                float3 backCol = _BackColor.rgb * (1.0 - tick * _SegmentStrength * 0.5);
                float4 result = float4(backCol, _BackColor.a * backMask);
                result = Over(result, float4(_ChipColor.rgb, _ChipColor.a * chipMask));
                result = Over(result, float4(fillCol, fillMask));
                result.rgb = lerp(result.rgb, float3(1.0, 1.0, 1.0), saturate(_Flash) * fillMask);

                // Honour the element's own alpha (background-color alpha / USS opacity).
                result.a *= IN.color.a;

                SurfaceDescription surface = (SurfaceDescription)0;
                surface.BaseColor = result.rgb;
                surface.Alpha = result.a;
                return surface;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
                output.color = input.color;
                output.uvClip = input.texCoord4;
                output.typeTexSettings = input.texCoord5;
                output.textCoreLoc = input.texCoord6.xy;
                output.layoutUV = input.texCoord6.zw;
                output.circle = input.texCoord7;
                output.opacity = input.opacity;
                output.TimeParameters = _TimeParameters.xyz;
                return output;
            }

            // Vertex / fragment entry points (uie_custom_vert / uie_custom_frag).
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UITKPass.hlsl"
            ENDHLSL
        }
    }
}
