using System;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/CrowFX/Crow Image Effects")]

[EffectSectionMeta("Master",      title: "Master",            icon: "d_Settings",             hint: "Global Blend",         order: 0,  defaultExpanded: true)]
[EffectSectionMeta("Sampling",    title: "Sampling & Grid",   icon: "d_GridLayoutGroup Icon", hint: "Pixel Size · Grid",    order: 10, defaultExpanded: true)]
[EffectSectionMeta("Pregrade",    title: "Pre-Grade",         icon: "d_PreMatCube",           hint: "Exposure · Contrast",  order: 20, defaultExpanded: true)]
[EffectSectionMeta("Posterize",   title: "Posterize",         icon: "d_PreTextureRGB",        hint: "Levels · Animation",   order: 30, defaultExpanded: true)]
[EffectSectionMeta("Palette",     title: "Palette",           icon: "d_color_picker",         hint: "LUT · Curve",          order: 40, defaultExpanded: false)]
[EffectSectionMeta("TextureMask", title: "Texture Mask",      icon: "d_RectTool",             hint: "Mask Texture",         order: 50, defaultExpanded: false)]
[EffectSectionMeta("DepthMask",   title: "Depth Mask",        icon: "d_SceneViewOrtho",       hint: "Depth Threshold",      order: 60, defaultExpanded: false)]
[EffectSectionMeta("Jitter",      title: "Channel Jitter",    icon: "d_Image Icon",           hint: "RGB Offset",           order: 70, defaultExpanded: false)]
[EffectSectionMeta("Bleed",       title: "RGB Bleed",         icon: "d_PreTexRGB",            hint: "Chromatic Aberration", order: 80, defaultExpanded: false)]
[EffectSectionMeta("Ghost",       title: "Ghosting",          icon: "d_CameraPreview",        hint: "Motion Trail",         order: 90, defaultExpanded: false)]
[EffectSectionMeta("Edges",       title: "Edge Outline",      icon: "d_SceneViewFx",          hint: "Depth-based",          order: 100, defaultExpanded: false)]
[EffectSectionMeta("Unsharp",     title: "Unsharp Mask",      icon: "d_Search Icon",          hint: "Sharpen",              order: 110, defaultExpanded: false)]
[EffectSectionMeta("Dither",      title: "Dithering",         icon: "d_PreTextureMipMapHigh", hint: "Noise Pattern",         order: 120, defaultExpanded: true)]
[EffectSectionMeta("Shaders",     title: "Shaders",           icon: "d_Shader Icon",          hint: "Advanced",             order: 1000, defaultExpanded: false)]
public sealed class CrowImageEffects : MonoBehaviour
{
    public enum DitherMode { None = 0, Ordered2x2 = 1, Ordered4x4 = 2, Ordered8x8 = 3, Noise = 4, BlueNoise = 5 }
    public enum GhostCombineMode { Mix = 0, Add = 1, Screen = 2, Max = 3 }
    public enum BleedMode { Manual = 0, Radial = 1 }
    public enum BleedBlendMode { Mix = 0, Add = 1, Screen = 2, Max = 3 }

    // -------------------- Master --------------------
    [EffectSection("Master", 0)]
    [Range(0, 1)] public float masterBlend = 1f;

    // -------------------- Sampling / Grid --------------------
    [EffectSection("Sampling", 0)]
    [Range(1, 1024)] public int pixelSize = 1;

    [EffectSection("Sampling", 10)]
    [Tooltip("Locks sampling & dithering to a fixed virtual pixel grid, independent of GameView/backbuffer size.\nDoes NOT replace Pixelation; it's an additional stabilizer.")]
    public bool useVirtualGrid = false;

    [EffectSection("Sampling", 20)]
    [Tooltip("Typical vibes: 640x448, 640x480, 512x448, etc.")]
    public Vector2Int virtualResolution = new Vector2Int(720, 480);

    // -------------------- Pregrade --------------------
    [EffectSection("Pregrade", 0)] public bool pregradeEnabled = false;
    [EffectSection("Pregrade", 10)][Range(-5f, 5f)] public float exposure = 0f;
    [EffectSection("Pregrade", 20)][Range(0f, 2f)] public float contrast = 1f;
    [EffectSection("Pregrade", 30)][Range(0.1f, 3f)] public float gamma = 1f;
    [EffectSection("Pregrade", 40)][Range(0f, 2f)] public float saturation = 1f;

    // -------------------- Posterize --------------------
    [EffectSection("Posterize", 0)][Range(2, 512)] public int levels = 64;
    [EffectSection("Posterize", 10)] public bool usePerChannel = false;
    [EffectSection("Posterize", 20)][Range(2, 512)] public int levelsR = 64;
    [EffectSection("Posterize", 30)][Range(2, 512)] public int levelsG = 64;
    [EffectSection("Posterize", 40)][Range(2, 512)] public int levelsB = 64;

    [EffectSection("Posterize", 50)] public bool animateLevels = false;
    [EffectSection("Posterize", 60)][Range(2, 512)] public int minLevels = 64;
    [EffectSection("Posterize", 70)][Range(2, 512)] public int maxLevels = 64;
    [EffectSection("Posterize", 80)] public float speed = 1f;

    [EffectSection("Posterize", 90)] public bool luminanceOnly = false;
    [EffectSection("Posterize", 100)] public bool invert = false;

