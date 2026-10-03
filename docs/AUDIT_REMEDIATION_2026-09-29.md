# Correcciones de auditoría — primera entrega, 29/09/2026

Estado: auditoría abierta. Este documento actualiza las instrucciones operativas anteriores; no constituye un cierre ni una validación de producción.

## Entrega del 30/09/2026 — atomicidad de pacientes (H04 parcial)

`PacienteService` persistía mediante sqlite-net sin registrar auditoría. `PanelRecepcionViewModel` intentaba registrar eventos por separado, ignorando los fallos; el guardado normal emitía incluso un éxito antes de escribir el paciente y otro después. Esto permitía cambios sin evento y eventos de éxito sin cambio confirmado.

Crear, editar y eliminar pacientes utilizan ahora `RegistrarEventoConOperacion`, ya implementado para festivos, para confirmar negocio y auditoría en una misma transacción SQLite. Sin servicio de auditoría o con bases distintas se rechaza la operación. El identificador y la fecha de creación nuevos se asignan al objeto solo tras confirmar. Se conservan todos los campos y el formato de fechas en ticks con fechas opcionales nulas. No se modifica el esquema ni las interfaces públicas, ni se añaden paquetes.

El evento del servicio conserva las acciones `Paciente.Crear`/`Paciente.Editar`, tipo `Paciente`, módulo `Recepción` e identificador `PacienteId`; la eliminación registra `Paciente.Eliminar`. La creación implícita para cita pasa por el mismo servicio. Se retiran los tres registros separados de éxito del ViewModel; el evento persistido contiene el identificador confirmado, sin volver a copiar correo y teléfono en sus detalles. No se modifican registros históricos. La garantía no agrupa en una sola transacción la creación del paciente y la cita posterior: esta última sigue pendiente de H04.

Pruebas nuevas sobre bases y datos sintéticos: persistencia y lectura sqlite-net de todos los campos; fechas opcionales; un evento por operación y cadena íntegra; rollback de alta, edición y eliminación ante fallo de clave, inserción de auditoría o COMMIT; fallo de negocio sin evento; rechazo sin auditoría o con bases distintas. Las pruebas del ViewModel verifican que no emite éxitos separados y que el alta implícita no continúa si falla el guardado.

Resultado final: compilación Release sin errores ni warnings emitidos, **118/118 pruebas aprobadas**, sin omitidas, incluidas las 16 de pacientes y sus flujos de recepción. [TRX final](../artifacts/audit-fixes-20260930/h04-pacientes-full/tests.trx). Se conservan las ejecuciones focalizadas intermedias, que detectaron dos supuestos incorrectos en las nuevas aserciones (zona horaria de fechas y `PacienteId` serializado como texto); se ajustaron las pruebas al formato existente.

H04 queda cubierto para festivos y pacientes; sigue abierto para citas, usuarios y otras operaciones. No se ha realizado validación manual de UI ni ensayo de producción en esta entrega.

## Entrega del 30/09/2026 — atomicidad de festivos (H04 parcial)

`FestivoService` guardaba el cambio mediante sqlite-net y después llamaba a auditoría por otra conexión, ocultando las excepciones. Un fallo de firma o escritura podía dejar un cambio confirmado sin evento.

Crear, actualizar y eliminar festivos utilizan ahora una operación interna de `AuditoriaService` que ejecuta negocio y auditoría en la misma conexión y transacción `BEGIN IMMEDIATE`. Solo después del COMMIT se expone el identificador del nuevo festivo y se inicia el envío/exportación existente. Se conserva el formato SQLite de fechas en ticks, los campos y las firmas públicas. No se añaden paquetes, tablas ni migraciones.

La falta de auditoría o una configuración con bases distintas bloquea la operación. Los errores se propagan al llamador; el ViewModel de guardado existente puede mostrar el fallo. No se han modificado las vistas ni eliminado sus eventos adicionales: la garantía corresponde al evento emitido por el servicio, no a cada evento complementario de UI.

Se añaden doce casos de prueba: operaciones válidas y lectura compatible con sqlite-net; rollback de creación, actualización y eliminación ante fallo de inserción de auditoría, clave o COMMIT; fallo de negocio; y ausencia de servicio o bases distintas. El fallo de COMMIT se induce mediante una clave foránea diferida en una base sintética. Las pruebas de integración y autorización de festivos ahora usan negocio y auditoría en la misma base temporal. `AssemblyInfo.cs` permite al proyecto de pruebas acceder a esas entradas internas sin ampliar la API pública.

Validación: 14/14 pruebas focalizadas aprobadas. La primera suite completa detectó una prueba antigua que no aprovisionaba auditoría; se adaptó al nuevo requisito y al aislamiento temporal. Resultado final en Release: **103/103 aprobadas**, sin fallidas ni omitidas. Evidencia: [TRX final](../artifacts/audit-fixes-20260930/h04-final/tests.trx); la ejecución intermedia queda en `artifacts/audit-fixes-20260930/h04-full/tests.trx`. La compilación final no emitió warnings ni errores.

