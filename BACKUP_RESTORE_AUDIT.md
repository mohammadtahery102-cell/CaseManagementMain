# BACKUP_RESTORE_AUDIT.md

**Phase 1 — Audit only. No code was modified.**

Date: 29 August 2026
Scope: `Helpers/BackupHelper.cs`, `Helpers/AccountingBackupHelper.cs`, `Helpers/AutoBackupService.cs`, `Helpers/BackupEncryption.cs`, `Helpers/DatabaseInitializer.cs`, `Helpers/AccountingInitializer.cs`, `Enterprise/EnterpriseInitializer.cs`, `Sync/OfflineSyncInitializer.cs`, `Helpers/AiInitializer.cs`
Evidence base: static trace of every export/import path + live table and row census from `bin/Debug/CaseDB.sqlite` (63 tables, 1,661 cases, 3,802 family members).

---

## 0. Verdict on the three reported findings

All three were verified against the code. **All three are confirmed.**

| ID | Claim | Verdict | Evidence |
|---|---|---|---|
| **C-1** | Backup exports only a subset of tables | ✅ **CONFIRMED** | `BackupHelper.cs:40–72` loads 17 tables. Live DB has 63. |
| **C-2** | `TblAppSettings` and `TblAuditLog` exported but never restored | ✅ **CONFIRMED** | Exported at `:56–57`. Absent from the prelude (`:146–157`), the merge branch (`:159–221`) and the classic branch (`:222–252`). |
| **C-3** | Merge restore skips `TblApplicant`, `TblApplicantStatusHistory`, `EntRecordVersion` | ✅ **CONFIRMED** | `RestoreWholeTable` for these three is called only at `:243–245`, inside the `else` (classic) branch. |

**Independent corroboration:** the existing test `DisasterRecoveryDrillTests.DisasterRecovery_FullRestore_AllCoveredCategories_Verified` already proves C-2 and C-3 empirically, with assertions that deliberately *lock in the broken behaviour* as documented known limitations (lines 476, 505, 512, 514, 518). A prior session found these bugs and was instructed to document rather than fix them.

### Two additional findings not in the original list

| ID | Finding | Severity |
|---|---|---|
| **C-4 (new)** | **Automatic backups contain no accounting data.** `AutoBackupService.cs:63–64` calls `BackupHelper.ExportEncryptedBackup`, never `AccountingBackupHelper`. Accounting backup is manual-only, reachable solely from `FrmAccounting.cs:2307`. A site that relies on scheduled backups has **zero** financial-data protection. | 🔴 Critical |
| **C-5 (new)** | **Merge restore is the path every modern backup takes.** The merge branch runs whenever `TblCase` has a `GlobalID` column (`:122`), which `DatabaseInitializer.cs:422` guarantees for every current installation. The classic branch — which restores strictly more tables — is now reachable only from pre-GlobalID legacy backup files. **The better-covered path is effectively dead code.** | 🔴 Critical |

---

## 1. All database tables

63 tables in the live database; 66 defined in code (`TblCardTemplateVersion` and the three `Ai*` tables initialise only when those modules are first used), plus 1 FTS5 virtual table.

Row counts are from the live `bin/Debug/CaseDB.sqlite`.

### Core (`Tbl*`) — 24 tables

