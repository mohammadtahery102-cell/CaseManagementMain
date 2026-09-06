# Modified Files Report — Disability Release

> Date: 2026-09-04
>
> **Important:** this repository has a **second session actively editing it**
> during this work (see §3). The tables below list **only the files I changed**.
> Many other files in `git status` were modified by that other session or were
> already dirty before I started — they are **not** mine and are **not**
> validated by this report.

---

## 1. Files I Changed (11)

| # | File | Purpose of Change | Validation | Risk |
|---|---|---|---|---|
| 1 | `FrmCase.Designer.cs` | **C1:** enable `ShowCheckBox`/`Checked=false` on `dtpDisabilityIssueDate`, `dtpDisabilityExpiryDate`, `dtpFatherDeathDate`. **H5:** add read-only `dgvTimeline` + "تاریخچه" tab (last tab). | Compiles clean; verified no `ValueChanged` handler exists on the three pickers, so init-time `RaiseValueChanged` is a no-op | **Low** — additive; existing controls untouched |
| 2 | `FrmCase.cs` | **C1:** `DateOrNull(...)` helper; three unconditional `.Value.Date` writes replaced; `SetDatePickerValue` maps NULL→unchecked; reset clears checkboxes. **H5:** `RefreshTimelineTab()` wired into load / new-case / post-save. | Compiles clean; verified `LoadCaseModules` clears pickers before load so no cross-case bleed | **Medium** — changes what is written to 3 date columns. Intended and tested behaviour change |
| 3 | `Helpers/CaseModuleService.cs` | **H2:** transaction around module write + `TblCase` mirror. **H1:** `PrepareDelete`/`CommitDelete` on delete. **H3:** `ReadCurrentValues` + `LogFieldChanges` diff. | 4 tests pass (`DeletingDisabilityModule_IsCapturedInSyncOutbox`, `EditingDisabilityField_RecordsOldAndNewValue`, `SavingWithoutChanges_DoesNotLogFieldRows`, plus the completion test) | **Medium** — core write path for all three module tables. Preserved the concurrent session's `VulnerabilityScoreService` call |
| 4 | `Helpers/TimelineService.cs` | **H3:** `LogModuleFieldChanged`. **H5:** `GetCaseTimeline` + `Str`/`FormatInstant` helpers; added `using System.Data`. | 2 timeline tests pass | **Low** — purely additive; no existing method altered |
| 5 | `Helpers/RequiredDocumentService.cs` | **C2:** conditional-requirement rule — 2 SQL fragment constants + `AddConditionalParameters`; applied to both query methods. | 5 tests pass, incl. the «ندارد»⊃«دارد» substring trap | **Medium** — changes which documents are mandatory. Exactly the intended fix |
| 6 | `Helpers/CaseCompletionService.cs` | **C2:** same rule applied to `CalculateDocumentCompletion` so the gate and the percentage agree. | `CompletionAndGate_AgreeOnConditionalDocument` passes | **Medium** — can raise completion % for disability cases without a card. Intended |
| 7 | `Helpers/OpenXmlCaseExporter.cs` | **M2:** `LEFT JOIN TblDisability` (aliased) + 9 new `{{Disability*}}` placeholders. | Compiles clean; `UNIQUE(CasID)` guarantees no row multiplication | **Low** — new dictionary entries only; templates without the placeholders are unaffected |
| 8 | `Helpers/ExcelReportExporter.cs` | **M2:** 9 disability columns + `LEFT JOIN TblDisability dis` on the case query. | Compiles clean | **Low–Medium** — adds columns to an existing export; column *order* shifts for downstream consumers |
| 9 | `FrmAdvancedSearch.cs` | **M1:** head disability type, member disability type, and member physical status now read `LookupHelper` instead of hardcoded arrays. | Compiles clean; matches the file's own existing `ServiceStatus` precedent | **Low** — same values by default; only extends to admin-added ones |
| 10 | `FrmSettings.cs` | **M5:** added `DisabilityCause`, `DisabilityCardStatus` to the managed lookup categories. | Compiles clean | **Very Low** — one array entry |
| 11 | `GuardianCardIntegration/GuardianCardData.cs` + `CardService.cs` | **M3:** expose `DisabilityType`/`DisabilityDegree` to the card JSON. Deliberately **not** added to `CardFieldCatalog`. | Compiles clean | **Very Low** — no existing template references them, so no rendered card changes |

