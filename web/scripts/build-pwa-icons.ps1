#requires -Version 7
<#
.SYNOPSIS
    Renders the PWA icon set into web/public/icons/ from the two source SVGs.

.DESCRIPTION
    Run by hand after changing AppIcon.svg or AppIcon.maskable.svg, then commit
    the PNGs. Deliberately NOT chained into `pnpm dev` / `pnpm build`:
    ImageMagick is not a project dependency and CI has no reason to need one.

    Kept separate from Murmur/Assets/Source/build-icons.ps1 on purpose. That
    script writes under Murmur/Assets/, and the website deploy workflow's
    `paths:` filter watches only two specific files there, so icons generated
    alongside the MSIX assets would silently never ship. Everything under
    web/** is already watched.

.PARAMETER Verify
    Render to a temp directory and compare hashes against the committed files
    instead of overwriting them. Mirrors build-icons.ps1's -Verify.
#>
[CmdletBinding()]
param([switch]$Verify)

$ErrorActionPreference = 'Stop'

$SourceDir = Join-Path $PSScriptRoot '..\..\Murmur\Assets\Source'
$ColoredSvg = Join-Path $SourceDir 'AppIcon.svg'
$MaskableSvg = Join-Path $SourceDir 'AppIcon.maskable.svg'
$OutDir = Join-Path $PSScriptRoot '..\public\icons'

# Both sources are 768 units square. 768 * 256 / 72 = 2730, so every output is
# downsampled from a raster comfortably larger than it, matching how
# build-icons.ps1 renders its masters.
$Density = 256

# icon-* are the manifest's "any" purpose and the Media Session artwork, so
# they keep the rounded tile. maskable-* are full-bleed, because a launcher
# mask wider than the tile's rounding would otherwise expose the corners.
# apple-touch is maskable too: iOS applies its own rounding and composites a
# transparent icon onto black.
$Targets = @(
    @{ Src = $ColoredSvg;  Size = 192; Name = 'icon-192.png';         Opaque = $false }
    @{ Src = $ColoredSvg;  Size = 512; Name = 'icon-512.png';         Opaque = $false }
    @{ Src = $MaskableSvg; Size = 192; Name = 'maskable-192.png';     Opaque = $true }
    @{ Src = $MaskableSvg; Size = 512; Name = 'maskable-512.png';     Opaque = $true }
    @{ Src = $MaskableSvg; Size = 180; Name = 'apple-touch-icon.png'; Opaque = $true }
)

function Invoke-Magick {
    param([string[]]$ArgList)
    # Without this every PNG carries a creation date, so two renders of the
    # same SVG differ and -Verify can never pass. Same guard build-icons.ps1
    # uses, and for the same reason.
    & magick '-define' 'png:exclude-chunk=date,time' @ArgList
    if ($LASTEXITCODE -ne 0) { throw "magick failed: $($ArgList -join ' ')" }
}

function Build-Icons {
    param([string]$Destination)
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    foreach ($target in $Targets) {
        $out = Join-Path $Destination $target.Name
        # -depth 8 is the difference between a 79 KB icon and a 674 KB one:
        # rsvg hands back 16 bits per channel, which a gradient this smooth
        # compresses badly and no display can show. Measured RMSE against the
        # 16-bit render is 0.0005, versus 0.0042 for a 256-colour quantise that
        # would also band the gradient.
        $args = @('-background', 'none', '-density', $Density, $target.Src,
                  '-resize', "$($target.Size)x$($target.Size)", '-depth', '8')
        # The maskable and apple-touch renders are full-bleed by construction,
        # so their alpha channel is a wasted plane.
        if ($target.Opaque) { $args += @('-alpha', 'off') }
        Invoke-Magick ($args + @('-strip', $out))
    }
}

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw 'ImageMagick (magick) is not on PATH.'
}
foreach ($svg in @($ColoredSvg, $MaskableSvg)) {
    if (-not (Test-Path $svg)) { throw "Missing source: $svg" }
}

if ($Verify) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) "murmur-pwa-icons-$([guid]::NewGuid().ToString('N'))"
    try {
        Build-Icons -Destination $temp
        $failed = $false
        foreach ($target in $Targets) {
            $committed = Join-Path $OutDir $target.Name
            if (-not (Test-Path $committed)) {
                Write-Host "MISSING  $($target.Name)"; $failed = $true; continue
            }
            $a = (Get-FileHash $committed -Algorithm SHA256).Hash
            $b = (Get-FileHash (Join-Path $temp $target.Name) -Algorithm SHA256).Hash
            if ($a -eq $b) { Write-Host "ok       $($target.Name)" }
            else { Write-Host "STALE    $($target.Name)"; $failed = $true }
        }
        if ($failed) { throw 'Committed icons do not match the sources.' }
        Write-Host 'All PWA icons match their sources.'
    } finally {
        Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Build-Icons -Destination $OutDir
    Get-ChildItem $OutDir -Filter *.png |
        Select-Object Name, @{ n = 'Bytes'; e = { $_.Length } } |
        Format-Table -AutoSize
}
