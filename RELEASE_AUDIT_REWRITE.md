Audit rewrite release

Reescritura de `AuditoriaView` y `AuditoriaViewModelV2` con mejoras de UX y controles de backup (Programar, Cancelar, Copia ahora, Restaurar, Probar 1 min).

Cambios principales:
- UI: ajustes de layout y estilos para evitar solapamientos y texto recortado en `AuditoriaView`.
- ViewModel V2: centralización de la lógica de backup, persistencia de horario programado y manejo de estado de programación.
- Tests: compilación y suite de tests locales (49/49) pasada.

Tag: audit-rewrite-8684b20

Notas de despliegue:
- Ver `CHANGELOG.md` para más contexto.
