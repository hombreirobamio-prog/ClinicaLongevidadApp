# Registro de sesiones de auditoría — ClinicaLongevidadApp

Este fichero recoge de forma cronológica las sesiones realizadas sobre el subsistema de auditoría: acciones ejecutadas, hallazgos, artefactos generados y siguientes pasos.

Formato recomendado por entrada:
- Fecha: YYYY-MM-DD
- Responsable: nombre/usuario
- Objetivo de la sesión
- Acciones realizadas (lista)
- Artefactos generados (rutas)
- Resultados / hallazgos
- Pendiente / siguientes pasos

## Entrada inicial
- Fecha: 2026-09-25
- Responsable: (registrar)
- Objetivo: Consolidar notas operativas extraídas de `README.md` y `docs/AUDIT_GUIDE.md` para auditores; crear punto central de registro.
- Acciones realizadas:
  - Se consolidaron las notas de `README.md` y `docs/AUDIT_GUIDE.md`.
  - Se definieron rutas de artefactos y checklist mínimo para el auditor.
  - Se creó este fichero para llevar el seguimiento de sesiones.
- Artefactos generados:
  - Quick diagnostics: `%LocalAppData%\\ClinicaLongevidadApp\\logs`
  - Integrity reports: `%ProgramData%\\ClinicaLongevidadApp\\AuditIntegrityReports`
  - Backups: `%LocalAppData%\\ClinicaLongevidadApp\\backups`
  - Logs: `%LocalAppData%\\ClinicaLongevidadApp\\logs\\backup.log`, `backup_vm.log`
- Resultados / hallazgos:
  - `AuditoriaService` implementa hashing encadenado, firma HMAC y cifrado AES‑GCM opcional.
  - Se requiere política de Key Vault en producción y pruebas adicionales (locks, rotación de claves).
- Pendiente:
  - Integrar tests de concurrencia para `RegistrarEvento`.
  - Documentar playbook de rotación de claves y restauración.
  - Añadir en CI la ejecución de diagnósticos básicos.

---

## Sesión: ejecución diagnósticos — 2026-09-24
- Fecha: 2026-09-24
- Responsable: Francisco
- Objetivo: Ejecutar diagnósticos automáticos (`tools/RunAuditDiagnostics`) y recopilar artefactos para análisis de integridad.
- Acciones realizadas:
  - Añadida utilidad `tools/RunAuditDiagnostics` y script `scripts/run_audit_diagnostics.ps1` para compilar y ejecutar diagnósticos reproducibles.
  - Ejecutado `scripts/run_audit_diagnostics.ps1 -UseLocalAppDataDb` (mediante `powershell -ExecutionPolicy Bypass` por política de ejecución).
  - La herramienta se compiló correctamente.
  - La ejecución devolvió `AuditoriaService.IsInitialized = False` y `VerifyIntegrity()` reportó 1 error: "Format of the initialization string does not conform to specification starting at index 0." — indica connection string malformada en la invocación por defecto.
  - El script listó artefactos previos disponibles en el sistema (no se generaron nuevos artefactos en esta ejecución debido al fallo de inicialización).
- Artefactos encontrados (existentes en el equipo):
  - `C:\Users\Francisco\AppData\Local\ClinicaLongevidadApp\logs\IntegrityQuickSummary_20260924_110727.txt`
  - `C:\Users\Francisco\AppData\Local\ClinicaLongevidadApp\logs\IntegrityQuickSummary_20260924_003020.txt`
  - `C:\Users\Francisco\AppData\Local\ClinicaLongevidadApp\logs\IntegrityProblemRows_20260924_110727.csv`
  - `C:\Users\Francisco\AppData\Local\ClinicaLongevidadApp\logs\IntegrityProblemRows_20260924_003020.csv`
  - `C:\Users\Francisco\AppData\Local\ClinicaLongevidadApp\logs\backup.log`
- Resultados / hallazgos:
  - La ejecución automática requiere una connection string válida; la invocación por defecto usada por el script no apuntó a una BD válida y por ello `AuditoriaService` no inicializó.
  - Existen informes previos en `%LocalAppData%` que contienen filas problemáticas; deben revisarse para identificar discrepancias de `Hash`/`Signature`.
