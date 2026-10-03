# Recordatorio operativo de seguridad de auditoría

Actualización documental: 30/09/2026. **Auditoría operativa abierta.** Consultar [estado de cierre](docs/AUDIT_CLOSURE.md) y [correcciones](docs/AUDIT_REMEDIATION_2026-09-29.md).

## Controles implementados que deben verificarse en el despliegue

- Las nuevas escrituras requieren clave HMAC y versión; un fallo se propaga desde el servicio. Festivos, pacientes, citas, usuarios, horarios y administración de cola confirman negocio y evento en una misma transacción; el inicio de sesión exige confirmar su auditoría antes de activar la sesión. El reenvío usa bandeja de salida transaccional y entrega al menos una vez: los destinos deben deduplicar por EventId. Queda rotación según el inventario H04. No existe garantía de atomicidad global.
- Los eventos nuevos utilizan payload v2. La compatibilidad con registros v1 no protege retrospectivamente sus metadatos.
- Fuera de los entornos explícitos Development/Test, los detalles requieren cifrado y no se conserva `DetallesPlain`. `AUDIT_ALLOW_PLAINTEXT_DETAILS` no permite eludirlo; un fallo de cifrado no degrada a texto sin cifrar.
- La restauración exige `.sha256`, `.hmac` y `.hmac.ver` válido, además de disponer de la clave correspondiente. No basta comprobar que los ficheros existen.
- `FORCE_ADMIN` fue retirado. Usar autenticación normal y verificar autorización con usuarios reales. `SILENT_MODE` suprime diálogos, pero deniega las confirmaciones.
- Los diagnósticos minimizan detalles por defecto. No activar su inclusión en logs o informes sin controlar el acceso y la distribución.

## Comprobaciones operativas pendientes

- Exigir Key Vault en producción mediante `REQUIRE_KEYVAULT=1` y verificar configuración y acceso efectivos. No guardar secretos en código, documentación, historial de comandos ni evidencias.
- Documentar rotación HMAC/ENC y disponibilidad de versiones históricas; evitar persistencia indefinida de claves en variables de usuario.
- Comprobar cifrado en reposo de backups y permisos de carpetas. El cifrado de `Detalles` no implica cifrado de toda la copia de base de datos.
- Acordar retención, custodia, alertas y responsables; no adoptar plazos arbitrarios como política aprobada.
- Verificar triggers append-only y acceso a base de datos en el entorno desplegado. No ejecutar migraciones ni modificar registros históricos como parte de una comprobación.
- Validar programación/cancelación y ausencia de timers duplicados, así como trazabilidad de las operaciones administrativas.
- Realizar recuperación aislada con datos representativos, aplicación detenida y conexiones cerradas; evaluar interrupciones y RTO/RPO. El [ensayo sintético](docs/AUDIT_RECOVERY_DRILL_2026-09-29.md) ya existe, pero no acredita estos requisitos operativos.
- Revisar dependencias como tarea específica; cualquier instalación o actualización requiere autorización.
- Completar anclaje externo, relación durable negocio/auditoría, ejecución remota de CI y revisión independiente según el estado de cierre.

No regenerar firmas históricas para presentar una copia como autenticada en su origen. Una clave histórica no disponible deja la verificación incompleta; no demuestra por sí sola manipulación.

Este recordatorio de la raíz es la referencia actualizada. La copia de igual nombre en `docs/` no se ha actualizado ni validado en esta tarea.

## Recordatorio: ordenación y visibilidad de auditoría (2026-10-02)

- Añadido trabajo para asegurar orden continuo en la vista de auditoría (`Views/AuditorMenuWindow.xaml.cs`).
- Revisar que la columna de fecha/hora esté exposada como `DateTime` y tenga `SortMemberPath` para permitir orden nativo.
- Ver informes en `reports/audit_fix_report_20261002_011700.txt`.
