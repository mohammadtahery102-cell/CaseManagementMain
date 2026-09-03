# Card Designer — UX Specification (Approval Document)

**Phase:** UX-first. No implementation until this is approved.
**Module:** `CaseManagement/GuardianCardIntegration/FrmCardTemplateManager.cs` (2,129 lines)
**Date:** 2026-08-31

> **Supersedes** `CARD_DESIGNER_REDESIGN_PLAN.md` and `CARD_DESIGNER_IA_AND_WIREFRAMES.md`.
> This is the single document to approve.
>
> **Two corrections to figures given earlier:** the Full variant has **23** toggleable fields (not 26), and the text-override matrix is **21 keys × 7 properties = 147** settings. True total: **~240**.
> **One design decision reversed:** navigation moves to the **top** (was proposed at the bottom). Top is correct for WinForms.

---

# PART A — AUDIT

## A1. Complete control inventory

Every control in the current designer, extracted from source. Nothing here may be lost.

### A1.1 Direct controls (30)

| # | Control field | Label today | Tab | New home | Mode |
|---|---|---|---|---|---|
| 1 | `_txtName` | نام قالب | 1 | کارت | Basic |
| 2 | `_cmbTemplateType` | نوع قالب | 1 | کارت | Advanced |
| 3 | `_cmbLayoutVariant` | طرحِ کامل / ساده | 1 | کارت | Basic |
| 4 | `_txtDescription` | توضیح | 1 | کارت | Advanced |
| 5 | `_chkIsActive` | قالب فعال است | 1 | کارت | Basic |
| 6 | `_lblDefaultTag` | (badge پیش‌فرض) | 1 | کارت | Basic |
| 7 | `_lblMetaInfo` | (meta) | 1 | کارت footer | Basic |
| 8 | `_chkFields` | فیلدهای قابل‌نمایش | 2 | محتوا | Basic |
| 9 | `_lstFieldOrder` | ترتیبِ فیلدها | 3 | محتوا (per-card) | Advanced |
| 10 | `_lstBandOrder` | ترتیبِ نوار امنیتی | 3 | ظاهر | Advanced |
| 11 | `_radPhotoBefore` | قبل از فیلدها | 3 | محتوا (عکس card) | Advanced |
| 12 | `_radPhotoAfter` | بعد از فیلدها | 3 | محتوا (عکس card) | Advanced |
| 13 | `_pnlPrimaryColor` | رنگ اصلی | 4 | ظاهر | Basic |
| 14 | `_pnlSecondaryColor` | رنگ فرعی | 4 | ظاهر | Advanced |
| 15 | `_pnlBackgroundColor` | رنگ پس‌زمینه | 4 | ظاهر | Advanced |
| 16 | `_pnlTextColor` | رنگ متن | 4 | ظاهر | Basic |
| 17 | `_cmbFont` | فونت | 4 | ظاهر | Basic |
| 18 | `_numFontScale` | اندازهٔ فونت (٪) | 4 | ظاهر | Basic¹ |
| 19 | `_chkQRCode` | نمایشِ QR Code | 5 | ظاهر | Advanced |
| 20 | `_chkBarcode` | نمایشِ بارکد | 5 | ظاهر | Advanced |
| 21 | `_chkHologram` | هولوگرامِ امنیتی | 5 | ظاهر | Advanced |
| 22 | `_txtBgFront` | پس‌زمینهٔ روی کارت | 6 | ظاهر | Advanced |
| 23 | `_txtBgBack` | پس‌زمینهٔ پشتِ کارت | 6 | ظاهر | Advanced |
| 24 | `_txtWatermark` | واترمارک | 6 | ظاهر | Advanced |
| 25 | `_numWatermarkOpacity` | شفافیتِ واترمارک | 6 | ظاهر | Advanced |
| 26 | `_pnlHeaderBgColor` | رنگ پس‌زمینهٔ هدر | 6 | ظاهر | Advanced |
| 27 | `_numHeaderHeightScale` | ارتفاع هدر (٪) | 6 | ظاهر | Advanced |
| 28 | `_numPortraitScale` | اندازهٔ عکس هدر | 6 | محتوا (عکس card) | Advanced |
| 29 | `_chkPortraitBlank` | حذف عکس پیش‌فرض هدر | 6 | محتوا (عکس card) | Advanced |
| 30 | `_cmbFamilyPhotoRatio` | نسبت عکس جمعی | 6 | محتوا (عکس card) | Advanced |
| 31 | `_numFamilyPhotoScale` | اندازهٔ عکس جمعی | 6 | محتوا (عکس card) | Advanced |
| 32 | `_chkFamilyPhotoFitContain` | نمایش کامل بدون برش | 6 | محتوا (عکس card) | Advanced |
| 33 | `_numFamilyListMaxRows` | سقف ردیف اعضا | 6 | محتوا (خانواده card) | Advanced |
| 34 | `_chkMonths` | ماه‌های جدول پرداخت (۱۲) | 7 | پشت کارت | Advanced |
| 35 | `_cmbTextOverrideField` | انتخاب متن | 8 | **dissolved** — see A1.4 | — |
| 36–42 | 7 override editors | محتوا/رنگ/فونت/سایز/فاصله/چینش/ضخامت | 8 | محتوا (per-card) | Advanced |
| 43 | `_lstVersions` | تاریخچه | 9 | تاریخچه | Basic |
| 44 | `_btnRestoreVersion` | بازگردانی | 9 | تاریخچه | Basic |
| 45 | `_btnCompareVersions` | مقایسه | 9 | تاریخچه | Advanced |
| 46 | `_lstTemplates` / `_txtSearchTemplates` | فهرست قالب‌ها | — | right panel (kept) | Basic |
| 47 | bottom bar buttons | ذخیره/حذف/تکثیر/فعال/خروجی/ورودی | — | top command bar + کارت | Basic |

¹ Basic exposes it as کوچک/متوسط/بزرگ; Advanced exposes the exact percentage. Same underlying `FontScalePercent`.

### A1.2 Toggleable fields — Full variant (23)