    // -------------------- Palette / Curve --------------------
    [EffectSection("Palette", 0)] public bool usePalette = false;
    [EffectSection("Palette", 10)] public Texture2D paletteTex;
    [EffectSection("Palette", 20)] public AnimationCurve thresholdCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // -------------------- Masks --------------------
    [EffectSection("TextureMask", 0)] public bool useMask = false;
    [EffectSection("TextureMask", 10)] public Texture2D maskTex;
    [EffectSection("TextureMask", 20)][Range(0, 1)] public float maskThreshold = 0.5f;

    [EffectSection("DepthMask", 0)] public bool useDepthMask = false;
    [EffectSection("DepthMask", 10)][Range(0.0f, 10.0f)] public float depthThreshold = 1.0f;

    // -------------------- Channel Jitter --------------------
    public enum JitterMode
    {
        Static = 0,          // fixed offsets (no time)
        TimeSine = 1,        // smooth wobble over time
        HashNoise = 2,       // pseudo-random noise (no texture needed)
        BlueNoiseTex = 3     // uses a noise texture (blue noise recommended)
    }

    [EffectSection("Jitter", 0)]
    public bool jitterEnabled = false;

    [EffectSection("Jitter", 10)]
    [Range(0f, 1f)] public float jitterStrength = 0f;

    [EffectSection("Jitter", 20)]
    public JitterMode jitterMode = JitterMode.TimeSine;

    [EffectSection("Jitter", 30)]
    [Tooltip("Scales offset in pixels (multiplied by texel size or virtual grid pixel size).")]
    [Range(0f, 8f)] public float jitterAmountPx = 1f;

    [EffectSection("Jitter", 40)]
    [Tooltip("Speed for TimeSine/Noise modes.")]
    [Range(0f, 30f)] public float jitterSpeed = 8f;

    [EffectSection("Jitter", 50)]
    [Tooltip("If enabled, randomizes using a stable seed (helps avoid identical look across cameras).")]
    public bool jitterUseSeed = false;

    [EffectSection("Jitter", 60)]
    [Range(0, 9999)] public int jitterSeed = 1337;

    [EffectSection("Jitter", 70)]
    [Tooltip("Optional: vary jitter per scanline (VHS-like).")]
    public bool jitterScanline = false;

    [EffectSection("Jitter", 80)]
    [Tooltip("Scanline density (lines per screen height). Typical: 240–720.")]
    [Range(32f, 2048f)] public float jitterScanlineDensity = 480f;

    [EffectSection("Jitter", 90)]
    [Tooltip("How much scanline modulation affects the offset.")]
    [Range(0f, 2f)] public float jitterScanlineAmp = 0.35f;

    [EffectSection("Jitter", 100)]
    [Tooltip("Per-channel intensity multipliers (R,G,B).")]
    public Vector3 jitterChannelWeights = new Vector3(1f, 1f, 1f);

    [EffectSection("Jitter", 110)]
    [Tooltip("Per-channel direction in pixel space (R.xy, G.xy, B.xy).")]
    public Vector2 jitterDirR = new Vector2(1f, 0f);

    [EffectSection("Jitter", 120)]
    public Vector2 jitterDirG = new Vector2(0f, 1f);

    [EffectSection("Jitter", 130)]
    public Vector2 jitterDirB = new Vector2(-1f, -1f);

    [EffectSection("Jitter", 140)]
    [Tooltip("Optional noise texture for BlueNoiseTex mode (128x128+ recommended).")]
    public Texture2D jitterNoiseTex = null;

    [EffectSection("Jitter", 150)]
    [Tooltip("Clamp UVs after offset (prevents sampling outside screen).")]
    public bool jitterClampUV = true;

    // -------------------- RGB Bleeding --------------------
    [EffectSection("Bleed", 0)][Range(0f, 1f)] public float bleedBlend = 0f;
    [EffectSection("Bleed", 10)][Range(0f, 10f)] public float bleedIntensity = 0f;
    [EffectSection("Bleed", 20)] public BleedMode bleedMode = BleedMode.Manual;
    [EffectSection("Bleed", 30)] public BleedBlendMode bleedBlendMode = BleedBlendMode.Screen;
    [EffectSection("Bleed", 40)] public Vector2 shiftR = new Vector2(-0.5f, 0.5f);
    [EffectSection("Bleed", 50)] public Vector2 shiftG = new Vector2(0.5f, -0.5f);
    [EffectSection("Bleed", 60)] public Vector2 shiftB = Vector2.zero;

    [EffectSection("Bleed", 70)] public bool bleedEdgeOnly = false;
    [EffectSection("Bleed", 80)][Range(0f, 1f)] public float bleedEdgeThreshold = 0.05f;
    [EffectSection("Bleed", 90)][Range(0.25f, 8f)] public float bleedEdgePower = 2f;

    [EffectSection("Bleed", 100)] public Vector2 bleedRadialCenter = new Vector2(0.5f, 0.5f);
    [EffectSection("Bleed", 110)][Range(0f, 5f)] public float bleedRadialStrength = 1f;

    [EffectSection("Bleed", 120)][Range(1, 8)] public int bleedSamples = 1;
    [EffectSection("Bleed", 130)][Range(0f, 5f)] public float bleedSmear = 0f;
    [EffectSection("Bleed", 140)][Range(0.25f, 6f)] public float bleedFalloff = 2f;

