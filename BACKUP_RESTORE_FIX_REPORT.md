# BACKUP_RESTORE_FIX_REPORT.md

**Phases 3 & 4 — repair and verification.**
Companion to `BACKUP_RESTORE_AUDIT.md` (Phase 1 audit).

Date: 29 August 2026
Approved scope: **Tier 1 + Tier 3**, with `TblAuditLog` policy = *restore only when the target table is empty*.
Build: ✅ succeeds. Tests: ✅ **87 passed, 0 failed** across the affected surface.

---

## 1. Modified files

Five files. No schema changes, no renames, no new dependencies, no structural changes.

### Production code (2 files)

| File | Change | Lines |
|---|---|---|
| `Helpers/BackupHelper.cs` | `MergeAppSettings` call added to the shared prelude (runs in both restore modes) | +14 |
| | Four `RestoreWholeTableIfEmpty` calls added to the **merge** branch | +26 |
| | `RestoreWholeTable(..., "TblAuditLog")` added to the **classic** branch | +7 |
| | New method `MergeAppSettings` — `INSERT OR IGNORE` on `SettingKey`, dynamic columns | +37 |
| | New method `RestoreWholeTableIfEmpty` — the emptiness-guarded primitive | +48 |
| `Helpers/AutoBackupService.cs` | Accounting backup now also runs on the automatic schedule (guarded by its own try/catch) | +19 |
| | `PruneOldBackups` split into `PruneByPrefix`, applied to both file prefixes | +12 |

### Tests (3 changes across 2 files)

| File | Change |
|---|---|
| `DisasterRecoveryDrillTests.cs` | Inverted the `TblApplicant` assertion — was asserting the bug (`0`), now asserts full restore |
| | Inverted the `TblAppSettings` assertion — was asserting `""`, now asserts both seeded values; added a `TblAuditLog` assertion |
| `AutoBackupServiceTests.cs` | `RunDailyBackupIfDue_WithPassword_...` now expects **2** encrypted files (main + accounting), and asserts each prefix exists |
| | `SetUp` now calls `AccountingInitializer.EnsureAccountingObjects()`, matching the real startup order in `Program.cs:47–50` |

---

## 2. Fixed tables

### C-2 — exported but never restored

| Table | Rows at risk | Before | After |
|---|---|---|---|
| `TblAppSettings` | 22 | never restored, in either mode | restored in **both** modes via `MergeAppSettings` (`INSERT OR IGNORE` on `SettingKey`) |
| `TblAuditLog` | 3,597 | never restored, in either mode | merge mode: restored when target empty · classic mode: full restore |

### C-3 — merge mode skipped what classic mode restored

| Table | Before | After |
|---|---|---|
| `TblApplicant` | classic only (a dead path) | **both** modes |
| `TblApplicantStatusHistory` | classic only | **both** modes |
| `EntRecordVersion` | classic only | **both** modes |

### C-4 — automatic backups had no financial data

| Scope | Before | After |
|---|---|---|
| Scheduled/automatic backup | 17 tables, **zero** `Acc*` | 17 tables **+ all 11 `Acc*`** |
| Retention pruning | `CaseManagementBackup_*` only | both prefixes, `keepCount` each |

### Coverage: before → after

| Restore path | Tables exported | Tables restored (before) | Tables restored (after) |
|---|---|---|---|
| **Merge** (every modern backup) | 17 | 12 | **17 — full parity** |
| **Classic** (legacy files) | 17 | 15 | **17 — full parity** |
| **Automatic backup** | 17 | — | **28 (17 + 11 `Acc*`)** |

**Export/restore asymmetry in `BackupHelper` is now zero.** Every table written into the backup is read back out.

---

## 3. The design decision behind the fix

`TblApplicant`, `TblApplicantStatusHistory`, `EntRecordVersion` and `TblAuditLog` have **no `GlobalID` and no natural unique key** — `DatabaseInitializer.cs:422–445` adds `GlobalID` only to `TblCase`, `TblFamily`, `TblDocs` and `TblAssistance`. A naive insert would duplicate every row each time a backup was merged. That is precisely why the previous session declined to fix it and documented it instead.

`RestoreWholeTableIfEmpty` resolves this without inventing a dedupe key, by letting the **state of the target decide**:

| Scenario | Target table | Behaviour |
|---|---|---|
| Disaster recovery / fresh install | empty | restore fully — *this is the case that silently lost data* |
| Merging another office's backup into a live DB | populated | skip untouched — *identical to today's behaviour* |

The change therefore only takes effect in the situation that was previously broken. Merge semantics are preserved exactly, and no duplicate row can be produced.

For `TblAppSettings` a real natural key exists (`SettingKey`), so it uses `INSERT OR IGNORE` — the same pattern already proven by `MergeUsers` and `MergeLookup`. **Live local settings are never overwritten**; only missing keys are filled, which is exactly the fresh-install case.

All new code reuses existing primitives (`RestoreWholeTable`, `InsertTable`, the `MergeLookup` pattern) and keeps the file's existing defensive style: table-exists check, per-table try/catch, and `Debug.WriteLine` on skip.

---

## 4. Test results

### Backup / restore suite — before vs after

