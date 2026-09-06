# Disability Module — Inventory Report & Gap Analysis

> Discovery pass only. No code changed. Every row below was verified in source.
> Date: 2026-09-04

---

## 0. Headline

**There is no self-contained "Disability Module."** Disability is one of six
`TblRequestType` values (`DISABLED`) whose data and behaviour are spread across
shared infrastructure: one module table, seven mirrored `TblCase` columns, one
section of `FrmCase`, four document categories, and member-level columns on
`TblFamily`. The total footprint is **31 files**.

The realistic production scope is therefore *stabilising an existing slice of a
general case-management system*, not hardening a standalone module.

---

## 1. Inventory

### 1.1 Forms

| Form | Disability role | Status |
|---|---|---|
| `FrmCase` | The only case-level disability data entry. 10 controls across **two non-adjacent tabs** | Implemented |
| `FrmFamily` | Member-level disability: `PhysicalStatus`, `HasDisability`, `MemberDisabilityDegree`, `DisabilityDetails` | Implemented |
| `FrmAdvancedSearch` | Head + member disability filters | Partially Implemented |
| `FrmDashboard` | Member-disability KPI + drill-down list | Implemented |
| `FrmDocs` | 4 disability document categories | Implemented |
| `FrmFinance` | Assistance entry (not disability-specific) | Implemented |
| `FrmSettings` | Lookup admin for `DisabilityType` / `DisabilityDegree` only | Partially Implemented |

### 1.2 Tabs (`FrmCase`, 9 total)

خلاصه پرونده · مشخصات سرپرست · **مشخصات جسمی** · **مشخصات پرونده** ·
اعضاء خانواده · اسناد پرونده · بازدید میدانی · خانواده · امتیاز آسیب‌پذیری

Disability fields are split: `DisabilityType`/`DisabilityDegree` sit in
*مشخصات جسمی*; the other nine sit in *مشخصات پرونده*.
**There is no history/timeline tab.**

### 1.3 Database

| Object | Status | Note |
|---|---|---|
| `TblDisability` (12 data cols, `UNIQUE(CasID)`, 3 indexes) | Implemented | `DatabaseInitializer.cs:981` |
| `TblCase` mirror columns (7) | Implemented | Dual-written by `CaseModuleService` |
| `TblFamily` member columns (4) | Implemented | Independent of `TblDisability` |
| 4 disability `TblDocumentCategory` rows | Implemented | Area photo, card photo, medical, verification |
| `TblRequiredField` for `DISABLED` (3) | Implemented | Type, Degree, Cause |
| `TblRequiredDocument` for `DISABLED` (3) | Implemented | Area photo, **card photo**, medical |
| `IX_TblDisability_Expiry` | **Unused** | No query anywhere reads `ExpiryDate` |

### 1.4 Reports, outputs, exports

| Output | Disability content | Status |
|---|---|---|
| `RptFullCase.rdlc` + `DsFullCaseReport` | `DisabilityType`, `DisabilityDegree` only | Partially Implemented |
| `ExcelReportExporter` | Same two, plus member `HasDisability`/degree | Partially Implemented |
| `OpenXmlCaseExporter` (Word) | Same two, plus member fields | Partially Implemented |
| `ExcelCaseImporter` | Imports the same two | Partially Implemented |
| `ReportDefinitions` (report builder) | Member `HasDisability` only | Partially Implemented |
| Disability-specific report | — | **Missing** |
| `Templates/Forms/*.docx` (7) | None disability-related | **Missing** |

**Nine of twelve `TblDisability` fields never reach any output**: `DisabilityCause`,
`DisabilityDescription`, `SpecialNeeds`, `HasDisabilityCard`, `DisabilityCardNumber`,
`CardIssuer`, `IssueDate`, `ExpiryDate`, `Notes`. They are write-only.

### 1.5 Card functionality — closest equivalent

There is **no dedicated disability card**. The nearest equivalent is the
**Guardian/beneficiary ID card** (`GuardianCardIntegration`, 13 files), which is
what the organisation actually prints.

| Card capability | Status | Evidence |
|---|---|---|
| Card number | Implemented — but it *is* `TblCase.FormNo` as `D6`, no separate number | `CardService.cs:68` |
| Duplicate card numbers | **Cannot occur** — `FormNo` is unique | verified |
| Beneficiary photo | Implemented | `GuardianCardData.Photo` |
| Full name / case number / province / district | Implemented | — |
| QR code + barcode | Implemented (barcode = `BranchCode-CardNumber`) | `GuardianCardRenderer.cs:1152` |
| Organisation info | Implemented, settings-driven | — |
| Issue date / expiry date | **Placeholder** — computed as `today` and `today+1y` at print time, never stored | `CardService.cs:46-47` |
| **Disability type / severity on card** | **Missing** — read into `CaseModel` then dropped; absent from `GuardianCardData` and from the 39-entry `CardFieldCatalog`, so it cannot be placed on any template | `CaseCardRepository.cs:310` |
| Card templates + designer | Implemented (`FrmCardTemplateManager`, batch print) | — |
| Card renewal | **Missing** | no issuance record exists |
| Card replacement | **Missing** | no issuance record exists |
| Card reports / card exports | **Missing** | — |

