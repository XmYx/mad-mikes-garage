// Lake, river and sea surfaces: vertex-coloured, alpha blended, fogged, bent with the planet's horizon (_MadMaxCurve)
// like every PixelVoxel surface so the shoreline stays put in the distance.
Shader "MadMax/Water"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            float _MadMaxCurve;      // 1 / (2 R): the horizon drop per squared metre
            float _MadMaxNight;
            float4 _MadMaxWaterHole;   // xz centre, radius (0 = none), alpha left in the middle
            // boat hulls (HullMask): world-to-hull matrices, (zMin, zMax, keel, gunwale), 16 half-widths along the keel
            #define MADMAX_HULLS 8
            float4x4 _MadMaxHullW2L[MADMAX_HULLS];
            float4 _MadMaxHullBox[MADMAX_HULLS];
            float4 _MadMaxHullWidth[MADMAX_HULLS * 4];
            float _MadMaxHullCount;
            struct A { float4 positionOS : POSITION; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; float2 xz : TEXCOORD1; float3 ws : TEXCOORD2; };

            float HullSlice(int h, int k)
            {
                float4 v = _MadMaxHullWidth[h * 4 + (k >> 2)];
                int c = k & 3;
                return c == 0 ? v.x : c == 1 ? v.y : c == 2 ? v.z : v.w;
            }

            // inside a hull's plan outline between keel and gunwale: the hull (or its deck) is there, not the sea
            bool InHull(float3 ws)
            {
                int n = (int)_MadMaxHullCount;
                [loop] for (int h = 0; h < MADMAX_HULLS; h++)
                {
                    if (h >= n) break;
                    float3 lp = mul(_MadMaxHullW2L[h], float4(ws, 1.0)).xyz;
                    float4 b = _MadMaxHullBox[h];
                    if (lp.z <= b.x || lp.z >= b.y || lp.y < b.z || lp.y > b.w) continue;
                    float s = saturate((lp.z - b.x) / max(1e-4, b.y - b.x)) * 15.0;
                    int k = min((int)floor(s), 14);
                    float w = lerp(HullSlice(h, k), HullSlice(h, k + 1), s - k);
                    if (abs(lp.x) < w) return true;
                }
                return false;
            }
            V vert (A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.xz = ws.xz;
                o.ws = ws;
                float2 d = ws.xz - _WorldSpaceCameraPos.xz;
                ws.y -= dot(d, d) * _MadMaxCurve;
                o.positionCS = TransformWorldToHClip(ws);
                o.color = i.color;
                o.color.rgb *= 1.0h - _MadMaxNight * 0.55h;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag (V i) : SV_Target
            {
                if (_MadMaxHullCount > 0.5 && InHull(i.ws)) discard;              // no water inside a boat
                half4 c = i.color;
                c.rgb = SRGBToLinear(c.rgb);                                   // vertex colours are sRGB (Linear project)
                if (_MadMaxWaterHole.z > 0.0)                                  // a window through the surface around a diver below
                {
                    float hd = distance(i.xz, _MadMaxWaterHole.xy);
                    c.a *= lerp(_MadMaxWaterHole.w, 1.0, saturate((hd - _MadMaxWaterHole.z * 0.6) / (_MadMaxWaterHole.z * 0.4)));
                }
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
