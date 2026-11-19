$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('selectmod-workshop-' + [guid]::NewGuid().ToString('N'))
function Assert-True($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Expect-Failure([scriptblock]$Action, [string]$Pattern) {
    $failed = $false
    try { & $Action } catch { $failed = $true; Assert-True ($_.Exception.Message -match $Pattern) "Unexpected failure: $_" }
    Assert-True $failed "Expected failure: $Pattern"
}
function global:dotnet { $global:LASTEXITCODE = 0; 'Fixture contract runner only; no production build.' }
try {
    New-Item -ItemType Directory -Path $fixture | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'scripts') -Destination (Join-Path $fixture 'scripts') -Recurse
    # Orchestration double only; NativeProcess.Tests.ps1 exercises real child pipes.
    [IO.File]::AppendAllText((Join-Path $fixture 'scripts/Common.ps1'), @'

function Invoke-SelectModNativeProcess {
    param($FilePath, $Arguments, $ReportPath)
    & $FilePath @Arguments | Out-File -LiteralPath $ReportPath -Encoding utf8
    return $global:LASTEXITCODE
}
'@)
    Copy-Item -LiteralPath (Join-Path $repo 'workshop') -Destination (Join-Path $fixture 'workshop') -Recurse
    $src = Join-Path $fixture 'src/SelectMOD'
    New-Item -ItemType Directory -Path $src -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'src/SelectMOD/SelectMOD.json') -Destination $src
    $game = Join-Path $fixture 'game with spaces'
    New-Item -ItemType Directory -Path $game -Force | Out-Null
    $hashes = [ordered]@{}
    foreach ($name in @('sts2.dll', '0Harmony.dll', 'GodotSharp.dll')) {
        $file = Join-Path $game $name
        [IO.File]::WriteAllText($file, 'independent fixture, not game binary')
        $hashes[$name] = (Get-FileHash -LiteralPath $file).Hash
    }
    New-Item -ItemType Directory -Path (Join-Path $fixture 'compatibility') | Out-Null
    [IO.File]::WriteAllText((Join-Path $fixture 'compatibility/reviewed-builds.json'), (@{Assemblies=$hashes} | ConvertTo-Json))
    [IO.File]::WriteAllText((Join-Path $fixture 'scripts/Build.ps1'), @'
param($GameDataDir)
if (Test-Path (Join-Path $PSScriptRoot 'fail-build')) { throw 'fixture build failure' }
$out = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/SelectMOD/bin/Release/net9.0'
New-Item -ItemType Directory -Path $out -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $out 'SelectMOD.dll'), 'fixture payload, never production')
[IO.File]::WriteAllText((Join-Path $out 'SelectMOD.paths.txt'), 'PRIVATE MUST NOT SHIP')
[IO.File]::WriteAllText((Join-Path $out 'sts2.dll'), 'GAME MUST NOT SHIP')
'@)
    $paths = Join-Path $fixture 'paths.txt'
    [IO.File]::WriteAllLines($paths, [string[]]@($game, (Join-Path $fixture 'save'), (Join-Path $fixture 'config')))
    $prepare = Join-Path $fixture 'scripts/Prepare-Workshop.ps1'
    $publish = Join-Path $fixture 'scripts/Publish-Workshop.ps1'
    & $publish -PathsFile $paths -PrepareOnly -NoOpen
    $workspace = Join-Path $fixture 'local/workshop/SelectMOD'
    $content = Join-Path $workspace 'content'
    Assert-True ((@(Get-ChildItem $content).Name | Sort-Object) -join '|' -eq 'SelectMOD.dll|SelectMOD.json') 'Non-allowlisted payload leaked'
    $metadata = Get-Content (Join-Path $workspace 'workshop.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-True ($metadata.visibility -eq 'unlisted' -and !$metadata.dependencies.Count) 'Unexpected visibility/dependencies'
    Assert-True ($metadata.description.Contains('SelectMOD 0.8.0')) 'Description encoding damaged'
    Assert-True (!(Test-Path (Join-Path $fixture 'config')) -and !(Test-Path (Join-Path $fixture 'save'))) 'Preparation touched config/save'
    $original = [IO.File]::ReadAllText((Join-Path $content 'SelectMOD.dll'))
    New-Item -ItemType File -Path (Join-Path $fixture 'scripts/fail-build') | Out-Null
    Expect-Failure { & $prepare -PathsFile $paths -NoOpen } 'fixture build failure'
    Assert-True ([IO.File]::ReadAllText((Join-Path $content 'SelectMOD.dll')) -eq $original) 'Failed build replaced prepared payload'
    Remove-Item (Join-Path $fixture 'scripts/fail-build')
    $uploader = Join-Path $fixture 'fake-uploader.ps1'
    [IO.File]::WriteAllText($uploader, @'
$Workspace = $args[2]
if ($args[0] -ne 'upload' -or $args[1] -ne '-w') { throw 'Incorrect uploader arguments' }
if (Test-Path (Join-Path $PSScriptRoot 'upload-fails')) { $global:LASTEXITCODE = 7; 'simulated partial failure'; return }
[IO.File]::WriteAllText((Join-Path $Workspace 'mod_id.txt'), '123456789')
$global:LASTEXITCODE = 0
'@)
    New-Item -ItemType File -Path (Join-Path $fixture 'upload-fails') | Out-Null
    Expect-Failure { & $publish -PathsFile $paths -UploaderPath $uploader -NoOpen } 'exit 7'
    Assert-True (Test-Path (Join-Path $fixture 'local/workshop/upload-pending.txt')) 'Failed attempt lost duplicate-prevention marker'
    Remove-Item (Join-Path $fixture 'upload-fails')
    Expect-Failure { & $publish -PathsFile $paths -UploaderPath $uploader -NoOpen } 'previous upload may have created'
    & $publish -PathsFile $paths -UploaderPath $uploader -WorkshopItemId '123456789' -NoOpen
    Assert-True (!(Test-Path (Join-Path $fixture 'local/workshop/upload-pending.txt'))) 'Successful upload retained pending marker'
    Remove-Item (Join-Path $workspace 'mod_id.txt')
    & $publish -PathsFile $paths -UploaderPath $uploader -NoOpen
    Assert-True ([IO.File]::ReadAllText((Join-Path $workspace 'mod_id.txt')) -eq '123456789') 'Durable ID backup was not restored'
    Expect-Failure { & $publish -PathsFile $paths -UploaderPath $uploader -WorkshopItemId '987654321' -NoOpen } 'Conflicting Workshop item IDs'
    [IO.File]::WriteAllText((Join-Path $game 'sts2.dll'), 'changed game')
    Expect-Failure { & $prepare -PathsFile $paths -NoOpen } 'Unreviewed game reference'
    Assert-True ([IO.File]::ReadAllText((Join-Path $workspace 'mod_id.txt')) -eq '123456789') 'Preparation lost item identity'
    Write-Host 'PASS: Workshop fixture orchestration, allowlist, fresh workspace, failure preservation and item-ID recovery. Not Steam/game acceptance.'
} finally {
    Remove-Item Function:/dotnet -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
