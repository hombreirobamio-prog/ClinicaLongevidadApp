# Roadmap de auditoría

Actualización documental: 30/09/2026. **Auditoría operativa abierta.** Estado y criterios de cierre: [AUDIT_CLOSURE.md](docs/AUDIT_CLOSURE.md).

## Avances documentados

- Controles de restauración que exigen SHA256, HMAC y versión de clave; propagación de errores de escritura de auditoría.
- Payload v2 y cifrado obligatorio de detalles fuera de Development/Test, sin refirmar registros históricos.
- Pipeline y paquete técnico autenticado implementados localmente: [operación vigente](docs/AUDIT_PIPELINE_CURRENT.md). Configuración y ejecución remotas pendientes.
- Ensayo sintético de recuperación ampliado a trece escenarios y validado con 91/91 pruebas: [evidencias y límites](docs/AUDIT_RECOVERY_DRILL_2026-09-29.md). No equivale a validar producción.
- H04 parcial: festivos, pacientes, citas, usuarios, horarios y administración de cola confirman negocio y evento en la misma transacción SQLite. El reenvío usa bandeja de salida transaccional y entrega al menos una vez. Última suite: **189/189 aprobadas**; [última entrega](docs/AUDIT_SESSIONS.md#continuacion-h04-reenvio-durable-2026-09-30).
- [Punto de reanudación actualizado](docs/AUDIT_SESSIONS.md#continuacion-h04-rotacion-de-claves-2026-09-30): validar operativamente las versiones históricas de Key Vault y los destinos de reenvío según el [inventario](docs/H04_INVENTARIO_2026-09-30.md).

## Próximos pasos

1. Validar manualmente permisos y UI con usuarios reales: Copia ahora, Programar, Cancelar y Probar 1 min. Comprobar una única programación activa y la liberación de timers/handlers al cerrar la vista. Registrar resultados; no se dan por comprobados mediante el ensayo sintético.
2. Configurar la clave y versión de firma de paquetes en CI y conservar una ejecución remota verificable. Las PR no reciben esa clave. El workflow no publica Releases.
3. Ejecutar un ensayo autorizado de recuperación en una copia representativa, protegida y aislada, con conexiones cerradas, claves históricas y objetivos RTO/RPO acordados. Verificar interrupciones y recuperación sin actuar sobre una base operativa activa.
4. Continuar H04 por rotación de claves según el inventario. Verificar que los destinos de reenvío deduplican por EventId: la entrega es al menos una vez. Login ya exige auditar antes de publicar sesión. Festivos, pacientes, citas, usuarios y horarios ya tienen transacción conjunta, pero el alta de paciente y su cita posterior no forman una sola operación atómica. Resolver también H01, anclaje externo, en una tarea específica.
5. Verificar gestión de claves, cifrado en reposo de backups, permisos, retención, alertas y custodia.
6. Completar el paquete operativo firmado y la revisión independiente con responsable, fecha y evidencias antes de declarar cierre o preparar la entrega final.

## Alcance

Las pruebas existentes y sus resultados están documentados; cualquier ampliación debe responder a una carencia concreta. Las mejoras visuales, refactorizaciones y actualizaciones de dependencias no forman parte de esta actualización.

Este roadmap de la raíz es la referencia vigente para esta tarea. La copia de igual nombre en `docs/` no se ha actualizado ni validado aquí; no debe prevalecer sobre el estado de cierre enlazado.

## Nota rápida — trabajo en ordenado de Auditoría (2026-10-02)

- Se realizaron cambios en `Views/AuditorMenuWindow.xaml.cs` para añadir utilidades de ordenado y una utilidad para ordenar logs en disco.
- Informe y tareas: `reports/audit_fix_report_20261002_011700.txt`, `reports/audit_fix_todo_20261002_011700.txt`.

Pendiente: aplicar `SortMemberPath`/binding de la columna Fecha/Hora en la pantalla principal de Auditoría para que el grid muestre registros en orden continuo.