H04 continúa abierto para pacientes, citas, usuarios y demás operaciones no adaptadas. No se han ensayado aquí pérdida de alimentación, disco lleno real, recuperación de producción ni interacción manual con la UI. H01 sigue pendiente de infraestructura externa.

## Cuarta entrega — ensayo de recuperación

Se añade un [ensayo aislado de backup/restauración](AUDIT_RECOVERY_DRILL_2026-09-29.md) con ocho escenarios y evidencias JSON por ejecución. Comprueba recuperación exacta, integridad de auditoría recuperada, copia del estado previo y rechazo de manipulaciones sin modificar destino. Se corrige la retención de conexiones del backup por pooling, detectada en la primera ejecución. CI conserva los informes sintéticos por separado; sigue pendiente su ejecución remota y el ensayo operativo representativo.

## Tercera entrega — CI, paquete técnico y verificación de solo lectura

La operación vigente y los resultados están en [AUDIT_PIPELINE_CURRENT.md](AUDIT_PIPELINE_CURRENT.md). El generador cambia de interfaz: recibe un TRX y la hora de inicio; produce evidencia técnica portátil y autenticada. Se retiran la recopilación indiscriminada de carpetas y la publicación automática. CI deja de saltarse la verificación y requiere una clave configurada para firmar los paquetes en push/manual. Las PR ejecutan pruebas sin recibir esa clave.

Las herramientas de integridad no crean ni migran la base examinada y devuelven fallo cuando corresponde. Resultado local: 78 pruebas .NET, 8 escenarios de manifiestos y 7 escenarios de paquetes aprobados; ambas herramientas compilan. Quedan pendientes la ejecución remota/configuración de CI y el paquete operativo con backup/restauración y evidencia nueva; H09 no se declara cerrado en su totalidad.

## Segunda entrega — cifrado y payload v2

Los eventos nuevos llevan `PayloadVersion=2`. Su hash y HMAC cubren también tipo, rol, área, sesión, equipo, versión de aplicación, versiones de claves, copias de detalles y enlace anterior. Los eventos existentes conservan la versión 1 mediante una columna nueva con valor predeterminado 1; no se recalculan ni refirman. La verificación admite cadenas mixtas. Versiones desconocidas y modificaciones de versión se rechazan. Esto no añade protección retrospectiva a los metadatos antiguos ni detecta por sí solo truncado final: sigue pendiente el anclaje externo.

La escritura exige clave HMAC y versión. Fuera de los entornos explícitos `Development` y `Test`, los detalles requieren cifrado y no se guarda `DetallesPlain`. Un entorno sin declarar aplica esa misma restricción. `AUDIT_ALLOW_PLAINTEXT_DETAILS` ya no permite eludirla. Una clave AES inválida o un fallo de cifrado abortan la escritura, también en desarrollo. Se mantiene el error para el llamador y no se persiste el evento fallido. La configuración real y los accesos a claves deben verificarse antes de desplegar.

El payload JSON enviado al exportador/webhook incorpora esos campos v2; consumidores externos que reconstruyan el JSON para verificar firmas deben soportar ese formato. Las herramientas antiguas que reconstruyan solo v1 requieren adaptación antes de usarse con eventos nuevos. La migración es aditiva, pero un binario antiguo no puede validar v2 correctamente.

Se añaden 17 casos: alteración de nueve columnas, degradación/versión desconocida, ausencia o invalidez de AES, cifrado sin copia en claro, y migración de una tabla sin `PayloadVersion` seguida de cadena mixta. Se corrigieron cuatro claves sintéticas de pruebas que tenían longitud AES inválida y antes pasaban por la degradación silenciosa. Las pruebas usan explícitamente el entorno `Test`; las de producción lo cambian temporalmente y lo restauran.

Resultado final de esta entrega: **78/78 pruebas aprobadas**, sin omitidas, y `git diff --check` sin errores. Evidencia: `artifacts/audit-fixes-20260929/phase2/Francisco_DESKTOP-6TIG8MA_2026-09-29_17_37_44.trx`. Los resultados intermedios se conservan en esa carpeta.

**Incidencia detectada en la suite previa:** `AuditoriaIntegrityWorkerTests` borraba la carpeta compartida `%ProgramData%/ClinicaLongevidadApp/AuditIntegrityReports` al comenzar y terminar. Las ejecuciones anteriores a la corrección pudieron eliminar informes allí presentes; no hay inventario previo que permita determinar cuáles. No se afirma conservación de esa carpeta. Se ha eliminado ese borrado y se inyecta una carpeta temporal única por prueba. Los ZIP históricos del repositorio no son destino de ese borrado y pueden servir para recuperar copias existentes, sin sobrescribir ni confundir evidencia original con recuperada.

Pendientes de la siguiente entrega: CI y empaquetado portable, herramientas consumidoras v2, anclaje externo, transacción de negocio/auditoría, verificación operativa y cierre documental. H07/H08 quedan corregidos para nuevas escrituras en los aspectos descritos; no equivalen a certificar la configuración desplegada ni los registros históricos.

