[CmdletBinding()]
param(
    [string]$PathsFile,
    [string]$ToolPath,
    [ValidateRange(1, 10000)][int]$MaxCallSites = 2500
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$local = Join-Path $repo 'local'
New-Item -ItemType Directory -Path $local -Force | Out-Null
# One export per checkout. A failed export retains the previous valid report.
$guard = [IO.File]::Open((Join-Path $local 'source-export.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
# Keep generated names away from a potentially long checkout path. CreateTempSubdirectory
# is unavailable in Windows PowerShell 5.1; the unique directory is created below instead.
$work = Join-Path ([IO.Path]::GetTempPath()) ('sm-' + [guid]::NewGuid().ToString('N').Substring(0, 12))
$stderr = Join-Path $work 'tool-errors.txt'
$success = $false
$temporary = $null
Push-Location $repo
try {
    New-Item -ItemType Directory -Path $work | Out-Null
    $paths = Get-SelectModPaths -PathsFile $PathsFile
    $assembly = Get-SelectModGameAssembly -Game $paths.Game
    $data = Split-Path $assembly -Parent
    if (!$ToolPath) {
        if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
            throw 'Install .NET 9 SDK: winget install --id Microsoft.DotNet.SDK.9 --exact; reopen PowerShell and retry.'
        }
        $toolDir = Join-Path $local 'ilspy9'
        $exe = if ([IO.Path]::DirectorySeparatorChar -eq '\') { 'ilspycmd.exe' } else { 'ilspycmd' }
        $ToolPath = Join-Path $toolDir $exe
        if (!(Test-Path -LiteralPath $ToolPath -PathType Leaf)) {
            & dotnet tool install --tool-path $toolDir ilspycmd --version 9.1.0.7988 --allow-roll-forward
            if ($LASTEXITCODE -ne 0) { throw 'Pinned ILSpyCmd installation failed. Check .NET 9 SDK/runtime and NuGet connectivity.' }
        }
    }
    $ToolPath = [IO.Path]::GetFullPath($ToolPath)
    $version = @(& $ToolPath --version 2> $stderr)
    if ($LASTEXITCODE -ne 0 -or !($version -match '^ilspycmd: 9\.1\.0\.7988$')) { throw 'Expected ILSpyCmd 9.1.0.7988.' }
    $fingerprints = @{}
    foreach ($name in @('sts2.dll', '0Harmony.dll', 'GodotSharp.dll')) {
        $file = Join-Path $data $name
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing game reference: $name" }
        $fingerprints[$name] = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    }
    $text = [Text.StringBuilder]::new()
    $null = $text.AppendLine('SELECTMOD STATIC SOURCE REPORT v2')
    $null = $text.AppendLine('Static evidence only; no game execution or runtime acceptance. No saves/config were read or changed.')
    $null = $text.AppendLine('Collected UTC: ' + [DateTime]::UtcNow.ToString('o'))
    $null = $text.AppendLine('Tool: ILSpyCmd 9.1.0.7988')
    $null = $text.AppendLine('PowerShell: ' + $PSVersionTable.PSVersion.ToString())
    $null = $text.AppendLine('Game file version: ' + [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).FileVersion)
    foreach ($name in @('sts2.dll', '0Harmony.dll', 'GodotSharp.dll')) {
        $null = $text.AppendLine($name + ' SHA256: ' + $fingerprints[$name])
    }
    $manifest = Join-Path (Split-Path (Split-Path $paths.Game -Parent) -Parent) 'appmanifest_2868840.acf'
    if (Test-Path -LiteralPath $manifest -PathType Leaf) {
        foreach ($line in [IO.File]::ReadAllLines($manifest)) {
            if ($line -match '"buildid"\s+"(\d+)"') { $null = $text.AppendLine('Steam Build ID: ' + $Matches[1]) }
        }
    }
    foreach ($dir in @($paths.Game, $data) | Select-Object -Unique) {
        foreach ($runtime in @(Get-ChildItem -LiteralPath $dir -File -Filter '*runtimeconfig.json')) {
            $null = $text.AppendLine("`n=== Runtime config: $($runtime.Name) ===")
            $null = $text.AppendLine([IO.File]::ReadAllText($runtime.FullName))
        }
    }
    # Full temporary decompilation lets us find real callers even when namespaces change.
    # The generated project is NEVER built or executed.
    $sourceDir = Join-Path $work 'source'
    Write-Host 'Decompiling sts2.dll for selection, ascension, persistence and network caller evidence. This can take several minutes.'
    & $ToolPath --disable-updatecheck --referencepath $data -p -o $sourceDir $assembly 1> (Join-Path $work 'tool-output.txt') 2> $stderr
    if ($LASTEXITCODE -ne 0) { throw 'Game source export failed. Previous report retained.' }
    $sources = @(Get-ChildItem -LiteralPath $sourceDir -Recurse -File -Filter '*.cs' | Sort-Object FullName)
    if (!$sources.Count) { throw 'ILSpy produced no C# source. Previous report retained.' }
    $targetNames = @(
        'RunManager', 'RunState', 'IRunState', 'ActModel', 'RoomSet', 'ModifierModel', 'AbstractModel', 'ModelDb',
        'SavedPropertyAttribute', 'SavedProperties', 'SerializableModifier', 'SerializableRun',
        'SerializableActModel', 'SerializableRoomSet',
        'AscensionManager', 'AscensionLevel', 'AscensionHelper',
        'StartRunLobby', 'LoadRunLobby', 'INetGameService', 'NetGameType',
        'LobbyBeginRunMessage', 'LobbyBeginLoadedRunMessage', 'LobbyModifiersChangedMessage',
        'ClientLobbyJoinResponseMessage', 'ClientLoadJoinResponseMessage', 'ClientRejoinResponseMessage',
        'INetHostGameService', 'INetClientGameService', 'NetHostGameService', 'NetClientGameService',
        'INetMessage', 'IPacketSerializable', 'NetMessageBus', 'ModelIdSerializationCache',
        'PacketWriter', 'PacketReader', 'PacketWriterExtensions', 'PacketReaderExtensions',
        'ModManager', 'ModInitializerAttribute', 'ReflectionHelper',
        'NCustomRunScreen', 'NCustomRunModifiersList', 'MapCmd',
        'Rng', 'RunRngSet', 'AncientEventModel', 'Neow', 'EventModel', 'EncounterModel'
    )
    $null = $text.AppendLine("`n=== Source inventory (relative paths; includes actual namespaces) ===")
    foreach ($source in $sources) { $null = $text.AppendLine($source.FullName.Substring($sourceDir.Length + 1)) }
    $found = @{}
    foreach ($source in $sources) {
        if ($source.BaseName -notin $targetNames) { continue }
        $found[$source.BaseName] = $true
        $relative = $source.FullName.Substring($sourceDir.Length + 1)
        $null = $text.AppendLine("`n=== Target source: $relative ===")
        $null = $text.AppendLine([IO.File]::ReadAllText((ConvertTo-SelectModIoPath $source.FullName)))
    }
    $null = $text.AppendLine("`n=== Requested types not found (names may differ; investigate inventory) ===")
    foreach ($name in $targetNames) { if (!$found.ContainsKey($name)) { $null = $text.AppendLine($name) } }
    # Bounded caller/context evidence; not an exhaustive semantic call graph.
    $pattern = '\b(HasLevel|AscensionLevel|Ascension|GenerateRooms|GenerateMap|GetUnlockedAncients|ValidateRoomsAfterLoad|InitializeShared|BeginRunForAllPlayers|BeginRunForAllPlayersIfAllReady|SavedProperty|ToSerializable|FromSerializable|RegisterMessageHandler|SendMessage|SetBossEncounter)\b|_sharedAncientSubset'
    $count = 0
    $total = 0
    $null = $text.AppendLine("`n=== Caller/search context (5 lines before/after; partial when capped) ===")
    foreach ($source in $sources) {
        $lines = [IO.File]::ReadAllLines((ConvertTo-SelectModIoPath $source.FullName))
        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($lines[$i] -notmatch $pattern) { continue }
            $total++
            if ($count -ge $MaxCallSites) { continue }
            $count++
            $relative = $source.FullName.Substring($sourceDir.Length + 1)
            $null = $text.AppendLine("`n--- ${relative}:$($i + 1) ---")
            for ($j = [Math]::Max(0, $i - 5); $j -le [Math]::Min($lines.Length - 1, $i + 5); $j++) {
                $null = $text.AppendLine(('{0}: {1}' -f ($j + 1), $lines[$j]))
            }
        }
    }
    $null = $text.AppendLine("Search matches: $total; included: $count; cap: $MaxCallSites. Text search does not prove complete route coverage.")
    if ($total -gt $count) {
        Write-Warning "Caller evidence truncated: $count / $total. Retry with -MaxCallSites 10000 if complete search context is needed."
    }
    foreach ($name in $fingerprints.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $data $name) -Algorithm SHA256).Hash -ne $fingerprints[$name]) {
            throw 'Game references changed during export. Previous report retained; retry after updates finish.'
        }
    }
    foreach ($required in @('RunManager', 'ActModel', 'ModifierModel', 'AscensionManager')) {
        if (!$found.ContainsKey($required)) { throw "Required type missing: $required. Partial source retained for investigation; previous report unchanged." }
    }
    $report = Join-Path $local 'selectmod-sources.txt'
    # Stage next to the report: TEMP may be on another drive, while Replace/Move
    # must preserve the same-filesystem atomic publication contract.
    $temporary = Join-Path $local ('report-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, $text.ToString(), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $report) {
        # Replace is atomic on the same filesystem and preserves the previous successful report.
        [IO.File]::Replace($temporary, $report, (Join-Path $local ('selectmod-sources.previous-' + [guid]::NewGuid().ToString('N') + '.txt')))
    } else { [IO.File]::Move($temporary, $report) }
    $success = $true
    Write-Host "Report ready: $report"
    Write-Host "Caller matches included: $count / $total. Send selectmod-sources.txt; do not commit generated game source."
} catch {
    $failure = "Export failed: $($_.Exception.Message)`nPrevious successful report was not replaced.`nPartial output: $work"
    [IO.File]::WriteAllText((Join-Path $local 'selectmod-export-error.txt'), $failure, [Text.UTF8Encoding]::new($false))
    Write-Warning 'Send local/selectmod-export-error.txt if export fails. Tool details are retained in the partial output directory.'
    throw
} finally {
    Pop-Location
    try {
        if ($success) {
            try { Remove-Item -LiteralPath (ConvertTo-SelectModIoPath $work) -Recurse -Force }
            catch { Write-Warning "Report succeeded, but temporary source cleanup failed. Retained: $work" }
        }
        if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -Force }
    } finally { $guard.Dispose() }
}
