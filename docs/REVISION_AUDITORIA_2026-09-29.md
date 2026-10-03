# Revisión técnica del subsistema y expediente de auditoría

Fecha: 29/09/2026. Revisión realizada por Codex sobre el árbol de trabajo local.
Referencia Git: `9179285ba5a4d950916dfc7f32359f8be2034a3e`, con cambios locales previos a esta revisión, incluidos el proyecto, documentación y scripts. No equivale a revisar exclusivamente ese commit.

## Dictamen

**No dar por cerrada ni presentar como satisfactoria la auditoría en su estado actual.** Hay controles útiles, pero existen fallos en la verificación de integridad y autenticidad, un mecanismo de elevación de permisos y evidencia de cierre incompleta. La suite no ha podido ejecutarse porque el proyecto no compila.

Esta revisión es técnica y documental, no una certificación normativa. No se han comprobado la configuración efectiva de producción, los permisos de sus cuentas, Key Vault, la recepción de alertas, el almacenamiento externo ni la publicación remota. No se han ejecutado restauraciones ni rotaciones ni publicado datos. Los hallazgos estáticos se identifican como tales; no se afirma explotación en producción.

## Comprobaciones realizadas y aspectos correctos

- Inspección del servicio de auditoría, arranque, proveedor local de claves, restauración, generador/verificador de evidencias, workflow y guías.
- Los SHA256 de los ZIP locales `20260928_150118` y `20260928_182037` coinciden con los consignados en el cierre. Esto acredita coincidencia de bytes con los valores anotados, no autenticidad independiente.
- Dentro del ZIP `20260928_182037`, los 13 backups coinciden con sus respectivos archivos `.sha256`. También contiene 13 `.hmac` y 13 `.ver`; no se ha validado su autenticidad con las claves originales.
- Existen mecanismos de hash, HMAC, AES-GCM, triggers contra UPDATE/DELETE, pruebas y herramientas. La restauración recalcula SHA/HMAC cuando sus archivos acompañantes están presentes.
- Prueba negativa sintética: un fichero sin datos clínicos, HMAC inventado de 64 ceros y sin `.hmac.ver` obtiene código 0, `Errors: 0`, `Warnings: 0`, usando `-RequireHmac`. Evidencia: `artifacts/audit-review-20260929/synthetic-verification.txt` y archivos sintéticos asociados.
- Intento de suite: `dotnet test ClinicaLongevidadApp.Tests/ClinicaLongevidadApp.Tests.csproj --no-restore --configuration Release --logger trx --results-directory artifacts/audit-review-20260929/test-results`. Tras superar restricciones iniciales de acceso al SDK, falla con CS2001 por `tools/InvokeBackup/Program.cs` inexistente. No hay resultado de tests aprobados de esta revisión.

## Hallazgos y acciones

### H01 — Alta: no se verifica la continuidad de la cadena

Evidencia estática: `Services/AuditoriaService.cs:839-898`. `lastHash` se inicializa y actualiza, pero nunca se compara con `PrevHash`. Se calcula cada hash usando el `PrevHash` de la propia fila. El HMAC tampoco incluye el enlace anterior.

Consecuencia: retirar una fila intermedia de una copia manipulada puede dejar válidas las verificaciones de las filas supervivientes. Los triggers no sustituyen la detección sobre copias modificadas o accesos directos al archivo.

Acción: comparar cada enlace con el hash anterior, definir el inicio de cadena y proteger el último estado mediante un punto de control independiente. Añadir pruebas de eliminación intermedia, truncado final, reordenación y alteración. El truncado final necesita una referencia externa o expectativa independiente del último evento.

### H02 — Alta: el verificador «estricto» no autentica los backups

Evidencia estática y reproducción: `scripts/verify_audit_manifest.ps1:119-172`. Solo compara texto del manifiesto y `.hmac`; no calcula HMAC con una clave. La versión solo se exige si aparece en el manifiesto. La prueba sintética descrita arriba pasa sin versión y con HMAC ficticio.