## Cambios

- Se elimina la referencia de compilación a `tools/InvokeBackup/Program.cs`, inexistente en el árbol local.
- `VerifyIntegrity` comprueba cada enlace con la fila precedente. Si la versión exacta de la clave no está disponible, comunica que la firma no es verificable. El proveedor local ya no interpreta `local` como alias de cualquier versión.
- El verificador de manifiestos recalcula HMAC sobre los bytes usando `AUDIT_HMAC_KEY` y su versión `AUDIT_HMAC_KEY_VERSION` (por defecto `local`). En modo estricto, clave, firma, versión y acompañantes son obligatorios. Las rutas relativas se resuelven desde el manifiesto. El SHA del manifiesto también es obligatorio.
- Se retira la asignación administrativa mediante `FORCE_ADMIN`. Los diagnósticos automáticos usan `AUDIT_RUN_DIAGNOSTICS=1`, sin crear sesión administrativa. La administración requiere el acceso normal de la aplicación.
- Suprimir diálogos ya no confirma operaciones: `ConfirmYesNo` devuelve falso en modo silencioso y bajo pruebas.
- La restauración requiere `.sha256`, `.hmac` y `.hmac.ver` no vacío, y verifica antes de sustituir la base. No se incorpora una opción de saltar estos controles.
- Los errores al leer el último hash, comenzar la transacción, insertar o confirmar ya no se ocultan dentro de `RegistrarEvento`. El servicio propaga el fallo; no inicia una cadena alternativa ni exporta después de un COMMIT fallido.

## Operación

Verificar una entrega concreta, sin publicar ni regenerar sus firmas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify_audit_manifest.ps1 -ManifestPath <manifiesto> -RequireHmac
```

La clave debe estar aprovisionada de forma segura en el proceso, nunca en el manifiesto o en un comando guardado en el historial. Este script solo dispone de una versión local por ejecución; para históricos de varias versiones falta integrar un resolvedor seguro de claves. Una clave no disponible implica verificación incompleta, no evidencia de manipulación.

**Compatibilidad:** los backups sin firma o sin versión ya no se restauran. Deben conservarse como evidencia histórica y tratarse mediante un procedimiento específico de recuperación; no regenerar acompañantes y afirmar que acreditan su origen. La elevación por `FORCE_ADMIN` deja de funcionar en todas las compilaciones.

## Verificación reproducible

```powershell
dotnet test ClinicaLongevidadApp.Tests/ClinicaLongevidadApp.Tests.csproj --no-restore --configuration Release --logger trx --results-directory artifacts/audit-fixes-20260929/test-results
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test_audit_manifest.ps1
```

Pruebas añadidas: eliminación de primera/fila intermedia, clave histórica indisponible, rechazo de inserción, restauración sin acompañantes preservando datos posteriores al backup, confirmación silenciosa denegada y versiones locales exactas. El script prueba un caso válido y siete casos negativos con datos y claves sintéticos. Los resultados quedan en `artifacts/audit-fixes-20260929/test-results` y `artifacts/audit-manifest-tests`.

Resultado final local: **61/61 pruebas .NET aprobadas**, sin omitidas, y **8/8 escenarios del verificador aprobados**. TRX final: `Francisco_DESKTOP-6TIG8MA_2026-09-29_17_30_21.trx`. Resultados del manifiesto: `artifacts/audit-manifest-tests/e33d4df233034ecc867303e8349c0bd0/results.csv`. Una ejecución intermedia falló en tres pruebas nuevas por lectura directa de una base abierta; se ajustaron para comprobar mediante SQL que la restauración rechazada preserva los datos posteriores al backup. Se conserva ese resultado intermedio y el final correcto.

## Pendientes expresos

- H01: anclaje externo para detectar truncado final o sustitución completa. La comprobación de enlaces no resuelve ese caso.
- H03: ensayo de permisos con usuarios reales sobre el binario desplegado; la eliminación del atajo no acredita toda la autorización.
- H04: transacción conjunta o mecanismo durable entre la operación de negocio y su evento. Algunos llamadores aún pueden capturar el fallo; el cambio actual garantiza la propagación desde el servicio, no atomicidad global.
- H07: impedir degradación del cifrado a texto, minimizar datos y verificar configuración efectiva de producción.
- H08: versionar el payload y proteger todas las columnas de metadatos sin invalidar históricos.
- H09: endurecer CI, eliminar YAML duplicado, exigir evidencia nueva y verificar un paquete portable completo. El generador solo se adapta aquí a la retirada de `FORCE_ADMIN`.
- H10: consolidar guías, inventario y cierre con identidad, fecha, evidencias y revisión independiente. Las guías antiguas se conservan con aviso de sustitución de instrucciones.
- Restauración operativa aislada, claves históricas, alertas, retención y custodia según el informe inicial.

No se han regenerado paquetes históricos, rotado claves, restaurado bases operativas ni publicado artefactos.