- Pendiente / siguientes pasos:
  1. Localizar el fichero de BD correcto en `%LocalAppData%\\ClinicaLongevidadApp` y re-ejecutar el script con: `scripts/run_audit_diagnostics.ps1 -ConnectionString "Data Source=<ruta_completa_a_db>"`.
  2. Si procede, pegar aquí el contenido (primeras 50-100 líneas) de `IntegrityQuickSummary_*.txt`, `IntegrityProblemRows_*.csv` y las últimas ~200 líneas de `backup.log` para análisis.
  3. Tras validar integridad, realizar prueba de `RestoreBackup` en sandbox y documentar resultados.
  4. Ajustar la utilidad para tomar por defecto la BD concreta (mejor manejo de cadena de conexión) y agregar validación de entrada para evitar fallos por formato.

---

Instrucciones: al terminar cada sesión añade una nueva entrada con la estructura indicada y actualiza "Pendiente" con tareas y responsables.


## Cierre de sesion 2026-09-30

- Responsable de la sesión: Francisco; actualización documental: Codex.
- Objetivo: conservar el estado de trabajo y preparar la continuación del 01/10/2026.
- Estado: auditoría operativa abierta. Árbol local en rama master, HEAD 9179285, con cambios de código, pruebas, scripts y documentación pendientes de consolidar en Git. Este cierre no crea commit ni publica cambios.

### Lo realizado y disponible

- Correcciones acumuladas del 29/09: verificación de enlaces y versiones de claves, autenticación de manifiestos y backups, retirada de FORCE_ADMIN, denegación de confirmaciones silenciosas, propagación de fallos de auditoría, cifrado obligatorio fuera de Development/Test y payload v2.
- Pipeline y paquete técnico autenticado implementados localmente; herramientas de integridad en solo lectura. Las validaciones previas documentan 8/8 escenarios de manifiestos y 7/7 de paquetes.
- Recuperación sintética ampliada a trece escenarios. El 30/09 se verificó active-sqlite (1/1) y la suite previa a H04 (91/91); se conserva la evidencia del fallo anterior.
- H04 parcial, festivos: crear, actualizar y eliminar confirman negocio y evento en una misma transacción SQLite; rollback ante fallo. Suite de esa entrega: 103/103.
- H04 parcial, pacientes: crear, editar y eliminar usan la transacción común; recepción deja de emitir éxitos anticipados y duplicados. Se conserva compatibilidad de campos y fechas. El alta implícita de paciente para una cita usa el mismo servicio, pero paciente y cita posterior no forman una única transacción.
- Archivos centrales para continuar: Services/AuditoriaService.cs, Services/FestivoService.cs, Services/PacienteService.cs, ViewModels/PanelRecepcionViewModel.cs y las pruebas FestivoAtomicityTests.cs y PacienteAtomicityTests.cs.
- Detalle de cambios y límites: [correcciones](AUDIT_REMEDIATION_2026-09-29.md), [recuperación](AUDIT_RECOVERY_DRILL_2026-09-29.md), [pipeline](AUDIT_PIPELINE_CURRENT.md).

### Validación conservada

- Última entrega: compilación Release sin errores ni warnings emitidos, según el registro de la entrega.
- Se ha leído el [TRX final](../artifacts/audit-fixes-20260930/h04-pacientes-full/tests.trx) al cerrar: 118 ejecutadas, 118 aprobadas, 0 fallidas y 0 omitidas. Finalización registrada: 30/09/2026 a las 00:25, hora de Madrid.
- En este cierre solo se actualiza documentación; no se vuelve a compilar ni ejecutar pruebas. La lectura del TRX confirma el resultado conservado, no una nueva validación del árbol.
- Se actualizan README.md, AUDIT_ROADMAP.md, AUDIT_HARDENING_REMINDER.md, AUDIT_CLOSURE.md y este registro para distinguir resultados históricos del estado vigente.

### Punto de reanudación para el 01/10/2026

