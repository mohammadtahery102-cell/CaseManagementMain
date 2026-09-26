# گزارشِ نگاشتِ کاملِ مجوز — پیش از گامِ ۲ (موتورِ Authorization)

تاریخ: ۱۴۰۵/۰۶/۲۹ (2026-09-20)
روش: دو عاملِ تحقیقِ مستقل (یکی رویِ کلِ CaseManagement — ۱۱۶ نقطهٔ فراخوانی در ۳۸ فایل؛ یکی رویِ کلِ SyncServer.Portal + SyncServer.API)، به‌علاوهٔ کاتالوگِ ۶۴کلیدیِ گامِ ۱. هیچ کدی نوشته نشده — طبقِ دستور، فقط گزارش.

---

## ۱. ۶۴ مجوزِ دسکتاپ، گروه‌بندی‌شده بر اساسِ ماژول

منبع: `EnterpriseInitializer.EnsureDefaultPermissions` (`Enterprise\EnterpriseInitializer.cs:659-787`). ستونِ «فعال؟» یعنی آیا واقعاً جایی رویِ دسکتاپ چک می‌شود (بخشِ ۴).

| دسته | کلید | Admin | Operator | Viewer | فعال؟ |
|---|---|:-:|:-:|:-:|:-:|
| **پرونده** | Case.View | ✓ | ✓ | ✓ | ✓ |
| | Case.Create | ✓ | ✓ | | **مرده** |
| | Case.Edit | ✓ | ✓ | | ✓ |
| | Case.Delete | ✓ | | | ✓ |
| | Case.Export | ✓ | ✓ | ✓ | **مرده** |
| | CaseRelation.Edit | ✓ | ✓ | | ✓ |
| | CaseRelation.Delete | ✓ | | | ✓ |
| | Case.Print | ✓ | ✓ | ✓ | ✓ |
| **گردش‌کار** | Workflow.View | ✓ | ✓ | ✓ | **مرده** |
| | Workflow.Review | ✓ | ✓ | | غیرمستقیم (`WorkflowService.CanTransition`) |
| | Workflow.Approve | ✓ | | | غیرمستقیم |
| | Workflow.Manage | ✓ | | | ✓ |
| **تأیید** | Approval.Decide | ✓ | | | نیمه‌مرده (سازوکار هست، هیچ سطحِ پیش‌فرضی وصلش نکرده) |
| | Approval.Manage | ✓ | | | ✓ |
| **وظایف** | Task.View | ✓ | ✓ | ✓ | **مرده** |
| | Task.Manage | ✓ | | | ✓ |
| **قواعد** | Rule.Manage | ✓ | | | ✓ |
| **امنیت** | Lock.Override | ✓ | | | ✓ |
| | Security.View | ✓ | | | ✓ |
| | Error.View | ✓ | | | ✓ |
| | Version.View | ✓ | ✓ | ✓ | ✓ |
| **سیستم** | User.Manage | ✓ | | | ✓ |
| | Permission.Manage | ✓ | | | ✓ |
| | Module.Manage | ✓ | | | ✓ |
| | Settings.Manage | ✓ | | | ✓ |
| | Center.Manage | | | | ✓ |
| | Backup.Create | | | | ✓ |
| | Backup.Restore | | | | ✓ |
| **مالی** | Finance.View | ✓ | ✓ | ✓ | ✓ |
| | Finance.Edit | ✓ | ✓ | | ✓ |
| | AssistanceReceipt.Print | ✓ | ✓ | | ✓ |
| **حسابداری** | Accounting.View | ✓ | ✓ | ✓ | ✓ |
| | Accounting.Edit | ✓ | ✓ | | ✓ |
| | Accounting.Reverse | ✓ | | | ✓ |
| | Accounting.ClosePeriod | ✓ | | | ✓ |
| | Accounting.Repair | | | | ✓ |
| | Accounting.Backup | | | | ✓ |
| **خانواده** | Family.Edit | ✓ | ✓ | | ✓ |
| | Family.Delete | ✓ | | | ✓ |
| | Family.Print | ✓ | ✓ | ✓ | ✓ |
| **اسناد** | Docs.Edit | ✓ | ✓ | | ✓ |
| | Docs.Delete | ✓ | | | ✓ |
| | Docs.Print | ✓ | ✓ | ✓ | ✓ |
| **متقاضیان** | Applicant.Edit | ✓ | ✓ | | ✓ |
| | Applicant.Delete | ✓ | | | ✓ |
| **بایگانی** | Archive.Restore | ✓ | | | ✓ |
| | Archive.PermanentDelete | | | | ✓ |
| **گزارش** | Report.Run | ✓ | ✓ | ✓ | ✓ |
| | Report.Export | ✓ | ✓ | ✓ | ✓ |
| **بارکد** | Barcode.Print | ✓ | ✓ | ✓ | ✓ |
| **کارت شناسایی** | GuardianCard.Print | ✓ | ✓ | ✓ | ✓ |
| | GuardianCard.ManageTemplates | ✓ | | | ✓ |
| | GuardianCard.Template.View | ✓ | ✓ | ✓ | ✓ |
| | GuardianCard.Template.Create | ✓ | | | ✓ |
| | GuardianCard.Template.Edit | ✓ | | | ✓ |
| | GuardianCard.Template.Delete | ✓ | | | ✓ |
| | GuardianCard.Template.Activate | ✓ | | | ✓ |
| **همگام‌سازی** | Sync.Execute | ✓ | ✓ | | ✓ |
| **دستیار هوشمند** | AI.Search | ✓ | ✓ | ✓ | ✓ |
| | AI.Reminders.Create | ✓ | ✓ | | ✓ |
| **نمایندهٔ قانونی** | Representative.View | ✓ | ✓ | ✓ | ✓ |
| | Representative.Edit | ✓ | ✓ | | ✓ |
| | Representative.Delete | ✓ | | | ✓ |
| | Representative.Print | ✓ | ✓ | ✓ | **مرده** |