| Table | Rows | Owner |
|---|---|---|
| TblCase | 1,661 | DatabaseInitializer |
| TblFamily | 3,802 | DatabaseInitializer |
| TblDocs | 0 | DatabaseInitializer |
| TblAssistance | 0 | DatabaseInitializer |
| TblAssistancePackage | 0 | DatabaseInitializer |
| TblAssistancePackageItem | 0 | DatabaseInitializer |
| TblCaseRelation | 0 | DatabaseInitializer |
| TblCaseStatusHistory | 6 | DatabaseInitializer |
| TblCaseTransferHistory | 0 | DatabaseInitializer |
| TblArchiveHistory | 0 | DatabaseInitializer |
| TblApplicant | 1 | DatabaseInitializer |
| TblApplicantStatusHistory | 0 | DatabaseInitializer |
| TblFamilyStatusHistory | 0 | DatabaseInitializer |
| TblFamilyRoleHistory | 0 | DatabaseInitializer |
| TblUsers | 6 | DatabaseInitializer |
| TblCenter | 11 | DatabaseInitializer |
| TblLookup | 141 | DatabaseInitializer |
| TblAppSettings | 22 | DatabaseInitializer |
| TblAuditLog | 3,597 | DatabaseInitializer |
| TblAuditLogs | 0 | DatabaseInitializer |
| TblReminder | 0 | DatabaseInitializer |
| TblReportTemplate | 0 | DatabaseInitializer |
| TblScheduledReport | 0 | DatabaseInitializer |
| TblCardTemplate | 1 | DatabaseInitializer |
| *TblCardTemplateVersion* | *not created* | DatabaseInitializer |

### Accounting (`Acc*`) — 11 tables

AccPeriod (4), AccFund (8), AccParty (4), AccIncomeCategory (6), AccExpenseCategory (12), AccTransaction (5), AccStipend (22), AccSalary (3), AccExpenseItem (5), AccSettings (8), AccAudit (43)

### Enterprise (`Ent*`) — 22 tables

EntWorkflow (1), EntWorkflowState (5), EntWorkflowTransition (5), EntWorkflowInstance (0), EntWorkflowHistory (0), EntApprovalChain (1), EntApprovalLevel (2), EntApprovalRequest (0), EntApprovalAction (0), EntTask (0), EntRule (2), EntRuleLog (0), EntRecordLock (0), EntRecordVersion (0), EntSecurityEvent (0), EntErrorLog (1), EntPermission (24), EntRolePermission (96), EntUserPermission (0), EntModule (24), EntRoleModule (0), EntUserModule (0)

### Sync (`Sync*`) — 6 tables

SyncOutbox (0), SyncState (0), SyncConflict (0), SyncBaseline (0), SyncFile (0), SyncFileDownload (0)

### AI (`Ai*`) — 3 tables + 1 virtual

AiConversation, AiMessage, AiIntentLog *(not created in this DB)*; `AiCaseSearchIndex` is an FTS5 virtual table — a derived index, rebuildable from `TblCase`.

---

## 2. Tables exported

### `BackupHelper.ExportBackup` — 17 tables (`BackupHelper.cs:40–72`)

| # | Table | Load method |
|---|---|---|
| 1 | TblCenter | `LoadTable` (required) |
| 2 | TblCase | `LoadTable` |
| 3 | TblFamily | `LoadTable` |
| 4 | TblDocs | `LoadTable` |
| 5 | TblAssistance | `LoadTable` |
| 6 | TblCaseRelation | `LoadTable` |
| 7 | TblArchiveHistory | `LoadTable` |
| 8 | TblUsers | `LoadTable` |
| 9 | TblLookup | `LoadTable` |
| 10 | TblAppSettings | `LoadTable` |
| 11 | TblAuditLog | `LoadTable` |
| 12 | TblCaseStatusHistory | `LoadTable` |
| 13 | TblFamilyStatusHistory | `LoadTableIfExists` |
| 14 | TblFamilyRoleHistory | `LoadTableIfExists` |
| 15 | TblApplicant | `LoadTableIfExists` |
| 16 | TblApplicantStatusHistory | `LoadTableIfExists` |
| 17 | EntRecordVersion | `LoadTableIfExists` |

### `AccountingBackupHelper.ExportBackup` — 11 tables (`AccountingBackupHelper.cs:33–38`)

All `Acc*` tables. **Separate file (`AccountingBackup.xml`), separate UI action, separate schedule.**

### Combined coverage: 28 of 63 tables = **44%**

---

## 3. Tables restored

### `BackupHelper.ImportBackup`

**Prelude — runs in BOTH modes** (`:146–157`)

