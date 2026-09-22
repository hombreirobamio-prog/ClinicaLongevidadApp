Resumen de la sesión de backfill
Fecha: (guardar ahora) — continuar mañana

Hecho:
- Arreglado y compilable: `tools/RotateKeys`.
  - `tools/RotateKeys/RotateKeys.csproj`: cambiado `TargetFramework` a `net8.0-windows7.0` y alineada la versión de `Microsoft.Data.Sqlite` con la app (10.0.11).
  - `tools/RotateKeys/Program.cs`: corregida la lógica de control (preview/apply/backfill), añadido `using System;` y limpiados errores de sintaxis.
  - `Services/KeyRotation/KeyRotationService.cs`: renombrado `RotateEncryptionKeyAsync` a `ApplyRotateEncryptionKeyAsync`, añadido métodos de ayuda estáticos `GenerateRandomKey` y `GenerateRandomKeyBase64` y `using System.Security.Cryptography`.
  - `ClinicaLongevidadApp.csproj`: eliminadas entradas `Compile Include` que incluían `tools\\*.cs` para evitar puntos de entrada duplicados.

Comandos ejecutados y resultados relevantes:
- `dotnet run --project tools/RotateKeys -- backfill preview --db "C:\\Users\\Francisco\\AppData\\Local\\ClinicaLongevidad.db"`
  - Resultado: `Backfill preview: 117 rows would be processed.`
- `dotnet run --project tools/RotateKeys -- backfill apply --db "C:\\Users\\Francisco\\AppData\\Local\\ClinicaLongevidad.db" --dryrun`
  - Resultado: `Backfill processed=100 created=0 skipped=0 (dryRun=True)`

Pendiente / Qué queda por hacer:
- Decidir si ejecutar `backfill apply` real (modifica la DB) o mantener solo el plan append-only.
- Revisar por qué el dry-run procesó 100 filas mientras el preview estimó 117 (investigar criterios de selección/batch).
- Documentar en `docs/AUDIT_ROADMAP.md` la estrategia final para `DetallesPlain` (append-only backfill vs re-encrypt/rewrite).
- Considerar extraer servicios compartidos a una librería (para evitar que herramientas referencien el proyecto WPF) — mejora a medio plazo.

Próximos pasos (mañana):
1) Confirmar con el equipo si ejecutar `backfill apply` real.
2) Si se aprueba, hacer backup de la DB y ejecutar: `dotnet run --project tools/RotateKeys -- backfill apply --db "<ruta-db>" --force --batch 100`
3) Revisar y registrar resultados; generar informe de cambios y artefactos para auditoría.

Cómo reanudar rápidamente:
- Desde la raíz del repo:
  - `dotnet build` para validar cambios.
  - `dotnet run --project tools/RotateKeys -- backfill preview --db "<ruta-db>"` para una nueva previsualización.

Notas:
- Los cambios aplicados son mínimos para desbloquear la ejecución del tool y preservar integridad. Mantener control de versiones y backups antes de operaciones destructivas.
