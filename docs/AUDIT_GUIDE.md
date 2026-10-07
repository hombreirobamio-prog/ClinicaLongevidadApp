# Guía operativa de auditoría

Actualización: 06/10/2026. **Auditoría operativa abierta.** Esta guía sustituye las instrucciones antiguas de acceso forzado, autoconfirmación, empaquetado de carpetas y publicación automática.

Consultar el [estado de cierre](AUDIT_CLOSURE.md), las [correcciones](AUDIT_REMEDIATION_2026-09-29.md) y el [checklist](AUDIT_CHECKLIST.md). Los resultados históricos no constituyen aprobación del despliegue.

## Acceso y preparación

Trabajar desde la raíz del repositorio en Windows con .NET 8 y registrar la revisión y los cambios locales examinados. Para la UI, iniciar sesión mediante el acceso normal con una cuenta autorizada de Administración.

`FORCE_ADMIN` fue retirado. `SILENT_MODE` deniega las confirmaciones cuando oculta diálogos. Los diagnósticos automáticos con `AUDIT_RUN_DIAGNOSTICS=1` no crean una sesión administrativa.

## Ubicación de archivos

Los archivos nuevos se guardan bajo `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts`: copias en `backups`, claves locales en `keys`, registros en `logs`, informes en `reports\integrity` y `reports\diagnostics`, exportaciones CSV en `exports` y otros artefactos en `audit_artifacts`. Las exportaciones CSV proponen `exports` como destino inicial, aunque el usuario puede elegir otro lugar. Los archivos de ubicaciones antiguas se conservan separados en `legacy_20261006`; no moverlos ni mezclarlos con las salidas activas.

Si existe una base heredada en la ubicación anterior, la aplicación sólo la copia a la raíz central cuando todavía no hay una base central. Una base central existente nunca se reemplaza automáticamente por fechas de archivo.

Aprovisionar claves por un mecanismo seguro sin escribirlas en comandos guardados, documentos o informes. Las nuevas escrituras exigen HMAC y versión. Fuera de Development/Test, los detalles requieren cifrado; `AUDIT_ALLOW_PLAINTEXT_DETAILS` no habilita texto sin cifrar en producción.

## Validación de Azure Key Vault

Para un almacén de prueba, crear los secretos HMAC y ENC con nombres configurados por `AUDIT_HMAC_SECRET_NAME` y `AUDIT_ENC_SECRET_NAME`. La identidad operadora necesita el rol RBAC **Key Vault Secrets Officer** (mostrado en el portal en español como **Agente de secretos de Key Vault**). No incluir valores de secretos en capturas, chats, documentos ni comandos guardados.

Instalar Azure CLI, iniciar sesión y comprobar el acceso de solo lectura desde una consola nueva:

```powershell
az login
$env:KEYVAULT_URI = 'https://nombre-real.vault.azure.net/'
$env:AUDIT_HMAC_SECRET_NAME = 'nombre-secreto-hmac'
$env:AUDIT_ENC_SECRET_NAME = 'nombre-secreto-enc'
dotnet run --project tools/RotateKeys -- verify
```

El resultado correcto indica `Configured: True` y lectura explícita correcta para HMAC y ENC. Los secretos deben ser Base64: HMAC debe descodificar al menos 32 bytes y ENC debe tener una longitud AES válida (16, 24 o 32 bytes). La herramienta no muestra ni guarda valores de claves. Esta comprobación acredita acceso a las versiones activas; para acreditar recuperación histórica debe existir una segunda versión creada de forma controlada y verificarse la lectura de ambas versiones.

No establecer `REQUIRE_KEYVAULT=1`, rotar claves ni usar el almacén de prueba con la base de auditoría existente hasta definir la transición y custodia de las versiones locales históricas. Cuando `REQUIRE_KEYVAULT=1` o el entorno es Production, la aplicación aborta el arranque si no puede inicializar Key Vault y leer ambas claves con longitudes válidas y sus versiones activas; no cambia silenciosamente al proveedor local. Key Vault y SQLite no comparten una transacción. La [evaluación de transición de claves históricas](KEY_TRANSITION_ASSESSMENT_2026-10-02.md) confirma que la base actual referencia más versiones que las conservadas localmente; recuperar las claves autorizadas es un requisito previo.

