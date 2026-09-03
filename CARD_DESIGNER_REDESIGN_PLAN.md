# Enterprise Card Designer — Complete Redesign Plan

**Module:** `CaseManagement/GuardianCardIntegration` · **Target form:** `FrmCardTemplateManager`
**Status:** Design specification — no code changed yet
**Date:** 2026-08-31

---

## 0. AUDIT — WHAT ACTUALLY EXISTS TODAY

Every recommendation in this document is derived from the current code, not from assumptions.

### 0.1 Files and sizes

| File | Lines | Role |
|---|---|---|
| `FrmCardTemplateManager.cs` | **2,129 (122 KB)** | The designer. List + 9 tabs + preview + versioning + import/export in one class. |
| `GuardianCardRenderer.cs` | 1,145 (74 KB) | Stages the frozen HTML package, injects CSS/JS overrides. |
| `CardTemplateRepository.cs` | 736 (47 KB) | Model + SQLite persistence + version snapshots. |
| `CaseCardRepository.cs` | 385 | Reads `TblCase` / `TblFamily`. |
| `CardService.cs` | 235 | `BuildCardData(caseId)` — assembles `GuardianCardData`. |
| `FrmGuardianCardPreview.cs` | 400 | WebView2 preview + `ShowPrintUI`. |
| `FrmGuardianCardBatchPrint.cs` | 647 | Batch printing. |

### 0.2 Current screen layout (`BuildUi`, line 268)

```
┌───────────────────────────────────────────────────────────────┐
│ header  💳 مدیریت قالب‌ها و طراحی کارت شناسایی                │
├──────────────┬──────────────────────────────┬─────────────────┤
│ left = 480px │ middle = Fill                │ right = 260px   │
│ WebView2     │ TabControl — 9 tabs          │ ListBox قالب‌ها  │
│ live preview │ each an AutoScroll panel     │ + search        │
│ FIXED WIDTH  │                              │ + "new" button  │
├──────────────┴──────────────────────────────┴─────────────────┤
│ bottomBar: save / delete / duplicate / activate / export/import│
└───────────────────────────────────────────────────────────────┘
```

### 0.3 The 9 current tabs

| # | Current label | Builder | Contents |
|---|---|---|---|
| 1 | 🧾 اطلاعات کارت | `BuildInfoSection` :461 | Name, type, variant, description, active flag, meta |
| 2 | 📋 [روی] فیلدهای قابل‌نمایش | `BuildFieldsSection` :533 | Owner-drawn CheckedListBox |
| 3 | 🔀 [روی] ترتیبِ فیلدها | `BuildFieldOrderSection` :561 | Two order lists + photo position radios |
| 4 | 🎨 ظاهر کارت (هر دو رو) | `BuildAppearanceSection` :722 | Colors, fonts, scales |
| 5 | 📷 [روی] QR Code و امنیت | `BuildQrSection` :739 | QR / barcode / hologram |
| 6 | 🖼 لوگو و تصویر (هر دو رو) | `BuildLogoImageSection` :752 | Backgrounds, watermark, portrait, family photo |
| 7 | 🖨 [پشت] جدولِ پرداخت | `BuildPrintSettingsSection` :834 | Ledger month checkboxes |
| 8 | 📜 [پشت] متنِ پشتِ کارت و حدیث | `BuildTextOverridesSection` :869 | `TextOverrides` editor |
| 9 | 🕘 تاریخچهٔ نسخه‌ها | `BuildVersionHistorySection` :924 | Version list, restore, compare |

**Diagnosis:** tabs 2/3/5 are all "fields", tabs 4/6 are all "appearance", tabs 7/8 are both "back of card". The `[روی]` / `[پشت]` prefixes are a workaround for a missing organising dimension — the *side of the card* is being encoded in tab titles instead of in the navigation model.

### 0.4 Rendering architecture — THE governing constraint

```
CardTemplateDesign (C# POCO, ~50 flat props + TextOverrides dict)
        │  serialized to DesignJson
        ▼
GuardianCardRenderer.StageAndPopulate()
        │  1. copy frozen GuardianCard/ → %TEMP%\CaseManagement_GuardianCardWork
        │  2. write SAMPLE_DATA.json from GuardianCardData
        │  3. stage images / Code128 barcode / QR
        │  4. INJECT <style> + <script> at fixed anchors in index.html
        ▼
WebView2 + SetVirtualHostNameToFolderMapping("guardiancard.local", workingFolder)
        │
        ▼   https://guardiancard.local/index.html   (or simple.html)
```

Hard rules encoded in the source comments:

1. **Nothing inside `GuardianCard/` is ever modified.** All customisation is CSS/JS injected into a throwaway copy.
2. **Layout is CSS flow, not coordinates.** Reordering works via `[data-order-field="X"]{order:N}` (`ApplyFieldOrderOverrides` :541). There is no positioning model in the template.
3. **Two variants:** `Full` → `index.html`, `Simple` → `simple.html`. They have *different* field sets (`ToggleableFields` vs `ToggleableFieldsSimple`) and *different* orderable sets.

### 0.5 Persistence

```sql
TblCardTemplate(TemplateID, Name, FieldsJson, DesignJson, LayoutVariant,
                TemplateType, Description, IsDefault, IsActive,
                CreatedBy, CreatedAt, ModifiedBy, ModifiedAt, PrintProfile)

TblCardTemplateVersion(VersionID, TemplateID, VersionNumber, Name, FieldsJson,
                       DesignJson, LayoutVariant, TemplateType, Description,
                       ChangedByUsername, ChangedAt, ChangeNote)
```

Every `SaveCurrent()` writes a version snapshot in the same transaction (:337).
Columns are selected **explicitly**, never `SELECT *` (:299) — a deliberate forward-compat choice already made by the existing code.

> **This is the single most important compatibility fact in this document:**
> `DesignJson` is an open JSON blob deserialised by `JavaScriptSerializer` into `CardTemplateDesign`.
> Unknown JSON keys are ignored on read; new C# properties get their default values on old JSON.
> **Therefore the entire redesign can add design capability with ZERO schema migration.**

### 0.6 Data source for the mapping inspector

`CardService.BuildCardData(caseId)` → `GuardianCardData` (69 printable fields), fed by:

