# Tests - ClinicaLongevidadApp

Breve nota sobre la configuración de las pruebas en este repositorio.

Propósito
- Contiene los tests unitarios y de integración ligera para `ClinicaLongevidadApp`.

Inicializador de ensamblado
- `TestAssemblyInitializer.cs`: inicializador de ensamblado que establece por defecto las variables
  de entorno `AUDIT_HMAC_KEY` y `AUDIT_ENC_KEY` cuando no están presentes. Esto evita que
  las pruebas que verifican firmas HMAC dependan de la configuración externa del entorno (CI).

Notas para CI
- En entornos de integración continua preferible establecer variables seguras:
  - `AUDIT_HMAC_KEY` (valor base o secreto administrado)
  - `AUDIT_ENC_KEY` (clave de 32 bytes en Base64)

Si el pipeline ya provee estas variables, el inicializador no las sobrescribirá.

Comandos útiles
- Ejecutar tests localmente: `dotnet test`
