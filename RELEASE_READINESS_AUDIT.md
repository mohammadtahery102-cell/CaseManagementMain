# RELEASE READINESS AUDIT
## CaseManagement — Version 1.0 State Review

| | |
|---|---|
| **Document Type** | Audit only — no code changed |
| **Date** | 2026-08-23 |
| **Method** | Direct re-verification against the live codebase (grep/read evidence for every claim, not recalled from memory) |
| **Builds on** | `SYSTEM_AUDIT_REPORT.md` · `VERSION1_FINAL_STATUS.md` · `VERSION1_COMPLETION_REPORT.md` |

---

## 1. Duplicate Tazkira Implementation — ✅ **Complete**

*(As designed: a soft warning at save time, not a hard database constraint — see `VERSION1_PRIORITY_ANALYSIS.md` §1.3 for why a `UNIQUE` constraint was explicitly rejected.)*

| File | Role |
|---|---|
| `Helpers/DuplicateDetector.cs` | `FindByTazkira(tazkira, excludeTable, excludeId)` — cross-table (Case/Family/Applicant), center-scoped, reuses `NormalizeIdentifier` |
| `FrmCase.cs` | Wired into both the insert path (`btnSave_Click`) and update path (`UpdateCurrentCase`) |
| `FrmFamily.cs` | Wired into `btnSave_Click` |
| `FrmApplicant.cs` | Wired into `SaveApplicant` |
| `CaseManagement.Tests/DuplicateTazkiraTests.cs` | 9 tests, all passing |

**Verified:** `FindByTazkira` call count — `DuplicateDetector.cs` (1 definition), `FrmCase.cs` (2 call sites), `FrmFamily.cs` (2), `FrmApplicant.cs` (1). No gaps found.

---

## 2. Record Locking Implementation — ✅ **Complete** (for the 3 forms in scope)

| File | Role |
|---|---|
| `Enterprise/LockService.cs` | Pre-existing engine (`TryAcquire`/`Heartbeat`/`Release`/`ForceRelease`/`Describe`/`GetActiveLocks`/`PurgeExpired`) — unmodified, now actually consumed |
| `FrmCase.cs` | `SetCaseEditMode` acquires/releases a `TblCase` lock; new `OnFormClosing` override; 5-min heartbeat timer |
| `FrmFamily.cs` | `LoadMemberToForm`/`ClearForm`/`OnFormClosed` acquire/release a `TblFamily` lock per record |
| `FrmDocs.cs` | Identical pattern for `TblDocs` |
| `CaseManagement.Tests/LockServiceTests.cs` | 14 tests against the engine, all passing |

**Verified:** `LockService.` reference count — `FrmCase.cs` (4), `FrmFamily.cs` (3), `FrmDocs.cs` (3). All non-zero, consistent with the implementation record.

**Caveat, not a gap in the locking mechanism itself:** `Enterprise/LockService.cs`'s own `ForceRelease` method still gates on legacy `SecurityContext.IsAdmin()` rather than `PermissionService` — this is a permission-migration leftover (see §3), not a locking-logic defect. The lock/release/heartbeat/expiry cycle itself is fully wired and tested.

---

## 3. Permission Migration Implementation — ⚠️ **Partial**

**Done:** Core CRUD (Case/Family/Docs/Applicant/Archive), all previously zero-check screens (Print/Export/Report Builder/Barcode/GuardianCard/Version History/Security Audit/Error Log), Finance, Accounting (6 keys), Users, Modules, Centers, Backup, Sync (4 new + 2 previously-orphaned keys). ~57 of the originally-inventoried 72 legacy call sites now route through `PermissionService`.

**Not done — 40 legacy `SecurityContext` checks remain**, confirmed by direct grep against the current tree:

