[CmdletBinding()]
param([string]$PathsFile, [switch]$NoOpen)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$local = Join-Path $repo 'local'
$root = Join-Path $local 'workshop'
$workspace = Join-Path $root 'SelectMOD'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$guard = [IO.File]::Open((Join-Path $root 'prepare.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$stage = Join-Path $root ('stage-' + [guid]::NewGuid().ToString('N'))
$report = Join-Path $local 'workshop-prepare-report.txt'
$trace = [Collections.Generic.List[string]]::new()
try {
    $paths = Get-SelectModPaths -PathsFile $PathsFile
    $data = Split-Path (Get-SelectModGameAssembly -Game $paths.Game) -Parent
    $policy = Get-Content -LiteralPath (Join-Path $repo 'compatibility/reviewed-builds.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $policy.Assemblies.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $data $entry.Name) -Algorithm SHA256).Hash -ne $entry.Value) {
            throw "Unreviewed game reference: $($entry.Name). Existing Workshop workspace retained."
        }
    }
    Invoke-SelectModUtf8 {
        & dotnet run --project (Join-Path $repo 'tests/SelectMOD.ContractTests') -c Release | Tee-Object -FilePath (Join-Path $local 'contract-output.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Independent contract tests failed. Workspace retained.' }
    }
    & (Join-Path $PSScriptRoot 'Build.ps1') -GameDataDir $data
    foreach ($entry in $policy.Assemblies.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $data $entry.Name) -Algorithm SHA256).Hash -ne $entry.Value) { throw 'Game reference changed during build.' }
    }
    $manifestPath = Join-Path $repo 'src/SelectMOD/SelectMOD.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.id -ne 'SelectMOD' -or $manifest.version -ne '0.7.1' -or !$manifest.has_dll -or $manifest.has_pck) { throw 'Unexpected 0.7.1 payload manifest.' }
    $content = Join-Path $stage 'content'
    New-Item -ItemType Directory -Path $content -Force | Out-Null
    # Explicit allowlist: never copy an installed mod folder, paths, saves, logs or game DLLs.
    Copy-Item -LiteralPath (Join-Path $repo 'src/SelectMOD/bin/Release/net9.0/SelectMOD.dll') -Destination $content
    Copy-Item -LiteralPath $manifestPath -Destination $content
    $metadata = [ordered]@{
        title = 'SelectMOD - ボス・エンシェント選択 [0.7.1 試験版]'
        description = [IO.File]::ReadAllText((Join-Path $repo 'workshop/description.txt'))
        visibility = 'unlisted'
        changeNote = '0.7.1を知人との試験用に配布。ボス・エンシェント選択、Standardカスタムのマルチ試験版。'
        tags = @(); dependencies = @(); contentDescriptors = @()
    }
    [IO.File]::WriteAllText((Join-Path $stage 'workshop.json'), ($metadata | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $repo 'workshop/image.png') -Destination $stage
    if ((Get-Item -LiteralPath (Join-Path $stage 'image.png')).Length -ge 1MB) { throw 'Workshop preview must be smaller than 1 MB.' }
    foreach ($file in @(Get-ChildItem -LiteralPath $content -File)) { $trace.Add("PAYLOAD $($file.Name) SHA256=$((Get-FileHash -LiteralPath $file.FullName).Hash)") }
    # Replace content only; durable mod_id.txt and upload-attempt state are never rebuilt.
    New-Item -ItemType Directory -Path $workspace -Force | Out-Null
    $oldContent = Join-Path $workspace 'content'
    $previous = Join-Path $root ('previous-content-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $oldContent) { Move-Item -LiteralPath $oldContent -Destination $previous }
    try { Move-Item -LiteralPath $content -Destination $oldContent }
    catch {
        if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $oldContent }
        throw
    }
    foreach ($name in @('workshop.json', 'image.png')) { Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $workspace $name) -Force }
    $trace.Add('PREPARED version=0.7.1 visibility=unlisted; no installation, saves, config or Steam changes')
    Write-Host "Prepared: $workspace"
    Write-Host 'Visibility: unlisted (anyone with the link). No Steam upload performed.'
    if (!$NoOpen -and $env:OS -eq 'Windows_NT') { Start-Process explorer.exe -ArgumentList ('"' + $workspace + '"') | Out-Null }
} catch {
    $trace.Add('FAILED: ' + $_.Exception.Message)
    Write-Warning 'Workshop preparation failed. Send local/workshop-prepare-report.txt; partial staging is retained.'
    throw
} finally {
    if (Test-Path -LiteralPath $report) { Copy-Item -LiteralPath $report -Destination ($report + '.previous') -Force }
    [IO.File]::WriteAllLines($report, $trace, [Text.UTF8Encoding]::new($false))
    $guard.Dispose()
}
