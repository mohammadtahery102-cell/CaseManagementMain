# Legal Representative (Lawyer) Feature — Concurrent-Work Audit

**Date:** 2026-09-04 · **Auditor:** independent review, not the implementing session
**Verdict: SAFE TO SHIP — and it belongs in this release.**

---

## Correction to my earlier report

I previously flagged this as *"an unrelated in-flight feature contaminating the
disability release."* **That framing was wrong, and I'm correcting it.**

`ShowRepresentativeSection` is seeded **`1` for `DISABLED` and `0` for all five
other request types** (`DatabaseInitializer.cs:1251-1256`). The tab appears only
on disability cases. This is not unrelated work — it is the Phase 8 Lawyer
requirement from your original mission, scoped precisely to the Disability
implementation.

My alarm was based on file timestamps and a broken mid-edit build, before I had
read the seeding. The feature itself is disciplined work.

---

## 1. Files Changed (12)

| File | Change |
|---|---|
| `Helpers/CaseRepresentativeService.cs` | **New, 40 KB.** Sole reader/writer. 16 public methods |
| `Helpers/DatabaseInitializer.cs` | `TblCaseRepresentative` + 4 indexes; `ShowRepresentativeSection` column + per-type seeding; `RepresentativeRelationship` lookup (15 values) |
| `Helpers/TimelineService.cs` | `CategoryRepresentative` + 3 event constants |
| `Helpers/BackupHelper.cs` | 4 integration points (load, merge, whole-replace, delete-current) |
| `Helpers/FileHelper.cs` | `SectionRepresentativePhotos` storage section |
| `Helpers/OpenXmlCaseExporter.cs` | `AddRepresentativeValues(...)` |
| `Helpers/ReferenceDataService.cs` | `ShowRepresentativeSection` in the sections model |
| `Sync/OfflineSyncInitializer.cs` | `TblCaseRepresentative` in `SyncedTables` |
| `FrmCase.cs` + `FrmCase.Designer.cs` | Representative tab, 2 photo slots, load/save/validate |
| `FrmAdvancedSearch.cs` | 4 search filters |
| `FrmCaseReport.cs` | Report integration |
| `CaseManagement.Tests/CaseRepresentativeTests.cs` | **New — 23 tests, 57 assertions** |

## 2. Database Changes

```sql
CREATE TABLE IF NOT EXISTS TblCaseRepresentative (
    RepresentativeID INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID INTEGER NOT NULL,
    RepresentativeOrder INTEGER NOT NULL DEFAULT 1,
    FullName TEXT NOT NULL,
    RelationshipToBeneficiary, IdCardType, NationalID, Phone,
    SecondaryPhone, Address, PhotoPath, Notes TEXT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CenterID, GlobalID, CreatedAt, CreatedBy, UpdatedAt,
    CONSTRAINT FK_CaseRepresentative_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE,
    CONSTRAINT UQ_TblCaseRepresentative_Order UNIQUE (CasID, RepresentativeOrder)
);
```
Plus 4 indexes (`CasID+Order`, `NationalID`, `Phone`, `FullName`), one
`TblRequestType.ShowRepresentativeSection` column, and a 15-value lookup.

**Assessment: conforms fully to project conventions.** FK CASCADE, `GlobalID`
and `CenterID` for sync, full audit columns, `UNIQUE(CasID, Order)` enforcing
exactly two slots. Every index backs a column the search actually filters on.
All DDL is `IF NOT EXISTS` / `EnsureColumn`, so it is additive and idempotent —
matching the project's migration rules. **No existing table or column is altered.**

## 3. Sync Changes

Registered in `SyncedTables` as `{"TblCaseRepresentative", "RepresentativeID"}`.

