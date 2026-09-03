# BACKUP / RESTORE DISASTER RECOVERY REPORT
## CaseManagement — Version 1.0, Encrypted Backup (Option D)

| | |
|---|---|
| **Document Type** | Disaster-recovery drill report — real backup/restore cycles executed, no production data touched |
| **Date** | 2026-08-23 |
| **Scope** | Verification only, per explicit instruction: no new features, no backup-architecture redesign |
| **Builds on** | `BACKUP_ENCRYPTION_IMPLEMENTATION_REPORT.md` · `ENCRYPTION_ARCHITECTURE_REVIEW.md` · `RELEASE_READINESS_AUDIT.md` |
| **Test code** | `CaseManagement.Tests/DisasterRecoveryDrillTests.cs` — 12 new tests, all passing |
| **Full regression** | ✅ **491 total / 489 passed / 2 skipped (pre-existing, unrelated) / 0 failed** |

---

## 1. Test Environment

Fully isolated from production — no real installation, database, or `%AppData%` config was read or written by the drill.

- **Database isolation:** each test creates a unique temp folder (`%TEMP%\DrillDR_<guid>\`) and redirects SQLite via `AppDomain.CurrentDomain.SetData("DataDirectory", ...)`. Nothing is written to the real app's data directory.
- **File-storage isolation:** `FileHelper.SetBaseRootFolder` is pointed at a temp `Files\` subfolder before any operation; documents/photos never touch the real `CaseFiles` folder.
- **Realistic dataset:** a purpose-built seeding routine (`SeedRealisticDataset`) reproduces the app's real startup sequence (`DatabaseInitializer` → `AccountingInitializer` → `EnterpriseInitializer` → `OfflineSyncInitializer`, matching `Program.cs` exactly) and then populates every category named in the task:

| Category | What was seeded |
|---|---|
| Cases | N cases with `GlobalID`, `FormNo`, `Code`, province/district, photo path |
| Families | 1 member per case |
| Applicants | Pre-conversion applicants (not yet cases) |
| Documents | 1 real PDF-like file per case, referenced from `TblDocs` |
| Photos | 1 real JPEG-header file per case, referenced from `TblCase.PhotoPath` |
| Users | 2 accounts (SuperAdmin + Operator) with **real PBKDF2 password hashes** via `PasswordHelper.CreateHash`, so login can be genuinely re-verified after restore |
| Permissions | A manual admin override on `EntRolePermission` (`Operator` → `Case.Delete` = granted), to test whether *customized* permissions (not just defaults) survive |
| Reminders | 3 `TblReminder` rows |
| Financial | 1 accounting period, 1 fund (+4 system defaults), several `AccTransaction` rows, plus `TblAssistance` rows in the main DB |
| Settings | Org name, address, min password length via `SettingsHelper.Set` |
| Sync data | 1 `SyncOutbox` row |

Three scales were used: **small (5 cases), medium (30 cases), large (150 cases)** — scaled-down proxies for the ~1,600+ case corpus referenced in the original system audit, sized for a reasonable automated-test runtime rather than the full production volume.

---

## 2. Backup Result — ✅ Pass

| Check | Result |
|---|---|
| Encrypted backup file created | ✅ `.cmbak` file produced, `BackupEncryption.LooksLikeEncryptedBackup` confirms the magic header |
| File cannot be opened without the correct password | ✅ wrong password → `BackupEncryption.IntegrityException`, no data extracted |
| Integrity validation | ✅ `VerifyEncryptedBackup` with the correct password returns a readable `DataSet` matching the seeded row counts, entirely read-only (no live DB touched) |
| Corrupted backup detected | ✅ flipping 2 bytes mid-file (ciphertext region) is caught by the HMAC check before any decryption is attempted — same `IntegrityException`, not a silent partial read |

Both the main system backup (`BackupHelper.ExportEncryptedBackup`) and the independent accounting backup (`AccountingBackupHelper.ExportEncryptedBackup`) were exercised.

---

## 3. Restore Result

A full disaster was simulated: the working `AppDomain.DataDirectory` and file-storage root were **replaced with a brand-new, empty location** (equivalent to removing/renaming the database and deleting the application's data folder), followed by a fresh-install sequence (`DatabaseInitializer` → `AccountingInitializer` → `EnterpriseInitializer` → `OfflineSyncInitializer`), then restore.

| Category | Restored? | Notes |
|---|---|---|
| Database opens | ✅ | Restore completes and the DB is immediately queryable |
| User login | ✅ | Both restored accounts' real passwords verify successfully against the restored `PasswordHash`/`PasswordSalt`/`PasswordIterations` via `PasswordHelper.Verify` |
| Cases | ✅ | Exact row-count match |
| Families | ✅ | Exact row-count match |
| Documents (DB rows + physical files) | ✅ | Row count matches; physical PDF file confirmed present on disk at the new install's file root |
| Photos (DB rows + physical files) | ✅ | Physical JPEG file confirmed present on disk at the new install's file root |
| Financial records (accounting backup: periods/funds/transactions) | ✅ | Restored via the independent `AccountingBackupHelper` restore, confirmed **full-replace** semantics (a transaction added after the backup was taken is correctly gone after restore) |
| Financial records (`TblAssistance`, main backup) | ✅ | Exact row-count match |
| Center | ✅ | Restored via `MergeCenters` |
| **Applicants (`TblApplicant`)** | ❌ **Bug — see §6** | Exported by `ExportBackup` but never restored by `ImportBackup` in the modern (GlobalID) restore path — the path every current backup uses |
| **Settings (`TblAppSettings`)** | ❌ **Bug — see §6** | Exported but never restored by `ImportBackup` in *either* restore path |
| **Audit log (`TblAuditLog`)** | ❌ **Bug — see §6** | Same as Settings — exported, never restored |
| Reminders (`TblReminder`) | ❌ Known gap — see §6 | Never included in any backup path (not an export/import asymmetry — simply out of scope today) |
| Permission overrides (`EntRolePermission`/`EntUserPermission`) | ❌ Known gap — see §6 | Never included in any backup path; a custom permission grant is silently lost and reverts to the seeded default after a disaster restore |
| Sync data (`SyncOutbox` etc.) | ❌ Known gap — see §6 | Never included in any backup path |

---

## 4. Security Validation — ✅ Pass

| Check | Result |
|---|---|
| No plaintext backup remains on disk | ✅ after `ExportEncryptedBackup`, the backup output folder contains **only** `.cmbak` files — the intermediate raw folder and intermediate `.zip` are both deleted in `finally` blocks (verified by directory scan, not just by reading the code) |
| Temporary restore/verify files are removed | ✅ verified before/after both a successful and a failed `VerifyEncryptedBackup` call — no `CMVerify_*` artifacts left in `%TEMP%` either way |
| Wrong password fails safely | ✅ `ImportEncryptedBackup` with an incorrect password throws before writing anything; the target database is confirmed still empty afterward — no partial/corrupt state |
| Backup integrity failure is detected | ✅ same HMAC-before-decrypt mechanism as §2 — confirmed again in the full disaster-recovery context, not just the crypto unit tests |

---

## 5. Performance Results

Measured with `Stopwatch` (wall-clock), `FileInfo.Length` (backup size), and `GC.GetTotalMemory` deltas (managed-heap allocation) around each operation. All runs on the same isolated temp environment used for correctness testing above.

| Scale | Cases | Backup time | Restore time | Backup size | Managed alloc Δ (backup) | Managed alloc Δ (restore) |
|---|---|---|---|---|---|---|
| Small | 5 | ~1.0 s | ~0.9 s | ~9.5 KB | ~3.3 MB | ~1.1 MB |
| Medium | 30 | ~1.1 s | ~0.9 s | ~22.4 KB | ~2.3 MB | ~2.7 MB |
| Large | 150 | ~1.7–2.2 s | ~1.2–1.6 s | ~81.7 KB | ~2.9 MB | ~3.8 MB |

**Caveats (stated plainly, not hidden):**
- These figures are for the scaled-down proxy dataset (5/30/150 cases), not the real ~1,600+ case production corpus. Backup/restore time is dominated by the number of files copied (one photo + one doc per case in this drill); a production dataset with larger real photos/scanned PDFs will scale roughly linearly with actual file size, which this drill's small synthetic files (a few hundred bytes each) do not represent.
- `GC.GetTotalMemory` measures **managed-heap allocations**, not peak working-set/RSS; it undercounts native SQLite driver memory and OS-level file-copy buffers. `Process.WorkingSet64` was also captured (~90 MB, dominated by the test host process itself, not backup/restore specifically) and is not a reliable per-operation signal in a shared-process test run — reported for completeness, not as a precise measurement.
- Full data: `CaseManagement.Tests/DisasterRecoveryDrillTests.cs` (`Performance_*` tests); raw numbers logged during this drill are reproducible by re-running those three tests.

**Conclusion:** for the current real-world scale of this system, backup/restore time and size are not a practical concern — even the large-proxy run completes in ~2 seconds and produces an ~80 KB encrypted file. This should be re-measured with real (larger) document/photo files before being treated as representative of full production load.

---

## 6. Problems Found

Ranked by severity.

### 6.1 — 🔴 `TblApplicant` is exported but never restored (confirmed code bug)

`BackupHelper.ExportBackup` includes `TblApplicant` in every backup (`BackupHelper.cs:70`). `BackupHelper.ImportBackup`, however, only restores `TblApplicant` in the **legacy "classic" restore path** (pre-GlobalID backups) — see `BackupHelper.cs`'s `else` branch. Every backup taken by the *current* version of the app includes a `GlobalID` on `TblCase` and therefore always takes the **"smart merge" path**, which never touches `TblApplicant` at all. Confirmed directly in this drill: 6 seeded applicants were present in the backup file (verified via `VerifyEncryptedBackup`) but 0 were present after restore.

**Net effect:** the Applicants list (people not yet converted to a case — the docstring in the source explicitly says "این‌ها هیچ‌جای دیگری ذخیره نمی‌شوند", i.e. "stored nowhere else") is **silently and permanently lost** on every real-world disaster-recovery restore.

**Why not fixed here:** `TblApplicant` has no `GlobalID` and no natural unique constraint (`ApplicantID` is a bare autoincrement key). A correct merge-mode restore requires deciding what counts as "the same applicant" on re-import (e.g., full name + father name + phone?) — that is a product/design decision, not a mechanical bug fix, and this task's brief explicitly says not to redesign backup architecture and to avoid guessing. Flagging for an explicit decision before fixing.

### 6.2 — 🔴 `TblAppSettings` and `TblAuditLog` are exported but never restored (confirmed code bug)

Both tables are loaded into the export `DataSet` (`BackupHelper.cs:56-57`) but a full read of `ImportBackup` (both the GlobalID and classic branches) shows no restore logic for either table anywhere. Confirmed directly in this drill: `SettingsHelper.Get(OrgName)` returned the seeded value before the disaster and an empty string after restore.

**Net effect:** after a disaster recovery restore, the organization's settings (name, address, password policy, backup schedule, etc.) revert to installation defaults, and the audit trail from before the disaster is gone — even though both were captured in the backup file.

**Why not fixed here:** unlike `TblApplicant`, `TblAppSettings` *does* have an obvious natural key (`SettingKey`), so a mechanical fix (`INSERT OR REPLACE` keyed by `SettingKey`, similar to the existing `MergeUsers`/`MergeLookup` pattern in the same file) is straightforward. However, whether a restore *should* overwrite an administrator's current live settings with old backed-up ones is a behavioral/policy question, not a pure bug fix — this needs a decision, not a guess, per this task's "no architecture changes" instruction.

### 6.3 — 🟡 Three data categories are entirely outside current backup scope (not an export/import asymmetry — simply never captured)

Confirmed by direct source inspection (not assumption) that these are absent from **both** `BackupHelper` and `AccountingBackupHelper`:

1. **`TblReminder`** (Reminders feature)
2. **`EntPermission` / `EntRolePermission` / `EntUserPermission`** (the fine-grained permission matrix) — a custom admin permission grant made before the disaster (`Operator` → `Case.Delete`) was confirmed gone after restore, reverted to the seeded default
3. **All `Sync*` tables** (`SyncOutbox`, `SyncState`, `SyncConflict`, `SyncBaseline`, `SyncFile`, `SyncFileDownload`)

This matches — and is now empirically confirmed, not just inferred — what `BACKUP_ENCRYPTION_IMPLEMENTATION_REPORT.md` §6 already flagged as an open scope question. Per this task's explicit "do not redesign backup architecture" instruction, these are reported, not patched.

---

## 7. Fixes Applied

**None applied to production code during this drill**, by design. The task's brief was verification-only ("do not add new features," "do not redesign backup architecture"), and all defects found (§6.1, §6.2) require a design decision (dedup key for applicants; overwrite-live-settings policy for settings/audit) rather than a pure mechanical fix — consistent with this project's standing rule to never guess or invent requirements.

Two **test-only** issues were found and fixed while building the drill itself (not production code):
- SQL-Server-style `N'...'` string literals in the test's own seed SQL (invalid SQLite syntax) — corrected to plain string literals.
- An incorrect test assumption that `AccFund` would contain exactly 1 row after seeding — `AccountingInitializer` seeds 4 default funds on every fresh install, so the correct expectation is 5 (4 default + 1 seeded). Corrected the assertion; not a product bug.

---

## 8. Remaining Risks

1. **`TblApplicant` data loss on every real restore (§6.1)** — highest-priority open item. Needs a design decision on dedup semantics before it can be fixed.
2. **Settings and audit log are not recoverable via restore (§6.2)** — needs a policy decision (should restore overwrite live settings?) before it can be fixed.
3. **Reminders, permission overrides, and sync state are not backed up at all (§6.3)** — a known, previously-documented scope limitation, now empirically confirmed rather than assumed.
4. **Performance figures are based on a small synthetic-file proxy dataset**, not real production-sized documents/photos — re-measure before treating as representative (§5).
5. All risks already carried forward from `BACKUP_ENCRYPTION_IMPLEMENTATION_REPORT.md` §6 remain unchanged by this drill (no password-recovery mechanism, live DB/files still unencrypted by design, DPAPI `LocalMachine` scope tradeoff, no automated UI-dialog test, pre-upgrade plaintext backups not retroactively encrypted).

---

## 9. Production Readiness Decision

**Conditional — not fully ready without a decision on §6.1 and §6.2.**

What works and is verified end-to-end with real encrypted backup files, a real simulated disaster, and a real restore onto a fresh install:
- Cases, families, documents (with physical files), photos (with physical files), users (with working login), centers, and both financial data paths (main-DB assistance + independent accounting backup) all restore correctly and completely.
- Every security property required by the task (no plaintext leftover, temp-file cleanup, wrong-password fail-safe, tamper/corruption detection) holds under real testing, not just unit-level assumption.
- Performance is not a concern at the scale tested.

What blocks an unconditional "yes":
- **Applicants, Settings, and the Audit Log are silently lost on every real disaster-recovery restore** (§6.1, §6.2) — this is worse than the previously-known Reminders/Permissions/Sync gaps because those were always known to be out of scope, whereas these two are backed up (giving false confidence that they're protected) and then quietly dropped on restore.

**Recommendation:** before relying on this for a real charity center's disaster recovery, get an explicit decision on (a) how to dedup `TblApplicant` on merge-mode restore, and (b) whether Settings/Audit Log restore should overwrite live values — then apply the two fixes and re-run this drill. The case/family/document/photo/user/financial recovery path itself — the core of what "disaster recovery" means for this system — is solid and can be relied on today.

---

*Full solution regression: 491 total / 489 passed / 2 skipped (pre-existing, unrelated to this work) / 0 failed, including all 12 new disaster-recovery drill tests.*
