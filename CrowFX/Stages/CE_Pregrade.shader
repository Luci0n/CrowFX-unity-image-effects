Shader "Hidden/CrowFX/Stages/Pregrade"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _PregradeEnabled ("Enabled", Float) = 0
        _Exposure ("Exposure (EV)", Float) = 0
        _Contrast ("Contrast", Float) = 1
        _Gamma ("Gamma", Float) = 1
        _Saturation ("Saturation", Float) = 1
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
            float _PregradeEnabled, _Exposure, _Contrast, _Gamma, _Saturation;

            inline float3 ApplyPregrade(float3 c)
            {
                if (_PregradeEnabled < 0.5) return c;

                c *= exp2(_Exposure);
                c = (c - 0.5) * _Contrast + 0.5;
                c = max(c, 0.0);
                c = pow(c, 1.0 / max(_Gamma, 0.001));

                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(float3(l,l,l), c, _Saturation);

                return saturate(c);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                c = ApplyPregrade(c);
                return float4(c, 1);
            }
            ENDCG
        }
    }
}
