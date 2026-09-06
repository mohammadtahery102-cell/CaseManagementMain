# H4 — Assistance Entry Silently Reclassifies Cases

> **Investigation complete. Not implemented — deliberately.**
> Your instruction: *"If the fix affects other request types, workflows, reports,
> or shared business logic, stop and provide an impact assessment before changing
> production behavior."* H4 meets **all four** of those conditions. Assessment below.

---

## 1. Root Cause

`FrmFinance.btnSave_Click` writes the case's request type on **every** assistance
save, whether or not the user touched the control:

```csharp
// FrmFinance.cs — inside the assistance INSERT transaction
using (SQLiteCommand cmdReq = new SQLiteCommand(
    "UPDATE TblCase SET RequestType=@RequestType WHERE CasID=@CasID", con))
{
    cmdReq.Parameters.Add("@RequestType", DbType.String, 100).Value = cmbRequestType.Text.Trim();
    cmdReq.Parameters.Add("@CasID", DbType.Int32).Value = selectedCaseId;
    cmdReq.ExecuteNonQuery();
}
```

There are **three independent defects** here, not one.

### 1a. `RequestTypeID` is never updated — the dual-write invariant is broken

`TblCase` carries the request type twice by design (Known Decision #1): the legacy
TEXT column `RequestType`, and the authoritative FK `RequestTypeID`. Every other
write path updates both. **This path updates only the TEXT column.**

Consequence: after any deliberate change here, the case's two type values disagree
permanently. Nothing detects or repairs it.

### 1b. A `DropDownList` silently ignores an unknown value

`cmbRequestType` is `ComboBoxStyle.DropDownList` and is filled from
`TblLookup` category `RequestType`. On case selection:

```csharp
cmbRequestType.Text = requestType;   // FrmFinance.cs, dgvCases_CellClick
```

For a `DropDownList`, assigning `.Text` to a value **not present in `Items`** does
nothing — no exception, no change. The control keeps its previous selection.

This is not hypothetical. `PROJECT_CONTEXT.md` records that the legacy value
«سایر» was removed from the request-type vocabulary in Phase 3 while existing
cases kept the TEXT value. Any such case — or any case whose type an admin later
removes from the lookup — selects into this form and leaves the combo showing
**the previously selected case's type**. Saving assistance then stamps that
foreign type onto the case.

### 1c. There is no guard, no confirmation, and no audit

The write is unconditional. `AuditLogger.Log("ثبت کمک", ...)` records the
assistance amount only — the type change is invisible to the audit log, to
`TblCaseStatusHistory`, and to `TblCaseTimeline`. `VersionService.Capture` does
snapshot `TblCase`, so the change is *recoverable* by diffing versions, but
nothing surfaces it.

`TblCase.RequestType` has **no CHECK constraint**, so an empty or invalid string
writes successfully. If `TblLookup` category `RequestType` were ever empty, the
combo would be empty and the save would blank the case's classification.

---

## 2. Impact Analysis

`TblCase.RequestType` is the **primary case classification**. Per Strategic
Business Rule 1: *"A case is classified by the Request Type of its Head of
Household, and by nothing else."* Every standard count, report, filter and search
reads it.

### Failure scenario (concrete)

1. Case A is `معلول` (disabled). Case B is a legacy case whose TEXT type is
   «سایر» — a value no longer in the lookup.
2. Operator selects A, records assistance. Combo shows «معلول».
3. Operator selects B. `cmbRequestType.Text = "سایر"` is silently ignored;
   the combo still shows «معلول».
4. Operator records assistance for B.
5. **Case B is now classified `معلول` in every report** — while its
   `RequestTypeID` still points to its original type.

### Split-brain consequences of 1a

The two columns feed different subsystems, so a divergent case behaves as two
different types simultaneously:

| Reads TEXT `RequestType` | Reads `RequestTypeID` |
|---|---|
| `FrmDashboard` KPIs and counts | `FrmCase` section visibility (`ShowDisabilitySection`, …) |
| `FrmAdvancedSearch` (head + member tabs) | `TblRequiredDocument` — mandatory documents |
| `ExcelReportExporter`, `OpenXmlCaseExporter` | `TblRequiredField` — completion scoring |
| `RptFullCase.rdlc` / `DsFullCaseReport` | `CaseActivationValidator` — the activation gate |
| `CardService` / card printing | `CaseCompletionService` |
| `ReportDefinitions` / report builder | `VulnerabilityScoreService` |
| Sync payloads, `HtmlSyncProvider` | `CaseModuleService` module gating |

A disability case wrongly stamped `ایتام` would: print «ایتام» on its ID card,
count as an orphan in every dashboard total and export — while still showing the
disability section, still demanding disability documents, and still gated for
activation as a disability case. Reports and the working screen would disagree,
and neither would be obviously wrong to a user.

### Blast radius

- **All six request types**, not just `DISABLED` — the case list in `FrmFinance`
  is filtered only by center, search text and service status.
- **Archived cases included** — the case list has no `IsArchived = 0` filter, so
  archived cases can be selected and reclassified.
- **Multi-center**: the change syncs. `SyncOutboxService.Capture("TblCase", …)`
  runs right after, so a wrong classification propagates to head office.

---

## 3. Affected Forms

| Form | Role |
|---|---|
| `FrmFinance` | **Origin of the defect.** The only writer of this UPDATE. |
| `FrmCase` | Displays and re-saves the case; a save here *would* correct both columns, masking the problem intermittently |
| `FrmDashboard` | Counts by TEXT type — shows the wrong totals |
| `FrmAdvancedSearch` | Filters by TEXT type — case appears under the wrong filter |
| `FrmArchive` | Archived cases are reachable and reclassifiable |
| `FrmReportBuilder` | Reads TEXT type |

## 4. Affected Reports & Exports

| Output | Effect |
|---|---|
| `RptFullCase.rdlc` | Prints the wrong request type |
| Excel case export | Wrong «نوع درخواست» column; wrong grouping and totals |
| Word case export | Wrong `{{RequestType}}` |
| Dashboard KPI cards | Miscounts by type — a disability case counted as an orphan |
| Report builder | Wrong type column and filters |
| Guardian/beneficiary card | Prints the wrong type on a physical card given to a beneficiary |

**Counts are wrong in both directions**: the true type is undercounted, the
stamped type overcounted. Totals still sum to the correct grand total, which is
exactly what makes this hard to notice.

---

## 5. Risk Assessment

| Dimension | Rating | Note |
|---|---|---|
| Severity of a wrong outcome | **High** | Corrupts the primary classification; wrong data on a printed beneficiary card |
| Likelihood (normal use) | **Low–Medium** | Requires a case whose type is missing from the lookup, or a user changing the combo |
| Detectability | **Very low** | Silent, unaudited, no error; visible only by diffing `RequestType` vs `RequestTypeID` |
| Reversibility | **Medium** | Original value recoverable from `EntRecordVersion` snapshots |
| Spread | **High** | Syncs to head office; feeds every report |
| **Overall** | **High** | Low probability × severe, silent, propagating consequences |

### Is the field even wanted here?

The code comment says the control exists «به‌درخواستِ کاربر» (at user request) so
an operator can set the request type while recording assistance, deliberately
reusing the existing column rather than adding a new one. So the feature is
intentional — **the defect is that it writes unconditionally, writes only half
the pair, and fails silently.**

---

## 6. Recommended Fix

Three layers, smallest first. **Layer 1 alone removes the silent-corruption risk**
and is the minimum viable fix.

**Layer 1 — write only on a real, intentional change (low risk)**
Capture the case's stored type when it is selected; skip the UPDATE entirely when
the combo still equals it. This makes the normal path a no-op, eliminating both
the "unknown value" scenario and every accidental overwrite.

**Layer 2 — keep the dual-write invariant (medium risk)**
When the type genuinely changes, resolve it via
`ReferenceDataService.FindRequestTypeByName(...)` and write **both**
`RequestType` and `RequestTypeID`. Refuse the change if the name does not resolve
to an active reference row — matching `FrmCase.ValidateForm`'s existing rule.

**Layer 3 — make it visible (low risk)**
Emit `TimelineService.LogRequestTypeChanged(casId, oldName, newName)` — the
method already exists and is already used by `FrmCase` — plus an
`AuditLogger` entry. With the new timeline tab live, this becomes user-visible
immediately.

**Also recommended, separately:** add `AND IFNULL(IsArchived, 0) = 0` to the
`FrmFinance` case list. Recording assistance against an archived case is
questionable independently of H4.

### Why I did not implement it

Layer 1 is genuinely small. But its behavioural surface is **every assistance
save for every request type**, and it changes what `TblCase.RequestType` — the
column Business Rule 1 designates as *the* classification — does on a path used
daily by operators. Layer 2 additionally introduces a new save-blocking
validation. Per your standing instruction, that is an impact assessment, not an
overnight change.

---

## 7. Testing Requirements

Before this ships, the fix needs the following coverage. None of it exists today
(`FrmFinance` currently has **no** test file).

**Regression tests (must pass before merge)**

1. Saving assistance without touching the combo leaves `RequestType` **and**
   `RequestTypeID` byte-identical.
2. Selecting case A (type X) then case B (type Y) and saving assistance for B
   leaves B as Y — the cross-contamination scenario in §2.
3. A case whose TEXT type is absent from `TblLookup` (e.g. «سایر») is **not**
   reclassified by an assistance save.
4. A deliberate type change updates **both** columns consistently.
5. A deliberate type change writes `REQUEST_TYPE_CHANGED` to the timeline and an
   audit entry.
6. An unresolvable type name is rejected, and the assistance is still saved (or
   the whole save is refused — decide which, then assert it).
7. An archived case either cannot be selected, or cannot be reclassified.

**Data-integrity check (run against production before and after)**

```sql
SELECT c.CasID, c.Code, c.RequestType, rt.Name AS TypeFromId
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
WHERE IFNULL(c.RequestType,'') <> IFNULL(rt.Name,'');
```

Every row returned is a case already diverged by this defect. **Run this before
handover** — it quantifies existing damage and is safe (read-only).

**Manual verification**

- Record assistance for a disability case; confirm the type is unchanged on the
  case form, in the Excel export, and on the printed card.
- Deliberately change the type; confirm both columns move together and the
  timeline tab shows the change.
