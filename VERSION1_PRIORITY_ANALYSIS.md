# VERSION 1.0 PRIORITY PACKAGE — ANALYSIS
## CaseManagement — Three Independent Hardening Tasks

| | |
|---|---|
| **Document Type** | Analysis & planning only — **no code changed, no commits, no refactoring** |
| **Date** | 2026-08-22 |
| **Method** | Full-codebase research (file:line evidence for every claim) |
| **Scope** | Task 1 — Duplicate Tazkira Block · Task 2 — Record Locking Activation · Task 3 — Financial Period Enforcement |

---

# TASK 1 — Duplicate Tazkira Block

## 1.1 Current State

**Save paths (no duplicate check exists at any of them):**

| Form | Method | Location | Pre-existing checks |
|---|---|---|---|
| `FrmCase.cs` | `btnSave_Click` → `UpdateCurrentCase` | insert ~1718-1739, update ~1851-1892 | `IsFormNoExists`, `IsCodeExists` only (Code/FormNo, never Tazkira) |
| `FrmFamily.cs` | `btnSave_Click` / `btnEdit_Click` | insert ~967-990, update ~1083-1088 (`MemberTazkiraNo`) | none |
| `FrmApplicant.cs` | `SaveApplicant` | insert ~465-467, update ~477-482 (`TazkiraNo`) | none |

**Schema** (`Helpers/DatabaseInitializer.cs`): `TblCase.HeadTazkiraNo`, `TblFamily.MemberTazkiraNo`, `TblApplicant.TazkiraNo` — all plain `TEXT NULL`. **No `UNIQUE` constraint or index on any of them** (full audit of every `UNIQUE`/`CREATE INDEX` in the file confirms uniqueness exists only for `Code`, `FormNo`, `Username`, `CenterCode`, `GlobalID` — never Tazkira).

**Existing fuzzy-match tool:** `Helpers/DuplicateDetector.cs` already does Tazkira-aware matching — exact grouping on normalized Tazkira (`NormalizeIdentifier`) plus fuzzy name/address scoring — but it: (a) **only scans `TblCase`**, never `TblFamily`/`TblApplicant`; (b) is **manual/on-demand only**, opened from `FrmDuplicates.cs`/`FrmDataQualityReport.cs`/Dev Center diagnostics — **zero call sites from any save path**. `TblApplicant.ConvertedCasID` (the applicant→case conversion link) exists in schema but is never read/written anywhere — the conversion feature is schema-only, not implemented, so there is no conversion-time re-check either. No test coverage exists for duplicate-Tazkira prevention.

## 1.2 Missing Pieces

1. No save-time check of any kind in any of the 3 forms.
2. No cross-table check — a person could be entered as a Case guardian, then again as a Family member elsewhere, then again as an Applicant, with nothing connecting the three.
3. `DuplicateDetector.cs` logic (normalization, fuzzy scoring) exists but is not reusable as-is for a real-time save-blocking check — it's designed as a batch report generator, not a per-record gate.
4. No design decision yet on: exact-vs-fuzzy match strictness, blank-Tazkira handling, and — the important one — **center scope**. Tazkira is a national ID and should logically be checked *across all centers*, but the rest of the app is deliberately center-isolated (`CenterGuard`/`CenterFilterId`). A cross-center duplicate check necessarily needs to look at data an Operator/Admin in another center normally can't see.

## 1.3 Design Solution

**Do not add a DB-level `UNIQUE` constraint.** Three reasons: (a) blank/NULL Tazkira is common and legitimate for pending registrations (children, delayed ID issuance); (b) the real requirement is a *cross-table* check (Case + Family + Applicant), which a single-table `UNIQUE` index cannot express at all; (c) if any duplicate already exists in production data today (plausible, given zero enforcement so far), a `CREATE UNIQUE INDEX IF NOT EXISTS` would fail at the next startup migration and could block the app from launching — an unacceptable upgrade risk on a production humanitarian system.

**Recommended:** an application-level **soft-warning, confirm-to-proceed** check — the same UX pattern already proven in this codebase for accounting (`IdenticalTransaction_IsBlockedUnlessConfirmed` — duplicate detected, user must explicitly confirm to proceed). Add one shared lookup method (e.g. `DuplicateDetector.FindByTazkira(tazkira, excludeEntity, excludeId)`) that queries all three tables for a normalized match, called from each save path just before the INSERT/UPDATE, skipped entirely when the field is blank.

