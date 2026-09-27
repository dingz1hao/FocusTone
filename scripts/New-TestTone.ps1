param([string]$Output = "$PSScriptRoot/../artifacts/FocusTone-test.wav")
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
$rate = 44100
$seconds = 12
$samples = $rate * $seconds
$stream = [IO.File]::Create($target)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $samples * 2))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
    $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]$rate); $writer.Write([int]($rate * 2))
    $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($samples * 2))
    $notes = @(220, 261.63, 329.63, 440, 392, 329.63, 261.63, 293.66)
    for ($i=0; $i -lt $samples; $i++) {
        $time = $i / $rate
        $beat = $time % 0.5
        $envelope = [Math]::Min(1, $beat * 50) * [Math]::Exp(-5 * $beat)
        $frequency = $notes[[int][Math]::Floor($time * 2) % $notes.Length]
        $value = [Math]::Sin(2 * [Math]::PI * $frequency * $time) * $envelope * 0.14
        $writer.Write([int16]($value * 32767))
    }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output $target
