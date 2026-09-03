# BACKUP_COVERAGE_TIER2_AUDIT.md

**Phases 1–3 — audit and plan. No code modified.**

Date: 30 August 2026
Follows: `BACKUP_RESTORE_AUDIT.md` and `BACKUP_RESTORE_FIX_REPORT.md` (Tier 1, completed)
Evidence: static trace of `BackupHelper.cs`, `AccountingBackupHelper.cs`, `EnterpriseInitializer.cs`, `PermissionService.cs`, `AdminInitializer.cs` + live census of `bin/Debug/CaseDB.sqlite`
Build status: ✅ `CaseManagement.csproj` compiles (the `AdminInitializer` csproj blocker from last session has been resolved externally)

---

## 0. Headline finding

**A naive Tier 2 fix would silently fail for the single most important table.**

`EntRolePermission` cannot be restored with either of the two techniques used in Tier 1. `EnterpriseInitializer` re-seeds 96 default rows on **every application start** (`Program.cs:59`), using `INSERT OR IGNORE` (`EnterpriseInitializer.cs:788–794`). So by the time any restore runs, the table is **never empty and every key already exists**:

| Technique | Result on disaster recovery | Why |
|---|---|---|
| `RestoreWholeTableIfEmpty` (Tier 1 primitive) | ❌ skips — customisations lost | table is pre-seeded, so never empty |
| `INSERT OR IGNORE` (the `MergeLookup` pattern) | ❌ no-op — customisations lost | all 96 `(RoleName, PermKey)` keys already present |

Both would produce a restore that *reports success* while silently reverting the permission matrix to factory defaults. This is the exact failure mode the task is trying to eliminate, and it is why Tier 2 needs a third restore strategy rather than a copy of Tier 1.

---

## 1. Full coverage inventory

**73 tables defined in code** (63 present in the live DB; `TblCardTemplateVersion`, 3 `Ai*` + 1 FTS5 index, and the 5 new `Adm*` tables initialise on first use of their module).

### ✅ Included in backup — 28 tables

**`BackupHelper` — 17** (all now restored in both modes after Tier 1)
TblCenter · TblCase · TblFamily · TblDocs · TblAssistance · TblCaseRelation · TblArchiveHistory · TblUsers · TblLookup · TblAppSettings · TblAuditLog · TblCaseStatusHistory · TblFamilyStatusHistory · TblFamilyRoleHistory · TblApplicant · TblApplicantStatusHistory · EntRecordVersion

**`AccountingBackupHelper` — 11** (now also runs on the automatic schedule after Tier 1)
AccPeriod · AccFund · AccParty · AccIncomeCategory · AccExpenseCategory · AccTransaction · AccStipend · AccSalary · AccExpenseItem · AccSettings · AccAudit

### ❌ Excluded from backup — 45 tables

#### Enterprise — 21 excluded (only `EntRecordVersion` is covered)

