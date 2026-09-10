# ClinicaLongevidadApp

<!-- CI and Coverage badges: replace {owner}/{repo} with your repository -->
![CI](https://github.com/{owner}/{repo}/actions/workflows/ci.yml/badge.svg)
![Coverage](https://codecov.io/gh/{owner}/{repo}/branch/main/graph/badge.svg)

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

