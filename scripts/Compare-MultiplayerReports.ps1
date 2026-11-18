[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Reports,
    [string]$OutputFile,
    [switch]$NoOpen
)
$ErrorActionPreference = 'Stop'
if ($Reports.Count -lt 2) { throw 'Provide the host report and at least one client report.' }
if (!$OutputFile) { $OutputFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'local/selectmod-multiplayer-compare.txt' }
$OutputFile = [IO.Path]::GetFullPath($OutputFile)
$rows = @()
foreach ($report in $Reports) {
    $raw = [IO.File]::ReadAllText([IO.Path]::GetFullPath($report))
    $captures = [regex]::Matches($raw, 'MP_CONTRACT_CAPTURED digest=([0-9A-F]{64}) role=(Host|Client)(?: phase=(start|resume))?')
    $snapshot = ''; $role = ''; $phase = ''; $context = ''; $plan = ''
    if ($captures.Count -gt 0) {
        $last = $captures[$captures.Count - 1]
        $snapshot = $last.Groups[1].Value; $role = $last.Groups[2].Value; $phase = $last.Groups[3].Value
        if (!$phase) { $phase = 'start' }
        # Never pair a new start with the previous run's result.
        $segment = $raw.Substring($last.Index)
        $contextMatch = [regex]::Match($segment, 'MP_START_CONTEXT digest=([0-9A-F]{64})')
        $planMatch = [regex]::Match($segment, 'ROOM_PLAN_DIGEST digest=([0-9A-F]{64})')
        if ($contextMatch.Success) { $context = $contextMatch.Groups[1].Value }
        if ($planMatch.Success) { $plan = $planMatch.Groups[1].Value }
        $completion = if ($phase -eq 'resume') { 'MP_RESUME_COMPLETED' } else { 'MP_START_COMPLETED' }
        if (![regex]::IsMatch($segment, ($completion + ' digest=' + $snapshot))) { $plan = '' }
        $attempts = [regex]::Matches($raw, 'MP_(START|RESUME)_ATTEMPT role=(Host|Client)')
        if ($attempts.Count -gt 0 -and $attempts[$attempts.Count - 1].Index -gt $last.Index) { $context = ''; $plan = '' }
    }
    $rows += [pscustomobject]@{ File = [IO.Path]::GetFileName($report); Role = $role; Phase = $phase; Snapshot = $snapshot; Context = $context; Plan = $plan }
}
$complete = @($rows | Where-Object { !$_.Snapshot -or !$_.Context -or !$_.Plan }).Count -eq 0
$roles = @($rows | Where-Object { $_.Role -eq 'Host' }).Count -ge 1 -and @($rows | Where-Object { $_.Role -eq 'Client' }).Count -ge 1
$equal = $complete -and $roles
foreach ($field in @('Snapshot', 'Context', 'Plan', 'Phase')) {
    if (@($rows | ForEach-Object { $_.$field } | Select-Object -Unique).Count -ne 1) { $equal = $false }
}
$result = if (!$complete -or !$roles) { 'INCOMPLETE' } elseif ($equal) { 'MATCH' } else { 'MISMATCH' }
$lines = @('SelectMOD multiplayer report comparison', "Result: $result", 'Compares recorded fixed-selection start/resume snapshots/context/plans only; not packet delivery or gameplay certification.')
foreach ($row in $rows) { $lines += @('', "File: $($row.File)", "Role: $($row.Role)", "Phase: $($row.Phase)", "Snapshot: $($row.Snapshot)", "Context: $($row.Context)", "Plan: $($row.Plan)") }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputFile)) | Out-Null
$temp = $OutputFile + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
[IO.File]::WriteAllLines($temp, [string[]]$lines, [Text.UTF8Encoding]::new($false))
if ([IO.File]::Exists($OutputFile)) { [IO.File]::Replace($temp, $OutputFile, $OutputFile + '.previous') }
else { [IO.File]::Move($temp, $OutputFile) }
Write-Host "Comparison $result : $OutputFile"
if (!$NoOpen -and [IO.Path]::DirectorySeparatorChar -eq '\') {
    try { Start-Process -FilePath explorer.exe -ArgumentList ('/select,"{0}"' -f $OutputFile) | Out-Null }
    catch { Write-Warning "Comparison saved, but Explorer could not select it: $OutputFile" }
}
if (!$equal) { throw "Multiplayer comparison $result. Compare reports from the same newly started fixed-selection run; send the reports and comparison file." }
