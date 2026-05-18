// EmaceArt — EA_CoolWater
// URP 12+ (Unity 2021.2+)
// Low-poly toon water: depth banding, shore glow, toon specular, fresnel,
// refraction (requires Opaque Texture in URP settings), projected + vertex foam.
// Caustics handled separately by CausticsFeature renderer feature.

Shader "EmaceArt/EA_CoolWater"
{
    Properties
    {
        [Header(Colors and Depth)]
        _ShallowColor   ("Shallow Color",    Color)        = (0.20, 0.62, 0.80, 0.65)
        _DeepColor      ("Deep Color",       Color)        = (0.05, 0.22, 0.48, 0.92)
        _WaterDepth     ("Water Depth",      Float)        = 3.0
        _ColorBands     ("Color Bands",      Range(1,8))   = 3
        _ShoreGlowColor ("Shore Glow Color", Color)        = (0.80, 0.96, 1.00, 1.00)
        _ShoreGlowWidth ("Shore Glow Width", Range(0,1))   = 0.15

        [Header(Lighting and Toon)]
        _SpecularColor  ("Specular Color",   Color)        = (1.0, 1.0, 1.0, 1.0)
        _SpecularPower  ("Specular Power",   Range(10,512))= 128
        _SpecularCutoff ("Specular Cutoff",  Range(0,1))   = 0.75
        _SpecularScale  ("Specular Scale",   Range(0,1))   = 0.9
        _FresnelPower   ("Fresnel Power",    Range(0,5))   = 2.0
        _RefractionStr  ("Refraction",       Range(0,0.1)) = 0.02

        [Header(Foam and Shore)]
        _FoamColor      ("Foam Color",       Color)        = (0.95, 0.97, 1.00, 1.00)
        [NoScaleOffset]
        _FoamTex        ("Foam Texture",     2D)           = "white" {}
        _FoamTiling     ("Foam Tiling",      Range(1,5))   = 2.5
        _FoamSpeed      ("Foam Speed",       Range(0,2))   = 0.4
        _FoamDistort    ("Foam Distort",     Range(0,1))   = 0.3
        _FoamCutoff     ("Foam Cutoff",      Range(0,1))   = 0.5
        _FoamDepth      ("Foam Depth (m)",   Range(0,2))   = 0.5

        [Header(Waves Geometry)]
        [NoScaleOffset]
        _NormalMap      ("Normal Map",       2D)           = "bump" {}
        _NormalTiling   ("Normal Tiling",    Float)        = 1.5
        _NormalStrength ("Normal Strength",  Range(0,2))   = 0.5
        _WaveSpeed      ("Wave Speed",       Range(0,2))   = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   WaterVert
            #pragma fragment WaterFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor;
                half4  _DeepColor;
                float  _WaterDepth;
                float  _ColorBands;
                half4  _ShoreGlowColor;
                float  _ShoreGlowWidth;
                half4  _SpecularColor;
                float  _SpecularPower;
                float  _SpecularCutoff;
                float  _SpecularScale;
                float  _FresnelPower;
                float  _RefractionStr;
                half4  _FoamColor;
                float  _FoamTiling;
                float  _FoamSpeed;
                float  _FoamDistort;
                float  _FoamCutoff;
                float  _FoamDepth;
                float  _NormalTiling;
                float  _NormalStrength;
                float  _WaveSpeed;
            CBUFFER_END

            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 vertColor  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 vertColor  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Triple-sine UV warp ───────────────────────────────────────────
            float2 DistortUV(float2 uv, float amount, float t)
            {
                float a = amount * 0.01;
                float b = amount * 0.12;
                uv.y += a * (sin(uv.x * 3.5 + t * 0.35) + sin(uv.x * 4.8 + t * 1.05) + sin(uv.x * 7.3 + t * 0.45)) / 3.0;
                uv.x += b * (sin(uv.y * 4.0 + t * 0.50) + sin(uv.y * 6.8 + t * 0.75) + sin(uv.y * 11.3 + t * 0.20)) / 3.0;
                uv.y += b * (sin(uv.x * 4.2 + t * 0.64) + sin(uv.x * 6.3 + t * 1.65) + sin(uv.x * 8.2 + t * 0.45)) / 3.0;
                return uv;
            }

            // ── Photoshop Overlay blend (branch-free per-component) ───────────
            half3 OverlayBlend(half3 base, half3 blend)
            {
                half3 lo = 2.0h * base * blend;
                half3 hi = 1.0h - 2.0h * (1.0h - base) * (1.0h - blend);
                return lerp(lo, hi, step(0.5h, base));
            }

            // ── Vertex ────────────────────────────────────────────────────────
            Varyings WaterVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // vertColor.b = 1 → frozen (no wave), 0 → animated
                float freeze = 1.0 - saturate(IN.vertColor.b);
                float t      = _Time.y * _WaveSpeed;
                float3 pos   = IN.positionOS.xyz;
                pos.y += (sin(pos.x * 2.0 + t) * 0.5 + sin(pos.z * 1.7 + t * 0.8) * 0.5) * 0.04 * freeze;

                VertexPositionInputs vi = GetVertexPositionInputs(pos);
                OUT.positionCS = vi.positionCS;
                OUT.screenPos  = ComputeScreenPos(vi.positionCS);
                OUT.positionWS = vi.positionWS;
                OUT.vertColor  = IN.vertColor;
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────────
            half4 WaterFrag(Varyings IN) : SV_Target
            {
                // vertColor.g < 0.5 → cave / hidden zone → clip
                // Default unpainted vertex = white (g=1) → always visible
                clip(IN.vertColor.g - 0.5);

                float3 posWS = IN.positionWS;
                float2 scrUV = IN.screenPos.xy / IN.screenPos.w;

                // vertColor.b = 1 → freeze foam animation (independent of waves)
                float freezeWaves = 1.0 - saturate(IN.vertColor.b);

                // ── Depth ─────────────────────────────────────────────────────
                float rawDepth  = SampleSceneDepth(scrUV);
                float eyeDepth  = LinearEyeDepth(rawDepth, _ZBufferParams);
                float fragDepth = IN.screenPos.w;
                float depthDiff = max(0.0, eyeDepth - fragDepth);
                float depthT    = saturate(depthDiff / max(0.001, _WaterDepth));

                // ── Flat face normal (low-poly faceted look) ───────────────────
                float3 flatN = normalize(cross(ddx(posWS), ddy(posWS)));
                if (flatN.y < 0.0) flatN = -flatN;

                // ── Wave normals (dual-layer scroll, world XZ projection) ───────
                float  t      = _Time.y * _WaveSpeed;
                float2 nmBase = posWS.xz * _NormalTiling;
                float3 tnA    = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                                    nmBase       + t * float2( 0.07,  0.05)));
                float3 tnB    = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                                    nmBase * 0.7 + t * float2(-0.04,  0.08)));
                float3 tn     = normalize(tnA + tnB);
                float3 waveN  = normalize(float3(tn.x, tn.z, tn.y));
                float3 blendN = normalize(lerp(flatN, waveN, _NormalStrength * 0.5));

                // High-freq normal for tight specular (avoids giant circle on flat mesh)
                float3 stA   = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                                    nmBase * 2.1 + t * float2( 0.11,  0.07)));
                float3 stB   = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                                    nmBase * 1.7 + t * float2(-0.06,  0.12)));
                float3 st    = normalize(stA + stB);
                float3 specN = normalize(float3(st.x, st.z, st.y));

                // ── Depth banding (toon bands) ────────────────────────────────
                float bands   = max(1.0, _ColorBands);
                float bandedT = floor(depthT * bands) / bands;
                half3 col     = lerp(_ShallowColor.rgb, _DeepColor.rgb, bandedT);

                // ── Shore glow ────────────────────────────────────────────────
                float shoreT = saturate(depthDiff / max(0.001, _ShoreGlowWidth * _WaterDepth));
                col = lerp(_ShoreGlowColor.rgb, col, shoreT);

                // ── Refraction (requires Opaque Texture ON in URP settings) ───
                float2 refOff = blendN.xz * _RefractionStr * (1.0 - depthT * 0.5);
                float2 refUV  = clamp(scrUV + refOff, 0.001, 0.999);
                half3 refCol  = SampleSceneColor(refUV);
                col = lerp(refCol, col, saturate(depthT * 2.0 + 0.3));

                // ── Lighting ──────────────────────────────────────────────────
                Light  mainLight = GetMainLight();
                float3 viewDir   = normalize(GetWorldSpaceViewDir(posWS));

                // Toon specular: binary step on Blinn-Phong
                float3 halfDir  = normalize(mainLight.direction + viewDir);
                float  ndoth    = max(0.0, dot(specN, halfDir));
                float  specRaw  = pow(ndoth, max(1.0, _SpecularPower));
                float  specMask = step(_SpecularCutoff, specRaw);
                col += _SpecularColor.rgb * specMask * _SpecularScale * mainLight.color;

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(blendN, viewDir)), max(0.1, _FresnelPower));

                // ── Alpha ─────────────────────────────────────────────────────
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depthT);
                alpha = saturate(alpha + fresnel * 0.3);

                // ── FOAM ──────────────────────────────────────────────────────
                float foamT = _Time.y * _FoamSpeed * freezeWaves;

                // Projected intersection foam: min of 4 diagonal depth taps (8px)
                float2 ts  = 8.0 / _ScreenParams.xy;
                float  pZ0 = LinearEyeDepth(SampleSceneDepth(scrUV + ts * float2( 1,  1)), _ZBufferParams);
                float  pZ1 = LinearEyeDepth(SampleSceneDepth(scrUV + ts * float2(-1,  1)), _ZBufferParams);
                float  pZ2 = LinearEyeDepth(SampleSceneDepth(scrUV + ts * float2( 1, -1)), _ZBufferParams);
                float  pZ3 = LinearEyeDepth(SampleSceneDepth(scrUV + ts * float2(-1, -1)), _ZBufferParams);
                float  minZ     = min(min(pZ0, pZ1), min(pZ2, pZ3));
                float  projDiff = max(0.0, minZ - fragDepth);
                float  projFoam = 1.0 - saturate(projDiff / max(0.001, _FoamDepth));

                // Surface foam islands: vertColor.r = 0 → foam, 1 → no foam
                float surfWeight = 1.0 - IN.vertColor.r;
                float foamBlend  = saturate(projFoam + surfWeight);

                // Foam texture with UV distortion
                float2 foamUV   = DistortUV(posWS.xz * _FoamTiling, _FoamDistort, foamT);
                float  foamSamp = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).r;
                float  foamMask = step(_FoamCutoff, foamSamp) * foamBlend;

                // Overlay blend foam onto water colour
                half3 foamFinal = OverlayBlend(col, _FoamColor.rgb);
                col   = lerp(col, foamFinal, foamMask);
                alpha = max(alpha, foamMask * _FoamColor.a);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    CustomEditor "EmaceArt.EA_CoolWaterInspector"
    FallBack "Hidden/InternalErrorShader"
}
