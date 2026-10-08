# Estado de cierre de auditoría

Actualización documental: 06/10/2026.

**Estado: auditoría operativa abierta.** Las correcciones y pruebas locales documentadas no constituyen aprobación de producción ni revisión independiente. Este documento sustituye la interpretación de cierre de su versión anterior.

## Evidencias actuales y alcance

Validación más reciente: pipeline remoto firmado [37699559905](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37699559905), con suite Release **204/204 aprobadas**, 0 fallidas, 0 errores y 0 omitidas. Su TRX y paquete técnico se conservan en `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261008-run-37699559905`. La compilación previa terminó sin errores ni advertencias. Sustituye las cifras de suite más recientes de esta sección; las cifras inferiores se conservan como evidencia histórica de cada entrega.

La base actual conserva referencias a 12 versiones HMAC y 8 de cifrado, pero sólo queda una versión local activa de cada tipo. No activar Key Vault sobre esa base ni volver a firmar eventos históricos. Los registros sin clave exacta deben conservarse como no verificables. [Evaluación y plan de recuperación](KEY_TRANSITION_ASSESSMENT_2026-10-02.md).

Los artefactos nuevos de la aplicación se guardan bajo `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts`, en las subcarpetas `backups`, `keys`, `logs`, `reports\integrity`, `reports\diagnostics`, `exports` y `audit_artifacts`. Los datos de ubicaciones anteriores se archivan separadamente y no se mezclan con los archivos activos.

Validación posterior de administración de cola: nueva ejecución Release sobre el estado actual, sin errores ni warnings emitidos, **188/188 aprobadas**, 0 fallidas y 0 omitidas. [TRX de validación](../artifacts/audit-fixes-20260930/h04-cola-admin-validation/tests.trx). No sustituye la validación manual WPF ni de permisos en despliegue.

