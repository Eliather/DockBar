param(
    [string]$SourceIcon = "Dock.png",
    [string]$OutputDir = "Package\Assets"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $SourceIcon)) {
    throw "No se encontro la imagen fuente: $SourceIcon"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$sourceImg = [System.Drawing.Image]::FromFile((Resolve-Path $SourceIcon))

function Resize-Png {
    param(
        [string]$FileName,
        [int]$TargetWidth,
        [int]$TargetHeight,
        [int]$IconDrawSize,
        [System.Drawing.Color]$BgColor
    )

    $destBitmap = New-Object System.Drawing.Bitmap($TargetWidth, $TargetHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($destBitmap)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    if ($BgColor -ne [System.Drawing.Color]::Transparent) {
        $brush = New-Object System.Drawing.SolidBrush($BgColor)
        $g.FillRectangle($brush, 0, 0, $TargetWidth, $TargetHeight)
        $brush.Dispose()
    } else {
        $g.Clear([System.Drawing.Color]::Transparent)
    }

    $x = [int](($TargetWidth - $IconDrawSize) / 2)
    $y = [int](($TargetHeight - $IconDrawSize) / 2)
    $g.DrawImage($sourceImg, $x, $y, $IconDrawSize, $IconDrawSize)
    $g.Dispose()

    $destPath = Join-Path $OutputDir $FileName
    $destBitmap.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $destBitmap.Dispose()
    Write-Host "Generado: $destPath ($TargetWidth x $TargetHeight)"
}

try {
    # Square 44x44
    Resize-Png -FileName "Square44x44Logo.png" -TargetWidth 44 -TargetHeight 44 -IconDrawSize 36 -BgColor ([System.Drawing.Color]::Transparent)
    Resize-Png -FileName "Square44x44Logo.targetsize-44_altform-unplated.png" -TargetWidth 44 -TargetHeight 44 -IconDrawSize 36 -BgColor ([System.Drawing.Color]::Transparent)
    Resize-Png -FileName "Square44x44Logo.scale-200.png" -TargetWidth 88 -TargetHeight 88 -IconDrawSize 72 -BgColor ([System.Drawing.Color]::Transparent)

    # Square 150x150
    Resize-Png -FileName "Square150x150Logo.png" -TargetWidth 150 -TargetHeight 150 -IconDrawSize 120 -BgColor ([System.Drawing.Color]::Transparent)
    Resize-Png -FileName "Square150x150Logo.scale-200.png" -TargetWidth 300 -TargetHeight 300 -IconDrawSize 240 -BgColor ([System.Drawing.Color]::Transparent)

    # Wide 310x150
    Resize-Png -FileName "Wide310x150Logo.png" -TargetWidth 310 -TargetHeight 150 -IconDrawSize 120 -BgColor ([System.Drawing.Color]::Transparent)
    Resize-Png -FileName "Wide310x150Logo.scale-200.png" -TargetWidth 620 -TargetHeight 300 -IconDrawSize 240 -BgColor ([System.Drawing.Color]::Transparent)

    # Store Logo (50x50)
    Resize-Png -FileName "StoreLogo.png" -TargetWidth 50 -TargetHeight 50 -IconDrawSize 42 -BgColor ([System.Drawing.Color]::Transparent)
    Resize-Png -FileName "StoreLogo.scale-200.png" -TargetWidth 100 -TargetHeight 100 -IconDrawSize 84 -BgColor ([System.Drawing.Color]::Transparent)

    # Splash Screen
    Resize-Png -FileName "SplashScreen.png" -TargetWidth 620 -TargetHeight 300 -IconDrawSize 160 -BgColor ([System.Drawing.Color]::FromArgb(255, 16, 16, 16))
    Resize-Png -FileName "SplashScreen.scale-200.png" -TargetWidth 1240 -TargetHeight 600 -IconDrawSize 320 -BgColor ([System.Drawing.Color]::FromArgb(255, 16, 16, 16))

    Write-Host "Assets de MSIX generados exitosamente en $OutputDir"
}
finally {
    $sourceImg.Dispose()
}
