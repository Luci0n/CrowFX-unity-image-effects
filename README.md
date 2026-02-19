# CrowFX

A Unity image effects stack for retro and analog aesthetics. Includes posterization, dithering, RGB bleed, channel jitter, ghosting, edge outlines, palette mapping, and more — all composited through a custom editor with per-section controls.

---

## Effects

| Effect | Description |
|---|---|
| **Sampling & Grid** | Pixel size multiplier and virtual resolution grid to stabilize sampling |
| **Pre-Grade** | Exposure, contrast, gamma, and saturation applied before quantization |
| **Posterize** | Uniform or per-channel color quantization with optional animation |
| **Palette** | Maps final colors through a palette texture with a tonal curve |
| **Dithering** | Ordered (2×2, 4×4, 8×8), noise, and blue noise patterns |
| **RGB Bleed** | Chromatic aberration with manual, radial, edge-gated, and smear modes |
| **Channel Jitter** | Per-channel UV offset with sine, hash noise, and blue noise modes |
| **Ghosting** | Weighted composite of previous frames for motion trail effects |
| **Edge Outline** | Depth-based edge detection with color and blend controls |
| **Unsharp Mask** | Sharpening via blurred subtraction with luma-only mode |
| **Texture Mask** | Selectively apply effects using a grayscale mask texture |
| **Depth Mask** | Attenuate effects by camera distance |

---

## Requirements

- Unity 2022.3 or later (earlier versions untested)
- Built-in Render Pipeline
- Camera with `OnRenderImage` support

---

## Installation

**Option A — Unity Package Manager (UPM)**

In Unity, open `Window > Package Manager`, click `+` → `Add package from git URL`, and enter:

```
https://github.com/Luci0n/CrowFX-unity-image-effects.git
```

**Option B — Unity Package**

Download the `.unitypackage` from the [Releases](https://github.com/Luci0n/CrowFX-unity-image-effects/releases) page and import it via `Assets > Import Package > Custom Package`.

---

## Usage

Add the `CrowImageEffects` component to any camera. All effects are controlled from the custom inspector:

- Each section can be **expanded**, **reset**, or **randomized** independently
- **Star** any section to pin it to the top of the inspector
- Use the **search bar** to filter settings by name
- The **summary bar** shows key active values at a glance
- Shaders are auto-assigned by name — no manual wiring needed

---

## Project Structure

```
CrowFX/
├── CrowImageEffects.cs
├── .gitignore
├── Editor/
│   └── CrowImageEffectsEditor.cs
├── Font/
│   └── JetBrainsMonoNL-Thin.ttf
├── Helpers/
│   ├── CE_GhostComposite.shader
│   └── EffectSectionAttribute.cs
├── Icons/
└── Stages/
    ├── CE_ChannelJitter.shader
    ├── CE_DepthMask.shader
    ├── CE_Dithering.shader
    ├── CE_EdgeOutline.shader
    ├── CE_Ghosting.shader
    ├── CE_MasterPresent.shader
    ├── CE_PaletteMapping.shader
    ├── CE_PosterizeTone.shader
    ├── CE_Pregrade.shader
    ├── CE_RGBBleeding.shader
    ├── CE_SamplingGrid.shader
    ├── CE_TextureMask.shader
    └── CE_UnsharpMask.shader
```

---

## License

MIT
