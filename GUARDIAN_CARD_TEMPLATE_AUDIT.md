# GUARDIAN CARD TEMPLATE MANAGEMENT & SECURITY — PHASE 2 AUDIT

## ممیزیِ پیش از پیاده‌سازی

| | |
|---|---|
| **Report Version** | 1.0 |
| **Date** | 2026-08-24 |
| **Scope of this document** | فقط ممیزی/تحلیل — هیچ کدی هنوز تغییر نکرده |
| **Prior phases** | Phase 1 (Card Designer: ترتیب فیلد، کنترل‌های سبک، هماهنگیِ کامل/ساده) تکمیل و تأییدشده |

---

## ۱) معماریِ فعلی (مرتبط با این فاز)

### ۱.۱ جدولِ قالب‌ها — `TblCardTemplate`
```sql
CREATE TABLE TblCardTemplate (
    TemplateID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name       TEXT NOT NULL UNIQUE,
    FieldsJson TEXT NOT NULL,
    IsDefault  INTEGER NOT NULL DEFAULT 0,
    CreatedAt  TEXT NOT NULL DEFAULT (datetime('now')),
    DesignJson TEXT NULL,               -- افزوده در فازی قبل‌تر
    LayoutVariant TEXT NOT NULL DEFAULT 'Full'  -- افزوده در فازی قبل‌تر (Full/Simple)
);
```
هیچ ستونِ نسخه/وضعیت/نوع/توضیح/سازنده/ویرایش‌کننده‌ای وجود ندارد.

### ۱.۲ لایه‌های موجود
- **`CardTemplateRepository.cs`** — `GetAll()`/`GetById()`/`Save()`/`Delete()` (حذفِ فیزیکی، بدونِ IsActive)، به‌علاوهٔ `CardTemplateDesign`/`TextFieldOverride` (که در Phase 1 گسترش یافت: رنگ/فونت/ترتیبِ فیلد/تراز/وزن).
- **`FrmCardTemplateManager.cs`** — فرمِ مدیریتِ قالب (۱۴۷۸+ خط، ۸ تب). دارد: فهرستِ قالب‌ها + جستجو، قالبِ تازه، ذخیره، حذف (بدونِ تأییدِ دو-مرحله‌ای صریح — نیاز به بررسی دقیق‌تر در کد)، Export/Import (JSON خودکفا با base64)، پیش‌نمایشِ زندهٔ WebView2. **ندارد:** Duplicate صریح، Rename مجزا (فقط از طریقِ ویرایشِ کلی)، Status Active/Inactive، Type/Description، تاریخچهٔ نسخه، Compare.
- **`GuardianCardRenderer.cs`** — لایهٔ رندر (فقط پوشهٔ کاریِ یک‌بارمصرف را دست می‌زند، هرگز `GuardianCard/` را). **این فاز نباید این فایل را لمس کند مگر ضروری باشد** (طبقِ دستورِ صریحِ کاربر).

### ۱.۳ مجوز (Permission)
- `PermissionService` (ماتریسِ نقش/کاربر، با fallback ایمنِ قدیمی برایِ کلیدهایِ ناشناخته — یعنی افزودنِ کلیدِ جدید هرگز چیزی را از روزِ اول قفل نمی‌کند).
- مجوزهایِ موجودِ کارتِ شناسایی: `GuardianCard.Print` (Admin/Operator/Viewer=مجاز) و `GuardianCard.ManageTemplates` (فقط Admin) — هر دو در `EnterpriseInitializer.EnsureDefaultPermissions` با کمکِ `AddPermission(...)` ثبت شده‌اند؛ الگویِ ثبتِ مجوزِ جدید کاملاً مشخص و آماده‌استفاده است.
- `FrmCardTemplateManager` و `FrmGuardianCardBatchPrint` هر دو در سازنده‌شان `PermissionService.Require(...)` را چک می‌کنند (دفاعِ لایه‌ای، حتی اگر فرم مستقیم ساخته شود).

### ۱.۴ Audit Log
- `AuditLogger.Log(operation, entityName, entityId, oldValue, newValue)` از قبل کاملاً آماده و در حالِ استفاده در بخش‌هایِ دیگرِ برنامه است (می‌نویسد در `TblAuditLog`: UserID/Username/Operation/EntityName/EntityID/OldValue/NewValue/CreatedAt/CenterID).
- **کارتِ شناسایی هیچ‌جا این را صدا نمی‌زند** — نه در Save، نه در Delete، نه در Print.