1. Continuar H04 por citas: inspeccionar primero el servicio de persistencia y sus llamadores, localizar escrituras y eventos separados, y definir el cambio mínimo. La siguiente implementación todavía no se ha realizado.
2. Extender después la revisión a usuarios y otras operaciones pendientes. No dar por cerrada la atomicidad de toda la aplicación.
3. Validar manualmente permisos y UI con usuarios reales, incluyendo pacientes/festivos y Copia ahora, Programar, Cancelar y Probar 1 min; comprobar timers y handlers.
4. Configurar clave y versión de firma de paquetes en CI y conservar una ejecución remota verificable.
5. Resolver anclaje externo H01 y comprobar claves, cifrado, consumidores v2, alertas, retención, permisos y custodia.
6. Realizar ensayo operativo autorizado sobre una copia representativa aislada, con claves históricas, conexiones cerradas y RTO/RPO acordados.
7. Completar paquete operativo firmado, inventario y revisión independiente antes de declarar cierre. Revisar cambios locales y archivos nuevos al preparar su consolidación en Git.

No se han realizado en este cierre cambios de código, migraciones, restauraciones, rotación de claves ni publicaciones. Los criterios completos y responsables aún pendientes constan en [AUDIT_CLOSURE.md](AUDIT_CLOSURE.md).

## Continuacion H04 citas 2026-09-30

- Requisito y causa: continuar H04 por citas. El servicio persistía con sqlite-net antes de registrar auditoría y ocultaba sus errores. Recepción emitía eventos adicionales, incluida una confirmación de éxito previa al guardado.
- Persistencia: se mantiene SQLite y la representación de fechas en ticks. CitaService usa la transacción común de AuditoriaService para guardar cita y evento. Ante fallos de negocio, clave, inserción de auditoría o COMMIT se revierte todo; Id y FechaCreacion solo se asignan tras confirmar. Se rechaza guardar sin auditoría, con una base distinta o actualizar una cita inexistente.
- Recepción: se retiran los cuatro bloques de auditoría separados. Edición, cambios de estado, confirmación y cancelación trabajan sobre una copia; un fallo no altera la cita seleccionada. La cancelación de la sesión de edición se realiza después de persistir.
- Eventos: se conserva el contrato del servicio Cita.Crear/Cita.Actualizar, módulo Citas y detalles con Id, PacienteId, PacienteNombre, Fecha, Hora, Profesional y Estado. Ya no se generan los eventos adicionales Cita.Editar, Cita.Confirmar, Cita.CancelarProxima ni los otros nombres de cambios de estado de recepción; cualquier consumidor que dependa de ellos debe revisarse.
- Código: Services/CitaService.cs y ViewModels/PanelRecepcionViewModel.cs. Pruebas: CitaAtomicityTests.cs (nuevo), AuditoriaTests.cs, AuthorizationEnforcementTests.cs y ServiceAuditoriaIntegrationTests.cs. Los casos de persistencia de citas usan bases sintéticas aisladas.
- Validación: selección inicial 22/22; tras añadir los casos de fallo de recepción, compilación Release sin errores ni warnings emitidos y suite completa **130/130**, 0 fallidas y 0 omitidas. Evidencia final: [TRX](../artifacts/audit-fixes-20260930/h04-citas-full/tests.trx). El primer intento de compilación quedó bloqueado por acceso del sandbox al SDK de Windows; la ejecución autorizada posterior terminó correctamente. Comprobación de formato Git sin errores.
- Documentación actualizada: README, roadmap, recordatorio de seguridad, estado de cierre y este registro.
- Pendientes: continuar H04 por usuarios y demás operaciones; validación manual de UI/permisos y consumidores de eventos. El alta implícita de paciente y la cita posterior siguen siendo dos transacciones independientes. H01 y los pendientes operativos anteriores permanecen abiertos.
- Cambios locales sin commit ni publicación. No se ejecutaron migraciones ni operaciones sobre datos de producción como parte de la implementación de citas.

## Continuacion H04 usuarios 2026-09-30

