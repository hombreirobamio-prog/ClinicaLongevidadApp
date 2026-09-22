Resumen y roadmap de auditoría — ClinicaLongevidadApp

Propósito

Guardar de forma clara y priorizada todas las tareas necesarias para convertir el subsistema de auditoría en una solución robusta, segura y reproducible.

Estado actual (resumen)
- Auditoría funcional implementada: `AuditoriaService` con hashing encadenado, HMAC y cifrado AES-GCM para `Detalles`.
- Vistas y ViewModels (`AuditoriaView`, `AuditoriaViewModel`) presentes y export CSV implementado.
- Workers: forward queue e integrity worker implementados pero deben volverse opt-in y revisarse logs.
- Rama `feature/force-admin` creada con helper opt-in `FORCE_ADMIN` y doc en README.

Prioridades críticas (ordenadas)
1. Seguridad de claves
   - Exigir Key Vault en producción (KEYVAULT_URI) o documentar claramente la excepción local.
   - Revisar `LocalKeyProvider` y `LocalKeyRotationProvider` (no persistir claves en texto plano en entornos compartidos).
2. Hardening del arranque
   - Hacer forwarder/exporter e integrity worker opt-in mediante variables: `AUDIT_FORWARD_ENABLED`, `AUDIT_INTEGRITY_ENABLED`.
   - Evitar arrancar workers si `AuditoriaService` no inicializó correctamente.
3. Control de acceso y autenticación
   - Implementar hashing fuerte de contraseñas (BCrypt/Argon2) si no está hecho.
   - Garantizar comprobaciones de autorización en la capa de servicios (no solo UI).
4. Auditoría obligatoria
   - Forzar llamadas a `AuditoriaService.RegistrarEvento(...)` desde la capa de servicios para todas las operaciones críticas (login, logout, CRUD usuarios, citas, festivos, rotación de claves, export).
5. Tests automáticos
   - Unit + integration: VerifyIntegrity, concurrencia (PrevHash), endpoints y que AuditoriaService se invoque desde servicios.
   - Añadir tests que confirmen no se filtra PII en logs.
6. Entorno de pruebas y datos
   - Crear staging DB y un usuario admin de prueba (seed/migration) para pruebas manuales.
   - Documentar pasos reproducibles para probar integridad y diagnósticos.
7. Observabilidad
   - Logs estructurados en `%LocalAppData%\ClinicaLongevidadApp\logs` y políticas de retención.
   - Alertas/forwarding auditado (webhook/export) opt-in y monitorización.
8. Documentación y procesos
   - README: pasos para pruebas locales (`FORCE_ADMIN`, `AUDIT_*` vars) y playbook para diagnóstico y recuperación.
   - Crear checklist de auditoría y playbook para respuesta a incidentes.

Acciones inmediatas (hacer ahora)
- [ ] Crear rama `chore/audit-hardening` y aplicar cambios opt-in en `App.xaml.cs` (support de `AUDIT_FORWARD_ENABLED` y `AUDIT_INTEGRITY_ENABLED`).
- [ ] Revisar `AuditoriaService` y eliminar/ocultar logs que puedan contener `Detalles` sin protección.
- [ ] Añadir tests básicos para `VerifyIntegrity()` y que `RegistrarEvento` se llame en operaciones CRUD de usuario y cita.
- [ ] Documentar en README comandos para pruebas (`FORCE_ADMIN=1`, `dotnet run`, `tools/CheckAdmin`).

Comandos útiles
- Ejecutar app con admin forzado (local):
  - PowerShell: `$env:FORCE_ADMIN='1'; dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug`
  - Ejecutable: `$env:FORCE_ADMIN='1'; dotnet bin\Debug\net8.0-windows\ClinicaLongevidadApp.dll`
- Ejecutar tests:
  - `dotnet test --configuration Debug`
- Comprobar usuario admin (tool auxiliar):
  - `dotnet run --project tools/CheckAdmin "C:\Users\<usuario>\AppData\Local\ClinicaLongevidad.db"`

Responsables y ramas
- Ramas propuestas:
  - `chore/audit-hardening` (hardening y opt-in)
  - `feature/auth-hardening` (hashing/roles/servicios)
  - `test/audit-integration` (tests de integridad y concurrencia)
- Yo (asistente) puedo aplicar los cambios y abrir PRs en ramas separadas; tú revisas y apruebas.

Notas finales
- No borrar ni reescribir `master` sin PR revisado.
- Mantener todas las pruebas en entornos aislados; nunca ejecutar operaciones de reparación directa en producción sin backup y plan de recuperación.

---
Generado y guardado automáticamente para referencia.

## Plan para auditoría (auditor-ready)

Objetivo: disponer de un subsistema de auditoría que pueda ser revisado por un auditor externo y que demuestre integridad, trazabilidad y controles de acceso claros.

