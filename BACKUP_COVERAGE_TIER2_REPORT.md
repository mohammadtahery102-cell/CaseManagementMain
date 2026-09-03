# BACKUP_COVERAGE_TIER2_REPORT.md

**Phases 4 & 5 — implementation and validation.**
Companion to `BACKUP_COVERAGE_TIER2_AUDIT.md`.

Date: 30 August 2026
Approved scope: **Category A (23 tables)** · merge policy = **skip, protect local config** · `Adm*` = **Employee/Leave/Mission**
Build: ✅ succeeds, no new warnings. Tests: ✅ **133 passed, 0 failed**.

---

## 1. Modified files

Two files. No schema changes, no renames, no new dependencies, no structural changes.

| File | Change | Purpose |
|---|---|---|
| `Helpers/BackupHelper.cs` | +23 `LoadTableIfExists` calls in `ExportBackup` | export Category A tables |
| | `isFreshInstall` probe added before the restore transaction | distinguish disaster recovery from merge |
| | `RestoreConfigurationTables` call added to **both** restore branches | wire the restore in |
| | New method `RestoreConfigurationTables` | full-replace config, fresh install only |
| | New method `RestoreUserKeyedTable` | `Username` remapping — prevents permission corruption |
| `CaseManagement.Tests/DisasterRecoveryDrillTests.cs` | seed now includes a revoked permission, a user-level override, and a card template | make the drill able to detect the real failure modes |
| | 6 new assertions replacing the one that asserted the bug | prove the matrix survives |
| | `TblReminder` assertion inverted; `SyncOutbox` reworded as a design decision | reflect new coverage |
| | New test `Tier2_MergeRestore_DoesNotOverwriteLocalConfiguration` | prove merge safety |

---

## 2. Tables added — 23

| Group | Tables |
|---|---|
| **Access control** (6) | EntPermission · EntRolePermission · EntUserPermission* · EntModule · EntRoleModule · EntUserModule* |
| **Workflow / approval / rules** (6) | EntWorkflow · EntWorkflowState · EntWorkflowTransition · EntApprovalChain · EntApprovalLevel · EntRule |
| **Templates & definitions** (6) | TblCardTemplate · TblCardTemplateVersion · TblAssistancePackage · TblAssistancePackageItem · TblReportTemplate · TblScheduledReport |
| **User data** (2) | TblCaseTransferHistory · TblReminder |
| **HR module** (3) | AdmEmployee · AdmLeave · AdmMission |

\* restored via `Username` remapping — see §3.2.

### Coverage before → after

| | Before Tier 2 | After Tier 2 |
|---|---|---|
| `BackupHelper` export | 17 | **40** |
| `AccountingBackupHelper` export | 11 | 11 |
| **Total covered** | **28 / 73 (38%)** | **51 / 73 (70%)** |
| Remaining excluded | 45 | 22 |

---

## 3. How the two hard problems were solved

### 3.1 Config tables are never empty — so neither Tier 1 technique worked

`EnterpriseInitializer` re-seeds 96 default permission rows on **every** application start using `INSERT OR IGNORE`. At restore time the table is therefore never empty and every key already exists, which defeats both Tier 1 approaches:

| Technique | Outcome | Why |
|---|---|---|
| `RestoreWholeTableIfEmpty` | skips silently | table is pre-seeded |
| `INSERT OR IGNORE` | no-op | all `(RoleName, PermKey)` keys present |

Either one would have produced a restore that **reports success while reverting the matrix to factory defaults** — the exact failure this task set out to eliminate.

**Solution — restore-mode detection.** A single probe runs before any write:

```csharp
bool isFreshInstall;                                  // TblCase is never seeded,
using (var probe = new SQLiteCommand("SELECT COUNT(1) FROM TblCase", con))
    isFreshInstall = Convert.ToInt32(probe.ExecuteScalar()) == 0;   // so it is a clean signal
```

| Scenario | `TblCase` | Config tables |
|---|---|---|
| Disaster recovery / fresh install | empty | **full replace** — backup overrides the freshly seeded defaults |
| Merge another office's backup into a live DB | populated | **skipped** — local configuration untouched |

It must be evaluated before the transaction writes, because after the first case is inserted the table is no longer empty.

### 3.2 `UserID` remapping — the permission-corruption hazard

`MergeUsers` deliberately drops the `UserID` column when inserting, so restored users receive **new** auto-increment IDs; and `DatabaseInitializer` seeds a default `admin` occupying `UserID = 1` before any restore begins. IDs therefore shift in practice, not just in theory.

`EntUserPermission` and `EntUserModule` are keyed by `UserID`. Inserting them verbatim would attach one person's permission overrides **to whoever now holds that integer** — silently granting a user someone else's access.

`RestoreUserKeyedTable` translates instead of copying:

1. from the backup, build `old UserID → Username`
2. from the target database, build `Username → new UserID`
3. rewrite only the `UserID` column; every other column passes through unchanged
4. rows whose user cannot be resolved are **skipped and counted** — skipping is safe, mis-attaching is not

Note also that `InsertRow` writes all columns including primary keys, so a full replace preserves original IDs. `TblAssistancePackageItem.PackageID` and `TblCardTemplateVersion.TemplateID` therefore stay valid with no remapping — only the two user-keyed tables needed special handling.

---