| Table | Strategy |
|---|---|
| TblCenter | `MergeCenters` — `INSERT OR IGNORE` on UNIQUE `CenterCode` |
| TblUsers | `MergeUsers` — `INSERT OR IGNORE` on UNIQUE `Username` |
| TblLookup | `MergeLookup` — `INSERT OR IGNORE` on UNIQUE `(Category, Value)` |

**Merge branch only — `hasGlobalId == true`** (`:159–221`)

| Table | Strategy |
|---|---|
| TblCase | GlobalID dedupe → `InsertCaseRow`, builds `casIdMap` |
| TblFamily | `MergeChildTable` + remap, builds `famIdMap` |
| TblDocs | `MergeChildTable` + remap |
| TblAssistance | `MergeChildTable` + remap |
| TblCaseStatusHistory | `MergeCaseStatusHistory` — newly-inserted cases only |
| TblFamilyStatusHistory | `MergeFamilyHistory` via `famIdMap` |
| TblFamilyRoleHistory | `MergeFamilyHistory` via `famIdMap` |
| TblCaseRelation | `MergeCaseRelations` — both ends remapped |
| TblArchiveHistory | `MergeArchiveHistory` — newly-inserted cases only |

**Classic branch only — `hasGlobalId == false`** (`:222–252`)

| Table | Strategy |
|---|---|
| TblCase, TblFamily, TblDocs, TblAssistance | `DeleteCurrentData` then `InsertTable` |
| TblCaseStatusHistory | `DELETE` then `InsertTable` |
| TblFamilyStatusHistory | `RestoreWholeTable` |
| TblFamilyRoleHistory | `RestoreWholeTable` |
| **TblApplicant** | `RestoreWholeTable` ← **merge branch lacks this** |
| **TblApplicantStatusHistory** | `RestoreWholeTable` ← **merge branch lacks this** |
| **EntRecordVersion** | `RestoreWholeTable` ← **merge branch lacks this** |
| TblCaseRelation, TblArchiveHistory | `InsertTable` |

### `AccountingBackupHelper.ImportBackup`

All 11 `Acc*` tables — full replace inside one transaction, preceded by a mandatory pre-restore safety snapshot (`:89–96`). **Export and restore are perfectly symmetric here.** This module is the reference implementation the main helper should follow.

---

## 4. Tables exported but NOT restored

| Table | Rows at risk | Merge mode | Classic mode | Finding |
|---|---|---|---|---|
| **TblAppSettings** | 22 | ❌ never | ❌ never | **C-2** |
| **TblAuditLog** | 3,597 | ❌ never | ❌ never | **C-2** |
| **TblApplicant** | 1 | ❌ never | ✅ restored | **C-3** |
| **TblApplicantStatusHistory** | 0 | ❌ never | ✅ restored | **C-3** |
| **EntRecordVersion** | 0 | ❌ never | ✅ restored | **C-3** |

`TblAppSettings` and `TblAuditLog` are written into the backup XML and are visible in `VerifyEncryptedBackup` output — the operator sees them listed and reasonably concludes they are protected. They are not. This is worse than not exporting them at all.

---

## 5. Tables restored but NOT exported

**None.** Every table touched by a restore path is present in the corresponding export. No phantom-restore defects exist.

---

## 6. Merge restore differences

The single most important structural finding.

| Aspect | Merge branch | Classic branch |
|---|---|---|
| Trigger | `TblCase` has `GlobalID` | no `GlobalID` column |
| Reachability today | **every backup produced by the current version** | legacy files only |
| Permission gate | any user with edit rights | SuperAdmin only (`:132`) |
| Destructive | no — additive | yes — `DeleteCurrentData` (`:836–845`) |
| ID handling | remaps via `casIdMap` / `famIdMap` | preserves original IDs |
| Tables restored | **9** | **12** |
| Missing vs. the other | TblApplicant, TblApplicantStatusHistory, EntRecordVersion | — |

