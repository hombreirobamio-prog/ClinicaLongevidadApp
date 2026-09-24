Backfill plan (draft)

Objective: populate missing Hash/Signature for historical audit rows by appending new signed events referencing originals.

Steps:
1. Make a copy of production DB and work only on the copy.
2. Run VerifyIntegrity() to identify problematic rows (IDs). Export list.
3. For each problematic row:
   - Read original Detalles (plain or legacy) and metadata.
   - Create a new AuditoriaEvento with Accion="Backfill.Reencrypt", Tipo="Backfill", Detalles including BackfilledFromId and OriginalDetalles.
   - Use AuditoriaService.RegistrarEvento(...) to append the new event (this computes hash/signature with current keys).
4. After batch, run VerifyIntegrity() again on the copy to ensure new rows are valid.
5. Prepare migration script/plan to optionally mark original rows as suspect (e.g., add a column "Suspect"=1) in a controlled way.

Notes:
- Do NOT modify existing rows; append-only is required.
- Keep artifact logs and a mapping between original IDs and backfill event IDs.
- Test on staging before any production changes.

Tools: consider implementing a small tool using Services.KeyRotation.BackfillService.ApplyBackfillAsync(dryRun:false) on a copy DB.
