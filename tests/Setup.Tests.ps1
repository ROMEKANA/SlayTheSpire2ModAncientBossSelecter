$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('selectmod-setup-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
function Assert-Setup($value, [string]$message) { if (!$value) { throw $message } }
function Expect-SetupFailure([scriptblock]$action, [string]$pattern) {
    try { & $action } catch { Assert-Setup ($_.Exception.Message -match $pattern) "Unexpected failure: $_"; return }
    throw "Expected failure: $pattern"
}
try {
    Copy-Item -LiteralPath (Join-Path $repo 'scripts') -Destination (Join-Path $fixture 'scripts') -Recurse
    New-Item -ItemType Directory -Path (Join-Path $fixture 'compatibility'), (Join-Path $fixture 'src/SelectMOD'), (Join-Path $fixture 'tests/SelectMOD.ContractTests') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $fixture 'src/SelectMOD/SelectMOD.csproj'), '<Project />')
    Copy-Item (Join-Path $repo 'src/SelectMOD/SelectMOD.json') (Join-Path $fixture 'src/SelectMOD/SelectMOD.json')
    $game = Join-Path $fixture 'game with spaces'
    $data = Join-Path $game 'data_sts2_windows_x86_64'
    $configDir = Join-Path $fixture 'config'
    $save = Join-Path $fixture 'untouched-save'
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    $hashes = @{}
    foreach ($name in @('sts2.dll', '0Harmony.dll', 'GodotSharp.dll')) {
        [IO.File]::WriteAllText((Join-Path $data $name), 'fixture reference')
        $hashes[$name] = (Get-FileHash (Join-Path $data $name)).Hash
    }
    @{ Assemblies = $hashes } | ConvertTo-Json | Set-Content (Join-Path $fixture 'compatibility/reviewed-builds.json')
    $paths = Join-Path $fixture 'paths.txt'
    [IO.File]::WriteAllLines($paths, [string[]]@($game, $save, $configDir))
    $originalPaths = [IO.File]::ReadAllText($paths)
    $setup = Join-Path $fixture 'scripts/Setup.ps1'
    $global:selectModSetupFixture = $fixture
    $global:selectModBuildFails = $false
    $global:selectModCopyFails = $false
    function global:dotnet {
        if ($args[0] -eq '--version') { '9.0.321'; $global:LASTEXITCODE = 0; return }
        if ($args[0] -eq 'build') {
            if ($global:selectModBuildFails) { $global:LASTEXITCODE = 17; return }
            $out = Join-Path $global:selectModSetupFixture 'src/SelectMOD/bin/Release/net9.0'
            if ($args -contains '-p:UseSelectModBaseLib=true') { $out = Join-Path $global:selectModSetupFixture 'src/SelectMOD/bin/BaseLib/Release/net9.0' }
            New-Item -ItemType Directory -Path $out -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $out 'SelectMOD.dll'), 'fixture MOD payload')
        }
        $global:LASTEXITCODE = 0
    }
    & $setup -PathsFile $paths
    $install = Join-Path $game 'mods/SelectMOD'
    $config = Join-Path $configDir 'config.json'
    Assert-Setup (@(Get-ChildItem $install).Count -eq 3) 'Unexpected payloads (reference/test DLL leakage)'
    Assert-Setup ((Get-Content $config -Raw | ConvertFrom-Json).ProbeValue -eq 42) 'Default config missing'
    Assert-Setup ([IO.File]::ReadAllText((Join-Path $install 'SelectMOD.paths.txt')) -eq $configDir) 'Custom config directory lost'
    & $setup -PathsFile $paths -ProbeValue 73 -SelectionDialog $false
    Assert-Setup ((Get-Content $config -Raw | ConvertFrom-Json).ProbeValue -eq 73) 'Requested config not applied'
    Assert-Setup (@(Get-ChildItem $configDir -Directory -Filter 'previous-mod-*').Count -eq 1) 'Previous release not preserved outside mods'
    & $setup -PathsFile $paths
    Assert-Setup ((Get-Content $config -Raw | ConvertFrom-Json).ProbeValue -eq 73) 'Existing preference reset'
    Assert-Setup (!((Get-Content $config -Raw | ConvertFrom-Json).SelectionDialog)) 'Dialog preference was not retained'
    $old = Get-Content $config -Raw | ConvertFrom-Json
    $old | Add-Member -NotePropertyName AscensionMask -NotePropertyValue 10
    $old | ConvertTo-Json | Set-Content $config
    & $setup -PathsFile $paths -SelectionDialog $true
    Assert-Setup ((Get-Content $config -Raw | ConvertFrom-Json).SelectionDialog) 'Dialog enable failed'
    Assert-Setup ((Get-Content $config -Raw | ConvertFrom-Json).AscensionMask -eq 10) 'Historical settings were destroyed'
    & $setup -PathsFile $paths -BaseLib
    $installedManifest = Get-Content (Join-Path $install 'SelectMOD.json') -Raw | ConvertFrom-Json
    Assert-Setup ($installedManifest.dependencies[0].id -eq 'BaseLib') 'BaseLib variant lost its loader dependency'
    & $setup -PathsFile $paths
    $installedManifest = Get-Content (Join-Path $install 'SelectMOD.json') -Raw | ConvertFrom-Json
    Assert-Setup ($installedManifest.dependencies.Count -eq 0) 'Switching back retained BaseLib dependency'
    $beforeConfig = [IO.File]::ReadAllText($config)
    $beforeDll = (Get-FileHash (Join-Path $install 'SelectMOD.dll')).Hash
    $global:selectModBuildFails = $true
    Expect-SetupFailure { & $setup -PathsFile $paths -ProbeValue 12 } 'build failed'
    $global:selectModBuildFails = $false
    Assert-Setup ([IO.File]::ReadAllText($config) -eq $beforeConfig) 'Failed build changed config'
    Assert-Setup ((Get-FileHash (Join-Path $install 'SelectMOD.dll')).Hash -eq $beforeDll) 'Failed build changed install'
    $global:selectModCopyFails = $true
    function global:Copy-Item {
        [CmdletBinding()]param([string]$LiteralPath, [string]$Destination, [switch]$Force)
        if ($global:selectModCopyFails -and $LiteralPath.EndsWith('SelectMOD.json') -and $Destination.Replace('\', '/').EndsWith('mods/SelectMOD')) {
            $global:selectModCopyFails = $false
            throw 'Simulated install copy failure'
        }
        Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
    }
    Expect-SetupFailure { & $setup -PathsFile $paths -ProbeValue 12 } 'copy failure'
    Remove-Item Function:/Copy-Item
    Assert-Setup ([IO.File]::ReadAllText($config) -eq $beforeConfig) 'Failed install changed config'
    Assert-Setup ((Get-FileHash (Join-Path $install 'SelectMOD.dll')).Hash -eq $beforeDll) 'Failed install did not restore old release'
    Assert-Setup (@(Get-ChildItem $configDir -Directory -Filter 'failed-install-*').Count -eq 1) 'Partial failure not retained'
    [IO.File]::AppendAllText((Join-Path $data 'sts2.dll'), 'unknown version')
    Expect-SetupFailure { & $setup -PathsFile $paths } 'Unreviewed game reference'
    Assert-Setup ([IO.File]::ReadAllText($config) -eq $beforeConfig) 'Unknown game changed config'
    Assert-Setup (!(Test-Path $save)) 'Setup touched real-save path'
    Assert-Setup ([IO.File]::ReadAllText($paths) -eq $originalPaths) 'Paths file changed'
    & (Join-Path $fixture 'scripts/Export-ProbeReport.ps1') -PathsFile $paths -NoOpen
    Assert-Setup (Test-Path (Join-Path $fixture 'local/selectmod-probe.txt')) 'Diagnostic report missing'
    New-Item -ItemType Directory -Path (Join-Path $save 'logs') -Force | Out-Null
    [IO.File]::WriteAllLines((Join-Path $save 'logs/godot.log'), [string[]](@('excluded-old-engine-line') + (1..160 | ForEach-Object { "engine line $_" })))
    [IO.File]::WriteAllText((Join-Path $save 'private.save'), 'private-save-content')
    & (Join-Path $fixture 'scripts/Export-ProbeReport.ps1') -PathsFile $paths -NoOpen
    $exported = Get-Content (Join-Path $fixture 'local/selectmod-probe.txt') -Raw
    Assert-Setup ($exported.Contains('engine line 160') -and !$exported.Contains('excluded-old-engine-line')) 'Engine log tail was not bounded'
    Assert-Setup (!$exported.Contains('private-save-content')) 'Export leaked save contents'

    & (Join-Path $fixture 'scripts/Disable.ps1') -PathsFile $paths
    Assert-Setup (!(Test-Path $install)) 'Disable left MOD under loader tree'
    Assert-Setup ([IO.File]::ReadAllText($config) -eq $beforeConfig) 'Disable changed config'
    Write-Host 'PASS: setup first install, custom paths, dialog migration/retention and retired-setting preservation, preference retention, update backup, failed build, install rollback, unknown-build refusal and save isolation (fixtures only).'
} finally {
    Remove-Item Function:/dotnet -ErrorAction SilentlyContinue
    Remove-Item Function:/Copy-Item -ErrorAction SilentlyContinue
    foreach ($name in @('selectModSetupFixture', 'selectModBuildFails', 'selectModCopyFails')) { Remove-Variable $name -Scope Global -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
