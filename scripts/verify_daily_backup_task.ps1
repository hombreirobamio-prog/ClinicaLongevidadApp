[CmdletBinding()]
param(
    [ValidateRange(1, 168)]
    [int]$MaxAgeHours = 24
)

$ErrorActionPreference = 'Stop'

$taskPath = '\ClinicaLongevidadApp\'
$taskName = 'DailyAuthenticatedBackup'
$backupDirectory = Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadAppArtifacts\backups'
$checkedAt = Get-Date
$failures = [System.Collections.Generic.List[string]]::new()

try {
    $task = Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName
    $taskInfo = Get-ScheduledTaskInfo -TaskPath $taskPath -TaskName $taskName

    if ($taskInfo.LastTaskResult -ne 0) {
        $failures.Add("La última ejecución de la tarea devolvió el código $($taskInfo.LastTaskResult).")
    }

    if (-not $task.Settings.StartWhenAvailable) {
        $failures.Add('La tarea no está configurada para ejecutarse cuando se pierda la hora prevista.')
    }

    if ($task.Settings.DisallowStartIfOnBatteries -or $task.Settings.StopIfGoingOnBatteries) {
        $failures.Add('La tarea no está configurada para ejecutarse con batería.')
    }
}
catch {
    $task = $null
    $taskInfo = $null
    $failures.Add("No se pudo consultar la tarea programada: $($_.Exception.Message)")
}

$latestBackup = Get-ChildItem -LiteralPath $backupDirectory -Filter 'ClinicaLongevidad_backup_*.db' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latestBackup) {
    $failures.Add('No se encontró ninguna copia diaria en la carpeta central de backups.')
    $backupAgeHours = $null
    $sha256Matches = $false
    $hmacExists = $false
    $hmacVersionExists = $false
}
else {
    $backupAgeHours = [math]::Round(($checkedAt - $latestBackup.LastWriteTime).TotalHours, 2)
    if ($backupAgeHours -gt $MaxAgeHours) {
        $failures.Add("La última copia tiene $backupAgeHours horas; supera el límite de $MaxAgeHours horas.")
    }

    $sha256Path = "$($latestBackup.FullName).sha256"
    $hmacPath = "$($latestBackup.FullName).hmac"
    $hmacVersionPath = "$($latestBackup.FullName).hmac.ver"
    $hmacExists = Test-Path -LiteralPath $hmacPath -PathType Leaf
    $hmacVersionExists = Test-Path -LiteralPath $hmacVersionPath -PathType Leaf

    if (-not (Test-Path -LiteralPath $sha256Path -PathType Leaf)) {
        $sha256Matches = $false
        $failures.Add('Falta el comprobante SHA-256 de la última copia.')
    }
    else {
        $expectedSha256 = ((Get-Content -LiteralPath $sha256Path -Raw).Trim() -split '\s+')[0]
        $actualSha256 = (Get-FileHash -LiteralPath $latestBackup.FullName -Algorithm SHA256).Hash
        $sha256Matches = $expectedSha256 -ieq $actualSha256
        if (-not $sha256Matches) {
            $failures.Add('El SHA-256 de la última copia no coincide con su comprobante.')
        }
    }

    if (-not $hmacExists) {
        $failures.Add('Falta el comprobante HMAC de la última copia.')
    }

    if (-not $hmacVersionExists) {
        $failures.Add('Falta el comprobante de versión de clave HMAC de la última copia.')
    }
}

$result = [pscustomobject]@{
    CheckedAt = $checkedAt.ToString('o')
    Healthy = $failures.Count -eq 0
    Task = [pscustomobject]@{
        Path = "$taskPath$taskName"
        LastRunTime = if ($null -eq $taskInfo) { $null } else { $taskInfo.LastRunTime.ToString('o') }
        NextRunTime = if ($null -eq $taskInfo) { $null } else { $taskInfo.NextRunTime.ToString('o') }
        LastTaskResult = if ($null -eq $taskInfo) { $null } else { $taskInfo.LastTaskResult }
        StartWhenAvailable = if ($null -eq $task) { $null } else { $task.Settings.StartWhenAvailable }
        AllowsBatteryRun = if ($null -eq $task) { $null } else { -not $task.Settings.DisallowStartIfOnBatteries -and -not $task.Settings.StopIfGoingOnBatteries }
    }
    LatestBackup = [pscustomobject]@{
        Path = if ($null -eq $latestBackup) { $null } else { $latestBackup.FullName }
        CreatedAt = if ($null -eq $latestBackup) { $null } else { $latestBackup.LastWriteTime.ToString('o') }
        AgeHours = $backupAgeHours
        Sha256Matches = $sha256Matches
        HmacExists = $hmacExists
        HmacVersionExists = $hmacVersionExists
    }
    Failures = $failures
}

$result | ConvertTo-Json -Depth 4
if ($failures.Count -gt 0) { exit 1 }