    [EffectSection("Bleed", 150)][Range(0f, 2f)] public float bleedIntensityR = 1f;
    [EffectSection("Bleed", 160)][Range(0f, 2f)] public float bleedIntensityG = 1f;
    [EffectSection("Bleed", 170)][Range(0f, 2f)] public float bleedIntensityB = 1f;
    [EffectSection("Bleed", 180)] public Vector2 bleedAnamorphic = Vector2.one;

    [EffectSection("Bleed", 190)] public bool bleedClampUV = false;
    [EffectSection("Bleed", 200)] public bool bleedPreserveLuma = false;

    [EffectSection("Bleed", 210)][Range(0f, 2f)] public float bleedWobbleAmp = 0f;
    [EffectSection("Bleed", 220)][Range(0f, 20f)] public float bleedWobbleFreq = 4f;
    [EffectSection("Bleed", 230)] public bool bleedWobbleScanline = false;

    // -------------------- Ghosting --------------------
    [EffectSection("Ghost", 0)] public bool ghostEnabled = false;
    [EffectSection("Ghost", 10)][Range(0f, 1f)] public float ghostBlend = 0.35f;
    [EffectSection("Ghost", 20)] public Vector2 ghostOffsetPx = Vector2.zero;

    [EffectSection("Ghost", 30)][Range(1, 16)] public int ghostFrames = 4;
    [EffectSection("Ghost", 40)][Range(0, 8)] public int ghostCaptureInterval = 0;
    [EffectSection("Ghost", 50)][Range(0, 8)] public int ghostStartDelay = 0;
    [EffectSection("Ghost", 60)][Range(0.25f, 4f)] public float ghostWeightCurve = 1.5f;

    [EffectSection("Ghost", 70)] public GhostCombineMode ghostCombineMode = GhostCombineMode.Screen;

    // -------------------- Unsharp --------------------
    [EffectSection("Unsharp", 0)] public bool unsharpEnabled = false;
    [EffectSection("Unsharp", 10)][Range(0f, 3f)] public float unsharpAmount = 0.5f;
    [EffectSection("Unsharp", 20)][Range(0.25f, 4f)] public float unsharpRadius = 1.0f;
    [EffectSection("Unsharp", 30)][Range(0f, 0.25f)] public float unsharpThreshold = 0.0f;

    [EffectSection("Unsharp", 40)] public bool unsharpLumaOnly = false;
    [EffectSection("Unsharp", 50)][Range(0f, 1f)] public float unsharpChroma = 0.0f;

    // -------------------- Edge Outline --------------------
    [EffectSection("Edges", 0)] public bool edgeEnabled = false;
    [EffectSection("Edges", 10)][Range(0f, 8f)] public float edgeStrength = 1f;
    [EffectSection("Edges", 20)][Range(0f, 1f)] public float edgeThreshold = 0.02f;
    [EffectSection("Edges", 30)][Range(0f, 1f)] public float edgeBlend = 1f;
    [EffectSection("Edges", 40)] public Color edgeColor = Color.black;

    // -------------------- Dithering --------------------
    [EffectSection("Dither", 0)] public DitherMode ditherMode = DitherMode.None;
    [EffectSection("Dither", 10)][Range(0f, 1f)] public float ditherStrength = 0.0f;
    [EffectSection("Dither", 20)] public Texture2D blueNoise;

    // -------------------- Stage shaders --------------------
    [EffectSection("Shaders", 0)] public Shader samplingGridShader;
    [EffectSection("Shaders", 10)] public Shader pregradeShader;
    [EffectSection("Shaders", 20)] public Shader channelJitterShader;
    [EffectSection("Shaders", 30)] public Shader ghostingShader;
    [EffectSection("Shaders", 40)] public Shader rgbBleedingShader;
    [EffectSection("Shaders", 50)] public Shader unsharpMaskShader;
    [EffectSection("Shaders", 60)] public Shader posterizeToneShader;
    [EffectSection("Shaders", 70)] public Shader ditheringShader;
    [EffectSection("Shaders", 80)] public Shader paletteMappingShader;
    [EffectSection("Shaders", 90)] public Shader edgeOutlineShader;
    [EffectSection("Shaders", 100)] public Shader masterPresentShader;
    [EffectSection("Shaders", 110)] public Shader textureMaskShader;
    [EffectSection("Shaders", 120)] public Shader depthMaskShader;
    [EffectSection("Shaders", 130)] public Shader ghostCompositeShader;

    // -------------------- Materials --------------------
    private Material _mSampling, _mPregrade, _mJitter, _mGhosting, _mBleed, _mUnsharp, _mPosterize, _mDither, _mPalette, _mEdges, _mPresent;
    private Material _mTexMask, _mDepthMask;
    private Material _mGhostComposite;

    // -------------------- Curve texture --------------------
    private Texture2D _curveTex;

    // -------------------- Ghost history --------------------
    private RenderTexture[] _ghostRing;
    private int _ghostWriteIndex;
    private int _ghostCaptureCounter;
    private RenderTexture _ghostCompositeTex;
    private bool _ghostSeeded;

    private void OnEnable()
    {
        AutoAssignShadersIfMissing();
        ClampAndSanitize();
        UpdateCurveTexture();
        EnsureDepthModeIfNeeded();
        EnsureGhostResources(null);
        _ghostSeeded = false;
    }

