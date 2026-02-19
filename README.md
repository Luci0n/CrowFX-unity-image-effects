<p align="center">
<img width="718" height="131" alt="header" src="https://github.com/user-attachments/assets/e4fbf6dc-9f26-40bd-9dae-4f3ff64741fc"/>
</p>

---
A Unity image effects stack for retro and analog aesthetics. Includes posterization, dithering, RGB bleed, channel jitter, ghosting, edge outlines, palette mapping, and more — all composited through a custom editor with per-section controls.

<img width="1323" height="794" alt="comparison" src="https://github.com/user-attachments/assets/75375a0d-0bea-44e4-9edf-8193545d5f07" />

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
https://github.com/Luci0n/CrowFX-Unity-Image-Effects.git?path=CrowFX
```

**Option B — Unity Package**

Download the `.unitypackage` from the [Releases](https://github.com/Luci0n/CrowFX-unity-image-effects/releases) page and import it via `Assets > Import Package > Custom Package` (or just double-click the file).

---

## Usage

Add the `CrowImageEffects` component to any camera. All effects are controlled from the custom inspector:

<img width="500" height="833" padding="5px" alt="crowfx-menu" src="https://github.com/user-attachments/assets/37c2cb3d-73c8-4c22-a8e7-58ef6f0c099f" />

- Each section can be **expanded**, **reset**, or **randomized** independently
- **Star** any section to pin it to the top of the inspector
- Use the **search bar** to filter settings by name
- The **summary bar** shows key active values at a glance

---
## Examples

<sub>1. Ghosting + Jitter + Dither</sub>

![recording](https://github.com/user-attachments/assets/604eeb15-4901-4867-8834-d25287cdd2c3)

<sub>2. Posterize + RGB Bleed (Luma) + Unsharp Mask + Virtual Resolution</sub>

![recording2](https://github.com/user-attachments/assets/9504b73c-be0f-4189-9c00-7c710078ede5)

<sub>3. Posterize (Per-Channel) + Edge Outline + Dither (Noise)</sub>

![recording3](https://github.com/user-attachments/assets/597c467b-2dcf-46ab-9e45-bdf9f59ac928)

---
## License

MIT
