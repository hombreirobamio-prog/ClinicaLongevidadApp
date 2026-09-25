Guía rápida para auditoría — ClinicaLongevidadApp

Propósito
-------
Documento operativo para que el auditor ejecute comprobaciones básicas del subsistema de auditoría, genere artefactos y valide el comportamiento de backups/restore e integridad.

Cómo ejecutar (entorno local)
----------------------------
1. Abrir PowerShell en el workspace del proyecto.
2. Forzar sesión de administrador y suprimir diálogos (modo auditor):
   - `$env:FORCE_ADMIN='1'; $env:SILENT_MODE='1'; dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug --no-launch-profile`
   - `FORCE_ADMIN=1` fija `Sesion.RolActual` a `Administración` para mostrar menús y comandos administrativos.
   - `SILENT_MODE=1` suprime MessageBox y auto-confirma yes/no para flujos automatizados.

Generar artefactos de diagnóstico
---------------------------------
- Desde la UI (Administración → Auditoría) use el botón `Generar diagnóstico`.
  - Esto ejecuta `GenerateQuickDiagnostics()` y `GenerateIntegrityDiagnosticReport()` en background.
  - Al terminar mostrará la ubicación de los artefactos y opcionalmente abrirá la carpeta en el Explorador.
- Alternativa CLI: si la app está corriendo con `App.AuditoriaService` disponible, ejecutar desde código o herramienta auxiliar `tools/GenerateIntegrity` (si existe en el repo).

Rutas de artefactos
-------------------
- Quick diagnostics (resumen + CSV):
  - `%LocalAppData%\ClinicaLongevidadApp\logs` (p. ej. `IntegrityQuickSummary_<ts>.txt`, `IntegrityProblemRows_<ts>.csv`)
- Informe de integridad completo (JSON):
  - `%ProgramData%\ClinicaLongevidadApp\AuditIntegrityReports` (p. ej. `IntegrityReport_<ts>_id<N>.json`)
- Backups generados por la UI:
  - `%LocalAppData%\ClinicaLongevidadApp\backups` (ficheros `.db`)
- Logs de backup/VM:
  - `%LocalAppData%\ClinicaLongevidadApp\logs\backup.log`
  - `%LocalAppData%\ClinicaLongevidadApp\logs\backup_vm.log`

Checklist mínimo para el auditor
-------------------------------
1. Menú y permisos
   - Iniciar app con `FORCE_ADMIN=1` y verificar que las opciones administrativas aparecen (`Generar diagnóstico`, `Rotar HMAC`, backups).
2. Generar diagnóstico
   - Pulsar `Generar diagnóstico` y comprobar que se generan los ficheros en las rutas indicadas.
   - Ver contenido del `IntegrityQuickSummary_*.txt` y del CSV `IntegrityProblemRows_*.csv` si existen.
   - Si hay errores de integridad, revisar el JSON en `AuditIntegrityReports`.
3. Probar backup ahora
   - En la vista Auditoría (admin) pulsar `Copia ahora`.
   - Verificar que aparece fichero en `backups` y que `backup.log`/`backup_vm.log` tienen entrada.
   - Restaurar: usar `Restaurar` con uno de los backups y confirmar que la operación finaliza sin error.
4. Prueba de programación
   - Programar una copia con hora próxima o usar `Probar 1 min` y verificar ejecución automática.
   - Confirmar que no se crean múltiples schedulers (buscar duplicados en `backup_vm.log`).
5. Verificación de integridad
   - Ejecutar `GenerateQuickDiagnostics()` y `GenerateIntegrityDiagnosticReport()` (UI o herramientas) y revisar `IntegrityReport_*.json` si existe.
   - Comprobar campos `PrevHash`, `Hash`, `Signature` y `KeyVersion`/`KeyVersionEnc` en las filas problemáticas.
6. Rotación de claves (solo si procede)
   - Ejecutar `Rotar HMAC` / `Rotar ENC` y verificar que se registra evento en Auditoría y que `KeyVersion` en nuevas filas corresponde al nuevo valor.
7. Exportar y revisar CSV
   - Exportar CSV desde UI y comprobar que contiene las columnas esperadas (Fechahora, UsuarioAdmin, Accion, Modulo, Detalles, PrevHash, Hash, Signature, KeyVersion...)

Evidencia y artefactos a recopilar
---------------------------------
- Quick summary TXT y CSV generados.
- Integrity report JSON si se produjo.
- Ejemplo de backup `.db` y logs relevantes (`backup.log`, `backup_vm.log`, `AuditDebug.txt` si existe).
- Capturas de pantalla de la UI con las acciones (opcional).

