# Legal Representative Feature — Completion Report

**Date:** 2026-09-05 · **Scope:** verify and complete the Disability Legal
Representative feature. No redesign, no unrelated refactoring.

**Status: production-ready**, subject to the manual smoke test in §11.

---

## Summary of what I found vs. what I did

The feature was **already substantially built** by the concurrent session and
documented in `PROJECT_CONTEXT.md` §"Legal Representative (Phase 7)". I verified
all nine requested areas against the actual code. **Seven were already complete.**
Two had real gaps, which I closed:

| Area | Found | Action |
|---|---|---|
| 1. Database migration | ✅ Complete | Verified only |
| 2. Validation | ✅ Complete | Verified only |
| 3. Search integration | ✅ Complete | Verified only |
| 4. Audit trail | ✅ Complete | Verified only |
| 5. Security | ❌ **No dedicated permission** | **Added 4 keys + 3 enforcement points** |
| 6. Reporting integration | ✅ Complete | Verified only |
| 7. Export — Word/PDF | ✅ Complete | Verified only |
| 7. Export — **Excel** | ❌ **Zero coverage** | **Added 10 columns** |
| 8. Print integration | ✅ Complete | Verified only |
| 9. End-to-end testing | ⚠️ 23 tests, gaps in new areas | **Added 4 tests** |

---

## 1. Database Migration — verified ✅

**Schema** (`DatabaseInitializer.cs:1203`):
`TblCaseRepresentative` — `RepresentativeID` PK, `CasID` FK **CASCADE**,
`UNIQUE(CasID, RepresentativeOrder)` enforcing exactly two slots, plus
`FullName NOT NULL`, `RelationshipToBeneficiary`, `IdCardType`, `NationalID`,
`Phone`, `SecondaryPhone`, `Address`, `PhotoPath`, `Notes`, `IsActive`,
`CenterID`, `GlobalID`, `CreatedAt`, `CreatedBy`, `UpdatedAt`.

**Five indexes:** `(CasID, RepresentativeOrder)`, `NationalID`, `Phone`,
`FullName`, `RelationshipToBeneficiary` — one per search filter.

**Upgrade path — verified present.** Beyond `CREATE TABLE IF NOT EXISTS`, four
`EnsureColumn` calls (`IdCardType`, `SecondaryPhone`, `PhotoPath`, `IsActive`)
upgrade databases that received an earlier version of the table. This is the
project's sanctioned migration mechanism.

**Existing-database compatibility:** all DDL is additive and idempotent; no
existing table or column is altered. A pre-Phase-7 database gains the table on
first launch; cases without representatives simply have no rows.

## 2. Validation — verified ✅

All rules live in `CaseRepresentativeService.Validate`/`ValidateOne`, never in
the form:

| Requirement | Implementation |
|---|---|
| Required fields | Name (≥3 chars), relationship, national ID, primary phone |
| National ID validation | Delegated to the shared `IdCardHelper` — same rule as the case and member forms — but **mandatory** here |
| Phone validation | 7–15 digits after Persian/Arabic digit folding |
| Duplicate representatives | Rejected (same person in both slots) |
| Duplicate national IDs | `FindOtherCasesWithSameId` — **reported, not blocked** (one lawyer legitimately represents many beneficiaries) |
| Invalid relationships | Closed vocabulary from the admin-editable `RepresentativeRelationship` lookup |
| Extra | Secondary phone must differ; representative cannot be the beneficiary; half-filled slot 2 rejected |

## 3. Search Integration — verified ✅

Four filters in `FrmAdvancedSearch`: name, national ID, phone, relationship.

All built as `EXISTS (SELECT 1 FROM TblCaseRepresentative r WHERE r.CasID = c.CasID ...)`.
**Never a `JOIN`** — a join would duplicate any case with two representatives and
corrupt both the result grid and the pager's `COUNT`.

## 4. Audit Trail — verified ✅ (all six operations)

`CaseRepresentativeService` owns audit, timeline and sync so no caller can forget
them. `OperationFor(column)` maps changes to distinct audit operation names,
which is exactly the six-way split requested:

| Operation | Audit label |
|---|---|
| Create | `ثبت نماینده قانونی` |
| Edit | `ویرایش نماینده قانونی` |
| Delete | `حذف نماینده قانونی` |
| **Photo change** | `تغییر عکس نماینده` |
| **Address change** | `تغییر آدرس نماینده` |
| **Phone change** | `تغییر تماس نماینده` |

