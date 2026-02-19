Shader "Hidden/CrowFX/Stages/SamplingGrid"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _PixelSize ("Pixel Size", Float) = 1
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

            float _PixelSize;
            float _UseVirtualGrid;
            float4 _VirtualRes;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 originalUV = i.uv;
                float2 uv = originalUV;

                // Pixelation (snap UV to pixel blocks in backbuffer space)
                if (_PixelSize > 1.0)
                {
                    float2 res = float2(1.0 / _MainTex_TexelSize.x, 1.0 / _MainTex_TexelSize.y);
                    float2 pixelatedUV = floor(uv * res / _PixelSize) * (_PixelSize / res) + (0.5 * _PixelSize / res);
                    uv = pixelatedUV;
                }

                // Virtual grid stabilization (snap UV to virtual grid)
                if (_UseVirtualGrid > 0.5)
                {
                    float2 grid = max(_VirtualRes.xy, 1.0);
                    uv = (floor(originalUV * grid) + 0.5) / grid;
                }

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
