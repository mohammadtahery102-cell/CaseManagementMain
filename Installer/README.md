# Guardian — ساخت نصب‌کننده (Installer)

نصب‌کننده‌ی حرفه‌ای با **Inno Setup 6** ساخته می‌شود.

## پیش‌نیاز
- [Inno Setup 6](https://jrsoftware.org/isdl.php) را نصب کنید.

## مراحل ساخت
1. نسخه‌ی **Release / x64** را بسازید:
   ```
   MSBuild CaseManagement.csproj /t:Rebuild /p:Configuration=Release /p:Platform=x64
   ```
   خروجی در `bin\x64\Release\` قرار می‌گیرد (شامل `CaseManagement.exe` و همه‌ی وابستگی‌ها و native‌های SQLite/WebView2).

2. اسکریپت نصب را کامپایل کنید:
   ```
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\Guardian.iss
   ```

3. خروجی نهایی: `Installer\Output\GuardianSetup-1.1.0.exe`

## قابلیت‌های نصب‌کننده
| مورد | وضعیت |
|------|-------|
| مسیر نصب قابل‌تغییر (`Program Files\Guardian`) | ✅ |
| میان‌بر Start Menu | ✅ |
| میان‌بر دسکتاپ (اختیاری) | ✅ |
| حذف نصب (Uninstall) + آیکون در «افزودن/حذف برنامه‌ها» | ✅ |
| نسخه، شرکت، Publisher در متادیتای نصب | ✅ |
| فقط ویندوز ۶۴بیتی (به‌خاطر SQLite/WebView2 native) | ✅ |
| اجرای برنامه پس از نصب | ✅ |
| ساخت خودکار دیتابیس در اولین اجرا (bundle نمی‌شود) | ✅ |

## قبل از انتشار (TODO)
در `Guardian.iss` این‌ها را با مقادیر واقعی جایگزین کنید:
- `#define AppPublisher` — نام واقعی شرکت/مؤسسه
- `#define AppURL` — وب‌سایت واقعی
- (اختیاری) فعال‌کردن زبان فارسی نصب‌کننده در بخش `[Languages]` (نیازمند فایل `Persian.isl`).
- (اختیاری) افزودن `SetupIconFile` با یک فایل `.ico` واقعی برای آیکون خود نصب‌کننده.

> توجه: `AppId` (GUID) هرگز نباید بین نسخه‌ها تغییر کند — مبنای شناسایی برای ارتقا و حذف است.

## رفع خطای «Unable to load DLL 'SQLite.Interop.dll'» بعد از نصب

این خطا یعنی DLL بومیِ SQLite (بدون امضای دیجیتال) لود نمی‌شود. علت تقریباً همیشه یکی از این دو مورد است:

1. **Visual C++ Redistributable (x64) 2015-2022 روی دستگاه نصب نیست.**
   نصب‌کننده حالا خودش قبل از شروع این را چک می‌کند و اگر نبود هشدار می‌دهد
   (نگاه کنید به `[Code]` در `Guardian.iss`). لینک رسمی مایکروسافت برای نصب دستی:
   `https://aka.ms/vs/17/release/vc_redist.x64.exe`

2. **آنتی‌ویروس/Windows Defender این DLL را داخل Program Files قرنطینه/حذف می‌کند**
   (الگوی شناخته‌شده برای DLLهای بومیِ بدون امضا — دقیقاً همان چیزی که باعث
   می‌شود «داخل Program Files کار نکند ولی بیرون از آن اجرا شود»). چک کنید:
   Windows Security → Protection history — اگر موردی حذف/قرنطینه شده، پوشه‌ی
   نصب را به استثناهای آنتی‌ویروس اضافه کرده و دوباره نصب کنید.

**رفع دائمی و حرفه‌ای:** امضای دیجیتال (Code Signing Certificate) روی خود
نصب‌کننده و/یا `CaseManagement.exe` — نصب‌کننده‌های بدون امضا هم SmartScreen
هشدار می‌دهند و هم بیشتر توسط آنتی‌ویروس‌ها با شک بررسی می‌شوند. برای تحویل
واقعی به مشتری، تهیه‌ی یک گواهی امضای کد (از مراجعی مثل DigiCert/Sectigo)
قویاً توصیه می‌شود.
