# App icon redesign, Fluent 2 with coffee-audio metaphor and "M" brand badge

**Date:** 2026-05-03
**Status:** Locked, ready for plan + implementation
**Scope:** Replace the current `Murmur/Assets/Source/AppIcon.svg` (and derived asset matrix) with a new design system covering all Windows launch-icon and tray sizes.

## Background

The current icon is a stylised uppercase "M" letterform with cascading dots, on a cream `#F4F1EA` tile. Three problems:

1. **Microsoft's app-icon guidance explicitly disallows typography** ("avoid letters and words"). The bare "M" violates that.
2. **Two metaphors at once**, the M letter plus the cascading dots intended to read as a soundwave. Microsoft caps at two; the wave reading collapses below ~24 px because the dots merge.
3. **Audit at small sizes** showed only the M survived; the wave dots and the "wave" reading disappeared at exactly the size where the icon lives most (16-32 px taskbar/tray).

Murmur is a Windows port of the Linux Blanket app. The original Blanket icon is a literal folded-blanket illustration in the Adwaita blue ramp. Murmur's icon need not match Blanket directly, but visual lineage to GNOME and to other Microsoft Fluent 2 icons (OneDrive, Calculator, PowerToys Awake) is desirable.

## Goal

A 3-tier icon system that:

- Reads instantly at every Windows asset size (16, 20, 24, 32, 48, 64, 96, 128, 192, 256 px).
- Communicates "warm cup of ambient sound", the metaphor unique to Murmur (= coffee shop / cosy ritual / atmospheric audio).
- Carries an unambiguous "M" brand mark at large sizes for taskbar/Store recognition.
- Generates from SVG masters via the existing `build-icons.ps1` pipeline (extended for tiered routing).

## Decisions made during brainstorming

26 visual iterations, summarised:

