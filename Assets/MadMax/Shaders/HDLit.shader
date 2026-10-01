Shader "MadMax/HDLit"
{
    // Textured HD assets (tools/blender/hd/PIPELINE.md). Lighting reproduces the asset sheets (Blender EEVEE stage: warm
    // sun, blue fill from the opposite side, uniform sky, AgX view transform) in both render modes: the pixel camera only
    // lowers the resolution. Every MadMax/PixelVoxel global and per-renderer feature works the same way (cutaways, sway,
    // grime, snow, night, horizon curve, water hole, fog, outline, unlit).
    Properties
    {
        _BaseMap ("Base (RGB) Opacity (A)", 2D) = "white" {}
        _MaskMap ("Mask (R metal, G AO, B paint, A smooth)", 2D) = "white" {}
        _MaskStrength ("Mask Used", Range(0,1)) = 1
        [Normal] _NormalMap ("Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        _EmissionMap ("Emission (lamps)", 2D) = "black" {}
        _EmissionScale ("Emission Scale", Float) = 1
        _LampOn ("Lamps Lit (renderer)", Range(0,4)) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _VertexAlbedo ("Albedo x Vertex Colour (voxel meshes)", Range(0,1)) = 0
        _PaintColor ("Paint (rgb, a = amount)", Color) = (1,1,1,0)
        _PaintRef ("Factory Paint (linear)", Color) = (0.5,0.5,0.5,1)
        _Smoothness ("Smoothness (no mask)", Range(0,1)) = 0.35
        _SpecularScale ("Specular Scale", Range(0,2)) = 1
        _OutlineColor ("Outline Color", Color) = (0.07,0.035,0.03,1)
        _OutlinePx ("Outline Width (px)", Range(0,3)) = 0
        _Unlit ("Unlit (backdrops)", Range(0,1)) = 0
        _SnowMask ("Snow Mask", Range(0,1)) = 1
        _CutY ("Cutaway Height", Float) = 100000
        _NoFog ("Ignore Fog (sky)", Range(0,1)) = 0
        _Sway ("Wind Sway by Height (trees)", Float) = 0
        _SwayTip ("Wind Sway by Vertex Alpha (grass)", Float) = 0
        _WorldCut ("Underground Cutaway", Range(0,1)) = 0
        _Dirt ("Mud Splatter (vehicles)", Range(0,1)) = 0
        _DirtTop ("Mud Line (world Y)", Float) = -100000
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _MaskStrength;
            half _NormalStrength;
            half _EmissionScale;
            half _LampOn;
            half4 _Tint;
            half _VertexAlbedo;
            half4 _PaintColor;
            half4 _PaintRef;
            half _Smoothness;
            half _SpecularScale;
            half4 _OutlineColor;
            half _OutlinePx;
            half _Unlit;
            half _SnowMask;
            float _CutY;
            half _NoFog;
            half _Sway;
            half _SwayTip;
            half _WorldCut;
            half _Dirt;
            float _DirtTop;
            half _Cull;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        // ---- MadMax globals (shared with PixelVoxel; 0 = default look)
        float _MadMaxOutlineDelta;
        float _MadMaxBrightnessDelta;
        float _MadMaxNight;
        float _MadMaxUnderFill;
        float _MadMaxSnow;
        float4 _MadMaxSnowLat;
        float _MadMaxCurve;
        float4 _MadMaxWaterHole;
        float _MadMaxSeaLevel;
        float _MadMaxAutumn;
        float4 _MadMaxClouds;
        float4 _MadMaxCloudOffset;
        float _MadMaxDither;
        float4 _MadMaxWind;
        float4 _MadMaxCut;
        // ---- HD look (0 = the asset-sheet defaults below; Atmosphere may drive them)
        float4 _MadMaxHDSun;      // rgb: game sun light -> sheet sun radiance gain; a > 0 = set
        float4 _MadMaxHDSky;      // rgb: uniform sky fill (Blender world 0.55 x #c9a37a); a > 0 = set
        float4 _MadMaxHDFill;     // rgb: blue fill sun from the side opposite the sun (0.6 x #9ab4d6); a > 0 = set
        float _MadMaxHDExposure;  // 0 = 1
        float _MadMaxHDTonemap;   // 0 = AgX (as the sheets), 1 = none (linear, clamped)

        float CloudHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float CloudValue(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(CloudHash(i), CloudHash(i + float2(1, 0)), f.x), lerp(CloudHash(i + float2(0, 1)), CloudHash(i + float2(1, 1)), f.x), f.y);
        }
        float Value3(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n = dot(i, float3(1.0, 57.0, 113.0));
            float a = frac(sin(n) * 43758.5453), b = frac(sin(n + 1.0) * 43758.5453), c = frac(sin(n + 57.0) * 43758.5453), d = frac(sin(n + 58.0) * 43758.5453);
            float e = frac(sin(n + 113.0) * 43758.5453), g = frac(sin(n + 114.0) * 43758.5453), h = frac(sin(n + 170.0) * 43758.5453), k = frac(sin(n + 171.0) * 43758.5453);
            return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
        }
        half CloudShadow(float3 positionWS)
        {
            if (_MadMaxClouds.x <= 0.001) return 0;
            float2 p = positionWS.xz * _MadMaxClouds.z + _MadMaxCloudOffset.xy;
            float n = CloudValue(p) * 0.55 + CloudValue(p * 2.1 + 17.3) * 0.3 + CloudValue(p * 4.3 + 41.7) * 0.15;
            return saturate((n - (1.0 - _MadMaxClouds.x)) * 5.0) * _MadMaxClouds.y;
        }
        float3 Curve(float3 ws)
        {
            float2 d = ws.xz - _WorldSpaceCameraPos.xz;
            ws.y -= dot(d, d) * _MadMaxCurve;
            return ws;
        }
        float3 WindSway(float3 ws, float3 os, half a)
        {
            float h = max(os.y, 0.0);
            float w = _Sway * h * h + _SwayTip * (1.0 - a);
            if (w <= 0.0) return Curve(ws);
            float2 wind = _MadMaxWind.xz;
            float2 origin = float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m23);
            float ph = _MadMaxWind.w * 1.9 + dot(ws.xz, float2(0.31, 0.23)) + dot(origin, float2(0.73, 1.37));
            float wave = 0.55 + 0.35 * sin(ph) + 0.2 * sin(ph * 2.7 + 1.3);
            float2 off = wind * (w * wave * (0.35 + _MadMaxWind.y));
            return Curve(ws + float3(off.x, -dot(off, off) * 0.6, off.y));
        }
        half SnowAmount(float3 ws)
        {
            half lat = _MadMaxSnowLat.z > 0.0 ? saturate((abs(ws.z - _MadMaxSnowLat.x) - _MadMaxSnowLat.y) / _MadMaxSnowLat.z) : 0.0h;
            return max(_MadMaxSnow, lat);
        }
        float CutMask(float3 ws)
        {
            float c = _CutY - ws.y;
            if (_WorldCut > 0.5 && _MadMaxCut.z > 0.0 && distance(ws.xz, _MadMaxCut.xy) < _MadMaxCut.z) c = min(c, _MadMaxCut.w - ws.y);
            return c;
        }
        half Bayer4(float2 px)
        {
            uint2 p = (uint2)px & 3u;
            const half m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            return (m[p.y * 4 + p.x] + 0.5h) / 16.0h;
        }
        // dithered glass: opacity below 1 keeps an ordered share of the pixels (no sorting, works in every pass)
        void ClipOpacity(half alpha, float2 px)
        {
            if (alpha < 0.999h) clip(alpha - Bayer4(px));
        }
        // Blender AgX (base look), the polynomial fit by B. Wrensch; input linear Rec.709, output linear for the sRGB target
        half3 AgX(half3 c)
        {
            const float3x3 inMat = float3x3(0.842479062253094, 0.0423282422610123, 0.0423756549057051,
                                            0.0784335999999992, 0.878468636469772, 0.0784336,
                                            0.0792237451477643, 0.0791661274605434, 0.879142973793104);
            const float3x3 outMat = float3x3(1.19687900512017, -0.0528968517574562, -0.0529716355144438,
                                             -0.0980208811401368, 1.15190312990417, -0.0980434501171241,
                                             -0.0990297440797205, -0.0989611768448433, 1.15107367264116);
            const float minEv = -12.47393, maxEv = 4.026069;
            float3 v = mul(max(c, 1e-10), inMat);
            v = clamp(log2(v), minEv, maxEv);
            v = (v - minEv) / (maxEv - minEv);
            float3 v2 = v * v, v4 = v2 * v2;
            v = 15.5 * v4 * v2 - 40.14 * v4 * v + 31.96 * v4 - 6.868 * v2 * v + 0.4298 * v2 + 0.1191 * v - 0.00232;
            v = mul(v, outMat);
            return (half3)pow(saturate(v), 2.2);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT;
                half4 color : COLOR; float2 uv : TEXCOORD0; float2 wear : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; half4 color : COLOR; half fog : TEXCOORD4;
                float3 positionOS : TEXCOORD5; half wear : TEXCOORD6;
            };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionOS = i.positionOS.xyz;
                o.positionWS = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                float s = i.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = half4(TransformObjectToWorldDir(i.tangentOS.xyz), s);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.color = i.color;
                o.wear = saturate(i.wear.x);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 SunGain() { return _MadMaxHDSun.a > 0 ? _MadMaxHDSun.rgb : half3(2.72h, 2.38h, 1.91h); }
            half3 SkyFill() { return _MadMaxHDSky.a > 0 ? _MadMaxHDSky.rgb : half3(0.321h, 0.201h, 0.107h); }
            half3 SideFill() { return _MadMaxHDFill.a > 0 ? _MadMaxHDFill.rgb : half3(0.198h, 0.276h, 0.402h); }

            // minimalist Cook-Torrance (URP's DirectBRDFSpecular)
            half Specular(half3 n, half3 l, half3 v, half rough)
            {
                half r = max(rough * rough, 0.002h);
                half3 h = SafeNormalize(l + v);
                half nh = saturate(dot(n, h)), lh = saturate(dot(l, h));
                half d = nh * nh * (r * r - 1.0h) + 1.00001h;
                return min(r * r / ((d * d) * max(0.1h, lh * lh) * (r * 4.0h + 2.0h)), 60.0h);
            }

            half4 frag (Varyings i) : SV_Target
            {
                clip(CutMask(i.positionWS));
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                ClipOpacity(baseTex.a, i.positionCS.xy);
                half4 mask = lerp(half4(0.0h, 1.0h, 0.0h, _Smoothness), SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv), _MaskStrength);
                half3 albedo = baseTex.rgb * _Tint.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                half3 vcol = SRGBToLinear(i.color.rgb);
                #else
                half3 vcol = i.color.rgb;
                #endif
                albedo *= lerp(half3(1, 1, 1), vcol, _VertexAlbedo);
                half metal = mask.r, ao = mask.g, smooth = mask.a;
                // repaint (VehiclePaint): the paint mask scales the factory colour towards the new one, keeping its shading
                half paint = mask.b * _PaintColor.a;
                if (paint > 0.001h) albedo = lerp(albedo, saturate(albedo * _PaintColor.rgb / max(_PaintRef.rgb, 0.02h)), paint);
                // scraped to bare metal (VehicleBreakables writes uv2.x)
                if (i.wear > 0.001h) { albedo = lerp(albedo, half3(0.20h, 0.19h, 0.18h), i.wear); metal = lerp(metal, 0.7h, i.wear); smooth = lerp(smooth, 0.45h, i.wear); }

                half3 nW = normalize(i.normalWS);
                half3 n = nW;
                if (_NormalStrength > 0.001h)
                {
                    half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                    half3 t = normalize(i.tangentWS.xyz);
                    half3 b = cross(nW, t) * i.tangentWS.w;
                    n = normalize(tn.x * t + tn.y * b + tn.z * nW);
                }
                // mud (VehicleGrime): blotches thick low on the body, thinning out up to the mud line
                if (_Dirt > 0.001h)
                {
                    half low = saturate((_DirtTop - i.positionWS.y) / 0.9h);
                    float nz = Value3(i.positionOS * 7.0) * 0.65 + Value3(i.positionOS * 19.0) * 0.35;
                    half m = saturate((_Dirt * low * 1.25h - (half)nz) * 5.0h);
                    albedo = lerp(albedo, half3(0.075h, 0.045h, 0.025h), m * 0.85h);
                    smooth *= 1.0h - m; metal *= 1.0h - m;
                }
                // autumn: swaying foliage turns gold and rust in soft patches
                if (_MadMaxAutumn > 0.001h && (_Sway + _SwayTip) > 0.0h && albedo.g > albedo.r * 1.1h && albedo.g > albedo.b)
                {
                    half ha = (half)Value3(i.positionWS * 1.5);
                    if (ha < _MadMaxAutumn) albedo = ha < _MadMaxAutumn * 0.45h ? albedo.ggg * half3(1.9h, 0.55h, 0.12h) : albedo.ggg * half3(1.6h, 1.05h, 0.18h);
                }
                // snow on up faces: weather dusting / polar snow
                half snowAmt = SnowAmount(i.positionWS);
                if (snowAmt > 0.001h)
                {
                    half up = smoothstep(0.45h, 0.85h, nW.y);
                    half cover = saturate((snowAmt * 1.3h * up - (half)Value3(i.positionWS * 9.0) * 0.5h) * 3.0h) * _SnowMask;
                    albedo = lerp(albedo, half3(0.82h, 0.84h, 0.9h), cover * 0.9h);
                    smooth = lerp(smooth, 0.3h, cover); metal *= 1.0h - cover;
                }

                half night = _MadMaxNight;
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = light.shadowAttenuation * (1.0h - CloudShadow(i.positionWS));
                half ndl = saturate(dot(n, light.direction));
                half rough = 1.0h - smooth;
                half3 diffC = albedo * (1.0h - metal);
                half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, metal);
                half3 sun = light.color * SunGain();
                half3 c = (diffC + f0 * Specular(n, light.direction, v, rough) * _SpecularScale) * sun * (ndl * shadow);
                // fill: a weak blue sun low on the opposite side (no shadow), the uniform sky, a warm bounce from the ground
                half3 flat = half3(-light.direction.x, 0.0h, -light.direction.z);
                half3 fillDir = normalize(normalize(flat + half3(1e-4h, 0, 0)) * 0.866h + half3(0, 0.5h, 0));
                half dayF = 1.0h - night * 0.9h;
                c += diffC * SideFill() * saturate(dot(n, fillDir)) * dayF * lerp(1.0h, ao, 0.5h);
                half3 sky = SkyFill() * dayF + half3(0.012h, 0.016h, 0.035h) * night + half3(0.05h, 0.052h, 0.06h) * _MadMaxUnderFill;
                half3 bounce = half3(0.10h, 0.065h, 0.04h) * saturate(-n.y) * dayF;
                c += diffC * (sky + bounce) * ao;
                // sky reflection (chrome, glass, paint): the uniform world colour, Schlick fresnel
                half nv = saturate(dot(n, v));
                half3 fr = f0 + (max(half3(smooth, smooth, smooth), f0) - f0) * pow(1.0h - nv, 5.0h);
                c += fr * SkyFill() * 2.2h * dayF * ao * smooth * _SpecularScale;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS);
                    half a = saturate(dot(n, al.direction)) * al.distanceAttenuation;
                    c += (diffC + f0 * Specular(n, al.direction, v, rough) * _SpecularScale) * al.color * a;
                LIGHT_LOOP_END
                #endif
                // lamps: baked glow times the renderer's lamp state (VehicleLights)
                c += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionScale * _LampOn;

                c *= _MadMaxHDExposure > 0 ? _MadMaxHDExposure : 1.0;
                c = _MadMaxHDTonemap > 0.5 ? saturate(c) : AgX(c);
                c = lerp(c, albedo, _Unlit);
                c *= 1.0h + _MadMaxBrightnessDelta;
                if (_MadMaxWaterHole.z > 0.0 && i.positionWS.y < _MadMaxSeaLevel)
                {
                    half dpt = saturate((_MadMaxSeaLevel - i.positionWS.y) / 20.0);
                    c = lerp(c, c * half3(0.6h, 0.85h, 0.9h) + half3(0.01h, 0.045h, 0.055h), 0.4h + dpt * 0.4h);
                }
                c = lerp(MixFog(c, i.fog), c, _NoFog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.ws = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a);
                float4 cs = TransformWorldToHClip(o.ws);
                float px = _OutlinePx * (1.0 + _MadMaxOutlineDelta);
                if (px <= 0.0) cs = float4(0, 0, 0, 0);                     // off (the default): degenerate, nothing drawn
                else
                {
                    float2 nCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(i.normalOS)).xy;
                    if (dot(nCS, nCS) > 1e-6) cs.xy += normalize(nCS) * (px * 2.0 / _ScreenParams.xy) * cs.w;
                }
                o.positionCS = cs;
                o.fog = ComputeFogFactor(cs.z);
                return o;
            }
            half4 frag (Varyings i) : SV_Target { clip(CutMask(i.ws)); return half4(lerp(MixFog(_OutlineColor.rgb, i.fog), _OutlineColor.rgb, _NoFog), 1); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert (Attributes i)
            {
                Varyings o;
                float3 ws = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, n, _LightDirection));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - 0.5h);      // glass casts no shadow
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 p : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; float2 uv : TEXCOORD1; };
            V vert (A i) { V o; o.ws = WindSway(TransformObjectToWorld(i.p.xyz), i.p.xyz, i.color.a); o.positionCS = TransformWorldToHClip(o.ws); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half frag (V i) : SV_Target
            {
                clip(CutMask(i.ws));
                ClipOpacity(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a, i.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 ws : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings vert (Attributes i)
            {
                Varyings o;
                o.ws = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a);
                o.positionCS = TransformWorldToHClip(o.ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                clip(CutMask(i.ws));
                ClipOpacity(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a, i.positionCS.xy);
                return half4(normalize(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
