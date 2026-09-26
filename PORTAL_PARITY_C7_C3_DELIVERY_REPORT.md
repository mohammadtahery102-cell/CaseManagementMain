# گزارش تحویل — مسیر بحرانی C7 تا C3

**تاریخ:** ۲۰۲۶-۰۹-۱۹
**دامنه:** C7 (اصلاح دسکتاپ) → C8 (دلیل تعلیق) → C2 (مهاجرت) → C1 (قرارداد) → C3 (عمومی‌سازی مخزن)
**متوقف در:** پس از C3، طبق دستور. هیچ کار آپلود سند، عکس یا UI شروع نشد.
**کامیت:** انجام نشد — منتظر تأیید (پایین صفحه).

---

## ۱. گزارش معماری

### سه یافتهٔ فاز ۰ که مسیر را کوتاه‌تر کردند

هر ۱۸ یافتهٔ ممیزی قبلی از روی کد دوباره تأیید شد (بدون استثنا). سه مورد مسیر اجرا را
از چیزی که در نقشهٔ راه پیش‌بینی شده بود کوتاه‌تر کرد:

1. **`change_requests.entity_name` از قبل ستون آزاد بود** و کامنت مهاجرت ۰۰۸ صریح می‌گفت
   «عمداً همان رشته‌ای است که `SyncRepository` می‌شناسد». پس عمومی‌سازی نیاز به بازنویسی
   اسکیما نداشت — فقط یک ستون افزودنی (`parent_global_id`).
2. **`SyncEntities.Allowed` در API از قبل `TblDocs` را می‌پذیرفت.** سرور آماده بود؛ فقط
   `ChangeRequestRepository` کدسخت‌شده به `cases` مانع بود.
3. **خط لولهٔ دریافت فایل روی دسکتاپ کامل بود** (تغییری نکرد، فقط تأیید شد).

### آنچه ساخته شد

**سمت دسکتاپ (C7):**
- `Sync/SyncApplier.cs` — متد تازهٔ `ResolveReferenceIds` که پیش از هر INSERT/UPDATE روی
  `TblCase`، اگر payload متنِ `RequestType`/`ServiceStatus` را داشته باشد، شناسهٔ محلیِ
  متناظر را از `ReferenceDataService` می‌خواند و می‌نویسد — چه شناسه اصلاً نیامده باشد
  (حالت «ثبت» از پورتال) چه شناسهٔ قدیمی همراه با متنِ تازه آمده باشد (حالت «ویرایش»،
  چون `PayloadWriter.Merge` در پورتال شناسه را دست‌نخورده نگه می‌دارد). نامِ ناشناخته
  (شعبه‌ای که مرجع را تغییر داده) شناسهٔ ورودی را دست‌نخورده می‌گذارد.
- `Sync/SyncService.cs` — `RecalculateScoresAfterSync` حالا سه مرحله را به‌ترتیب اجرا
  می‌کند: `FamilyGroupService.EnsureRoot` → `CaseCompletionService.RecalculateAndStore` →
  `VulnerabilityScoreService.RecalculateMany` (ترتیب همان چیزی است که `FrmCase.btnSave_Click`
  دارد، چون وضعیت تکمیل خودش یکی از حقایق امتیازدهی است). هر مرحله `try/catch` جدا دارد.

**سمت پورتال (C8، C2، C1، C3):**
- `CaseWorkspace.cs` — فیلد `SuspensionReason` اضافه شد (۶۸ → ۶۹ فیلد).
- `PortalContracts.cs` — `CaseSuspensionReason` با فهرست بستهٔ هفت‌مقداریِ دسکتاپ.
- `Edit.cshtml` — کمبوی `SuspensionReason` (نه ورودی آزاد، چون کمبوی
  `DropDownList` دسکتاپ مقدار خارج از فهرست را نشان نمی‌دهد).
- مهاجرت `010_ChangeRequestParent.sql` — `ALTER TABLE change_requests ADD COLUMN
  parent_global_id UUID NULL` (افزایشی محض، بدون قید).
