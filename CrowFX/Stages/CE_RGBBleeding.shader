// RGBBleeding.shader
Shader "Hidden/CrowFX/Stages/RGBBleeding"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}

        _BleedBlend ("Bleed Blend", Range(0,1)) = 0
        _BleedIntensity ("Bleed Intensity", Float) = 0

        _ShiftR ("Shift R", Vector) = (-0.5, 0.5, 0, 0)
        _ShiftG ("Shift G", Vector) = (0.5, -0.5, 0, 0)
        _ShiftB ("Shift B", Vector) = (0, 0, 0, 0)

        // --- mode / blending ---
        [Enum(ManualShift,0,Radial,1)] _BleedMode ("Bleed Mode", Float) = 0
        [Enum(Mix,0,Add,1,Screen,2,Max,3)] _BlendMode ("Blend Mode", Float) = 0

        // --- edge-only ---
        _EdgeOnly ("Edge Only", Float) = 0
        _EdgeThreshold ("Edge Threshold", Range(0,1)) = 0.05
        _EdgePower ("Edge Power", Range(0.25,8)) = 2

        // --- radial ---
        _RadialCenter ("Radial Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _RadialStrength ("Radial Strength", Range(0,5)) = 1

        // --- smear / multitap ---
        [Range(1,8)] _Samples ("Smear Samples", Float) = 1
        _Smear ("Smear Length", Range(0,5)) = 0
        _Falloff ("Smear Falloff", Range(0.25,6)) = 2

        // --- per-channel intensity & shaping ---
        _IntensityR ("Intensity R", Float) = 1
        _IntensityG ("Intensity G", Float) = 1
        _IntensityB ("Intensity B", Float) = 1
        _Anamorphic ("Anamorphic (X,Y)", Vector) = (1,1,0,0)

        // --- safety / vibe ---
        _ClampUV ("Clamp UV", Float) = 0
        _PreserveLuma ("Preserve Luma", Float) = 0

        // --- wobble ---
        _WobbleAmp ("Wobble Amp", Range(0,2)) = 0
        _WobbleFreq ("Wobble Freq", Range(0,20)) = 4
        _WobbleScanline ("Wobble Scanline", Float) = 0

        // --- virtual grid anchoring (mirrors your other stages) ---
        _PixelSize ("Pixel Size", Float) = 1
        _UseVirtualGrid ("Use Virtual Grid", Float) = 0
        _VirtualRes ("Virtual Res", Vector) = (720, 480, 0, 0)
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

            float _BleedBlend, _BleedIntensity;
            float4 _ShiftR, _ShiftG, _ShiftB;

            float _BleedMode;
            float _BlendMode;

            float _EdgeOnly, _EdgeThreshold, _EdgePower;

            float4 _RadialCenter;
            float _RadialStrength;

            float _Samples, _Smear, _Falloff;

            float _IntensityR, _IntensityG, _IntensityB;
            float4 _Anamorphic;

            float _ClampUV, _PreserveLuma;

            float _WobbleAmp, _WobbleFreq, _WobbleScanline;

            float _PixelSize, _UseVirtualGrid;
            float4 _VirtualRes;

            inline float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            inline float2 GetPxSize()
            {
                float2 px = _MainTex_TexelSize.xy;

                if (_UseVirtualGrid > 0.5)
                {
                    float vx = max(1.0, _VirtualRes.x);
                    float vy = max(1.0, _VirtualRes.y);
                    px = float2(1.0 / vx, 1.0 / vy);
                }

                // NOTE: in this stage px is used as "grid pixel" size for shifting.
                // Edge detection will undo this multiplication to sample 1 pixel steps.
                px *= max(1.0, _PixelSize);
                return px;
            }

            // FIXED: edge gating that doesn't collapse to ~0 everywhere.
            // - Uses texel/virtual-pixel taps (not ddx/ddy luma which is often tiny)
            // - Amplifies edge energy into a useful range
            // - Soft-knee threshold via smoothstep so it doesn't kill the whole frame
            inline float EdgeFactor(float2 uv, float2 px, float3 baseRGB)
            {
                if (_EdgeOnly < 0.5) return 1.0;

                // We want "one pixel" step for edge detection.
                // px currently includes PixelSize (block). Undo to get 1-step.
                float pxBlock = max(1.0, _PixelSize);
                float2 stepPx = px / pxBlock;

                float2 uvR = uv + float2(stepPx.x, 0.0);
                float2 uvL = uv - float2(stepPx.x, 0.0);
                float2 uvU = uv + float2(0.0, stepPx.y);
                float2 uvD = uv - float2(0.0, stepPx.y);

                // For stability, clamp the edge taps
                uvR = saturate(uvR);
                uvL = saturate(uvL);
                uvU = saturate(uvU);
                uvD = saturate(uvD);

                float lR = Luma(tex2D(_MainTex, uvR).rgb);
                float lL = Luma(tex2D(_MainTex, uvL).rgb);
                float lU = Luma(tex2D(_MainTex, uvU).rgb);
                float lD = Luma(tex2D(_MainTex, uvD).rgb);

                float gx = (lR - lL);
                float gy = (lU - lD);
                float e = abs(gx) + abs(gy);

                // Boost into a usable range so thresholds like 0.01..0.2 work
                e *= 8.0;

                float thr = saturate(_EdgeThreshold);
                float knee = 0.05; // soft region width (feels good in practice)
                float t = smoothstep(thr, thr + knee, e);

                return pow(t, max(0.25, _EdgePower));
            }

            inline float WobbleFactor(float2 uv)
            {
                if (_WobbleAmp <= 0.0) return 0.0;

                float t = _Time.y * _WobbleFreq;

                // optional scanline phase for VHS-ish wobble
                float phase = (_WobbleScanline > 0.5) ? (uv.y * 480.0) : 0.0;
                float s = sin(t + phase);

                return _WobbleAmp * s;
            }

            inline float2 BaseDir(float2 uv, float2 manualShift)
            {
                // 0 ManualShift, 1 Radial
                if (_BleedMode < 0.5) return manualShift;

                float2 c = _RadialCenter.xy;
                float2 d = uv - c;
                float len = max(1e-5, length(d));
                return (d / len) * _RadialStrength;
            }

            inline float3 SampleSmearRGB(float2 uv, float2 off, float2 px, float smearLen, float falloffPow, float samples, float clampUV)
            {
                // off is “direction in pixels”; convert to UV via px
                float2 dirUV = off * px;

                if (smearLen <= 0.0 || samples <= 1.0)
                {
                    float2 uv1 = uv + dirUV;
                    if (clampUV > 0.5) uv1 = saturate(uv1);
                    return tex2D(_MainTex, uv1).rgb;
                }

                int n = (int)clamp(samples, 1.0, 8.0);

                float3 acc = 0;
                float wsum = 0;

                [unroll] for (int k = 1; k <= 8; k++)
                {
                    if (k > n) break;

                    float t = (float)k / (float)n;           // 0..1
                    float w = pow(1.0 - t, max(0.25, falloffPow));

                    float2 uvK = uv + dirUV * (t * smearLen);
                    if (clampUV > 0.5) uvK = saturate(uvK);

                    acc += tex2D(_MainTex, uvK).rgb * w;
                    wsum += w;
                }

                return acc / max(1e-5, wsum);
            }

            inline float3 PreserveLuma(float3 baseRGB, float3 bleedRGB)
            {
                if (_PreserveLuma < 0.5) return bleedRGB;

                float lb = Luma(baseRGB);
                float le = max(1e-5, Luma(bleedRGB));
                return bleedRGB * (lb / le);
            }

            inline float3 Combine(float3 baseRGB, float3 bleedRGB, float blend, float mode)
            {
                blend = saturate(blend);

                // 0 Mix, 1 Add, 2 Screen, 3 Max
                if (mode < 0.5)
                {
                    return lerp(baseRGB, bleedRGB, blend);
                }
                else if (mode < 1.5)
                {
                    return saturate(baseRGB + bleedRGB * blend);
                }
                else if (mode < 2.5)
                {
                    float3 s = 1.0 - (1.0 - baseRGB) * (1.0 - bleedRGB);
                    return lerp(baseRGB, s, blend);
                }
                else
                {
                    float3 m = max(baseRGB, bleedRGB);
                    return lerp(baseRGB, m, blend);
                }
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                float3 baseRGB = tex2D(_MainTex, uv).rgb;

                float blend = saturate(_BleedBlend);
                float inten = max(0.0, _BleedIntensity);

                // keep fast path
                if (blend <= 0.0 || inten <= 0.0)
                    return float4(baseRGB, 1);

                float2 px = GetPxSize();

                // FIXED edge gating (doesn't zero out the effect)
                float edge = EdgeFactor(uv, px, baseRGB);

                // wobble modulates shift strength
                float wob = WobbleFactor(uv);

                float2 dirR = BaseDir(uv, _ShiftR.xy);
                float2 dirG = BaseDir(uv, _ShiftG.xy);
                float2 dirB = BaseDir(uv, _ShiftB.xy);

                // per-channel intensity + anamorphic shaping
                float2 an = max(float2(1e-5, 1e-5), _Anamorphic.xy);

                float wobMul = (1.0 + wob);

                float2 offR = (dirR * inten * _IntensityR) * an * wobMul;
                float2 offG = (dirG * inten * _IntensityG) * an * wobMul;
                float2 offB = (dirB * inten * _IntensityB) * an * wobMul;

                float3 sR = SampleSmearRGB(uv, offR, px, _Smear, _Falloff, _Samples, _ClampUV);
                float3 sG = SampleSmearRGB(uv, offG, px, _Smear, _Falloff, _Samples, _ClampUV);
                float3 sB = SampleSmearRGB(uv, offB, px, _Smear, _Falloff, _Samples, _ClampUV);

                float3 bleedRGB = float3(sR.r, sG.g, sB.b);

                bleedRGB = PreserveLuma(baseRGB, bleedRGB);
                bleedRGB = saturate(bleedRGB);

                float gatedBlend = blend * edge;

                float3 outRGB = Combine(baseRGB, bleedRGB, gatedBlend, _BlendMode);
                return float4(outRGB, 1);
            }
            ENDCG
        }
    }
}
