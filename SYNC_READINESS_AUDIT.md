# ممیزیِ آمادگیِ همگام‌سازیِ دسکتاپ ← سرور

تاریخ: ۱۴۰۵/۰۶/۳۰ (2026-09-21)
روش: بازخوانیِ مستقیمِ کد (نه مستندات) + پرس‌وجویِ مستقیمِ هر دو پایگاه‌دادهٔ واقعی (`CaseDB.sqlite` زندهٔ دسکتاپ و `syncserver` روی Postgres محلی).

**نتیجهٔ یک‌خطی: این نصبِ دسکتاپ هرگز حتی یک‌بار تلاش نکرده با هیچ سروری همگام شود — چون `ServerUrl` هرگز تنظیم نشده. ۱۰۰٬۹۳۸ عملیات در صفِ خروجی، همه از روز اول، دست‌نخورده مانده‌اند.**

---

## بخشِ ۱ — ممیزیِ مرکز

### ۱.۱ چگونه مراکز رویِ دسکتاپ ساخته می‌شوند
`Helpers\DatabaseInitializer.cs:2658-2679` — `EnsureDefaultCenters`: اگر `TblCenter` خالی باشد (نصبِ اول)، **۱۱ مرکزِ ثابت و از‌پیش‌نوشته‌شده** به‌طورِ خودکار درج می‌شوند: `001=کابل ... 010=تخار، 011=مرکز اصلی`. هیچ ورودیِ کاربر یا تنظیمِ دستی‌ای در این مرحله دخیل نیست.

### ۱.۲ چگونه به سرور سینک می‌شوند
**نمی‌شوند.** `TblCenter` در `Sync\OfflineSyncInitializer.SyncedTables` (خطِ ۵۰-۸۴، فهرستِ کاملِ ۱۳ موجودیتِ سینک‌شونده) **حضور ندارد**. سمتِ سرور هم `SyncRepository.Entities` (نگاشتِ موجودیت↔جدول) هیچ ورودیِ `centers`ی ندارد. مراکز یک مفهومِ کاملاً محلی و مستقل در هر دو سمت‌اند.

### ۱.۳ آیا CenterID=2 باید از قبل رویِ سرور باشد؟
نه به‌طورِ خودکار — چون سازوکاری برایِ این کار وجود ندارد (بندِ ۱.۲). اگر منظور «باید کسی دستی اضافه می‌کرد» باشد: بله، ولی هیچ‌کس این کار را نکرده.

### ۱.۴ چرا CenterID=2 غایب است
سرور فقط دقیقاً همان چیزی را دارد که مهاجرتِ `002_SeedPermissions.sql:11-13` می‌سازد: یک مرکزِ ثابتِ `center_id=1, code='001', name='مرکز اصلی'`. دسکتاپ به‌طورِ کاملاً مستقل ۱۱ مرکزِ خودش را ساخته. **این دو فهرست حتی کدهایشان هم یکی نیست** — کدِ «۰۰۱» رویِ سرور یعنی «مرکز اصلی»، ولی رویِ دسکتاپ یعنی «کابل». هیچ نگاشت یا تطبیقی بینشان تعریف نشده.

### ۱.۵ آیا سینکِ مرکز اصلاً پیاده‌سازی شده؟
**خیر — به‌هیچ‌وجه، نه خودکار و نه دستی از راهِ سینک.** تأییدشده با جست‌وجویِ کامل در `SyncedTables` و `SyncRepository.Entities`.

### ۱.۶ آیا مراکز دستی seed می‌شوند یا خودکار؟
هر دو سمت **مستقلاً و خودکار** seed می‌شوند (دسکتاپ در اولین اجرا؛ سرور در مهاجرت). **هیچ seedِ دستی یا هماهنگ‌شده‌ای بینشان نیست.**

