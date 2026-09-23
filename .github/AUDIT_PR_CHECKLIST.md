Se elimina la VM legacy `AuditoriaViewModel` y se deja `AuditoriaViewModelV2` como única implementación del panel Auditoría.

Cambios principales:
- UI: movido checkbox de filtro debajo de los controles para evitar solapamientos, ajustes de tamaños y márgenes de botones y controles.
- ViewModel V2: centralización de la lógica de backup, persistencia de horario programado y manejo de estado de programación.
- Tests: compilación y suite de tests locales (49/49) pasada.

Checklist para el auditor (validación manual):
- [ ] UI: Abrir la vista `Auditoría` y comprobar que el checkbox "Mostrar sólo operaciones de herramientas (export/restore/replay)" aparece debajo de los filtros y no se superpone a los `ComboBox`.
- [ ] UI: Verificar que los botones del toolbar (Filtrar, Limpiar, Actualizar, Últimos movimientos, Exportar CSV, Generar diagnóstico, Rotar HMAC/ENC) tienen altura uniforme y texto completo visible en resoluciones comunes.
- [ ] Backup - Copia ahora: Pulsar "Copia ahora" y confirmar que se crea la copia y aparece notificación/registro.
- [ ] Backup - Programar: Introducir hora válida (formato HH:mm) en "Hora copia:" y pulsar "Programar". Verificar que el estado de programación se refleja en la UI (`BackupScheduleStatus`) y que `IsBackupScheduled` muestra/oculta el botón "Cancelar".
- [ ] Backup - Probar 1 min: Pulsar "Probar 1 min" y comprobar que se ejecuta una copia de prueba en ~1 minuto y que el resultado se registra.
- [ ] Backup - Cancelar: Con una programación activa, pulsar "Cancelar" y confirmar que la programación se detiene y el estado se actualiza.
- [ ] Restaurar: Pulsar "Restaurar" y verificar que se abre el diálogo apropiado y la operación de restauración se completa (si procede en entorno de pruebas).
- [ ] Logs/diagnósticos: Ejecutar "Generar diagnóstico" y/o "Diagnósticos" desde el menú y comprobar que los ficheros/informes se generan y la UI muestra mensajes informativos.
- [ ] Pruebas automáticas: Ejecutar `dotnet test` local y confirmar que la suite pasa (49/49).

Criterios de aceptación:
- Todos los items del checklist deben marcarse como correctos o documentarse con evidencias (capturas/logs).
- No deben quedar elementos de la UI solapados o con texto recortado en resoluciones estándar.

Solicito al auditor que, tras verificar, añada comentario en esta PR con la aprobación o con los puntos detectados.
