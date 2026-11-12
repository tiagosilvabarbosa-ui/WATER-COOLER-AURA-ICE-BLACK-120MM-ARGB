# Gera assets\icon.ico (hexágono neon com "R") em 16/32/48/256 px.
Add-Type -AssemblyName System.Drawing
$sizes = 16, 32, 48, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $pts = for ($i = 0; $i -lt 6; $i++) {
        $a = [Math]::PI / 3 * $i - [Math]::PI / 2
        New-Object Drawing.PointF ([float]($s / 2 + [Math]::Cos($a) * $s * 0.47)), ([float]($s / 2 + [Math]::Sin($a) * $s * 0.47))
    }
    $rect = New-Object Drawing.RectangleF 0, 0, $s, $s
    $br = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(255, 36, 66)), ([Drawing.Color]::FromArgb(255, 112, 40)), 45
    $g.FillPolygon($br, $pts)
    $font = New-Object Drawing.Font 'Bahnschrift', ([float]($s * 0.40)), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString('R', $font, [Drawing.Brushes]::White, $rect, $sf)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$d.Length); $w.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $pngs) { $w.Write($d) }
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'icon.ico'), $out.ToArray())
Write-Host 'icon.ico gerado'
