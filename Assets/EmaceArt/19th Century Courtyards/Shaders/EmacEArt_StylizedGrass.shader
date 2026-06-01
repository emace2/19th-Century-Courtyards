// EmacEArt Stylized Grass - minimal URP shader for foliage/grass.
// Worldspace vertex wind, root-anchored (objectspace y=0), URP Lit PBR.

Shader "EmacEArt/StylizedGrass"
{
    Properties
    {
        [Header(Base)]
        _MainTex       ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        _BaseColor     ("Base Tint", Color)          = (1, 1, 1, 1)
        _Cutoff        ("Alpha Cutoff", Range(0, 1)) = 0.4

        [Header(Normal)]
        _NormalMap     ("Normal Map", 2D)            = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0

        [Header(PBR)]
        _Smoothness    ("Smoothness", Range(0, 1))   = 0.2
        _Metallic      ("Metallic",   Range(0, 1))   = 0.0

        [Header(Wind)]
        _WindStrength  ("Wind Strength",  Range(0, 2)) = 0.15
        _WindSpeed     ("Wind Speed",     Range(0, 10)) = 1.5
        _WindFrequency ("Wind Frequency", Range(0, 5)) = 0.5
        _WindDirectionX("Wind Direction X", Range(-1, 1)) = 1
        _WindDirectionZ("Wind Direction Z", Range(-1, 1)) = 0

        [Header(Gust)]
        _GustNoise     ("Gust Noise Map", 2D) = "white" {}
        _GustStrength  ("Gust Strength", Range(0, 2)) = 0.5
        _GustScale     ("Gust World Scale", Range(0, 1)) = 0.05
        _GustSpeed     ("Gust Speed", Range(0, 5)) = 0.4

        [Header(Color Tint Small)]
        _TintColorSmall("Color Small", Color) = (0, 0, 0, 1)
        [Toggle] _StripesSmall("Stripes Mode Small", Float) = 1

        [Header(Small _ Blob Mode)]
        _TintScaleSmallBlob   ("Scale Small Blob",     Range(0.01, 1))  = 0.504
        _TintIntensitySmallBlob("Intensity Small Blob",Range(0, 1))     = 0.47
        _TintWidthSmallBlob   ("Width Small Blob",     Range(0, 1))     = 0.867
        _TintSpeedSmallBlob   ("Speed Small Blob",     Range(0, 2))     = 0.806

        [Header(Small _ Stripes Mode)]
        _TintScaleSmallStripes   ("Scale Small Stripes",     Range(0.01, 1))  = 0.504
        _TintIntensitySmallStripes("Intensity Small Stripes",Range(0, 1))     = 0.47
        _TintWidthSmallStripes   ("Width Small Stripes",     Range(0, 1))     = 0.867
        _TintSpeedSmallStripes   ("Speed Small Stripes",     Range(0, 2))     = 0.806

        [Header(Color Tint Large)]
        _TintColorLarge("Color Large", Color) = (0.45, 0.5, 0.2, 1)
        [Toggle] _StripesLarge("Stripes Mode Large", Float) = 1

        [Header(Large _ Blob Mode)]
        _TintScaleLargeBlob   ("Scale Large Blob",     Range(0.005, 0.3))  = 0.06
        _TintIntensityLargeBlob("Intensity Large Blob",Range(0, 1))        = 0.386
        _TintWidthLargeBlob   ("Width Large Blob",     Range(0, 1))        = 0.0
        _TintSpeedLargeBlob   ("Speed Large Blob",     Range(0, 2))        = 0.192

        [Header(Large _ Stripes Mode)]
        _TintScaleLargeStripes   ("Scale Large Stripes",     Range(0.005, 0.3))  = 0.06
        _TintIntensityLargeStripes("Intensity Large Stripes",Range(0, 1))        = 0.386
        _TintWidthLargeStripes   ("Width Large Stripes",     Range(0, 1))        = 0.0
        _TintSpeedLargeStripes   ("Speed Large Stripes",     Range(0, 2))        = 0.192

        [Header(Foliage Shading)]
        _AmbientLift   ("Ambient Lift (softens shadows)", Range(0, 1)) = 0.35
        _AmbientTint   ("Ambient Lift Tint", Color) = (0.55, 0.7, 0.55, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"   = "UniversalPipeline"
            "RenderType"       = "TransparentCutout"
            "Queue"            = "AlphaTest"
            "IgnoreProjector"  = "True"
        }
        LOD 200
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseColor;
            float  _Cutoff;
            float  _NormalStrength;
            float  _Smoothness;
            float  _Metallic;
            float  _WindStrength;
            float  _WindSpeed;
            float  _WindFrequency;
            float  _WindDirectionX;
            float  _WindDirectionZ;
            float4 _GustNoise_ST;
            float  _GustStrength;
            float  _GustScale;
            float  _GustSpeed;
            float4 _TintColorSmall;
            float  _StripesSmall;
            float  _TintScaleSmallBlob;
            float  _TintIntensitySmallBlob;
            float  _TintWidthSmallBlob;
            float  _TintSpeedSmallBlob;
            float  _TintScaleSmallStripes;
            float  _TintIntensitySmallStripes;
            float  _TintWidthSmallStripes;
            float  _TintSpeedSmallStripes;
            float4 _TintColorLarge;
            float  _StripesLarge;
            float  _TintScaleLargeBlob;
            float  _TintIntensityLargeBlob;
            float  _TintWidthLargeBlob;
            float  _TintSpeedLargeBlob;
            float  _TintScaleLargeStripes;
            float  _TintIntensityLargeStripes;
            float  _TintWidthLargeStripes;
            float  _TintSpeedLargeStripes;
            float  _AmbientLift;
            float4 _AmbientTint;
        CBUFFER_END

        TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
        TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
        TEXTURE2D(_GustNoise);  SAMPLER(sampler_GustNoise);

        // Wind: three layers.
        //   1) Per-blade jitter (sin + bladePhase) - each blade slightly
        //      different, gives "life" during calm weather.
        //   2) Coherent gust (sample _GustNoise by world XZ + scroll) -
        //      neighbouring vertices get similar values => whole region leans
        //      together like a real breeze travelling across a meadow.
        //   3) Microjitter - fast small leaf trembling.
        // Anchor: positionOS.y -> 0 at root = static, 1 at tip = max sway.
        float3 EmacEArt_ApplyWind(float3 positionOS)
        {
            float anchor = saturate(positionOS.y);
            float3 worldPos = TransformObjectToWorld(positionOS);
            float2 dir = normalize(float2(_WindDirectionX, _WindDirectionZ) + float2(1e-4, 0));

            // 1) Per-blade jitter
            float worldPhase = _Time.y * _WindSpeed
                             + worldPos.x * _WindFrequency
                             + worldPos.z * _WindFrequency;
            float bladePhase = positionOS.x * 12.7 + positionOS.z * 8.3;
            float wave    = sin(worldPhase + bladePhase);
            float jitter  = sin(_Time.y * _WindSpeed * 3.1 + bladePhase * 2.1) * 0.35;
            float perBlade = wave + jitter;

            // 2) Coherent gust - noise sampled by world XZ, scrolled by time*dir.
            // LOD 0 to avoid mip reads in vertex shader.
            float2 gustUV = worldPos.xz * _GustScale - dir * _Time.y * _GustSpeed;
            float gustSample = SAMPLE_TEXTURE2D_LOD(_GustNoise, sampler_GustNoise,
                                                   gustUV, 0).r;
            float gust = (gustSample - 0.5) * 2.0 * _GustStrength;

            // Sum + push along wind direction, weighted by anchor.
            float total = perBlade + gust;
            float3 push = float3(dir.x, 0, dir.y) * total * _WindStrength * anchor;

            // World-space push -> object-space (correct under parent scale / rotation).
            float3 pushOS = mul((float3x3)GetWorldToObjectMatrix(), push);
            return positionOS + pushOS;
        }

        // Procedural value noise - no texture dependency. Stable across
        // frames (deterministic on world position) and animatable via UV
        // scroll (which we do below). Hash function is the standard
        // Inigo Quilez style fract-of-large-multiplied-vec.
        float EmacEArt_Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float EmacEArt_ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = EmacEArt_Hash21(i);
            float b = EmacEArt_Hash21(i + float2(1, 0));
            float c = EmacEArt_Hash21(i + float2(0, 1));
            float d = EmacEArt_Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // Two crossed scrolling noise layers, multiplied. Single scrolling
        // layer would show a visible stripe sweeping across the field; two
        // layers crossing at different speeds/directions break that into
        // organic blobs that drift.
        //
        // Width is a power curve on the resulting noise:
        //   width=0  -> pow(n, 4)    : few sharp peaks, most of surface clear
        //   width=1  -> pow(n, 0.25) : noise floor lifted, tint covers most
        //
        // Scroll speed is in noise units/s, independent of scale - large
        // blobs animate just as visibly as small ones.
        float EmacEArt_TintNoise(float2 worldXZ, float2 windDir,
                                  float scale, float scrollSpeed, float width,
                                  float phaseOffset, float stripes)
        {
            float nMul;
            if (stripes > 0.5)
            {
                // Stripes mode. Noise is sampled along axis perpendicular to
                // wind, so values are constant along wind direction -> visible
                // parallel bands that drift along wind.
                //
                // To avoid mechanical equal-width stripes, the band coordinate
                // itself is warped by a slower noise (frequency modulation).
                // Effect: some stripes come out wide & lazy, others narrow &
                // sharp, with irregular spacing - reads as natural wind waves
                // not a barcode.
                float2 perp = float2(-windDir.y, windDir.x);
                float perpCoord = dot(worldXZ, perp);
                float t = _Time.y * scrollSpeed;

                // Slow modulator: stretches/compresses the band axis locally.
                float warp = EmacEArt_ValueNoise(
                    float2(perpCoord * scale * 0.18 + t * 0.4,
                           3.7 + phaseOffset));
                float warpedCoord = perpCoord * scale + warp * 4.0 + t;

                // Main stripe pattern: 1D-ish noise, constant in wind-dir.
                float nMain = EmacEArt_ValueNoise(
                    float2(warpedCoord, 1.7 + phaseOffset));
                // Width envelope: slow noise lifting some bands, suppressing
                // others, so neighbours have different intensities.
                float nEnv = EmacEArt_ValueNoise(
                    float2(perpCoord * scale * 0.32 + t * 0.55,
                           9.1 + phaseOffset));
                nMul = saturate(nMain * (0.2 + nEnv * 1.6));
            }
            else
            {
                // Blob mode: two crossed 2D noise layers, multiplied. Single
                // layer would show a visible stripe sweeping; crossed layers
                // break that into organic drifting blobs.
                float2 baseUV = worldXZ * scale;
                float2 scrollA = windDir * _Time.y * scrollSpeed;
                float2 scrollB = float2(-windDir.y, windDir.x)
                               * _Time.y * scrollSpeed * 0.7;
                float nA = EmacEArt_ValueNoise(baseUV + scrollA + phaseOffset);
                float nB = EmacEArt_ValueNoise(baseUV * 1.6 + scrollB + 7.37 + phaseOffset);
                nMul = saturate(nA * nB * 2.0);
            }
            float curve = lerp(4.0, 0.25, saturate(width));
            return pow(nMul, curve);
        }

        half3 EmacEArt_DualNoiseTint(float3 worldPos)
        {
            float2 windDir = normalize(float2(_WindDirectionX, _WindDirectionZ)
                                     + float2(1e-4, 0));

            // Per-layer: pick blob-mode or stripes-mode parameter set so each
            // mode keeps its own remembered values when the user toggles back.
            float sScale     = lerp(_TintScaleSmallBlob,
                                    _TintScaleSmallStripes,     _StripesSmall);
            float sIntensity = lerp(_TintIntensitySmallBlob,
                                    _TintIntensitySmallStripes, _StripesSmall);
            float sWidth     = lerp(_TintWidthSmallBlob,
                                    _TintWidthSmallStripes,     _StripesSmall);
            float sSpeed     = lerp(_TintSpeedSmallBlob,
                                    _TintSpeedSmallStripes,     _StripesSmall);

            float lScale     = lerp(_TintScaleLargeBlob,
                                    _TintScaleLargeStripes,     _StripesLarge);
            float lIntensity = lerp(_TintIntensityLargeBlob,
                                    _TintIntensityLargeStripes, _StripesLarge);
            float lWidth     = lerp(_TintWidthLargeBlob,
                                    _TintWidthLargeStripes,     _StripesLarge);
            float lSpeed     = lerp(_TintSpeedLargeBlob,
                                    _TintSpeedLargeStripes,     _StripesLarge);

            float nS = EmacEArt_TintNoise(worldPos.xz, windDir,
                                          sScale, sSpeed, sWidth,
                                          0.0, _StripesSmall);

            float nL = EmacEArt_TintNoise(worldPos.xz, windDir,
                                          lScale, lSpeed, lWidth,
                                          11.3, _StripesLarge);

            half3 tintS = lerp(half3(1, 1, 1), _TintColorSmall.rgb,
                               nS * sIntensity);
            half3 tintL = lerp(half3(1, 1, 1), _TintColorLarge.rgb,
                               nL * lIntensity);
            return tintS * tintL;
        }
        ENDHLSL

        // ============================================================
        // Pass 1: ForwardLit (URP PBR)
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                float4 color      : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 displacedOS = EmacEArt_ApplyWind(IN.positionOS.xyz);

                VertexPositionInputs vpi = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS   = vni.normalWS;
                OUT.tangentWS  = float4(vni.tangentWS, IN.tangentOS.w);
                OUT.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 albedoA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(albedoA.a - _Cutoff);

                half3 nTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv),
                    _NormalStrength);

                float3 bitangentWS = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(
                    nTS,
                    half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS));
                normalWS = normalize(normalWS);

                // Two-sided foliage: flip normal on backface so the underside
                // of leaves is lit correctly instead of going black.
                normalWS *= (facing >= 0.0) ? 1.0 : -1.0;

                // Fixed mild bias toward world-up: grass cards' normals point
                // sideways, so a hard side-sun would leave most of the field
                // black. Half-blend toward up keeps the field naturally lit.
                // Fixed value, not exposed - tweaking it gave no useful range.
                normalWS = normalize(lerp(normalWS, half3(0, 1, 0), 0.5));

                // Dual-noise color variation, per-clump look without per-instance mat.
                half3 tint = EmacEArt_DualNoiseTint(IN.positionWS);

                InputData input = (InputData)0;
                input.positionWS      = IN.positionWS;
                input.normalWS        = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                input.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                input.fogCoord        = IN.fogFactor;
                input.bakedGI         = SampleSH(normalWS);

                SurfaceData surf = (SurfaceData)0;
                surf.albedo     = albedoA.rgb * _BaseColor.rgb * tint;
                surf.metallic   = _Metallic;
                surf.smoothness = _Smoothness;
                surf.normalTS   = nTS;
                surf.occlusion  = 1;
                surf.alpha      = 1;

                half4 color = UniversalFragmentPBR(input, surf);

                // Ambient lift: keep shadowed sides from going to black.
                // Adds a subtle floor proportional to albedo so colour stays
                // grass-like rather than turning gray.
                color.rgb += surf.albedo * _AmbientTint.rgb * _AmbientLift;

                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 2: ShadowCaster - our own, with wind, samples alpha from
        // _MainTex (our property name, not _BaseMap like URP Lit).
        // UsePass URP Lit did not work because URP Lit's shadow pass
        // samples _BaseMap which we don't have -> all pixels clipped.
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex   VertShadow
            #pragma fragment FragShadow

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // _LightDirection / _LightPosition are NOT declared by Shadows.hlsl
            // nor Lighting.hlsl - only by URP's ShadowCasterPass.hlsl template.
            // Since we don't include that template, we declare them ourselves.
            // Without them = (0,0,0) = shadow projected to origin = off shadow
            // map = no shadows on ground.
            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesSC
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsSC
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsSC VertShadow(AttributesSC IN)
            {
                VaryingsSC OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 displacedOS = EmacEArt_ApplyWind(IN.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(displacedOS);
                float3 normalWS   = TransformObjectToWorldDir(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 FragShadow(VaryingsSC IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 3: DepthOnly (needed for URP depth texture / SSAO)
        // ============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   VertDepth
            #pragma fragment FragDepth
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings VertDepth(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 displacedOS = EmacEArt_ApplyWind(IN.positionOS.xyz);
                OUT.positionCS = TransformObjectToHClip(displacedOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 FragDepth(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 4 + 5: DepthNormals / DepthNormalsOnly - for SSAO.
        // URP renderers use one or the other LightMode depending on the
        // renderer asset config, so we expose both. They write world-space
        // normal to _CameraNormalsTexture so SSAO can compute occlusion.
        // ============================================================
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex   VertDN
            #pragma fragment FragDN
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct AttributesDNO
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDNO
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDNO VertDN(AttributesDNO IN)
            {
                VaryingsDNO OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 displacedOS = EmacEArt_ApplyWind(IN.positionOS.xyz);
                OUT.positionCS = TransformObjectToHClip(displacedOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                VertexNormalInputs nIn = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS = NormalizeNormalPerVertex(nIn.normalWS);
                return OUT;
            }

            half4 FragDN(VaryingsDNO IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(a - _Cutoff);
                float3 n = NormalizeNormalPerPixel(IN.normalWS);
                return half4(n, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex   VertDN
            #pragma fragment FragDN
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct AttributesDN
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDN VertDN(AttributesDN IN)
            {
                VaryingsDN OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 displacedOS = EmacEArt_ApplyWind(IN.positionOS.xyz);
                OUT.positionCS = TransformObjectToHClip(displacedOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                VertexNormalInputs nIn = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS = NormalizeNormalPerVertex(nIn.normalWS);
                return OUT;
            }

            half4 FragDN(VaryingsDN IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(a - _Cutoff);
                float3 n = NormalizeNormalPerPixel(IN.normalWS);
                return half4(n, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "EmacEArtStylizedGrassGUI"
}