- `TblCase` — `SELECT * FROM TblCase WHERE CasID=@CasID` (`CaseCardRepository.GetCase` :72)
- `TblFamily` — members / orphan counts (`MemberRole = 'یتیم'`), `GetOrphans` :219
- `TblCase.CardNotice1..5` — per-case notice overrides
- App settings — organisation name, branch, logo, contact block

### 0.7 Concrete usability defects found in code

| # | Defect | Evidence |
|---|---|---|
| D1 | Preview picks an **arbitrary** case — operator cannot choose whose card they see | `FindAnyAccessibleCaseId()` in `RefreshPreviewNowAsync` :1387 |
| D2 | Preview panel is **hard-coded 480 px**, no splitter, no zoom, no full-screen | `new Panel { Dock = Left, Width = 480 }` :384 |
| D3 | **Full re-stage on every keystroke** (500 ms debounce) — copies the whole package, rewrites HTML, renavigates | `_previewTimer.Interval = 500` :421 → `StageAndPopulate` |
| D4 | **No undo/redo anywhere** — no command stack exists in the form | — |
| D5 | **No validation before save** — `SaveCurrent()` writes whatever is in the controls | :1760 |
| D6 | **No mapping visibility** — nothing tells the operator a field maps to `TblCase.RequestType` | — |
| D7 | Field list is a flat `CheckedListBox` — no search, no grouping, no bulk operations | `PopulateFieldChecklist` :1529 |
| D8 | Version compare is a **text dialog**, not visual | `CompareSelectedVersions` :1025 |
| D9 | Field-order tab admits one of its lists is decorative — "ترتیبِ این لیست فقط برای سازمان‌دهیِ شماست" | :537 |
| D10 | Save/delete/duplicate live in a bottom bar far from the tabs being edited — long mouse travel every iteration | :340 |

---

## 1. THE ONE ARCHITECTURAL DECISION THAT MUST BE MADE FIRST

### Phase 2 as literally written is not buildable against the frozen renderer

The brief asks for drag & drop with resize handles, snap-to-grid and smart guides. That implies an absolute-coordinate layout model. The renderer has none — it drives a CSS flexbox document, and modifying `GuardianCard/` is forbidden by the project's own stated rule and would invalidate every existing template and print calibration.

**Recommendation: deliver direct manipulation in three levels. Ship L1+L2; make L3 opt-in.**

| Level | What the operator does | What it writes | Compatibility |
|---|---|---|---|
| **L1 — Visual Reorder** (ship first) | Drags a field block on a real-scale card canvas; blocks snap into the flow slots that actually exist | existing `FieldOrderCsv`, `SecurityBandOrderCsv`, `PhotoPosition` | **100% — existing keys, existing renderer path** |
| **L2 — Direct Manipulation of existing knobs** | Grabs a resize handle on the portrait / family photo / header; drags a font-size handle on a text block | existing `PortraitScalePercent`, `HeaderHeightScalePercent`, `FamilyPhotoScalePercent`, `FontScalePercent`, `TextOverrides[x].FontSizePercent` | **100% — no new keys at all** |
| **L3 — Free Layout (opt-in per template)** | True X/Y drag, resize, snap-to-grid, smart guides | **new** `Design.LayoutMode = "Free"` + `Design.Elements[]` (id, x, y, w, h, z in mm) | Additive JSON. Old templates deserialise with `LayoutMode = "Flow"` and are byte-identical in output. |

L3 is rendered by one additional injected stylesheet that sets `position:absolute` on the named elements inside the existing card frame — still zero edits to `GuardianCard/`. It is **off by default**, gated behind a per-template switch with an explicit warning, and a one-click "بازگشت به چیدمان استاندارد" that clears `Elements[]` and restores flow.

This is the honest answer: the operator gets a genuinely visual, direct-manipulation designer immediately, without a single existing card changing by one pixel, and free positioning arrives as a deliberate, reversible opt-in.

---

## 2. DELIVERABLE 1 — COMPLETE UX REDESIGN PLAN

### 2.1 Design principles

1. **The card is the interface.** The canvas is the primary surface; panels serve it. Today the canvas is 480 px in a corner.
2. **Select an object → see its settings.** Replace "find the tab that owns this setting" with "click the thing, edit the thing". This single change removes most tab-switching.
3. **Front/back is a view state, not a tab-name prefix.** Kill every `[روی]` / `[پشت]` prefix; add a front/back toggle above the canvas that filters both canvas and inspector.
4. **Nothing is hidden more than two clicks deep.**
5. **Every destructive or ambiguous action is previewed before commit.**
6. **RTL is the native direction, not a flag.** Primary navigation lives on the right; reading flow is right → left.
7. **Progressive disclosure:** Simple mode (7 essential controls) → Advanced mode (everything). Persisted per user.

### 2.2 The workflow the redesign optimises

```
choose template → choose a real beneficiary → see the card → click something on the card
      → change it → see it change → validate → save (versioned) → print test
```

Today that loop requires: select template (right), hunt tab (centre), change control (centre), wait 500 ms, squint at 480 px (left), travel to the bottom bar to save. The redesign keeps eyes and mouse in one zone.

---

## 3. DELIVERABLE 2 — NEW NAVIGATION STRUCTURE

Seven sections as specified in the brief, implemented as a **right-hand vertical rail** (RTL-native) instead of a horizontal `TabControl`. Vertical rails hold full Persian labels without truncation, scale to more sections, and support keyboard `Ctrl+1..7`.

| # | Persian label | Icon | Absorbs from today | Keyboard |
|---|---|---|---|---|
| 1 | **اطلاعات قالب** | 🧾 | Tab 1 + Duplicate/Restore/Export/Import from the bottom bar | `Ctrl+1` |
| 2 | **طراحی و ظاهر** | 🎨 | Tab 4 + header/footer/background parts of Tab 6 | `Ctrl+2` |
| 3 | **فیلدها و محتوا** | 📋 | Tabs 2 + 3 merged into one field manager | `Ctrl+3` |
| 4 | **تصاویر و رسانه‌ها** | 🖼 | Tab 6 (logo, watermark, portrait, family photo, placeholders) | `Ctrl+4` |
| 5 | **پشت کارت** | 📜 | Tabs 7 + 8 (ledger + back text + hadith + notes) | `Ctrl+5` |
| 6 | **امنیت و اعتبارسنجی** | 🔐 | Tab 5 + signature/stamp + new validation report | `Ctrl+6` |
| 7 | **نسخه‌ها و بازبینی** | 🕘 | Tab 9 + new visual diff + audit trail | `Ctrl+7` |

