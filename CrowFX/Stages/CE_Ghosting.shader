Shader "Hidden/CrowFX/Stages/Ghosting"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _PrevTex ("Prev", 2D) = "black" {}

        _GhostEnabled ("Ghost Enabled", Float) = 0
        _GhostBlend ("Ghost Amount", Range(0,1)) = 0
        _GhostOffsetPx ("Ghost Offset (px)", Vector) = (0,0,0,0)

        // 0=Mix (lerp), 1=Add, 2=Screen, 3=Max
        _CombineMode ("Combine Mode", Float) = 2

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
            sampler2D _PrevTex;
            float4 _MainTex_TexelSize;

            float _GhostEnabled, _GhostBlend;
            float4 _GhostOffsetPx;
            float _CombineMode;

            float _UseVirtualGrid;
            float4 _VirtualRes;

            // Compute step only if we actually need an offset.
            inline float2 StepUVFast()
            {
                // Branch is uniform (same for all pixels), so this is cheap.
                if (_UseVirtualGrid > 0.5)
                {
                    float2 g = max(_VirtualRes.xy, 1.0);
                    return rcp(g); // 1.0 / g
                }
                return _MainTex_TexelSize.xy;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // Always sample current.
                float3 cur = tex2D(_MainTex, uv).rgb;

                // EARLY OUT: if ghost is off, we do exactly 1 sample total.
                // Using <= 0.5 / <= 0 keeps it stable, and avoids extra work.
                if (_GhostEnabled <= 0.5 || _GhostBlend <= 0.0)
                    return float4(cur, 1.0);

                // Clamp blend once.
                float amt = saturate(_GhostBlend);

                // If offset is zero (common), don’t compute StepUV or add.
                float2 offPx = _GhostOffsetPx.xy;
                float2 uvPrev = uv;

                if (dot(offPx, offPx) > 0.0)
                {
                    float2 stepUV = StepUVFast();
                    uvPrev = uv + offPx * stepUV;
                }

                float3 prev = tex2D(_PrevTex, uvPrev).rgb;

                // Combine mode selection.
                float m = _CombineMode;

                // 0: Mix (lerp)
                if (m < 0.5)
                {
                    cur = lerp(cur, prev, amt);
                    return float4(cur, 1.0);
                }

                // For overlay modes, scale prev once.
                float3 prevScaled = prev * amt;

                // 1: Add
                if (m < 1.5)
                {
                    cur = saturate(cur + prevScaled);
                    return float4(cur, 1.0);
                }

                // 2: Screen (result is already in [0,1] if inputs are)
                if (m < 2.5)
                {
                    cur = 1.0 - (1.0 - cur) * (1.0 - prevScaled);
                    return float4(cur, 1.0);
                }

                // 3: Max
                cur = max(cur, prevScaled);
                return float4(cur, 1.0);
            }
            ENDCG
        }
    }
}