## 1.4 Files & Methods Involved

| File | Change |
|---|---|
| `Helpers/DuplicateDetector.cs` | Add `FindByTazkira(...)` — reuses existing `NormalizeIdentifier` |
| `FrmCase.cs` | Call it in `btnSave_Click`/`UpdateCurrentCase`, before the INSERT/UPDATE |
| `FrmFamily.cs` | Call it in `btnSave_Click` |
| `FrmApplicant.cs` | Call it in `SaveApplicant` |

## 1.5 Database Changes Required

None mandatory. Optional, safe, additive: non-`UNIQUE` `CREATE INDEX IF NOT EXISTS` on the three Tazkira columns, purely to keep the new lookup query fast at scale.

## 1.6 UI Validation Required

A confirmation dialog listing existing matches (case code / member name / applicant name) with an explicit "Yes, continue anyway" action — never a hard block, since legitimate near-duplicates exist (data-entry corrections, re-issued Tazkira numbers, twins).

**Open product question that must be answered before implementation:** should the match be checked (and displayed) across *all* centers, or only the current user's center? A national-ID duplicate is meaningful nationally, but showing "already registered at Center X" to an Operator in Center Y is a visibility leak relative to the existing `CenterGuard` isolation model. Recommend showing a non-identifying "a matching Tazkira already exists elsewhere" message to non-SuperAdmin/Admin roles, with full detail (which case/center) shown only to Admin/SuperAdmin.

## 1.7 Risk Analysis

| Risk | Severity | Mitigation |
|---|---|---|
| Center-isolation leak in the confirm dialog | 🟡 Medium | Role-based detail redaction (above) |
| Under-matching due to Tazkira format variance | 🟢 Low | Reuse existing `NormalizeIdentifier`/`NormalizeTazkira` logic already proven elsewhere |
| Performance of cross-table SELECT | 🟢 Low | Add the optional indexes; query is a simple equality lookup |
| Data/schema risk | 🟢 None | Fully additive, no constraint, no migration required |

## 1.8 Implementation Plan

1. Resolve the center-scoping/visibility product decision (§1.6) — blocking prerequisite.
2. Add `FindByTazkira` to `DuplicateDetector.cs`.
3. Add optional performance indexes (`DatabaseInitializer.cs`).
4. Wire the confirm dialog into `FrmCase.cs`.
5. Wire into `FrmFamily.cs`.
6. Wire into `FrmApplicant.cs`.
7. (Stretch, optional, lower priority) Extend `DuplicateDetector`'s existing report (`FrmDuplicates.cs`) to also scan Family/Applicant, for consistency with the new save-time check.

## 1.9 Rollback Plan

Purely additive — revert the commit. No schema constraint was added, so no data migration or cleanup is needed even after the fact.

## 1.10 Test Plan