| | Before | After |
|---|---|---|
| `DisasterRecoveryDrillTests` + `AutoBackupServiceTests` + `BackupRestoreEncryptedTests` + `BackupEncryptionTests` + `AccountingBackupEncryptedTests` | 37 passed, **1 failed** | **38 passed, 0 failed** |

```
Test Run Successful.
Total tests: 38
     Passed: 38
 Total time: 5.0170 Minutes
```

### Regression batches

| Suite | Result |
|---|---|
| `ApplicantDocumentTests`, `RecordHistoryWiringTests` | **12 / 12 passed** |
| `DevCenterSafetyTests`, `FrmSettingsDeleteCasesTests` (includes `Delete_TakesRealBackupFirst_ThenCascadesToFamilyAndDocs`, which exercises `BackupHelper` directly) | **37 / 37 passed** |
| **Total verified** | **87 passed, 0 failed** |

### The one intermediate failure, and why it was a fixture defect not a product defect

`RunDailyBackupIfDue_WithPassword_CreatesEncryptedBackupFile` failed with `no such table: AccPeriod`. The fixture called only `DatabaseInitializer.EnsureDatabaseObjects()`, simulating an installation that cannot exist in production — `Program.cs:47–50` always initialises accounting immediately after the core schema.

The product behaved **correctly** under that impossible condition: the accounting backup failed, the error was logged, and the main backup completed intact — which is exactly what the guarded try/catch is for. The fix was to make the fixture match real startup. This is worth stating plainly: the failing test proved the error handling works, rather than revealing a bug.

### Assertions deliberately inverted (expected, not regressions)

Three assertions previously encoded the broken behaviour as "known limitations". They now assert correct behaviour:

| Location | Was | Now |
|---|---|---|
| `DisasterRecoveryDrillTests` ~line 476 | `Assert.AreEqual(0, CountRows("TblApplicant"))` | `Assert.AreEqual(summary.ApplicantCount, ...)` |
| ~line 505 | `Assert.AreEqual("", SettingsHelper.Get(OrgName))` | asserts `"مؤسسهٔ خیریهٔ آزمونِ مانور"` and the address |
| new | — | `Assert.IsTrue(CountRows("TblAuditLog") > 0)` |

Three other "known limitation" assertions were **left untouched and still pass**, because they belong to Tier 2, which was deliberately not in this scope: `TblReminder`, `SyncOutbox`, and the `EntRolePermission` custom exception.

---

## 5. ⚠️ Build blocker introduced outside this task — needs your decision

Midway through Phase 4 the main project stopped compiling:

```
Program.cs(54,17): error CS0103: The name 'AdminInitializer' does not exist in the current context
```

**This is not from my changes.** File timestamps establish the sequence:

| Time | Event |
|---|---|
| 22:50:30 | my `BackupHelper.cs` edits |
| 22:51:38 | my `AutoBackupService.cs` edits |
| **22:55:06** | **build succeeded** — `CaseManagement.exe` produced with all my changes |
| 22:56:16 | `Helpers/AdminInitializer.cs` created |
| 22:57:00 | `Program.cs` edited to call `AdminInitializer.EnsureAdminObjects()` |
| 22:59:17 | `FrmEmployees.cs` created |

Something outside this task — a parallel session or your own editing — began adding an **Admin / Employees module** after my build succeeded. Both new files exist on disk and are untracked in git, but neither is registered in `CaseManagement.csproj`:

- `Helpers/AdminInitializer.cs` — **not in csproj** (yet `Program.cs:54` calls it)
- `FrmEmployees.cs` — **not in csproj**

So the project cannot compile from a clean state until those two `<Compile Include>` entries are added.

**I did not fix this**, for two reasons: `CLAUDE.md` says never change project structure, and if another session is actively writing those files, editing the shared `.csproj` risks a collision.

**How Phase 4 was completed regardless:** the test project was built with `-p:BuildProjectReferences=false`, linking against the `CaseManagement.exe` produced at 22:55:06 — the binary that already contains every one of my changes. Verification is therefore valid; my code is confirmed to compile and pass.

**What I need from you:** confirm whether I should add those two files to `CaseManagement.csproj`, or whether the other session/task owns that and should finish it. It is a two-line change, but it is not mine to make.

---

## 6. Not done (deliberately out of the approved scope)

Tier 2 from the audit remains open — roughly 30 tables still absent from backup entirely, most importantly:

| Table | Rows | Consequence today |
|---|---|---|
| `EntRolePermission` | **96** | the entire permission matrix silently reverts to defaults after disaster recovery |
| `EntPermission` / `EntModule` | 24 / 24 | permission catalogue and module enablement lost |
| `EntWorkflow*` | 11 | workflow definitions lost |
| `EntApprovalChain` / `EntApprovalLevel` | 3 | financial approval routing lost |
| `TblCardTemplate` | 1 | hand-built card designs lost |

`Sync*` (6 tables), `EntRecordLock` and the `AiCaseSearchIndex` FTS5 index remain excluded **by design** — see §7 of the audit for why backing them up would be actively harmful.

Recommended next step: Tier 2, with `EntRolePermission` first — it is the highest-value unprotected table in the database.
