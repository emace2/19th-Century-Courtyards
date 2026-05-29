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
        _ShoreGlowWidth ("Shore Glow Width", Range(0, 0.75))= 0.15

        [Header(Lighting and Toon)]
        _SpecularColor  ("Specular Color",   Color)        = (1.0, 1.0, 1.0, 1.0)
        _SpecularPower  ("Specular Size",      Range(10, 25)) = 40
        _SpecularCutoff ("Specular Softness",  Range(0, 0.8))= 0.15
        _SpecularScale  ("Specular Intensity", Range(0, 0.8))= 0.9
        _FresnelPower   ("Fresnel Power",    Range(0, 1.5)) = 2.0
        _RefractionStr  ("Refraction",       Range(0, 0.1)) = 0.02

        [Header(Foam and Shore)]
        [KeywordEnum(Texture, Cloudy, Blobs, Scatter, Cellular)]
        _FoamStyle      ("Foam Style",       Float)        = 1
        _FoamColor      ("Foam Color",       Color)        = (0.95, 0.97, 1.00, 1.00)
        [NoScaleOffset]
        _FoamTex        ("Foam Texture",     2D)           = "white" {}
        _FoamTiling     ("Foam Tiling",      Range(0, 0.1))= 0.25
        _FoamCutoff     ("Foam Cutoff",      Range(0, 1))  = 0.5
        _FoamDepth      ("Foam Depth (m)",   Range(0, 2))  = 0.5
        _FoamSpeed      ("Foam Speed",       Range(0, 0.6))= 0.4

        [Header(Caustics)]
        _CausticsColor    ("Caustics Tint",     Color)         = (1.0, 1.0, 1.0, 1.0)
        _CausticsStrength ("Caustics Strength", Range(0, 0.5)) = 0.25
        _CausticsScale    ("Caustics Scale",    Range(0, 0.1)) = 0.5
        _CausticsSpeed    ("Caustics Speed",    Range(0, 1.2)) = 0.5
        [Toggle(_CHROMATIC_ABERRATION)] _ChromaticAberration ("Chromatic Aberration", Float) = 0

        [Header(Sparkle)]
        _SparkleBoost  ("Sparkle Boost", Range(0, 1)) = 0.0

        [Header(Waves Geometry)]
        [NoScaleOffset]
        _NormalMap      ("Normal Map",       2D)            = "bump" {}
        _NormalTiling   ("Normal Tiling",    Range(0, 0.15))= 0.08
        _NormalStrength ("Normal Strength",  Range(0, 2))   = 0.5
        _WaveSpeed      ("Wave Speed",       Range(0, 0.2)) = 0.5
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
            #pragma shader_feature_local _FOAMSTYLE_TEXTURE _FOAMSTYLE_CLOUDY _FOAMSTYLE_BLOBS _FOAMSTYLE_SCATTER _FOAMSTYLE_CELLULAR
            #pragma shader_feature_local _CHROMATIC_ABERRATION

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
                float  _FoamCutoff;
                float  _FoamDepth;
                float  _FoamSpeed;
                half4  _CausticsColor;
                float  _CausticsStrength;
                float  _CausticsScale;
                float  _CausticsSpeed;
                float  _SparkleBoost;
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

            // ── Reconstruct world position from screen UV + raw depth ─────────
            float3 ReconstructWorldPos(float2 suv, float rawDepth)
            {
                float4 posCS = float4(suv * 2.0 - 1.0, rawDepth, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    posCS.y = -posCS.y;
                #endif
                float4 posWS4 = mul(UNITY_MATRIX_I_VP, posCS);
                return posWS4.xyz / posWS4.w;
            }

            // ── Procedural foam helpers ───────────────────────────────────────
            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float WorleyMinDist(float2 p)
            {
                float2 pi = floor(p);
                float2 pf = frac(p);
                float minD = 1.0;
                for (int wx = -1; wx <= 1; wx++)
                {
                    for (int wy = -1; wy <= 1; wy++)
                    {
                        float2 g = float2(wx, wy);
                        float2 o = float2(
                            frac(sin(dot(pi + g, float2(127.1, 311.7))) * 43758.5),
                            frac(sin(dot(pi + g, float2(269.5, 183.3))) * 12345.6)
                        );
                        minD = min(minD, length(g + o - pf));
                    }
                }
                return minD;
            }

            // Cloudy — soft low-frequency wisps (FBM), animated by t
            float FoamCloudy(float2 p, float t)
            {
                float2 dA = float2( t * 0.45,  t * 0.30);
                float2 dB = float2(-t * 0.32,  t * 0.50);
                float v = VNoise(p * 0.8 + dA) * 0.55
                        + VNoise(p * 1.7 + dB + 3.1) * 0.30
                        + VNoise(p * 3.3 + dA * 0.6 + 7.4) * 0.15;
                return smoothstep(0.42, 0.50, v);     // sharp toon edge
            }

            // Blobs — large organic shapes, hard binary toon edge
            float FoamBlobs(float2 p, float t)
            {
                float2 d = float2(t * 0.18, t * 0.12);
                float v = VNoise(p * 0.45 + d) * 0.60
                        + VNoise(p * 0.95 - d * 0.6 + 2.7) * 0.40;
                return smoothstep(0.47, 0.50, v);     // hard edge
            }

            // Scatter — small high-freq specks
            float FoamScatter(float2 p, float t)
            {
                float2 d = float2(t * 0.85, t * 0.65);
                float v = VNoise(p * 4.5 + d)
                        * (0.6 + 0.4 * VNoise(p * 0.7 - d * 0.4));
                return smoothstep(0.64, 0.67, v);     // hard speckles
            }

            // Cellular — Worley voronoi cells, animated scroll
            float FoamCellular(float2 p, float t)
            {
                float2 d = float2(t * 0.25, t * 0.18);
                float dist = WorleyMinDist(p * 1.2 + d);
                return smoothstep(0.42, 0.36, dist);  // sharp cell interior
            }

            // ── Caustics — organic tongue/streak refraction lines ─────────────
            // Real pool caustics look like irregular bright tongues, NOT a regular
            // diamond grid. We distort the sample position with low-freq noise
            // before computing the wavefront interference — straight ridges bend
            // into organic streaks. 1 - abs(sum) gives bright RIDGES where waves
            // cross zero; pow() sharpens into thin tongues with continuous coverage.
            float Caustic(float2 p, float t)
            {
                float ct = t * 0.55;
                // Low-freq distortion → bends straight ridges into organic tongues
                float2 dist = float2(
                    VNoise(p * 0.35 + ct * 0.08),
                    VNoise(p * 0.35 + ct * 0.10 + 5.1)
                ) - 0.5;
                float2 pd = p + dist * 0.7;
                // Four wave fronts at irrational angles → non-repeating interference
                float w1 = sin(dot(pd, float2( 1.000,  0.000)) * 2.0 + ct * 0.60);
                float w2 = sin(dot(pd, float2( 0.500,  0.866)) * 2.2 + ct * 0.45);
                float w3 = sin(dot(pd, float2(-0.500,  0.866)) * 1.8 + ct * 0.55);
                float w4 = sin(dot(pd, float2( 0.707,  0.707)) * 2.7 + ct * 0.40);
                float v     = (w1 + w2 + w3 + w4) * 0.25;     // -1 … +1
                float ridge = 1.0 - abs(v);                   // 0 (valley) … 1 (ridge)
                ridge       = pow(ridge, 4.0);                // thin tongues
                // Soft flicker for shimmer (not high-freq speckle)
                float flicker = 0.75 + 0.25 * sin(ct * 3.0 + p.x * 1.3 + p.y * 0.9);
                return saturate(ridge * flicker * 1.4);
            }

            // Two-tap union blend — two caustic networks at different scale/phase
            // overlap into a richer organic pattern. max() preserves brightness on
            // both networks rather than carving (min() would erase most of it).
            float CausticBlend(float2 p, float t)
            {
                float cA = Caustic(p,              t);
                float cB = Caustic(p * 1.43 + 2.7, t * 0.78);
                return max(cA, cB);
            }

            float SampleFoamStyled(float2 uv, float t, TEXTURE2D_PARAM(tex, smp))
            {
                #if defined(_FOAMSTYLE_CLOUDY)
                    return FoamCloudy(uv, t);
                #elif defined(_FOAMSTYLE_BLOBS)
                    return FoamBlobs(uv, t);
                #elif defined(_FOAMSTYLE_SCATTER)
                    return FoamScatter(uv, t);
                #elif defined(_FOAMSTYLE_CELLULAR)
                    return FoamCellular(uv, t);
                #else  // _FOAMSTYLE_TEXTURE
                    return SAMPLE_TEXTURE2D(tex, smp, uv).r;
                #endif
            }

            // ── Vertex ────────────────────────────────────────────────────────
            Varyings WaterVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // vertColor.b = 1 (default, unpainted) → animated
                // vertColor.b = 0 (painted black) → frozen (under bridges, ice, etc.)
                float waveAnim = saturate(IN.vertColor.b);
                float t        = _Time.y * _WaveSpeed;
                float3 pos     = IN.positionOS.xyz;
                pos.y += (sin(pos.x * 2.0 + t) * 0.5 + sin(pos.z * 1.7 + t * 0.8) * 0.5) * 0.04 * waveAnim;

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

                // vertColor.b = 1 (default) animates foam; 0 freezes it.
                float foamAnim = saturate(IN.vertColor.b);

                // ── Depth ─────────────────────────────────────────────────────
                // World-space vertical Y: camera-independent — bands don't dance
                // when you rotate / tilt the camera.
                float  rawDepth  = SampleSceneDepth(scrUV);
                float  eyeDepth  = LinearEyeDepth(rawDepth, _ZBufferParams);
                float  fragDepth = IN.screenPos.w;
                float3 sceneWS   = ReconstructWorldPos(scrUV, rawDepth);
                // Discard water pixels where scene is ABOVE the water surface
                // (no cyan halo around protruding objects, no overshoot of basin).
                if (sceneWS.y > posWS.y + 0.05) discard;
                float  depthDiff = max(0.0, posWS.y - sceneWS.y);   // vertical metres
                float  depthT    = saturate(depthDiff / max(0.001, _WaterDepth));

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
                // Dedicated normal sample with FIXED tiling (0.45). Independent of
                // _NormalTiling — at low slider values the lighting normal becomes
                // nearly constant across the surface → no per-pixel variation → no
                // visible distortion. Hard-coded tiling keeps refraction working
                // regardless of how the user sets the lighting normal.
                float2 refNmUV = posWS.xz * 0.45 + _Time.y * _WaveSpeed * float2(0.06, 0.04);
                float3 refTn   = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, refNmUV));
                float2 refOff  = refTn.xy * _RefractionStr * 4.0 * (1.0 - depthT * 0.3);
                float2 refUV   = clamp(scrUV + refOff, 0.001, 0.999);
                half3 refCol   = SampleSceneColor(refUV);
                col = lerp(refCol, col, saturate(depthT * 0.9 - 0.1));

                // ── Caustics — vein refraction network, projected on the floor ─
                // Sampled in sceneWS.xz (ground beneath the water), NOT posWS.xz
                // (water surface) → caustics stick to the terrain like real refracted
                // light. Walk past a rock under the pool and the bright veins crawl
                // over it instead of sliding off the rock edge.
                // Slider _CausticsScale 0..1 remapped to density 0.5..2.5
                // (slider 0 → broad veins, slider 1 → moderate mesh — no over-dense).
                // Mask: low-freq VNoise (also in sceneWS) breaks the sheet into
                // patches: some areas lit, others dark → no "matte film" look.
                // RGB split (Zucconi): three offset samples → chromatic aberration.
                float  causticZone = saturate(1.0 - smoothstep(0.30, 1.00, depthT));
                float  cScale      = lerp(0.5, 2.5, _CausticsScale);
                float2 cP          = sceneWS.xz * cScale;
                float  cT          = _Time.y * _CausticsSpeed;
                #if defined(_CHROMATIC_ABERRATION)
                    // Chromatic aberration: three offset samples → visible rainbow
                    // fringe along caustic edges (like real underwater refraction).
                    float2 cOff       = float2(0.28, 0.0);
                    float  causticR   = CausticBlend(cP + cOff, cT);
                    float  causticG   = CausticBlend(cP,        cT);
                    float  causticB   = CausticBlend(cP - cOff, cT);
                    half3  causticRGB = half3(causticR, causticG, causticB);
                #else
                    // No CA: one sample, no rainbow fringing, ~3× cheaper.
                    float caustic     = CausticBlend(cP, cT);
                    half3 causticRGB  = half3(caustic, caustic, caustic);
                #endif
                // Patchy mask: VNoise at low freq, animated slowly → some regions
                // fully bright (1), others completely cut (0), soft transition.
                float  caustMask   = smoothstep(0.35, 0.65,
                                        VNoise(sceneWS.xz * 0.30 + cT * 0.18));
                // Auto-color: blend Shallow ↔ ShoreGlow (70% toward glow, the
                // brighter end), boost luminance so the effect reads as a true
                // light reflex relative to the underlying water tint. _CausticsColor
                // remains as an optional modulator (default white = pure auto).
                half3 caustAutoTint = lerp(_ShallowColor.rgb, _ShoreGlowColor.rgb, 0.7);
                caustAutoTint       = saturate(caustAutoTint * 1.55);
                half3 caustEffect   = caustAutoTint * _CausticsColor.rgb * causticRGB
                                    * causticZone * caustMask * _CausticsStrength;
                // Screen blend: 1 - (1-a)(1-b) — light that PENETRATES the water
                // color instead of being painted on top. No over-saturation, no
                // chalky film. Works on any water tint by construction.
                col.rgb = 1.0 - (1.0 - col.rgb) * (1.0 - saturate(caustEffect));

                // ── Lighting ──────────────────────────────────────────────────
                Light  mainLight = GetMainLight();
                float3 viewDir   = normalize(GetWorldSpaceViewDir(posWS));

                // Toon specular — three orthogonal controls:
                //   _SpecularPower (Size)      → highlight tightness (pow exponent)
                //   _SpecularCutoff (Softness) → toon edge AA width (0 hard, 1 soft)
                //   _SpecularScale (Intensity) → brightness multiplier
                float3 halfDir  = normalize(mainLight.direction + viewDir);
                float  ndoth    = max(0.0, dot(specN, halfDir));
                float  specRaw  = pow(ndoth, max(1.0, _SpecularPower));
                float  specEdge = lerp(0.004, 0.35, _SpecularCutoff);
                float  specMask = smoothstep(0.5 - specEdge, 0.5 + specEdge, specRaw);

                // ── Sparkle — break the toon specular blob into sun glints ─────
                // Sample the same normal map at much higher frequency → produces a
                // tight secondary highlight that lives WITHIN the existing spec
                // blob. Then modulate by animated low-freq fbm noise → shimmer
                // (mienienie). This is the same material highlight, just shattered.
                // Stylistically coherent: no procedural overlay, all light-driven.
                if (_SparkleBoost > 0.0)
                {
                    float2 spkUV  = posWS.xz * _NormalTiling * 6.0
                                  + t * float2(0.13, 0.09);
                    float3 spkTn  = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,
                                        sampler_NormalMap, spkUV));
                    float3 spkN   = normalize(float3(spkTn.x, spkTn.z, spkTn.y));
                    float  spkH   = max(0.0, dot(spkN, halfDir));
                    float  spk    = pow(spkH, max(1.0, _SpecularPower * 2.0));
                    spk           = smoothstep(0.30, 0.55, spk);    // crisp pinpoints
                    // Shimmer mask: two-octave VNoise → patches of bright/dim
                    float shimmer = VNoise(posWS.xz * 1.5 + t * 0.35)
                                  * VNoise(posWS.xz * 3.2 - t * 0.22 + 4.7);
                    shimmer       = saturate(shimmer * 2.4);
                    // Add as extra spec layer — same color as base highlight
                    col += _SpecularColor.rgb * spk * shimmer
                         * _SparkleBoost * _SpecularScale * mainLight.color;
                }

                col += _SpecularColor.rgb * specMask * _SpecularScale * mainLight.color;

                // Fresnel — now also brightens the colour at grazing angles
                // (previously affected alpha only → looked too subtle).
                float fresnel = pow(1.0 - saturate(dot(blendN, viewDir)), max(0.1, _FresnelPower));
                col = lerp(col, _ShoreGlowColor.rgb, fresnel * 0.55);

                // ── Alpha ─────────────────────────────────────────────────────
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depthT);
                alpha = saturate(alpha + fresnel * 0.3);

                // ── FOAM ──────────────────────────────────────────────────────
                float foamT = _Time.y * _FoamSpeed * foamAnim;

                // Projected foam — vertical world-Y at 4 diagonal taps (8 px).
                // World-space Y is camera-independent → foam DOES NOT dance
                // during camera rotation / tilt (eye-depth based foam did).
                float2 ts  = 8.0 / _ScreenParams.xy;
                float2 fu0 = scrUV + ts * float2( 1,  1);
                float2 fu1 = scrUV + ts * float2(-1,  1);
                float2 fu2 = scrUV + ts * float2( 1, -1);
                float2 fu3 = scrUV + ts * float2(-1, -1);
                float3 fw0 = ReconstructWorldPos(fu0, SampleSceneDepth(fu0));
                float3 fw1 = ReconstructWorldPos(fu1, SampleSceneDepth(fu1));
                float3 fw2 = ReconstructWorldPos(fu2, SampleSceneDepth(fu2));
                float3 fw3 = ReconstructWorldPos(fu3, SampleSceneDepth(fu3));
                // Above-water taps → absolute huge sentinel (no foam contribution).
                // Must NOT be tied to _FoamDepth — when FoamDepth=0 a relative
                // sentinel collapsed to 0 and stamped a full foam ring around every
                // protruding object (outline bug).
                const float bigD = 1e6;
                float  fd0  = (fw0.y > posWS.y) ? bigD : (posWS.y - fw0.y);
                float  fd1  = (fw1.y > posWS.y) ? bigD : (posWS.y - fw1.y);
                float  fd2  = (fw2.y > posWS.y) ? bigD : (posWS.y - fw2.y);
                float  fd3  = (fw3.y > posWS.y) ? bigD : (posWS.y - fw3.y);
                // Smallest vertical metres = shallowest tap = shore zone.
                float  projDiff = min(min(fd0, fd1), min(fd2, fd3));
                // projDist: 0 = at object edge, 1 = full _FoamDepth away
                float  projDist = saturate(projDiff / max(0.001, _FoamDepth));

                // Foam texture with UV distortion — sample once, use for tongues
                // Slider 0..1 mapped linearly to internal 0.05..2.0 tiling range.
                // Slider 0 → huge tongues (5 cm/repeat), slider 1 → fine pattern (2 m/repeat).
                float  foamTilingX = lerp(0.05, 2.0, _FoamTiling);
                float2 foamUV      = DistortUV(posWS.xz * foamTilingX, 0.3, foamT);
                float  foamSamp = SampleFoamStyled(foamUV, foamT, TEXTURE2D_ARGS(_FoamTex, sampler_FoamTex));

                // ── Projected foam — solid inner ring + wispy outer tongues ──
                // Reference style: thick GUARANTEED white band along shoreline
                // (innerMask, no noise) + irregular outer patches breaking up
                // farther from the edge (outerMask, noise-modulated).
                // _FoamCutoff controls total reach: 0 → none, 1 → full _FoamDepth.
                float  fw         = fwidth(projDist) + 0.01;
                float  innerReach = _FoamCutoff * 0.30;
                float  innerMask  = 1.0 - smoothstep(innerReach - fw, innerReach + fw, projDist);
                float  outerReach = _FoamCutoff * (0.35 + foamSamp * 1.30);
                float  outerMask  = (1.0 - smoothstep(outerReach - fw, outerReach + fw, projDist))
                                  * smoothstep(0.30, 0.55, foamSamp);   // gaps between patches
                float  projFoam   = max(innerMask, outerMask);

                // Surface foam islands: vertColor.r = 0 → foam, 1 → no foam.
                // Modulated by noise so painted foam also gets soft, broken edges.
                float surfWeight = 1.0 - IN.vertColor.r;
                float surfFoam   = surfWeight * smoothstep(0.40, 0.70, foamSamp);

                float foamMask = max(projFoam, surfFoam);

                // Foam — direct blend to _FoamColor (independent of base water tint).
                // Overlay blend mixed in fresnel/shore-glow color → foam looked tinted.
                col   = lerp(col, _FoamColor.rgb, foamMask * _FoamColor.a);
                alpha = max(alpha, foamMask * _FoamColor.a);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    CustomEditor "EmaceArt.EA_CoolWaterInspector"
    FallBack "Hidden/InternalErrorShader"
}
