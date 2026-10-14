Shader "Hidden/MadMax/Grade"
{
    // Last pass on the low-res image: colour grading per biome / weather / night (saturation, vibrance, contrast,
    // split toning: cool shadows, warm highlights), a soft vignette, heat haze shimmer low on screen over hot ground, and
    // the lightning flash. Driven by World/Atmosphere.
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _GradeTint ("Tint", Color) = (1,1,1,1)
        _Saturation ("Saturation", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0.5,2)) = 1
        _Vibrance ("Vibrance", Range(0,1)) = 0
        _ShadowTint ("Shadow Tint", Color) = (1,1,1,1)
        _HighlightTint ("Highlight Tint", Color) = (1,1,1,1)
        _Vignette ("Vignette", Range(0,1)) = 0
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
            float4 _GradeTint, _ShadowTint, _HighlightTint;
            float _Saturation, _Contrast, _Vibrance, _Vignette, _Haze, _Flash;

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
                // vibrance: lift dull colours more than the already vivid ones (paint and lamps don't go neon)
                float mx = max(c.r, max(c.g, c.b)), mn = min(c.r, min(c.g, c.b));
                float chroma = (mx - mn) / max(mx, 1e-4);
                c = lerp(l.xxx, c, 1.0 + _Vibrance * (1.0 - chroma) * (1.0 - chroma));
                c = max(c, 0.0);
                // contrast as an S around linear mid-grey in log space: deeper shadows, highlights roll off instead of clipping
                float3 lg = log2(max(c, 1e-5) / 0.18);
                c = 0.18 * exp2(lg * _Contrast);
                c = c / (1.0 + max(c - 0.8, 0.0) * 0.6);
                // split toning by luminance
                float lt = saturate(dot(c, float3(0.299, 0.587, 0.114)) * 2.2);
                c *= lerp(_ShadowTint.rgb, _HighlightTint.rgb, smoothstep(0.0, 1.0, lt));
                c *= _GradeTint.rgb;
                // vignette (aspect-corrected, soft)
                float2 vq = (i.uv - 0.5) * float2(_MainTex_TexelSize.y * _MainTex_TexelSize.z, 1.0);
                c *= 1.0 - _Vignette * smoothstep(0.35, 0.95, dot(vq, vq) * 2.2);
                c += _Flash * float3(0.55, 0.6, 0.75) * (0.4 + l);
                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
