Add-Type -AssemblyName System.Drawing
$OutDir = 'D:/project/KiiroStarWARForRim/Textures/Things/Item/Resource'
New-Item $OutDir -ItemType Directory -Force | Out-Null

# name -> base color, highlight color, shape (ingot / canister / crate / chip / missile)
$defs = [ordered]@{
  'StarWarKiiro_Tungsten'                 = @('70,74,82',   '130,136,148', 'ingot')
  'StarWarKiiro_Iridium'                  = @('180,200,215','230,240,250',  'ingot')
  'StarWarKiiro_UraniumAlloy'             = @('96,140,80',  '150,200,120',  'ingot')
  'StarWarKiiro_HighExplosive'            = @('160,60,50',  '255,120,90',   'crate')
  'StarWarKiiro_SolidifiedHelium'         = @('120,170,220','200,230,255',  'canister')
  'StarWarKiiro_SolidifiedHydrogen'       = @('80,120,190', '170,210,255',  'canister')
  'StarWarKiiro_AdvancedMicroelectronics' = @('40,90,60',   '120,255,160',  'chip')
  'StarWarKiiro_ShipMissile'              = @('90,90,95',   '255,200,90',   'missile')
  'StarWarKiiro_GlitterworldWreckage'     = @('150,120,180','240,220,255',  'crate')
}

foreach ($k in $defs.Keys) {
  $c = [int[]]($defs[$k][0] -split ',')
  $h = [int[]]($defs[$k][1] -split ',')
  $shape = $defs[$k][2]
  $bmp = [System.Drawing.Bitmap]::new(64, 64)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)
  $body = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, $c[0], $c[1], $c[2]))
  $hi   = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, $h[0], $h[1], $h[2]))
  $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 30, 30, 34))
  switch ($shape) {
    'ingot'    { $g.FillRectangle($body, 8, 36, 48, 20); $g.FillRectangle($hi, 8, 36, 48, 5) }
    'canister' { $g.FillRectangle($body, 20, 12, 24, 44); $g.FillRectangle($dark, 20, 12, 24, 8); $g.FillRectangle($hi, 24, 24, 16, 10) }
    'crate'    { $g.FillRectangle($body, 8, 16, 48, 40); $g.FillRectangle($dark, 8, 30, 48, 6); $g.FillRectangle($hi, 8, 16, 48, 5) }
    'chip'     { $g.FillRectangle($body, 14, 14, 36, 36); $g.FillRectangle($hi, 20, 20, 24, 24) }
    'missile'  {
      $g.FillPolygon($body, @(
        [System.Drawing.PointF]::new(32, 2),
        [System.Drawing.PointF]::new(24, 18),
        [System.Drawing.PointF]::new(24, 52),
        [System.Drawing.PointF]::new(40, 52),
        [System.Drawing.PointF]::new(40, 18)))
      $g.FillPolygon($hi, @(
        [System.Drawing.PointF]::new(32, 2),
        [System.Drawing.PointF]::new(27, 14),
        [System.Drawing.PointF]::new(37, 14)))
      $g.FillRectangle($dark, 20, 52, 24, 8)
    }
  }
  $g.Dispose()
  $bmp.Save((Join-Path $OutDir ($k + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host ('ok ' + $k)
}
Write-Host 'all textures done'