1) Qué ya está hecho
  - `AuditoriaService` con hash encadenado, firma HMAC y cifrado AES‑GCM opcional para `Detalles`.
  - `AuditoriaView` / `AuditoriaViewModel` con filtros básicos y export CSV.
  - Tests automatizados existentes y nuevos tests de `VerifyIntegrity()` en `ClinicaLongevidadApp.Tests`.
  - `tools/AuditDecrypt` añadido para que administradores inspeccionen filas cifradas.
  - CI actualizado para compilar `tools/*` y ejecutar tests automáticamente.

2) Qué falta / pendiente (prioridad)
  - Seguridad de claves: configurar Key Vault en staging/producción y usar `KEYVAULT_URI`.
  - Rotación de claves: implementar flujo de rotación, versionado y backfill seguro.
  - Evitar persistir `DetallesPlain` en producción; detectar filas existentes y decidir migración.
  - Forzar (o garantizar) que todas las operaciones críticas llaman a `RegistrarEvento(...)`.
  - Añadir job CI que ejecute `VerifyIntegrity()` y falle si detecta inconsistencias.
  - Revisar que los logs no expongan PII/`Detalles` en entornos no seguros.

3) Tareas operativas concretas
  - Crear `feature/key-rotation`: scaffolding para `RotateKeys` tool y providers KeyVault/Local.
  - Script `tools/db-audit-inspect` que exporte filas con `DetallesPlain` y genere CSV/JSON.
  - Habilitar en CI los secrets `AUDIT_HMAC_KEY` (obligatorio) y `AUDIT_ENC_KEY` (opcional) para validar rutas cifradas.
  - Documentar playbook de auditoría: pasos para generar `IntegrityReport`, interpretación y artefactos a entregar.

4) Criterios de aceptación para auditoría
  - Todas las filas contienen `PrevHash`, `Hash` y `Signature` verificables.
  - KeyVersion/KeyVersionEnc se registran y son trazables por cada inserción.
  - `DetallesPlain` no contiene PII en producción (o está vacío) y existe un plan de migración si procede.
  - Existencia de herramientas administrativas reproducibles (`AuditDecrypt`, `RotateKeys` scaffold) y logs que demuestren quién las ejecutó.
  - CI produce artefactos (tests, coverage, integrity checks) que pueden adjuntarse al paquete para auditoría.

5) Artefactos a conservar y entregar al auditor
  - `IntegrityReport_<ts>_id<N>.json` generado por `GenerateIntegrityDiagnosticReport()`.
  - `IntegrityQuickSummary_*.txt` y `IntegrityProblemRows_*.csv` desde `GenerateQuickDiagnostics()`.
  - Historial de versiones de claves (KeyVersion) y registro de rotaciones.
  - Logs de la ejecución de tools y de la CI (build/test/integrity job).

6) Próximo paso recomendado (acciones inmediatas)
  - (HOY) Mergear `chore/audit-hardening` para consolidar tests y cambios opt‑in.
  - Crear `feature/key-rotation` y PR con scaffold de rotación + guía de uso de Key Vault.
  - Añadir CI job que ejecute `VerifyIntegrity()` y publique `IntegrityReport` como artefacto.

---
Actualizado por: GitHub Copilot en la rama `chore/audit-hardening`.

## Backfill: resumen de la sesión reciente

- Fecha: (última sesión)
- Estado: herramienta `tools/RotateKeys` reparada y verificaciones realizadas.

Hecho:
- `tools/RotateKeys/RotateKeys.csproj`: alineado TFM y `Microsoft.Data.Sqlite` con la app.
- `tools/RotateKeys/Program.cs`: corregida la lógica `preview|apply|backfill` y errores de sintaxis.
- `Services/KeyRotation/KeyRotationService.cs`: añadido `ApplyRotateEncryptionKeyAsync` y helpers `GenerateRandomKey`/`GenerateRandomKeyBase64`.
- `ClinicaLongevidadApp.csproj`: eliminadas inclusiones accidentales de `tools\*.cs`.

Resultados de ejecución:
- `dotnet run --project tools/RotateKeys -- backfill preview --db "C:\\Users\\Francisco\\AppData\\Local\\ClinicaLongevidad.db"` → `Backfill preview: 117 rows would be processed.`
- `dotnet run --project tools/RotateKeys -- backfill apply --db "C:\\Users\\Francisco\\AppData\\Local\\ClinicaLongevidad.db" --dryrun` → `Backfill processed=100 created=0 skipped=0 (dryRun=True)`

Pendiente inmediato:
- Decidir si ejecutar `backfill apply` real (hacer backup antes).
- Investigar discrepancia preview (117) vs dry-run (100).
- Documentar estrategia final en este roadmap (append-only vs re-encrypt).

Ver `docs/BACKFILL_SESSION_SUMMARY.md` para detalles y comandos reproducibles.