| Decision | Outcome |
|---|---|
| Metaphor | Coffee cup with audio bars rising from the rim ("coffee-audio"), combines Lucide `coffee` + `book-audio` patterns. |
| Style register | Fluent 2 *product launch icon* (multi-gradient, layered) for large sizes; Fluent 2 *system icon* (monoline, currentColor) for small sizes. |
| Number of variants | 3 tiers, not 1, per the icon-design skill's "Size Variant System" pattern and the screen-15 sharp-edges audit. |
| Geometry grid | 768 × 768 viewBox at 32 px per Lucide unit, divisible by every standard icon size (16/24/32/48/64/96/128/192/256/384), so every output rasterises pixel-perfect. |
| Cup geometry | Wider/squatter teacup shape (16 × 8 ratio, bulging walls, integrated handle with `fill-rule="evenodd"` aperture). Path adopted from a user-supplied reference, not from Lucide `coffee` directly. |
| Cup body palette | Warm cream gradient (white → `#C8A878`), 2 stops. |
| Tile palette | Warm Adwaita-derived ramp: `#FFEDC8` → `#F5C375` → `#A85820`, single radial gradient with off-centre origin. |
| Audio bars | Solid white, 3 rectangles at 4:2:2 height ratio, no gradient or opacity. |
| Coffee surface | Removed at user request after exploring it as a focal element. The cup is a vessel; semantics shift from "coffee with audio" to "ambient sound from a warm vessel." |
| M brand badge | Word-style: rounded rectangular plate at bottom-left, white "M" letterform (Word's W path Y-mirrored, scaled 1.58×). Plate uses the coffee gradient (`#8B5E3C` → `#4E342E` → `#21120E`) so the M reads as the warmest/deepest element, balancing the light tile. |
| Shadow | Fluent 2 standard two-layer stack (ambient + key) on the cup-and-handle group only. Audio bars and M plate cast no shadows. |
| What got nuked | Coffee surface ellipse, coffee highlight, cup gradient mid-stops, three tile overlay layers (bloom, vignette, lower-left shadow), audio bar 0.9 opacity. Total fill count went from ~25 → 5 (excluding the shadow filter and M badge). |

## The locked design

### Tier 3, launch icon (48-256 px)

Ships as `Square150x150Logo.scale-*`, `SplashScreen.scale-*`, `StoreLogo.scale-*`, `Square44x44Logo.targetsize-{48,256}`, `.ico`'s 256-px slice.

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 768 768">
  <defs>
    <radialGradient id="tile" cx="0.7" cy="0.15" r="0.95">
      <stop offset="0" stop-color="#FFEDC8"/>
      <stop offset="0.5" stop-color="#F5C375"/>
      <stop offset="1" stop-color="#A85820"/>
    </radialGradient>
    <linearGradient id="cup" x1="0" y1="349" x2="0" y2="611" gradientUnits="userSpaceOnUse">
      <stop offset="0%" stop-color="#FFFFFF"/>
      <stop offset="100%" stop-color="#C8A878"/>
    </linearGradient>
    <radialGradient id="plate" cx="0.3" cy="0.3" r="0.85">
      <stop offset="0" stop-color="#8B5E3C"/>
      <stop offset="0.5" stop-color="#4E342E"/>
      <stop offset="1" stop-color="#21120E"/>
    </radialGradient>
    <clipPath id="tile-clip">
      <rect width="768" height="768" rx="144"/>
    </clipPath>
    <filter id="cup-shadow" x="-30%" y="-20%" width="160%" height="160%">
      <feGaussianBlur in="SourceAlpha" stdDeviation="22" result="blur-ambient"/>
      <feOffset in="blur-ambient" dx="0" dy="16" result="offset-ambient"/>
      <feComponentTransfer in="offset-ambient" result="ambient">
        <feFuncA type="linear" slope="0.3"/>
      </feComponentTransfer>
      <feGaussianBlur in="SourceAlpha" stdDeviation="6" result="blur-key"/>
      <feOffset in="blur-key" dx="0" dy="4" result="offset-key"/>
      <feComponentTransfer in="offset-key" result="key">
        <feFuncA type="linear" slope="0.5"/>
      </feComponentTransfer>
      <feMerge>
        <feMergeNode in="ambient"/>
        <feMergeNode in="key"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  <rect width="768" height="768" rx="144" fill="url(#tile)"/>
  <rect x="170" y="221" width="64" height="64" rx="32" fill="white"/>
  <rect x="298" y="157" width="64" height="128" rx="32" fill="white"/>
  <rect x="426" y="221" width="64" height="64" rx="32" fill="white"/>
  <g filter="url(#cup-shadow)">
    <path d="M 126 349 C 92 349 64 377 64 411 V 573 a 256 256 0 0 0 509 38 h 24 a 107 107 0 0 0 0 -214 h -22 c -6 -28 -31 -48 -60 -48 H 126 Z M 576 445 h 21 a 59 59 0 1 1 0 118 H 576 V 445 Z"
          fill="url(#cup)" fill-rule="evenodd"/>
  </g>
  <g clip-path="url(#tile-clip)">
    <rect x="0" y="362" width="351" height="341" rx="72" fill="url(#plate)"/>
    <g transform="translate(175.5 532.5) scale(1.58 -1.58) translate(-111.085 -347.215)" fill="white">
      <path d="M187.26 283.73 159.92 410.7l-32.69.02-16.14-76.19-16.9 76.19h-33L34.91 283.75h26.95l16.21 83.79 16.11-83.79h33.04l16.87 83.79 15.82-83.79 27.34-.02Z"/>
    </g>
  </g>
</svg>
```

### Tier 2, taskbar / Start small tile (24-32 px)

Ships as `Square44x44Logo.targetsize-{24,32}*` (all altforms) and `Square44x44Logo.scale-{100,125,150,200}`. Used both unplated (currentColor adapts to taskbar theme) and plated (rendered onto the warm tile by the build pipeline).

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"
     fill="none" stroke="currentColor" stroke-width="2"
     stroke-linecap="round" stroke-linejoin="round">
  <path d="M10 2v4"/>
  <path d="M14 4v2"/>
  <path d="M6 4v2"/>
  <path d="M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1"/>
</svg>
```

### Tier 1, tray (16-20 px)

Ships as `Square44x44Logo.targetsize-{16,20}*`. The audio bars are dropped, at 16 px they collapse to 1.3-px specks per the screen-15 audit, matching Microsoft's PowerToys Awake passive icon pattern (cup silhouette only).

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"
     fill="none" stroke="currentColor" stroke-width="2"
     stroke-linecap="round" stroke-linejoin="round">
  <path d="M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1"/>
</svg>
```

## Geometry reference

768 viewBox, 32 px per Lucide unit:

| Element | Coordinates |
|---|---|
| Tile | 0,0 → 768,768, rx=144 (= 18.75% of canvas, Microsoft Fluent 2 standard) |
| Audio bar, left | x=170, y=221, w=64, h=64, rx=32 |
| Audio bar, center | x=298, y=157, w=64, h=128, rx=32 |
| Audio bar, right | x=426, y=221, w=64, h=64, rx=32 |
| Cup body, rim line | y=349, x=126 → 480 (rim), curves to handle attach at x=515 |
| Cup body, left wall | x=64, y=411 → 573 |
| Cup body, bottom curve | radius 256, from (64,573) to (573,611) |
| Cup body, handle outer arc | radius 107, from (597,611) to (597,397) |
| Cup body, handle inner aperture | x=576-597, y=445-563, radius 59 |
| Cup gradient anchor | y1=349 → y2=611 (matches rim → bottom) |
| M plate | x=0, y=362, w=351, h=341, rx=72 (clipped to tile shape) |
| M letter | bbox x=55-296, y=432-633 (centered at 175.5, 532.5) |

Whole composition (audio bars + cup) is vertically centred, top padding 157, bottom padding 157.

## Palette

| Role | Stops |
|---|---|
| Tile (warm Adwaita-analog) | `#FFEDC8` → `#F5C375` → `#A85820` |
| Cup body | `#FFFFFF` → `#C8A878` |
| M plate (coffee gradient) | `#8B5E3C` → `#4E342E` → `#21120E` |
| Audio bars | `#FFFFFF` solid |
| M letter | `#FFFFFF` solid |

Contrast: M letter on plate is white-on-`#21120E` ≈ 17:1 (WCAG AAA). Cup on tile (cream on amber) ≈ 4.6:1 (WCAG AA for large text/decorative).

## Implementation plan (high-level)

Detailed plan deferred to the writing-plans phase. Sketch:

1. **Replace `Murmur/Assets/Source/AppIcon.svg`** and `AppIcon.mono.svg` with three new files:
   - `AppIcon.tier1.svg`, cup-only Lucide path, `currentColor`, 24×24
   - `AppIcon.tier2.svg`, cup + audio bars Lucide path, `currentColor`, 24×24
   - `AppIcon.tier3.svg`, Tier 3 launch icon (768×768, multi-gradient, M badge, Fluent 2 shadow)
2. **Extend `build-icons.ps1`** to route by output size:
   - 16, 20 px → Tier 1
   - 24, 32 px → Tier 2 (also drives `Square44x44Logo.scale-100/125/150/200`)
   - 48, 64, 96, 128, 192, 256 px → Tier 3 (also drives `Square150x150Logo.scale-*`, `SplashScreen.scale-*`, `StoreLogo.scale-*`, `.ico`)
3. **Verify SVG `<filter>` rendering** in ImageMagick 7+, the Fluent 2 shadow uses `feGaussianBlur` + `feOffset` + `feMerge`, all supported but worth testing the pipeline output at 256 px to confirm the shadow renders cleanly. Fallback if not: pre-rasterize a shadow PNG layer for Tier 3 only.
4. **Regenerate the asset matrix** via `build-icons.ps1`.
5. **Update `Source/README.md`** to describe the 3-tier system and the routing logic.
6. **Run `build-icons.ps1 -Verify`** in CI to catch SVG-edit-without-regenerate.

## Validation

- **Visual review at every output size**, render Tier 1/2/3 at 16, 20, 24, 32, 48, 64, 128, 192, 256 px and confirm:
  - Tier 1 cup silhouette is unambiguous at 16 px
  - Tier 2 audio bars are visible at 24 px
  - Tier 3 M badge and Fluent shadow render correctly at 64 px+
- **Theme adaptation**, confirm `currentColor` Tier 1/2 SVGs adapt correctly on light and dark Windows taskbars (check both `altform-unplated` and `altform-lightunplated` outputs).
- **Build verification**, `build-icons.ps1 -Verify` exits 0 after a fresh regenerate.
- **Manifest reference check**, confirm `Murmur/Package.appxmanifest` continues to reference the existing logo paths; no manifest edits required.

## Out of scope

- High-contrast mode assets, Windows 11 no longer requires them per the iconography docs.
- Linux / GNOME builds, Murmur is Windows-only; the Tier 3 SVG could in principle become a `.svg` on Linux, but no Linux packaging exists.
- Animated / Lottie variants, no use case in Murmur today.
- Apple / Android adaptations, out of scope for this project.

## Rollback

If the new design fails review or is rejected by a Microsoft Store certification step, revert by:

1. `git revert` the spec, the new `AppIcon.tier{1,2,3}.svg` files, the `build-icons.ps1` changes, and the regenerated asset matrix in `Murmur/Assets/`.
2. Verify the previous "M" letterform icon set re-emerges in the build.

The previous `AppIcon.svg` and `AppIcon.mono.svg` remain in git history and can be restored cleanly.

## Open questions for the implementation phase

- Exact ImageMagick command-line for rasterising Tier 3's `<filter>` correctly at all output sizes, if the default `magick -density 256` insufficient, may need `-resize` order tweaks or per-tier render strategy.
- Whether to keep `AppIcon.svg` and `AppIcon.mono.svg` filenames (replacing their content) or rename to the tiered convention. Filename change touches `build-icons.ps1`; content-only change is safer.
- Whether the M badge should appear in any plated 32-48 px context (Tier 2 plated, perhaps?), or only at Tier 3. Currently Tier 2 has no M; this could feel inconsistent at the 32→48 transition. Worth visual verification once the build pipeline produces real PNGs.
