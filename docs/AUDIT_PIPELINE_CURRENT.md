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
- Push/manual: requieren el secret `AUDIT_PACKAGE_HMAC_KEY` y la variable `AUDIT_PACKAGE_HMAC_KEY_VERSION`. Si faltan, falla la generación autenticada. Ambos quedaron configurados y se verificaron en la ejecución manual `37676469997` del 07/10/2026.
- Permisos del workflow: lectura del repositorio. Sin creación ni subida a Releases.
- Solo se sube el ZIP de la ejecución actual, no todos los archivos de `artifacts`.

La ejecución manual remota `37676469997` sobre el commit `345303c` terminó correctamente: restauración, pruebas, regresiones de manifiesto/paquete y creación/conservación del paquete técnico autenticado. La evidencia descargada se conserva en `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261007-run-37676469997`. No se dispone de `actionlint` en este equipo.

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

## Segunda ejecución remota firmada de CI 2026-10-07

- La ejecución manual [37687005598](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37687005598) sobre el commit `2f6ccad` terminó correctamente.
- Superó restauración, **200 pruebas**, compilación y arranque de ayuda de `tools/ScheduledBackup`, regresiones de manifiesto/paquete y creación del paquete técnico autenticado.
- Evidencia descargada: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261007-run-37687005598`. El paquete `audit-tests-40acbca35bdd4fa7b34acb1e52dbf942.zip` tiene SHA-256 `D9BE58419BC25F00D41502C2379443987F5C8C3816242A313DCF468AD885BD0D`; se conservaron un TRX y 13 informes sintéticos de recuperación.

## Tercera ejecución remota firmada de CI 2026-10-08

- La ejecución manual [37698849282](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37698849282) sobre el commit `b10a1a0` terminó correctamente.
- Superó restauración, **203 pruebas**, compilación y ayuda de `tools/ScheduledBackup`, regresiones de manifiesto/paquete y creación del paquete técnico autenticado.
- Evidencia descargada: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261008-run-37698849282`. El paquete `audit-tests-f930ffecb420474699547c48a1823807.zip` tiene SHA-256 `495196F4CC5310929265C8A0F50BD7EFF5C11DE472369B0F3CBE56FDBA67C38A`; se conservaron un TRX y 13 informes sintéticos de recuperación.

## Cuarta ejecución remota firmada de CI 2026-10-08

- La ejecución manual [37699559905](https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/actions/runs/37699559905) sobre el commit `506c0f6` terminó correctamente.
- Superó restauración, **204 pruebas**, compilación y ayuda de `tools/ScheduledBackup`, regresiones de manifiesto/paquete y creación del paquete técnico autenticado.
- Evidencia descargada: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\ci-20261008-run-37699559905`. El paquete `audit-tests-938673cbf1c2496f8f92953af26d2ace.zip` tiene SHA-256 `69B9FA0700D9898721BEDA546368AE7BAA7F3FF4B23A5E7BD8223823D8882A22`; se conservaron un TRX y 13 informes sintéticos de recuperación.
