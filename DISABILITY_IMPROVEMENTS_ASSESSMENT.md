# Disability Improvements — Impact Assessment & Implementation Report

**Date:** 2026-09-04 · **Scope:** the six requested improvements
**Method:** analyse → impact → risk → decide → implement only if safely validatable

**Outcome: 1 of 6 implemented. 1 needed no change. 4 deferred with plans.**

| # | Item | Class | Safe? | Action |
|---|---|---|---|---|
| 3 | Migration Document Type visibility | Release Critical | **Yes** | **Implemented + tested** |
| 2 | Form cleanup (orphan fields on disability cases) | — | n/a | **Already correct** — locked with tests |
| 4 | Individual photo upload (delete) | Release Important | Conditional | **Design ready, needs go-ahead** |
| 5 | Representative review | Release Important | Partial | **Audited; 3 gaps, all deferred** |
| 1 | Disability field consolidation | Future Enhancement | **No** | Migration plan only |
| 6 | Project-based allocation | Future Enhancement | **No** | Roadmap only |

---

## Item 3 — Migration Document Type Visibility ✅ IMPLEMENTED

### 1. Current State (before)
`txtMigrationCardType` («نوع برگه مهاجرت») was added to the case grid at
`FrmCase.Designer.cs:381` as a **bare** `AddCaseField(...)` call — its `FieldBox`
was never captured into any section array. Every other migration field lives in
`migrantSectionFields`, which `UpdateRequestTypeSectionVisibility()` hides via the
data-driven `ShowMigrantSection` flag.

