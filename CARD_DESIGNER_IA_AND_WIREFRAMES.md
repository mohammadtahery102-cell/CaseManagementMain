# Card Designer — Information Architecture & Wireframes

**UX deliverable — to be approved BEFORE any code is written.**
**Module:** `CaseManagement/GuardianCardIntegration/FrmCardTemplateManager.cs`
**Date:** 2026-08-31

> **Relationship to `CARD_DESIGNER_REDESIGN_PLAN.md`:** this document **supersedes** that plan's sections 2–5 (UX plan, navigation, layouts, wireframes) and **defers** its sections 9–11 (mapping inspector, validation engine, free layout) to a later, separately-approved phase. Simplification ships first. Features wait.

---

## 1. THE REAL PROBLEM IS NOT TAB COUNT

Tab count is a symptom. The measurable problem is the **settings surface** and how it is scattered.

### 1.1 Actual settings inventory (counted from source)

| Group | Count | Where today |
|---|---|---|
| Direct design controls (colours, fonts, scales, images, toggles) | 30 | Tabs 1, 4, 5, 6, 7 |
| Toggleable fields — Full variant (`ToggleableFields`) | 26 | Tab 2 |
| Toggleable fields — Simple variant (`ToggleableFieldsSimple`) | 15 | Tab 2 (same control, different list) |
| Reorderable text fields (`FieldOrderableKeys`) | 5 | Tab 3 |
| Reorderable security cells (`SecurityBandOrderableKeys`) | 5 | Tab 3 |
| Ledger month toggles | 12 | Tab 7 |
| **Text overrides: 20 keys × 7 properties each** | **140** | **Tab 8 — behind ONE combo box** |
| **TOTAL individually addressable settings** | **≈ 230** | across 9 tabs |

**140 of ~230 settings — 61% of the entire product — are hidden behind a single dropdown in tab 8.**
The operator must select a field from a combo, then edit content / colour / font / size / line-height / weight / alignment, then select the next field. There is no list, no overview, no indication which fields have been customised. This is the single largest discoverability failure in the module, and a tab-count discussion completely misses it.

### 1.2 The scatter problem — proven, not asserted

These five field keys appear in **three separate lists** in the source:

- `ToggleableFields` — `PublicCode, GuardianName, FatherName, NationalID, RequestType`
- `FieldOrderableKeys` — `PublicCode, GuardianName, FatherName, NationalID, RequestType`
- `TextOverrideFieldKeys` — `..., GuardianName, FatherName, NationalID, RequestType, PublicCode, ...`

**Identical sets.** So to fully configure one field — «نام سرپرست» — today's operator must visit:

| To do this | They go to |
|---|---|
| Show/hide it | Tab 2 · فیلدهای قابل‌نمایش |
| Move it up/down | Tab 3 · ترتیبِ فیلدها |
| Change its colour, size, font, weight, alignment, line-height | Tab 8 · متنِ پشتِ کارت و حدیث |

**Three tabs, for one field.** And the third tab is named "back of card text and hadith" — which is where you edit the *front* card's guardian name. An operator cannot possibly guess that.

> **This is the finding that determines the whole architecture.** The fix is not fewer tabs. The fix is: **settings must be grouped around the object the operator is thinking about, not around the mechanism that implements them.**

### 1.3 Other IA defects

| # | Defect | Evidence |
|---|---|---|
| A1 | Tab titles encode card side as a prefix — `[روی]` / `[پشت]` — because side is a missing dimension | 4 of 9 tab labels |
| A2 | Tab 8's name describes 2 of its 20 keys; the other 18 are front-of-card | `TextOverrideFieldKeys` |
| A3 | Tab 3 contains a list that does nothing, and says so: «ترتیبِ این لیست فقط برای سازمان‌دهیِ شماست» | :537 |
| A4 | Tab 6 is named "لوگو و تصویر" but holds portrait scale, header height, family-photo ratio, row limits — layout, not images | `BuildLogoImageSection` |
| A5 | Some text fields allow content editing, some only colour/size (`TextOverrideContentLocked`) — the UI does not explain which or why | :148 |
| A6 | Same control (`_chkFields`) shows a different field list depending on variant, with no signposting | `PopulateFieldChecklist` |
| A7 | Preview — the thing the operator is actually trying to affect — occupies 480 px while settings occupy the rest | :384 |