`PublicCode` کد عمومی · `Website` وبسایت · `Email` ایمیل · `IssuedBy` نام صادرکننده · `Position` سمت صادرکننده · `Logo` لوگوی مؤسسه · `Signature` امضا · `Stamp` مهر · `CardCode` شناسه کارت · `Besmellah` بسمه‌تعالی · `OrganizationName` تیتر هدر · `Portrait` عکس گرد هدر · `BranchName` خط ولایت · `Address` آدرس دفتر · `Phone` شماره تماس · `GuardianName` نام سرپرست · `FatherName` نام پدر · `NationalID` شماره تذکره · `RequestType` نوع مددجو · `FamilyList` فهرست اعضا · `OrphansCount` ایتام · `FamilyPhoto` عکس جمعی · `FamilyListPhotos` عکس هر عضو

### A1.3 Toggleable fields — Simple variant (15)

`Photo` عکس · `FamilyPhoto` عکس جمعی · `PublicCode` کد اختصاصی · `CaseNo` پرونده · `Province` ولایت · `District` ولسوالی · `Phone` شماره تماس · `NationalID` شماره تذکره · `GuardianName` نام سرپرست · `FatherName` نام پدر · `RelationshipToFamily` نسبت با اعضاء · `SimpleNotes` تذکرات · `Thumbprint` محل شصت · `IssueDate` تاریخ صدور · `Orphans` نام‌های ایتام

### A1.4 Text overrides — 21 keys × 7 properties = **147 settings**

Properties per key: `Content`, `Color`, `FontSizePercent`, `FontFamily`, `LineHeightPercent`, `Alignment`, `FontWeight`.

| Key | Label today | Content editable? |
|---|---|---|
| `OrganizationName` | تیترِ بزرگِ هدر | ✓ |
| `Besmellah` | بسمه‌تعالی | ✓ |
| `MottoArabic` | الله فی الایتام | ✓ |
| `MottoTranslation` | ترجمه‌ی موتو | ✓ |
| `Kicker` | نوارِ سبزِ پایینِ هدر | ✓ |
| `Phone` | شماره‌های تماس | ✓ |
| `Email` | ایمیل | ✓ |
| `ComplaintMessage` | پیامِ شکایت | ✓ |
| `FoundCardMessage` | پیامِ پیداکردنِ کارت | ✓ |
| `Address` | آدرس دفتر **(فقط رنگ/سایز)** | ✗ locked |
| `Website` | وبسایت **(فقط رنگ/سایز)** | ✗ locked |
| `GuardianName` | نامِ سرپرست **(فقط رنگ/سایز/فونت)** | ✗ locked |
| `FatherName` | نامِ پدر **(فقط رنگ/سایز/فونت)** | ✗ locked |
| `NationalID` | شماره تذکره **(فقط رنگ/سایز/فونت)** | ✗ locked |
| `RequestType` | نوعِ مددجو **(فقط رنگ/سایز/فونت)** | ✗ locked |
| `PublicCode` | کدِ عمومی **(فقط رنگ/سایز/فونت)** | ✗ locked |
| `Notice1`–`Notice5` | تذکرِ ۱–۵ | ✗ locked |

### A1.5 Other settings

Order (Full) 5 · Order (Simple) 3 · Security band order 5 · Ledger months 12

### A1.6 Total

| | Count |
|---|---|
| Direct controls | 30 |
| Toggleable Full | 23 |
| Toggleable Simple | 15 |
| Text overrides | **147** |
| Ordering | 13 |
| Ledger months | 12 |
| **TOTAL** | **~240** |

---

## A2. Duplicate configuration paths

**Three source lists contain the identical five keys:**

```
ToggleableFields      → PublicCode, GuardianName, FatherName, NationalID, RequestType
FieldOrderableKeys    → PublicCode, GuardianName, FatherName, NationalID, RequestType
TextOverrideFieldKeys → ..., GuardianName, FatherName, NationalID, RequestType, PublicCode, ...
```

To fully configure **one** field — نام سرپرست — the operator must visit:

| Intent | Tab today |
|---|---|
| Show / hide it | **2** · فیلدهای قابل‌نمایش |
| Move it up / down | **3** · ترتیبِ فیلدها |
| Colour, size, font, weight, alignment, line-height | **8** · متنِ پشتِ کارت و حدیث |

Three tabs. And tab 8 is named *"back of card text and hadith"* — where you edit the **front** card's guardian name. This is unguessable.

**Additional duplicates:**

| Setting | Appears as | And as |
|---|---|---|
| `Phone` | toggle (tab 2) | text override with content (tab 8) |
| `Email` | toggle (tab 2) | text override with content (tab 8) |
| `Address` | toggle (tab 2) | text override, content locked (tab 8) |
| `Website` | toggle (tab 2) | text override, content locked (tab 8) |
| `Besmellah` | toggle (tab 2) | text override (tab 8) |
| `OrganizationName` | toggle (tab 2) | text override (tab 8) |
| Photo position | radio (tab 3) | related to portrait scale (tab 6) |
| Family photo | toggle (tab 2) | ratio/scale/crop (tab 6) |

## A3. Technical groupings that leak implementation

| Leak | Where | Why it's wrong |
|---|---|---|
| «فیلد» throughout | tabs 2, 3 | A database word. Operators think "information on the card". |
| «(فقط رنگ/سایز/فونت)» **inside the label** | 7 override labels | Implementation constraint printed in the UI as a name |
| «قالب» vs «کارت» used interchangeably | everywhere | Two words for one thing |
| «طرحِ کامل / ساده» | tab 1 | `LayoutVariant` enum surfaced verbatim |
| «نوارِ سبزِ پایینِ هدر» (`Kicker`) | tab 8 | Describes a CSS element, not a purpose |
| «عکسِ گردِ تزئینیِ هدر» (`Portrait`) | tab 2 | Describes the shape, not what it is |
| «مقیاس ٪» spinners (50–200) | tabs 4, 6 | A CSS multiplier exposed raw |
| Tab labels prefixed `[روی]` / `[پشت]` | 4 of 9 tabs | Side-of-card encoded in a label because the model lacks the dimension |

## A4. Low-discoverability controls

