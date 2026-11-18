$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts/Common.ps1')
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('selectmod-native-' + [guid]::NewGuid().ToString('N'))
function Assert-True($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
try {
    New-Item -ItemType Directory -Path $fixture | Out-Null
    $child = Join-Path $fixture 'child with spaces.ps1'
    [IO.File]::WriteAllText($child, @'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::Error.WriteLine('Setting breakpad minidump AppID = 2868840')
[Console]::Out.WriteLine(('x' * 70000))
[Console]::Error.WriteLine(('e' * 70000))
[Console]::Out.WriteLine(($args | ConvertTo-Json -Compress))
[Console]::Out.WriteLine(([string][char]0x65e5 + [char]0x672c + [char]0x8a9e))
if ($args[-1] -eq 'failure') { exit 17 }
exit 0
'@)
    $shellExe = (Get-Process -Id $PID).Path
    $report = Join-Path $fixture 'process-report.txt'
    $values = @('path with spaces', 'quote"inside', 'C:\trailing slash\', '$() literal', '', 'ok')
    $result = Invoke-SelectModNativeProcess -FilePath $shellExe -Arguments (@('-NoProfile', '-NonInteractive', '-File', $child) + $values) -ReportPath $report 6>$null
    Assert-True ($result -eq 0) 'Harmless stderr did not preserve successful exit'
    $text = [IO.File]::ReadAllText($report)
    Assert-True ($text.Contains('Setting breakpad minidump AppID = 2868840')) 'Stderr message was lost'
    Assert-True ($text.Contains('x' * 70000) -and $text.Contains('e' * 70000)) 'Concurrent stdout/stderr pipe capture was incomplete'
    Assert-True ($text.Contains(($values | ConvertTo-Json -Compress))) 'Native argument boundaries/quoting changed'
    $sample = [string][char]0x65e5 + [char]0x672c + [char]0x8a9e
    Assert-True ($text.Contains($sample)) 'Native UTF-8 text was corrupted'
    Assert-True ($text.Contains('EXIT_CODE=0')) 'Success exit code missing from report'
    $result = Invoke-SelectModNativeProcess -FilePath $shellExe -Arguments @('-NoProfile', '-NonInteractive', '-File', $child, 'failure') -ReportPath $report 6>$null
    Assert-True ($result -eq 17 -and [IO.File]::ReadAllText($report).Contains('EXIT_CODE=17')) 'Native failure exit code was hidden'
    Assert-True ($ErrorActionPreference -eq 'Stop') 'Caller error preference was changed'
    Write-Host 'PASS: real native child stderr with exit 0, nonzero exit, simultaneous full pipes, argument quoting and UTF-8 capture. Not Windows PowerShell 5.1 or Steam acceptance.'
} finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