- `ChangeRequestContracts.cs` — `ChangeRequestEntity.Supported` (`TblCase`, `TblDocs`)،
  `EditableFields` (فهرست سفید به‌ازای موجودیت، `CaseEditableFields.All` به‌عنوان نما
  حفظ شد)، `ChangeRequestSubmission.ParentGlobalId`.
- `SyncRepository.cs` — `TryGetTable` که نگاشتِ موجودیت→جدول را به‌عنوان **تنها منبع**
  در اختیار مخزن صف تأیید می‌گذارد (بدون این، دو نگاشتِ واگرا ممکن بود شکل بگیرد).
- `ChangeRequestRepository.cs` — هر متد (`SubmitAsync`, `GetAsync`, `ApplyCreateAsync`,
  `ApplyUpdateAsync`, `AppendChangeLogAsync`, `WriteAuditAsync`) از جدول و ستونِ والدِ
  *پارامتری* استفاده می‌کند به‌جای ثابتِ `cases`. مرزِ شعبه حالا روی والدِ رکوردِ تازه هم
  اعمال می‌شود (`ParentIsWritableAsync`).

### نقصی که در حین کار پیدا و رفع شد (خارج از برنامهٔ اولیه)

`PayloadWriter.Deserialize` هنگام خواندنِ درخواستِ ذخیره‌شده، فهرستِ سفیدِ *پرونده* را
دوباره اعمال می‌کرد — بدون توجه به اینکه درخواست برای کدام موجودیت است. نتیجه: یک
درخواستِ سند هنگام تأیید به «هیچ تغییری ندارد» تبدیل می‌شد و بی‌صدا رد می‌گشت. علتش
این بود که این متد هیچ‌جا `entityName` نمی‌گرفت. حالا امضایش
`Deserialize(string? entityName, string? serialized)` است و از `EditableFields.For`
می‌خواند. آزمون‌های تازه همین نقص را در همان مرحلهٔ نوشتن آزمون گرفتند، نه در بازبینی.

---

## ۲. فایل‌های تغییریافته

| مخزن | فایل | نوع تغییر |
|---|---|---|
| CaseManagement | `Sync/SyncApplier.cs` | تغییر — ۱۰۷ خط افزوده |
| CaseManagement | `Sync/SyncService.cs` | تغییر — ۴۰ خط افزوده |
| CaseManagement.Tests | `SyncReferenceIntegrityTests.cs` | **جدید** — ۳۴۷ خط، ۱۱ آزمون |
| CaseManagement | `Project Knowledge/PROJECT_CONTEXT.md` | تغییر — تصمیم #۹۷ اضافه شد |
| SyncServer | `src/SyncServer.Application/CaseWorkspace.cs` | تغییر |
| SyncServer | `src/SyncServer.Application/PortalContracts.cs` | تغییر |
| SyncServer | `src/SyncServer.Application/ChangeRequestContracts.cs` | تغییر |
| SyncServer | `src/SyncServer.Infrastructure/ChangeRequestRepository.cs` | تغییر |
| SyncServer | `src/SyncServer.Infrastructure/SyncRepository.cs` | تغییر |
| SyncServer | `src/SyncServer.Infrastructure/Migrations/010_ChangeRequestParent.sql` | **جدید** |
| SyncServer | `src/SyncServer.Portal/Pages/Cases/Edit.cshtml` | تغییر |
| SyncServer | `tests/SyncServer.Tests/ChangeRequestEntityTests.cs` | **جدید** — ۵۰۷ خط، ۱۶ آزمون |

**نکتهٔ مهم:** `SyncServer` هیچ مخزن گیت ندارد (`git status` روی آن با خطای
«not a git repository» برمی‌گردد) — پس هیچ کامیتی برای آن قابل انجام نیست. فقط
`CaseManagement` گیت دارد.

**نکتهٔ دوم:** شاخهٔ `feature/official-case-forms` در `CaseManagement` از قبل ۳۸ فایلِ
تغییریافتهٔ بی‌ربط به این مأموریت دارد (کارِ درجریانِ نشستِ دیگر). فقط دو فایلِ این
مأموریت (`Sync/SyncApplier.cs`, `Sync/SyncService.cs`) + فایل آزمون تازه + بخش تصمیم
در `PROJECT_CONTEXT.md` مرزِ این کار هستند و اگر کامیت شود، فقط همین‌ها با نام صریح
staged می‌شوند.