### ۱.۵ الگویِ موجودِ «تاریخچه» (پیش از این هم استفاده شده)
`TblFamilyRoleHistory` نمونهٔ دقیقاً مشابهی از یک جدولِ تاریخچه است که پیش‌تر برای موردی دیگر ساخته شده:
```sql
CREATE TABLE TblFamilyRoleHistory (
    RoleHistoryID     INTEGER PRIMARY KEY AUTOINCREMENT,
    FamilyMemberID    INTEGER NOT NULL,
    OldRole           TEXT NULL,
    NewRole           TEXT NOT NULL,
    ChangedByUserID   INTEGER NULL,
    ChangedByUsername TEXT NULL,
    ChangedAt         TEXT NOT NULL DEFAULT (datetime('now')),
    Notes             TEXT NULL,
    CONSTRAINT FK... ON DELETE CASCADE
);
```
این دقیقاً همان الگویی است که برایِ «تاریخچهٔ نسخه‌هایِ قالب» پیشنهاد می‌شود — نه یک مکانیزمِ جدید.

---

## ۲) محدودیت‌هایِ فعلی (نگاشتِ مستقیم به درخواستِ کاربر)

| درخواست | وضعِ فعلی |
|---|---|
| چند قالب، نام/نوع/وضعیت/توضیح | فقط نام + IsDefault موجود است؛ Type/Description/IsActive/CreatedBy/ModifiedAt/ModifiedBy هیچ‌کدام وجود ندارند |
| Duplicate/Rename صریح | هیچ‌کدام دکمهٔ مجزا ندارند (Rename با ویرایشِ فیلدِ نام + Save قابل‌انجام است، ولی Duplicate اصلاً مسیر ندارد) |
| نسخه‌بندی | صفر — Save فعلی رکورد را **جای‌گزین (UPDATE in place)** می‌کند؛ هیچ تاریخچه‌ای نگه داشته نمی‌شود |
| Audit Log | صفر — `AuditLogger` هیچ‌جایِ این ماژول صدا زده نمی‌شود |
| مجوزهایِ دقیق‌تر | فقط دو مجوزِ خشن (`Print` / `ManageTemplates`) — بدونِ تفکیکِ Create/Edit/Delete/Activate |
| جداسازیِ روی/پشتِ کارت | یک قالب = یک `FieldsJson`/`DesignJson`؛ روی/پشت از هم به‌طورِ کامل مستقل نیستند (توضیحِ کامل در بخشِ ۴) |
| پروفایل‌هایِ چاپ (PVC/A4/چندکارت‌درصفحه) | صفر — `print.css` همیشه یک اندازهٔ ثابت (۲۱۶×۱۵۴٫۵mm، یک کارت در هر صفحه) دارد |

---

## ۳) تغییراتِ لازمِ دیتابیس (پیشنهادی، همه Additive)

همه با `EnsureColumn`/`CREATE TABLE IF NOT EXISTS` — دقیقاً همان الگویِ امنِ موجود در `DatabaseInitializer.cs`؛ هیچ ستون/جدولِ موجود تغییر نوع/حذف نمی‌شود، پس دادهٔ فعلی هرگز در معرضِ خطر نیست.

```sql
-- ستون‌هایِ جدید روی TblCardTemplate (مدیریتِ حرفه‌ای‌تر، بخش ۱)
ALTER TABLE TblCardTemplate ADD COLUMN TemplateType TEXT NULL;      -- «کارت ایتام»/«کارت مددجو»/... (متنِ آزاد یا Lookup)
ALTER TABLE TblCardTemplate ADD COLUMN Description  TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN IsActive     INTEGER NOT NULL DEFAULT 1;  -- قالب‌هایِ موجود = فعال (بدونِ تغییرِ رفتار)
ALTER TABLE TblCardTemplate ADD COLUMN CreatedBy    TEXT NULL;      -- NULL برایِ رکوردهایِ قدیمی (نامعلوم، صادقانه)
ALTER TABLE TblCardTemplate ADD COLUMN ModifiedAt   TEXT NULL;
ALTER TABLE TblCardTemplate ADD COLUMN ModifiedBy   TEXT NULL;

-- جدولِ تازهٔ تاریخچهٔ نسخه‌ها (بخش ۲) — هم‌الگویِ TblFamilyRoleHistory
CREATE TABLE IF NOT EXISTS TblCardTemplateVersion (
    VersionID         INTEGER PRIMARY KEY AUTOINCREMENT,
    TemplateID        INTEGER NOT NULL,
    VersionNumber     INTEGER NOT NULL,
    Name              TEXT NOT NULL,     -- عکسِ لحظه‌ای (Snapshot) از همهٔ ستون‌هایِ قالب
    FieldsJson        TEXT NOT NULL,
    DesignJson        TEXT NULL,
    LayoutVariant     TEXT NOT NULL,
    TemplateType      TEXT NULL,
    Description       TEXT NULL,
    ChangedByUserID   INTEGER NULL,
    ChangedByUsername TEXT NULL,
    ChangedAt         TEXT NOT NULL DEFAULT (datetime('now')),
    ChangeNote        TEXT NULL,         -- توضیحِ کوتاهِ خودکار یا دستیِ کاربر
    CONSTRAINT FK_CardTemplateVersion_Template FOREIGN KEY (TemplateID)
        REFERENCES TblCardTemplate (TemplateID) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_TblCardTemplateVersion_TemplateID ON TblCardTemplateVersion(TemplateID, VersionNumber);
```

