# گزارشِ وارسیِ پس‌ازِ‌پیاده‌سازی — رفعِ باگِ همزمانیِ سینکِ کاربر

تاریخ: ۱۴۰۵/۰۶/۲۹ (2026-09-20)
دامنه: پیاده‌سازیِ کاملِ `USER_SYNC_CONCURRENCY_DECISION.md`، وارسیِ ساخت، رگرسیون، و حذفِ سناریویِ داده‌ازدست‌رفته.

---

## ۱. خلاصه

باگِ تأییدشدهٔ ممیزیِ ۲۰۲۶-۰۹-۲۰ (بازنویسیِ خاموشِ تغییراتِ پورتال توسطِ پوشِ دسکتاپ) **رفع شد**. رفع **فقط سمتِ سرور** است (`SyncServer.Infrastructure/SyncRepository.cs`) — هیچ فایلی در CaseManagement (دسکتاپ) تغییر نکرد، دقیقاً طبقِ پیش‌بینیِ گزارشِ تصمیم. هیچ مهاجرتِ اسکیمای تازه‌ای لازم نبود.

**نتیجه:** ۲۱ آزمون (۱۷ موجود + ۴ رگرسیونِ تازه) در `UserSyncTests.cs` سبز؛ کلِ مجموعهٔ ۴۲۴‌تاییِ `SyncServer.Tests` با همان ۲۹ شکستِ از‌پیش‌موجود و بی‌ربط اجرا شد (صفر رگرسیونِ تازه).

---

## ۲. چه چیزی پیاده‌سازی شد

### بندِ ۳ (اول) — رفعِ چرخهٔ حیاتِ مبنا
`SyncRepository.PullAsync` اکنون برایِ هر تغییرِ `TblUsers` که تحویلِ یک دستگاه می‌دهد، مبنایِ همان دستگاه را هم ثبت می‌کند (`UpsertBaselineAsync` با `transaction: null`، best-effort). پیش از این، مبنا فقط پس از پذیرشِ یک Push ثبت می‌شد — دستگاهی که فقط Pull می‌کرد هرگز مبنا نداشت.

### بندِ ۴ — حذفِ FailedLoginCount/LockoutUntil از سینک
`StripSyncExcludedUserFields` در ابتدایِ `ApplyUserChangeAsync` این دو کلید را از payload حذف می‌کند — یک‌بار، پیش از هر مسیرِ پایین‌دستی (درج، ویرایش، change_log، مبنا). کدهایِ اختصاصیِ این دو فیلد در مسیرِ ویرایش هم به‌طورِ کامل حذف شدند (نه فقط بی‌اثر شدند).

### بندِ ۲ — ادغامِ سطحِ فیلد (رفعِ اصلیِ باگ)
`UpdateUserAsync` اکنون تصمیم می‌گیرد کدام مسیر را طی کند:
- **بدونِ رقیب** (`sync_baseline.row_version` این دستگاه == نسخهٔ فعلیِ سرور): مسیرِ مستقیمِ همیشگی — کلِ فیلدهایِ ارسالی اعمال می‌شود (`ApplyDirectUpdateAsync`؛ رفتارِ قبل از رفع، منهایِ دو فیلدِ حذف‌شده).
- **با رقیب** (نسخهٔ سرور از مبنایِ این دستگاه جلوتر رفته، یا اصلاً مبنایی نیست): `ApplyFieldMergeAsync` — پرتابِ مستقیمِ الگوریتمِ `SyncConflictAnalyzer.Decide` از دسکتاپ (همان چهار حالت: Same/TakeLocal/TakeRemote/Contested، همان قاعدهٔ فیلدِ حساس برایِ Role/IsActive، همان محافظه‌کاریِ بدونِ‌مبنا). هر فیلدِ Contested کلِ به‌روزرسانی را به Conflict می‌برد (نه فقط آن فیلد را) — دقیقاً رفتارِ `CanAutoMerge` روی دسکتاپ.

این گیت («فقط وقتی رقیبِ واقعی هست») حیاتی بود: بدونِ آن، هر ویرایشِ معمولیِ یک دستگاه رویِ رکوردِ خودش (بدونِ هیچ مداخله‌ای) هم به‌خاطرِ حساس‌بودنِ Role/IsActive به تعارض می‌رفت — دقیقاً همان‌طور که آزمونِ قبلاً-سبزِ `Push_Update_MergesFields_AndBumpsVersion` ثابت کرد.

هر بارِ ادغام‌شده، payloadِ بازنویسی‌شده (وضعیتِ واقعیِ پس‌ازادغام، نه فقط چیزی که این دستگاه فرستاد) در `change_log`/مبنا ثبت می‌شود — وگرنه دستگاهی که بعداً Pull می‌کند فیلدهایِ حفظ‌شده را با نسخهٔ کهنه بازمی‌نویسد.

---

## ۳. نتایجِ آزمون

### `UserSyncTests.cs` — همهٔ ۲۱ آزمون سبز

| آزمون | نتیجه |
|---|---|
| `Concurrent_PortalRoleChange_ThenDesktopPasswordPush_RevertsPortalsRoleChange` (Skip برداشته شد) | Passed |
| `Concurrent_PasswordChangeVsRoleChange_IsConflict_AndPreservesPortalsRoleChange` (تازه) | Passed |
| `Concurrent_PasswordChangeVsDeactivate_IsConflict_AndKeepsUserDeactivated` (تازه) | Passed |
| `Concurrent_TwoDevicesEditDifferentFields_BothChangesSurvive_NoDataLoss` (تازه) | Passed |
| `Push_AfterFirstEverPull_SucceedsWithoutConflict_BecausePullSeedsBaseline` (تازه) | Passed |
| ۱۳ آزمونِ از‌قبل‌موجود (Create/Update/Delete/مرزِ مرکز/Idempotency/Pull) | همه Passed |

