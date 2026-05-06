# Murmur app icon, source assets

This folder is the **source of truth** for the Murmur app icon. The PNG/ICO files in `Murmur/Assets/` are *generated* from the SVGs here, never edit them by hand.

## Layout

| File | Purpose |
|---|---|
| `AppIcon.svg` | Master design, the Fader M in white on the amber gradient tile. Used for plated icons, tiles, splash, and `.ico`. |
| `AppIcon.mono.svg` | Single-color silhouette of the mark (uses `#000000` as a templating placeholder). Tighter viewBox so the levels fill the canvas, used for `_altform-unplated*` taskbar variants. |
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

The script renders three high-resolution master PNGs from the SVGs (one colored, two mono-color-swapped) and derives every output size by resizing those masters, fast and consistent. Total runtime ~10 seconds.

## Verifying assets match SVGs (CI)

```powershell
./build-icons.ps1 -Verify
```

Renders to a temp dir and compares SHA-256 hashes against committed files. Exits non-zero on mismatch, useful for CI to catch "edited the SVG but forgot to regenerate."

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

- **Fader M.** Three levels on the tile: the outer two are the letter's stems, the dropped middle one its valley. A mixer at a glance, a letter on second read.
- Everything sits on the 24 grid inside a 768 viewBox: keep-out `96`, level `w96` on a `48` gap with `r48`, heights `312 / 144 / 312`, baseline `528`.
- The tile is a full-height `#A85820` rect under a `744`-tall gradient face. The 24 that shows along the bottom grounds the mark without costing a shadow filter.
- Tile gradient is radial (`cx 0.7, cy 0.15, r 0.95`) through `#FFEDC8 → #F5C375 → #A85820`.
- The gaps stay open down to 16 px because each level is an eighth of the tile wide.
- Unplated mono SVG crops to `168 156 432 432` so the levels fill the taskbar icon; Windows draws the surrounding plate, we don't. `$BgColor` fills it on dark taskbars, `$FgColor` (`#21120E`) on light ones.
