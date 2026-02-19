Shader "Hidden/CrowFX/Stages/TextureMask"
{
    Properties
    {
        _MainTex ("Base (Unmasked)", 2D) = "white" {}
        _MaskedTex ("Masked Result", 2D) = "white" {}

        _UseMask ("Use Mask", Float) = 0
        _MaskTex ("Mask", 2D) = "white" {}
        _MaskThreshold ("Mask Threshold", Range(0,1)) = 0.5
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
            sampler2D _MaskedTex;

            float _UseMask;
            sampler2D _MaskTex;
            float _MaskThreshold;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                float3 baseCol = tex2D(_MainTex, uv).rgb;
                float3 fxCol   = tex2D(_MaskedTex, uv).rgb;

                if (_UseMask < 0.5)
                    return float4(fxCol, 1); // no masking -> just pass processed

                float m = tex2D(_MaskTex, uv).r;

                float a = step(_MaskThreshold, m);

                float3 outc = lerp(baseCol, fxCol, a);
                return float4(outc, 1);
            }
            ENDCG
        }
    }
}