- Requisito y causa: continuar H04 por usuarios. Guardar, eliminar y restablecer contraseña persistían antes de registrar auditoría, ocultando los errores del registro; los ViewModels añadían eventos de éxito separados.
- Persistencia: UsuarioService mantiene SQLite, las fechas en ticks y las firmas públicas. Usa RegistrarEventoConOperacion para confirmar cada escritura junto a su evento. El Id, la fecha de creación y el hash del objeto se asignan tras COMMIT. Ante fallo de negocio, clave, auditoría o COMMIT se revierte todo. Se rechazan auditoría ausente, base distinta y actualizaciones/eliminaciones/restablecimientos de usuarios inexistentes.
- Contraseñas y permisos: se conserva PBKDF2 y la comprobación de administrador para guardar/eliminar; el restablecimiento permite administrador o propio usuario según la configuración existente de autorización. Lee la identidad dentro de la misma transacción, actualiza solo PasswordHash y devuelve la contraseña temporal únicamente tras confirmar. No registra contraseñas ni hashes.
- Eventos: se mantienen Usuario.Crear, Usuario.Actualizar, Usuario.Eliminar y Usuario.RestablecerContraseña. El evento de actualización añade RolAnterior, AreaAnterior, ActivoAnterior y PasswordCambiada, sin secretos. El de eliminación conserva Id y añade NombreUsuario. Se retiran los éxitos adicionales de los ViewModels (incluidos CambiarRol, CambiarArea, Activar/Desactivar, Editar y RestablecerPassword); sus consumidores requieren revisión. Se conservan los intentos de registrar fallos desde los ViewModels, sin garantía transaccional.
- Archivos de código: Services/UsuarioService.cs, ViewModels/UsuarioFormViewModel.cs y ViewModels/UsuariosViewModel.cs.
- Pruebas: UsuarioAtomicityTests.cs (nuevo), UsuarioServiceAuditoriaTests.cs y AuthorizationEnforcementTests.cs. Las escrituras de usuarios en estas pruebas usan bases sintéticas aisladas; se verifican todos los campos, contraseñas, permisos, integridad y rollback.
- Validación específica: **31/31 aprobadas**. [TRX específico](../artifacts/audit-fixes-20260930/h04-usuarios-targeted/tests.trx). El acceso al SDK de Windows requirió ejecución fuera del sandbox.
- Pendientes: inventariar otras escrituras no adaptadas antes de cerrar H04; revisar por separado el inicio de sesión y sus fallos de auditoría. Verificar manualmente formularios, permisos y consumidores. Paciente y cita posterior siguen siendo transacciones independientes. Los pendientes de H01, CI, recuperación operativa y revisión independiente siguen abiertos.
- Cambios locales sin commit ni publicación; sin migraciones ejecutadas.
- Validación final de esta entrega: compilación Release sin errores ni warnings emitidos y suite completa **151/151**, 0 fallidas y 0 omitidas. [TRX final](../artifacts/audit-fixes-20260930/h04-usuarios-full/tests.trx). Comprobación de formato Git sin errores. README, roadmap, recordatorio y estado de cierre actualizados.

## Continuacion H04 login 2026-09-30

- Causa: LoginViewModel publicaba la sesión y notificaba a sus suscriptores antes de registrar Login.Correcto; el método de registro ocultaba excepciones.
- Cambio: registro confirmado antes de sesión/notificación/navegación, con rol y área explícitos. Mensaje genérico ante error y liberación de reentrada. Credenciales comprobadas sobre una sola lectura; contador de fallos actualizado antes de auditar. Constructor público conservado.
- Archivos: ViewModels/LoginViewModel.cs y ClinicaLongevidadApp.Tests/LoginAuditTests.cs (nuevo).
- Pruebas específicas: 11/11 aprobadas. Casos de éxito, fallos de clave/inserción/COMMIT, reintento, credenciales/usuario/área denegados, bloqueo aunque falle auditoría, normalización del nombre y limpieza de intentos tras éxito. [TRX específico](../artifacts/audit-fixes-20260930/h04-login-targeted/tests.trx).
- Inventario y límites: [H04_INVENTARIO_2026-09-30.md](H04_INVENTARIO_2026-09-30.md). La sesión en memoria y la navegación no forman parte de una transacción SQLite; se garantiza el orden, no atomicidad entre esos recursos.
- Próximo paso: HorarioProfesionalService.Guardar. Después, administración y durabilidad de cola; rotación de claves y recuperación requieren revisión propia. Mantener los pendientes operativos anteriores y validación manual.
- Cambios locales sin commit ni publicación.
- Validación final: Release sin errores ni warnings emitidos, **162/162 pruebas aprobadas**, 0 fallidas y 0 omitidas. [TRX completo](../artifacts/audit-fixes-20260930/h04-login-full/tests.trx). Corregida durante la validación una aserción nueva que trataba Resultado como booleano en lugar del texto ERROR del DTO. Comprobación de formato Git correcta; documentación de estado actualizada.

## Continuacion H04 horarios 2026-09-30

