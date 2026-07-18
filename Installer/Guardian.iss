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

[Code]
// ─────────────────────────────────────────────────────────────────────────
// آموزش — رفع خطای «Unable to load DLL 'SQLite.Interop.dll'» بعد از نصب:
// این DLL بومی (native, بدون امضای دیجیتال) برای اجرا به Visual C++
// Redistributable x64 نیاز دارد. روی دستگاه توسعه (که Visual Studio نصب است)
// این runtime از قبل حاضر است، پس مستقیماً از پوشه‌ی build اجرا می‌شود؛ اما
// روی دستگاه مشتری/کاربر نهایی (بدون Visual Studio) ممکن است نصب نباشد —
// دقیقاً همین خطا را می‌دهد. این تابع وجودش را در رجیستری چک می‌کند و اگر
// نبود، قبل از ادامه‌ی نصب به کاربر هشدار روشن می‌دهد (نه فقط شکست خاموش).
function VCRedistX64Installed(): Boolean;
var
  Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64', 'Installed', Installed)
    and (Installed = 1);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not VCRedistX64Installed() then
  begin
    if MsgBox(
      'به نظر می‌رسد «Microsoft Visual C++ Redistributable (x64) 2015-2022» روی این دستگاه نصب نیست.' + #13#10 +
      'بدون آن، نرم‌افزار هنگام اجرا خطای زیر می‌دهد:' + #13#10 +
      '  Unable to load DLL ''SQLite.Interop.dll''' + #13#10#13#10 +
      'پیشنهاد می‌شود ابتدا آن را از آدرس رسمی مایکروسافت نصب کنید:' + #13#10 +
      '  https://aka.ms/vs/17/release/vc_redist.x64.exe' + #13#10#13#10 +
      'آیا می‌خواهید بدون نصب آن ادامه دهید؟ (در این صورت ممکن است برنامه اجرا نشود)',
      mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;

// آموزش — رفع خطای «کار می‌کند اما نه از داخل Program Files»: دلیل شایع این
// الگو، مسدودسازیِ خودکارِ DLLهای بومیِ بدون امضا توسط آنتی‌ویروس/Windows
// Defender هنگام نصب در Program Files است (این نصب‌کننده امضای دیجیتال
// ندارد). این پیام بعد از اتمام نصب راهنمایی می‌دهد تا کاربر متوجه علت
// احتمالی شود و لازم نباشد دوباره از ابتدا حدس بزند.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox(
      'نصب کامل شد.' + #13#10#13#10 +
      'اگر هنگام اجرا خطای «Unable to load DLL SQLite.Interop.dll» دیدید:' + #13#10 +
      '۱) مطمئن شوید Visual C++ Redistributable (x64) نصب است (پیام بالا).' + #13#10 +
      '۲) در Windows Security > Protection history بررسی کنید آیا فایلی از پوشه‌ی' + #13#10 +
      '   نصب (مخصوصاً SQLite.Interop.dll) قرنطینه/حذف شده؛ اگر بله، پوشه‌ی نصب را' + #13#10 +
      '   به لیست استثناهای آنتی‌ویروس اضافه کرده و دوباره نصب کنید.',
      mbInformation, MB_OK);
  end;
end;

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
