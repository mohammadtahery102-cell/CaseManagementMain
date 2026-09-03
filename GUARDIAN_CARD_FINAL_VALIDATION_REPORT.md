# GUARDIAN CARD — FINAL VALIDATION REPORT

## کارت شناسایی سرپرست ایتام — گزارش نهایی ممیزی و اعتبارسنجی

| | |
|---|---|
| **Report Version** | 1.0 |
| **Audit Date** | 2026-08-24 |
| **Scope** | ID Card single/batch printing pipeline — final audit only, no redesign |
| **Codebase** | `C:\Projects\CaseManagement\GuardianCardIntegration` + frozen package `C:\Projects\GuardianCard` |
| **Prior fix under re-validation** | TextOverride font-scaling compounding bug in `GuardianCardRenderer.cs` |
| **Method** | Static code re-audit + empirical reproduction (headless Edge, 1/10/50/100-card batches) + MSBuild verification |

---

## 1) Current Architecture

Two independent WinForms entry points share one rendering pipeline and one frozen HTML/CSS/JS package:

```
Single card:
  FrmGuardianCardPreview
    → CardService.BuildCardData(caseId)                         [one GuardianCardData]
    → GuardianCardRenderer.StageAndPopulate(...)                 [copies GuardianCard/ → temp working folder,
                                                                    stages 1 set of images/QR/barcode,
                                                                    writes sample/SAMPLE_DATA.json as an OBJECT]
    → WebView2.Navigate(index.html)
    → guardian-card.js: loadData() → populateCard(data, document)  [fills the two ORIGINAL #cardFront/#cardBack elements]
    → ShowPrintUI / PrintToPdfAsync

Batch cards:
  FrmGuardianCardBatchPrint  (two-stage UI: filter+grid preview → explicit "N cards, continue?" confirm)
    → CaseCardRepository.PreviewBatch(filter)                    [cheap list-only preview, no card rendering yet]
    → CardService.BuildCardDataForCaseIds(caseIds)                [List<GuardianCardData>, per-case failures skipped]
    → GuardianCardRenderer.StageAndPopulateBatch(...)             [copies GuardianCard/ → temp working folder ONCE,
                                                                    stages shared Logo/Signature/Stamp ONCE,
                                                                    stages per-record Photo/QR/Barcode/FamilyPhoto/Orphans
                                                                    with unique index suffixes,
                                                                    writes sample/SAMPLE_DATA.json as an ARRAY]
    → WebView2.Navigate(index.html)
    → guardian-card.js: loadData() → populateBatch(list)          [clones #cardFront/#cardBack N times via cloneNode,
                                                                    stripIds(), inserts front_i/back_i pairs in order,
                                                                    calls populateCard(item, clone) per face]
    → ShowPrintUI / PrintToPdfAsync
```

The frozen package (`C:\Projects\GuardianCard`) is **never modified**. `GuardianCardRenderer` copies it into a disposable temp working folder on every render and only injects extra `<style>`/`<script>` tags into that *copy's* `index.html` (never `simple.html` — see §6) for: disabled-field cleanup, Card Designer color/font/background overrides, watermark, and per-field text overrides.

Both single and batch print use the **same** `print.css` (`@page` bleed-inclusive custom size, `.card { page-break-after: always }`, `.card:last-child { page-break-after: auto }`) — batch relies on `populateBatch` producing N pairs of `.card` elements for this rule to paginate correctly.

---

## 2) Previous Root Cause (recap, for context)

**File:** `GuardianCardIntegration/GuardianCardRenderer.cs`, method `BuildTextOverrideScript`.

The injected per-field font-size override script listened for the `guardiancard:populated` DOM event and, on every firing, read the element's **current** computed font-size (`getComputedStyle(el).fontSize`) and multiplied it by the configured scale:

```js
var basePx = parseFloat(getComputedStyle(el).fontSize);
el.style.fontSize = (basePx * scale) + "px";
```

