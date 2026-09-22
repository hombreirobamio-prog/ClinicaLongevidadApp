# Changelog

## [audit-rewrite-8684b20] - Merged

### Added
- Reescritura de `AuditoriaView` y `AuditoriaViewModelV2` con mejoras de UX y controles de backup (Programar, Cancelar, Copia ahora, Restaurar, Probar 1 min).

### Changed
- UI: ajustes de layout y estilos para evitar solapamientos y texto recortado en `AuditoriaView`.

### Removed
- `ViewModels/AuditoriaViewModel.cs` (legacy) reemplazado por `AuditoriaViewModelV2`.

Tag: `audit-rewrite-8684b20`

---

## Unreleased

### Added
- Auditoría reforzada: EventId, PrevHash/Hash, Signature (HMAC), cifrado de `Detalles` (AES).
- `IKeyProvider` y `AzureKeyVaultKeyProvider` / `LocalKeyProvider`.
- Exporter a Blob (`BlobAuditExporter`) y forwarder a webhook (`WebhookForwarder`).
- `AuditoriaIntegrityWorker` para verificación periódica de integridad.
- `KeyRotationService` y `IKeyRotationProvider` (LocalKeyRotationProvider scaffold).
- Integración en `App.xaml.cs` para inicialización y worker.
- Tests unitarios para auditoría e integridad.
- `RUNBOOK_AUDITORIA.md` y `AUDIT_SETUP.md` con instrucciones operativas.

### Changed
- Refactor `PanelRecepcionViewModel` para inyección de servicios (IPacienteService, ICitaService, IAuditoriaService).
- Añadidos métodos async y mejoras en manejo de UI/Dispatcher.
- Workflow CI actualizado para publicar artefactos de cobertura.

### Fixed
- Deterministic audit registration and integrity checks.

---

Please review before release; see `RUNBOOK_AUDITORIA.md` for deployment steps.