| Control | Problem |
|---|---|
| **147 text-override settings** | Behind ONE combo box. **61% of the product.** No list, no overview, no indicator of which fields are customised. |
| `_lstFieldOrder` (decorative list) | Tab 3 contains a list that does nothing and says so: «ترتیبِ این لیست فقط برای سازمان‌دهیِ شماست» (:537) |
| `_chkFields` | Flat list of 23 unlabelled-by-group rows; no search, no bulk, no grouping |
| Variant-dependent field list | Same control silently shows a different set for Full vs Simple, with no signpost |
| `_numFamilyListMaxRows` | Buried at the bottom of an "images" tab |
| `_lstBandOrder` | Only appears for Full; disappears silently for Simple |
| Save / Delete / Duplicate | Bottom bar, far from every tab being edited |

---

# PART B — NEW INFORMATION ARCHITECTURE

## B1. Organising rule

> **Group by user intent. One object = one place. One task = one workflow.**

Two consequences:

**Rule 1 — Everything about a field lives with that field.** Dissolves tabs 2, 3 and 8 into محتوا. Fixes A2.
**Rule 2 — Side of card is a *view*, not a category.** Dissolves every `[روی]`/`[پشت]` prefix. Fixes A3.

## B2. Five sections — 9 tabs → 5 (−44%)

| # | Section | Contains | From |
|---|---|---|---|
| 1 | **کارت** | Name, card type, status, description, duplicate, export/import, restore-to-default, meta | Tab 1 + bottom bar |
| 2 | **ظاهر** | Colours, theme, fonts, sizes, header, footer/security band, branding, logo, backgrounds, watermark | Tabs 4 + 5 + 6 (visual parts) |
| 3 | **محتوا** | **Every field: visibility, order, format, text override, source, validation** | **Tabs 2 + 3 + 8** + 6 (layout parts) |
| 4 | **پشت کارت** | Back text, hadith, notes, payment table, extra info | Tabs 7 + 8 (back subset) |
| 5 | **تاریخچه** | Versions, restore, compare, audit trail | Tab 9 |

## B3. Renaming — technical → operator language

| Today | New |
|---|---|
| 🧾 اطلاعات کارت | **کارت** |
| 📋 [روی] فیلدهای قابل‌نمایش | *merged into* **محتوا** |
| 🔀 [روی] ترتیبِ فیلدها | *merged into* **محتوا** |
| 🎨 ظاهر کارت (هر دو رو) | **ظاهر** |
| 📷 [روی] QR Code و امنیت | *merged into* **ظاهر** |
| 🖼 لوگو و تصویر (هر دو رو) | *split:* images→**ظاهر**, layout→**محتوا** |
| 🖨 [پشت] جدولِ پرداخت | **پشت کارت** |
| 📜 [پشت] متنِ پشتِ کارت و حدیث | *merged into* **محتوا** + **پشت کارت** |
| 🕘 تاریخچهٔ نسخه‌ها | **تاریخچه** |

**Vocabulary rules:**

| Never | Always |
|---|---|
| فیلد | مورد / اطلاعات |
| قالب (mechanism) | کارت |
| Override | تغییر متن |
| Toggle / فعال‌سازی | نشان بده / نشان نده |
| مقیاس ٪ (Basic) | کوچک‌تر / بزرگ‌تر |
| طرحِ کامل / ساده | کارت کامل / کارت ساده |
| نوارِ سبزِ پایینِ هدر | زیرنویس هدر |
| عکسِ گردِ تزئینیِ هدر | عکس تزئینی هدر |
| «(فقط رنگ/سایز)» in a label | *(never in the label — shown as an ⓘ line)* |
| CSV / JSON / Variant | *never user-visible* |

---

# PART C — BASIC & ADVANCED MODE

## C1. Inclusion rule for Basic

A setting is Basic only if it passes all four:

1. **Frequency** — changed when setting up a card for a branch.
2. **Safety** — a wrong value is visibly wrong on screen, never only at print time.
3. **Self-evident** — needs no knowledge of the renderer or the database.
4. **Independence** — cannot break another setting.

## C2. Basic Mode — 13 controls

| Section | Controls |
|---|---|
| **کارت** | نام کارت · نوع کارت (کامل/ساده) · در حال استفاده |
| **ظاهر** | تم آماده · رنگ اصلی · رنگ متن · قلم · اندازهٔ متن (کوچک/متوسط/بزرگ) · لوگو |
| **محتوا** | field list, **show/hide only** |
| **پشت کارت** | جدول پرداخت (on/off) · متن پشت کارت |
| **تاریخچه** | list + restore |

**13 controls produce a complete, branded, printable card.** That is the usability target.

## C3. Advanced Mode

Everything else, **revealed in place**. Rules:

- **Never moves a control between modes.** Advanced expands sections; it never relocates anything. Nothing learned in Basic becomes wrong.
- **Never changes the section a setting lives in.**
- **Advanced values that are non-default are flagged in Basic:**
  `ⓘ ۳ تنظیم پیشرفته روی این کارت فعال است  [نمایش بده]`
  Clicking it switches to Advanced **and scrolls to the first one**. The operator is never misled about what is affecting their card.
- Basic **never strips capability** from a template — it hides controls, not values.
- Default for a new user: **Basic**. Persisted per user afterwards.

---

# PART D — WIREFRAMES

