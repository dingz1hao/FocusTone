# Recreate the repository-owned waveform icon; no third-party artwork.
param([string]$Output = (Join-Path $PSScriptRoot '../src/FocusTone/Assets/FocusTone.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(256,256)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$background = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#151c2b'))
$accent = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#b4a2ff'))
$outline = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#a18aff'),10)
try {
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.FillEllipse($background, 5, 5, 246, 246)
    $graphics.DrawEllipse($outline,31,31,194,194)
    $bars = @(@(57,109,38),@(88,80,96),@(119,58,140),@(150,91,74),@(181,113,30))
    foreach($bar in $bars) {
        $graphics.FillRectangle($accent,$bar[0],$bar[1]+9,18,$bar[2]-18)
        $graphics.FillEllipse($accent,$bar[0],$bar[1],18,18)
        $graphics.FillEllipse($accent,$bar[0],$bar[1]+$bar[2]-18,18,18)
    }
    $png = [IO.MemoryStream]::new()
    $bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
    $stream = [IO.File]::Create([IO.Path]::GetFullPath($Output))
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
        $writer.Write($png.ToArray())
    } finally { $writer.Dispose(); $png.Dispose() }
} finally { $graphics.Dispose(); $bitmap.Dispose(); $background.Dispose(); $accent.Dispose(); $outline.Dispose() }
