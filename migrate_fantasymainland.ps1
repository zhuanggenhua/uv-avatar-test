param(
    [string]$Source = "D:\gameObject\project\FantasyMainland",
    [string]$Destination = $PSScriptRoot,
    [switch]$Execute,
    [switch]$Mirror,
    [ValidateSet("Equipment", "Full")]
    [string]$Scope = "Equipment",
    [switch]$IncludeRenderPipeline
)

$ErrorActionPreference = "Stop"

function Test-IsUnityProjectRoot {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $required = @("Assets", "Packages", "ProjectSettings")
    foreach ($dir in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $dir))) {
            return $false
        }
    }

    return $true
}

function Resolve-UnityProjectRoot {
    param([string]$Path)

    if (Test-IsUnityProjectRoot -Path $Path) {
        return (Get-Item -LiteralPath $Path).FullName
    }

    # Some upgrade directories wrap the real project in one more folder.
    $candidates = Get-ChildItem -LiteralPath $Path -Directory -Recurse -Depth 2 -ErrorAction SilentlyContinue |
        Where-Object { Test-IsUnityProjectRoot -Path $_.FullName } |
        Select-Object -ExpandProperty FullName

    if (-not $candidates -or $candidates.Count -eq 0) {
        throw "No Unity project root found under source path: $Path"
    }

    if ($candidates.Count -gt 1) {
        throw "Multiple Unity project roots found under source path. Please pass the exact one. Found: $($candidates -join '; ')"
    }

    return $candidates[0]
}

function Write-LogLine {
    param(
        [string]$Text,
        [string]$LogFile
    )

    $Text | Tee-Object -FilePath $LogFile -Append
}

function Invoke-RobocopyChecked {
    param(
        [string]$SourceDir,
        [string]$DestinationDir,
        [string]$LogFile,
        [switch]$UseMirror,
        [switch]$DryRun
    )

    $args = @($SourceDir, $DestinationDir)
    if ($UseMirror) {
        $args += "/MIR"
    } else {
        $args += "/E"
    }
    $args += "/R:2"
    $args += "/W:1"
    $args += "/FFT"
    $args += "/Z"
    $args += "/NP"
    $args += "/NDL"

    if ($DryRun) {
        $args += "/L"
    }

    & robocopy @args | Tee-Object -FilePath $LogFile -Append | Out-Null
    $rc = $LASTEXITCODE
    if ($rc -gt 7) {
        throw "Robocopy failed ($rc) for $SourceDir => $DestinationDir"
    }
}

function Copy-RelativeFile {
    param(
        [string]$RelativePath,
        [string]$SourceRoot,
        [string]$DestinationRoot,
        [string]$LogFile,
        [switch]$DryRun
    )

    $sourceFile = Join-Path $SourceRoot $RelativePath
    if (-not (Test-Path -LiteralPath $sourceFile)) {
        Write-LogLine "[SKIP] file missing in source: $RelativePath" $LogFile
        return
    }

    $destinationFile = Join-Path $DestinationRoot $RelativePath
    $destinationDir = Split-Path -Parent $destinationFile

    if ($DryRun) {
        Write-LogLine "[DRYRUN] file: $RelativePath" $LogFile
        return
    }

    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    Copy-Item -LiteralPath $sourceFile -Destination $destinationFile -Force
    Write-LogLine "[COPY] file: $RelativePath" $LogFile
}

function Copy-RelativeDirectory {
    param(
        [string]$RelativePath,
        [string]$SourceRoot,
        [string]$DestinationRoot,
        [string]$LogFile,
        [switch]$UseMirror,
        [switch]$DryRun
    )

    $sourceDir = Join-Path $SourceRoot $RelativePath
    if (-not (Test-Path -LiteralPath $sourceDir)) {
        Write-LogLine "[SKIP] dir missing in source: $RelativePath" $LogFile
        return
    }

    $destinationDir = Join-Path $DestinationRoot $RelativePath
    Write-LogLine "[SYNC] dir: $RelativePath" $LogFile
    Invoke-RobocopyChecked -SourceDir $sourceDir -DestinationDir $destinationDir -LogFile $LogFile -UseMirror:$UseMirror -DryRun:$DryRun
}

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source path not found: $Source"
}
if (-not (Test-Path -LiteralPath $Destination)) {
    throw "Destination path not found: $Destination"
}