## W0 · Master frame — top navigation, preview-first

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║ کارت شناسایی — طراحی                                                              ║
║ [💾 ذخیره] [🖨 چاپ آزمایشی]  🔍[جستجوی تنظیمات…]  ( ●ساده ‖ ○پیشرفته )  [؟] [✕] ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║  [ کارت ]  [ ظاهر ]  [ محتوا ]  [ پشت کارت ]  [ تاریخچه ]        ✓ سالم           ║  ← TOP NAV
╠══════════════════════════════════════╤════════════════════════╤═══════════════════╣
║  PREVIEW — 65%                       │ SETTINGS — 35%         │ CARDS (collapse)  ║
║ ┌──────────────────────────────────┐ │ ┌────────────────────┐ │ 🔍 [جستجو…]       ║
║ │ [روی کارت][پشت کارت]   زوم ▾  ⛶ │ │ │ ★ پرکاربرد        │ │ ┌───────────────┐ ║
║ │ نمایش برای: (نمونه|واقعی)        │ │ │  رنگ اصلی  [██▾]  │ │ │ کارت اصلی     │ ║
║ │ [احمد رضایی — پروندهٔ ۱۲۴۰ ▾] ◀▶ │ │ │  اندازهٔ متن (م)  │ │ │ ★ پیش‌فرض     │ ║
║ └──────────────────────────────────┘ │ └────────────────────┘ │ └───────────────┘ ║
║                                      │ ┌────────────────────┐ │ ┌───────────────┐ ║
║        ┌────────────────────┐        │ │ contextual to the  │ │ │ کارت ساده     │ ║
║        │                    │        │ │ selection, or to   │ │ └───────────────┘ ║
║        │   THE CARD, LIVE   │        │ │ the active section │ │                   ║
║        │  true print size   │        │ │                    │ │ [+ کارت جدید]    ║
║        │  click to select   │        │ │                    │ │                   ║
║        │                    │        │ │                    │ │                   ║
║        └────────────────────┘        │ └────────────────────┘ │                   ║
╠══════════════════════════════════════╧════════════════════════╧═══════════════════╣
║ ✓ ذخیره شد · نسخهٔ ۱۲ · احمد ۱۴۰۵/۰۶/۰۹      ✓ سالم — بدون مشکل      [بررسی کارت]║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Space:** preview 65%, settings 35%, template list collapsible (reclaims its width into preview → up to 72%).
**Top nav** — five items, full Persian words, no truncation, `Ctrl+1..5`.

---

## W1 · کارت

```
┌ کارت ──────────────────────────────────────────┐
│  نام کارت                                      │
│  ┌──────────────────────────────────────────┐  │
│  │ کارت هویت ایتام — دفتر بامیان            │  │
│  └──────────────────────────────────────────┘  │
│                                                │
│  نوع کارت                                      │
│  ┌───────────────┐   ┌───────────────┐         │
│  │  ▨ thumbnail  │   │  ▨ thumbnail  │         │
│  │  ● کارت کامل  │   │  ○ کارت ساده  │         │
│  │ دو رو، با     │   │ یک رو، خلاصه  │         │
│  │ جدول پرداخت   │   │               │         │
│  └───────────────┘   └───────────────┘         │
│                                                │
│  ☑ این کارت در حال استفاده است                 │
│  ★ کارت پیش‌فرض سازمان                         │
│                                                │
│ ── پیشرفته ─────────────────────────────────── │
│  دستهٔ کارت    [عمومی            ▾]            │
│  توضیح         ┌──────────────────────────┐    │
│                └──────────────────────────┘    │
│  [کپی از این کارت] [ذخیره در فایل]             │
│  [بازیابی از فایل] [بازگشت به پیش‌فرض]         │
│                                                │
│  ساخته: مریم · ۱۴۰۵/۰۳/۱۲                     │
│  آخرین تغییر: احمد · ۱۴۰۵/۰۶/۰۹ · نسخهٔ ۱۲    │
└────────────────────────────────────────────────┘
```

Replaces the `LayoutVariant` dropdown with picture choices — recognition, not recall.

---

## W2 · ظاهر — Basic

```
┌ ظاهر ──────────────────────────────────────────┐
│  تم آماده                                      │
│  ┌───┐┌───┐┌───┐┌───┐┌───┐                     │
│  │███││███││███││███││███│                      │
│  └───┘└───┘└───┘└───┘└───┘                     │
│   ●سازمانی  آبی  سبز  خنثی  پرکنتراست          │
│                                                │
│  رنگ اصلی      [██ سرمه‌ای  ▾]                 │
│  رنگ متن       [██ مشکی     ▾]                 │
│  قلم           [بی‌نازنین   ▾]                 │
│  اندازهٔ متن   ( کوچک )( ●متوسط )( بزرگ )      │
│                                                │
│  لوگوی سازمان                                  │
│  ┌────────┐                                    │
│  │ [عکس]  │  [تعویض] [حذف]                     │
│  └────────┘                                    │
│                                                │
│  ⓘ ۲ تنظیم پیشرفته روی این کارت فعال است       │
│    [نمایش بده]                                 │
└────────────────────────────────────────────────┘
```

Colours are **named**, not hex. Text size is three words, not a 50–200 spinner.

## W2b · ظاهر — Advanced (same section, expanded in place)

```
│  … all Basic controls, unchanged, still above …│
│                                                │
│ ▼ رنگ‌های بیشتر                                │
│   رنگ فرعی     [██ #3E7CB1]                    │
│   رنگ پس‌زمینه [██ سفید   ]                    │
│   رنگ نوار بالا[██ سرمه‌ای] کنتراست ۷.۲:۱ ✓    │
│                                                │
│ ▼ اندازه‌ها                                    │
│   اندازهٔ متن      [───●───] ۱۰۰٪              │
│   ارتفاع نوار بالا [───●───] ۱۰۰٪              │
│                                                │
│ ▼ تصویرها                                      │
│   ┌───────┐ ┌───────┐ ┌────────┐               │
│   │ روی   │ │ پشت   │ │واترمارک│               │
│   │ [عکس] │ │ [عکس] │ │ [عکس]  │               │
│   │تعویض ✕│ │تعویض ✕│ │شفافیت  │               │
│   │       │ │       │ │[──●─]۱۵٪│              │
│   └───────┘ └───────┘ └────────┘               │
│   ⚠ فایل واترمارک پیدا نشد    [انتخاب دوباره]  │
│                                                │
│ ▼ نوار پایین کارت                              │
│   ☐ QR Code   ☑ بارکد   ☑ هولوگرام             │
│   ترتیب (بکشید یا ▲▼):                         │
│   [بارکد ⋮⋮][امضا ⋮⋮][مهر ⋮⋮][هولوگرام ⋮⋮]     │
└────────────────────────────────────────────────┘
```

---

## W3 · محتوا — **field cards** (the section that fixes A2)

