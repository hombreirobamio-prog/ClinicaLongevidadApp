# Registro de evidencias de auditoría

**Estado: inventario inicial.** Custodia responsable: Inés Hombreiro Pazos. Este registro no contiene bases SQLite, datos clínicos, HMAC ni claves.

| Fecha | Evidencia | Verificación | Ubicación de custodia | Estado |
| --- | --- | --- | --- | --- |
| 07/10/2026 | Paquete de evidencia del ensayo aislado de recuperación | SHA-256 `06AFC9038F85CE10B16B4395E45C44A16957E0CD6AD0744D9146D419C72DF337` | Local: `audit_artifacts\recovery-drill-20261007_195518`; copia externa: `ESD-USB (E:)\ClinicaLongevidadApp\AuditEvidence\2026-10-07` | Verificado en ambos destinos; no contiene base clínica. |
| 07/10/2026 | Paquete técnico firmado de CI, ejecución `37676469997` | SHA-256 `40FB7D5E43234C4AC9B98674D2C9B7383629295EC5188D933B47E1FE3A0B5D32` | Local: `audit_artifacts\ci-20261007-run-37676469997` | Verificado; alcance `technical-tests-only`. |
| 07/10/2026 | Paquete técnico firmado de CI, ejecución `37687005598` | SHA-256 `D9BE58419BC25F00D41502C2379443987F5C8C3816242A313DCF468AD885BD0D` | Local: `audit_artifacts\ci-20261007-run-37687005598` | Verificado; incluye resultado de 200 pruebas y artefactos sintéticos. |
| 08/10/2026 | Paquete técnico firmado de CI, ejecución `37698849282` | SHA-256 `495196F4CC5310929265C8A0F50BD7EFF5C11DE472369B0F3CBE56FDBA67C38A` | Local: `audit_artifacts\ci-20261008-run-37698849282` | Verificado; 203/203 pruebas aprobadas y 13 informes sintéticos. |
| 07/10/2026 | Punto de control externo H01 | Id de auditoría `2170`; objeto `audit/anchors/20261007T1330372053720Z_00000000000000002170_2808fcf40f40.json` | Azure Blob Storage privado: `audit-anchors` | Creado y verificado; retención de 30 días aún desbloqueada. |
| 08/10/2026 | Supervisión de tarea diaria de copias | Resultado de tarea `0`; SHA-256 de copia coincidente; acompañantes presentes | Se conservará el JSON generado por `verify_daily_backup_task.ps1` en `audit_artifacts` tras la ejecución automática de las 19:17 | Comprobación manual correcta; evidencia automática pendiente. |

## Alta de una nueva evidencia

1. Clasificarla conforme al [borrador de custodia](EVIDENCE_CUSTODY_POLICY_DRAFT.md).
2. Anotar fecha, descripción, ubicación y método de verificación.
3. Incluir un hash SHA-256 cuando el archivo pueda salir de su ubicación local.
4. No anotar secretos ni copiar bases clínicas a este registro.
5. Indicar de forma expresa si la evidencia está pendiente, verificada o rechazada.

## Pendientes del registro

- Añadir el JSON de salud de la primera copia automática cuando se ejecute a las 19:17 del 08/10/2026.
- La custodia es provisionalmente unipersonal a cargo de Inés Hombreiro Pazos. Registrar un sustituto, fecha de aprobación y periodos de retención cuando se revise la política.
- Actualizar el estado del anclaje H01 cuando se apruebe y bloquee su retención definitiva.
