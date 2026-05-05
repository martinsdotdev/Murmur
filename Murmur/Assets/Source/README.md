# Murmur app icon — source assets

This folder is the **source of truth** for the Murmur app icon. The PNG/ICO files in `Murmur/Assets/` are *generated* from the SVGs here — never edit them by hand.

## Layout

| File | Purpose |
|---|---|
| `AppIcon.svg` | Master design — colored M on a warm-white rounded-square tile. Used for plated icons, tiles, splash, and `.ico`. |
| `AppIcon.mono.svg` | Single-color silhouette of the M (uses `#000000` as a templating placeholder). Tighter viewBox so the M fills the canvas — used for `_altform-unplated*` taskbar variants. |
| `build-icons.ps1` | Renders both SVGs into the full asset matrix in `Murmur/Assets/`. |

## Prerequisites

ImageMagick 7+ on PATH:

```powershell
winget install ImageMagick.ImageMagick
```

## Regenerating assets

```powershell
cd Murmur/Assets/Source
./build-icons.ps1
```

The script renders three high-resolution master PNGs from the SVGs (one colored, two mono-color-swapped) and derives every output size by resizing those masters — fast and consistent. Total runtime ~10 seconds.

## Verifying assets match SVGs (CI)

```powershell
./build-icons.ps1 -Verify
```

Renders to a temp dir and compares SHA-256 hashes against committed files. Exits non-zero on mismatch — useful for CI to catch "edited the SVG but forgot to regenerate."

## Asset matrix

| Logo | Sizes generated |
|---|---|
| `Square44x44Logo.scale-{100,125,150,200,400}` | 44/55/66/88/176 px |
| `Square44x44Logo.targetsize-{16,20,24,32,48,256}` | × 3 altforms (plated / unplated / lightunplated) |
| `Square150x150Logo.scale-*` | 150/188/225/300/600 px |
| `Wide310x150Logo.scale-*` | 310×150 → 1240×600 (icon centered with `#F4F1EA` padding) |
| `StoreLogo.scale-*` | 50/63/75/100/200 px |
| `SplashScreen.scale-*` | 620×300 → 2480×1200 (centered + padded) |
| `AppIcon.ico` | Multi-resolution: 16/20/24/32/48/64/128/256 |

## Design notes

- Palette: deep indigo `#2D2A6F` foreground on warm-white `#F4F1EA` background. ~10.5:1 contrast (WCAG AA).
- M is built as a 5-point stroke (`stroke-width="80"`, `stroke-linecap="round"`, `stroke-linejoin="round"`) inside a 1024 viewBox.
- Wave dots collapse below visibility around 24 px — intended; the M silhouette carries the icon at that size.
- Unplated mono SVG uses a tighter viewBox (`72 72 880 880`) so the M fills the icon area on the taskbar — Windows draws the surrounding plate, we don't.
