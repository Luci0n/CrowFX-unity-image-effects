Shader "Hidden/CrowFX/Stages/PaletteMapping"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _ThresholdTex ("Threshold Curve", 2D) = "white" {}
        _UsePalette ("Use Palette", Float) = 0
        _PaletteTex ("Palette", 2D) = "white" {}
        _Invert ("Invert", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _ThresholdTex;

            float _UsePalette;
            sampler2D _PaletteTex;

            float _Invert;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;

                // Threshold curve remap per channel (use y=0 for a 256x1 curve texture)
                c.r = tex2D(_ThresholdTex, float2(c.r, 0.0)).r;
                c.g = tex2D(_ThresholdTex, float2(c.g, 0.0)).r;
                c.b = tex2D(_ThresholdTex, float2(c.b, 0.0)).r;

                // Palette lookup (1D in X)
                if (_UsePalette > 0.5)
                {
                    float v = dot(c, float3(0.333, 0.333, 0.334));
                    c = tex2D(_PaletteTex, float2(v, 0.5)).rgb;
                }

                // Inversion (perceptual in Linear projects, plain in Gamma projects)
                if (_Invert > 0.5)
                {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    c = 1.0 - c;
                #else
                    float3 g = LinearToGammaSpace(c);
                    g = 1.0 - g;
                    c = GammaToLinearSpace(g);
                #endif
                }

                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
