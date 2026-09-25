@echo off
REM Simple wrapper for auditors: double-click to run full audit session and upload artifacts to release.
SETLOCAL
SET TAG=audit-rewrite-8684b20

echo Running audit session (this will start the app, generate diagnostics and package artifacts)...

REM 1) Generate artifacts (do not upload yet)
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0generate_audit_artifacts.ps1' -RequireBackup -ListFiles -ReleaseTag '%TAG%' -TimeoutSeconds 300"
if %ERRORLEVEL% neq 0 (
    echo Artifact generation failed - exit code %ERRORLEVEL%. Check script output.
    goto :END
)

REM 2) Verify manifest
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0verify_audit_manifest.ps1'"
if %ERRORLEVEL% neq 0 (
    echo Manifest verification failed - exit code %ERRORLEVEL%. Aborting upload.
    goto :END
)

REM 3) Upload the created ZIP to the release
powershell -NoProfile -ExecutionPolicy Bypass -Command "
    $zip = Get-ChildItem -Path '%CD%' -Filter 'audit-artifacts_*.zip' -File | Sort-Object LastWriteTime | Select-Object -Last 1; 
    if (-not $zip) { Write-Error 'No artifact ZIP found to upload'; exit 2 }
    $remote = git config --get remote.origin.url 2>$null; 
    if (-not $remote) { Write-Error 'Cannot detect git remote origin url'; exit 2 }
    if ($remote -match 'github.com[:/](.+?)(\.git)?$') { $repo = $matches[1] } else { Write-Error 'Could not parse repo from remote'; exit 2 }
    Write-Host "Uploading $($zip.FullName) to release %TAG% ...";
    gh release upload '%TAG%' $zip.FullName --repo $repo --clobber
"
if %ERRORLEVEL% neq 0 (
    echo Upload failed - exit code %ERRORLEVEL%.
    goto :END
)

echo Audit session finished.
pause
ENDLOCAL
