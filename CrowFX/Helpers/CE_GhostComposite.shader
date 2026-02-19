Shader "Hidden/CrowFX/Helpers/GhostComposite"
{
    Properties
    {
        _MainTex ("Unused", 2D) = "black" {} // required by Unity blit, not used
        _Count ("Count", Int) = 0
        _WeightCurve ("WeightCurve", Float) = 1.5
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

            // History textures (newest -> oldest as bound by C#)
            sampler2D _Hist0;
            sampler2D _Hist1;
            sampler2D _Hist2;
            sampler2D _Hist3;
            sampler2D _Hist4;
            sampler2D _Hist5;
            sampler2D _Hist6;
            sampler2D _Hist7;
            sampler2D _Hist8;
            sampler2D _Hist9;
            sampler2D _Hist10;
            sampler2D _Hist11;
            sampler2D _Hist12;
            sampler2D _Hist13;
            sampler2D _Hist14;
            sampler2D _Hist15;

            int _Count;
            float _WeightCurve;

            float3 SampleHist(int idx, float2 uv)
            {
                if (idx == 0)  return tex2D(_Hist0,  uv).rgb;
                if (idx == 1)  return tex2D(_Hist1,  uv).rgb;
                if (idx == 2)  return tex2D(_Hist2,  uv).rgb;
                if (idx == 3)  return tex2D(_Hist3,  uv).rgb;
                if (idx == 4)  return tex2D(_Hist4,  uv).rgb;
                if (idx == 5)  return tex2D(_Hist5,  uv).rgb;
                if (idx == 6)  return tex2D(_Hist6,  uv).rgb;
                if (idx == 7)  return tex2D(_Hist7,  uv).rgb;
                if (idx == 8)  return tex2D(_Hist8,  uv).rgb;
                if (idx == 9)  return tex2D(_Hist9,  uv).rgb;
                if (idx == 10) return tex2D(_Hist10, uv).rgb;
                if (idx == 11) return tex2D(_Hist11, uv).rgb;
                if (idx == 12) return tex2D(_Hist12, uv).rgb;
                if (idx == 13) return tex2D(_Hist13, uv).rgb;
                if (idx == 14) return tex2D(_Hist14, uv).rgb;
                return tex2D(_Hist15, uv).rgb;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                int count = clamp(_Count, 0, 16);

                // If no history, output black (C# will avoid using it once we seed)
                if (count <= 0)
                    return float4(0,0,0,1);

                float3 acc = 0;
                float wsum = 0;

                // Weight newest -> oldest
                // w_i = pow(1 - i/(count-1), curve), normalized by wsum
                for (int k = 0; k < 16; k++)
                {
                    if (k >= count) break;

                    float t = (count <= 1) ? 1.0 : (1.0 - (k / (count - 1.0)));
                    float w = pow(saturate(t), max(_WeightCurve, 0.0001));

                    acc += SampleHist(k, i.uv) * w;
                    wsum += w;
                }

                acc /= max(wsum, 1e-6);
                return float4(saturate(acc), 1);
            }
            ENDCG
        }
    }
}
