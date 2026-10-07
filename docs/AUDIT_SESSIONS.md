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

## Validacion de claves historicas no disponibles 2026-10-06

- Se verificó el comportamiento ya implantado para una versión de clave histórica no disponible: `VerifyIntegrity` la informa explícitamente como `signature unverifiable (exact key version unavailable)`; no la acepta como una firma correcta ni intenta usar la clave actual como sustituta.
- Validación: prueba Release específica `AuditoriaIntegrityTests.VerifyIntegrity_ReportsUnavailableHistoricalKey`, **1/1 aprobada**, sin fallos ni omitidas.
- Se actualizó la guía del auditor con la interpretación y actuación obligatoria ante ese resultado: conservar el registro, declararlo no verificable mientras falte la clave y no volver a firmarlo.
- No se modificaron la base de auditoría, las claves, Key Vault ni la configuración de ejecución.

## Actualizacion de evidencia y ubicaciones 2026-10-06

- Validación Release completa posterior: **196/196 pruebas aprobadas**, sin fallos ni omitidas. Evidencia: [TRX](../artifacts/validation-20261006/full-tests.trx). La compilación previa terminó sin errores ni advertencias.
- Las salidas nuevas se concentran en `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts`, con subcarpetas `backups`, `keys`, `logs`, `reports\integrity`, `reports\diagnostics`, `exports` y `audit_artifacts`. Los avisos de copia manual y programada indican su ruta completa; el CSV propone `exports`.
- Las carpetas antiguas de Local, Roaming y ProgramData se trasladaron sin sobrescrituras a `legacy_20261006`, bajo la raíz central y separadas por origen. No se borró contenido.
- Se actualizaron README, estado de cierre, roadmap y guía del auditor. Sigue bloqueada la activación de Key Vault sobre la base existente por ausencia de claves históricas; no se refirma el historial.

## Copias con evidencia verificable 2026-10-06

- Se corrigió `BackupService`: una copia nueva sólo se devuelve si dispone de SHA-256, HMAC y versión de clave. Si falta la clave de firma o no puede escribirse cualquiera de esas evidencias, se rechaza la operación y se eliminan los artefactos incompletos.
- Pruebas específicas de copias: **8/8 aprobadas**, sin fallos ni omitidas. La nueva prueba confirma que no se crea una copia cuando falta el material de firma.
- Validación Release completa posterior: **197/197 pruebas aprobadas**, 0 fallidas y 0 omitidas. Evidencia: [TRX](../artifacts/validation-20261006/full-tests-final.trx).

## Scripts de copia centralizados 2026-10-06

- `scripts/backup.ps1` crea ahora el ZIP directamente en `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\backups`, salvo que se indique otra raíz mediante `-ArtifactsDir`; ya no genera una segunda copia en la ruta antigua.
- `scripts/generate_companions.ps1` busca las copias nuevas en esa misma subcarpeta. Ninguno de los dos scripts usa `legacy_20261006`.
- Validación: sintaxis de ambos scripts y sus opciones `-Help` comprobadas sin crear copias ni modificar datos.

## Scripts de preparación y diagnóstico centralizados 2026-10-06

- `scripts/check_audit_readiness.ps1` usa el registro de copias de la raíz activa y devuelve el código de error de `dotnet test` si las pruebas fallan; ya no puede informar una preparación correcta tras una suite fallida.
- `scripts/run_audit_diagnostics.ps1` resuelve la base y los informes desde la raíz activa. Sus salidas estándar y de error se conservan en `reports\diagnostics`; los informes de integridad se buscan en `reports\integrity`.
- Validación: sintaxis y opciones `-Help` de ambos scripts comprobadas sin ejecutar pruebas, diagnósticos ni modificar datos.

## Evidencia HMAC con versión obligatoria 2026-10-06

- `scripts/generate_companions.ps1 -IncludeHmac` ya no asigna la versión ficticia `1`. Requiere `AUDIT_HMAC_KEY` y `AUDIT_HMAC_KEY_VERSION` antes de procesar copias.
- La guía aclara que este procedimiento sólo es apropiado para copias nuevas y que no debe sobrescribir acompañantes de evidencia histórica.
- Validación: sintaxis y opción `-Help` comprobadas sin procesar copias ni utilizar claves.