- Causa: HorarioProfesionalService.Guardar persistía con Insert/Update de sqlite-net sin evento de auditoría.
- Cambio: alta y actualización mediante la conexión de RegistrarEventoConOperacion. Eventos HorarioProfesional.Crear/HorarioProfesional.Actualizar, módulo Horarios, con todos los campos persistidos en detalles. El Id solo se asigna tras COMMIT.
- Se rechaza auditoría ausente, base de auditoría distinta y actualización de un horario inexistente. Los fallos de negocio, clave, inserción de auditoría y COMMIT revierten negocio y evento.
- Archivos: Services/HorarioProfesionalService.cs y ClinicaLongevidadApp.Tests/HorarioProfesionalAtomicityTests.cs (nuevo). Se conservan firma pública, campos, representación SQLite y lógica de disponibilidad, horarios predeterminados y citas canceladas.
- Alcance: no se encontraron llamadores de Guardar en las vistas revisadas; recepción utiliza consultas de horarios. No se introduce una política de roles que no existía en este servicio.
- Pruebas nuevas: 12 casos sobre bases sintéticas, con compatibilidad de todos los campos mediante lectura sqlite-net, un evento por guardado, integridad y rollback.
- Próximo paso: administración de cola (RequeueDeadLetter/DeleteDeadLetter), revisando primero llamadores y permisos; después durabilidad del reenvío y rotación según el inventario. H04 permanece abierto.
- Cambios locales sin commit ni publicación; no se ejecutaron migraciones ni escrituras de horarios sobre datos operativos.
- Validación final: compilación Release sin errores ni warnings emitidos; **174/174 pruebas aprobadas**, 0 fallidas y 0 omitidas. [TRX completo](../artifacts/audit-fixes-20260930/h04-horarios-full/tests.trx). Comprobación de formato Git correcta. Actualizados README, roadmap, recordatorio, inventario y estado de cierre.

## Continuacion H04 administracion de cola 2026-09-30

- Causa: reencolar movía registros transaccionalmente entre tablas pero no auditaba la intervención; eliminar tampoco auditaba. Ambos podían devolver true sin afectar ninguna fila. El ViewModel no mostraba el fallo.
- Cambio: RequeueDeadLetter y DeleteDeadLetter usan RegistrarEventoConOperacion sobre la misma base. Se confirma la intervención junto a AuditQueue.Reencolar/AuditQueue.Eliminar. Referencian DeadLetterId y EventIdAfectado sin copiar payload, firma ni errores de entrega.
- Reencolado conserva EventId, Payload, Signature y CreatedAt; reinicia Attempts a cero y programa NextAttemptAt al momento actual. El evento administrativo tiene su propio EventId.
- Permisos: comprobación de Administración con AuthorizationHelper, respetando su activación existente mediante AUDIT_ENFORCE_AUTH. No equivale a hacer obligatoria globalmente esa configuración; queda verificarla en despliegue.
- Contrato público bool conservado: false ante permisos insuficientes, auditoría ausente, bases diferentes, fila inexistente o cualquier fallo. Solo true tras COMMIT. El ViewModel muestra un mensaje genérico cuando recibe false.
- Archivos: Services/AuditAdminService.cs, ViewModels/AuditAdminViewModel.cs y ClinicaLongevidadApp.Tests/AuditAdminAtomicityTests.cs (nuevo).
- Pruebas nuevas: 14 casos sobre bases sintéticas. Éxito, conservación del contenido, un evento por intervención, repetición sin falso éxito, rollback de inserción/eliminación/firma/auditoría/COMMIT, contexto inválido, permisos y competencia entre dos intervenciones (un único ganador).
- Límites: el evento administrativo puede seguir el reenvío normal; no se añade auditoría recursiva al mantenimiento del worker. La entrega durable entre registro y reenvío sigue pendiente. No se efectuaron intervenciones sobre colas operativas.
- Próximo paso: durabilidad del reenvío en AuditoriaService/AuditForwardQueue; después rotación según el inventario. H04 permanece abierto, junto con validación manual y pendientes operativos.
- Cambios locales sin commit ni publicación.
- Validación: suite completa **188/188**, sin fallidas ni omitidas ([TRX](../artifacts/audit-fixes-20260930/h04-cola-admin-full/tests.trx)). Corregido el warning xUnit2031 de una aserción nueva; compilación Release posterior sin errores ni warnings emitidos y **14/14** casos afectados aprobados ([TRX final](../artifacts/audit-fixes-20260930/h04-cola-admin-final/tests.trx)). Comprobación de formato Git correcta. Estado documental actualizado.

