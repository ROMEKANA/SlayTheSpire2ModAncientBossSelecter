$ErrorActionPreference = 'Stop'
$script = Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts/Compare-MultiplayerReports.ps1'
$dir = Join-Path ([IO.Path]::GetTempPath()) ('selectmod-compare-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
function Assert-Compare($condition, $name) { if (!$condition) { throw $name } }
function Expect-CompareFailure([scriptblock]$action, [string]$pattern) {
    try { & $action } catch { Assert-Compare ($_.Exception.Message -like $pattern) 'Unexpected comparison error'; return }
    throw 'Expected comparison failure was not raised'
}
try {
    $hostFile = Join-Path $dir 'host.txt'; $clientFile = Join-Path $dir 'client.txt'; $out = Join-Path $dir 'result.txt'
    $snapshot = 'A' * 64; $context = 'B' * 64; $plan = 'C' * 64
    $base = "MP_CONTRACT_CAPTURED digest=$snapshot role=ROLE`nMP_START_CONTEXT digest=$context`nROOM_PLAN_DIGEST digest=$plan`nMP_START_COMPLETED digest=$snapshot"
    [IO.File]::WriteAllText($hostFile, $base.Replace('ROLE', 'Host'))
    [IO.File]::WriteAllText($clientFile, $base.Replace('ROLE', 'Client'))
    & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen
    Assert-Compare ((Get-Content $out -Raw).Contains('Result: MATCH')) 'Matching peer records rejected'
    [IO.File]::WriteAllText($clientFile, $base.Replace('ROLE', 'Client').Replace($plan, ('D' * 64)))
    Expect-CompareFailure { & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen } '*MISMATCH*'
    Assert-Compare ((Get-Content ($out + '.previous') -Raw).Contains('Result: MATCH')) 'Previous comparison not retained'
    [IO.File]::WriteAllText($clientFile, "ROOM_PLAN_DIGEST digest=$plan`nMP_CONTRACT_CAPTURED digest=$snapshot role=Client`nMP_START_CONTEXT digest=$context")
    Expect-CompareFailure { & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen } '*INCOMPLETE*'
    [IO.File]::WriteAllText($clientFile, $base.Replace('ROLE', 'Host'))
    Expect-CompareFailure { & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen } '*INCOMPLETE*'
    [IO.File]::WriteAllText($clientFile, $base.Replace('ROLE', 'Client phase=resume').Replace('MP_START_COMPLETED', 'MP_RESUME_COMPLETED'))
    Expect-CompareFailure { & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen } '*MISMATCH*'
    [IO.File]::WriteAllText($clientFile, $base.Replace('ROLE', 'Client') + "`nMP_RESUME_ATTEMPT role=Client")
    Expect-CompareFailure { & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen } '*INCOMPLETE*'
    [IO.File]::WriteAllText($clientFile, $base.Replace('ROLE', 'Client').Replace("MP_START_COMPLETED digest=$snapshot", ''))
    Expect-CompareFailure { & $script -Reports @($hostFile, $clientFile) -OutputFile $out -NoOpen } '*INCOMPLETE*'
    Write-Host 'PASS: multiplayer report equality, mismatch, incomplete last run, completion/latest-attempt/role/phase checks and previous-report preservation (synthetic logs only).'
} finally { Remove-Item $dir -Recurse -Force }