## Validación de material HMAC para acompañantes 2026-10-07

- El generador exige que `AUDIT_HMAC_KEY` sea Base64 y descodifique al menos 32 bytes antes de crear cualquier acompañante. Se mantiene la versión obligatoria.
- Prueba aislada: una clave no Base64 fue rechazada sin crear archivos SHA-256 ni HMAC; los archivos de prueba se eliminaron al terminar.

## Verificación y regresiones de manifiestos 2026-10-07

- `scripts/verify_audit_manifest.ps1` ya no acepta una clave de texto libre ni una versión implícita `local`: exige Base64 de al menos 32 bytes y la versión exacta configurada.
- Los scripts de regresión de manifiesto y de paquete devuelven ahora el código 0 cuando todos sus casos terminan correctamente; antes podían conservar el código 1 de un caso negativo esperado.
- Validación: **8/8** escenarios sintéticos de manifiesto y **7/7** de paquete aprobados, ambos con código de salida 0. Se usaron claves y archivos sintéticos.

## Resultado fiable de diagnósticos 2026-10-07

- `scripts/run_audit_diagnostics.ps1` propaga el código de salida del proceso de diagnósticos y devuelve 124 cuando vence el tiempo máximo. Ya no puede declarar una ejecución correcta si el proceso interno falla o se cancela por tiempo.
- Validación: sintaxis y opción `-Help` comprobadas sin iniciar diagnósticos ni acceder a la base de datos.

## Preparación ejecutada en Release 2026-10-07

- `scripts/check_audit_readiness.ps1` ejecuta ahora `dotnet test --configuration Release`, alineando la comprobación de preparación con la configuración usada por la evidencia de auditoría.
- Validación: sintaxis y comprobación de formato correctas. La referencia vigente de la suite completa permanece en **197/197** pruebas aprobadas.

## Resumen de informes de integridad centralizado 2026-10-07

- `tools/summarize_integrity_report.ps1` prioriza el informe JSON más reciente de `reports\integrity` bajo la raíz activa; conserva `-Path` para revisar un informe elegido expresamente.
- Validación: informe JSON sintético resumido correctamente; el archivo temporal se eliminó al finalizar.

## Preparación rechaza copias incompletas 2026-10-07

- `scripts/check_audit_readiness.ps1` devuelve error si una copia registrada en el log no existe o si le faltan SHA-256, HMAC o versión de HMAC. Antes sólo informaba de la ausencia y podía finalizar correctamente.
- El resumen conserva el estado de presencia de los acompañantes, sin imprimir sus valores HMAC en consola.
- Validación: sintaxis y opción `-Help` comprobadas sin ejecutar pruebas ni leer copias.

## Acceso completo a informes desde Auditoría 2026-10-07

- El botón `Abrir diagnósticos` de la vista principal abre ahora `reports`, la carpeta común que contiene los informes de integridad y de diagnósticos, en lugar de abrir siempre sólo `reports\integrity`.
- Validación: compilación de la solución correcta, sin errores ni advertencias.

## ZIP de auditoría en la raíz central 2026-10-07

- `scripts/check_audit_readiness.ps1` busca ahora el ZIP de auditoría en `audit_artifacts` bajo la raíz activa, en lugar de consultar únicamente la raíz del repositorio.
- Validación: sintaxis, opción `-Help` y comprobación de formato correctas sin ejecutar pruebas ni leer artefactos.

## Informes predeterminados de GenerateIntegrity 2026-10-07

- `tools/GenerateIntegrity` guarda ahora su informe predeterminado en `reports\integrity`, alineado con el servicio, el resumidor y la interfaz. Una ruta indicada explícitamente se conserva sin cambios.
- Validación: compilación de `GenerateIntegrity` correcta, sin errores ni advertencias.

## Menú del auditor sin rutas compartidas antiguas 2026-10-07

- El menú del auditor detecta y propone ZIPs únicamente desde `audit_artifacts` y `backups` bajo la raíz central de usuario; ya no consulta ProgramData.
- Validación: compilación de la solución correcta, sin errores ni advertencias.

## Pendiente bloqueado: migración del proveedor local de claves 2026-10-07

