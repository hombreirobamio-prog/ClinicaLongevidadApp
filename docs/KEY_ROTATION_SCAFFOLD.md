Key rotation scaffold (feature/key-rotation)
------------------------------------------

What was added
- A minimal scaffold service: `Services/KeyRotation/KeyRotationService.cs` — placeholder methods for HMAC and encryption key rotation.
- A tools scaffold: `tools/RotateKeys` console project with a minimal `Program.cs` and project file.

Purpose
- Provide a safe place to implement key rotation, versioning and Key Vault integration without touching the main app yet.

Next steps
- Wire `KeyRotationService` to `IKeyProvider` and implement secure persistence (Key Vault or protected local store).
- Implement atomic rotation workflows and backfill/re-encryption strategies.
- Add tests for rotation metadata and a CI job to validate rotation tools (dry-run/preview mode).

Notes
- This is intentionally a scaffold: the methods currently throw `NotImplementedException` so accidental use is visible during development.