$sourceRoot = Resolve-UnityProjectRoot -Path $Source
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPath = Join-Path $Destination "migration_fantasymainland_$timestamp.log"

Write-Host "Source input: $Source"
Write-Host "Source root: $sourceRoot"
Write-Host "Destination: $Destination"
Write-Host "Scope: $Scope"
Write-Host "Mode: $(if ($Execute) { if ($Mirror) { 'EXECUTE + MIRROR (deletes in synced scope)' } else { 'EXECUTE (copy only)' } } else { 'DRY RUN' })"
Write-Host "Log: $logPath"
Write-Host ""

"==== Migration Start $(Get-Date -Format s) ====" | Out-File -FilePath $logPath -Encoding utf8
"Source root: $sourceRoot" | Out-File -FilePath $logPath -Append -Encoding utf8
"Destination: $Destination" | Out-File -FilePath $logPath -Append -Encoding utf8
"Scope: $Scope" | Out-File -FilePath $logPath -Append -Encoding utf8

$isDryRun = -not $Execute

if ($Scope -eq "Full") {
    $excludeDirs = @("Library", "Logs", "Temp", "obj", ".git", ".vs", "UserSettings")
    $excludeFiles = @("*.csproj", "*.sln", "*.pidb", "*.user")

    $args = @($sourceRoot, $Destination)
    if ($Mirror) { $args += "/MIR" } else { $args += "/E" }
    $args += "/R:2"
    $args += "/W:1"
    $args += "/FFT"
    $args += "/Z"
    $args += "/NP"
    $args += "/NDL"
    if ($isDryRun) { $args += "/L" }
    $args += "/XD"
    $args += $excludeDirs
    $args += "/XF"
    $args += $excludeFiles

    & robocopy @args | Tee-Object -FilePath $logPath -Append | Out-Null
    $rc = $LASTEXITCODE
    if ($rc -gt 7) {
        throw "Robocopy failed ($rc) for full sync. Check log: $logPath"
    }
} else {
    $equipmentDirs = @(
        "Assets/Scripts/EquipmentSystem",
        "Assets/Data/Appearance",
        "Assets/Data/FrameData",
        "Assets/Data/Equip",
        "Assets/Art/equip"
    )

    foreach ($relativeDir in $equipmentDirs) {
        Copy-RelativeDirectory -RelativePath $relativeDir -SourceRoot $sourceRoot -DestinationRoot $Destination -LogFile $logPath -UseMirror:$Mirror -DryRun:$isDryRun
    }

    $optionalFiles = @(
        "Assets/Art/equip.meta",
        "Assets/Data/Appearance.meta",
        "Assets/Data/FrameData.meta",
        "Assets/Data/Equip.meta",
        "Assets/Scripts/EquipmentSystem.meta"
    )

    if ($IncludeRenderPipeline) {
        $optionalFiles += @(
            "Assets/Settings/Renderer2D.asset",
            "Assets/Settings/Renderer2D.asset.meta",
            "Assets/Settings/UniversalRP.asset",
            "Assets/Settings/UniversalRP.asset.meta",
            "Assets/UniversalRenderPipelineGlobalSettings.asset",
            "Assets/UniversalRenderPipelineGlobalSettings.asset.meta",
            "ProjectSettings/GraphicsSettings.asset",
            "Packages/manifest.json",
            "Packages/packages-lock.json"
        )
    }

    foreach ($relativeFile in $optionalFiles) {
        Copy-RelativeFile -RelativePath $relativeFile -SourceRoot $sourceRoot -DestinationRoot $Destination -LogFile $logPath -DryRun:$isDryRun
    }

    $uvBase = "Assets/Art/MINIFANTASY Creatures - Super Low Res 2D Pixel Art by Krishna Palacio/Sprites/Humanoids"
    $uvBasePath = Join-Path $sourceRoot $uvBase
    if (Test-Path -LiteralPath $uvBasePath) {
        $uvFiles = Get-ChildItem -LiteralPath $uvBasePath -Recurse -File |
            Where-Object { $_.Name -like "*UV*" }

        foreach ($file in $uvFiles) {
            $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
            Copy-RelativeFile -RelativePath $relative -SourceRoot $sourceRoot -DestinationRoot $Destination -LogFile $logPath -DryRun:$isDryRun
        }
    } else {
        Write-LogLine "[SKIP] UV base missing: $uvBase" $logPath
    }
}

Write-Host ""
Write-Host "Migration finished. Log: $logPath"
