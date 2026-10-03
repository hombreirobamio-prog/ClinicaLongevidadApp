# Validación manual de la cola: instrucciones

## Abrir

1. Pulsa Windows + E para abrir el Explorador de archivos.
2. En la barra de dirección pega C:\Proyectos\ClinicaLongevidadApp y pulsa Entrar.
3. Haz doble clic en VALIDAR_COLA.cmd.
4. Espera a la ventana «PRUEBA AISLADA — Cola de auditoría».
5. Pulsa «Abrir cola (Audit Admin)».

Si ya tenías la prueba abierta antes de la corrección del 30/09, cierra primero las dos ventanas de la prueba y vuelve a abrir `VALIDAR_COLA.cmd`. Al seleccionar un registro de Dead Letter, Requeue y Delete deben activarse.

No se necesita contraseña. No se inicia App.OnStartup: esta herramienta utiliza una base nueva dentro de la carpeta temporal ClinicaLongevidad-PruebaCola, con cuatro registros ficticios. Las variables se cambian solo dentro de ese proceso, se desactiva el reenvío y se elimina la configuración de destinos externos. No se abre la base habitual de la aplicación.

## Probar

- Administración: selecciona PRUEBA-01 en Dead Letter y pulsa Requeue. Debe pasar a Pending Forwards.
- Selecciona PRUEBA-02 y pulsa Delete. Debe desaparecer.
- En la ventana de instrucciones, cambia el rol a Recepción. Intenta Requeue y Delete sobre PRUEBA-03: ambos deben mostrar un error y conservar el registro.
- Vuelve a Administración y marca Simular fallo de auditoría. Repite ambas acciones: deben fallar y conservar PRUEBA-03.
- Desmarca Simular fallo y reencola PRUEBA-03. Ahora debe pasar a pendientes.
- Cierra solo Audit Admin y pulsa Abrir cola de nuevo: deben conservarse los cambios.
- Pulsa Ver resultados y guardar informe. Resultado esperado: 2 pendientes, 1 fallido, 0 errores de integridad y 3 eventos de éxito (2 AuditQueue.Reencolar y 1 AuditQueue.Eliminar).
- Anota aparte si viste los mensajes correctos. El informe refleja datos y eventos; no certifica automáticamente lo que has observado en pantalla.

Refresh significa actualizar. Requeue significa reencolar. Delete significa eliminar.

El informe resultado.txt se guarda en la ruta que muestra la ventana. Puedes copiar su contenido al chat y contar si algún paso se comportó de forma diferente.

## Terminar o empezar otra vez

Cierra la ventana de instrucciones. Para repetir, vuelve a abrir VALIDAR_COLA.cmd: comienza una prueba nueva con cuatro registros. Las claves de firma son aleatorias y se mantienen solo en memoria durante esa ejecución. Los informes y bases anteriores quedan en la carpeta temporal; no se reutilizan automáticamente.

## Alcance técnico

Herramienta separada tools/AuditAdminManual, sin paquetes nuevos ni cambios de lógica en la aplicación. Utiliza la vista AuditAdminView y los métodos públicos de AuditAdminService; el rol se simula para comprobar la denegación en el servicio, no el inicio de sesión ni el acceso a la ventana desde el menú real.

Validada mediante compilación Release y --self-test: datos aislados, rol denegado, fallo de firma, rollback, operaciones correctas, integridad y construcción de la vista WPF. La comprobación visual interactiva sigue pendiente. Se requiere el SDK .NET del equipo para ejecutar el lanzador.