Automatización recomendada
--------------------------
Hay un script opcional que puede añadirse para generar artefactos y empaquetarlos; puedo crear `scripts/generate_audit_artifacts.ps1` si lo preferís.

CI / Integración continua (comportamiento actual)
-----------------------------------------------
Se ha añadido un workflow de GitHub Actions (`.github/workflows/audit-pipeline.yml`) que automatiza build, tests y la generación/verificación de artefactos de auditoría.

- Qué hace el workflow:
  1. Compila el proyecto y ejecuta tests.
  2. Ejecuta `scripts/generate_audit_artifacts.ps1 -RequireBackup` para generar los artefactos de auditoría (incluye backup permanente cuando procede).
  3. Ejecuta `scripts/verify_audit_manifest.ps1 -RequireHmac` (verificación estricta en CI).
  4. Sube los artefactos del job como `audit-artifacts` (artifact del workflow) siempre.
  5. Intenta subir el ZIP al Release solamente si existe el Release para el tag configurado. La creación automática de la Release se permite únicamente en ejecuciones manuales (`workflow_dispatch`).

- Política por seguridad (recomendada y aplicada):
  - Evitamos crear Releases automáticamente desde pushes para prevenir publicaciones accidentales desde PRs o builds automatizados.
  - En `push` a `master` el workflow exige que la Release ya exista; si no existe el job falla para forzar creación manual de la Release.
  - En ejecuciones manuales (`workflow_dispatch`) el workflow puede crear la Release y subir el ZIP (útil para handoffs de auditoría controlados).

- Cómo ejecutar desde GitHub:
  - Manual: en la pestaña Actions seleccionar `Audit pipeline` y pulsar `Run workflow`. Opcionalmente proporcionar `release_tag` para indicar el tag de Release (por defecto `audit-rewrite-8684b20`).
  - Push: crear previamente la Release en GitHub con el tag deseado, luego push a `master` disparará el pipeline y el ZIP se subirá al Release existente.

Recomendación: para auditorías formales usar la ejecución manual y confirmar el `release_tag` o crear la Release antes del push para que CI pueda adjuntar los artefactos.

Notas de seguridad y privacidad
------------------------------
- Por defecto los diagnósticos redactan `Detalles` para evitar exponer datos sensibles. Para incluir `Detalles` en informes se usan variables de entorno:
  - `AUDIT_INCLUDE_DETAILS_IN_REPORTS=1`
  - `AUDIT_INCLUDE_DETAILS_IN_DIAGNOSTICS=1`
  - Úsalas sólo en entornos seguros de pruebas.
- Rotar claves es una operación sensible: documentad y aprobad con procedimientos operativos antes de ejecutar en entornos compartidos.

Contacto y siguientes pasos
--------------------------
- Si queréis, añado el script `scripts/generate_audit_artifacts.ps1` y automatizo el empaquetado + subida al release.
- También puedo añadir esta guía al `README.md` o mantenerla en `docs/AUDIT_GUIDE.md` (ya creada).

One-click para el auditor
-------------------------
Para facilitar la tarea al auditor se ha añadido un runner sencillo: `scripts/run_audit_for_auditor.bat`.

- Qué hace: inicia la aplicación en modo auditor (FORCE_ADMIN + SILENT_MODE), espera la generación de diagnósticos, empaqueta los artefactos en un ZIP y, si está configurado `gh` autenticado, intenta subirlo al release `audit-rewrite-8684b20`.
- Uso: desde el equipo donde está el repositorio, el auditor sólo tiene que hacer doble clic en `scripts\run_audit_for_auditor.bat`.
- Requisitos para subida automática: `gh` instalado y autenticado con token que tenga permisos `repo`. Si no está, el ZIP se crea localmente en el workspace y no se sube.
- Tiempo de espera: el runner usa un timeout (por defecto 300s) para esperar a los artefactos; si la generación tarda más, se puede ejecutar manualmente el PowerShell `scripts\generate_audit_artifacts.ps1` con un timeout mayor.

Recomendación: entrega al auditor una copia del repo con la carpeta `scripts` y las instrucciones de esta guía; así no necesita tocar nada del código ni del entorno.

Estado actual (resumen para el auditor)
-------------------------------------
Breve resumen de lo que ya está implementado y cómo verificarlo rápidamente:

- Generación de artefactos automatizada: `scripts/generate_audit_artifacts.ps1` crea un ZIP con
  logs, informes de integridad y backups permanentes. El script puede ejecutar `tools/InvokeBackup`
  para generar una copia permanente en `artifacts/backups` antes de empaquetar.