## 4. Validation results

### Disaster recovery — permission matrix survives

`DisasterRecovery_FullRestore_AllCoveredCategories_Verified` now performs a real backup → total data loss → fresh install → restore, and asserts:

| # | Assertion | Proves |
|---|---|---|
| 1 | custom **grant** (`Operator`/`Case.Delete`) is still `1` | deliberate grants survive |
| 2 | custom **revoke** (`Operator`/`Sync.Execute`, default `1`) is still `0` | **no privilege escalation** — the critical case |
| 3 | user override resolves via `JOIN TblUsers ON Username` | `Username` remapping works |
| 4 | zero `EntUserPermission` rows point at a non-existent user | no orphaned/mis-attached permissions |
| 5 | `TblUsers.Role` still `Operator` | role assignments survive |
| 6 | workflow states, permission catalogue and the hand-built card template restored | config definitions survive |

Assertion 2 is the one that matters most: it is the case where a lost matrix silently **hands access back** to someone it was deliberately taken from.

### Merge restore — local configuration protected

New test `Tier2_MergeRestore_DoesNotOverwriteLocalConfiguration`: take a backup with `Case.Delete = 1`, change the local setting to `0`, then import that backup into the live database.

| Assertion | Result |
|---|---|
| local value stays `0`, not overwritten by the backup's `1` | ✅ |
| `EntRolePermission` row count unchanged | ✅ no duplicates |
| no `(RoleName, PermKey)` pair appears twice | ✅ PK integrity held |

### Test totals

| Suite | Result |
|---|---|
| Backup / restore / encryption / auto-backup / accounting-backup | **39 / 39** |
| Permission suites — `AdminPermissionTests`, `AccountingPermissionTests`, `AiPermissionTests`, `LockServiceTests` | **33 / 33** |
| `ApplicantDocumentTests`, `RecordHistoryWiringTests`, `DevCenterSafetyTests`, `FrmSettingsDeleteCasesTests`, `IntegrityTests` | **61 / 61** |
| **Total** | **133 passed, 0 failed** |

The 33 permission-suite tests were run specifically because this change touches permission restore; all pass unchanged.

---

## 5. Remaining exclusions — 22 tables

### Deferred (Category B) — 13 tables

EntWorkflowInstance · EntWorkflowHistory · EntApprovalRequest · EntApprovalAction · EntTask · EntSecurityEvent · EntErrorLog · EntRuleLog · AiConversation · AiMessage · AiIntentLog · AdmJobApplication · AdmDriverContract

All currently hold **0 rows**. Several carry integer foreign keys to `TblCase`/`TblUsers` that would need the same remapping treatment as the case child tables — real complexity for no data at present. Worth revisiting once the workflow, approval and HR modules are in active use.

### Excluded by design — 9 tables

| Table(s) | Why backing them up would be harmful |
|---|---|
| SyncOutbox | Pending outbound operations **for this device**; restoring another machine's queue would replay foreign operations against the server |
| SyncState, SyncBaseline | Per-device sync cursors; stale cursors cause incorrect delta computation and data divergence |
| SyncConflict, SyncFile, SyncFileDownload | Transient transport state, meaningless on a different install |
| EntRecordLock | Live edit locks; restoring would lock records for users who are not editing |
| AiCaseSearchIndex | FTS5 derived index — must be rebuilt from `TblCase`, never restored as data |
| TblAuditLogs | Vestigial second audit table: 0 rows, superseded by `TblAuditLog`, no writer in the codebase |

The reasoning is recorded in code comments and in the drill test, so a future reader does not "fix" these omissions.

---

## 6. Risk assessment

### Risks closed

| Risk | Severity before | Status |
|---|---|---|
| Permission matrix silently reverts to factory defaults after DR | 🔴 Critical — **silent privilege escalation** | ✅ closed, regression-tested |
| Per-user overrides re-attach to the wrong person | 🔴 Critical — silent mis-grant | ✅ closed via `Username` remapping |
| Workflow / approval-chain definitions lost | 🔴 Critical | ✅ closed |
| Hand-built card templates lost | 🔴 Critical | ✅ closed |
| HR module (staff register, leave) entirely unprotected | 🔴 Critical | ✅ closed for Employee/Leave/Mission |
| Case transfer history (legal record) lost | 🔴 Critical | ✅ closed |

### Residual risks

| Risk | Level | Note |
|---|---|---|
| Category B tables still unprotected | 🟡 Low **today** | all 0 rows; becomes 🟠 as workflow/approval/HR modules go into real use |
| Config not propagated on merge restores | 🟡 By design | approved policy; a multi-office deployment wanting central config push would need a separate, deliberate mechanism — restore is the wrong tool for it |
| `Adm*` schema may still change | 🟡 Low | `LoadTableIfExists` degrades safely; if columns are added they are picked up automatically, but new `Adm*` **tables** must be added to the export list |
| `isFreshInstall` treats an emptied-but-not-new DB as fresh | 🟢 Very low | restoring a full backup into a database with zero cases is legitimately a recovery scenario |

### One operational recommendation

`AdmJobApplication` and `AdmDriverContract` were left out per the approved scope, so the HR module is **partially** covered. If that module goes into production use, those two should be added — otherwise a restore would return staff and leave records while silently dropping recruitment and contract data, which is a confusing half-state to debug.
