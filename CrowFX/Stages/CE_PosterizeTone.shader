Shader "Hidden/CrowFX/Stages/PosterizeTone"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _LuminanceOnly ("Luminance Only", Float) = 0
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
            float _LuminanceOnly;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;

                if (_LuminanceOnly > 0.5)
                {
                    float lum = dot(col, float3(0.299, 0.587, 0.114));
                    col = lum.xxx;
                }

                return float4(saturate(col), 1);
            }
            ENDCG
        }
    }
}
