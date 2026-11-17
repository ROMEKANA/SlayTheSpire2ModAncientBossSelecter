[CmdletBinding()]
param([string]$PathsFile)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$paths = Get-SelectModPaths -PathsFile $PathsFile
$local = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $local -Force | Out-Null
$text = [Text.StringBuilder]::new()
$null = $text.AppendLine('SelectMOD 0.1.0 probe report; export only, not automated gameplay acceptance.')
foreach ($source in @((Join-Path $local 'setup-report.txt'), (Join-Path $local 'contract-output.txt'), (Join-Path $local 'build-output.txt'), (Join-Path $paths.Config 'selectmod.log'))) {
    $null = $text.AppendLine("`n=== $(Split-Path $source -Leaf) ===")
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        foreach ($line in @(Get-Content -LiteralPath $source -Tail 200)) { $null = $text.AppendLine($line) }
    } else { $null = $text.AppendLine('Not found; setup or runtime may not have completed.') }
}
$report = Join-Path $local 'selectmod-probe.txt'
[IO.File]::WriteAllText($report, $text.ToString(), [Text.UTF8Encoding]::new($false))
Write-Host "Send report: $report"
