[CmdletBinding()]
param(
    [string]$PathsFile,
    [string]$PreviewPath,
    [switch]$PrepareOnly,
    [string]$UploaderPath,
    [ValidatePattern('^[1-9][0-9]*$')][string]$WorkshopItemId,
    [switch]$RetryNewItem,
    [switch]$NoOpen
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo 'local/workshop-selectmod'
$workspace = Join-Path $root 'SelectMOD'
if (!$PrepareOnly -and (Get-Process -Name ModUploader -ErrorAction SilentlyContinue)) {
    throw 'A ModUploader process is still running. Wait for it to finish before retrying; it may still be creating or uploading the item.'
}
& (Join-Path $PSScriptRoot 'Prepare-Workshop.ps1') -PathsFile $PathsFile -PreviewPath $PreviewPath -NoOpen:$NoOpen
if ($PrepareOnly) { return }
# Share the preparation lock so another process cannot replace content during upload.
$guard = [IO.File]::Open((Join-Path $root 'prepare.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$report = Join-Path $repo 'local/workshop-upload-report.txt'
$idFile = Join-Path $workspace 'mod_id.txt'
$idBackup = Join-Path $root 'SelectMOD-item-id.txt'
$pending = Join-Path $root 'upload-pending.txt'
try {
    $knownIds = @()
    foreach ($file in @($idFile, $idBackup)) {
        if (Test-Path -LiteralPath $file) {
            $value = [IO.File]::ReadAllText($file).Trim()
            if ($value -notmatch '^[1-9][0-9]*$') { throw "Invalid Workshop ID in $file; retained for diagnosis." }
            $knownIds += $value
        }
    }
    if ($WorkshopItemId) { $knownIds += $WorkshopItemId }
    $knownIds = @($knownIds | Select-Object -Unique)
    if ($knownIds -contains '3842017022') { throw 'This ID belongs to the retired Workshop listing.' }
    if ($knownIds.Count -gt 1) { throw 'Conflicting Workshop item IDs. No upload performed.' }
    if ($knownIds.Count -eq 1) { [IO.File]::WriteAllText($idFile, $knownIds[0]) }
    elseif ((Test-Path -LiteralPath $pending) -and !$RetryNewItem) {
        throw 'A previous upload may have created an item before failing. Find its ID on your Steam Workshop page and retry with -WorkshopItemId. Only if no item exists, use -RetryNewItem.'
    }
    $names = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'content') -Force | Select-Object -ExpandProperty Name | Sort-Object)
    if (($names -join '|') -ne 'SelectMOD.dll|SelectMOD.json') { throw 'Unexpected Workshop payload. No upload performed.' }
    $metadata = Get-Content -LiteralPath (Join-Path $workspace 'workshop.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($metadata.visibility -ne 'unlisted') { throw 'Expected unlisted visibility. No upload performed.' }
    if (!$UploaderPath) {
        if ($env:OS -ne 'Windows_NT') { throw 'Automatic uploader setup requires Windows x64 with Steam running and logged in.' }
        $toolDir = Join-Path $repo 'local/tools/ModUploader-v0.2.0'
        $archive = Join-Path $repo 'local/tools/ModUploader-v0.2.0-win-x64.zip'
        New-Item -ItemType Directory -Path (Split-Path $archive -Parent) -Force | Out-Null
        if (!(Test-Path -LiteralPath $archive)) {
            # Immutable official release, verified against GitHub's published asset digest.
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/megacrit/sts2-mod-uploader/releases/download/v0.2.0/ModUploader-win-x64.zip' -OutFile ($archive + '.download')
            Move-Item -LiteralPath ($archive + '.download') -Destination $archive
        }
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '2B55C19CC5932235CA9DBD663CA07D04E5D5A0018402303199F9B4EC8CA06578') { throw 'Official uploader archive hash mismatch. No uploader executed.' }
        # Extract verified bytes each time rather than trusting a previously extracted executable.
        $toolDir = $toolDir + '-' + [guid]::NewGuid().ToString('N')
        Expand-Archive -LiteralPath $archive -DestinationPath $toolDir
        $executables = @(Get-ChildItem -LiteralPath $toolDir -Recurse -Filter ModUploader.exe -File)
        if ($executables.Count -ne 1) { throw 'Unexpected official uploader archive layout.' }
        $UploaderPath = $executables[0].FullName
    }
    $UploaderPath = [IO.Path]::GetFullPath($UploaderPath)
    if (!(Test-Path -LiteralPath $UploaderPath -PathType Leaf)) { throw 'Uploader executable not found.' }
    if (Test-Path -LiteralPath $report) { Copy-Item -LiteralPath $report -Destination ($report + '.previous') -Force }
    [IO.File]::WriteAllText($pending, "Upload requested UTC=$([DateTime]::UtcNow.ToString('O')); keep until item ID is known.")
    # The uploader resolves steam_appid.txt from its working directory.
    Push-Location (Split-Path $UploaderPath -Parent)
    try {
        Write-Host 'Uploading via Steam. Output is captured until the uploader exits; please wait.'
        $exitCode = Invoke-SelectModNativeProcess -FilePath $UploaderPath -Arguments @('upload', '-w', $workspace) -ReportPath $report
        if ($exitCode -ne 0) { throw "Official uploader failed (exit $exitCode). Keep workspace and pending marker; send local/workshop-upload-report.txt." }
    } finally { Pop-Location }
    if (!(Test-Path -LiteralPath $idFile)) { throw 'Uploader returned without mod_id.txt. Keep pending marker and inspect the Workshop page before retrying.' }
    $id = [IO.File]::ReadAllText($idFile).Trim()
    if ($id -notmatch '^[1-9][0-9]*$') { throw 'Uploader returned an invalid item ID.' }
    [IO.File]::WriteAllText($idBackup, $id)
    Remove-Item -LiteralPath $pending
    if (Select-String -LiteralPath $report -Pattern 'Failed to set (title|description|preview image)!' -Quiet) {
        throw "Steam rejected a listing field. Item ID $id was retained. Check local/workshop-upload-report.txt and rerun to update the same item."
    }
    Write-Host "Workshop item: https://steamcommunity.com/sharedfiles/filedetails/?id=$id"
    Write-Host 'Requested visibility: unlisted. Check the page visibility and any Steam Workshop agreement prompt before sharing the link.'
    if (!$NoOpen -and $env:OS -eq 'Windows_NT') { Start-Process explorer.exe -ArgumentList ('/select,"' + $idFile + '"') | Out-Null }
} catch {
    [IO.File]::AppendAllText($report, "`nFAILED: $($_.Exception.Message)`n", [Text.UTF8Encoding]::new($false))
    throw
} finally { $guard.Dispose() }
