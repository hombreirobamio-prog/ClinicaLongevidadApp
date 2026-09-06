Auditoría segura - Configuración rápida

Resumen:
La aplicación incluye auditoría reforzada con:
- Hash-chain (PrevHash/Hash)
- Firma HMAC (Signature)
- Cifrado AES de detalles (opcional)
- Export a Blob y forwarding a webhook (opcional)

Variables de entorno / configuración:
- KEYVAULT_URI: https://<mi-keyvault>.vault.azure.net/
- AUDIT_HMAC_SECRET_NAME
- AUDIT_ENC_SECRET_NAME
- STORAGE_CONNECTION_STRING o STORAGE_ACCOUNT_URI
- AUDIT_BLOB_CONTAINER (optional, default 'audit-events')
- AUDIT_WEBHOOK_URL (optional)

Dev fallback:
- AUDIT_HMAC_KEY (base64)
- AUDIT_ENC_KEY (base64)

Runbook: ver RUNBOOK_AUDITORIA.md
