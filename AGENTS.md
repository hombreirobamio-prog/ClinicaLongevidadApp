# AGENTS.md

## Proyecto

Esta solución contiene una aplicación de escritorio desarrollada con:

- C#
- .NET 8
- WPF
- Visual Studio 2022

Antes de realizar cambios, inspecciona la estructura existente y respeta la arquitectura actual del proyecto.

## Principio general

Realiza siempre el cambio mínimo necesario para completar la tarea solicitada.

No amplíes el alcance de una tarea sin necesidad.

No realices refactorizaciones, reorganizaciones o mejoras no solicitadas simplemente porque parezcan convenientes.

## Alcance del análisis

Para cada tarea:

1. Examina primero los archivos directamente relacionados.
2. Consulta sus dependencias únicamente cuando sea necesario.
3. Evita analizar toda la solución si la tarea puede resolverse localmente.
4. No vuelvas a inspeccionar archivos que ya conoces salvo que sea necesario verificar su estado actual.

## Antes de modificar código

Determina:

- cuál es el problema o requisito;
- cuál es su causa, cuando se trate de un error;
- qué archivos están implicados;
- cuál es el cambio mínimo necesario.

Si la solución requiere modificar componentes que aparentemente no están relacionados con la petición, explica primero la razón.

## WPF

Respeta las prácticas existentes del proyecto para:

- Views
- ViewModels
- Models
- Commands
- Services
- Data Binding
- ResourceDictionary
- Styles
- Converters
- Dependency Injection
- navegación entre ventanas o vistas

No introduzcas un patrón arquitectónico nuevo sin autorización.

Si el proyecto utiliza MVVM, mantén la lógica de negocio fuera del code-behind siempre que la arquitectura existente así lo establezca.

No muevas código existente entre View, ViewModel y Services salvo que forme parte explícita de la tarea.

## XAML

Al modificar XAML:

- conserva los estilos existentes;
- reutiliza recursos existentes cuando sea posible;
- no cambies innecesariamente nombres de controles;
- no alteres bindings no relacionados;
- comprueba DataContext y Binding;
- evita duplicar estilos o recursos;
- mantén compatibilidad con WPF .NET 8.

Los cambios visuales deben limitarse a lo solicitado.

## C#

Mantén:

- Nullable Reference Types según la configuración existente;
- async/await cuando corresponda;
- manejo adecuado de excepciones;
- convenciones de nombres existentes;
- separación de responsabilidades existente.

No suprimas warnings simplemente para conseguir una compilación limpia.

Corrige su causa cuando forme parte de la tarea.

## Dependencias

No:

- instales paquetes NuGet sin autorización;
- actualices paquetes existentes sin autorización;
- cambies versiones de .NET;
- cambies TargetFramework;
- modifiques configuraciones globales de compilación sin necesidad.

Si consideras imprescindible una nueva dependencia, explica primero:

1. por qué es necesaria;
2. qué paquete propones;
3. qué alternativa existe sin añadir la dependencia.

## Datos

No realices sin autorización explícita:

- migraciones;
- cambios de esquema;
- eliminación de datos;
- cambios destructivos;
- modificaciones masivas de información.

Antes de modificar código relacionado con persistencia, identifica el mecanismo utilizado por la aplicación.

## Seguridad

Nunca escribas credenciales, contraseñas, API keys, tokens o secretos directamente en el código fuente.

No expongas secretos encontrados durante el análisis.

## Archivos generados

No modifiques manualmente archivos generados automáticamente salvo que sea estrictamente necesario y se haya solicitado.

No modifiques:

- bin/
- obj/

## Compilación

Después de realizar cambios de código, cuando sea razonablemente posible:

1. compila el proyecto afectado;
2. comprueba errores;
3. revisa warnings relacionados con los cambios;
4. corrige únicamente los problemas provocados por la tarea actual.

No conviertas una corrección local en una limpieza general de warnings de toda la solución.

## Cambios

Al finalizar una tarea, informa brevemente de:

- causa del problema o requisito implementado;
- archivos modificados;
- cambios realizados;
- resultado de compilación/pruebas;
- cualquier riesgo o cuestión pendiente.

No afirmes que algo funciona si no ha podido verificarse.

## Restricciones

No elimines funcionalidades existentes salvo petición explícita.

No cambies APIs públicas, contratos, formatos de datos o comportamiento existente salvo que la tarea lo requiera.

No realices cambios destructivos sin advertirlo previamente.

Ante una ambigüedad que pueda provocar un cambio importante o destructivo, pregunta antes de continuar.
