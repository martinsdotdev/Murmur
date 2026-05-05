# Murmur website

The marketing site at `martinsdotdev.github.io/Murmur`. Astro static build, deployed by GitHub Actions.

## Develop

```powershell
cd website
npm install
npm run dev      # → http://localhost:4321/Murmur
```

`npm run dev` automatically copies the app icon and the 14 bundled OGGs from `../Murmur/Assets/Source/AppIcon.svg` and `../Murmur/Sounds/*.ogg` into `public/` so the demo can play them locally.

## Build

```powershell
npm run build    # → dist/
npm run preview  # serves dist/ at localhost:4321
```

## Deploy

Pushed to `master`, the `.github/workflows/website.yml` workflow builds and deploys to GitHub Pages. The repo's *Settings → Pages → Source* must be set to *GitHub Actions* (one-time manual toggle).

## Source-of-truth links

| Site asset | Lives in app at | Sync |
|---|---|---|
| Brand SVG | `Murmur/Assets/Source/AppIcon.svg` | copied at build |
| Soundscape OGGs | `Murmur/Sounds/*.ogg` | copied at build |
| Sound list & lucide icons | `Murmur/Models/SoundCatalog.cs` + `Murmur/ViewModels/SoundCardViewModel.cs` | hand-mirrored to `src/data/sounds.ts` |
| License attribution | `Murmur/Sounds/SOUNDS_LICENSING.md` | hand-mirrored to `src/data/sounds.ts` |

Keep the mirrors in sync when those source files change.