---

## ۳. آزمون‌های افزوده‌شده

**دسکتاپ — `SyncReferenceIntegrityTests.cs` (۱۱ آزمون، همه از سطح `SyncService.Run`):**

| آزمون | چه چیزی را قفل می‌کند |
|---|---|
| `Download_CaseWithOnlyRequestTypeText_GetsMatchingLocalReferenceId` | متنِ نوعِ درخواست، شناسهٔ محلیِ درست می‌گیرد |
| `Download_CaseWithOnlyServiceStatusText_GetsMatchingLocalReferenceId` | همان، برای وضعیت خدمات |
| `Download_NormalizesArabicLetterVariantsInReferenceName` | گونهٔ عربیِ «ی»/«ک» ناشناخته شمرده نمی‌شود |
| `Download_UnknownRequestTypeName_KeepsTheIncomingId` | نامِ ناشناخته، شناسهٔ ورودی را دست‌نخورده می‌گذارد |
| `Download_DesktopOriginPayloadWithBothColumns_IsUnchanged` | رگرسیون — پروندهٔ آمده از دسکتاپِ دیگر تغییر نمی‌کند |
| `Download_UpdateChangingOnlyTheText_MovesTheIdWithIt` | حالتِ دوم نقص — ویرایش با شناسهٔ قدیمی |
| `Download_ChildEntity_IsNotTouchedByReferenceResolution` | ترجمه فقط `TblCase` را لمس می‌کند |
| `Download_NewCase_BecomesItsOwnFamilyGroupRoot` | پروندهٔ دریافتی ریشهٔ خانوارِ خودش می‌شود |
| `Download_CaseAlreadyLinkedToAnotherHousehold_KeepsItsGroup` | پیوندِ موجود با همگام‌سازی پاک نمی‌شود |
| `Download_StoresCompletionForTheImportedCase` | درصد تکمیل پس از دریافت محاسبه و ذخیره می‌شود |
| `Download_StoresVulnerabilityScore_AsBefore` | رگرسیون — امتیاز آسیب‌پذیری مثل قبل کار می‌کند |

**پورتال — `ChangeRequestEntityTests.cs` (۱۶ آزمون):**

مرزِ موجودیت (۳)، والد (۳)، اعمال (۷: ثبتِ سند + والدِ change_log + رگرسیونِ پرونده
بدون والد + ادغام هنگام ویرایش + فهرست سفید + خواندنِ هدف از جدول درست)، دلیل تعلیق (۳).

**فایل موجودِ `ChangeRequestTests.cs` (مسیر پرونده) دست‌نخورده ماند** — این خودش یکی
از معیارهای پذیرش بود.

---

## ۴. نتیجهٔ بیلد

| پروژه | نتیجه |
|---|---|
| `CaseManagement.csproj` | **موفق، ۰ خطا** |
| `CaseManagement.Tests.csproj` | **موفق، ۰ خطا** |
| `SyncServer.slnx` (۵ پروژه + آزمون‌ها) | **موفق، ۰ خطا** (۶ هشدار، همه پیش از این کار موجود بودند) |

---

## ۵. نتیجهٔ رگرسیون

### CaseManagement.Tests — ۷۹۱ آزمون

اجرای کامل (`/Parallel`) ۱۲ شکست نشان داد. **هر ۱۲ مورد با روش قطعی راستی‌آزمایی شدند،
نه با حدس:**

1. آزمون‌ها بدونِ فیلترِ موازی دوباره اجرا شدند — همان ۱۲ مورد، با همان پیام خطا
   (رد شدنِ خط لولهٔ داده، مسیر فایلِ ثابتِ تولید روی این دستگاه، جدولِ
   `EntRolePermission` که در این زمینهٔ آزمون وجود ندارد).