---

## 2. IA METHOD — HOW THE NEW GROUPING WAS DERIVED

I grouped by **the operator's mental object**, not by the implementing mechanism.

Every one of the ~230 settings was assigned to exactly one of these questions an NGO operator actually asks:

| Operator's question | Becomes |
|---|---|
| "Which card is this, and is it in use?" | **کارت** (Card) |
| "What does the card look like overall?" | **ظاهر** (Look) |
| "What information appears on it, and how does each item look?" | **محتوا** (Content) |
| "What's on the back?" | **پشت کارت** (Back) |
| "Who changed what?" | **تاریخچه** (History) |

Two organising rules fell out of this:

**Rule 1 — One field, one place.** Every setting belonging to a field (visible? order? colour? size? content?) lives with that field. This dissolves tabs 2, 3 and 8 into a single content section and eliminates the three-tab scatter proven in §1.2.

**Rule 2 — Side of card is a view, not a category.** The front/back toggle sits above the preview and filters both the preview and the settings panel. This dissolves every `[روی]`/`[پشت]` prefix.

---

## 3. DELIVERABLE 1 & 2 — NEW IA: 9 TABS → 5 SECTIONS

| # | New section | Persian | Absorbs |
|---|---|---|---|
| 1 | Card | **کارت** | Tab 1 + the bottom bar's duplicate/export/import/default |
| 2 | Look | **ظاهر** | Tab 4 (all) + Tab 6 (backgrounds, watermark) + Tab 5 (security toggles) |
| 3 | Content | **محتوا** | **Tabs 2 + 3 + 8 merged** + Tab 6's portrait/family layout settings |
| 4 | Back | **پشت کارت** | Tab 7 + the back-side subset of Tab 8 |
| 5 | History | **تاریخچه** | Tab 9 |

**9 → 5 sections (−44%).** More importantly: **the number of places you must visit to configure one field goes 3 → 1.**

Five is the right number, not seven. The brief proposed seven; two of those (تصاویر و رسانه‌ها, امنیت و اعتبارسنجی) contain 4 and 3 controls respectively — too thin to earn a top-level slot, and splitting them re-creates the scatter problem. Images belong with the look they create; security toggles are three checkboxes that belong with the look of the card's footer band.

### 3.1 Deliverable 4 — Renaming: technical → operator language

| Today | New | Why |
|---|---|---|
| 🧾 اطلاعات کارت | **کارت** | It's the template's identity. One word. |
| 📋 [روی] فیلدهای قابل‌نمایش | *(merged)* | "فیلد" is developer vocabulary — a database word |
| 🔀 [روی] ترتیبِ فیلدها | *(merged)* | Ordering is an action, not a place |
| 🎨 ظاهر کارت (هر دو رو) | **ظاهر** | "(هر دو رو)" becomes unnecessary once side is a view |
| 📷 [روی] QR Code و امنیت | *(merged into ظاهر)* | 3 checkboxes never justified a tab |
| 🖼 لوگو و تصویر (هر دو رو) | *(split: images→ظاهر, layout→محتوا)* | The tab mixed two unrelated things (A4) |
| 🖨 [پشت] جدولِ پرداخت | **پشت کارت** | Operators say "پشت کارت", not "جدول پرداخت" |
| 📜 [پشت] متنِ پشتِ کارت و حدیث | *(merged)* | Actively misleading — held mostly front-card settings (A2) |
| 🕘 تاریخچهٔ نسخه‌ها | **تاریخچه** | "نسخه" is fine but redundant |

**Vocabulary rules applied throughout:**