- Se identificó que el proveedor local histórico persiste claves en archivos de texto y variables de entorno de usuario. No se modifica ahora porque las claves locales existentes pueden ser necesarias para verificar registros históricos.
- Retomar sólo después de localizar o recuperar el material histórico autorizado. Preparar y validar entonces una migración controlada a DPAPI o Key Vault sobre una copia aislada, sin modificar la base original ni volver a firmar eventos históricos.

## Migración heredada sin reemplazo automático 2026-10-07

- El arranque sólo copia una base heredada si aún no existe una base central. Se eliminó el reemplazo automático basado en fechas de archivo, que podía sustituir una base activa por una copia antigua.
- Validación: compilación de la solución correcta, sin errores ni advertencias.

## Key Vault obligatorio sin retorno local 2026-10-07

- Si `REQUIRE_KEYVAULT=1` o el entorno es Production, el arranque exige una inicialización válida de Key Vault y ambos nombres de secreto antes de crear el servicio de auditoría. Una URI inválida ya no permite volver silenciosamente al proveedor local.
- Validación: compilación de la solución correcta, sin errores ni advertencias.

## Key Vault obligatorio con comprobación de lectura 2026-10-07

- El arranque obligatorio ahora comprueba antes de crear el servicio de auditoría que puede leer HMAC y ENC, que tienen longitudes válidas y que las dos versiones activas están disponibles. Evita arrancar con URI y nombres correctos pero con permisos, contenido o versiones no utilizables.
- Límite: esta verificación consulta Key Vault durante el arranque obligatorio; si Azure no está disponible o faltan permisos, el arranque se bloquea deliberadamente.
- Validación: compilación de la solución correcta, sin errores ni advertencias.

## Preparación de anclaje externo H01 2026-10-07

- Se añadió `tools/AnchorAudit`, una herramienta que recibe una base explícita en modo solo lectura y usa Azure Blob Storage con `DefaultAzureCredential`. `write` crea un punto de control nuevo con el Id, EventId y hash final; `verify` comprueba que la fila anclada continúa presente e idéntica.
- No crea contenedores, no utiliza claves de cuenta y no modifica SQLite. Requiere una cuenta de almacenamiento, un contenedor privado con inmutabilidad y el rol mínimo Storage Blob Data Contributor para la identidad operadora.
- Validación local: compilación correcta, sin errores ni advertencias; ayuda comprobada. Falta aprovisionar el almacenamiento y ejecutar el primer anclaje verificable.

## Primer anclaje externo H01 verificado 2026-10-07

- Se creó la cuenta de almacenamiento `clongevityaudit2026` en Spain Central, con acceso anónimo y claves de cuenta deshabilitados, y el contenedor privado `audit-anchors`. La identidad operadora recibió el rol Storage Blob Data Contributor.
- `AnchorAudit write` creó el punto de control `audit/anchors/20261007T1330372053720Z_00000000000000002170_2808fcf40f40.json` para el Id de auditoría 2170. `AnchorAudit verify` confirmó que la fila y su hash continúan presentes. La herramienta abrió SQLite en modo solo lectura.
- El contenedor tiene retención de 30 días en estado desbloqueado, elegida para la prueba inicial. H01 sigue abierto: la retención definitiva debe aprobarse y bloquearse antes del cierre.

## Copias preventivas de la sesión 2026-10-07

- Se creó una copia coherente de la base activa mediante `BackupService`: `backups\ClinicaLongevidad_backup_20261007_134426025.db`. Incluye sus acompañantes SHA-256, HMAC y versión de HMAC.
- Se creó además la instantánea de código y documentación `backups\ClinicaLongevidadApp_backup_20261007_154437.zip`.
- Ambas salidas se comprobaron presentes y con tamaño mayor que cero. La aplicación estaba cerrada al iniciar la copia de SQLite.

## Verificación de integridad de la base activa 2026-10-07

- `tools/VerifyIntegrity` se ejecutó en modo solo lectura contra la base activa, después del primer anclaje externo.
- Resultado: 2.100 incidencias. Los registros iniciales devuelven hash almacenado vacío y no se pueden verificar sus firmas porque falta la versión histórica exacta de la clave.
- No se modificó la base ni se intentó volver a firmar ningún evento. La recuperación controlada del material histórico sigue siendo el bloqueo para cerrar esta parte de la auditoría.

