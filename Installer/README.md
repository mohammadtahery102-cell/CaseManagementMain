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