**Complete traceability — every current control keeps a home:**

| Current control | New home |
|---|---|
| `_txtName`, `_cmbTemplateType`, `_txtDescription`, `_chkIsActive`, `_lblMetaInfo` | 1 · اطلاعات قالب |
| `LayoutVariant` (Full/Simple) radios | 1 · اطلاعات قالب — promoted to a **card-type chooser with thumbnails** |
| `_cmbFont`, `PrimaryColor`, `SecondaryColor`, `FontScalePercent`, `HeaderBackgroundColor`, `HeaderHeightScalePercent` | 2 · طراحی و ظاهر |
| `_chkFields` checklist | 3 · فیلدها و محتوا — becomes a grouped, searchable grid |
| `FieldOrderCsv`, `SecurityBandOrderCsv`, `PhotoPosition` | 3 · فیلدها و محتوا — **and** direct drag on the canvas |
| `_txtBgFront`, `_txtBgBack`, `_txtWatermark`, `WatermarkOpacityPercent`, `PortraitScalePercent`, `PortraitBlank`, `FamilyPhoto*` | 4 · تصاویر و رسانه‌ها |
| `LedgerMonthsCsv` month checkboxes | 5 · پشت کارت |
| `TextOverrides` editor (`TextOverrideFieldKeys` :135) | 5 · پشت کارت (back-side keys) + 2 · طراحی (front-side keys), filtered by the front/back toggle |
| `_chkQRCode`, `_chkBarcode`, `_chkHologram` | 6 · امنیت و اعتبارسنجی |
| Version list, restore, compare | 7 · نسخه‌ها و بازبینی |
| Save / Delete / Duplicate / Toggle-active / Export / Import | **Command bar at top**, always visible — no longer buried at the bottom |

---

## 4. DELIVERABLE 3 — NEW SCREEN LAYOUTS