**The consequence (finding C-5):** the branch with better table coverage is the one nobody reaches any more. Every backup taken today restores through the merge path, and the merge path silently drops three tables the classic path handles. The comment at `:237–240` even calls classic mode "exactly the disaster-recovery case" — but disaster recovery from a current backup does not run that code.

**Why the merge branch omits them (root cause):** these three tables have no `GlobalID` and no natural unique key. `DatabaseInitializer.cs:422–445` adds `GlobalID` only to `TblCase`, `TblFamily`, `TblDocs` and `TblAssistance`. With no dedupe key, a naive insert would duplicate rows every time a backup is merged into a populated database. The prior session correctly declined to guess a policy. **That policy decision is what section 8 below proposes.**

---

## 7. Risk level per table

Risk = (data value × irreplaceability) given current backup coverage.

### 🔴 CRITICAL — real data, no protection at all

| Table | Rows | Why it matters |
|---|---|---|
| **EntRolePermission** | **96** | The entire security model. After restore it silently reverts to defaults — a user could regain or lose access without anyone noticing. Test line 518 already proves this. |
| **EntPermission** | 24 | Permission catalogue |
| **EntModule** | 24 | Module licensing / enablement state |
| **EntWorkflowState** | 5 | Workflow definitions |
| **EntWorkflowTransition** | 5 | Workflow definitions |
| **EntWorkflow** | 1 | Workflow definitions |
| **EntApprovalChain / EntApprovalLevel** | 1 / 2 | Financial approval routing |
| **EntRule** | 2 | Business rules |
| **TblCardTemplate** | 1 | Card designs — hand-built, hours of work |
| **All 11 Acc\* tables** | 110 total | **Not in automatic backups at all** (finding C-4) |

### 🔴 CRITICAL — exported, operator believes protected, silently lost

| Table | Rows |
|---|---|
| **TblAppSettings** | 22 |
| **TblAuditLog** | 3,597 |
| **TblApplicant** | 1 |

### 🟠 HIGH — no protection, currently empty but will fill in production

TblAssistancePackage, TblAssistancePackageItem, TblReportTemplate, TblScheduledReport, TblReminder, TblCaseTransferHistory, TblCardTemplateVersion, EntUserPermission, EntRoleModule, EntUserModule, EntApprovalRequest, EntApprovalAction, EntTask

### 🟡 MEDIUM — append-only logs; loss is regrettable, not operationally fatal

EntSecurityEvent, EntErrorLog, EntRuleLog, EntWorkflowHistory, EntWorkflowInstance, AiConversation, AiMessage, AiIntentLog

### 🔵 LOW — deliberately should NOT be backed up

| Table | Reason |
|---|---|
| `SyncOutbox` | Pending outbound operations for **this device**. Restoring another machine's queue onto a fresh install would replay foreign operations against the server. |
| `SyncState`, `SyncBaseline` | Per-device sync cursors. Restoring stale cursors causes incorrect delta computation. |
| `SyncConflict`, `SyncFile`, `SyncFileDownload` | Transient transport state. |
| `EntRecordLock` | Live edit locks. Restoring them would lock records for users who are not editing. |
| `AiCaseSearchIndex` | FTS5 derived index — rebuilt from `TblCase`, must not be restored as data. |
| `TblAuditLogs` | 0 rows, superseded by `TblAuditLog`, appears vestigial. Recommend confirming it is dead rather than backing it up. |

**This is why "100% table coverage" is the wrong target.** Six `Sync*` tables, `EntRecordLock` and the FTS5 index are *correctly* excluded. The right target is **100% coverage of durable business data**, which is 54 of 63 tables.

---

## 8. Proposed repair strategy (for approval before Phase 3)

Three tiers, smallest blast radius first. All reuse existing primitives — no new architecture, no schema changes.

### Tier 1 — Fix the confirmed defects C-2 and C-3 (lowest risk, highest value)