## Búsqueda local de claves históricas 2026-10-07

- Se revisaron, sin leer contenido, las rutas de claves central, heredada y de perfil de usuario, además de los nombres de archivo administrados por la aplicación.
- No se localizaron archivos de claves históricas. Las variables de entorno de usuario contienen únicamente una clave HMAC y una clave de cifrado vigentes, con sus versiones actuales.
- La siguiente fuente posible es una copia anterior del perfil, otro equipo o un respaldo autorizado de claves. No se creó material nuevo ni se sustituyó ninguna clave.

## Informe de integridad posterior al anclaje 2026-10-07

- Se generó el informe de solo lectura `reports\integrity\IntegrityReport_20261007_post-anchor.json` sobre la base activa. Confirma las 2.100 incidencias ya detectadas por `VerifyIntegrity` y no modifica SQLite.
- `dotnet run --project tools/GenerateIntegrity` quedó bloqueado sin escribir informe; se terminó únicamente ese proceso. La ejecución directa del binario Release generó el informe correctamente. Este comportamiento de la invocación mediante `dotnet run` queda pendiente de revisión, sin afectar la evidencia generada.

## Prueba aislada de administración de auditoría 2026-10-07

- `tools/AuditAdminManual --self-test` terminó correctamente con datos ficticios. Verificó la cola, las restricciones de rol simuladas, la reversión ante fallo de auditoría y la construcción de la vista WPF.
- Esta prueba no sustituye la validación manual con usuarios reales ni accedió a la base operativa.

## Validación manual de Auditoría y copia 2026-10-07

- En la aplicación real se comprobó manualmente que la vista de Auditoría carga, se ordena y responde a filtrar y actualizar sin errores.
- La acción `Copia ahora` creó `backups\ClinicaLongevidad_backup_20261007_164738742.db`. Se confirmó la presencia de la base y de sus acompañantes SHA-256, HMAC y versión HMAC; los cuatro archivos tienen el tamaño esperado o están presentes.

## Corrección de la prueba de copia en un minuto 2026-10-07

- Se corrigió `TestScheduleInOneMinuteCommand`: la espera se ejecutaba en un hilo de fondo y después accedía a propiedades de WPF, por lo que la excepción quedaba oculta y no se creaba la copia.
- La continuación permanece ahora en el contexto de la interfaz hasta completar la copia única. Compilación Release correcta, sin errores ni advertencias.
- Falta reiniciar la aplicación y comprobar manualmente el botón `Probar 1 min`; la instancia abierta durante la compilación conservaba el ejecutable Debug anterior.

## Cancelación de copia de prueba pendiente 2026-10-07

- Se añadió `Cancelar prueba`, visible únicamente mientras está pendiente la copia única iniciada con `Probar 1 min`.
- Cancela la espera de esa prueba y limpia su estado visual sin modificar la programación diaria. El cierre de la vista también cancela una prueba pendiente.
- Validación: compilación Release correcta, sin errores ni advertencias. Comprobación manual superada después de reiniciar la aplicación: `Probar 1 min` muestra `Cancelar prueba` y la cancelación funciona correctamente.
- Comprobación manual adicional superada: al cerrar la vista de Auditoría antes del minuto, no se crea ninguna copia posterior.

## Validación manual de programación diaria 2026-10-07

- Se comprobó en la aplicación real que una copia diaria puede programarse, cancelarse antes de su ejecución y ejecutarse correctamente al llegar la hora.
- La copia programada más reciente fue `backups\ClinicaLongevidad_backup_20261007_171700012.db`; se verificó que conserva SHA-256, HMAC y versión HMAC.
- Con ello queda completada la comprobación manual de `Copia ahora`, `Programar`, `Cancelar` y `Probar 1 min` para una única programación activa.

## Visibilidad de la siguiente copia 2026-10-07

- El panel de copias muestra ahora de forma permanente `Siguiente copia: <fecha y hora>` cuando existe una programación; muestra `-- Ninguna --` cuando no la hay.
- Se corrigió el enlace del texto para que sea explícitamente de solo lectura (`OneWay`); evitaba una excepción al heredar un modo bidireccional de la vista.
- El estado de la programación ya no repite fecha y hora: el indicador fijo `Siguiente copia` es la única referencia temporal visible.
- Validación: compilación Release correcta, sin errores ni advertencias.

