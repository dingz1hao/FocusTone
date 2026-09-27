param([string]$Runtime = 'win-x64', [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outDir = Join-Path $root "artifacts/FocusTone-$Runtime"
$licenses = Join-Path $outDir 'licenses'
$sources = Join-Path $outDir 'third-party-source'
& $Dotnet test (Join-Path $root 'FocusTone.sln') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; release cancelled.' }
& $Dotnet publish (Join-Path $root 'src/FocusTone/FocusTone.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -o $outDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
[IO.Directory]::CreateDirectory($licenses) | Out-Null
[IO.Directory]::CreateDirectory($sources) | Out-Null
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'README.md'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $outDir
Copy-Item (Join-Path $root 'SECURITY.md') -Destination $outDir
Copy-Item (Join-Path $root 'docs') -Destination $outDir -Recurse -Force
$downloads = @{
    'NAudio-MIT.txt' = 'https://raw.githubusercontent.com/naudio/NAudio/v2.2.1/license.txt'
    'NAudio-Vorbis-MIT.txt' = 'https://raw.githubusercontent.com/naudio/Vorbis/master/LICENSE'
    'NVorbis-MIT.txt' = 'https://raw.githubusercontent.com/NVorbis/NVorbis/master/LICENSE'
    'TagLibSharp-LGPL-2.1.txt' = 'https://raw.githubusercontent.com/mono/taglib-sharp/TaglibSharp-2.3.0.0/COPYING'
}
foreach ($entry in $downloads.GetEnumerator()) { Invoke-WebRequest -Uri $entry.Value -OutFile (Join-Path $licenses $entry.Key) }
$packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
$webview = Join-Path $packageRoot 'microsoft.web.webview2/1.0.4191.47'
Copy-Item (Join-Path $webview 'LICENSE.txt') (Join-Path $licenses 'WebView2-LICENSE.txt')
Copy-Item (Join-Path $webview 'NOTICE.txt') (Join-Path $licenses 'WebView2-NOTICE.txt')
Invoke-WebRequest 'https://github.com/mono/taglib-sharp/archive/refs/tags/TaglibSharp-2.3.0.0.zip' -OutFile (Join-Path $sources 'TaglibSharp-2.3.0.0.zip')
$archive = Join-Path $root "artifacts/FocusTone-$Runtime.zip"
Compress-Archive -Path "$outDir/*" -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Output "Published: $archive"