```
┌ محتوا ─────────────────────────────────────────┐
│ 🔍[جستجوی مورد…]   نمایش:(همه)(روشن)(خاموش)    │
│                              ۲۰ از ۲۳ روشن     │
├────────────────────────────────────────────────┤
│ ▼ مشخصات سرپرست (۵)              [همه] [هیچ]   │
│  ┌──────────────────────────────────────────┐  │
│  │ ☑ نام سرپرست                    ⚙  ⋮⋮   │  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │ ☑ نام پدر سرپرست                ⚙  ⋮⋮   │  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │ ☑ شماره تذکرهٔ سرپرست           ⚙  ⋮⋮   │  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │ ☐ نوع مددجو                     ⚙  ⋮⋮   │  │
│  └──────────────────────────────────────────┘  │
│                                                │
│ ▼ عکس‌ها (۳)                     [همه] [هیچ]   │
│ ▼ خانواده (۳)                    [همه] [هیچ]   │
│ ▼ هدر کارت (۴)                   [همه] [هیچ]   │
│ ▼ سازمان و تماس (۶)              [همه] [هیچ]   │
│ ▼ امضا و مهر (۴)                 [همه] [هیچ]   │
└────────────────────────────────────────────────┘
```

## W3b · One field card, expanded — **everything about that field**

```
┌──────────────────────────────────────────────┐
│ ☑ نام سرپرست                        ⚙▲  ⋮⋮  │
├──────────────────────────────────────────────┤
│  ☑ روی کارت نشان بده                         │
│  ☑ در چاپ هم بیاید                           │
│                                              │
│  جای این مورد                                │
│  [▲ بالاتر]  [▼ پایین‌تر]        ۲ از ۵      │
│                                              │
│ ── پیشرفته ────────────────────────────────  │
│  رنگ    [██ ▾]      اندازه  [──●──] ۱۰۰٪     │
│  قلم    [پیش‌فرض ▾] ضخامت  [متوسط  ▾]        │
│  چینش   (راست)(وسط)(چپ)                      │
│  فاصلهٔ خط [──●──] ۱۰۰٪                       │
│                                              │
│  متن این مورد                                │
│  ⓘ از پروندهٔ مددجو می‌آید و اینجا تغییر     │
│    نمی‌کند.                                  │
│                                              │
│  منبع اطلاعات                                │
│  پروندهٔ مددجو ← نام سرپرست          ✓ سالم  │
│                                              │
│                          [بازنشانی این مورد] │
└──────────────────────────────────────────────┘
```

**This one card replaces tabs 2, 3 and 8 for that field.** Note:

- **Source mapping in plain language** — «پروندهٔ مددجو ← نام سرپرست», not `TblCase.CaseName`. The technical name appears only in a tooltip, for IT.
- **Validation status** per field (`✓ سالم` / `⚠ ستون پیدا نشد`).
- The `ⓘ` line **explains** the content-lock instead of showing a mysteriously disabled textbox — and removes «(فقط رنگ/سایز/فونت)» from the label entirely (fixes A3).
- For unlocked keys (بسمه‌تعالی, شعار, پیام شکایت…) that `ⓘ` is replaced by an editable **متن** box.

---

## W4 · پشت کارت

Selecting this section **auto-flips the preview to the back** — nobody edits what they cannot see.

```
┌ پشت کارت ──────────────────────────────────────┐
│  ☑ جدول پرداخت را چاپ کن                       │
│                                                │
│  متن پشت کارت / حدیث                           │
│  ┌──────────────────────────────────────────┐  │
│  │                                          │  │
│  └──────────────────────────────────────────┘  │
│  رنگ [██▾]   اندازه [──●──] ۱۰۰٪               │
│                                                │
│  پیام شکایت           [ویرایش]                 │
│  پیام پیدا شدن کارت   [ویرایش]                 │
│  یادداشت‌های اضافی    [ویرایش]                 │
│                                                │
│ ── پیشرفته ─────────────────────────────────── │
│  کدام ماه‌ها چاپ شوند؟      [همه] [هیچ‌کدام]   │
│  ☑حمل  ☑ثور  ☑جوزا  ☑سرطان  ☑اسد   ☑سنبله      │
│  ☑میزان ☑عقرب ☑قوس  ☑جدی    ☑دلو   ☑حوت        │
└────────────────────────────────────────────────┘
```

---

## W5 · تاریخچه

```
┌ تاریخچه ───────────────────────────────────────┐
│ ● نسخهٔ ۱۲ — احمد — ۱۴۰۵/۰۶/۰۹ ۱۰:۴۲          │
│   رنگ اصلی، اندازهٔ متن، «نوع مددجو» روشن شد   │
│                             [بازگرداندن]       │
│ ○ نسخهٔ ۱۱ — مریم — ۱۴۰۵/۰۶/۰۱                │
│   لوگو تعویض شد             [بازگرداندن]       │
│ ○ نسخهٔ ۱۰ — مریم — ۱۴۰۵/۰۵/۲۸                │
│                             [بازگرداندن]       │
│                                                │
│ ── پیشرفته ─────────────────────────────────── │
│  دو نسخه را انتخاب کنید:         [مقایسه]      │
│  [📄 خروجی گزارش تغییرات]                      │
└────────────────────────────────────────────────┘
```

Restore states: «نسخهٔ فعلی حفظ می‌شود و چیزی پاک نمی‌شود» — true, because `RestoreSelectedVersion` already saves forward.

---

## W6 · Settings search (global)

`Ctrl+F` or the header box. Searches **all five sections**, across labels, synonyms, and field names.

```
🔍 [ QR                                    ]
┌──────────────────────────────────────────┐
│ ظاهر › نوار پایین کارت                   │
│   ☐ QR Code                    [برو]     │
│                                          │
│ محتوا › امضا و مهر                       │
│   ترتیب نوار پایین             [برو]     │
│                                          │
│ ⓘ «QR» خاموش است روی این کارت            │
└──────────────────────────────────────────┘
```

- Matches Persian labels, English keys (`QRCode`), and synonyms («بارکد»→بارکد/QR, «رنگ»→ all colour settings).
- `[برو]` switches section, expands the group **and the Advanced disclosure if needed**, then flashes the control.
- Search results state current value/status inline, so simple questions ("is QR on?") are answered without navigating at all.

---

## W7 · Favorites — پرکاربرد

Pinned settings appear at the top of the settings panel in **every** section.

