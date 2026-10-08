# Checklist de traslado a un ordenador nuevo

Esta lista sirve para instalar una entrega limpia. No traslada la base SQLite, copias, claves locales, registros ni secretos del equipo de desarrollo.

## Antes de copiar

1. Conservar el ZIP de entrega y su archivo `.sha256` juntos.
2. Comprobar el SHA-256 del ZIP antes de descomprimirlo.
3. Copiar el ZIP a un directorio local del ordenador de destino y descomprimirlo.

## Instalación limpia

1. Ejecutar `App\ClinicaLongevidadApp.exe` desde la carpeta descomprimida.
2. Confirmar que la aplicación inicia sin una base ni registros heredados.
3. No copiar `ClinicaLongevidad.db`, carpetas de artefactos ni claves desde el ordenador de desarrollo.

## Claves y auditoría antes de datos reales

1. Designar el Key Vault final y las personas autorizadas para administrarlo.
2. Crear o configurar los secretos HMAC y de cifrado para el nuevo entorno siguiendo la guía de auditoría.
3. Conservar cada versión de clave mientras existan eventos o copias que dependan de ella.
4. Verificar el acceso a Key Vault mediante el procedimiento aprobado antes de activar su uso obligatorio para la nueva base.
5. Crear el primer usuario administrador y comprobar que un inicio de sesión correcto queda registrado en la auditoría.

## Copias

1. Tras el primer uso de la aplicación, ejecutar `INSTALAR-COPIA-DIARIA.ps1` incluido en la entrega y elegir una hora diaria.
2. Crear una copia de prueba y comprobar que existen la copia SQLite, `.sha256`, `.hmac` y `.hmac.ver`.
3. Probar una restauración únicamente sobre un destino aislado, nunca sobre la base activa.
4. Guardar la evidencia de la primera copia y de la prueba de restauración con la responsable de custodia.

## Cierre de instalación

1. Registrar fecha, equipo, responsable, versión de la entrega y resultado de las comprobaciones.
2. Conservar el USB de continuidad y sus comprobantes como respaldo de desarrollo; no contiene datos clínicos ni claves.
3. Mantener el Key Vault y el almacenamiento de anclajes de prueba separados del entorno final hasta que se apruebe su configuración definitiva.
