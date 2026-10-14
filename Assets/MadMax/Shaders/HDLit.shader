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
        [NoScaleOffset] _CarveMask ("Carve Mask (3D, R: 1 kept, 0 carved)", 3D) = "" {}
        _CarveOn ("Carved (destructible props)", Range(0,1)) = 0
        _CarveInset ("Carve Sample Inset (m)", Float) = 0.04
        _CarveRow0 ("Carve World->Grid Row 0", Vector) = (1,0,0,0)
        _CarveRow1 ("Carve World->Grid Row 1", Vector) = (0,1,0,0)
        _CarveRow2 ("Carve World->Grid Row 2", Vector) = (0,0,1,0)
        _CarveMin ("Carve Grid Min (voxels)", Vector) = (0,0,0,0)
        _CarveSize ("Carve Grid Size (voxels)", Vector) = (1,1,1,0)
        [NoScaleOffset] _TerrainAlbedo ("Terrain Detail Albedo (array, A smooth)", 2DArray) = "" {}
        [NoScaleOffset] _TerrainNormal ("Terrain Detail Normal (array)", 2DArray) = "" {}
        _TerrainScale0 ("Terrain Tiles per m 0-3", Vector) = (0.5,0.5,0.5,0.5)
        _TerrainScale1 ("Terrain Tiles per m 4-7", Vector) = (0.5,0.5,0.5,0.5)
        _TerrainScale2 ("Terrain Tiles per m 8", Vector) = (0.5,0.5,0.5,0.5)
        [NoScaleOffset] _RoadAlbedo ("Road Layers (array: asphalt x4, sidewalk, kerb, paint, decals)", 2DArray) = "" {}
        [NoScaleOffset] _RoadNormal ("Road Layer Normals (array)", 2DArray) = "" {}
        _RoadTex ("Road Layers Loaded", Range(0,1)) = 0
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
            float4 _CarveRow0, _CarveRow1, _CarveRow2;   // world -> voxel grid coordinates (voxel centres on integers)
            float4 _CarveMin, _CarveSize;
            half _CarveOn;
            float _CarveInset;
            float4 _TerrainScale0, _TerrainScale1, _TerrainScale2;
            half _RoadTex;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE3D(_CarveMask); SAMPLER(sampler_CarveMask);
        TEXTURE2D_ARRAY(_TerrainAlbedo); SAMPLER(sampler_TerrainAlbedo);
        TEXTURE2D_ARRAY(_TerrainNormal); SAMPLER(sampler_TerrainNormal);
        TEXTURE2D_ARRAY(_RoadAlbedo); SAMPLER(sampler_RoadAlbedo);
        TEXTURE2D_ARRAY(_RoadNormal);

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
        // destructible props (HDCarve): the HD surface goes where its voxel cell (half a voxel inside the surface) was carved
        void CarveClip(float3 ws, float3 nws)
        {
            if (_CarveOn < 0.5) return;
            float4 p = float4(ws - nws * _CarveInset, 1.0);
            float3 g = float3(dot(_CarveRow0, p), dot(_CarveRow1, p), dot(_CarveRow2, p));
            float3 uvw = (g - _CarveMin.xyz + 0.5) / max(_CarveSize.xyz, 1.0);
            if (any(uvw < 0.0) || any(uvw > 1.0)) return;
            clip(SAMPLE_TEXTURE3D_LOD(_CarveMask, sampler_CarveMask, uvw, 0).r - 0.5);
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma shader_feature_local _TERRAIN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT;
                half4 color : COLOR; float2 uv : TEXCOORD0; float2 wear : TEXCOORD1;
                #if defined(_TERRAIN)
                float4 splat0 : TEXCOORD2; float4 splat1 : TEXCOORD3; float4 town : TEXCOORD4; float4 town2 : TEXCOORD5;
                #endif
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; half4 color : COLOR; half fog : TEXCOORD4;
                float3 positionOS : TEXCOORD5; half wear : TEXCOORD6; float3 carveWS : TEXCOORD7;
                #if defined(_TERRAIN)
                half4 splat0 : TEXCOORD8; half4 splat1 : TEXCOORD9; float2 road : TEXCOORD10;
                float4 town : TEXCOORD11; float4 town2 : TEXCOORD12;
                #endif
            };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionOS = i.positionOS.xyz;
                o.carveWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS = WindSway(o.carveWS, i.positionOS.xyz, i.color.a);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                float s = i.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = half4(TransformObjectToWorldDir(i.tangentOS.xyz), s);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.color = i.color;
                o.wear = saturate(i.wear.x);
                o.fog = ComputeFogFactor(o.positionCS.z);
                #if defined(_TERRAIN)
                o.splat0 = i.splat0; o.splat1 = i.splat1; o.road = i.wear; o.town = i.town; o.town2 = i.town2;
                o.wear = 0;
                #endif
                return o;
            }

            #if defined(_TERRAIN)
            // HD terrain (World/HDTerrain): nine ground classes blended by the mesh's corner weights, each a tileable detail
            // albedo (around mid grey, A = smoothness) times 2 x the vertex colour, top projection plus the sides on slopes;
            // a second, slower sample of the same layer breaks the tiling up.
            void TerrainSurface(float3 ws, half3 nW, half4 s0, half4 s1, half3 vcol, out half3 albedo, out half3 n, out half smooth)
            {
                half w[9] = { s0.x, s0.y, s0.z, s0.w, s1.x, s1.y, s1.z, s1.w, saturate(1.0h - dot(s0, 1.0h) - dot(s1, 1.0h)) };
                float sc[9] = { _TerrainScale0.x, _TerrainScale0.y, _TerrainScale0.z, _TerrainScale0.w,
                                _TerrainScale1.x, _TerrainScale1.y, _TerrainScale1.z, _TerrainScale1.w, _TerrainScale2.x };
                float3 bw = pow(abs((float3)nW), 4.0);
                bw /= max(dot(bw, 1.0), 1e-4);
                half3 a = 0; half sm = 0; half3 pert = 0; half tw = 0;
                [unroll] for (int k = 0; k < 9; k++)
                {
                    half wk = w[k];
                    if (wk < 0.01h) continue;
                    float s = sc[k];
                    half4 A = 0; half3 p = 0;
                    if (bw.y > 0.02)
                    {
                        float2 uv = ws.xz * s;
                        half4 t = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedo, sampler_TerrainAlbedo, uv, k);
                        half4 t2 = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedo, sampler_TerrainAlbedo, float2(uv.y, -uv.x) * 0.231 + 0.37, k);
                        t.rgb *= lerp(1.0h, t2.rgb * 2.0h, 0.45h);
                        half3 nt = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(_TerrainNormal, sampler_TerrainNormal, uv, k));
                        A += t * (half)bw.y; p += half3(nt.x, 0, nt.y) * (half)bw.y;
                    }
                    if (bw.x > 0.02)
                    {
                        float2 uv = ws.zy * s;
                        half4 t = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedo, sampler_TerrainAlbedo, uv, k);
                        half3 nt = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(_TerrainNormal, sampler_TerrainNormal, uv, k));
                        A += t * (half)bw.x; p += half3(0, nt.y, nt.x) * (half)bw.x;
                    }
                    if (bw.z > 0.02)
                    {
                        float2 uv = ws.xy * s;
                        half4 t = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedo, sampler_TerrainAlbedo, uv, k);
                        half3 nt = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(_TerrainNormal, sampler_TerrainNormal, uv, k));
                        A += t * (half)bw.z; p += half3(nt.x, nt.y, 0) * (half)bw.z;
                    }
                    a += A.rgb * wk; sm += A.a * wk; pert += p * wk; tw += wk;
                }
                tw = max(tw, 1e-3h);
                albedo = saturate(vcol * (a / tw) * 2.0h);
                smooth = sm / tw;
                n = normalize(nW + pert / tw);
            }

            // ---- streets (World/HDTerrain road layers R*: 0-3 asphalt fresh / worn / cracked / patched, 4 sidewalk,
            // 5 kerb, 6 paint wear (A = coverage), 7 manhole, 8 drain, 9 oil, 10 pothole; procedural when absent)
            float Band(float v, float lo, float hi, float aa) { return smoothstep(lo - aa, lo + aa, v) * (1.0 - smoothstep(hi - aa, hi + aa, v)); }
            // bars of width w centred on x = k * period
            float Bars(float x, float period, float w, float aa) { float d = abs(frac(x / period + 0.5) - 0.5) * period; return 1.0 - smoothstep(w * 0.5 - aa, w * 0.5 + aa, d); }
            float Line(float d, float w, float aa) { return 1.0 - smoothstep(w * 0.5 - aa, w * 0.5 + aa, abs(d)); }
            float Hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 RoadLayer(float2 uv, int k) { return SAMPLE_TEXTURE2D_ARRAY(_RoadAlbedo, sampler_RoadAlbedo, uv, k); }
            half3 RoadBump(float2 uv, int k) { half3 t = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(_RoadNormal, sampler_RoadAlbedo, uv, k)); return half3(t.x, 0.0h, t.y); }

            // a decal at local uv 0..1 (layer k): linear colour + cut-out, and its bump in world xz (uv along x/z)
            half4 Decal(float2 uv, float2 dx, float2 dy, int k, out half3 bump)
            {
                bump = 0;
                half inside = (half)(all(uv > 0.0) && all(uv < 1.0));
                if (inside < 0.5h) return 0;
                if (_RoadTex > 0.5h)
                {
                    half4 t = SAMPLE_TEXTURE2D_ARRAY_GRAD(_RoadAlbedo, sampler_RoadAlbedo, uv, k, dx, dy);
                    half3 nt = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY_GRAD(_RoadNormal, sampler_RoadAlbedo, uv, k, dx, dy));
                    bump = half3(nt.x, 0.0h, nt.y) * t.a;
                    return half4(SRGBToLinear(t.rgb) * 0.7h, t.a);
                }
                float2 c = uv - 0.5;
                float r = length(c);
                if (k == 7) return half4(half3(0.03h, 0.028h, 0.026h) * (1.0h + (half)Bars(c.x + c.y, 0.08, 0.03, 0.01) * 0.5h), (half)(r < 0.45));
                if (k == 8) return half4(lerp(half3(0.04h, 0.036h, 0.033h), half3(0.006h, 0.006h, 0.006h), (half)(Bars(c.x, 0.09, 0.04, 0.01) * step(abs(c.y), 0.38))), (half)(abs(c.x) < 0.36 && abs(c.y) < 0.44));
                if (k == 9) return half4(0.012h, 0.011h, 0.012h, (half)(saturate((0.42 - r - Value3(float3(c * 9.0, 1.0)) * 0.12) * 12.0) * 0.6));
                return half4(0.02h, 0.019h, 0.018h, (half)saturate((0.38 - r - Value3(float3(c * 7.0, 3.0)) * 0.1) * 14.0));
            }

            // tarmac: the photo layers (fresh / worn with cracked and patched stretches, more of those as worn goes up)
            // or a procedural aggregate; a dark neutral grey either way
            void Tarmac(float3 pf, half worn, out half3 col, out half sm, out half3 pert)
            {
                half grain = (half)(Value3(pf * 23.0) * 0.55 + Value3(pf * 7.3) * 0.45);
                half patch = (half)Value3(pf * 0.11);
                col = half3(0.043h, 0.041h, 0.04h) * lerp(0.78h, 1.22h, grain) * lerp(0.88h, 1.1h, patch);
                sm = 0.3h; pert = 0;
                if (_RoadTex > 0.5h)
                {
                    float2 uv = pf.xz * (1.0 / 3.2);
                    half wv = (half)saturate((Value3(pf * 0.06 + 3.1) - 0.5) * 3.0 + 0.5);
                    half crk = (half)saturate((Value3(pf * 0.045 + 11.0) - 0.64 + worn * 0.3) * 5.0);
                    half pat = (half)saturate((Value3(pf * 0.035 + 29.0) - 0.7 + worn * 0.25) * 5.0);
                    float2 u1 = uv * 0.83 + float2(0.37, 0.71);
                    half4 a = lerp(RoadLayer(uv, 0), RoadLayer(u1, 1), wv);
                    half3 b = lerp(RoadBump(uv, 0), RoadBump(u1, 1), wv);
                    if (crk > 0.01h) { float2 u2 = uv * 0.9 + float2(0.21, 0.67); a = lerp(a, RoadLayer(u2, 2), crk); b = lerp(b, RoadBump(u2, 2), crk); }
                    if (pat > 0.01h) { float2 u3 = uv * 0.7 + float2(0.59, 0.13); a = lerp(a, RoadLayer(u3, 3), pat); b = lerp(b, RoadBump(u3, 3), pat); }
                    col = half3(0.05h, 0.048h, 0.046h) * a.rgb * 2.0h * lerp(0.92h, 1.06h, patch);
                    sm = a.a; pert = b * 0.8h;
                }
            }

            // paint coverage left by wear (photo chips, patchy fading)
            half PaintWear(float3 pf, half fade)
            {
                half chips = _RoadTex > 0.5h ? RoadLayer(pf.xz * 0.5, 6).a : (half)saturate((Value3(pf * 6.0) - 0.22) * 3.0);
                return chips * lerp(1.0h - fade, 1.0h, (half)saturate((Value3(pf * 0.9 + 5.0) - 0.2) * 3.0));
            }

            // tar-sealed cracks, oil stains and potholes on asphalt (amt 0 kept .. 1 neglected; oil + = more stains)
            void AsphaltWear(float3 pf, half amt, half oil, inout half3 tar, inout half sm, inout half3 pert)
            {
                float cv = Value3(pf * 0.55 + 7.0);
                float cw = max(fwidth(cv), 1e-4);
                half seal = (half)((1.0 - smoothstep(0.012 - cw, 0.012 + cw, abs(cv - 0.5))) * saturate((Value3(pf * 0.08 + 4.0) - 0.62 + amt * 0.25) * 6.0));
                tar = lerp(tar, half3(0.012h, 0.011h, 0.011h), seal * 0.9h); sm = lerp(sm, 0.55h, seal);
                half3 bump;
                float2 c = floor(pf.xz / 5.0);
                float h = Hash2(c), h2 = Hash2(c + 17.3), h3 = Hash2(c + 5.9);
                float r = 0.5 + 0.6 * h2;
                float2 o = (c + 0.3 + 0.4 * float2(h3, frac(h2 * 7.1))) * 5.0;
                float sn, cs; sincos(h3 * 6.2832, sn, cs);
                float2 q = pf.xz - o;
                float2 uv = float2(q.x * cs - q.y * sn, q.x * sn + q.y * cs) / (2.0 * r) + 0.5;
                float2 dx = ddx(uv), dy = ddy(uv);
                if (h > 0.66 - oil)
                {
                    half4 d = Decal(uv, dx, dy, 9, bump);
                    tar = lerp(tar, d.rgb, d.a * 0.8h); sm = lerp(sm, 0.5h, d.a * 0.6h);
                }
                float2 c2 = floor(pf.xz / 9.0 + 0.37);
                float g = Hash2(c2 + 41.0), g2 = Hash2(c2 + 3.3);
                float r2 = 0.3 + 0.25 * g2;
                float2 uv2 = (pf.xz - (c2 - 0.37 + 0.3 + 0.4 * float2(g2, frac(g * 13.7))) * 9.0) / (2.0 * r2) + 0.5;
                float2 dx2 = ddx(uv2), dy2 = ddy(uv2);
                if (g > 0.975 - amt * 0.1)
                {
                    half4 d = Decal(uv2, dx2, dy2, 10, bump);
                    tar = lerp(tar, d.rgb, d.a); pert += bump * 1.5h; sm = lerp(sm, 0.2h, d.a);
                }
            }

            // straight-ahead arrow: tip at v = 0 pointing to -v, head 0.95 m long and 0.8 m wide, shaft to v = 3
            half Arrow(float u, float v, float aa)
            {
                float shaft = max(abs(u) - 0.08, max(0.9 - v, v - 3.0));
                float head = max(abs(u) - 0.4 * saturate(v / 0.95), max(-v, v - 0.95));
                return (half)(1.0 - smoothstep(-aa, aa, min(shaft, head)));
            }

            // towns (TownPlan.Surface per vertex: fr = marking frame, tw = carriageway edge, sidewalk edge, half width, code,
            // tw2 = junction distance, plan x, plan z, plaza): crisp asphalt from the carriageway-edge distance field, granite
            // kerbs, slab sidewalks and paver squares in the plan's frame, car parks, paint in each street's own frame
            // (zebras, stop and give-way lines, arrows, centre and lane lines, bays, roundabout lines) and light wear
            // (sealed cracks, stains, a rare pothole, skids, manholes, kerb drains). Where neighbouring vertices disagree
            // (the plan's rim) the cells' colours stay.
            void TownStreets(float3 ws, float2 fr, float4 tw, float4 tw2, half cover, inout half3 albedo, inout half3 n, inout half smooth)
            {
                float3 pf = float3(ws.x, 0.0, ws.z);
                float pxm = max(fwidth(ws.x) + fwidth(ws.z), 1e-5);
                float edge = tw.x;
                half okE = (half)saturate(2.6 - fwidth(edge) / pxm) * cover;                     // no early out: gradients below
                half okM = (half)(saturate(2.6 - fwidth(fr.x) / pxm) * saturate(2.6 - fwidth(fr.y) / pxm) * step(fwidth(tw.w), 0.01) * step(fwidth(tw.z), 0.01));
                float code = round(tw.w);
                int kind = (int)fmod(code, 8.0);
                float flags = floor(code / 8.0);
                float aa = max(fwidth(edge), 1e-4), aw = max(fwidth(tw.y), 1e-4);
                float lat = fr.x, t = fr.y, alat = abs(lat), hw = tw.z, e = tw2.x;
                float aL = max(fwidth(lat), 1e-4), aT = max(fwidth(t), 1e-4), aE = max(fwidth(e), 1e-4);

                half tarmac = (half)(1.0 - smoothstep(-aa, aa, edge));
                half kerb = (half)Band(edge, 0.0, 0.3, aa);
                half walk = (half)((1.0 - smoothstep(-aw, aw, tw.y)) * smoothstep(0.3 - aa, 0.3 + aa, edge));
                if (kind >= 5)                                                                          // car park / forecourt tarmac
                {
                    float lotD = max(abs(lat) - hw, max(-t, t - e));
                    float al = max(fwidth(lotD), 1e-4);
                    tarmac = max(tarmac, (half)(1.0 - smoothstep(-al, al, lotD)) * okM * (1.0h - walk) * (1.0h - kerb));
                }
                half plaza = (half)smoothstep(0.4, 0.6, tw2.w) * (1.0h - tarmac) * (1.0h - kerb) * (1.0h - walk);
                float2 pp = tw2.yz;
                float ap = max(fwidth(pp.x) + fwidth(pp.y), 1e-4) * 0.5;
                half3 col = albedo; half sm = smooth; half3 pert = 0;

                if (walk > 0.001h)                                                                      // 1.5 m slabs, hairline joints
                {
                    float2 g = abs(frac(pp / 1.5 + 0.5) - 0.5) * 1.5;
                    half joint = (half)(1.0 - smoothstep(0.012 - ap, 0.012 + ap, min(g.x, g.y)));
                    half tone = (half)Hash2(floor(pp / 1.5) + 0.5);
                    half4 d = _RoadTex > 0.5h ? RoadLayer(ws.xz / 3.0, 4) : half4(0.5h + (half)(Value3(pf * 9.0) - 0.5) * 0.1h, 0.5h, 0.5h, 0.2h);
                    if (_RoadTex <= 0.5h) d.gb = d.rr;
                    half3 slab = half3(0.25h, 0.243h, 0.232h) * lerp(0.9h, 1.06h, tone) * d.rgb * 2.0h;
                    if (tone > 0.96h) slab *= 0.78h;                                                     // a stained slab
                    slab = lerp(slab, half3(0.08h, 0.077h, 0.073h), joint * 0.85h);
                    col = lerp(col, slab, walk); sm = lerp(sm, d.a, walk);
                    if (_RoadTex > 0.5h) pert += RoadBump(ws.xz / 3.0, 4) * walk * 0.6h;
                }
                if (kerb > 0.001h)                                                                      // granite: shaded face, lit arris, joints
                {
                    half4 d = _RoadTex > 0.5h ? RoadLayer(ws.xz / 1.5, 5) : half4(0.5h + (half)(Value3(pf * 31.0) - 0.5) * 0.25h, 0.0h, 0.0h, 0.25h);
                    if (_RoadTex <= 0.5h) d.gb = d.rr;
                    half3 k = half3(0.3h, 0.295h, 0.28h) * d.rgb * 2.0h;
                    half face = (half)(1.0 - smoothstep(0.03, 0.07, edge));
                    half arris = (half)Band(edge, 0.07, 0.11, aa);
                    k *= lerp(1.0h, 0.5h, face) * (1.0h + arris * 0.3h);
                    k = lerp(k, k * 0.5h, (half)Bars(t, 1.0, 0.02, aT) * okM);
                    col = lerp(col, k, kerb); sm = lerp(sm, d.a, kerb);
                }
                if (plaza > 0.001h)                                                                     // pavers: 0.5 m checker, 3 m bands
                {
                    float2 g3 = abs(frac(pp / 3.0 + 0.5) - 0.5) * 3.0, g1 = abs(frac(pp / 0.5 + 0.5) - 0.5) * 0.5;
                    half j3 = (half)(1.0 - smoothstep(0.04 - ap, 0.04 + ap, min(g3.x, g3.y)));
                    half j1 = (half)(1.0 - smoothstep(0.008 - ap, 0.008 + ap, min(g1.x, g1.y)));
                    half chk = (half)abs(fmod(floor(pp.x * 2.0) + floor(pp.y * 2.0), 2.0));
                    half4 d = _RoadTex > 0.5h ? RoadLayer(ws.xz / 3.0 + 0.5, 4) : half4(0.5h, 0.5h, 0.5h, 0.2h);
                    half3 pv = lerp(half3(0.195h, 0.128h, 0.076h), half3(0.15h, 0.098h, 0.058h), chk) * d.rgb * 2.0h;
                    pv *= lerp(0.92h, 1.08h, (half)Hash2(floor(pp * 2.0)));
                    pv = lerp(pv, half3(0.072h, 0.052h, 0.037h), max(j3, j1 * 0.7h));
                    col = lerp(col, pv, plaza); sm = lerp(sm, 0.2h, plaza);
                }
                if (tarmac > 0.001h)
                {
                    half3 tar; half tsm; half3 tp;
                    Tarmac(pf, 0.0h, tar, tsm, tp);
                    half gutter = (half)smoothstep(-0.7, -0.05, edge) * (half)Value3(pf * 1.3);       // swept dust along the kerbs
                    tar = lerp(tar, tar * 1.6h + half3(0.012h, 0.009h, 0.006h), gutter * 0.6h);
                    AsphaltWear(pf, 0.0h, kind == 5 ? 0.25h : 0.0h, tar, tsm, tp);
                    half paint = 0, yellow = 0;
                    if (okM > 0.001h)
                    {
                        float sideIn = (fmod(floor(flags / 4.0), 2.0) >= 1.0) ? lat : -lat;
                        half incoming = (half)smoothstep(-aL, aL, sideIn);
                        bool zebra = fmod(flags, 2.0) >= 1.0, ringEnd = fmod(floor(flags / 2.0), 2.0) >= 1.0;
                        if (kind >= 1 && kind <= 3)
                        {
                            float z0 = ringEnd ? 1.6 : 0.3, z1 = z0 + floor(code / 64.0) * 0.25 - 0.4;
                            if (zebra) paint += (half)(Band(e, z0, z1, aE) * Bars(lat, 1.0, 0.5, aL) * (1.0 - smoothstep(hw - 0.2 - aL, hw - 0.2 + aL, alat)));
                            float s0 = zebra ? z1 + 0.5 : 0.4;
                            half lim = (half)(1.0 - smoothstep(hw - 0.3 - aL, hw - 0.3 + aL, alat));
                            if (zebra || !ringEnd) paint += (half)Band(e, s0, s0 + 0.35, aE) * lim * incoming;       // stop line
                            if (ringEnd) paint += (half)(Band(e, 0.15, 0.5, aE) * Bars(lat + 0.225, 0.9, 0.45, aL)) * lim * incoming;   // give way
                            half run = (half)smoothstep(s0 + 0.8, s0 + 1.1, e);
                            if (kind <= 2) yellow += (half)Line(alat - 0.17, 0.11, aL) * run;                     // double yellow
                            if (kind == 1 && hw >= 4.8) paint += (half)(Line(alat - hw * 0.5 - 0.1, 0.12, aL) * Bars(t - 2.0, 9.0, 4.0, aT)) * run;
                            if (kind == 3) paint += (half)(Line(alat, 0.12, aL) * Bars(t - 1.5, 6.0, 3.0, aT)) * run;
                            if (hw >= 3.0 && e < 98.0)                                                         // arrows in each incoming lane
                            {
                                float lw = hw >= 4.8 ? hw * 0.5 : hw;
                                float u = alat - (floor(alat / lw) + 0.5) * lw;
                                paint += Arrow(u, e - (s0 + 2.0), aL) * incoming * lim;
                            }
                            // skid marks: now and then a braking car's two tyre streaks
                            float sc = floor(t / 23.0);
                            float sh = Hash2(float2(sc, hw * 3.1 + 0.5));
                            if (sh > 0.8 && e > 2.0)
                            {
                                float lc = (sh > 0.9 ? 1.0 : -1.0) * hw * 0.5 + sin(t * 0.21 + sh * 40.0) * 0.3;
                                float st = t - sc * 23.0;
                                half skid = (half)(max(Line(lat - lc - 0.75, 0.17, aL), Line(lat - lc + 0.75, 0.17, aL)) * Band(st, 3.0, 9.0 + 8.0 * frac(sh * 11.0), aT * 4.0 + 1.5));
                                tar = lerp(tar, tar * 0.45h, skid * 0.55h);
                            }
                        }
                        else if (kind == 4)                                                                    // roundabout ring
                        {
                            paint += (half)Line(lat + hw - 0.55, 0.12, aL);
                            if (hw >= 3.4) paint += (half)(Line(lat, 0.12, aL) * Bars(t, 3.0, 1.5, aT));
                        }
                        else if (kind == 5)                                                                    // car park bays
                        {
                            half rows = (half)max(Band(t, 1.0, 6.0, aT), e > 15.0 ? Band(t, e - 6.0, e - 1.0, aT) : 0.0);
                            half bay = (half)Bars(lat + 100.25, 2.7, 0.12, aL) * rows;
                            half rowLine = (half)(Line(t - 6.0, 0.12, aT) + (e > 15.0 ? Line(t - (e - 6.0), 0.12, aT) : 0.0));
                            paint += (bay + rowLine) * (half)Band(lat, -hw + 0.8, hw - 0.8, aL);
                        }
                        paint = saturate(paint) * okM; yellow = saturate(yellow) * okM;
                    }
                    half wear = PaintWear(pf, 0.45h);
                    tar = lerp(tar, half3(0.6h, 0.58h, 0.54h), paint * wear * 0.92h);
                    tar = lerp(tar, half3(0.62h, 0.4h, 0.08h), yellow * wear * 0.9h);
                    tsm = lerp(tsm, 0.42h, (paint + yellow) * wear * 0.5h);
                    // street furniture in the road: manholes in the lanes, drains in the gutters
                    if (okM > 0.001h && kind <= 3 && e > 2.0)
                    {
                        half3 bump;
                        float mc = floor(t / 31.0);
                        float mh = Hash2(float2(mc, hw * 1.7));
                        float2 muv = float2(lat - (mh - 0.5) * hw, t - (mc * 31.0 + 8.0 + mh * 15.0)) / 0.8 + 0.5;
                        float2 mdx = ddx(muv), mdy = ddy(muv);
                        float dc = floor((t + 7.0) / 24.0);
                        float dh = Hash2(float2(dc, sign(lat) + hw));
                        float2 duv = float2((alat - (hw - 0.3)) / 0.66, (t - (dc * 24.0 + 5.0)) / 0.9) + 0.5;
                        float2 ddx2 = ddx(duv), ddy2 = ddy(duv);
                        if (mh > 0.35 && alat < hw - 0.6)
                        {
                            half4 d = Decal(muv, mdx, mdy, 7, bump);
                            tar = lerp(tar, d.rgb, d.a); tsm = lerp(tsm, 0.35h, d.a); tp += bump * 1.2h;
                        }
                        if (dh > 0.25)
                        {
                            half4 d = Decal(duv, ddx2, ddy2, 8, bump);
                            tar = lerp(tar, d.rgb, d.a); tp += bump * 1.2h;
                        }
                    }
                    col = lerp(col, tar, tarmac); sm = lerp(sm, tsm, tarmac); pert += tp * tarmac;
                }
                half ok = okE;
                albedo = lerp(albedo, col, ok);
                smooth = lerp(smooth, sm, ok);
                n = normalize(lerp(n, normalize(n + pert), ok));
            }
            #endif

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
                CarveClip(i.carveWS, normalize(i.normalWS));
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
                #if defined(_TERRAIN)
                // ruts (vertex alpha = depth): the mud layer, darker and wetter, per pixel
                half rut = saturate(1.0h - i.color.a);
                half4 s1 = i.splat1;
                if (rut > 0.01h) { half4 s0r = i.splat0 * (1.0h - rut); s1 = s1 * (1.0h - rut); s1.w += rut; i.splat0 = s0r; }
                TerrainSurface(i.positionWS, nW, i.splat0, s1, vcol * (1.0h - rut * 0.25h), albedo, n, smooth);
                metal = 0.0h; ao = 1.0h;
                // snow cover (the 9th class weight) lies over the streets as it does over the ground
                half roadCover = 1.0h - saturate(1.0h - dot(i.splat0, 1.0h) - dot(s1, 1.0h));
                if (i.town.z > 0.01)
                    TownStreets(i.positionWS, i.road, i.town, i.town2, roadCover, albedo, n, smooth);
                else
                {
                // highways (uv1.x = distance to the centre line, valid out to the shoulder): one crisp asphalt band with a
                // crumbling edge, tyre-polished lanes, faded edge lines and the yellow centre dashes, a dusty gravel
                // shoulder outside; the per-cell asphalt colours under it only show where the edge has broken away
                float rd = abs(i.road.x);
                // a true distance field changes ~1 m per m; across a junction or past a road's end neighbouring vertices
                // measure to different roads (or sides) and the interpolation is not a distance: keep the cells' asphalt there
                float grad = fwidth(i.road.x) / max(fwidth(i.positionWS.x) + fwidth(i.positionWS.z), 1e-5);
                half consistent = (half)saturate(2.6 - grad) * roadCover;
                if (rd < 6.0 && consistent > 0.0h)
                {
                    float aa = max(fwidth(rd), 1e-4);
                    float crumble = Value3(float3(i.positionWS.x, 0.0, i.positionWS.z) * 1.3) * 0.32 + Value3(i.positionWS * 4.7) * 0.14;
                    half tarmac = (half)(1.0 - smoothstep(4.12 - crumble - aa, 4.12 - crumble + aa, rd)) * consistent;
                    // tarmac: the photo layers (worn, cracked and patched stretches) or a procedural aggregate with a
                    // gentle, large patchwork of repairs (the terrain asphalt's soft blotches read as smudges from above)
                    float3 pf = float3(i.positionWS.x, 0.0, i.positionWS.z);
                    half3 tar; half tsm; half3 tp;
                    Tarmac(pf, 0.6h, tar, tsm, tp);
                    half lanes = (half)(1.0 - smoothstep(0.35, 0.75, abs(rd - 2.0)));              // tyre lanes: darker, smoother
                    tar *= 1.0h - lanes * 0.18h;
                    tar *= lerp(1.0h, 1.35h, (half)smoothstep(2.9, 4.1, rd));                      // sun-bleached, dusty edges
                    AsphaltWear(pf, 0.7h, 0.0h, tar, tsm, tp);
                    half wear = PaintWear(pf, 0.6h);
                    half edgeLine = (half)(1.0 - smoothstep(0.07 - aa, 0.07 + aa, abs(rd - 3.62)));
                    edgeLine *= (half)saturate((Value3(i.positionWS * 1.9) - 0.3) * 3.0);
                    tar = lerp(tar, half3(0.42h, 0.40h, 0.35h), edgeLine * 0.75h * wear);
                    float aaC = max(fwidth(i.road.x), 1e-4);
                    half paintMask = (half)(1.0 - smoothstep(0.12 - aaC, 0.12 + aaC, abs(i.road.x)));
                    float ph = frac(i.road.y / 6.0);
                    float ab = max(fwidth(i.road.y) / 6.0, 1e-4);
                    paintMask *= (half)(smoothstep(0.0, ab, ph) * (1.0 - smoothstep(0.5 - ab, 0.5 + ab, ph)));
                    paintMask *= (half)saturate((Value3(i.positionWS * 2.3) - 0.12) * 5.0) * wear;
                    tar = lerp(tar, half3(0.62h, 0.38h, 0.07h), paintMask * 0.92h);
                    half shoulder = (1.0h - tarmac) * (half)(1.0 - smoothstep(4.5, 5.15, rd)) * consistent;
                    albedo = lerp(albedo, albedo * half3(0.8h, 0.78h, 0.76h) + half3(0.012h, 0.011h, 0.01h), shoulder * 0.7h);
                    albedo = lerp(albedo, tar, tarmac);
                    smooth = lerp(smooth, lerp(tsm, 0.42h, lanes * 0.3h + paintMask * 0.5h), tarmac);
                    n = normalize(lerp(n, normalize(nW + tp), tarmac));                               // flatter than the ground around it
                }
                }
                #else
                if (_NormalStrength > 0.001h)
                {
                    half3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                    half3 t = normalize(i.tangentWS.xyz);
                    half3 b = cross(nW, t) * i.tangentWS.w;
                    n = normalize(tn.x * t + tn.y * b + tn.z * nW);
                }
                #endif
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
                // screen-space reflections (settings REFLECTIONS) where the ray found something, the sky where it didn't
                half3 env = SkyFill() * 2.2h * dayF;
                half4 ssr = GetScreenSpaceReflection(GetNormalizedScreenSpaceUV(i.positionCS), i.positionWS, rough);
                env = lerp(env, ssr.rgb, saturate(ssr.a));
                c += fr * env * ao * smooth * _SpecularScale;
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 carveWS : TEXCOORD1; float3 carveN : TEXCOORD2; };
            Varyings vert (Attributes i)
            {
                Varyings o;
                o.carveWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 ws = WindSway(o.carveWS, i.positionOS.xyz, i.color.a);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                o.carveN = n;
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
                CarveClip(i.carveWS, normalize(i.carveN));
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
            struct A { float4 p : POSITION; float3 n : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; float2 uv : TEXCOORD1; float3 carveWS : TEXCOORD2; float3 carveN : TEXCOORD3; };
            V vert (A i) { V o; o.carveWS = TransformObjectToWorld(i.p.xyz); o.carveN = TransformObjectToWorldNormal(i.n); o.ws = WindSway(o.carveWS, i.p.xyz, i.color.a); o.positionCS = TransformWorldToHClip(o.ws); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half frag (V i) : SV_Target
            {
                clip(CutMask(i.ws));
                CarveClip(i.carveWS, normalize(i.carveN));
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
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 ws : TEXCOORD1; float2 uv : TEXCOORD2; float3 carveWS : TEXCOORD3; };
            Varyings vert (Attributes i)
            {
                Varyings o;
                o.carveWS = TransformObjectToWorld(i.positionOS.xyz);
                o.ws = WindSway(o.carveWS, i.positionOS.xyz, i.color.a);
                o.positionCS = TransformWorldToHClip(o.ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                clip(CutMask(i.ws));
                CarveClip(i.carveWS, normalize(i.normalWS));
                ClipOpacity(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a, i.positionCS.xy);
                half smooth = 0.0h;
                #if defined(_WRITE_SMOOTHNESS)
                smooth = lerp(_Smoothness, SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv).a, _MaskStrength);   // SSR reads it from alpha
                #endif
                return half4(normalize(i.normalWS), smooth);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