اجرایِ کامل: `dotnet test --filter FullyQualifiedName~UserSyncTests` → **17→21، ۲۱/۲۱ سبز**.

### کلِ `SyncServer.Tests` — بدونِ رگرسیونِ تازه

```
Total tests: 424
Passed:      395
Failed:       29   (همه از‌پیش‌موجود و بی‌ربط — نگاه کنید به بخشِ ۴)
```

هر ۱۵ آزمونِ `SyncIntegrationTests` (۱۲ موجودیتِ دیگرِ سینک) سبز ماندند — تغییرِ `PullAsync` (افزودنِ باسلاین‌نویسی، محدود به شرطِ `EntityName == "TblUsers"`) و تغییرِ امضایِ `UpsertBaselineAsync` (nullable transaction) هیچ اثری روی مسیرِ عمومیِ ۱۲ موجودیتِ دیگر نداشت.

### ساخت
```
dotnet build SyncServer.sln → Build succeeded, 0 Errors (۱۱ هشدارِ از‌پیش‌موجود، بدونِ هشدارِ تازه)
```

---

## ۴. تحلیلِ ۲۹ شکست — همه بی‌ربط، صفر رگرسیون

| گروه | تعداد | علت | ربط به این رفع |
|---|---|---|---|
| `ApiTests` | ۱۱ | کلید JWT در `WebApplicationFactory` (پیکربندیِ محیطِ آزمون) | ندارد |
| `PortalDatabaseTests.Search_EachFieldFindsTheCase` | ۷ | نامِ مرکز در مهاجرتِ ۰۰۲ («مرکز اصلی») با انتظارِ آزمون («شعبهٔ اصلی») نمی‌خواند | ندارد |
| `LargeDatasetTests` | ۹ | حساسیتِ زمانی/محیطی (میزبانِ آزمون) | ندارد |
| `GeoAnalyticsTests.ServiceStatusList_...` | ۱ | فهرستِ ثابتِ وضعیتِ خدمت (بدونِ ربط به کاربر/سینک) | ندارد |
| `DatabaseTests.MigrationScripts_ArePresentAndOrdered` | ۱ | کپی‌نشدنِ پوشهٔ Migrations کنارِ اسمبلیِ آزمون در برخی اجراها | ندارد |

هیچ‌کدام `TblUsers`، `SyncRepository`، یا `sync_baseline`/`conflicts` را لمس نمی‌کنند. جزئیات: `[[syncserver-test-database]]` (حافظهٔ به‌روزشده).

---

## ۵. اثباتِ حذفِ سناریویِ داده‌ازدست‌رفته

سه سناریویِ مستقیماً برگرفته از یافتهٔ ممیزی اکنون **رفتارِ امن** دارند (نه پذیرشِ خاموش، نه رد کاملِ غیرِ منطقی):

1. **پورتال Role را عوض می‌کند، دسکتاپ رمز را عوض می‌کند** → `Conflict`؛ Role دست‌نخورده می‌ماند (پیش از رفع: بی‌سروصدا به Viewer برمی‌گشت).
2. **پورتال کاربر را غیرفعال می‌کند، دسکتاپ رمز را عوض می‌کند** → `Conflict`؛ کاربر غیرفعال می‌ماند (پیش از رفع: می‌توانست بی‌سروصدا دوباره فعال شود — ریسکِ امنیتی).
3. **دو دستگاه هم‌زمان دو فیلدِ مستقل را عوض می‌کنند** (A یوزرنیم، B رمز) → `Accepted` برایِ هردو؛ **هیچ تغییری از دست نرفت** — این همان اثباتِ مستقیمِ «بدونِ داده‌ازدست‌رفته» است که ادغامِ سطحِ فیلد (به‌جایِ جایگزینیِ کلِ ردیف) هدفش بود.

و یک سناریوی پیش‌نیاز:

4. **دستگاهی که فقط Pull کرده، اولین Push خودش را می‌فرستد** → `Accepted` بدونِ تعارض (پیش از رفعِ بندِ ۳: محافظه‌کارانه به Conflict می‌رفت، حتی بدونِ هیچ رقیبِ واقعی).

---

## ۶. فایل‌هایِ تغییریافته

- `SyncServer/src/SyncServer.Infrastructure/SyncRepository.cs` — تنها فایلِ کدِ تولید؛ `ApplyUserChangeAsync`، `UpdateUserAsync` (بازنویسیِ کامل به دو مسیر)، `PullAsync`، `UpsertBaselineAsync` (امضا).
- `SyncServer/tests/SyncServer.Tests/UserSyncTests.cs` — Skip برداشته شد از یک آزمون؛ ۴ آزمونِ تازه + ۳ متدِ کمکی (`UserIdOfAsync`، `ReadPasswordHashAsync`، `CreateDeviceAsync`).
- `CaseManagement` — **بدونِ تغییر** (طبقِ طراحیِ تصمیمِ معماری: فقط سرور).
- بدونِ مهاجرتِ تازه — `sync_baseline`/`conflicts` از فازهایِ قبلی موجود بودند.

---

## ۷. جمع‌بندی

باگِ تأییدشده رفع شد، با کمترین تغییر ممکن (یک فایلِ کد، بدونِ تغییرِ دسکتاپ، بدونِ مهاجرتِ تازه)، با بازاستفادهٔ کاملِ الگوریتمِ ادغامِ موجودِ دسکتاپ (نه اختراعِ چیزِ تازه)، و با پوششِ آزمونِ هر چهار سناریویِ درخواست‌شده. کلِ مجموعهٔ آزمون بدونِ رگرسیونِ تازه اجرا شد.

**آماده برایِ فازِ ۲.**
