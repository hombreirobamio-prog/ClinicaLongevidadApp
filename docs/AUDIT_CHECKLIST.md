# Checklist de auditoría

Actualización: 29/09/2026. **Auditoría operativa abierta.** Utilizar junto a la [guía vigente](AUDIT_GUIDE.md) y el [estado de cierre](AUDIT_CLOSURE.md).

Las casillas corresponden a la ejecución que se va a documentar; no se consideran completadas por resultados de sesiones anteriores.

## Preparación

- [ ] Identificar responsable, fecha, revisión de código, cambios locales y entorno examinado.
- [ ] Disponer de acceso autorizado y claves/versiones aprovisionadas sin exponer secretos.
- [ ] Definir copia protegida, destino aislado y autorización específica si se realizará una recuperación operativa.
- [ ] Acceder a la UI mediante autenticación normal; comprobar permisos con usuarios reales. No utilizar el retirado `FORCE_ADMIN` ni considerar `SILENT_MODE` una autorización.

## Pruebas técnicas

- [ ] Ejecutar la suite .NET en una carpeta nueva, registrar hora de inicio y conservar TRX, incluyendo fallos.
- [ ] Ejecutar las regresiones de manifiestos y paquetes; revisar códigos de salida.
- [ ] Generar el paquete con `-TestResultsPath` y `-RunStartedUtc`, clave y versión; rechazar resultados fallidos, omitidos o antiguos.
- [ ] Verificar el ZIP concreto con `scripts/verify_audit_package.ps1 -ZipPath <ruta>`.
- [ ] Identificar el alcance `technical-tests-only`: el paquete no contiene backups ni demuestra recuperación operativa.
- [ ] Conservar por separado los informes del ensayo sintético según su [procedimiento](AUDIT_RECOVERY_DRILL_2026-09-29.md).

## Integridad, firmas y backups

- [ ] Ejecutar las herramientas de solo lectura sobre una base existente; revisar códigos de salida e informes según la guía.
- [ ] Comprobar criptográficamente SHA256 y HMAC del backup, versión exacta y disponibilidad de la clave histórica. La mera existencia de acompañantes no basta.
- [ ] Verificar un manifiesto histórico explícito con `-ManifestPath` y `-RequireHmac` cuando corresponda; no mezclarlo con el formato del paquete técnico.
- [ ] Tratar una clave no disponible como verificación incompleta, sin afirmar manipulación ni aceptar la copia como autenticada.
- [ ] No regenerar firmas, modificar registros ni ejecutar backfill para hacer que una evidencia pase los controles.

## Validación operativa

- [ ] Comprobar Copia ahora, Programar, Cancelar, Probar 1 min y liberación de recursos al cerrar la vista; registrar resultados y ausencia de programaciones duplicadas.
- [ ] Realizar el ensayo autorizado sobre una copia representativa con conexiones cerradas y destino aislado; comprobar estado previo, recuperación exacta, integridad y manejo de interrupciones.
- [ ] Comparar tiempos y pérdida de datos con RTO/RPO acordados. El ensayo sintético no acredita objetivos de producción.
- [ ] Verificar Key Vault, cifrado efectivo, versiones históricas, permisos, cifrado en reposo de backups, retención, alertas y custodia.
- [ ] Configurar y verificar CI remoto; conservar la evidencia de esa ejecución. La configuración local no acredita ejecución remota.

## Criterios de cierre

- [ ] Resolver o someter a aceptación formal documentada los hallazgos pendientes, incluidos anclaje externo y relación durable negocio/auditoría.
- [ ] Completar paquete operativo firmado con backup autenticado, informe de integridad nuevo, resultado de recuperación y custodia independiente.
- [ ] Registrar revisión independiente, responsable, fecha y evidencias verificadas en el estado de cierre.

Una suite aprobada, un ZIP generado o una publicación histórica no bastan para cerrar la auditoría. La escritura actual exige HMAC y versión; la restauración exige SHA256, HMAC y versión verificables.

## Si falla una comprobación

Conservar salida y evidencias; identificar si el problema es de integridad, disponibilidad de claves, configuración o ejecución. No aumentar reintentos ni cambiar código como paso automático de remediación. Abrir una tarea concreta con el fallo reproducible y el cambio mínimo propuesto.

Esta actualización documental no ejecuta estas comprobaciones ni marca casillas como completadas.
