Audit regeneration record

Date: 2026-09-26 UTC

Summary
-------
A regenerated audit package was created and uploaded to the existing release `audit-rewrite-8684b20` because the original audit package contained HMAC values that could not be validated with the available HMAC key(s).

Files produced
--------------
- `audit-artifacts_20260926_185645.zip` (uploaded to the release)
- `tmp_regen/audit_manifest_20260926_185645.txt` (manifest verified locally)

Reason
------
During local verification of release assets the manifest `audit_manifest_20260925_150728.txt` contained HMAC values and an HMAC.Version that did not match the HMACs generated with the available key. The original package therefore could not be cryptographically validated with the key the operator had.

Actions taken
-------------
1. Attempted to locate the original HMAC key in repo/org secrets and environments. No accessible repo/org secret matched; an environment named `copilot` exists but contained no accessible secret for `AUDIT_HMAC_KEY` from this session.
2. Generated companions for local and artifact backups (`.sha256` and optionally `.hmac` using the operator-provided key when available).
3. Regenerated a new audit package locally using `scripts/generate_audit_artifacts.ps1`. The new ZIP `audit-artifacts_20260926_185645.zip` was created.
4. Extracted and verified the new package's manifest with `scripts/verify_audit_manifest.ps1 -RequireHmac:$true`. Verification returned 0 errors and 0 warnings.
5. Uploaded the regenerated ZIP to the release `audit-rewrite-8684b20` as an additional asset (did not delete or overwrite original assets).
6. Added a release note documenting the regeneration and reason.
7. Cleaned temporary files and removed the local regenerated ZIP after upload.

Recommendations / next steps
----------------------------
- If preserving original evidentiary validation is required, locate the original HMAC key/version and re-run verification against the original manifest. If found, record the key location and access policy in the project ops documentation (do not store keys in the repo).
- Add the operational HMAC key to the repository secrets or to the CI environment (GitHub Actions secret) under the name `AUDIT_HMAC_KEY` so CI can reproduce/verify HMACs.
- Keep the regenerated audit package as a supplemental verified artifact; do not delete the original release assets unless an audit decision is made to replace them.
- Optionally add this document to the release notes or to the auditor handoff docs.

Commands used (for operator reference)
--------------------------------------
- Generate companions:
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate_companions.ps1 -IncludeHmac:$true
- Regenerate audit package:
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate_audit_artifacts.ps1 -RequireBackup -ListFiles -ReleaseTag "regen-$(Get-Date -Format yyyyMMdd_HHmmss)" -TimeoutSeconds 300
- Verify manifest:
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1 -ManifestPath <path-to-manifest> -RequireHmac:$true
- Upload to release:
  gh release upload 'audit-rewrite-8684b20' <path-to-zip> --repo hombreirobamio-prog/ClinicaLongevidadApp

Notes
-----
This operation preserved the original released artifacts and added a regenerated, verified package as supplemental evidence. All key operations and reasons are logged here for the auditor record.