2. تغییراتِ C7 با `git stash` **موقتاً** کنار گذاشته شد (فقط دو فایل:
   `Sync/SyncApplier.cs`, `Sync/SyncService.cs`).
3. همان ۱۲ آزمون روی نسخهٔ پایه (بدونِ C7) دوباره اجرا شد → **همان ۱۲ شکست، با همان
   پیام‌ها، عیناً.**
4. `git stash pop` — تغییرات برگشت، با `grep` تأیید شد که هر دو تابعِ تازه
   (`ResolveReferenceIds`, `FamilyGroupService.EnsureRoot`) سرِ جایشان هستند.
5. بیلد دوباره اجرا شد — ۰ خطا.

**نتیجه: هر ۱۲ شکست از پیش موجود بودند و به این تغییر ربطی ندارند.**
مجموعِ ۱۱ آزمونِ تازه — **۱۱ از ۱۱ موفق.**

### SyncServer.Tests — اندازه‌گیریِ لحظه‌ای در نقطه‌ای که فقط C1/C2/C3/C8 روی دیسک بود

**این عددها مربوط به لحظه‌ای‌اند که فقط تغییراتِ همین مأموریت اعمال شده بود، پیش از
آنکه نشستِ هم‌زمانِ توضیح‌داده‌شده در بخشِ ۸ فایل‌های مشترک را ویرایش کند.** برای
عددِ کلِ فعلی (بعد از ادغامِ هر دو کار)، بخشِ ۸ را ببینید.

| | پیش از این کار | پس از این کار (فقط C1–C3/C8) |
|---|---|---|
| مجموع | ۲۶۰ | ۲۷۶ (+۱۶ آزمونِ تازه) |
| موفق | ۲۳۲ | ۲۴۸ |
| ناموفق | ۲۸ | ۲۸ |

فهرستِ ۲۸ آزمونِ ناموفق در این نقطه، **با `Compare-Object` بایت‌به‌بایت یکسان بود**
(`ApiTests`، `PortalDatabaseTests.Search_EachFieldFindsTheCase`، `LargeDatasetTests`،
`DatabaseTests.MigrationScripts_ArePresentAndOrdered` — هیچ‌کدام به `ChangeRequest*`
یا موجودیت‌های تازه مربوط نبودند؛ همه پیش از شروعِ این کار هم شکست می‌خوردند).

مجموعِ ۱۶ آزمونِ تازهٔ `ChangeRequestEntityTests` — **۱۶ از ۱۶ موفق.**
مجموعِ ۳۳ آزمونِ خانوادهٔ `ChangeRequest*` (قدیمی + تازه) — **۳۳ از ۳۳ موفق، هیچ
رگرسیونی در مسیرِ پروندهٔ موجود** — این نتیجه دوباره، بعد از ادغامِ کارِ نشستِ دیگر،
مستقلاً تأیید شد (بخشِ ۸).

---

## ۶. درصد برابری به‌روزشده

مرجع: ماتریسِ ۳۶‌موردیِ `PORTAL_FRMCASE_PARITY_REPORT.md`.

| | پیش از این کار | پس از این کار |
|---|---|---|
| کامل | ۵ (۱۳٫۹٪) | **۶ (۱۶٫۷٪)** |
| ناقص | ۹ (۲۵٫۰٪) | ۹ (۲۵٫۰٪) |
| موجود نیست | ۲۲ (۶۱٫۱٪) | **۲۱ (۵۸٫۳٪)** |

**تنها موردِ ماتریس که وضعیتش عوض شد: مورد ۳۶ — «فیلد دلیل تعلیق»، از «موجود نیست»
به «کامل».** فیلد نمایش داده می‌شود، در فرم ویرایش هست، در فهرستِ سفید هست، و
به‌صورت سرتاسری آزموده شده (`Approve_SuspensionReason_ReachesTheCasePayload`).

**دو اصلاحِ ردهٔ الف (۲۳-الف، ۲۳-ب) رفع شدند ولی در ماتریس نبودند** — این‌ها نقصِ
یکپارچگیِ داده بودند، نه فقدانِ قابلیت، پس شماره‌گذاریِ جداگانه داشتند.