- **Single card:** the event fires exactly twice (front, back) — each field is scaled once. Correct.
- **Batch:** `populateBatch` dispatches the event once per populated card face (2×N times for N records), and the override script re-scans the **entire document** each time (`document.querySelectorAll`), including elements from *earlier, already-scaled* cards. Because it read the *current* (already-inflated) value instead of the original base value, every re-firing multiplied the already-multiplied value again — a geometric compounding. A template with a 130% font-size override on one field produced a font-size of **1266.99px** (from a ~6.7px base) by the time a 10-card batch finished populating — visually massive, overlapping text.

This was fixed by caching the *original* base font-size once per element (`data-base-font-size` attribute), so every re-application — no matter how many times the event fires — always multiplies from the same true base value.

---

## 3) Fix Verification (this audit)

### 3.1 Code re-check
Confirmed the fix is present and unchanged in `GuardianCardRenderer.cs` (`BuildTextOverrideScript`, `applyOne`):

```js
var baseAttr = el.getAttribute("data-base-font-size");
var basePx = baseAttr ? parseFloat(baseAttr) : parseFloat(getComputedStyle(el).fontSize);
if (!isNaN(basePx)) {
  if (!baseAttr) el.setAttribute("data-base-font-size", String(basePx));
  el.style.fontSize = (basePx * scale) + "px";
}
```

### 3.2 Empirical scale test (1 / 10 / 50 / 100 cards)

Method: a clean copy of the frozen `GuardianCard` package was served locally; the exact script `GuardianCardRenderer.BuildTextOverrideScript` currently generates (three overridden fields: `GuardianName` color+130% scale, `Notice1` 150% scale + 120% line-height, `OrganizationName` 80% scale) was injected the same way the C# renderer injects it. Batches of 1, 10, 50, and 100 distinct records were rendered with headless Edge (`--dump-dom`) and the resulting inline `font-size` was inspected on every card.

| Batch size | Distinct `GuardianName` font-size values found | Distinct `Notice1` font-size values | Distinct `OrganizationName` font-size values |
|---|---|---|---|
| 1 | `8.667px` | `10.0px` | `11.733px` |
| 10 | `8.667px` (all 10 identical) | `10.0px` (all 10) | `11.733px` (all 10) |
| 50 | `8.667px` (all 50 identical) | `10.0px` (all 50) | `11.733px` (all 50) |
| 100 | `8.667px` (all 100 identical) | `10.0px` (all 100) | `11.733px` (all 100) |

**Result: no compounding, no drift, at any tested scale.** Values are identical to the single-card baseline in every case.

### 3.3 Per-record data integrity at scale (100 cards)
- 100/100 `GuardianName` values unique and exactly matching the expected per-record text (`سرپرست شماره 1..100`).
- 100/100 `PublicCode` values unique.
- Zero ordering/content mismatches between DOM position and source record.

### 3.4 Print/PDF pagination at scale
Real PDF generation via headless Edge `--print-to-pdf`:

| Batch size | PDF page objects found | Expected (2 × N) |
|---|---|---|
| 50 | 100 | 100 ✅ |
| 100 | 200 | 200 ✅ |

- `@page` custom size honored uniformly: MediaBox `612×438pt` = `215.9×154.52mm` (spec: `216×154.5mm` bleed-inclusive) on every sampled page — no dimension drift across the batch.
- Front/back page order is guaranteed by code, not just observed: `populateBatch`'s `insertBefore` sequence yields DOM order `front1, back1, front2, back2, …, frontN, backN`, and `.card{page-break-after:always}` / `.card:last-child{page-break-after:auto}` maps that 1:1 to page order.

### 3.5 Build
```
MSBuild CaseManagement.csproj /p:Configuration=Debug /t:Build
→ exit code 0, no errors
```

**Conclusion: the previous fix holds correctly at 1×, 10×, 50×, and 100× scale. No font multiplication, no overlap, no layout corruption was reproduced.**

---

## 4) Single vs Batch Pipeline — Comparison

