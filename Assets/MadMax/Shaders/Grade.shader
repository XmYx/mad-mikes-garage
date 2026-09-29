Shader "Hidden/MadMax/Grade"
{
    // Last pass on the low-res image: colour grading per biome / weather / night, heat haze shimmer low on screen
    // over hot ground, and the lightning flash. Driven by World/Atmosphere.
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _GradeTint ("Tint", Color) = (1,1,1,1)
        _Saturation ("Saturation", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0.5,2)) = 1
        _Haze ("Heat Haze", Range(0,1)) = 0
        _Flash ("Lightning Flash", Range(0,1)) = 0
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
            float4 _GradeTint;
            float _Saturation, _Contrast, _Haze, _Flash;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                // heat haze: whole-pixel wobble, strongest near the bottom of the view (the hot ground in front)
                if (_Haze > 0.001)
                {
                    float band = saturate(1.0 - uv.y * 1.6);
                    float w = sin(uv.y * 180.0 + _Time.y * 7.0) * sin(uv.y * 47.0 - _Time.y * 3.1);
                    uv.x += round(w * _Haze * band * 1.6) * _MainTex_TexelSize.x;
                }
                float3 c = tex2D(_MainTex, uv).rgb;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c *= _GradeTint.rgb;
                c += _Flash * float3(0.55, 0.6, 0.75) * (0.4 + l);
                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