```
┌ ★ پرکاربرد ────────────────────── [ویرایش] ┐
│  رنگ اصلی        [██ سرمه‌ای ▾]            │
│  اندازهٔ متن     (کوچک)(●متوسط)(بزرگ)      │
│  لوگوی سازمان    [تعویض]                   │
└────────────────────────────────────────────┘
```

- Any control gets a ☆ on hover; clicking pins it.
- Per user, persisted. Max 6 — beyond that the panel stops being a shortcut.
- **Seeded with sensible defaults** for a first-time user (رنگ اصلی، اندازهٔ متن، لوگو) so it is useful before anyone customises it.
- Empty state: «تنظیمات پرکاربرد خود را اینجا سنجاق کنید ☆».

---

## W8 · Template health check

Status lives permanently in the status bar; the panel opens from it.

```
┌ بررسی کارت ────────────────────────────────────┐
│  ✕ ۱ خطا    ⚠ ۲ هشدار    ⓘ ۱ پیشنهاد          │
├────────────────────────────────────────────────┤
│ ✕ فایل لوگو پیدا نشد                           │
│   logo-bamyan.png                              │
│                        [انتخاب دوباره] [برو]   │
│                                                │
│ ⚠ «شماره تذکره» روشن است ولی در پرونده‌ها      │
│   خالی است — روی کارت جای خالی می‌ماند         │
│                                       [برو]    │
│                                                │
│ ⚠ اندازهٔ متن ۱۴۵٪ است — احتمال سرریز          │
│   «نام سرپرست» روی کارت چاپی                   │
│                                       [برو]    │
│                                                │
│ ⓘ QR Code خاموش است. برای اعتبارسنجی کارت     │
│   می‌توانید روشنش کنید.                        │
│                                       [برو]    │
├────────────────────────────────────────────────┤
│              [بررسی دوباره]  [📄 خروجی گزارش]  │
└────────────────────────────────────────────────┘
```

**Status bar states:** `✓ سالم` · `⚠ ۲ هشدار` · `✕ ۱ خطا`.
**Rule:** errors block **publish** (making a card default/active) but **never block save**. An operator must always be able to save unfinished work — blocking save loses work and teaches people to fear the tool.

---

## W9 · Selection — clicking the card

```
   PREVIEW                        SETTINGS
┌────────────────────┐   ┌──────────────────────────┐
│  ┌──────────────┐  │   │ ← بازگشت به «محتوا»      │
│  │╔════════════╗│   │   ├──────────────────────────┤
│  │║نام: احمد   ║│◄──┼───┤  نام سرپرست              │
│  │╚════════════╝│   │   │  …identical to W3b…      │
│  └──────────────┘  │   └──────────────────────────┘
└────────────────────┘
```

Same control set as W3b — learned once, reached two ways. The «← بازگشت» link preserves the section model so nobody gets lost.

---

## W10 · Empty & error states

| State | Copy |
|---|---|
| No templates | «هنوز کارتی ساخته نشده.» + blank-card illustration + [ساخت اولین کارت] |
| No cases in DB | «برای نمایش، حداقل یک پرونده لازم است. فعلاً از دادهٔ نمونه استفاده می‌شود.» — preview still renders |
| Preview loading | Skeleton card outline, never a blank panel |
| Image missing | Red badge on the slot: «فایل پیدا نشد» + [انتخاب دوباره] |
| Unsaved on close | «تغییرات ذخیره نشده. [ذخیره و خروج][خروج بدون ذخیره][ادامهٔ ویرایش]» |
| Field off but styled | Card greys; tooltip «چون این مورد خاموش است، این تنظیمات اثری ندارند» |
| Search no results | «چیزی پیدا نشد. شاید منظورتان: رنگ، قلم، لوگو» |

---

# PART E — NAVIGATION MAP

## E1. Routes to every setting

Each setting is reachable **four ways**. All four land in the same place.

```
                    ┌──────────────────────────┐
                    │      ANY SETTING         │
                    └────────────▲─────────────┘
        ┌────────────────┬───────┴────────┬──────────────────┐
        │                │                │                  │
   ① TOP NAV        ② CLICK CARD     ③ SEARCH          ④ FAVORITES
   Ctrl+1..5        click element    Ctrl+F            ★ panel
   → section        → its card       → jump + flash    → inline
   → group          (W9)             (W6)              (W7)
   → card (W3b)
```

## E2. Full map

```
TOP NAV ─┬─ [کارت]      Ctrl+1
         │    ├─ نام / نوع / وضعیت                    (Basic)
         │    └─ ▸ پیشرفته: دسته، توضیح، کپی، خروجی، ورودی، بازگشت
         │
         ├─ [ظاهر]      Ctrl+2
         │    ├─ تم آماده / رنگ اصلی / رنگ متن / قلم / اندازه / لوگو  (Basic)
         │    ├─ ▸ رنگ‌های بیشتر: فرعی، پس‌زمینه، نوار بالا
         │    ├─ ▸ اندازه‌ها: متن ٪، ارتفاع نوار بالا
         │    ├─ ▸ تصویرها: پس‌زمینهٔ رو/پشت، واترمارک + شفافیت
         │    └─ ▸ نوار پایین: QR، بارکد، هولوگرام، ترتیب
         │
         ├─ [محتوا]     Ctrl+3
         │    ├─ جستجوی مورد + فیلتر روشن/خاموش + [همه]/[هیچ]
         │    └─ گروه‌ها ─► field card ─► ⚙ expand
         │         ├─ مشخصات سرپرست (۵)      نمایش | ترتیب | قالب‌بندی
         │         ├─ عکس‌ها (۳)              نمایش | اندازه | نسبت | برش
         │         ├─ خانواده (۳)             نمایش | سقف ردیف
         │         ├─ هدر کارت (۴)            نمایش | متن | قالب‌بندی
         │         ├─ سازمان و تماس (۶)       نمایش | متن | قالب‌بندی
         │         └─ امضا و مهر (۴)          نمایش | ترتیب
         │
         ├─ [پشت کارت]  Ctrl+4   → preview auto-flips
         │    ├─ جدول پرداخت on/off · متن پشت/حدیث         (Basic)
         │    └─ ▸ پیشرفته: ماه‌ها (۱۲)، پیام‌ها، یادداشت
         │
         └─ [تاریخچه]   Ctrl+5
              ├─ فهرست نسخه‌ها + بازگرداندن                (Basic)
              └─ ▸ پیشرفته: مقایسه، خروجی گزارش

ALWAYS VISIBLE (not in the nav):
  ★ پرکاربرد        top of the settings panel, every section
  🔍 جستجوی تنظیمات  header, Ctrl+F
  ● ساده/پیشرفته    header
  ✓ وضعیت سلامت     status bar → W8
  پیش‌نمایش          centre, 65% — never covered
```