### ۱.۷ آیا موتورِ سینک وقتی CenterID رویِ سرور نیست کار می‌کند؟
جدولِ `cases` رویِ سرور قیدِ `FOREIGN KEY (center_id) REFERENCES centers(center_id)` دارد (تأییدشده با `\d cases`). **رفتار: نه بی‌سروصدا موفق می‌شود، نه کلِ درخواست را می‌شکند.** `SyncController.Push` هر قلم را در `try/catch` خودش اجرا می‌کند (`SyncController.cs:130-141`) — یک نقضِ کلیدِ خارجی به `Rejected` با پیامِ خامِ استثنایِ Postgres (`ex.Message`) تبدیل می‌شود، نه کِرشِ کلِ Push. یعنی: اگر امروز دسکتاپ به این سرور Push می‌کرد، هر ۷۱ پروندهٔ CenterID=2 با پیامی شبیهِ `"23503: insert or update on table \"cases\" violates foreign key constraint..."` رد می‌شد — پیامی فنی و غیرِقابلِ‌فهم برایِ کاربرِ نهایی، نه یک خطایِ تجاریِ روشن.

---

## بخشِ ۲ — ممیزیِ دستگاه و تأیید

### ۲.۱ آیا دستگاه‌ها قبل از سینک نیاز به تأیید دارند؟
بله. `Device.CanSynchronize` (`SyncServer.Domain\Entities.cs:93-95`): `Status == Approved AND Kind == "sync"`. `SyncController.Push/Pull` هردو این را چک می‌کنند (`FindByGuidAsync` + `device.CanSynchronize`، `SyncController.cs:112-118`).

### ۲.۲ وضعیتِ فعلیِ ثبتِ دستگاه (پرس‌وجویِ مستقیم)
```sql
SELECT device_id, device_guid, machine_name, center_id, status, kind, created_at, approved_at, last_seen_at
FROM devices WHERE kind='sync';
```
نتیجه: **فقط یک ردیف** —
```
device_id=4 | device_guid=f810f782-1d14-4308-9638-1a1d85997bd5 | machine_name=DESKTOP-SYNC-TEST
center_id=1 | status=1 (Approved) | created_at=2026-08-09 15:29 | approved_at=NULL | last_seen_at=2026-08-10 11:05
```

### ۲.۳ وضعیتِ فعلیِ تأیید
`status=1` = `DeviceStatus.Approved` (`SyncServer.Domain\Entities.cs:104-107`). ولی `approved_at IS NULL` — ناسازگار با مسیرِ عادیِ تأیید (`DeviceRepository.ApproveAsync` باید `approved_at=NOW()` بزند)، نشانهٔ اینکه این ردیف با یک `UPDATE` دستی/مستقیم به Approved رسیده، نه از راهِ صفحهٔ Devices.

### ۲.۴ آیا دستگاهِ دسکتاپِ *واقعی* تاکنون تأیید شده؟
**خیر — چون هرگز حتی ثبت هم نشده.** `device_guid` واقعیِ این نصب (از `SyncState` دسکتاپ، پایینِ همین سند: `39d9d0fd-5a4f-4f20-b1a5-8d4712f9109c`) در جدولِ `devices` سرور **اصلاً وجود ندارد**. تنها ردیفِ `kind='sync'` مربوط به یک GUID و نامِ ماشینِ کاملاً متفاوت است (`DESKTOP-SYNC-TEST`) — یک دستگاهِ آزمایشیِ دستی، نه این نصب.

### ۲.۵ آیا دستگاهِ دسکتاپ تاکنون واقعاً وصل شده؟
**خیر.** حتی دستگاهِ تستی هم فقط یک‌بار، در ۹-۱۰ اوت، وصل شده (`last_seen_at`) و از آن پس هرگز. دستگاهِ واقعیِ این نصب هرگز حتی یک درخواستِ HTTP هم به این سرور نفرستاده — تأییدشده در بخشِ ۵.

---

## بخشِ ۳ — پیکربندیِ سینکِ دسکتاپ (مقادیرِ واقعی از پایگاه‌داده)

```sql
SELECT StateKey, StateValue, UpdatedAt FROM SyncState ORDER BY StateKey;
```

| StateKey | StateValue |
|---|---|
| `AutoSyncEnabled` | `1` |
| `AutoSyncIntervalMinutes` | `15` |
| `DeviceGuid` | `39d9d0fd-5a4f-4f20-b1a5-8d4712f9109c` |

**`ServerUrl` در این جدول اصلاً وجود ندارد.** `HttpSyncTransport.KeyServerUrl = "ServerUrl"` (`Sync\HttpSyncTransport.cs:32`)، خوانده‌شده با `SyncOutboxService.GetState(KeyServerUrl, "")` (خطِ ۴۶) — نبودِ ردیف یعنی مقدارِ پیش‌فرضِ رشتهٔ خالی. `IsConfigured` (خطِ ۵۶): `!string.IsNullOrWhiteSpace(_baseUrl)` → **false**.