**۶ کلیدِ مرده** (تعریف‌شده، هرگز چک‌نشده رویِ خودِ دسکتاپ): `Case.Create`, `Case.Export`, `Workflow.View`, `Task.View`, `Approval.Decide` (سازوکار هست، هیچ سطحِ پیش‌فرضی وصل نمی‌کند)، `Representative.Print`. **توصیه: پورتال هم نباید برایِ این ۶ تا اجرایی بسازد** — طبقِ «دسکتاپ مرجع است»، اگر خودِ دسکتاپ اجرا نمی‌کند، پورتال هم نباید رفتاری اختراع کند که دسکتاپ ندارد.

**یافتهٔ جانبی (نه در دامنهٔ این گزارش، فقط ثبت می‌شود):** `Case.BatchExport` رویِ `FrmCase.cs:370` واقعاً چک می‌شود ولی جزوِ ۶۴ کلیدِ seed **نیست** — یعنی چون هیچ نقشی برایش grant نشده، امروز فقط SuperAdmin می‌تواند از آن استفاده کند (به‌طورِ ناخواسته، نه تصمیمِ محصولی). نیاز به تصمیمِ جداگانه دارد (seed‌شدن به‌عنوانِ کلیدِ ۶۵، یا تغییرِ کد به یک کلیدِ موجود). همچنین یک فایلِ کپیِ یتیمِ `PermissionService.cs` در ریشهٔ پروژه هست (کامپایل نمی‌شود، بی‌خطر ولی گمراه‌کننده) — پاک‌سازیِ پیشنهادی، خارج از دامنهٔ این فاز.

---

## ۲. نگاشتِ صفحاتِ پورتال ← مجوزِ لازم

هیچ صفحه‌ای امروز مجوزِ ریزدانه چک نمی‌کند — همه رویِ ۶ سیاستِ درشتِ نقشی‌اند. ستونِ «مجوزِ پیشنهادی» نگاشتِ من به نزدیک‌ترین کلیدِ دسکتاپ است.

