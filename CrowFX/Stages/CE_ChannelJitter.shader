Shader "Hidden/CrowFX/Stages/ChannelJitter"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}

        _JitterEnabled  ("Enabled", Float) = 0
        _JitterStrength ("Strength", Range(0,1)) = 0
        _JitterMode     ("Mode (0=Static,1=TimeSine,2=HashNoise,3=BlueNoiseTex)", Float) = 1

        _JitterAmountPx ("Amount (px)", Range(0,8)) = 1
        _JitterSpeed    ("Speed", Range(0,30)) = 8

        _UseSeed ("Use Seed", Float) = 0
        _Seed    ("Seed", Float) = 1337

        _Scanline        ("Scanline", Float) = 0
        _ScanlineDensity ("Scanline Density", Float) = 480
        _ScanlineAmp     ("Scanline Amp", Float) = 0.35

        _ChannelWeights ("Channel Weights (RGB)", Vector) = (1,1,1,0)
        _DirR ("Dir R (xy)", Vector) = (1,0,0,0)
        _DirG ("Dir G (xy)", Vector) = (0,1,0,0)
        _DirB ("Dir B (xy)", Vector) = (-1,-1,0,0)

        _ClampUV ("Clamp UV", Float) = 1
        _NoiseTex ("Noise Tex", 2D) = "gray" {}

        _PixelSize      ("PixelSize", Float) = 1
        _UseVirtualGrid ("UseVirtualGrid", Float) = 0
        _VirtualRes     ("VirtualRes", Vector) = (720,480,0,0)
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

            float _JitterEnabled;
            float _JitterStrength;
            float _JitterMode;

            float _JitterAmountPx;
            float _JitterSpeed;

            float _UseSeed;
            float _Seed;

            float _Scanline;
            float _ScanlineDensity;
            float _ScanlineAmp;

            float4 _ChannelWeights;
            float4 _DirR, _DirG, _DirB;

            float _ClampUV;
            sampler2D _NoiseTex;

            float _PixelSize;
            float _UseVirtualGrid;
            float4 _VirtualRes;

            // ------------------------------
            // Hash helpers
            // ------------------------------
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 hash22(float2 p)
            {
                float n = hash21(p);
                return float2(n, hash21(p + 17.17));
            }

            float2 getBaseTexelSize()
            {
                if (_UseVirtualGrid > 0.5)
                {
                    float2 vr = max(_VirtualRes.xy, 1.0);
                    return 1.0 / vr;
                }
                return _MainTex_TexelSize.xy;
            }

            float2 safeUV(float2 uv)
            {
                return (_ClampUV > 0.5) ? clamp(uv, 0.0, 1.0) : uv;
            }

            float scanlineMod(float2 uv, float t)
            {
                if (_Scanline <= 0.5) return 1.0;

                float y = uv.y;
                float dens = max(_ScanlineDensity, 1.0);
                float lineId = floor(y * dens);

                float seedVal = (_UseSeed > 0.5) ? _Seed : 0.0;
                float timeStep = floor(t * 30.0);

                float r = hash11(lineId + seedVal + timeStep);
                float s = (r * 2.0 - 1.0) * _ScanlineAmp;

                return 1.0 + s;
            }

            float2 modeOffset(float2 uv, float t, float2 texel, float strength)
            {
                float ampPx = _JitterAmountPx * strength;

                // 0 Static
                if (_JitterMode < 0.5)
                {
                    float k = (_UseSeed > 0.5) ? _Seed : 0.0;
                    float2 r = hash22(float2(7.1, 3.9) + k) * 2.0 - 1.0;
                    return r * ampPx * texel;
                }
                // 1 TimeSine
                else if (_JitterMode < 1.5)
                {
                    float k = (_UseSeed > 0.5) ? _Seed : 0.0;
                    float a = sin(t * _JitterSpeed + k);
                    float b = cos(t * (_JitterSpeed * 0.73) + k * 1.7);
                    float2 r = float2(a, b);
                    return r * ampPx * texel;
                }
                // 2 HashNoise
                else if (_JitterMode < 2.5)
                {
                    float k = (_UseSeed > 0.5) ? _Seed : 0.0;
                    float stepT = floor(t * max(_JitterSpeed, 0.0) + 1e-6);
                    float2 cell = floor(uv * 256.0);
                    float2 r = hash22(cell + k + stepT) * 2.0 - 1.0;
                    return r * ampPx * texel;
                }
                // 3 BlueNoiseTex
                else
                {
                    float k = (_UseSeed > 0.5) ? _Seed : 0.0;
                    float2 nuv = frac(uv * 8.0 + float2(0.01, 0.013) * (t * _JitterSpeed) + k * 0.001);
                    float2 r = tex2D(_NoiseTex, nuv).rg * 2.0 - 1.0;
                    return r * ampPx * texel;
                }
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float3 baseRGB = tex2D(_MainTex, uv).rgb;

                if (_JitterEnabled <= 0.5 || _JitterStrength <= 1e-5)
                    return float4(baseRGB, 1);

                float t = _Time.y;
                float strength = saturate(_JitterStrength);

                float2 texel = getBaseTexelSize();

                float mod = scanlineMod(uv, t);

                float2 o = modeOffset(uv, t, texel, strength) * mod;

                float2 oR = o * _DirR.xy;
                float2 oG = o * _DirG.xy;
                float2 oB = o * _DirB.xy;

                float3 jitterRGB = float3(
                    tex2D(_MainTex, safeUV(uv + oR)).r,
                    tex2D(_MainTex, safeUV(uv + oG)).g,
                    tex2D(_MainTex, safeUV(uv + oB)).b
                );

                float3 w = saturate(_ChannelWeights.rgb);
                float3 outc = lerp(baseRGB, jitterRGB, w * strength);

                return float4(saturate(outc), 1);
            }
            ENDCG
        }
    }
}
