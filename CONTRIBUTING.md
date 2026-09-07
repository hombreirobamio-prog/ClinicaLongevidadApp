# Contribuir a ClinicaLongevidadApp

Sigue estas normas para contribuir al repositorio y mantener la coherencia del proyecto.

## Flujo de trabajo
- Crea ramas temáticas siguiendo: `tipo/área/descripción-corta`, por ejemplo:
  - `feature/recepcion/guardar-paciente`
  - `fix/auditoria/parse-detalles`
  - `chore/auditoria/readme-editorconfig`
- Abre Pull Requests contra `master`. Incluye descripción, pasos para probar y screenshots si procede.

## Mensajes de commit
Usa prefijo tipo: `feat:`, `fix:`, `chore:`, `docs:`, `test:`.  
Ejemplo:
```
chore: actualizar README; añadir .editorconfig y CONTRIBUTING.md con convenciones de auditoría
```

## Estilo de código
- Se respeta `.editorconfig` en la raíz del repositorio.
- C# 12, objetivo .NET 8. Mantener compatibilidad con WPF en la UI.
- Prefiere `file-scoped namespaces`, `var` cuando el tipo es aparente, indentación con 4 espacios.

## Tests y CI
- Ejecuta la suite de tests antes de abrir PR:
  - `dotnet test`
- Añade tests para cambios funcionales y para bugs críticos (p. ej. auditoría, integridad).

## Auditoría y seguridad
- Mantén la convención `Detalles` en JSON. El sistema es compatible con el formato legado `Clave=Valor;...`.
- Antes de cerrar una sesión o introducir cambios sensibles relacionados con auditoría:
  1. Ejecutar la compilación completa.
  2. Ejecutar la suite de tests.
  3. Verificar integridad con `AuditoriaService.VerifyIntegrity()` en entorno de pruebas (añadir test si procede).
- Gestión de claves HMAC/encryption: documentar rotación y control de accesos; nunca subir secretos a Git.

## Revisión de código y merges
- Al menos una revisión aprobada por otro desarrollador para cambios funcionales.
- Squash commits cuando la PR sea de corrección menor (opcional según PR).
- Actualiza la documentación (`README.md`, `CONTRIBUTING.md`) si cambian flujos o convenciones.

## Cierre de sesión del proyecto
Al indicar "cerramos sesión" en una tarea, actualiza `README.md` con un resumen operativo que incluya:
- Cambios realizados.
- Decisiones técnicas.
- Estado de módulos afectados.
- Incidencias y tareas pendientes.
- Siguientes pasos recomendados.