Traced line-by-line (`FrmGuardianCardPreview` / `StageAndPopulate` vs `FrmGuardianCardBatchPrint` / `StageAndPopulateBatch`, and `populateCard` vs `populateBatch` in `guardian-card.js`).

**No functional gaps found** in data staging: both paths stage Photo/Logo/Signature/Stamp/Barcode/QR/FamilyPhoto/Orphans, apply the family-list row cap, `ApplyTextOverrides`, disabled-field cleanup, and Card Designer overrides. Batch shares Logo/Signature/Stamp once (correct — they're organization-wide) and gives every record its own uniquely-indexed Photo/Barcode/QR/FamilyPhoto/Orphan-photo filenames (`_i`, `_i_m` suffixes) — collision-free for any N.

**Intentional, non-bug differences** (documented for completeness, not flagged as defects):

| Difference | Single | Batch | Verdict |
|---|---|---|---|
| Per-case notice/content override ("فقط برای این چاپ") | Available (`FrmCardNoticesEdit`) | Not available — uses saved DB values only | By design; noted as a possible future feature request, not a defect |
| `_original*Path` caching for template re-switch | Present (avoids re-feeding an already-staged relative path back in as a source path) | Not needed — template is chosen once before a single render call, no re-render-on-switch loop exists in the batch UI | Correct, no gap |
| Readiness gating before enabling Print/PDF | No explicit wait beyond `Navigate()` (WebView always visible) | Awaits `NavigationCompleted` only, then makes WebView visible and enables buttons | See §7.1 — soft observation, not a reproduced defect |
| Pre-render confirmation | None (cheap, one card) | Two-stage filter+grid preview, then explicit "N cards will be printed — continue?" | Deliberate UX safeguard for batch |

---

## 5) Template / DOM Isolation Audit

- **Cloning:** `guardian-card.js:populateBatch` deep-clones `#cardFront`/`#cardBack` per record and strips **all** `id` attributes recursively (`stripIds`). Confirmed via `grep` of `guardian-card.css` that **zero** CSS rules target `#cardFront`/`#cardBack` by id — all styling is class- and `data-field`-based, so id-stripping has no visual side effect; it exists purely to keep the printed HTML valid (no duplicate ids).
- **Event scoping:** all three injected override scripts (disabled-field cleanup, watermark, text-overrides) intentionally re-scan the *whole* `document` on every `guardiancard:populated` firing (the event is dispatched on `document`, not the clone) — this is deliberate, documented in the source comments, and each is idempotent by construction:
  - Disabled-field cleanup → `el.style.display = "none"` (idempotent, absolute assignment).
  - Watermark → checks `trim.querySelector(".__watermark")` before inserting (idempotent, guarded).
  - Text overrides (color/fontFamily/lineHeight) → absolute assignment (idempotent).
  - Text overrides (font-size) → **was** the one non-idempotent path; now fixed (§2–3).
- **Data isolation:** `CardService.BuildCardData` builds a fresh `GuardianCardData` POCO per case; `PaymentLedger`/`Orphans` are freshly-`new`'d per instance (no static/shared mutable collections anywhere in `GuardianCardData.cs`, `CaseCardRepository.cs`, or `CardTemplateRepository.cs`). `CardTemplateRepository.ApplyTextFields`/`ApplyTextOverrides` only read from the shared `CardTemplateDesign`/`CardTemplate` and write into the per-item `data` — no cross-item mutation possible. Confirmed empirically at n=100 (§3.3): zero cross-contamination.
- **Images/QR/Barcode:** every per-record asset filename includes the record's loop index (and member index for orphans) — verified collision-free by construction for any batch size.

**Simple layout (`simple.html`/`simple.js`) — separately audited:**
- `ApplyDesignOverrides` in `GuardianCardRenderer.cs` only ever writes to `index.html`; it never touches `simple.html`. This means Card Designer colors/fonts/background/watermark and per-field `TextOverrides` **silently have no effect** on templates whose `LayoutVariant = "Simple"`. This is a **pre-existing scope limitation**, not a regression — confirmed by reading the method (hardcoded `Path.Combine(workingFolder, "index.html")`). Worth documenting for template authors so it isn't mistaken for a bug.
- `simple.js`'s own `populateBatch` does not call `stripIds()` — audited and confirmed harmless, since `simple.html` has **zero** `id=` attributes to begin with.
- `simple.js`'s disabled-field cleanup script only toggles `display:none` — no font-scale logic exists on this path at all, so the compounding-bug pattern was never reachable here in the first place (not because it was fixed, but because Simple never gets font-size overrides injected — see previous bullet).

---

## 6) Print Quality Validation

| Check | Result |
|---|---|
| Page breaks (50/100-card PDF) | Exactly 2×N pages, verified via raw PDF object count |
| Card dimensions | `216×154.5mm` bleed-inclusive size uniform across every sampled page |
| Front/back alignment | Guaranteed by DOM insertion order + `page-break-after` CSS (code-level guarantee, not just observed) |
| RTL layout | `<html lang="fa" dir="rtl">` in both `index.html`/`simple.html`; WinForms hosts set `RightToLeft.Yes` + `RightToLeftLayout=true` — consistent at both layers |
| Photo quality | `StageImage()` does a byte-for-byte `File.Copy`, no resize/recompression — original resolution fully preserved at any batch size |
| Visual (pixel) screenshot check | **Not performed** — the Chrome browser extension (`claude-in-chrome`) was not connected in this environment. Verification instead relied on deterministic structural evidence (exact PDF page counts, uniform `MediaBox`, uniform font-size values, code-guaranteed DOM ordering), which is stronger evidence for *this specific bug class* than a visual screenshot, but a human eyeballing one real multi-card PDF before shipping is still recommended as a final sanity check. |

---

## 7) Remaining Issues / Residual Observations

None of the following were reproduced as defects in this audit; they are flagged per the instruction to "not assume the problem is completely solved."

1. **Readiness gating is implicit, not explicit.** Neither pipeline waits for a JS-side "fully populated and images loaded" signal before enabling Print/PDF — both rely on WebView2's `NavigationCompleted` plus the human pause of visually reviewing the on-screen card before clicking Print. No failure was reproduced under this audit (including an artificially tight headless print budget), but it remains an assumption rather than a guarantee. **Low priority** given zero observed failures; flagged for completeness only.
2. **Simple layout ignores Card Designer visual settings** (§5) — could surprise a template author. Recommend a documentation note or a UI hint in `FrmCardTemplateManager` when `LayoutVariant = Simple` is selected (out of scope for this audit — report only).
3. **No bulk notice/content override for batch printing** — acceptable today (batch reads saved per-case DB overrides), but a gap if bulk pre-print editing is ever requested.
4. **No automated regression test exists** for the card-rendering pipeline — no test under `CaseManagement.Tests` references `GuardianCardIntegration`. All verification (the original fix and this audit) was manual/external, using a headless-browser harness built ad hoc. **Recommendation:** capture that harness as a lightweight repeatable regression check so a future edit to `BuildTextOverrideScript` or `populateBatch` cannot silently reintroduce compounding.
5. **Batch sizes beyond 100 were not tested** (task ceiling was 100). File I/O scales linearly with record count (each record does a bounded number of `File.Copy`/`File.Exists` calls); no O(N²) structure was found anywhere in the traced pipeline, so larger batches are expected to simply take proportionally longer, not fail — but this is inferred from code structure, not empirically verified beyond N=100.

---

## 8) Template Management Review

### 8.1 Current capabilities (confirmed by code)
- **Field enable/disable:** ~20 optional fields per layout (`ToggleableFields` / `ToggleableFieldsSimple`), enforced both in the manager UI (checklist) and at render time (DOM hide script).
- **Font settings:** global `FontScalePercent` (CSS variable) + per-field `TextOverrides[field].FontSizePercent/FontFamily/LineHeightPercent` — **Full layout only** (§5).
- **Color settings:** Primary/Secondary/Background/Text/HeaderBackground colors, all optional CSS custom-property overrides.
- **Logo/Signature/Stamp:** independently toggleable, sourced from global org settings.
- **QR/Barcode:** independently toggleable, both generated fresh per record from a collision-free identifier.
- **Front/back:** controlled via `LayoutVariant` (Full = `index.html`, has both faces; Simple = `simple.html` front + shared ledger back) plus independent front/back background images — but front and back are not independently "template-able" beyond background image + shared field toggles (no separate back-specific template concept).
- **Live preview:** a real embedded WebView2 render inside `FrmCardTemplateManager` (not a mockup), debounced re-render on setting change.
- **Export/Import:** confirmed present — self-contained JSON export (base64-embedded images) and matching import, via `ExportCurrent()`/`ImportTemplate()`.
- **Permission gating:** `GuardianCard.ManageTemplates` (template CRUD) is a separate permission from `GuardianCard.Print` (printing) — both independently enforced via `PermissionService.Require` in each form's constructor.

### 8.2 Missing / gaps for a more "professional" system (report only — not implemented, per instruction)

| Gap | Evidence | Notes |
|---|---|---|
| **Field ordering** | Explicitly acknowledged in the existing code comment: the drag-handle (⋮⋮) in the field checklist reorders only the *list UI*, never the actual printed position, because `index.html`/`simple.html` are fixed HTML/CSS layouts with pre-determined per-field positions. | True field repositioning would require a different rendering architecture (e.g. a real drag-and-drop canvas) — correctly out of scope for a report-only pass. |
| **Template versioning** | `TblCardTemplate` schema has only a single `CreatedAt` timestamp; `Save()` performs an in-place `UPDATE`/`INSERT` with no history table, no version numbers, no rollback, no diff view. | Would need a new history table + UI; non-trivial. |
| **Audit log** | No `ModifiedBy`/`ModifiedAt` columns and no separate audit table exist for `TblCardTemplate` — there is currently no way to answer "who changed this template and when." | Same schema-change caveat as above. |
| **Permission control granularity** | Only two coarse permissions exist (`ManageTemplates`, `Print`) — no per-template ownership/scoping (e.g. center-restricted templates), no read-only "view but not edit" role, no approval workflow before a template goes live. | Would build on the existing `PermissionService`, but needs new permission keys + scoping logic. |

---

## 9) Recommended Next Improvements (priority order)

1. **Add a lightweight regression test** for the batch font-scaling fix using the headless-Edge harness built for this audit (§3.2) — cheapest way to guarantee this exact class of bug can never silently return.
2. **Document the Simple-layout Card-Designer limitation** (§5, §7.2) in `FrmCardTemplateManager`'s UI (a one-line hint when Simple is selected) so template authors aren't confused by settings that silently do nothing.
3. If bulk editing is ever requested: extend batch printing with an optional "apply this notice text to all selected cases" step before confirmation — a small, additive feature, not a redesign.
4. If audit/versioning becomes a compliance requirement: add `ModifiedBy`/`ModifiedAt` columns to `TblCardTemplate` first (cheapest, additive, non-breaking) before considering full version history.
5. Field ordering (true drag-and-drop repositioning) and per-template permission scoping are legitimate long-term asks but represent significant architecture changes — correctly deferred, not attempted here.

---

## 10) Summary

- The previously-identified and previously-fixed TextOverride font-scaling compounding bug **remains correctly fixed** under re-audit, verified at 1×, 10×, 50×, and 100× batch scale with no font multiplication, no overlap, and no layout corruption.
- No other correctness defects were found in the single-vs-batch pipeline, DOM/template isolation, or print pagination during this audit.
- A small number of pre-existing, non-blocking scope gaps were identified and documented (§7, §8.2) — none require code changes to close out this audit; they are recommendations for future work, in line with the "report only" instruction.
- No rewrites, redesigns, or new template engine work were performed or are recommended as urgent. Existing single-card printing behavior is unchanged. Build is clean (exit code 0).
