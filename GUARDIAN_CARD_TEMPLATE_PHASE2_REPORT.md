# GUARDIAN CARD TEMPLATE MANAGEMENT & SECURITY — PHASE 2 REPORT

| | |
|---|---|
| **Report Version** | 1.0 |
| **Date** | 2026-08-24 |
| **Scope** | Template management: metadata, versioning, audit log, permissions, front/back organization, print-profile groundwork |
| **Explicitly not touched** | `GuardianCardRenderer.cs` staging/pagination, `print.css`, `CardService.cs`, `CaseCardRepository.cs`, the actual `ShowPrintUI`/`PrintToPdfAsync` call sites |

Pre-implementation audit: [GUARDIAN_CARD_TEMPLATE_AUDIT.md](GUARDIAN_CARD_TEMPLATE_AUDIT.md). Two scope decisions were made there with the user before coding: front/back separation stays UI-organization-only (no new data model), and only the PVC print profile is implemented this phase (A4/multi-card-per-sheet deferred, since it would require changing `print.css`).

---

## 1) Database changes (all additive)

```sql
ALTER TABLE TblCardTemplate ADD COLUMN TemplateType TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN Description  TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN IsActive     INTEGER NOT NULL DEFAULT 1;
ALTER TABLE TblCardTemplate ADD COLUMN CreatedBy    TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN ModifiedAt   TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN ModifiedBy   TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN PrintProfile TEXT NOT NULL DEFAULT 'PVC';

CREATE TABLE IF NOT EXISTS TblCardTemplateVersion ( ... FK TemplateID → TblCardTemplate ON DELETE CASCADE ... );
CREATE INDEX IF NOT EXISTS IX_TblCardTemplateVersion_TemplateID ON TblCardTemplateVersion(TemplateID, VersionNumber);
```

Applied via the existing `EnsureColumn`/`CREATE TABLE IF NOT EXISTS` helpers in `Helpers/DatabaseInitializer.cs` — the same idempotent, safe-migration pattern already used for every prior schema change in this project (e.g. `DesignJson`/`LayoutVariant` on the same table).

**Verified against a scratch copy of the real, populated `bin/Debug/CaseDB.sqlite`** (never the live file): the migration ran with zero errors, and the one pre-existing template row came out with exactly the expected safe defaults — `IsActive=1`, `PrintProfile='PVC'`, `TemplateType`/`CreatedBy`/`ModifiedAt` all `NULL` (honestly "unknown" for a pre-existing row, not a guess).

---

## 2) Security changes

Five new permission keys registered in `Enterprise/EnterpriseInitializer.cs` (`AddPermission`, same call site/pattern as the existing `GuardianCard.Print`/`GuardianCard.ManageTemplates`):

| Key | Default (Admin / Operator / Viewer) |
|---|---|
| `GuardianCard.Template.View` | مجاز / مجاز / مجاز |
| `GuardianCard.Template.Create` | مجاز / — / — |
| `GuardianCard.Template.Edit` | مجاز / — / — |
| `GuardianCard.Template.Delete` | مجاز / — / — |
| `GuardianCard.Template.Activate` | مجاز / — / — |

`GuardianCard.ManageTemplates` (the existing form-level gate) is **unchanged** — still required to open `FrmCardTemplateManager` at all. The five new keys are additional, per-action checks (`PermissionService.Require(...)`) wired into `SaveCurrent()` (Create/Edit), `DeleteCurrent()`, `DuplicateCurrent()` (Create), `ToggleActiveState()` (Activate), and `LoadTemplateList()` (View, silent `HasPermission` check — redundant with the form gate today, kept for defense in depth and because it was explicitly requested). SuperAdmin is unaffected (always allowed, per existing `PermissionService` rule). An admin can now grant/restrict these individually via the existing Role Matrix UI — no new permission UI was needed.

---

## 3) Audit logging

Every mutating action now calls the existing `AuditLogger.Log(operation, "CardTemplate", templateId, oldValue, newValue)` (the same helper already used throughout the app, e.g. `FrmCase.cs`, `FrmFamily.cs`) — no changes to `AuditLogger.cs` or `TblAuditLog` were needed:

| Action | Operation logged |
|---|---|
| Create | `"ایجاد"` |
| Edit | `"ویرایش"` |
| Delete | `"حذف"` |
| Duplicate | `"ایجاد"` (with a note identifying the source template) |
| Activate/Deactivate | `"فعال‌سازی"` / `"غیرفعال‌سازی"` |
| Restore version | `"بازگردانی"` |

---

## 4) Template versioning