| File | What's still legacy |
|---|---|
| `Enterprise/ApprovalService.cs` | Approval decision/cancellation gates |
| `Enterprise/ErrorLogger.cs` | Mark-resolved / purge |
| `Enterprise/TaskService.cs` | Assign / delete task |
| `Enterprise/LockService.cs` | `ForceRelease` (see §2 caveat) |
| `Enterprise/ModuleService.cs` | Line 109 — module-cache SuperAdmin bypass (**intentionally** not migrated, see below) |
| `Enterprise/WorkflowService.cs` | Fallback path when `PermissionGate` is unset |
| `Enterprise/PermissionService.cs` | Its own matrix-edit guard (`SetRolePermission`/`SetUserPermission`) — **intentionally** hardcoded to avoid a circular bootstrapping risk |
| `Enterprise/FrmApprovals.cs`, `FrmRules.cs`, `FrmTasks.cs`, `FrmWorkflowAdmin.cs` | `RequireAdmin()` helpers |
| `FrmDashboard.cs` | Sidebar menu-item visibility (Users/Settings/Assign-Role/Card-Templates) |
| `FrmLogin.cs` | Post-login "All Centers" option — **intentionally** hardcoded (SuperAdmin-only by design) |
| `FrmSettings.cs` | SuperAdmin-credential-update section; "Delete Cases" maintenance tab (out of explicit scope for this package) |
| `FrmUsers.cs` | Center-selection logic (lines 248/252) — **intentionally** hardcoded, not an action gate |
| `Helpers/BackupHelper.cs` | Legacy non-GlobalID restore guard — **intentionally** hardcoded (stricter than the general `Backup.Restore` key; see `VERSION1_COMPLETION_REPORT.md` §3) |
| `DevCenter/DevCenterAccess.cs`, `FrmDevCenter.cs` | Hidden diagnostics entry gates — **intentionally** hardcoded |

Roughly half of this remaining list (marked above) is a **deliberate design decision**, not an oversight — migrating them would risk letting an Admin grant themselves SuperAdmin-tier access via the permission matrix. The other half (Enterprise governance: Approval/Task/Error/Rule/Workflow-admin screens, plus Dashboard menu visibility) is genuinely **unmigrated, still open work**.

**Net effect:** the application currently runs on **two authorization systems** — the fine-grained matrix for the areas covered above, and raw role comparison for Enterprise governance and menu visibility. Both are actively enforced (this is not a security hole relative to before this engagement), but the inconsistency is real.

---

## 4. Financial Period Enforcement — ✅ **Complete** (for the identified gaps)

| File | Role |
|---|---|
| `Accounting/AccountingRepo.cs` | `IsRecordPeriodOpen` helper; `EnsureMutable` already correctly guarded every edit/void path (confirmed directly — no bug existed in `UpdateSalary` contrary to an earlier, since-corrected analysis) |
| `Accounting/AccRepair.cs` | Closed-period guards added to `ApplyAssignPeriod`/`ApplyAssignCenter`/`ApplyFixDate` — previously the only 3 code paths in the module that could bypass period-closed protection |
| `Accounting/FrmAccounting.cs` | `CloseSelectedPeriod` now permission-gated (`Accounting.ClosePeriod`) |

**Verified:** `IsRecordPeriodOpen`/`EnsureMutable` reference counts — `AccRepair.cs` (2), `AccountingRepo.cs` (11). Consistent with the implementation record. All 50 accounting-related tests pass.

**Not in scope / not addressed:** no reopen-period mechanism exists (by design — a product decision was deferred, not a bug); no `ClosedBy`/`ClosedAt` audit columns (cosmetic gap only, `AccAudit` log covers it in free text).

---

## 5. Backup and Restore Process — ⚠️ **Partial**

| File | Role |
|---|---|
| `Helpers/BackupHelper.cs` | `ExportBackup`/`ImportBackup` — main system backup engine (54 KB, functional) |
| `Helpers/AccountingBackupHelper.cs` | Separate backup path for `Acc*` tables |
| `Helpers/AutoBackupService.cs` | Scheduled (daily/weekly/monthly) automatic backup |
| `FrmSettings.cs` | UI entry points, now correctly gated by `Backup.Create`/`Backup.Restore` (Phase 4) |
| `Accounting/FrmAccounting.cs` | Accounting-specific backup UI, gated by `Accounting.Backup` (Phase 3) |

**What works:** the mechanism itself is real, functional, and (as of this session) properly permission-gated — this is a genuine improvement over the session's starting state, where these entry points were `IsSuperAdmin()`-only with no matrix visibility.

**What's missing, confirmed by direct search:**
- **Zero automated test coverage.** A search of the entire `CaseManagement.Tests` project for `ImportBackup`/`RestoreBackup` returns no matches — the restore path (merge-mode ID remapping, classic-mode disaster recovery) has never been exercised by a test.
- **No encryption** on backup archives (see §6).
- **No integrity checksums** — a corrupted or truncated backup file is only discovered when restore is actually attempted, not before.

