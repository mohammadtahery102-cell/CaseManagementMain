# PHASE 5.5-E — Export & Reporting Final Verification

**Date:** 2026-09-05 · **Type:** release-blocking audit
**Method:** every finding below was produced by **rendering the real artefact and
inspecting its content** — not by reading code and not by "the build passed".

**Verdict: NO RELEASE BLOCKER FOUND.** Every export, report and print surface
produces valid output. What the audit found is **coverage gaps in templates**,
not broken code. Five gaps were closed; the rest need template edits that are a
design decision, not a code fix.

---

## 1. Modified Files

| # | File | Change | Risk |
|---|---|---|---|
| 1 | `Helpers/CaseExportDataProvider.cs` | **+5 providers** — `GetDisabilityInfo`, `GetOrphanInfo`, `GetMigrantInfo`, `GetFamilyMembers`, `GetRepresentatives`. Purely additive; no existing provider touched | **Low** |
| 2 | `Helpers/CaseFileExportService.cs` | 5 new sections wired into `BuildSections`, inserted **before** the timeline so the mandated "history last" rule still holds | **Low** |
| 3 | `CaseManagement.Tests/ExportReportingAuditTests.cs` | **New — 22 tests.** The audit itself, kept as permanent regression coverage | **None** (test-only) |

**No change to:** the RDLC, any `.docx` template, `OpenXmlCaseExporter`,
`ExcelReportExporter`, `RdlcExportHelper`, `DashboardMetricsService`, the
database, sync, or any business rule. **No schema change, no migration.**

---

## 2. Fixed Defects / Closed Gaps

| ID | Gap | Surfaces affected | Status |
|---|---|---|---|
| E1 | **Disability detail absent from every per-case output.** Only type + degree reached any single-case artefact; the other 9 fields existed solely in the *multi-case* Excel report | Excel (per-case), Print | **Closed** — new «اطلاعات معلولیت» section, all 11 fields |
| E2 | **Family members missing from the per-case file export.** Members appeared in RDLC and Word but the "پرونده کامل" Excel/Print had no member list at all | Excel, Print | **Closed** — new «اعضای خانواده» section |
| E3 | **Legal Representative had no full output anywhere.** RDLC prints a one-line summary; **no Word template contains a single `{{Rep…}}` placeholder** (verified by scanning all three templates) | Excel, Print | **Closed** — new «نماینده قانونی» section, all 9 fields |
| E4 | Orphan module data absent from per-case export | Excel, Print | **Closed** — new «اطلاعات ایتام» section |
| E5 | Migrant module data absent from per-case export | Excel, Print | **Closed** — new «اطلاعات مهاجرت» section |

The per-case export went from **8 sections to 13**.

---

## PART A — RDLC Audit

### Structure
`RptFullCase.rdlc` declares **3 datasets / 71 fields**, contains **1 Tablix** and
**0 subreports**.

| Check | Result |
|---|---|
| Dataset validation | ✅ 3 datasets (`CaseData`, `FamilyData`, `DocsData`) all supplied at runtime |
| **Missing columns** | ✅ **None.** Every one of the 71 declared fields exists in its feeding query — asserted by `PartA_RdlcDeclaredFields_AllExistInFeedingQueries` |
| Expression validation | ✅ No expression references an undeclared field. 37 refs are correctly scoped `First(…, "CaseData")`; 5 bare refs sit inside the FamilyData Tablix — no ambiguity |
| Renders | ✅ PDF, WORD and WORDOPENXML all render; PDF carries a valid `%PDF` signature and real content |
| Empty case | ✅ A case with no modules, members or documents still renders a valid report |

### Orphan fields — 26 declared but never rendered