## Acceso restringido al rol seleccionado 2026-10-07

- El acceso desde las pantallas Administración, Recepción y Médico se valida contra el rol almacenado del usuario y su contraseña. El campo descriptivo `Área` ya no puede conceder acceso a una pantalla distinta.
- La comparación admite las variantes con y sin tildes (`Recepción`/`Recepcion` y `Médico`/`Medico`) para no bloquear usuarios válidos con datos heredados.
- Validación: 13 pruebas de inicio de sesión superadas, incluida la denegación cuando solo coincide el área; compilación Release correcta, sin errores ni advertencias.
- Validación manual superada: Auditoría registró `Login.FallidoCredenciales` para una contraseña incorrecta, `Login.AccesoNoAutorizadoArea` al intentar entrar desde Recepción en Administración y `Login.Correcto` para `admin` en Administración.
- Validación manual adicional superada: el usuario `recepcion` accedió a Recepción (`Login.Correcto`) y cerró su sesión (`Sesion.Cerrar`) antes de que `admin` volviera a entrar en Administración.
- El área Médico se mantiene visible como función futura, pero mientras no tenga panel operativo ya no publica una sesión ni registra un inicio correcto. Registra `Login.AreaNoDisponible` y muestra el aviso correspondiente.
- Validación: 14 pruebas de inicio de sesión aprobadas y compilación Release correcta, sin errores ni advertencias.

## Suite completa previa a CI 2026-10-07

- Se ejecutó la suite completa en Release antes de preparar la primera ejecución remota firmada: **200/200 pruebas aprobadas**, 0 fallidas y 0 omitidas.
- Evidencia local: `artifacts\validation-20261007\tests_20261007_ci_prep.trx`.

## Primera ejecución remota firmada de CI 2026-10-07

- Se publicó el commit `345303c` en `chore/centralize-paths` y se ejecutó manualmente `Audit pipeline` en GitHub Actions: [ejecución 37676469997](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37676469997).
- Resultado: correcto. Se completaron restauración, pruebas, regresiones de manifiesto/paquete y la creación/conservación del paquete técnico autenticado.
- Evidencia descargada: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261007-run-37676469997`; el ZIP del paquete tiene SHA-256 `40FB7D5E43234C4AC9B98674D2C9B7383629295EC5188D933B47E1FE3A0B5D32` e inventario `tests.trx`, `package-manifest.json` y `package-manifest.hmac`.

## Copia de cierre de sesión 2026-10-07

- Se creó la instantánea de código y documentación `backups\ClinicaLongevidadApp_backup_20261007_203703.zip` bajo `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts`.
- Verificación: 250842604 bytes; SHA-256 `DF3B11B84CD3FA1F07F5B61699C26AB83298254EC9341F0B9F14C73795C6A1E8`.
- Tras cerrar la aplicación, se creó además la copia coherente de SQLite `backups\ClinicaLongevidad_backup_20261007_193716798.db` mediante `BackupService`.
- Se verificó la presencia de la base y sus acompañantes `.sha256`, `.hmac` y `.hmac.ver`; el SHA-256 calculado coincide con el comprobante almacenado.

## Secretos de Key Vault con formato estricto 2026-10-07

- El proveedor de Azure Key Vault acepta únicamente HMAC Base64 de al menos 32 bytes y claves de cifrado Base64 de longitud AES válida (16, 24 o 32 bytes). Ya no convierte valores de texto libre en material de clave.
- Validación: compilación de la solución correcta, sin errores ni advertencias.

## Backfill con base explícita 2026-10-06

- `scripts/run_backfill.ps1` ya no selecciona una base automáticamente. Requiere `-DbPath` para una copia aislada y autorizada, evitando que el flujo de modificación apunte por defecto a la base activa.
- La guía prohíbe usarlo para volver a firmar registros históricos.
- Validación: sintaxis y ayuda correctas; la ejecución sin `-DbPath` devuelve el código 2 sin abrir ni modificar ninguna base.

## Ensayo operativo aislado de recuperación 2026-10-07

- Se restauró la copia autenticada `ClinicaLongevidad_backup_20261007_193716798.db` exclusivamente sobre un destino de prueba aislado; la base activa permaneció cerrada y no se abrió ni modificó.
- `BackupService.RestoreBackup` comprobó SHA-256, HMAC y versión de clave antes de sustituir el destino. La restauración creó una copia previa del destino y reemplazó un marcador SQLite de prueba.
- Resultado: correcto. El SHA-256 de la restauración coincidió con el de la copia fuente (`35B12BB2BB0DEF96737A45118F91EC4A91F7D4F5EC45F8A77F019EF929936B3F`), `PRAGMA integrity_check` devolvió `ok`, la copia previa coincidió con el destino de prueba y el marcador no permaneció tras restaurar.
- Evidencia local protegida: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\recovery-drill-20261007_195518\recovery-result.json`.
- Este ensayo acredita el flujo técnico sobre una copia reciente y aislada. No resuelve las 2.100 verificaciones históricas sin su clave original, ni define RTO/RPO, ni autoriza restaurar sobre producción.

