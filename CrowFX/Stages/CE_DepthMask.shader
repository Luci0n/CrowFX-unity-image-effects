Shader "Hidden/CrowFX/Stages/DepthMask"
{
    Properties
    {
        _MainTex ("Base (Unmasked)", 2D) = "white" {}
        _MaskedTex ("Masked Result", 2D) = "white" {}

        _UseDepthMask ("Use Depth Mask", Float) = 0
        _DepthThreshold ("Depth Threshold (Linear)", Float) = 1.0
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

            sampler2D_float _CameraDepthTexture;

            float _UseDepthMask;
            float _DepthThreshold;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                float3 baseCol = tex2D(_MainTex, uv).rgb;
                float3 fxCol   = tex2D(_MaskedTex, uv).rgb;

                if (_UseDepthMask < 0.5)
                    return float4(fxCol, 1); // no depth mask -> just pass processed

                float raw = tex2D(_CameraDepthTexture, uv).r;
                float sceneDepth = LinearEyeDepth(raw);

                // If closer than threshold -> keep base, else apply processed
                float a = step(_DepthThreshold, sceneDepth);

                float3 outc = lerp(baseCol, fxCol, a);
                return float4(outc, 1);
            }
            ENDCG
        }
    }
}
