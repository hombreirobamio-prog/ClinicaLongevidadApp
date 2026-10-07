# Roadmap de auditoría

Actualización documental: 06/10/2026. **Auditoría operativa abierta.** Estado y criterios de cierre: [AUDIT_CLOSURE.md](docs/AUDIT_CLOSURE.md).

## Avances documentados

- Controles de restauración que exigen SHA256, HMAC y versión de clave; propagación de errores de escritura de auditoría.
- Payload v2 y cifrado obligatorio de detalles fuera de Development/Test, sin refirmar registros históricos.
- Pipeline y paquete técnico autenticado verificados en GitHub Actions: la ejecución manual `37676469997` sobre `345303c` completó pruebas, regresiones y la generación/conservación del paquete técnico firmado. [Operación vigente](docs/AUDIT_PIPELINE_CURRENT.md).
- Ensayo sintético de recuperación ampliado a trece escenarios y validado con 91/91 pruebas: [evidencias y límites](docs/AUDIT_RECOVERY_DRILL_2026-09-29.md). No equivale a validar producción.
- H04 parcial: festivos, pacientes, citas, usuarios, horarios y administración de cola confirman negocio y evento en la misma transacción SQLite. El reenvío usa bandeja de salida transaccional y entrega al menos una vez. Validación actual: **204/204 pruebas aprobadas**, sin fallidas ni omitidas en el [pipeline remoto 37699559905](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37699559905).
- Claves históricas: la base existente usa más versiones de las que quedan disponibles localmente. Queda bloqueada de forma segura la activación de Key Vault para esa base; no se sustituyen ni refirman registros históricos. [Evaluación de transición](docs/KEY_TRANSITION_ASSESSMENT_2026-10-02.md).
- H01, anclaje externo: la herramienta `tools/AnchorAudit` creó y verificó un punto de control de la base central en Azure Blob Storage. El contenedor privado tiene retención de 30 días, todavía desbloqueada por decisión operativa; no se declara H01 cerrado hasta bloquear una retención aprobada y conservar la evidencia de operación.
- Los archivos nuevos se centralizan en `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts` por subcarpetas. El contenido de ubicaciones antiguas se conserva por separado en `legacy_20261006`.

## Próximos pasos

1. Validación manual de copias completada: `Copia ahora`, `Programar`, `Cancelar` y `Probar 1 min` se probaron en la aplicación real, incluida la ejecución al llegar la hora, una única programación activa y la cancelación del temporizador al cerrar la vista. El acceso real de Administración y Recepción también se comprobó: credenciales erróneas y selección de área no autorizada se deniegan y auditan; ambos roles acceden y cierran sesión correctamente. Médico, todavía sin panel operativo, se deniega de forma controlada y auditada sin crear sesión; el desarrollo de su panel queda fuera del alcance de auditoría actual.
2. Objetivos provisionales aprobados: RTO 4 horas y RPO 24 horas ([detalle](docs/RECOVERY_OBJECTIVES.md)). El ensayo técnico aislado validó SHA-256, HMAC, versión de clave, reemplazo seguro y `integrity_check=ok`; existe además una copia externa verificada de la evidencia. La tarea independiente diaria ya cubre el caso de aplicación cerrada mientras el usuario haya iniciado sesión. Su primera ejecución automática y el ensayo tras suspensión quedaron verificados con resultado de tarea `0`, copia reciente y acompañantes íntegros; faltan confirmar ejecuciones continuadas y resolver las verificaciones históricas sin claves.
3. Resolver el tratamiento de las claves históricas antes de activar Key Vault para la base existente. El reenvío ya propaga los fallos de webhook y Blob a la cola para reintento o `dead-letter`; verificar que los destinos reales deduplican por `EventId`, ya que la entrega es al menos una vez. Login ya exige auditar antes de publicar sesión. Festivos, pacientes, citas, usuarios y horarios ya tienen transacción conjunta, pero el alta de paciente y su cita posterior no forman una sola operación atómica. Para H01, acordar y bloquear la retención definitiva del anclaje externo.
4. Verificar gestión de claves, cifrado en reposo de backups, permisos, retención, alertas y custodia.
5. Completar el paquete operativo firmado y la revisión independiente con responsable, fecha y evidencias antes de declarar cierre o preparar la entrega final.

## Alcance

Las pruebas existentes y sus resultados están documentados; cualquier ampliación debe responder a una carencia concreta. Las mejoras visuales, refactorizaciones y actualizaciones de dependencias no forman parte de esta actualización.

Este roadmap de la raíz es la referencia vigente para esta tarea. La copia de igual nombre en `docs/` no se ha actualizado ni validado aquí; no debe prevalecer sobre el estado de cierre enlazado.

## Nota rápida — ordenado de Auditoría (2026-10-02, comprobado 2026-10-07)

- La pantalla principal ya usa `SortMemberPath="FechaHora"` y aplica orden descendente por `FechaHora` al cargarse en `Views/AuditoriaView.xaml` y `Views/AuditoriaView.xaml.cs`.
- Las utilidades experimentales de `AuditorMenuWindow` y sus informes históricos se conservan como referencia, pero no representan un pendiente del grid principal.
- Sigue pendiente únicamente la validación manual en ejecución tras cargar, filtrar y actualizar registros.
