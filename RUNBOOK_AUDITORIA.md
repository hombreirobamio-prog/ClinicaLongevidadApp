# Runbook: Auditoría Segura - ClinicaLongevidadApp

Propósito: describir pasos operativos para provisionar, configurar y operar el sistema de auditoría segura.

1) Provisionamiento de Infraestructura
- Key Vault
  - Crear Key Vault in Azure: `az keyvault create --name <vault> --resource-group <rg>`
  - Crear secretos: `audit-hmac-key` (base64 32 bytes), `audit-enc-key` (base64 32 bytes)
  - Permisos: otorgar `Get` a la identidad que ejecuta la app (Managed Identity o Service Principal)
- Storage (Blob)
  - Crear Storage Account
  - Crear contenedor `audit-events` con políticas de retención / immutability (WORM) si es requerido
- SIEM
  - Configurar endpoint HTTP o ingest para recibir eventos

2) Configuración de la aplicación
- Variables de entorno (en el host):
  - `KEYVAULT_URI` = `https://<vault>.vault.azure.net/`
  - `AUDIT_HMAC_SECRET_NAME` = `audit-hmac-key`
  - `AUDIT_ENC_SECRET_NAME` = `audit-enc-key`
  - `STORAGE_CONNECTION_STRING` o `STORAGE_ACCOUNT_URI`
  - `AUDIT_BLOB_CONTAINER` (opcional, default `audit-events`)
  - `AUDIT_WEBHOOK_URL` (opcional)
- Si no se usa Key Vault, setear localmente:
  - `AUDIT_HMAC_KEY` (base64)
  - `AUDIT_ENC_KEY` (base64)

3) Key rotation
- Use KeyRotationService (built-in).
- To rotate HMAC key:
  - Call `KeyRotationService.RotateHmacKey()` (size default 32 bytes)
  - Verify new key persisted and audit event created
- Best practice: rotate keys periodically and record rotation events.

4) Integrity checks
- AuditoriaIntegrityWorker runs periodically and writes report files under `C:\ProgramData\ClinicaLongevidadApp\AuditIntegrityReports`.
- If VerifyIntegrity reports errors:
  - Isolate the DB snapshot
  - Run VerifyIntegrity manually
  - Check logs and recent operations
  - If tampering suspected, open incident and restore from backups

5) Incident Response
- If tampering or signature mismatches:
  - Lock the system
  - Export current DB snapshot (read-only)
  - Notify security + compliance
  - Rotate keys
  - Restore from last known good backup

6) Backups and retention
- Backup DB nightly to immutable storage
- Retention policy: 7 years (ajustar según regulación)

7) Monitoring
- Integrate logs with SIEM
- Alert on:
  - VerifyIntegrity errors
  - Failed writes to audit table
  - Excessive audit volume

8) Testing and validation
- Unit tests included cover integrity and HMAC
- Run `dotnet test` in CI before deployment
- Validate end-to-end in staging

9) Notes
- LocalKeyProvider is for development only. Use AzureKeyVaultKeyProvider in production.
- Storage immutability must be configured at the account/container level; code cannot enforce WORM alone.

---

End of runbook.
