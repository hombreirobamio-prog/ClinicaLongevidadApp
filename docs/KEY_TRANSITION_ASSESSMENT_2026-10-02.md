# Evaluación de transición de claves históricas

Fecha: 02/10/2026. Estado: **bloqueada de forma segura hasta recuperar material histórico o decidir su tratamiento.**

## Hallazgo

La base de auditoría existente contiene eventos con 12 versiones HMAC no vacías y 8 versiones de cifrado no vacías. El almacenamiento local actual conserva una sola clave HMAC y una sola clave de cifrado: las versiones locales activas. Por tanto, no están disponibles localmente las claves necesarias para comprobar todas las firmas y detalles históricos.

No se leyó, copió ni registró material de claves durante esta evaluación. Tampoco se modificó la base de auditoría, el almacén de claves ni las copias de seguridad.

## Hallazgo posterior: copia heredada localizada (08/10/2026)

Se localizó una pareja local heredada bajo `legacy_20261006\local\keys`: `hmac.key` con su versión y `enc.key` con su versión. Ambas claves están en Base64 válido de 32 bytes y sus identificadores de versión tienen el formato esperado. Los valores no se mostraron, copiaron ni registraron.

La copia heredada `legacy_20261006\local\ClinicaLongevidad.db` no contiene registros de auditoría; su resultado de cero errores en `VerifyIntegrity` no acredita una validación de firmas. La clave HMAC heredada sí coincide con su versión exacta en la base actual, pero esta conserva 2.100 incidencias por registros tempranos sin hash o versión verificable. Además, se evaluó una segunda clave Base64 de 32 bytes localizada fuera de la copia heredada: no coincide con ninguna versión HMAC no vacía de la base actual. Ninguna de estas comprobaciones autoriza reescribir ni declarar verificados los registros incompletos.

Los archivos de acompañamiento de las copias (`.hmac.ver`) identifican una versión para verificar una copia; no contienen por sí mismos el material de esa versión y no permiten reconstruir una clave perdida.

## Decisión operativa

No activar `REQUIRE_KEYVAULT=1` ni configurar el proveedor directo de Key Vault para la base de auditoría actual. Hacerlo ahora impediría resolver las versiones locales históricas que aparecen en los eventos existentes.

No sustituir, regenerar ni volver a firmar eventos históricos. Una clave nueva no valida una firma anterior y reescribir evidencia destruiría la capacidad de distinguir el historial original.

## Recuperación necesaria

1. Localizar una copia segura y autorizada de cada clave histórica necesaria: exportaciones anteriores, copia protegida del directorio de claves anterior o custodia equivalente.
2. Inventariar cada versión recuperada sin mostrar su valor. Para cada una, confirmar que puede validar los eventos que la referencian sobre una copia aislada de la base.
3. Importar el material recuperado en Key Vault como versiones inmutables y crear un mapeo no secreto entre el identificador histórico almacenado en la auditoría y el secreto y versión de Key Vault que lo contiene.
4. Implementar un proveedor de transición: los eventos nuevos se firmarán con la versión activa de Key Vault y los históricos se resolverán únicamente por su identificador exacto. No debe existir una sustitución silenciosa por la clave actual.
5. Probar la transición con una copia aislada: verificar eventos antiguos, crear y verificar un evento nuevo, reiniciar la aplicación y repetir la comprobación. Sólo después se evaluará la activación para la base existente.

## Si no se recuperan las claves

Los registros cuyas versiones no puedan recuperarse deben conservarse intactos y declararse como no verificables criptográficamente. La aplicación no debe afirmar que su integridad fue validada. Cualquier política de retención, etiquetado o exportación de esos registros se decidirá antes de modificar código o datos.

## Próximo dato necesario

Determinar si existe una custodia segura de las claves locales usadas antes de la versión actual. Sin ese material no se puede completar una transición verificable del histórico.
