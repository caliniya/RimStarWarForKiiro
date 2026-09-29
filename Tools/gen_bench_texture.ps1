Add-Type -AssemblyName System.Drawing
$OutDir = 'D:/project/RimKiior/Textures/Things/Building/Production'
New-Item $OutDir -ItemType Directory -Force | Out-Null

function Draw-BenchBody {
    param([System.Drawing.Graphics]$g, [int]$W, [int]$H)
    $body = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 60, 64, 72))
    $edge = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 90, 96, 108))
    $screen = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 40, 120, 140))
    $glow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 90, 220, 230))
    $gold = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 200, 90))
    $g.FillRectangle($body, 10, [int]($H * 0.6), $W - 20, [int]($H * 0.3))
    $g.FillRectangle($edge, 10, [int]($H * 0.6) - 4, $W - 20, 4)
    $g.FillRectangle($screen, 40, 12, 150, [int]($H * 0.35))
    $g.FillRectangle($glow, 48, 20, 100, 6)
    $g.FillRectangle($glow, 48, 34, 70, 6)
    $g.FillRectangle($gold, $W - 56, 18, 16, 40)
    $g.FillRectangle($gold, 12, [int]($H * 0.72), $W - 24, 4)
    $body.Dispose(); $edge.Dispose(); $screen.Dispose(); $glow.Dispose(); $gold.Dispose()
}

$W = 256; $H = 128
foreach ($suffix in @('_north', '_east', '_south', '_west')) {
    $bmp = [System.Drawing.Bitmap]::new($W, $H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    Draw-BenchBody $g $W $H
    if ($suffix -eq '_east')  { $bmp.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone) }
    if ($suffix -eq '_west')  { $bmp.RotateFlip([System.Drawing.RotateFlipType]::Rotate270FlipNone) }
    $g.Dispose()
    $bmp.Save((Join-Path $OutDir ('StarWarKiiro_AdvResearchBench' + $suffix + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ('ok ' + $suffix)
}
Write-Host 'bench textures done'
