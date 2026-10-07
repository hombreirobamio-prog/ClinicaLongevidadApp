param(
    [string] $ConnectionString = '',
    [switch] $UseLocalAppDataDb,
    [string] $ArtifactsDir = '',
    [string] $ProjectPath = 'tools/RunAuditDiagnostics',
    [int] $TimeoutSeconds = 300,
    [switch] $Help
)

if ($Help) {
    Write-Output "Usage: .\scripts\run_audit_diagnostics.ps1 [-ConnectionString 'Data Source=...'] [-UseLocalAppDataDb] [-ArtifactsDir 'C:\Artifacts'] [-ProjectPath tools/RunAuditDiagnostics] [-TimeoutSeconds 300]"
    exit 0
}

# Default DB path when -UseLocalAppDataDb is supplied and no ConnectionString provided.
$artifactRoot = if (-not [string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir
} else {
    Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadAppArtifacts'
}
if ($UseLocalAppDataDb -and [string]::IsNullOrWhiteSpace($ConnectionString)) {
    $localDb = Join-Path -Path $artifactRoot -ChildPath 'ClinicaLongevidad.db'
    $ConnectionString = "Data Source=$localDb"
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Error "ConnectionString is required. Use -ConnectionString or -UseLocalAppDataDb.`nExample: .\scripts\run_audit_diagnostics.ps1 -UseLocalAppDataDb"
    exit 2
}

# Build the tool
Write-Output "Building RunAuditDiagnostics..."
dotnet build $ProjectPath -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet build failed"; exit $LASTEXITCODE }

# Run the tool
Write-Output "Running diagnostics (timeout ${TimeoutSeconds}s)..."
$diagnosticsDir = Join-Path $artifactRoot 'reports\diagnostics'
New-Item -ItemType Directory -Path $diagnosticsDir -Force | Out-Null
$runId = Get-Date -Format 'yyyyMMdd_HHmmss'
$stdoutPath = Join-Path $diagnosticsDir "run_audit_diagnostics_${runId}.stdout.txt"
$stderrPath = Join-Path $diagnosticsDir "run_audit_diagnostics_${runId}.stderr.txt"
$proc = Start-Process -FilePath 'dotnet' -ArgumentList "run --project $ProjectPath -- $ConnectionString" -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru

$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not $proc.HasExited -and $sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    Start-Sleep -Milliseconds 200
}

$timedOut = $false
if (-not $proc.HasExited) {
    Write-Warning "Process did not finish within timeout; killing."
    $timedOut = $true
    try { $proc.Kill(); $proc.WaitForExit() } catch { }
}

# Show outputs
if (Test-Path $stdoutPath) {
    Write-Output "--- STDOUT ---"
    Get-Content $stdoutPath -Tail 200
}
if (Test-Path $stderrPath) {
    Write-Output "--- STDERR ---"
    Get-Content $stderrPath -Tail 200
}

# Identify generated artifacts under the active artifacts root.
$localLogs = Join-Path $artifactRoot 'logs'
$integrityReports = Join-Path $artifactRoot 'reports\integrity'
$diagnosticReports = Join-Path $artifactRoot 'reports\diagnostics'

Write-Output "Looking for generated artifacts..."
if (Test-Path $localLogs) {
    Get-ChildItem -Path $localLogs -Filter 'IntegrityQuickSummary_*.txt' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
    Get-ChildItem -Path $localLogs -Filter 'IntegrityProblemRows_*.csv' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
    Get-ChildItem -Path $localLogs -Filter 'backup.log' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
}
if (Test-Path $integrityReports) {
    Get-ChildItem -Path $integrityReports -Filter 'IntegrityReport_*.json' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
}
if (Test-Path $diagnosticReports) {
    Get-ChildItem -Path $diagnosticReports -Filter 'IntegrityQuickSummary_*.txt' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
    Get-ChildItem -Path $diagnosticReports -Filter 'IntegrityProblemRows_*.csv' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
}

if ($timedOut) {
    Write-Error "Diagnostics timed out after ${TimeoutSeconds}s."
    exit 124
}
if ($proc.ExitCode -ne 0) {
    Write-Error "Diagnostics failed with exit code $($proc.ExitCode)."
    exit $proc.ExitCode
}

Write-Output "Diagnostics run complete. If you want, paste the stdout and any artifact paths here and I will analyze and draft the session entry."
exit 0