**Correct.** It is a direct child of `TblCase` with a `CasID` and a `GlobalID`, so
`SyncApplier.ResolveParent` handles it exactly like `TblFamily`/`TblDocs`. It is
**not** a grandchild, so it does not hit the `TblFieldVisitPhoto` limitation
(Decision #25). Delete propagation uses the two-phase
`PrepareDelete`/`CommitDelete` pattern — the same fix I applied for H1.

## 4. Search Changes

Four filters (name, national ID, phone, relationship), all built as:

```sql
AND EXISTS (SELECT 1 FROM TblCaseRepresentative r WHERE r.CasID = c.CasID AND ...)
```

**This is the correct and safe choice.** `TblCaseRepresentative` is 1:N against
`TblCase`; a `JOIN` would multiply case rows and silently corrupt result counts
and every export built from the grid. `EXISTS` cannot. The author understood the
risk.

## 5. Export Changes

`AddRepresentativeValues(...)` adds `{{Rep1*}}`/`{{Rep2*}}` placeholders to the
Word export dictionary. Purely additive — a template without those placeholders
is byte-identical to before, and `RemoveUnusedPlaceholdersEverywhere` cleans
empties for cases with no representative. Photos use the established
`FileHelper` section pattern.

**Not in the Excel export or the RDLC** — consistent with the disability fields'
own gap (R1). Not a defect; a known limit.

## 6. Test Coverage — 23 tests, 57 assertions

| Area | Tests |
|---|---|
| Schema | table + flag created; one-row-per-slot enforced by DB; CASCADE on case delete |
| Save | upsert not blind insert; two slots round-trip; delete frees slot for reuse |
| Validation (10) | primary required; missing fields; bad national ID; bad phone; unknown relationship; duplicate representatives; representative = beneficiary; identical phones; partially-filled secondary; valid-primary-alone |
| Audit (3) | field change records old→new; unchanged save writes no rows; create/delete logged |
| Sync | registered + rows get `GlobalID` |
| Normalisation | dashes and Persian digits normalised in ID comparison |
| Visibility | reads the flag, never a hardcoded request type |

**This is stronger coverage than most of the existing codebase.** Two tests
(`Audit_FieldChange_RecordsOldAndNewValue`, `Audit_UnchangedSave_WritesNoFieldRows`)
are the same discipline I independently built for H3 — the two workstreams
converged on identical audit semantics, which is a good consistency signal.

All 23 passed in the full regression run.

## 7. Risks

| # | Risk | Severity | Assessment |
|---|---|---|---|
| 1 | **Ships a schema change** — `TblCaseRepresentative` created on every laptop at first launch | **Medium** | Additive and idempotent, `IF NOT EXISTS`, no existing object altered. Standard for this project — every phase has shipped schema this way. Backup/restore/sync all cover it. |
| 2 | Feature is one day old | **Medium** | Mitigated by 23 tests and by being invisible to 5 of 6 request types |
| 3 | No `PermissionService` check inside the service | **Low** | Matches project convention — gating lives at the form handler. `SaveCaseRepresentatives` is reached only from the case save path, already gated by `Case.Edit`. **However:** there is no representative-specific permission, so anyone with `Case.Edit` can add/edit/delete a legal representative. Acceptable, but be aware. |
| 4 | Photo files left on disk after delete | **Low** | Deliberate — same conservatism as `FrmDocs` and visit photos |
| 5 | Not in Excel export or RDLC | **Low** | Same gap as the disability fields (R1) |
| 6 | Written by a session I did not review line-by-line | **Low** | I audited schema, sync, search, export, backup and tests independently |

**No Critical or High risk found.**

## 8. Ship / Exclude Decision

**SHIP IT.**

Reasoning:

1. **It is disability scope**, not foreign work — enabled for `DISABLED` only.
2. **Excluding it is riskier than including it.** It spans 12 files including
   `DatabaseInitializer`, `BackupHelper`, `SyncApplier` and two forms. Unpicking
   that the night before handover, without a feature branch, would very likely
   break the build or leave orphaned references — a far larger risk than shipping
   tested code.
3. **It is well-tested and conventional** — 23 tests, correct `EXISTS` search,
   full backup/sync coverage, idempotent additive DDL.
4. **It fails safe.** Five of six request types never see it. A charity not using
   disability cases is entirely unaffected.

**One caveat:** I did not review all 40 KB of `CaseRepresentativeService.cs`
line-by-line, nor exercise the UI by hand. My confidence rests on schema
correctness, integration-point correctness, and its test suite. **Manual smoke
test before handover** — §9.

## 9. Required Smoke Test Before Handover

1. Open a `DISABLED` case → the «نمایندهٔ قانونی» tab appears.
2. Open an `ORPHAN` case → the tab is **absent**.
3. Add Lawyer 1 with a photo, save, reopen — data and photo persist.
4. Leave Lawyer 2 blank — no empty row is created.
5. Add then clear Lawyer 2 — the row is deleted, not orphaned.
6. Enter an invalid phone / unknown relationship — rejected with a Persian message.
7. Search by representative name — the correct case is found **once**, not twice.
8. Export the case to Word — representative data appears; a template without the
   placeholders is unchanged.
9. Check the «تاریخچه» tab — the representative edit appears with old→new values.