- H04, reenvío durable (30/09/2026): el evento y su salida se confirman en la misma transacción SQLite; el worker elimina solo tras completar todos los destinos y evita procesamientos locales simultáneos. Entrega al menos una vez: los destinos deben deduplicar mediante EventId. Release sin errores ni warnings emitidos; **189/189 pruebas aprobadas**, 0 fallidas y 0 omitidas. [TRX completo](../artifacts/audit-fixes-20260930/h04-forwarding-final/tests.trx), [TRX específico](../artifacts/audit-fixes-20260930/h04-forwarding-targeted-final/tests.trx). La recuperación operativa y la idempotencia efectiva del destino siguen pendientes.
- H04, administración de cola (30/09/2026): reencolar/eliminar y su evento en una transacción, rechazo de filas inexistentes y control de Administración sujeto a AUDIT_ENFORCE_AUTH. Suite completa **188/188**; tras corregir un warning en una aserción nueva, Release sin errores ni warnings emitidos y **14/14** casos afectados aprobados. [TRX completo](../artifacts/audit-fixes-20260930/h04-cola-admin-full/tests.trx), [TRX final específico](../artifacts/audit-fixes-20260930/h04-cola-admin-final/tests.trx). Validación manual del flujo correcto realizada con datos aislados: 2 pendientes, 1 fallido y 0 errores de integridad. Sigue pendiente comprobar los mensajes de denegación/fallo si no se observaron, y la configuración de permisos en despliegue.
- H04, horarios (30/09/2026): Guardar confirma alta/actualización y su evento en una transacción; Id solo tras COMMIT. Sin cambios de disponibilidad ni de política de permisos. Release sin errores ni warnings emitidos; **174/174 pruebas aprobadas**, 0 fallidas y 0 omitidas. [TRX completo](../artifacts/audit-fixes-20260930/h04-horarios-full/tests.trx).
- Login (30/09/2026): auditoría confirmada antes de publicar sesión y navegar; se mantiene el bloqueo aunque falle el registro de intentos. Release sin errores ni warnings emitidos y **162/162 pruebas aprobadas**, sin fallidas ni omitidas. [TRX completo](../artifacts/audit-fixes-20260930/h04-login-full/tests.trx). [Inventario y límites](H04_INVENTARIO_2026-09-30.md).
- H04, usuarios (30/09/2026): guardar, eliminar y restablecer contraseña confirman datos y evento en una transacción; sin secretos en auditoría ni éxitos duplicados de los ViewModels. Se conserva la información de cambios de rol, área y activación en el evento del servicio. Release sin errores ni warnings emitidos; **151/151 pruebas aprobadas**, 0 fallidas y 0 omitidas. [TRX completo](../artifacts/audit-fixes-20260930/h04-usuarios-full/tests.trx). [Alcance y pendientes](AUDIT_SESSIONS.md#continuacion-h04-usuarios-2026-09-30).
- H04, citas (30/09/2026): alta y actualización, incluidos estados y cancelaciones, confirman negocio y evento en una transacción. Recepción deja de registrar éxitos anticipados o duplicados y trabaja con copias hasta guardar. Compilación Release sin errores ni warnings emitidos; **130/130 pruebas aprobadas**, sin fallidas ni omitidas. [TRX de citas y regresiones](../artifacts/audit-fixes-20260930/h04-citas-full/tests.trx). Se mantienen Cita.Crear/Cita.Actualizar con Estado en detalles; se retiran los eventos adicionales de recepción. Pendiente validación manual de UI y consumidores de esos eventos.
- H04, pacientes (30/09/2026): crear, editar y eliminar pacientes confirman el cambio y su evento en la transacción común. Se retiran los eventos de éxito anticipados y duplicados de recepción. Compilación Release y suite de esa entrega: **118/118 pruebas aprobadas**, sin fallidas ni omitidas. [TRX de pacientes y regresiones](../artifacts/audit-fixes-20260930/h04-pacientes-full/tests.trx). Pendiente validación manual de UI; no implica atomicidad del flujo completo de creación de una cita.
- H04, festivos (30/09/2026): crear, actualizar y eliminar el festivo y su evento se confirman en una sola transacción SQLite. Si falla negocio, firma, inserción de auditoría o COMMIT, se revierte la operación. Compilación Release y suite final: **103/103 pruebas aprobadas**, sin omitidas. [TRX actual](../artifacts/audit-fixes-20260930/h04-final/tests.trx). No cierra H04 en otros módulos ni acredita validación manual de UI.
- [Correcciones realizadas](AUDIT_REMEDIATION_2026-09-29.md): propagación de errores de auditoría, controles de restauración, cifrado y payload v2, entre otras correcciones. Sus apartados de entregas posteriores actualizan el estado de los pendientes iniciales.
- [Pipeline y paquete técnico](AUDIT_PIPELINE_CURRENT.md): documenta 78/78 pruebas .NET, 8/8 escenarios de manifiestos y 7/7 escenarios de paquetes aprobados en la tercera entrega. Las ejecuciones remotas manuales `37676469997` y `37687005598` completaron correctamente pruebas, regresiones y la creación/conservación del paquete `technical-tests-only`; la segunda también compiló el ejecutor de copia independiente. No acredita operación en producción.
- [Ensayo aislado de recuperación](AUDIT_RECOVERY_DRILL_2026-09-29.md): escenarios sintéticos satisfactorios y suite final de 86/86 pruebas aprobadas, sin omitidas. Evidencia: [TRX final](../artifacts/audit-fixes-20260929/phase4-final/tests.trx). Los informes de recuperación se conservan por separado y todavía no forman parte del ZIP técnico firmado.

El 30/09/2026 se compiló en Release y se ejecutó la suite anterior a las entregas de H04: **91/91 pruebas aprobadas**, sin fallidas ni omitidas. Evidencia: [TRX de la suite](../artifacts/audit-fixes-20260930/full-suite/tests.trx). El escenario `active-sqlite` también pasó de forma aislada: [TRX individual](../artifacts/audit-fixes-20260930/active-sqlite/tests.trx).

Esta validación resuelve el pendiente del resultado `phase5` del 29/09/2026 (90/91 aprobadas): el fallo registrado era una `IOException` al leer el destino para calcular su hash. El código actual de la prueba ya libera la transacción y la conexión SQLite antes de comprobar la conservación del destino. Se validó esa corrección existente sin modificar el servicio ni la prueba. Se conservan las evidencias anteriores; no se han revalidado paquetes históricos ni realizado pruebas operativas de producción.

## Pendientes para el cierre

- H01: `tools/AnchorAudit` está verificado con Azure Blob Storage privado para crear y comprobar un punto de control externo. La cuenta no admite claves compartidas y la identidad operadora usa Storage Blob Data Contributor. El contenedor tiene una retención de 30 días que permanece desbloqueada por decisión operativa, por lo que todavía falta aprobar y bloquear la retención definitiva antes de declarar H01 cerrado.
- H03: verificar permisos con usuarios reales sobre el binario desplegado.
- H04: no activar la rotación con Key Vault sobre la base existente hasta recuperar las claves históricas o aprobar formalmente el tratamiento de los registros no verificables. Los errores de webhook y Blob ya se propagan a la cola para reintento y posterior `dead-letter`, evitando que una entrega fallida se borre como éxito. Verificar aún los destinos de reenvío reales y su deduplicación por `EventId`. Login exige auditar antes de publicar sesión, sin transacción común entre memoria y SQLite. Festivos, pacientes, citas, usuarios, horarios y administración de cola ya confirman sus cambios y eventos en una transacción; no se afirma atomicidad global de la aplicación.
- H07/H08: una pareja de claves heredadas localizada el 08/10/2026 corresponde a una versión de la base actual; su copia SQLite asociada no contiene registros de auditoría y no acredita validación histórica. Los 2.100 registros tempranos sin hash o versión exacta de la base actual siguen sin poder verificarse y deben conservarse intactos. Una segunda clave HMAC localizada no coincide con las versiones HMAC no vacías de la base actual. Falta comprobar la configuración efectiva de cifrado y claves en producción y la compatibilidad de consumidores con payload v2. Las correcciones de nuevas escrituras no protegen retrospectivamente los metadatos v1.
- H09: CI remoto y el paquete técnico autenticado ya están verificados. El resultado de recuperación ya está empaquetado en una evidencia sin datos clínicos; sigue pendiente completar el paquete operativo con backup autenticado, informe de integridad nuevo. La evidencia de recuperación ya tiene una copia externa verificada en USB; el [borrador de custodia](EVIDENCE_CUSTODY_POLICY_DRAFT.md) prepara el registro de responsable y conservación, que siguen pendientes de aprobación.
- Recuperación: el 07/10/2026 se completó un ensayo técnico aislado con una copia reciente autenticada, validación HMAC/SHA-256, sustitución de un destino de prueba y `integrity_check=ok`. Se adoptan provisionalmente RTO de 4 horas y RPO de 24 horas; la evidencia ya cuenta con copia externa verificada. La tarea independiente diaria ya cubre el caso de aplicación cerrada con el usuario conectado. El 08/10/2026 se verificaron su primera ejecución automática y la recuperación tras suspensión: la tarea prevista a las 01:20 se reanudó a las 01:22:55 con resultado `0` y comprobantes SHA-256/HMAC/versionado válidos. Falta confirmar ejecuciones continuadas y validar el tratamiento de los registros que requieren claves históricas; no se ha restaurado sobre una base activa ni de producción.
- Verificar alertas, retención, acceso y custodia de evidencias.
- H10: el [inventario inicial de evidencias](EVIDENCE_REGISTER.md) ya identifica los paquetes, anclaje y la primera copia automática verificada. Falta definir sustituto/retenciones, confirmar las ejecuciones continuadas y realizar la revisión independiente con fecha y aceptación antes de declarar el cierre.

## Validación de cierre

- Responsable de aprobación: pendiente de identificar.
- Fecha de aprobación: pendiente.
- Revisión independiente y aceptación operativa: pendientes.

## Referencias históricas del 28/09/2026

Se conservan los nombres, hashes, resultados y ubicaciones registrados anteriormente como referencias históricas. No se ha comprobado en esta actualización su existencia, disponibilidad, autenticidad ni custodia. Los resultados históricos `Errors:0 Warnings:0` no acreditan el cumplimiento de los controles actuales ni el cierre operativo.

### Artefactos registrados

- `audit-artifacts_20260928_152437.zip` -> sha256:988A712955D3FF2C9879458A3CDCD6993C97E134147C98013660894257A227E6
- `audit_manifest_20260928_152437.txt`

- `audit-artifacts_20260928_150118.zip` -> sha256:533E9B6AEBA5E6622E6C7EE99DDEC475F54FEA41810E975D9F0E15D787C1B62B
- `audit_manifest_20260928_150118.txt`

### Resultados de verificación registrados

- `audit_manifest_20260928_152437.txt` -> Errors:0 Warnings:0
- `audit_manifest_20260928_150118.txt` -> Errors:0 Warnings:0

### Publicación registrada

- [Release audit-rewrite-8684b20](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/releases/tag/audit-rewrite-8684b20)

### Ubicaciones registradas

- Archivo local declarado: `C:\ProgramData\ClinicaLongevidadApp\AuditArtifacts`
- Copia declarada: `C:\SecureArchives\ClinicaLongevidadApp\AuditArtifacts_20260928_172901`

### Hash adicional registrado

- audit-artifacts_20260928_182037.zip sha256:19CD15CE00B7D696CA44086A484A768ED6477A78468B73F6F63C5E7B11715FA4
