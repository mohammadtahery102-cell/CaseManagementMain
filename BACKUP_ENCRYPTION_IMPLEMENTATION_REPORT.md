# BACKUP ENCRYPTION IMPLEMENTATION REPORT
## CaseManagement — Encrypted Backup/Restore (Version 1.0, Option D)

| | |
|---|---|
| **Document Type** | Implementation report — code was written and tested |
| **Date** | 2026-08-23 |
| **Implements** | `ENCRYPTION_ARCHITECTURE_REVIEW.md`'s recommended Option D (encrypted backups; live database and document files left unencrypted — see that document for the full options analysis and rationale) |
| **Final build result** | ✅ 0 errors, main + test projects |
| **Final test result** | ✅ **459 total / 457 passed / 2 skipped (pre-existing) / 0 failed** |

**Scope note:** an unrelated "AI Assistant" module is being developed in parallel in this same repository (`AI/`, `FrmAiAssistant.cs`, `Helpers/AiInitializer.cs`, `AI_ASSISTANT_*.md`, 7 `Ai*Tests.cs` files) — not part of this work, not modified by it, excluded from the lists below. It explains why the test count grew beyond what this feature alone added.

---

## 1. Modified Files

| File | Change |
|---|---|
| `Helpers/BackupEncryption.cs` **(new)** | AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC) file encryption engine. PBKDF2 key derivation (100,000 iterations, matching `PasswordHelper`'s existing convention) split into separate AES/HMAC keys. Fail-safe integrity check before any decryption is attempted. |
| `Helpers/FrmPasswordPrompt.cs` **(new)** | Reusable password-entry dialog, styled like the existing `FrmChangePassword`. Two modes: single password (restore/verify) and password+confirmation (new backup). |
| `Helpers/BackupHelper.cs` | Added `ExportEncryptedBackup`, `ImportEncryptedBackup`, `VerifyEncryptedBackup` — wrap the existing (unmodified) `ExportBackup`/`ImportBackup` with zip + encrypt / decrypt + unzip. All intermediate plaintext artifacts are deleted in `finally` blocks regardless of success or failure. |
| `Helpers/AccountingBackupHelper.cs` | Same wrapper pattern (`ExportEncryptedBackup`/`ImportEncryptedBackup`). `ImportBackup` gained an overload accepting a password so its internal automatic pre-restore safety snapshot is also encrypted, not left as plaintext. |
| `Helpers/AutoBackupService.cs` | Scheduled backups now call `ExportEncryptedBackup` using a DPAPI-protected stored passphrase. **If no passphrase is configured, the backup is skipped and logged — it never falls back to writing a plaintext archive.** `PruneOldBackups` updated for the new single-file (not folder) output. Added `ProtectPassword`/`UnprotectPassword` (DPAPI, `LocalMachine` scope), `IsAutoBackupPasswordConfigured`, `SetAutoBackupPassword`. |
| `Helpers/SettingsHelper.cs` | Added `AutoBackupPasswordProtected` setting key. |
| `FrmSettings.cs` | Backup-now/restore/verify handlers rewritten for the encrypted flow (password prompt, `OpenFileDialog` for the new `.cmbak` format). Added a dedicated **"Restore Legacy Backup (unencrypted)"** button that calls the original, completely unmodified `ImportBackup(folder)` path, preserving the ability to restore pre-upgrade backups. Added an auto-backup-password configuration section to the Backup tab. |
| `Accounting/FrmAccounting.cs` | Same treatment for the accounting-specific backup screen: encrypted export/import + a separate "legacy restore" button for pre-upgrade accounting backups. |
| `CaseManagement.csproj` | Registered the 2 new files above. **Also fixed two pre-existing build blockers unrelated to this feature**, encountered while trying to get a clean baseline to work from: a missing `<Compile Include>` for `Helpers/AiInitializer.cs` (file existed on disk, referenced by `Program.cs`, but wasn't in the project — the AI module's own build was broken before this session touched anything) and a missing `<Reference Include="System.Security" />` needed for `ProtectedData`/`DataProtectionScope` (DPAPI). |

## 2. Database Changes

**None.** No table, column, or schema change of any kind. The only persistence-layer change is a new *key* in the existing `TblAppSettings` key-value table (`AutoBackupPasswordProtected`, storing a DPAPI-encrypted Base64 blob) — the same mechanism every other setting already uses, added the same way (`AddPermission`-style, purely additive).

## 3. Tests Added (27 new, all passing)

| File | Tests | Covers |
|---|---|---|
| `BackupEncryptionTests.cs` | 11 | Round-trip correctness, wrong-password rejection, tampered-ciphertext detection, non-encrypted-file rejection, salt/IV randomness (two encryptions of the same file differ), format-sniffing (`LooksLikeEncryptedBackup`), `VerifyIntegrity` leaves no temp files (success and failure paths), empty-password rejection, non-block-aligned binary content |
| `BackupRestoreEncryptedTests.cs` | 5 | **Full disaster-recovery round trip**: create backup on one install, restore onto a completely separate fresh install, data matches exactly; wrong password fails safely with the target database left untouched; no temporary artifacts left behind on export or import; both operations appear in the audit trail |
| `AccountingBackupEncryptedTests.cs` | 4 | Same guarantees for the independent accounting backup path, including its full-replace semantics (restored state matches the backup, not later changes made after it) |
| `AutoBackupServiceTests.cs` | 7 | DPAPI protect/unprotect round-trip, protected value doesn't leak the plaintext, garbage input handled without throwing, configured-state tracking, **and the core security guarantee: `RunDailyBackupIfDue` creates zero backup files when no password is configured, and exactly one correctly-encrypted file when one is** |

## 4. Test Results

- Targeted runs (crypto core, backup/restore round trip, accounting backup, auto-backup service): **26/26 passed** in isolation, **26/26 passed** run together.
- Full solution regression: **459 total / 457 passed / 2 skipped / 0 failed.**

**Two real bugs were caught and fixed during testing** (not left for later):
1. `AccountingTestBase` (shared test fixture) didn't initialize the Enterprise permission tables — a pre-existing gap from earlier permission-migration work, unrelated to encryption but blocking these tests; fixed by adding `EnterpriseInitializer.EnsureEnterpriseObjects()` to its setup.
2. `SettingsHelper` has a process-wide static cache that's only loaded once per test-host process. `AutoBackupService.SetAutoBackupPassword` originally wrote via a private raw-SQL path that bypassed this cache, so a value set and immediately re-read within the same call appeared not to exist when tests ran alongside other test classes. Fixed by routing through `SettingsHelper.Set` consistently, and added the same `SettingsHelper.ClearCache()` call in test setup that several other existing test files in this project already carry for exactly this reason.

Also caught mid-investigation and fixed: a test-data bug in my own `BackupRestoreEncryptedTests.cs` (bound a non-numeric string to `TblCase.FormNo`, an `INTEGER UNIQUE` column — the ADO driver silently coerced it to `0` for every row, producing a real but self-inflicted `UNIQUE` violation). Verified this was not a `BackupHelper` defect by reproducing it against the original, completely unmodified `ExportBackup`/`ImportBackup` methods before concluding it was test data, not production code.

## 5. Security Impact

1. **Closes the highest-priority gap identified in `RELEASE_READINESS_AUDIT.md` §5 and `ENCRYPTION_ARCHITECTURE_REVIEW.md`**: backup archives — "the highest-value single-file exfiltration target" per the original system audit — are now AES-256 encrypted, both for manual and scheduled backups, for both the main system backup and the independent accounting backup.
2. **Integrity is verified before any decryption is attempted** (HMAC-SHA256 over salt+IV+ciphertext, checked first) — a corrupted or tampered backup file, or a wrong password, is rejected cleanly with no partial processing and no data written.
3. **No plaintext ever touches disk outside the final encrypted file** — every intermediate plaintext folder and zip is deleted in a `finally` block, verified by dedicated tests, on both success and failure.
4. **Scheduled (unattended) backups cannot silently degrade to plaintext.** If no auto-backup passphrase is configured, `AutoBackupService` skips the backup and logs why, rather than falling back to an unencrypted archive. This is a deliberate, tested behavior change from the prior version, where auto-backup always ran unencrypted.
5. **Both success and failure of every export/import operation are written to `TblAuditLog`**, satisfying the logging requirement and matching this codebase's existing audit conventions.
6. **The auto-backup passphrase is never stored in plaintext** — DPAPI (`LocalMachine` scope) protects it at rest in `TblAppSettings`, consistent with the architecture review's reasoning for why `LocalMachine` (not `CurrentUser`) was chosen for this specific use case.
7. **Backward compatibility is preserved deliberately, not accidentally**: the original unencrypted `ExportBackup`/`ImportBackup` methods are completely untouched and remain reachable through explicit, clearly-labeled "legacy" buttons in both `FrmSettings` and `FrmAccounting`, so backups taken before this upgrade remain restorable.

## 6. Remaining Risks

1. **Password recovery has no built-in mechanism.** This is inherent to any encryption scheme, and was flagged in the architecture review as a prerequisite: a lost backup password means that specific backup is permanently unrecoverable. This needs an organizational policy (e.g., a documented recovery/escrow procedure for the SuperAdmin) — not something code can solve.
2. **The live database and document/photo files remain unencrypted**, exactly as Option D intended and scoped. `CM-01`/`SEC-003` from the original system audit remain open; this work only closes the backup-file exposure, not a stolen-live-machine scenario.
3. **DPAPI `LocalMachine` scope means the stored auto-backup passphrase is decryptable by any process on that machine** — appropriate for protecting against a stolen backup *file*, not against a compromised machine. This tradeoff was made explicitly (see `AutoBackupService.ProtectPassword`'s inline rationale) to survive Windows-account changes/service execution, and is consistent with the architecture review's analysis of DPAPI's limits.
4. **No automated test exercises the real UI dialogs** (`FrmPasswordPrompt`, the `FrmSettings`/`FrmAccounting` button handlers) — consistent with this codebase's established testing boundary (WinForms event wiring is verified by code review and manual testing, not unit tests; the underlying logic each handler calls is fully tested). A manual pass through the Backup tab and Accounting Backup tab is recommended before relying on this in production.
5. **Existing plaintext backups already on disk from before this upgrade are not retroactively encrypted** — by design (out of scope for code to fix); recommend an operational cleanup pass (re-backup with the new encrypted flow, then securely delete the old plaintext archives) as a follow-up administrative task, not a code task.
6. **This has not yet been through a real end-to-end disaster-recovery drill on production-shaped data** — the automated tests prove correctness on synthetic data; `RELEASE_READINESS_AUDIT.md`'s recommendation to run an actual restore rehearsal before wider deployment still stands.

---

*Full build and full test suite both verified clean before this report was written, per the explicit requirement not to report until everything passes.*
