Set-StrictMode -Version Latest

function ConvertTo-SelectModIoPath {
    param([Parameter(Mandatory)][string]$Path)
    # Explicit extended paths avoid legacy MAX_PATH handling in Windows PowerShell's
    # .NET Framework file reads. Keep display/relative paths in their original form.
    if ($Path.StartsWith('\\?\')) { return $Path }
    if ($Path -match '^[A-Za-z]:\\') { return '\\?\' + $Path }
    if ($Path.StartsWith('\\')) { return '\\?\UNC\' + $Path.Substring(2) }
    return $Path
}

function Get-SelectModPaths {
    param([string]$PathsFile)
    $repo = Split-Path $PSScriptRoot -Parent
    if (!$PathsFile) { $PathsFile = Join-Path $repo 'local/paths.txt' }
    $PathsFile = [IO.Path]::GetFullPath($PathsFile)
    if (!(Test-Path -LiteralPath $PathsFile -PathType Leaf)) {
        if (!${env:ProgramFiles(x86)} -or !$env:APPDATA -or !$env:LOCALAPPDATA) {
            throw 'Automatic defaults require Windows. Supply -PathsFile with three non-empty absolute paths.'
        }
        New-Item -ItemType Directory -Path (Split-Path $PathsFile -Parent) -Force | Out-Null
        $defaults = [string[]]@(
            (Join-Path ${env:ProgramFiles(x86)} 'Steam/steamapps/common/Slay the Spire 2'),
            (Join-Path $env:APPDATA 'SlayTheSpire2'),
            (Join-Path $env:LOCALAPPDATA 'SelectMOD')
        )
        [IO.File]::WriteAllLines($PathsFile, $defaults, [Text.UTF8Encoding]::new($false))
        Write-Host "Created paths file: $PathsFile"
    }
    $lines = [IO.File]::ReadAllLines($PathsFile)
    if ($lines.Count -ne 3 -or @($lines | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count) {
        throw "Expected exactly three non-empty lines: game root, save root, config directory. Check $PathsFile"
    }
    $resolved = @($lines | ForEach-Object {
        $expanded = [Environment]::ExpandEnvironmentVariables($_.Trim())
        if (![IO.Path]::IsPathRooted($expanded)) { throw 'Paths must be absolute.' }
        [IO.Path]::GetFullPath($expanded)
    })
    # Diagnostics deliberately do not open or create the save/config directories.
    [pscustomobject]@{ Game = $resolved[0]; Save = $resolved[1]; Config = $resolved[2]; File = $PathsFile }
}

function Get-SelectModGameAssembly {
    param([Parameter(Mandatory)][string]$Game)
    if (!(Test-Path -LiteralPath $Game -PathType Container)) {
        throw "Game root not found: $Game. Edit line 1 of local/paths.txt (or your -PathsFile)."
    }
    $preferred = Join-Path $Game 'data_sts2_windows_x86_64/sts2.dll'
    if (Test-Path -LiteralPath $preferred -PathType Leaf) { return $preferred }
    # Avoid recursively scanning mods, saves or stale backup DLLs.
    $candidates = @()
    $direct = Join-Path $Game 'sts2.dll'
    if (Test-Path -LiteralPath $direct -PathType Leaf) { $candidates += $direct }
    foreach ($dir in @(Get-ChildItem -LiteralPath $Game -Directory -Filter 'data_sts2_*')) {
        $candidate = Join-Path $dir.FullName 'sts2.dll'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $candidates += $candidate }
    }
    if ($candidates.Count -ne 1) { throw "Expected one game sts2.dll; found $($candidates.Count). Check game layout and paths file." }
    $candidates[0]
}