## Validacion de administracion de cola 2026-09-30

- Solicitada una validación de la última entrega, sin avanzar a durabilidad del reenvío.
- Revisión local de AuditAdminService, sus llamadas desde AuditAdminViewModel, pruebas y dependencias transaccionales/de autorización. No se detectaron defectos nuevos en el alcance revisado.
- Nueva ejecución Release del estado actual: **188/188 pruebas aprobadas**, 0 fallidas y 0 omitidas; sin errores ni warnings emitidos. [TRX de validación](../artifacts/audit-fixes-20260930/h04-cola-admin-validation/tests.trx).
- Comprobación de formato Git correcta. Se mantienen pendientes la validación manual WPF, la comprobación de AUDIT_ENFORCE_AUTH=1 en despliegue y la durabilidad del reenvío. No se han validado usuarios reales ni operado sobre colas de producción.
- Sin cambios de código en esta validación. Solo se actualiza este registro y la evidencia de cierre.

## Preparacion de validacion manual de cola 2026-09-30

- A petición del usuario, se prepara VALIDAR_COLA.cmd y la herramienta independiente tools/AuditAdminManual para facilitar la validación manual sin usar la base habitual.
- Base temporal nueva por ejecución, cuatro registros sintéticos, claves aleatorias en memoria, sin App.OnStartup ni destinos de reenvío. Selector de rol y simulación de fallo de firma; usa la vista real y los servicios públicos. No valida el login ni los permisos del menú de la aplicación.
- Guía: docs/VALIDACION_MANUAL_COLA.md. Informe resultado.txt en la carpeta temporal indicada por la herramienta.
- Compilación y --self-test correctos: denegación por rol, rollback ante fallo de firma, éxito de reencolado/eliminación, integridad y construcción de AuditAdminView. Sin errores ni warnings emitidos en la ejecución final. La revisión visual interactiva queda pendiente del usuario.
- No se modifica código de producción ni se añaden paquetes. No se repite la suite principal, ya validada con 188/188, porque el cambio es una herramienta separada. Sin commit ni publicación.

## Validacion manual de cola: flujo correcto 2026-09-30

- Evidencia visual aportada por el usuario desde la prueba aislada: 2 pendientes, 1 fallido y 0 errores de integridad.
- Eventos mostrados: `AuditQueue.Reencolar — PRUEBA-01 — OK`, `AuditQueue.Eliminar — PRUEBA-02 — OK` y `AuditQueue.Reencolar — PRUEBA-03 — OK`.
- El resultado coincide con el flujo esperado: reencolar PRUEBA-01, eliminar PRUEBA-02 y reencolar PRUEBA-03 tras restaurar las condiciones de prueba. La base de prueba permaneció aislada.
- Pendiente, si no se comprobó visualmente durante el flujo: registrar por separado que Recepción y la simulación de fallo mostraron el mensaje de error y conservaron PRUEBA-03. La validación del rol y del rollback ya está cubierta por las pruebas automáticas.

## Continuacion H04 reenvio durable 2026-09-30

- Causa: AuditoriaService confirmaba el evento y después intentaba reenviarlo en segundo plano; si el proceso terminaba antes de insertar el reintento, el evento quedaba sin salida persistida.
- Cambio: si existe webhook o exportador configurado, AuditoriaService inserta la fila de AuditForwardQueue en la misma transacción SQLite que Auditoria. Si falla el outbox, se revierte el evento. Se retira el envío inmediato duplicado; el worker es el único responsable de entregar y borrar la fila tras éxito de todos los destinos.
- Concurrencia y semántica: el worker ignora ejecuciones solapadas dentro del proceso. La entrega es al menos una vez: si el proceso cae tras aceptar un destino y antes de borrar la fila, puede reenviar al reiniciar. El destino debe deduplicar por EventId. No se afirma entrega exactamente una vez ni atomicidad entre SQLite y un servicio externo.
- Archivos: Services/AuditoriaService.cs, Services/AuditForwardQueue.cs, ClinicaLongevidadApp.Tests/AuditForwardQueueTests.cs y ClinicaLongevidadApp.Tests/WebhookForwarderIntegrationTests.cs.
- Pruebas: se verifica inserción inmediata del outbox, rollback cuando falla su inserción, entrega posterior por worker, cola vacía tras éxito y paso a dead-letter. La prueba de webhook se adapta al orden durable: evento, cola, worker, destino.
- Validación: primer intento completo detectó una prueba antigua que esperaba envío inmediato (188/189); se corrigió la prueba al nuevo contrato. Pruebas específicas 5/5 y validación final Release sin errores ni warnings emitidos, suite completa **189/189**, 0 fallidas y 0 omitidas. [TRX específico](../artifacts/audit-fixes-20260930/h04-forwarding-targeted-final/tests.trx), [TRX final](../artifacts/audit-fixes-20260930/h04-forwarding-final/tests.trx). Comprobación de formato Git correcta.
- Próximo paso: rotación de claves y verificación operativa de destinos, idempotencia, recuperación y alertas. Cambios locales sin commit ni publicación.