**Consequence: the field was visible on all six request types**, including
Disability, Orphan and Elderly. This is a genuine defect, and the only remaining
always-visible cross-type field on the form. The in-code comment at
`FrmCase.cs:550` confirms it was a deliberate Phase-3 carve-out ("existing
`DisabilityType`/`DisabilityDegree`/`MigrationCardType` are intentionally left
untouched") that was never revisited.

### 2. Impact Analysis

| Layer | Affected? | Detail |
|---|---|---|
| Database | **No** | No schema change. `TblCase.MigrationCardType` untouched |
| Save path | **No** | Written unconditionally at `FrmCase.cs:2724/3528/3746`, independent of the section flag. Hiding does not change what is written |
| Data loss | **No** | `SetFieldGroupVisible` sets `Visible` only — it never clears values. A hidden field round-trips its loaded value on save |
| Sync | **No** | Column-level sync of `TblCase`, unaffected by UI visibility |
| Backup/restore | **No** | Unaffected |
| Reports (RDLC) | **No** | `DsFullCaseReport` still carries `MigrationCardType`; printing unchanged |
| Exports | **No** | No export reads form visibility |
| Search | **No** | `FrmAdvancedSearch` does not filter on this field |
| Permissions | **No** | Unchanged |
| Backward compat | **Preserved** | A legacy non-migrant case holding a value keeps it, and still prints it |

**Affected files: 1** — `FrmCase.Designer.cs` (3 lines: capture the `FieldBox`,
add it to the array, declare the field). Follows the existing
`fieldCoveredByOrgNames` precedent exactly.

### 3. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Field hidden for a migrant case | Low | `ShowMigrantSection = 1` for `MIGRANT`, asserted by test |
| Value silently cleared when hidden | **None** | Verified: `SetFieldGroupVisible` only toggles `Visible` |
| Layout shift | **None** | Position in the grid is unchanged; only the visibility wiring changed |
| Legacy data with a value on a non-migrant case | Low | Preserved and still reported; only the input is hidden |

### 4. Recommendation
Implement. Smallest possible change, closes a real defect, matches the stated
requirement exactly ("visible only for Migration cases").

### 5. Safe To Implement? **YES — done.**

### 6. Testing Results
- Build: clean, 0 errors; warnings all pre-existing (17 baseline).
- `MigrationCardType_IsWiredIntoMigrantSection` — **PASS** (non-vacuous: the
  array had 8 entries and none was this control before the change).
- `MigrantSectionFlag_IsOnlyOnForMigrantCases` — **PASS** (on for `MIGRANT`, off
  for معلول / ایتام / کهن‌سال).
- Regression: **88 tests green** — disability (10), representative (23),
  activation gate, case grid, exports, report templates, record history, domain
  migration, dashboard filter.

### 7. Rollback
Revert the 3 lines in `FrmCase.Designer.cs`; backup at
`scratchpad/backup_170607/FrmCase.Designer.cs`. Code-only, no migration, no data
change — reverting fully restores prior behaviour.

---

## Item 2 — Form Cleanup ✅ ALREADY CORRECT (no change needed)

### 1. Current State
**All six named examples are already correctly hidden for Disability cases.**
The Phase-3/4 section mechanism handles them:

| Field | Group | `DISABLED` flag | Hidden? |
|---|---|---|---|
| Child Guardian Name (`txtGuardianName`) | `guardianSectionFields` | `guardian: 0` | ✅ |
| Guardian Relationship (`txtGuardianRelationship`) | `guardianSectionFields` | `guardian: 0` | ✅ |
| School Name (`txtOrphanSchoolName`) | `orphanSectionFields` | `orphan: 0` | ✅ |
| Education Level (`txtOrphanEducationLevel`) | `orphanSectionFields` | `orphan: 0` | ✅ |
| Education Status (`chkIsStudent`) | `orphanSectionFields` | `orphan: 0` | ✅ |
| Original Residence (province/district/village) | `orphanSectionFields` | `orphan: 0` | ✅ |

Seeded at `DatabaseInitializer.cs:1051`:
`SetRequestTypeSections("DISABLED", orphan: 0, disability: 1, migrant: 0, guardian: 0)`.

**A full sweep of the case grid found exactly one incorrectly-displayed field —
`MigrationCardType`, which is Item 3.** Every other field in `gridCase` is
genuinely universal (code, form no, zone, province, request type, priority,
covered-by-org, case date, service status, location, surveyors, referrer).

### 2. Impact Analysis
No change required. The mechanism is data-driven — an administrator can retune
any type's sections from `TblRequestType` without a recompile.

### 3. Risks
Only that the behaviour regresses unnoticed. Previously **untested**.

### 4. Recommendation
Add no functionality. Lock the existing correct behaviour with tests.

### 5. Safe To Implement? **N/A — no change made.** Tests added.

### 6. Testing Results
- `OrphanOnlyFields_AreHiddenForDisabilityCases` — **PASS**
- `OrphanOnlyFields_AreGroupedSoTheyCanBeHidden` — **PASS** (asserts all 8
  controls are in the right groups, so a future refactor cannot silently unhide them)

### 7. Rollback
Delete `CaseManagement.Tests/CaseFormSectionVisibilityTests.cs`. Test-only.

---

## Item 1 — Disability Field Consolidation ❌ NOT SAFE — DEFER

### 1. Current State
The 11 disability fields are split across two tabs by **two different gating
mechanisms**:

| Location | Fields | Gate |
|---|---|---|
| «مشخصات جسمی» (`gridPhysical`) | `DisabilityType`, `DisabilityDegree` | `chkHeadHealthy` checkbox (**enable/disable + clears text**) |
| «مشخصات پرونده» (`gridCase`, `disabilitySectionFields`) | the other 9 (cause, description, special needs, card status, card number, issuer, issue date, expiry date, notes) | `ShowDisabilitySection` flag (**show/hide**) |

### 2. Impact Analysis — dependencies of the fields to be moved

| Consumer | Dependency | Breaks on move? |
|---|---|---|
| Save/load (`FrmCase.cs`) | By control **name**, not container | No |
| `CaseModuleService` dual-write | Control name | No |
| Excel / Word export | Reads DB, not the form | No |
| RDLC report | Reads DB | No |
| `FrmAdvancedSearch` | Reads DB + `TblLookup` | No |
| Vulnerability scoring, assistance rules | Read DB | No |
| **TabIndex sequence** (`FrmCase.cs:~472`) | Explicit ordinal assignment | **Yes** — needs resequencing or keyboard navigation becomes erratic |
| **Gating semantics** | Two incompatible models in one visual card | **Yes — the real blocker** |

**The blocker is not the move; it is the gating.** Putting all 11 fields in one
card leaves `chkHeadHealthy` disabling 2 of them while the other 9 stay enabled —
visibly inconsistent in a single card. Making the checkbox govern all 11 is a
**behaviour change** to a Critical-adjacent path (`chkHeadHealthy` *clears* the
text when ticked, so wrong wiring would silently erase card numbers and dates —
the same class of defect as C1).

There is also an unresolved semantic question: `chkHeadHealthy` means "*the head*
is healthy". A Disability case may have a healthy head and a disabled *member*.
Merging the two groups forces an answer to that question that nobody has made yet.

### 3. Risks

| Risk | Severity |
|---|---|
| Data loss via `chkHeadHealthy` clearing newly-governed fields | **High** |
| TabIndex/keyboard navigation regression | Medium |
| Layout regression on a form with 2506 designer lines | Medium |
| No automated coverage for visual layout — validation is manual only | Medium |

### 4. Recommendation — **defer.** Migration plan for after handover:
1. **Decide the semantics first** — does "head is healthy" govern the card fields?
   (Recommendation: no. Rename the checkbox to scope it to type/degree only.)
2. Move the 9 `AddCaseField(gridCase, …)` calls to `gridPhysical`, keeping them in
   `disabilitySectionFields` — visibility wiring stays identical.
3. Resequence TabIndex across the physical card.
4. Leave `chkHeadHealthy` governing only type/degree; do **not** extend its clearing.
5. Validate: full round-trip save/load of all 11 fields; C1 regression (empty
   dates stay empty); C2 regression (activation with card status «ندارد»).

**Reason to defer:** this is pure UI reorganisation with zero data, reporting or
correctness benefit, on the most complex form in the system, days before handover.
It is the definition of a change that trades stability for polish.

### 5. Safe To Implement? **NO.**

### 6. Testing Requirements (when done)
Round-trip persistence for all 11 fields; C1 and C2 regression; tab-order test;
manual visual check on a real laptop at the target resolution.

---

## Item 4 — Individual Photo Upload ⚠️ MOSTLY EXISTS — 1 GAP, DESIGN READY

### 1. Current State
Four of the five requirements **already exist**:

| Requirement | Status | Where |
|---|---|---|
| Upload single image | ✅ Exists | `btnBrowsePhoto` (personal), `btnBrowseFamilyPhoto` (group), `btnBrowseMemberPhoto` (member), 2 representative slots |
| Replace image | ✅ Exists | Browsing again replaces; `FileHelper.SaveFileToCaseFolder` takes the old path and supersedes it |
| **Delete image** | ❌ **MISSING** | Only representatives have it (`btnRep1ClearPhoto`/`btnRep2ClearPhoto`) |
| Use for unsynchronised photos | ✅ Exists | Manual browse is exactly the fallback when HTML sync brings no photo |
| Use for group photos | ✅ Exists | `btnBrowseFamilyPhoto`, labelled «عکس جمعی» |

**The single real gap is deletion** for the personal, group and member photos.
Today a wrong photo can only be replaced, never removed — there is no in-app way
to clear one, which matters because the photo prints on the beneficiary card.

### 2. Impact Analysis
**The save path already supports deletion — no service change is needed.**
`SaveSelectedPhotos()` (`FrmCase.cs:2835`) falls through to
`txtPhotoPath.Text = savedHeadPhotoPath`. Setting `savedHeadPhotoPath = ""` and
`selectedHeadPhotoSource = ""` therefore writes an empty column — exactly what
`ClearRepresentativePhoto` already does. The work is **UI-only**:

| Layer | Change |
|---|---|
| `FrmCase.Designer.cs` | 2 buttons + handler wiring (proven 3-control layout from the representative cards) |
| `FrmCase.cs` | 1 clear method mirroring `ClearRepresentativePhoto` |
| `FrmFamily.*` | 1 button + clear for the member photo |
| DB / sync / backup / reports / exports / permissions | **None** |

Deliberate design point: follow the established convention of **clearing the
reference but leaving the file on disk** (as `FrmDocs`, field-visit photos and
`ClearRepresentativePhoto` all do), so a mis-click is never unrecoverable.

### 3. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Layout shift on the FrmCase photo cards | **Medium** | Screenshot/layout tests exist; the representative cards prove the 3-control pattern fits |
| Accidental deletion | Low | Confirmation dialog; file retained on disk |
| Card printing with a now-empty photo | Low | Already handles missing photos |
| Cleared photo not persisted until save | Low | Matches existing photo semantics |

### 4. Recommendation
**Implement the delete buttons — but not without your go-ahead**, because it adds
UI to `FrmCase`, the most-used form, and layout is the one thing the test suite
cannot fully validate. Classified **Release Important**: an operator who attaches
the wrong beneficiary photo currently has no remedy short of a DB edit.

I stopped here deliberately rather than modifying `FrmCase` layout days before
handover without explicit approval.

### 5. Safe To Implement? **CONDITIONAL — yes for FrmFamily, needs approval for FrmCase.**
The `FrmFamily` member photo is the lowest-risk of the three (simpler layout,
`photoPanel` already stacks a button under the picture box).

### 6. Testing Requirements
Clear → save → reopen shows no photo; DB column empty; file still on disk;
card print with no photo; screenshot tests re-baselined; manual check at target
resolution.

---

## Item 5 — Representative (Lawyer) Feature Review ✅ AUDITED — 3 GAPS

### 1. Current State
Independently audited (`LAWYER_FEATURE_AUDIT.md`). Scoped to Disability:
`ShowRepresentativeSection = 1` for `DISABLED`, `0` for the other five.

| Layer | Status |
|---|---|
| Database | ✅ `TblCaseRepresentative`, FK CASCADE, `GlobalID`, `CenterID`, `UNIQUE(CasID, Order)`, 4 indexes, additive/idempotent DDL |
| Sync | ✅ Registered in `SyncedTables`; two-phase delete (same pattern as the H1 fix) |
| Search | ✅ 4 filters, built with `EXISTS` — cannot multiply case rows |
| Backup/restore | ✅ All 4 paths (load, merge, whole-replace, delete-current) |
| Audit | ✅ Field-level old→new, create/delete; unchanged saves write nothing |
| UI | ✅ Tab + 2 photo slots + validation (10 validation tests) |
| Word export | ✅ `{{Rep1*}}`/`{{Rep2*}}` placeholders, additive |
| **Excel export** | ❌ **Absent** (verified) |
| **RDLC report** | ❌ **Absent** — 0 occurrences in `DsFullCaseReport.xsd` (verified) |
| **Permissions** | ⚠️ **No dedicated key** — anyone with `Case.Edit` can add/edit a legal representative |

Coverage: **23 tests / 57 assertions** — all passing in my regression run.

### 2. Impact Analysis of closing the gaps
- **Excel export:** `TblCaseRepresentative` is **1:N**. A `LEFT JOIN` — the pattern
  used for the disability fields — would **multiply case rows** and corrupt every
  count in the sheet. It requires correlated subqueries or aggregation instead.
  This is a materially different and riskier change than the disability M2 fix.
- **RDLC:** requires editing `RptFullCase.rdlc` *and* `DsFullCaseReport.xsd` plus
  its 3 generated files — the highest-risk reporting change available, and the
  same reason R1 was deferred.
- **Permission:** adding `Case.Representative.Edit` means a new seeded key across
  4 roles plus a new `LegacyFallback` surface. `LegacyFallback` is **fail-open**,
  so a typo would grant access to every logged-in user.

### 3. Risks

| Gap | Severity | If left as-is |
|---|---|---|
| Not in Excel export | Low | Same known limit as the disability fields; Word export covers it |
| Not in RDLC | Low | Same as R1 |
| No dedicated permission | Low | Any `Case.Edit` holder (SuperAdmin/Admin/Operator) can edit a representative. Viewer **cannot**. Acceptable for a section of the case form |

### 4. Recommendation
**Ship as-is; fix nothing now.** No Critical or High gap exists. All three gaps
are consistent with how the rest of the system already behaves, and each fix is
in a higher risk class than the gap it closes.
**Still required before handover: the 9-step manual smoke test** in
`LAWYER_FEATURE_AUDIT.md` §9 — manual UI verification is the one assurance gap.

### 5. Safe To Implement (the fixes)? **NO — deferred deliberately.**

### 6. Testing Requirements (when done)
Excel export: assert a case with 2 representatives yields **exactly one row**.
RDLC: render a case with 0, 1 and 2 representatives. Permission: verify each role,
and that an unseeded key is never reachable.

---

## Item 6 — Assistance Allocation Review 📋 ROADMAP ONLY

### 1. Current State — more exists than expected

| Component | Table(s) | Purpose |
|---|---|---|
| Assistance records | `TblAssistance` | Individual grants |
| Packages | `TblAssistancePackage`, `TblAssistancePackageItem` | In-kind bundles |
| Funding sources | `TblFundingSource`, `TblCaseFunding` | Which source funds which case |
| Sponsors | `TblSponsor` | Donor registry |
| **Rule engine** | `TblAssistanceRule`, `TblAssistanceRuleCondition` | Suggested amount, **19 facts**, priority-ordered |

The rule engine already exposes the facts project allocation would need —
including `RequestType`, `DisabilityDegree`, `DisabilityType`,
`HasDisabilityRecord`, `FamilyMemberCount`, `HeadAge`, `CompletionPercent`.
Critically, it is **advisory only**: it suggests an amount, creates no payment and
blocks nothing.

**Known live defect: H4** — assistance entry can silently reclassify a case's
primary type. It is unfixed and is the single biggest obstacle to building
allocation on top of this. `FrmFinance`'s case list also lacks an
`IsArchived = 0` filter, so archived cases are selectable for assistance.

### 2. Required business rules for project-based allocation
1. A project has a **budget ceiling**; allocations may not exceed it.
2. A project has **eligibility criteria** — reuse the existing 19-fact condition
   model rather than inventing a second one.
3. A case may belong to **several projects**; one project must be the funder of
   record for any given grant (otherwise double-counting).
4. Allocation must be **reversible and audited** (who allocated, when, why).
5. Projects are **time-boxed** (start/end); an expired project accepts no new
   allocations.
6. **Center isolation** must hold — a project belongs to a center, and
   `CenterGuard` must apply.

### 3. Required filters / data points
Filters: request type, disability degree/type, vulnerability score, completion %,
province/district, service status, center, existing funding, family size, age band.
New data points: `ProjectID`, budget, spent-to-date, remaining, allocation status,
per-case allocated amount, allocation date, allocating user.

### 4. Risks

| Risk | Severity |
|---|---|
| **H4 must be fixed first** — allocation keyed on request type is unsound while type can silently change | **High** |
| Double-funding a case from two projects | High |
| Budget arithmetic in a synced, multi-center, offline-first system — two branches allocating against one ceiling | **High** |
| Money + sync + offline = the hardest combination in this codebase | High |
| Archived cases selectable for allocation | Medium |

### 5. Recommendation
**Do not build a project engine** (as instructed). Post-handover order:
1. **Fix H4 first** — it is a precondition, not a nice-to-have.
2. Add the `IsArchived = 0` filter to `FrmFinance`.
3. Design `TblProject` + `TblProjectAllocation` reusing the existing condition model.
4. Treat the budget ceiling as **advisory** in v1, exactly as the rule engine is
   advisory today — enforcing a hard ceiling across offline branches requires a
   reservation protocol this system does not have.

### 6. Safe To Implement? **NO — and none was attempted.**

### 7. Testing Requirements (when built)
Budget never exceeded under concurrent branch allocation; a case funded by two
projects is counted once; expired projects reject allocations; center isolation
holds; allocations survive backup/restore and sync round-trip.

---

## Consolidated Testing Results

| Suite | Result |
|---|---|
| `CaseManagement` build | **Clean** — 0 errors, warnings all pre-existing |
| `CaseManagement.Tests` build | **Clean** |
| New `CaseFormSectionVisibilityTests` | **4 / 4 pass** |
| Regression batch 1 (disability, representative, activation gate, case grid) | **54 / 54 pass** |
| Regression batch 2 (exports, report templates, record history, domain migration, dashboard filter) | **34 / 34 pass** |
| **Total verified** | **92 tests, 0 failures** |

> Whole-suite runs truncate under `vstest.console` in this environment
> (pre-existing, documented). Batched `--TestCaseFilter` runs are stable and were
> used throughout.

## Rollback Strategy

| Change | Rollback |
|---|---|
| `FrmCase.Designer.cs` (3 lines) | Restore `scratchpad/backup_170607/FrmCase.Designer.cs`, or revert the 3 `fieldMigrationCardType` lines |
| `CaseFormSectionVisibilityTests.cs` (new) | Delete the file |

Both are code-only. **No schema change, no migration, no data modification.**
`git checkout` is *not* a valid rollback here — the working tree carries
uncommitted work from a concurrent session, so a checkout would revert that too.