Acción: resolver la clave por su versión y recalcular HMAC sobre los bytes del backup; exigir campos, longitud/formato, versión y archivo de versión. Distinguir «auténtico», «no verificable» y «rechazado». Sin clave, no declarar validación criptográfica satisfactoria. Añadir pruebas negativas independientes.

### H03 — Alta: sesión administrativa por variable de entorno

Evidencia estática: `App.xaml.cs:257-272`. `FORCE_ADMIN=1` establece usuario `admin.local`, rol y área de Administración sin comprobar credenciales y sin restricción de compilación Debug. Las guías lo recomiendan para el auditor.

Acción: retirar esta vía del binario de producción y usar una identidad real de auditor con acceso de lectura. Verificar con el binario Release que ninguna variable otorga permisos administrativos. El alcance real depende de los controles posteriores de cada operación; no se ha probado contra producción.

### H04 — Alta: errores de escritura pueden perder trazabilidad sin informar al llamador

Evidencia estática: `Services/AuditoriaService.cs:623-632`, `683-690` y `804-807`. Ante fallo de transacción se continúa con `PrevHash` vacío; los errores de COMMIT se ignoran y el método registra errores en el log sin devolver fallo al llamador.

Acción: no continuar con una cadena alternativa; confirmar persistencia antes de exportar y devolver un resultado verificable. Definir qué operaciones deben detenerse si no pueden auditarse y cuáles pueden usar una cola durable. Probar disco lleno, bloqueo y fallo de COMMIT, además de concurrencia.

### H05 — Alta: restauración permite ausencia de pruebas de integridad

Evidencia estática: `Services/BackupService.cs:398-429`. SHA256 y HMAC se verifican únicamente si existen sus archivos. Si faltan ambos, este método termina sin rechazar el backup.

Acción: establecer requisitos obligatorios para restauración de producción. Separar una importación histórica excepcional con autorización y trazabilidad. Probar ausencia de archivos, MAC incorrecto, clave no disponible y versión desconocida sobre una base desechable.

### H06 — Alta: no es reproducible la validación mediante tests

Evidencia ejecutada: error CS2001. `ClinicaLongevidadApp.csproj:32` incluye explícitamente `tools/InvokeBackup/Program.cs`, que no existe en este checkout. Los comentarios del mismo proyecto afirman que la inclusión fue retirada.

Acción: corregir la referencia conforme a la estructura real y ejecutar build y suite completos. Conservar TRX, comando, SDK, commit y cambios locales. Un resultado histórico de CI no acredita este árbol de trabajo.

### H07 — Alta: confidencialidad dependiente de configuración y degradación a texto

Evidencia estática: `Services/AuditoriaService.cs:550-584` y `655-664`. Si falla o falta el cifrado, `Detalles` puede recibir texto plano. Excluir `DetallesPlain` en Production no evita ese camino. Si no se declara el entorno Production, se permite además guardar la copia en claro.

Acción: exigir la configuración de cifrado aplicable antes de aceptar eventos sensibles y evitar degradación silenciosa. Minimizar datos registrados. Verificar mediante datos sintéticos que los campos protegidos no aparecen en claro en DB, logs, CSV ni paquetes. El generador recopila backups y logs completos y dispone de subida a Release: revisar destinatarios, acceso y contenido antes de distribuir; no se ha comprobado exposición remota ni datos personales reales.

### H08 — Media: la cobertura criptográfica es incompleta y falta el estado «no verificable»

Evidencia estática: `Services/AuditoriaService.cs:589-599`, `834` y `886-896`. Rol, área, sesión, equipo, versión y tipo no forman parte del payload canónico como columnas independientes. Sin clave disponible se omite la verificación de firma; si no se encuentra una versión se intenta la clave actual.

Acción: definir y versionar todos los campos protegidos, verificar la versión exacta y declarar explícitamente la imposibilidad de autenticar. Añadir pruebas de alteración de metadatos y pérdida de claves históricas.

### H09 — Media: CI y generación pueden aparentar evidencia suficiente sin producirla

Evidencia estática: `.github/workflows/audit-pipeline.yml` repite la clave inicial `name`; tanto la generación manual como la automática usan `-SkipPermanentBackup`; la verificación estricta solo se contempla en ejecución manual y se omite si no hay backups. Contradice partes de `docs/AUDIT_GUIDE.md`.

