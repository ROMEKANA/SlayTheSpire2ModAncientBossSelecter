[CmdletBinding()]
param([string]$PathsFile, [switch]$NoOpen)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$paths = Get-SelectModPaths -PathsFile $PathsFile
$local = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $local -Force | Out-Null
$text = [Text.StringBuilder]::new()
$null = $text.AppendLine('SelectMOD 0.5.0 report; export only, not automated gameplay acceptance.')
foreach ($source in @((Join-Path $local 'setup-report.txt'), (Join-Path $local 'contract-output.txt'), (Join-Path $local 'build-output.txt'), (Join-Path $paths.Config 'selectmod.log'))) {
    $null = $text.AppendLine("`n=== $(Split-Path $source -Leaf) ===")
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        foreach ($line in @(Get-Content -LiteralPath $source -Tail 200)) { $null = $text.AppendLine($line) }
    } else { $null = $text.AppendLine('Not found; setup or runtime may not have completed.') }
}
# Only explicit engine-log paths are read; never scan or copy profile saves.
$engineLog = Join-Path $paths.Save 'logs/godot.log'
$null = $text.AppendLine("`n=== engine log (tail; if available) ===")
if (Test-Path -LiteralPath $engineLog -PathType Leaf) {
    foreach ($line in @(Get-Content -LiteralPath $engineLog -Encoding UTF8 -Tail 160)) { $null = $text.AppendLine($line) }
} else { $null = $text.AppendLine('Game engine log not found at the conventional logs/godot.log location.') }
$report = Join-Path $local 'selectmod-probe.txt'
[IO.File]::WriteAllText($report, $text.ToString(), [Text.UTF8Encoding]::new($false))
Write-Host "Send report: $report"

if (!$NoOpen -and [IO.Path]::DirectorySeparatorChar -eq '\') {
    try { Start-Process -FilePath explorer.exe -ArgumentList ('/select,"{0}"' -f $report) | Out-Null }
    catch { Write-Warning "Report saved, but Explorer could not select it: $report" }
}