| Table | Merge mode | Classic mode | Reused primitive |
|---|---|---|---|
| TblAppSettings | `INSERT OR IGNORE` on `SettingKey` — never clobber live local settings | full restore | new `MergeAppSettings`, modelled exactly on `MergeLookup` (`:433`) |
| TblApplicant | restore only when target table is **empty** (fresh-install disaster recovery); skip when populated | unchanged | `RestoreWholeTable` (`:499`) + emptiness guard |
| TblApplicantStatusHistory | same | unchanged | same |
| EntRecordVersion | same | unchanged | same |
| TblAuditLog | **see open question Q1 below** | full restore | — |

The emptiness guard is the key insight: it makes the fix safe in both scenarios without inventing a dedupe key. On a fresh install (real disaster recovery) the table is empty, so data is restored. When merging another office's backup into a live database the table is populated, so nothing is touched and no duplicates appear — exactly the current behaviour.

### Tier 2 — Close C-1 for durable business data

Add to export via `LoadTableIfExists`, and to both restore paths via `RestoreWholeTable` guarded by the same emptiness rule: all 22 `Ent*` tables except `EntRecordLock`, plus TblAssistancePackage, TblAssistancePackageItem, TblReportTemplate, TblScheduledReport, TblReminder, TblCaseTransferHistory, TblCardTemplate, TblCardTemplateVersion, TblAuditLogs *(pending Q3)*, and the three `Ai*` tables.

### Tier 3 — Close C-4

Make `AutoBackupService` also produce an accounting backup, so scheduled backups protect financial data. Smallest form: one additional call alongside the existing one at `AutoBackupService.cs:64`.

### Explicitly out of scope

`Sync*` (6), `EntRecordLock`, `AiCaseSearchIndex` — excluded by design, with the reason recorded in code comments so a future reader does not "fix" the omission.

---

## Open questions requiring your decision

**Q1 — `TblAuditLog` (3,597 rows) restore policy.**
It is append-only with no unique key, so re-merging a backup would duplicate entries. Options: **(a)** restore only when the target table is empty (consistent with Tier 1, safe, recommended); **(b)** always append and accept duplicates; **(c)** leave unrestored and remove it from the export so the operator is not misled. I recommend **(a)**.

**Q2 — Tier 2 scope.**
Do you want the full Tier 2 in this pass, or Tier 1 + Tier 3 first (fixing the confirmed findings and the accounting-backup gap), with Tier 2 as a separate reviewed change? Tier 1 + 3 touches ~2 files; full Tier 2 adds roughly 30 table entries and materially enlarges the backup file.

**Q3 — `TblAuditLogs` (the second, empty audit table).**
Confirm it is dead so I can exclude it, rather than adding backup coverage for a table nothing writes to.

**Q4 — Existing test assertions.**
`DisasterRecoveryDrillTests` currently asserts the *broken* behaviour at lines 476, 505, 512, 514 and 518. Fixing the bugs will make these tests fail. I will invert them to assert correct behaviour and record the change in the fix report — confirming this is expected and not a regression.

---

## What is already correct

For balance — these were examined and found sound:

- **`AccountingBackupHelper` is the reference implementation.** Export and restore are perfectly symmetric, the whole restore is one transaction, and a mandatory pre-restore safety snapshot runs first (`:89–96`). The main helper should adopt this snapshot pattern.
- **`RestoreWholeTable` (`:499–521`) is a well-built primitive** — skips absent backup tables, skips tables missing from the target schema, catches per-table failures so one side table cannot abort a whole restore. It is the correct base for the fix.
- **Transaction safety** — both helpers wrap restore in a transaction with rollback on any error (`:142–261`, `:102–123`).
- **Multi-center guard at `:132`** — correctly blocks non-SuperAdmin users from running the destructive classic restore, which would otherwise wipe every center's data.
- **`MergeCaseStatusHistory` (`:466–472`)** was previously fixed to insert all nine columns instead of five; the dynamic-column approach in `MergeFamilyHistory` (`:526–528`) prevents that class of bug recurring.
- **File/photo restore** works end-to-end and is verified by the existing drill test at lines 479–484.