## E3. Keyboard map

| Key | Action |
|---|---|
| `Ctrl+1..5` | Sections |
| `Ctrl+F` | Settings search |
| `Ctrl+S` | Save |
| `Ctrl+P` | Test print |
| `F5` | Refresh preview |
| `F11` | Fullscreen preview |
| `Ctrl+Shift+A` | Toggle Basic / Advanced |
| `Tab` / `Shift+Tab` | RTL reading order |
| `Space` | Toggle focused field |
| `Alt+↑/↓` | Move focused field's order |
| `Enter` | Expand/collapse focused field card |
| `Esc` | Deselect / close panel |

**Every canvas action has a keyboard equivalent** — drag always has ▲▼ beside it. The canvas is never the only path.

---

# PART F — WORKFLOW MAP

## F1. Task 1 — Create a branch card (most common)

```
[کارت جدید] → کپی از «کارت اصلی»
   ↓
① کارت      نام: «دفتر بامیان»                       2 clicks
   ↓
② ظاهر      تم آماده «آبی» → لوگو [تعویض]            3 clicks
   ↓
③ محتوا     ☐ وبسایت  ☐ ایمیل                        2 clicks
   ↓
   preview  نمایش برای: [احمد رضایی]                  2 clicks
   ↓
   ✓ سالم   → [💾 ذخیره]                              1 click
                                          ────────────────────
                          TOTAL: 10 clicks · 3 sections · Basic only
```

**Today: 7 tabs, ~28 clicks.** → **−64% clicks, −57% sections.**

## F2. Task 2 — Fully configure one field

```
③ محتوا → 🔍 «نام سرپرست» → ⚙
   ┌─ ONE CARD ────────────────────┐
   │ ☑ نمایش   ▲▼ ترتیب            │
   │ رنگ · اندازه · قلم · ضخامت    │
   │ چینش · فاصله · منبع · وضعیت   │
   └───────────────────────────────┘
   TOTAL: 1 section, ~5 clicks
```

**Today: 3 tabs (2 → 3 → 8), ~14 clicks.** → **−64% clicks, 3 places → 1.**

## F3. Task 3 — Fix a reported print problem

```
status bar  ⚠ ۲ هشدار → [بررسی کارت]
   ↓
W8 panel    «اندازهٔ متن ۱۴۵٪ — احتمال سرریز» → [برو]
   ↓
lands in ظاهر › اندازه‌ها, Advanced auto-expanded, control flashing
   ↓
fix → preview updates → ✓ سالم → ذخیره
                                          ────────────────────
                                    TOTAL: 4 clicks, no hunting
```

**Today: the problem is only discovered after printing.**

## F4. Task 4 — "Where is the QR setting?"

```
Ctrl+F → «QR» → result shows: ظاهر › نوار پایین · currently OFF → [برو]
                                          ────────────────────
                             TOTAL: 1 shortcut + 1 click (or 0 — the
                             answer was in the result line)
```

**Today: unguessable — it lives in a tab titled «QR Code و امنیت» which also has to be found.**

## F5. Task 5 — Print for a specific beneficiary

```
preview → نمایش برای: (●واقعی) → [جستجو: احمد رضایی] → ◀▶ check 3 records
   ↓
⛶ fullscreen → verify → [🖨 چاپ آزمایشی]
                                          TOTAL: 5 clicks
```

**Today: impossible — the preview shows an arbitrary record.**

## F6. Coverage

| Operator task | Sections visited | Mode |
|---|---|---|
| Create a branch card | 3 | Basic |
| Change colours/logo | 1 | Basic |
| Hide/show information | 1 | Basic |
| Reorder information | 1 | Advanced |
| Change one field's look | 1 | Advanced |
| Edit back of card | 1 | Basic |
| Fix a health warning | 1 | either |
| Restore an old version | 1 | Basic |
| Preview a real record | 0 (always visible) | either |

**No task requires more than 3 sections. Six of nine require one.**

---

# PART G — MIGRATION STRATEGY

## G1. Data — no migration required

`TblCardTemplate.DesignJson` is an open JSON blob deserialised by `JavaScriptSerializer` into `CardTemplateDesign`.

- **Unknown JSON keys are ignored on read.**
- **Missing keys take C# property defaults.**

Therefore the redesign reads and writes existing templates unchanged. **No `ALTER TABLE`. No new tables. No data rewrite. No backfill.**

| Asset | Treatment |
|---|---|
| Existing `DesignJson` | Loads unchanged |
| Existing `FieldsJson` | Loads unchanged — same keys |
| Existing version snapshots | Load unchanged |
| `IsDefault` template | Never deletable (`DELETE … AND IsDefault = 0`) — preserved |
| `PrintProfile`, `TemplateType` | Preserved, surfaced under کارت › Advanced |

## G2. New keys — UI-only, additive, optional

Nothing in this phase requires a new *design* key. The three new UI features store **per-user preferences**, not template data:

| Preference | Scope | Storage |
|---|---|---|
| Basic/Advanced mode | per user | app settings |
| Pinned favourites | per user | app settings |
| Preview record + zoom + splitter positions | per user | app settings |

**No template is modified by any of them.** A template edited on one machine renders identically on another.

## G3. Operator migration

1. **Ship behind a toggle.** «طراح جدید» opens the new form; the old form stays for one release. Choice remembered.
2. **First-run tour** — 5 coach marks: preview · top nav · Basic/Advanced · search · save.
3. **«کجا رفت؟» map** — a searchable old-tab → new-section table in the ? menu, built directly from the A1 inventory table. An experienced operator is never lost.
4. **Retire the old form** only after one full release with no fallbacks recorded.

## G4. Rollback