| صفحه | سیاستِ فعلی | مجوزِ پیشنهادی | نکته |
|---|---|---|---|
| `/Index` (داشبورد) | Access | `Case.View`یا بدونِ‌مجوز (خلاصه‌یِ آماری) | — |
| `/Login`,`/Denied`,`/Error`,`/Health` | ناشناس (عمدی) | — | — |
| `/Logout` | Access | — | — |
| `/Cases/Index` | Access | `Case.View` | — |
| `/Cases/Details` | Access | `Case.View` | — |
| `/Cases/Edit` (GET/POST) | **SubmitChanges** (تنها صفحه‌ای که override داردِ اعلانی) | GET: `Case.View`؛ POST: `Case.Edit` | امروز GET و POST یک سیاست دارند |
| `/Cases/Family` (GET/POST/Delete) | Access + چکِ **امری** `CanSubmitChanges()` | GET: `Case.View`+`Representative.View`-مانند؛ POST: `Family.Edit`؛ Delete: `Family.Delete` | چکِ نوشتن در کد است، نه در Policy |
| `/Cases/Documents` (GET/Upload/Photo) | Access + چکِ امری | GET: `Docs.Print`-مانند(مشاهده)؛ Upload: `Docs.Edit`؛ عکس‌هایِ سرپرست/خانواده مستقیم می‌نویسند، بدونِ صفِ تأیید | نیاز به تصمیم: عکس‌ها همان مجوزِ Docs.Edit بگیرند یا کلیدِ جدا |
| `/Cases/Assistance` | Access + چکِ امری | GET: `Finance.View`؛ POST: `Finance.Edit` | — |
| `/Cases/Disability`,`/Cases/Migrant`,`/Cases/Orphan` | Access + چکِ امری | GET: `Case.View`؛ POST/Delete: `Family.Edit`/`Family.Delete` (این‌ها زیرمجموعهٔ ماژول‌هایِ Caseاند، کلیدِ اختصاصی ندارند رویِ دسکتاپ) | Guardian (Orphan) هم همینجا — بدونِ کلیدِ اختصاصی |
| `/Cases/Representatives` | Access + چکِ امری | GET: `Representative.View`؛ POST: `Representative.Edit`؛ Delete: `Representative.Delete` | — |
| `/Cases/Visits` | Access + چکِ امری | GET: `Case.View`؛ نوشتن: کلیدِ اختصاصی رویِ دسکتاپ نیست (فازِ آیندهٔ Visits، نه فازِ ۲/۳) | خارج از دامنهٔ فعلی |
| `/Cases/Forms`,`/Cases/Print` | Access | `Case.Print` | — |
| `/Families/Index` | Access | `Case.View` | — |
| `/Files/Content` | Access + چکِ امری `CanDownloadFiles()` | `Docs.Print`(مشاهده) — با استثنایِ Viewer که دسکتاپ هم برایِ دانلود ندارد | — |
| `/Reports/Index` (صفحه+Excel+Word) | **Access — بدونِ چکِ امری هم** | `Report.Run` (نمایش)، `Report.Export` (Excel/Word) | **شکافِ واقعی — بخشِ ۶** |
| `/Statistics/Index` | Access | `Report.Run` | — |
| `/Approvals/Index`,`/Approvals/Details` | ApproveChanges | مشاهده: نیازمندِ کلیدِ جدیدی مثلِ `Approval.View` (دسکتاپ ندارد چون گردشِ‌کارِ پورتال، نه دسکتاپ)؛ تأیید/رد: `Workflow.Approve`-مانند | امروز مشاهده و تأیید یک سیاست دارند — اپراتورِ فرستنده نمی‌تواند وضعیتِ درخواستِ خودش را ببیند |
| `/Audit/Index` | ViewAudit | `Security.View` | — |
| `/Devices/Index` (list/approve/revoke) | ManageUsers | مشاهده: `User.Manage`(خواندنی)؛ تأیید/ابطال: بدونِ معادلِ دسکتاپ (مفهومِ «دستگاه» دسکتاپ ندارد) | — |
| `/Users/Index` (create/role/center/active/reset) | ManageUsers (همه یکسان) | `User.Manage` برایِ همه — دسکتاپ هم این‌ها را زیرِ یک کلید می‌گذارد (بخشِ ۱۹) | تفکیکِ ریزتر رویِ دسکتاپ هم وجود ندارد — پس تفکیکِ پورتال یعنی سخت‌گیریِ *بیشتر* از دسکتاپ، تصمیمی که باید آگاهانه گرفته شود |
| `/Users/Activity` | ManageUsers | `Security.View` یا `User.Manage`(خواندنی) | — |