### New files I created

| File | Purpose | Validation |
|---|---|---|
| `CaseManagement.Tests/DisabilityModuleReleaseTests.cs` | 10 regression tests locking C2, H1, H3, H5 | All 10 pass |
| `DISABILITY_RELEASE_REPORT.md` | Final release report (inventory + gap analysis in §1) | — |
| `H4_IMPACT_ASSESSMENT.md` | H4 investigation | — |
| `DISABILITY_MODIFIED_FILES.md` | This file | — |

### Also updated

| File | Change |
|---|---|
| `Project Knowledge/PROJECT_CONTEXT.md` | Known Decisions #32–36 recording the C1/C2/H2/H3/H5/M3 rules |

---

## 2. Files I Did **Not** Touch (but which are modified in `git status`)

These carry changes from the other session and/or from work that was already
uncommitted before I started. **I did not review, test, or validate them**, and
they are outside the scope of this release report:

`CLAUDE.md` · `CaseManagement.csproj` · `FrmDocs.cs` · `FrmDocs.Designer.cs` ·
`Helpers/BackupHelper.cs` · `Helpers/CaseDomain.cs` · `Helpers/DatabaseInitializer.cs` ·
`Helpers/DuplicateDetector.cs` · `Helpers/FileHelper.cs` ·
`Sync/OfflineSyncInitializer.cs` · `Sync/SyncApplier.cs` · `Sync/SyncService.cs` ·
plus new files `Helpers/CaseRepresentativeService.cs` and related Phase 7 work.

Note that `FrmAdvancedSearch.cs`, `FrmCase.cs`, `FrmCase.Designer.cs`,
`Helpers/OpenXmlCaseExporter.cs` and `Helpers/TimelineService.cs` contain **both**
my changes and the other session's — verified coexisting, not conflicting.

---

## 3. Concurrent-Edit Warning

Files changed on disk *during* this session, without my involvement:

| Time | File | Change observed |
|---|---|---|
| 01:38 | `Helpers/CaseModuleService.cs` | `VulnerabilityScoreService.RecalculateAndStore` call added (preserved in my edit) |
| ~08:1x | `Helpers/TimelineService.cs` | Phase 7 `CategoryRepresentative` + 3 event constants |
| ~08:3x | `FrmAdvancedSearch.cs` | Phase 7 representative search fields |
| 08:36 | `Helpers/CaseRepresentativeService.cs` | **New, 40 KB** |
| ~08:4x | `Helpers/DatabaseInitializer.cs` | **`CREATE TABLE TblCaseRepresentative` + 3 indexes** |
| ~08:4x | `Sync/OfflineSyncInitializer.cs` | `TblCaseRepresentative` registered in `SyncedTables` |
| ~08:4x | `Helpers/OpenXmlCaseExporter.cs` | `AddRepresentativeValues(...)` |
| 08:41 | `CaseManagement.Tests.dll` | Rebuilt **mid-test-run** |
| 08:53 | `CaseManagement.exe` | Rebuilt **mid-test-run** |

**Consequences you must know about:**

1. **My first full regression run was invalidated** — the DLL under test was
   rebuilt beneath the running process at 08:41 and 08:53. It was killed and
   restarted against a stable build.
2. **The Lawyer/Legal-Representative feature is being built into this codebase
   right now** — the feature you instructed be documented only. It is not a
   design doc; it is a new table, a 40 KB service, sync registration, search
   fields and export integration.
3. **This release therefore carries a schema change**, contradicting what I
   reported earlier. `TblCaseRepresentative` is created by
   `DatabaseInitializer.EnsureDatabaseObjects()` and will be created on every
   user's laptop at first launch after deployment.

None of the Phase 7 work is covered by my testing or this report.
