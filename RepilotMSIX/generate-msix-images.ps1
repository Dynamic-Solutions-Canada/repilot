# Generates the MSIX visual assets, the app .ico and its 256 px PNG from one source: a dark keycap
# bearing a white "R", on a light rounded tile.
# Run with Windows PowerShell: powershell.exe -NoProfile -File generate-msix-images.ps1
# ASCII only (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI).
Add-Type -AssemblyName System.Drawing

$repoRoot  = Split-Path $PSScriptRoot -Parent
$imagesDir = Join-Path $PSScriptRoot "Images"
$icoPath   = Join-Path $repoRoot "Repilot\Resources\Repilot.ico"
# The 256 px PNG the settings shell and the About page show (they take a PNG, not an .ico).
$pngPath   = Join-Path $repoRoot "Repilot\Resources\Repilot.png"
New-Item -ItemType Directory -Force $imagesDir | Out-Null

$tile  = [System.Drawing.Color]::FromArgb(255, 233, 240, 250)  # E9F0FA  light tile
$face  = [System.Drawing.Color]::FromArgb(255, 45, 50, 59)     # 2D323B  keycap face
$base  = [System.Drawing.Color]::FromArgb(255, 32, 36, 43)     # 20242B  keycap base (3D side)
$edge  = [System.Drawing.Color]::FromArgb(255, 58, 64, 73)     # 3A4049  keycap edge
$white = [System.Drawing.Color]::White

function Rounded($x, $y, $w, $h, $r) {
    $d = [single]($r * 2)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc([single]$x, [single]$y, $d, $d, 180, 90)
    $p.AddArc([single]($x + $w - $d), [single]$y, $d, $d, 270, 90)
    $p.AddArc([single]($x + $w - $d), [single]($y + $h - $d), $d, $d, 0, 90)
    $p.AddArc([single]$x, [single]($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Draws the dark keycap bearing a white "R", centered in a square of side S at (ox, oy).
function Draw-Key($g, $S, $ox, $oy) {
    $kc  = [single]($S * 0.64)
    $kx  = [single]($ox + ($S - $kc) / 2)
    $ky  = [single]($oy + ($S - $kc) / 2 - $S * 0.015)
    $r   = [single]($kc * 0.2)
    $lip = [single]($kc * 0.075)

    $bp = Rounded $kx ($ky + $lip) $kc $kc $r
    $bb = New-Object System.Drawing.SolidBrush($base)
    $g.FillPath($bb, $bp); $bb.Dispose(); $bp.Dispose()

    $fp = Rounded $kx $ky $kc $kc $r
    $fb = New-Object System.Drawing.SolidBrush($face)
    $g.FillPath($fb, $fp); $fb.Dispose()
    $pen = New-Object System.Drawing.Pen($edge, [single][Math]::Max(1.0, $kc * 0.02))
    $g.DrawPath($pen, $fp); $pen.Dispose(); $fp.Dispose()

    $font = New-Object System.Drawing.Font("Segoe UI", [single]($kc * 0.62), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $wb = New-Object System.Drawing.SolidBrush($white)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $tr = New-Object System.Drawing.RectangleF($kx, [single]($ky - $kc * 0.04), $kc, $kc)
    $g.DrawString("R", $font, $wb, $tr, $fmt)
    $font.Dispose(); $wb.Dispose()
}

# Render the whole icon (rounded tile + keycap) at side M.
function New-Master([int]$M) {
    $bmp = New-Object System.Drawing.Bitmap($M, $M, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $tp = Rounded 0 0 $M $M ([single]($M * 0.18))
    $tb = New-Object System.Drawing.SolidBrush($tile)
    $g.FillPath($tb, $tp); $tb.Dispose(); $tp.Dispose()
    Draw-Key $g $M 0 0
    $g.Dispose()
    return $bmp
}

function New-Resized($src, [int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    return $bmp
}

# Render each asset at its OWN size with 4x supersampling (render large, shrink once).
# Drawing tiny bitmaps directly aliases the keycap edges and the "R"; supersampling keeps
# small frames (16/24/44 px) crisp.
$SS = 4
function New-Crisp([int]$size) {
    $m = New-Master ($size * $SS)
    $r = New-Resized $m $size $size
    $m.Dispose()
    return $r
}

function Save-Square([int]$size, [string]$name) {
    $b = New-Crisp $size
    $b.Save((Join-Path $imagesDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    Write-Output "  $name ($size x $size)"
}

function Save-Splash([int]$w, [int]$h, [string]$name) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $badge = [int]($h * 0.5); $bx = [int](($w - $badge) / 2); $by = [int](($h - $badge) / 2)
    $bb = New-Crisp $badge
    $g.DrawImage($bb, $bx, $by, $badge, $badge)
    $bb.Dispose(); $g.Dispose()
    $bmp.Save((Join-Path $imagesDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "  $name ($w x $h)"
}

function Save-Ico([int[]]$sizes, [string]$path) {
    $blobs = @()
    foreach ($s in $sizes) {
        $b = New-Crisp $s
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $blobs += , ($ms.ToArray())
        $ms.Dispose(); $b.Dispose()
    }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $len = $blobs[$i].Length
        $wb = if ($s -ge 256) { 0 } else { $s }
        $bw.Write([byte]$wb); $bw.Write([byte]$wb); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
        $offset += $len
    }
    foreach ($b in $blobs) { $bw.Write($b) }
    $bw.Flush(); $bw.Close(); $fs.Close()
    Write-Output "  Repilot.ico ($($sizes -join ', '))"
}

Write-Output "Generating MSIX images:"
Save-Square 50  "StoreLogo.png"
Save-Square 44  "Square44x44Logo.png"
Save-Square 88  "Square44x44Logo.scale-200.png"
Save-Square 24  "Square44x44Logo.targetsize-24_altform-unplated.png"
Save-Square 150 "Square150x150Logo.png"
Save-Square 300 "Square150x150Logo.scale-200.png"
Save-Splash 620 300  "SplashScreen.png"
Save-Splash 1240 600 "SplashScreen.scale-200.png"

Write-Output "Generating app icon:"
Save-Ico @(16, 20, 24, 32, 40, 48, 64, 128, 256) $icoPath
$appPng = New-Crisp 256
$appPng.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$appPng.Dispose()
Write-Output "  Repilot.png (256 x 256)"

Write-Output "Done."