`CardTemplateRepository.Save(...)` now writes one `TblCardTemplateVersion` snapshot after every successful insert/update, in the same connection — so it can never be skipped by a caller. New methods: `GetVersions`, `GetVersion`, `RestoreVersion` (loads a past snapshot and re-saves it through the normal `Save` path, which itself creates a new version — so restoring never deletes intervening history), `SetActive` (status-only, no version — a toggle isn't a content edit), `Duplicate`.

**Verified end-to-end against the scratch DB** by replaying the exact SQL the repository issues:
- Create → v1; two edits → v2, v3.
- Restore v1 → creates **v4** (content matches v1); v2 and v3 remain in the table, untouched.
- Duplicate → an independent template with its own v1, unaffected by the source's later deletion.
- Deleting a template cascades to its own versions only (`ON DELETE CASCADE`) — a duplicate's versions were confirmed to survive the original's deletion.

## 5) Professional template management (UI)

`FrmCardTemplateManager.cs`, "🧾 اطلاعات کارت" tab: Type (editable combo, pre-seeded with کارت ایتام/مددجو/خانواده/پرسنل, free text allowed), Description (multi-line), Active/Inactive toggle (wired to `SetActive` + `Template.Activate` permission + audit, immediate — not tied to the Save button), read-only Created/Modified (date + by) summary. New "تکثیرِ این قالب" button. New "🕘 تاریخچهٔ نسخه‌ها" tab: version list, Restore (single selection), Compare (exactly two selections — manual property-by-property diff of `Fields`/`Design`/metadata, not a generic diff engine, so no new dependency was introduced).

## 6) Front/back organization

Per the agreed low-risk scope: tab titles now explicitly mark `[روی]` (front: fields, field order, QR/security) vs `[پشت]` (back: payment-ledger months, back-of-card text/notices); shared tabs (Appearance, Logo/Image) are marked "هر دو رو". No data-model or rendering change — still one `CardTemplate` = one `FieldsJson`/`DesignJson` record, as decided.

## 7) Printing profiles

`TblCardTemplate.PrintProfile` (default `'PVC'`) and a read-only label in `FrmGuardianCardBatchPrint` showing the selected template's profile before printing. No new print logic — `print.css`/pagination is untouched, matching the agreed PVC-only scope for this phase.

---

## 8) Tests performed

1. `MSBuild /t:Rebuild` — clean, zero errors (only pre-existing, unrelated `FrmSettings` warnings).
2. Migration replay against a scratch copy of the real populated database — zero errors, existing row got correct safe defaults (see §1).
3. Full CRUD + versioning + restore + duplicate + cascade-delete flow replayed against the same scratch DB, matching the repository's exact SQL — all behaved as designed (see §4).
4. Static verification that none of the new fields (`TemplateType`, `Description`, `IsActive`, `PrintProfile`) are referenced anywhere in `GuardianCardRenderer.cs`, `CardService.cs`, or `CaseCardRepository.cs`, and that the `StageAndPopulate`/`StageAndPopulateBatch`/`ShowPrintUI`/`PrintToPdfAsync` call sites in both print forms are byte-for-byte unchanged from before this phase — proves by construction that rendered/printed output cannot have changed.
5. Permission wiring reviewed against the existing, proven `PermissionService.Require(...)` pattern used elsewhere in this form (`GuardianCard.ManageTemplates` check in the constructor) — same call shape, same denial path (`SecurityAudit.PermissionDenied`).

**Not performed (environment limitation):** interactive WinForms UI automation (click-through of Create/Edit/Duplicate/Restore/Compare in the running app) — no UI-automation tool is available in this environment for a WinForms desktop app. The DB-layer replay in tests 2–3 exercises the *exact* SQL the UI triggers, which is the part of the change with real risk (schema correctness, data integrity, cascade behavior); the UI code itself was verified by full-project compilation and manual code review. Recommend a short manual click-through pass before shipping to end users.

---

## 9) Remaining limitations (by design, per agreed scope)

- Front/back are still one record — not independently selectable/combinable templates. Upgrading to that would require a real data-model and `GuardianCardRenderer` change (flagged as high-risk in the audit; not attempted).
- Only the PVC print profile is functional. A4/multi-card-per-sheet needs `print.css`/pagination changes and was explicitly deferred.
- `IsActive` is not yet enforced anywhere — deactivating a template does **not** currently hide it from the template pickers in `FrmGuardianCardPreview`/`FrmGuardianCardBatchPrint` (only the management UI shows/edits the flag). This was not explicitly required by the task's field list, but is a reasonable next-step follow-up if "Inactive" should mean "unselectable for printing."
- Created/Modified timestamps are shown in Gregorian format (`yyyy/MM/dd`), not converted to Persian/Shamsi via `PersianDateHelper` like case-facing dates elsewhere in the app — acceptable for admin-only audit metadata, but worth aligning if consistency is desired.
- "Compare versions" is a manual, hardcoded property comparison (Fields dictionary + the Design properties most likely to change), not a fully generic reflection-based diff — sufficient for this project's fixed schema, but would need extending by hand if new `CardTemplateDesign` properties are added later.