    private void OnValidate()
    {
        AutoAssignShadersIfMissing();
        ClampAndSanitize();
        UpdateCurveTexture();
        EnsureDepthModeIfNeeded();
    }

    private void OnDisable()
    {
        DestroyMat(ref _mSampling);
        DestroyMat(ref _mPregrade);
        DestroyMat(ref _mJitter);
        DestroyMat(ref _mGhosting);
        DestroyMat(ref _mBleed);
        DestroyMat(ref _mUnsharp);
        DestroyMat(ref _mPosterize);
        DestroyMat(ref _mDither);
        DestroyMat(ref _mPalette);
        DestroyMat(ref _mEdges);
        DestroyMat(ref _mPresent);
        DestroyMat(ref _mTexMask);
        DestroyMat(ref _mDepthMask);
        DestroyMat(ref _mGhostComposite);

        if (_curveTex) DestroyImmediate(_curveTex);
        ReleaseGhostResources();
    }

    private void ClampAndSanitize()
    {
        virtualResolution.x = Mathf.Max(1, virtualResolution.x);
        virtualResolution.y = Mathf.Max(1, virtualResolution.y);

        gamma = Mathf.Max(0.1f, gamma);

        ghostFrames = Mathf.Clamp(ghostFrames, 1, 16);
        ghostCaptureInterval = Mathf.Clamp(ghostCaptureInterval, 0, 8);
        ghostStartDelay = Mathf.Clamp(ghostStartDelay, 0, 8);
        ghostWeightCurve = Mathf.Clamp(ghostWeightCurve, 0.25f, 4f);

        minLevels = Mathf.Clamp(minLevels, 2, 512);
        maxLevels = Mathf.Clamp(maxLevels, 2, 512);
        levels = Mathf.Clamp(levels, 2, 512);
        levelsR = Mathf.Clamp(levelsR, 2, 512);
        levelsG = Mathf.Clamp(levelsG, 2, 512);
        levelsB = Mathf.Clamp(levelsB, 2, 512);

        bleedSamples = Mathf.Clamp(bleedSamples, 1, 8);
        bleedFalloff = Mathf.Clamp(bleedFalloff, 0.25f, 6f);
        bleedSmear = Mathf.Max(0f, bleedSmear);

        bleedIntensityR = Mathf.Max(0f, bleedIntensityR);
        bleedIntensityG = Mathf.Max(0f, bleedIntensityG);
        bleedIntensityB = Mathf.Max(0f, bleedIntensityB);

        bleedAnamorphic.x = Mathf.Max(0.0001f, bleedAnamorphic.x);
        bleedAnamorphic.y = Mathf.Max(0.0001f, bleedAnamorphic.y);

        bleedRadialStrength = Mathf.Max(0f, bleedRadialStrength);

        bleedWobbleAmp = Mathf.Max(0f, bleedWobbleAmp);
        bleedWobbleFreq = Mathf.Max(0f, bleedWobbleFreq);

        jitterSeed = Mathf.Clamp(jitterSeed, 0, 9999);
        jitterAmountPx = Mathf.Max(0f, jitterAmountPx);
        jitterSpeed = Mathf.Max(0f, jitterSpeed);
        jitterScanlineDensity = Mathf.Max(32f, jitterScanlineDensity);
        jitterScanlineAmp = Mathf.Max(0f, jitterScanlineAmp);
        jitterChannelWeights.x = Mathf.Max(0f, jitterChannelWeights.x);
        jitterChannelWeights.y = Mathf.Max(0f, jitterChannelWeights.y);
        jitterChannelWeights.z = Mathf.Max(0f, jitterChannelWeights.z);

    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (src == null) { Graphics.Blit(src, dest); return; }

        EnsureDepthModeIfNeeded();
        EnsureGhostResources(src);

        var desc = src.descriptor;
        desc.depthBufferBits = 0;

        RenderTexture a = null;
        RenderTexture b = null;
        RenderTexture baseAnchor = null;

        try
        {
            a = RenderTexture.GetTemporary(desc);
            b = RenderTexture.GetTemporary(desc);

            Graphics.Blit(src, a);

            RunSamplingGrid(a, b); Swap(ref a, ref b);
            RunPregrade(a, b); Swap(ref a, ref b);
            RunChannelJitter(a, b); Swap(ref a, ref b);

            BuildGhostComposite();
            RunGhosting(a, b); Swap(ref a, ref b);

            RunBleed(a, b); Swap(ref a, ref b);
            RunUnsharp(a, b); Swap(ref a, ref b);

            RunPosterizeTone(a, b); Swap(ref a, ref b);
            RunDithering(a, b); Swap(ref a, ref b);
            RunPaletteMapping(a, b); Swap(ref a, ref b);
            RunEdges(a, b); Swap(ref a, ref b);

            baseAnchor = RenderTexture.GetTemporary(desc);
            Graphics.Blit(src, baseAnchor);

            RunSamplingGrid(baseAnchor, b); Swap(ref baseAnchor, ref b);
            RunPregrade(baseAnchor, b); Swap(ref baseAnchor, ref b);

            if (useMask) { RunTextureMask(baseAnchor, a, b); Swap(ref a, ref b); }
            if (useDepthMask) { RunDepthMask(baseAnchor, a, b); Swap(ref a, ref b); }

            RunPresent(src, a, dest);
            CaptureGhostFrame(a);
        }
        finally
        {
            if (baseAnchor != null) RenderTexture.ReleaseTemporary(baseAnchor);
            if (a != null) RenderTexture.ReleaseTemporary(a);
            if (b != null) RenderTexture.ReleaseTemporary(b);
        }
    }