| Never say | Say instead |
|---|---|
| فیلد | اطلاعات / مورد / سطر |
| قالب (as a mechanism) | کارت |
| رندر / پیش‌نمایش زنده | نمایش کارت |
| Toggle / فعال‌سازی | نمایش بده / نشان نده |
| Override | تغییر بده |
| مقیاس (٪) | بزرگ‌تر / کوچک‌تر |
| Variant | نوع کارت |
| LayoutVariant: Full / Simple | کارت کامل / کارت ساده |
| CSV / JSON (anywhere user-visible) | — never shown |

---

## 4. DELIVERABLE 5 — BASIC MODE & ADVANCED MODE

### 4.1 The rule for what belongs in Basic

A setting is in **Basic** only if it passes all four tests:

1. **Frequency** — a typical operator changes it when setting up a card for their branch.
2. **Safety** — a wrong value is visibly wrong on screen, never wrong only at print time.
3. **Self-evident** — needs no knowledge of the rendering engine or the database.
4. **Independence** — changing it cannot break another setting.

Everything else is **Advanced**. Advanced is not hidden — it's one visible switch away, and it never moves a control to a different section. **Switching modes reveals or collapses; it never relocates.** This is what makes the mode switch safe to use: nothing you learned in Basic becomes wrong in Advanced.

### 4.2 Basic Mode — 14 settings total

| Section | Basic settings |
|---|---|
| **کارت** | نام کارت · نوع کارت (کامل/ساده) · در حال استفاده (on/off) |
| **ظاهر** | تم آماده (preset) · رنگ اصلی · رنگ متن · قلم · اندازهٔ متن (کوچک/متوسط/بزرگ) · لوگو |
| **محتوا** | the field list with **show/hide only** (one checkbox per row) |
| **پشت کارت** | جدول پرداخت (on/off) · حدیث/متن پشت |
| **تاریخچه** | list + restore |

An operator can produce a correct, branded, printable card touching **only these 14**. That is the design target for "usable by non-technical staff".

### 4.3 Advanced Mode — everything else, revealed in place

| Section | Additionally revealed |
|---|---|
| **ظاهر** | رنگ فرعی · رنگ پس‌زمینه · اندازهٔ متن as exact ٪ · پس‌زمینهٔ رو/پشت · واترمارک + شفافیت · ارتفاع سربرگ · رنگ سربرگ · QR / بارکد / هولوگرام · ترتیب نوار پایین |
| **محتوا** | per-field expander: ترتیب (▲▼) · رنگ · اندازه · قلم · ضخامت · چینش · فاصلهٔ خط · متن دلخواه · عکس سرپرست (اندازه/خالی/جای عکس) · عکس خانوادگی (نسبت/اندازه/بدون برش) · سقف تعداد اعضا |
| **پشت کارت** | انتخاب ماه‌ها (۱۲) · per-text typography |
| **تاریخچه** | مقایسه |

### 4.4 Mode behaviour

- Default for a **new user: Basic.** Persisted per user thereafter.
- A single switch in the header: `( ● ساده ‖ ○ پیشرفته )`.
- **Advanced-only settings that hold a non-default value show a badge in Basic:** «۳ تنظیم پیشرفته فعال است [نمایش]». An operator is never lied to about what's affecting their card — the top cause of distrust in simplified UIs.
- Basic never *removes capability from the template* — it only hides controls. A template edited in Basic keeps every Advanced value it already had.

---

## 5. DELIVERABLE 6 — LIVE PREVIEW AS THE PRIMARY ELEMENT

### 5.1 Space allocation, before and after

| | Preview | Settings | Template list |
|---|---|---|---|
| **Today** (1600 px) | 480 px (30%) | ~860 px (54%) | 260 px (16%) |
| **New** (1600 px) | **~900 px (56%)** | 400 px (25%) | 260 px, collapsible (16%) |

Preview goes from a side panel to the largest thing on screen — **+88% width**, and it sits in the centre of vision rather than the left edge.

### 5.2 Why the preview moves to the centre, not the left

In an RTL interface the eye starts at the **right**. Today the operator's attention starts at the template list (right), travels left through settings, and only reaches the preview last — the reverse of the actual task, which is *look at the card, then change it*. Centring the preview puts it in the path of every glance, and puts the settings panel adjacent to it rather than across the screen.

