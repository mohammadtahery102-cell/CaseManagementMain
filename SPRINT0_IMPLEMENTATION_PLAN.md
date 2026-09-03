# SPRINT 0 — PERMISSION INTEGRATION IMPLEMENTATION PLAN
## CaseManagement — Wiring `PermissionService` into Live Enforcement

| | |
|---|---|
| **Document Type** | Implementation package (analysis & planning only — **no code changed**) |
| **Version** | 1.0 |
| **Date** | 2026-08-22 |
| **Builds On** | `SYSTEM_AUDIT_REPORT.md` §10.2 (Authorization), §17.1 CM-02, §18 Roadmap 2.5 |
| **Scope** | Permission-related files only (re-scanned fresh for this document) |
| **Out of Scope** | Encryption (CM-01), concurrency (CM-05), sync content correctness, terminology fixes |

---

# 0. Executive Summary

The system has **two parallel authorization systems**:

- **`SecurityContext`** (`Helpers/SecurityContext.cs`) — 4 hard-coded roles (`SuperAdmin`, `Admin`, `Operator`, `Viewer`), coarse role-string checks (`CanEdit()`, `CanDelete()`, `IsAdmin()`, `IsSuperAdmin()`). **This is the system that is actually enforced today**, at **72 call sites across 30 files**.
- **`PermissionService`** (`Enterprise/PermissionService.cs`) — fine-grained RBAC (`EntPermission` / `EntRolePermission` / `EntUserPermission`), fully built, with a UI (`FrmPermissionMatrix`), a working cache, and a documented decision order (SuperAdmin → user exception → role grant → **legacy fallback**). **It enforces nothing except workflow transitions** (`WorkflowService.PermissionGate`).

Sprint 0 is the work to make `PermissionService` the actual gate for all 72 call sites, while a re-scan found a **second, previously-uncatalogued gap**: at least **9 operations have no permission check of any kind** — not even the crude `SecurityContext` one. These include Word/PDF case export, case printing, the dynamic report builder, guardian card and assistance-receipt batch printing, and all three read-only Enterprise screens (Version History, Security Audit, Error Log). Today, any logged-in user — including `Viewer` — can open and use every one of these.

**This document's recommendation (§12) is to resequence the work**: close the "zero-check" gaps first (they are a bigger confidentiality risk than the coarse-role CRUD system, which is at least enforced), do the mechanical `SecurityContext` → `PermissionService` remap for core CRUD second, and treat the two brand-new roles (`Manager`, `Finance`) the user has requested as an **additive, low-risk Wave 1** step — mapped identically to existing roles until enforcement lands, so user-facing behavior never changes twice in the same sprint.

---

# 1. Full Inventory — Legacy Authorization Call Sites

Re-scan of `grep -rn "SecurityContext\.(CanEdit|CanDelete|IsAdmin|IsSuperAdmin)\(\)"` across all non-generated `.cs` files (backup folders and `bin`/`obj` excluded). **72 call sites, 30 files.**

## 1.1 `SecurityContext.CanEdit()` — 15 sites

| # | File:Line | Guarded Operation |
|---|---|---|
| 1 | `FrmCase.cs:1665` | Save case (insert/update) |
| 2 | `FrmCase.cs:1804` | Enter case edit mode |
| 3 | `FrmFamily.cs:934` | Save family member |
| 4 | `FrmFamily.cs:1028` | Enter family member edit mode |
| 5 | `FrmDocs.cs:435` | Save document attachment |
| 6 | `FrmDocs.cs:538` | Enter document edit mode |
| 7 | `FrmApplicant.cs:438` | Save applicant |
| 8 | `FrmCaseRelations.cs:175` | Add case relation |
| 9 | `FrmAssignMemberRole.cs:318` | Apply bulk member-role assignment |
| 10 | `FrmFinance.cs:393` | Register assistance (financial) |
| 11 | `Accounting/FrmAccounting.cs:758` | Begin revise transaction |
| 12 | `Accounting/FrmAccounting.cs:847` | Save accounting transaction |
| 13 | `Sync/FrmSyncWizard.cs:1400` | Sync wizard write operation |
| 14 | `Sync/MediaSyncEngine.cs:52` | Media file sync write |

## 1.2 `SecurityContext.CanDelete()` — 13 sites

| # | File:Line | Guarded Operation |
|---|---|---|
| 1 | `FrmCase.cs:1954` | Delete case |
| 2 | `FrmFamily.cs:1186` | Delete family member |
| 3 | `FrmDocs.cs:655` | Delete document |
| 4 | `FrmApplicant.cs:525` | Delete applicant |
| 5 | `FrmCaseRelations.cs:269` | Delete case relation |
| 6 | `FrmArchive.cs:353` | Restore archived record |
| 7 | `FrmDashboard.cs:2856` | Cleanup orphaned files (maintenance) |
| 8 | `FrmSettings.cs:219` | Show "Delete Cases" maintenance tab |
| 9 | `FrmSettings.cs:265` | Show "Delete Cases" maintenance tab (duplicate gate) |
| 10 | `FrmSettings.cs:594` | Execute bulk case deletion |
| 11 | `Accounting/FrmAccounting.cs:976` | Reverse/void accounting transaction |

## 1.3 `SecurityContext.IsAdmin()` — 20 sites

