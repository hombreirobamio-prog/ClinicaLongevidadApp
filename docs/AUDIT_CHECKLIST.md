# Checklist de auditoría — ClinicaLongevidadApp

Objetivo: pasos claros para que un auditor verifique la integridad del subsistema de auditoría, comprobación de backups y artefactos asociados.

1) Preparación
- Abrir una sesión de PowerShell/Terminal con permisos de usuario que tenga acceso al repositorio y a los paths locales.
- Clonar / actualizar el repo: `git pull origin master`.
- Opcional: crear una rama de trabajo para cambios: `git checkout -b fix/backup-audit`.

2) Ejecutar la suite de tests
- Comando: `dotnet test` (desde la raíz del repo).
- Criterio: todos los tests deben pasar. Si hay fallos, capturar output y proceder a investigar.

3) Ejecutar el runner de auditoría (one-click)
- Ejecutar: `scripts\run_audit_for_auditor.bat` (Windows) o `powershell -File scripts\generate_audit_artifacts.ps1`.
- Resultado esperado: archivo ZIP `audit-artifacts_<timestamp>.zip` creado en la raíz del repo y/o subido al release `audit-rewrite-8684b20`.

4) Comprobar logs y artefactos
- Logs de backup: `%LocalAppData%\ClinicaLongevidadApp\logs\backup.log`.
  - Buscar líneas `TriggerImmediateBackup created:` — indican backups creados y su ruta.
  - Buscar líneas `ComputeAndWriteChecksums succeeded` — indican checksum/HMAC generados con éxito.
- Para cada backup reportado, verificar en disco (ejemplo):
  - `<backup>.db`
  - `<backup>.db.sha256` (SHA256 en hex, no vacío)
  - `<backup>.db.hmac` y `<backup>.db.hmac.ver` (si hay clave HMAC configurada)

5) Ejecutar verificación de integridad
- Ejecutar herramienta de diagnóstico: `dotnet run --project tools/RunAuditDiagnostics/RunAuditDiagnostics.csproj "Data Source=<ruta a la DB local>"`
- Revisar la salida `VerifyIntegrity devolvió N errores.` — N idealmente 0.
- Revisar `AuditIntegrityReports` y CSVs generados.

6) Comprobaciones de clave y firma
- Verificar variable de entorno `AUDIT_HMAC_KEY` (y `AUDIT_HMAC_KEY_VERSION` si procede).
- Si `AUDIT_HMAC_KEY` no está presente, las firmas HMAC no se generarán (esto es intencional en entornos sin clave).

7) Criterios para dar por buena la auditoría
- Suite de tests pasa (local o CI).
- Backups recientes existen y tienen `.sha256` válidos.
- Si hay clave HMAC configurada, los `.hmac` y `.hmac.ver` existen y la verificación (VerifyIntegrity) acepta las firmas.
- `IntegrityReport_*.json` no contiene problemas críticos o los problemas están justificados en el informe.
- Artefacto ZIP creado y (opcional) subido al release.

8) Si algo falla (acciones de remediación)
- Backup creado pero sin `.sha256`/`.hmac`:
  - Revisar `%LocalAppData%\ClinicaLongevidadApp\logs\backup.log` para errores `ComputeAndWriteChecksums`.
  - Aumentar reintentos en `Services/BackupService.cs` (ya implementado: temp-copy y retries). Reintentar.
- `HMAC verification failed`:
  - Verificar que `AUDIT_HMAC_KEY` contiene la clave correcta (base64 o raw) y que `GetHmacKeyByVersion` devuelve la versión adecuada.
- Tests fallan:
  - Ejecutar `dotnet test -v minimal` y aislar tests con fallos.
- Si algún proceso bloquea archivos repetidamente:
  - Diagnóstico con herramientas (Process Explorer / Handle) para identificar quien mantiene handles.

9) Evidencia final
- ZIP de artefactos en release o adjunto.
- `backup.log` con entradas `ComputeAndWriteChecksums succeeded` y `TriggerImmediateBackup created` (fechas y rutas).
- Salida de `RunAuditDiagnostics` y `AuditIntegrityReports` JSON.
- Resultado de `dotnet test`.

---

Si quieres, creo también un script que automatice las comprobaciones (tests, logs, existencia de `.sha256/.hmac`) y muestre un resumen. Estoy listo para crear y ejecutar ese script si me autorizas.