### 5.3 Preview promotion rules

1. The card renders at **true print size at 100% zoom** — so what fills the operator's screen is literally the physical card.
2. The preview is **never obscured** — no modal dialogs over it; settings appear beside it.
3. **Every change is visible without scrolling the preview.** Changing a back-side setting auto-flips the preview to the back.
4. The preview shows a **real beneficiary** (see §7), not an arbitrary record.
5. The preview has a **fullscreen key (`F11`)** for print-check.

---

## 6. DELIVERABLE 7 — MINIMISING NAVIGATION

### 6.1 Click-to-edit: the primary mechanism

Clicking an element on the card selects it, and the settings panel becomes that element's settings. **This is what makes tab-switching mostly disappear**, because the operator navigates by pointing at the thing they can see rather than by guessing which section owns it.

The section rail remains for people who prefer lists, and for keyboard/screen-reader users — the canvas is never the only path (see §9).

### 6.2 Measured navigation reduction

| Task | Today | New | Change |
|---|---|---|---|
| Fully configure one field (show + order + colour + size) | **3 tabs, ~14 clicks** | 1 panel, ~5 clicks | **−64%** |
| Change the org name text and its colour | 2 tabs (1 + 8) | click it on the card | **−1 tab** |
| Hide a field | tab 2, scan 26 unlabelled rows | type 3 letters, Space | **−1 tab** |
| Set up a new branch card (typical full task) | **7 tabs visited** | **2 sections** | **−71%** |
| Reorder two items | tab 3, two lists, one decorative | drag on the card | **−1 tab** |

### 6.3 Structural rules that keep it low

- **No setting appears in two places.** One home each; §3 accounts for all ~230.
- **No setting requires a different section to take effect.** Today, hiding `GuardianName` in tab 2 makes its tab-8 typography settings silently inert — the new per-field row greys its own options instead.
- **Section switching never resets the preview** (no scroll or zoom loss).
- **The five sections are ordered by frequency of use**, not by data model: کارت → ظاهر → محتوا → پشت کارت → تاریخچه.

---

## 7. DELIVERABLE 8 — WIREFRAMES

### W0 · Master frame (RTL — right is primary)

