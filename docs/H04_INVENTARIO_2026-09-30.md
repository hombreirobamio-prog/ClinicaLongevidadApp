# H04: inventario acotado de escrituras e inicio de sesión

Revisión local del 30/09/2026. H04 permanece abierto.

Se buscaron escrituras SQLite y de archivos en el código de la aplicación, excluyendo pruebas, herramientas y directorios generados. Es un inventario estático de rutas identificadas, no una certificación exhaustiva de ejecución.

| Ruta | Evidencia y situación | Siguiente paso |
| --- | --- | --- |
| FestivoService, PacienteService, CitaService, UsuarioService | Escrituras de negocio adaptadas a RegistrarEventoConOperacion en entregas previas; pruebas de rollback conservadas. | Validación manual de UI y permisos. Revisar operaciones que afectan cero filas en pacientes/festivos. |
| LoginViewModel | La sesión se publicaba antes del registro y se ignoraban los fallos de auditoría. Corregido: auditar primero; después asignar sesión, notificar y navegar. | Validación manual de acceso y mensajes con auditoría no disponible. |
| HorarioProfesionalService.Guardar | Adaptado: alta/actualización y evento en una transacción, con rollback e Id asignado tras COMMIT. | Mantener validación operativa; no se encontraron llamadores de escritura en las vistas revisadas. La política de permisos no se ha modificado. |
| AuditAdminService.RequeueDeadLetter / DeleteDeadLetter | Adaptados: intervención y evento en la transacción común. Comprueban Administración mediante AuthorizationHelper y devuelven false ante fallo o Id inexistente. | Validación manual; el control de roles conserva la activación por AUDIT_ENFORCE_AUTH. Durabilidad del envío pendiente. |
| AuditForwardQueue y worker | Adaptado: el evento auditado y la salida se insertan en una transacción SQLite; el worker procesa una sola ejecución simultánea y elimina tras éxito de todos los destinos. | Verificar recuperación operativa y que los destinos deduplican por EventId. La entrega es al menos una vez; no asumir que cada mantenimiento de cola deba auditarse recursivamente. |
| KeyRotationService y AuditoriaViewModelV2.RotateHmacAsync/RotateEncAsync | La UI ya no anuncia éxito si falla el evento tras persistir la clave; muestra la incidencia y conserva las versiones anterior/nueva sin material secreto. La fábrica reconoce `REQUIRE_KEYVAULT=1` y bloquea el proveedor local en producción. La rotación usa los mismos nombres `AUDIT_HMAC_SECRET_NAME` y `AUDIT_ENC_SECRET_NAME` que el proveedor de auditoría. | Verificar operativamente el acceso a versiones históricas de esos secretos y la recuperación entre almacén externo y auditoría. No hay transacción SQLite que abarque Key Vault o archivos. |
| BackupService, exportaciones e informes | Escrituras de archivos, companions, sustitución de base e informes. | Mantener revisión de recuperación y custodia en su ámbito operativo; las transacciones de las entidades no cubren estas operaciones. |
| Alta implícita de paciente y cita | Dos transacciones independientes. | Decidir e implementar la unidad de negocio compuesta en una tarea específica. |
| UsuarioService.ValidarLogin y registros de fallo de ViewModels | Persisten rutas que ocultan fallos al registrar intentos. LoginViewModel ya no usa ValidarLogin: valida una única lectura del usuario y emite Login.*. | Revisar consumidores restantes y política de registros de fallo. No declarar resuelta la auditoría global. |

## Alcance de la corrección de login

- Mantiene el constructor público, las comprobaciones de usuario activo, contraseña y acceso al área, y el bloqueo tras cinco intentos fallidos durante un minuto.
- La consulta y navegación tienen puntos internos de sustitución para probar el comando sin acceder a datos reales ni abrir ventanas.
- Login.Correcto recibe rol y área explícitos antes de publicar la sesión; no requiere elevar la sesión para construir el evento.
- Los errores se muestran de forma genérica y liberan la protección contra reentrada, permitiendo reintentar.
- El contador de credenciales fallidas avanza antes de auditar; una caída de auditoría no evita el bloqueo.
- El flujo produce un único Login.FallidoCredenciales para usuario inexistente, inactivo o contraseña incorrecta. Deja de generar el Usuario.Login adicional del servicio para usuarios inactivos.
- No existe una transacción común entre SQLite, memoria y navegación WPF. Un evento puede quedar confirmado si el proceso se interrumpe antes de abrir el panel; el control implementado impide publicar una nueva sesión cuando falla su auditoría.
- El bloqueo sigue siendo local al proceso, como antes. No se modifica su política ni se añade persistencia.
