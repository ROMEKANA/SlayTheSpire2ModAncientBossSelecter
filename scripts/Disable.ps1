[CmdletBinding()]
param([string]$PathsFile)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'Close STS2 before disabling SelectMOD.' }
$paths = Get-SelectModPaths -PathsFile $PathsFile
$source = Join-Path $paths.Game 'mods/SelectMOD'
if (!(Test-Path -LiteralPath $source -PathType Container)) { Write-Host 'SelectMOD is not installed.'; return }
# Refuse an unsafe rollback location rather than moving a MOD within the scanned tree.
$mods = [IO.Path]::GetFullPath((Join-Path $paths.Game 'mods')).TrimEnd([IO.Path]::DirectorySeparatorChar)
$config = $paths.Config.TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($config.Equals($mods, [StringComparison]::OrdinalIgnoreCase) -or $config.StartsWith($mods + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Config directory must be outside mods.' }
New-Item -ItemType Directory -Path $config -Force | Out-Null
$guard = [IO.File]::Open((Join-Path $config 'setup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    Move-Item -LiteralPath $source -Destination (Join-Path $config ('disabled-mod-' + [guid]::NewGuid().ToString('N')))
    Write-Host 'SelectMOD moved outside mods. Saves/config are unchanged. Saved runs containing SelectMOD modifiers still require SelectMOD to resume.'
} finally { $guard.Dispose() }
