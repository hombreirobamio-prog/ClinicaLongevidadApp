# ClinicaLongevidadApp

-> Nota: al reanudar, lea `docs/BACKFILL_SESSION_SUMMARY.md` para el contexto de la última sesión.

-> Nota para el auditor: para ejecutar la comprobación de auditoría sin tocar código, doble clic en `scripts\\run_audit_for_auditor.bat` o siga `docs/AUDIT_GUIDE.md`. Si desea que los artefactos se suban automáticamente al release, asegúrese de tener `gh` autenticado con permisos `repo`.

One-click audit (recommended)
- `scripts\\run_audit_for_auditor.bat` — doble clic para ejecutar el runner: arranca la app en modo auditor, espera la generación de diagnósticos, empaqueta `logs`/`backups`/`AuditIntegrityReports` en un ZIP y (opcional) sube el ZIP al release `audit-rewrite-8684b20` si `gh` está disponible.
- `scripts\\generate_audit_artifacts.ps1` — script PowerShell invocado por el runner; puede ejecutarse directamente para ajustar timeout o desactivar la subida automática.
## Resumen de la última sesión (acciones realizadas)

- UI: `AuditoriaView` — movido el `CheckBox` largo para que quede debajo de los filtros y evitar solapamientos; ajustes de tamaños, `MinWidth` y padding en controles y botones.  
- ViewModel: consolidada la lógica en `ViewModels/AuditoriaViewModelV2` y eliminado el `AuditoriaViewModel` legacy (PR #14).  
- Tests/build: ejecutados localmente — `dotnet build` OK y `dotnet test` pasó (49/49).  
- Git/GH: PR #14 mergeada (squash), tag `audit-rewrite-8684b20` creado y release marcado como pre-release.  
- Artefacto: publicado localmente `audit-rewrite-8684b20.zip` (creado en el workspace); subida automática al release pendiente (intentos con `gh release upload` fallaron — se recomienda adjuntar manualmente si es necesario).

### Cambios aplicados en esta sesión (resumen corto)

- `Services/BackupService.cs`: mitigación de bloqueo al verificar backups:
  - Reemplazado acceso directo con `File.OpenRead` por `OpenFileWithRetry` que abre el archivo con `FileShare.ReadWrite` y realiza reintentos breves en caso de `IOException` por locks transitorios.
  - `ComputeAndWriteChecksums` ya abre el fichero en modo compatible con lecturas concurrentes.
  - Se eliminó el `using System.Threading;` ambiguo y se usó `System.Threading.Thread.Sleep` fully-qualified para evitar conflictos con `System.Timers.Timer`.

Estos cambios permiten que las pruebas de backup/restore sean más robustas frente a locks temporales del sistema.

### Pendiente tras los cambios aplicados

- Ejecutar la suite de tests completa (`dotnet test`) para verificar que la corrección evita la excepción `IOException` observada en `BackupServiceTests.TriggerImmediateBackup_CreatesFiles_And_RestoreSucceeds`.
- Revisar logs generados en `%LocalAppData%\\ClinicaLongevidadApp\\logs\\backup.log` y `backup_vm.log` tras ejecutar la prueba de backup.
- Considerar añadir tests adicionales que simulen locks concurrentes para evitar regresiones.

### Siguientes pasos recomendados

- Ejecutar localmente: `dotnet test --no-build --filter "FullyQualifiedName~BackupServiceTests.TriggerImmediateBackup_CreatesFiles_And_RestoreSucceeds"` y revisar que pasa sin errores.
- Si el problema persiste, instrumentar temporalmente `BackupService` para dump de handles/processos que bloquean el fichero o aumentar el tiempo/máximo de reintentos en `OpenFileWithRetry`.
- Commitear y push de cambios: `git add Services/BackupService.cs README.md && git commit -m "fix(backup): retry on locked backup file and use shared read" && git push`.

<!-- CI and Coverage badges: replace {owner}/{repo} with your repository -->
Pendiente / siguientes pasos prioritarios:
![CI](https://github.com/{owner}/{repo}/actions/workflows/ci.yml/badge.svg)
- Verificación manual UI en entorno local: abrir la app, ir a `Auditoría` y validar checklist (`.github/AUDIT_PR_CHECKLIST.md`).  
- Probar flujos de backup (Copia ahora, Programar/Cancelar, Probar 1 min, Restaurar) y revisar logs en `%LocalAppData%`.  
- Revisar advertencias detectadas en compilación (nullability warnings en `AuditoriaViewModelV2` y `AuditoriaService`) y corregir donde sea necesario.  
- Adjuntar el artefacto ZIP al release (manual o con token) si se requiere distribuible para auditoría.  
- Documentar política de persistencia de claves y decidir provider (Key Vault en producción).
![Coverage](https://codecov.io/gh/{owner}/{repo}/branch/main/graph/badge.svg)
Las tareas anteriores están registradas también en la PR y en `CHANGELOG.md`.

## Changelog

- `v0.1.0-auditoria` — 2026-09-05: mejoras en subsistema de auditoría:
  - Escritura atómica del `PrevHash` + `INSERT` para evitar condiciones de carrera.
  - Firma HMAC del payload y almacenamiento de `KeyVersion`/`KeyVersionEnc`.
  - Cifrado AES‑GCM de `Detalles` cuando hay clave de encriptación.
  - Retries simples y backoff en forwarding/exporting (cola en memoria, 3 intentos).
  - Test de concurrencia que valida la cadena de hashes bajo escrituras paralelas.


## Estado actual de la aplicación

### Visión funcional
La aplicación `ClinicaLongevidadApp` está orientada a servir como software clínico propio para una clínica de longevidad y envejecimiento metabólico basada en evidencia. La prioridad actual es consolidar una base operativa segura y trazable sobre la que después se construyan los módulos clínicos avanzados.

### Módulos actualmente implementados

#### Acceso y seguridad
- Selección de área: Administración, Recepción y Médico.
- Inicio de sesión con validación de credenciales.
- Control de acceso por área.
- Bloqueo temporal por intentos fallidos.
- Cierre de sesión.
- Bloqueo por inactividad.

#### Administración
- Gestión de usuarios.
- Auditoría con filtros y exportación CSV.
- Gestión de festivos.

#### Recepción
- Gestión de pacientes.
- Gestión de citas.
- Vista combinada y modos separados para pacientes y citas.
- Historial y próximas citas del paciente.
- Flujo de estados de cita.
- Control de festivos en agenda.
- Aviso de regularización de protección de datos.

### Flujo actual de citas en recepción
- `Pendiente`
- `Confirmada`
- `Sala espera`
- `En consulta`
- `Finalizada`
- `Facturada`
- `Cancelada`

### Estado actual de la auditoría

#### Ya auditado
- Login correcto/fallido y acceso no autorizado (`Login.*`).
- Cierre de sesión, bloqueo por inactividad y desbloqueo (`Sesion.*`).
- Crear/editar/cambiar rol/cambiar área/activar/desactivar/restablecer password/eliminar usuario (`Usuario.*`).
- Alta/edición de pacientes en recepción (`Paciente.*`).
- Creación/edición/confirmación/cancelación/cambios de estado/facturación/eliminación lógica de próximas citas (`Cita.*`).
- Alta/edición/eliminación de festivos (`Festivo.*`).

#### No auditado todavía
- Cobertura de módulos clínicos futuros (área Médico).

### Auditoría enriquecida
Se añadió `AuditoriaEvento` y `AuditoriaService.RegistrarEvento(...)` con compatibilidad hacia `Registrar(...)`.

Estado actual:
- Eventos nuevos con `Tipo` y `Detalles` en JSON.
- Metadatos automáticos en servicio: `Rol`, `Area`, `SesionId`, `Equipo`, `VersionApp`.
- Migración completada de flujos activos a `RegistrarEvento(...)`.
- Sin usos restantes de `App.AuditoriaService?.Registrar(...)` en código activo.

### Nota: comportamiento en entornos de pruebas y variables de entorno relevantes

- Durante la ejecución de pruebas unitarias la aplicación omite la creación de los triggers "append-only" (los triggers que impiden `UPDATE`/`DELETE` en la tabla `Auditoria`) para permitir que los tests simulen manipulación y escenarios de integridad. La detección de ejecución bajo test se realiza buscando `DOTNET_ENVIRONMENT=Test` o ensamblados de pruebas comunes (xUnit/NUnit/VSTest). En entornos reales los triggers se crean por defecto para reforzar la inmutabilidad.

- Variables de entorno relevantes para el comportamiento de auditoría (resumido):
  - `REQUIRE_KEYVAULT=1` — exigir Azure Key Vault al inicializar `AuditoriaService` (fallará rápido si no está disponible).
  - `AUDIT_ALLOW_PLAINTEXT_DETAILS=1` — permitir almacenar `DetallesPlain` en entornos de `Production` cuando sea necesario para diagnósticos.
  - `AUDIT_INCLUDE_DETAILS_IN_REPORTS=1` — incluir el campo `Detalles` en los informes completos de integridad (por defecto está redactado).
  - `AUDIT_INCLUDE_DETAILS_IN_DIAGNOSTICS=1` — incluir `Detalles` en los CSV/diagnósticos rápidos (por defecto está redactado).
  - `AUDIT_INCLUDE_DETAILS_IN_LOGS=1` — permitir que `AuditLogHelper` escriba detalles completos en logs (por defecto `AuditLogHelper` redacta payloads sensibles).

Estas opciones permiten equilibrar seguridad y diagnósticos: por defecto la aplicación minimiza la exposición de `Detalles` y fuerza inmutabilidad en producción, mientras que en entornos de desarrollo/pruebas se relajan ciertas restricciones para facilitar pruebas y depuración.

### Generar diagnósticos de integridad (administradores)

Para auditores y administradores la aplicación dispone de un flujo profesional para generar diagnósticos de integridad de la cadena de auditoría. Resumen:

- Requisito: el usuario debe tener rol/área de `Administración` (el acceso está restringido a Administradores).
- Desde la UI (recomendado):
  - Iniciar sesión seleccionando el área `Administración` y entrar con una cuenta con rol `Administración`.
  - Navegar a `Administración` → `Abrir Auditoría` (o usar el botón `Generar diagnóstico` en la vista `Auditoría` si está visible para administradores).
  - Pulsar `Generar diagnóstico`. La aplicación ejecuta comprobaciones rápidas y un informe de integridad completo.

- Salida y ubicación de ficheros:
  - Informes rápidos y logs: `%LocalAppData%\ClinicaLongevidadApp\logs` (ej. `AuditDebug.txt`, `IntegrityQuickSummary_*.txt`).
  - Informe de integridad completo (JSON): `%ProgramData%\ClinicaLongevidadApp\AuditIntegrityReports\IntegrityReport_<timestamp>_id<N>.json`.
  - CSVs con filas problemáticas (si se detectan): junto al informe JSON o en la carpeta de logs.

- CLI / herramienta auxiliar (solo lectura):
  - Hay una herramienta de comprobación incluida en `tools/CheckAdmin`. Para ejecutarla desde el repositorio:
    ```
    dotnet run --project tools/CheckAdmin "C:\Users\<usuario>\AppData\Local\ClinicaLongevidad.db"
    ```
  - La herramienta devuelve si el usuario `admin` existe y un resumen básico; es útil para automatizar comprobaciones de estado.

- Buenas prácticas tras un diagnóstico:
  1. No modificar la base de datos de producción directamente. Hacer copia de la BD antes de cualquier reparación.
  2. Clasificar problemas (hash faltante, firma inválida, desalineado de PrevHash) y priorizar por impacto.
  3. Preparar un plan de recuperación sobre copia: backfill de hashes o marcar filas como `suspect` para análisis manual.
  4. Mantener artefactos generados con marca de tiempo para auditoría y trazabilidad.

### Convención actual de `Detalles` (JSON)
Claves estándar actuales:
- `PacienteId`
- `CitaId`
- `FestivoId`
- `UsuarioId`
- `Fecha`
- `Tipo`
- `Profesional`
- `Hora`
- `NuevoEstado`
- `Motivo`
- `Error`
- `SesionId`
- `Rol`
- `Area`
- `Equipo`
- `VersionApp`

Compatibilidad:
- El sistema de filtros acepta JSON nuevo y formato legado `Clave=Valor;...`.

### Roadmap inmediato

#### Fase 1
- [x] Crear `AuditoriaEvento`.
- [x] Ampliar `AuditoriaService` sin romper la API actual.
- [x] Preparar ampliación de tabla `Auditoria` con migraciones defensivas.

#### Fase 2
- [x] Auditoría de operaciones de `PanelRecepcionViewModel`.

#### Fase 3
- [x] Auditoría de `FestivosViewModel` (`Festivo.Crear`, `Festivo.Editar`, `Festivo.Eliminar`).

#### Fase 4
- [x] Filtros avanzados en `AuditoriaView` (`Tipo`, `Rol`, `Área`, `Severidad`, `PacienteId`, `CitaId`, `SesionId`).
- [x] Detalle formateado JSON y botón para copiar `SesionId`.

### Riesgos detectados
- El área `Médico` no está implementada aún.
- Los futuros módulos clínicos deben mantener el mismo patrón de auditoría para no perder trazabilidad.

### Nota de decisión funcional
El área `Médico` aún no está disponible y deberá construirse sobre esta base consolidada de auditoría, recepción y administración.

## Proceso de cierre de sesión de trabajo

Al cerrar una sesión de trabajo del proyecto, se debe actualizar `README.md` con un resumen operativo para conservar el contexto.

### Qué debe guardarse al cerrar sesión
- Cambios realizados en la sesión.
- Decisiones técnicas tomadas.
- Estado actual de cada módulo afectado.
- Incidencias detectadas.
- Tareas pendientes.
- Siguiente paso recomendado.

### Formato recomendado del resumen de cierre

#### Sesión
- Fecha: 04/09/2026
- Módulos tocados: Auditoría (AuditoriaService, AuditoriaViewModel), Helpers (AuditoriaDetallesHelper), Views relacionadas.
- Objetivo de la sesión: Corregir error en el filtrado/parseo de detalles de auditoría y completar la compatibilidad con formatos JSON y legado.

#### Hecho
- Añadido `Helpers/AuditoriaDetallesHelper.cs` con `CoincideCampo(...)` para soportar JSON y formato legado.
- Revisado `AuditoriaService` y `AuditoriaViewModel`; integrado helper en la vista de auditoría.
- Preparada la lógica de integridad y firma en `AuditoriaService` (ver métodos `VerifyIntegrity`, `CalcularHmacInstance`).
- Se detectó un error externo en Copilot/Integración (context window exceeded) durante la operación de cierre de sesión.

#### Pendiente
- Ejecutar compilación completa y pruebas funcionales sobre filtros y exportación CSV.
- Verificar integridad de la cadena de auditoría con `AuditoriaService.VerifyIntegrity()` en entorno de pruebas.
- Confirmar gestión y rotación de claves para HMAC/encryption en entornos seguros.

#### Riesgos o incidencias
- Errores de la herramienta Copilot no afectan al repositorio pero impiden algunas acciones de integración. Mantener registro.
- Si la tabla `Auditoria` cambia en producción, asegurarse de migraciones defensivas.

#### Siguiente paso
- Compilar y probar localmente; validar filtros por `PacienteId`, `CitaId`, `SesionId` y exportación CSV.
- Si todo OK, confirmar cierre de sesión y documentar resultados adicionales si aparecen.

### Regla de trabajo
Cuando se indique expresamente "cerramos sesión", se debe preparar y dejar actualizado en `README.md` el resumen del estado del proyecto para que no se pierda el progreso entre sesiones.

#### Sesión
- Fecha: 04/09/2026
- Módulos tocados: Auditoría (AuditoriaService, AuditoriaViewModel), Helpers (AuditoriaDetallesHelper), Views relacionadas.
- Objetivo de la sesión: Corregir error en el filtrado/parseo de detalles de auditoría y completar la compatibilidad con formatos JSON y legado.

#### Hecho
- Añadido `Helpers/AuditoriaDetallesHelper.cs` con `CoincideCampo(...)` para soportar JSON y formato legado.
- Revisado `AuditoriaService` y `AuditoriaViewModel`; integrado helper en la vista de auditoría.
- Preparada la lógica de integridad y firma en `AuditoriaService` (ver métodos `VerifyIntegrity`, `CalcularHmacInstance`).
- Añadidos tests unitarios `ClinicaLongevidadApp.Tests/AuditoriaDetallesHelperTests.cs`.

#### Pendiente
- Ejecutar compilación completa y pruebas funcionales sobre filtros y exportación CSV.
- Verificar integridad de la cadena de auditoría con `AuditoriaService.VerifyIntegrity()` en entorno de pruebas.
- Confirmar gestión y rotación de claves para HMAC/encryption en entornos seguros.

#### Riesgos o incidencias
- Errores de la herramienta Copilot no afectan al repositorio pero impiden algunas acciones de integración. Mantener registro.
- Si la tabla `Auditoria` cambia en producción, asegurarse de migraciones defensivas.

#### Siguiente paso
- Compilar y probar localmente; validar filtros por `PacienteId`, `CitaId`, `SesionId` y exportación CSV.
- Si todo OK, confirmar cierre de sesión y documentar resultados adicionales si aparecen.

## Registro automático
Este bloque no modifica el `README.md`. Se añade únicamente para registrar que el asistente leyó el archivo a petición del usuario.

#### Sesión de cierre reciente
- Fecha: 05/09/2026
- Objetivo: Cierre de sesión de trabajo y estabilización del flujo en recepción tras detectar un NRE en guardado de pacientes durante tests.
- Cambios realizados:
  - `ViewModels/PanelRecepcionViewModel.cs`: endurecido `GuardarDatos()` para usar una copia local (`pacienteLocal`) y asignar `_pacienteActual` solo tras un guardado exitoso. Auditoría referenciada a la copia local para evitar NRE en entornos de prueba.
  - `ClinicaLongevidadApp.Tests/CerrarSesionTests.cs`: prueba unitaria añadida que verifica `App.CerrarSesion()` limpia `Sesion` sin lanzar.
- Acciones verificadas:
  - Compilación completa: OK (con advertencias menores de paquetes y SDK).
  - Suite de tests: 21/21 OK.
  - La aplicación arrancó con `dotnet run` sin errores visibles en este entorno (la UI WPF no se muestra aquí).
- Notas:
  - Advertencias NuGet: `sqlite-net-pcl` y `Azure.Identity` (revisar actualización de paquetes si procede).
- Siguientes pasos recomendados:
  1. Revisar y actualizar paquetes NuGet según política del proyecto.
  2. Ejecutar pruebas manuales UI en entorno local para validar flujo completo de recepción.
  3. Confirmar y subir cambios al repositorio:
      - `git add .`
      - `git commit -m "Harden GuardarDatos in PanelRecepcionViewModel; add CerrarSesion test"`
      - `git push`

### Sesi?n (actualizaci?n automatizada)
- Fecha: 05/09/2026
- Objetivo: Ejecutar compilaci?n y suite de tests para validar estado del proyecto.
- Hecho:
  - Compilaci?n: OK (con advertencias sobre paquetes NuGet).
  - Tests: 21/21 correctas.
  - Advertencias detectadas: `Azure.Identity` (vulnerabilidades reportadas), `sqlite-net-pcl` versi?n resuelta a 1.9.172.
- Pendiente:
  - Proponer y aplicar actualizaciones de paquetes vulnerables.
  - Ejecutar pruebas manuales de UI para validar flujo de recepci?n.
  - Validar integridad de auditor?a en entorno de pruebas si procede.
- Siguiente paso recomendado:
  1. Actualizar `Azure.Identity` a una versi?n sin vulnerabilidades o documentar mitigaci?n si la actual es requerida.
  2. Ejecutar pruebas manuales UI en entorno local.
  3. Si todo OK, commitear y pushear cambios de dependencias y pruebas.

# Resumen de cambios y estado del trabajo

Fecha: 2026-09-06

Estado general
- Proyecto: ClinicaLongevidadApp (WPF, .NET 8)
- Rama actual: master (repositorio local en C:\Proyectos\ClinicaLongevidadApp)

Qué se ha hecho
- Restaurado el formulario "Crear Cita" en `Views/PanelRecepcionView.xaml` y vinculado su visibilidad a la propiedad `MostrarFormularioCrearCita` del ViewModel.
- En `ViewModels/PanelRecepcionViewModel.cs`:
  - Añadida gestión de visibilidad para el formulario de cita; en modo `Citas` el formulario aparece por defecto.
  - Implementada lógica de creación/edición de citas con validaciones (festivos, disponibilidad, datos de paciente).
  - Añadida colección `ProximasCitasPaciente` y presentación en la ficha del paciente.
  - Añadida propiedad `ProximaCitaSeleccionada` y comandos `EditarProximaCitaCommand` y `EliminarProximaCitaCommand`.
  - `EliminarProximaCitaCommand` marca la cita como `Cancelada` y actualiza la vista.
  - Ajustes para cargar horas disponibles y mantener la edición correcta cuando se selecciona una cita.
- En `Views/PanelRecepcionView.xaml` (UI):
  - `Proximas citas` ahora muestra un `DataGrid` seleccionable con columnas `Fecha / Hora / Profesional / Estado`.
  - Estilo de cabecera de columnas alineado con `Historial reciente` (mismo fondo, color y padding).
  - Listado de próximas citas limitado visualmente para mostrar ~3 filas y con scroll si hay más.
  - Añadidos botones "Editar cita seleccionada" y "Eliminar seleccionada" (a la derecha), con habilitado según selección.
  - Ajustes de espaciado/alto para consistencia visual entre listados.
- Servicio nuevo/modificado: `Services/AuditoriaService.cs`
  - Servicio que registra eventos en base SQLite con hashing encadenado, firma HMAC, opcional cifrado AES-GCM para detalles, y capacidad de exportar/reenviar.
  - Incluye utilidades para verificar integridad y generar reportes de diagnóstico.

Qué tenemos que hacer / siguientes pasos sugeridos
- Tests manuales:
  - Verificar que al seleccionar distintas próximas citas, el botón Editar abre la cita correcta.
  - Probar eliminar cita (marcar como Cancelada) y comprobar que desaparece del listado de próximas citas y aparece en historial.
  - Confirmar que la UI no presenta regresiones en otros listados (Pacientes, Citas del día).
- Mejoras opcionales:
  - Habilitar doble clic en fila de `Proximas citas` para abrir edición directa.
  - Añadir confirmación visual (toast/snackbar) tras eliminar o editar cita.
  - Añadir tests unitarios para `PanelRecepcionViewModel` (lógica de selección/edición/elim.)
  - Revisar y configurar `AuditoriaService` en entorno (provider de claves, webhook/exportador) y documentar claves necesarias en variables de entorno.

Qué estamos haciendo ahora
- Mantenimiento de consistencia visual y comportamiento de listados en la ficha de paciente (alineación de cabeceras, padding, altura y scroll).
- Registrar y recordar las decisiones implementadas para continuar con nuevos ajustes bajo la misma base.

Notas adicionales
- Para aplicar cambios en ejecución, usar Hot Reload o reiniciar la app si está en modo depuración.
- Si quieres que incluya cambios adicionales (doble clic, confirmaciones, tests), indícalo y lo implemento.

-- GitHub Copilot (resumen automático)

## Resumen de la sesión (actual)

- Fecha: 2026-09-07
- Estado: la aplicación compila y el panel `Auditoría` muestra registros al pulsar `Actualizar`.

Qué tenemos
- `AuditoriaService` con integridad (hash encadenado), firma HMAC y cifrado AES-GCM para `Detalles` cuando hay clave.
- `GetRecentAudits(...)` implementado y expuesto para la UI.
- `AuditoriaViewModel` con `ListaAuditoria` y `ActualizarCommand` que llena el `DataGrid` del panel principal.
- `RecentAuditWindow` sigue disponible como fallback.

Qué hicimos hoy
- Movida la implementación de `GetRecentAudits` fuera del constructor para restaurar la sintaxis y evitar errores de compilación.
- Añadida propiedad `EventId` a `Models/AuditoriaModel` para ajustar el contrato con la consulta.
- Modificado `BtnRecentAudit_Click` para que, por defecto, ejecute `ActualizarCommand` del ViewModel y solo abra la ventana auxiliar si no hay ViewModel.

Pendiente / siguientes pasos
- Probar en ejecución: arrancar la app, abrir `Auditoría` desde el dashboard y pulsar `Últimos movimientos` (debe rellenar el panel sin abrir ventana).
- Si la vista queda vacía: comprobar que la vista se instancia con `DataContext = new AuditoriaViewModel(App.AuditoriaService)` y que `Application.Current.Properties["AuditConnectionString"]` está configurada.
- Mejoras opcionales: paginación, aplicar filtros en `GetRecentAudits`, exportar CSV e integrar tests para la consulta de recientes.

Acciones recomendadas antes de cerrar sesión
- `git add README.md` + `git commit -m "docs: resumen de sesión 2026-09-07 — auditoría"` + `git push` para preservar el estado.

---

#### Sesión de cierre
- Fecha: 08/09/2026
- Módulos tocados: Auditoría (`AuditoriaService`, `AuditoriaViewModel`, `AuditoriaView`), rotación de claves (providers locales), helpers y logs diagnósticos.
- Objetivo de la sesión: estabilizar el panel de `Auditoría` y comprobar rotación y lectura de claves HMAC/ENC en entorno local.

#### Hecho
- `GetRecentAudits(...)` amplió la consulta para incluir `Rol`, `Area`, `SesionId`, `Equipo`, `VersionApp` y rellena metadata desde `Detalles` JSON cuando las columnas están vacías.
- `AuditoriaViewModel`:
  - Añadidos `AplicarFiltrosCommand` / `LimpiarFiltrosCommand` y lógica cliente para filtros (texto, usuario, módulo, fechas, `PacienteId`/`CitaId`/`SesionId`).
  - Añadidos `RotateHmacCommand` y `RotateEncCommand` y verificación (`VerifyIntegrity`) tras rotación.
  - Añadido `CargarUltimosAsync(limit)` para mostrar N últimos en el panel.
- `AuditoriaView`:
  - `Últimos movimientos` carga los 10 últimos dentro del panel cuando es posible; hay fallback a `RecentAuditWindow`.
  - `DetalleRegistroFormateado` incluye metadata y formatea JSON.
- Rotación local: `LocalKeyRotationProvider` persiste claves en `%LocalAppData%\ClinicaLongevidadApp\keys`, crea identificador de versión y establece variables de entorno en `Process` y (opcional) `User` para desarrollo.
- Añadido volcado diagnóstico: `%LocalAppData%\ClinicaLongevidadApp\logs\AuditDebug.txt`.

#### Pendiente / problemas conocidos
- Algunos filtros no funcionan correctamente en todos los casos; la implementación cliente está en su sitio pero requiere pruebas y probablemente desplazar parte del filtrado a consultas SQL para grandes volúmenes.
- La rotación local escribe variables a nivel `User` para comodidad en desarrollo; discutir política de persistencia (recomiendo `Process` solo o Key Vault en producción).
- Mejorar feedback UI y logging para operaciones largas (rotación, verify) y añadir tests automatizados para filtros.

#### Siguiente paso recomendado
1. Ejecutar pruebas manuales sobre `Filtrar`, `Actualizar`, `Últimos movimientos` y `Exportar CSV`; revisar `%LocalAppData%/.../AuditDebug.txt` si hay inconsistencias.
2. Decidir política de persistencia de claves locales y, si procede, restringir a `Process` o usar Azure Key Vault.
3. Si el volumen de datos crece, mover filtros pesados a la consulta SQL e implementar paginación.

Mañana seguimos con las pruebas y ajustes de filtros. Cierro sesión.

## Pruebas locales: opción `FORCE_ADMIN` (opt-in)

Se ha añadido en la rama `feature/force-admin` una ayuda para pruebas localmente que permite forzar
la sesión como `Administración` sin pasar por el flujo de login. Esto es estrictamente para pruebas
locales y debe activarse de forma explícita.

Uso:

- PowerShell:
  - `$env:FORCE_ADMIN='1'; dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug --no-launch-profile`
- CMD:
  - `set FORCE_ADMIN=1 && dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug --no-launch-profile`

Comportamiento:

- Si `FORCE_ADMIN=1` está presente en el entorno al arrancar la aplicación, `App` establece `Sesion.RolActual`
  y `Sesion.AreaActual` a `Administración` para facilitar pruebas de las vistas y comandos restringidos.
- La lógica está contenida en `App.xaml.cs` en la rama `feature/force-admin` y no está aplicada en `master`.

Advertencias de seguridad:

- No aplicar esta variable en entornos compartidos ni en producción.
- Revisar y aprobar mediante PR antes de considerar mantener la opción en el repositorio principal.

Si quieres, creo el PR automáticamente con esta documentación y la rama `feature/force-admin` listos para revisión.

## Cierre de sesión (GitHub)

### Resumen de la sesión (cierre)
- Fecha: 2026-09-07 (cierre)
- Rama activa: `feat/audit-rewrite`

Hecho en esta sesión:
- Consolidada `AuditoriaViewModelV2` como VM única para el panel Auditoría.
- Eliminado VM legacy y actualizado `Views/AuditoriaView.xaml(.cs)` y `MainWindow.xaml.cs` para usar V2.
- Implementada copia inmediata (`Copia ahora`), restauración y programación diaria.
- Añadidos logs de diagnóstico: `%LocalAppData%\\ClinicaLongevidadApp\\logs\\backup_vm.log` y `backup.log`.
- Añadida guardia para evitar programaciones duplicadas (evita múltiples timers al pulsar repetidamente `Programar`).

Pendientes inmediatos:
- Validar en ejecución que el botón `Programar/Cancelar` muestra estado inequívoco tras los cambios.
- Revisar y endurecer `Cleanup()` para asegurar que `DispatcherTimer` y eventos quedan desuscritos correctamente.
- Añadir pruebas automatizadas básicas para: scheduling, cancelar scheduling y restore.
- Limpiar trazas de debug una vez validado el comportamiento en entorno local.

Siguiente paso recomendado:
1. Ejecutar la app localmente y reproducir estos escenarios: programar misma hora varias veces; cambiar hora y reprogramar; cancelar programación.
2. Revisar `backup_vm.log` para confirmar que no hay múltiples registros "ScheduleBackup: registering..." para la misma hora.
3. Si OK, commitear y push: `git add . && git commit -m "fix(audit): prevent duplicate scheduling, consolidate AuditoriaViewModelV2" && git push`.

## Resumen adicional: trabajo en rama feat/audit-rewrite (síntesis)

Breve resumen de lo realizado durante la reescritura y pruebas del panel de Auditoría y del servicio de copias:

- Se reimplementó `AuditoriaViewModelV2` con comandos de backup (`BackupNowCommand`, `ScheduleOrCancelCommand`, `RestoreBackupCommand`, `TestScheduleInOneMinuteCommand`) y persistencia de la hora programada.
- `BackupService` ahora ofrece `ScheduleDailyBackup(TimeSpan, ...)` que devuelve la próxima ejecución (`DateTime?`) y `TriggerImmediateBackup(...)` para disparos manuales.
- Se añadió un temporizador a nivel de VM (`_uiTimer`) para asegurar que la hora introducida en el textbox dispare la misma acción que "Probar 1 min".
- Forzado el `DataContext` de la vista `AuditoriaView` a la VM V2 y protegido contra reasignaciones externas (evita que el diseñador/runtime use el VM legacy y produzca bindings rotos).
- Añadidos logs: `%LocalAppData%\\ClinicaLongevidadApp\\logs\\backup.log` (servicio) y `backup_vm.log` (VM) para diagnosticar programación y ejecuciones.

Qué falta / próximos pasos prioritarios:

1. Validar en el entorno del usuario que al pulsar "Programar" la UI actualiza `Próxima copia:` y que se crea la copia en `%LocalAppData%\\ClinicaLongevidadApp\\backups`.
2. Si la programación no dispara, pegar los contenidos recientes de `backup_vm.log` y `backup.log` y la salida del depurador para investigar.
3. Eliminar/ajustar MessageBox en ejecuciones automáticas para que las copias programadas sean silenciosas (usar snackbar + log).
4. Consolidar y limpiar trazas/hacks de transición (code-behind) antes de merge final.

Comandos y ubicaciones útiles para pruebas:

- Forzar copia inmediata desde VM: `BackupNowCommand` (UI) o método `ForceScheduledNow()`/`TriggerImmediateBackup(...)` en el servicio.
- Logs: `%LocalAppData%\\ClinicaLongevidadApp\\logs\\backup.log`, `%LocalAppData%\\ClinicaLongevidadApp\\logs\\backup_vm.log`.
- Copias generadas: `%LocalAppData%\\ClinicaLongevidadApp\\backups`.

Si lo prefieres, preparo mañana un pequeño parche para que las copias programadas no abran MessageBox y para añadir más trazas puntuales si la reproducción falla en tu equipo.
- Objetivo: dejar un resumen claro de lo realizado en la sesión y las tareas pendientes antes de cerrar la sesión del repositorio.

### Hecho
- Restaurado el botón/admin de diagnóstico en la vista de Auditoría y añadidos los comandos de backup en `AuditoriaViewModel`.
- Mejorada la lógica de `Services/BackupService.cs`: uso de backup online de SQLite (`BackupDatabase`), configuración de `PRAGMA busy_timeout`, y fallback a copia de fichero.
- Añadido logging en `BackupService` (usa `Services/LogService`) para registrar inicio, éxito y fallos de los backups.
- Ajustes en `tools/GenerateIntegrity` (TFM y paquetes) y ejecución de diagnóstico de integridad (genera `IntegrityReport_local.json`).
- Proyecto compila correctamente después de los cambios.

### Pendiente (inmediato)
- Probar el backup en runtime: ejecutar la acción "Copia ahora" desde la UI de Auditoría o invocar `BackupService.CreateBackup(...)` con la connection string usada en `Application.Current.Properties["AuditConnectionString"]` y confirmar que se crea el `.db` de backup.
- Si el backup falla: inspeccionar procesos que bloqueen la DB y revisar la cadena de conexión usada por la UI.
- Ejecutar la suite de tests local (`dotnet test`) y revisar logs generados en `%LocalAppData%\ClinicaLongevidadApp\Logs`.

### Falta (trabajo a medio plazo)
- Decidir y documentar la política de persistencia/rotación de claves HMAC/ENC (Local vs Key Vault) y aplicar en `KeyRotation` providers.
- Plan de backfill/append-only para corregir filas históricas sin `Hash`/`Signature` — preparar script sobre copia de la BD y pruebas en entorno no productivo.
- Mejorar feedback UI (toasts/snackbars) y añadir logging más detallado/rotación de logs.

### Nota final
Antes de cualquier intervención en la tabla `Auditoria` en producción, realizar copia de seguridad válida y comprobada y documentar el proceso. Para la próxima sesión: primero validar backups y logs, luego proceder con el plan de remediación de integridad si procede.

## Audit hardening — environment variables & runtime notes

Breve referencia para desarrolladores/operadores. Leer antes de modificar auditoría o activar workers en entornos compartidos.

- `KEYVAULT_URI`: prefer Azure Key Vault for HMAC/encryption keys when set.
- `REQUIRE_KEYVAULT=1`: fail startup if `KEYVAULT_URI` is not set (use to enforce Key Vault in production/staging).
- `AUDIT_FORWARD_ENABLED=1`: enable forward queue worker (opt-in).
- `AUDIT_INTEGRITY_ENABLED=1`: enable integrity worker (opt-in).
- `AUDIT_INCLUDE_DETAILS_IN_REPORTS=1`: include `Detalles` in integrity reports (disabled by default).
- `AUDIT_INCLUDE_DETAILS_IN_DIAGNOSTICS=1`: include `Detalles` in quick diagnostics CSV (disabled by default).
- `AUDIT_INCLUDE_DETAILS_IN_LOGS=1`: allow writing `Detalles` into application logs (disabled by default).
- `AUDIT_ALLOW_PLAINTEXT_DETAILS=1`: permit persisting `DetallesPlain` in Production (disabled by default; avoid in prod).
- `AUDIT_HMAC_KEY`, `AUDIT_ENC_KEY`: local-only keys for `LocalKeyProvider` (development/testing only).
- `FORCE_ADMIN=1`: development helper to force admin session for local testing (do not use in shared environments).

Quick run (development):

- Force admin and run:
  - PowerShell: `$env:FORCE_ADMIN='1'; dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug`
- Enable workers locally:
  - PowerShell: `$env:AUDIT_FORWARD_ENABLED='1'; $env:AUDIT_INTEGRITY_ENABLED='1'; dotnet run --project ClinicaLongevidadApp.csproj --configuration Debug`

Notes:
- Reports and diagnostics redact `Detalles` by default to avoid leaking sensitive payloads; enable inclusion only in trusted environments.
- The audit table is protected by SQLite triggers to enforce append-only behavior (UPDATE/DELETE are aborted).

## Cierre de sesión: 24/09/2026

- Fecha: 24/09/2026
- Estado: sesión de auditoría y limpieza de código realizada.

Hecho (resumen corto):
- Limpiadas `using` duplicadas y eliminado warning de inicialización en `AuditoriaService` (`_keyProvider = null!`).
- Corregidos avisos `async` sin `await` en `AuditoriaViewModelV2`/`BackfillService` para evitar advertencias de compilación.
- Ejecutada la suite de tests local (`dotnet test`) — 50/50 passed.
- Ejecutado el runner de auditoría; creado `audit-artifacts_20260924_003022.zip` y subido al release `audit-rewrite-8684b20` en `hombreirobamio-prog/ClinicaLongevidadApp`.

Qué se está haciendo ahora:
- Inspección y extracción de los artefactos de auditoría para revisión manual.
- Revisión de logs en `%LocalAppData%\\ClinicaLongevidadApp\\logs` y generación de informe de integridad si procede.

Pendiente / siguiente pasos:
- Revisar `backup.log` y `backup_vm.log` tras ejecuciones programadas y tests de backup.
- Añadir tests que simulen locks concurrentes sobre ficheros de backup para evitar regresiones.
- Decidir política de persistencia/rotación de claves HMAC/ENC (Local vs Key Vault) y aplicar cambios en `KeyRotation` si procede.
- Corregir advertencias en tests (`xUnit2020` recomendaciones) reemplazando `Assert.True(false, ...)` por `Assert.Fail(...)` o equivalente.

Acción recomendada antes de cerrar sesión:
- Confirmar que los logs y el informe de integridad no contienen problemas críticos. Si todo OK, commitear y push final.

 - Audit finalizada: artefactos subidos al release `audit-rewrite-8684b20` (`audit-artifacts_20260928_150118.zip`).

