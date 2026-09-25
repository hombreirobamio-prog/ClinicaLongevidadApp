param(
    [string] $ConnectionString = '',
    [switch] $UseLocalAppDataDb,
    [string] $ProjectPath = 'tools/RunAuditDiagnostics',
    [int] $TimeoutSeconds = 300
)

# Default DB path in LocalAppData
if ($UseLocalAppDataDb -and [string]::IsNullOrWhiteSpace($ConnectionString)) {
    $localDb = Join-Path -Path (Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadApp') -ChildPath 'ClinicaLongevidad.db'
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
$proc = Start-Process -FilePath 'dotnet' -ArgumentList "run --project $ProjectPath -- $ConnectionString" -NoNewWindow -RedirectStandardOutput stdout.txt -RedirectStandardError stderr.txt -PassThru

$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not $proc.HasExited -and $sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    Start-Sleep -Milliseconds 200
}

if (-not $proc.HasExited) {
    Write-Warning "Process did not finish within timeout; killing."
    try { $proc.Kill() } catch { }
}

# Show outputs
if (Test-Path stdout.txt) {
    Write-Output "--- STDOUT ---"
    Get-Content stdout.txt -Tail 200
}
if (Test-Path stderr.txt) {
    Write-Output "--- STDERR ---"
    Get-Content stderr.txt -Tail 200
}

# Identify generated artifacts
$localLogs = Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadApp\logs'
$progData = Join-Path $env:ProgramData 'ClinicaLongevidadApp\AuditIntegrityReports'

Write-Output "Looking for generated artifacts..."
if (Test-Path $localLogs) {
    Get-ChildItem -Path $localLogs -Filter 'IntegrityQuickSummary_*.txt' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
    Get-ChildItem -Path $localLogs -Filter 'IntegrityProblemRows_*.csv' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
    Get-ChildItem -Path $localLogs -Filter 'backup.log' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
}
if (Test-Path $progData) {
    Get-ChildItem -Path $progData -Filter 'IntegrityReport_*.json' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output "Found: $($_.FullName)" }
}

Write-Output "Diagnostics run complete. If you want, paste the stdout and any artifact paths here and I will analyze and draft the session entry."
