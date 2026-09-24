param(
    [string]$Path = "C:\ProgramData\ClinicaLongevidadApp\AuditIntegrityReports\IntegrityReport_20260924_110728_id1.json",
    [int]$MaxErrors = 20,
    [int]$MaxSurround = 10
)
if (-not (Test-Path $Path)) { Write-Error "Report not found: $Path"; exit 2 }
try {
    $text = Get-Content -Raw -LiteralPath $Path
    $j = $text | ConvertFrom-Json
} catch {
    Write-Error "Failed to read or parse JSON: $_"
    exit 3
}
$errs = $j.Errors
$errsCount = if ($errs -ne $null) { $errs.Count } else { 0 }
Write-Output "ErrorsCount: $errsCount"
if ($errsCount -gt 0) {
    $take = [Math]::Min($MaxErrors, $errsCount)
    Write-Output "First $take errors:"
    0..($take-1) | ForEach-Object { Write-Output "- $($errs[$_])" }
}
$sur = $j.SurroundingRows
$surCount = if ($sur -ne $null) { $sur.Count } else { 0 }
Write-Output "SurroundingRows: $surCount"
if ($surCount -gt 0) {
    $takeS = [Math]::Min($MaxSurround, $surCount)
    Write-Output "First $takeS surrounding rows (Id, UsuarioAdmin, Accion, FechaHora):"
    for ($i=0; $i -lt $takeS; $i++) {
        $r = $sur[$i]
        Write-Output "Id=$($r.Id) UsuarioAdmin=$($r.UsuarioAdmin) Accion=$($r.Accion) FechaHora=$($r.FechaHora)"
    }
}
Write-Output "ReportPath: $Path"
exit 0
