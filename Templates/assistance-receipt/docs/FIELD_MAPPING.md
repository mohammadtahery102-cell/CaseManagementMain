# AssistanceReceipt — field mapping

Mirrors the `GuardianCard` package's convention: this static bundle (`index.html` + `receipt.js` + `receipt.css`) is frozen design/render logic. The C# side (new `AssistanceReceiptIntegration/` module, built the same way as `GuardianCardIntegration/`) builds the JSON below and stages it as `sample/SAMPLE_DATA.json` next to a copy of this bundle, then loads it in a WebView2 via `SetVirtualHostNameToFolderMapping` (fetch is blocked on `file://`).

Single print → a JSON **object**. Batch print → a JSON **array** (2 receipts print per A4 sheet, oldest-first or by the batch's own sort — see `receipt.js`'s `render()`).

| JSON field | Type | Source | Notes |
|---|---|---|---|
| `OrganizationName` | string | `SettingsHelper.OrgName` | Same settings key `CardService` already reads for the guardian card. |
| `Logo` | string (relative path) | `SettingsHelper.LogoPath`, staged into `uploads/` | Same staging pattern as `GuardianCardRenderer.StageImage`. Empty → placeholder box. |
| `ReceiptCode` | string | **new**, system-generated, sequential | User asked for auto/sequential (not manual entry). Suggest `"AFG-" + CenterCode + "-" + ReceiptNo.ToString("D6")` — mirrors `CardNumber` in `CardService.BuildCardData` (`FormNo.ToString("D6")`). Needs a counter — see Open Questions. |
| `SerialNo` | string | same counter as `ReceiptCode` | `"SN-" + ReceiptNo.ToString("D6")`. Printed identically on both receipt and office stub so they can be matched (anti-fraud). |
| `RecipientName` | string | `TblCase.HeadFullName` | |
| `FatherName` | string | `TblCase.HeadFatherName` | |
| `TazkiraNo` | string | `TblCase.HeadTazkiraNo` | |
| `Phone` | string | `TblCase.Phone` | |
| `AidTypeAndAmount` | string, pre-formatted | `TblAssistance.AssistanceType` + `Amount` | Format server-side, e.g. `AssistanceType + " — " + Amount.ToString("N0") + " افغانی"` — keep the HTML dumb (same policy as `OrphansCount` in `GuardianCardData`). |
| `DistributionDate` | string, pre-formatted | `TblAssistance.AssistanceDate` via `PersianDateHelper.ToPersianDateString` | |
| `ProvinceDistrict` | string, pre-formatted | `TblCase.Province` + `TblCase.District` | `Province + " — " + District"`. |
| `FamilyMembersCount` | string, pre-formatted | `COUNT(TblFamily WHERE CasID=...)` | Same query `CaseCardRepository.GetFamilyMemberCount` already runs; append `" نفر"`. |
| `ProgramName` | string | **missing today** | `TblAssistance` has no program/project column — see Open Questions. |
| `RequestType` | string | `TblCase.RequestType`? | One of: ایتام، بدسرپرست، بی‌سرپرست، کهن‌سال، معلول. Confirm this maps to the existing `RequestType` column (its current values aren't visible from the model alone) rather than needing a new column. |
| `PickupLocation` | string | **missing today** | Where the recipient collects the aid (office/distribution point). Editable per-receipt or per-program — add a column or reuse a settings default with an override. See Open Questions. |
| `CoordinatorPhone` | string | **missing today** | Program coordinator's contact number — likely a per-program or per-batch value, not per-case. See Open Questions. |
| `DisplacedCardNo` | string | **missing today** | No IDP/refugee-card column found on `TblCase` — see Open Questions. |
| `Photo` | string (relative path) | `TblCase.PhotoPath`, staged into `uploads/` | Empty → placeholder box. |
| `Barcode` | string (relative path) | generate with the **existing** `Code128Barcode.SaveToFile(ReceiptCode, ...)` | Reuse as-is — do not rewrite; it already produces a real, scannable Code128 PNG. |

## Open questions (please confirm before wiring the C# side)
1. **`ReceiptNo` counter** — is there already a sequence/table for it, or should `AssistanceReceiptRepository` add one (a new `ReceiptNo INTEGER` column on `TblAssistance`, filled on first print, center-scoped like `FormNo`)?
2. **`ProgramName`** — add a column to `TblAssistance` (e.g. `ProgramName TEXT`), or is this actually `RequestType`/some existing field reused?
3. **`IsPrinted` status** — the "چاپ‌شده/نشده" filter needs a boolean/date column on `TblAssistance` (e.g. `PrintedAt DATETIME NULL`), set when a receipt is generated.
4. **`DisplacedCardNo`** — new column on `TblCase`, or does `MigrationCardType` already cover this (it looks like a category, not a card number)?
5. **`RequestType`** — confirm the 5 category values (ایتام، بدسرپرست، بی‌سرپرست، کهن‌سال، معلول) already live in `TblCase.RequestType`, or need a new lookup/column.
6. **`PickupLocation` / `CoordinatorPhone`** — per-case, per-program, or a single settings default the office can override at print time? Affects whether these belong on `TblAssistance`/`TblCase` or in `SettingsHelper`.

## Fonts
`receipt.css` falls back to `Segoe UI`/`Tahoma` (both ship with Windows and render Persian). For the exact look shown in the design (Vazirmatn), self-host the two Vazirmatn weight files next to this bundle and add an `@font-face` — do not rely on a Google Fonts `<link>`, since offices may print offline.
