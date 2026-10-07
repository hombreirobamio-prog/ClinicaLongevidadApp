# Borrador de custodia de evidencias de auditoría

**Estado: pendiente de aprobación.** Este documento prepara la decisión operativa de custodia; no modifica permisos, retenciones, copias ni servicios de Azure.

## Finalidad

Conservar evidencias suficientes para comprobar las pruebas, las copias y las recuperaciones realizadas, limitando el acceso a datos clínicos y secretos.

## Clasificación de evidencias

| Clase | Ejemplos | Datos clínicos | Tratamiento propuesto |
| --- | --- | --- | --- |
| Técnica | TRX, ZIP `technical-tests-only`, manifiestos y resultados de CI | No | Puede conservarse en el repositorio de evidencias con acceso de auditoría. |
| Recuperación sin datos | `recovery-result.json`, manifiesto y ZIP de evidencia | No | Puede custodiarse de forma independiente tras comprobar su SHA-256. |
| Restringida | Informes de integridad y diagnósticos | Puede contener identificadores técnicos | Mantener bajo la raíz central de artefactos, con acceso limitado a Administración/auditoría. |
| Clínica | Bases SQLite, backups `.db` y sus acompañantes | Sí | No exportar a USB, correo, repositorios ni servicios externos sin autorización documentada y medidas de protección adecuadas. |
| Secreto | Claves HMAC, claves de cifrado, tokens o contraseñas | No, pero son confidenciales | No incluir en informes, evidencias, ZIPs ni documentación. Custodiar únicamente mediante el proveedor aprobado. |

## Ubicaciones actuales comprobadas

- Artefactos locales activos: `%LOCALAPPDATA%\ClinicaLongevidadAppArtifacts`.
- Copias autenticadas: subcarpeta `backups`; requieren `.sha256`, `.hmac` y `.hmac.ver`.
- Evidencias técnicas, ensayos y controles: subcarpeta `audit_artifacts`.
- Anclajes externos: contenedor privado `audit-anchors` de Azure Blob Storage. La retención configurada es de 30 días y permanece desbloqueada; no se considera retención definitiva.
- Copia externa ya verificada: el paquete de recuperación sin base SQLite ni datos clínicos se trasladó a USB el 07/10/2026. La custodia física posterior debe quedar registrada por el responsable designado.

## Procedimiento de entrega y verificación

1. Clasificar el archivo antes de copiarlo o compartirlo.
2. Para evidencias técnicas o de recuperación sin datos, calcular y conservar SHA-256 junto al archivo.
3. Verificar el SHA-256 en el destino antes de registrar la entrega.
4. Registrar fecha, evidencia, hash, origen, destino, persona que entrega y persona que recibe.
5. Para una copia clínica, exigir además autorización específica, destino protegido, cifrado y control de acceso antes de moverla.
6. No registrar valores de HMAC, claves, contraseñas ni tokens en el registro de custodia.

El [registro de evidencias](EVIDENCE_REGISTER.md) contiene el inventario inicial y debe actualizarse al conservar una evidencia nueva.

## Registro mínimo de custodia

| Campo | Estado actual |
| --- | --- |
| Responsable de custodia | Inés Hombreiro Pazos, Doctora y responsable de Administración de la clínica. |
| Sustituto o contacto alternativo | Pendiente de designación. |
| Responsable de aprobación | Propuesta: Inés Hombreiro Pazos. Pendiente de aprobación formal. |
| Fecha de aprobación de la política | Pendiente. |
| Retención de evidencias técnicas | Pendiente de aprobación. |
| Retención de backups clínicos | Pendiente de aprobación conforme a requisitos aplicables. |
| Retención definitiva del anclaje de Azure | Pendiente de aprobación y bloqueo explícito. |

## Revisión periódica propuesta

- Tras cada ensayo de recuperación, comprobar que su evidencia se conserva en la ubicación prevista y que su hash coincide.
- Tras cada ejecución de la copia diaria, conservar el JSON generado por `verify_daily_backup_task.ps1` cuando corresponda a una comprobación operativa.
- Revisar trimestralmente las personas autorizadas y la disponibilidad de las ubicaciones de custodia.
- Registrar inmediatamente cualquier pérdida de medio, discrepancia de hash o acceso no autorizado; no sustituir ni regenerar una evidencia afectada.

## Para aprobar este borrador

La persona responsable debe completar los campos pendientes, fijar periodos de retención compatibles con las obligaciones de la clínica, decidir la retención definitiva del anclaje externo y firmar o registrar la aprobación según el procedimiento interno aplicable.