| Group | Fields | Meaning |
|---|---|---|
| **`DocsData` — entire dataset** | `DocID`, `DocType`, `DocDescription`, `DocFilePath`, `OriginalFileName`, `RelatedCaseRef`, `CasID` | **A2 — the report queries documents and passes them to the renderer, but no data region consumes them. Documents never appear on the printed report.** |
| **All three photo fields** | `PhotoPath`, `FamilyPhotoPath`, `MemberPhotoPath` | **A3 — the RDLC contains `0` `<Image>` elements. Photos are declared, fetched, and never printed.** Word *does* print them |
| Family detail | `SchoolName`, `StudyField`, `StudyYear`, `UniversityName`, `Major`, `GradeLevel`, `MemberEducation`, `MemberSadat`, `OfficialStatus` | Member education/status not printed |
| **Member disability** | `HasDisability`, `PhysicalStatus`, `MemberDisabilityDegree` | **A4 — member-level disability is not printed on the report** |
| Case | `LocationAddress`, `SurveyDate` | Not printed |
| Keys | `CasID`, `FamID`, `DocID` | Legitimately unused |

**A5 — `CSV` is not a supported render format** in this ReportViewer build; it
throws `ArgumentOutOfRangeException`. The comment on `RdlcExportHelper.RenderCase`
recommends CSV for automated content verification — that advice does not work.
`WORDOPENXML` does, and is what this suite uses. Locked by
`PartA_CsvRenderFormat_IsNotSupported` so the comment gets corrected if the format
is ever added.

---

## PART B — Word Export Audit

| Check | Result |
|---|---|
| Templates present | ✅ `FullCaseTemplate.docx` (77 placeholders), `الگوی_شماره_1` (47), `الگوی_شماره_2` (90) — all tracked in git |
| **Missing values** | ✅ **Zero.** Every placeholder in every template is filled by the exporter |
| **Unreplaced placeholders in output** | ✅ **Zero** — verified on a real generated document (`PartB_WordExport_LeavesNoUnreplacedPlaceholders`) |
| Mapping verification | ✅ Values reach the document — head name, disability type and degree all confirmed present in the produced `.docx` |
| Null safety | ✅ `GetValue` returns `""` for a missing column *and* for `DBNull`, so a dead mapping can never throw |
| Empty case | ✅ Produces a valid document |

### Dead mappings — 20 keys the exporter fills that no template contains

| Group | Keys | Verdict |
|---|---|---|
| **Disability detail (9)** | `DisabilityCause`, `DisabilityDescription`, `SpecialNeeds`, `DisabilityCardStatus`, `DisabilityCardNumber`, `DisabilityCardIssuer`, `DisabilityIssueDate`, `DisabilityExpiryDate`, `DisabilityNotes` | **B1 — the disability release recorded these as "9 placeholders added to the Word export". The mapping exists; no template renders them, so the printed document still shows 2 of 11 disability fields.** Proven by `PartB_WordExport_DisabilityDetailFields_AreNotInTemplate` |
| **Representative (all)** | `{{Rep1*}}` / `{{Rep2*}}` — 8 keys per slot, built dynamically | **B2 — no template contains any `{{Rep…}}` placeholder. Representative data never reaches a Word document.** |
| Phase 5.5-D status (8) | `CompletionPercent`, `CompletionStatus`, `VulnerabilityScore`, `VulnerabilityBand`, `FundingSummary`, `SponsorSummary`, `VerifiedDocs`, `FieldVisitCount` | **Known and deliberate** — the code comment says so explicitly and lists them for a future template edit. Not a defect |

**B3 — architectural note.** The Word exporter defines its **own** case query,
separate from `RdlcExportHelper.CaseDataSql`. This is a third feed definition of
the same data. Phase 5.5-D centralised the RDLC's two feeds precisely because that
divergence silently broke PDF/Word output. The pattern has re-emerged. Not a live
defect — but the same trap.

### What Word *does* carry that the RDLC does not
Word appends two generated sections beyond the template: **بخش کمک‌های مالی**
(assistance with running total) and **بخش تاریخچهٔ تغییرات** (status history +
record versions + audit log). It also renders **photos**. The RDLC does none of these.

---

## PART C — PDF Export Audit

PDF is the RDLC rendered to PDF, so its coverage is identical to Part A.