## Paquete de evidencia para custodia independiente 2026-10-07

- Se preparó un paquete sin base de datos ni datos clínicos: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\recovery-drill-20261007_195518\recovery-evidence-package.zip`.
- Incluye `recovery-result.json` y `evidence-manifest.json`. Su SHA-256 es `06AFC9038F85CE10B16B4395E45C44A16957E0CD6AD0744D9146D419C72DF337`; el comprobante se guarda junto al ZIP como `.sha256`.
- Está preparado para copiarlo a una ubicación de custodia independiente. Esa copia externa y su responsable siguen pendientes; no se ha subido información clínica ni una base SQLite.

## Custodia externa de evidencia en USB 2026-10-07

- El paquete de evidencia de recuperación, sin base SQLite ni datos clínicos, se copió al medio extraíble `ESD-USB (E:)` en `ClinicaLongevidadApp\AuditEvidence\2026-10-07\`.
- Se trasladaron `recovery-evidence-package.zip` y su comprobante `.sha256`. La comprobación posterior confirmó el SHA-256 `06AFC9038F85CE10B16B4395E45C44A16957E0CD6AD0744D9146D419C72DF337`, idéntico al original local.
- La custodia física del medio queda a cargo del responsable que lo retirará. Este soporte contiene solo el paquete de evidencia, no copias de bases de datos clínicas.

## Ensayo de rechazo de evidencia manipulada 2026-10-07

- Se alteró únicamente el comprobante HMAC de una copia aislada de prueba y se intentó restaurar sobre un destino ficticio.
- Resultado: `BackupService` rechazó la evidencia antes de iniciar una sustitución; el destino conservó exactamente su hash previo y no se creó ninguna copia `pre_restore`.
- Evidencia: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\recovery-rejection-drill-20261007_203353\recovery-rejection-result.json`.

## Programación independiente de copia diaria 2026-10-07

- Se añadió `tools/ScheduledBackup`, que crea una copia autenticada de la base central usando el mismo `BackupService`, SHA-256, HMAC y versión de clave que la aplicación.
- Se publicó el ejecutor en `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\scheduled-backup-runner` y se instaló la tarea de Windows `ClinicaLongevidadApp\DailyAuthenticatedBackup` a las 19:17 diariamente.
- La tarea acepta ejecución con batería y se configura para ejecutarse cuando Windows vuelva a estar disponible si se perdió la hora prevista. Se ejecuta en modo interactivo con el usuario Francisco, necesario para acceder al material de claves local.
- Validación manual: ejecución bajo demanda correcta con resultado `0`; generó `ClinicaLongevidad_backup_20261007_204734959.db` junto con `.sha256`, `.hmac` y `.hmac.ver`. El SHA-256 calculado coincide con su comprobante: `68acc881255b0eafcef287b959ae4d392a1de7a3110bd11f45e84627f023a0cb`.
- Cuando el ejecutor está instalado, los botones `Programar` y `Cancelar` de Auditoría actualizan o eliminan esta tarea de Windows, evitando depender del temporizador de la ventana.
- El RPO de 24 horas queda condicionado a que el equipo se inicie y el usuario de la tarea haya iniciado sesión. Ejecutar sin inicio de sesión requeriría custodiar credenciales o migrar el material de claves a una identidad de servicio; no se ha configurado por seguridad.

