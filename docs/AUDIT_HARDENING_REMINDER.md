# Recordatorio: Auditoría — empezar a trabajar en la App

Propósito

Este fichero es un recordatorio corto y práctico para quien retome el trabajo en el subsistema de auditoría. Antes de tocar código en la aplicación, leer:

- `README.md` (resumen de sesión y estado actual)
- `docs/AUDIT_ROADMAP.md` (roadmap y prioridades)

Qué revisar al empezar

1. Qué ya se hizo (rápido):
   - `AuditoriaService` con PrevHash/Hash encadenado, Firma HMAC y cifrado AES-GCM opcional.
   - Opt‑in para workers: `AUDIT_FORWARD_ENABLED`, `AUDIT_INTEGRITY_ENABLED`.
   - Redacción por defecto de `Detalles` en diagnósticos; opt‑in para incluirlos.
   - `DetallesPlain` solo para tests/diagnósticos (debe evitarse en prod).
   - Tests: `VerifyIntegrity` smoke test añadido y suite de tests existente pasando.

2. Qué falta (prioridad):
   - Forzar Key Vault en entornos production/staging si corresponde.
   - Evitar persistir `DetallesPlain` en producción (opt‑in dev only).
   - Triggers/medidas append‑only para impedir UPDATE/DELETE en tabla `Auditoria`.
   - CI job que ejecute `VerifyIntegrity()` y falle si hay errores.
   - Revisar logs y tools para asegurar que no escriben `Detalles` sin opt‑in.

Acciones rápidas al abrir la rama de trabajo

- Cambiar a la rama: `git checkout chore/audit-hardening-local`
- Actualizar: `git pull --ff-only origin chore/audit-hardening-local`
- Ejecutar tests: `dotnet test --configuration Debug`
- Revisar PR abierto: https://github.com/hombreirobamio-prog/ClinicaLongevidadApp/pull/11

Sugerencia alternativa (opcional)

- Añadir un aviso en la pantalla principal de administración (o en el `README.md`) que enlace a este fichero para que cualquier desarrollador lo vea al arrancar la app o abrir el repositorio.

Mantener este fichero corto. Si quieres, lo amplío para incluir checklist automático o una tarea en el CI para recordarlo al crear PRs.
