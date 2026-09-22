# Audit Hardening - Recordatorio operativo

Lista rápida de comprobaciones y recordatorios para endurecer el subsistema de auditoría y backups antes de producción:

- Key management
  - En producción exigir Key Vault (`REQUIRE_KEYVAULT=1`).
  - No persistir claves sensibles en variables `User` indefinidamente; preferir `Process` o Key Vault.
  - Documentar procedimiento de rotación de claves (HMAC y ENC) y pruebas tras rotación.

- Backups
  - Cifrar backups en repositorio local o mover a storage con cifrado en reposo.
  - Restringir permisos de carpeta de backups a cuenta de servicio/usuario administrativo.
  - Mantener política de retención y rotación (p. ej. 30 días + ciclo de retención diferencial si procede).
  - Añadir checksum/firmas a backups para validar integridad en restore.

- Base de datos y esquema
  - Triggers "append-only" habilitados fuera de entornos de test para impedir UPDATE/DELETE en `Auditoria`.
  - Migraciones defensivas cuando se amplíe la tabla (agregar columnas con `IF NOT EXISTS` / comprobaciones de compatibilidad).

- Logging y trazabilidad
  - No volcar `Detalles` sensibles en logs por defecto; usar flags de diagnóstico explícitos.
  - Auditar y limitar quien puede ver las herramientas de diagnóstico.
  - Registrar quién programa/cancela backups y cuándo (usuario, IP/host si aplica).

- Operaciones y pruebas
  - Automatizar una prueba de restauración periódica (sandbox) para garantizar que los backups son válidos.
  - Añadir test que simule programación duplicada y verifique que solo queda una programación activa.
  - Documentar el playbook de restauración y el runbook de incidentes (pasos a seguir si la integridad falla).

- Seguridad del entorno
  - Revisar variables de entorno sensibles en despliegues y pipelines; no incluir secretos en logs.
  - Revisar dependencias (Azure.Identity, sqlite libs) y actualizar versiones con vulnerabilidades conocidas.

- Acceso y control
  - Restringir la vista/admin de Auditoría a roles `Administración` y anotar operaciones críticas en la propia auditoría.
  - Evitar opciones como `FORCE_ADMIN` fuera de entornos controlados y marcarlo claramente en documentación.

Recordatorio final: antes de pasar a producción completar la lista de pruebas manuales y automáticas, documentar los pasos de validación y confirmar que Key Vault y cifrado de backups funcionan según la política.
