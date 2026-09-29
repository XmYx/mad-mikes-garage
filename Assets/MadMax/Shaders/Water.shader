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
            struct A { float4 positionOS : POSITION; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; };
            V vert (A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
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
                half4 c = i.color;
                c.rgb = SRGBToLinear(c.rgb);                                   // vertex colours are sRGB (Linear project)
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
