Add-Type -AssemblyName System.Drawing
# 把 4 帧横排的研究台贴图拆成 Graphic_Multi 需要的 4 个方向文件
$src = 'D:/project/RimKiior/Textures/Things/Building/Production/StarWarKiiro_AdvResearchBench.png'
$dstDir = 'D:/project/RimKiior/Textures/Things/Building/Production'
$suffixes = @('_north', '_east', '_south', '_west')

$atlas = [System.Drawing.Bitmap]([System.Drawing.Image]::FromFile($src))
$frameW = [int]($atlas.Width / 4)
$frameH = $atlas.Height
for ($i = 0; $i -lt 4; $i++) {
    $frame = $atlas.Clone([System.Drawing.Rectangle]::new($i * $frameW, 0, $frameW, $frameH), $atlas.PixelFormat)
    $out = Join-Path $dstDir ("StarWarKiiro_AdvResearchBench" + $suffixes[$i] + ".png")
    $frame.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $frame.Dispose()
    Write-Host ('ok ' + $suffixes[$i])
}
$atlas.Dispose()
Write-Host 'split done'
