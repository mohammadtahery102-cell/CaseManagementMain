# VERSION 1.0 — FINAL STATUS AUDIT
## CaseManagement — Where Things Actually Stand

| | |
|---|---|
| **Document Type** | Status audit — precedes `VERSION1_COMPLETION_REPORT.md` |
| **Date** | 2026-08-23 |
| **Scope of this audit** | The full engagement: `SYSTEM_AUDIT_REPORT.md`'s original roadmap (CM-01..IM-10, SEC-001..003) · Sprint 0A permission work · Task 3 (Financial Period Enforcement) · the 4-phase Version 1.0 Completion Package just finished (Duplicate Tazkira, Record Locking, Wave 4 & Wave 5 permission integration) |
| **Test baseline at time of writing** | 384 total / 382 passed / 2 skipped (pre-existing) / **0 failed** — confirmed after every phase in this package |

---

## 1. Fully Complete

| Item | Evidence |
|---|---|
| **Duplicate Tazkira detection** (Priority-Analysis Task 1) | Cross-table (Case/Family/Applicant) soft-warning check, center-scoped, audit-logged. 9 dedicated tests, all passing. |
| **Record locking activation — CM-05** | `LockService` wired into `FrmCase` (edit-mode chokepoint) and `FrmFamily`/`FrmDocs` (row-selection lifecycle), with heartbeat renewal and crash-recovery via existing expiry/purge. 14 dedicated tests, all passing. |
| **Financial period enforcement gaps** (Priority-Analysis Task 3) | `AccRepair`'s three raw-SQL repair methods now check period-closed status; `CloseSelectedPeriod` now permission-gated. (Note: the originally-reported `UpdateSalary` bug did not actually exist — verified and corrected during implementation.) |
| **Permission enforcement — core CRUD & zero-check screens** (Sprint 0A) | Case/Family/Docs/Applicant/CaseRelations/Archive CRUD, plus previously-unguarded Print/Export/Report Builder/Barcode/GuardianCard/Version-History/Security-Audit/Error-Log screens — all now gated via `PermissionService`. |
| **Permission enforcement — Finance & Accounting** (Phase 3) | `FrmFinance`, `FrmAccounting` (edit/reverse/close-period/repair/backup), `AccRepair` — all remapped to 6 new/reused permission keys. 7 dedicated tests confirming defaults exactly mirror prior behavior. |
| **Permission enforcement — Users, Modules, Centers, Backup, Sync** (Phase 4) | `FrmUsers` (finally wired the previously-orphaned `User.Manage` key), `ModuleService` (finally wired `Module.Manage`), plus 4 new keys (`Center.Manage`, `Backup.Create`, `Backup.Restore`, `Sync.Execute`) across `FrmSettings`, `FrmSyncWizard`, `MediaSyncEngine`. 8 dedicated tests. |

---

## 2. Partially Complete

