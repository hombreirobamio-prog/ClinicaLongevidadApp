# Evidencia técnica de auditoría — operación vigente

Actualización: 29/09/2026. Sustituye las instrucciones antiguas del generador y del runner. La auditoría operativa sigue abierta.

## Alcance y cambio de interfaz

`generate_audit_artifacts.ps1` genera ahora exclusivamente un paquete de resultados de pruebas identificado como `technical-tests-only`. Requiere un TRX concreto y la hora UTC de inicio de la ejecución. Ya no arranca la aplicación, recopila carpetas personales o compartidas, busca el ZIP más reciente, crea copias de bases ni publica Releases. Se retiran los argumentos antiguos `RequireBackup`, `SkipPermanentBackup`, `UploadToRelease`, `ReleaseTag` y los tiempos de espera. El runner BAT pasa los argumentos al generador y conserva el código de salida.

Esto corrige la mezcla de evidencia de CI con evidencia operativa; **no sustituye un ensayo de restauración ni un paquete de producción**. El empaquetado operativo con backups autenticados, informe de integridad nuevo y custodia independiente continúa pendiente.

## Generar y verificar

Con una clave de firma de al menos 32 bytes en Base64 aprovisionada en `AUDIT_HMAC_KEY`, y su identificador en `AUDIT_HMAC_KEY_VERSION`:

```powershell
$auditStart = [datetime]::UtcNow
dotnet test ClinicaLongevidadApp.Tests/ClinicaLongevidadApp.Tests.csproj --configuration Release --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/current-audit-tests
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
$auditZip = & ./scripts/generate_audit_artifacts.ps1 -TestResultsPath artifacts/current-audit-tests/tests.trx -RunStartedUtc $auditStart
& ./scripts/verify_audit_package.ps1 -ZipPath $auditZip
```

El generador rechaza TRX antiguos, vacíos, fallidos o con pruebas omitidas, y compara los resultados individuales con el resumen. El paquete contiene exactamente `tests.trx`, `package-manifest.json` y `package-manifest.hmac`. El manifiesto incluye SHA256 del TRX, identificador de ejecución, fechas, commit, indicador de cambios locales, alcance y versión de clave. Su HMAC cubre el manifiesto completo. Un árbol modificado queda identificado como tal: el commit no basta para reproducir esos cambios locales.

El verificador abre el ZIP sin extraerlo, exige inventario exacto sin entradas duplicadas, verifica firma y hash y no depende de rutas absolutas del equipo de origen. La clave debe conservarse aparte para la verificación posterior; no se incluye en el ZIP. HMAC acredita autenticidad frente a quienes no poseen la clave, no identidad independiente del firmante ni sellado temporal externo.

## CI

El workflow `audit-pipeline.yml` ejecuta la suite, las pruebas de manifiestos y las pruebas del paquete. Conserva el TRX, incluyendo fallos, como artefacto de resultados; solo genera el paquete autenticado tras éxito.

- Pull requests: no reciben claves de firma ni generan paquetes operativos; ejecutan regresiones con claves sintéticas.
- Push/manual: requieren el secret `AUDIT_PACKAGE_HMAC_KEY` y la variable `AUDIT_PACKAGE_HMAC_KEY_VERSION`. Si faltan, falla la generación autenticada. Configurarlos es un requisito pendiente del repositorio remoto.
- Permisos del workflow: lectura del repositorio. Sin creación ni subida a Releases.
- Solo se sube el ZIP de la ejecución actual, no todos los archivos de `artifacts`.

El workflow se ha editado localmente; no se ha disparado ni verificado una ejecución remota. Sus pasos principales se han probado localmente; no se dispone de `actionlint` en este equipo.

## Herramientas de integridad

`VerifyIntegrity` y `GenerateIntegrity` usan el servicio común, compatible con v1/v2, abriendo SQLite en modo de solo lectura y sin inicialización ni migración del esquema. La ausencia de `PayloadVersion` se interpreta como v1. No se crea una base vacía para aparentar una comprobación satisfactoria.

`VerifyIntegrity` devuelve 0 con integridad aceptada, 1 con problemas y 2 ante errores de inicialización/ejecución. `GenerateIntegrity` exige una ruta de base existente y devuelve 3 si encuentra problemas, 2 si no puede ejecutarse. Su JSON se escribe en la ruta de salida indicada; ya no crea diagnósticos en carpetas compartidas. El informe puede incluir identificadores técnicos: controlar su distribución.

## Validación local de esta entrega

- 78/78 pruebas .NET aprobadas; TRX: `artifacts/audit-fixes-20260929/phase3/tests.trx`.
- Se amplía la prueba histórica para verificar una tabla sin `PayloadVersion` en solo lectura y comprobar que sus bytes permanecen iguales.
- 8/8 escenarios de `test_audit_manifest.ps1` aprobados.
- 7/7 escenarios de `test_audit_package.ps1`: paquete válido, traslado, evidencia antigua, tests fallidos, falta de clave, alteración del TRX y entrada inesperada.
- Compilan `VerifyIntegrity` y `GenerateIntegrity`, sin advertencias ni errores.
- Scripts PowerShell analizados sintácticamente; `git diff --check` sin errores.

Las claves y evidencias de las regresiones son sintéticas. No constituyen aprobación de producción ni un cierre de los hallazgos operativos.
