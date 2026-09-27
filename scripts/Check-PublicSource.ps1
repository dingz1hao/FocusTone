$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$code = Get-ChildItem (Join-Path $root 'src') -Recurse -File -Include '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
$matches = $code | Select-String -Pattern 'CacheAudioAsync|__playinfo__|x/player/playurl|x/player/wbi|SESSDATA|SetWindowsHookEx|WriteProcessMemory|ReadProcessMemory|CreateRemoteThread'
if ($matches) { throw 'Public-source policy check failed. Review platform extraction, credentials or process injection code.' }
Write-Output 'Public-source policy check passed.'