| # | File:Line | Guarded Operation |
|---|---|---|
| 1 | `FrmDashboard.cs:2815` | Open Users screen |
| 2 | `FrmDashboard.cs:2830` | Open Assign-Member-Role screen |
| 3 | `FrmDashboard.cs:2844` | Open Card Template Manager |
| 4 | `FrmDashboard.cs:2955` | Open Settings screen |
| 5 | `FrmUsers.cs:216` | Add user |
| 6 | `FrmUsers.cs:314` | Toggle user active/inactive |
| 7 | `FrmUsers.cs:370` | Delete user |
| 8 | `Enterprise/ApprovalService.cs:201` | Cancel approval request (non-owner) |
| 9 | `Enterprise/ApprovalService.cs:227` | Approval decision (fallback path) |
| 10 | `Enterprise/ApprovalService.cs:230` | Approval decision (final fallback) |
| 11 | `Enterprise/ErrorLogger.cs:200` | Mark error resolved |
| 12 | `Enterprise/ErrorLogger.cs:223` | Purge old error logs |
| 13 | `Enterprise/FrmApprovals.cs:595` | `RequireAdmin()` helper |
| 14 | `Enterprise/FrmTasks.cs:40` | Show "All Tasks" tab |
| 15 | `Enterprise/FrmRules.cs:376` | `RequireAdmin()` helper |
| 16 | `Enterprise/WorkflowService.cs:377` | Workflow transition fallback (rarely hit — `PermissionGate` installed) |
| 17 | `Enterprise/PermissionService.cs:213` | Change role permission (matrix edit) |
| 18 | `Enterprise/PermissionService.cs:238` | Change user permission exception |
| 19 | `Enterprise/TaskService.cs:155` | Assign task |
| 20 | `Enterprise/TaskService.cs:169` | Delete task |
| 21 | `Enterprise/ModuleService.cs:291` | Toggle module enable/disable |
| 22 | `Enterprise/LockService.cs:189` | Force-release record lock |
| 23 | `Enterprise/FrmWorkflowAdmin.cs:655` | `RequireAdmin()` helper |

## 1.4 `SecurityContext.IsSuperAdmin()` — 24 sites

| # | File:Line | Guarded Operation |
|---|---|---|
| 1 | `DevCenter/FrmDevCenter.cs:64` | Dev Center form construction guard |
| 2 | `DevCenter/FrmDevCenter.cs:111` | Dev Center activation guard |
| 3 | `DevCenter/DevCenterAccess.cs:49` | Dev Center hidden entry point |
| 4 | `DevCenter/DevCenterAccess.cs:101` | Dev Center keyboard-hook gate |
| 5 | `Accounting/AccRepair.cs:508` | Historical accounting data repair |
| 6 | `Accounting/FrmAccounting.cs:2077` | Open accounting repair tool |
| 7 | `Accounting/FrmAccounting.cs:2253` | Export accounting-only backup |
| 8 | `Accounting/FrmAccounting.cs:2275` | Import accounting-only backup |
| 9 | `FrmArchive.cs:462` | Permanent (unrecoverable) delete |
| 10 | `FrmDashboard.cs:225` | Show SuperAdmin-only dashboard controls |
| 11 | `FrmLogin.cs:766` | Post-login "All Centers" option |
| 12 | `FrmSettings.cs:248` | Show Centers tab |
| 13 | `FrmSettings.cs:255` | Show Backup tab |
| 14 | `FrmSettings.cs:1611` | Add center |
| 15 | `FrmSettings.cs:1649` | Update center |
| 16 | `FrmSettings.cs:1705` | Toggle center active |
| 17 | `FrmSettings.cs:1729` | Delete center |
| 18 | `FrmSettings.cs:2658` | Show license/security-sensitive section |
| 19 | `FrmSettings.cs:2708` | Update SuperAdmin credentials |
| 20 | `FrmSettings.cs:2964` | **Execute manual database backup** |
| 21 | `FrmSettings.cs:2991` | **Execute database restore** |
| 22 | `FrmUsers.cs:248` | Center selection logic for new user |
| 23 | `FrmUsers.cs:252` | Cross-center access check |
| 24 | `Helpers/BackupHelper.cs:131` | Legacy (non-GlobalID) restore guard |
| 25 | `Enterprise/ApprovalService.cs:216` | SuperAdmin always-approve short-circuit |
| 26 | `Enterprise/ModuleService.cs:109` | Module cache bypass for SuperAdmin |
| 27 | `Enterprise/PermissionService.cs:43` | `HasPermission()` SuperAdmin short-circuit (by design — do not remap) |

**Note on `PermissionService.cs` itself:** lines 43, 130, 135, 140 are the engine's own `LegacyFallback()` — this is the safety net Sprint 0 must preserve, not a call site to migrate.

---

# 2. Current-Check → Target-Permission-Key Mapping Table

Grouped by module. Keys marked **(existing)** are already seeded in `EnterpriseInitializer.EnsureDefaultPermissions()`. Keys marked **(NEW)** must be added.