No schema change and no destructive rewrite ⇒ rollback is shipping the previous binary. Templates saved by the new designer remain fully loadable by the old one.

---

# PART H — BACKWARD COMPATIBILITY STRATEGY

## H1. Guarantees

| # | Guarantee | Mechanism |
|---|---|---|
| B1 | Every existing template renders **pixel-identically** | No renderer change; `GuardianCard/` untouched; same `CardTemplateDesign` → same injected CSS |
| B2 | No functionality removed | A1 inventory maps all ~240 settings to a new home — verified line by line |
| B3 | No database migration | `DesignJson` / `FieldsJson` are blobs; new UI state is per-user, not per-template |
| B4 | Version history intact | `TblCardTemplateVersion` untouched; save→snapshot stays in one transaction |
| B5 | Print output unchanged | Preview zoom is `WebView2.ZoomFactor` (screen raster only); print goes via `ShowPrintUI` + `print.css` in `mm` |
| B6 | Both variants preserved | Full/Simple keep separate field sets; محتوا shows the right set with a visible label (fixes A4) |
| B7 | Import/export format unchanged | Same JSON contract — templates exchange between old and new builds |
| B8 | Multi-centre scope respected | Record selector reuses the existing permission-filtered case query, never a raw `SELECT *` |

## H2. Renderer freeze

The rule «هیچ فایلی داخل پوشه GuardianCard تغییر نمی‌کند» is **preserved absolutely**. This phase is IA and WinForms UI only — it does not touch `GuardianCardRenderer`, the staging pipeline, or the frozen package.

Click-to-select (W9) is the one place needing renderer cooperation. It uses **the existing anchor-injection mechanism** (`ApplyDesignOverrides` already injects `<style>`/`<script>` into the *staged copy*) and is active only in designer mode. `GuardianCard/` stays byte-identical. **If this proves risky in build, W9 is dropped without affecting anything else** — routes ①③④ in E1 still reach every setting.

## H3. Verification gate

Before any UI work merges:

1. **Golden-render suite** — every existing template × both variants rendered to PDF, geometry hashed. Every later change must reproduce the hashes. This is the contract behind B1 and B5.
2. **Round-trip test** — old `DesignJson` → model → serialise → model, asserted equal.
3. **Inventory test** — an automated check that every key in `ToggleableFields`, `ToggleableFieldsSimple`, `TextOverrideFieldKeys`, `FieldOrderableKeys` and `SecurityBandOrderableKeys` is reachable in the new UI. **This is what mechanically enforces "no lost functionality".**

---

# PART I — DESKTOP UX STANDARDS

Reference quality: Dynamics 365 · ERPNext · Odoo · Power BI · Adobe property panels.

| Standard | Specification |
|---|---|
| Direction | RTL-first: `RightToLeft.Yes` + `RightToLeftLayout`; right-origin; RTL tab order |
| Click targets | Minimum **32×32 px**; list rows **40 px**; nav items **44 px** |
| Spacing | 8 px base grid. Section padding 16. Control gap 12. Group gap 24. |
| Section headers | Identical treatment everywhere: 13 pt semibold, 16 px top margin, 8 px bottom, 1 px bottom rule |
| Labels | Right-aligned, fixed 120 px column, consistent across all five sections |
| Advanced disclosure | Always `── پیشرفته ──` rule + chevron. Same visual, every section. |
| Colours | Existing `UiTheme` palette — no new colour system |
| Fonts | `UiTheme.Font()`; honours `UiTheme.SizeScale` for 100/125/150% |
| Focus | 2 px visible ring on **every** control, including owner-drawn lists (missing today) |
| Status | Never colour-only — ✓ / ⚠ / ✕ glyphs always accompany |
| Numerals | Persian numerals in UI text; Solar month names |
| Minimum window | 1280×800; splitters persisted; panels collapsible |
| Latency | Preview updates ≤ 150 ms after a control change (today: 500 ms + full re-stage) |
| Modality | No modal dialog ever covers the preview |

---

# PART J — SCOPE & PRIORITY

## J1. In scope — this phase

IA restructure (9→5) · renaming · Basic/Advanced · preview-first 65% · field cards · settings search · favourites · health check · real-data preview · top navigation · empty/error states · desktop standards.

## J2. Explicitly deferred

| Deferred | Why |
|---|---|
| Free X/Y drag layout (`LayoutMode="Free"`) | Adds capability *and* risk — the opposite of this phase's goal. Also impossible without an additive layer on the frozen flow-based renderer. |
| Full DB mapping inspector (standalone) | Per-field source line in W3b covers the operator need. The admin tool is a separate phase. |
| Visual version diff | The plain-language change summary in W5 delivers most of the value at a fraction of the cost. |
| Undo/redo | **Genuinely valuable, still deferred** — it is a cross-cutting architectural change, not an IA change. W10's unsaved-changes handling and per-card [بازنشانی] cover the common cases meanwhile. |
| Cross-template audit trail | Admin reporting, not operator UX. |

## J3. The one thing added because it *removes* work

**Theme presets (W2).** One click replaces four separate colour decisions. It reduces surface rather than adding it — which is why it belongs in a simplification phase.

---

# PART K — APPROVAL CHECKLIST

- [ ] **A1 inventory is complete** — no setting missing from the ~240
- [ ] **A2 scatter fix** (one field = one place) is the right organising rule
- [ ] **5 sections + names** (کارت · ظاهر · محتوا · پشت کارت · تاریخچه) read correctly to a real operator
- [ ] **Basic Mode's 13 controls** are the right 13
- [ ] **Preview at 65%** is the right allocation
- [ ] **Top navigation** confirmed
- [ ] **W3b field card** is approved as the core interaction
- [ ] **Search + favourites + health check** scope is agreed
- [ ] **Deferred list (J2)** is agreed — especially undo/redo
- [ ] **Verification gate (H3)** is accepted as the merge condition

**Recommended before coding:** paper-test W1, W3 and W3b with two real operators. One hour will find more than a week of review.

**Success criterion:** an untrained NGO operator duplicates the default card, renames it, changes its colour and logo, hides two items, previews a real beneficiary, and prints a test — **without opening Advanced Mode and without asking anyone.**