نه `RefreshToken` (`HttpSyncTransport.KeyRefreshToken`) و نه `DeviceId` (`SyncOutboxService.KeyDeviceId`، سرور-تخصیص‌داده‌شده) در جدول هست — هیچ ورودِ موفقی هرگز رخ نداده.

**نتیجه:**
| کلید | مقدار |
|---|---|
| ServerUrl | **تنظیم‌نشده (غایب)** |
| DeviceId (سمتِ سرور) | غایب — هرگز ثبت نشده |
| DeviceGuid (محلی) | `39d9d0fd-5a4f-4f20-b1a5-8d4712f9109c` |
| CenterId (انتخاب‌شدهٔ فعلی) | `TblCenter.IsActive=1` فقط برایِ CenterID=2 («بلخ») — بقیهٔ ۱۰ مرکز `IsActive=0` |
| وضعیتِ تأیید | بی‌معنا — چون دستگاه هرگز ثبت نشده |
| AutoSyncEnabled | `1` (روشن) — ولی بی‌اثر، چون `IsConfigured=false` |

---

## بخشِ ۴ — ممیزیِ پایگاه‌دادهٔ سینکِ دسکتاپ

```sql
SELECT State, COUNT(*) FROM SyncOutbox GROUP BY State;
SELECT EntityName, COUNT(*) FROM SyncOutbox GROUP BY EntityName ORDER BY COUNT(*) DESC;
SELECT COUNT(*) FROM SyncConflict;
SELECT COUNT(*) FROM SyncBaseline;
```

| جدول | تعداد | جزئیات |
|---|---|---|
| `SyncOutbox` | **۱۰۰٬۹۳۸** | همه `State='در انتظار'` (Pending) — **صفر** ارسال‌شده، صفر شکست‌خورده |
| — بر اساسِ موجودیت | | `TblFamily=64436`, `TblCase=28555`, `TblDocs=7913`, `TblAssistance=12`, `TblDisability=11`, `TblCaseRepresentative=9`, `TblOrphan=2` |
| `SyncConflict` | **۰** | منتظرِ توضیح ندارد — تعارض فقط از تعاملِ واقعی با سرور پیدا می‌شود |
| `SyncBaseline` | **۰** | سازگار با «هیچ Pushِ پذیرفته‌شده‌ای هرگز رخ نداده» |
| `SyncFile` / `SyncFileDownload` | ۰ / ۰ | هیچ فایلی هم منتقل نشده |

نمونه — قدیمی‌ترین قلم (`OutboxID=1`):
```
ثبت | TblCase | 2026-08-03 17:50:14 | CenterID=1 | State=در انتظار | Attempts=0
```
نمونه — جدیدترین قلم (`OutboxID=100938`):
```
ویرایش | TblCase | 2026-09-17 08:30:44 | CenterID=2 | State=در انتظار | Attempts=0
```

**نکتهٔ مهم:** قلم‌هایِ اولیه (اوایلِ اوت) با `CenterID=1` ثبت شده‌اند؛ قلم‌هایِ اخیر با `CenterID=2` — یعنی مرکزِ فعالِ انتخاب‌شده در طولِ زمان از «کابل» به «بلخ» عوض شده. `SyncOutbox.CenterID` مرکزِ *در لحظهٔ ثبت* را ضبط می‌کند، نه لزوماً مرکزِ فعلیِ رکورد.

`Attempts=0` رویِ همه — تأییدِ مستقیم که حتی یک تلاشِ ارسال (چه موفق چه ناموفق) هرگز رخ نداده. این با معماریِ مستندشدهٔ خودِ پروژه سازگار است: «نبودِ سرور یک خطا نیست» (`ONLINE.md:21-24`) — وقتی `IsConfigured=false`، `SyncService` اصلاً وارد حلقهٔ ارسال نمی‌شود، پس نه شمارندهٔ تلاش بالا می‌رود و نه چیزی State عوض می‌کند.

---

## بخشِ ۵ — ممیزیِ سرور