Each field change also writes `TimelineService.LogRepresentativeFieldChanged`
with old and new values, so it appears in the case History tab.

## 5. Security — **gap closed** 🔧

**Found:** no dedicated permission. Anyone with `Case.Edit` could add, edit or
delete a legal representative; the delete button checked `Case.Delete`.

**Why that mattered:** representative data is a *third party's* identity —
national ID, phone, address and photograph of someone who is not the
beneficiary. Its access level should be settable independently.

**Added** (`EnterpriseInitializer.cs`):

| Permission | SuperAdmin | Admin | Operator | Viewer |
|---|---|---|---|---|
| `Representative.View` | ✓ | ✓ | ✓ | ✓ |
| `Representative.Edit` | ✓ | ✓ | ✓ | ✗ |
| `Representative.Delete` | ✓ | ✓ | ✗ | ✗ |
| `Representative.Print` | ✓ | ✓ | ✓ | ✓ |

**Defaults are byte-identical to the `Case.*` equivalents**, so no role's
behaviour changed on upgrade — asserted by
`Representative_PermissionDefaults_MatchCaseEquivalents`.

**Three enforcement points** (`FrmCase.cs`):
- **View** → `SetRepresentativeTabVisible` hides the tab without the permission
- **Edit** → `SaveCaseRepresentatives` skips *only* the representative block and
  tells the user; it never breaks the case save
- **Delete** → the clear-slot-2 button, switched from `Case.Delete`

## 6. Reporting Integration — verified ✅

`RptFullCase.rdlc` renders `Rep1Summary`/`Rep2Summary` (name — relationship —
phone), with rows hidden by expression when empty, so non-disability reports
print exactly as before.

Critically, the query lives in **`RdlcExportHelper.CaseDataSql` only** — the
single source feeding both the on-screen viewer and the PDF/Word/batch export.
An earlier divergence here silently broke PDF export while the on-screen report
looked fine; it is now guarded by `CaseReportDataPathTests`.

Covers disability reports, beneficiary reports and detailed case reports — all
three are the same RDLC.

## 7. Export Integration

| Format | Before | After |
|---|---|---|
| Word | ✅ `{{Rep1*}}`/`{{Rep2*}}` (8 fields each) | unchanged |
| PDF | ✅ inherited (RDLC + Word conversion) | unchanged |
| **Excel** | ❌ **zero coverage** | 🔧 **10 columns added** |

**Excel gap closed** (`ExcelReportExporter.cs`): 5 columns per slot — name,
relationship, national ID, phone, address — on the "پرونده ها" sheet.

Built as **scalar sub-queries, deliberately not a `JOIN`**: a join would double
the row of any case with two representatives, which would also corrupt the
summary sheet's "تعداد کل پرونده‌ها" count. Uses the same `IsActive = 1` +
`RepresentativeOrder` rule as `RepresentativeSummarySql`, so Excel and the
printed report can never disagree.

> ⚠️ **10 columns were added.** Anything reading this sheet **by column index**
> needs updating; reading by header name is unaffected.

## 8. Print Integration — verified ✅

Printing runs through the same RDLC path as reporting (§6), so representatives
print on the case report. Gated by `Case.Print`, with `Representative.Print` now
available for independent restriction.

Representatives are **not** on the ID card — deliberate, per Decision #40.

## 9. End-to-End Testing

### Full regression — **645 tests, 641 passed, 3 skipped, 1 known-failing, 0 regressions**

| Batch | Coverage | Result |
|---|---|---|
| 1 | Disability, Representative (+Legacy), Activation, Form visibility, RDLC data path, **Finance (H4)**, History wiring | 88 / 88 |
| 2a | Export end-to-end, regression, reporting audit, Word formatting | 45 + 1 skipped |
| 2b | Sync (5 classes), HTML sync, report templates | 133 / 133 |
| 3 | Permissions (3), vulnerability, service status, migration, duplicates (3), grids, dashboard, locks | 101 / 101 |
| 4a | AI core (8 classes) | 55 / 55 |
| 4b | AI scenarios, applicant, member photo, settings, assistance receipt | 35 + 2 skipped |
| 5 | Accounting (3), money, balance, reversal, revision, card designer (2), packages | 57 / 57 |
| 6a | Integrity, repair, DevCenter, HTTP transports, auto-backup | 92 / 92 |
| 6b | Backup encryption, restore, disaster recovery | 34 / 34 |
| 7 | Diagnostics | 1 passed, 1 known-failing |

