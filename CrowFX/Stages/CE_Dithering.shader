Shader "Hidden/CrowFX/Stages/Dithering"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}

        _Levels   ("Levels", Range(2,512)) = 64
        _LevelsR  ("Levels R", Range(2,512)) = 64
        _LevelsG  ("Levels G", Range(2,512)) = 64
        _LevelsB  ("Levels B", Range(2,512)) = 64
        _UsePerChannel ("Use Per Channel", Float) = 0

        _AnimateLevels ("Animate Levels", Float) = 0
        _MinLevels ("Min Levels", Float) = 64
        _MaxLevels ("Max Levels", Float) = 64
        _Speed ("Speed", Float) = 1

        _DitherMode ("Dither Mode", Float) = 0
        _DitherStrength ("Dither Strength", Range(0,1)) = 0
        _BlueNoise ("Blue Noise (128x128)", 2D) = "gray" {}

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

            float _Levels, _LevelsR, _LevelsG, _LevelsB, _UsePerChannel;
            float _AnimateLevels, _MinLevels, _MaxLevels, _Speed;

            float _DitherMode, _DitherStrength;
            sampler2D _BlueNoise;

            float _PixelSize;
            float _UseVirtualGrid;
            float4 _VirtualRes;

            static const float bayer2[4] = { 0.0/4.0, 2.0/4.0, 3.0/4.0, 1.0/4.0 };

            static const float bayer4[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            static const float bayer8[64] =
            {
                 0.0/64.0, 32.0/64.0,  8.0/64.0, 40.0/64.0,  2.0/64.0, 34.0/64.0, 10.0/64.0, 42.0/64.0,
                48.0/64.0, 16.0/64.0, 56.0/64.0, 24.0/64.0, 50.0/64.0, 18.0/64.0, 58.0/64.0, 26.0/64.0,
                12.0/64.0, 44.0/64.0,  4.0/64.0, 36.0/64.0, 14.0/64.0, 46.0/64.0,  6.0/64.0, 38.0/64.0,
                60.0/64.0, 28.0/64.0, 52.0/64.0, 20.0/64.0, 62.0/64.0, 30.0/64.0, 54.0/64.0, 22.0/64.0,
                 3.0/64.0, 35.0/64.0, 11.0/64.0, 43.0/64.0,  1.0/64.0, 33.0/64.0,  9.0/64.0, 41.0/64.0,
                51.0/64.0, 19.0/64.0, 59.0/64.0, 27.0/64.0, 49.0/64.0, 17.0/64.0, 57.0/64.0, 25.0/64.0,
                15.0/64.0, 47.0/64.0,  7.0/64.0, 39.0/64.0, 13.0/64.0, 45.0/64.0,  5.0/64.0, 37.0/64.0,
                63.0/64.0, 31.0/64.0, 55.0/64.0, 23.0/64.0, 61.0/64.0, 29.0/64.0, 53.0/64.0, 21.0/64.0
            };

            inline float D2(int2 p) { p = p & int2(1,1); return bayer2[p.y*2 + p.x]; }
            inline float D4(int2 p) { p = p & int2(3,3); return bayer4[p.y*4 + p.x]; }
            inline float D8(int2 p) { p = p & int2(7,7); return bayer8[p.y*8 + p.x]; }

            inline float DNoise(int2 p)
            {
                uint2 up = (uint2)uint2((uint)abs(p.x), (uint)abs(p.y));
                uint h = up.x * 1103515245u + up.y * 12345u;
                h = (h >> 13u) ^ h;
                h = h * 1103515245u + 12345u;
                return frac((float)h * 2.3283064e-10);
            }

            inline float DBlue(int2 p)
            {
                int2 pp = p & 127; // assumes 128x128
                float2 uv = (float2(pp) + 0.5) / 128.0;
                return tex2D(_BlueNoise, uv).r;
            }

            inline float3 Quantize(float3 rgb, float3 levels, float2 gridPos)
            {
                rgb = saturate(rgb);
                levels = max(levels, 2.0);

                int2 pix = (int2)floor(gridPos);

                int mode = (int)(_DitherMode + 0.5);
                float thr = 0.5;
                if (mode == 1) thr = D2(pix);
                else if (mode == 2) thr = D4(pix);
                else if (mode == 3) thr = D8(pix);
                else if (mode == 4) thr = DNoise(pix);
                else if (mode == 5) thr = DBlue(pix);

                float3 scaled = rgb * (levels - 1.0);

                if (_DitherStrength > 0.0)
                    scaled += (thr - 0.5) * _DitherStrength;

                float3 q = clamp(floor(scaled + 0.5), 0.0, levels - 1.0);
                return q / (levels - 1.0);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float3 col = tex2D(_MainTex, uv).rgb;

                float levelsAnim = _Levels;
                if (_AnimateLevels > 0.5)
                {
                    float t = 0.5 + 0.5 * sin(_Time.y * _Speed);
                    levelsAnim = lerp(_MinLevels, _MaxLevels, t);
                }

                float3 perChLevels = float3(levelsAnim, levelsAnim, levelsAnim);
                if (_UsePerChannel > 0.5)
                    perChLevels = float3(_LevelsR, _LevelsG, _LevelsB);

                float2 screenRes = float2(1.0 / _MainTex_TexelSize.x, 1.0 / _MainTex_TexelSize.y);
                float2 baseRes = (_UseVirtualGrid > 0.5) ? max(_VirtualRes.xy, 1.0) : screenRes;

                float pxBlock = max(_PixelSize, 1.0);
                float2 gridPos = uv * (baseRes / pxBlock);

                float3 quant = Quantize(col, perChLevels, gridPos);
                return float4(quant, 1.0);
            }
            ENDCG
        }
    }
}
