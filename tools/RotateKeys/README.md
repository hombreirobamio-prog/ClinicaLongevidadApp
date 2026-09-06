RotateKeys - Key rotation helper

Usage:
  dotnet run --project tools/RotateKeys -- [--hmac] [--enc] [--all] [--provider azure|local] [--dry-run]

Notes:
- By default the tool will prefer Azure Key Vault when `KEYVAULT_URI` is set in the environment.
- `LocalKeyRotationProvider` stores keys under local AppData and sets process environment variables (development only).
- When using Azure provider, configure:
  - `KEYVAULT_URI` (https://...)
  - `AUDIT_HMAC_SECRET_NAME`
  - `AUDIT_ENC_SECRET_NAME`

Security:
- Rotate keys during maintenance windows and ensure consumers are able to read the new key version.
- Keep the previous key available to verify historical events until you have a migration strategy.

Commands and examples
- Dry-run (generate keys but don't persist):
  - `dotnet run --project tools/RotateKeys -- --all --dry-run`

- Persist new keys to local storage (development):
  - `dotnet run --project tools/RotateKeys -- --all --provider local`

- Persist new keys to Azure Key Vault and run VerifyIntegrity against a DB:
  - Ensure `KEYVAULT_URI`, `AUDIT_HMAC_SECRET_NAME`, `AUDIT_ENC_SECRET_NAME` are set in env
  - `dotnet run --project tools/RotateKeys -- --all --provider azure --verify-after --db "Data Source=C:\path\to\auditoria.db"`

- Backfill (dry-run):
  - `dotnet run --project tools/RotateKeys -- --backfill --db "Data Source=C:\path\to\auditoria.db"`

- Backfill and apply changes (DESCTRUCTIVE: backup DB first):
  - `dotnet run --project tools/RotateKeys -- --backfill --apply --db "Data Source=C:\path\to\auditoria.db"`

Safety checklist before applying changes
1. Backup: create a filesystem copy of the audit DB and store it in an immutable location.
2. Maintenance window: perform rotation/backfill during low-traffic time and notify stakeholders.
3. Verify current keys: ensure the application/consumers can read the new key (Key Vault access policies or environment updated).
4. Dry-run first: always run `--dry-run` and `--backfill` without `--apply` to review planned changes.
5. Post-check: after applying, run `--verify-after` or `dotnet test` integration to confirm no integrity errors.
6. Audit log of operation: record who performed rotation, when, and the KeyVersion values used.

Notes
- The tool is designed for operators comfortable performing DB backups and maintenance. If you prefer, I can add an automated backup step before `--apply`.