## Pruebas y paquete técnico

La [operación del pipeline](AUDIT_PIPELINE_CURRENT.md) define el formato `technical-tests-only` y sus límites. El paquete contiene TRX, manifiesto JSON y HMAC; no incluye backups ni acredita recuperación operativa.

Con `AUDIT_HMAC_KEY` (Base64 de al menos 32 bytes) y `AUDIT_HMAC_KEY_VERSION` ya aprovisionadas en el proceso, ejecutar en PowerShell:

```powershell
$auditStart = [datetime]::UtcNow
$auditResults = Join-Path $PWD ('artifacts/audit-tests-' + [guid]::NewGuid().ToString('N'))
dotnet test ClinicaLongevidadApp.Tests/ClinicaLongevidadApp.Tests.csproj --configuration Release --logger 'trx;LogFileName=tests.trx' --results-directory $auditResults
if ($LASTEXITCODE -ne 0) { throw 'Pruebas fallidas; conservar el TRX y revisar los errores.' }
$auditZip = & ./scripts/generate_audit_artifacts.ps1 -TestResultsPath (Join-Path $auditResults 'tests.trx') -RunStartedUtc $auditStart
& ./scripts/verify_audit_package.ps1 -ZipPath $auditZip
if (-not $?) { throw 'Verificación del paquete fallida.' }
```

El generador rechaza evidencias antiguas, vacías, fallidas u omitidas y verifica el paquete antes de devolver su ruta. La salida predeterminada es `artifacts/audit-packages/audit-tests-<id>.zip`.

`scripts/run_audit_for_auditor.bat` pasa los argumentos al generador y conserva el código de salida. Requiere `-TestResultsPath` y `-RunStartedUtc`; el doble clic sin esos datos no completa el procedimiento. No inicia la aplicación ni publica Releases. Los parámetros antiguos `-RequireBackup`, `-SkipPermanentBackup`, `-UploadToRelease` y los timeouts ya no se admiten.

Las regresiones adicionales se ejecutan con `scripts/test_audit_manifest.ps1` y `scripts/test_audit_package.ps1`; conservar sus resultados y revisar sus códigos de salida.

## Integridad de una base existente

Sobre una copia protegida y consistente, con las claves y versiones necesarias disponibles, usar las herramientas de solo lectura. Sustituir las rutas de ejemplo; elegir una salida nueva para el informe:

```powershell
dotnet run --project tools/VerifyIntegrity -- 'Data Source=C:\ruta\copia.db'
# Revisar $LASTEXITCODE: 0 aceptada, 1 problemas, 2 error de ejecución.
dotnet run --project tools/GenerateIntegrity -- 'C:\ruta\copia.db' 'C:\ruta\informe-nuevo.json'
# Revisar $LASTEXITCODE: 0 aceptada, 3 problemas, 2 error de ejecución.
```

Estas herramientas no crean ni migran la base examinada. La falta de una clave histórica impide completar la verificación. Una cadena aceptada no demuestra por sí sola ausencia de truncado final o sustitución completa: complementar con el anclaje externo descrito a continuación.

Si el resultado indica `signature unverifiable (exact key version unavailable)`, el registro no se considera válido ni alterado: significa que la firma no se puede comprobar porque falta su clave histórica exacta. Conservar el registro intacto, registrar ese estado y no activar Key Vault sobre esa base hasta disponer de la clave o aprobar formalmente su conservación como histórico no verificable. No generar una clave sustituta ni volver a firmar el evento.

`scripts/run_backfill.ps1` requiere `-DbPath` de forma explícita y sólo debe recibir una copia aislada autorizada. No selecciona automáticamente la base activa ni debe utilizarse como mecanismo para volver a firmar registros históricos.

Desde la UI autorizada puede utilizarse `Generar diagnóstico`. El botón `Abrir diagnósticos` abre la carpeta común `reports`, que contiene `integrity` y `diagnostics`. Revisar la ruta comunicada por la aplicación y conservar los resultados; los informes pueden contener identificadores técnicos y requieren acceso controlado.

