> Actualización 29/09/2026: las instrucciones de FORCE_ADMIN, autoconfirmación y verificación/restauración permisiva quedan sustituidas por [Correcciones y operación vigente](AUDIT_REMEDIATION_2026-09-29.md). La auditoría sigue abierta; el texto histórico siguiente no acredita el cierre.

# Audit Runbook — ClinicaLongevidadApp

Purpose
-------
Short, actionable runbook for auditors and operators to validate the audit subsystem, perform backups/restores and rotate keys in a controlled way.

Prerequisites
-------------
- A machine with the repository checked out.
- .NET 8 SDK installed.
- For production Key Vault checks: a Key Vault configured and accessible, or set `REQUIRE_KEYVAULT=0` for local tests.
- (Optional) `gh` CLI authenticated when you want to upload artifacts to a GitHub release.

Quick start (auditor local)
---------------------------
1. Open a PowerShell prompt in the repo root.
2. Start the app in auditor mode:
   ```powershell
   $env:FORCE_ADMIN='1'; $env:SILENT_MODE='1'; dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug --no-launch-profile
   ```
   - `FORCE_ADMIN=1` reveals administration menus.
   - `SILENT_MODE=1` suppresses MessageBox dialogs and auto-confirms prompts to allow unattended flows.

3. From the application UI (Administration → Auditoría):
   - Click `Generar diagnóstico`. Wait for the script to finish; it will create files in `%ProgramData%\ClinicaLongevidadApp\AuditIntegrityReports` and logs in `%LocalAppData%\ClinicaLongevidadApp\logs`.
   - Click `Copia ahora` to generate a backup. Backups are stored in `%LocalAppData%\ClinicaLongevidadApp\backups` and accompanied by `.sha256` (checksum) and, when available, `.hmac` and `.hmac.ver` files.

Integrity verification
----------------------
- Inspect `IntegrityQuickSummary_*.txt` and `IntegrityReport_*.json` in `%ProgramData%\ClinicaLongevidadApp\AuditIntegrityReports`.
- Confirm problematic rows include `PrevHash`, `Hash`, `Signature`, `KeyVersion`/`KeyVersionEnc`.
- Quick checks:
  - The chain of `PrevHash` → `Hash` must be consistent.
  - Signatures (HMAC) must exist when `AUDIT_HMAC_KEY`/Key Provider is configured.

Backup and restore
------------------
1. Create a backup via the UI (`Copia ahora`) or call the `BackupService.TriggerImmediateBackup(...)` API.
2. Verify that for the created backup file `<db>_backup_<ts>.db` the companion files exist:
   - `<db>_backup_<ts>.db.sha256` — SHA‑256 checksum of the file.
   - `<db>_backup_<ts>.db.hmac` — HMAC-SHA256 MAC if a HMAC key is available.
   - `<db>_backup_<ts>.db.hmac.ver` — version identifier of the HMAC key used.
3. To perform a restore (operator):
   - Ensure you have the correct backup file and its `.sha256`/.hmac files.
   - Run restore from UI or call `BackupService.RestoreBackup(backupPath, connectionString)`.
   - The restore step verifies checksum and HMAC (if present) before applying. If verification fails the operation aborts.

Key rotation (operator)
------------------------
- Use the UI buttons `Rotar HMAC` and `Rotar ENC` from Administration → Auditoría (requires admin role).
- Procedure in staging/production:
  1. Confirm `KEYVAULT_URI`, `AUDIT_HMAC_SECRET_NAME` and `AUDIT_ENC_SECRET_NAME` are configured, and that the service identity can read both the current and historical versions of those same secrets.
  2. Before rotating, run `dotnet run --project tools/RotateKeys -- verify`. It returns nonzero if either active version cannot be read again explicitly; it never displays or stores key material.
  3. Execute one rotation and confirm that its audit event contains the previous and new versions, without key material.
  4. Generate diagnostics and run `VerifyIntegrity()` to ensure chain remains consistent.
  5. If the key persists but the audit event fails, stop subsequent rotations and record the incident before continuing; Key Vault and SQLite do not share a transaction.
  6. If any integrity problems arise, follow the incident runbook (backfill hashes or mark rows as suspect).

CI and reproducible artifacts
-----------------------------
- The repository includes `scripts/run_audit_for_auditor.bat` and `scripts/generate_audit_artifacts.ps1` to automate artifact generation and optional upload.
- The GitHub Actions CI (if configured) will build the solution, run tests with coverage and upload TestResults/coverage artifacts.

Security checklist before production
-----------------------------------
- `REQUIRE_KEYVAULT=1` must be set in production and `KEYVAULT_URI` configured.
- Ensure backups are stored in secure area and only accessible to authorized service accounts.
- Ensure rotation policy for HMAC/ENC keys is documented and tested regularly.

Troubleshooting
---------------
- If `AuditoriaService` fails to initialize with Key Vault required, check `KEYVAULT_URI` and the service principal/managed identity permissions.
- If backups fail due to file locks, check for running processes using the DB and retry (online backup is attempted first).
- If HMAC verification fails during restore, ensure the HMAC key/version used to create the backup is available to the `LocalKeyProvider` or KeyVault provider.

Contact
-------
- For operational issues contact the repository owner or the admin team listed in repository metadata.