### 1.6 Search & filters

| Filter | Status |
|---|---|
| Head `DisabilityType` (exact) | Implemented |
| Member `HasDisability` (exact) + `PhysicalStatus` | Implemented |
| Filter vocabularies | **Partially** — hardcoded in `FrmAdvancedSearch`, lookup-driven everywhere else |
| Filter on card number / expiry / cause / severity | **Missing** |

### 1.7 Assistance

| Capability | Status |
|---|---|
| Cash + in-kind entry, receipt printing | Implemented (`FrmFinance`, `AssistanceReceiptIntegration`) |
| Duplicate prevention | Implemented — three layers incl. a confirm prompt |
| Permission gate (`Finance.Edit`) | Implemented |
| Audit + sync + version capture | Implemented |
| Assistance in case timeline | **Missing** — `ASSISTANCE_GRANTED` constant defined, never called |
| Eligibility rules / approval flow | **Missing** — `AssistanceRuleService` recommends an *amount* only, writes nothing |
| Disability-specific assistance | **Missing** |

### 1.8 Project functionality

**Missing entirely.** No `TblProject`, no allocation, no distribution engine.
Every "پروژه" occurrence in the codebase is the word "project" in a comment.

### 1.9 Audit / history

| Mechanism | Status |
|---|---|
| `TblAuditLog` / `TblAuditLogs` | Implemented (two overlapping tables — known debt) |
| `TblCaseStatusHistory` | Implemented — status + suspension/reactivation, with reason |
| `EntRecordVersion` (full-row snapshots) + `FrmVersions` viewer | Implemented |
| `TblCaseTimeline` (writes) | Implemented |
| `TblCaseTimeline` (**display**) | **Missing** — no form reads it, anywhere |
| Field-level old/new for disability edits | **Missing** — logged as "record updated", nulls in `FieldName`/`OldValue`/`NewValue` |

### 1.10 Permissions

40 permission keys, role matrix per `Enterprise/PermissionService`. Relevant:
`Case.Edit/Delete/Print/Export`, `Docs.Edit/Delete/Print`, `Finance.Edit`,
`GuardianCard.Print` + 5 template keys, `Report.Run/Export`, `Archive.Restore`,
`Archive.PermanentDelete`. **No disability-specific permission** — and none is
needed, since disability is a case attribute.

### 1.11 Dashboard / statistics

Member-disability KPI, a dedicated "نمایش اعضای معلول" drill-down, and Excel
export of that list — all **member-level and correctly labelled as such**
(compliant with the Primary Case Type Rule). **No case-level disability
statistics**, and nothing reads `TblDisability`.

---

## 2. Gap Analysis — requested vs. actual

| Requested phase | Reality | Verdict |
|---|---|---|
| 1 — Case management | Exists; shares the general case lifecycle | **In scope** |
| 2 — Disability card | No such feature; guardian card is the equivalent | **Partially in scope** (audit the guardian card) |
| 3 — Audit trail | Writers exist, field-level detail and any UI do not | **In scope** |
| 4 — Assistance | Entry/receipt/duplicate-guard exist; eligibility & approval do not | **Partially in scope** |
| 5 — Project distribution | Does not exist | **Out of scope — ignored** |
| 6 — Reports & outputs | Exist, but carry 2 of 12 disability fields | **In scope** |
| 7 — UI/UX | Exists | **In scope** |
| 8 — Lawyer 1 / Lawyer 2 | Does not exist (only 3 print-only proxy fields on a DOCX form) | **Out of scope — ignored** |
| 9 — Security | Exists | **In scope** |
| 10 — Database | Exists | **In scope** |
| 11 — Performance | Exists | **In scope** |
| 12 — Deployment | Exists | **In scope** |
| 13 — Final verification | — | **In scope** |

---

## 3. Findings

### CRITICAL

**C1 — Disability card dates are fabricated for every case.**
`FrmCase.cs:630-631` writes `dtpDisabilityIssueDate.Value.Date` unconditionally;
`FrmCase.cs:770-771` defaults both pickers to `DateTime.Today`;
`SetDatePickerValue` (`FrmCase.cs:3376`) turns `NULL` back into today.
`PersianDatePicker` *does* support an empty state (`ShowCheckBox`/`Checked`,
`PersianDatePicker.cs:129-141`) — it is simply never enabled.
*Impact:* every disability record claims a card issued today and expiring today,
including beneficiaries whose `DisabilityCardStatus` is "ندارد". There is no way
to record "no card". `IX_TblDisability_Expiry` indexes 100% synthetic data, so
any future expiry report returns every disabled case, all expired.
The identical defect affects `FatherDeathDate` (`FrmCase.cs:591`) — every orphan
case records a father's death date of "the day the record was saved".

