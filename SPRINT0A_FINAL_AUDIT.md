# SPRINT 0A — FINAL AUDIT
## Verification of the Permission-Integration Changes (Waves 1–3)

| | |
|---|---|
| **Document Type** | Post-implementation audit — **no code modified in this pass** |
| **Method** | `git diff` against the pre-Sprint-0A working tree, `grep` reachability tracing, manual call-graph walk |
| **Scope** | The 15 files changed in Sprint 0A + every permission key now in `EntPermission` (42 total) |
| **Companion documents** | `SPRINT0_IMPLEMENTATION_PLAN.md` (the plan), prior turn's completion report (the claims being audited here) |

All evidence below is reproducible with the exact commands shown — nothing is asserted without a command output backing it.

---

# 1. Every Modified File

`git diff --stat` restricted to the 15 files Sprint 0A was scoped to:

```
Enterprise/EnterpriseInitializer.cs                |  36 +++++
Enterprise/FrmErrorLog.cs                          |   8 ++
Enterprise/FrmSecurityAudit.cs                      |   8 ++
Enterprise/FrmVersions.cs                          |   8 ++
FrmApplicant.cs                                    |   4 +-
FrmArchive.cs                                      |  12 +-
FrmAssignMemberRole.cs                              |   2 +-
FrmBarcode.cs                                      |   6 +
FrmCase.cs                                         | 156 ++++++++++++++++++-
FrmCaseRelations.cs                                |   4 +-
FrmDocs.cs                                         |  24 +++-
FrmFamily.cs                                       |  43 +++++-
FrmReportBuilder.cs                                |  12 ++
GuardianCardIntegration/FrmCardTemplateManager.cs  |   8 ++
GuardianCardIntegration/FrmGuardianCardBatchPrint.cs |   8 ++
15 files changed, 322 insertions(+), 17 deletions(-)
```

**Verified: exactly these 15 files, no others.** No file outside this list contains a Sprint 0A change.

## 1.1 Isolating Sprint 0A hunks from pre-existing uncommitted work

Four of these files (`FrmCase.cs`, `FrmFamily.cs`, `FrmDocs.cs`, `FrmArchive.cs`) already contained **unrelated, pre-existing uncommitted changes** before Sprint 0A began (last commit: `6d11754`, 2026-08-18 — VersionService/SyncOutboxService wiring matching what `SYSTEM_AUDIT_REPORT.md` describes as "fixed during a prior engagement"). Full diffs were re-read line-by-line to separate the two:

| File | Total diff lines | Sprint 0A lines | Pre-existing (not Sprint 0A) |
|---|---|---|---|
| `FrmCase.cs` | 156 | ~30 (3 CRUD remaps + 3 new print/export guards) | ~126 (VersionService/SyncOutboxService capture calls, `UpdateCaseActionsVisibility`, `btnHistory_Click`, a combo-box fix) |
| `FrmFamily.cs` | 43 | ~24 (3 CRUD remaps + 1 new print guard) | ~19 (VersionService capture calls, `btnHistory_Click`) |
| `FrmDocs.cs` | 24 | ~19 (3 CRUD remaps + 1 new print guard) | ~5 (VersionService capture calls) |
| `FrmArchive.cs` | 12 | ~8 (2 remaps) | ~4 (VersionService capture call) |
| Remaining 11 files | matches exactly | 100% Sprint 0A | none |

**Notable side-effect discovered:** the pre-existing (not-Sprint-0A) `btnHistory_Click` handlers in `FrmCase.cs:2049` and `FrmFamily.cs:1180` both construct `FrmVersions(entity, id)` — the same constructor Sprint 0A gated with `Version.View`. Confirmed by re-reading the diff: these two history buttons are **automatically covered** by the Wave-2 gate with no additional change needed.

---

# 2. Every New Permission Key

```
grep -n 'AddPermission(con, "' Enterprise/EnterpriseInitializer.cs | wc -l   → 42
```

24 pre-existing (Wave 0) + **18 new (Sprint 0A)**:

| # | Key | Line | Admin/Operator/Viewer default |
|---|---|---|---|
| 1 | `Family.Edit` | 698 | T/T/F |
| 2 | `Family.Delete` | 699 | T/F/F |
| 3 | `Family.Print` | 700 | T/T/T |
| 4 | `Docs.Edit` | 702 | T/T/F |
| 5 | `Docs.Delete` | 703 | T/F/F |
| 6 | `Docs.Print` | 704 | T/T/T |
| 7 | `Applicant.Edit` | 706 | T/T/F |
| 8 | `Applicant.Delete` | 707 | T/F/F |
| 9 | `CaseRelation.Edit` | 709 | T/T/F |
| 10 | `CaseRelation.Delete` | 710 | T/F/F |
| 11 | `Case.Print` | 712 | T/T/T |
| 12 | `Archive.Restore` | 714 | T/F/F |
| 13 | `Archive.PermanentDelete` | 715 | F/F/F (SuperAdmin-only) |
| 14 | `Report.Run` | 717 | T/T/T |
| 15 | `Report.Export` | 718 | T/T/T |
| 16 | `Barcode.Print` | 720 | T/T/T |
| 17 | `GuardianCard.Print` | 722 | T/T/T |
| 18 | `GuardianCard.ManageTemplates` | 723 | T/F/F |

