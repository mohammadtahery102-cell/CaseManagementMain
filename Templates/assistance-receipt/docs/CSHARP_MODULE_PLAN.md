# AssistanceReceiptIntegration — C# module plan (for Claude Code)

Mirrors `GuardianCardIntegration/` 1:1. Consumes the frozen bundle at `assistance-receipt/` (deploy as Content next to the exe, e.g. rename to `AssistanceReceipt/`, same as `GuardianCard/`).

## New files
- `AssistanceReceiptIntegration/AssistanceReceiptData.cs` — data contract, properties = exactly the keys in `assistance-receipt/docs/FIELD_MAPPING.md`.
- `AssistanceReceiptIntegration/AssistanceReceiptRepository.cs` — mirrors `CaseCardRepository`: `GetAssistance(int assistanceId)`, `GetAssistanceIdsByFilter(AssistanceReceiptFilter f)` (Province, District, FormNo, ProgramName, DateFrom/DateTo, AssistanceType, IsPrinted), reuse `GetFamilyMemberCount`. Same `CenterGuard.EnsureCaseAccess` + `@CID = 0 OR CenterID = @CID` pattern.
- `AssistanceReceiptIntegration/AssistanceReceiptService.cs` — mirrors `CardService`: `BuildReceiptData(int assistanceId)`, `BuildReceiptDataBatch(filter, out failedCount)`. Formats `AidTypeAndAmount`, `DistributionDate` (via `PersianDateHelper`), `ProvinceDistrict`, `FamilyMembersCount` server-side (HTML stays dumb, same policy as `OrphansCount`).
- `AssistanceReceiptIntegration/AssistanceReceiptRenderer.cs` — mirrors `GuardianCardRenderer`: stage bundle copy + `uploads/` (photo/logo) + `Code128Barcode.SaveToFile(ReceiptCode, ...)` (reuse as-is) + write `sample/SAMPLE_DATA.json`. `StageAndPopulate` (single) and `StageAndPopulateBatch` (array, 2-per-A4-page — bundle already handles pagination).
- `AssistanceReceiptIntegration/FrmAssistanceReceiptFilterPrint.cs` — mirrors `FrmGuardianCardBatchPrint`: filter panel (Province/District cascading via `AfghanGeoData`, FormNo, ProgramName, date range via `PersianDatePicker`, AssistanceType, IsPrinted), WebView2 preview, چاپ + ذخیره PDF buttons (identical WebView2/PrintToPdfAsync pattern).
- `AssistanceReceiptIntegration/FrmAssistanceReceiptSinglePrint.cs` — search grid (by name/code/FormNo) → pick one → preview/print via the same renderer, single-item JSON.

## Reuse — do not rewrite
`Code128Barcode`, `PersianDateHelper`, `AfghanGeoData`, `CenterGuard`, `SettingsHelper`, `UiTheme`, `Msg`, the WebView2 toolbar/print/PDF pattern in `FrmGuardianCardBatchPrint`.

## Database changes (additive only, via `DatabaseInitializer`'s existing column-guard pattern)
- `TblAssistance`: add `ReceiptNo INTEGER` (sequential, center-scoped like `FormNo`), `PrintedAt DATETIME NULL` (drives the چاپ‌شده/نشده filter), `ProgramName TEXT`, `PickupLocation TEXT`, `CoordinatorPhone TEXT`.
- `TblCase`: confirm `RequestType` already stores one of ایتام/بدسرپرست/بی‌سرپرست/کهن‌سال/معلول; add `DisplacedCardNo TEXT` if not present.
- `ReceiptCode` = `"AFG-" + CenterCode + "-" + ReceiptNo.ToString("D6")`, `SerialNo` = `"SN-" + ReceiptNo.ToString("D6")` — set once, on first print.

## Risks
- Schema changes touch two live tables — additive `ALTER TABLE` only, guarded by column-exists checks (existing project convention).
- `ReceiptNo` must be center-scoped to avoid collisions across branches (same risk `FormNo`/`CardNumber` already solve for).
- Decide once: is a receipt per `TblAssistance` row (one family can receive aid several times) — recommended — or per `TblCase`? Plan above assumes per-assistance-record.
- Self-host Vazirmatn font files in the bundle for offline print reliability (noted in FIELD_MAPPING.md).

Approval gate per project convention: confirm the DB additions above before implementing (nothing destructive, but new columns should be a deliberate decision).