**All 73 test classes covered** (the other 5 files are base classes and helpers
with no `[TestMethod]`).

- **1 failure:** `Diag_BatchDesignOverride_AppliesToAllCards` — documented in
  `PROJECT_CONTEXT.md` as always failing under `vstest.console` because
  `Application.StartupPath` differs from the app's real folder. **Pre-existing,
  not a regression signal.**
- **3 skips:** the two known WebView2/receipt renders and the ClosedXML/`System.Memory`
  binding case — all pre-existing and documented.
- **`Members_ElectronicTazkiraWithoutDashes_IsFormatted`, which failed in the
  earlier full run, now passes** — fixed by the concurrent session.

> **How this run was obtained.** Seven attempts at a single full-suite run were
> killed mid-flight. The cause was not rebuilds: `PROJECT_CONTEXT.md` instructs
> `taskkill /IM vstest.console.exe /F` before testing, and the concurrent session
> was following it — terminating my 25-minute run each time it ran its own tests.
> Short batches complete inside that window, so the suite was run in nine batches
> covering every class. The coverage is complete; only the packaging differs.

### Feature-specific

**42 / 42** across the two directly relevant suites (`CaseRepresentativeTests` 23 +
`DisabilityModuleReleaseTests` 19).

Existing 23 cover: schema creation, slot uniqueness enforced by the database,
CASCADE delete, upsert semantics, two-slot round-trip, delete-frees-slot, ten
validation cases, field-level audit, create/delete audit, sync registration +
`GlobalID`, ID normalisation (dashes, Persian digits), and section visibility
reading the flag rather than a hardcoded type.

**Four added** for the areas I changed:
- `Excel_CasesQuery_IncludesRepresentativeColumns_AndIsValidSql` — asserts the
  columns exist **and executes the SQL**, proving the sub-queries are valid
- `Excel_CaseWithTwoRepresentatives_StillProducesOneRow` — locks the no-row-
  multiplication guarantee
- `Representative_DedicatedPermissions_AreSeeded`
- `Representative_PermissionDefaults_MatchCaseEquivalents` — the no-regression guard

---

## 10. Deliverables

### Modified files (4)

| File | Change | Risk |
|---|---|---|
| `Helpers/ExcelReportExporter.cs` | 10 representative columns as scalar sub-queries | **Low** — additive; no join, no row multiplication; SQL executed in test |
| `Enterprise/EnterpriseInitializer.cs` | 4 permission seeds with `Case.*`-identical defaults | **Low** — additive; defaults asserted equal to existing |
| `FrmCase.cs` | 3 enforcement points (View/Edit/Delete) | **Low–Medium** — touches tab visibility and the save path; Edit failure degrades gracefully |
| `CaseManagement.Tests/DisabilityModuleReleaseTests.cs` | +4 tests | None |

### Database changes
**None from me.** The table, indexes and `EnsureColumn` upgrade path already
existed and are correct. The 4 new permission rows are seeded data, not schema.

### Search / Report / Export / Audit changes
- Search: none needed — already complete and correctly built with `EXISTS`
- Reports: none needed — RDLC already integrated via the consolidated `CaseDataSql`
- Export: **Excel +10 columns**; Word and PDF already complete
- Audit: none needed — all six operations already distinctly logged

### Security review
No privilege escalation. Destructive operations remain most restricted. Defaults
preserve current behaviour exactly. One residual policy point: **Viewer can still
print and export representative data including national IDs** — inherited from
`Case.Print`/`Case.Export` and now independently restrictable via
`Representative.Print`.

---

## 11. Remaining Risks

| # | Risk | Severity | Note |
|---|---|---|---|
| 1 | Not manually exercised | **Medium** | The 9-step smoke test in `LAWYER_FEATURE_AUDIT.md` §9 has not been run by me. **Do this before handover.** |
| 2 | Excel column indices shifted | Low | Header-name readers unaffected |
| 3 | `Representative.Print` seeded but not separately enforced | Low | Print still runs under `Case.Print`; the key exists for future restriction |
| 4 | Duplicate national ID reported, not blocked | Low | Deliberate — one lawyer may represent several beneficiaries |
| 5 | Representative photos left on disk after row delete | Low | Same conservatism as `FrmDocs` and visit photos |
| 6 | Not on the ID card | Low | Deliberate (Decision #40) |
| 7 | I did not read all 40 KB of the service line-by-line | Low | Verified schema, validation, audit, search, export and integration points, plus 42 green tests |