| Check | Result |
|---|---|
| Single export | ✅ Valid `%PDF`, > 1 KB, real content |
| Batch export | ✅ Same `Render` path per case; `CenterGuard.EnsureCaseAccess` is enforced **per case**, so multi-center isolation holds in batch |
| Representative fields | ✅ **Printed** — `Rep1Summary`/`Rep2Summary` verified present in rendered output |
| Disability fields | ⚠️ **2 of 11** (type, degree) — B1/A-group |
| Timeline | ❌ Not on the report |
| Funding | ✅ `FundingSummary` printed (sponsor names in parentheses) |
| Completion status | ✅ Percent + Persian status text printed |
| Vulnerability | ✅ Score + Persian band printed |

---

## PART D — Excel Export Audit

Two distinct exports; they are different data shapes by design.

| Export | Worksheets | Result |
|---|---|---|
| **Per-case** (`CaseFileExportService`) | **13** (was 8) | ✅ All 13 sections return a table with columns for both a populated and an empty case |
| **Multi-case** (`ExcelReportExporter`) | خلاصه + per-entity sheets | ✅ Carries **all 11 disability columns** — the only surface that always did |

| Check | Result |
|---|---|
| Columns | ✅ Every section has ≥ 1 column even when it has 0 rows, so no sheet is structurally empty |
| Data integrity | ✅ Providers scoped by `@CasID`; an unknown id returns 0 rows rather than another case's data |
| Empty datasets | ✅ `AddSheet` writes «داده‌ای برای نمایش وجود ندارد.» — no crash, no blank sheet |
| Null handling | ✅ Every text column wrapped in `IFNULL(...,'')` |

> ⚠️ **`ClosedXML` cannot run under the test host** (the documented `System.Memory`
> binding conflict). The *workbook file* is therefore **not** produced in automated
> tests — only the data that feeds it is verified. **Excel file generation must be
> confirmed manually** — see Part H.

---

## PART E — Print Audit

`PrintFullCase` prints the same 13 sections as the per-case Excel, sequentially.

| Check | Result |
|---|---|
| Full case print | ✅ Driven by the identical `BuildSections` list — Excel and print can never diverge |
| Timeline print | ✅ Present, and asserted to be **last** (`PartE_PrintSectionOrder_PutsTimelineLast`) per the approved requirement |
| Visits print | ✅ Section present |
| Funding print | ✅ Section present |
| Document status print | ✅ Both «وضعیت اسناد» and «اسناد ناقص» present |

> Physical printing requires a printer and cannot be automated. Section
> composition and ordering are verified; **paper output needs manual confirmation.**

---

## PART F — Coverage Matrix

Legend: ✅ full · ⚠️ partial · ❌ absent · **NEW** = closed by this audit

| Feature | Word | PDF | Excel | Print | RDLC |
|---|---|---|---|---|---|
| Case Information | ✅ | ✅ | ✅ | ✅ | ✅ |
| Family Members | ✅ | ✅ | ✅ **NEW** | ✅ **NEW** | ✅ |
| Documents | ✅ full section | ❌ | ✅ | ✅ | ❌ **A2** |
| Missing Documents | ❌ | ❌ | ✅ | ✅ | ❌ |
| Timeline | ❌ | ❌ | ✅ | ✅ | ❌ |
| Field Visits | ❌ | ❌ | ✅ | ✅ | ❌ |
| Funding | ❌ **B-dead** | ✅ | ✅ | ✅ | ✅ |
| Sponsors | ❌ **B-dead** | ⚠️ inside funding text | ✅ | ✅ | ⚠️ |
| Assistance | ✅ + total | ❌ | ✅ | ✅ | ❌ |
| Vulnerability Score | ❌ **B-dead** | ✅ | ✅ breakdown | ✅ | ✅ |
| Completion Status | ❌ **B-dead** | ✅ | ✅ | ✅ | ✅ |
| Service Status History | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Disability Information** | ⚠️ 2/11 **B1** | ⚠️ 2/11 | ✅ **11/11 NEW** | ✅ **NEW** | ⚠️ 2/11 |
| Orphan Information | ⚠️ school only | ❌ | ✅ **NEW** | ✅ **NEW** | ❌ |
| Migrant Information | ⚠️ card type only | ❌ | ✅ **NEW** | ✅ **NEW** | ❌ |
| Representative Information | ❌ **B2** | ✅ summary | ✅ **full NEW** | ✅ **NEW** | ✅ summary |
| Photos | ✅ | ❌ **A3** | ❌ | ❌ | ❌ **A3** |

