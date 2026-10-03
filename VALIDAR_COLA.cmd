@echo off
cd /d "%~dp0"
echo Abriendo PRUEBA AISLADA de cola. No inicia la aplicacion habitual.
dotnet run --project "tools/AuditAdminManual/AuditAdminManual.csproj" -c Release --no-restore
if errorlevel 1 (
    echo No se pudo abrir la prueba. Copia el mensaje de error y compartelo en el chat.
    pause
)