En `scripts/generate_audit_artifacts.ps1`, cualquier log recién modificado puede satisfacer la espera; tras timeout se empaqueta lo disponible. `RequireBackup` cuenta archivos antes del filtro `.db`, no acredita backup nuevo ni restaurable. El manifiesto contiene rutas absolutas de la máquina original y cubre backups, no todos los archivos del expediente.

Acción: exigir un informe nuevo y válido, backup nuevo cuando se requiera, tests aprobados y verificación efectiva. Usar datos sintéticos en CI. Verificar el contenido extraído del ZIP con rutas relativas e inventario de todos los archivos; asociar inequívocamente manifiesto, ZIP, ejecución y commit. Validar el YAML antes de ejecutarlo.

### H10 — Media: cierre documental insuficiente

Evidencia: `docs/AUDIT_CLOSURE.md:5-13,27-32` contiene nombre y fecha como expresiones literales sin evaluar, una línea de hash vacía y un paquete `152437` que no aparece en la raíz examinada. No se concluye que no exista en otro archivo: debe localizarse y verificarse. Las guías contienen rutas erróneas (`templates`/`tools` frente a `scripts`), duplicaciones y ofrecimientos conversacionales.

Acción: seleccionar un expediente definitivo, identificar responsable y revisor, fecha con zona horaria, alcance, entorno, versión exacta, resultado de cada control, excepciones y aceptación firmada. Resolver todos los archivos referenciados. Eliminar afirmaciones de cierre no respaldadas y mantener una única guía operativa autorizada.

## Qué falta para entregarlo a un auditor

Lo siguiente no ha quedado acreditado por el expediente examinado; requiere evidencia, no solo una declaración:

1. Alcance y criterios acordados: sistema, periodo, entorno, población de eventos y exclusiones.
2. Matriz requisito → control → prueba → evidencia → resultado → responsable. Cubrir acceso y consulta de información, cambios, exportaciones, fallos de autenticación, permisos y acciones administrativas.
3. Pruebas de permisos con identidades reales y segregación entre operador, administrador y auditor.
4. Política de conservación, eliminación, custodia y acceso a registros y backups; evidencia de su aplicación.
5. Inventario de claves/versiones, accesos autorizados, rotación y recuperación de claves históricas, sin incluir secretos en el expediente.
6. Restauración ensayada en entorno aislado, validación funcional posterior y tiempos/pérdida de datos medidos frente a objetivos RTO/RPO definidos.
7. Pruebas negativas de manipulación y fallos; comprobación de triggers en condiciones equivalentes a producción. Actualmente `RunningUnderTest()` omite su creación bajo pruebas.
8. Evidencia de recepción y atención de alertas, colas fallidas, responsable y plazo de respuesta.
9. Hora y zona coherentes, identificación de sesión y correlación de acciones verificadas.
10. Registro de hallazgos con prioridad, responsable, fecha objetivo y evidencia de nueva comprobación; cierre por un revisor identificado.

## Qué sobra o debe separarse

- Instrucciones de forzar administrador y autoconfirmar acciones dentro del recorrido ordinario del auditor.
- Documentación repetida o contradictoria, texto conversacional y referencias a herramientas «si existen».
- Múltiples ZIP sin un índice que señale cuál es definitivo. Conservar los históricos bajo política de archivo; no borrarlos para maquillar el expediente.
- Datos completos de backups/logs que no sean necesarios para la revisión. Preparar una entrega mínima y controlada.
- Regenerar checksums o HMAC sobre evidencia recibida como paso de verificación: `generate_companions.ps1` sobrescribe acompañantes. Esa operación genera evidencia nueva y debe distinguirse de comprobar la evidencia original.

## Condición de cierre propuesta

Corregir los hallazgos altos, ejecutar pruebas positivas y negativas con resultados conservados, restaurar una copia en entorno aislado y entregar un único expediente verificable fuera de la máquina original. Documentar las limitaciones y excepciones restantes con aceptación explícita del responsable y revisión independiente. Hasta entonces, estado recomendado: **abierta, pendiente de remediación y nueva verificación**.