| جدول | تعداد | جزئیات |
|---|---|---|
| `devices` | ۶ | ۵ نشستِ مرورگرِ پورتال (`kind='portal'`) + ۱ دستگاهِ تستیِ دستی (`DESKTOP-SYNC-TEST`) |
| `centers` | ۱ | فقط `center_id=1, code='001', name='مرکز اصلی'` |
| `change_log` (`entity_name='TblCase'`) | ۱ | یک ردیف، از `device_id=4` (همان دستگاهِ تستی) |
| `sync_baseline` | ۱ | مطابقِ همان یک تغییرِ پذیرفته‌شده |
| `conflicts` | ۰ | — |

**آیا این سرور تاکنون از یک نصبِ *واقعیِ* دسکتاپ داده گرفته؟**
**خیر.** سه مدرکِ مستقل:
1. تنها `cases` rowِ سرور، payloadِ **JSON-شکل** دارد (`{"FullName":"آزمون همگام‌سازی","Note":"بار دوم — با UTF-8 صریح",...}`) — دسکتاپِ واقعی همیشه با فرمتِ `Key=Value\n` می‌فرستد (`SyncOutboxService.Serialize`)، هرگز JSON. خودِ محتوا هم می‌گوید «آزمونِ همگام‌سازی».
2. تنها دستگاهِ `kind='sync'`، نامش `DESKTOP-SYNC-TEST` است — نامِ ماشینِ واقعی نیست.
3. `device_guid` این دستگاهِ تستی (`f810f782-...`) با `DeviceGuid`ِ واقعیِ این نصب (`39d9d0fd-...`) **کاملاً متفاوت** است.

---

## بخشِ ۶ — ممیزیِ اتصال

- **آیا دسکتاپ به این سرور اشاره می‌کند؟** نه به هیچ سروری — `ServerUrl` تنظیم نشده (بخشِ ۳).
- **آیا به سرورِ دیگری اشاره می‌کند؟** خیر — همان کلید در همان جدول است؛ نبودنش یعنی نبودنش، نه اشاره به‌جایِ‌دیگر.
- **آیا سینک تاکنون کامل شده؟** خیر — صفر تلاش، صفر Baseline، صفر تغییرِ واقعی در `change_log` از این دستگاه.
- **نقطهٔ دقیقِ شکست:** حتی قبل از شروع — در `HttpSyncTransport.IsConfigured` (`Sync\HttpSyncTransport.cs:56`)، همان اولین شرطی که `SyncService`/`BackgroundSyncManager` پیش از هر تلاشی چک می‌کنند. کد هرگز به مرحلهٔ HTTP، احرازِ هویت، ثبتِ دستگاه، یا حتیِ خطایِ شبکه نمی‌رسد — چون هرگز شروع نشده.

---

## خروجی

### ۱. علتِ ریشه‌ای
این نصبِ دسکتاپ **هرگز پیکربندیِ سینک نداشته است.** `SyncState.ServerUrl` هرگز نوشته نشده (نه از راهِ `FrmServerConnection`، نه از هیچ راهِ دیگر) — کاربر ۷۱ پرونده را کاملاً آفلاین، از ۳ اوت تا ۱۷ سپتامبر، ساخته بدونِ اینکه هرگز آدرسِ سرور را تنظیم کند. این یک باگ نیست؛ این «هرگز روشن نشده» است. جداگانه از این، حتی اگر همین امروز `ServerUrl` تنظیم شود، اولین Push بلافاصله با شکستِ کلیدِ خارجیِ `centers` روبه‌رو می‌شود چون CenterID=2 («بلخ») رویِ سرور اصلاً وجود ندارد — سینکِ مرکز هرگز پیاده‌سازی نشده.

### ۲. مدرک
- `SyncState`: بدونِ ردیفِ `ServerUrl`/`RefreshToken`/`DeviceId`.
- `SyncOutbox`: ۱۰۰٬۹۳۸ ردیف، همه Pending، همه `Attempts=0`.
- `devices` (سرور): تنها دستگاهِ sync، GUIDِ متفاوت از دسکتاپِ واقعی.
- `cases`/`change_log`/`sync_baseline` (سرور): فقط یک ردیفِ تستیِ دستی، نه دادهٔ واقعی.
- `centers` (سرور): ۱ ردیف؛ `centers` (دسکتاپ): ۱۱ ردیف، بدونِ هیچ نگاشتی بینشان.