| Current Check | File:Line | Target Permission Key | Status |
|---|---|---|---|
| `CanEdit()` | FrmCase.cs:1665, 1804 | `Case.Edit` | (existing) |
| `CanDelete()` | FrmCase.cs:1954 | `Case.Delete` | (existing) |
| — *(no check today)* | FrmCase.cs:2936 (`btnPrint_Click`) | `Case.Print` | **(NEW)** |
| — *(no check today)* | FrmCase.cs:2980 (`btnExportWord_Click`) | `Case.Export` | (existing key, **currently unused**) |
| — *(no check today)* | FrmCase.cs:3043 (`btnExportPdf_Click`) | `Case.Export` | (existing key, **currently unused**) |
| `CanEdit()` | FrmFamily.cs:934, 1028 | `Family.Edit` | **(NEW)** |
| `CanDelete()` | FrmFamily.cs:1186 | `Family.Delete` | **(NEW)** |
| — *(no check today)* | FrmFamily.cs:1313 (`btnPrint_Click`) | `Family.Print` | **(NEW)** |
| `CanEdit()` | FrmDocs.cs:435, 538 | `Docs.Edit` | **(NEW)** |
| `CanDelete()` | FrmDocs.cs:655 | `Docs.Delete` | **(NEW)** |
| — *(no check today)* | FrmDocs.cs:727 (`btnPrint_Click`) | `Docs.Print` | **(NEW)** |
| `CanEdit()` | FrmApplicant.cs:438 | `Applicant.Edit` | **(NEW)** |
| `CanDelete()` | FrmApplicant.cs:525 | `Applicant.Delete` | **(NEW)** |
| `CanEdit()` | FrmCaseRelations.cs:175 | `CaseRelation.Edit` | **(NEW)** |
| `CanDelete()` | FrmCaseRelations.cs:269 | `CaseRelation.Delete` | **(NEW)** |
| `CanEdit()` | FrmAssignMemberRole.cs:318 | `Family.Edit` *(reuse — same data)* | **(NEW)** |
| `CanDelete()` | FrmArchive.cs:353 | `Archive.Restore` | **(NEW)** |
| `IsSuperAdmin()` | FrmArchive.cs:462 | `Archive.PermanentDelete` | **(NEW)** |
| `CanDelete()` | FrmDashboard.cs:2856 | `Maintenance.CleanupFiles` | **(NEW)** |
| `CanDelete()` | FrmSettings.cs:219, 265, 594 | `Case.BulkDelete` | **(NEW)** |
| `CanEdit()` | FrmFinance.cs:393 | `Finance.Edit` | **(NEW — `Finance.View` exists, no write key)** |
| `CanEdit()` | Accounting/FrmAccounting.cs:758, 847 | `Accounting.Edit` | **(NEW — `Accounting.View` exists, no write key)** |
| `CanDelete()` | Accounting/FrmAccounting.cs:976 | `Accounting.Reverse` | **(NEW)** |
| `IsSuperAdmin()` | Accounting/FrmAccounting.cs:2077, AccRepair.cs:508 | `Accounting.Repair` | **(NEW)** |
| `IsSuperAdmin()` | Accounting/FrmAccounting.cs:2253, 2275 | `Accounting.Backup` | **(NEW)** |
| — *(no check today)* | FrmReportBuilder.cs, ReportDefinitions.cs | `Report.Run`, `Report.Export` | **(NEW)** |
| — *(no check today)* | GuardianCardIntegration (batch print, template manager) | `GuardianCard.Print`, `GuardianCard.ManageTemplates` | **(NEW)** |
| — *(no check today)* | AssistanceReceiptIntegration (batch/single print) | `AssistanceReceipt.Print` | **(NEW)** |
| — *(no check today)* | FrmVersions.cs | `Version.View` | (existing key, **currently unused**) |
| — *(no check today)* | FrmSecurityAudit.cs | `Security.View` | (existing key, **currently unused**) |
| — *(no check today)* | FrmErrorLog.cs (view) | `Error.View` | (existing key, **currently unused**) |
| `IsAdmin()` | ErrorLogger.cs:200, 223 | `Error.Manage` | **(NEW — view vs manage split)** |
| `IsAdmin()` | FrmUsers.cs:216, 314, 370 | `User.Manage` | (existing) |
| `IsSuperAdmin()` | FrmSettings.cs:1611/1649/1705/1729 | `Center.Manage` | **(NEW)** |
| `IsSuperAdmin()` | FrmSettings.cs:2964 | `Backup.Create` | **(NEW)** |
| `IsSuperAdmin()` | FrmSettings.cs:2991, BackupHelper.cs:131 | `Backup.Restore` | **(NEW)** |
| `IsAdmin()` | TaskService.cs:155, 169 | `Task.Manage` | (existing) |
| `IsAdmin()` | LockService.cs:189 | `Lock.Override` | (existing) |
| `IsAdmin()` | ModuleService.cs:291 | `Module.Manage` | (existing) |
| `IsAdmin()` | PermissionService.cs:213, 238 | `Permission.Manage` | (existing) |
| `CanEdit()` | Sync/FrmSyncWizard.cs:1400 | `Sync.Execute` | **(NEW)** |
| `CanEdit()` | Sync/MediaSyncEngine.cs:52 | `Sync.Execute` *(reuse)* | **(NEW)** |
| `IsSuperAdmin()` | DevCenter/*.cs (×3) | *(not remapped — see §3 note)* | N/A |
| `IsSuperAdmin()` | FrmSettings.cs:2708 (update SuperAdmin creds), FrmLogin.cs:766, ApprovalService.cs:216, ModuleService.cs:109, PermissionService.cs:43 | *(not remapped — hard-coded by design)* | N/A |

**Design note (do not remap):** `IsSuperAdmin()` checks whose entire *purpose* is "nothing can ever override this" (`PermissionService.cs:43`, the Dev Center gates, the SuperAdmin-credential-update gate, module-cache bypass) must **stay as direct role checks**. `EnterpriseInitializer.cs:704` already encodes this rule in its own comment: SuperAdmin permissions are seeded but intentionally non-editable in `FrmPermissionMatrix`. Converting these to permission keys would let an `Admin` with `Permission.Manage` grant themselves Dev Center access — a privilege-escalation regression. This mirrors the audit's own §6.10 finding about why SuperAdmin grants are locked in `PermissionService.SetRolePermission`.

---

# 3. Permission Key Validation

## 3.1 Seeded Keys (24) — currently in `EntPermission` via `EnterpriseInitializer.EnsureDefaultPermissions()`

`Case.View, Case.Create, Case.Edit, Case.Delete, Case.Export, Workflow.View, Workflow.Review, Workflow.Approve, Workflow.Manage, Approval.Decide, Approval.Manage, Task.View, Task.Manage, Rule.Manage, Lock.Override, Security.View, Error.View, Version.View, User.Manage, Permission.Manage, Module.Manage, Settings.Manage, Finance.View, Accounting.View`

## 3.2 Reachability Audit of Seeded Keys

| Key | Reachable Today? | Finding |
|---|---|---|
| `Case.Export` | 🔴 **No** | Seeded, has role defaults, but **zero code calls `PermissionService` for export/print** — dead key |
| `Version.View` | 🔴 **No** | Seeded but `FrmVersions.cs` has 0 `PermissionService`/`SecurityContext` references — anyone can open it |
| `Security.View` | 🔴 **No** | Seeded but `FrmSecurityAudit.cs` has 0 references — anyone can open it |
| `Error.View` | 🔴 **No** | Seeded but `FrmErrorLog.cs` view path has 0 references (only `ErrorLogger.MarkResolved`/purge check `IsAdmin()`, not this key) |
| `Case.Create` | ⚠ **Partial** | No distinct "create" code path exists — `FrmCase` uses one `CanEdit()` gate for both insert and update, so this key is seeded but has no corresponding call site to attach to (only `Case.Edit` does) |
| `Finance.View`, `Accounting.View` | ⚠ **Partial** | Seeded and used as *labels* on the module sidebar (`ModuleService`), but never passed to `PermissionService.HasPermission()` — the actual read access to these screens is gated only by `ModuleService.IsEnabled()`, a coarser on/off switch, not this key |
| `Workflow.*`, `Approval.Decide`, `Task.Manage`, `Rule.Manage`, `Lock.Override`, `Module.Manage`, `Permission.Manage`, `User.Manage` | ✅ **Reachable** | Actually consumed via `WorkflowService.PermissionGate` or directly compared in service-layer guard clauses |

## 3.3 Missing Keys — 24 to add (see §2 "(NEW)" column for full list)

Summary by category: `Family.*` (2), `Docs.*` (3), `Applicant.*` (2), `CaseRelation.*` (2), `Case.Print` (1), `Archive.*` (2), `Maintenance.CleanupFiles` (1), `Case.BulkDelete` (1), `Finance.Edit` (1), `Accounting.Edit/Reverse/Repair/Backup` (4), `Report.Run/Export` (2), `GuardianCard.*` (2), `AssistanceReceipt.Print` (1), `Error.Manage` (1), `Center.Manage` (1), `Backup.Create/Restore` (2), `Sync.Execute` (1).

## 3.4 Duplicate Keys

None found — `EntPermission.PermKey` has a `UNIQUE` constraint and `AddPermission()` is called with 24 distinct literal strings. ✅ Clean.

## 3.5 Unused Keys

`Case.Export`, `Version.View`, `Security.View`, `Error.View` (see §3.2) — seeded, granted per-role, displayed in `FrmPermissionMatrix`, **and functionally inert**. This is the exact "false assurance" pattern the audit flagged as SEC-001, just on four additional keys not previously catalogued.

## 3.6 Unreachable Keys

None beyond the "unused" set above — no key exists that *cannot* be reached by any code path in principle; the gap is entirely "not yet wired," not "structurally dead."

---

# 4. Operations With NO Permission Check At All

Re-scanned specifically for Export / Print / Reports / History / Backup / Restore / Sync / Accounting, per the request. This category is **more severe** than the CRUD remap because these operations currently have **no gate whatsoever** — not even the crude `SecurityContext` role check. Any authenticated user, including `Viewer`, can execute them.

| # | Operation | File | Confirmed By |
|---|---|---|---|
| 1 | Print case dossier | `FrmCase.cs:2936` `btnPrint_Click` | Only guards `currentCaseId == 0`; no role/permission check |
| 2 | Export case to Word | `FrmCase.cs:2980` `btnExportWord_Click` | Same |
| 3 | Export case to PDF | `FrmCase.cs:3043` `btnExportPdf_Click` | Same |
| 4 | Print family member card | `FrmFamily.cs:1313` `btnPrint_Click` | No `SecurityContext`/`PermissionService` reference in file for this handler |
| 5 | Print document | `FrmDocs.cs:727` `btnPrint_Click` | Same |
| 6 | Print barcode label | `FrmBarcode.cs:143` `btnPrint_Click` | Same |
| 7 | **Dynamic Report Builder** — run/filter/export to Excel | `FrmReportBuilder.cs`, `Helpers/ReportDefinitions.cs` | 1 reference in each file, and both are audit-trail writes (`SecurityContext.Username`, `SecurityContext.CenterFilterId`) — **not** an authorization check |
| 8 | Guardian card batch print | `GuardianCardIntegration/FrmGuardianCardBatchPrint.cs` | 0 `SecurityContext`/`PermissionService` matches |
| 9 | Guardian card template management | `GuardianCardIntegration/FrmCardTemplateManager.cs` | 0 matches (opening the form itself is gated by `IsAdmin()` at `FrmDashboard.cs:2844`, but nothing re-checks once inside — a Manager/Operator reached via a saved shortcut or future refactor would have no gate) |
| 10 | Assistance receipt printing (single/filtered/package batch) | `AssistanceReceiptIntegration/*.cs` (3 files) | 0 matches |
| 11 | **Version / change history viewer** | `Enterprise/FrmVersions.cs` | 0 matches — seeded `Version.View` key exists but is never queried |
| 12 | **Security audit log viewer** | `Enterprise/FrmSecurityAudit.cs` | 0 matches — seeded `Security.View` key exists but is never queried |
| 13 | **Error log viewer** (read path) | `Enterprise/FrmErrorLog.cs` | 0 matches — seeded `Error.View` key exists but is never queried |

**Root cause for #11–13:** these three screens are gated only at the sidebar-menu level via `ModuleService.AddModuleNav(...)` (`FrmDashboard.cs:154-156`). `ModuleService.IsEnabled()` defaults every module to **enabled for every role** unless an admin explicitly disables it (`EnterpriseInitializer.EnsureDefaultModules` seeds no role-level restrictions). So today, a fresh install's `Operator` and `Viewer` accounts can both see and open Security Audit, Error Log, and Version History from the sidebar — screens that expose failed-login attempts, permission-denial events, and every historical edit to every case.

**Backup/Restore/Accounting-Backup/Sync status:** these *do* have a gate — all four are behind `IsSuperAdmin()` (`FrmSettings.cs:2964/2991`, `FrmAccounting.cs:2253/2275`) or `CanEdit()` (`FrmSyncWizard.cs:1400`, `MediaSyncEngine.cs:52`). They are not in the "zero check" category, but they are in the "wrong-grained check" category (§2) — they should move to `Backup.Create`/`Backup.Restore`/`Accounting.Backup`/`Sync.Execute` keys so an admin can grant "run sync" to an Operator without also granting full SuperAdmin.

---

# 5. Final Permission Model — 6 Roles

## 5.1 Roles

| Role | Definition | Relationship to Current System |
|---|---|---|
| **SuperAdmin** | Unrestricted, cross-center, non-revocable | Unchanged |
| **Admin** | Full branch administration: users, settings, centers*, backup/restore | Unchanged in scope, but re-expressed as explicit keys instead of `IsAdmin()`/`IsSuperAdmin()` string checks |
| **Manager** ⭐ NEW | Branch operations lead: full case/family/docs/finance CRUD + approvals + reports, **no** user/settings/center/backup administration | Currently does not exist — closest today is `Admin` minus system administration |
| **Finance** ⭐ NEW | Accounting + per-case assistance: full `Accounting.*` and `Finance.*`, read-only on cases for reference, reports | Currently does not exist — closest today is `Admin` (over-privileged) or `Operator` (under-privileged, no accounting write) |
| **Operator** | Field data entry: create/edit case/family/docs/applicant, no delete, no admin | Unchanged |
| **Viewer** | Read-only + export | Unchanged |

*Center administration (add/edit/delete branch centers) is recommended to **stay SuperAdmin-only** even for `Admin` — see §5.3 note.

## 5.2 Role × Permission Matrix

Legend: ✅ granted · ➖ not granted. Grouped by category.

### Case / Family / Docs / Applicant

| Permission Key | SuperAdmin | Admin | Manager | Finance | Operator | Viewer |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `Case.View` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Case.Create` | ✅ | ✅ | ✅ | ➖ | ✅ | ➖ |
| `Case.Edit` | ✅ | ✅ | ✅ | ➖ | ✅ | ➖ |
| `Case.Delete` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Case.Export` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Case.Print` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Case.BulkDelete` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Family.Edit` / `Family.Print` | ✅ | ✅ | ✅ | ➖ / ✅ | ✅ / ✅ | ➖ / ✅ |
| `Family.Delete` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Docs.Edit` / `Docs.Print` | ✅ | ✅ | ✅ | ➖ / ✅ | ✅ / ✅ | ➖ / ✅ |
| `Docs.Delete` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Applicant.Edit` | ✅ | ✅ | ✅ | ➖ | ✅ | ➖ |
| `Applicant.Delete` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `CaseRelation.Edit` / `.Delete` | ✅ | ✅ | ✅ | ➖ | ✅ / ➖ | ➖ |
| `Archive.Restore` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Archive.PermanentDelete` | ✅ | ➖ | ➖ | ➖ | ➖ | ➖ |
| `Maintenance.CleanupFiles` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |

### Finance / Accounting

| Permission Key | SuperAdmin | Admin | Manager | Finance | Operator | Viewer |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `Finance.View` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Finance.Edit` | ✅ | ✅ | ✅ | ✅ | ➖ | ➖ |
| `Accounting.View` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Accounting.Edit` | ✅ | ✅ | ➖ | ✅ | ➖ | ➖ |
| `Accounting.Reverse` | ✅ | ✅ | ➖ | ✅ | ➖ | ➖ |
| `Accounting.Repair` | ✅ | ➖ | ➖ | ➖ | ➖ | ➖ |
| `Accounting.Backup` | ✅ | ➖ | ➖ | ➖ | ➖ | ➖ |

*Manager deliberately excluded from `Accounting.Edit`/`Reverse` — Manager owns case operations, Finance owns the ledger; this separation of duties is itself a control the current 4-role system cannot express.*

### Reports / History / Security

| Permission Key | SuperAdmin | Admin | Manager | Finance | Operator | Viewer |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `Report.Run` / `Report.Export` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Version.View` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Security.View` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Error.View` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Error.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `GuardianCard.Print` / `AssistanceReceipt.Print` | ✅ | ✅ | ✅ | ✅ | ✅ | ➖ |
| `GuardianCard.ManageTemplates` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |

### Workflow / Governance / System (unchanged from current defaults)

| Permission Key | SuperAdmin | Admin | Manager | Finance | Operator | Viewer |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `Workflow.View` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Workflow.Review` | ✅ | ✅ | ✅ | ➖ | ✅ | ➖ |
| `Workflow.Approve` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Workflow.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Approval.Decide` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Approval.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Task.View` / `Task.Manage` | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ➖ | ✅ / ➖ | ✅ / ➖ |
| `Rule.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Lock.Override` | ✅ | ✅ | ✅ | ➖ | ➖ | ➖ |
| `Sync.Execute` | ✅ | ✅ | ✅ | ➖ | ✅ | ➖ |
| `User.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Permission.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Module.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Settings.Manage` | ✅ | ✅ | ➖ | ➖ | ➖ | ➖ |
| `Center.Manage` | ✅ | ➖ | ➖ | ➖ | ➖ | ➖ |
| `Backup.Create` / `Backup.Restore` | ✅ | ➖ | ➖ | ➖ | ➖ | ➖ |

## 5.3 Notes on Contested Defaults

- **`Center.Manage`, `Backup.*` restricted to SuperAdmin only, not Admin:** the current code already gates these with `IsSuperAdmin()`, not `IsAdmin()`. Widening to `Admin` would be a **privilege increase**, not a neutral remap. Recommend keeping SuperAdmin-only in Wave 1 seeding; a product owner can widen later via `FrmPermissionMatrix` without a code change.
- **`Archive.PermanentDelete` restricted to SuperAdmin only:** same reasoning — today's code already checks `IsSuperAdmin()` here (`FrmArchive.cs:462`), not `IsAdmin()`/`CanDelete()`.
- Everywhere else, the **default grants for `Admin`/`Operator`/`Viewer` are set to exactly reproduce current `SecurityContext` behavior** — this is the load-bearing backward-compatibility rule already established in `EnterpriseInitializer.EnsureDefaultPermissions()`'s own comment (line 654-658) and must not be broken.

## 5.4 Finance Role — Full Action List (Task 7)

`Case.View`, `Case.Export`, `Case.Print` (read/reference only — no case edit), `Finance.View`, `Finance.Edit` (register/edit assistance), `Accounting.View`, `Accounting.Edit`, `Accounting.Reverse`, `Report.Run`, `Report.Export`, `GuardianCard.Print`, `AssistanceReceipt.Print`, `Task.View`, `Workflow.View`, `Sync.Execute` — ➖ **not**: any case/family/docs write, `Accounting.Repair`, `Accounting.Backup`, `User.Manage`, `Center.Manage`, `Backup.*`, `Approval.Decide`, `Workflow.Approve`.

## 5.5 Manager Role — Full Action List (Task 8)

`Case.*` (View/Create/Edit/Delete/Export/Print/BulkDelete: all except BulkDelete), `Family.*`, `Docs.*`, `Applicant.*`, `CaseRelation.*`, `Archive.Restore` (not PermanentDelete), `Workflow.Review`, `Workflow.Approve`, `Approval.Decide`, `Task.View`/`Task.Manage`, `Lock.Override`, `Sync.Execute`, `Finance.View` (not Edit), `Accounting.View` (not Edit/Reverse), `Version.View`, `Report.Run`/`Export`, `GuardianCard.Print` — ➖ **not**: `Accounting.Edit/Reverse/Repair/Backup`, `User.Manage`, `Permission.Manage`, `Module.Manage`, `Settings.Manage`, `Center.Manage`, `Backup.*`, `Security.View`, `Error.*`, `Rule.Manage`, `Workflow.Manage`, `Approval.Manage`.

---

# 6. Implementation Waves

Each wave is scoped to be independently shippable and independently revertible (§9). Effort assumes 1 developer familiar with the codebase, matching the audit's own estimation basis.

## Wave 1 — Foundation (roles + keys, zero enforcement change)

| | |
|---|---|
| **Goal** | Add `Manager`/`Finance` roles and all 24 missing permission keys to the schema, with defaults that exactly preserve current behavior. **No call site changed.** |
| **Files** | `Enterprise/EnterpriseInitializer.cs` (extend `AddPermission` to 6 role columns; add 24 `AddPermission(...)` calls), `Enterprise/PermissionService.cs` (`GetRoles()` → return 6 roles), `FrmUsers.cs` (`_cmbRole.Items.AddRange` → add Manager/Finance `RoleItem`s) |
| **Methods touched** | `EnsureDefaultPermissions`, `AddPermission`, `GetRoles`, `FrmUsers` role combo construction |
| **Estimated changes** | ~40 new lines (24 `AddPermission` calls + 2 signature params), 1 combo-box edit |
| **Risk** | 🟢 **Low** — purely additive `INSERT OR IGNORE` rows; no existing call site changes; `FrmPermissionMatrix` automatically grows two columns via `GetRoles()` |

## Wave 2 — Close the zero-check gaps (read-tier + export/print)

| | |
|---|---|
| **Goal** | Wire `PermissionService.Require()` into the 13 operations identified in §4 that currently have **no check at all**, defaulting every role's grant to match today's de-facto "anyone logged in" behavior — so nothing breaks on day one, but it becomes configurable and auditable. |
| **Files** | `FrmCase.cs` (3 handlers), `FrmFamily.cs` (1), `FrmDocs.cs` (1), `FrmBarcode.cs` (1), `FrmReportBuilder.cs`, `Helpers/ReportDefinitions.cs`, `Enterprise/FrmVersions.cs`, `Enterprise/FrmSecurityAudit.cs`, `Enterprise/FrmErrorLog.cs`, `GuardianCardIntegration/FrmGuardianCardBatchPrint.cs`, `GuardianCardIntegration/FrmCardTemplateManager.cs`, `AssistanceReceiptIntegration/*.cs` (3 files) |
| **Methods touched** | `btnPrint_Click`, `btnExportWord_Click`, `btnExportPdf_Click` and equivalents; form `Load`/constructor guards for the 3 Enterprise viewers |
| **Estimated changes** | ~16 files, 1 `PermissionService.Require(...)` guard clause each (~2-4 lines per site) |
| **Risk** | 🟡 **Low-medium** — mechanically simple, but each default grant must be verified against §5.2 before merge; an over-restrictive default here is a visible regression (a Viewer who could print yesterday and can't today) |

## Wave 3 — Core CRUD remap (Case / Family / Docs / Applicant / Relations)

| | |
|---|---|
| **Goal** | Replace the 15 `CanEdit()` and 13 `CanDelete()` call sites in core case-management forms with `PermissionService.Require(key, entity, id)`, per the §2 mapping. `SecurityContext` remains as the engine's built-in `LegacyFallback` — untouched. |
| **Files** | `FrmCase.cs`, `FrmFamily.cs`, `FrmDocs.cs`, `FrmApplicant.cs`, `FrmCaseRelations.cs`, `FrmAssignMemberRole.cs`, `FrmArchive.cs` |
| **Methods touched** | `btnSave_Click`, `btnEdit_Click`, `btnDelete_Click` and named equivalents across 7 forms (25 call sites) |
| **Estimated changes** | 25 one-line guard-clause replacements (`SecurityContext.CanX()` → `PermissionService.HasPermission("Key")`), no structural change |
| **Risk** | 🟠 **Medium** — highest call-site count; regression here means a data-entry operator cannot save a case, which stops field work. Requires the full regression matrix (§7) run against all 6 roles before merge. |

## Wave 4 — Finance, Accounting, Backup, Restore, Sync

| | |
|---|---|
| **Goal** | Remap the money-adjacent and disaster-recovery-adjacent call sites: `FrmFinance`, `FrmAccounting` (6 sites), `AccRepair`, `FrmSettings` backup/restore/center tabs, `BackupHelper`, `Sync/FrmSyncWizard`, `Sync/MediaSyncEngine`. |
| **Files** | `FrmFinance.cs`, `Accounting/FrmAccounting.cs`, `Accounting/AccRepair.cs`, `FrmSettings.cs` (backup + center sections), `Helpers/BackupHelper.cs`, `Sync/FrmSyncWizard.cs`, `Sync/MediaSyncEngine.cs` |
| **Methods touched** | `SaveTransaction`, `DeleteSelectedTxn`, `BeginReviseSelectedTxn`, `OpenRepairTool`, `ExportAccountingBackup`, `ImportAccountingBackup`, `BtnBackupNow_Click`, `BtnRestoreBackup_Click`, `BtnCenter*_Click` (×4), sync write paths |
| **Estimated changes** | ~14 call sites |
| **Risk** | 🔴 **High** — touches financial reversal and full-database restore, both explicitly called out in `CLAUDE.md` as production-critical ("never sacrifice stability"). Requires dedicated test pass beyond the standard regression checklist (§7.3). |

## Wave 5 — Administration (Users / Centers / Modules / DevCenter / Enterprise governance)

| | |
|---|---|
| **Goal** | Remap the remaining `IsAdmin()`/`IsSuperAdmin()` sites governing users, modules, tasks, locks, errors, approvals, and workflow admin — the "keys to the kingdom" tier. |
| **Files** | `FrmUsers.cs`, `Enterprise/TaskService.cs`, `Enterprise/LockService.cs`, `Enterprise/ModuleService.cs`, `Enterprise/ErrorLogger.cs`, `Enterprise/ApprovalService.cs`, `Enterprise/FrmApprovals.cs`, `Enterprise/FrmRules.cs`, `Enterprise/FrmTasks.cs`, `Enterprise/FrmWorkflowAdmin.cs` |
| **Methods touched** | `BtnAdd_Click`/`BtnToggle_Click`/`BtnDelete_Click` (Users), `Assign`/`Delete` (Task), `ForceRelease` (Lock), `EnsureManageable` (Module), `MarkResolved`/purge (Error), `RequireAdmin()` helpers (×3) |
| **Estimated changes** | ~15 call sites |
| **Risk** | 🟠 **Medium-high** — a mapping mistake here risks locking administrators out of the tools needed to fix a mapping mistake. Do this wave **last**, once Waves 1-4 have proven the pattern is safe, and keep a SuperAdmin test account verified working after every commit. `DevCenter` and hard-coded `IsSuperAdmin()` sites (§2 "do not remap" list) are explicitly **out of scope** for this wave. |

---

# 7. Regression Checklist

## 7.1 Automated Baseline (must not regress)

- [ ] Full suite still **346 tests / 344 pass / 2 skip / 0 fail** (current baseline per `SYSTEM_AUDIT_REPORT.md` §3.4) — any new failure is a blocker, not a "pre-existing" issue
- [ ] Clean MSBuild compile, 0 new errors, warning count not increased
- [ ] `FrmPermissionMatrix` renders 6 role columns without exception
- [ ] `FrmUsers` role dropdown shows Manager/Finance and successfully persists a user with each

## 7.2 Per-Role Manual Pass (repeat for all 6 roles × each wave's affected screens)

| Role | Must be able to | Must NOT be able to |
|---|---|---|
| SuperAdmin | Everything, with zero exceptions, including after any permission-matrix edit | — |
| Admin | All case/family/docs/finance/accounting ops, user/module/settings mgmt, backup, restore | Center management (per §5.3), Dev Center |
| Manager | Case/family/docs/applicant full CRUD, approvals, reports, sync | Accounting edit/reverse, user mgmt, backup/restore, settings |
| Finance | Accounting CRUD, assistance registration, reports, case view/export | Case/family/docs edit or delete, user mgmt |
| Operator | Case/family/docs/applicant create+edit, sync | Delete anything, accounting write, admin screens |
| Viewer | View, export, print everything readable | Any write, any delete, any admin screen |

## 7.3 Specific Regression Risks

- [ ] **SuperAdmin lockout test** — after seeding + every wave, confirm SuperAdmin retains 100% access (test explicitly; this is the one failure mode that cannot be fixed via the UI if it occurs, since `FrmPermissionMatrix` itself requires `Permission.Manage`)
- [ ] **Identical-behavior diff** — for every existing key (`Case.Edit`, `Case.Delete`, `User.Manage`, etc.), the enabled/disabled state of the corresponding button/menu for `Admin`/`Operator`/`Viewer` must be pixel-identical before and after the wave (no accidental widening or narrowing)
- [ ] **New-role isolation** — Manager and Finance must not inherit any permission through a missed boolean in the extended `AddPermission()` call (24 keys × 2 new columns = 48 new booleans to get right — the highest transcription-error risk in the whole plan)
- [ ] **Cache invalidation** — confirm `PermissionService.InvalidateCache()` fires wherever `EnterpriseInitializer` re-seeds on app upgrade for an already-provisioned database, so an existing install doesn't need a full app restart to see new keys (verify via `EnsureEnterpriseObjects()` ordering)
- [ ] **Center isolation orthogonality** — confirm `CenterGuard`/`CenterFilterId` behavior is unaffected by any Wave (permission and center-scoping are separate axes; a bug that conflates them would leak cross-branch data)
- [ ] **`FrmPermissionMatrix` and `FrmModules` still function** — both read `GetRoles()`; confirm they don't assume exactly 4 roles anywhere else (check for hard-coded column counts or width calculations)
- [ ] **Workflow/Approval fallback paths** — `WorkflowService.cs:377`, `ApprovalService.cs:227/230` fall back to `IsAdmin()` only when `PermissionGate` is null; confirm `PermissionService.Install()` still runs before any workflow transition can occur, in every code path (including tests that call `SecurityContext.SignIn` directly without full app bootstrap)
- [ ] **Accounting reversal audit trail** — `Accounting.Reverse` must still write to `AccAudit` exactly as `CanDelete()` did today (Wave 4)
- [ ] **Backup/restore still functional end-to-end** — run one real backup + restore cycle manually after Wave 4, independent of unit tests (per audit GAP-01, restore has zero direct test coverage today)

---

# 8. Rollback Plan

1. **Wave-per-commit discipline.** Each wave (§6) lands as its own commit (or small commit series) so any single wave can be reverted with `git revert` without touching the others. Do not squash waves together.
2. **Schema changes are additive-only and self-safe.** All new `EntPermission`/`EntRolePermission` rows use `INSERT OR IGNORE` — a rollback of the *code* (reverting the C# changes) leaves harmless unused rows in the database; no destructive migration is ever required to undo Wave 1.
3. **Operational rollback without a deploy.** Because default grants in Waves 2-5 are calibrated to reproduce current behavior exactly, if a specific role/permission combination is found wrong in production, an Admin can correct it live via `FrmPermissionMatrix` — no code rollback needed for a single-key mistake. Reserve `git revert` for structural problems (wrong key attached to wrong call site, missing guard clause).
4. **The engine's built-in safety net must remain intact.** `PermissionService.HasPermission()` already wraps its cache lookup in `try/catch` and falls back to `LegacyFallback()` (role-string logic) on any exception (`PermissionService.cs:56-60`). Sprint 0 must not remove or weaken this — it means a bug in the new `EntRolePermission` query degrades to today's behavior instead of hard-locking users out. Verify this path is still exercised (e.g., a temporarily corrupt `EntRolePermission` row) as part of Wave 1 testing.
5. **Tag before each wave.** `git tag pre-wave-N` immediately before merging wave *N*, so a bad wave can be rolled back to a known-good point even if later commits have already landed on top (cherry-pick the good commits forward rather than reverting in place, if waves 4/5 have already started).
6. **SuperAdmin escape hatch.** Confirm at least one SuperAdmin account's credentials are documented/available to the team before starting Wave 5 — this is the account that must be used to fix any permission-matrix mistake made during that wave, and it is the one role never gated by the matrix itself.

---

# 9. Final Recommendation

**Should Sprint 0 be implemented exactly as proposed, or should priorities change?**

**Recommend proceeding, with two sequencing changes:**

1. **Reorder by actual risk, not by module familiarity.** The zero-check gaps found in §4 — unrestricted printing/export of case dossiers containing Tazkira numbers and minors' photos, and unrestricted read access to the Security Audit and Error Log screens — are a **larger and more surprising** exposure than the coarse-but-*enforced* `SecurityContext` CRUD system, because today literally nothing is stopping a `Viewer` from opening the Security Audit log. Wave 2 (close zero-check gaps) should be treated as equal priority to Wave 3 (CRUD remap), not as an afterthought to it.

2. **Decouple "add two new roles" from "enforce 72 call sites."** Introducing `Manager` and `Finance` *and* flipping the enforcement mechanism for the entire system in the same sprint multiplies risk combinatorially — a mapping error and a new-role transcription error become indistinguishable when both change at once. Wave 1 (§6) already isolates this: **seed the new roles first, with defaults identical to their nearest existing analog, and only differentiate them once enforcement (Waves 2-4) is proven stable.** This matches the audit's own Phase-2 recommendation (§18, item 2.5) to "keep `SecurityContext` as fallback" — the same conservatism should extend to the two new roles.

**On scope:** Waves 1-3 (foundation, zero-check closure, core CRUD) are a reasonable Sprint 0. **Recommend deferring Waves 4-5 (Finance/Accounting/Backup/Sync, and Admin/User/Center/DevCenter) to Sprint 1**, gated on Sprint 0's regression pass being clean, because:
- They carry the highest blast radius (money reversal, full-database restore) per `CLAUDE.md`'s explicit stability-over-features priority.
- They are the two waves most likely to reveal that a permission key was scoped wrong (e.g., should `Manager` really be excluded from `Accounting.View`? that's a business decision, not a technical one, and deserves its own sign-off cycle rather than being bundled into a mechanical remap sprint).

**Net effect of this resequencing:** the same total scope the user requested still ships, but the highest-uncertainty business decisions (new-role permission boundaries, money-adjacent enforcement) are isolated from the highest-volume mechanical work (72-call-site remap), so a problem in one does not block or contaminate the other.

---

*End of implementation package. No code was modified, no commits were created, in accordance with the task constraints.*