> **Correction (verified by generating a document 2026-09-05):** `FillDocsBlock`
> appends a full per-document section to the Word export — an earlier reading of
> this file recorded Word document coverage as "count only", which was wrong.

**Every feature now has at least one surface with full coverage.** Before this
audit, Representative and the 9 disability detail fields had **none** for a single
case.

### Remaining coverage gaps, by cost to close

| Gap | Fix required | Risk |
|---|---|---|
| B1 — disability detail in Word | Add 9 placeholders to `FullCaseTemplate.docx` | **Low code risk, but a layout decision** |
| B2 — representative in Word | Add `{{Rep1*}}`/`{{Rep2*}}` to a template | Same |
| Phase 5.5-D status fields in Word | Add 8 placeholders | Same — already documented as pending |
| A2 — documents on the RDLC | Add a Tablix bound to `DocsData` | **High** — 90 KB absolute-layout RDLC |
| A3 — photos on the RDLC | Add `<Image>` elements | **High** — same reason |
| Service status history in Excel/Print | New provider + section | Low, but no one asked for it |

**None of these were changed.** Editing a `.docx` requires deciding *where* each
field goes, under which heading, in what order — that is the user's call, not a
mechanical fix. Editing the RDLC is the highest-risk reporting change available
and was deferred for that reason in the previous release too.

---

## PART G — Dashboard Audit

| Check | Result |
|---|---|
| Queries | ✅ All 6 distribution queries + `GetManagementMetrics` execute and return non-null tables with columns |
| **Empty database** | ✅ All run on a database with no cases — the state of a fresh install |
| Metrics | ✅ `GetManagementMetrics` returns a populated object |
| Risk bands | ✅ `GetVulnerabilityBandDistribution` runs; reads the indexed `VulnerabilityBand` cache column, never a live recompute |
| Completion metrics | ✅ `GetCompletionDistribution` reads `CompletionStatusCode` cache column |
| Funding metrics | ✅ Funding + sponsor distributions run |
| Visit metrics | ✅ Included in the management composite |
| **Center isolation** | ✅ A non-existent center id never returns more rows than the unfiltered call |
| Drill-down | ⚠️ **Not automatically verifiable** — drill-down is a `FrmDashboard` UI interaction (`AttachCardClick`), needs manual check |

`FrmDashboard` was being actively edited during this audit (see §5); dashboard
**UI** behaviour is therefore verified only at the service layer.

---

## PART H — Final Manual Verification Checklist

Automated tests cover data and rendering. These four things cannot be automated:
**ClosedXML workbook generation**, **physical printing**, **visual layout**, and
**dashboard drill-down**.

### Orphan case
- [ ] Create an `ORPHAN` case; fill orphan section + guardian name/relationship; add 2 family members and 1 document.
- [ ] **Word** — orphan school fields appear; family member rows populated; photos render; assistance and history sections appear if data exists.
- [ ] **PDF** — renders; family table populated; **confirm documents and photos are absent** (known, A2/A3).
- [ ] **Excel** — 13 sheets; «اطلاعات ایتام» populated; «اطلاعات معلولیت» and «اطلاعات مهاجرت» show the empty-data message; «تایم‌لاین» is the **last** sheet.
- [ ] **Print** — all 13 sections print in order, timeline last.

