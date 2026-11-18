[CmdletBinding()]
param(
    [string]$PathsFile,
    [ValidateRange(0, 1000000)][int]$ProbeValue = 42,
    [bool]$SelectionDialog = $true
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$local = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $local -Force | Out-Null
$report = Join-Path $local 'setup-report.txt'
$trace = [Collections.Generic.List[string]]::new()
$guard = $null
$stage = $null
Push-Location $repo
try {
    if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'Close STS2 before setup.' }
    $paths = Get-SelectModPaths -PathsFile $PathsFile
    # Keep rollback/config data outside the loader and outside actual saves.
    $game = $paths.Game.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $save = $paths.Save.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $configDir = $paths.Config.TrimEnd([IO.Path]::DirectorySeparatorChar)
    foreach ($protected in @($game, $save)) {
        $sep = [IO.Path]::DirectorySeparatorChar
        if ($configDir.Equals($protected, [StringComparison]::OrdinalIgnoreCase) -or
            $configDir.StartsWith($protected + $sep, [StringComparison]::OrdinalIgnoreCase) -or
            $protected.StartsWith($configDir + $sep, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'SelectMOD config/rollback directory must be separate from game and save roots.'
        }
    }
    if ($env:SELECTMOD_CONFIG_DIR -and !([IO.Path]::GetFullPath($env:SELECTMOD_CONFIG_DIR).Equals($configDir, [StringComparison]::OrdinalIgnoreCase))) {
        throw 'SELECTMOD_CONFIG_DIR differs from line 3 of the paths file. Align them before setup.'
    }
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    $guard = [IO.File]::Open((Join-Path $configDir 'setup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $data = Split-Path (Get-SelectModGameAssembly -Game $game) -Parent
    $policy = Get-Content -LiteralPath (Join-Path $repo 'compatibility/reviewed-builds.json') -Raw | ConvertFrom-Json
    foreach ($entry in $policy.Assemblies.PSObject.Properties) {
        $file = Join-Path $data $entry.Name
        if (!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value) {
            throw "Unreviewed game reference: $($entry.Name). Existing install/config retained; collect current source evidence before updating compatibility policy."
        }
    }
    $trace.Add('REFERENCES_MATCH_REVIEWED_SOURCE')
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 9 SDK: winget install --id Microsoft.DotNet.SDK.9 --exact' }
    Invoke-SelectModUtf8 {
        & dotnet run --project (Join-Path $repo 'tests/SelectMOD.ContractTests') -c Release | Tee-Object -FilePath (Join-Path $local 'contract-output.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Independent contract tests failed.' }
    }
    $trace.Add('CONTRACT_TESTS_PASSED (not game acceptance)')
    & (Join-Path $PSScriptRoot 'Build.ps1') -GameDataDir $data
    $trace.Add('ACTUAL_REFERENCE_BUILD_PASSED (not game acceptance)')
    # Recheck updates before touching config/installation.
    foreach ($entry in $policy.Assemblies.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $data $entry.Name) -Algorithm SHA256).Hash -ne $entry.Value) { throw 'Game reference changed during build.' }
    }
    $config = Join-Path $configDir 'config.json'
    $settings = if (Test-Path -LiteralPath $config -PathType Leaf) { Get-Content -LiteralPath $config -Raw | ConvertFrom-Json } else { [pscustomobject]@{ ProbeValue = 42 } }
    if ($PSBoundParameters.ContainsKey('ProbeValue')) { $settings | Add-Member -NotePropertyName ProbeValue -NotePropertyValue $ProbeValue -Force }
    if (!$settings.PSObject.Properties['ProbeValue'] -or !($settings.ProbeValue -is [int] -or $settings.ProbeValue -is [long]) -or $settings.ProbeValue -lt 0 -or $settings.ProbeValue -gt 1000000) { throw 'Invalid config ProbeValue. Expected integer 0..1000000; config retained.' }
    if ($PSBoundParameters.ContainsKey('SelectionDialog') -or !$settings.PSObject.Properties['SelectionDialog']) {
        $settings | Add-Member -NotePropertyName SelectionDialog -NotePropertyValue $SelectionDialog -Force
    }
    if (!($settings.SelectionDialog -is [bool])) { throw 'SelectionDialog must be true or false. Config retained.' }
    # Retain old AscensionMask for history, but encounter-selector releases never read or apply it.
    $stage = Join-Path $configDir ('staged-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $sourceDll = Join-Path $repo 'src/SelectMOD/bin/Release/net9.0/SelectMOD.dll'
    if (!(Test-Path -LiteralPath $sourceDll -PathType Leaf)) { throw 'Production DLL missing after build.' }
    Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $stage 'SelectMOD.dll')
    Copy-Item -LiteralPath (Join-Path $repo 'src/SelectMOD/SelectMOD.json') -Destination $stage
    [IO.File]::WriteAllText((Join-Path $stage 'SelectMOD.paths.txt'), $configDir, [Text.UTF8Encoding]::new($false))
    $tempConfig = Join-Path $configDir ('config-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($tempConfig, ($settings | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $hadConfig = Test-Path -LiteralPath $config -PathType Leaf
    $backupConfig = Join-Path $configDir ('config.previous-' + [guid]::NewGuid().ToString('N') + '.json')
    $mods = Join-Path $game 'mods'
    $destination = Join-Path $mods 'SelectMOD'
    New-Item -ItemType Directory -Path $mods -Force | Out-Null
    if ((Test-Path -LiteralPath $destination) -and !(Test-Path -LiteralPath $destination -PathType Container)) { throw 'MOD destination is not a directory.' }
    if (Get-Process -Name 'sts2*' -ErrorAction SilentlyContinue) { throw 'STS2 started during setup. Close it and retry.' }
    $previous = $null
    $configWritten = $false
    $installStarted = $false
    try {
        if (Test-Path -LiteralPath $destination) {
            $backup = Join-Path $configDir ('previous-mod-' + [guid]::NewGuid().ToString('N'))
            Move-Item -LiteralPath $destination -Destination $backup
            $previous = $backup
        }
        $installStarted = $true
        New-Item -ItemType Directory -Path $destination | Out-Null
        foreach ($name in @('SelectMOD.dll', 'SelectMOD.json', 'SelectMOD.paths.txt')) {
            Copy-Item -LiteralPath (Join-Path $stage $name) -Destination $destination
        }
        if ($hadConfig) { [IO.File]::Replace($tempConfig, $config, $backupConfig) }
        else { [IO.File]::Move($tempConfig, $config) }
        $configWritten = $true
        $trace.Add('INSTALLED version=0.6.0 solo selector + Standard custom multiplayer beta; independent ascension disabled')
        Write-Host 'Installed SelectMOD 0.6.0. Solo: use the character-screen settings. Multiplayer beta: host a Standard CUSTOM run, 2-4 players, same SelectMOD on every peer. Live rejoin is unsupported.'
        Write-Host "Solo selection panel: $($settings.SelectionDialog). Multiplayer always displays synchronized host settings. Independent ascension is disabled; standard cumulative ascension is preserved."
    } catch {
        if ($installStarted -and (Test-Path -LiteralPath $destination)) { Move-Item -LiteralPath $destination -Destination (Join-Path $configDir ('failed-install-' + [guid]::NewGuid().ToString('N'))) }
        if ($previous) { Move-Item -LiteralPath $previous -Destination $destination }
        if ($configWritten) {
            if ($hadConfig) { Copy-Item -LiteralPath $backupConfig -Destination $config -Force }
            else { Remove-Item -LiteralPath $config -Force }
        }
        throw
    }
} catch {
    $trace.Add('FAILED: ' + $_.Exception.Message)
    Write-Warning 'Setup failed. Send local/setup-report.txt; staging is retained for diagnosis.'
    throw
} finally {
    try { [IO.File]::WriteAllLines($report, $trace, [Text.UTF8Encoding]::new($false)) }
    finally {
        if ($guard) { $guard.Dispose() }
        Pop-Location
    }
}
