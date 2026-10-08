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

Se instaló una tarea diaria independiente de la interfaz a las 19:17, con ejecución diferida y permitida con batería. La primera ejecución bajo demanda creó una copia autenticada verificada. El RPO sigue condicionado a que Windows esté iniciado y el usuario configurado haya iniciado sesión, porque las claves actuales son locales al perfil. Ejecutar sin inicio de sesión requeriría una identidad de servicio o credenciales custodiadas.

## Criterios de aceptación futuros

1. Confirmar durante varios días que la tarea crea una copia automática a las 19:17 aun con la interfaz cerrada.
2. Conservar la evidencia de cada copia y alertar ante un fallo.
3. Medir el tiempo total de recuperación en un ensayo controlado y comprobar que no supera cuatro horas.
4. Revisar los objetivos con el responsable de la clínica al cambiar horarios, volumen de datos o criticidad.
