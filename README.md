# ClinicaLongevidadApp

<!-- CI and Coverage badges: replace {owner}/{repo} with your repository -->
![CI](https://github.com/{owner}/{repo}/actions/workflows/ci.yml/badge.svg)
![Coverage](https://codecov.io/gh/{owner}/{repo}/branch/main/graph/badge.svg)

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