---

## ۳. نگاشتِ Endpointهایِ API ← مجوزِ لازم

| Endpoint | سیاستِ فعلی | مجوزِ پیشنهادی |
|---|---|---|
| `POST auth/login`,`POST auth/refresh`,`POST devices/register`,`GET health` | ناشناس (عمدی) | — |
| `GET auth/me` | Authorize خام | — (فقط هویتِ خودش) |
| `POST devices/{id}/approve`,`/revoke` | AdminOnly | معادلِ دسکتاپ ندارد (مفهومِ دستگاه دسکتاپی نیست) |
| `POST files/upload/*` (سه اکشن) | SyncPush | `Docs.Edit` |
| `GET files/{id}` (دانلود) | **Authorize خام — بدونِ نقش** | `Docs.Print` — **شکافِ واقعی، بخشِ ۶** |
| `GET files/manifest` | SyncPull (که در واقع فقط Authenticated است، نه نقشی) | `Docs.Print` |
| `GET files/entity/{name}/{id}` | Authorize خام | `Docs.Print` |
| `DELETE files/{id}` | AdminOnly | `Docs.Delete` |
| `GET sync/status` | Authorize خام | — (بی‌خطر) |
| `POST sync/push` | SyncPush | `Sync.Execute` |
| `GET sync/pull` | SyncPull (نقشی نیست) | `Sync.Execute` — **شکافِ واقعی، بخشِ ۶** |

---

## ۴. نگاشتِ منو/تب/دکمه/عمل روی دسکتاپ ← کلیدِ مجوز

جدولِ کاملِ ۱۱۶ نقطهٔ فراخوانی (فایل:خط + آنچه کنترل می‌کند) عیناً در ضمیمهٔ گزارشِ عامل موجود است — به‌خاطرِ حجم اینجا تکرار نمی‌شود، فقط ارجاع داده می‌شود؛ اگر نسخهٔ کاملِ جدول لازم است بگویید تا به‌عنوانِ فایلِ جداگانه ضمیمه شود. خلاصه:

- **۵۸ از ۶۴ کلید** حداقل یک نقطهٔ فراخوانیِ واقعی دارند (مستقیم یا از راهِ `WorkflowService.CanTransition`/`ModuleService.RequiredPermission`).
- سیستمِ ماژول (۲۵ ماژولِ نمایشِ منو، `ModuleService.cs`) کاملاً **جدا** از این ۶۴ کلید است — فقط ۷ ماژول یک `RequiredPermission` دارند (بقیه فقط روشن/خاموش، بدونِ کلیدِ مجوز). این سیستم رویِ پورتال معادلی ندارد و ساختنش خارج از دامنهٔ «پورتال باید با دسکتاپ برابر شود» است مگر تصمیمِ جداگانه بگیرید.

---

## ۵. مجوزهایِ دسکتاپ بدونِ معادلِ پورتالی

### الف) چون خودِ ویژگی رویِ پورتال اصلاً وجود ندارد (نه شکاف، بلکه خارج از دامنهٔ فازِ ۲+۳)
`Workflow.*`, `Approval.Manage`, `Task.*`, `Rule.Manage`, `Lock.Override`, `Security.View`(به‌جزِ معادلِ Audit)، `Error.View`, `Version.View`, `Module.Manage`, `Center.Manage`, `Backup.*`, `Accounting.*`, `Archive.*`, `Barcode.Print`, `GuardianCard.*`, `AI.*`, `Applicant.*`, `CaseRelation.*`. پورتال هیچ صفحه/endpointی برایِ این‌ها ندارد — ساختنِ مجوزِ ریزدانه برایِ چیزی که وجود ندارد بی‌معناست.

### ب) چون ویژگی رویِ پورتال هست ولی امروز هیچ چک ندارد (شکافِ واقعی — اولویتِ گامِ ۲)
`Case.Edit/View/Print`, `Family.Edit/Delete/Print`, `Docs.Edit/Delete/Print`, `Representative.View/Edit/Delete/Print`, `Finance.View/Edit`, `Report.Run/Export`, `User.Manage`, `Security.View`(معادلِ Audit), `Sync.Execute`.

