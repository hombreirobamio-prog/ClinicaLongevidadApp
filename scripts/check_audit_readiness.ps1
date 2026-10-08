# PowerShell script: check_audit_readiness.ps1
# Ejecuta comprobaciones básicas para auditoría y muestra un resumen.

param(
    [string]$ArtifactsDir = '',
    [switch]$Help
)

if ($Help) {
    Write-Output "Usage: .\scripts\check_audit_readiness.ps1 [-ArtifactsDir <path>]"
    Write-Output "Performs basic audit readiness checks and inspects recent backup artifacts."
    exit 0
}

Write-Output "== ClinicaLongevidadApp: Audit Readiness Check =="
$readinessProblems = @()

# 1) dotnet test
Write-Output "\n-- Running Release unit tests (dotnet test) --"
$tests = & dotnet test --configuration Release -v minimal
$testExitCode = $LASTEXITCODE
Write-Output $tests
if ($testExitCode -ne 0) {
    Write-Error "Unit tests failed with exit code $testExitCode."
    exit $testExitCode
}

# 2) Check backup.log in the active artifacts root.
$artifactRoot = if (-not [string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir
} else {
    Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadAppArtifacts'
}
$log = Join-Path $artifactRoot 'logs\backup.log'
if (Test-Path $log) {
    Write-Output "\n-- Found backup.log: $log --"
    $created = Select-String -Path $log -Pattern "TriggerImmediateBackup created:" | Select-Object -Last 5
    if ($created) {
        Write-Output "Last backups found:"
        $created | ForEach-Object { Write-Output $_.Line }
    } else { Write-Output "No 'TriggerImmediateBackup created' entries found." }
} else { Write-Output "backup.log not found at $log" }

# 3) For each recent backup check artifacts
if ($created) {
    Write-Output "\n-- Checking artifacts for recent backups --"
    foreach ($c in $created) {
        $line = $c.Line
        $parts = $line -split 'TriggerImmediateBackup created:'
        if ($parts.Length -gt 1) {
            $path = $parts[1].Trim()
            Write-Output "Checking: $path"
            $dir = Split-Path $path -Parent
            if (Test-Path $path) {
                Write-Output "  DB exists: yes"
                $sha = "$path.sha256"
                $hmac = "$path.hmac"
                $hmacver = "$path.hmac.ver"
                Write-Output ("  Size: " + (Get-Item $path).Length + " bytes")
                Write-Output ("  SHA present: " + (Test-Path $sha))
                Write-Output ("  HMAC present: " + (Test-Path $hmac))
                Write-Output ("  HMAC.ver present: " + (Test-Path $hmacver))
                if (-not (Test-Path $sha)) { $readinessProblems += "Missing SHA-256 evidence: $sha" }
                if (-not (Test-Path $hmac)) { $readinessProblems += "Missing HMAC evidence: $hmac" }
                if (-not (Test-Path $hmacver)) { $readinessProblems += "Missing HMAC version evidence: $hmacver" }
            } else {
                Write-Output "  DB exists: NO (directory may have been removed)"
                $readinessProblems += "Backup reported in log is missing: $path"
            }
        }
    }
}

# 4) Check audit artifact ZIP in the active artifacts root.
$auditArtifactsDir = Join-Path $artifactRoot 'audit_artifacts'
$auditZip = Get-ChildItem -Path $auditArtifactsDir -Filter "audit-artifacts_*.zip" -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($auditZip) { Write-Output "\n-- Found audit artifact zip: $($auditZip.FullName)" } else { Write-Output "\n-- No audit artifact zip found in $auditArtifactsDir." }

if ($readinessProblems.Count -gt 0) {
    Write-Error ("Audit readiness failed:`n - " + ($readinessProblems -join "`n - "))
    exit 1
}

Write-Output "\n== Check finished =="

# Exit code: 0
exit 0
