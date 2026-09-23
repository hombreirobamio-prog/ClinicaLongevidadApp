param(
    [string]$ReleaseTag = "audit-rewrite-8684b20",
    [switch]$UploadToRelease,
    [int]$TimeoutSeconds = 90
)

Write-Host "Generating audit artifacts..."

$start = Get-Date
$env:FORCE_ADMIN = '1'
$env:SILENT_MODE = '1'

# Start the WPF app in background (it runs self-check when FORCE_ADMIN=1)
$dotnetArgs = "run --project ClinicaLongevidadApp.csproj --configuration Release --no-launch-profile"
Write-Host "Starting app: dotnet $dotnetArgs"
$proc = Start-Process -FilePath dotnet -ArgumentList $dotnetArgs -PassThru

# Paths to watch
$localLogs = Join-Path $env:LOCALAPPDATA "ClinicaLongevidadApp\logs"
$commonReports = Join-Path $env:ProgramData "ClinicaLongevidadApp\AuditIntegrityReports"
$backups = Join-Path $env:LOCALAPPDATA "ClinicaLongevidadApp\backups"

Write-Host "Waiting for diagnostics (timeout: $TimeoutSeconds s)..."
$end = $start.AddSeconds($TimeoutSeconds)
$found = $false
while ((Get-Date) -lt $end) {
    try {
        if (Test-Path $commonReports) {
            if ((Get-ChildItem -Path $commonReports -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $start }).Count -gt 0) { $found = $true; break }
        }
        if (Test-Path $localLogs) {
            if ((Get-ChildItem -Path $localLogs -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $start }).Count -gt 0) { $found = $true; break }
        }
    } catch { }
    Start-Sleep -Seconds 2
}

# Attempt to stop the started app
try {
    if ($proc -and !$proc.HasExited) {
        Write-Host "Stopping app (pid $($proc.Id))..."
        $proc | Stop-Process -Force
    }
} catch { }

if (-not $found) {
    Write-Warning "No diagnostics files detected within timeout. Still proceeding to package available artifacts."
}

# Prepare artifact zip
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$zipName = "audit-artifacts_$ts.zip"
$zipPath = Join-Path (Get-Location) $zipName

# Collect files
$filesToZip = @()
if (Test-Path $localLogs) { $filesToZip += Get-ChildItem -Path $localLogs -Recurse -File -ErrorAction SilentlyContinue }
if (Test-Path $commonReports) { $filesToZip += Get-ChildItem -Path $commonReports -Recurse -File -ErrorAction SilentlyContinue }
if (Test-Path $backups) { $filesToZip += Get-ChildItem -Path $backups -Recurse -File -ErrorAction SilentlyContinue }

if ($filesToZip.Count -eq 0) {
    Write-Warning "No files found to include in the artifact zip."
} else {
    Write-Host "Creating zip with $($filesToZip.Count) files -> $zipPath"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    # Use Compress-Archive
    try {
        $tempList = $filesToZip | ForEach-Object { $_.FullName }
        Compress-Archive -LiteralPath $tempList -DestinationPath $zipPath -Force
    } catch {
        Write-Warning "Compress-Archive failed: $_. Exception. Trying fallback per-folder packaging."
        # Fallback: copy directories to temp and zip
        $tmp = Join-Path $env:TEMP "audit_artifacts_$ts"
        if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
        New-Item -ItemType Directory -Path $tmp | Out-Null
        if (Test-Path $localLogs) { Copy-Item -Path $localLogs -Destination $tmp -Recurse -Force -ErrorAction SilentlyContinue }
        if (Test-Path $commonReports) { Copy-Item -Path $commonReports -Destination $tmp -Recurse -Force -ErrorAction SilentlyContinue }
        if (Test-Path $backups) { Copy-Item -Path $backups -Destination $tmp -Recurse -Force -ErrorAction SilentlyContinue }
        Compress-Archive -LiteralPath (Join-Path $tmp '*') -DestinationPath $zipPath -Force
        Remove-Item $tmp -Recurse -Force
    }
}

Write-Host "Artifact created: $zipPath"

if ($UploadToRelease) {
    # detect repo from git remote
    try {
        $remote = git config --get remote.origin.url 2>$null
        if (-not $remote) { throw "Cannot detect git remote origin url" }
        $repo = $null
        if ($remote -match "github.com[:/](.+?)(\.git)?$") { $repo = $matches[1] }
        if (-not $repo) { throw "Could not parse repo from remote: $remote" }
        Write-Host "Detected repo: $repo"

        Write-Host "Uploading $zipPath to release $ReleaseTag ..."
        gh release upload $ReleaseTag $zipPath --repo $repo --clobber
        Write-Host "Upload complete."
    } catch {
        Write-Warning "Upload failed: $_"
    }
}

Write-Host "Done."
return @{ Zip = $zipPath }