## Validación de tarea diaria independiente 2026-10-07

- Compilación Release correcta de la aplicación y de `tools/ScheduledBackup`, sin errores ni advertencias.
- Suite completa Release: **200/200 pruebas aprobadas**, 0 fallidas y 0 omitidas. Evidencia: `artifacts\validation-20261007\scheduled_backup_runner_validation.trx`.
- El pipeline de auditoría compilará y ejecutará la ayuda de `tools/ScheduledBackup` en futuras ejecuciones para detectar regresiones de la herramienta.
- Comprobación directa del servicio usado por la interfaz: reprogramó correctamente la tarea existente a las 19:17 y conservó `DisallowStartIfOnBatteries=False`, `StopIfGoingOnBatteries=False` y `StartWhenAvailable=True`.

## Segunda ejecución remota firmada de CI 2026-10-07

- La ejecución manual [37687005598](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37687005598) sobre el commit `2f6ccad` terminó correctamente.
- Superó restauración, **200 pruebas**, compilación y arranque de ayuda de `tools/ScheduledBackup`, regresiones de manifiesto/paquete y creación del paquete técnico autenticado.
- Evidencia descargada: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261007-run-37687005598`. El paquete `audit-tests-40acbca35bdd4fa7b34acb1e52dbf942.zip` tiene SHA-256 `D9BE58419BC25F00D41502C2379443987F5C8C3816242A313DCF468AD885BD0D`; se conservaron un TRX y 13 informes sintéticos de recuperación.

## Supervisión de la copia diaria 2026-10-08

- Se añadió `scripts\verify_daily_backup_task.ps1`, un control de solo lectura de la tarea `ClinicaLongevidadApp\DailyAuthenticatedBackup` y de la evidencia de copia más reciente. Comprueba el código de resultado, ejecución con batería, reanudación tras una hora perdida, antigüedad máxima, presencia de SHA-256/HMAC/versión HMAC y coincidencia del SHA-256.
- Primera comprobación: correcta. Resultado de tarea `0`, siguiente ejecución `08/10/2026 19:17`, copia más reciente de 1,61 horas y todos los comprobantes presentes, con SHA-256 coincidente.
- El control puede recibir `-OutputPath` para conservar automáticamente el JSON de cada comprobación en `audit_artifacts`. Se validó esta salida sobre un archivo temporal y se eliminó al finalizar la prueba.
- Pendiente: conservar la evidencia de la primera ejecución automática y realizar pruebas de interrupción. La tarea se ejecuta con el usuario Francisco conectado para no custodiar credenciales de servicio.

## Custodia de evidencias 2026-10-08

- Se designó a **Inés Hombreiro Pazos**, Doctora y responsable de Administración de la clínica, como responsable de custodia de evidencias. También se propone como responsable de aprobación de la política; la aprobación formal, la fecha, el sustituto y las retenciones siguen pendientes.

## Reenvío durable de auditoría 2026-10-08

- Se corrigió la propagación de errores de `WebhookForwarder` y `BlobAuditExporter`. Antes registraban el fallo internamente y devolvían éxito a la cola, que podía borrar una entrega no realizada. Ahora el worker conserva la fila para reintento o la mueve a `dead-letter` tras el máximo de intentos.
- El webhook crea una solicitud HTTP nueva en cada intento; así sus tres reintentos no reutilizan una solicitud ya enviada.
- Blob Storage usa un nombre basado en la fecha firmada dentro del payload y el `EventId`. Si un intento anterior creó el blob pero su respuesta se perdió, una repetición acepta solo el objeto existente con el mismo identificador y firma; si difieren, conserva el fallo para revisión.
- Validación: 8 pruebas dirigidas de cola/webhook aprobadas y compilación Release de la solución correcta, sin advertencias ni errores. Se añadieron pruebas que verifican la propagación de un webhook inaccesible, la coincidencia de metadatos Blob y la estabilidad del destino por fecha firmada.
- Pendiente: comprobar la deduplicación efectiva por `EventId` en cada destino real; la entrega continúa siendo al menos una vez.