- Manifiesto de verificación: el empaquetador añade `audit_manifest_YYYYMMDD_HHMMSS.txt` con `SHA256` y,
  cuando procede, `HMAC` y `HMAC.Version` para cada backup incluido.
- Verificador automático: `scripts/verify_audit_manifest.ps1` valida que los SHA calculados de los
  `.db` coinciden con los valores del manifiesto y comprueba la presencia/consistencia de `.sha256` / `.hmac` / `.hmac.ver`.
- Runner "one-click": `scripts/run_audit_for_auditor.bat` ejecuta el flujo completo:
  1) genera artefactos (requiere al menos un backup permanente por defecto),
  2) ejecuta el verificador del manifiesto y aborta si hay errores,
  3) sube el ZIP al release `audit-rewrite-8684b20` si la verificación es correcta.
- Estado operativo: el flujo ha sido probado localmente — ZIPs como `audit-artifacts_20260925_150728.zip` se crearon y se subieron correctamente.
- Código y PR: los cambios (scripts y documentos) fueron agrupados en la rama `audit/artifacts-improvements` y se creó la PR
  correspondiente: https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/pull/15

Qué puede hacer el auditor ahora (pasos rápidos)
----------------------------------------------
- Ejecutar el runner (one-click): doble clic en `scripts\run_audit_for_auditor.bat` o desde PowerShell:
  `.\scripts\run_audit_for_auditor.bat` — el proceso fallará si no se genera al menos un backup permanente o si la verificación detecta discrepancias.
- Inspeccionar artefactos sin ejecutar el runner: ejecutar `scripts/generate_audit_artifacts.ps1 -SkipPermanentBackup -ListFiles` para empaquetar lo disponible sin crear backup nuevo.
- Verificar manualmente el manifiesto: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1` (usa el manifiesto más reciente si no se pasa ruta).

Recomendación final
-------------------
Conservar `-RequireBackup` activo en ejecuciones de auditoría y en CI para asegurar evidencia reproducible. Si preferís ejecuciones locales menos estrictas, usar `-SkipPermanentBackup`.

Nota sobre verificación estricta de HMAC
--------------------------------------
El verificador `scripts/verify_audit_manifest.ps1` soporta el flag `-RequireHmac`.
- Uso por defecto: HMAC es opcional (el verificador devuelve warnings si faltan). Esto permite compatibilidad con backups históricos.
- Uso estricto: pasar `-RequireHmac` para exigir la presencia y coincidencia de los ficheros `.hmac` y `.hmac.ver` además de `.sha256`.

Ejemplo (estricto):
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1 -RequireHmac
```

Recomendación: en auditorías formales o en CI usar `-RequireHmac` si la clave HMAC está disponible para comprobar firmas completas.


Verificación del manifiesto y backups
-----------------------------------
Cuando se genera el ZIP de auditoría, el runner añade un fichero `audit_manifest_YYYYMMDD_HHMMSS.txt` que contiene los valores SHA256 y, si existen, los HMAC y su versión para cada backup `.db` incluido.

Pasos básicos para verificar localmente:

- Comprobar el hash SHA256 de un backup:
  - PowerShell: `Get-FileHash -Algorithm SHA256 "path\to\ClinicaLongevidadApp_backup_*.db"`
  - Comparar el valor con la entrada `SHA256:` en el `audit_manifest_*.txt` o con el contenido del fichero `.sha256` junto al backup.

- Comprobar HMAC (nota: requiere la clave HMAC):
  - Si dispones de la clave HMAC, puedes calcular el HMAC-SHA256 en PowerShell con .NET:
    ```powershell
    $key = "<clave-en-texto-plano>"
    $hmac = New-Object System.Security.Cryptography.HMACSHA256([System.Text.Encoding]::UTF8.GetBytes($key))
    $hash = $hmac.ComputeHash([System.IO.File]::ReadAllBytes("path\to\ClinicaLongevidadApp_backup_*.db"))
    ([BitConverter]::ToString($hash) -replace '-','').ToLower()
    ```
  - Comparar el resultado con `HMAC:` en el manifiesto o con el fichero `.hmac` si existe.

- Verificar versión de HMAC: el fichero `.hmac.ver` contiene la `KeyVersion` usada; confirmar que coincide con la gestión de claves registrada.

Uso del runner con requisitos estrictos
-------------------------------------
- Para exigir que se genere al menos un backup permanente y fallar si no hay ninguno (útil en CI):
  - `.\	ools\generate_audit_artifacts.ps1 -RequireBackup`
- Si prefieres omitir la generación del backup permanente (por ejemplo en entornos de desarrollo), usa `-SkipPermanentBackup`.