### 4.1 Master layout (RTL — right is primary)

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║ COMMAND BAR                                                                   ║
║ [ذخیره ▾] [پیش‌نمایش چاپ] [اعتبارسنجی ●۲] │ ↶ ↷ │ ⚙ ساده/پیشرفته │ ✕        ║
╠═════════════╤═══════════════════════════════════════════════╤═══════════════╤═╣
║ INSPECTOR   │                CANVAS                         │  TEMPLATES    │▐║
║ (left,      │                                               │  (right,      │R║
║  360px,     │  ┌─ view bar ───────────────────────────────┐ │   280px,      │A║
║  splitter)  │  │ [روی کارت][پشت کارت] │ 🔍 100% ▾ │ ⛶     │ │  collapsible) │I║
║             │  │ نمونه: [احمد رضایی ▾] [دادهٔ نمونه]       │ │               │L║
║ contextual  │  └──────────────────────────────────────────┘ │  🔍 جستجو     │ ║
║ to the      │                                               │  ┌──────────┐ │1║
║ selected    │      ┌──────────────────────────────┐         │  │قالب اصلی │ │2║
║ object OR   │      │                              │         │  │ ★ پیش‌فرض│ │3║
║ the active  │      │     LIVE CARD (WebView2)     │         │  └──────────┘ │4║
║ section     │      │     selection overlay on top │         │  ┌──────────┐ │5║
║             │      │                              │         │  │کارت ساده │ │6║
║             │      └──────────────────────────────┘         │  └──────────┘ │7║
║             │                                               │ [+ قالب جدید] │ ║
╠═════════════╧═══════════════════════════════════════════════╧═══════════════╧═╣
║ STATUS BAR: ✓ ذخیره شد · نسخه ۱۲ · آخرین تغییر: مریم ۱۴۰۵/۰۶/۰۹ · ⚠ ۱ هشدار  ║
╚═══════════════════════════════════════════════════════════════════════════════╝
```

Splitters are user-draggable and persisted per user. Canvas gets all remaining space (≈ 900–1100 px on a 1600 px screen vs. today's 480 px — **a 2× increase**).

### 4.2 Selection model — the core interaction

Clicking an element on the card selects it. The inspector becomes that element's property sheet.

Implementation without touching `GuardianCard/`: the renderer already injects a `<script>` at a fixed anchor. Add one **designer-only** injected script (only when `designerMode=true`) that:

1. attaches `click` handlers to every `[data-field]` element,
2. draws a selection outline + drag affordance in an overlay div,
3. posts `{type:"select", field:"GuardianName"}` to C# via `WebMessageReceived`,
4. receives `{type:"highlight"|"applyOrder"|"applyScale"}` back from C#.

The frozen files are untouched — this is exactly the same injection mechanism `ApplyDesignOverrides` (:293) already uses.

### 4.3 Inspector — object-selected state

```
┌ ویژگی‌های «نام سرپرست» ───────────────── ✕ ┐
│ 🔗 منبع: TblCase.CaseName            [بررسی]│
├─────────────────────────────────────────────┤
│ ☑ نمایش روی کارت                            │
│ اندازهٔ متن     [───●────] ۱۰۰٪   [بازنشانی]│
│ رنگ            [■ #25313F]                  │
│ ضخامت          (۴۰۰) (۵۰۰) (۶۰۰) (۷۰۰)     │
│ چینش           (راست) (وسط) (چپ)            │
│ جایگاه در ستون [▲ بالاتر] [▼ پایین‌تر]  ۲/۵ │
├─────────────────────────────────────────────┤
│ ⓘ این فیلد روی قالب «ساده» وجود ندارد.      │
└─────────────────────────────────────────────┘
```

Every control here maps to a property that **already exists** in `CardTemplateDesign` / `TextFieldOverride`.

---

## 5. DELIVERABLE 4 — WIREFRAME DESCRIPTIONS

### W1 · اطلاعات قالب

Two-column form. Right column: name, type combo, status pill (فعال/غیرفعال/پیش‌فرض), description. Left column: a **card-type chooser** — two large thumbnails ("کارت کامل" / "کارت ساده") rendering an actual miniature, replacing today's radio buttons. Below: an action strip — تکثیر قالب · خروجی JSON · ورودی JSON · بازگردانی به پیش‌فرض. Footer strip: created-by / modified-by / version count (from `BuildMetaInfoText` :1614).

### W2 · طراحی و ظاهر

Accordion, all open by default in Advanced, first two open in Simple:

- **تم آماده** — 4–6 preset swatch cards (سازمانی، آبی، سبز، خنثی، پرکنتراست). One click sets primary + secondary + header colour + font. *New capability, stored entirely in existing colour keys.*
- **رنگ‌ها** — the existing swatch+hex control (`SetSwatchColor` :1223) with a live contrast readout: "کنتراست متن روی سربرگ: ۷.۲:۱ ✓ AAA".
- **قلم و اندازه** — font combo + a size slider previewing on the canvas live.
- **سربرگ و پابرگ** — header background, header height scale.
- **پس‌زمینهٔ کارت** — front/back background images.

### W3 · فیلدها و محتوا

See §8 (Phase 5).

### W4 · تصاویر و رسانه‌ها

Grid of **media slots**, each a drop target with a thumbnail, not a textbox+browse row:

```
┌ لوگوی سازمان ┐ ┌ واترمارک  ┐ ┌ پس‌زمینهٔ رو ┐ ┌ پس‌زمینهٔ پشت ┐
│  [thumb]     │ │ [thumb]   │ │  [thumb]     │ │  [thumb]      │
│ تعویض ✕ حذف  │ │ شفافیت    │ │ تعویض ✕      │ │ تعویض ✕       │
│              │ │ [──●──]۱۵٪│ │              │ │               │
└──────────────┘ └───────────┘ └──────────────┘ └───────────────┘
```

Missing files show a red "فایل پیدا نشد" badge instead of failing silently at print time. Portrait and family-photo controls (scale, aspect ratio, fit-contain, blank) sit in a second row with the same slot metaphor.

### W5 · پشت کارت

Left: a **live back-of-card mini preview**. Right: month grid for the ledger (12 toggle chips + "همه/هیچ‌کدام"), then the text-override editor with a proper list of back-side keys (حدیث، پیام شکایت، پیام کارت پیداشده، خط قانونی) — each row showing its current value inline instead of requiring select-then-edit.

### W6 · امنیت و اعتبارسنجی

Top: security-band **visual strip** showing the 5 cells (QR · بارکد · امضا · مهر · هولوگرام) in their real print order, drag to reorder → writes `SecurityBandOrderCsv`. Each cell has an on/off switch. Bottom: the validation report panel (Phase 7).

### W7 · نسخه‌ها و بازبینی

Timeline list (newest first) — each row: version number, who, when, change summary. Selecting two enables **مقایسهٔ تصویری**: side-by-side rendered thumbnails + a changed-properties table with before/after. Restore is a two-step confirm stating "نسخهٔ فعلی به‌عنوان نسخهٔ N+1 حفظ می‌شود" — because `RestoreSelectedVersion` (:992) already saves forward rather than destroying history.

---

## 6. PHASE 3 — LIVE PREVIEW EXPERIENCE

| Requirement | Implementation |
|---|---|
| Large preview panel | Canvas is `Dock=Fill` between two splitters — 2× today's width |
| Adjustable splitter | `SplitContainer` ×2, positions persisted per user |
| Full-screen preview | `F11` → canvas fills the form, panels collapse; `Esc` restores |
| Real-time updates | See §6.1 — two-tier refresh |
| Zoom 25–200% | `_webView.ZoomFactor` — a WebView2 property that scales rendering only |
| Fit Width / Height / Screen | Compute zoom from card mm dimensions ÷ viewport px |

**Print safety (non-negotiable):** zoom uses `CoreWebView2Controller.ZoomFactor`, which affects the on-screen raster only. Print goes through `ShowPrintUI` against `print.css`, whose dimensions are in `mm`. Zoom **cannot** reach print output. A regression test must assert this: render at 25% and 200%, print to PDF, compare page geometry byte-for-byte.

### 6.1 Fixing the slow preview (defect D3)

Today every change triggers a full re-stage: directory copy → JSON write → image staging → HTML rewrite → navigate. Replace with a two-tier model:

- **Tier A — hot path (≈95% of edits).** Colours, fonts, sizes, scales, field on/off, order, text overrides → send a JSON patch over `PostWebMessageAsJson`; the injected designer script updates CSS custom properties and `order` values in place. **No navigation, no file I/O. Sub-50 ms, and no scroll-position loss.**
- **Tier B — cold path.** Only when a *file* changes (background, watermark, logo, photo) or the record/variant changes → full `StageAndPopulate` as today.

Debounce drops 500 ms → 120 ms for Tier A. This alone changes the subjective feel of the tool more than any other single change.

---

## 7. PHASE 4 — REAL DATA PREVIEW

Replace `FindAnyAccessibleCaseId()` (D1) with an explicit record selector in the canvas view bar:

```
نمونه: ( ● دادهٔ نمونه   ○ پروندهٔ واقعی )   [جستجوی مددجو… ▾]   [◀ قبلی] [بعدی ▶]
```

- **دادهٔ نمونه** — a static `GuardianCardData` with realistic Persian values and worst-case lengths (longest plausible name, 12 family members, 12 ledger rows). Guarantees a preview even on an empty database, and makes overflow bugs visible before print.
- **پروندهٔ واقعی** — a typeahead over `TblCase` honouring the operator's centre/permission scope (reusing the existing `CaseCardRepository` filtered query at :144, which already handles role and orphan filters). Prev/Next step through the result set so an operator can flip through 5 real records and confirm the template survives all of them.
- Selection persists per user, so reopening the designer restores the same reference record.
- Switching record is a **Tier B** refresh; everything else stays Tier A.

---

## 8. PHASE 5 — FIELD MANAGEMENT SYSTEM

Replace the flat `CheckedListBox` (D7) with a grouped `DataGridView`:

```
┌ فیلدها و محتوا ─────────────────────────────────────────────────────┐
│ 🔍 [جستجوی فیلد…]   نمایش: (همه) (فعال) (غیرفعال) (بدون نگاشت)      │
│ [✓ فعال‌سازی گروهی] [✗ غیرفعال‌سازی گروهی]           ۲۴ از ۶۹ فعال │
├──────────────────────┬─────────────┬──────────────┬──────┬──────────┤
│ نام فیلد             │ جدول منبع   │ ستون منبع    │ نوع  │ وضعیت    │
├──────────────────────┴─────────────┴──────────────┴──────┴──────────┤
│ ▼ اطلاعات فردی (۸)                                   [✓ همه] [✗ هیچ]│
│  ☑ نام سرپرست         TblCase      CaseName          متن    ✓ سالم  │
│  ☑ نام پدر            TblCase      FatherName        متن    ✓ سالم  │
│  ☑ شماره تذکره        TblCase      NationalID        متن    ✓ سالم  │
│  ☐ نوع مددجو          TblCase      RequestType       متن    ✓ سالم  │
│ ▼ اطلاعات خانواده (۵)                                               │
│  ☑ تعداد ایتام        TblFamily    MemberRole='یتیم' شمارش  ✓ سالم  │
│  ☑ فهرست اعضا         TblFamily    (چند ردیف)        لیست   ✓ سالم  │
│ ▼ اطلاعات سازمان (۹)                                                │
│  ☑ نام سازمان         تنظیمات      OrganizationName  متن    ✓ سالم  │
│ ▼ اطلاعات امنیتی (۵)                                                │
│  ☑ بارکد              محاسبه‌شده   CardNumber        تصویر  ✓ سالم  │
│  ☐ QR Code            محاسبه‌شده   —                 تصویر  ✓ سالم  │
│ ▼ متن‌های ثابت (۱۲)                                                 │
└─────────────────────────────────────────────────────────────────────┘
```

**Categories** (derived from `GuardianCardData` + `FIELD_MAPPING.md`):
اطلاعات فردی · اطلاعات سرپرست · اطلاعات خانواده · اطلاعات سازمان · موقعیت مکانی · اطلاعات امنیتی · متن‌های ثابت · جدول پرداخت

Behaviours: search filters across name/table/column; group headers have bulk ✓/✗; multi-select + `Space` toggles all selected; a row that is off in the current `LayoutVariant` is greyed with a tooltip explaining which variant it belongs to; clicking a row **highlights that element on the canvas** (and vice-versa).

**Field catalogue** — a new static `CardFieldCatalog` class holding, per field key: Persian label, category, source table, source column, data type, which variants it exists in, whether it is toggleable, whether it is orderable. This is the single source of truth that Phases 5, 6 and 7 all read. It is a **read-only in-memory table**, not a database object.

---

## 9. PHASE 6 — DATABASE MAPPING INSPECTOR

A dedicated dialog (opened from section 3 or 6, `Ctrl+M`):

```
┌ بازرس نگاشت پایگاه داده ────────────────────────────────────┐
│ [🔄 بررسی مجدد]  [📄 خروجی گزارش]   آخرین بررسی: ۱۰:۴۲     │
│ ✓ ۶۴ سالم   ⚠ ۳ هشدار   ✕ ۲ خطا                             │
├────────────┬──────────┬───────────┬────────┬────────────────┤
│ فیلد       │ جدول     │ ستون      │ نوع    │ وضعیت          │
├────────────┼──────────┼───────────┼────────┼────────────────┤
│ نام سرپرست │ TblCase  │ CaseName  │ TEXT   │ ✓ سالم         │
│ کد پستی    │ TblCase  │ PostCode  │ —      │ ✕ ستون نیست    │
│ ولایت      │ TblCase  │ Province  │ TEXT   │ ⚠ ۱۰۰٪ خالی    │
└────────────┴──────────┴───────────┴────────┴────────────────┘
```

Detection runs entirely on SQLite introspection — **read-only, no writes ever**:

```sql
PRAGMA table_info(TblCase);                 -- existence + declared type
SELECT COUNT(*), COUNT(col) FROM TblCase;   -- emptiness / coverage ratio
```

| Condition | Detection | Severity |
|---|---|---|
| Missing column | key absent from `PRAGMA table_info` | ✕ خطا |
| Missing table | absent from `sqlite_master` | ✕ خطا |
| Renamed column | missing, but a Levenshtein-near name exists → suggest it | ⚠ هشدار |
| Type mismatch | declared type ≠ expected | ⚠ هشدار |
| Always empty | non-null count = 0 across all rows | ⚠ هشدار |
| Enabled field with broken mapping | field is ✓ in template **and** mapping is ✕ | ✕ خطا — blocks publish |

**Export** produces a CSV/HTML report with template name, timestamp, operator and every row — usable as an audit attachment. CSV avoids the ClosedXML binding issue that affects the test host.

---

## 10. PHASE 7 — VALIDATION ENGINE

`CardTemplateValidator` — a pure, side-effect-free class returning `List<ValidationIssue>`, so it is trivially unit-testable and callable from the batch-print path too.

| Rule | Severity | Message (Persian) |
|---|---|---|
| Template name empty / duplicate | خطا | «نام قالب الزامی است» |
| Enabled field with broken DB mapping | خطا | «فیلد X فعال است ولی ستون آن در پایگاه داده وجود ندارد» |
| Referenced image file missing | خطا | «فایل واترمارک پیدا نشد: …» |
| QR enabled with no content source | خطا | «QR فعال است ولی محتوایی برایش تعریف نشده» |
| `LayoutMode="Free"` element outside card bounds | خطا | «عنصر X بیرون از محدودهٔ کارت است» |
| `LayoutMode="Free"` overlapping elements | هشدار | «دو عنصر روی هم افتاده‌اند» |
| Text/background contrast < 4.5:1 | هشدار | «کنتراست کم — روی چاپ خوانا نخواهد بود» |
| Font scale > 130% on a long field | هشدار | «احتمال سرریز متن روی کارت چاپی» |
| Ledger with zero months selected | هشدار | «جدول پرداخت خالی چاپ می‌شود» |
| No security element enabled | پیشنهاد | «هیچ عنصر امنیتی فعال نیست» |
| Field enabled but not present in this variant | پیشنهاد | «این فیلد روی قالب ساده اثری ندارد» |

**Gate:** خطا blocks *publish* (setting default/active) but never blocks *save* — an operator must always be able to save work in progress. This distinction matters: blocking save loses work and trains people to fear the tool.

Presentation: a persistent badge in the command bar (`اعتبارسنجی ●۲`), a slide-in panel listing issues grouped by severity, each row clickable to jump to the offending control **and** highlight the element on the canvas.

---

## 11. PHASE 8 — VERSION MANAGEMENT

The version table already stores everything needed (`ChangedByUsername`, `ChangedAt`, full `DesignJson` + `FieldsJson` snapshots). What is missing is presentation.

- **Change summary on save.** An optional note field in the save dropdown → existing `ChangeNote` column. If left blank, auto-generate one by diffing against the previous snapshot: «۳ تغییر: رنگ اصلی، اندازهٔ قلم، فیلد «نوع مددجو» فعال شد».
- **Visual compare.** Replace the text dialog (:1025) with side-by-side rendered cards. Render both versions headlessly into two staged folders, capture each, show them side by side with synchronized zoom, plus the property-diff table below (before → after, with colour swatches shown as swatches, not hex strings).
- **Restore.** Keeps the current forward-saving behaviour; the confirm dialog states explicitly that nothing is lost.
- **Audit trail.** A flat chronological list across all templates: who / what / when, filterable by user and date, exportable. Reads only `TblCardTemplateVersion` — no new storage.

---

## 12. PHASE 9 — ACCESSIBILITY

| Requirement | Implementation |
|---|---|
| RTL-first | `RightToLeft.Yes` + `RightToLeftLayout` on the form; primary rail on the right; tab order right→left; canvas view bar mirrored |
| Keyboard navigation | Full: `Ctrl+1..7` sections · `Ctrl+S` save · `Ctrl+Z/Y` undo/redo · `F5` refresh preview · `F11` full-screen · `Ctrl+M` mapping inspector · `Ctrl+F` field search · arrows nudge selection · `Tab` cycles canvas elements · `Space` toggles selected field · `Esc` deselects. **No mouse-only operation anywhere.** |
| Focus visibility | 2 px high-contrast focus ring on every custom-painted control (today's owner-drawn list items have none) |
| High contrast mode | New `UiTheme.ApplyHighContrast()` alongside the existing `ApplyFullPalette` (:57) — a palette swap, not a second UI. Also injects a high-contrast CSS variable set into the preview. Respects `SystemInformation.HighContrast` on startup. |
| Large font mode | `UiTheme.SizeScale` (:180) already exists but is not exposed. Surface as a 100/125/150% control in the command bar's ⚙ menu; all layout uses `Dock`/`TableLayoutPanel` so it reflows rather than clipping. |
| Screen reader | `AccessibleName` / `AccessibleDescription` on every control; the canvas exposes an accessible tree listing card elements — the visual canvas must never be the *only* way to reorder fields, which is why section 3's list view retains ▲▼ buttons. |
| Colour independence | Status never encoded by colour alone: ✓ / ⚠ / ✕ glyphs accompany every state. |
| Motion | No animation on preview refresh; respects reduced-motion. |

---

## 13. PHASE 10 — UX POLISH: MEASURED IMPROVEMENTS

| Task | Today | After | How |
|---|---|---|---|
| Change the guardian-name font size | 4 clicks + 2 tab switches | **1 click + 1 drag** | Click the name on the canvas → slider in inspector |
| Turn off a field | Find tab 2, scan a flat list of 40 | **type 3 letters + Space** | Field search |
| Reorder two fields | Tab 3, understand two lists, one decorative | **1 drag on the canvas** | L1 visual reorder |
| See a real beneficiary's card | Impossible | **2 clicks** | Record selector |
| Save | Travel to the bottom bar | **`Ctrl+S`** | Command bar + shortcut |
| Know a template is broken | Discover at print time | **Always-visible badge** | Validation engine |
| Undo a mistake | Reload template, lose everything | **`Ctrl+Z`** | Command stack |
| Compare two versions | Read a text dialog | **Side-by-side images** | Visual diff |

**Undo/redo (D4).** All edits route through an `ICardEditCommand { Apply(); Revert(); Describe(); }` stack — a `CollectDesign()` snapshot before/after each logical edit, coalescing rapid slider moves into one entry. Depth 50. This is the single most requested feature by non-technical users, because it removes the fear that makes them avoid experimenting.

---

## 14. DELIVERABLE 5 — MIGRATION STRATEGY

### 14.1 Data migration: **none required**

| Concern | Resolution |
|---|---|
| Existing `DesignJson` | Deserialises unchanged. New properties take C# defaults. |
| New properties written by the new UI | Serialise as extra keys. **The old form ignores them** (`JavaScriptSerializer` skips unknown members) — so even a rollback to the old build cannot corrupt data. |
| Existing `FieldsJson` | Untouched — same keys, same semantics. |
| Existing version snapshots | Readable as-is; new fields default. |
| `IsDefault` template | Never deleted (`DELETE … AND IsDefault = 0`, :517) — behaviour preserved. |

### 14.2 New JSON keys (all optional, all defaulted)

```csharp
// CardTemplateDesign — additive only, never remove or rename an existing property
public string LayoutMode { get; set; } = "Flow";        // "Flow" | "Free"
public List<CardElementLayout> Elements { get; set; }   // null/empty ⇒ pure flow
public string ThemePresetId  { get; set; } = "";        // "" ⇒ custom colours (today)
public string DesignerNotes  { get; set; } = "";        // internal, never printed
```

`LayoutMode = "Flow"` + empty `Elements` reproduces today's rendering path **exactly** — the free-layout stylesheet is only injected when `LayoutMode == "Free"`.

### 14.3 UI migration for operators

1. **Ship behind a toggle.** A "طراح جدید" button opens the redesigned form; the old form stays reachable for one release cycle. Preference remembered per user.
2. **First-run tour.** 5 coach marks: canvas · inspector · record selector · validation badge · save. Dismissible, replayable from ⚙.
3. **A "کجا رفت؟" map** in the ⚙ menu: a searchable table of old-tab → new-section for every setting, so an experienced operator is never lost.
4. **Remove the old form** only after one full release with zero fallbacks recorded.

### 14.4 Rollback plan

Because no schema changes and no destructive JSON rewrites occur, rollback is: ship the previous binary. Templates saved by the new designer remain fully loadable by the old one — losing only the new optional keys' effects, never the template.

---

## 15. DELIVERABLE 6 — DATABASE COMPATIBILITY STRATEGY

| Rule | Enforcement |
|---|---|
| No `ALTER TABLE`, no new tables, no dropped columns | Nothing in this plan requires one |
| Keep explicit column lists (never `SELECT *`) | Extend the existing `SelectColumns` constant pattern (:299) |
| Mapping inspector is strictly read-only | Only `PRAGMA table_info`, `sqlite_master`, `COUNT` |
| Version snapshots stay in the save transaction | Preserve `SaveCurrent` → `InsertVersion` atomicity (:337) |
| Multi-centre scope respected | Record selector reuses the existing filtered case query (:144), which already enforces role/scope — never a raw `SELECT * FROM TblCase` |
| Backup before any release | Verify `TblCardTemplate` + `TblCardTemplateVersion` are both inside the covered backup table set before shipping |

**Optional, deferred, non-blocking:** if audit-trail queries across thousands of versions become slow, add

```sql
CREATE INDEX IF NOT EXISTS IX_CardTplVer_Tpl
  ON TblCardTemplateVersion(TemplateID, VersionNumber DESC);
```

An index is not a schema change to data, is safely re-runnable, and can be skipped entirely.

---

## 16. DELIVERABLE 7 — IMPLEMENTATION PLAN

The 2,129-line God-form is decomposed first. Every stage ships independently and is independently revertible.

### Stage 0 — Safety net (0.5 week)

- Commit current state; verify git coverage includes this module.
- **Golden-render regression harness:** render all existing templates × both variants to PDF, hash the geometry. **Every later stage must reproduce these hashes.** This is the contract that guarantees "no existing template breaks".
- Unit tests for `CardTemplateRepository` round-trip: old JSON → model → new JSON → model.

### Stage 1 — Decomposition, zero behaviour change (1.5 weeks)

```
CardDesigner/
  FrmCardDesigner.cs              — shell: command bar, splitters, section rail (~300 lines)
  Sections/SecTemplateInfo.cs     — UserControl per section (7 files, 150–350 lines each)
  Canvas/CardCanvasHost.cs        — WebView2 host, zoom, fit, full-screen, selection bridge
  Canvas/DesignerBridge.cs        — WebMessage protocol C# ⇄ injected script
  Model/CardFieldCatalog.cs       — field metadata single source of truth
  Model/CardEditCommand.cs        — undo/redo stack
  Validation/CardTemplateValidator.cs
  Validation/MappingInspector.cs
```

Each section is a `UserControl` with `LoadFrom(CardTemplate)` / `SaveInto(CardTemplate)` — testable without a form. **Golden renders must still match.**

### Stage 2 — Shell + navigation + preview (1.5 weeks)

New layout, 7 sections, splitters, zoom, fit modes, full-screen, record selector, sample data, Tier A/B refresh. *Ships as the visible "new designer" behind the toggle.*

### Stage 3 — Field management + mapping inspector + validation (2 weeks)

`CardFieldCatalog`, grouped field grid, search/bulk, mapping inspector, validator, report export.

### Stage 4 — Direct manipulation L1 + L2 (2 weeks)

Designer-mode injected script, selection overlay, click-to-select, drag-to-reorder writing existing CSV keys, resize handles writing existing percent keys. Undo/redo wired through everything.

### Stage 5 — Versioning + accessibility polish (1.5 weeks)

Visual diff, change summaries, audit trail, high-contrast, large-font, full keyboard map, accessible names, screen-reader canvas tree.

### Stage 6 — Free Layout (L3), opt-in (2 weeks, gated)

`LayoutMode="Free"`, `Elements[]`, absolute-position injected stylesheet, snap-to-grid, smart guides, multi-select, copy/paste/duplicate, keyboard nudge. Ships **off by default** with a prominent revert. Start only after Stages 1–5 are in production and the golden renders have held.

**Total: ~11 weeks.** Stages 1–3 alone (5 weeks) already resolve D1, D2, D3, D5, D6, D7 and D10 — most of the measured pain.

---

## 17. DELIVERABLE 8 — PRODUCTION-READY ARCHITECTURE

```
┌─────────────────────────── Presentation ──────────────────────────┐
│ FrmCardDesigner (shell)                                           │
│   ├ CommandBar        ├ SectionRail       ├ StatusBar             │
│   ├ CardCanvasHost ──── DesignerBridge ──── WebView2              │
│   └ Sections/ (7 UserControls, each Load/Save against the model)  │
└───────────────────────────────┬───────────────────────────────────┘
                                │ operates on CardTemplate (POCO)
┌───────────────────────────────▼───────────────────────────────────┐
│                          Application                              │
│  CardEditCommandStack (undo/redo)   CardTemplateValidator         │
│  CardFieldCatalog (metadata)        MappingInspector (read-only)  │
│  PreviewCoordinator (Tier A patch / Tier B restage)               │
└───────────────────────────────┬───────────────────────────────────┘
┌───────────────────────────────▼───────────────────────────────────┐
│                    Domain / Existing (UNCHANGED)                  │
│  CardTemplate · CardTemplateDesign · TextFieldOverride            │
│  GuardianCardData · PaymentLedgerRow · OrphanRow                  │
└───────────────────────────────┬───────────────────────────────────┘
┌───────────────────────────────▼───────────────────────────────────┐
│                 Infrastructure (UNCHANGED)                        │
│  CardTemplateRepository · CaseCardRepository · CardService        │
│  GuardianCardRenderer ──► frozen GuardianCard/ package            │
└───────────────────────────────────────────────────────────────────┘
```

**Invariants:**

- The **only** new coupling to the renderer is one designer-mode injected script, added through the existing anchor-replacement mechanism. `GuardianCard/` remains byte-identical.
- Sections never talk to the repository. They read/write `CardTemplate` only. This is what makes them unit-testable and keeps save/version semantics in one place.
- The validator and mapping inspector are pure and reusable — batch print can call the same validator before a 500-card run, which is where a broken template is most expensive.
- Preview patching is one-directional and idempotent: any Tier A patch can be replaced by a Tier B restage with identical results. A "پیش‌نمایش کامل" button forces Tier B if an operator ever distrusts the fast path.

**Threading:** all rendering work (`StageAndPopulate` does file I/O) moves off the UI thread; WebView2 calls marshal back. Today's `async void` preview refresh gains a cancellation token so rapid edits cancel in-flight stages instead of queueing.

---

## 18. DELIVERABLE 9 — RISK ANALYSIS

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | A redesign silently changes printed output | Medium | **Critical** — cards are legal identity documents | Golden-render hash suite in Stage 0; every stage must reproduce it. No stage merges without it green. |
| R2 | Free Layout (L3) produces unprintable cards | Medium | High | Opt-in only, off by default, bounds validation as a blocking error, one-click revert to flow, never applied to the default template |
| R3 | Decomposing a 2,129-line form loses a subtle behaviour | **High** | High | Stage 1 is pure mechanical extraction with zero feature work, verified by golden renders + the existing test suite |
| R4 | Tier A fast preview diverges from Tier B truth | Medium | Medium | Force Tier B on every save and before print; automated test comparing Tier A and Tier B renders per design property |
| R5 | WebView2 unavailable on an operator machine | Low | High | Already a dependency today; keep the existing graceful status-label fallback and degrade to list+form mode rather than failing to open |
| R6 | Operators reject the new layout | Medium | Medium | Ship behind a toggle, keep the old form one cycle, first-run tour, "کجا رفت؟" map, pilot with 2–3 operators before wide release |
| R7 | Mapping inspector misreports on a non-standard database | Low | Medium | Read-only by construction; report labels every finding as "بررسی" not "اصلاح"; it never offers to alter the schema |
| R8 | Undo stack memory growth on large templates | Low | Low | Depth 50, coalesced slider edits, snapshots are small JSON strings |
| R9 | Silent file reversion recurs (as previously seen on `FrmDashboard.cs`) | Medium | High | Confirm git covers this module before Stage 1; commit at every stage boundary; filesystem backup before starting |
| R10 | Scope creep from Phase 2's literal wording | **High** | High | The L1/L2/L3 split is the contract. L3 is a separately-approved stage, not part of the main delivery. |
| R11 | RTL layout bugs in the new shell | Medium | Medium | Project precedent: when an operator says "چپ است" it means RTL is wrong — add an RTL review checkpoint to every stage's definition of done |

---

## 19. DELIVERABLE 10 — FINAL RECOMMENDED UI STRUCTURE

```
FrmCardDesigner  (RTL, resizable, min 1280×800, remembers size/splitters/section)
│
├── COMMAND BAR  (always visible, top)
│   ├── [💾 ذخیره ▾]  → ذخیره · ذخیره با یادداشت · ذخیره به‌عنوان قالب جدید
│   ├── [🖨 پیش‌نمایش چاپ]
│   ├── [✓ اعتبارسنجی ●N]   → slide-in issues panel
│   ├── [↶ برگردان] [↷ دوباره]
│   ├── [⚙]  → ساده/پیشرفته · اندازهٔ قلم ۱۰۰/۱۲۵/۱۵۰٪ · کنتراست بالا · کجا رفت؟ · راهنما
│   └── [✕ بستن]
│
├── SECTION RAIL  (right edge, vertical, 7 items, Ctrl+1..7)
│   1 🧾 اطلاعات قالب      5 📜 پشت کارت
│   2 🎨 طراحی و ظاهر      6 🔐 امنیت و اعتبارسنجی
│   3 📋 فیلدها و محتوا    7 🕘 نسخه‌ها و بازبینی
│   4 🖼 تصاویر و رسانه‌ها
│
├── TEMPLATE PANEL  (right, 280px, collapsible)
│   جستجو · فهرست قالب‌ها (نام، نوع، وضعیت، ★پیش‌فرض) · + قالب جدید
│   right-click: تکثیر · فعال/غیرفعال · خروجی · حذف
│
├── CANVAS  (centre, Fill — the primary surface)
│   ├── VIEW BAR: [روی کارت|پشت کارت] · زوم ▾ (۲۵…۲۰۰٪, عرض/ارتفاع/صفحه) · ⛶
│   │             نمونه: (دادهٔ نمونه|پروندهٔ واقعی) [جستجوی مددجو ▾] ◀ ▶
│   ├── WebView2 live card + selection overlay
│   └── click element → select · drag → reorder · handles → resize · arrows → nudge
│
├── INSPECTOR  (left, 360px, splitter)
│   ├── when an element is selected → its property sheet + its DB mapping + [بررسی]
│   └── otherwise → the active section's full controls
│
└── STATUS BAR
    وضعیت ذخیره · شمارهٔ نسخه · آخرین تغییردهنده و تاریخ · شمارندهٔ خطا/هشدار
```

### Why this reaches the usability target

| Audience | What this structure gives them |
|---|---|
| **Non-technical operator** | Sees a real card at real size; changes things by clicking them; can undo; cannot save a broken template unknowingly; never needs to know what `FieldOrderCsv` is |
| **NGO field staff** | Previews the actual beneficiary about to be printed; catches a wrong photo or an overflowing name before wasting a PVC card |
| **Administrator** | Version history with who/what/when, visual diff, exportable audit trail and mapping report |
| **ERP/IT** | Zero schema change, additive JSON, read-only introspection, decomposed testable architecture, golden-render regression contract |
| **Accessibility** | Full keyboard path, high contrast, large font, RTL-native, no colour-only status, canvas mirrored by an accessible list |

---

## 20. WHAT THIS PLAN DELIBERATELY DOES NOT DO

Stated explicitly so the boundaries are a decision, not an oversight:

1. **Does not modify `GuardianCard/`.** Every visual capability is delivered by injection into a staged copy.
2. **Does not migrate the database.** The only optional DDL is a non-blocking index.
3. **Does not remove a single existing setting.** §3's traceability table accounts for all of them.
4. **Does not make free X/Y positioning the default.** It is Stage 6, opt-in, reversible — because the flow layout is what every existing printed card depends on.
5. **Does not block saving on validation errors.** Only publishing. Operators must never lose work.
6. **Does not change print geometry.** Zoom is screen-only, and a regression test enforces it.