### Disability case
- [ ] Create a `DISABLED` case; fill **all 11** disability fields including card number and both dates; add a legal representative.
- [ ] **Word** — type and degree appear; **confirm the other 9 do not** (known, B1); **confirm representative data does not appear** (known, B2).
- [ ] **PDF** — type + degree present; representative **summary line** present.
- [ ] **Excel** — «اطلاعات معلولیت» shows all 11 fields including card number; «نماینده قانونی» shows the full representative record. *This is the only place either appears in full.*
- [ ] **Print** — both new sections print.

### Migrant case
- [ ] Create a `MIGRANT` case; fill the migration section including «نوع برگه مهاجرت».
- [ ] **Word** — `{{MigrationCardType}}` populated.
- [ ] **PDF** — migration card type present.
- [ ] **Excel** — «اطلاعات مهاجرت» populated with all 10 fields.
- [ ] **Print** — section prints.

### Cross-cutting
- [ ] Export a case with **no** members/documents/modules — every surface produces a valid, non-crashing artefact.
- [ ] Batch PDF export across ≥ 2 cases, one from another center → the other center's case must be refused.
- [ ] Dashboard: click each management card → drill-down opens the right filtered list.
- [ ] Confirm the Excel workbook actually **opens in Excel** (ClosedXML output is untested by the suite).

---

## 3. Remaining Risks

| # | Risk | Severity | Notes |
|---|---|---|---|
| R1 | **ClosedXML workbook generation is untested** | **Medium** | Data is verified; file writing is not. Pre-existing, documented. Manual check required |
| R2 | Word disability + representative coverage (B1/B2) | Medium | Users told these are exported may not find them. Excel now covers both |
| R3 | RDLC prints no documents and no photos (A2/A3) | Medium | Long-standing; the RDLC is a summary report in practice |
| R4 | Physical print output unverified | Medium | Needs a printer |
| R5 | Dashboard drill-down unverified | Low | Service layer verified; UI is manual |
| R6 | Third divergent case query in the Word exporter (B3) | Low | Latent maintenance trap, not a live defect |
| R7 | **Concurrent session actively editing audited files** | **Medium** | See §5 |

---

## 4. Build & Test Result

**Build:** `CaseManagement` and `CaseManagement.Tests` both compile clean, **0 errors**.

| Suite | Result |
|---|---|
| `ExportReportingAuditTests` (new) | **22 / 22 pass** |
| Export/report regression — `ExportRegressionTests`, `CaseReportTemplateTests`, `WordExportFormattingTests`, `Template1PageBreakTests`, `DisabilityModuleReleaseTests`, `CaseRepresentativeTests`, `CaseFormSectionVisibilityTests`, `DashboardLayoutTests` | **87 / 87 pass** |
| **Total verified** | **109 tests, 0 failures** |

> Whole-suite `vstest` runs truncate in this environment (pre-existing, documented).
> Batched `--TestCaseFilter` runs are stable and were used throughout.

---

## 5. Audit Integrity Note

A **second session was editing this repository throughout the audit**, including
files inside the audit scope:

| Time | File |
|---|---|
| 11:46 | `Helpers/RdlcExportHelper.cs` |
| 12:09 | `Helpers/ExcelReportExporter.cs` |
| 12:10 | `Enterprise/EnterpriseInitializer.cs` |
| 12:11 | `FrmCase.cs` |

Findings reflect the tree as of **12:20**. `ExcelReportExporter` was re-inspected
after its 12:09 edit and still carries all 11 disability columns. The two files I
modified (`CaseExportDataProvider.cs`, `CaseFileExportService.cs`) had not been
touched since 2026-09-04 and were not contended.

**Re-run the audit suite immediately before cutting the build** — it is fast
(~50 s) and will catch any regression the other session introduces.

---

## 6. Rollback

| Change | Rollback |
|---|---|
| `CaseExportDataProvider.cs`, `CaseFileExportService.cs` | Restore from `scratchpad/backup_5e_122139/` |
| `ExportReportingAuditTests.cs` | Delete the file |

Code-only and purely additive. **No schema change, no migration, no data
modification.** `git checkout` is *not* a valid rollback — the working tree carries
uncommitted work from the concurrent session.
