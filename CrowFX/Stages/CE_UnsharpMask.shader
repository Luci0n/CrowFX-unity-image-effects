Shader "Hidden/CrowFX/Stages/UnsharpMask"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}

        _UnsharpEnabled ("Unsharp Enabled", Float) = 0
        _UnsharpAmount ("Amount", Range(0,3)) = 0.5
        _UnsharpRadius ("Radius (px)", Float) = 1.0
        _UnsharpThreshold ("Threshold", Range(0,0.25)) = 0.0

        _UnsharpLumaOnly ("Luma Only", Float) = 0
        _UnsharpChroma ("Chroma Sharpen", Range(0,1)) = 0.0

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

            float _UnsharpEnabled, _UnsharpAmount, _UnsharpRadius, _UnsharpThreshold;
            float _UnsharpLumaOnly, _UnsharpChroma;

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

            // 3x3 blur (gaussian-ish)
            float3 Blur3x3(float2 uv, float2 texelStep)
            {
                float3 c = tex2D(_MainTex, uv).rgb * 4.0;

                float2 o = texelStep;

                float3 edges =
                    tex2D(_MainTex, uv + float2( o.x,  0)).rgb +
                    tex2D(_MainTex, uv + float2(-o.x,  0)).rgb +
                    tex2D(_MainTex, uv + float2( 0,   o.y)).rgb +
                    tex2D(_MainTex, uv + float2( 0,  -o.y)).rgb;

                float3 corners =
                    tex2D(_MainTex, uv + float2( o.x,  o.y)).rgb +
                    tex2D(_MainTex, uv + float2(-o.x,  o.y)).rgb +
                    tex2D(_MainTex, uv + float2( o.x, -o.y)).rgb +
                    tex2D(_MainTex, uv + float2(-o.x, -o.y)).rgb;

                return (c + edges * 2.0 + corners) / 16.0;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float3 col = tex2D(_MainTex, uv).rgb;

                if (_UnsharpEnabled < 0.5 || _UnsharpAmount <= 0.0)
                    return float4(col, 1);

                float radius = max(_UnsharpRadius, 0.25);
                float2 texelStep = StepUV() * radius;

                float3 blurred = Blur3x3(uv, texelStep);

                float3 detail = col - blurred;

                float thr = max(_UnsharpThreshold, 0.0);
                if (thr > 0.0)
                {
                    float3 mask = step(thr.xxx, abs(detail));
                    detail *= mask;
                }

                float3 rgbSharpen = saturate(col + _UnsharpAmount * detail);

                if (_UnsharpLumaOnly > 0.5)
                {
                    float yC = dot(col, float3(0.299, 0.587, 0.114));
                    float yB = dot(blurred, float3(0.299, 0.587, 0.114));
                    float yD = yC - yB;

                    if (thr > 0.0)
                        yD *= step(thr, abs(yD));

                    float ySharp = saturate(yC + _UnsharpAmount * yD);
                    float3 lumaSharpen = saturate(col + (ySharp - yC).xxx);

                    float3 combined = lerp(lumaSharpen, rgbSharpen, saturate(_UnsharpChroma));
                    return float4(combined, 1);
                }

                return float4(rgbSharpen, 1);
            }
            ENDCG
        }
    }
}