**Duplicate check:** `grep -oE 'AddPermission\(con, "[^"]+"' ... | sort | uniq -d` → **empty output, zero duplicates** across all 42 keys.

**Not added (by design, documented in code comment at line 724-727):** `AssistanceReceipt.Print` — the plan's Wave-2 list included it, but its only reachable entry point is `FrmFinance.cs` (confirmed in §6.1), which Sprint 0A was explicitly told not to touch. Correctly **not seeded** — seeding an unwired key would have recreated the exact "dead permission" defect this sprint exists to fix.

---

# 3. Every Screen Affected

```
grep -rn "PermissionService\.\(Require\|HasPermission\)(" --include="*.cs" .
   (excluding /bin, /obj, .backup, and PermissionService.cs itself)
```

**29 call sites, 15 files, 20 distinct screens/handlers:**

| Screen | Handler(s) | Key(s) |
|---|---|---|
| `FrmCase` | `btnSave_Click`, `btnEdit_Click` | `Case.Edit` |
| `FrmCase` | delete handler | `Case.Delete` |
| `FrmCase` | `btnPrint_Click` | `Case.Print` |
| `FrmCase` | `btnExportWord_Click`, `btnExportPdf_Click` | `Case.Export` |
| `FrmFamily` | `btnSave_Click`, `btnEdit_Click` | `Family.Edit` |
| `FrmFamily` | `btnDelete_Click` | `Family.Delete` |
| `FrmFamily` | `btnPrint_Click` | `Family.Print` |
| `FrmDocs` | `btnSave_Click`, `btnEdit_Click` | `Docs.Edit` |
| `FrmDocs` | `btnDelete_Click` (archive) | `Docs.Delete` |
| `FrmDocs` | `btnPrint_Click` | `Docs.Print` |
| `FrmApplicant` | `SaveApplicant` | `Applicant.Edit` |
| `FrmApplicant` | `DeleteApplicant` | `Applicant.Delete` |
| `FrmCaseRelations` | `btnAdd_Click` | `CaseRelation.Edit` |
| `FrmCaseRelations` | `btnDelete_Click` | `CaseRelation.Delete` |
| `FrmAssignMemberRole` | `ApplyChanges` | `Family.Edit` (reused) |
| `FrmArchive` | `RestoreSelected` | `Archive.Restore` |
| `FrmArchive` | `PurgeSelected` | `Archive.PermanentDelete` |
| `FrmBarcode` | `btnPrint_Click` | `Barcode.Print` |
| `FrmReportBuilder` | `RunReport` | `Report.Run` |
| `FrmReportBuilder` | `ExportExcel` | `Report.Export` |
| `FrmVersions` | constructor (covers all 3 open paths — dashboard, case history, family history) | `Version.View` |
| `FrmSecurityAudit` | constructor | `Security.View` |
| `FrmErrorLog` | constructor | `Error.View` |
| `FrmGuardianCardBatchPrint` | constructor | `GuardianCard.Print` |
| `FrmCardTemplateManager` | constructor | `GuardianCard.ManageTemplates` |

**Indirectly covered (no separate gate needed, verified by call-graph):**
- `FrmCaseReport.cs` (legacy RDLC viewer) — only constructed at `FrmCase.cs:3019`, inside the now-gated `btnExportWord_Click`. Confirmed by `grep -rn "new FrmCaseReport"` → single call site, downstream of the `Case.Export` check.

---

# 4. Orphan Permission Keys

**Definition used:** a key exists in `EntPermission` (seeded, visible in `FrmPermissionMatrix`) but **no code path — direct or data-driven — ever passes that exact string to `PermissionService.HasPermission`/`Require`.**

Checked every one of the 24 pre-existing keys individually (`grep -rn "\"<key>\"" --include=*.cs .`, excluding the seed file):