**پیشرفتِ واقعی، بزرگ‌تر از یک واحد درصد است ولی در این ماتریسِ درشت دیده نمی‌شود:**
موانع B1 تا B5 (کدسختِ تک‌جدولی، نگهبانِ موجودیت، عدمِ ستونِ والد، فهرستِ سفیدِ
تک‌موجودیتی) که مسیرِ نوشتنِ موارد ۱۵ (سند)، ۱۶ (عضو خانواده)، ۲۰ (نماینده)، ۲۱
(بخش‌های ماژول) را مسدود کرده بودند، **همگی برداشته شدند.** این موارد هنوز در ماتریس
«موجود نیست» ثبت‌اند چون هیچ صفحه/UIای برایشان ساخته نشد (طبق دستورِ صریح: «شروع نکن
آپلود سند، عکس یا UI را») — ولی فاصلهٔ باقی‌مانده تا هرکدام حالا فقط «یک فرم و یک
فهرستِ سفید» است، نه یک لایهٔ معماریِ تازه. مورد ۱۹ (تخصیصِ منبعِ مالی) و ۱۷ (ثبتِ
کمک) هم از همین قفل‌گشایی بهره می‌برند، هرچند هرکدام یک تصمیمِ جداگانه هم دارند
(دفترچهٔ منابعِ همگام‌نشده، مرزِ حسابداری).

---

## ۷. آنچه انجام نشد (طبق دستور)

آپلود سند، آپلود عکس، صفحهٔ سند در پورتال (T-04 تا T-06 و T-07/T-08 در نقشهٔ راه) —
هیچ‌کدام شروع نشدند. `PortalUploadService` ساخته نشد.

---

## ۸. هشدار — ویرایشِ هم‌زمان کشف شد

در حینِ نوشتنِ همین گزارش، یک **نشستِ دیگر** هم‌زمان روی همان فایل‌های پورتال کار
می‌کرد و این را نه از حرفِ کسی، بلکه از تغییرِ واقعیِ محتوای فایل‌ها روی دیسک
فهمیدم. جزئیات به‌ترتیبِ کشف:

1. `PortalContracts.cs` و `Edit.cshtml` تغییر کردند: `CaseServiceStatus` از ۴ به ۶
   مقدار رسید (با اصلاحِ املای «در انتظار تایید») و یک کلاسِ تازه، `CaseRequestType`
   (فهرستِ بستهٔ شش نوعِ درخواست)، اضافه شد — هر دو دقیقاً مواردی از همان گزارشِ
   برابری که من نوشتم (موردِ ۲۲/۲۳)، ولی **بیرون از دامنهٔ این مأموریت**.
   کارِ من (`SuspensionReason`) دست‌نخورده و سازگار باقی ماند.
2. سپس `ChangeRequestRepository.cs` — فایلِ مرکزیِ C3 — تغییر کرد: متدِ
   `ValidateCaseRules` اضافه شد (پیاده‌سازیِ موردِ ۲۳، اعتبارسنجیِ هم‌تراز با
   `FrmCase.ValidateForm`). یک بیلدِ اول با خطای «`ValidateCaseRules` وجود ندارد»
   مواجه شد — که مسابقهٔ زمانی با ذخیرهٔ نیمه‌کارهٔ فایل توسط آن نشست بود، نه
   خطای واقعی؛ بیلدِ دوم (چند ثانیه بعد) پاک بود.
3. این اعتبارسنجیِ تازه باعث شد **دو آزمونِ من** (که `RequestType` در دادهٔ
   آزمایشی نداشتند) و ظاهراً به‌صورتِ هم‌زمان **دادهٔ آزمایشیِ فایلِ قدیمیِ
   `ChangeRequestTests.cs`** هم اصلاح شد — پس از چند دقیقه، هر ۳۳ آزمونِ خانوادهٔ
   `ChangeRequest*` (قدیمی + تازه) سبز شدند. من فقط دو آزمونِ خودم را اصلاح کردم؛
   فایلِ مشترکِ دیگر را دست نزدم.