| Item | What's done | What's missing |
|---|---|---|
| **SEC-001 / CM-02 — Permission enforcement, system-wide** | ~57 of 72 originally-inventoried legacy `SecurityContext` call sites now route through `PermissionService` (Case/Family/Docs/Applicant/Archive/Finance/Accounting/Users/Modules/Centers/Backup/Sync). | **40 legacy checks remain** across `Enterprise/ApprovalService.cs`, `ErrorLogger.cs`, `TaskService.cs`, `LockService.cs` (its own `ForceRelease`), `FrmApprovals.cs`, `FrmRules.cs`, `FrmTasks.cs`, `FrmWorkflowAdmin.cs`, `WorkflowService.cs`, `FrmDashboard.cs` (menu visibility), `FrmLogin.cs`, plus the intentionally-hardcoded SuperAdmin-only sites (`DevCenter/*`, `PermissionService`'s own matrix-edit guard, the legacy-restore guard in `BackupHelper.cs`, SuperAdmin-credential-update in `FrmSettings.cs`). The app currently runs on **two authorization systems**, not one — improved from Sprint 0's starting point but not unified. |
| **IM-02 — Role management** | The permission-key infrastructure now cleanly supports any role, and this package's defaults were calibrated to zero-change existing behavior. | No `TblRole` table exists — roles are still free-text strings (`SuperAdmin`/`Admin`/`Operator`/`Viewer`), no custom roles, still typo-prone. The `Manager`/`Finance` role split proposed during Sprint 0 planning was **never implemented** — an explicit deferred decision, still open. |

---

## 3. Not Started

| Item | Why it matters |
|---|---|
| **CM-01 — Encryption at rest** | The single risk the original audit called a "physical safety risk to data subjects," not just a compliance gap. Database, photos, documents, and backups remain entirely plaintext. **Completely untouched across this whole engagement.** |
| **CM-03 — Backup encryption & verification** | Backups are the highest-value single-file exfiltration target and remain unencrypted, with no integrity checksums. |
| **CM-04 — History & accounting sync** | Status-history tables and `Acc*` tables still excluded from cross-branch sync — two branches will hold permanently divergent financial ledgers. |
| **CM-06 — Assistance↔ledger reconciliation** | No structural link between `TblAssistance` and `Acc*` — organization still can't answer "how much has this household received?" from its own books. |
| **SEC-002 — Center isolation schema weakness** | `TblFamily`/`TblDocs` still have no `CenterID` column; isolation still depends entirely on every query remembering to `JOIN TblCase`. |
| **IM-01 — Afghan terminology** | Iranian vocabulary (`استان`, `ریال`) still appears on printed receipts and ID cards handed directly to beneficiaries/donors. |
| **IM-03 — Functional password policy** | `ForcePasswordChangeDays` remains uncomputable — no `PasswordChangedAt` column. |
| **IM-04 — Backup/restore test coverage** | Zero direct automated tests for the restore path, despite it being the disaster-recovery mechanism. |
| **IM-05 — Assistance approval workflow** | Aid can still be recorded with no approval step or duplicate-payment detection. |
| **IM-06 — CI/CD pipeline** | The now-384-test suite only protects the codebase if a human remembers to run it before every change. |
| **IM-07 — Schema version guard** | No `schema_version` table — an older executable opening a newer database still produces undefined behavior. |
| **IM-08 — Multi-center schema enforcement** | Same root cause as SEC-002. |
| **IM-09 — File upload validation** | No extension whitelist, size cap, or content-type check on attached documents — untouched. |
| **IM-10 — Translation completion** | 927 previously-identified untranslated strings remain untranslated. |

---

## 4. Security Gaps That Still Remain

1. **No encryption anywhere** (CM-01/SEC-003) — the dominant, unaddressed critical finding from the original audit.
2. **Center isolation is convention-based, not schema-enforced** (SEC-002) — a future query that forgets to join `TblCase` silently leaks cross-branch data.
3. **Two parallel authorization systems still coexist** — 40 call sites (Enterprise governance, Dashboard menu gating, DevCenter, a handful of deliberately-hardcoded SuperAdmin checks) remain on raw `SecurityContext` role comparisons rather than the fine-grained matrix. Each of these is still *enforced* (not a regression from before this engagement), but the inconsistency is itself a long-term maintenance and audit risk.
4. **No `TblRole` table** — role assignment is a free-text field; a typo silently misassigns access with no constraint to catch it.
5. **No MFA, no functional password expiry.**
6. **No file upload validation** — a malicious or oversized attachment can be uploaded without any check.
7. `PermissionService`'s own matrix-editing guard and the legacy-restore guard in `BackupHelper.cs` remain hardcoded to `IsAdmin()`/`IsSuperAdmin()` — a **deliberate, defensible** choice (avoids a circular bootstrapping risk and protects the single most destructive restore path) but worth naming explicitly so a future reviewer doesn't mistake it for an oversight.

---

## 5. Production Risks That Still Remain

1. **Backup/restore has zero automated test coverage** — this was true before this engagement and remains true after it; the disaster-recovery mechanism itself is unverified.
2. **No CI/CD gate** — regressions are only caught if someone manually runs the suite (currently ~8-9 minutes).
3. **`LockService` timing is unvalidated under real field conditions** — the 15-minute expiry / 5-minute heartbeat were chosen from the existing setting default and unit-tested at the engine level, but never piloted with real concurrent users on real network conditions.
4. **`AccRepair`'s new closed-period guards (Task 3) could block legitimate historical-data repairs** — an intentional tightening per explicit instruction, but it hasn't been field-validated against real corrupted-data scenarios a SuperAdmin might need to fix.
5. **The Sync module — the largest, most complex subsystem (15K+ LOC) — was untouched by this entire engagement.** It remains the least-audited high-risk area in the codebase.
6. **SuperAdmin single point of failure** — `Center.Manage`, `Backup.Create`, `Backup.Restore`, `Accounting.Repair`, `Accounting.Backup` are now *cleanly* gated but remain 100% SuperAdmin-exclusive with zero delegation path. If the SuperAdmin account becomes unavailable (staff turnover, forgotten credentials, lockout), centers/backups/historical-data-repair all freeze. This risk pre-dates this engagement and was deliberately preserved (per the explicit "do not widen to Admin" design decision), not introduced by it — but it remains a real operational exposure.
7. **Duplicate-Tazkira detection is soft-warning only, by design** — it surfaces likely duplicates but does not prevent a determined or careless user from proceeding anyway.

---

## 6. Before Deploying to Real Charity Centers

In priority order:

1. **Resolve encryption at rest** (database, documents/photos, backups) — given the population served (orphans, widows, minority communities explicitly tracked as data fields) and the original audit's own framing of this as a physical-safety issue, this is the clearest pre-deployment blocker of everything on this list.
2. **Run an actual backup/restore disaster-recovery drill** on a realistic copy of production-shaped data — not a unit test, an actual rehearsal.
3. **Pilot record locking (Phase 2) with real concurrent users at one real center for at least a full working week** before wider rollout, to validate the timeout/heartbeat tuning against real usage patterns and real network conditions.
4. **Decide on and, if needed, implement the `TblRole` table and the `Manager`/`Finance` role split** deferred during Sprint 0 planning — this is a product decision that has been sitting open for the whole engagement.
5. **Finish migrating the remaining 40 legacy permission checks** (Enterprise governance forms especially) so the application runs on one consistent authorization model instead of two.
6. **Add file upload validation** before broad field use of document attachments.
7. **Fix Afghan terminology** on printed receipts and ID cards — lowest effort, highest visibility to the people actually receiving them.
8. **Stand up even a minimal CI gate** (run the test suite on every commit) before multiple developers or centers depend on this codebase in parallel.

---

## 7. Percentages

| Metric | Estimate | Basis |
|---|---|---|
| **Version 1.0 completion** (against the full original roadmap: 6 CM items + 10 IM items + SEC-002 + role management, weighted by the work actually done this engagement) | **≈ 38%** | 4 items fully done, 2 partially done (permission enforcement is the largest, most substantive item and is meaningfully advanced), 13 items untouched. *Against just the 4-phase package explicitly requested in this session, completion is 100% — but that package was always a subset of the full Version 1.0 roadmap, not the whole of it.* |
| **Production readiness** | **≈ 68%** (up from the original audit's baseline estimate of 61%) | Real, targeted improvement in exactly the areas the audit flagged as blocking (SEC-001 permission enforcement, CM-05 concurrent edit protection, financial period integrity), still capped by the untouched encryption gap and the unverified backup/restore path. |
| **Security readiness** | **≈ 62 / 100** (up from the original audit's baseline score of 48/100) | Permission enforcement (SEC-001) is now substantially — not fully — closed across the highest-traffic parts of the app. The other two originally-critical findings, SEC-002 (center isolation) and SEC-003 (encryption), remain **fully open** and cap how high this score can honestly go until they're addressed. |

---

*Next: `VERSION1_COMPLETION_REPORT.md`, per the original instruction sequence — not produced in this turn, per this audit's explicit "then stop."*
