$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('selectmod-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
function Assert-True($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Expect-Failure([scriptblock]$Action, [string]$Pattern) {
    $failed = $false
    try { & $Action } catch { $failed = $true; Assert-True ($_.Exception.Message -match $Pattern) "Unexpected failure: $_" }
    Assert-True $failed "Expected failure: $Pattern"
}
try {
    foreach ($file in @(Get-ChildItem (Join-Path $repo 'scripts') -Filter '*.ps1')) {
        $tokens = $null; $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        Assert-True (!$errors.Count) "Parse failure: $($file.Name): $errors"
    }
    Copy-Item -LiteralPath (Join-Path $repo 'scripts') -Destination (Join-Path $fixture 'scripts') -Recurse
    $game = Join-Path $fixture 'game with spaces'
    $data = Join-Path $game 'data_sts2_windows_x86_64'
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    foreach ($name in @('sts2.dll', '0Harmony.dll', 'GodotSharp.dll')) { [IO.File]::WriteAllText((Join-Path $data $name), 'fixture-not-a-game-assembly') }
    [IO.File]::WriteAllText((Join-Path $data 'sts2.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net9.0"}}')
    $paths = Join-Path $fixture 'paths.txt'
    $save = Join-Path $fixture 'untouched-save'
    $config = Join-Path $fixture 'untouched-config'
    [IO.File]::WriteAllLines($paths, [string[]]@($game, $save, $config))
    $originalPaths = [IO.File]::ReadAllText($paths)
    . (Join-Path $fixture 'scripts/Common.ps1')
    $parsed = Get-SelectModPaths -PathsFile $paths
    Assert-True ($parsed.Game -eq $game) 'Path containing spaces was not preserved'
    Assert-True ((Get-SelectModGameAssembly -Game $game) -eq (Join-Path $data 'sts2.dll')) 'Assembly resolution failed'
    $badPaths = Join-Path $fixture 'bad-paths.txt'
    [IO.File]::WriteAllLines($badPaths, [string[]]@($game, $save))
    Expect-Failure { Get-SelectModPaths -PathsFile $badPaths } 'three non-empty'
    Expect-Failure { & (Join-Path $fixture 'scripts/Build.ps1') -PathsFile $paths } 'not implemented yet'
    $tool = Join-Path $fixture 'fake-ilspy.ps1'
    # This fake tests script orchestration only. It does not model or certify ILSpy/game behavior.
    [IO.File]::WriteAllText($tool, @'
$modeFile = Join-Path $PSScriptRoot 'mode.txt'
$mode = if (Test-Path $modeFile) { [IO.File]::ReadAllText($modeFile) } else { 'success' }
if ($args[0] -eq '--version') { 'ilspycmd: 9.1.0.7988'; $global:LASTEXITCODE = 0; return }
$out = $args[[Array]::IndexOf($args, '-o') + 1]
New-Item -ItemType Directory -Path $out -Force | Out-Null
foreach ($name in @('RunManager', 'ActModel', 'ModifierModel', 'AscensionManager')) {
    if ($mode -eq 'missing' -and $name -eq 'ActModel') { continue }
    [IO.File]::WriteAllText((Join-Path $out ($name + '.cs')), "class $name { bool Test() { return manager.HasLevel(AscensionLevel.DoubleBoss); } }")
}
if ($mode -eq 'mutate') { [IO.File]::AppendAllText($args[-1], 'changed') }
if ($mode -eq 'fail') { $global:LASTEXITCODE = 7; return }
$global:LASTEXITCODE = 0
'@)
    $export = Join-Path $fixture 'scripts/Export-GameSources.ps1'
    & $export -PathsFile $paths -ToolPath $tool -MaxCallSites 2
    $report = Join-Path $fixture 'local/selectmod-sources.txt'
    $text = [IO.File]::ReadAllText($report)
    Assert-True ($text -match 'included: 2; cap: 2') 'Caller cap or report output failed'
    Assert-True ($text -match 'Target source:.*RunManager.cs') 'Target source missing'
    Assert-True ($text -match 'net9.0') 'Runtime configuration missing'
    Assert-True ($text -notmatch [regex]::Escape($fixture)) 'Report leaked absolute fixture path'
    $hash = (Get-FileHash $report).Hash
    & $export -PathsFile $paths -ToolPath $tool -MaxCallSites 2
    Assert-True (@(Get-ChildItem (Join-Path $fixture 'local') -Filter 'selectmod-sources.previous-*.txt').Count -eq 1) 'Previous successful report was not preserved'
    $hash = (Get-FileHash $report).Hash
    foreach ($mode in @('fail', 'missing', 'mutate')) {
        [IO.File]::WriteAllText((Join-Path $fixture 'mode.txt'), $mode)
        $pattern = switch ($mode) { 'fail' { 'export failed' }; 'missing' { 'Required type missing' }; 'mutate' { 'references changed' } }
        Expect-Failure { & $export -PathsFile $paths -ToolPath $tool } $pattern
        Assert-True ((Get-FileHash $report).Hash -eq $hash) 'Failed export replaced previous report'
        Assert-True (Test-Path (Join-Path $fixture 'local/selectmod-export-error.txt')) 'Failure report missing'
    }
    Assert-True ([IO.File]::ReadAllText($paths) -eq $originalPaths) 'Existing paths file changed'
    Assert-True (!(Test-Path $save) -and !(Test-Path $config)) 'Diagnostics touched save/config'
    # Build orchestration: fake dotnet checks argument boundaries and nonzero propagation.
    $project = Join-Path $fixture 'test project.csproj'
    [IO.File]::WriteAllText($project, '<Project />')
    function global:dotnet {
        if ($args[0] -eq '--version') { '9.0.203'; $global:LASTEXITCODE = 0; return }
        if ($args[1] -ne $project) { throw 'Project argument split incorrectly' }
        if ($args -notcontains "-p:GameDataDir=$data") { throw 'Reference directory argument missing' }
        $global:LASTEXITCODE = $global:selectModFakeBuildExit
    }
    $global:selectModFakeBuildExit = 0
    & (Join-Path $fixture 'scripts/Build.ps1') -ProjectPath $project -GameDataDir $data
    $global:selectModFakeBuildExit = 9
    Expect-Failure { & (Join-Path $fixture 'scripts/Build.ps1') -ProjectPath $project -GameDataDir $data } 'build failed'
    Write-Host 'PASS: parsing, path validation, report/backup, bounded search, export failures, changed DLL, read-only scope and build exit propagation (fixtures only).'
} finally {
    Remove-Item Function:/dotnet -ErrorAction SilentlyContinue
    Remove-Variable selectModFakeBuildExit -Scope Global -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