Para resumir un informe de integridad desde consola, `tools/summarize_integrity_report.ps1` busca primero el JSON más reciente de `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\reports\integrity`. También puede recibir una ruta explícita mediante `-Path`.

## Anclaje externo H01

El punto de control externo se conserva en Azure Blob Storage, separado de SQLite. Crear una cuenta de almacenamiento y un contenedor privado, por ejemplo `audit-anchors`, en el mismo entorno de Azure. Activar una directiva de inmutabilidad con la retención acordada antes de almacenar evidencias. La identidad que ejecuta la herramienta necesita únicamente el rol **Storage Blob Data Contributor** sobre ese contenedor; no asignar permisos de propietario ni usar claves de cuenta.

Después de iniciar sesión con `az login`, configurar la sesión de PowerShell sin incluir secretos:

```powershell
$env:AUDIT_ANCHOR_STORAGE_URI = 'https://nombre-real.blob.core.windows.net'
$env:AUDIT_ANCHOR_CONTAINER = 'audit-anchors'
$env:AUDIT_ANCHOR_SCOPE = 'audit'
dotnet run --project tools/AnchorAudit -- write --db 'C:\ruta\copia-o-base-autorizada.db'
dotnet run --project tools/AnchorAudit -- verify --db 'C:\ruta\copia-o-base-autorizada.db'
```

`write` abre SQLite en solo lectura y crea un JSON nuevo con el Id, EventId y hash final; no crea contenedores ni modifica la base. `verify` exige que la fila anclada siga presente y conserve EventId y hash, por lo que detecta el truncado posterior al punto de control o la sustitución por una copia anterior. Ejecutar también `VerifyIntegrity` para comprobar todos los enlaces y firmas. No considerar H01 cerrado hasta que haya un contenedor inmutable, un punto de control creado y una verificación conservada como evidencia.

## Backups y recuperación

Verificar el backup y sus acompañantes `.sha256`, `.hmac` y `.hmac.ver` con la clave de la versión indicada antes de planificar una restauración. No regenerar firmas para convertir una evidencia histórica incompleta en evidencia autenticada de origen.

Para un manifiesto histórico concreto, con la clave correspondiente aprovisionada:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify_audit_manifest.ps1 -ManifestPath 'C:\ruta\audit_manifest.txt' -RequireHmac
if ($LASTEXITCODE -ne 0) { throw 'Verificación del manifiesto fallida.' }
```

Este script dispone de una versión local por ejecución; los históricos con varias versiones necesitan resolución segura de claves. No confundir este manifiesto con el JSON del paquete técnico.

Cuando se use `scripts/generate_companions.ps1 -IncludeHmac` sobre una copia nueva, el proceso exige `AUDIT_HMAC_KEY` en Base64 de al menos 32 bytes y `AUDIT_HMAC_KEY_VERSION`; no asigna una versión por defecto. No usarlo para sobrescribir los acompañantes de evidencia histórica.

Usar el [ensayo sintético de recuperación](AUDIT_RECOVERY_DRILL_2026-09-29.md) para reproducir los escenarios aislados documentados. Una restauración operativa requiere autorización específica, copia representativa protegida, destino aislado, aplicación detenida y conexiones cerradas. Registrar estado previo, integridad recuperada, datos recuperados, tiempos y rechazos. No ejecutar restauraciones sobre una base activa como prueba rutinaria de esta guía.

## CI y evidencia

El workflow ejecuta pruebas y regresiones. Las PR no reciben la clave de firma; push/manual requieren el secret `AUDIT_PACKAGE_HMAC_KEY` y la variable `AUDIT_PACKAGE_HMAC_KEY_VERSION`. No crea ni publica Releases. La primera ejecución manual remota firmada (`37676469997`, 07/10/2026) terminó correctamente y su evidencia se conserva en el almacén central de artefactos.

Conservar TRX, informes y resultados de verificación con fechas y revisión del código. Los JSON del ensayo sintético se conservan por separado del paquete técnico firmado. Los paquetes operativos, la custodia independiente y la aprobación final siguen pendientes según [AUDIT_CLOSURE.md](AUDIT_CLOSURE.md).
