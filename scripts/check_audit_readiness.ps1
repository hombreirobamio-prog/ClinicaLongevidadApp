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

# 1) dotnet test
Write-Output "\n-- Running unit tests (dotnet test) --"
$tests = dotnet test -v minimal
Write-Output $tests

# 2) Check backup.log (prefer -ArtifactsDir, then ProgramData, then LOCALAPPDATA)
if (-not [string]::IsNullOrWhiteSpace($ArtifactsDir) -and (Test-Path $ArtifactsDir)) {
    $log = Join-Path $ArtifactsDir 'logs\backup.log'
}
elseif (Test-Path (Join-Path $env:ProgramData 'ClinicaLongevidadAppArtifacts\logs')) {
    $log = Join-Path (Join-Path $env:ProgramData 'ClinicaLongevidadAppArtifacts') 'logs\backup.log'
}
else {
    $log = Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadApp\logs\backup.log'
}
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
                if (Test-Path $sha) { Write-Output ("    -> " + (Get-Content $sha -Raw)) }
                Write-Output ("  HMAC present: " + (Test-Path $hmac))
                if (Test-Path $hmac) { Write-Output ("    -> " + (Get-Content $hmac -Raw)) }
                Write-Output ("  HMAC.ver present: " + (Test-Path $hmacver))
            } else {
                Write-Output "  DB exists: NO (directory may have been removed)"
            }
        }
    }
}

# 4) Check audit-artifacts zip in repo
$repoZip = Get-ChildItem -Path (Get-Location) -Filter "audit-artifacts_*.zip" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($repoZip) { Write-Output "\n-- Found audit artifact zip: $($repoZip.FullName)" } else { Write-Output "\n-- No audit artifact zip found in repo root." }

Write-Output "\n== Check finished =="

# Exit code: 0
exit 0
