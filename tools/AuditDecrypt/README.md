# AuditDecrypt

Herramienta de consola pequeña para que administradores inspeccionen filas de la tabla `Auditoria` y desencripten el campo `Detalles` cuando esté cifrado.

Uso:

1. Establece la clave de cifrado en la variable de entorno `AUDIT_ENC_KEY`.
   - Puede ser un valor en Base64 (preferible) o una cadena UTF-8. Si es Base64 se decodifica antes de su uso.

2. Ejecuta la herramienta indicando la cadena de conexión SQLite y opcionalmente el número máximo de filas a mostrar:

   dotnet run --project tools/AuditDecrypt/AuditDecrypt.csproj -- "Data Source=C:\path\to\auditoria.db" 50

   O ejecuta el binario compilado:

   dotnet tools/AuditDecrypt/bin/Release/net8.0/AuditDecrypt.dll "Data Source=C:\path\to\auditoria.db" 50

Notas de seguridad:
- Esta herramienta está pensada para uso administrativo. No debe incluirse ni compilarse dentro del ejecutable principal de la aplicación.
- No almacena claves ni detalles en disco; la clave se lee únicamente desde la variable de entorno en tiempo de ejecución.
- Si no se puede desencriptar una fila, la herramienta muestra el blob original como fallback.

Compilación:

- El proyecto `tools/AuditDecrypt/AuditDecrypt.csproj` es un proyecto de consola independiente targeting `net8.0`.
- Para compilar: `dotnet build tools/AuditDecrypt/AuditDecrypt.csproj -c Release`

Contacto:
- Equipo de desarrollo: repo principal