```
╔══════════════════════════════════════════════════════════════════════════════╗
║  کارت شناسایی — طراحی                                                        ║
║  [💾 ذخیره]  [🖨 چاپ آزمایشی]        ( ● ساده ‖ ○ پیشرفته )   [؟]  [✕]      ║
╠═══════════════════════╤════════════════════════════════════╤═════════════════╣
║ SETTINGS (left, 400)  │  PREVIEW (centre, ~900 — PRIMARY)  │ CARDS (right,   ║
║                       │                                    │  260, collapse) ║
║ ┌───────────────────┐ │  ┌ [روی کارت] [پشت کارت]  ⛶ ─────┐ │ 🔍 [جستجو…]     ║
║ │ contextual to the │ │  │ نمایش برای: [احمد رضایی ▾]     │ │                 ║
║ │ selection, or to  │ │  │ زوم: [—— ● ——] ۱۰۰٪            │ │ ┌─────────────┐ ║
║ │ the active section│ │  └────────────────────────────────┘ │ │ کارت اصلی   │ ║
║ │                   │ │                                    │ │ ★ پیش‌فرض   │ ║
║ │                   │ │      ┌──────────────────────┐      │ └─────────────┘ ║
║ │                   │ │      │                      │      │ ┌─────────────┐ ║
║ │                   │ │      │   THE CARD, LIVE     │      │ │ کارت ساده   │ ║
║ │                   │ │      │   at true print size │      │ └─────────────┘ ║
║ │                   │ │      │   click to select    │      │                 ║
║ │                   │ │      │                      │      │ [+ کارت جدید]  ║
║ │                   │ │      └──────────────────────┘      │                 ║
║ └───────────────────┘ │                                    │                 ║
╠═══════════════════════╧════════════════════════════════════╧═════════════════╣
║ [کارت] [ظاهر] [محتوا] [پشت کارت] [تاریخچه]        ✓ ذخیره شد · نسخهٔ ۱۲     ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

**Note the section rail is at the BOTTOM, under the settings panel** — not a top tab strip. Reason: it sits directly beneath the panel it controls, so the eye travels a short distance, and it cannot be confused with the front/back toggle above the preview. Five items fit comfortably with full Persian words and no truncation.

---

### W1 · کارت (Card)

```
┌ کارت ──────────────────────────────────────────┐
│                                                │
│  نام کارت                                      │
│  ┌──────────────────────────────────────────┐  │
│  │ کارت هویت ایتام — دفتر بامیان            │  │
│  └──────────────────────────────────────────┘  │
│                                                │
│  نوع کارت                                      │
│  ┌────────────────┐  ┌────────────────┐        │
│  │ ▨▨▨▨▨▨▨▨▨▨▨▨  │  │ ▨▨▨▨▨▨▨▨▨▨▨▨  │        │
│  │ ▨ thumbnail ▨  │  │ ▨ thumbnail ▨  │        │
│  │ ▨▨▨▨▨▨▨▨▨▨▨▨  │  │ ▨▨▨▨▨▨▨▨▨▨▨▨  │        │
│  │  ● کارت کامل   │  │  ○ کارت ساده   │        │
│  │ با جدول پرداخت │  │ یک‌رو، خلاصه   │        │
│  └────────────────┘  └────────────────┘        │
│                                                │
│  ☑ این کارت در حال استفاده است                 │
│  ★ کارت پیش‌فرض سازمان                         │
│                                                │
│ ─── پیشرفته ─────────────────────────────────  │
│  توضیح                                         │
│  ┌──────────────────────────────────────────┐  │
│  │                                          │  │
│  └──────────────────────────────────────────┘  │
│  [کپی از این کارت] [ذخیره در فایل] [بازیابی]   │
│                                                │
│  ساخته: مریم · ۱۴۰۵/۰۳/۱۲                     │
│  آخرین تغییر: احمد · ۱۴۰۵/۰۶/۰۹ · نسخهٔ ۱۲    │
└────────────────────────────────────────────────┘
```

Replaces today's `_cmbLayoutVariant` dropdown with two picture choices — an operator picks a card by *recognising* it, not by reading "Full"/"Simple".

---

### W2 · ظاهر (Look) — Basic Mode

```
┌ ظاهر ──────────────────────────────────────────┐
│  تم آماده                                      │
│  ┌────┐┌────┐┌────┐┌────┐┌────┐                │
│  │████││████││████││████││████│                 │
│  │سازمانی││آبی ││سبز ││خنثی││پرکنتراست│         │
│  └────┘└────┘└────┘└────┘└────┘                │
│   ● selected                                   │
│                                                │
│  رنگ اصلی      [██ سرمه‌ای    ▾]               │
│  رنگ متن       [██ مشکی       ▾]               │
│                                                │
│  قلم           [بی‌نازنین      ▾]              │
│  اندازهٔ متن   ( کوچک )( ● متوسط )( بزرگ )     │
│                                                │
│  لوگوی سازمان                                  │
│  ┌────────┐                                    │
│  │ [عکس]  │  [تعویض]  [حذف]                    │
│  └────────┘                                    │
│                                                │
│  ⓘ ۲ تنظیم پیشرفته روی این کارت فعال است       │
│    [نمایش بده]                                 │
└────────────────────────────────────────────────┘
```

Colours are named («سرمه‌ای»), not hex. Text size is three words, not a 50–200 spinner. The notice at the bottom is the honesty rule from §4.4.

### W2b · ظاهر — Advanced Mode (same section, expanded in place)

```
┌ ظاهر ─────────────────────────────── پیشرفته ──┐
│  … everything from Basic, unchanged, above …   │
│                                                │
│ ▼ رنگ‌های بیشتر                                │
│    رنگ فرعی        [██ #3E7CB1] کنتراست ۷.۲:۱✓│
│    رنگ پس‌زمینه    [██ سفید   ]                │
│    رنگ نوار بالا   [██ سرمه‌ای]                │
│                                                │
│ ▼ اندازه‌ها                                    │
│    اندازهٔ متن     [──●───] ۱۰۰٪                │
│    ارتفاع نوار بالا[──●───] ۱۰۰٪                │
│                                                │
│ ▼ تصویرهای پس‌زمینه                            │
│    ┌──────┐ ┌──────┐ ┌──────┐                  │
│    │ روی  │ │ پشت  │ │واترمارک│                │
│    │[عکس] │ │[عکس] │ │ [عکس] │                 │
│    │تعویض✕│ │تعویض✕│ │شفافیت │                 │
│    │      │ │      │ │[─●─]۱۵٪│                │
│    └──────┘ └──────┘ └──────┘                  │
│    ⚠ فایل واترمارک پیدا نشد                     │
│                                                │
│ ▼ نوار پایین کارت                              │
│    ☐ QR Code   ☑ بارکد   ☑ هولوگرام            │
│    ترتیب (بکشید):                              │
│    [بارکد][امضا][مهر][هولوگرام]                 │
└────────────────────────────────────────────────┘
```

---

### W3 · محتوا (Content) — **the section that fixes §1.2**

```
┌ محتوا ─────────────────────────────────────────┐
│ 🔍 [جستجو…]      نمایش: (همه)(روشن)(خاموش)     │
│                                    ۲۴ از ۲۶ روشن│
├────────────────────────────────────────────────┤
│ ▼ مشخصات سرپرست                    [همه][هیچ]  │
│   ☑ نام سرپرست                          ⚙  ⋮⋮  │
│   ☑ نام پدر                             ⚙  ⋮⋮  │
│   ☑ شماره تذکره                         ⚙  ⋮⋮  │
│   ☐ نوع مددجو                           ⚙  ⋮⋮  │
│   ☑ کد عمومی                            ⚙  ⋮⋮  │
│                                                │
│ ▼ عکس‌ها                            [همه][هیچ] │
│   ☑ عکس سرپرست                          ⚙      │
│   ☑ عکس خانوادگی                        ⚙      │
│                                                │
│ ▼ خانواده                          [همه][هیچ]  │
│   ☑ فهرست اعضا                          ⚙      │
│   ☑ عکس هر عضو                                 │
│   ☑ تعداد ایتام                         ⚙      │
│                                                │
│ ▼ سازمان و تماس                    [همه][هیچ]  │
│   ☑ نام سازمان                          ⚙      │
│   ☑ آدرس    ☑ تلفن   ☐ وب‌سایت  ☐ ایمیل        │
│                                                │
│ ▼ تذکرات (۵)                       [همه][هیچ]  │
└────────────────────────────────────────────────┘
```

`⋮⋮` = drag handle (order). `⚙` = expand this item's own settings.

**W3b · One item expanded — every setting for that field, in one place:**

```
│   ☑ نام سرپرست                          ⚙▲ ⋮⋮  │
│   ┌──────────────────────────────────────────┐ │
│   │  جای این مورد   [▲ بالاتر] [▼ پایین‌تر] ۲/۵│ │
│   │                                          │ │
│   │  ─── پیشرفته ───────────────────────────│ │
│   │  رنگ      [██ ▾]     اندازه [──●──]۱۰۰٪ │ │
│   │  قلم      [پیش‌فرض ▾] ضخامت [متوسط  ▾] │ │
│   │  چینش     (راست)(وسط)(چپ)                │ │
│   │  فاصلهٔ خط[──●──] ۱۰۰٪                   │ │
│   │                                          │ │
│   │  ⓘ متن این مورد از پروندهٔ مددجو می‌آید  │ │
│   │    و اینجا قابل تغییر نیست.              │ │
│   │                        [بازنشانی همه]    │ │
│   └──────────────────────────────────────────┘ │
```

The `ⓘ` line makes `TextOverrideContentLocked` (defect A5) visible and explained, instead of a mysteriously disabled textbox. For unlocked items (e.g. «بسم‌الله», «شعار»), that line is replaced by an editable **متن** box.

**This single row replaces tabs 2, 3 and 8 for that field. Three tabs → one expander.**

---

### W4 · پشت کارت (Back)

Selecting this section **auto-flips the preview to the back** — the operator never edits something they cannot see.

```
┌ پشت کارت ──────────────────────────────────────┐
│  ☑ جدول پرداخت را چاپ کن                       │
│                                                │
│  متن پشت کارت / حدیث                           │
│  ┌──────────────────────────────────────────┐  │
│  │ …                                        │  │
│  │                                          │  │
│  └──────────────────────────────────────────┘  │
│  رنگ [██ ▾]   اندازه [──●──] ۱۰۰٪              │
│                                                │
│  پیام شکایت          [ویرایش]                  │
│  پیام کارت پیدا شده  [ویرایش]                  │
│                                                │
│ ─── پیشرفته ─────────────────────────────────  │
│  کدام ماه‌ها چاپ شوند؟         [همه] [هیچ‌کدام]│
│  ☑حمل ☑ثور ☑جوزا ☑سرطان ☑اسد ☑سنبله            │
│  ☑میزان ☑عقرب ☑قوس ☑جدی ☑دلو ☑حوت              │
└────────────────────────────────────────────────┘
```

Today these two lists (ledger months, back text) live in two separate tabs (7 and 8) despite being the same physical side of the same card.

---

### W5 · تاریخچه (History)

```
┌ تاریخچه ───────────────────────────────────────┐
│  ● نسخهٔ ۱۲  — احمد — ۱۴۰۵/۰۶/۰۹ ۱۰:۴۲         │
│    رنگ اصلی، اندازهٔ متن، «نوع مددجو» روشن شد   │
│                              [بازگرداندن]      │
│  ○ نسخهٔ ۱۱  — مریم — ۱۴۰۵/۰۶/۰۱               │
│    لوگو تعویض شد                               │
│                              [بازگرداندن]      │
│  ○ نسخهٔ ۱۰  — مریم — ۱۴۰۵/۰۵/۲۸               │
│                              [بازگرداندن]      │
│                                                │
│ ─── پیشرفته ─────────────────────────────────  │
│  دو نسخه را انتخاب کنید:        [مقایسه]       │
└────────────────────────────────────────────────┘
```

Each row carries a plain-language change summary. Restore states: «نسخهٔ فعلی حفظ می‌شود و چیزی پاک نمی‌شود» — because the existing `RestoreSelectedVersion` already saves forward.

---

### W6 · Selection state — clicking the card

```
    PREVIEW                          SETTINGS PANEL
 ┌──────────────────┐    ┌─────────────────────────────┐
 │  ┌────────────┐  │    │ ← بازگشت به «محتوا»         │
 │  │╔══════════╗│  │    ├─────────────────────────────┤
 │  │║نام: احمد ║│◄─┼────┤  نام سرپرست                 │
 │  │╚══════════╝│  │    │                             │
 │  │ selected   │  │    │  ☑ روی کارت نشان بده        │
 │  └────────────┘  │    │  جای این مورد [▲][▼]  ۲/۵   │
 └──────────────────┘    │  ─── پیشرفته ──────────────│
                         │  رنگ [██▾]  اندازه[──●──]  │
                         │  …same as W3b…             │
                         └─────────────────────────────┘
```

Identical control set to W3b — the operator learns it once and reaches it two ways. The «← بازگشت» link keeps the section model intact so nobody gets lost.

---

### W7 · Empty & error states (frequently skipped, always needed)

| State | Wireframe copy |
|---|---|
| No templates yet | «هنوز کارتی ساخته نشده. [ساخت اولین کارت]» + a picture of a blank card |
| No cases in DB | «برای نمایش، حداقل یک پرونده لازم است. فعلاً از دادهٔ نمونه استفاده می‌شود.» — preview still renders with sample data |
| Preview loading | Skeleton card outline, never a blank panel |
| Image file missing | Inline red badge on the slot: «فایل پیدا نشد» + [انتخاب دوباره] |
| Unsaved changes on close | «تغییرات ذخیره نشده. [ذخیره و خروج] [خروج بدون ذخیره] [ادامهٔ ویرایش]» |
| Field off but styled | Row greys, tooltip: «چون این مورد خاموش است، این تنظیمات اثری ندارند» (fixes §6.3's silent-inert problem) |

---

## 8. DELIVERABLE 9 — OPTIMISED FOR NGO OPERATORS

| Operator reality | Design response |
|---|---|
| Not a designer — doesn't know hex, points, or leading | Named colours, three text sizes, presets. Numbers only in Advanced. |
| Doesn't know the database | Never shows table/column names in the main flow (that's the deferred inspector, and it lives behind Advanced) |
| Afraid of breaking the official card | Preview is always truthful and always visible; every setting has [بازنشانی]; history is one section away |
| Prints on real PVC stock — mistakes cost money | Real-record preview + fullscreen print check before printing |
| Shared/low-spec machines, small screens | Panels collapse; minimum 1280×800; template list collapsible to reclaim 260 px |
| Persian-first, RTL | Right-origin layout, Persian numerals in the UI, Solar month names, no English in the main flow |
| Trained by a colleague, not by a manual | Everything visible is self-describing; the ⓘ lines explain *why* a control is disabled |
| Interrupted constantly | Unsaved state is explicit in the status bar; nothing is lost on section switch |

**The acceptance test for the IA:** a new NGO operator, given no training, must be able to *duplicate the default card, change its name and colour, hide two items, and print a test* — without opening Advanced Mode and without asking anyone. If any step requires explanation, the IA is not finished.

---

## 9. ACCESSIBILITY BUILT INTO THE IA (not bolted on)

- **The canvas is never the only path.** Every click-to-select action has an equivalent in the محتوا list (checkbox, ▲▼ buttons, ⚙ expander). Drag always has ▲▼ next to it.
- **Sections reachable by `Ctrl+1..5`**; `Tab` order follows RTL reading order.
- **Mode switch is keyboard-reachable and announced**; Advanced content is `aria-expanded`, not merely visually hidden.
- **Status never colour-only** — ✓ / ⚠ / ✕ glyphs accompany every state (the ⚠ on the watermark slot in W2b).
- **Named colours help colour-blind operators** far more than swatches alone.

---

## 10. SCOPE DISCIPLINE — WHAT THIS PHASE DELIBERATELY DOES NOT ADD

Per priority 10, simplification ships before features. **Explicitly deferred**, and not part of this approval:

| Deferred | Why it waits |
|---|---|
| Database Mapping Inspector | A power/admin feature. Adds a whole section for an audience of one or two. |
| Full Validation Engine | Keep only two inline checks that prevent visible breakage (missing image file, empty name). The full rules engine is a separate phase. |
| Free X/Y layout (`LayoutMode="Free"`) | Adds capability *and* risk. The opposite of this phase's goal. |
| Visual version diff | The plain-language change summary in W5 delivers most of the value for none of the cost. |
| Audit trail across templates | Admin reporting, not operator UX. |
| Undo/redo | **Genuinely tempting, and still deferred** — it's a cross-cutting architectural change, not an IA change. W7's explicit unsaved-changes handling and one-click [بازنشانی] cover the common cases meanwhile. |

**One thing this phase does add**, because it is simplification, not a feature: **theme presets** (W2). One click replaces four separate colour decisions — it removes work rather than adding surface.

---

## 11. APPROVAL CHECKLIST

Before any code is written, confirm:

- [ ] 9 tabs → 5 sections, and the names in §3 read correctly to an actual operator
- [ ] The §1.2 scatter fix (one field, one place) is the right call
- [ ] Basic Mode's 14 settings are the right 14
- [ ] The bottom-placed section rail is acceptable (vs. a right-hand vertical rail)
- [ ] Preview at ~56% of width is the right allocation
- [ ] The deferred list in §10 is agreed — especially undo/redo
- [ ] Wireframes W1–W7 are approved as the build target

**Recommended next step after approval:** paper-test W1–W3 with two real operators before writing code. An hour of that will find more than a week of review.
