@echo off
REM Simple wrapper for auditors: double-click to run full audit session and upload artifacts to release.
SETLOCAL
SET TAG=audit-rewrite-8684b20

echo Running audit session (this will start the app, generate diagnostics, package artifacts and try to upload to release)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0generate_audit_artifacts.ps1' -UploadToRelease -ReleaseTag '%TAG%' -TimeoutSeconds 300"

if %ERRORLEVEL% equ 0 (
    echo Audit session finished.
) else (
    echo Audit session completed with errors (exit code %ERRORLEVEL%). Check logs and the script output.
)
pause
ENDLOCAL
