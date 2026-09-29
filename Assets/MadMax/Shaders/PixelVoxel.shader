Shader "MadMax/PixelVoxel"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Bands ("Light Bands", Range(1,6)) = 3
        _ShadowTint ("Shadow Tint", Color) = (0.42,0.28,0.32,1)
        _Ambient ("Ambient", Color) = (0.10,0.06,0.05,1)
        _OutlineColor ("Outline Color", Color) = (0.07,0.035,0.03,1)
        _OutlinePx ("Outline Width (px)", Range(0,3)) = 1
        _Unlit ("Unlit (backdrops)", Range(0,1)) = 0
        _SnowMask ("Snow Mask", Range(0,1)) = 1
        _CutY ("Cutaway Height", Float) = 100000
        _NoFog ("Ignore Fog (sky)", Range(0,1)) = 0
        _Sway ("Wind Sway by Height (trees)", Float) = 0
        _SwayTip ("Wind Sway by Vertex Alpha (grass)", Float) = 0
        _WorldCut ("Underground Cutaway", Range(0,1)) = 0
        _Dirt ("Mud Splatter (vehicles)", Range(0,1)) = 0
        _DirtTop ("Mud Line (world Y)", Float) = -100000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Bands;
            half4 _ShadowTint;
            half4 _Ambient;
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
        CBUFFER_END
        // global settings (0 = default look)
        float _MadMaxOutlineDelta;
        float _MadMaxBrightnessDelta;
        float _MadMaxNight;      // 0 day .. 1 night
        float _MadMaxUnderFill;  // 0 .. 1 under a cave / bunker roof: faint fill light
        float _MadMaxSnow;       // snow dusting on upward faces
        float4 _MadMaxSnowLat;   // polar snow: x equator z, y distance where it starts, z ramp (m); z = 0 disables
        float _MadMaxCurve;      // planet horizon: drop per squared metre from the camera (1 / 2R)
        float _MadMaxAutumn;     // 0..1 foliage turning gold and rust (swaying materials: trees, grass)
        float4 _MadMaxClouds;    // x coverage 0..1, y shadow strength, z 1/scale (1/m); x = 0 disables
        float4 _MadMaxCloudOffset; // xy drift (noise space)

        float CloudHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float CloudValue(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(CloudHash(i), CloudHash(i + float2(1, 0)), f.x), lerp(CloudHash(i + float2(0, 1)), CloudHash(i + float2(1, 1)), f.x), f.y);
        }
        // 0 = clear sky .. 1 = full cloud shadow at a world position
        half CloudShadow(float3 positionWS)
        {
            if (_MadMaxClouds.x <= 0.001) return 0;
            float2 p = positionWS.xz * _MadMaxClouds.z + _MadMaxCloudOffset.xy;
            float n = CloudValue(p) * 0.55 + CloudValue(p * 2.1 + 17.3) * 0.3 + CloudValue(p * 4.3 + 41.7) * 0.15;
            return saturate((n - (1.0 - _MadMaxClouds.x)) * 5.0) * _MadMaxClouds.y;
        }
        float _MadMaxDither;     // ordered dither between light bands
        float4 _MadMaxWind;      // xz wind (m/s), y gust 0..1, w time
        float4 _MadMaxCut;       // underground cutaway: xy centre (world xz), z radius (0 = off), w height

        // the planet's horizon: everything sinks with the square of its distance from the camera
        float3 Curve(float3 ws)
        {
            float2 d = ws.xz - _WorldSpaceCameraPos.xz;
            ws.y -= dot(d, d) * _MadMaxCurve;
            return ws;
        }

        // wind bend: trees by height above their origin (_Sway), grass and vines by vertex alpha (_SwayTip, 255 = rooted); then the horizon curve
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

        // snow on up faces: the weather's dusting, or the polar snow of the latitude
        half SnowAmount(float3 ws)
        {
            half lat = _MadMaxSnowLat.z > 0.0 ? saturate((abs(ws.z - _MadMaxSnowLat.x) - _MadMaxSnowLat.y) / _MadMaxSnowLat.z) : 0.0h;
            return max(_MadMaxSnow, lat);
        }

        // > 0 keeps the fragment: per-renderer building cutaway (_CutY) and the radial underground cutaway
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
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; half fog : TEXCOORD2; float3 positionOS : TEXCOORD3; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionOS = i.positionOS.xyz;
                o.positionWS = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                clip(CutMask(i.positionWS));                               // building / underground cutaway above the player
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = saturate(dot(normalize(i.normalWS), light.direction));
                half lit = ndl * light.shadowAttenuation * (1.0h - CloudShadow(i.positionWS));   // drifting cloud shadows
                half bias = lerp(0.35h, Bayer4(i.positionCS.xy), _MadMaxDither);
                half q = floor(lit * _Bands + bias) / _Bands;            // hard light bands, no gradients (optionally dithered)
                half3 albedo = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                albedo = SRGBToLinear(albedo);                             // vertex colours are authored in sRGB
                #endif
                albedo *= _Tint.rgb;
                // mud on vehicles (VehicleGrime): voxel splatters, thick low on the body, thinning out up to the mud line
                if (_Dirt > 0.001h)
                {
                    float3 cd = floor(i.positionOS * 12.5 + 0.37);
                    half hd = frac(sin(dot(cd, float3(41.13, 17.71, 93.37))) * 24634.63);
                    half low = saturate((_DirtTop - i.positionWS.y) / 0.9);
                    if (hd < _Dirt * low * 0.75h) albedo = lerp(albedo, half3(0.075h, 0.045h, 0.025h), 0.8h);
                }
                // autumn: leaves and grass turn gold and rust, voxel by voxel
                if (_MadMaxAutumn > 0.001h && (_Sway + _SwayTip) > 0.0h && albedo.g > albedo.r * 1.1h && albedo.g > albedo.b)
                {
                    float3 ac = floor(i.positionWS * 6.0 + 0.5);
                    half ha = frac(sin(dot(ac, float3(7.13, 157.1, 113.7))) * 43758.5453);
                    if (ha < _MadMaxAutumn) albedo = ha < _MadMaxAutumn * 0.45h ? albedo.ggg * half3(1.9h, 0.55h, 0.12h) : albedo.ggg * half3(1.6h, 1.05h, 0.18h);
                }
                // light snow dusting: a scatter of voxels on upward faces
                half3 nW = normalize(i.normalWS);
                half snowAmt = SnowAmount(i.positionWS);
                if (snowAmt > 0.001h && nW.y > 0.55h)
                {
                    float3 cell = floor(i.positionWS * 12.5 + 0.01);
                    half h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                    if (h < snowAmt * 0.32h * _SnowMask) albedo = lerp(albedo, half3(0.82h, 0.84h, 0.9h), 0.85h);
                }
                half night = _MadMaxNight;
                half3 shade = _ShadowTint.rgb * (1.0h - night * 0.85h);
                half3 amb = _Ambient.rgb * (1.0h - night * 0.6h) + half3(0.012h, 0.016h, 0.035h) * night + half3(0.05h, 0.052h, 0.06h) * _MadMaxUnderFill;
                half3 c = albedo * (lerp(shade, light.color, q) + amb);
                #if defined(_ADDITIONAL_LIGHTS)
                // point lights (lamps, stoves), banded like the sun
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 nrm = normalize(i.normalWS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS);
                    half a = saturate(dot(nrm, al.direction) * 0.7h + 0.3h) * al.distanceAttenuation;
                    c += albedo * al.color * (floor(a * _Bands * 2.0h + 0.35h) / (_Bands * 2.0h));
                LIGHT_LOOP_END
                #endif
                c = lerp(c, albedo, _Unlit);
                c *= 1.0h + _MadMaxBrightnessDelta;
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
            struct Attributes { float4 positionOS : POSITION; float3 smoothNormal : TEXCOORD3; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.ws = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a);
                float4 cs = TransformWorldToHClip(o.ws);
                float3 nWS = TransformObjectToWorldNormal(i.smoothNormal);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                if (dot(nCS, nCS) > 1e-6)
                    cs.xy += normalize(nCS) * (max(0.0, _OutlinePx * (1.0 + _MadMaxOutlineDelta)) * 2.0 / _ScreenParams.xy) * cs.w;
                o.positionCS = cs;
                o.fog = ComputeFogFactor(cs.z);
                return o;
            }
            half4 frag (Varyings i) : SV_Target { clip(CutMask(i.ws)); return half4(lerp(MixFog(_OutlineColor.rgb, i.fog), _OutlineColor.rgb, _NoFog), 1); }   // outlines fade into fog too
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; };
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
                return o;
            }
            half4 frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 p : POSITION; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; };
            V vert (A i) { V o; o.ws = WindSway(TransformObjectToWorld(i.p.xyz), i.p.xyz, i.color.a); o.positionCS = TransformWorldToHClip(o.ws); return o; }
            half frag (V i) : SV_Target { clip(CutMask(i.ws)); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 ws : TEXCOORD1; };
            Varyings vert (Attributes i) { Varyings o; o.ws = WindSway(TransformObjectToWorld(i.positionOS.xyz), i.positionOS.xyz, i.color.a); o.positionCS = TransformWorldToHClip(o.ws); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag (Varyings i) : SV_Target { clip(CutMask(i.ws)); return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