- Blank Tazkira never triggers the dialog, in all 3 forms.
- Exact duplicate within the same table (Case↔Case, Family↔Family, Applicant↔Applicant) surfaces the dialog.
- Cross-table duplicate (e.g. Case guardian Tazkira == an existing Family member's Tazkira) surfaces the dialog.
- Confirm → save proceeds; Cancel → save aborts, no partial write.
- Center-scoping: verify the chosen visibility policy (redacted vs. full detail) per role.
- Full 346-test regression suite stays green.

**Estimated effort:** 3–4 days. **Risk level:** 🟡 Medium (mostly product-decision risk on center-scoping, not code risk).

---

# TASK 2 — Record Locking Activation

## 2.1 Current State

`Enterprise/LockService.cs` is a **complete, already-working engine**: `TryAcquire` (purges expired locks for the entity, renews if held by the same user, returns holder info if held by another, fails open on any exception), `Heartbeat`, `Release` (by lock id or by entity), `ForceRelease` (Admin-only, audited), `ReleaseAllForCurrentUser`, `Describe` (read-only check), `GetActiveLocks`, `PurgeExpired`. Backed by `EntRecordLock` (`UNIQUE(EntityName, EntityID)`, `ExpiresAt`), with expired rows auto-deleted at every app startup.

`Enterprise/FrmLocks.cs` is a **fully functional admin screen** — but purely observational/corrective (view active locks, force-release, purge expired). It never calls `TryAcquire` itself.

**The gap:** a full-codebase grep for `LockService.` finds exactly 6 references, all inside `FrmLocks.cs` and Dev Center diagnostics. **Zero calls to `TryAcquire`, `Release`, or `Heartbeat` exist anywhere.** `FrmCase.cs`, `FrmFamily.cs`, `FrmDocs.cs` each have **zero** references to "Lock" at all. Today, two users editing the same case/family/document simultaneously is pure last-write-wins with no guard whatsoever — the `RowVersion` column that could detect this exists only inside the **offline sync merge engine** (`SyncApplier.cs`), a completely separate concurrency domain from live interactive editing. `ReleaseAllForCurrentUser` has no caller anywhere (dead code today).

**Lifecycle precedents that already exist and can be reused:** `FrmDocs.cs` and `FrmFamily.cs` already override `OnFormClosed` (for unrelated cleanup — preview/photo disposal) — these hooks can be *extended* rather than newly created. `FrmCase.cs` has no such override today — a new one is needed. No `AppDomain.ProcessExit` handler exists anywhere. `SessionTimeoutMonitor.cs` performs a hard `Application.Exit()` on idle timeout and does **not** release any locks — on timeout, a held lock only clears via its own `ExpiresAt` (15-minute default), not immediately.

## 2.2 Missing Pieces

1. **Acquire-on-open** — nothing calls `TryAcquire` when a record is opened/entered for editing.
2. **Heartbeat** — nothing renews a lock during a long edit session; without one, a lock silently expires after 15 minutes even while someone is actively typing.
3. **Release-on-close** — nothing calls `Release` when editing finishes or the form closes.
4. **Logout wiring** — `ReleaseAllForCurrentUser` is fully built and completely unused.
5. **Architectural wrinkle:** `FrmFamily`/`FrmDocs` are not always independent modal dialogs — the case-management audit previously found `FrmCase` keeps a single long-lived *embedded* instance of `FrmFamily` inside a tab (`_embeddedFamily`, reused across many different case selections via `RefreshForCase`, not re-constructed per case). Lock acquire/release for the embedded case must therefore be tied to "the currently displayed record changed" rather than to the embedded form's own open/close lifecycle — a real design nuance specific to this codebase, not a generic "wire OnFormClosed everywhere" problem.

## 2.3 Flows

**Open record flow (today):** load record → populate fields → (optionally) `btnEdit_Click` unlocks UI fields → no lock touched anywhere in this path.

**Proposed lock acquisition flow:** on entering edit mode (`btnEdit_Click`, or on selecting a record for the embedded Family/Docs tabs), call `LockService.TryAcquire(entityName, id)`. If `Acquired == false`, use `Describe`/holder info to show "locked by <user> since <time>" and — for V1 — **warn, do not hard-block** (consistent with the low-concurrency, branch-office reality this system runs in; `FrmLocks` already gives an Admin a manual override path for genuinely stuck locks).

**Lock release flow:** on successful save-and-exit-edit-mode, on Cancel, on form close (`OnFormClosed`/`FormClosing`), and on switching to a different record within an embedded tab — call `Release(entityName, id)`. Multiple exit paths must each be covered; a missed one leaves an orphaned lock that self-heals only at natural expiry.

**Crash recovery flow:** already correctly designed and requires no new code — `ExpiresAt` + the startup `DELETE FROM EntRecordLock WHERE ExpiresAt <= datetime('now')` purge is the existing safety net for any ungraceful exit (crash, kill, power loss). Task 2 only needs to make sure the *timeout* is long enough relative to real editing sessions, which is exactly what the heartbeat renewal is for.

**Timeout strategy:** keep the existing 15-minute (`RecordLockMinutes` setting) default expiry; add a heartbeat timer (~5-minute interval, modeled on `SessionTimeoutMonitor`'s existing idle-polling pattern) while a form is in active edit mode, so a lock never expires mid-edit but still self-clears quickly after an ungraceful exit.

## 2.4 Risk Analysis

| Risk | Severity | Notes |
|---|---|---|
| Embedded-tab lock lifecycle mismatch | 🔴 High | The single largest technical risk in this task — must tie lock lifetime to "current record shown," not form construction/destruction |
| Missed release path (Cancel / window X / tab-switch / timeout / crash) | 🟠 Medium | Each is a separate code path; crash path is already covered by expiry, but the others need explicit wiring |
| UX friction if policy is "hard block" instead of "warn" | 🟠 Medium | Recommend warn-only for V1, given `FrmLocks`' existing admin override exists as an escape hatch |
| Heartbeat failure (DB hiccup, sleep/resume) | 🟢 Low | `TryAcquire`/existing engine already fails open on exceptions — same posture should apply to heartbeat |
| Same user, two tabs of the same case | 🟢 Low | Already handled — `TryAcquire` renews rather than blocks when the existing holder is the same user |

## 2.5 Implementation Plan

1. `FrmDocs.cs` first — simplest, standalone-ish usage, already has an `OnFormClosed` hook to extend. Use this as the pilot to validate the acquire/heartbeat/release pattern.
2. `FrmFamily.cs` next — also has an existing `OnFormClosed` hook; account for embedded-tab usage from `FrmCase`.
3. `FrmCase.cs` last — highest complexity: needs a new `FormClosing`/`OnFormClosed` override, and must coordinate lock lifetime with `RefreshForCase`'s record-switching behavior for the embedded children.
4. Add the heartbeat timer to all three once the base pattern is proven.
5. Wire `ReleaseAllForCurrentUser` into the logout path (`FrmLogin.cs`/`SecurityContext.SignOut`) — small, independent, high-value fix for the dead-code gap.
6. Decide and implement the warn-vs-block policy for "record is locked by someone else."

## 2.6 Test Scenarios

- Single user: open → edit → save → close → lock acquired then cleanly released.
- Two users, same case: A edits, B opens same case → B sees "locked by A," per chosen policy.
- Crash recovery: kill A's process mid-edit → lock persists until `ExpiresAt` → B can reacquire without manual intervention once expired.
- Admin force-release via `FrmLocks` while A still has the form open → A's next save/heartbeat must not crash.
- Embedded tabs: switch between multiple cases inside one session → previous case's lock releases, new case's lock acquires, no orphans left behind.
- Logout releases all of that user's locks.
- Full 346-test regression suite stays green; no existing single-user edit flow is broken.

**Estimated effort:** 1.5–2 weeks. **Risk level:** 🟠 Medium-High (touches the three most-used forms in the entire application; embedded-tab architecture adds genuine complexity).

---

# TASK 3 — Financial Period Enforcement

## 3.1 Current State

This feature is **largely already implemented**, not a from-scratch gap. `AccPeriod.Status` (free-text 'باز'/'بسته', no `ClosedBy`/`ClosedAt` columns) is enforced via a shared repo-layer guard, `AccountingRepo.EnsureMutable(...)`, which throws if the period is closed or the record already reversed. This guard **is correctly called** by `ReviseTransactionAtomic`, `VoidTransaction`, `UpdateStipend`, `VoidStipend`, and `UpdateExpenseItem`/`VoidExpenseItem`. `FrmAccounting.cs` additionally blocks new postings at the UI level (`IsPeriodOpen` checks before new transaction, new stipend, new salary, new expense, and period edit) and specifically blocks loading a transaction for revision when its *own* period is closed (a prior bug fix, per an in-code comment).

## 3.2 Missing Pieces (the actual gaps)

| # | Gap | File:Method | Severity |
|---|---|---|---|
| 1 | **`UpdateSalary` has no `EnsureMutable`/period check at all** — its sibling `VoidSalary` does | `AccountingRepo.cs:919` | 🔴 **Highest priority** — a salary record inside a *closed* period can be silently edited with zero enforcement, while every other transaction type is correctly protected |
| 2 | No matching UI-level `IsPeriodOpen` check before the salary-edit save, mirroring the gap above | `FrmAccounting.cs` (salary edit path) | 🔴 Same root cause as #1 |
| 3 | `AccRepair.ApplyAssignPeriod`/`ApplyAssignCenter` never check target period's `Status` | `AccRepair.cs` | 🟠 Medium — scoped to already-orphaned (`PeriodID IS NULL`) records only, but can silently fold one into a *closed* period |
| 4 | `AccRepair.ApplyFixDate` has no period-status check (only an optimistic old-value guard) | `AccRepair.cs` | 🟠 Medium — could move a transaction's date without re-validating period boundaries |
| 5 | **No reopen mechanism exists at all** — `SetPeriodStatus` has exactly one caller, always passing "closed" | `AccountingRepo.cs:91`, caller `FrmAccounting.cs:1231` | 🟡 Operational gap, not a security bug — an accidentally-closed period cannot be corrected without a direct DB edit |
| 6 | **No permission gate on closing a period** — any user who can open the Periods tab can close it; no `CanEdit`/`IsSuperAdmin`/`IsAdmin` check near the close handler | `FrmAccounting.cs:1226-1235` | 🟠 Medium — ties into the still-unaddressed Accounting permission gap from the earlier Sprint 0 work (out of scope there, unresolved here too) |
| 7 | No structured `ClosedBy`/`ClosedAt` audit columns — closure is only inferable from free-text `AccAudit` log entries | `Helpers/AccountingInitializer.cs` | 🟢 Low — nice-to-have, not a correctness bug |

`AccIntegrity.cs` is confirmed entirely read-only (explicit header comment) and does not itself bypass anything — it's not part of the gap.

## 3.3 Affected Files & Methods

| File | Method | Fix |
|---|---|---|
| `Accounting/AccountingRepo.cs` | `UpdateSalary` (:919) | Add `EnsureMutable` call, matching `UpdateStipend`/`UpdateExpenseItem` |
| `Accounting/FrmAccounting.cs` | Salary-edit save handler | Add `IsPeriodOpen` pre-check, matching sibling flows |
| `Accounting/AccRepair.cs` | `ApplyAssignPeriod`, `ApplyAssignCenter`, `ApplyFixDate` | Add target-period `Status` check with an explicit SuperAdmin override/reason path (not a hard block — repairs sometimes legitimately need to touch closed-period data) |
| `Accounting/FrmAccounting.cs` | `CloseSelectedPeriod` (:1226) | Add a permission check before allowing close |
| `Accounting/AccountingRepo.cs` | `SetPeriodStatus` (:91) | (Optional, product decision) add reopen support, SuperAdmin-only + mandatory reason + audit |
| `Helpers/AccountingInitializer.cs` | `AccPeriod` schema | (Optional) add `ClosedBy`/`ClosedAt` columns via additive `EnsureColumn` |

## 3.4 Risk Analysis

| Item | Risk | Rationale |
|---|---|---|
| #1/#2 `UpdateSalary` fix | 🟢 Very low | Copies an already-proven pattern used by 2 sibling methods in the same file; single guard-clause addition |
| #3/#4 `AccRepair` checks | 🟡 Medium | Must not accidentally block legitimate historical-data repairs — needs an override path, not a hard block, consistent with `AccRepair`'s existing "mandatory reason" philosophy |
| #6 permission gate on close | 🟢 Low (mechanically) / 🟡 Medium (sequencing) | Simple additive check, but should reuse the `Accounting.*` permission-key line of work (deferred from Sprint 0A/0B) rather than inventing a parallel one-off role check |
| #5 reopen capability | 🔴 Highest in this task | Lets a user retroactively alter data assumed to be locked/already reported to donors — needs an explicit product-policy decision on whether it should exist at all before any design work starts |
| #7 audit columns | 🟢 None | Purely additive schema, optional |

## 3.5 Implementation Plan

1. **Quick win, do first:** fix `UpdateSalary` (#1/#2) — mirrors existing sibling code, near-zero risk, closes a real live financial-integrity gap.
2. Add `Status` checks to `AccRepair`'s three methods (#3/#4), with an explicit override/reason path.
3. Add a close-period permission gate (#6) — sequence this *after* the Accounting permission-key work if/when it lands, to avoid a parallel one-off mechanism.
4. **Separately, gated on a product decision:** design (or explicitly decide not to build) a reopen-period capability (#5) — SuperAdmin-only, mandatory reason, fully audited, if pursued at all.
5. **Optional, lowest priority:** add `ClosedBy`/`ClosedAt` columns (#7).

## 3.6 Rollback Strategy

All changes are additive guard clauses or optional new nullable columns — revert the commit to roll back; no destructive migration involved even for the schema addition.

## 3.7 Test Checklist

- Attempt a salary edit inside a closed period → currently succeeds silently (proves the gap); must be blocked after the fix.
- Existing stipend/expense-item closed-period blocks still work unchanged (no regression).
- `AccRepair.ApplyAssignPeriod` targeting a closed period → blocked or requires explicit override, per chosen design.
- Closing a period without the required permission → blocked once the gate is added.
- If reopen is implemented: requires SuperAdmin + reason; subsequent edits in the reopened period succeed; re-closing still works.
- Full 346-test regression suite (plus any new accounting tests) stays green.

**Estimated effort:** 3–5 days for items 1–3 (the core fix set); +1 week if the reopen capability (#5) is pursued, plus a mandatory upfront product-policy decision. **Risk level:** 🟢 Low for items 1–3; 🟡 Medium if reopen capability is added.

---

# CROSS-TASK SUMMARY

| Task | Current State | Missing Pieces | Est. Effort | Risk Level |
|---|---|---|---|---|
| **1 — Duplicate Tazkira Block** | No check anywhere; existing `DuplicateDetector` is Case-only and manual/on-demand | Cross-table save-time warning; center-scoping policy decision | 3–4 days | 🟡 Medium |
| **2 — Record Locking Activation** | Engine + admin UI fully built; zero wiring into any data-entry form | Acquire/heartbeat/release wiring in 3 forms; embedded-tab lifecycle design; logout wiring | 1.5–2 weeks | 🟠 Medium-High |
| **3 — Financial Period Enforcement** | Mostly implemented and working; one sibling method (`UpdateSalary`) missing the guard everyone else has | `UpdateSalary` fix; `AccRepair` period checks; close-period permission gate; reopen capability (policy decision) | 3–5 days (core) / +1wk (reopen) | 🟢 Low (core) / 🟡 Medium (reopen) |

---

# FINAL RECOMMENDATION

**1. Which task should be implemented first?**
**Task 3's core fixes (items 1–3)** — specifically the `UpdateSalary` guard. It is the smallest, fastest, and safest possible change (literally copying a pattern already proven twice in the same file), it closes a real, live financial-correctness gap, and it produces an early win before committing to the much larger design work in Task 2.

**2. Which task gives the highest risk reduction?**
**Task 2 — Record Locking Activation.** Today, *every* concurrent edit of *any* case, family member, or document in the entire system is silent last-write-wins with zero protection — this is the single largest data-loss exposure in the whole application (matches the original system audit's CM-05 finding), and it affects the most heavily used forms, every day, for every user. Task 1's exposure (duplicate entries, recoverable via manual review) and Task 3's exposure (a narrow set of closed-period edge cases) are both smaller in blast radius by comparison.

**3. Which task is safest to implement?**
**Task 3's core items (1–3).** Narrowest surface, mirrors already-proven code exactly, fully additive, no schema constraint risk. (This does *not* extend to Task 3's optional reopen-period capability, which is the highest-risk single item across all three tasks and should be scoped and decided separately.)

**4. Which task is most likely to break existing functionality?**
**Task 2 — Record Locking Activation.** It changes the interactive behavior of the three most-used forms in the application, must correctly handle `FrmCase`'s non-standard embedded-tab lifecycle across many exit paths (save, cancel, window close, session timeout, crash, admin force-release), and any missed release path or wrong policy choice (hard-block vs. warn) risks stalling real daily work for the whole staff — a materially larger surface for accidental regression than Task 1 (three isolated, additive save-time checks) or Task 3 (a handful of narrow guard-clause additions).

**5. Recommended execution sequence:**
1. **Task 3, core items (1–3)** — quick, safe, real win.
2. **Task 1** — after resolving the center-scoping/visibility product decision; moderate risk, directly serves the core data-quality mission of the system.
3. **Task 2** — start with `FrmDocs.cs` as the simplest pilot, then `FrmFamily.cs`, then `FrmCase.cs` last given its embedded-tab complexity; do this last so the team has full runway to design the lock lifecycle properly rather than under time pressure.
4. **Deferred, separately scoped:** Task 3's reopen-period capability (#5) — gated on an explicit product-policy decision, and ideally sequenced after any future Accounting permission-key work so the close/reopen gate reuses that system rather than a one-off check.

No code was modified, no commits were created, in accordance with the task constraints.
