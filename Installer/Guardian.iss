; ═══════════════════════════════════════════════════════════════════════════
;  Guardian Orphan Management System — Inno Setup installer script
;  نصب‌کننده‌ی حرفه‌ای: Shortcut، Uninstall، Version، Publisher، مسیر نصب، آیکون.
;
;  ساخت خروجی نصب:
;    ۱. Inno Setup 6 را نصب کنید:  https://jrsoftware.org/isdl.php
;    ۲. ابتدا نسخه‌ی Release را بسازید (x64):
;         MSBuild CaseManagement.csproj /p:Configuration=Release /p:Platform=x64
;    ۳. این فایل را با ISCC کامپایل کنید:
;         "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\Guardian.iss
;    خروجی در Installer\Output\GuardianSetup-<version>.exe ساخته می‌شود.
;
;  ★ قبل از انتشار، مقادیر Publisher/URL را (پایین) با اطلاعات واقعی جایگزین کنید.
; ═══════════════════════════════════════════════════════════════════════════

#define AppName        "Guardian Orphan Management System"
#define AppNameFa      "سیستم مدیریت پرونده‌های ایتام"
#define AppVersion     "1.1.0"
#define AppPublisher   "Guardian"          ; TODO: نام واقعی شرکت/مؤسسه
#define AppURL         "https://example.org" ; TODO: وب‌سایت واقعی
#define AppExeName     "CaseManagement.exe"
#define SourceDir      "..\bin\x64\Release"

[Setup]
; AppId یکتا و ثابت — هرگز بین نسخه‌ها تغییر نکند (مبنای شناسایی نصب برای ارتقا/حذف).
AppId={{6F3B9A2C-7C41-4E28-9D5B-A1B2C3D4E5F6}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}

; مسیر نصب پیش‌فرض: Program Files (کاربر می‌تواند تغییر دهد).
DefaultDirName={autopf}\Guardian
DefaultGroupName=Guardian
DisableProgramGroupPage=yes
AllowNoIcons=yes

; فقط روی ویندوز ۶۴بیتی (SQLite/WebView2 native x64 هستند).
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1sp1

; نصب برای همه‌ی کاربران نیاز به دسترسی مدیر دارد.
PrivilegesRequired=admin

OutputDir=Output
OutputBaseFilename=GuardianSetup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; آیکون نمایش داده‌شده در «افزودن/حذف برنامه‌ها» = آیکون خود برنامه.
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

; رابط فارسی نصب‌کننده (در صورت وجود فایل زبان فارسی؛ در غیر این صورت انگلیسی).
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
; Name: "fa"; MessagesFile: "compiler:Languages\Persian.isl"  ; در صورت نصب زبان فارسی فعال کنید

[Tasks]
Name: "desktopicon"; Description: "ایجاد میان‌بر روی دسکتاپ"; GroupDescription: "میان‌برها:"; Flags: checkedonce

[Files]
; کل خروجی Release به‌صورت بازگشتی (شامل زیرپوشه‌های native x64/runtimes/Fonts).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
; آموزش — دیتابیس عمداً bundle نمی‌شود: برنامه در اولین اجرا خودش CaseDB.sqlite را
; در |DataDirectory| می‌سازد و کاربر پیش‌فرض admin (رمز موقت) را ایجاد می‌کند.

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\حذف {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Guardian"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; پیشنهاد اجرای برنامه پس از نصب.
Filename: "{app}\{#AppExeName}"; Description: "اجرای {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; هنگام حذف، لاگ‌های تولیدشده‌ی کنار برنامه پاک شوند (نه دیتابیس/داده‌های کاربر).
Type: files; Name: "{app}\audit_errors.log"
