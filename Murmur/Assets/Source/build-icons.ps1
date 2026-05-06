[CmdletBinding()]
param(
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ScriptDir = $PSScriptRoot
$AssetsDir = (Resolve-Path (Join-Path $ScriptDir '..')).Path
$SrcSvg    = Join-Path $ScriptDir 'AppIcon.svg'
$MonoSvg   = Join-Path $ScriptDir 'AppIcon.mono.svg'
$BgColor   = '#F4F1EA'
$FgColor   = '#2D2A6F'

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    $env:PATH = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
                [Environment]::GetEnvironmentVariable('Path', 'User')
}
if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    Write-Error "ImageMagick (magick.exe) not on PATH. Install: winget install ImageMagick.ImageMagick"
    exit 1
}

$ScaleAssets = @(
    @{ Name = 'Square44x44Logo';    W =  44; H =  44 },
    @{ Name = 'Square150x150Logo';  W = 150; H = 150 },
    @{ Name = 'Wide310x150Logo';    W = 310; H = 150 },
    @{ Name = 'StoreLogo';          W =  50; H =  50 },
    @{ Name = 'SplashScreen';       W = 620; H = 300 }
)
$Scales      = 100, 125, 150, 200, 400
$TargetSizes = 16, 20, 24, 32, 48, 256
$IcoSizes    = 16, 20, 24, 32, 48, 64, 128, 256

# Master raster size, must equal/exceed the largest output (SplashScreen.scale-400 is 2480w).
$MasterSize  = 2560

function Invoke-Magick {
    param([string[]]$ArgList)
    & magick @ArgList
    if ($LASTEXITCODE -ne 0) { throw "magick failed: $($ArgList -join ' ')" }
}

function New-MonoMaster {
    param([string]$Color, [string]$OutPath)
    $tempSvg = Join-Path $env:TEMP "murmur-mono-$([guid]::NewGuid().ToString('N')).svg"
    try {
        (Get-Content $MonoSvg -Raw).Replace('#000000', $Color) | Set-Content -Path $tempSvg -NoNewline
        # Mono SVG uses a 24-viewBox (Lucide grid). Density 7680 = 24 * 7680 / 72 ≈ 2560,
        # rendering at master size directly so we don't upscale 30× from a tiny initial raster.
        Invoke-Magick @('-background', 'none', '-density', '7680', $tempSvg,
                        '-resize', "${MasterSize}x${MasterSize}", $OutPath)
    } finally {
        Remove-Item $tempSvg -Force -ErrorAction SilentlyContinue
    }
}

function Resize-FromMaster {
    param([string]$Source, [int]$Width, [int]$Height, [string]$OutPath, [bool]$Pad = $true)
    if ($Width -eq $Height) {
        Invoke-Magick @($Source, '-resize', "${Width}x${Height}", $OutPath)
    } else {
        $iconSize = [math]::Min($Width, $Height)
        $bg = if ($Pad) { $BgColor } else { 'none' }
        Invoke-Magick @('-background', $bg, $Source,
                        '-resize', "${iconSize}x${iconSize}",
                        '-gravity', 'center', '-extent', "${Width}x${Height}", $OutPath)
    }
}

function Build-AllAssets {
    param([string]$OutDir)

    $tempDir = Join-Path $env:TEMP "murmur-build-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    try {
        $masterColored = Join-Path $tempDir 'master-colored.png'
        $masterMonoBg  = Join-Path $tempDir 'master-mono-bg.png'
        $masterMonoFg  = Join-Path $tempDir 'master-mono-fg.png'

        Write-Host "  Rendering master rasters at ${MasterSize}x${MasterSize}..."
        Invoke-Magick @('-background', 'none', '-density', '256', $SrcSvg,
                        '-resize', "${MasterSize}x${MasterSize}", $masterColored)
        New-MonoMaster -Color $BgColor -OutPath $masterMonoBg
        New-MonoMaster -Color $FgColor -OutPath $masterMonoFg

        foreach ($asset in $ScaleAssets) {
            foreach ($scale in $Scales) {
                $w = [int][math]::Round($asset.W * $scale / 100)
                $h = [int][math]::Round($asset.H * $scale / 100)
                $out = Join-Path $OutDir "$($asset.Name).scale-$scale.png"
                Write-Host ("  {0,-44} ({1}x{2})" -f "$($asset.Name).scale-$scale.png", $w, $h)
                Resize-FromMaster -Source $masterColored -Width $w -Height $h -OutPath $out
            }
        }

        foreach ($size in $TargetSizes) {
            $plated   = Join-Path $OutDir "Square44x44Logo.targetsize-$size.png"
            $unplated = Join-Path $OutDir "Square44x44Logo.targetsize-${size}_altform-unplated.png"
            $light    = Join-Path $OutDir "Square44x44Logo.targetsize-${size}_altform-lightunplated.png"
            Write-Host ("  Square44x44Logo.targetsize-$size  (plated + unplated + lightunplated, ${size}x${size})")
            Resize-FromMaster -Source $masterColored -Width $size -Height $size -OutPath $plated
            Resize-FromMaster -Source $masterMonoBg  -Width $size -Height $size -OutPath $unplated -Pad $false
            Resize-FromMaster -Source $masterMonoFg  -Width $size -Height $size -OutPath $light    -Pad $false
        }

        $icoSources = foreach ($size in $IcoSizes) {
            $tmp = Join-Path $tempDir "ico-$size.png"
            Resize-FromMaster -Source $masterColored -Width $size -Height $size -OutPath $tmp
            $tmp
        }
        $ico = Join-Path $OutDir 'AppIcon.ico'
        Write-Host "  AppIcon.ico  (multi-res $($IcoSizes -join '/'))"
        Invoke-Magick (@($icoSources) + @($ico))
    } finally {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Verify) {
    $tempOut = Join-Path $env:TEMP "murmur-verify-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force -Path $tempOut | Out-Null
    try {
        Write-Host "Rendering to temp dir for verification..."
        Build-AllAssets -OutDir $tempOut

        $diffs = @()
        foreach ($file in Get-ChildItem $tempOut -File) {
            $committed = Join-Path $AssetsDir $file.Name
            if (-not (Test-Path $committed)) {
                $diffs += "MISSING: $($file.Name) is generated but not committed."
                continue
            }
            $newHash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
            $oldHash = (Get-FileHash $committed     -Algorithm SHA256).Hash
            if ($newHash -ne $oldHash) {
                $diffs += "CHANGED: $($file.Name)"
            }
        }
        if ($diffs.Count -gt 0) {
            Write-Host ""
            Write-Host "Verification failed:" -ForegroundColor Red
            $diffs | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
            Write-Host ""
            Write-Host "Re-run without -Verify to regenerate assets, then commit." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "Verification passed: all generated assets match committed files." -ForegroundColor Green
    } finally {
        Remove-Item $tempOut -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "Rendering all icon assets to: $AssetsDir" -ForegroundColor Cyan
    Write-Host ""
    Build-AllAssets -OutDir $AssetsDir
    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
}