### ۳. نقطهٔ دقیقِ مسدودشدن
`CaseManagement\Sync\HttpSyncTransport.cs:56` — `IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl)` → `false`، چون `SyncState.ServerUrl` هرگز درج نشده. اگر این هم رفع شود، مسدودیتِ بعدی: `SyncServer.Infrastructure\SyncRepository.cs` (نبودِ `centers.center_id=2`) → هر Pushِ CenterID=2 با نقضِ کلیدِ خارجی رد می‌شود.

### ۴. فایل‌هایِ کدِ درگیر
- `CaseManagement\Sync\HttpSyncTransport.cs` (`IsConfigured`, `KeyServerUrl`)
- `CaseManagement\Sync\FrmServerConnection.cs` (جایی که کاربر *باید* ServerUrl را تنظیم کند)
- `CaseManagement\Sync\OfflineSyncInitializer.cs` (`SyncedTables` — بدونِ TblCenter)
- `CaseManagement\Helpers\DatabaseInitializer.cs` (`EnsureDefaultCenters`)
- `SyncServer.Infrastructure\SyncRepository.cs` (`Entities` — بدونِ centers؛ کاتالوگِ خطایِ FK)
- `SyncServer.Infrastructure\Migrations\002_SeedPermissions.sql` (تنها seedِ مرکز رویِ سرور)

### ۵. مدرکِ پایگاه‌داده
همهٔ کوئری‌ها و نتایجشان در بخش‌هایِ ۱ تا ۵ بالا، عیناً از اجرایِ واقعی رویِ `CaseDB.sqlite` (دسکتاپ) و `syncserver` (Postgres محلی).

### ۶. ترتیبِ رفعِ لازم (فقط توصیه — چیزی پیاده‌سازی نشد)
۱. تصمیمِ محصولی: کدام مرکزِ دسکتاپ (کابل=۱ یا بلخ=۲، یا هردو) باید واقعاً به این سرور وصل شود، و آن مرکز دقیقاً با چه کد/نامی رویِ سرور باید وجود داشته باشد.
۲. ساختنِ آن مرکز رویِ سرور (دستی، چون سینکِ مرکز وجود ندارد) — یا طراحیِ یک سازوکارِ سینکِ مرکز اگر قرار است این تکرار نشود.
۳. تنظیمِ `ServerUrl` رویِ همین نصبِ دسکتاپ (`FrmServerConnection`).
۴. ثبت و تأییدِ دستگاهِ *واقعی* (نه دستگاهِ تستی) از راهِ صفحهٔ Devicesِ پورتال.
۵. تصمیم دربارهٔ ۱۰۰٬۹۳۸ قلمِ انباشته‌شده — دسته‌بندی/پاکسازی/آزمونِ حجمِ اولین Sync پیش از رهاسازی روی داده‌هایِ واقعی.
۶. بهبودِ پیامِ خطایِ رد‌شدن به‌خاطرِ نبودِ مرکز (امروز پیامِ خامِ Postgres به کلاینت می‌رسد).

### ۷. ارزیابیِ ریسک
| ریسک | شدت |
|---|---|
| از‌دست‌رفتنِ دادهٔ ۷۱ پرونده در صورتِ شروعِ نابه‌جایِ سینک | **بالا** — ۱۰۰٬۹۳۸ قلمِ انباشته یعنی اولین Sync حجمِ عظیمی دارد؛ بدونِ آزمون، ریسکِ Rejectedهایِ زنجیره‌ای یا Timeout واقعی است |
| ناسازگاریِ مدلِ مرکز | **بالا** — این محدود به CenterID=2 نیست؛ هر ۱۱ مرکزِ دسکتاپ همین مشکل را دارند مگر جداگانه رفع شوند |
| نشتِ پیامِ خطایِ فنی به کاربرِ نهایی | **متوسط** — امنیتی نیست (فقط ساختارِ خطایِ DB)، ولی تجربهٔ کاربریِ گیج‌کننده |
| هیچ رگرسیونی از کارِ فازِ ۲/۳ ندیده شد | — این ممیزی هیچ ارتباطی با تغییراتِ اخیرِ Authorization پیدا نکرد؛ مشکل صرفاً پیکربندی‌نشدنِ سینک از روزِ اول است |

فازِ ۲ و فازِ ۳ متوقف می‌مانند تا زمانی که این آمادگیِ سینک تأیید شود، طبقِ دستور.
