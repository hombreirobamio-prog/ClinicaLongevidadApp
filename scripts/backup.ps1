param(
    [string]$BaseDir = (Get-Location).Path,
    [string]$LocalAppData = $env:LOCALAPPDATA
)

$dt = Get-Date -Format 'yyyyMMdd_HHmmss'
$cwd = $BaseDir
$backupDir = Join-Path $cwd 'backups'
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

# Also copy the generated ZIP to the user's LocalAppData backups folder so the app can detect it
try {
    $localDest = if ([string]::IsNullOrWhiteSpace($LocalAppData)) { Join-Path $cwd 'backups' } else { Join-Path $LocalAppData 'ClinicaLongevidadApp\backups' }
    New-Item -ItemType Directory -Force -Path $localDest | Out-Null
    Copy-Item -Path $dest -Destination $localDest -Force
    $localFile = Join-Path $localDest (Split-Path $dest -Leaf)
    if (Test-Path $localFile) {
        Write-Output "COPIED_TO_LOCALAPPDATA:$localFile"
    }
    else {
        Write-Warning "Copy reported success but file not found at destination: $localFile"
    }
}
catch {
    Write-Warning "Failed to copy backup to LocalAppData: $($_.Exception.Message)"
}
