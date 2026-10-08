# Ensayo aislado de recuperación — 29/09/2026

Alcance: bases, datos, identidades y claves sintéticos creados en una carpeta única por escenario. No se admite una base operativa como entrada. Este ensayo demuestra comportamiento funcional sobre una base pequeña; no acredita RTO/RPO de producción ni disponibilidad de claves históricas reales.

## Comprobaciones

El caso válido crea tres filas con valores conocidos y tres eventos de auditoría firmados, genera un backup y añade después una cuarta fila a la fuente. Restaura sobre una base de destino distinta y verifica:

- `PRAGMA integrity_check` devuelve `ok`.
- Se recuperan exactamente las tres filas originales, con sus identificadores y valores; el cambio posterior al backup no aparece.
- Los tres eventos recuperados conservan integridad criptográfica usando el verificador en solo lectura.
- La copia `.pre_restore` conserva exactamente los bytes de la base de destino anterior.
- El archivo restaurado coincide con el backup.

Los siete casos negativos prueban modificación de base, modificación acompañada de SHA recalculado, HMAC inventado, versión de clave desconocida y ausencia individual de SHA/HMAC/versión. Todos deben fallar antes de cambiar el destino: se comparan sus bytes y un dato testigo, y se comprueba que no se creó una copia previa de restauración.

## Hallazgo corregido

La primera ejecución encontró conexiones SQLite de destino de backup retenidas por pooling, lo que impedía abrir la copia para comprobar sus bytes. `BackupService` desactiva ahora pooling en las conexiones temporales de origen y destino utilizadas en la creación del backup. No cambia la configuración de conexiones de uso normal de la aplicación.

Se conservan los informes de la primera ejecución, con dos fallos, en `artifacts/audit-fixes-20260929/phase4`. La ejecución final queda separada en `artifacts/audit-fixes-20260929/phase4-final`.

## Evidencia final del ensayo

Los ocho escenarios finalizaron correctamente. La restauración válida tardó 4 ms y su escenario completo 262 ms en este equipo. Son medidas del ensayo sintético, no objetivos ni garantías operativas.

La suite completa finaliza con **86/86 pruebas aprobadas**, sin omitidas. TRX final: `artifacts/audit-fixes-20260929/phase4-final/tests.trx`.

Cada escenario guarda `recovery-result.json` con identificador, fechas UTC, resultado, duración, hashes del destino y motivo de rechazo cuando procede. Las claves aleatorias son efímeras y no se exportan; el informe describe la verificación realizada durante la ejecución, no permite repetir posteriormente la autenticación de esas copias sin sus claves.

CI conserva únicamente los JSON del ensayo en un artefacto separado `synthetic-recovery-reports`, incluso si fallan las pruebas. Estos informes no forman parte todavía del ZIP técnico firmado; el TRX identifica el resultado de cada prueba. La configuración remota de CI sigue pendiente de ejecutar.

## Reproducir localmente

```powershell
$env:AUDIT_RECOVERY_EVIDENCE_DIR = Join-Path $PWD ('artifacts/recovery-' + [guid]::NewGuid().ToString('N'))
dotnet test ClinicaLongevidadApp.Tests/ClinicaLongevidadApp.Tests.csproj --configuration Release --filter FullyQualifiedName~RecoveryDrillTests --logger trx
```

Las bases sintéticas se conservan para inspección, en directorios exclusivos. No se borran carpetas compartidas ni se publican backups.

## Verificación posterior — 30/09/2026

La ampliación del ensayo incluye trece escenarios: los ocho originales y rechazo ante archivo abierto, conexión SQLite con transacción activa, sidecar WAL, interrupción antes de sustituir el destino y contenido firmado que no es una base SQLite válida.

El TRX `artifacts/audit-fixes-20260929/phase5/tests.trx` registró 90/91 pruebas aprobadas y un fallo en `active-sqlite`: la lectura del destino para calcular su hash encontró el archivo en uso. La prueba actual ya dispone la transacción y las conexiones antes de verificar el hash y el dato testigo. No se modificó código en esta validación.

Se compiló en Release y se ejecutó primero `active-sqlite` de forma aislada (1/1 aprobada), y después la suite completa (**91/91 aprobadas**, sin omitidas). Evidencias nuevas:

- [TRX individual](../artifacts/audit-fixes-20260930/active-sqlite/tests.trx).
- [TRX de la suite completa](../artifacts/audit-fixes-20260930/full-suite/tests.trx).
- Informes sintéticos por escenario en `artifacts/audit-fixes-20260930/full-suite/recovery/<id>/recovery-result.json`.

La prueba exige que la restauración sobre la conexión activa sea rechazada y que el destino se conserve; no habilita restauración con la base en uso. Los resultados anteriores se mantienen como historial. Esta ejecución no acredita despliegue, CI remoto ni recuperación operativa representativa.

## Pendiente antes del cierre operativo

- Ensayo autorizado sobre una copia representativa y protegida, con claves históricas y política de acceso reales.
- Medición con volumen representativo y comparación con RTO/RPO acordados.
- Recuperación con la aplicación detenida y conexiones cerradas: este ensayo no valida restaurar sobre una base activa con WAL o transacciones concurrentes.
- Revisión de atomicidad de sustitución, interrupción durante la restauración y relación entre cambios de negocio y eventos de auditoría.
- Paquete operativo firmado que incluya backup autenticado, informe de integridad, resultado de recuperación y custodia independiente.

Estado: ensayo sintético satisfactorio; auditoría operativa abierta.

## Ensayo operativo aislado de recuperación 2026-10-07

- Se restauró la copia autenticada `ClinicaLongevidad_backup_20261007_193716798.db` exclusivamente sobre un destino de prueba aislado; la base activa permaneció cerrada y no se abrió ni modificó.
- `BackupService.RestoreBackup` comprobó SHA-256, HMAC y versión de clave antes de sustituir el destino. La restauración creó una copia previa del destino y reemplazó un marcador SQLite de prueba.
- Resultado: correcto. El SHA-256 de la restauración coincidió con el de la copia fuente (`35B12BB2BB0DEF96737A45118F91EC4A91F7D4F5EC45F8A77F019EF929936B3F`), `PRAGMA integrity_check` devolvió `ok`, la copia previa coincidió con el destino de prueba y el marcador no permaneció tras restaurar.
- Evidencia local protegida: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\recovery-drill-20261007_195518\recovery-result.json`.
- Este ensayo acredita el flujo técnico sobre una copia reciente y aislada. No resuelve las 2.100 verificaciones históricas sin su clave original, ni define RTO/RPO, ni autoriza restaurar sobre producción.

## Ensayo de rechazo de evidencia manipulada 2026-10-07

- Se alteró únicamente el comprobante HMAC de una copia aislada de prueba y se intentó restaurar sobre un destino ficticio.
- Resultado: `BackupService` rechazó la evidencia antes de iniciar una sustitución; el destino conservó exactamente su hash previo y no se creó ninguna copia `pre_restore`.
- Evidencia: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts\audit_artifacts\recovery-rejection-drill-20261007_203353\recovery-rejection-result.json`.
