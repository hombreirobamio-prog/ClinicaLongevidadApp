@echo off
setlocal
REM Explicit evidence inputs; no application startup, database access or upload.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0generate_audit_artifacts.ps1" %*
exit /b %ERRORLEVEL%