**C2 — A disabled beneficiary without a government card can never be activated.**
`DISABILITY_CARD_PHOTO` is seeded mandatory (`DatabaseInitializer.cs:1718`,
MinCount 1); `CaseActivationValidator` hard-**blocks** the transition to ACTIVE on
any missing mandatory document. But `DisabilityCardStatus` offers "ندارد" and
"در حال اقدام" as valid states (`DatabaseInitializer.cs:1700`).
*Impact:* the two rules contradict. Cases legitimately without a card are
permanently stuck below ACTIVE and receive no service.

### HIGH

**H1 — Module deletes never sync.** `CaseModuleService.Delete`
(`CaseModuleService.cs:122-151`) writes the timeline and recalculates completion
but never touches the outbox, while `Save` does capture create/update.
`TblDisability` *is* a registered synced table (`OfflineSyncInitializer.cs:61`),
and a purpose-built `PrepareDelete`/`CommitDelete` pair already exists.
*Impact:* clearing a disability record at a branch never propagates; head office
keeps the stale row and can resurrect it locally.

**H2 — Dual-write is not atomic.** `CaseModuleService.Save` runs the module
INSERT/UPDATE and the `TblCase` mirror UPDATE as two statements with no
transaction. *Impact:* a failure between them diverges the module table from the
mirror — the exact drift the class's own header says it exists to prevent. RDLC,
Word/Excel export, advanced search and the dashboard all read the mirror, so the
case would show one severity on screen and another in every report.

**H3 — No field-level audit on disability edits.**
`TimelineService.LogModuleRecordUpdated` (`TimelineService.cs:188`) passes
`null, null, null` into the `FieldName`/`OldValue`/`NewValue` slots that
`TblCaseTimeline` already provides. *Impact:* changing severity from اول to سوم
records only "ویرایش اطلاعات معلولیت". Schema is already correct; only the call
site is wrong.

**H4 — Assistance entry silently reclassifies the case.** `FrmFinance.cs:478`
runs `UPDATE TblCase SET RequestType=@RequestType` on every assistance save,
writing **only the legacy TEXT column** and not `RequestTypeID`.
*Impact:* recording assistance can silently change a disability case's primary
classification, and desynchronises the dual-written pair — the TEXT column drives
~150 call sites, the FK drives all Phase 3+ logic. No audit, no timeline event.

**H5 — The case timeline is never displayed.** `TblCaseTimeline` is written by
six subsystems and read by none; no form queries it.
*Impact:* the richest history the system keeps is invisible to users.

### MEDIUM

**M1 — Search vocabularies drift.** `FrmAdvancedSearch.cs:161,498` hardcodes the
six disability types; `FrmCase.cs:1447` and `FrmFamily.cs:279` read them from
`TblLookup`. An admin-added type becomes permanently unsearchable.

**M2 — Nine of twelve disability fields are write-only** (see 1.4).

**M3 — Disability type/severity cannot be printed on any card** (see 1.5).

**M4 — No card issuance record**, hence no renewal and no replacement (see 1.5).

**M5 — `DisabilityCause` / `DisabilityCardStatus` are not admin-manageable.**
Seeded at `DatabaseInitializer.cs:1699-1700` but absent from the `FrmSettings`
category list (`FrmSettings.cs:1795-1802`), unlike every other disability dropdown.

**M6 — `DisabilityCardNumber` has no uniqueness check** — two cases can record
the same government card number with no warning.

**M7 — Assistance never reaches the case timeline** (see 1.7).

### LOW

**L1 — Disability data entry is split across two non-adjacent tabs**, and only
the *مشخصات پرونده* group is gated by `ShowDisabilitySection` (Decision #12), so
type and severity stay visible for request types that have no disability section.

**L2 — Two overlapping audit tables** (`TblAuditLog` / `TblAuditLogs`) —
pre-existing, documented debt.

---

## 4. Verified as correct (no action)

- Card numbers cannot duplicate — `FormNo` is unique and is the card number.
- Barcode is center-qualified (`BranchCode-CardNumber`), unique across centers.
- Assistance duplicate prevention: three layers, including a user confirm.
- Dashboard disability metrics are member-level and explicitly labelled as such —
  compliant with the Primary Case Type Rule.
- `TblDisability` is correctly registered for sync and for backup/restore.
- All disability SQL is parameterised; no injection surface found.
- Section visibility is driven solely by `TblRequestType` flags, never hardcoded.
