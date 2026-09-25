Nota rápida para el Auditor — ClinicaLongevidadApp

Resumen
------
Este paquete incluye las herramientas y el flujo para generar evidencia reproducible de backups e integridad.
- Runner one-click: `scripts/run_audit_for_auditor.bat` (genera backup, verifica manifiesto y sube ZIP al release).
- Generador de artefactos: `scripts/generate_audit_artifacts.ps1` (crea ZIP con logs, informes y backups; puede ejecutar `tools/InvokeBackup`).
- Verificador de manifiesto: `scripts/verify_audit_manifest.ps1` (compara SHA256/HMAC listados en `audit_manifest_*.txt` con los archivos).

Artefactos generados
--------------------
- ZIP de evidencia: `audit-artifacts_YYYYMMDD_HHMMSS.zip` (en la raíz del repo tras la ejecución).
- Manifiesto: `audit_manifest_YYYYMMDD_HHMMSS.txt` (lista `SHA256`, `HMAC`, `HMAC.Version` para cada backup `.db`).
- Backups permanentes: `artifacts/backups/*.db` con `.sha256`, `.hmac`, `.hmac.ver` cuando proceda.
- Logs e informes: `%LocalAppData%\ClinicaLongevidadApp\logs` y `%ProgramData%\ClinicaLongevidadApp\AuditIntegrityReports`

Comprobaciones recomendadas (rápidas)
------------------------------------
1) Ejecutar el flujo completo (recomendado):
   - Doble clic en `scripts\run_audit_for_auditor.bat` o desde PowerShell:
     `.\scripts\run_audit_for_auditor.bat`
   - Resultado esperado: ZIP creado y subido al release `audit-rewrite-8684b20`.

2) Verificar manifiesto localmente (si prefieres inspección manual):
   - `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1`
   - Salida esperada: `Errors: 0`.

Opciones de verificación
-----------------------
- Verificación permisiva (por defecto):
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1`
  - Comprueba SHA y companion `.sha256` obligatorios; HMAC se trata como recomendado (warnings si falta).
- Verificación estricta (recomendada en auditoría/CI si se dispone de la clave HMAC):
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1 -RequireHmac`
  - Esta opción exige la presencia y coincidencia de los ficheros `.hmac` y `.hmac.ver` además de `.sha256`.

Recomendación: usar `-RequireHmac` en auditorías formales o pipelines donde la clave HMAC esté disponible para comprobación completa.

3) Comprobación manual de hash:
   - `Get-FileHash -Algorithm SHA256 "artifacts\backups\<backup>.db"` y comparar con `audit_manifest_*.txt` o el archivo `.sha256` correspondiente.

4) Revisar logs de backup:
   - `Get-Content "$env:LOCALAPPDATA\ClinicaLongevidadApp\logs\backup.log" -Tail 200`
   - `Get-Content "$env:LOCALAPPDATA\ClinicaLongevidadApp\logs\backup_vm.log" -Tail 200`

Contexto operativo
------------------
- La rama y PR con estas mejoras fue mergeada en `master` (PR: https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/pull/15).
- Release/tag objetivo usado por el runner: `audit-rewrite-8684b20`.

Notas de seguridad
-----------------
- Los HMAC requieren la clave correspondiente para verificar fuera del manifiesto. La clave NO se incluye en los artefactos. Pedir al equipo operativo la clave si se precisa verificación de HMAC.

Contacto
--------
Si necesitas que ejecute el flujo en tu entorno o que prepare un paquete con instrucciones impresas, indícalo y lo preparo.
