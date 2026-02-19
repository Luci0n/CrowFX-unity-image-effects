Shader "Hidden/CrowFX/Stages/EdgeOutline"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}

        _EdgeEnabled ("Edge Enabled", Float) = 0
        _EdgeStrength ("Edge Strength", Range(0,8)) = 1
        _EdgeThreshold ("Edge Threshold (Linear)", Range(0,1)) = 0.02
        _EdgeBlend ("Edge Blend", Range(0,1)) = 1
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)

        _UseVirtualGrid ("Use Virtual Grid", Float) = 0
        _VirtualRes ("Virtual Resolution (xy)", Vector) = (640,448,0,0)
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
            float4 _MainTex_TexelSize;

            // camera depth
            sampler2D_float _CameraDepthTexture;

            float _EdgeEnabled, _EdgeStrength, _EdgeThreshold, _EdgeBlend;
            float4 _EdgeColor;

            float _UseVirtualGrid;
            float4 _VirtualRes;

            inline float2 StepUV()
            {
                if (_UseVirtualGrid > 0.5)
                {
                    float2 g = max(_VirtualRes.xy, 1.0);
                    return 1.0 / g;
                }
                return _MainTex_TexelSize.xy;
            }

            inline float EdgeFromDepth(float2 uv)
            {
                float2 stepUV = StepUV();

                float dC = LinearEyeDepth(tex2D(_CameraDepthTexture, uv).r);
                float dR = LinearEyeDepth(tex2D(_CameraDepthTexture, uv + float2(stepUV.x, 0)).r);
                float dL = LinearEyeDepth(tex2D(_CameraDepthTexture, uv - float2(stepUV.x, 0)).r);
                float dU = LinearEyeDepth(tex2D(_CameraDepthTexture, uv + float2(0, stepUV.y)).r);
                float dD = LinearEyeDepth(tex2D(_CameraDepthTexture, uv - float2(0, stepUV.y)).r);

                float diff = max(max(abs(dR - dC), abs(dL - dC)), max(abs(dU - dC), abs(dD - dC)));

                float e = saturate((diff - _EdgeThreshold) * _EdgeStrength);
                return e;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float3 c = tex2D(_MainTex, uv).rgb;

                if (_EdgeEnabled > 0.5 && _EdgeBlend > 0.0)
                {
                    float e = EdgeFromDepth(uv);
                    c = lerp(c, _EdgeColor.rgb, saturate(e * _EdgeBlend));
                }

                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
