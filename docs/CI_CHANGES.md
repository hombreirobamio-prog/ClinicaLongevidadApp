Resumen de cambios CI y pipeline de auditoría

Propósito
- Estabilizar los workflows de CI y de auditoría para que se ejecuten de forma fiable en runners hospedados y no fallen por ausencia de backups ni por fallos de subida de assets.

Cambios aplicados
1. `.github/workflows/ci.yml`
   - Añadido `workflow_dispatch` para ejecuciones manuales.
   - Caché de paquetes NuGet con `actions/cache@v4` para acelerar y estabilizar `dotnet restore`.
   - En `Build tools projects` se ejecuta `dotnet restore` por cada proyecto de `tools/*` antes de `dotnet build` para evitar errores NETSDK1004.
   - Añadido `concurrency` para evitar ejecuciones solapadas.

2. `.github/workflows/audit-pipeline.yml`
   - Uso de `actions/setup-dotnet@v4` para consistencia.
   - Generación de artefactos en modo no permanente (`-SkipPermanentBackup`) cuando no hay backups en runners hospedados.
   - Verificación estricta del manifiesto (`-RequireHmac`) solo si se detectan ficheros de backup en el runner.
   - Subida de asset al Release mediante `actions/github-script@v6` y GitHub REST API (evita problemas de credenciales con `upload-release-asset@v1`).
   - Añadido `concurrency` para evitar ejecuciones solapadas.

3. Limpieza y verificaciones
   - Se probó la subida y verificación de `audit-artifacts_20260926_185645.zip` y la verificación local devolvió: Files checked: 13, Errors: 0, Warnings: 0.

Recomendaciones finales (acciones manuales sugeridas)
- Habilitar protección de rama `master` (requerir revisiones de PR) en GitHub para evitar cambios directos a `master` sin revisión.
- Guardar en GitHub Secrets: `AUDIT_HMAC_KEY`, `AUDIT_HMAC_KEY_VERSION`, `AUDIT_ENC_KEY`, `CODECOV_TOKEN` si procede.
- Añadir monitorización/alertas (Slack/Email) para fallos en Actions si se desea.

Cómo reproducir localmente
- Descargar y verificar release ZIP:
  - `gh release download audit-rewrite-8684b20 --pattern "audit-artifacts_*.zip" --repo hombreirobamio-prog/ClinicaLongevidadApp --dir tmp_download`
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1 -ManifestPath tmp_regen\audit_manifest_YYYYMMDD_HHMMSS.txt -RequireHmac`

Contacto
- Si quieres que pase los cambios a PR en lugar de `master`, lo hago.
