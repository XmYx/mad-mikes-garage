Shader "Hidden/MadMax/TiltShift"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _Focus ("Focus Line (0..1 screen Y)", Range(0,1)) = 0.45
        _Band ("Sharp Band", Range(0,0.5)) = 0.12
        _MaxBlur ("Max Blur (px)", Range(0,8)) = 5
        _Saturation ("Saturation", Range(0,2)) = 1.1
        _Contrast ("Contrast", Range(0.5,2)) = 1.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Focus, _Band, _MaxBlur, _Saturation, _Contrast;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float d = saturate((abs(i.uv.y - _Focus) - _Band) / max(1e-3, 0.5 - _Band));
                float r = d * d * _MaxBlur;
                float3 acc = 0; float wsum = 0;
                for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                {
                    float w = exp(-(x * x + y * y) / 4.0);
                    acc += tex2D(_MainTex, i.uv + float2(x, y) * 0.5 * r * _MainTex_TexelSize.xy).rgb * w;
                    wsum += w;
                }
                float3 c = acc / wsum;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