## Continuacion H04 rotacion de claves 2026-09-30

- Causa: la fábrica de rotación interpretaba únicamente `REQUIRE_KEYVAULT=true`, mientras que el inicio de la aplicación usa `REQUIRE_KEYVAULT=1`. Con `1` podía escoger el proveedor local. Además, los proveedores y la UI ocultaban errores de persistencia o de auditoría y podían anunciar una rotación correcta sin evidencia del evento.
- Cambio: la fábrica reconoce `1` y `true`, y no permite proveedor local cuando Key Vault es obligatorio ni en producción. El inicio ya no sustituye Key Vault por el proveedor local cuando dicho entorno lo exige. Los errores de escritura en Azure se propagan. Tras persistir una clave, la pantalla registra las versiones anterior y nueva, sin secreto; si ese registro falla, muestra que la clave ya se guardó y que la auditoría está pendiente, sin anunciar éxito.
- Límite: SQLite y Key Vault no tienen una transacción compartida. La recuperación de una rotación interrumpida requiere conservar y comprobar el acceso a las versiones históricas de ambos secretos en Key Vault.
- Pruebas: casos específicos de rotación **10/10** y suite Release completa **196/196**, sin fallos ni omitidas. [TRX específico](../artifacts/audit-fixes-20260930/h04-key-rotation/key-rotation-tests.trx), [TRX completo](../artifacts/audit-fixes-20260930/h04-key-rotation/h04-key-rotation-full.trx). Comprobación de formato Git correcta.
- Próximo paso: validación operativa controlada en un entorno con Key Vault, verificando lectura de versiones históricas antes y después de una rotación, seguida de integridad. Cambios locales sin commit ni publicación.

## Seguimiento H04 alineacion de secretos de rotacion 2026-09-30

- Hallazgo: el proveedor empleado por los botones de rotación usaba los nombres fijos `audit-hmac-key` y `audit-enc-key`, mientras que AuditoriaService firma y descifra con los nombres configurados `AUDIT_HMAC_SECRET_NAME` y `AUDIT_ENC_SECRET_NAME`. Una rotación podía escribir un secreto diferente al que consume la auditoría.
- Cambio: el proveedor de rotación exige y utiliza los dos nombres configurados por la auditoría. Si falta Key Vault o cualquiera de esos nombres, la rotación se rechaza antes de escribir. La guía operativa requiere comprobar ambos nombres y el acceso de lectura a sus versiones históricas.
- Validación: suite Release completa **196/196**, 0 fallidas y 0 omitidas, sin avisos de compilación relacionados. [TRX](../artifacts/audit-fixes-20260930/h04-key-rotation/h04-key-rotation-alignment-full.trx). Comprobación de formato Git correcta.
- Próximo paso: ejecutar la validación controlada contra Key Vault real con identidad autorizada; confirmar lectura de una versión histórica y verificar integridad antes de rotar en producción. Cambios locales sin commit ni publicación.

## Preparacion de validacion H04 Key Vault 2026-09-30