| Table | Rows | Purpose | Business impact if lost | DR impact | Risk |
|---|---|---|---|---|---|
| **EntRolePermission** | **96** | The permission matrix: which role may do what | Every deliberate grant/revoke is erased | **Silently reverts to factory defaults** — see §2 | 🔴 **CRITICAL** |
| **EntPermission** | 24 | Catalogue of permission keys | Custom keys vanish; matrix loses rows | Re-seeded to defaults only | 🔴 CRITICAL |
| **EntUserPermission** | 0 | Per-user overrides of role permissions | Individual exceptions erased | Lost; **and keyed by `UserID`, which restore reassigns** (§2.3) | 🔴 CRITICAL |
| **EntModule** | 24 | Which modules are enabled | Module enablement resets | Re-seeded to defaults | 🔴 CRITICAL |
| **EntRoleModule** | 0 | Module access per role | Role-level module gating erased | Lost entirely (never seeded) | 🔴 CRITICAL |
| **EntUserModule** | 0 | Module access per user | Per-user gating erased | Lost; same `UserID` problem | 🔴 CRITICAL |
| **EntWorkflow** | 1 | Workflow definitions | Custom workflows lost | Re-seeded to default only | 🔴 CRITICAL |
| **EntWorkflowState** | 5 | States within workflows | Custom states lost | Re-seeded to default | 🔴 CRITICAL |
| **EntWorkflowTransition** | 5 | Allowed state transitions | Transition rules lost | Re-seeded to default | 🔴 CRITICAL |
| **EntApprovalChain** | 1 | Approval routing chains | Financial approval routing lost | Re-seeded to default | 🔴 CRITICAL |
| **EntApprovalLevel** | 2 | Levels/approver roles per chain | Who approves what is lost | Re-seeded to default | 🔴 CRITICAL |
| **EntRule** | 2 | Business rules engine content | Custom rules lost | Re-seeded to default | 🔴 CRITICAL |
| EntWorkflowInstance | 0 | In-flight workflow runs | Cases stuck mid-workflow lose position | Lost | 🟠 HIGH |
| EntWorkflowHistory | 0 | Workflow transition log | Audit trail of decisions lost | Lost | 🟠 HIGH |
| EntApprovalRequest | 0 | Pending approval requests | Pending financial approvals vanish | Lost | 🟠 HIGH |
| EntApprovalAction | 0 | Approve/reject actions taken | Evidence of who approved lost | Lost | 🟠 HIGH |
| EntTask | 0 | Assigned staff tasks | Assigned work lost | Lost | 🟠 HIGH |
| EntSecurityEvent | 0 | Security event log | Security forensics lost | Lost | 🟡 MEDIUM |
| EntErrorLog | 1 | Application error log | Diagnostics lost | Lost | 🟡 MEDIUM |
| EntRuleLog | 0 | Rule execution log | Rule audit lost | Lost | 🟡 MEDIUM |
| EntRecordLock | 0 | Live edit locks | — | — | 🔵 EXCLUDE BY DESIGN |

#### Core `Tbl*` — 9 excluded

| Table | Rows | Purpose | Business impact if lost | DR impact | Risk |
|---|---|---|---|---|---|
| **TblCardTemplate** | 1 | Guardian-card designs (`Name` UNIQUE) | Hand-built card layouts lost — hours of design work | Lost entirely | 🔴 CRITICAL |
| **TblCardTemplateVersion** | n/a | Version history of card designs | Rollback capability lost | Lost | 🟠 HIGH |
| **TblAssistancePackage** | 0 | In-kind aid package definitions | Aid catalogue lost | Lost | 🔴 CRITICAL |
| **TblAssistancePackageItem** | 0 | Items within each package | Package contents lost | Lost | 🔴 CRITICAL |
| **TblCaseTransferHistory** | 0 | Record of case transfers between offices | **Legal/audit record** of custody changes lost | Lost | 🔴 CRITICAL |
| **TblReportTemplate** | 0 | User-built report definitions | Custom reports lost | Lost | 🟠 HIGH |
| **TblScheduledReport** | 0 | Scheduled report configuration | Automation config lost | Lost | 🟠 HIGH |
| **TblReminder** | 0 | User reminders/alarms | Pending reminders lost | Lost | 🟠 HIGH |
| TblAuditLogs | 0 | Second, unused audit table | none observed | — | 🔵 EXCLUDE (vestigial) |

#### Admin / HR module (`Adm*`) — 5 excluded — **NEW, entirely unprotected**

Added to the project after the Tier 1 work (`Helpers/AdminInitializer.cs`, wired at `Program.cs:54`). **No backup coverage whatsoever.**

| Table | Purpose | Business impact if lost | Risk |
|---|---|---|---|
| **AdmEmployee** | Employee records | Entire staff register lost | 🔴 CRITICAL |
| **AdmLeave** | Leave requests/balances | Leave history and entitlements lost | 🔴 CRITICAL |
| **AdmMission** | Duty missions | Mission records lost | 🔴 CRITICAL |
| **AdmJobApplication** | Recruitment applications | Applicant pipeline lost | 🟠 HIGH |
| **AdmDriverContract** | Transport contracts | Contractual records lost | 🟠 HIGH |

#### Sync — 6 excluded (correctly)

SyncOutbox · SyncState · SyncConflict · SyncBaseline · SyncFile · SyncFileDownload — 🔵 **EXCLUDE BY DESIGN**, see §3-C.

#### AI — 4 excluded

| Table | Purpose | Risk |
|---|---|---|
| AiConversation / AiMessage / AiIntentLog | Assistant chat history | 🟡 MEDIUM |
| AiCaseSearchIndex | FTS5 derived index | 🔵 EXCLUDE BY DESIGN — rebuildable |

---

## 2. Permission system review (Phase 2)

### 2.1 How permission resolution actually works

