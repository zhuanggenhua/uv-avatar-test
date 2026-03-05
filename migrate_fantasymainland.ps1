param(
    [string]$Source = "D:\gameObject\project\FantasyMainland",
    [string]$Destination = $PSScriptRoot,
    [switch]$Execute,
    [switch]$Mirror
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source path not found: $Source"
}

if (-not (Test-Path -LiteralPath $Destination)) {
    throw "Destination path not found: $Destination"
}

$requiredUnityDirs = @("Assets", "Packages", "ProjectSettings")
$missingUnityDirs = @()
foreach ($dir in $requiredUnityDirs) {
    if (-not (Test-Path -LiteralPath (Join-Path $Source $dir))) {
        $missingUnityDirs += $dir
    }
}

if ($missingUnityDirs.Count -gt 0) {
    throw "Source does not look like a complete Unity project. Missing: $($missingUnityDirs -join ', ')"
}

$excludeDirs = @(
    "Library",
    "Logs",
    "Temp",
    "obj",
    ".git",
    ".vs",
    "UserSettings"
)

$excludeFiles = @(
    "*.csproj",
    "*.sln",
    "*.pidb",
    "*.user"
)

$robocopyArgs = @()
if ($Mirror) {
    $robocopyArgs += "/MIR"
} else {
    $robocopyArgs += "/E"
}
$robocopyArgs += "/R:2"
$robocopyArgs += "/W:1"
$robocopyArgs += "/FFT"
$robocopyArgs += "/Z"
$robocopyArgs += "/NP"
$robocopyArgs += "/NDL"
$robocopyArgs += "/TEE"

if (-not $Execute) {
    $robocopyArgs += "/L"
}

$robocopyArgs += "/XD"
$robocopyArgs += $excludeDirs
$robocopyArgs += "/XF"
$robocopyArgs += $excludeFiles

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPath = Join-Path $Destination "migration_fantasymainland_$timestamp.log"
$robocopyArgs += "/LOG:$logPath"

Write-Host "Source: $Source"
Write-Host "Destination: $Destination"
Write-Host "Mode: $(if ($Execute) { if ($Mirror) { 'EXECUTE + MIRROR (can delete files)' } else { 'EXECUTE (copy only)' } } else { 'DRY RUN' })"
Write-Host "Log: $logPath"
Write-Host ""

& robocopy $Source $Destination @robocopyArgs
$exitCode = $LASTEXITCODE

# Robocopy exit codes 0-7 are successful outcomes.
if ($exitCode -le 7) {
    Write-Host ""
    Write-Host "Robocopy finished successfully with code $exitCode."
    exit 0
}

throw "Robocopy failed with exit code $exitCode. Check log: $logPath"