**دربارهٔ «Store: Changed fields, Changed settings»:** به‌جای یک ستونِ ساختاریافتهٔ جداگانه برایِ «فقط فیلدهایِ تغییریافته» (که یک موتورِ diff جدید و پیچیده می‌طلبد)، هر نسخه یک Snapshot کامل نگه می‌دارد؛ فهرستِ «چه چیزی عوض شد» با مقایسهٔ دو Snapshot **در لحظهٔ نمایش** (نه در لحظهٔ ذخیره) محاسبه می‌شود. این دقیقاً همان کاری است که «Compare versions» نیاز دارد و یک موتورِ ذخیره‌سازیِ دوم نمی‌سازد.

```sql
-- ثبتِ مجوزهایِ تازه (بخش ۴) — با AddPermission موجود در EnterpriseInitializer، بدونِ جدولِ جدید
GuardianCard.Template.View      (Admin, Operator, Viewer = مجاز — هم‌ارزِ سطحِ دسترسیِ مشاهده‌ایِ فعلی)
GuardianCard.Template.Create    (فقط Admin — هم‌ارزِ GuardianCard.ManageTemplates فعلی)
GuardianCard.Template.Edit      (فقط Admin)
GuardianCard.Template.Delete    (فقط Admin)
GuardianCard.Template.Activate  (فقط Admin)
```
`GuardianCard.ManageTemplates` **حذف نمی‌شود** — همچنان دروازهٔ کلیِ خودِ فرم می‌ماند (سازگاریِ کامل با نصب‌هایِ موجود)؛ ۵ مجوزِ تازه در کنارش، برایِ کنترلِ دقیق‌ترِ هر عملیات، اضافه می‌شوند.

---

## ۴) نقاطِ پرریسک — نیازمندِ تصمیمِ محصولی قبل از پیاده‌سازی

### ۴.۱ «جداسازیِ روی/پشتِ کارت» — چقدر واقعی؟
امروز یک `CardTemplate` = یک `FieldsJson` + یک `DesignJson` که **هر دو رویِ کارت را با هم** توصیف می‌کنند (چون `index.html` یک فایلِ واحد با هر دو `#cardFront`/`#cardBack` است، و `GuardianCardRenderer` هر دو را در یک `StageAndPopulate` واحد پر می‌کند). دو سطحِ ممکنِ پیاده‌سازی:

- **سطحِ کم‌ریسک (پیشنهادِ من):** در UI فرمِ مدیریتِ قالب، تب‌ها/کنترل‌ها را واضح‌تر به دو گروهِ «روی» (عکس/مشخصاتِ فردی/QR/بارکد) و «پشت» (اطلاعاتِ مؤسسه/قوانین/تماس/فیلدهایِ پویا — که همین حالا هم عمدتاً در تبِ «تنظیماتِ چاپ»/«متن پشتِ کارت» هستند) سازمان‌دهی کنم؛ نسخه‌بندی (بخش۲) هنوز کلِ رکورد را Snapshot می‌گیرد، ولی «Compare» می‌تواند تغییراتِ روی/پشت را در دو بخشِ جدا نشان دهد. **بدونِ تغییرِ دیتابیس/موتورِ رندر.**
- **سطحِ پرریسک:** روی و پشت را به دو موجودیتِ کاملاً مستقل تبدیل کنم (مثلاً `FrontTemplateID` + `BackTemplateID` جدا، قابلِ‌ترکیب/جایگزینیِ مستقل) — این یعنی تغییرِ ساختاریِ `TblCardTemplate`، تغییرِ `GuardianCardRenderer.StageAndPopulate` (که امروز فرض می‌کند «یک design برایِ کل کارت» است)، و ریسکِ واقعی برایِ پایپ‌لاینِ چاپیِ تازه‌تثبیت‌شده — دقیقاً همان چیزی که دستورِ صریحِ کاربر («Do not modify... unless required») می‌خواهد از آن پرهیز شود.

