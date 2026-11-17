[CmdletBinding()]
param(
    [string]$PathsFile,
    [string]$ProjectPath,
    [string]$GameDataDir
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
if (!$ProjectPath) { $ProjectPath = Join-Path $repo 'src/SelectMOD/SelectMOD.csproj' }
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
if (!(Test-Path -LiteralPath $ProjectPath -PathType Leaf)) {
    throw 'SelectMOD project is not implemented yet. Build.ps1 is a reusable build entry point, not a working MOD or installer.'
}
if (!$GameDataDir) {
    $paths = Get-SelectModPaths -PathsFile $PathsFile
    $GameDataDir = Split-Path (Get-SelectModGameAssembly -Game $paths.Game) -Parent
}
$GameDataDir = [IO.Path]::GetFullPath($GameDataDir)
foreach ($name in @('sts2.dll', '0Harmony.dll', 'GodotSharp.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $GameDataDir $name) -PathType Leaf)) { throw "Missing game-owned reference: $name" }
}
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the SDK selected by the project before building.' }
Push-Location $repo
try {
    & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'SDK selection failed. Check global.json and installed SDKs.' }
    & dotnet build $ProjectPath -c Release "-p:GameDataDir=$GameDataDir" "-p:GameAssemblyPath=$(Join-Path $GameDataDir 'sts2.dll')" "-p:HarmonyAssemblyPath=$(Join-Path $GameDataDir '0Harmony.dll')" "-p:GodotAssemblyPath=$(Join-Path $GameDataDir 'GodotSharp.dll')"
    if ($LASTEXITCODE -ne 0) { throw 'Game-referenced build failed. No installation was performed.' }
    Write-Host 'Build completed. No installation or distribution staging was performed; runtime acceptance is separate.'
} finally { Pop-Location }