This matches the original `SYSTEM_AUDIT_REPORT.md`'s GAP-01/IM-04 finding, which remains **fully open**.

---

## 6. Encryption Status — ❌ **Missing**

Full-codebase search for `Aes`, `SQLCipher`, `SetPassword`, `ProtectedData` (all standard .NET/SQLite encryption mechanisms) returns **zero matches** outside test/library noise. Confirmed: the only cryptographic code in the entire application is `HMACSHA256` (license token signing) and `Rfc2898DeriveBytes` (password hashing) — neither encrypts data at rest.

**Concretely unencrypted:** the SQLite database file, all attached documents and photographs, and all backup archives (§5).

This is the single largest unaddressed item from the original audit (CM-01/SEC-003), explicitly described there as "a physical safety risk to data subjects" given the population this system serves (orphans, widows, minority communities tracked as data fields). **Nothing in this entire engagement touched this.**

---

## 7. Installer Readiness — ⚠️ **Partial**

| File | Role |
|---|---|
| `Installer/CaseManagement.iss` | Inno Setup script (x64-only, publisher = generic "CaseManagement") |
| `Installer/Guardian.iss` | A second, parallel Inno Setup script — appears to be a rebrand-in-progress (`AppName`/`AppPublisher` under the "Guardian" name) |
| `Installer/README.md` | Build instructions, VC++ Redistributable pre-check documentation, known antivirus-quarantine troubleshooting for the unsigned native SQLite DLL |

**What works:** the installer mechanics are genuinely mature — correct install path, Start Menu/Desktop shortcuts, proper uninstall registration, x64-gated (matches the SQLite/WebView2 native dependency), VC++ Redistributable presence check before install, auto-creates the database on first run rather than bundling one.

**What's missing, confirmed directly in the script files:**
- **`Guardian.iss` still has literal `TODO` placeholders** for `AppPublisher` ("Guardian" — not a real organization name) and `AppURL` (`https://example.org`).
- **No code signing configured** for either the installer or `CaseManagement.exe` — the README itself flags this as the "permanent, professional fix" for the antivirus-quarantine issue it documents, and recommends obtaining a certificate before real customer delivery, but none exists yet.
- **Two divergent installer scripts** (`CaseManagement.iss` vs `Guardian.iss`) with different app names/branding — unclear which is canonical for a Version 1.0 release; this should be resolved, not shipped as-is.
- **Installer execution has never been verified** (this audit, like the original system audit, is static file review — actually running the compiled installer on a clean machine, including the upgrade path from a prior version, was not performed).

---

## Summary Table

| # | Item | Status |
|---|---|---|
| 1 | Duplicate Tazkira | ✅ Complete |
| 2 | Record Locking | ✅ Complete |
| 3 | Permission Migration | ⚠️ Partial (~57/72 sites; rest is a mix of deliberate exclusions and genuinely open work) |
| 4 | Financial Period Enforcement | ✅ Complete |
| 5 | Backup and Restore | ⚠️ Partial (mechanism works and is now permission-gated; unencrypted, unverified, untested) |
| 6 | Encryption | ❌ Missing |
| 7 | Installer | ⚠️ Partial (mechanically sound; unsigned, unbranded, unverified) |

---

## Remaining Production Blockers

In order of severity:

1. **No encryption at rest (§6).** Given the sensitivity of the data (national ID numbers, photographs of minors, disability/vulnerability status, religious/ethnic minority markers), this is the single item that should block real deployment regardless of anything else on this list.
2. **Backup/restore is functionally real but operationally unverified (§5).** Shipping without ever having exercised the restore path — the actual disaster-recovery mechanism — is a live risk, not a theoretical one.
3. **Installer is not release-branded or signed (§7).** `Guardian.iss` cannot ship with `TODO`/`example.org` placeholders, and an unsigned installer will trigger SmartScreen/antivirus friction for every real-world install.
4. **Permission migration is incomplete (§3).** Two authorization systems coexisting is a maintainability and audit-clarity risk more than an active vulnerability, but should be finished before this codebase has multiple long-term maintainers.
5. **Record locking and duplicate-Tazkira detection are both functionally complete but have zero real-world usage data** — recommend a supervised single-center pilot (per `VERSION1_COMPLETION_REPORT.md` §8) before wider rollout, specifically to validate lock timeout tuning and duplicate-warning false-positive rate under real conditions.

---

*Audit only. No code was modified. Stopping here as instructed.*