---

## ۶. عملکردهایِ پورتالی که امروز از مجوز عبور می‌کنند

۱. **`Reports/Index.cshtml.cs`** — خروجیِ کاملِ Excel/Word را با هیچ چیزی فراتر از سیاستِ همگانیِ `Access` می‌دهد؛ حتی چکِ امریِ داخلِ‌کد هم ندارد (برخلافِ صفحاتِ Cases). **یک Viewer می‌تواند کلِ دیتابیس را خروجی بگیرد.**
۲. **`FilesController.GetById`/`ListForEntity`** (API) — فقط `[Authorize]` خام، بدونِ نقش؛ برخلافِ همتایِ پورتالی‌اش (`Files/Content.cshtml.cs`) که Viewer را با `CanDownloadFiles()` صریحاً کنار می‌گذارد. **همان داده، دو سطحِ دسترسیِ متفاوت بسته به کلاینت.**
۳. **سیاستِ `SyncPull`** (`API\Program.cs:83`) در واقع فقط `RequireAuthenticatedUser()` است، نه نقشی — با اینکه اسمش کنارِ `SyncPush`/`AdminOnly` نقشی به‌نظر می‌رسد. `sync/pull` و `files/manifest` را هر نقشی (حتی Viewer) می‌تواند بزند.
۴. **نوشتن‌هایِ صفحاتِ Cases/*** (Family, Documents, Assistance, Disability, Migrant, Orphan, Representatives, Visits) — همه فقط با یک چکِ **امریِ** `CanSubmitChanges()` داخلِ کدِ handler محافظت می‌شوند، نه یک Policyِ اعلانی. اگر کسی یک handlerِ تازه اضافه کند و این چک را فراموش کند، بی‌صدا فقط به سیاستِ ضعیفِ `Access` برمی‌گردد.
۵. **`/Approvals`**، **`/Devices/Index`**، **`/Users/Index`** — مشاهده و نوشتن (حتی نوشتن‌هایِ حساس مثلِ تغییرِ نقش/رمز) زیرِ یک سیاستِ واحدند؛ اپراتورِ فرستنده حتی نمی‌تواند وضعیتِ درخواستِ خودش را در `/Approvals` ببیند.

---

## ۷. ترتیبِ پیشنهادیِ اجرا

۱. **بستنِ شکاف‌هایِ فوریِ بندِ ۶** (کم‌هزینه، همین حالا قابلِ انجام، حتی پیش از موتورِ کاملِ مجوز): تصحیحِ `Policies.SyncPull` به نقشی، افزودنِ چکِ نقش به `FilesController.GetById`/`ListForEntity` هم‌تراز با نسخهٔ پورتال، افزودنِ حداقل یک چکِ نقشِ ابتدایی به `Reports/Index`.
۲. **موتورِ Authorization** (خودِ گامِ ۲): `RequiresPermission` بر پایهٔ `role_permissions`+`user_permissions` تازه‌ساخته، با همان اولویتِ دسکتاپ.
۳. **صفحاتِ پرریسک/مدیریتی اول**: `Users/Index` (تفکیکِ حداقلی: مشاهده در برابرِ نوشتن)، `Devices/Index`، `Approvals` (تفکیکِ مشاهده از تأیید).
۴. **صفحاتِ Case-محور**: `Cases/Edit`, `Family`, `Documents`, `Representatives` — تبدیلِ چکِ امریِ موجود به Policyِ اعلانی، بدونِ تغییرِ رفتار.
۵. **گزارش/خروجی**: `Reports/Index`, `Statistics/Index`, `Files/Content`+API معادلش.
۶. **ثبتِ رخدادِ امنیتیِ تایپ‌شده** (کنارِ کار، نه بعدش) — هر Denyِ تازه باید از روزِ اول ممیزی شود.
۷. آنچه در بندِ ۵-الف آمد را **دست‌نخورده** می‌گذاریم — نه رفعِ شکاف، بلکه خارج از دامنه.

منتظرِ تأییدِ شما برایِ شروعِ گامِ ۲.