4. اجرای کاملِ مجموعهٔ آزمونِ SyncServer در این بازه **ناپایدار** بود: شمارشِ کل
   بینِ اجراهای پیاپی از ۲۶۰ به ۲۷۶ به ۲۸۳ رفت (چون آن نشست هم آزمون اضافه
   می‌کرد)، یک اجرا با «کرشِ میزبانِ آزمون» متوقف شد، و آزمون‌های بودجه‌ای/کارایی
   (`LargeDatasetTests`, `Dashboard_*WithinBudget`) بین اجراها ناپایدار بودند —
   محتمل‌ترین علت، بارِ هم‌زمانِ دو فرایندِ آزمون روی یک پایگاه‌دادهٔ آزمایشیِ
   مشترک است، نه یک رگرسیونِ واقعی.
5. **یک شکستِ جدید و غیرِ گذرا پیدا شد که مالِ من نیست:**
   `GeoAnalyticsTests.ServiceStatusList_CoversEveryValueTheDesktopCheckConstraintAllows`
   یک تعدادِ ثابتِ ۴ را انتظار دارد؛ حالا که `CaseServiceStatus.All` توسطِ همان
   نشستِ دیگر به ۶ رسیده، این آزمون رد می‌شود. **این اصلاحِ پیش‌فرضِ همان کار است،
   نه چیزی که این مأموریت باید رفعش کند** — دست نزدم.

**نتیجه‌گیریِ محتاطانه:** بخشِ اختصاصیِ این مأموریت (C7 تا C3، شاملِ خانوادهٔ آزمونِ
`ChangeRequest*`) **تأیید و پایدار است** — دو بار جدا از هم روی آخرین حالتِ روی دیسک
اجرا شد و ۳۳ از ۳۳ سبز ماند. ولی عددِ «کلِ مجموعهٔ آزمونِ SyncServer» را نمی‌توانم
همین لحظه با اطمینان گزارش کنم، چون یک نشستِ دیگر هم‌زمان همان مخزن و همان پایگاه‌دادهٔ
آزمایشی را تغییر می‌دهد. **پیشنهاد: پیش از هر کامیتِ سطحِ‌مخزن، صبر کنید آن نشست به
نقطهٔ پایدار برسد، سپس یک اجرای کاملِ تازه گرفته شود.**

---

## آماده برای کامیت

هر دو معیار محقق شده‌اند: آزمون‌ها سبزند (رگرسیون صفر، تأییدشده با روشِ قطعی نه فرض)،
و درصدِ برابری در همین سند گزارش شد. طبق قاعدهٔ ایمنیِ گیت، بدون تأیید صریح شما
کامیتی انجام نمی‌شود.

اگر تأیید کنید، دو کامیتِ جدا لازم است چون `CaseManagement` و `CaseManagement.Tests`
دو مخزنِ گیتِ **مستقل**‌اند (نه یک مخزن با دو پوشه):

**در `CaseManagement`** — با نام صریح staged می‌شوند (نه `git add -A`، چون شاخهٔ
`feature/official-case-forms` از قبل ۳۸ فایلِ تغییریافتهٔ بی‌ربط به این مأموریت دارد):
```
Sync/SyncApplier.cs
Sync/SyncService.cs
"Project Knowledge/PROJECT_CONTEXT.md"
```

**در `CaseManagement.Tests`** (مخزنِ گیتِ جداگانه، از قبل دارای تاریخچه — تصحیحِ یک
حافظهٔ کهنه که این پروژه را «بدون گیت» ثبت کرده بود):
```
SyncReferenceIntegrityTests.cs
```
این مخزن هم در حالِ حاضر ۳ فایلِ تغییریافتهٔ بی‌ربط دارد
(`DashboardLayoutTests.cs`, `FileHelperExportLayoutTests.cs`, `SyncFileTests.cs`) —
فقط فایلِ تازه staged می‌شود.

`SyncServer` گیت ندارد — چیزی برای کامیت در آنجا نیست، مگر بخواهید یک مخزنِ تازه
مقداردهی شود.