- Se añade `tools/RotateKeys -- verify`, una comprobación de solo lectura. Obtiene la versión activa HMAC y ENC y confirma que ambas pueden leerse otra vez por versión explícita. No imprime ni guarda material de clave y devuelve código distinto de cero ante configuración incompleta o acceso insuficiente.
- En este equipo no están configurados `KEYVAULT_URI`, `AUDIT_HMAC_SECRET_NAME` ni `AUDIT_ENC_SECRET_NAME`; la ejecución devuelve el estado esperado `Configured: False`, sin conexión a Key Vault ni exposición de secretos. La herramienta compila correctamente en Release.
- Ejecución autorizada: `dotnet run --project tools/RotateKeys -- verify`. Requiere una identidad con permisos de lectura sobre las versiones actual e histórica de ambos secretos.
- Próximo paso: ejecutar ese comando en el entorno autorizado; si devuelve éxito, realizar una rotación controlada y verificar integridad. Cambios locales sin commit ni publicación.

## Correccion de validacion H04 Key Vault 2026-09-30

- Hallazgo de validación manual: un valor inválido de `KEYVAULT_URI` producía una excepción técnica antes de que la herramienta pudiera informar del problema.
- Cambio: el proveedor valida que la dirección sea HTTPS absoluta con host y la herramienta crea sus servicios dentro de su manejo de errores. Ahora informa `KEYVAULT_URI debe ser una dirección HTTPS válida` y termina con código 4, sin conexión ni secretos.
- Validación: ejecución Release con URI ficticia inválida, mensaje esperado y código 4. Próximo paso: sustituir el valor por la dirección real de Azure Key Vault, con formato `https://mi-almacen.vault.azure.net/`, y repetir `tools/RotateKeys -- verify`.

## Validacion manual H04 Key Vault 2026-09-30

- Se creó un Key Vault de prueba independiente y se configuraron los secretos `audit-hmac-key` y `audit-enc-key`, con el rol de datos de secretos asignado a la identidad operadora. No se incluyó material de clave en el registro.
- Tras instalar Azure CLI e iniciar sesión, `tools/RotateKeys -- verify` confirmó `Configured: True` y lectura explícita correcta de la versión activa de HMAC y ENC. La herramienta no muestra ni persiste los valores de los secretos.
- Alcance: esta comprobación acredita conectividad, autorización y recuperación por versión de las versiones activas. Aún no prueba una versión anterior, porque el almacén de prueba acaba de crearse.
- No activar `REQUIRE_KEYVAULT=1` ni ejecutar una rotación sobre la base de auditoría existente hasta definir la transición de los registros históricos que fueron firmados con claves locales. Cambios locales sin commit ni publicación.

## Cierre de sesion 2026-10-01

- Documentación actualizada en README, guía del auditor y este registro: correcciones de rotación, Key Vault de prueba, configuración de secretos, acceso autenticado con Azure CLI y validación de lectura por versión.
- Estado disponible: `audit-hmac-key` y `audit-enc-key` habilitados en el almacén de prueba; `tools/RotateKeys -- verify` confirmó configuración y recuperación explícita de sus versiones activas, sin exponer valores.
- Pendiente de mañana: definir y probar la transición de los eventos firmados con claves locales, antes de activar Key Vault en la base de auditoría existente o rotar en ella. Después, crear una segunda versión en el almacén de prueba y validar lectura de versión anterior y nueva junto con integridad.
- No se realizaron commits, publicaciones ni cambios sobre la base de auditoría habitual. Los cambios permanecen locales.

## Evaluacion de transicion de claves historicas 2026-10-02

- Inventario de solo lectura: la base de auditoría existente referencia 12 versiones HMAC no vacías y 8 versiones de cifrado no vacías. El almacenamiento local actual conserva únicamente una versión activa de cada tipo.
- Conclusión: no es seguro activar todavía Key Vault para esa base. Las claves actuales no bastan para verificar todo el historial y las claves nuevas no pueden sustituir las firmas existentes.
- No se leyó, copió ni documentó material de claves; no se modificaron la base, las copias ni el almacén de prueba. Los archivos `.hmac.ver` de los backups sólo identifican versiones, no recuperan una clave.
- Se documenta el plan de recuperación en [KEY_TRANSITION_ASSESSMENT_2026-10-02.md](KEY_TRANSITION_ASSESSMENT_2026-10-02.md): recuperar material autorizado, probarlo sobre una copia aislada, mapear cada identificador histórico a una versión inmutable de Key Vault y usar un proveedor de transición sin sustituciones silenciosas.
- Próximo paso: localizar una custodia segura de las claves históricas. Si no existen, conservar los registros intactos y tratarlos como no verificables criptográficamente; no regenerar ni volver a firmar evidencia histórica.