From `PermissionService.HasPermission` / `GetCache` (`PermissionService.cs:38–111`):

```
1. SuperAdmin                    → always allowed (short-circuit, ignores all tables)
2. EntRolePermission[Role]       → base grants for the user's role
3. EntUserPermission[UserID]     → per-user overrides, applied on top (wins over role)
4. key not found in the matrix   → LegacyFallback(): role-shaped guess by key suffix
```

`LegacyFallback` (`:125–144`) infers from the key suffix: `.Delete` → `CanDelete()`, `.Manage`/`.Override`/`.Approve` → `IsAdmin()`, `.Create`/`.Edit`/`.Review` → `CanEdit()`, anything else → **any logged-in user is allowed**.

### 2.2 What happens after disaster recovery today

The matrix does **not** end up empty — which is precisely what makes this dangerous. `EnterpriseInitializer` re-seeds it at startup, so after a restore the system comes up looking healthy while every administrative decision has been quietly undone:

| Original administrative decision | After disaster recovery | Consequence |
|---|---|---|
| Permission explicitly **revoked** from a role (`IsGranted = 0` overriding a default of 1) | reverts to **granted** | 🔴 **Privilege escalation** — a user silently regains access that was deliberately taken away |
| Permission explicitly **granted** to a role (default 0) | reverts to **denied** | 🟠 Work stoppage — staff can no longer do their job, with no visible cause |
| Custom permission key added to `EntPermission` | row absent | falls through to `LegacyFallback` → **unknown keys default to "any logged-in user allowed"** |
| Per-user override in `EntUserPermission` | row absent | user silently falls back to plain role permissions |
| Module disabled via `EntModule` / `EntRoleModule` | reverts to seeded default | disabled modules become reachable again |

The privilege-escalation row is the serious one: **nothing in the UI indicates it happened.** The administrator sees a successful restore, correct case counts, and working logins. There is no error, no warning, and no audit entry saying the matrix was reset. The existing drill test already proves this behaviour (`DisasterRecoveryDrillTests`, the `permSurvived` assertion left in place at Tier 1).

### 2.3 The `UserID` remapping hazard (critical for correctness)

`MergeUsers` deliberately **excludes the `UserID` column** when inserting (`BackupHelper.cs`, `MergeUsers`: `if (col == "UserID") continue;`). Restored users therefore receive **new auto-increment IDs**. Compounding this, `DatabaseInitializer` seeds a default `admin` user when the table is empty (`:2016–2039`), so a fresh install already occupies `UserID = 1` before any restore begins.

Consequence: `EntUserPermission` and `EntUserModule` are keyed by `UserID`. Restoring their rows verbatim would bind permissions to **whichever user now happens to hold that integer** — silently granting one person another person's permissions.

**These two tables must be remapped by `Username`, never inserted verbatim.** This is the "no permission corruption" requirement in practice.

Tables keyed by **text** (`RoleName`, `PermKey`, `ModuleKey`) are unaffected — `EntRolePermission`, `EntRoleModule`, `EntPermission` and `EntModule` carry no integer FKs and are safe to restore directly.

### 2.4 Role assignment survival

Role assignment itself lives in `TblUsers.Role` (a text column), which **is** already backed up and restored. So roles survive today; it is only the *meaning* of each role — the matrix — that is lost.

---

## 3. Implementation plan (Phase 3 — classification)

### Restore-mode detection: the prerequisite for all of Category A

Because config tables are pre-seeded and never empty, restore must distinguish the two scenarios **before writing anything**:

```
isFreshInstall = (SELECT COUNT(1) FROM TblCase) == 0   // evaluated once, before the restore writes
```

| Scenario | `TblCase` | Strategy for config tables |
|---|---|---|
| **Disaster recovery / fresh install** | empty | **full replace** — `DELETE` + `INSERT` so backup wins over seeded defaults |
| **Merging another office's backup into a live DB** | populated | **skip** — never overwrite this office's live configuration |

This reuses the existing `RestoreWholeTable` primitive and keeps merge semantics untouched, exactly as the Tier 1 emptiness guard did — but keyed on a signal that still works for pre-seeded tables.

### Category A — MUST be backed up (23 tables)

