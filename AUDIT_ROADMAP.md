# Audit Roadmap (inmediato)

Objetivo: finalizar y endurecer el subsistema de auditoría y backups para que quede estable, comprobable y seguro.

1) Pulido y validación
- Validar en entorno local: `Copia ahora`, `Programar`, `Cancelar`, `Probar 1 min`, `Restaurar`.
- Verificar que solo existe una programación activa y que `backup_vm.log` muestra entradas coherentes.
- Probar `Cleanup()` cerrando la vista y comprobando que no quedan timers ni handlers activos.

2) Tests y automatización
- Añadir tests unitarios/integración para:
  - Programación diaria (simular timer y comprobar `NextScheduledRun`).
  - Cancelación de programación.
  - Restauración de copia (crear backup, restaurar y comprobar que se creó copia previa).
- Integrar en CI runs básicos que ejecuten las pruebas no-UI.

3) UX y telemetría
- Mejorar feedback del botón `Programar/Cancelar` si procede (color/tooltip) para evitar confusión.
- Añadir métricas/logs estructurados sobre scheduling (operación, hora solicitada, acción tomada).

4) Harden y producción
- Revisar la política de persistencia de claves: usar Azure Key Vault en producción (REQUIRE_KEYVAULT=1).
- Establecer permisos y cifrado de backups en repositorio local o en storage remoto.
- Programar rotación periódica de claves y tests de restauración automáticos.

5) Limpieza y merge
- Revisar trazas de debug y eliminar mensajes temporales.
- Preparar PR final con descripción de cambios y pasos de validación.
- Revisar feedback en code review y mergear a `master` cuando esté validado.

Notas:
- Logs y backups en `%LocalAppData%\\ClinicaLongevidadApp\\`.
- Priorizar pruebas manuales en entorno local antes de merge.

