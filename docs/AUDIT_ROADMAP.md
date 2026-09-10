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