    private void RunSamplingGrid(RenderTexture src, RenderTexture dst)
    {
        var m = MSampling;
        if (!m) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_PixelSize", Mathf.Max(1, pixelSize));
        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));

        Graphics.Blit(src, dst, m);
    }

    private void RunPregrade(RenderTexture src, RenderTexture dst)
    {
        var m = MPregrade;
        if (!m) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_PregradeEnabled", pregradeEnabled ? 1f : 0f);
        m.SetFloat("_Exposure", exposure);
        m.SetFloat("_Contrast", contrast);
        m.SetFloat("_Gamma", gamma);
        m.SetFloat("_Saturation", saturation);

        Graphics.Blit(src, dst, m);
    }

    private void RunChannelJitter(RenderTexture src, RenderTexture dst)
    {
        var m = MJitter;

        if (!m || !jitterEnabled || jitterStrength <= 0f)
        {
            Graphics.Blit(src, dst);
            return;
        }

        m.SetFloat("_JitterEnabled", 1f);
        m.SetFloat("_JitterStrength", jitterStrength);
        m.SetFloat("_JitterMode", (float)jitterMode);

        m.SetFloat("_JitterAmountPx", jitterAmountPx);
        m.SetFloat("_JitterSpeed", jitterSpeed);

        m.SetFloat("_UseSeed", jitterUseSeed ? 1f : 0f);
        m.SetFloat("_Seed", jitterSeed);

        m.SetFloat("_Scanline", jitterScanline ? 1f : 0f);
        m.SetFloat("_ScanlineDensity", jitterScanlineDensity);
        m.SetFloat("_ScanlineAmp", jitterScanlineAmp);

        m.SetVector("_ChannelWeights", new Vector4(jitterChannelWeights.x, jitterChannelWeights.y, jitterChannelWeights.z, 0f));
        m.SetVector("_DirR", new Vector4(jitterDirR.x, jitterDirR.y, 0f, 0f));
        m.SetVector("_DirG", new Vector4(jitterDirG.x, jitterDirG.y, 0f, 0f));
        m.SetVector("_DirB", new Vector4(jitterDirB.x, jitterDirB.y, 0f, 0f));

        m.SetFloat("_ClampUV", jitterClampUV ? 1f : 0f);

        // Optional noise texture (used in BlueNoiseTex mode)
        m.SetTexture("_NoiseTex", jitterNoiseTex != null ? jitterNoiseTex : Texture2D.grayTexture);

        // Anchor to virtual grid if enabled (so jitter is resolution-stable)
        m.SetFloat("_PixelSize", Mathf.Max(1, pixelSize));
        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));

        Graphics.Blit(src, dst, m);
    }

    private void RunGhosting(RenderTexture src, RenderTexture dst)
    {
        var m = MGhosting;

        if (!m || !ghostEnabled || ghostBlend <= 0f || _ghostCompositeTex == null || !_ghostSeeded)
        {
            Graphics.Blit(src, dst);
            return;
        }

        m.SetFloat("_GhostEnabled", 1f);
        m.SetFloat("_GhostBlend", ghostBlend);
        m.SetVector("_GhostOffsetPx", new Vector4(ghostOffsetPx.x, ghostOffsetPx.y, 0, 0));
        m.SetFloat("_CombineMode", (float)ghostCombineMode);

        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));
        m.SetTexture("_PrevTex", _ghostCompositeTex);

        Graphics.Blit(src, dst, m);
    }

    private void RunBleed(RenderTexture src, RenderTexture dst)
    {
        var m = MBleed;
        if (!m || bleedBlend <= 0f || bleedIntensity <= 0f) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_BleedBlend", bleedBlend);
        m.SetFloat("_BleedIntensity", bleedIntensity);
        m.SetFloat("_BleedMode", (float)bleedMode);

        m.SetFloat("_BleedBlendMode", (float)bleedBlendMode);
        m.SetFloat("_Mode", (float)bleedMode);
        m.SetFloat("_BlendMode", (float)bleedBlendMode);
        m.SetFloat("_CombineMode", (float)bleedBlendMode);
        
        m.SetVector("_ShiftR", new Vector4(shiftR.x, shiftR.y, 0, 0));
        m.SetVector("_ShiftG", new Vector4(shiftG.x, shiftG.y, 0, 0));
        m.SetVector("_ShiftB", new Vector4(shiftB.x, shiftB.y, 0, 0));

        m.SetFloat("_EdgeOnly", bleedEdgeOnly ? 1f : 0f);
        m.SetFloat("_EdgeThreshold", bleedEdgeThreshold);
        m.SetFloat("_EdgePower", bleedEdgePower);

        m.SetVector("_RadialCenter", new Vector4(bleedRadialCenter.x, bleedRadialCenter.y, 0, 0));
        m.SetFloat("_RadialStrength", bleedRadialStrength);

        m.SetFloat("_Samples", bleedSamples);
        m.SetFloat("_Smear", bleedSmear);
        m.SetFloat("_Falloff", bleedFalloff);

        m.SetFloat("_IntensityR", bleedIntensityR);
        m.SetFloat("_IntensityG", bleedIntensityG);
        m.SetFloat("_IntensityB", bleedIntensityB);
        m.SetVector("_Anamorphic", new Vector4(bleedAnamorphic.x, bleedAnamorphic.y, 0, 0));

        m.SetFloat("_ClampUV", bleedClampUV ? 1f : 0f);
        m.SetFloat("_PreserveLuma", bleedPreserveLuma ? 1f : 0f);

        m.SetFloat("_WobbleAmp", bleedWobbleAmp);
        m.SetFloat("_WobbleFreq", bleedWobbleFreq);
        m.SetFloat("_WobbleScanline", bleedWobbleScanline ? 1f : 0f);

        m.SetFloat("_PixelSize", Mathf.Max(1, pixelSize));
        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));

        Graphics.Blit(src, dst, m);
    }

    private void RunUnsharp(RenderTexture src, RenderTexture dst)
    {
        var m = MUnsharp;
        if (!m || !unsharpEnabled || unsharpAmount <= 0f) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_UnsharpEnabled", 1f);
        m.SetFloat("_UnsharpAmount", unsharpAmount);
        m.SetFloat("_UnsharpRadius", Mathf.Max(0.25f, unsharpRadius));
        m.SetFloat("_UnsharpThreshold", Mathf.Max(0f, unsharpThreshold));
        m.SetFloat("_UnsharpLumaOnly", unsharpLumaOnly ? 1f : 0f);
        m.SetFloat("_UnsharpChroma", unsharpChroma);

        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));

        Graphics.Blit(src, dst, m);
    }

    private void RunPosterizeTone(RenderTexture src, RenderTexture dst)
    {
        var m = MPosterize;
        if (!m) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_LuminanceOnly", luminanceOnly ? 1f : 0f);
        m.SetFloat("_AnimateLevels", animateLevels ? 1f : 0f);
        m.SetFloat("_MinLevels", Mathf.Max(2, minLevels));
        m.SetFloat("_MaxLevels", Mathf.Max(2, maxLevels));
        m.SetFloat("_Speed", speed);

        Graphics.Blit(src, dst, m);
    }

    private void RunDithering(RenderTexture src, RenderTexture dst)
    {
        var m = MDither;
        if (!m) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_Levels", Mathf.Max(2, levels));
        m.SetFloat("_UsePerChannel", usePerChannel ? 1f : 0f);
        m.SetFloat("_LevelsR", Mathf.Max(2, levelsR));
        m.SetFloat("_LevelsG", Mathf.Max(2, levelsG));
        m.SetFloat("_LevelsB", Mathf.Max(2, levelsB));

        m.SetFloat("_AnimateLevels", animateLevels ? 1f : 0f);
        m.SetFloat("_MinLevels", Mathf.Max(2, minLevels));
        m.SetFloat("_MaxLevels", Mathf.Max(2, maxLevels));
        m.SetFloat("_Speed", speed);

        m.SetFloat("_DitherMode", (float)ditherMode);
        m.SetFloat("_DitherStrength", (ditherMode == DitherMode.None) ? 0f : ditherStrength);

        m.SetTexture("_BlueNoise",
            (ditherMode == DitherMode.BlueNoise && blueNoise != null) ? blueNoise : Texture2D.grayTexture);

        m.SetFloat("_PixelSize", Mathf.Max(1, pixelSize));
        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));

        Graphics.Blit(src, dst, m);
    }

    private void RunPaletteMapping(RenderTexture src, RenderTexture dst)
    {
        var m = MPalette;
        if (!m) { Graphics.Blit(src, dst); return; }

        m.SetTexture("_ThresholdTex", _curveTex ? _curveTex : Texture2D.whiteTexture);

        bool paletteOn = usePalette && paletteTex != null;
        m.SetFloat("_UsePalette", paletteOn ? 1f : 0f);
        m.SetTexture("_PaletteTex", paletteTex != null ? paletteTex : Texture2D.whiteTexture);

        m.SetFloat("_Invert", invert ? 1f : 0f);

        Graphics.Blit(src, dst, m);
    }

    private void RunEdges(RenderTexture src, RenderTexture dst)
    {
        var m = MEdges;
        if (!m || !edgeEnabled || edgeBlend <= 0f) { Graphics.Blit(src, dst); return; }

        m.SetFloat("_EdgeEnabled", 1f);
        m.SetFloat("_EdgeStrength", edgeStrength);
        m.SetFloat("_EdgeThreshold", edgeThreshold);
        m.SetFloat("_EdgeBlend", edgeBlend);
        m.SetColor("_EdgeColor", edgeColor);

        m.SetFloat("_UseVirtualGrid", useVirtualGrid ? 1f : 0f);
        m.SetVector("_VirtualRes", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));

        Graphics.Blit(src, dst, m);
    }

    private void RunTextureMask(RenderTexture baseTex, RenderTexture fxTex, RenderTexture dst)
    {
        var m = MTextureMask;
        if (!m) { Graphics.Blit(fxTex, dst); return; }

        bool enabled = useMask && maskTex != null;
        m.SetFloat("_UseMask", enabled ? 1f : 0f);

        m.SetTexture("_MaskTex", maskTex != null ? maskTex : Texture2D.whiteTexture);
        m.SetFloat("_MaskThreshold", maskThreshold);
        m.SetTexture("_MaskedTex", fxTex);

        Graphics.Blit(baseTex, dst, m);
    }

    private void RunDepthMask(RenderTexture baseTex, RenderTexture fxTex, RenderTexture dst)
    {
        var m = MDepthMask;
        if (!m) { Graphics.Blit(fxTex, dst); return; }

        m.SetFloat("_UseDepthMask", useDepthMask ? 1f : 0f);
        m.SetFloat("_DepthThreshold", Mathf.Max(0f, depthThreshold));
        m.SetTexture("_MaskedTex", fxTex);

        Graphics.Blit(baseTex, dst, m);
    }

    private void RunPresent(RenderTexture originalSrc, RenderTexture processed, RenderTexture dst)
    {
        var m = MPresent;
        if (!m) { Graphics.Blit(processed, dst); return; }

        m.SetTexture("_OriginalTex", originalSrc);
        m.SetFloat("_MasterBlend", masterBlend);

        Graphics.Blit(processed, dst, m);
    }

    private void EnsureGhostResources(RenderTexture src)
    {
        int w = Mathf.Max(1, src ? src.width : Screen.width);
        int h = Mathf.Max(1, src ? src.height : Screen.height);

        int n = Mathf.Clamp(ghostFrames, 1, 16);

        bool needsRing = (_ghostRing == null || _ghostRing.Length != n);
        bool needsResize = false;

        if (!needsRing && _ghostRing != null && _ghostRing.Length > 0 && _ghostRing[0] != null)
            needsResize = (_ghostRing[0].width != w || _ghostRing[0].height != h);

        if (!needsRing && !needsResize && _ghostCompositeTex != null)
            needsResize = (_ghostCompositeTex.width != w || _ghostCompositeTex.height != h);

        if (!needsRing && !needsResize) return;

        ReleaseGhostResources();

        _ghostRing = new RenderTexture[n];
        for (int i = 0; i < n; i++)
        {
            _ghostRing[i] = CreateGhostRT(w, h);
            Graphics.Blit(Texture2D.blackTexture, _ghostRing[i]);
        }

        _ghostCompositeTex = CreateGhostRT(w, h);
        Graphics.Blit(Texture2D.blackTexture, _ghostCompositeTex);

        _ghostWriteIndex = 0;
        _ghostCaptureCounter = 0;
        _ghostSeeded = false;
    }

    private static RenderTexture CreateGhostRT(int w, int h)
    {
        var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        rt.Create();
        return rt;
    }

    private static void SafeReleaseAndDestroyRT(ref RenderTexture rt)
    {
        if (rt == null) return;
        if (RenderTexture.active == rt) RenderTexture.active = null;
        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }

    private void ReleaseGhostResources()
    {
        if (_ghostRing != null)
        {
            for (int i = 0; i < _ghostRing.Length; i++)
                SafeReleaseAndDestroyRT(ref _ghostRing[i]);
            _ghostRing = null;
        }

        SafeReleaseAndDestroyRT(ref _ghostCompositeTex);
        _ghostSeeded = false;
    }

    private void BuildGhostComposite()
    {
        if (!_ghostSeeded || !ghostEnabled || ghostBlend <= 0f || _ghostRing == null || _ghostRing.Length == 0 || _ghostCompositeTex == null)
        {
            if (_ghostCompositeTex != null) Graphics.Blit(Texture2D.blackTexture, _ghostCompositeTex);
            return;
        }

        var m = MGhostComposite;
        if (!m) { Graphics.Blit(Texture2D.blackTexture, _ghostCompositeTex); return; }

        int n = _ghostRing.Length;
        int start = Mathf.Clamp(ghostStartDelay, 0, n - 1);

        for (int i = 0; i < 16; i++)
            m.SetTexture("_Hist" + i, Texture2D.blackTexture);

        int count = 0;
        for (int k = start; k < n && count < 16; k++)
        {
            int idx = WrapIndex(_ghostWriteIndex - 1 - k, n);
            var rt = _ghostRing[idx];
            m.SetTexture("_Hist" + count, rt != null ? rt : Texture2D.blackTexture);
            count++;
        }

        m.SetInt("_Count", count);
        m.SetFloat("_WeightCurve", ghostWeightCurve);

        Graphics.Blit(Texture2D.blackTexture, _ghostCompositeTex, m);
    }

    private void CaptureGhostFrame(RenderTexture frameTex)
    {
        if (!ghostEnabled || _ghostRing == null || _ghostRing.Length == 0 || frameTex == null)
            return;

        if (!_ghostSeeded)
        {
            for (int i = 0; i < _ghostRing.Length; i++)
                if (_ghostRing[i] != null) Graphics.Blit(frameTex, _ghostRing[i]);

            if (_ghostCompositeTex != null) Graphics.Blit(frameTex, _ghostCompositeTex);

            _ghostWriteIndex = 0;
            _ghostCaptureCounter = 0;
            _ghostSeeded = true;
            return;
        }

        int interval = Mathf.Max(0, ghostCaptureInterval);
        if (_ghostCaptureCounter < interval) { _ghostCaptureCounter++; return; }

        _ghostCaptureCounter = 0;

        var rt = _ghostRing[_ghostWriteIndex];
        if (rt != null) Graphics.Blit(frameTex, rt);

        _ghostWriteIndex = WrapIndex(_ghostWriteIndex + 1, _ghostRing.Length);
    }

    private static int WrapIndex(int x, int n)
    {
        if (n <= 0) return 0;
        x %= n;
        if (x < 0) x += n;
        return x;
    }

    private void EnsureDepthModeIfNeeded()
    {
        var cam = GetComponent<Camera>();
        if (!cam) return;
        if (useDepthMask || edgeEnabled) cam.depthTextureMode |= DepthTextureMode.Depth;
    }

    private void UpdateCurveTexture()
    {
        if (_curveTex == null || _curveTex.width != 256)
        {
            _curveTex = new Texture2D(256, 1, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (thresholdCurve == null)
            thresholdCurve = AnimationCurve.Linear(0, 0, 1, 1);

        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            float v = Mathf.Clamp01(thresholdCurve.Evaluate(t));
            _curveTex.SetPixel(i, 0, new Color(v, 0, 0, 0));
        }

        _curveTex.Apply(false, false);
    }

    private Material MSampling => GetOrCreate(ref _mSampling, samplingGridShader, "Hidden/CrowFX/Stages/SamplingGrid");
    private Material MPregrade => GetOrCreate(ref _mPregrade, pregradeShader, "Hidden/CrowFX/Stages/Pregrade");
    private Material MJitter => GetOrCreate(ref _mJitter, channelJitterShader, "Hidden/CrowFX/Stages/ChannelJitter");
    private Material MGhosting => GetOrCreate(ref _mGhosting, ghostingShader, "Hidden/CrowFX/Stages/Ghosting");
    private Material MBleed => GetOrCreate(ref _mBleed, rgbBleedingShader, "Hidden/CrowFX/Stages/RGBBleeding");
    private Material MUnsharp => GetOrCreate(ref _mUnsharp, unsharpMaskShader, "Hidden/CrowFX/Stages/UnsharpMask");
    private Material MPosterize => GetOrCreate(ref _mPosterize, posterizeToneShader, "Hidden/CrowFX/Stages/PosterizeTone");
    private Material MDither => GetOrCreate(ref _mDither, ditheringShader, "Hidden/CrowFX/Stages/Dithering");
    private Material MPalette => GetOrCreate(ref _mPalette, paletteMappingShader, "Hidden/CrowFX/Stages/PaletteMapping");
    private Material MEdges => GetOrCreate(ref _mEdges, edgeOutlineShader, "Hidden/CrowFX/Stages/EdgeOutline");
    private Material MPresent => GetOrCreate(ref _mPresent, masterPresentShader, "Hidden/CrowFX/Stages/MasterPresent");

    private Material MTextureMask => GetOrCreate(ref _mTexMask, textureMaskShader, "Hidden/CrowFX/Stages/TextureMask");
    private Material MDepthMask => GetOrCreate(ref _mDepthMask, depthMaskShader, "Hidden/CrowFX/Stages/DepthMask");

    private Material MGhostComposite => GetOrCreate(ref _mGhostComposite, ghostCompositeShader, "Hidden/CrowFX/Helpers/GhostComposite");

    private static Material GetOrCreate(ref Material mat, Shader assigned, string fallbackName)
    {
        if (mat) return mat;

        Shader s = assigned ? assigned : Shader.Find(fallbackName);
        if (!s) return null;

        mat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        return mat;
    }

    private static void DestroyMat(ref Material m)
    {
        if (m) { DestroyImmediate(m); m = null; }
    }

    private static void Swap(ref RenderTexture a, ref RenderTexture b)
    {
        var tmp = a; a = b; b = tmp;
    }

    private void AutoAssignShadersIfMissing()
    {
        samplingGridShader = AutoShader(samplingGridShader, "Hidden/CrowFX/Stages/SamplingGrid");
        pregradeShader = AutoShader(pregradeShader, "Hidden/CrowFX/Stages/Pregrade");
        channelJitterShader = AutoShader(channelJitterShader, "Hidden/CrowFX/Stages/ChannelJitter");
        ghostingShader = AutoShader(ghostingShader, "Hidden/CrowFX/Stages/Ghosting");
        rgbBleedingShader = AutoShader(rgbBleedingShader, "Hidden/CrowFX/Stages/RGBBleeding");
        unsharpMaskShader = AutoShader(unsharpMaskShader, "Hidden/CrowFX/Stages/UnsharpMask");
        posterizeToneShader = AutoShader(posterizeToneShader, "Hidden/CrowFX/Stages/PosterizeTone");
        ditheringShader = AutoShader(ditheringShader, "Hidden/CrowFX/Stages/Dithering");
        paletteMappingShader = AutoShader(paletteMappingShader, "Hidden/CrowFX/Stages/PaletteMapping");
        edgeOutlineShader = AutoShader(edgeOutlineShader, "Hidden/CrowFX/Stages/EdgeOutline");
        masterPresentShader = AutoShader(masterPresentShader, "Hidden/CrowFX/Stages/MasterPresent");

        textureMaskShader = AutoShader(textureMaskShader, "Hidden/CrowFX/Stages/TextureMask");
        depthMaskShader = AutoShader(depthMaskShader, "Hidden/CrowFX/Stages/DepthMask");

        ghostCompositeShader = AutoShader(ghostCompositeShader, "Hidden/CrowFX/Helpers/GhostComposite");
    }

    private static Shader AutoShader(Shader current, string shaderName)
    {
        if (current != null) return current;
        return Shader.Find(shaderName);
    }
}
