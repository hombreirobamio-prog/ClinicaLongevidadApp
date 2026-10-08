[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^([01]?\d|2[0-3]):[0-5]\d$')]
    [string]$Time = '19:17',

    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$taskName = 'ClinicaLongevidadApp\DailyAuthenticatedBackup'
if ($Remove) {
    schtasks.exe /Delete /TN $taskName /F
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo eliminar la tarea programada.' }
    Write-Output 'Tarea diaria de copia eliminada.'
    exit 0
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'tools\ScheduledBackup\ScheduledBackup.csproj'
$destination = Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadAppArtifacts\scheduled-backup-runner'

dotnet publish $projectPath --configuration Release --output $destination
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar el ejecutor de copia.' }

$runner = Join-Path $destination 'ScheduledBackup.exe'
if (-not (Test-Path -LiteralPath $runner)) { throw 'No se encontró el ejecutor publicado.' }

schtasks.exe /Create /TN $taskName /TR ('"' + $runner + '"') /SC DAILY /ST $Time /RL LIMITED /F
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la tarea programada.' }

$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Set-ScheduledTask -TaskName 'DailyAuthenticatedBackup' -TaskPath '\ClinicaLongevidadApp\' -Settings $settings | Out-Null

Write-Output "Tarea diaria creada a las ${Time}: $taskName"
