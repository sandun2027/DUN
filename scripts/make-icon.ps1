# Draws the app icon (a clothesline with a photo and a note on a blue tile) and writes assets/DUN.ico.
# The icon is generated in code so it can be tweaked without an image editor.
param([string]$Out = (Join-Path $PSScriptRoot '..\assets\DUN.ico'))

Add-Type -AssemblyName System.Drawing

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Card($g, [float]$cx, [float]$top, [float]$angle, $paper, $ink, [bool]$note) {
    $state = $g.Save()
    $g.TranslateTransform($cx, $top)
    $g.RotateTransform($angle)
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(60, 0, 0, 0))
    $g.FillPath($shadow, (New-RoundRect -8.5 3.5 17 21 3))
    $g.FillPath((New-Object System.Drawing.SolidBrush $paper), (New-RoundRect -9 2 18 21 3))
    if ($note) {
        $pen = New-Object System.Drawing.Pen $ink, 1.6
        foreach ($ly in 8, 12, 16) { $g.DrawLine($pen, -5.5, $ly, $(if ($ly -eq 16) { 1.5 } else { 5.5 }), $ly) }
    } else {
        $g.FillPath((New-Object System.Drawing.SolidBrush $ink), (New-RoundRect -6.5 4.5 13 13 2))
    }
    $peg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0xC9, 0xA2, 0x72))
    $g.FillPath($peg, (New-RoundRect -2 -3 4 9 1.5))
    $g.Restore($state)
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 64.0, $size / 64.0)

    $tile = New-RoundRect 2 2 60 60 14
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, 64),
        ([System.Drawing.Color]::FromArgb(0x5B, 0x9B, 0xFA)), ([System.Drawing.Color]::FromArgb(0x25, 0x56, 0xD4))
    $g.FillPath($bg, $tile)

    $rope = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 255, 255, 255)), 2.6
    $g.DrawBezier($rope, 6, 17, 22, 25, 42, 25, 58, 17)
    Draw-Card $g 22 21 -8 ([System.Drawing.Color]::White) ([System.Drawing.Color]::FromArgb(0x60, 0xA5, 0xFA)) $false
    Draw-Card $g 42 21 7 ([System.Drawing.Color]::FromArgb(0xFE, 0xF3, 0xC7)) ([System.Drawing.Color]::FromArgb(0xB4, 0x9A, 0x5E)) $true
    $g.Dispose()
    return $bmp
}

# ICO with PNG-compressed entries (supported since Windows Vista).
$sizes = 256, 64, 48, 32, 24, 16
$pngs = foreach ($s in $sizes) {
    $ms = New-Object System.IO.MemoryStream
    (Draw-Icon $s).Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
$w.Close()
Write-Host "Wrote $Out"
