# Disability Field Coverage Across All Outputs

**Date:** 2026-09-05 · Verified by direct inspection of each output's source.

There are **11 disability fields** (`TblDisability`, 7 of them mirrored to
`TblCase`). This matrix shows where each one actually appears.

| # | Field | Word | PDF | Excel | RDLC | Card | Report Builder |
|---|---|---|---|---|---|---|---|
| 1 | `DisabilityType` (نوع معلولیت) | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ |
| 2 | `DisabilityDegree` (درجه معلولیت) | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ |
| 3 | `DisabilityCause` (دلیل معلولیت) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 4 | `DisabilityDescription` (شرح معلولیت) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 5 | `SpecialNeeds` (نیازهای خاص) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 6 | `DisabilityCardStatus` (وضعیت کارت) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 7 | `DisabilityCardNumber` (شماره کارت) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 8 | `CardIssuer` (صادرکننده کارت) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 9 | `IssueDate` (تاریخ صدور) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 10 | `ExpiryDate` (تاریخ انقضا) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 11 | `Notes` (یادداشت معلولیت) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| | **Coverage** | **11/11** | **11/11** | **11/11** | **2/11** | **0/11 renderable** | **0/11** |

✅ present · ⚠️ data available but not renderable · ❌ absent

---

## Detail per output

### Word export — 11/11 ✅ complete
`OpenXmlCaseExporter` reads all 11 via `LEFT JOIN TblDisability` (safe:
`UNIQUE(CasID)` cannot multiply rows) and exposes `{{Disability*}}` /
`{{SpecialNeeds}}` placeholders. A template lacking a placeholder is unaffected;
`RemoveUnusedPlaceholdersEverywhere` cleans empties.

*Also present:* `{{DisabilityDetails}}` — the **member-level** free-text field
from `TblFamily`, distinct from the case-level 11.

### PDF — 11/11 ✅ complete (inherited)
PDF is produced by converting the Word document
(`PdfConversionHelper.ConvertDocxToPdf`), so it carries Word's coverage exactly.
No separate PDF field list exists.

### Excel export — 11/11 ✅ complete
All 11 present as Persian-headed columns on the "پرونده ها" sheet.

> ⚠️ **9 columns were added after «نوع معلولیت».** Any downstream spreadsheet or
> macro reading this sheet **by column index** must be updated. Reading by header
> name is unaffected.

### RDLC printed report — 2/11 ❌ **gap**
`RptFullCase.rdlc` and the typed dataset `DsFullCaseReport.xsd` carry only
`DisabilityType` and `DisabilityDegree`.

**Not fixed — deliberately.** Extending it means editing the `.rdlc`, the
`.xsd`, and its three generated files (`.Designer.cs`, `.cs`, `.xsc`/`.xss`).
That is the highest-risk reporting change available, is not covered by the test
suite, and falls under the "no further architectural changes" instruction.

*The RDLC has **zero** aggregate expressions — it is a single-case detail report,
so there are no counts or totals to verify.*

### Beneficiary card — 0/11 renderable ⚠️ **gap**
There is **no disability card**; the beneficiary/guardian card is the closest
equivalent.

- `DisabilityType`/`DisabilityDegree` now reach the card JSON (`GuardianCardData`)
- **`CardFieldCatalog` contains zero disability entries**, and no HTML template
  binds them → nothing prints

**Not fixed — deliberately.** The catalog's own code comment warns that listing a
field the template does not bind means the user configures something silently
ignored. The catalog entry and the template change must ship together.

### Report builder — 0/11 ❌ **gap**
`ReportDefinitions` exposes only the **member-level** `HasDisability`
(`f.HasDisability`). No case-level disability column is available to the dynamic
report builder.

**Not fixed** — adding columns is feature work, excluded by instruction.

---

## Where the data is complete

Nothing is lost. Every one of the 11 fields is:

- **stored** correctly (`TblDisability`, 7 mirrored to `TblCase`)
- **backed up** and **restored**
- **synced** between centers (create, update **and delete**)
- **audited** at field level with old→new values
- **visible** in the case History tab
- **exportable** in full via Word, PDF and Excel

The three gaps are all *presentation* gaps in secondary outputs, with a complete
alternative available (Word/PDF/Excel).

---

## Recommendation for users

> For the complete disability record of a case, use **Word, PDF or Excel export**.
> The printed RDLC report and the beneficiary card intentionally show only
> disability **type** and **degree**.