| Tables | Justification | Restore strategy |
|---|---|---|
| EntRolePermission, EntPermission, EntModule, EntRoleModule | Access control. Loss causes silent privilege escalation (§2.2) | full replace when fresh install |
| EntUserPermission, EntUserModule | Access control, per user | **remap by `Username`**, never by raw `UserID` (§2.3) |
| EntWorkflow, EntWorkflowState, EntWorkflowTransition | Workflow definitions; re-seeded to defaults otherwise | full replace when fresh install |
| EntApprovalChain, EntApprovalLevel | Financial approval routing — controls who signs off on money | full replace when fresh install |
| EntRule | Business rules content | full replace when fresh install |
| TblCardTemplate, TblCardTemplateVersion | Irreplaceable hand-built design work (`Name` UNIQUE) | full replace when fresh install |
| TblAssistancePackage, TblAssistancePackageItem | Aid catalogue; child keyed by `PackageID` → **needs parent-ID remapping** | full replace when fresh install |
| TblCaseTransferHistory | Legal/audit record of custody transfer between offices | full replace when fresh install |
| TblReportTemplate, TblScheduledReport | User-built reports and their automation | full replace when fresh install |
| TblReminder | User-entered data with no other home | full replace when fresh install |
| AdmEmployee, AdmLeave, AdmMission | **New HR module with zero protection** — staff register, leave entitlements, missions | full replace when fresh install |

### Category B — SHOULD be backed up (9 tables)

| Tables | Justification | Note |
|---|---|---|
| AdmJobApplication, AdmDriverContract | Real business records, lower operational urgency than payroll-adjacent data | same strategy as Category A |
| EntWorkflowInstance, EntWorkflowHistory | In-flight workflow position and decision trail | integer FKs to cases — needs care |
| EntApprovalRequest, EntApprovalAction | Pending approvals and who acted | integer FKs — needs care |
| EntTask | Assigned staff work | integer FKs to cases/users |
| EntSecurityEvent, EntErrorLog, EntRuleLog | Diagnostic and security logs | append-only; restore only when empty |
| AiConversation, AiMessage, AiIntentLog | Assistant history | low value, low risk |

**Recommendation:** defer Category B to a later pass. Several carry integer foreign keys to `TblCase`/`TblUsers` that would need the same remapping treatment as the case child tables — meaningful extra complexity for data that is currently all zero rows, versus Category A which contains live, irreplaceable configuration.

### Category C — SHOULD REMAIN EXCLUDED (9 tables)

| Table | Why exclusion is correct |
|---|---|
| SyncOutbox | Pending outbound operations **for this device**. Restoring another machine's queue would replay foreign operations against the server. |
| SyncState, SyncBaseline | Per-device sync cursors. Stale cursors produce incorrect delta computation and data divergence. |
| SyncConflict, SyncFile, SyncFileDownload | Transient transport state, meaningless on a different install. |
| EntRecordLock | Live edit locks. Restoring them would lock records for users who are not editing anything. |
| AiCaseSearchIndex | FTS5 derived index — must be rebuilt from `TblCase`, never restored as data. |
| TblAuditLogs | Vestigial second audit table: 0 rows, superseded by `TblAuditLog`, no writer found in the codebase. |

Backing any of these up would be actively harmful, not merely wasteful. Their exclusion should be recorded in code comments so a future reader does not "fix" the omission.

---

## 4. Proposed scope for Phase 4

**Category A only — 23 tables**, using restore-mode detection, with `Username` remapping for the two user-keyed tables and `PackageID` remapping for assistance package items.

Projected coverage after implementation:

| | Before Tier 2 | After Category A |
|---|---|---|
| Tables backed up | 28 / 73 (38%) | **51 / 73 (70%)** |
| Durable business data protected | partial | **complete** |
| Remaining exclusions | 45 | 22 (9 by design + 13 Category B deferred) |

---

## Open questions before Phase 4

**Q1 — Confirm Category A scope (23 tables), or include Category B as well?**
I recommend Category A only. Category B is currently 0 rows across the board and several tables need integer-FK remapping that materially increases risk for no present data.

**Q2 — Merge-mode behaviour for configuration tables.**
My plan: on a merge into a populated database, **skip** config tables entirely to protect the local office's settings. The alternative — merging another office's permission matrix into yours — seems clearly wrong, but it changes what a multi-office restore does, so I want it confirmed rather than assumed.

**Q3 — `Adm*` module ownership.**
These five tables arrived from work outside this task and the module may still be in progress. Adding backup coverage now is straightforward, but if its schema is still changing, the export list would need updating again. Include now, or wait until that module settles?