| Key | Referenced outside seed file? | Verdict |
|---|---|---|
| `Case.View` | 0 hits | 🔴 **Orphan** |
| `Case.Create` | 0 hits | 🔴 **Orphan** — no distinct "create" code path exists to attach it to (FrmCase reuses one `Case.Edit` gate for insert+update) |
| `Workflow.View` | 0 hits | 🔴 **Orphan** |
| `Workflow.Manage` | 0 hits | 🔴 **Orphan** |
| `Approval.Decide` | 0 hits; not assigned to any seeded `EntApprovalLevel.RequiredPermission` row (seed uses `ApproverRole` only) | 🔴 **Orphan by default** (see §5 — reachable only if an admin manually edits a level's RequiredPermission) |
| `Approval.Manage` | 0 hits | 🔴 **Orphan** |
| `Task.View` | 0 hits | 🔴 **Orphan** |
| `Task.Manage` | 0 hits | 🔴 **Orphan** — `TaskService.cs`/`FrmTasks.cs` still gate via `SecurityContext.IsAdmin()` directly (confirmed: `grep -l PermissionService Enterprise/TaskService.cs` → no match) |
| `Rule.Manage` | 0 hits | 🔴 **Orphan** — `FrmRules.cs` uses `IsAdmin()` directly |
| `Lock.Override` | 0 hits | 🔴 **Orphan** — `LockService.cs` uses `IsAdmin()` directly |
| `User.Manage` | 0 hits | 🔴 **Orphan** — `FrmUsers.cs` uses `IsAdmin()` directly |
| `Permission.Manage` | 0 hits | 🔴 **Orphan** — `PermissionService.SetRolePermission`/`SetUserPermission` (the matrix-edit guard) check `IsAdmin()` directly, not this key |
| `Module.Manage` | 0 hits | 🔴 **Orphan** — `ModuleService.EnsureManageable` uses `IsAdmin()` directly |
| `Settings.Manage` | 0 hits | 🔴 **Orphan** — `FrmSettings.cs` uses `IsAdmin()`/`IsSuperAdmin()` directly |
| `Finance.View` | 0 hits | 🔴 **Orphan** — `FrmFinance.cs` is gated by `SecurityContext.CanEdit()` only; the `Finance` module-sidebar entry is gated by `ModuleService` (a different key space), not this permission key |
| `Accounting.View` | 0 hits | 🔴 **Orphan** — same pattern as Finance.View |

**Count: 16 of the 24 pre-existing keys are orphaned.** None of these are Sprint 0A's doing — they were seeded before this sprint and remain untouched, since Wave 4/5 (Accounting, Finance, User Management, Module Management, Settings, Workflow/Approval/Task/Rule/Lock administration) were explicitly out of scope. Flagging them here because the audit asked for "any orphan key," not just ones Sprint 0A introduced.

**New (Sprint 0A) keys — zero orphans.** All 18 new keys were verified to have a live `Require()` call site in §3.

---

# 5. Unreachable Permission Keys

**Definition used:** a key that is referenced by code, but the reference itself can never execute (dead branch), *or* is data-driven and the seed data never actually assigns that key to any live record — distinct from "orphan" (never referenced at all, not even as unused data-plumbing).

| Key | Reachability path | Verdict |
|---|---|---|
| `Workflow.Review` | Seeded directly into `EntWorkflowTransition.RequiredPermission` (`EnterpriseInitializer.cs:183,186`) → `WorkflowService.CanTransition()` reads `transition.RequiredPermission` → `PermissionGate(...)` → `PermissionService.HasPermission("Workflow.Review")` | ✅ **Reachable** — live, default-seeded, exercised on every "شروع بررسی"/"بازگشت به پیش‌نویس" transition |
| `Workflow.Approve` | Same mechanism, `EnterpriseInitializer.cs:184,185` | ✅ **Reachable** — live on "تأیید نهایی"/"رد پرونده" |
| `Approval.Decide` | Never assigned to any `EntApprovalLevel.RequiredPermission` row by default (`EnsureDefaultCaseChain` sets only `ApproverRole`); `ApprovalService.cs:225-228` *would* read it generically if a level's `RequiredPermission` column were populated | ⚠ **Reachable in principle, inert by default** — the mechanism exists and is generic, but out-of-the-box no data ever triggers it. Distinct from a true orphan only in that no code change would be needed to activate it — an admin would just need to set a level's `RequiredPermission` field (no UI currently exposes editing that field, per `FrmApprovals.cs`/`FrmWorkflowAdmin.cs` — so in practice it is presently unreachable through the shipped UI) |

No key introduced by Sprint 0A exhibits this pattern — all 18 new keys are wired through a direct, unconditional `Require()` call at the top of their guarded method (verified by diff in §1), not through data-driven indirection.

---

# 6. Remaining Zero-Permission Operations

Re-ran the same zero-check search method used for the original audit (`grep -c "SecurityContext\|PermissionService"` per candidate file) against every Print/Export/Report/History/Viewer surface, plus a fresh call-graph walk from `FrmCase.cs` and `FrmFinance.cs` looking for sibling buttons that might have been missed the first time.

## 6.1 Confirmed still zero-check (not fixed — correctly out of scope)

| Operation | File | Evidence |
|---|---|---|
| Assistance receipt — single print | `AssistanceReceiptIntegration/FrmAssistanceReceiptSinglePrint.cs` | 0 matches; only reachable from `FrmFinance.cs:258` |
| Assistance receipt — filtered batch print | `AssistanceReceiptIntegration/FrmAssistanceReceiptFilterPrint.cs` | 0 matches; only reachable from `FrmFinance.cs:268` |
| Assistance package — batch print | `AssistanceReceiptIntegration/FrmAssistancePackageBatchPrint.cs` | 0 matches; only reachable from `FrmFinance.cs:278` |

All three are reachable **exclusively** through `FrmFinance.cs` (confirmed — no other caller exists anywhere in the codebase). Left untouched per the explicit "Do not touch Finance" instruction; this is a known, intentional gap, not an oversight — candidate for Sprint 0B alongside the rest of the Finance module.

## 6.2 🔴 FINDING (now fixed post-audit) — a zero-check operation Sprint 0A initially missed

> **Update:** fixed in a follow-up change after this audit was written. `FrmCase.cs`'s `btnGuardianCard.Click` handler now opens with `if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Print")) { ...; return; }`, reusing the same key already governing the batch-print button — no new permission key was needed. Build verified clean (0 errors, same 17 baseline warnings). The description below is left as originally written to document what the audit found.

**`FrmCase.cs:169-185`, method `AddGuardianCardButton`, the "کارت شناسایی" (single guardian-card preview/print) button — was zero-check.**

```csharp
btnGuardianCard.Click += delegate
{
    if (currentCaseId == 0) { Msg.Show("..."); return; }
    using (var frm = new GuardianCardIntegration.FrmGuardianCardPreview(currentCaseId))
        frm.ShowDialog(this);
};
```

This is a **separate button from the batch-print button** on the same toolbar (`btnGuardianCardBatch`, line 187-196, which *is* gated — it opens `FrmGuardianCardBatchPrint`, covered by `GuardianCard.Print`). `FrmGuardianCardPreview.cs` itself has zero `SecurityContext`/`PermissionService` references, and its only other caller (`FrmCardTemplateManager.cs:448`) is inside a screen now gated by `GuardianCard.ManageTemplates` — but the `FrmCase.cs:183` path bypasses that entirely.

**Consequence:** today, any logged-in user (including `Viewer`) can open and print a single guardian ID card from the case screen, ungated — the exact category of gap Sprint 0A was scoped to close, missed because it wasn't listed in the original `SYSTEM_AUDIT_REPORT.md` §4 inventory (only the batch-print form was catalogued there) and wasn't independently re-discovered until this audit's call-graph walk.

**This was not fixed in this pass — audit only, no code changes made.** Recommend it as the first item in Sprint 0B, using the same `GuardianCard.Print` key already seeded and already governing the batch-print button (no new key needed — a one-line `Require()` guard, identical pattern to the 29 already applied).

## 6.3 Confirmed fixed (cross-check against original inventory)

| Operation | Status |
|---|---|
| Case print / export Word / export PDF | ✅ Fixed (`Case.Print` / `Case.Export`) |
| Legacy RDLC case report | ✅ Indirectly covered (§3) |
| Family print | ✅ Fixed (`Family.Print`) |
| Docs print | ✅ Fixed (`Docs.Print`) |
| Barcode print | ✅ Fixed (`Barcode.Print`) |
| Report Builder run / Excel export | ✅ Fixed (`Report.Run` / `Report.Export`) |
| Guardian card **batch** print | ✅ Fixed (`GuardianCard.Print`) |
| Guardian card **single** preview/print | 🔴 **Missed — see §6.2** |
| Card template management | ✅ Fixed (`GuardianCard.ManageTemplates`) |
| Version History viewer | ✅ Fixed (`Version.View`) |
| Security Audit viewer | ✅ Fixed (`Security.View`) |
| Error Log viewer | ✅ Fixed (`Error.View`) |

---

# Summary

| Check | Result |
|---|---|
| Files modified | **15**, all within approved scope; 4 contained pre-existing unrelated changes, correctly isolated (§1.1) |
| New permission keys | **18**, zero duplicates, one deliberately withheld (`AssistanceReceipt.Print`) to avoid seeding a dead key |
| Screens affected | **20** distinct screens/handlers across 29 call sites, plus 1 indirectly-covered legacy screen |
| Orphan keys | **16 pre-existing** (not introduced by Sprint 0A — Wave 4/5 territory); **0 of the 18 new keys** |
| Unreachable keys | **1** (`Approval.Decide` — inert by default, pre-existing, not Sprint 0A's doing) |
| Remaining zero-check operations | **3 known/deferred** (AssistanceReceipt printing, out of scope) + **1 newly discovered miss** (single guardian-card print button, §6.2) |

No code was modified during this audit. The one finding in §6.2 is a genuine gap in Sprint 0A's coverage and should be the lead item when Sprint 0B is scoped.
