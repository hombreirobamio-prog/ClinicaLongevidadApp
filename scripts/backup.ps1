param(
    [string]$BaseDir = (Get-Location).Path,
    [string]$LocalAppData = $env:LOCALAPPDATA,
    [string]$ArtifactsDir = '',
    [switch]$Help
)

if ($Help) {
    Write-Output "Usage: .\scripts\backup.ps1 [-BaseDir <path>] [-ArtifactsDir <path>]"
    Write-Output "Creates a ZIP backup under the active artifacts backups folder."
    exit 0
}

$dt = Get-Date -Format 'yyyyMMdd_HHmmss'
$cwd = $BaseDir
$artifactRoot = if (-not [string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir
} elseif (-not [string]::IsNullOrWhiteSpace($LocalAppData)) {
    Join-Path $LocalAppData 'ClinicaLongevidadAppArtifacts'
} else {
    Join-Path $cwd 'artifacts'
}
$backupDir = Join-Path $artifactRoot 'backups'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
 $dest = Join-Path $backupDir ("ClinicaLongevidadApp_backup_$dt.zip")

# Collect files to include in backup, excluding common developer/temp folders and files that are locked
$excludeDirs = @('.git', '.vs', '\bin\', '\obj\', 'artifacts', 'node_modules')

Write-Output "Scanning files under: $cwd"
$allFiles = Get-ChildItem -Path $cwd -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {
    $full = $_.FullName
    # exclude the backups output folder itself
    if ($full -like "$backupDir*") { return $false }
    foreach ($ex in $excludeDirs) { if ($full -like "*$ex*") { return $false } }
    return $true
}

$valid = New-Object System.Collections.Generic.List[string]
foreach ($f in $allFiles) {
    try {
        # Try open read to detect locked files
        $stream = [System.IO.File]::Open($f.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        $stream.Close()
        $valid.Add($f.FullName)
    }
    catch {
        Write-Warning "Skipping locked or inaccessible file: $($f.FullName) -> $($_.Exception.Message)"
    }
}

if ($valid.Count -eq 0) {
    throw 'No valid files found to include in the backup.'
}

try {
    Compress-Archive -LiteralPath $valid -DestinationPath $dest -Force -ErrorAction Stop
}
catch {
    Write-Warning "Compress-Archive failed: $($_.Exception.Message)"
    throw
}

Write-Output "BACKUP_CREATED:$dest"

# Also attempt to copy recent audit logs into the artifacts logs folder so tools can find diagnostics
try {
    $targetLogs = Join-Path $artifactRoot 'logs'

    New-Item -ItemType Directory -Force -Path $targetLogs | Out-Null

    # Build candidate paths (avoid Join-Path producing arrays when inputs are unexpected)
    $candidates = @()
    if ($targetLogs) {
        $candidates += "$targetLogs\backup.log"
        $candidates += "$targetLogs\backup_vm.log"
    }

    $copiedAny = $false
    foreach ($path in $candidates | Get-Unique) {
        if (Test-Path $path) {
            try {
                Copy-Item -Path $path -Destination (Join-Path $targetLogs (Split-Path $path -Leaf)) -Force
                Write-Output "COPIED_LOG:$path -> $targetLogs"
                $copiedAny = $true
            }
            catch { }
        }
    }
    if (-not $copiedAny) {
        Write-Output "No backup logs found to copy into artifacts logs ($targetLogs)" | Out-Null
    }
}
catch {
    Write-Warning "Failed to copy backup logs into artifacts folder: $($_.Exception.Message)"
}
