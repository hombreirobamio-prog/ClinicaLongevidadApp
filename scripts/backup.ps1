$dt = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = Join-Path (Get-Location).Path 'backups'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
$dest = Join-Path $backupDir ("ClinicaLongevidadApp_backup_$dt.zip")
$items = Get-ChildItem -Path (Get-Location).Path -Force | Where-Object { $_.Name -ne 'backups' } | ForEach-Object { $_.FullName }
Compress-Archive -LiteralPath $items -DestinationPath $dest -Force
Write-Output "BACKUP_CREATED:$dest"