**این ممیزی سطحِ کم‌ریسک را توصیه می‌کند**، مگر کاربر صراحتاً «ترکیبِ آزادِ روی/پشتِ مستقل» را بخواهد.

### ۴.۲ «پروفایل‌هایِ چاپ» — تنشِ مستقیم با «موتورِ رندر را دست نزن»
`print.css` امروز **همیشه** دقیقاً یک اندازهٔ ثابت دارد (۲۱۶×۱۵۴٫۵mm، شاملِ Bleed، یک کارت در هر صفحه — طراحیِ PVC واقعی). درخواستِ «A4، چند کارت در صفحه، Portrait/Landscape، حاشیه» یعنی یک **مدلِ صفحه‌بندیِ کاملاً متفاوت** (چند مستطیلِ هم‌اندازهٔ کارت در یک شبکهٔ رویِ صفحهٔ A4) — این ذاتاً همان `print.css`/منطقِ `page-break` را لمس می‌کند، دقیقاً همان پایپ‌لاینی که دو بار (فازِ اعتبارسنجی + فازِ ۱) با دقت تست و تثبیت شد.

دو گزینه:
- **گزینهٔ کم‌ریسک:** فعلاً فقط «پروفایلِ PVC» (رفتارِ امروز، بدونِ تغییر) را به‌عنوانِ یک انتخابِ صریح در UI معرفی کنم (که هیچ چیزِ فنی عوض نمی‌کند، فقط یک برچسب/تنظیمِ ذخیره‌شده است) و طراحیِ واقعیِ «A4 چند-کارت-در-صفحه» را به فازِ بعدی موکول کنم (چون واقعاً `print.css` را لمس می‌کند).
- **گزینهٔ کامل:** همین حالا `print.css`/رندر را برایِ حالتِ A4 چندکارتی گسترش دهم — فنی ممکن است، ولی مستقیماً برخلافِ «unless required» است مگر کاربر تأیید کند که این فاز واقعاً به آن نیاز دارد.

---

## ۵) ارزیابیِ ریسک (خلاصه)

| بخش | ریسک | چرا |
|---|---|---|
| مدیریتِ حرفه‌ایِ قالب (نام/نوع/وضعیت/توضیح/Duplicate) | 🟢 کم | فقط ستون‌هایِ Additive + UI جدید؛ هیچ مسیرِ رندر لمس نمی‌شود |
| نسخه‌بندی | 🟢 کم | جدولِ جدید، مستقل، هم‌الگویِ اثبات‌شدهٔ `TblFamilyRoleHistory`؛ Save موجود فقط یک INSERT اضافه می‌گیرد |
| Audit Log | 🟢 کم | فقط چند فراخوانیِ `AuditLogger.Log(...)` در نقاطِ موجود؛ زیرساخت از قبل آماده |
| مجوز | 🟡 کم-متوسط | باید مطمئن شد ۵ کلیدِ تازه رفتارِ *امروز* را برایِ کاربرانِ فعلی عوض نمی‌کند (پیش‌فرض‌ها باید دقیقاً هم‌ارزِ `ManageTemplates` باشند) |
| جداسازیِ روی/پشت (سطحِ کم‌ریسکِ پیشنهادی) | 🟡 متوسط | فقط UI/سازمان‌دهی؛ اگر کاربر سطحِ پرریسک را بخواهد → 🔴 بالا |
| پروفایل‌هایِ چاپ | 🔴 بالا (اگر A4 چندکارتی همین فاز پیاده شود) / 🟢 کم (اگر فقط PVC این فاز باشد) | مستقیماً `print.css`/پایپ‌لاینِ تثبیت‌شده را لمس می‌کند |

---

## ۶) سؤال‌هایِ باز (نیاز به تصمیمِ کاربر قبل از پیاده‌سازی)

این‌ها جداگانه از طریقِ ابزارِ پرسش از کاربر مطرح می‌شوند، نه حدس زده می‌شوند:
1. سطحِ جداسازیِ روی/پشت — کم‌ریسک (سازمان‌دهیِ UI) یا کامل (موجودیت‌هایِ مستقل)؟
2. پروفایل‌هایِ چاپ — فقط PVC در این فاز، یا A4-چندکارتی هم همین فاز (با پذیرشِ ریسکِ لمسِ `print.css`)؟
3. دکمهٔ «حذف» فعلی چطور تأییدِ دومرحله‌ای می‌گیرد؟ (باید در کد دقیقاً بررسی شود؛ اگر از قبل تأیید دارد، فقط Audit Log اضافه می‌شود.)
