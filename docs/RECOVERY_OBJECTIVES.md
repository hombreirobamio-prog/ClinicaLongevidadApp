# Objetivos de recuperación

Aprobado operativamente el 07/10/2026 para la fase actual de Clínica Longevidad.

## Objetivos provisionales

| Objetivo | Valor | Significado |
| --- | --- | --- |
| RTO | 4 horas | La aplicación debe recuperarse y quedar disponible en un máximo de cuatro horas tras un incidente. |
| RPO | 24 horas | Tras un incidente se acepta perder, como máximo, los cambios realizados desde la última copia válida de las 24 horas anteriores. |

Estos valores son objetivos operativos provisionales. Deben revisarse si la clínica necesita conservar las altas, citas o cambios del mismo día con menor pérdida tolerable.

## Evidencia disponible

- Copias con SHA-256, HMAC y versión de clave.
- Ensayo aislado correcto de restauración con comprobación `integrity_check=ok`.
- Ensayo correcto de rechazo de evidencia HMAC manipulada, sin modificar el destino de prueba.
- Paquete de evidencia sin datos clínicos copiado y verificado en un USB independiente.

## Condición aún pendiente para cumplir el RPO

La programación diaria actual se guarda y se vuelve a crear cuando se abre la vista de Auditoría, pero el temporizador se ejecuta dentro del proceso de la aplicación. Si la aplicación está cerrada o el equipo apagado cuando llegue la hora, no genera la copia programada.

Para poder afirmar que el RPO de 24 horas se cumple de forma continua, hace falta un ejecutor independiente de la interfaz, por ejemplo una tarea programada de Windows que cree y verifique la copia diaria, o un servicio equivalente. Su instalación y la frecuencia concreta deben validarse antes de declararlo operativo.

## Criterios de aceptación futuros

1. Ejecutar una copia automática diaria aun con la interfaz cerrada.
2. Conservar la evidencia de cada copia y alertar ante un fallo.
3. Medir el tiempo total de recuperación en un ensayo controlado y comprobar que no supera cuatro horas.
4. Revisar los objetivos con el responsable de la clínica al cambiar horarios, volumen de datos o criticidad.